using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Ghostline.Tests.Scene
{
    public sealed class RaceConfigurationTests
    {
        [TestCase(1)]
        [TestCase(4)]
        [TestCase(12)]
        public void HudShowsConfiguredProgressAndFinishInstruction(int count)
        {
            var root = new GameObject("HUD configuration test", typeof(RectTransform));
            var statusObject = new GameObject("Status", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusObject.transform.SetParent(root.transform, false);
            try
            {
                HudView hud = root.AddComponent<HudView>();
                TMP_Text status = statusObject.GetComponent<TMP_Text>();
                hud.Configure(null, null, status);
                var session = new RaceSession(count);
                session.CrossStartFinish(0f, 0f, 0f);
                hud.Render(session, null, false);
                Assert.That(status.text, Is.EqualTo($"Next checkpoint: 1 / {count}"));
                for (int i = 0; i < count; i++)
                    session.PassCheckpoint(i);
                hud.Render(session, null, false);
                Assert.That(status.text, Is.EqualTo("All checkpoints passed. Cross the white finish line."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RestartRestoresConfiguredPoseMotionAndCamera()
        {
            var root = new GameObject("Restart configuration test");
            try
            {
                var carObject = new GameObject("Car", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CarController));
                carObject.transform.SetParent(root.transform, false);
                CarController car = carObject.GetComponent<CarController>();
                // EditMode does not run the normal Play lifecycle; initialize only the car body.
                typeof(CarController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(car, null);
                var ghostObject = new GameObject("Ghost");
                ghostObject.transform.SetParent(root.transform, false);
                new GameObject("Visual", typeof(SpriteRenderer)).transform.SetParent(ghostObject.transform, false);
                GhostCarView ghost = ghostObject.AddComponent<GhostCarView>();
                var cameraObject = new GameObject("Camera", typeof(Camera), typeof(CameraFollow));
                cameraObject.transform.SetParent(root.transform, false);
                CameraFollow camera = cameraObject.GetComponent<CameraFollow>();
                camera.Configure(car.transform);
                HudView hud = root.AddComponent<HudView>();
                RaceManager race = root.AddComponent<RaceManager>();
                race.Configure(car, ghost, hud, camera, 12, new Vector2(3f, 8f), -140f);
                var session = new RaceSession(12);
                session.CrossStartFinish(0f, 0f, 0f);
                session.Tick(1f, 0f, 0f, 0f);
                session.PassCheckpoint(0);
                typeof(RaceManager).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(race, session);
                car.Body.linearVelocity = Vector2.one * 3f;
                car.Body.angularVelocity = 40f;
                car.CanDrive = false;
                race.Restart();
                Assert.That(car.Body.position, Is.EqualTo(new Vector2(3f, 8f)));
                Assert.That(Mathf.DeltaAngle(car.Body.rotation, -140f), Is.Zero.Within(0.001f));
                Assert.That(car.Body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(car.Body.angularVelocity, Is.Zero);
                Assert.That(car.CanDrive, Is.True);
                Assert.That(car.InputEnabled, Is.False);
                Assert.That(camera.transform.position, Is.EqualTo(new Vector3(3f, 8f, -10f)));
                Assert.That(session.Timer.State, Is.EqualTo(LapTimerState.NotStarted));
                Assert.That(session.Checkpoints.NextCheckpointIndex, Is.Zero);
                Assert.That(session.Checkpoints.CheckpointCount, Is.EqualTo(12));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ManagerDefaultsToTwelveCheckpointsAndAcceptsConfiguredSpawn()
        {
            var root = new GameObject("Race configuration test");
            try
            {
                RaceManager manager = root.AddComponent<RaceManager>();
                var serialized = new SerializedObject(manager);
                Assert.That(serialized.FindProperty("_checkpointCount").intValue, Is.EqualTo(12));
                manager.Configure(null, null, null, null, 4, new Vector2(3f, 8f), 45f);
                serialized.Update();
                Assert.That(serialized.FindProperty("_checkpointCount").intValue, Is.EqualTo(4));
                Assert.That(serialized.FindProperty("_spawnPosition").vector2Value, Is.EqualTo(new Vector2(3f, 8f)));
                Assert.That(serialized.FindProperty("_spawnRotation").floatValue, Is.EqualTo(45f));
                manager.Configure(null, null, null, null);
                serialized.Update();
                Assert.That(serialized.FindProperty("_checkpointCount").intValue, Is.EqualTo(12));
                Assert.That(serialized.FindProperty("_spawnPosition").vector2Value, Is.EqualTo(new Vector2(3f, 8f)));
                Assert.That(serialized.FindProperty("_spawnRotation").floatValue, Is.EqualTo(-90f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ManagerRejectsNonPositiveCheckpointCounts(int count)
        {
            var root = new GameObject("Invalid race configuration test");
            try
            {
                RaceManager manager = root.AddComponent<RaceManager>();
                Assert.Throws<ArgumentOutOfRangeException>(() => manager.Configure(null, null, null, null, count));
                var serialized = new SerializedObject(manager);
                Assert.That(serialized.FindProperty("_checkpointCount").intValue, Is.EqualTo(12));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VersionOneRectangleLapIsIgnoredAndVersionFourLapRoundTrips()
        {
            string directory = Path.Combine(Path.GetTempPath(), "GhostlineTests", Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "best-lap.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path, "{\"Version\":1,\"LapTime\":1,\"Samples\":["
                    + "{\"Time\":0,\"X\":0,\"Y\":0,\"Rotation\":0},"
                    + "{\"Time\":1,\"X\":1,\"Y\":0,\"Rotation\":90}]}");
                var storage = new JsonFileBestLapStorage(path, 2);
                Assert.That(storage.Load(), Is.Null);
                var lap = new BestLapData
                {
                    Version = BestLapData.CurrentVersion,
                    TrackId = BestLapData.DefaultTrackId,
                    LapTime = 2f,
                    Splits = new[] { 0.5f, 1.5f },
                    Samples = new List<GhostSample>
                    {
                        new GhostSample(0f, 3f, 8f, 45f), new GhostSample(2f, 4f, 9f, 90f)
                    }
                };
                storage.Save(lap);
                Assert.That(File.ReadAllText(path), Does.Contain("\"Version\": " + BestLapData.CurrentVersion));
                BestLapData loaded = new JsonFileBestLapStorage(path, 2).Load();
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.LapTime, Is.EqualTo(lap.LapTime));
                Assert.That(loaded.Splits, Is.EqualTo(lap.Splits));
                Assert.That(loaded.Samples.Count, Is.EqualTo(lap.Samples.Count));
                for (int i = 0; i < lap.Samples.Count; i++)
                {
                    Assert.That(loaded.Samples[i].Time, Is.EqualTo(lap.Samples[i].Time));
                    Assert.That(loaded.Samples[i].X, Is.EqualTo(lap.Samples[i].X));
                    Assert.That(loaded.Samples[i].Y, Is.EqualTo(lap.Samples[i].Y));
                    Assert.That(loaded.Samples[i].Rotation, Is.EqualTo(lap.Samples[i].Rotation));
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
