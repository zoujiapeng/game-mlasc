using System.Collections.Generic;
using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaceCheckpoint : MonoBehaviour
    {
        private static readonly Dictionary<int, RaceCheckpoint> Registry = new();

        [SerializeField] private int index;
        [SerializeField] private int pathSampleIndex;

        public int Index => index;
        public int PathSampleIndex => pathSampleIndex;
        public Vector3 RespawnPosition =>
            transform.position + transform.forward * 2.25f - Vector3.up * 1.02f;
        public Quaternion RespawnRotation => Quaternion.LookRotation(transform.forward, Vector3.up);

        private void OnEnable()
        {
            Registry[index] = this;
        }

        private void OnDisable()
        {
            if (Registry.TryGetValue(index, out RaceCheckpoint checkpoint) && checkpoint == this)
            {
                Registry.Remove(index);
            }
        }

        public void Configure(int checkpointIndex, int sampleIndex)
        {
            index = checkpointIndex;
            pathSampleIndex = sampleIndex;
            Registry[index] = this;

            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        public static RaceCheckpoint GetByIndex(int checkpointIndex)
        {
            Registry.TryGetValue(checkpointIndex, out RaceCheckpoint checkpoint);
            return checkpoint;
        }

        public static void RebuildRegistry()
        {
            Registry.Clear();
            RaceCheckpoint[] checkpoints =
                UnityEngine.Object.FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None);
            foreach (RaceCheckpoint checkpoint in checkpoints)
            {
                Registry[checkpoint.index] = checkpoint;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            RaceProgress progress = other.GetComponentInParent<RaceProgress>();
            progress?.TryPassCheckpoint(this);
        }
    }
}
