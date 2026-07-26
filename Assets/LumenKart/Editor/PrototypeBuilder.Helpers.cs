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
        private static GameObject CreateVisualPrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material,
            int layer,
            bool disableShadows = false)
        {
            GameObject gameObject = GameObject.CreatePrimitive(primitiveType);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = localRotation;
            gameObject.transform.localScale = localScale;
            gameObject.layer = layer;

            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                if (disableShadows)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            return gameObject;
        }

        private static Image CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot,
            Color color)
        {
            GameObject panelObject = CreateUiObject(
                name,
                parent,
                anchorMin,
                anchorMax,
                anchoredPosition,
                size,
                pivot);
            Image image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor,
            Vector2 pivot)
        {
            GameObject textObject = CreateUiObject(
                name,
                parent,
                anchor,
                anchor,
                anchoredPosition,
                size,
                pivot);
            Text text = textObject.AddComponent<Text>();
            text.font = runtimeFont;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            GameObject gameObject = new(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return gameObject;
        }

        private static void SaveMeshAsset(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(mesh, path);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }

        private sealed class Palette
        {
            public Material Road;
            public Material CurbA;
            public Material CurbB;
            public Material Barrier;
            public Material Grass;
            public Material GrassLight;
            public Material Trunk;
            public Material LeafA;
            public Material LeafB;
            public Material Cloud;
            public Material Cream;
            public Material Dark;
            public Material Tire;
            public Material Glass;
            public Material Boost;
            public Material Pickup;
            public Material Pulse;
            public Material Trap;
            public Material Shield;
            public Material ParticleWarm;
            public Material ParticleCool;
            public Material[] KartBodies;
        }

        private sealed class TrackBuildResult
        {
            public GameObject Root;
            public TrackPath Path;
            public Vector3[] Samples;
            public float HalfWidth;
            public int CheckpointCount;
            public int StartSample;
        }

        private sealed class KartVisualRig
        {
            public Transform VisualRoot;
            public Transform[] Wheels;
            public Transform[] FrontPivots;
            public ParticleSystem[] ExhaustParticles;
            public ParticleSystem[] DriftParticles;
            public ParticleSystem BoostParticles;
            public GameObject ShieldVisual;
        }
    }
}
