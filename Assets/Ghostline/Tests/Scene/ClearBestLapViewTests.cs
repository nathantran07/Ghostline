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
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace Ghostline.Tests.Scene
{
    public sealed class ClearBestLapViewTests
    {
        [TestCase(1600f, 900f)]
        [TestCase(1200f, 900f)]
        [TestCase(2100f, 900f)]
        [TestCase(800f, 450f)]
        public void RuntimePanelFitsCanvasAndClearsMinimapWithoutBlockingInput(float width, float height)
        {
            var root = new GameObject("Clear presentation test", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(HudView));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 7;
                ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
                var textObject = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(root.transform, false);
                TMP_Text status = textObject.GetComponent<TMP_Text>();
                status.fontSize = 22f;
                HudView hud = root.GetComponent<HudView>();
                hud.Configure(null, null, status);
                var flow = new ClearBestLapFlow();
                flow.Tick(1f, true, false, false);
                hud.RenderClearBest(flow);
                hud.RenderClearBest(flow);
                Canvas.ForceUpdateCanvases();
                ClearBestLapView[] views = root.GetComponentsInChildren<ClearBestLapView>(true);
                Assert.That(views, Has.Length.EqualTo(1));
                ClearBestLapView view = views[0];
                Canvas overlay = view.GetComponent<Canvas>();
                Assert.That(overlay.overrideSorting, Is.True);
                Assert.That(overlay.sortingOrder, Is.GreaterThan(canvas.sortingOrder));
                Assert.That(view.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(view.GetComponent<GraphicRaycaster>(), Is.Null);
                Assert.That(view.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                Assert.That(view.GetComponentsInChildren<Image>(true).All(image => image.material == image.defaultMaterial), Is.True);
                RectTransform panel = (RectTransform)view.transform.Find("Confirmation");
                Assert.That(panel.gameObject.activeSelf, Is.True);
                Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.4f)));
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root.transform, panel);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-width * 0.5f));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(width * 0.5f));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-height * 0.5f));
                // Even the largest permitted minimap occupies the top 40%, with a 24-unit inset.
                Assert.That(bounds.max.y, Is.LessThan(height * 0.1f - 24f));
                TMP_Text title = panel.Find("Title").GetComponent<TMP_Text>();
                Assert.That(title.fontSize, Is.GreaterThan(status.fontSize));
                Assert.That(title.fontStyle, Is.EqualTo(FontStyles.Bold));
                Color backing = panel.Find("Backing").GetComponent<Image>().color;
                Assert.That(backing.a, Is.InRange(0.5f, 0.99f));
                Assert.That(backing.r, Is.LessThan(0.1f));
                Color border = panel.Find("Border Top").GetComponent<Image>().color;
                Assert.That(border.r > border.g && border.g > border.b, Is.True);

                var quit = new QuitFlow();
                quit.Tick(true, false, false, true);
                hud.RenderQuit(quit);
                hud.RenderQuit(quit);
                Canvas.ForceUpdateCanvases();
                RectTransform quitPanel = (RectTransform)view.transform.Find("Quit");
                Assert.That(root.GetComponentsInChildren<ClearBestLapView>(true), Has.Length.EqualTo(1));
                Assert.That(quitPanel.gameObject.activeSelf, Is.True);
                Assert.That(panel.gameObject.activeSelf, Is.False);
                Assert.That(quitPanel.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(quitPanel.localScale, Is.EqualTo(panel.localScale));
                Assert.That(quitPanel.Find("Backing").GetComponent<Image>().color, Is.EqualTo(backing));
                Assert.That(quitPanel.Find("Border Top").GetComponent<Image>().color, Is.EqualTo(border));
                Assert.That(quitPanel.Find("Title").GetComponent<TMP_Text>().fontSize, Is.EqualTo(title.fontSize));
                bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root.transform, quitPanel);
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.center.y, Is.EqualTo(0f).Within(0.01f));
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-width * 0.5f));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(width * 0.5f));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-height * 0.5f));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(height * 0.5f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BarsRenderActualHalfFillsAndCenteredMessagesExportPreviewsWithoutChangingScene()
        {
            string path = "Assets/GhostlineClearPresentation-" + Guid.NewGuid().ToString("N") + ".unity";
            byte[] sourceBytes = File.ReadAllBytes(GhostlineSceneBuilder.ScenePath);
            Assert.That(AssetDatabase.CopyAsset(GhostlineSceneBuilder.ScenePath, path), Is.True);
            UnityScene scene = default;
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                Transform root = scene.GetRootGameObjects().Single(g => g.name == "Ghostline").transform;
                HudView hud = root.Find("HUD Canvas").GetComponent<HudView>();
                Canvas canvas = hud.GetComponent<Canvas>();
                camera = root.Find("Main Camera").GetComponent<Camera>();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 30000;
                Canvas.ForceUpdateCanvases();
                MinimapView minimap = root.GetComponentInChildren<MinimapView>();
                if (minimap != null)
                    typeof(MinimapView).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(minimap, null);
                var flow = new ClearBestLapFlow();
                flow.Tick(0.5f, true, false, false);
                hud.RenderClearBest(flow);
                Export("ClearBestHold.png", camera, target, image);
                AssertHalfBar(hud.transform.Find("Clear Best Lap Overlay/Hold/Progress"), camera, image);
                flow.Tick(0.5f, true, false, false);
                flow.Tick(2.5f, false, false, false);
                hud.RenderClearBest(flow);
                Export("ClearBestConfirm.png", camera, target, image);
                AssertHalfBar(hud.transform.Find("Clear Best Lap Overlay/Confirmation/Timeout"), camera, image);
                flow.Tick(0f, false, true, false);
                hud.RenderClearBest(flow);
                hud.ShowClearBestResult(true);
                Export("ClearBestSuccess.png", camera, target, image);
                hud.ShowClearBestResult(false);
                Export("ClearBestWarning.png", camera, target, image);
                var quit = new QuitFlow();
                quit.Tick(true, false, false, true);
                hud.RenderQuit(quit);
                Export("QuitConfirm.png", camera, target, image);
                Transform overlay = hud.transform.Find("Clear Best Lap Overlay");
                Assert.That(overlay.Find("Result").gameObject.activeSelf, Is.False);
                RectTransform quitBorder = overlay.Find("Quit/Border Bottom").GetComponent<RectTransform>();
                Vector3 borderPixel = camera.WorldToScreenPoint(quitBorder.TransformPoint(Vector3.zero));
                Color renderedBorder = image.GetPixel(Mathf.RoundToInt(borderPixel.x), Mathf.RoundToInt(borderPixel.y));
                Assert.That(renderedBorder.r, Is.GreaterThan(0.8f), "The quit panel border must render amber.");
                quit.Tick(false, false, true, true);
                hud.RenderQuit(quit);
                Assert.That(overlay.Find("Quit").gameObject.activeSelf, Is.False);
                hud.TickClearBestMessage(2f);
                Assert.That(hud.transform.Find("Clear Best Lap Overlay/Result").gameObject.activeSelf, Is.False);
            }
            finally
            {
                if (camera != null)
                    camera.targetTexture = null;
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

        private static void Export(string name, Camera camera, RenderTexture target, Texture2D image)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            image.Apply();
            if (Application.isBatchMode)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(Path.Combine("Logs", name), image.EncodeToPNG());
            }
        }

        private static void AssertHalfBar(Transform bar, Camera camera, Texture2D image)
        {
            RectTransform rectangle = (RectTransform)bar;
            Vector3 filled = camera.WorldToScreenPoint(rectangle.TransformPoint(
                new Vector3(rectangle.rect.width * -0.25f, 0f, 0f)));
            Vector3 empty = camera.WorldToScreenPoint(rectangle.TransformPoint(
                new Vector3(rectangle.rect.width * 0.25f, 0f, 0f)));
            Color filledPixel = image.GetPixel(Mathf.RoundToInt(filled.x), Mathf.RoundToInt(filled.y));
            Color emptyPixel = image.GetPixel(Mathf.RoundToInt(empty.x), Mathf.RoundToInt(empty.y));
            Assert.That(filledPixel.r, Is.GreaterThan(0.8f), "The filled half must render amber.");
            Assert.That(filledPixel.g, Is.GreaterThan(0.4f));
            Assert.That(emptyPixel.r, Is.LessThan(0.4f), "The empty half must render the dark track.");
        }
    }
}
