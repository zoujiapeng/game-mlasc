using UnityEngine;

namespace LumenKart
{
    public enum KartItemType
    {
        None = 0,
        BurstDrive = 1,
        ArcPulse = 2,
        CloudShield = 3,
        GlimmerTrap = 4,
    }

    [DisallowMultipleComponent]
    public sealed class ItemDirector : MonoBehaviour
    {
        [SerializeField] private Material pulseMaterial;
        [SerializeField] private Material trapMaterial;
        [SerializeField] private int runtimeItemLayer;

        public static ItemDirector Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Configure(Material pulse, Material trap, int itemLayer)
        {
            pulseMaterial = pulse;
            trapMaterial = trap;
            runtimeItemLayer = itemLayer;
        }

        public void SpawnPulse(RaceProgress owner)
        {
            if (owner == null)
            {
                return;
            }

            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = $"Arc Pulse - {owner.RacerName}";
            pulse.layer = runtimeItemLayer;
            pulse.transform.position = owner.transform.position + owner.transform.up * 0.55f + owner.transform.forward * 1.55f;
            pulse.transform.localScale = Vector3.one * 0.55f;

            MeshRenderer renderer = pulse.GetComponent<MeshRenderer>();
            if (renderer != null && pulseMaterial != null)
            {
                renderer.sharedMaterial = pulseMaterial;
            }

            Collider collider = pulse.GetComponent<Collider>();
            collider.isTrigger = true;

            Rigidbody rigidbody = pulse.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.mass = 0.2f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            PulseProjectile projectile = pulse.AddComponent<PulseProjectile>();
            projectile.Configure(owner, rigidbody, owner.transform.forward);
        }

        public void SpawnTrap(RaceProgress owner)
        {
            if (owner == null)
            {
                return;
            }

            GameObject trap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trap.name = $"Glimmer Trap - {owner.RacerName}";
            trap.layer = runtimeItemLayer;
            trap.transform.position = owner.transform.position - owner.transform.forward * 1.4f + Vector3.up * 0.18f;
            trap.transform.rotation = Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f);
            trap.transform.localScale = new Vector3(0.62f, 0.11f, 0.62f);

            MeshRenderer renderer = trap.GetComponent<MeshRenderer>();
            if (renderer != null && trapMaterial != null)
            {
                renderer.sharedMaterial = trapMaterial;
            }

            Collider collider = trap.GetComponent<Collider>();
            collider.isTrigger = true;

            GlimmerTrap glimmerTrap = trap.AddComponent<GlimmerTrap>();
            glimmerTrap.Configure(owner);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartController))]
    public sealed class KartItemInventory : MonoBehaviour
    {
        [SerializeField] private KartController controller;
        [SerializeField] private RaceProgress progress;
        [SerializeField] private KartVisuals visuals;
        [SerializeField] private KartItemType currentItem;

        private float shieldTimer;
        private float pickupLockout;

        public KartItemType CurrentItem => currentItem;
        public bool HasItem => currentItem != KartItemType.None;
        public bool ShieldActive => shieldTimer > 0f;

        public string DisplayName
        {
            get
            {
                if (currentItem == KartItemType.None)
                {
                    return ShieldActive ? "CLOUD SHIELD" : "EMPTY";
                }

                return currentItem switch
                {
                    KartItemType.BurstDrive => "BURST DRIVE",
                    KartItemType.ArcPulse => "ARC PULSE",
                    KartItemType.CloudShield => "CLOUD SHIELD",
                    KartItemType.GlimmerTrap => "GLIMMER TRAP",
                    _ => "EMPTY",
                };
            }
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<KartController>();
            }

            if (progress == null)
            {
                progress = GetComponent<RaceProgress>();
            }

            if (visuals == null)
            {
                visuals = GetComponent<KartVisuals>();
            }
        }

        private void Update()
        {
            pickupLockout = Mathf.Max(0f, pickupLockout - Time.deltaTime);
            if (shieldTimer > 0f)
            {
                shieldTimer = Mathf.Max(0f, shieldTimer - Time.deltaTime);
                if (shieldTimer <= 0f)
                {
                    visuals?.SetShieldVisible(false);
                }
            }
        }

        public void Configure(KartController kartController, RaceProgress raceProgress, KartVisuals kartVisuals)
        {
            controller = kartController;
            progress = raceProgress;
            visuals = kartVisuals;
        }

        public bool TryCollectItem()
        {
            if (currentItem != KartItemType.None || pickupLockout > 0f)
            {
                return false;
            }

            int position = progress != null ? progress.RacePosition : 4;
            int racerCount = progress != null && progress.Manager != null
                ? progress.Manager.Racers.Count
                : 8;
            currentItem = RollItem(position, racerCount);
            pickupLockout = 0.8f;
            return true;
        }

        public void UseCurrentItem()
        {
            if (currentItem == KartItemType.None || controller == null)
            {
                return;
            }

            KartItemType item = currentItem;
            currentItem = KartItemType.None;
            pickupLockout = 0.35f;

            switch (item)
            {
                case KartItemType.BurstDrive:
                    controller.ApplyBoost(1.25f, controller.Tuning.defaultBoostSpeed + 3.5f);
                    break;
                case KartItemType.ArcPulse:
                    ItemDirector.Instance?.SpawnPulse(progress);
                    break;
                case KartItemType.CloudShield:
                    shieldTimer = 6f;
                    visuals?.SetShieldVisible(true);
                    break;
                case KartItemType.GlimmerTrap:
                    ItemDirector.Instance?.SpawnTrap(progress);
                    break;
            }
        }

        public bool AbsorbHazard()
        {
            if (!ShieldActive)
            {
                return false;
            }

            shieldTimer = 0f;
            visuals?.SetShieldVisible(false);
            controller?.ApplyBoost(0.22f, controller.Tuning.maxForwardSpeed + 1f);
            return true;
        }

        private static KartItemType RollItem(int position, int racerCount)
        {
            float rearFactor = racerCount <= 1
                ? 0.5f
                : Mathf.Clamp01((position - 1f) / (racerCount - 1f));
            float roll = UnityEngine.Random.value;

            if (rearFactor > 0.68f)
            {
                if (roll < 0.46f)
                {
                    return KartItemType.BurstDrive;
                }

                if (roll < 0.78f)
                {
                    return KartItemType.ArcPulse;
                }

                if (roll < 0.9f)
                {
                    return KartItemType.CloudShield;
                }

                return KartItemType.GlimmerTrap;
            }

            if (rearFactor < 0.25f)
            {
                if (roll < 0.38f)
                {
                    return KartItemType.GlimmerTrap;
                }

                if (roll < 0.68f)
                {
                    return KartItemType.CloudShield;
                }

                if (roll < 0.86f)
                {
                    return KartItemType.ArcPulse;
                }

                return KartItemType.BurstDrive;
            }

            if (roll < 0.3f)
            {
                return KartItemType.BurstDrive;
            }

            if (roll < 0.56f)
            {
                return KartItemType.ArcPulse;
            }

            if (roll < 0.78f)
            {
                return KartItemType.CloudShield;
            }

            return KartItemType.GlimmerTrap;
        }
    }
}
