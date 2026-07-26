using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LumenKart.Editor
{
    public static partial class PrototypeBuilder
    {
        private static TrackBuildResult CreateTrack(Palette palette)
        {
            Vector3[] controlPoints =
            {
                new(0f, 0f, 0f),
                new(0f, 0.3f, 38f),
                new(25f, 1.8f, 70f),
                new(65f, 3.8f, 76f),
                new(100f, 2.2f, 52f),
                new(112f, 0.2f, 12f),
                new(96f, -0.4f, -28f),
                new(60f, 1.9f, -55f),
                new(15f, 4.8f, -68f),
                new(-30f, 4f, -64f),
                new(-70f, 1.2f, -42f),
                new(-94f, 0.1f, -6f),
                new(-88f, 2.1f, 32f),
                new(-62f, 3.2f, 58f),
                new(-34f, 1.4f, 46f),
                new(-20f, 0.2f, -25f),
            };

            Vector3[] samples = GeometryFactory.SampleClosedCatmullRom(controlPoints, 10);
            const float halfWidth = 5.5f;

            GameObject trackRoot = new("Lumen Circuit Track");
            TrackPath path = trackRoot.AddComponent<TrackPath>();
            path.Configure(samples, halfWidth * 2f);

            Mesh roadMesh = GeometryFactory.CreateRoadMesh(samples, halfWidth, 0.04f);
            SaveMeshAsset(roadMesh, GeneratedRoot + "/Meshes/Road.asset");
            GameObject road = CreateMeshObject("Road", trackRoot.transform, roadMesh, new[] { palette.Road }, groundLayer, true);
            road.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            Mesh leftRibbon = GeometryFactory.CreateRibbonMesh(samples, halfWidth, halfWidth + 1.05f, -1, 0.085f, true);
            Mesh rightRibbon = GeometryFactory.CreateRibbonMesh(samples, halfWidth, halfWidth + 1.05f, 1, 0.085f, true);
            SaveMeshAsset(leftRibbon, GeneratedRoot + "/Meshes/LeftCurb.asset");
            SaveMeshAsset(rightRibbon, GeneratedRoot + "/Meshes/RightCurb.asset");
            CreateMeshObject("Left Curb", trackRoot.transform, leftRibbon, new[] { palette.CurbA, palette.CurbB }, groundLayer, true);
            CreateMeshObject("Right Curb", trackRoot.transform, rightRibbon, new[] { palette.CurbA, palette.CurbB }, groundLayer, true);

            CreateBarrierSegments(trackRoot.transform, samples, halfWidth + 1.22f, palette);
            CreateStartLineAndArch(trackRoot.transform, samples, halfWidth, palette);
            const int checkpointCount = 8;
            CreateCheckpoints(trackRoot.transform, samples, halfWidth, checkpointCount);

            return new TrackBuildResult
            {
                Root = trackRoot,
                Path = path,
                Samples = samples,
                HalfWidth = halfWidth,
                CheckpointCount = checkpointCount,
                StartSample = 3,
            };
        }

        private static GameObject CreateMeshObject(
            string name,
            Transform parent,
            Mesh mesh,
            Material[] materials,
            int layer,
            bool collider)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = layer;
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.receiveShadows = true;
            if (collider)
            {
                MeshCollider meshCollider = gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }

            GameObjectUtility.SetStaticEditorFlags(
                gameObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);
            return gameObject;
        }

        private static void CreateBarrierSegments(
            Transform parent,
            Vector3[] samples,
            float offset,
            Palette palette)
        {
            GameObject barrierRoot = new("Track Barriers");
            barrierRoot.transform.SetParent(parent, false);
            for (int i = 0; i < samples.Length; i += 2)
            {
                int next = (i + 2) % samples.Length;
                Vector3 start = samples[i];
                Vector3 end = samples[next];
                Vector3 direction = end - start;
                float length = direction.magnitude + 0.2f;
                if (length < 0.1f)
                {
                    continue;
                }

                Vector3 forward = direction.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 center = (start + end) * 0.5f + Vector3.up * 0.48f;
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    barrier.name = side < 0 ? "Barrier L" : "Barrier R";
                    barrier.transform.SetParent(barrierRoot.transform, true);
                    barrier.transform.position = center + right * (offset * side);
                    barrier.transform.rotation = rotation;
                    barrier.transform.localScale = new Vector3(0.34f, 0.86f, length);
                    barrier.layer = groundLayer;
                    barrier.GetComponent<MeshRenderer>().sharedMaterial =
                        i % 12 == 0 ? palette.CurbA : palette.Barrier;
                    GameObjectUtility.SetStaticEditorFlags(
                        barrier,
                        StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic);
                }
            }
        }

        private static void CreateStartLineAndArch(
            Transform parent,
            Vector3[] samples,
            float halfWidth,
            Palette palette)
        {
            Vector3 point = samples[0] + Vector3.up * 0.1f;
            Vector3 forward = GeometryFactory.GetForward(samples, 0);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

            GameObject lineRoot = new("Start Line");
            lineRoot.transform.SetParent(parent, true);
            lineRoot.transform.position = point;
            lineRoot.transform.rotation = rotation;

            const int tiles = 10;
            float tileWidth = halfWidth * 2f / tiles;
            for (int i = 0; i < tiles; i++)
            {
                GameObject tile = CreateVisualPrimitive(
                    $"Start Tile {i + 1}",
                    PrimitiveType.Cube,
                    lineRoot.transform,
                    new Vector3(-halfWidth + tileWidth * (i + 0.5f), 0f, 0f),
                    Quaternion.identity,
                    new Vector3(tileWidth * 0.96f, 0.035f, 1.15f),
                    i % 2 == 0 ? palette.Cream : palette.CurbB,
                    groundLayer);
                tile.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            GameObject arch = new("Start Arch");
            arch.transform.SetParent(parent, true);
            arch.transform.position = point + forward * 1.8f;
            arch.transform.rotation = rotation;
            CreateVisualPrimitive(
                "Arch Left",
                PrimitiveType.Cube,
                arch.transform,
                new Vector3(-(halfWidth + 1.3f), 2.2f, 0f),
                Quaternion.identity,
                new Vector3(0.42f, 4.4f, 0.42f),
                palette.Cream,
                groundLayer);
            CreateVisualPrimitive(
                "Arch Right",
                PrimitiveType.Cube,
                arch.transform,
                new Vector3(halfWidth + 1.3f, 2.2f, 0f),
                Quaternion.identity,
                new Vector3(0.42f, 4.4f, 0.42f),
                palette.Cream,
                groundLayer);
            CreateVisualPrimitive(
                "Arch Header",
                PrimitiveType.Cube,
                arch.transform,
                new Vector3(0f, 4.18f, 0f),
                Quaternion.identity,
                new Vector3(halfWidth * 2f + 3f, 0.55f, 0.62f),
                palette.CurbB,
                groundLayer);
        }

        private static void CreateCheckpoints(
            Transform parent,
            Vector3[] samples,
            float halfWidth,
            int checkpointCount)
        {
            GameObject root = new("Race Checkpoints");
            root.transform.SetParent(parent, false);
            for (int i = 0; i < checkpointCount; i++)
            {
                int sampleIndex = Mathf.RoundToInt(i * samples.Length / (float)checkpointCount) % samples.Length;
                Vector3 point = samples[sampleIndex] + Vector3.up * 1.4f;
                Vector3 forward = GeometryFactory.GetForward(samples, sampleIndex);
                GameObject checkpointObject = new($"Checkpoint {i:00}");
                checkpointObject.transform.SetParent(root.transform, true);
                checkpointObject.transform.position = point;
                checkpointObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                BoxCollider trigger = checkpointObject.AddComponent<BoxCollider>();
                trigger.size = new Vector3(halfWidth * 2f, 3.6f, 1.6f);
                trigger.isTrigger = true;
                RaceCheckpoint checkpoint = checkpointObject.AddComponent<RaceCheckpoint>();
                checkpoint.Configure(i, sampleIndex);
            }
        }
    }
}
