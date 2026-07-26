using UnityEngine;

namespace LumenKart
{
    [DisallowMultipleComponent]
    public sealed class TrackPath : MonoBehaviour
    {
        [SerializeField] private Vector3[] localSamples = System.Array.Empty<Vector3>();
        [SerializeField] private float trackWidth = 11f;

        public int Count => localSamples?.Length ?? 0;
        public float TrackWidth => trackWidth;

        public void Configure(Vector3[] worldSamples, float width)
        {
            if (worldSamples == null)
            {
                localSamples = System.Array.Empty<Vector3>();
                trackWidth = width;
                return;
            }

            localSamples = new Vector3[worldSamples.Length];
            for (int i = 0; i < worldSamples.Length; i++)
            {
                localSamples[i] = transform.InverseTransformPoint(worldSamples[i]);
            }

            trackWidth = width;
        }

        public Vector3 GetPoint(int index)
        {
            if (Count == 0)
            {
                return transform.position;
            }

            return transform.TransformPoint(localSamples[Wrap(index)]);
        }

        public Vector3 GetForward(int index)
        {
            if (Count < 2)
            {
                return transform.forward;
            }

            Vector3 forward = GetPoint(index + 1) - GetPoint(index - 1);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
        }

        public Vector3 GetRight(int index)
        {
            return Vector3.Cross(Vector3.up, GetForward(index)).normalized;
        }

        public Quaternion GetRotation(int index)
        {
            Vector3 forward = GetForward(index);
            return forward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(forward, Vector3.up)
                : Quaternion.identity;
        }

        public int FindClosestIndex(Vector3 worldPosition, int hint = -1, int searchRadius = 28)
        {
            if (Count == 0)
            {
                return 0;
            }

            int bestIndex = 0;
            float bestDistance = float.PositiveInfinity;

            if (hint < 0 || hint >= Count || searchRadius * 2 >= Count)
            {
                for (int i = 0; i < Count; i++)
                {
                    TestCandidate(i, worldPosition, ref bestIndex, ref bestDistance);
                }
            }
            else
            {
                for (int offset = -searchRadius; offset <= searchRadius; offset++)
                {
                    TestCandidate(Wrap(hint + offset), worldPosition, ref bestIndex, ref bestDistance);
                }
            }

            return bestIndex;
        }

        public float GetNormalizedProgress(int sampleIndex)
        {
            return Count <= 1 ? 0f : Wrap(sampleIndex) / (float)Count;
        }

        public int Wrap(int index)
        {
            if (Count == 0)
            {
                return 0;
            }

            index %= Count;
            return index < 0 ? index + Count : index;
        }

        private void TestCandidate(
            int index,
            Vector3 worldPosition,
            ref int bestIndex,
            ref float bestDistance)
        {
            float distance = (GetPoint(index) - worldPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Count < 2)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 0.85f, 0.95f, 0.8f);
            for (int i = 0; i < Count; i++)
            {
                Gizmos.DrawLine(GetPoint(i), GetPoint(i + 1));
            }
        }
    }
}
