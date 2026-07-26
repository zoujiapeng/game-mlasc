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
        private static RaceHUD CreateHud(Palette palette)
        {
            runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont == null)
            {
                runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject canvasObject = new("Race HUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            Color panelColor = new(0.075f, 0.1f, 0.12f, 0.66f);
            Color primaryText = new(0.94f, 0.94f, 0.89f, 1f);
            Color accentText = new(0.5f, 0.83f, 0.86f, 1f);

            Image infoPanel = CreatePanel(
                "Race Info Panel",
                canvasObject.transform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(38f, -36f),
                new Vector2(330f, 132f),
                new Vector2(0f, 1f),
                panelColor);
            Text lap = CreateText(
                "Lap",
                infoPanel.transform,
                "LAP  1 / 3",
                28,
                TextAnchor.UpperLeft,
                primaryText,
                new Vector2(20f, -16f),
                new Vector2(290f, 46f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            Text timer = CreateText(
                "Timer",
                infoPanel.transform,
                "00:00.00",
                40,
                TextAnchor.LowerLeft,
                accentText,
                new Vector2(20f, 12f),
                new Vector2(290f, 64f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f));

            Image positionPanel = CreatePanel(
                "Position Panel",
                canvasObject.transform,
                Vector2.one,
                Vector2.one,
                new Vector2(-42f, -36f),
                new Vector2(300f, 132f),
                Vector2.one,
                panelColor);
            Text position = CreateText(
                "Position",
                positionPanel.transform,
                "1<size=42> / 8</size>",
                82,
                TextAnchor.MiddleRight,
                primaryText,
                new Vector2(-18f, 0f),
                new Vector2(260f, 108f),
                Vector2.one,
                Vector2.one);

            Image itemPanel = CreatePanel(
                "Item Panel",
                canvasObject.transform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-46f, 42f),
                new Vector2(270f, 112f),
                new Vector2(1f, 0f),
                panelColor);
            Text item = CreateText(
                "Item",
                itemPanel.transform,
                "EMPTY",
                29,
                TextAnchor.MiddleCenter,
                accentText,
                Vector2.zero,
                new Vector2(238f, 80f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));

            Text help = CreateText(
                "Controls Help",
                canvasObject.transform,
                "WASD / ARROWS  DRIVE     SPACE  DRIFT     E  ITEM     R  RESTART",
                20,
                TextAnchor.MiddleLeft,
                new Color(0.92f, 0.92f, 0.88f, 0.82f),
                new Vector2(42f, 34f),
                new Vector2(760f, 48f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f));

            Image driftBackground = CreatePanel(
                "Drift Charge",
                canvasObject.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 38f),
                new Vector2(390f, 26f),
                new Vector2(0.5f, 0f),
                new Color(0.07f, 0.09f, 0.1f, 0.72f));
            GameObject driftFillObject = CreateUiObject(
                "Drift Fill",
                driftBackground.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                new Vector2(0f, 0.5f));
            Image driftFill = driftFillObject.AddComponent<Image>();
            driftFill.color = new Color(0.45f, 0.82f, 0.87f, 0.95f);
            driftFill.type = Image.Type.Filled;
            driftFill.fillMethod = Image.FillMethod.Horizontal;
            driftFill.fillOrigin = 0;
            driftFill.fillAmount = 0f;
            Text driftTier = CreateText(
                "Drift Tier",
                canvasObject.transform,
                "DRIFT",
                18,
                TextAnchor.MiddleCenter,
                primaryText,
                new Vector2(0f, 68f),
                new Vector2(220f, 34f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f));

            Text status = CreateText(
                "Status",
                canvasObject.transform,
                string.Empty,
                38,
                TextAnchor.MiddleCenter,
                new Color(0.94f, 0.54f, 0.42f),
                new Vector2(0f, 185f),
                new Vector2(520f, 64f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f));

            Text countdown = CreateText(
                "Countdown",
                canvasObject.transform,
                "3",
                148,
                TextAnchor.MiddleCenter,
                primaryText,
                Vector2.zero,
                new Vector2(520f, 220f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            countdown.gameObject.SetActive(false);

            Image resultsPanel = CreatePanel(
                "Results Panel",
                canvasObject.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(720f, 580f),
                new Vector2(0.5f, 0.5f),
                new Color(0.055f, 0.075f, 0.09f, 0.94f));
            Text results = CreateText(
                "Results",
                resultsPanel.transform,
                string.Empty,
                30,
                TextAnchor.MiddleCenter,
                primaryText,
                Vector2.zero,
                new Vector2(650f, 520f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            resultsPanel.gameObject.SetActive(false);

            Image pausePanel = CreatePanel(
                "Pause Panel",
                canvasObject.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(520f, 210f),
                new Vector2(0.5f, 0.5f),
                new Color(0.055f, 0.075f, 0.09f, 0.9f));
            CreateText(
                "Paused Label",
                pausePanel.transform,
                "PAUSED\n<size=24>PRESS ESC TO CONTINUE</size>",
                48,
                TextAnchor.MiddleCenter,
                primaryText,
                Vector2.zero,
                new Vector2(470f, 160f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            pausePanel.gameObject.SetActive(false);

            RaceHUD hud = canvasObject.AddComponent<RaceHUD>();
            hud.Configure(
                position,
                lap,
                timer,
                countdown,
                item,
                status,
                driftTier,
                driftFill,
                resultsPanel.gameObject,
                results,
                pausePanel.gameObject);
            return hud;
        }
    }
}
