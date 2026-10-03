using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Ghostline.Tests.Scene
{
    public sealed class CountdownAndDeltaTests
    {
        [Test]
        public void HudFormatsDeltaFadesAndReplacesPreviousValue()
        {
            var root = new GameObject("Delta HUD test", typeof(RectTransform));
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                TMP_Text delta = CreateText(root.transform, "Delta");
                TMP_Text countdown = CreateText(root.transform, "Countdown");
                HudView hud = root.AddComponent<HudView>();
                hud.Configure(null, null, null, countdown, delta);
                var sequence = new StartSequence();
                hud.RenderCountdown(sequence);
                Assert.That(countdown.text, Is.EqualTo("3"));
                Assert.That(countdown.enabled, Is.True);
                sequence.Tick(4f);
                hud.RenderCountdown(sequence);
                Assert.That(countdown.enabled, Is.False);
                hud.ShowDelta(-0.142f);
                Assert.That(delta.text, Is.EqualTo("-0.142"));
                Assert.That(delta.color, Is.EqualTo(Color.green));
                hud.Tick(2.5f);
                Assert.That(delta.color.a, Is.EqualTo(0.5f).Within(0.001f));
                hud.ShowDelta(0.312f);
                Assert.That(delta.text, Is.EqualTo("+0.312"));
                Assert.That(delta.color, Is.EqualTo(Color.red));
                hud.Tick(2.5f);
                Assert.That(delta.enabled, Is.True);
                hud.Tick(0.5f);
                Assert.That(delta.enabled, Is.False);
                Assert.That(delta.text, Is.Empty);
                hud.ShowDelta(0f);
                Assert.That(delta.text, Is.EqualTo("+0.000"));
                Assert.That(delta.color, Is.EqualTo(Color.white));
                hud.ShowDelta(-0.0004f);
                Assert.That(delta.text, Is.EqualTo("-0.000"));
                Assert.That(delta.color, Is.EqualTo(Color.green));
                hud.ShowDelta(null);
                Assert.That(delta.text, Is.Empty);
                Assert.That(delta.enabled, Is.False);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisabledInputStopsMotionWithoutChangingHandling()
        {
            var root = new GameObject("Input lock test", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CarController));
            try
            {
                CarController car = root.GetComponent<CarController>();
                Invoke(car, "Awake");
                car.Body.position = new Vector2(2f, 3f);
                car.Body.rotation = 45f;
                car.Body.linearVelocity = Vector2.one * 3f;
                car.Body.angularVelocity = 40f;
                SetField(car, "_throttle", 1f);
                SetField(car, "_turn", 1f);
                car.InputEnabled = false;
                Invoke(car, "FixedUpdate");
                Invoke(car, "Update");
                Assert.That(car.Body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(car.Body.angularVelocity, Is.Zero);
                Assert.That(car.Body.position, Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(car.Body.rotation, Is.EqualTo(45f));
                Assert.That(GetField<float>(car, "_throttle"), Is.Zero);
                Assert.That(GetField<float>(car, "_turn"), Is.Zero);
                Assert.That(car.CanDrive, Is.True);
                Assert.That(car.Body.linearDamping, Is.Zero);
                Assert.That(GetField<float>(car, "_drag"), Is.EqualTo(0.85f));
                Assert.That(car.Body.angularDamping, Is.EqualTo(8f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(8f, "-2.000", 8f)]
        [TestCase(12f, "+2.000", 10f)]
        [TestCase(10f, "+0.000", 10f)]
        [TestCase(8f, "", 8f, false)]
        public void ManagerUsesPreviousBestAtFinishAndRestartsCountdown(float lapTime, string expectedDelta,
            float expectedBest, bool hasBest = true)
        {
            var root = new GameObject("Countdown race test", typeof(RectTransform));
            try
            {
                var carObject = new GameObject("Car", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CarController));
                carObject.transform.SetParent(root.transform, false);
                CarController car = carObject.GetComponent<CarController>();
                Invoke(car, "Awake");
                var ghostObject = new GameObject("Ghost");
                ghostObject.transform.SetParent(root.transform, false);
                var visual = new GameObject("Visual", typeof(SpriteRenderer));
                visual.transform.SetParent(ghostObject.transform, false);
                GhostCarView ghost = ghostObject.AddComponent<GhostCarView>();
                TMP_Text countdown = CreateText(root.transform, "Countdown");
                TMP_Text delta = CreateText(root.transform, "Delta");
                HudView hud = root.AddComponent<HudView>();
                hud.Configure(null, null, null, countdown, delta);
                RaceManager manager = root.AddComponent<RaceManager>();
                manager.Configure(car, ghost, hud, null, 1);
                var session = new RaceSession(1);
                var storage = new MemoryStorage();
                BestLapData best = hasBest ? CreateBest() : null;
                storage.Data = best;
                SetField(manager, "_session", session);
                SetField(manager, "_repository", new BestLapRepository(storage, 1));
                SetField(manager, "_bestLap", best);
                manager.Restart();
                Assert.That(car.InputEnabled, Is.False);
                Assert.That(countdown.text, Is.EqualTo("3"));
                manager.CrossTrigger(car, true, 0);
                Assert.That(session.Timer.State, Is.EqualTo(LapTimerState.NotStarted));
                for (int i = 0; i < 150; i++)
                    Invoke(manager, "FixedUpdate");
                Assert.That(car.InputEnabled, Is.True, "Countdown must unlock after 150 configured fixed ticks.");
                Assert.That(session.Timer.ElapsedTime, Is.Zero);
                Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
                manager.CrossTrigger(car, true, 0);
                Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.EqualTo(hasBest));
                session.Tick(lapTime * 0.5f, 0f, 0f, 0f);
                manager.CrossTrigger(car, false, 0);
                string gateDelta = delta.text;
                hud.Tick(0.5f);
                manager.CrossTrigger(car, false, 0);
                Assert.That(delta.text, Is.EqualTo(gateDelta));
                session.Tick(lapTime * 0.5f, 0f, 0f, 0f);
                manager.CrossTrigger(car, true, 0);
                Invoke(manager, "Update");
                Assert.That(delta.text, Is.EqualTo(expectedDelta));
                Assert.That(storage.Data.LapTime, Is.EqualTo(expectedBest));
                Assert.That(car.CanDrive, Is.False);
                manager.Restart();
                Assert.That(car.CanDrive, Is.True);
                Assert.That(car.InputEnabled, Is.False);
                Assert.That(countdown.text, Is.EqualTo("3"));
                Assert.That(delta.text, Is.Empty);
                Assert.That(visual.GetComponent<SpriteRenderer>().enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TMP_Text CreateText(Transform parent, string name)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<TMP_Text>();
        }

        private static BestLapData CreateBest()
        {
            return new BestLapData
            {
                Version = BestLapData.CurrentVersion,
                TrackId = BestLapData.DefaultTrackId,
                LapTime = 10f,
                Splits = new[] { 6f },
                Samples = new List<GhostSample>
                {
                    new GhostSample(0f, 0f, 0f, 0f), new GhostSample(10f, 1f, 0f, 0f)
                }
            };
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static void SetField(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static T GetField<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private sealed class MemoryStorage : IBestLapStorage
        {
            public BestLapData Data { get; set; }

            public BestLapData Load()
            {
                return Data;
            }

            public void Save(BestLapData data)
            {
                Data = data;
            }
        }
    }
}
