using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Ghostline.Tests.Scene
{
    // Mock player updates provide frame presses; Editor-only input buffers do not.
    public sealed class ClearBestLapTests : InputTestFixture
    {
        [Test]
        public void CenteredBarsFollowHoldReleaseAndConfirmationTimeout()
        {
            using (var rig = new Rig())
            {
                rig.Step(0f, Key.Delete);
                Assert.That(rig.Panel("Hold").activeSelf, Is.True);
                Assert.That(rig.Fill("Hold/Progress/Fill").fillAmount, Is.Zero);
                rig.AssertPrompt(false);
                rig.Step(0.5f, Key.Delete);
                Assert.That(rig.Fill("Hold/Progress/Fill").fillAmount, Is.EqualTo(0.5f));
                rig.Step(0f);
                Assert.That(rig.Panel("Hold").activeSelf, Is.False);
                Assert.That(rig.Fill("Hold/Progress/Fill").fillAmount, Is.Zero);
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(true);
                Assert.That(rig.Panel("Hold").activeSelf, Is.False);
                Assert.That(rig.Fill("Confirmation/Timeout/Fill").fillAmount, Is.EqualTo(1f));
                rig.Step(2.5f);
                Assert.That(rig.Fill("Confirmation/Timeout/Fill").fillAmount, Is.EqualTo(0.5f));
                rig.Step(2.5f);
                rig.AssertPrompt(false);
                Assert.That(rig.Fill("Confirmation/Timeout/Fill").fillAmount, Is.Zero);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
            }
        }

        [Test]
        public void ConfirmHidesGhostAndDotImmediatelyWithoutChangingCurrentAttempt()
        {
            using (var rig = new Rig())
            {
                rig.Hud.ShowDelta(1f);
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(true);
                Assert.That(rig.Session.Timer.State, Is.EqualTo(LapTimerState.Running));
                float elapsed = rig.Session.Timer.ElapsedTime;
                Vector2 velocity = rig.Car.Body.linearVelocity;
                rig.Step(0.1f, Key.Y);
                rig.RefreshMinimap();
                Assert.That(rig.Storage.ClearCount, Is.EqualTo(1));
                Assert.That(rig.Storage.Data, Is.Null);
                Assert.That(rig.Ghost.HasRecording, Is.False);
                Assert.That(rig.GhostRenderer.enabled, Is.False);
                Assert.That(rig.GhostDot.enabled, Is.False);
                Assert.That(rig.Best.text, Is.EqualTo("Best  --"));
                rig.AssertResult("Best lap cleared");
                Assert.That(rig.Delta.text, Is.Empty);
                Assert.That(rig.Session.Timer.ElapsedTime, Is.EqualTo(elapsed));
                Assert.That(rig.Car.Body.linearVelocity, Is.EqualTo(velocity));
                Assert.That(rig.Car.CanDrive && rig.Car.InputEnabled, Is.True);
                rig.Step(1.9f);
                rig.AssertResult("Best lap cleared");
                rig.Step(0.2f);
                rig.AssertPrompt(false);
            }
        }

        [TestCase(Key.N)]
        [TestCase(Key.Escape)]
        public void DedicatedCancelKeysRetainBestAndGhost(Key key)
        {
            using (var rig = new Rig())
            {
                rig.Step(1f, Key.Delete);
                rig.Step(0.1f, key);
                rig.AssertPrompt(false);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
                Assert.That(rig.Ghost.HasRecording, Is.True);
                Assert.That(rig.Storage.Data.LapTime, Is.EqualTo(5f));
            }
        }

        [Test]
        public void EarlyDeleteReleaseAndTimeoutNeverDelete()
        {
            using (var rig = new Rig())
            {
                rig.Step(0.75f, Key.Delete);
                rig.Step(0.25f);
                rig.AssertPrompt(false);
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(true);
                rig.Step(5f);
                rig.AssertPrompt(false);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
                Assert.That(rig.Ghost.HasRecording, Is.True);
            }
        }

        [TestCase(Key.W)]
        [TestCase(Key.A)]
        [TestCase(Key.S)]
        [TestCase(Key.D)]
        [TestCase(Key.UpArrow)]
        [TestCase(Key.DownArrow)]
        [TestCase(Key.LeftArrow)]
        [TestCase(Key.RightArrow)]
        [TestCase(Key.R)]
        public void DrivingAndRestartKeysNeverConfirmOrCancel(Key key)
        {
            using (var rig = new Rig())
            {
                // Isolate key routing from the Editor's variable frame duration.
                Set(rig.Race, "_clearBestConfirmTimeout", 60f);
                rig.Step(1f, Key.Delete);
                rig.Keys(key);
                Invoke(rig.Race, "Update");
                rig.AssertPrompt(true);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
                Assert.That(rig.Storage.Data.LapTime, Is.EqualTo(5f));
                Assert.That(rig.Ghost.HasRecording, Is.True);
                if (key == Key.R)
                    Assert.That(rig.Session.Timer.State, Is.EqualTo(LapTimerState.NotStarted));
            }
        }

        [Test]
        public void AttemptTimerAndCheckpointProgressContinueWhilePromptIsOpen()
        {
            using (var rig = new Rig())
            {
                rig.Step(1f, Key.Delete);
                float elapsed = rig.Session.Timer.ElapsedTime;
                Invoke(rig.Race, "FixedUpdate");
                rig.Race.CrossTrigger(rig.Car, false, 0);
                rig.Step(0f);
                Assert.That(rig.Session.Timer.ElapsedTime, Is.GreaterThan(elapsed));
                Assert.That(rig.Session.Checkpoints.NextCheckpointIndex, Is.EqualTo(1));
                Assert.That(rig.Car.InputEnabled, Is.True);
                rig.AssertPrompt(true);
            }
        }

        [TestCase("io")]
        [TestCase("access")]
        [TestCase("security")]
        public void DeleteFailurePreservesBestGhostAndDotAndShowsBriefWarning(string failure)
        {
            using (var rig = new Rig())
            {
                rig.Storage.ClearFailure = failure == "io" ? (Exception)new IOException("Test delete failure")
                    : failure == "access" ? new UnauthorizedAccessException("Test delete failure")
                    : new System.Security.SecurityException("Test delete failure");
                rig.Step(1f, Key.Delete);
                LogAssert.Expect(LogType.Warning, "Ghostline could not clear its best lap: Test delete failure");
                rig.Step(0.1f, Key.Y);
                rig.RefreshMinimap();
                Assert.That(rig.Storage.Data.LapTime, Is.EqualTo(5f));
                Assert.That(rig.Best.text, Is.EqualTo("Best  5.00 s"));
                Assert.That(rig.Ghost.HasRecording && rig.GhostRenderer.enabled && rig.GhostDot.enabled, Is.True);
                rig.AssertResult("Could not clear best lap (see Console)");
                rig.Step(2.1f);
                rig.AssertPrompt(false);
            }
        }

        [Test]
        public void ClearingWithoutBestSucceedsAndNextValidLapBecomesBestAndGhost()
        {
            using (var rig = new Rig(false))
            {
                rig.Step(1f, Key.Delete);
                rig.Step(0.1f, Key.Y);
                rig.AssertResult("Best lap cleared");
                Assert.That(rig.Storage.ClearCount, Is.EqualTo(1));
                rig.CompleteLap();
                Assert.That(rig.Storage.Data.LapTime, Is.EqualTo(11f));
                rig.Race.Restart();
                Assert.That(rig.Ghost.HasRecording, Is.True);
            }
        }

        [Test]
        public void SlowerCompletedAttemptBecomesBestAndGhostAfterClear()
        {
            using (var rig = new Rig())
            {
                rig.Step(1f, Key.Delete);
                rig.Step(0.1f, Key.Y);
                rig.CompleteLap();
                Assert.That(rig.Storage.Data.LapTime, Is.EqualTo(11f));
                rig.Race.Restart();
                Assert.That(rig.Ghost.HasRecording, Is.True);
                Assert.That(rig.Ghost.PlaybackDuration, Is.EqualTo(11f));
            }
        }

        [Test]
        public void ManagerUsesSerializedHoldAndTimeoutDurations()
        {
            using (var rig = new Rig())
            {
                Set(rig.Race, "_clearBestHoldDuration", 2f);
                Set(rig.Race, "_clearBestConfirmTimeout", 3f);
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(false);
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(true);
                rig.Step(3f);
                rig.AssertPrompt(false);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
            }
        }

        [TestCase(Key.N)]
        [TestCase(Key.Escape)]
        public void PlayerQuitPromptResumesWithoutChangingAttemptOrGhost(Key cancel)
        {
            using (var rig = new Rig(player: true))
            {
                float elapsed = rig.Session.Timer.ElapsedTime;
                Vector2 velocity = rig.Car.Body.linearVelocity;
                rig.Step(0f, Key.Escape);
                rig.AssertQuitPrompt(true);
                Assert.That(rig.Session.Timer.ElapsedTime, Is.EqualTo(elapsed));
                Assert.That(rig.Car.Body.linearVelocity, Is.EqualTo(velocity));
                Vector3 ghostPosition = rig.Ghost.transform.position;
                rig.Step(0f);
                Invoke(rig.Race, "FixedUpdate");
                Invoke(rig.Race, "Update");
                Assert.That(rig.Ghost.transform.position.x, Is.GreaterThan(ghostPosition.x));
                rig.Race.CrossTrigger(rig.Car, false, 0);
                Assert.That(rig.Session.Timer.ElapsedTime, Is.GreaterThan(elapsed));
                Assert.That(rig.Session.Checkpoints.NextCheckpointIndex, Is.EqualTo(1));
                Assert.That(rig.Car.CanDrive && rig.Car.InputEnabled, Is.True);
                rig.Step(600f);
                rig.AssertQuitPrompt(true);
                rig.Step(0f, cancel);
                rig.AssertQuitPrompt(false);
                Assert.That(rig.QuitRequests, Is.Zero);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
                Assert.That(rig.Ghost.HasRecording && rig.GhostRenderer.enabled && rig.GhostDot.enabled, Is.True);
            }
        }

        [Test]
        public void PlayerConfirmationInvokesQuitCallbackOnce()
        {
            using (var rig = new Rig(player: true))
            {
                rig.Step(0f, Key.Escape);
                rig.Step(0f, Key.Y);
                rig.AssertQuitPrompt(false);
                Assert.That(rig.QuitRequests, Is.EqualTo(1));
                rig.Step(0f);
                rig.Step(0f, Key.Y);
                Assert.That(rig.QuitRequests, Is.EqualTo(1));
                Assert.That(rig.Storage.ClearCount, Is.Zero);
            }
        }

        [TestCase(Key.W)]
        [TestCase(Key.A)]
        [TestCase(Key.S)]
        [TestCase(Key.D)]
        [TestCase(Key.UpArrow)]
        [TestCase(Key.DownArrow)]
        [TestCase(Key.LeftArrow)]
        [TestCase(Key.RightArrow)]
        [TestCase(Key.R)]
        public void DrivingAndRestartKeysDoNotDismissQuitPrompt(Key key)
        {
            using (var rig = new Rig(player: true))
            {
                rig.Step(0f, Key.Escape);
                rig.Keys(key);
                Invoke(rig.Race, "Update");
                rig.AssertQuitPrompt(true);
                Assert.That(rig.QuitRequests, Is.Zero);
                if (key == Key.R)
                    Assert.That(rig.Session.Timer.State, Is.EqualTo(LapTimerState.NotStarted));
            }
        }

        [Test]
        public void EditorEscapeNeverCreatesQuitPromptOrRequestsQuit()
        {
            using (var rig = new Rig())
            {
                rig.Step(0f, Key.Escape);
                rig.Step(0f, Key.Y);
                Assert.That(rig.Root.transform.Find("Clear Best Lap Overlay"), Is.Null);
                Assert.That(rig.Session.Timer.State, Is.EqualTo(LapTimerState.Running));
                var prompts = new ConfirmationPrompts(new ClearBestLapFlow(), () => rig.QuitRequests++);
                prompts.Tick(0f, false, true, false, false);
                prompts.Tick(0f, false, false, true, false);
                Assert.That(prompts.Quit.State, Is.EqualTo(QuitState.Idle));
                Assert.That(rig.QuitRequests, Is.Zero);
            }
        }

        [Test]
        public void ClearPromptConsumesEscapeAndBothPromptsStayExclusive()
        {
            using (var rig = new Rig(player: true))
            {
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(true);
                rig.Step(0f, Key.Escape);
                rig.AssertPrompt(false);
                rig.AssertQuitPrompt(false);
                rig.Step(0f);
                rig.Step(0f, Key.Escape);
                rig.AssertQuitPrompt(true);
                rig.Step(2f, Key.Delete);
                rig.AssertQuitPrompt(true);
                rig.AssertPrompt(false);
                rig.Step(0f, Key.N, Key.Delete);
                rig.AssertQuitPrompt(false);
                rig.AssertPrompt(false);
                rig.Step(1f, Key.Delete);
                rig.AssertPrompt(true);
                rig.AssertQuitPrompt(false);
                Assert.That(rig.Storage.ClearCount, Is.Zero);
            }
        }

        [Test]
        public void SimultaneousDeleteHoldAndEscapePreferClearPrompt()
        {
            using (var rig = new Rig(player: true))
            {
                rig.Step(1f, Key.Delete, Key.Escape);
                rig.AssertPrompt(true);
                rig.AssertQuitPrompt(false);
                Assert.That(rig.QuitRequests, Is.Zero);
            }
        }

        [Test]
        public void ControlsHintIncludesQuitWithoutSceneChanges()
        {
            using (var rig = new Rig())
            {
                rig.Race.Restart();
                rig.Hud.Render(rig.Session, rig.Storage.Data, false);
                Assert.That(rig.Status.text, Does.Contain("WASD / arrows | R: restart | Esc: quit"));
            }
        }

        private static void Invoke(object target, string method, params object[] arguments)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, arguments);
        }

        private static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, field);
            info.SetValue(target, value);
        }

        private sealed class MemoryStorage : IBestLapStorage
        {
            internal BestLapData Data;
            internal Exception ClearFailure;
            internal int ClearCount;
            public BestLapData Load() => Data;
            public void Save(BestLapData data) => Data = data;
            public void Clear()
            {
                if (ClearFailure != null)
                    throw ClearFailure;
                Data = null;
                ClearCount++;
            }
        }

        private sealed class Rig : IDisposable
        {
            internal readonly GameObject Root = new GameObject("Clear best lap test", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            internal readonly Keyboard Keyboard = InputSystem.AddDevice<Keyboard>();
            internal readonly MemoryStorage Storage = new MemoryStorage();
            internal readonly RaceSession Session = new RaceSession(1);
            internal readonly RaceManager Race;
            internal readonly CarController Car;
            internal readonly GhostCarView Ghost;
            internal readonly SpriteRenderer GhostRenderer;
            internal readonly HudView Hud;
            internal readonly TMP_Text Best;
            internal readonly TMP_Text Status;
            internal readonly TMP_Text Delta;
            internal readonly Image GhostDot;
            private readonly MinimapView _minimap;
            internal int QuitRequests;

            internal Rig(bool hasBest = true, bool player = false)
            {
                Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = Root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1600f, 900f);
                var carObject = Child("Car", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CarController));
                Car = carObject.GetComponent<CarController>();
                Invoke(Car, "Awake");
                var ghostObject = Child("Ghost", typeof(SpriteRenderer), typeof(GhostCarView));
                Ghost = ghostObject.GetComponent<GhostCarView>();
                GhostRenderer = ghostObject.GetComponent<SpriteRenderer>();
                Best = Text("Best");
                Status = Text("Status");
                Delta = Text("Delta");
                Hud = Root.AddComponent<HudView>();
                Hud.Configure(Text("Current"), Best, Status, deltaText: Delta);
                Race = Root.AddComponent<RaceManager>();
                Race.Configure(Car, Ghost, Hud, null, 1);
                if (player)
                    Set(Race, "_prompts", new ConfirmationPrompts(new ClearBestLapFlow(),
                        () => QuitRequests++, () => false));
                Storage.Data = hasBest ? new BestLapData
                {
                    Version = BestLapData.CurrentVersion,
                    TrackId = BestLapData.DefaultTrackId,
                    LapTime = 5f,
                    Splits = new[] { 2f },
                    Samples = new List<GhostSample>
                    {
                        new GhostSample(0f, 0f, 0f, 0f), new GhostSample(5f, 1f, 0f, 0f)
                    }
                } : null;
                Set(Race, "_session", Session);
                Set(Race, "_repository", new BestLapRepository(Storage, 1));
                Set(Race, "_bestLap", Storage.Data);
                var sequence = (StartSequence)typeof(RaceManager).GetField("_startSequence",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Race);
                sequence.Tick(4f);
                Ghost.SetLap(Storage.Data);
                Session.CrossStartFinish(0f, 0f, 0f);
                Session.Tick(1f, 0f, 0f, 0f);
                Ghost.ShowAt(1f);
                Car.Body.linearVelocity = new Vector2(1f, 2f);
                _minimap = Child("Minimap", typeof(RectTransform), typeof(MinimapView)).GetComponent<MinimapView>();
                GhostDot = Child("Ghost dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                Set(_minimap, "_projection", new MinimapProjection(new TrackPoint(-10f, -10f),
                    new TrackPoint(10f, 10f), 64f, 64f, 4f));
                Set(_minimap, "_playerDot", Child("Player dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>());
                Set(_minimap, "_ghostDot", GhostDot);
                Set(_minimap, "_map", Child("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>());
                Set(_minimap, "_player", Car.transform);
                Set(_minimap, "_ghost", Ghost);
                Set(_minimap, "_race", Race);
                RefreshMinimap();
            }

            internal void Keys(params Key[] keys)
            {
                InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
                InputSystem.Update();
            }

            internal void Step(float seconds, params Key[] keys)
            {
                Keys(keys);
                Invoke(Race, "UpdatePrompts", seconds);
                var best = (BestLapData)typeof(RaceManager).GetField("_bestLap",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Race);
                Hud.Render(Session, best, false);
            }

            internal void RefreshMinimap() => Invoke(_minimap, "LateUpdate");

            internal GameObject Panel(string path) => Root.transform.Find("Clear Best Lap Overlay/" + path).gameObject;

            internal Image Fill(string path) => Panel(path).GetComponent<Image>();

            internal void AssertQuitPrompt(bool visible)
            {
                Assert.That(Panel("Quit").activeSelf, Is.EqualTo(visible));
                if (!visible)
                    return;
                Assert.That(Panel("Quit/Title").GetComponent<TMP_Text>().text, Is.EqualTo("Quit Ghostline?"));
                Assert.That(Panel("Quit/Keys").GetComponent<TMP_Text>().text, Is.EqualTo("[Y] Quit    [N / Esc] Resume"));
                Assert.That(Panel("Quit").GetComponent<RectTransform>().anchorMin, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(Panel("Hold").activeSelf || Panel("Confirmation").activeSelf || Panel("Result").activeSelf, Is.False);
                Assert.That(Panel("Quit").transform.Find("Timeout"), Is.Null);
            }

            internal void AssertPrompt(bool visible)
            {
                Transform overlay = Root.transform.Find("Clear Best Lap Overlay");
                Assert.That(overlay, Is.Not.Null);
                Assert.That(Panel("Confirmation").activeSelf, Is.EqualTo(visible));
                Assert.That(Panel("Result").activeSelf, Is.False);
                Assert.That(Status.text, Does.Not.Contain("Clear best lap"));
                if (visible)
                {
                    Assert.That(Panel("Confirmation/Title").GetComponent<TMP_Text>().text,
                        Is.EqualTo("Clear best lap and ghost?"));
                    Assert.That(Panel("Confirmation/Keys").GetComponent<TMP_Text>().text,
                        Is.EqualTo("[Y] Clear    [N / Esc] Cancel"));
                }
            }

            internal void AssertResult(string message)
            {
                Assert.That(Panel("Result").activeSelf, Is.True);
                Assert.That(Panel("Result/Message").GetComponent<TMP_Text>().text, Is.EqualTo(message));
                Assert.That(Panel("Confirmation").activeSelf || Panel("Hold").activeSelf, Is.False);
                Assert.That(Status.text, Does.StartWith("Next checkpoint:"));
            }

            internal void CompleteLap()
            {
                Race.CrossTrigger(Car, false, 0);
                Session.Tick(10f, 1f, 0f, 0f);
                Race.CrossTrigger(Car, true, 0);
            }

            private GameObject Child(string name, params Type[] components)
            {
                var child = new GameObject(name, components);
                child.transform.SetParent(Root.transform, false);
                return child;
            }

            private TMP_Text Text(string name) => Child(name, typeof(RectTransform),
                typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();

            public void Dispose()
            {
                InputSystem.RemoveDevice(Keyboard);
                Object.DestroyImmediate(Root);
            }
        }
    }
}
