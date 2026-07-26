using System;
using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KartController : MonoBehaviour
    {
        [SerializeField] private KartTuning tuning;
        [SerializeField] private KartInputSource inputSource;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private bool controlsEnabled = true;

        private Rigidbody body;
        private KartInputFrame currentInput;
        private RaycastHit groundHit;
        private bool grounded;
        private bool previousDriftHeld;
        private bool drifting;
        private int driftDirection;
        private float driftCharge;
        private float driftAirGrace;
        private float boostTimer;
        private float activeBoostSpeed;
        private float spinOutTimer;
        private float spinDirection = 1f;
        private float upsideTimer;
        private float externalSpeedMultiplier = 1f;
        private KartItemInventory inventory;
        private RaceProgress raceProgress;

        public event Action<float> BoostStarted;
        public event Action SpinOutStarted;

        public Rigidbody Body => body;
        public KartTuning Tuning => tuning;
        public bool ControlsEnabled => controlsEnabled;
        public bool IsGrounded => grounded;
        public bool IsDrifting => drifting;
        public bool IsBoosting => boostTimer > 0f;
        public bool IsSpinningOut => spinOutTimer > 0f;
        public bool IsHuman => inputSource != null && inputSource.IsHuman;
        public int DriftDirection => driftDirection;
        public float DriftCharge => driftCharge;
        public float CurrentSteering => currentInput.Steering;
        public float ForwardSpeed { get; private set; }
        public float PlanarSpeed { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public KartInputFrame CurrentInput => currentInput;

        public float NormalizedSpeed
        {
            get
            {
                float denominator = Mathf.Max(1f, tuning != null ? tuning.maxForwardSpeed : 24f);
                return Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / denominator);
            }
        }

        public float DriftCharge01
        {
            get
            {
                if (tuning == null || tuning.tierThreeCharge <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(driftCharge / tuning.tierThreeCharge);
            }
        }

        public int DriftTier
        {
            get
            {
                if (tuning == null)
                {
                    return 0;
                }

                if (driftCharge >= tuning.tierThreeCharge)
                {
                    return 3;
                }

                if (driftCharge >= tuning.tierTwoCharge)
                {
                    return 2;
                }

                if (driftCharge >= tuning.tierOneCharge)
                {
                    return 1;
                }

                return 0;
            }
        }

        public float ExternalSpeedMultiplier
        {
            get => externalSpeedMultiplier;
            set => externalSpeedMultiplier = Mathf.Clamp(value, 0.85f, 1.15f);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            inventory = GetComponent<KartItemInventory>();
            raceProgress = GetComponent<RaceProgress>();

            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 8f;
            body.linearDamping = 0.05f;
            body.angularDamping = tuning != null ? tuning.angularDamping : 4f;
            body.centerOfMass = new Vector3(0f, -0.28f, 0.02f);
        }

        private void Update()
        {
            currentInput = controlsEnabled && inputSource != null && spinOutTimer <= 0f
                ? inputSource.ReadInput()
                : KartInputFrame.Neutral;

            if (currentInput.PausePressed && RaceManager.Instance != null)
            {
                RaceManager.Instance.TogglePause();
            }

            if (currentInput.ItemPressed && inventory != null)
            {
                inventory.UseCurrentItem();
            }

            UpdateDriftState();
            UpdateRecoveryState();
        }

        private void FixedUpdate()
        {
            if (tuning == null || body == null)
            {
                return;
            }

            ProbeGround();
            UpdateMeasuredSpeed();

            if (boostTimer > 0f)
            {
                boostTimer = Mathf.Max(0f, boostTimer - Time.fixedDeltaTime);
                if (boostTimer <= 0f)
                {
                    activeBoostSpeed = 0f;
                }
            }

            if (spinOutTimer > 0f)
            {
                spinOutTimer = Mathf.Max(0f, spinOutTimer - Time.fixedDeltaTime);
            }

            ApplySuspensionAndGravity();
            ApplyGroundAlignment();
            ApplyDriveForces();
            ApplySteeringAndGrip();
            ApplySpeedLimit();
            ClampAngularVelocity();
        }

        public void Configure(KartTuning kartTuning, KartInputSource source, LayerMask roadMask)
        {
            tuning = kartTuning;
            inputSource = source;
            groundMask = roadMask;

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body != null && tuning != null)
            {
                body.angularDamping = tuning.angularDamping;
            }
        }

        public void SetInputSource(KartInputSource source)
        {
            inputSource = source;
        }

        public void SetControlsEnabled(bool enabled)
        {
            controlsEnabled = enabled;
            if (!enabled)
            {
                currentInput = KartInputFrame.Neutral;
                CancelDrift(false);
            }
        }

        public void ApplyBoost(float duration, float targetSpeed = -1f)
        {
            if (tuning == null || duration <= 0f)
            {
                return;
            }

            boostTimer = Mathf.Max(boostTimer, duration);
            activeBoostSpeed = Mathf.Max(
                activeBoostSpeed,
                targetSpeed > 0f ? targetSpeed : tuning.defaultBoostSpeed);
            BoostStarted?.Invoke(duration);
        }

        public void SpinOut(float duration = -1f)
        {
            if (tuning == null)
            {
                return;
            }

            CancelDrift(false);
            boostTimer *= 0.25f;
            spinOutTimer = Mathf.Max(
                spinOutTimer,
                duration > 0f ? duration : tuning.spinOutDuration);
            spinDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            body.AddTorque(GroundNormal * (tuning.spinOutTorque * spinDirection), ForceMode.VelocityChange);
            body.AddForce(Vector3.up * 1.25f, ForceMode.VelocityChange);
            SpinOutStarted?.Invoke();
        }

        public void RespawnAt(Vector3 position, Quaternion rotation)
        {
            CancelDrift(false);
            boostTimer = 0f;
            activeBoostSpeed = 0f;
            spinOutTimer = 0f;
            upsideTimer = 0f;

            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private void UpdateDriftState()
        {
            if (driftAirGrace > 0f)
            {
                driftAirGrace -= Time.deltaTime;
            }

            bool pressedThisFrame = currentInput.DriftHeld && !previousDriftHeld;
            bool releasedThisFrame = !currentInput.DriftHeld && previousDriftHeld;

            if (pressedThisFrame && CanStartDrift())
            {
                drifting = true;
                driftDirection = currentInput.Steering < 0f ? -1 : 1;
                driftCharge = 0f;
                driftAirGrace = 0.32f;
                body.AddForce(transform.up * tuning.driftHopImpulse, ForceMode.VelocityChange);
            }

            bool lostDriftConditions =
                drifting &&
                ((Mathf.Abs(ForwardSpeed) < tuning.minimumDriftSpeed * 0.65f) ||
                 (!grounded && driftAirGrace <= 0f) ||
                 spinOutTimer > 0f);

            if (drifting && (releasedThisFrame || lostDriftConditions))
            {
                CancelDrift(releasedThisFrame && grounded);
            }

            previousDriftHeld = currentInput.DriftHeld;
        }

        private bool CanStartDrift()
        {
            return tuning != null &&
                   controlsEnabled &&
                   grounded &&
                   spinOutTimer <= 0f &&
                   Mathf.Abs(ForwardSpeed) >= tuning.minimumDriftSpeed &&
                   Mathf.Abs(currentInput.Steering) >= 0.2f;
        }

        private void CancelDrift(bool awardMiniTurbo)
        {
            if (!drifting)
            {
                driftCharge = 0f;
                driftDirection = 0;
                return;
            }

            int tier = DriftTier;
            if (awardMiniTurbo && tuning != null)
            {
                switch (tier)
                {
                    case 1:
                        ApplyBoost(tuning.tierOneDuration, tuning.defaultBoostSpeed);
                        break;
                    case 2:
                        ApplyBoost(tuning.tierTwoDuration, tuning.defaultBoostSpeed + 1.5f);
                        break;
                    case 3:
                        ApplyBoost(tuning.tierThreeDuration, tuning.defaultBoostSpeed + 3f);
                        break;
                }
            }

            drifting = false;
            driftCharge = 0f;
            driftDirection = 0;
        }

        private void ProbeGround()
        {
            Vector3 origin = body.worldCenterOfMass + transform.up * 0.35f;
            grounded = Physics.SphereCast(
                origin,
                tuning.probeRadius,
                -transform.up,
                out groundHit,
                tuning.probeDistance,
                groundMask,
                QueryTriggerInteraction.Ignore);

            GroundNormal = grounded ? groundHit.normal.normalized : Vector3.up;
        }

        private void UpdateMeasuredSpeed()
        {
            Vector3 planeNormal = grounded ? GroundNormal : Vector3.up;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, planeNormal);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, planeNormal).normalized;
            PlanarSpeed = planarVelocity.magnitude;
            ForwardSpeed = Vector3.Dot(planarVelocity, forward);
        }

        private void ApplySuspensionAndGravity()
        {
            if (grounded)
            {
                float compression = tuning.rideHeight - groundHit.distance;
                float normalVelocity = Vector3.Dot(body.GetPointVelocity(groundHit.point), GroundNormal);
                float springAcceleration =
                    compression * tuning.suspensionStrength -
                    normalVelocity * tuning.suspensionDamping;
                body.AddForce(GroundNormal * springAcceleration, ForceMode.Acceleration);
                body.AddForce(-GroundNormal * tuning.customGravity, ForceMode.Acceleration);
            }
            else
            {
                body.AddForce(Vector3.down * tuning.customGravity, ForceMode.Acceleration);
            }
        }

        private void ApplyGroundAlignment()
        {
            if (!grounded)
            {
                return;
            }

            Vector3 alignmentAxis = Vector3.Cross(transform.up, GroundNormal);
            Vector3 pitchRollVelocity = Vector3.ProjectOnPlane(body.angularVelocity, GroundNormal);
            body.AddTorque(
                alignmentAxis * tuning.groundAlignment - pitchRollVelocity * 2.2f,
                ForceMode.Acceleration);
        }

        private void ApplyDriveForces()
        {
            Vector3 planeNormal = grounded ? GroundNormal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, planeNormal).normalized;

            if (spinOutTimer > 0f)
            {
                body.AddTorque(
                    planeNormal * (spinDirection * tuning.spinOutTorque * 0.55f),
                    ForceMode.Acceleration);
                body.AddForce(-forward * Mathf.Max(0f, ForwardSpeed) * 2f, ForceMode.Acceleration);
                return;
            }

            float maxSpeed = tuning.maxForwardSpeed * externalSpeedMultiplier;
            if (boostTimer > 0f)
            {
                maxSpeed = Mathf.Max(maxSpeed, activeBoostSpeed * externalSpeedMultiplier);
                body.AddForce(forward * tuning.boostAcceleration, ForceMode.Acceleration);
            }

            if (currentInput.Throttle > 0f)
            {
                if (ForwardSpeed < -0.5f)
                {
                    body.AddForce(forward * tuning.brakePower * currentInput.Throttle, ForceMode.Acceleration);
                }
                else if (ForwardSpeed < maxSpeed)
                {
                    float speedFactor = 1f - Mathf.Clamp01(ForwardSpeed / Mathf.Max(1f, maxSpeed)) * 0.38f;
                    body.AddForce(
                        forward * (tuning.acceleration * speedFactor * currentInput.Throttle),
                        ForceMode.Acceleration);
                }
            }

            if (currentInput.Brake > 0f)
            {
                if (ForwardSpeed > 0.75f)
                {
                    body.AddForce(-forward * tuning.brakePower * currentInput.Brake, ForceMode.Acceleration);
                }
                else if (ForwardSpeed > -tuning.maxReverseSpeed)
                {
                    body.AddForce(-forward * tuning.reverseAcceleration * currentInput.Brake, ForceMode.Acceleration);
                }
            }

            if (currentInput.Throttle < 0.05f && currentInput.Brake < 0.05f)
            {
                Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, planeNormal);
                body.AddForce(-planarVelocity * tuning.coastDrag, ForceMode.Acceleration);
            }
        }

        private void ApplySteeringAndGrip()
        {
            Vector3 planeNormal = grounded ? GroundNormal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, planeNormal).normalized;
            Vector3 right = Vector3.Cross(planeNormal, forward).normalized;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, planeNormal);
            float lateralSpeed = Vector3.Dot(planarVelocity, right);

            if (grounded)
            {
                float speedRatio = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / tuning.maxForwardSpeed);
                float steeringAuthority = Mathf.Lerp(1f, tuning.steeringAtTopSpeed, speedRatio);
                float directionFactor = ForwardSpeed < -0.5f ? -1f : 1f;
                float steering = currentInput.Steering;
                float grip = tuning.lateralGrip;

                if (drifting)
                {
                    float counterSteer = steering * driftDirection < 0f ? 0.58f : 1f;
                    float driftSteering = driftDirection * 0.72f + steering * 0.58f * counterSteer;
                    steering = driftSteering * tuning.driftSteeringMultiplier;
                    grip = tuning.driftLateralGrip;

                    float chargeInput = 0.55f + Mathf.Abs(currentInput.Steering) * 0.4f + speedRatio * 0.35f;
                    driftCharge += Time.fixedDeltaTime * tuning.driftChargeRate * chargeInput;
                }

                if (boostTimer > 0f)
                {
                    steering *= tuning.boostSteeringMultiplier;
                }

                body.AddTorque(
                    planeNormal *
                    (steering * tuning.steeringAcceleration * steeringAuthority * directionFactor),
                    ForceMode.Acceleration);
                body.AddForce(-right * lateralSpeed * grip, ForceMode.Acceleration);
            }
            else
            {
                body.AddTorque(
                    Vector3.up * (currentInput.Steering * tuning.airSteering),
                    ForceMode.Acceleration);
            }
        }

        private void ApplySpeedLimit()
        {
            Vector3 planeNormal = grounded ? GroundNormal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, planeNormal).normalized;
            float limit = tuning.maxForwardSpeed * externalSpeedMultiplier;
            if (boostTimer > 0f)
            {
                limit = Mathf.Max(limit, activeBoostSpeed * externalSpeedMultiplier);
            }

            if (ForwardSpeed > limit)
            {
                body.AddForce(-forward * (ForwardSpeed - limit) * 8f, ForceMode.Acceleration);
            }
            else if (ForwardSpeed < -tuning.maxReverseSpeed)
            {
                body.AddForce(forward * (-tuning.maxReverseSpeed - ForwardSpeed) * 8f, ForceMode.Acceleration);
            }
        }

        private void ClampAngularVelocity()
        {
            float maxAngularSpeed = drifting ? 6.5f : 4.5f;
            if (body.angularVelocity.magnitude > maxAngularSpeed)
            {
                body.angularVelocity = body.angularVelocity.normalized * maxAngularSpeed;
            }
        }

        private void UpdateRecoveryState()
        {
            if (raceProgress == null || tuning == null || body == null)
            {
                return;
            }

            bool clearlyUpsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.15f;
            bool offWorld = transform.position.y < -8f;

            if (clearlyUpsideDown && PlanarSpeed < 3f)
            {
                upsideTimer += Time.deltaTime;
            }
            else
            {
                upsideTimer = Mathf.Max(0f, upsideTimer - Time.deltaTime * 2f);
            }

            if (offWorld || upsideTimer >= tuning.autoRespawnDelay)
            {
                raceProgress.RespawnToLastCheckpoint();
                upsideTimer = 0f;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (body == null || collision.contactCount == 0)
            {
                return;
            }

            float impact = collision.relativeVelocity.magnitude;
            if (impact < 7f)
            {
                return;
            }

            Vector3 normal = collision.GetContact(0).normal;
            body.AddForce(normal * Mathf.Min(3f, impact * 0.12f), ForceMode.VelocityChange);
            boostTimer *= 0.65f;
        }
    }
}
