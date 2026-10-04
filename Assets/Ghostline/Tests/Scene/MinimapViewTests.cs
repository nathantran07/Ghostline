using System;
using System.Collections.Generic;
using System.Reflection;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Ghostline.Tests.Scene
{
    public sealed class MinimapViewTests
    {
        private GameObject _root;
        private TrackGenerator _track;
        private Transform _player;
        private GhostCarView _ghost;
        private RaceManager _race;
        private RaceSession _session;
        private MinimapView _view;
        private RawImage _map;
        private Image _playerDot;
        private Image _ghostDot;
        private Image _background;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Minimap view test");
            var trackObject = Child("Track", _root.transform, typeof(TrackGenerator));
            _track = trackObject.GetComponent<TrackGenerator>();
            // Explicit centerline samples give the raster checks exact pixel locations.
            SetField(_track, "_samples", new[]
            {
                new TrackSample(new Vector2(-10f, -5f), Vector2.right, 0f),
                new TrackSample(new Vector2(10f, -5f), Vector2.up, 20f),
                new TrackSample(new Vector2(10f, 5f), Vector2.left, 30f),
                new TrackSample(new Vector2(-10f, 5f), Vector2.down, 50f)
            });
            _player = Child("Player", _root.transform).transform;
            var ghostObject = Child("Ghost", _root.transform);
            Child("Visual", ghostObject.transform, typeof(SpriteRenderer));
            _ghost = ghostObject.AddComponent<GhostCarView>();
            Invoke(_ghost, "Awake");
            _race = _root.AddComponent<RaceManager>();
            _race.Configure(null, _ghost, null, null, spawnPosition: new Vector2(-4f, -3f));
            _session = new RaceSession(12);
            SetField(_race, "_session", _session);
            var canvasObject = Child("Canvas", _root.transform, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = Child("Minimap", canvasObject.transform, typeof(RectTransform), typeof(Image), typeof(MinimapView));
            _background = panel.GetComponent<Image>();
            _map = Child("Track", panel.transform, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _playerDot = Child("Player dot", _map.transform, typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<Image>();
            _ghostDot = Child("Ghost dot", _map.transform, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _view = panel.GetComponent<MinimapView>();
            _view.Configure(_track, _player, _ghost, _race, _background, _map, _playerDot, _ghostDot);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null)
                Invoke(_view, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void DrawsClosedLoopAndTickOnceWithTransparentBackgroundAndDefaultMaterial()
        {
            Invoke(_view, "Start");
            var texture = (Texture2D)_map.texture;
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.GetPixel(128, 128).a, Is.Zero);
            Assert.That(texture.GetPixel(16, 128).a, Is.GreaterThan(0.5f), "The last-to-first segment must close.");
            Assert.That(texture.GetPixel(17, 130).a, Is.InRange(0.1f, 0.9f), "Three-pixel line includes smoothed edge pixels.");
            Assert.That(texture.GetPixel(16, 67).a, Is.GreaterThan(0.5f), "Start tick extends past the lower-left corner, in texture Y-up coordinates.");
            Assert.That(_map.material, Is.SameAs(Graphic.defaultGraphicMaterial));
            Assert.That(_playerDot.GetComponent<Outline>().effectColor.a, Is.EqualTo(1f));
            Invoke(_view, "LateUpdate");
            Invoke(_view, "LateUpdate");
            Assert.That(_map.texture, Is.SameAs(texture));
        }

        [Test]
        public void ProjectsWorldPositionsAndFlipsYConsistentlyBetweenTextureAndDots()
        {
            _track.transform.position = new Vector3(40f, -20f, 0f);
            _track.transform.localScale = new Vector3(2f, 3f, 1f);
            _player.position = _track.transform.TransformPoint(new Vector3(10f, 5f, 0f));
            Invoke(_view, "Start");
            Invoke(_view, "LateUpdate");
            float size = _map.rectTransform.rect.width;
            Assert.That(_playerDot.rectTransform.anchoredPosition.x, Is.EqualTo(size * 240f / 256f).Within(0.001f));
            Assert.That(_playerDot.rectTransform.anchoredPosition.y, Is.EqualTo(-size * 44f / 256f).Within(0.001f));
            _player.position = new Vector3(10000f, -10000f, 0f);
            Invoke(_view, "LateUpdate");
            Assert.That(_playerDot.rectTransform.anchoredPosition.x, Is.EqualTo(size * 240f / 256f).Within(0.001f));
            Assert.That(_playerDot.rectTransform.anchoredPosition.y, Is.EqualTo(-size * 240f / 256f).Within(0.001f));
        }

        [Test]
        public void GhostAppearsAtSpawnDuringCountdownThenFollowsPlaybackAndHidesAtEnd()
        {
            _ghost.SetLap(Lap());
            Invoke(_view, "Start");
            Invoke(_view, "LateUpdate");
            Assert.That(_ghostDot.enabled, Is.True);
            Assert.That(_race.GhostMinimapPosition, Is.EqualTo(new Vector2(-4f, -3f)));
            Assert.That(_ghostDot.color.a, Is.EqualTo(0.5f));
            Assert.That(_ghost.transform.position, Is.EqualTo(Vector3.zero), "The minimap must not move the world ghost.");
            _session.Timer.Start();
            _session.Timer.Tick(0.5f);
            _ghost.ShowAt(0.5f);
            Invoke(_view, "LateUpdate");
            Assert.That(_race.GhostMinimapPosition, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(_ghostDot.enabled, Is.True);
            _session.Timer.Tick(0.5f);
            Invoke(_view, "LateUpdate");
            Assert.That(_ghostDot.enabled, Is.False, "Playback ends at the saved recording's duration.");
            _session.Reset();
            Invoke(_view, "LateUpdate");
            Assert.That(_ghostDot.enabled, Is.True, "Restart shows spawn again.");
            _session.Timer.Start();
            _session.Timer.Finish();
            Invoke(_view, "LateUpdate");
            Assert.That(_ghostDot.enabled, Is.False, "Finishing the attempt ends playback even before the best time.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingRecordingOrGhostObjectHidesDot(bool missingObject)
        {
            if (missingObject)
            {
                UnityEngine.Object.DestroyImmediate(_ghost.gameObject);
                _view.Configure(_track, _player, null, _race, _background, _map, _playerDot, _ghostDot);
            }
            Invoke(_view, "Start");
            Invoke(_view, "LateUpdate");
            Assert.That(_ghostDot.enabled, Is.False);
            Assert.That(_playerDot.enabled, Is.True);
        }

        [Test]
        public void ResizeKeepsSameTextureAndDotAlignmentAtThirtyPercentCanvasHeight()
        {
            Invoke(_view, "Start");
            _player.position = Vector3.zero;
            Invoke(_view, "LateUpdate");
            Texture texture = _map.texture;
            RectTransform canvas = (RectTransform)_map.canvas.transform;
            Assert.That(_map.rectTransform.rect.height, Is.EqualTo(canvas.rect.height * 0.3f).Within(0.01f));
            SetField(_view, "_heightFraction", 0.2f);
            Invoke(_view, "LateUpdate");
            float size = _map.rectTransform.rect.height;
            Assert.That(size, Is.EqualTo(canvas.rect.height * 0.2f).Within(0.01f));
            Assert.That(_playerDot.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(size * 0.5f, -size * 0.5f)));
            Assert.That(_map.texture, Is.SameAs(texture));
        }

        [Test]
        public void DestroyReleasesRuntimeTexture()
        {
            Invoke(_view, "Start");
            Texture texture = _map.texture;
            // EditMode does not drive a plain MonoBehaviour's Play lifecycle.
            Invoke(_view, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(_view.gameObject);
            Assert.That(texture == null, Is.True);
        }

        private static BestLapData Lap()
        {
            return new BestLapData
            {
                LapTime = 1f,
                Samples = new List<GhostSample> { new GhostSample(0f, -2f, 0f, 0f), new GhostSample(1f, 2f, 2f, 0f) }
            };
        }

        private static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
