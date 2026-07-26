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
        private static List<KartController> CreateKarts(
            TrackBuildResult track,
            RaceManager raceManager,
            KartTuning tuning,
            Palette palette)
        {
            string[] names = { "YOU", "ASTER", "BRIO", "CIELO", "DUNE", "MALLOW", "NORI", "VELA" };
            List<KartController> result = new(names.Length);
            GameObject racersRoot = new("Racers");

            Vector3 startPoint = track.Samples[track.StartSample] + Vector3.up * 0.78f;
            Vector3 forward = GeometryFactory.GetForward(track.Samples, track.StartSample);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            LayerMask roadMask = 1 << groundLayer;

            for (int i = 0; i < names.Length; i++)
            {
                int row = i / 2;
                float lane = i % 2 == 0 ? -1.35f : 1.35f;
                Vector3 position = startPoint - forward * (row * 2.55f) + right * lane;
                GameObject kart = new($"Kart {names[i]}");
                kart.transform.SetParent(racersRoot.transform, true);
                kart.transform.position = position;
                kart.transform.rotation = rotation;
                SetLayerRecursively(kart, kartLayer);

                Rigidbody rigidbody = kart.AddComponent<Rigidbody>();
                rigidbody.mass = 1.35f;
                rigidbody.useGravity = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                BoxCollider bodyCollider = kart.AddComponent<BoxCollider>();
                bodyCollider.center = new Vector3(0f, 0.32f, 0f);
                bodyCollider.size = new Vector3(1.45f, 0.72f, 2.15f);

                KartInputSource input = i == 0
                    ? kart.AddComponent<PlayerKartInput>()
                    : kart.AddComponent<AiKartInput>();
                KartController controller = kart.AddComponent<KartController>();
                controller.Configure(tuning, input, roadMask);

                RaceProgress progress = kart.AddComponent<RaceProgress>();
                progress.Configure(
                    raceManager,
                    track.Path,
                    controller,
                    names[i],
                    i == 0,
                    track.CheckpointCount,
                    position,
                    rotation);

                KartVisualRig rig = CreateKartVisuals(kart.transform, palette.KartBodies[i], palette);
                KartVisuals visuals = kart.AddComponent<KartVisuals>();
                visuals.Configure(
                    controller,
                    rig.VisualRoot,
                    rig.Wheels,
                    rig.FrontPivots,
                    rig.ExhaustParticles,
                    rig.DriftParticles,
                    rig.BoostParticles,
                    rig.ShieldVisual);

                KartItemInventory inventory = kart.AddComponent<KartItemInventory>();
                inventory.Configure(controller, progress, visuals);
                KartAudio audio = kart.AddComponent<KartAudio>();
                audio.Configure(controller);

                if (input is AiKartInput ai)
                {
                    float aiLane = i % 2 == 0 ? -0.9f : 0.9f;
                    ai.Configure(
                        track.Path,
                        controller,
                        progress,
                        inventory,
                        aiLane,
                        Mathf.Lerp(0.7f, 0.94f, (i - 1) / 6f),
                        roadMask);
                }

                result.Add(controller);
            }

            return result;
        }

        private static KartVisualRig CreateKartVisuals(
            Transform kartRoot,
            Material bodyMaterial,
            Palette palette)
        {
            GameObject visualRootObject = new("Visual Root");
            visualRootObject.transform.SetParent(kartRoot, false);
            Transform visualRoot = visualRootObject.transform;

            CreateVisualPrimitive(
                "Chassis",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0f, 0.38f, 0f),
                Quaternion.identity,
                new Vector3(1.34f, 0.34f, 1.82f),
                bodyMaterial,
                kartLayer);
            CreateVisualPrimitive(
                "Rounded Nose",
                PrimitiveType.Capsule,
                visualRoot,
                new Vector3(0f, 0.5f, 0.68f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.76f, 0.55f, 0.58f),
                bodyMaterial,
                kartLayer);
            CreateVisualPrimitive(
                "Front Bumper",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0f, 0.26f, 1.08f),
                Quaternion.identity,
                new Vector3(1.48f, 0.18f, 0.32f),
                palette.Cream,
                kartLayer);
            CreateVisualPrimitive(
                "Seat",
                PrimitiveType.Cube,
                visualRoot,
                new Vector3(0f, 0.67f, -0.34f),
                Quaternion.Euler(-10f, 0f, 0f),
                new Vector3(0.72f, 0.58f, 0.62f),
                palette.Dark,
                kartLayer);
            CreateVisualPrimitive(
                "Driver Torso",
                PrimitiveType.Capsule,
                visualRoot,
                new Vector3(0f, 1.0f, -0.18f),
                Quaternion.identity,
                new Vector3(0.38f, 0.45f, 0.38f),
                palette.Cream,
                kartLayer);
            CreateVisualPrimitive(
                "Helmet",
                PrimitiveType.Sphere,
                visualRoot,
                new Vector3(0f, 1.47f, -0.1f),
                Quaternion.identity,
                new Vector3(0.58f, 0.55f, 0.58f),
                bodyMaterial,
                kartLayer);
            CreateVisualPrimitive(
                "Visor",
                PrimitiveType.Sphere,
                visualRoot,
                new Vector3(0f, 1.5f, 0.12f),
                Quaternion.identity,
                new Vector3(0.43f, 0.24f, 0.22f),
                palette.Glass,
                kartLayer);

            List<Transform> wheels = new();
            List<Transform> frontPivots = new();
            Vector3[] wheelPositions =
            {
                new(-0.83f, 0.25f, 0.72f),
                new(0.83f, 0.25f, 0.72f),
                new(-0.83f, 0.25f, -0.68f),
                new(0.83f, 0.25f, -0.68f),
            };

            for (int i = 0; i < wheelPositions.Length; i++)
            {
                GameObject pivotObject = new($"Wheel Pivot {i + 1}");
                pivotObject.transform.SetParent(visualRoot, false);
                pivotObject.transform.localPosition = wheelPositions[i];
                Transform pivot = pivotObject.transform;
                if (i < 2)
                {
                    frontPivots.Add(pivot);
                }

                GameObject wheel = CreateVisualPrimitive(
                    $"Wheel {i + 1}",
                    PrimitiveType.Cylinder,
                    pivot,
                    Vector3.zero,
                    Quaternion.Euler(0f, 0f, 90f),
                    new Vector3(0.36f, 0.19f, 0.36f),
                    palette.Tire,
                    kartLayer);
                CreateVisualPrimitive(
                    $"Wheel Hub {i + 1}",
                    PrimitiveType.Cylinder,
                    wheel.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    new Vector3(0.62f, 1.04f, 0.62f),
                    palette.Cream,
                    kartLayer);
                wheels.Add(wheel.transform);
            }

            ParticleSystem exhaustLeft = CreateParticleSystem(
                "Exhaust Left",
                visualRoot,
                new Vector3(-0.34f, 0.35f, -1.03f),
                Quaternion.Euler(0f, 180f, 0f),
                palette.ParticleWarm,
                new Color(1f, 0.52f, 0.2f),
                0.28f,
                2.1f,
                0.13f);
            ParticleSystem exhaustRight = CreateParticleSystem(
                "Exhaust Right",
                visualRoot,
                new Vector3(0.34f, 0.35f, -1.03f),
                Quaternion.Euler(0f, 180f, 0f),
                palette.ParticleWarm,
                new Color(1f, 0.52f, 0.2f),
                0.28f,
                2.1f,
                0.13f);
            ParticleSystem driftLeft = CreateParticleSystem(
                "Drift Left",
                visualRoot,
                new Vector3(-0.76f, 0.18f, -0.65f),
                Quaternion.Euler(-10f, 180f, 0f),
                palette.ParticleCool,
                new Color(0.25f, 0.85f, 1f),
                0.34f,
                1.5f,
                0.12f);
            ParticleSystem driftRight = CreateParticleSystem(
                "Drift Right",
                visualRoot,
                new Vector3(0.76f, 0.18f, -0.65f),
                Quaternion.Euler(-10f, 180f, 0f),
                palette.ParticleCool,
                new Color(0.25f, 0.85f, 1f),
                0.34f,
                1.5f,
                0.12f);
            ParticleSystem boost = CreateParticleSystem(
                "Boost Trail",
                visualRoot,
                new Vector3(0f, 0.3f, -1.12f),
                Quaternion.Euler(0f, 180f, 0f),
                palette.ParticleCool,
                new Color(0.3f, 0.9f, 1f),
                0.48f,
                4.2f,
                0.2f);

            GameObject shield = CreateVisualPrimitive(
                "Cloud Shield",
                PrimitiveType.Sphere,
                visualRoot,
                new Vector3(0f, 0.82f, 0f),
                Quaternion.identity,
                new Vector3(2.05f, 1.45f, 2.65f),
                palette.Shield,
                kartLayer,
                true);
            MeshRenderer shieldRenderer = shield.GetComponent<MeshRenderer>();
            shieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shieldRenderer.receiveShadows = false;
            shield.SetActive(false);

            return new KartVisualRig
            {
                VisualRoot = visualRoot,
                Wheels = wheels.ToArray(),
                FrontPivots = frontPivots.ToArray(),
                ExhaustParticles = new[] { exhaustLeft, exhaustRight },
                DriftParticles = new[] { driftLeft, driftRight },
                BoostParticles = boost,
                ShieldVisual = shield,
            };
        }

        private static ParticleSystem CreateParticleSystem(
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Material material,
            Color color,
            float lifetime,
            float speed,
            float size)
        {
            GameObject particleObject = new(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = localPosition;
            particleObject.transform.localRotation = localRotation;
            ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 128;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return system;
        }

        private static void CreatePickupsAndBoostPads(TrackBuildResult track, Palette palette)
        {
            GameObject pickupRoot = new("Pickups and Boost Pads");
            Mesh diamond = GeometryFactory.CreateDiamondMesh(0.48f, 0.72f);
            SaveMeshAsset(diamond, GeneratedRoot + "/Meshes/PickupDiamond.asset");

            int[] pickupRows = { 16, 46, 78, 111, 141 };
            foreach (int rawIndex in pickupRows)
            {
                int index = rawIndex % track.Samples.Length;
                Vector3 point = track.Samples[index];
                Vector3 forward = GeometryFactory.GetForward(track.Samples, index);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                for (int lane = -1; lane <= 1; lane++)
                {
                    GameObject pickup = new($"Pickup {index:000}-{lane + 2}");
                    pickup.transform.SetParent(pickupRoot.transform, true);
                    pickup.transform.position = point + right * lane * 3.1f + Vector3.up * 1.2f;
                    pickup.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                    pickup.layer = itemLayer;
                    MeshFilter filter = pickup.AddComponent<MeshFilter>();
                    filter.sharedMesh = diamond;
                    MeshRenderer renderer = pickup.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = palette.Pickup;
                    BoxCollider trigger = pickup.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(1.15f, 1.5f, 1.15f);
                    ItemPickup itemPickup = pickup.AddComponent<ItemPickup>();
                    itemPickup.Configure(new Renderer[] { renderer }, trigger, 2.6f);
                }
            }

            int[] boostIndices = { 32, 68, 101, 132 };
            foreach (int rawIndex in boostIndices)
            {
                int index = rawIndex % track.Samples.Length;
                Vector3 point = track.Samples[index];
                Vector3 forward = GeometryFactory.GetForward(track.Samples, index);
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = $"Boost Pad {index:000}";
                pad.transform.SetParent(pickupRoot.transform, true);
                pad.transform.position = point + Vector3.up * 0.12f;
                pad.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
                pad.transform.localScale = new Vector3(4.2f, 0.09f, 1.45f);
                pad.layer = itemLayer;
                pad.GetComponent<MeshRenderer>().sharedMaterial = palette.Boost;
                BoostPad boostPad = pad.AddComponent<BoostPad>();
                boostPad.Configure(0.82f, 32.5f);
            }
        }

        private static void CreateCamera(KartController player, LayerMask environmentMask)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 420f;
            camera.fieldOfView = 58f;
            cameraObject.AddComponent<AudioListener>();

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;

            KartChaseCamera chaseCamera = cameraObject.AddComponent<KartChaseCamera>();
            chaseCamera.Configure(player.transform, player, environmentMask);
        }
    }
}
