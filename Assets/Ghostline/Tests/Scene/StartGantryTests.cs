using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Ghostline.Core;
using Ghostline.Editor;
using Ghostline.Game;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Ghostline.Tests.Scene
{
    public sealed class StartGantryTests
    {
        [TestCase(0f, 1, true)]
        [TestCase(0.599f, 1, true)]
        [TestCase(0.6f, 2, true)]
        [TestCase(1.199f, 2, true)]
        [TestCase(1.2f, 3, true)]
        [TestCase(1.799f, 3, true)]
        [TestCase(1.8f, 4, true)]
        [TestCase(2.399f, 4, true)]
        [TestCase(2.4f, 5, true)]
        [TestCase(2.999f, 5, true)]
        [TestCase(3f, 0, true)]
        [TestCase(3.749f, 0, true)]
        [TestCase(3.75f, 0, false)]
        [TestCase(100f, 0, false)]
        public void RuntimeGantryFollowsBoundariesAndRestartsWithoutDuplicating(float seconds, int lamps, bool visible)
        {
            var root = new GameObject("Gantry HUD test", typeof(RectTransform), typeof(HudView));
            try
            {
                var textObject = new GameObject("Countdown", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(root.transform, false);
                TMP_Text countdown = textObject.GetComponent<TMP_Text>();
                HudView hud = root.GetComponent<HudView>();
                hud.Configure(null, null, null, countdown);
                var sequence = new StartSequence();
                sequence.Tick(seconds);
                hud.RenderCountdown(sequence);
                StartGantryView gantry = countdown.GetComponentInChildren<StartGantryView>(true);
                Assert.That(gantry, Is.Not.Null);
                Assert.That(gantry.GetComponent<CanvasRenderer>(), Is.Not.Null);
                Assert.That(gantry.LitLampCount, Is.EqualTo(lamps));
                Assert.That(gantry.gameObject.activeSelf, Is.EqualTo(visible));
                Assert.That(gantry.raycastTarget, Is.False);
                Assert.That(countdown.text, Is.Empty);
                Assert.That(countdown.enabled, Is.False);
                Assert.That(gantry.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(gantry.rectTransform.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(gantry.rectTransform.anchorMax, Is.EqualTo(Vector2.one * 0.5f));
                sequence.Reset();
                hud.RenderCountdown(sequence);
                Assert.That(countdown.GetComponentsInChildren<StartGantryView>(true), Has.Length.EqualTo(1));
                Assert.That(gantry.LitLampCount, Is.EqualTo(1));
                Assert.That(gantry.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HudWithoutCountdownReferenceDoesNotCreateGantry()
        {
            var root = new GameObject("Optional countdown test", typeof(HudView));
            try
            {
                root.GetComponent<HudView>().RenderCountdown(new StartSequence());
                Assert.That(root.GetComponentsInChildren<StartGantryView>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GantryRendersOneThreeFiveAndLightsOutAndExportsPreviews()
        {
            string path = "Assets/GhostlineGantryVerification-" + Guid.NewGuid().ToString("N") + ".unity";
            byte[] sourceBytes = File.ReadAllBytes(GhostlineSceneBuilder.ScenePath);
            Assert.That(AssetDatabase.CopyAsset(GhostlineSceneBuilder.ScenePath, path), Is.True);
            UnityScene scene = default;
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            Camera camera = null;
            Canvas canvas = null;
            RenderTexture previousTarget = null;
            RenderMode previousMode = RenderMode.ScreenSpaceOverlay;
            Camera previousCamera = null;
            float previousPlane = 0f;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Transform root = scene.GetRootGameObjects().Single(g => g.name == "Ghostline").transform;
                HudView hud = root.Find("HUD Canvas").GetComponent<HudView>();
                canvas = hud.GetComponent<Canvas>();
                camera = root.Find("Main Camera").GetComponent<Camera>();
                previousTarget = camera.targetTexture;
                previousMode = canvas.renderMode;
                previousCamera = canvas.worldCamera;
                previousPlane = canvas.planeDistance;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                // Preserve overlay ordering when routing this HUD through a camera for export.
                canvas.sortingOrder = short.MaxValue;
                Canvas.ForceUpdateCanvases();
                MinimapView minimap = root.GetComponentInChildren<MinimapView>();
                if (minimap != null)
                    typeof(MinimapView).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(minimap, null);
                float[] times = { 0f, 1.2f, 2.4f, 3f };
                int[] lamps = { 1, 3, 5, 0 };
                string[] names = { "GantryLamp1.png", "GantryLamp3.png", "GantryLamp5.png", "GantryLightsOut.png" };
                int firstRedPixels = 0;
                for (int i = 0; i < times.Length; i++)
                {
                    var sequence = new StartSequence();
                    sequence.Tick(times[i]);
                    hud.RenderCountdown(sequence);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                    image.Apply();
                    Color32[] pixels = image.GetPixels32();
                    int redPixels = 0;
                    // Sample the lamp centers, excluding red track paint and other world visuals.
                    for (int lamp = 0; lamp < 5; lamp++)
                        for (int y = 440; y < 460; y++)
                            for (int x = 790 + (lamp - 2) * 72; x < 810 + (lamp - 2) * 72; x++)
                            {
                                Color32 pixel = pixels[y * target.width + x];
                                if (pixel.r > 180 && pixel.g < 80 && pixel.b < 80)
                                    redPixels++;
                            }
                    if (i == 0)
                    {
                        firstRedPixels = redPixels;
                        Assert.That(firstRedPixels, Is.GreaterThan(300), "The active lamp must render bright red.");
                    }
                    Assert.That(redPixels, Is.EqualTo(firstRedPixels * lamps[i]), "Only the expected lamps should be bright.");
                    if (Application.isBatchMode)
                    {
                        Directory.CreateDirectory("Logs");
                        File.WriteAllBytes(Path.Combine("Logs", names[i]), image.EncodeToPNG());
                    }
                }
            }
            finally
            {
                if (camera != null)
                    camera.targetTexture = previousTarget;
                if (canvas != null)
                {
                    canvas.renderMode = previousMode;
                    canvas.worldCamera = previousCamera;
                    canvas.planeDistance = previousPlane;
                }
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(path);
            }
            Assert.That(File.ReadAllBytes(GhostlineSceneBuilder.ScenePath), Is.EqualTo(sourceBytes));
        }
    }
}
