using System.Collections;
using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ItemPickup : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider triggerCollider;
        [SerializeField, Min(0.1f)] private float respawnDelay = 2.6f;
        [SerializeField] private float rotationSpeed = 52f;
        [SerializeField] private float bobHeight = 0.18f;
        [SerializeField] private float bobSpeed = 2.2f;

        private Vector3 baseLocalPosition;
        private float phase;
        private bool available = true;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            phase = UnityEngine.Random.value * Mathf.PI * 2f;
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        private void Update()
        {
            transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
            Vector3 position = baseLocalPosition;
            position.y += Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
            transform.localPosition = position;
        }

        public void Configure(Renderer[] pickupRenderers, Collider collider, float delay)
        {
            renderers = pickupRenderers;
            triggerCollider = collider;
            respawnDelay = delay;
            baseLocalPosition = transform.localPosition;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!available)
            {
                return;
            }

            KartItemInventory inventory = other.GetComponentInParent<KartItemInventory>();
            if (inventory != null && inventory.TryCollectItem())
            {
                StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            available = false;
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            SetVisible(true);
            available = true;
        }

        private void SetVisible(bool visible)
        {
            if (renderers != null)
            {
                foreach (Renderer itemRenderer in renderers)
                {
                    if (itemRenderer != null)
                    {
                        itemRenderer.enabled = visible;
                    }
                }
            }

            if (triggerCollider != null)
            {
                triggerCollider.enabled = visible;
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BoostPad : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float boostDuration = 0.8f;
        [SerializeField, Min(1f)] private float boostSpeed = 32f;

        public void Configure(float duration, float speed)
        {
            boostDuration = duration;
            boostSpeed = speed;
            Collider collider = GetComponent<Collider>();
            collider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            KartController kart = other.GetComponentInParent<KartController>();
            kart?.ApplyBoost(boostDuration, boostSpeed);
        }
    }

    [DisallowMultipleComponent]
    public sealed class PulseProjectile : MonoBehaviour
    {
        [SerializeField] private RaceProgress owner;
        [SerializeField] private Rigidbody body;
        [SerializeField] private float speed = 25f;
        [SerializeField] private float homingStrength = 5.2f;
        [SerializeField] private float lifetime = 5f;

        private Vector3 launchDirection;

        public void Configure(RaceProgress projectileOwner, Rigidbody rigidbody, Vector3 direction)
        {
            owner = projectileOwner;
            body = rigidbody;
            launchDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            body.linearVelocity = launchDirection * speed;
        }

        private void FixedUpdate()
        {
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (body == null)
            {
                return;
            }

            Vector3 desiredDirection = body.linearVelocity.sqrMagnitude > 0.01f
                ? body.linearVelocity.normalized
                : launchDirection;
            RaceManager manager = RaceManager.Instance;
            if (manager != null)
            {
                RaceProgress target = manager.FindNearestOpponent(
                    owner,
                    transform.position,
                    desiredDirection,
                    32f,
                    0.15f);
                if (target != null)
                {
                    Vector3 towardTarget =
                        (target.transform.position + Vector3.up * 0.35f - transform.position).normalized;
                    desiredDirection = Vector3.Slerp(
                        desiredDirection,
                        towardTarget,
                        homingStrength * Time.fixedDeltaTime).normalized;
                }
            }

            body.linearVelocity = desiredDirection * speed;
            if (desiredDirection.sqrMagnitude > 0.001f)
            {
                body.MoveRotation(Quaternion.LookRotation(desiredDirection, Vector3.up));
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            RaceProgress target = other.GetComponentInParent<RaceProgress>();
            if (target == owner)
            {
                return;
            }

            if (target != null)
            {
                KartItemInventory inventory = target.GetComponent<KartItemInventory>();
                if (inventory == null || !inventory.AbsorbHazard())
                {
                    target.Controller.SpinOut(0.78f);
                }
            }

            Destroy(gameObject);
        }
    }

    [DisallowMultipleComponent]
    public sealed class GlimmerTrap : MonoBehaviour
    {
        [SerializeField] private RaceProgress owner;
        [SerializeField] private float lifetime = 18f;

        public void Configure(RaceProgress trapOwner)
        {
            owner = trapOwner;
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.Self);
            if (lifetime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            RaceProgress target = other.GetComponentInParent<RaceProgress>();
            if (target == null || target == owner)
            {
                return;
            }

            KartItemInventory inventory = target.GetComponent<KartItemInventory>();
            if (inventory == null || !inventory.AbsorbHazard())
            {
                target.Controller.SpinOut(0.95f);
            }

            Destroy(gameObject);
        }
    }
}
