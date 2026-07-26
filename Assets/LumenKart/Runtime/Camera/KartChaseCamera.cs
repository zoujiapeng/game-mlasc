using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class KartChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private KartController controller;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float baseDistance = 6.4f;
        [SerializeField] private float highSpeedDistance = 7.35f;
        [SerializeField] private float baseHeight = 3.05f;
        [SerializeField] private float lookAhead = 3.25f;
        [SerializeField] private float positionSmoothTime = 0.13f;
        [SerializeField] private float rotationSharpness = 10f;
        [SerializeField] private float collisionRadius = 0.28f;

        private Camera cameraComponent;
        private Vector3 positionVelocity;
        private Vector3 smoothedForward;
        private float shakeTimer;
        private float shakeStrength;

        private void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            smoothedForward = target != null ? target.forward : transform.forward;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void LateUpdate()
        {
            if (target == null || controller == null || cameraComponent == null)
            {
                return;
            }

            float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float speed = controller.NormalizedSpeed;
            smoothedForward = Vector3.Slerp(
                smoothedForward,
                Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized,
                1f - Mathf.Exp(-deltaTime * 7.5f));
            if (smoothedForward.sqrMagnitude < 0.001f)
            {
                smoothedForward = target.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, smoothedForward).normalized;
            float distance = Mathf.Lerp(baseDistance, highSpeedDistance, speed);
            float driftOffset = controller.IsDrifting
                ? -controller.DriftDirection * Mathf.Lerp(0.15f, 0.52f, speed)
                : -controller.CurrentSteering * 0.14f;
            Vector3 lookPoint = target.position + Vector3.up * 1.05f + smoothedForward * (lookAhead + speed * 1.2f);
            Vector3 desiredPosition =
                target.position +
                Vector3.up * (baseHeight + speed * 0.18f) -
                smoothedForward * distance +
                right * driftOffset;

            desiredPosition = ResolveCameraCollision(lookPoint, desiredPosition);
            desiredPosition += CalculateShake(deltaTime);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime,
                Mathf.Infinity,
                deltaTime);

            Vector3 viewDirection = lookPoint - transform.position;
            if (viewDirection.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(viewDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-deltaTime * rotationSharpness));
            }

            float targetFov = Mathf.Lerp(55f, 69f, speed);
            if (controller.IsBoosting)
            {
                targetFov += 4.5f;
            }

            cameraComponent.fieldOfView = Mathf.Lerp(
                cameraComponent.fieldOfView,
                targetFov,
                1f - Mathf.Exp(-deltaTime * 5.5f));
        }

        public void Configure(Transform followTarget, KartController kartController, LayerMask environmentMask)
        {
            Unsubscribe();
            target = followTarget;
            controller = kartController;
            collisionMask = environmentMask;
            smoothedForward = target != null ? target.forward : transform.forward;
            Subscribe();

            if (target != null)
            {
                transform.position = target.position + Vector3.up * baseHeight - target.forward * baseDistance;
                transform.LookAt(target.position + Vector3.up + target.forward * lookAhead);
            }
        }

        public void AddImpulse(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeTimer = Mathf.Max(shakeTimer, duration);
        }

        private Vector3 ResolveCameraCollision(Vector3 lookPoint, Vector3 desiredPosition)
        {
            Vector3 direction = desiredPosition - lookPoint;
            float distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                return desiredPosition;
            }

            if (Physics.SphereCast(
                    lookPoint,
                    collisionRadius,
                    direction.normalized,
                    out RaycastHit hit,
                    distance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point - direction.normalized * collisionRadius + Vector3.up * 0.08f;
            }

            return desiredPosition;
        }

        private Vector3 CalculateShake(float deltaTime)
        {
            if (shakeTimer <= 0f)
            {
                shakeStrength = 0f;
                return Vector3.zero;
            }

            shakeTimer = Mathf.Max(0f, shakeTimer - deltaTime);
            float envelope = Mathf.Clamp01(shakeTimer * 5f);
            return Random.insideUnitSphere * (shakeStrength * envelope);
        }

        private void Subscribe()
        {
            if (controller == null)
            {
                return;
            }

            controller.BoostStarted -= HandleBoost;
            controller.SpinOutStarted -= HandleSpin;
            controller.BoostStarted += HandleBoost;
            controller.SpinOutStarted += HandleSpin;
        }

        private void Unsubscribe()
        {
            if (controller == null)
            {
                return;
            }

            controller.BoostStarted -= HandleBoost;
            controller.SpinOutStarted -= HandleSpin;
        }

        private void HandleBoost(float duration)
        {
            AddImpulse(0.045f, Mathf.Min(0.45f, duration));
        }

        private void HandleSpin()
        {
            AddImpulse(0.12f, 0.42f);
        }
    }
}
