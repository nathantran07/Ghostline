using System;
using System.Linq;
using Ghostline.Editor;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ghostline.Tests.Scene
{
    public sealed class MinimapInstallerTests
    {
        private UnityEngine.SceneManagement.Scene _scene;
        private GameObject _root;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            // Preview scenes also work when the user's active scene is untitled or unsaved.
            _scene = EditorSceneManager.NewPreviewScene();
            _root = new GameObject("Minimap installer test");
            SceneManager.MoveGameObjectToScene(_root, _scene);
            var track = Child("Track", _root.transform, typeof(TrackGenerator)).GetComponent<TrackGenerator>();
            var car = Child("Car", _root.transform, typeof(CarController)).GetComponent<CarController>();
            var ghostObject = Child("Ghost", _root.transform);
            Child("Visual", ghostObject.transform, typeof(SpriteRenderer));
            GhostCarView ghost = ghostObject.AddComponent<GhostCarView>();
            var canvasObject = Child("HUD Canvas", _root.transform, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudView));
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            _root.AddComponent<RaceManager>().Configure(car, ghost, canvasObject.GetComponent<HudView>(), null);
            Assert.That(track.Samples.Count, Is.Zero, "Installing must not need to generate the track.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid())
                EditorSceneManager.ClosePreviewScene(_scene);
        }

        [Test]
        public void RunningTwiceReusesObjectsAndPreservesSerializedTuningAndCanvasScaler()
        {
            MinimapView first = GhostlineMinimapInstaller.AddToScene(_scene);
            var serialized = new SerializedObject(first);
            serialized.FindProperty("_heightFraction").floatValue = 0.25f;
            serialized.FindProperty("_playerColor").colorValue = Color.green;
            serialized.ApplyModifiedProperties();
            int objectCount = _root.GetComponentsInChildren<Transform>(true).Length;
            MinimapView second = GhostlineMinimapInstaller.AddToScene(_scene);
            Assert.That(second, Is.SameAs(first));
            Assert.That(_root.GetComponentsInChildren<MinimapView>(true), Has.Length.EqualTo(1));
            Assert.That(_root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
            Assert.That(first.transform.parent, Is.SameAs(_canvas.transform));
            Assert.That(first.GetComponentsInChildren<RawImage>(), Has.Length.EqualTo(1));
            Assert.That(first.GetComponentsInChildren<Image>(), Has.Length.EqualTo(3));
            serialized.Update();
            Assert.That(serialized.FindProperty("_heightFraction").floatValue, Is.EqualTo(0.25f));
            Assert.That(first.transform.Find("Track/Player Dot").GetComponent<Image>().color, Is.EqualTo(Color.green));
            Assert.That(_canvas.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1600f, 900f)));
            Assert.That(_scene.isDirty, Is.True);
            Assert.That(_scene.path, Is.Empty, "The installer must not save or overwrite any scene.");
        }

        [Test]
        public void WiresAllReferencesUsesDefaultSpritesAndPlacesLargerPanelAtTopRight()
        {
            MinimapView view = GhostlineMinimapInstaller.AddToScene(_scene);
            var serialized = new SerializedObject(view);
            foreach (string field in new[] { "_track", "_player", "_ghost", "_race", "_background", "_map", "_playerDot", "_ghostDot" })
                Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            RectTransform panel = (RectTransform)view.transform;
            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(panel.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(panel.pivot, Is.EqualTo(Vector2.one));
            Assert.That(panel.anchoredPosition, Is.EqualTo(new Vector2(-24f, -24f)));
            Rect canvasRect = ((RectTransform)_canvas.transform).rect;
            Assert.That(panel.rect.height, Is.EqualTo(canvasRect.height * 0.3f).Within(0.01f));
            Assert.That(canvasRect.width - panel.rect.width + panel.anchoredPosition.x,
                Is.GreaterThan(480f), "The panel must stay right of the lap time and delta at the reference resolution.");
            Image[] images = view.GetComponentsInChildren<Image>();
            foreach (Image image in images)
            {
                Assert.That(image.material, Is.SameAs(Graphic.defaultGraphicMaterial));
                Assert.That(image.raycastTarget, Is.False);
            }
            foreach (Image dot in images.Where(image => image.gameObject != view.gameObject))
                Assert.That(dot.sprite, Is.SameAs(AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")));
            Assert.That(view.transform.Find("Track/Player Dot").GetComponent<Outline>(), Is.Not.Null);
            Assert.That(view.transform.Find("Track/Ghost Dot").GetComponent<Image>().enabled, Is.False);
            Assert.That(view.transform.Find("Track/Ghost Dot").GetSiblingIndex(),
                Is.GreaterThan(view.transform.Find("Track/Player Dot").GetSiblingIndex()),
                "The translucent ghost must be visible over the bright player at the shared countdown spawn.");
        }

        [Test]
        public void ReinstallRepairsDeletedDotWithoutAddingAnotherPanel()
        {
            MinimapView view = GhostlineMinimapInstaller.AddToScene(_scene);
            UnityEngine.Object.DestroyImmediate(view.transform.Find("Track/Ghost Dot").gameObject);
            Assert.That(GhostlineMinimapInstaller.AddToScene(_scene), Is.SameAs(view));
            Assert.That(view.transform.Find("Track/Ghost Dot"), Is.Not.Null);
            Assert.That(_root.GetComponentsInChildren<MinimapView>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void MissingGhostIsSupported()
        {
            UnityEngine.Object.DestroyImmediate(_root.GetComponentInChildren<GhostCarView>().gameObject);
            MinimapView view = GhostlineMinimapInstaller.AddToScene(_scene);
            Assert.That(new SerializedObject(view).FindProperty("_ghost").objectReferenceValue, Is.Null);
        }

        [Test]
        public void MissingRaceFailsBeforeCreatingUi()
        {
            UnityEngine.Object.DestroyImmediate(_root.GetComponent<RaceManager>());
            int objectCount = _root.GetComponentsInChildren<Transform>(true).Length;
            Assert.Throws<InvalidOperationException>(() => GhostlineMinimapInstaller.AddToScene(_scene));
            Assert.That(_root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
        }

        [Test]
        public void AmbiguousTrackFailsBeforeCreatingUi()
        {
            Child("Another track", _root.transform, typeof(TrackGenerator));
            Assert.Throws<InvalidOperationException>(() => GhostlineMinimapInstaller.AddToScene(_scene));
            Assert.That(_root.GetComponentsInChildren<MinimapView>(), Is.Empty);
        }

        [Test]
        public void InstallationIsUndoableAsOneOperation()
        {
            GhostlineMinimapInstaller.AddToScene(_scene);
            Undo.PerformUndo();
            Assert.That(_root.GetComponentsInChildren<MinimapView>(), Is.Empty);
            Assert.That(_root.GetComponentInChildren<TrackGenerator>(), Is.Not.Null);
            Assert.That(_root.GetComponent<RaceManager>(), Is.Not.Null);
        }

        private static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
