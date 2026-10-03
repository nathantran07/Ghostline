using System;
using System.Collections.Generic;
using System.IO;
using Ghostline.Core;
using Ghostline.Game;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode.Storage
{
    public sealed class JsonFileBestLapStorageTests
    {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GhostlineTests", Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "best-lap.json");
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_directory, true);
        }

        [Test]
        public void VersionThreeRoundTripsSplitsAndReplacesExistingFile()
        {
            var storage = new JsonFileBestLapStorage(_path, 2);
            Assert.That(storage.Load(), Is.Null);
            storage.Save(CreateLap(4f));
            BestLapData lap = CreateLap(2f);
            storage.Save(lap);
            Assert.That(File.ReadAllText(_path), Does.Contain("\"Version\": 3"));
            BestLapData loaded = new JsonFileBestLapStorage(_path, 2).Load();
            Assert.That(loaded.LapTime, Is.EqualTo(lap.LapTime));
            Assert.That(loaded.Splits, Is.EqualTo(lap.Splits));
            Assert.That(loaded.Samples, Is.EqualTo(lap.Samples));
            Assert.That(File.Exists(_path + ".tmp"), Is.False);
            Assert.That(new JsonFileBestLapStorage(_path, 3).Load(), Is.Null);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void OldVersionIsIgnoredEvenWithOtherwiseValidData(int version)
        {
            var storage = new JsonFileBestLapStorage(_path, 2);
            storage.Save(CreateLap(2f));
            string json = File.ReadAllText(_path).Replace("\"Version\": 3", "\"Version\": " + version);
            File.WriteAllText(_path, json);
            Assert.That(storage.Load(), Is.Null);
        }

        [TestCase("")]
        [TestCase(",\"Splits\":null")]
        [TestCase(",\"Splits\":[1]")]
        [TestCase(",\"Splits\":[1,0.5]")]
        [TestCase(",\"Splits\":[1,3]")]
        public void InvalidOrMissingSplitsAreIgnored(string splitsJson)
        {
            File.WriteAllText(_path, "{\"Version\":3,\"LapTime\":2" + splitsJson + ",\"Samples\":["
                + "{\"Time\":0,\"X\":0,\"Y\":0,\"Rotation\":0},"
                + "{\"Time\":2,\"X\":1,\"Y\":0,\"Rotation\":90}]}");
            Assert.That(new JsonFileBestLapStorage(_path, 2).Load(), Is.Null);
        }

        [Test]
        public void InvalidSplitsCannotOverwriteValidSave()
        {
            var storage = new JsonFileBestLapStorage(_path, 2);
            storage.Save(CreateLap(4f));
            BestLapData invalid = CreateLap(2f);
            invalid.Splits = new[] { 1f, 3f };
            Assert.Throws<ArgumentException>(() => storage.Save(invalid));
            Assert.That(storage.Load().LapTime, Is.EqualTo(4f));
        }

        private static BestLapData CreateLap(float duration)
        {
            return new BestLapData
            {
                LapTime = duration,
                Splits = new[] { duration * 0.25f, duration * 0.75f },
                Samples = new List<GhostSample>
                {
                    new GhostSample(0f, 3f, 8f, 45f), new GhostSample(duration, 4f, 9f, 90f)
                }
            };
        }
    }
}
