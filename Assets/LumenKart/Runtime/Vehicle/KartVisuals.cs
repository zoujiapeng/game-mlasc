using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    public sealed class KartVisuals : MonoBehaviour
    {
        [SerializeField] private KartController controller;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform[] wheelMeshes;
        [SerializeField] private Transform[] frontSteerPivots;
        [SerializeField] private ParticleSystem[] exhaustParticles;
        [SerializeField] private ParticleSystem[] driftParticles;
        [SerializeField] private ParticleSystem boostParticles;
        [SerializeField] private GameObject shieldVisual;

        private Quaternion bodyBaseRotation;
        private Quaternion[] wheelBaseRotations;
        private Quaternion[] steerBaseRotations;
        private float wheelSpin;
        private bool shieldVisible;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<KartController>();
            }

            if (bodyRoot != null)
            {
                bodyBaseRotation = bodyRoot.localRotation;
            }

            wheelBaseRotations = CaptureRotations(wheelMeshes);
            steerBaseRotations = CaptureRotations(frontSteerPivots);
            SetShieldVisible(false);
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            wheelSpin += controller.ForwardSpeed * 155f * deltaTime;

            AnimateBody(deltaTime);
            AnimateWheels();
            UpdateParticles();
            AnimateShield();
        }

        public void Configure(
            KartController kartController,
            Transform visualBodyRoot,
            Transform[] wheels,
            Transform[] frontPivots,
            ParticleSystem[] exhaust,
            ParticleSystem[] drift,
            ParticleSystem boost,
            GameObject shield)
        {
            controller = kartController;
            bodyRoot = visualBodyRoot;
            wheelMeshes = wheels;
            frontSteerPivots = frontPivots;
            exhaustParticles = exhaust;
            driftParticles = drift;
            boostParticles = boost;
            shieldVisual = shield;

            bodyBaseRotation = bodyRoot != null ? bodyRoot.localRotation : Quaternion.identity;
            wheelBaseRotations = CaptureRotations(wheelMeshes);
            steerBaseRotations = CaptureRotations(frontSteerPivots);
            SetShieldVisible(false);
        }

        public void SetShieldVisible(bool visible)
        {
            shieldVisible = visible;
            if (shieldVisual != null)
            {
                shieldVisual.SetActive(visible);
            }
        }

        private void AnimateBody(float deltaTime)
        {
            if (bodyRoot == null)
            {
                return;
            }

            float driftYaw = controller.IsDrifting ? controller.DriftDirection * 7f : 0f;
            float lean = -controller.CurrentSteering * Mathf.Lerp(2.5f, 7f, controller.NormalizedSpeed);
            if (controller.IsDrifting)
            {
                lean -= controller.DriftDirection * 3f;
            }

            float pitch = controller.IsBoosting ? -2.5f : controller.CurrentInput.Brake * 2.2f;
            Quaternion target = bodyBaseRotation * Quaternion.Euler(pitch, driftYaw, lean);
            bodyRoot.localRotation = Quaternion.Slerp(
                bodyRoot.localRotation,
                target,
                1f - Mathf.Exp(-deltaTime * 9f));
        }

        private void AnimateWheels()
        {
            if (wheelMeshes != null)
            {
                for (int i = 0; i < wheelMeshes.Length; i++)
                {
                    Transform wheel = wheelMeshes[i];
                    if (wheel == null)
                    {
                        continue;
                    }

                    Quaternion baseRotation = i < wheelBaseRotations.Length
                        ? wheelBaseRotations[i]
                        : Quaternion.identity;
                    wheel.localRotation = baseRotation * Quaternion.AngleAxis(wheelSpin, Vector3.up);
                }
            }

            if (frontSteerPivots != null)
            {
                for (int i = 0; i < frontSteerPivots.Length; i++)
                {
                    Transform pivot = frontSteerPivots[i];
                    if (pivot == null)
                    {
                        continue;
                    }

                    Quaternion baseRotation = i < steerBaseRotations.Length
                        ? steerBaseRotations[i]
                        : Quaternion.identity;
                    pivot.localRotation = baseRotation * Quaternion.Euler(0f, controller.CurrentSteering * 24f, 0f);
                }
            }
        }

        private void UpdateParticles()
        {
            bool accelerate = controller.ControlsEnabled && controller.CurrentInput.Throttle > 0.1f;
            SetEmission(exhaustParticles, accelerate || controller.IsBoosting, controller.IsBoosting ? 24f : 8f);
            SetEmission(driftParticles, controller.IsDrifting && controller.IsGrounded, 10f + controller.DriftTier * 8f);

            if (boostParticles != null)
            {
                ParticleSystem.EmissionModule emission = boostParticles.emission;
                emission.enabled = controller.IsBoosting;
                emission.rateOverTime = controller.IsBoosting ? 32f : 0f;
            }
        }

        private void AnimateShield()
        {
            if (!shieldVisible || shieldVisual == null)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.time * 5.5f) * 0.035f;
            shieldVisual.transform.localScale = Vector3.one * pulse;
            shieldVisual.transform.Rotate(0f, 28f * Time.deltaTime, 0f, Space.Self);
        }

        private static Quaternion[] CaptureRotations(Transform[] transforms)
        {
            if (transforms == null)
            {
                return System.Array.Empty<Quaternion>();
            }

            Quaternion[] rotations = new Quaternion[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                rotations[i] = transforms[i] != null ? transforms[i].localRotation : Quaternion.identity;
            }

            return rotations;
        }

        private static void SetEmission(ParticleSystem[] systems, bool enabled, float rate)
        {
            if (systems == null)
            {
                return;
            }

            foreach (ParticleSystem system in systems)
            {
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = enabled;
                emission.rateOverTime = enabled ? rate : 0f;
            }
        }
    }
}
