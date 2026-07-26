using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LumenKart.Editor
{
    internal static class GeometryFactory
    {
        public static Vector3[] SampleClosedCatmullRom(
            IReadOnlyList<Vector3> controlPoints,
            int samplesPerSegment)
        {
            if (controlPoints == null || controlPoints.Count < 4)
            {
                return System.Array.Empty<Vector3>();
            }

            samplesPerSegment = Mathf.Max(2, samplesPerSegment);
            List<Vector3> result = new(controlPoints.Count * samplesPerSegment);
            int count = controlPoints.Count;

            for (int segment = 0; segment < count; segment++)
            {
                Vector3 p0 = controlPoints[(segment - 1 + count) % count];
                Vector3 p1 = controlPoints[segment];
                Vector3 p2 = controlPoints[(segment + 1) % count];
                Vector3 p3 = controlPoints[(segment + 2) % count];

                for (int sample = 0; sample < samplesPerSegment; sample++)
                {
                    float t = sample / (float)samplesPerSegment;
                    result.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            return result.ToArray();
        }

        public static Mesh CreateRoadMesh(Vector3[] samples, float halfWidth, float verticalOffset = 0f)
        {
            Mesh mesh = new() { name = "Lumen Circuit Road" };
            if (samples == null || samples.Length < 3)
            {
                return mesh;
            }

            int count = samples.Length;
            Vector3[] vertices = new Vector3[count * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[count * 6];
            float distance = 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 forward = GetForward(samples, i);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 center = samples[i] + Vector3.up * verticalOffset;
                vertices[i * 2] = center - right * halfWidth;
                vertices[i * 2 + 1] = center + right * halfWidth;

                if (i > 0)
                {
                    distance += Vector3.Distance(samples[i - 1], samples[i]);
                }

                uv[i * 2] = new Vector2(0f, distance * 0.08f);
                uv[i * 2 + 1] = new Vector2(1f, distance * 0.08f);

                int next = (i + 1) % count;
                int triangle = i * 6;
                triangles[triangle] = i * 2;
                triangles[triangle + 1] = next * 2;
                triangles[triangle + 2] = next * 2 + 1;
                triangles[triangle + 3] = i * 2;
                triangles[triangle + 4] = next * 2 + 1;
                triangles[triangle + 5] = i * 2 + 1;
            }

            mesh.indexFormat = vertices.Length > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateRibbonMesh(
            Vector3[] samples,
            float innerHalfWidth,
            float outerHalfWidth,
            int side,
            float verticalOffset,
            bool alternatingSubmeshes)
        {
            Mesh mesh = new() { name = side < 0 ? "Left Edge Ribbon" : "Right Edge Ribbon" };
            if (samples == null || samples.Length < 3)
            {
                return mesh;
            }

            side = side < 0 ? -1 : 1;
            int count = samples.Length;
            Vector3[] vertices = new Vector3[count * 4];
            Vector2[] uv = new Vector2[vertices.Length];
            List<int> firstTriangles = new(count * 3);
            List<int> secondTriangles = new(count * 3);

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                Vector3 right0 = Vector3.Cross(Vector3.up, GetForward(samples, i)).normalized * side;
                Vector3 right1 = Vector3.Cross(Vector3.up, GetForward(samples, next)).normalized * side;
                Vector3 up = Vector3.up * verticalOffset;

                int vertex = i * 4;
                vertices[vertex] = samples[i] + right0 * innerHalfWidth + up;
                vertices[vertex + 1] = samples[i] + right0 * outerHalfWidth + up;
                vertices[vertex + 2] = samples[next] + right1 * innerHalfWidth + up;
                vertices[vertex + 3] = samples[next] + right1 * outerHalfWidth + up;

                uv[vertex] = new Vector2(0f, 0f);
                uv[vertex + 1] = new Vector2(1f, 0f);
                uv[vertex + 2] = new Vector2(0f, 1f);
                uv[vertex + 3] = new Vector2(1f, 1f);

                List<int> target = !alternatingSubmeshes || i % 2 == 0
                    ? firstTriangles
                    : secondTriangles;

                if (side > 0)
                {
                    target.Add(vertex);
                    target.Add(vertex + 2);
                    target.Add(vertex + 3);
                    target.Add(vertex);
                    target.Add(vertex + 3);
                    target.Add(vertex + 1);
                }
                else
                {
                    target.Add(vertex);
                    target.Add(vertex + 3);
                    target.Add(vertex + 2);
                    target.Add(vertex);
                    target.Add(vertex + 1);
                    target.Add(vertex + 3);
                }
            }

            mesh.indexFormat = vertices.Length > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.subMeshCount = alternatingSubmeshes ? 2 : 1;
            mesh.SetTriangles(firstTriangles, 0);
            if (alternatingSubmeshes)
            {
                mesh.SetTriangles(secondTriangles, 1);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh CreateDiamondMesh(float radius, float height)
        {
            Vector3[] vertices =
            {
                new(0f, height, 0f),
                new(radius, 0f, 0f),
                new(0f, 0f, radius),
                new(-radius, 0f, 0f),
                new(0f, 0f, -radius),
                new(0f, -height, 0f),
            };

            int[] triangles =
            {
                0, 2, 1,
                0, 3, 2,
                0, 4, 3,
                0, 1, 4,
                5, 1, 2,
                5, 2, 3,
                5, 3, 4,
                5, 4, 1,
            };

            Mesh mesh = new()
            {
                name = "Pickup Diamond",
                vertices = vertices,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3 GetForward(Vector3[] samples, int index)
        {
            int count = samples.Length;
            Vector3 forward = samples[(index + 1) % count] - samples[(index - 1 + count) % count];
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 CatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f *
                   ((2f * p1) +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
