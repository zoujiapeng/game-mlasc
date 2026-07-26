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
    [InitializeOnLoad]
    public static partial class PrototypeBuilder
    {
        private const string GeneratedRoot = "Assets/LumenKart/Generated";
        private const string SceneFolder = "Assets/LumenKart/Scenes";
        private const string ScenePath = SceneFolder + "/LumenCircuit.unity";
        private const string PipelinePath = GeneratedRoot + "/Rendering/LumenURP.asset";
        private const string RendererPath = GeneratedRoot + "/Rendering/LumenRenderer.asset";
        private const string VolumePath = GeneratedRoot + "/Rendering/LumenVolume.asset";
        private const string TuningPath = GeneratedRoot + "/Tuning/KartTuning.asset";

        private static bool buildInProgress;
        private static Font runtimeFont;
        private static int groundLayer;
        private static int kartLayer;
        private static int itemLayer;

        static PrototypeBuilder()
        {
            EditorApplication.delayCall += AutoBuildIfNeeded;
        }

        [MenuItem("Lumen Circuit/Rebuild Prototype", priority = 1)]
        public static void RebuildPrototype()
        {
            BuildPrototype(true);
        }

        [MenuItem("Lumen Circuit/Open Prototype Scene", priority = 2)]
        public static void OpenPrototypeScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildPrototype(false);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Lumen Circuit/Build Windows x64", priority = 20)]
        public static void BuildWindowsPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildPrototype(false);
            }

            Directory.CreateDirectory("Builds/Windows");
            BuildPlayerOptions options = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/LumenCircuit.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildPipeline.BuildPlayer(options);
        }

        public static void BuildFromCommandLine()
        {
            BuildPrototype(true);
        }

        private static void AutoBuildIfNeeded()
        {
            if (Application.isBatchMode || buildInProgress || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                BuildPrototype(false);
            }
        }

        private static void BuildPrototype(bool forceRebuild)
        {
            if (buildInProgress)
            {
                return;
            }

            buildInProgress = true;
            try
            {
                if (forceRebuild)
                {
                    AssetDatabase.DeleteAsset(GeneratedRoot);
                    AssetDatabase.DeleteAsset(ScenePath);
                }

                EnsureFolders();
                ConfigureProjectSettings();
                EnsureProjectLayers();
                SetupUniversalRenderPipeline();

                Palette palette = CreatePalette();
                KartTuning tuning = CreateKartTuning();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                TrackBuildResult track = CreateTrack(palette);
                CreateEnvironment(track, palette);
                RaceHUD hud = CreateHud(palette);

                GameObject systems = new("Game Systems");
                RaceManager raceManager = systems.AddComponent<RaceManager>();
                AudioSource uiAudio = systems.AddComponent<AudioSource>();
                uiAudio.playOnAwake = false;
                uiAudio.spatialBlend = 0f;
                raceManager.Configure(track.Path, hud, 3, uiAudio);

                ItemDirector itemDirector = systems.AddComponent<ItemDirector>();
                itemDirector.Configure(palette.Pulse, palette.Trap, itemLayer);

                List<KartController> karts = CreateKarts(track, raceManager, tuning, palette);
                CreatePickupsAndBoostPads(track, palette);
                CreateCamera(karts[0], 1 << groundLayer);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeGameObject = karts[0].gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
                Debug.Log("Lumen Circuit prototype generated. Press Play to race.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                buildInProgress = false;
            }
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                GeneratedRoot,
                GeneratedRoot + "/Materials",
                GeneratedRoot + "/Meshes",
                GeneratedRoot + "/Rendering",
                GeneratedRoot + "/Tuning",
                SceneFolder,
            };

            foreach (string folder in folders)
            {
                Directory.CreateDirectory(folder);
            }

            AssetDatabase.Refresh();
        }

        private static void ConfigureProjectSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "Lumen Forge";
            PlayerSettings.productName = "Lumen Circuit";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 120f;
        }

        private static void EnsureProjectLayers()
        {
            groundLayer = EnsureLayer("Ground", 8);
            kartLayer = EnsureLayer("Kart", 9);
            itemLayer = EnsureLayer("RuntimeItem", 10);
        }

        private static int EnsureLayer(string layerName, int preferredIndex)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                return preferredIndex;
            }

            SerializedObject tagManager = new(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == layerName)
                {
                    return i;
                }
            }

            int target = preferredIndex;
            if (target < 8 || target >= layers.arraySize || !string.IsNullOrEmpty(layers.GetArrayElementAtIndex(target).stringValue))
            {
                target = -1;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        target = i;
                        break;
                    }
                }
            }

            if (target >= 0)
            {
                layers.GetArrayElementAtIndex(target).stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return target;
            }

            Debug.LogWarning($"No free Unity layer was available for {layerName}; using Default.");
            return 0;
        }

        private static void SetupUniversalRenderPipeline()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                rendererData.name = "Lumen Renderer";
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.name = "Lumen URP";
                pipeline.supportsHDR = true;
                pipeline.supportsCameraDepthTexture = true;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.supportsMainLightShadows = true;
                pipeline.supportsAdditionalLightShadows = false;
                pipeline.supportsSoftShadows = true;
                pipeline.shadowDistance = 120f;
                pipeline.msaaSampleCount = 4;
                pipeline.renderScale = 1f;
                pipeline.useSRPBatcher = true;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(rendererData);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        private static KartTuning CreateKartTuning()
        {
            KartTuning tuning = AssetDatabase.LoadAssetAtPath<KartTuning>(TuningPath);
            if (tuning != null)
            {
                return tuning;
            }

            tuning = ScriptableObject.CreateInstance<KartTuning>();
            tuning.name = "Arcade Kart Tuning";
            AssetDatabase.CreateAsset(tuning, TuningPath);
            return tuning;
        }

        private static Palette CreatePalette()
        {
            Palette palette = new()
            {
                Road = CreateLitMaterial("Road", new Color(0.22f, 0.27f, 0.32f), 0.02f, 0.47f),
                CurbA = CreateLitMaterial("Curb Cream", new Color(0.88f, 0.84f, 0.73f), 0.02f, 0.42f),
                CurbB = CreateLitMaterial("Curb Coral", new Color(0.76f, 0.43f, 0.38f), 0.02f, 0.4f),
                Barrier = CreateLitMaterial("Barrier", new Color(0.79f, 0.81f, 0.76f), 0.04f, 0.52f),
                Grass = CreateLitMaterial("Sage Ground", new Color(0.43f, 0.58f, 0.43f), 0f, 0.3f),
                GrassLight = CreateLitMaterial("Mint Ground", new Color(0.54f, 0.67f, 0.51f), 0f, 0.32f),
                Trunk = CreateLitMaterial("Warm Trunk", new Color(0.42f, 0.31f, 0.24f), 0f, 0.35f),
                LeafA = CreateLitMaterial("Leaf Sage", new Color(0.34f, 0.55f, 0.42f), 0f, 0.33f),
                LeafB = CreateLitMaterial("Leaf Mint", new Color(0.48f, 0.65f, 0.49f), 0f, 0.34f),
                Cloud = CreateLitMaterial("Cloud", new Color(0.91f, 0.93f, 0.92f), 0f, 0.52f),
                Cream = CreateLitMaterial("Warm Cream", new Color(0.92f, 0.88f, 0.78f), 0.01f, 0.55f),
                Dark = CreateLitMaterial("Graphite", new Color(0.11f, 0.14f, 0.17f), 0.08f, 0.58f),
                Tire = CreateLitMaterial("Soft Tire", new Color(0.055f, 0.065f, 0.075f), 0f, 0.3f),
                Glass = CreateLitMaterial("Visor", new Color(0.12f, 0.28f, 0.34f), 0.1f, 0.86f),
                Boost = CreateLitMaterial("Boost Emission", new Color(0.25f, 0.68f, 0.75f), 0.02f, 0.55f, new Color(0.2f, 1.1f, 1.4f)),
                Pickup = CreateLitMaterial("Pickup Emission", new Color(0.75f, 0.63f, 0.36f), 0.05f, 0.58f, new Color(1.25f, 0.78f, 0.22f)),
                Pulse = CreateLitMaterial("Pulse Emission", new Color(0.38f, 0.64f, 0.82f), 0.08f, 0.6f, new Color(0.2f, 0.85f, 1.65f)),
                Trap = CreateLitMaterial("Trap Emission", new Color(0.72f, 0.43f, 0.72f), 0.04f, 0.55f, new Color(1.0f, 0.24f, 1.2f)),
                Shield = CreateTransparentMaterial("Shield", new Color(0.45f, 0.82f, 0.9f, 0.18f)),
                ParticleWarm = CreateParticleMaterial("Warm Particles", new Color(1f, 0.53f, 0.23f, 0.9f)),
                ParticleCool = CreateParticleMaterial("Cool Particles", new Color(0.28f, 0.85f, 1f, 0.85f)),
            };

            Color[] kartColors =
            {
                new(0.36f, 0.68f, 0.72f),
                new(0.76f, 0.47f, 0.4f),
                new(0.52f, 0.65f, 0.45f),
                new(0.63f, 0.55f, 0.74f),
                new(0.75f, 0.65f, 0.45f),
                new(0.45f, 0.62f, 0.78f),
                new(0.73f, 0.52f, 0.6f),
                new(0.46f, 0.72f, 0.61f),
            };
            palette.KartBodies = new Material[kartColors.Length];
            for (int i = 0; i < kartColors.Length; i++)
            {
                palette.KartBodies[i] = CreateLitMaterial($"Kart Body {i + 1:00}", kartColors[i], 0.12f, 0.7f);
            }

            return palette;
        }

        private static Material CreateLitMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            string path = $"{GeneratedRoot}/Materials/{Sanitize(name)}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            SetMaterialColor(material, color);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission.Value);
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentMaterial(string name, Color color)
        {
            Material material = CreateLitMaterial(name, color, 0f, 0.72f, color * 1.4f);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        private static Material CreateParticleMaterial(string name, Color color)
        {
            string path = $"{GeneratedRoot}/Materials/{Sanitize(name)}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            SetMaterialColor(material, color);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
