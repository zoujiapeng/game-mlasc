using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartController))]
    public sealed class RaceProgress : MonoBehaviour
    {
        [SerializeField] private RaceManager manager;
        [SerializeField] private TrackPath trackPath;
        [SerializeField] private KartController controller;
        [SerializeField] private string racerName = "Racer";
        [SerializeField] private bool isPlayer;
        [SerializeField, Min(2)] private int checkpointCount = 8;
        [SerializeField] private Vector3 respawnPosition;
        [SerializeField] private Quaternion respawnRotation = Quaternion.identity;

        private int currentLap = 1;
        private int nextCheckpoint = 1;
        private int closestPathSample;
        private int racePosition = 1;
        private bool finished;
        private int finishPosition;
        private float finishTime;
        private float wrongWayTimer;

        public RaceManager Manager => manager;
        public TrackPath TrackPath => trackPath;
        public KartController Controller => controller;
        public string RacerName => racerName;
        public bool IsPlayer => isPlayer;
        public int CurrentLap => currentLap;
        public int RacePosition => racePosition;
        public bool HasFinished => finished;
        public int FinishPosition => finishPosition;
        public float FinishTime => finishTime;
        public int ClosestPathSample => closestPathSample;
        public bool IsDrivingWrongWay => wrongWayTimer > 0.65f;

        public float ProgressScore
        {
            get
            {
                int pathCount = trackPath != null ? trackPath.Count : 0;
                if (pathCount <= 0)
                {
                    return 0f;
                }

                return (currentLap - 1) * pathCount + closestPathSample;
            }
        }

        public float DistanceToNextCheckpoint
        {
            get
            {
                RaceCheckpoint checkpoint = RaceCheckpoint.GetByIndex(nextCheckpoint);
                return checkpoint != null
                    ? Vector3.Distance(transform.position, checkpoint.transform.position)
                    : 0f;
            }
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<KartController>();
            }

            if (manager == null)
            {
                manager = RaceManager.Instance;
            }

            manager?.Register(this);
        }

        private void Update()
        {
            if (trackPath == null || trackPath.Count == 0)
            {
                return;
            }

            closestPathSample = trackPath.FindClosestIndex(transform.position, closestPathSample, 30);
            Vector3 expectedForward = trackPath.GetForward(closestPathSample);
            float facing = Vector3.Dot(transform.forward, expectedForward);
            if (facing < -0.35f && controller.PlanarSpeed > 3f)
            {
                wrongWayTimer += Time.deltaTime;
            }
            else
            {
                wrongWayTimer = Mathf.Max(0f, wrongWayTimer - Time.deltaTime * 2f);
            }
        }

        public void Configure(
            RaceManager raceManager,
            TrackPath path,
            KartController kartController,
            string displayName,
            bool playerControlled,
            int numberOfCheckpoints,
            Vector3 initialRespawnPosition,
            Quaternion initialRespawnRotation)
        {
            manager = raceManager;
            trackPath = path;
            controller = kartController;
            racerName = displayName;
            isPlayer = playerControlled;
            checkpointCount = Mathf.Max(2, numberOfCheckpoints);
            respawnPosition = initialRespawnPosition;
            respawnRotation = initialRespawnRotation;
        }

        public void TryPassCheckpoint(RaceCheckpoint checkpoint)
        {
            if (finished || checkpoint == null || checkpoint.Index != nextCheckpoint)
            {
                return;
            }

            nextCheckpoint = (checkpoint.Index + 1) % checkpointCount;
            respawnPosition = checkpoint.RespawnPosition;
            respawnRotation = checkpoint.RespawnRotation;

            if (checkpoint.Index == 0)
            {
                currentLap++;
                if (manager != null && currentLap > manager.TotalLaps)
                {
                    finished = true;
                    currentLap = manager.TotalLaps;
                    manager.NotifyFinished(this);
                }
            }
        }

        public void RespawnToLastCheckpoint()
        {
            if (controller == null)
            {
                return;
            }

            Vector3 right = respawnRotation * Vector3.right;
            float currentLaneOffset = Vector3.Dot(transform.position - respawnPosition, right);
            Vector3 laneOffset = right * Mathf.Clamp(currentLaneOffset, -1.4f, 1.4f);
            controller.RespawnAt(respawnPosition + laneOffset + Vector3.up * 0.4f, respawnRotation);
        }

        public void SetRacePosition(int position)
        {
            racePosition = Mathf.Max(1, position);
        }

        public void SetFinishPosition(int position, float time)
        {
            finishPosition = Mathf.Max(1, position);
            finishTime = Mathf.Max(0f, time);
        }
    }
}
