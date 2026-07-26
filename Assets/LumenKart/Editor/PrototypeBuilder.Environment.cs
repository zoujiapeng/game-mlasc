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
        private static void CreateEnvironment(TrackBuildResult track, Palette palette)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.64f, 0.76f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.64f, 0.59f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.34f, 0.29f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.8f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 280f;

            Material skybox = CreateSkyboxMaterial();
            RenderSettings.skybox = skybox;

            GameObject sunObject = new("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.78f);
            sun.intensity = 1.28f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sunObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            RenderSettings.sun = sun;

            CreateGlobalVolume();

            GameObject environmentRoot = new("Environment");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Landscape Ground";
            ground.transform.SetParent(environmentRoot.transform, false);
            ground.transform.position = new Vector3(0f, -0.62f, 0f);
            ground.transform.localScale = new Vector3(32f, 1f, 32f);
            ground.layer = groundLayer;
            ground.GetComponent<MeshRenderer>().sharedMaterial = palette.Grass;
            GameObjectUtility.SetStaticEditorFlags(
                ground,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(270726);
            CreateScenicHills(environmentRoot.transform, palette);
            CreateTrees(environmentRoot.transform, track, palette);
            CreateClouds(environmentRoot.transform, palette);
            UnityEngine.Random.state = randomState;
        }

        private static Material CreateSkyboxMaterial()
        {
            string path = GeneratedRoot + "/Rendering/LumenSkybox.mat";
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Skybox/Procedural");
            if (skybox == null)
            {
                skybox = new Material(shader) { name = "Lumen Skybox" };
                AssetDatabase.CreateAsset(skybox, path);
            }

            if (skybox.HasProperty("_SkyTint"))
            {
                skybox.SetColor("_SkyTint", new Color(0.48f, 0.68f, 0.78f));
                skybox.SetColor("_GroundColor", new Color(0.48f, 0.56f, 0.5f));
                skybox.SetFloat("_AtmosphereThickness", 0.82f);
                skybox.SetFloat("_SunSize", 0.025f);
                skybox.SetFloat("_Exposure", 1.05f);
            }

            EditorUtility.SetDirty(skybox);
            return skybox;
        }

        private static void CreateGlobalVolume()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Lumen Volume";
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }

            bloom.intensity.Override(0.36f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.58f);

            if (!profile.TryGet(out ColorAdjustments color))
            {
                color = profile.Add<ColorAdjustments>(true);
            }

            color.postExposure.Override(0.08f);
            color.contrast.Override(5f);
            color.saturation.Override(-8f);

            if (!profile.TryGet(out WhiteBalance whiteBalance))
            {
                whiteBalance = profile.Add<WhiteBalance>(true);
            }

            whiteBalance.temperature.Override(3f);
            whiteBalance.tint.Override(-2f);

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
            }

            tonemapping.mode.Override(TonemappingMode.Neutral);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.62f);

            EditorUtility.SetDirty(profile);
            GameObject volumeObject = new("Global Post Processing");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private static void CreateScenicHills(Transform parent, Palette palette)
        {
            Vector3[] positions =
            {
                new(-130f, -4f, 85f), new(-125f, -5f, -90f), new(145f, -5f, 70f),
                new(140f, -4f, -92f), new(20f, -7f, 145f), new(-20f, -7f, -145f),
                new(-155f, -6f, 5f), new(165f, -6f, -5f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject hill = CreateVisualPrimitive(
                    $"Soft Hill {i + 1}",
                    PrimitiveType.Sphere,
                    parent,
                    positions[i],
                    Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 180f), 0f),
                    new Vector3(
                        UnityEngine.Random.Range(25f, 43f),
                        UnityEngine.Random.Range(7f, 13f),
                        UnityEngine.Random.Range(24f, 40f)),
                    i % 2 == 0 ? palette.Grass : palette.GrassLight,
                    0,
                    true);
                hill.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void CreateTrees(Transform parent, TrackBuildResult track, Palette palette)
        {
            GameObject treeRoot = new("Trees");
            treeRoot.transform.SetParent(parent, false);
            for (int i = 0; i < track.Samples.Length; i += 7)
            {
                Vector3 point = track.Samples[i];
                Vector3 right = Vector3.Cross(Vector3.up, GeometryFactory.GetForward(track.Samples, i)).normalized;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (UnityEngine.Random.value < 0.24f)
                    {
                        continue;
                    }

                    float distance = track.HalfWidth + UnityEngine.Random.Range(8f, 19f);
                    Vector3 position = point + right * distance * side;
                    position.y = Mathf.Max(-0.25f, point.y - 0.2f);
                    float scale = UnityEngine.Random.Range(0.78f, 1.35f);
                    CreateTree(treeRoot.transform, position, scale, palette, i + side);
                }
            }
        }

        private static void CreateTree(
            Transform parent,
            Vector3 position,
            float scale,
            Palette palette,
            int seed)
        {
            GameObject root = new($"Tree {seed}");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            root.transform.localScale = Vector3.one * scale;

            CreateVisualPrimitive(
                "Trunk",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, 1.35f, 0f),
                Quaternion.identity,
                new Vector3(0.32f, 1.35f, 0.32f),
                palette.Trunk,
                0);
            CreateVisualPrimitive(
                "Canopy Lower",
                PrimitiveType.Sphere,
                root.transform,
                new Vector3(0f, 3.05f, 0f),
                Quaternion.identity,
                new Vector3(1.7f, 1.35f, 1.7f),
                palette.LeafA,
                0);
            CreateVisualPrimitive(
                "Canopy Upper",
                PrimitiveType.Sphere,
                root.transform,
                new Vector3(0.18f, 4.05f, -0.12f),
                Quaternion.identity,
                new Vector3(1.22f, 1.05f, 1.22f),
                palette.LeafB,
                0);
        }

        private static void CreateClouds(Transform parent, Palette palette)
        {
            GameObject cloudRoot = new("Clouds");
            cloudRoot.transform.SetParent(parent, false);
            for (int i = 0; i < 9; i++)
            {
                GameObject cloud = new($"Cloud {i + 1}");
                cloud.transform.SetParent(cloudRoot.transform, false);
                cloud.transform.position = new Vector3(
                    UnityEngine.Random.Range(-150f, 150f),
                    UnityEngine.Random.Range(24f, 42f),
                    UnityEngine.Random.Range(-145f, 145f));
                cloud.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 180f), 0f);
                float size = UnityEngine.Random.Range(1.2f, 2.2f);
                CreateVisualPrimitive(
                    "Cloud A",
                    PrimitiveType.Sphere,
                    cloud.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(3.6f, 1.1f, 1.5f) * size,
                    palette.Cloud,
                    0,
                    true);
                CreateVisualPrimitive(
                    "Cloud B",
                    PrimitiveType.Sphere,
                    cloud.transform,
                    new Vector3(2.3f * size, 0.15f, 0f),
                    Quaternion.identity,
                    new Vector3(2.5f, 0.95f, 1.3f) * size,
                    palette.Cloud,
                    0,
                    true);
                CreateVisualPrimitive(
                    "Cloud C",
                    PrimitiveType.Sphere,
                    cloud.transform,
                    new Vector3(-2.2f * size, -0.05f, 0.1f),
                    Quaternion.identity,
                    new Vector3(2.2f, 0.85f, 1.2f) * size,
                    palette.Cloud,
                    0,
                    true);
            }
        }
    }
}
