using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    public sealed class AiKartInput : KartInputSource
    {
        [SerializeField] private TrackPath trackPath;
        [SerializeField] private KartController controller;
        [SerializeField] private RaceProgress progress;
        [SerializeField] private KartItemInventory inventory;
        [SerializeField, Range(-3f, 3f)] private float laneOffset;
        [SerializeField, Range(0.55f, 1f)] private float skill = 0.82f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private int closestIndex;
        private float steeringMemory;
        private float itemDecisionTimer;
        private float stuckTimer;
        private float laneWanderTimer;
        private float laneWander;

        public override KartInputFrame ReadInput()
        {
            if (trackPath == null || controller == null || trackPath.Count < 4)
            {
                return KartInputFrame.Neutral;
            }

            RaceManager manager = RaceManager.Instance;
            if (manager != null)
            {
                controller.ExternalSpeedMultiplier = manager.GetCatchupMultiplier(progress);
            }

            closestIndex = trackPath.FindClosestIndex(transform.position, closestIndex, 24);
            UpdateLaneWander();

            int lookAhead = Mathf.RoundToInt(Mathf.Lerp(5f, 13f, controller.NormalizedSpeed));
            int targetIndex = closestIndex + lookAhead;
            Vector3 target = trackPath.GetPoint(targetIndex) +
                             trackPath.GetRight(targetIndex) * (laneOffset + laneWander);
            Vector3 localTarget = transform.InverseTransformPoint(target);
            float rawSteering = Mathf.Clamp(
                localTarget.x / Mathf.Max(2.2f, Mathf.Abs(localTarget.z)) * 2.4f,
                -1f,
                1f);

            float avoidance = CalculateObstacleAvoidance();
            rawSteering = Mathf.Clamp(rawSteering + avoidance, -1f, 1f);
            steeringMemory = Mathf.MoveTowards(
                steeringMemory,
                rawSteering,
                Time.deltaTime * Mathf.Lerp(4.5f, 8f, skill));

            Vector3 nearForward = trackPath.GetForward(targetIndex);
            Vector3 farForward = trackPath.GetForward(targetIndex + 9);
            float cornerAngle = Mathf.Abs(Vector3.SignedAngle(nearForward, farForward, Vector3.up));
            float desiredSpeed = Mathf.Lerp(15f, 24f, skill);
            desiredSpeed -= Mathf.InverseLerp(8f, 62f, cornerAngle) * Mathf.Lerp(5f, 9f, skill);

            float throttle = controller.ForwardSpeed < desiredSpeed ? 1f : 0.2f;
            float brake = controller.ForwardSpeed > desiredSpeed + 2.2f ? 0.55f : 0f;
            bool drift =
                cornerAngle > Mathf.Lerp(31f, 21f, skill) &&
                controller.ForwardSpeed > controller.Tuning.minimumDriftSpeed + 1f &&
                Mathf.Abs(steeringMemory) > 0.28f;

            float finalSteering = steeringMemory;
            UpdateStuckState(ref throttle, ref brake, ref finalSteering);
            bool useItem = DecideItemUse(cornerAngle);

            return new KartInputFrame(
                finalSteering,
                throttle,
                brake,
                drift,
                useItem,
                false);
        }

        public void Configure(
            TrackPath path,
            KartController kartController,
            RaceProgress raceProgress,
            KartItemInventory itemInventory,
            float preferredLaneOffset,
            float drivingSkill,
            LayerMask collisionMask)
        {
            trackPath = path;
            controller = kartController;
            progress = raceProgress;
            inventory = itemInventory;
            laneOffset = preferredLaneOffset;
            skill = Mathf.Clamp(drivingSkill, 0.55f, 1f);
            obstacleMask = collisionMask;
            laneWanderTimer = UnityEngine.Random.Range(1.5f, 3.5f);
            itemDecisionTimer = UnityEngine.Random.Range(1f, 2.5f);
        }

        private float CalculateObstacleAvoidance()
        {
            Vector3 origin = transform.position + transform.up * 0.45f + transform.forward * 0.8f;
            float distance = Mathf.Lerp(3.5f, 6.5f, controller.NormalizedSpeed);
            float avoid = 0f;

            if (Physics.Raycast(
                    origin - transform.right * 0.45f,
                    transform.forward,
                    distance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                avoid += 0.55f;
            }

            if (Physics.Raycast(
                    origin + transform.right * 0.45f,
                    transform.forward,
                    distance,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore))
            {
                avoid -= 0.55f;
            }

            return avoid;
        }

        private void UpdateStuckState(ref float throttle, ref float brake, ref float steering)
        {
            RaceManager manager = RaceManager.Instance;
            bool raceActive = manager != null && manager.RaceStarted && !manager.RaceFinished;
            if (raceActive && controller.PlanarSpeed < 1.1f)
            {
                stuckTimer += Time.deltaTime;
            }
            else
            {
                stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime * 2f);
            }

            if (stuckTimer > 1.5f)
            {
                throttle = 0f;
                brake = 1f;
                steering = Mathf.Sin(Time.time * 3.1f) > 0f ? 1f : -1f;
            }

            if (stuckTimer > 4.5f && progress != null)
            {
                progress.RespawnToLastCheckpoint();
                stuckTimer = 0f;
            }
        }

        private bool DecideItemUse(float cornerAngle)
        {
            if (inventory == null || !inventory.HasItem)
            {
                return false;
            }

            itemDecisionTimer -= Time.deltaTime;
            if (itemDecisionTimer > 0f)
            {
                return false;
            }

            itemDecisionTimer = UnityEngine.Random.Range(1.2f, 3.4f);
            switch (inventory.CurrentItem)
            {
                case KartItemType.BurstDrive:
                    return cornerAngle < 18f;
                case KartItemType.GlimmerTrap:
                    return controller.ForwardSpeed > 8f;
                case KartItemType.ArcPulse:
                    return true;
                case KartItemType.CloudShield:
                    return progress != null && progress.RacePosition <= 4;
                default:
                    return false;
            }
        }

        private void UpdateLaneWander()
        {
            laneWanderTimer -= Time.deltaTime;
            if (laneWanderTimer > 0f)
            {
                return;
            }

            laneWanderTimer = UnityEngine.Random.Range(2.2f, 5.5f);
            laneWander = UnityEngine.Random.Range(-0.65f, 0.65f) * (1f - skill * 0.45f);
        }
    }
}
