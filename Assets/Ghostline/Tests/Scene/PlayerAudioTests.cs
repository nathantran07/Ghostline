using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Ghostline.Tests.Scene
{
    public sealed class PlayerAudioTests
    {
        [TestCase("EngineAudio")]
        [TestCase("SfxPlayer")]
        [TestCase("PlayerAudioSettings")]
        [TestCase("PlayerAudioOutput")]
        public void PlayerAudioComponentsExist(string component)
        {
            Assert.That(typeof(RaceManager).Assembly.GetType("Ghostline.Game." + component), Is.Not.Null);
        }

        [Test]
        public void ConservativeVolumesAndMuteStateDoNotChangeConfiguredLevels()
        {
            using (var rig = new Rig())
            {
                Assert.That(rig.Settings.MasterVolume, Is.EqualTo(0.5f));
                Assert.That(rig.Settings.EngineVolume, Is.EqualTo(0.4f));
                Assert.That(rig.Settings.SfxVolume, Is.EqualTo(0.6f));
                rig.Settings.ToggleMute();
                Assert.That(rig.Settings.IsMuted, Is.True);
                rig.Settings.ToggleMute();
                Assert.That(rig.Settings.IsMuted, Is.False);
                Assert.That(rig.Settings.MasterVolume, Is.EqualTo(0.5f));
            }
        }

        [Test]
        public void CountdownPublishesEachPlayerCueOnceAndRestartBeginsAtThree()
        {
            using (var rig = new Rig())
            {
                var cues = new List<int>();
                int restarts = 0;
                rig.Race.CountdownCue += cues.Add;
                rig.Race.Restarted += () => restarts++;
                rig.Race.Restart();
                for (int i = 0; i < 200; i++)
                    Invoke(rig.Race, "FixedUpdate");
                Assert.That(cues, Is.EqualTo(new[] { 3, 2, 1, 0 }));
                rig.Race.Restart();
                Assert.That(restarts, Is.EqualTo(2));
                Assert.That(cues[cues.Count - 1], Is.EqualTo(3));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AcceptedPlayerFinishPublishesOneChimeAndGhostCannotPublish(bool failSave)
        {
            using (var rig = new Rig())
            {
                var completions = new List<bool>();
                rig.Race.LapCompleted += completions.Add;
                rig.Storage.FailSave = failSave;
                rig.Race.CrossTrigger(null, true, 0);
                Assert.That(completions, Is.Empty);
                if (failSave)
                    LogAssert.Expect(LogType.Warning, "Ghostline could not save its best lap: Audio test save failure");
                Finish(rig, 2f);
                rig.Race.CrossTrigger(rig.Car, true, 0);
                Assert.That(completions, Is.EqualTo(new[] { !failSave }));
                Assert.That(rig.Car.CanDrive, Is.False);
                Assert.That(rig.Ghost.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                if (!failSave)
                {
                    Finish(rig, 3f);
                    Assert.That(completions, Is.EqualTo(new[] { true, false }));
                }
            }
        }

        [Test]
        public void AudioConfigurationRejectsForeignCarsAndSharedEngineSource()
        {
            using (var rig = new Rig())
            {
                var foreign = new GameObject("Foreign car", typeof(CarController), typeof(EngineAudio));
                try
                {
                    Assert.Throws<ArgumentException>(() => foreign.GetComponent<EngineAudio>().Configure(rig.Race));
                    Assert.Throws<ArgumentException>(() => rig.Sfx.Configure(rig.Race, rig.EngineSource));
                }
                finally
                {
                    Object.DestroyImmediate(foreign);
                }
            }
        }

        [UnityTest]
        public IEnumerator EngineAndFinalMixActuallyRunAndReenableRestartsCarrier()
        {
            yield return new EnterPlayMode();
            using (var listeners = new OtherListeners())
            using (var rig = new Rig())
            {
                // Unity begins recording output history on the first query.
                var samples = new float[1024];
                AudioListener.GetOutputData(samples, 0);
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(rig.EngineSource.isPlaying, Is.True);
                Assert.That(rig.EngineSource.loop, Is.True);
                Assert.That(rig.EngineSource.clip, Is.Not.Null);
                Assert.That(rig.EngineSource.clip.hideFlags, Is.EqualTo(HideFlags.DontSave));
                Assert.That(rig.Engine.RenderedBufferCount, Is.GreaterThan(0), "The real audio thread must render the engine.");
                Assert.That(rig.Output.ProcessedBufferCount, Is.GreaterThan(0), "Final listener mixing must run.");
                AudioListener.GetOutputData(samples, 0);
                Assert.That(Array.Exists(samples, sample => Math.Abs(sample) > 0.0001f), Is.True,
                    "A playing silent carrier alone is insufficient: synthesized output must be audible.");
                Assert.That(samples, Is.All.InRange(-0.85f, 0.85f));
                rig.Engine.enabled = false;
                Assert.That(rig.EngineSource.isPlaying, Is.False);
                Assert.That(rig.EngineSource.clip, Is.Null);
                rig.Engine.enabled = true;
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.That(rig.EngineSource.isPlaying, Is.True);
                Assert.That(rig.Sfx.Source.transform.parent, Is.SameAs(rig.Car.transform));
                Assert.That(rig.Ghost.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MutingRampsFinalMixToSilenceAndSuppressesNewEffects()
        {
            yield return new EnterPlayMode();
            using (var listeners = new OtherListeners())
            using (var rig = new Rig())
            {
                var samples = new float[1024];
                AudioListener.GetOutputData(samples, 0);
                yield return new WaitForSecondsRealtime(0.2f);
                rig.Settings.ToggleMute();
                rig.Race.Restart();
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(rig.Sfx.Source.isPlaying, Is.False, "Muted countdowns must not queue SFX.");
                AudioListener.GetOutputData(samples, 0);
                foreach (float sample in samples)
                    Assert.That(Math.Abs(sample), Is.LessThan(0.00001f));
                rig.Settings.ToggleMute();
                yield return new WaitForSecondsRealtime(0.2f);
                AudioListener.GetOutputData(samples, 0);
                Assert.That(Array.Exists(samples, sample => Math.Abs(sample) > 0.0001f), Is.True);
                rig.Race.Restart();
                Assert.That(rig.Sfx.Source.isPlaying, Is.True);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StoppedEngineSourceWarnsOnceWithoutWarningSpam()
        {
            yield return new EnterPlayMode();
            using (var listeners = new OtherListeners())
            using (var rig = new Rig())
            {
                yield return null;
                rig.EngineSource.Stop();
                LogAssert.Expect(LogType.Warning, "Ghostline engine AudioSource is not playing; synthesized engine audio cannot run. Check that the source is enabled and the audio device is available.");
                yield return null;
                yield return null;
                LogAssert.NoUnexpectedReceived();
                rig.EngineSource.Play();
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RealWallContactReportsIncomingSpeedOnceAndRetainsExistingPhysics()
        {
            yield return new EnterPlayMode();
            var scene = SceneManager.CreateScene("Audio wall verification " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var carObject = new GameObject("Wall audio player", typeof(CarController));
            SceneManager.MoveGameObjectToScene(carObject, scene);
            var track = new GameObject("Wall owner", typeof(TrackGenerator));
            SceneManager.MoveGameObjectToScene(track, scene);
            var wall = new GameObject("Wall", typeof(EdgeCollider2D));
            wall.transform.SetParent(track.transform, false);
            wall.GetComponent<EdgeCollider2D>().points = new[] { Vector2.left * 10f, Vector2.right * 10f };
            try
            {
                CarController car = carObject.GetComponent<CarController>();
                Invoke(car, "Awake");
                Set(car, "_drag", 0f);
                Set(car, "_engineBraking", 0f);
                Set(car, "_wallSpeedLoss", 0.35f);
                car.GetComponent<BoxCollider2D>().size = new Vector2(0.55f, 0.1f);
                car.Body.position = Vector2.up * 0.18f;
                car.Body.linearVelocity = Vector2.down * 18f;
                var impacts = new List<float>();
                car.WallImpacted += impacts.Add;
                Physics2D.SyncTransforms();
                for (int i = 0; i < 10 && car.Body.linearVelocity.y < 0f; i++)
                {
                    Invoke(car, "FixedUpdate");
                    scene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime);
                }
                Assert.That(impacts, Has.Count.EqualTo(1));
                Assert.That(impacts[0], Is.EqualTo(18f).Within(0.01f));
                Assert.That(car.Body.linearVelocity.y, Is.EqualTo(11.7f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(carObject);
                Object.DestroyImmediate(track);
                SceneManager.UnloadSceneAsync(scene);
            }
            yield return new ExitPlayMode();
        }

        private static void Finish(Rig rig, float lapTime)
        {
            rig.Race.Restart();
            var sequence = Get<StartSequence>(rig.Race, "_startSequence");
            sequence.Tick(4f);
            rig.Race.CrossTrigger(rig.Car, true, 0);
            var session = Get<RaceSession>(rig.Race, "_session");
            session.Tick(lapTime * 0.5f, 0f, 0f, 0f);
            rig.Race.CrossTrigger(rig.Car, false, 0);
            session.Tick(lapTime * 0.5f, 0f, 0f, 0f);
            rig.Race.CrossTrigger(rig.Car, true, 0);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static T Get<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private sealed class MemoryStorage : IBestLapStorage
        {
            private BestLapData _data;
            internal bool FailSave;
            public BestLapData Load() => _data;
            public void Save(BestLapData data)
            {
                if (FailSave)
                    throw new IOException("Audio test save failure");
                _data = data;
            }
        }

        private sealed class Rig : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly CarController Car;
            internal readonly GhostCarView Ghost;
            internal readonly RaceManager Race;
            internal readonly PlayerAudioSettings Settings;
            internal readonly EngineAudio Engine;
            internal readonly AudioSource EngineSource;
            internal readonly SfxPlayer Sfx;
            internal readonly PlayerAudioOutput Output;
            internal readonly MemoryStorage Storage = new MemoryStorage();

            internal Rig()
            {
                Root = new GameObject("Player audio test");
                Root.SetActive(false);
                var carObject = Child("Car", Root.transform);
                Car = carObject.AddComponent<CarController>();
                Car.enabled = false;
                EngineSource = carObject.AddComponent<AudioSource>();
                Settings = carObject.AddComponent<PlayerAudioSettings>();
                Engine = carObject.AddComponent<EngineAudio>();
                Sfx = carObject.AddComponent<SfxPlayer>();
                AudioSource sfxSource = Child("Audio SFX", carObject.transform).AddComponent<AudioSource>();
                var ghostObject = Child("Ghost", Root.transform);
                Child("Visual", ghostObject.transform).AddComponent<SpriteRenderer>();
                Ghost = ghostObject.AddComponent<GhostCarView>();
                HudView hud = Root.AddComponent<HudView>();
                Race = Root.AddComponent<RaceManager>();
                Race.enabled = false;
                Race.Configure(Car, Ghost, hud, null, 1);
                var camera = Child("Audio test camera", Root.transform);
                camera.AddComponent<Camera>();
                camera.AddComponent<AudioListener>();
                Output = camera.AddComponent<PlayerAudioOutput>();
                Set(Race, "_session", new RaceSession(1));
                Set(Race, "_repository", new BestLapRepository(Storage, 1));
                Engine.Configure(Race);
                Sfx.Configure(Race, sfxSource);
                Output.Configure(Settings);
                Root.SetActive(true);
                if (!Application.isPlaying)
                    Invoke(Car, "Awake");
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
            }

            private static GameObject Child(string name, Transform parent)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                return child;
            }
        }

        private sealed class OtherListeners : IDisposable
        {
            private readonly AudioListener[] _listeners;
            private readonly bool[] _enabled;
            internal OtherListeners()
            {
                _listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                _enabled = new bool[_listeners.Length];
                for (int i = 0; i < _listeners.Length; i++)
                {
                    _enabled[i] = _listeners[i].enabled;
                    _listeners[i].enabled = false;
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < _listeners.Length; i++)
                    if (_listeners[i] != null)
                        _listeners[i].enabled = _enabled[i];
            }
        }
    }
}
