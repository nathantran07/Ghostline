using System;
using System.Collections.Generic;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class BestLapRepositoryTests
    {
        [Test]
        public void SavesFirstLapAndReplacesOnlyWhenStrictlyFaster()
        {
            var storage = new InMemoryStorage();
            var repository = new BestLapRepository(storage, 2);
            Assert.That(repository.Load(), Is.Null);
            Assert.That(repository.TrySave(CreateLap(10f)), Is.True);
            Assert.That(repository.TrySave(CreateLap(11f)), Is.False);
            Assert.That(repository.TrySave(CreateLap(10f)), Is.False);
            Assert.That(repository.TrySave(CreateLap(9f)), Is.True);
            Assert.That(storage.SaveCount, Is.EqualTo(2));
            Assert.That(repository.Load().LapTime, Is.EqualTo(9f));
        }

        [Test]
        public void RoundTripsThroughStorageWithoutSharingMutableLists()
        {
            var storage = new InMemoryStorage();
            var candidate = CreateLap(4f);
            new BestLapRepository(storage, 2).TrySave(candidate);
            candidate.Splits[0] = 99f;
            BestLapData loaded = new BestLapRepository(storage, 2).Load();
            Assert.That(loaded.LapTime, Is.EqualTo(4f));
            Assert.That(loaded.Samples.Count, Is.EqualTo(2));
            Assert.That(loaded.Samples[1].X, Is.EqualTo(10f));
            Assert.That(loaded.Splits, Is.EqualTo(new[] { 1f, 3f }));
            loaded.Splits[0] = 99f;
            loaded.Samples.Clear();
            BestLapData reloaded = new BestLapRepository(storage, 2).Load();
            Assert.That(reloaded.Samples.Count, Is.EqualTo(2));
            Assert.That(reloaded.Splits, Is.EqualTo(new[] { 1f, 3f }));
        }

        [Test]
        public void InvalidStoredDataFallsBackToNoLapAndInvalidCandidatesAreRejected()
        {
            var storage = new InMemoryStorage { Data = new BestLapData() };
            var repository = new BestLapRepository(storage, 2);
            Assert.That(repository.Load(), Is.Null);
            Assert.Throws<ArgumentException>(() => repository.TrySave(new BestLapData()));
            storage.Data = CreateLap(3f);
            storage.Data.Samples[1] = new GhostSample(2f, 0f, 0f, 0f);
            Assert.That(repository.Load(), Is.Null);
        }

        [TestCaseSource(nameof(InvalidSplits))]
        public void InvalidSplitsAreRejectedOnSaveAndLoad(float[] splits)
        {
            BestLapData candidate = CreateLap(4f);
            candidate.Splits = splits;
            var storage = new InMemoryStorage { Data = candidate };
            var repository = new BestLapRepository(storage, 2);
            Assert.That(repository.Load(), Is.Null);
            Assert.Throws<ArgumentException>(() => repository.TrySave(candidate));
            Assert.That(storage.SaveCount, Is.Zero);
        }

        private static IEnumerable<TestCaseData> InvalidSplits()
        {
            yield return new TestCaseData(new object[] { null });
            yield return new TestCaseData(new object[] { new float[0] });
            yield return new TestCaseData(new object[] { new[] { 1f } });
            yield return new TestCaseData(new object[] { new[] { 1f, 2f, 3f } });
            yield return new TestCaseData(new object[] { new[] { float.NaN, 2f } });
            yield return new TestCaseData(new object[] { new[] { 1f, float.PositiveInfinity } });
            yield return new TestCaseData(new object[] { new[] { float.NegativeInfinity, 2f } });
            yield return new TestCaseData(new object[] { new[] { -1f, 2f } });
            yield return new TestCaseData(new object[] { new[] { 2f, 1f } });
            yield return new TestCaseData(new object[] { new[] { 1f, 4.01f } });
        }

        [Test]
        public void EqualSplitsAndBoundaryTimesAreValid()
        {
            BestLapData lap = CreateLap(4f);
            lap.Splits = new[] { 0f, 0f };
            Assert.That(BestLapRepository.IsValid(lap, 2), Is.True);
            lap.Splits = new[] { 4f, 4f };
            Assert.That(BestLapRepository.IsValid(lap, 2), Is.True);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RepositoryRejectsNonPositiveCheckpointCount(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BestLapRepository(new InMemoryStorage(), count));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(7)]
        public void IncompatibleVersionsAreDiscardedAndCannotBeSaved(int version)
        {
            BestLapData lap = CreateLap(4f);
            lap.Version = version;
            var repository = new BestLapRepository(new InMemoryStorage { Data = lap }, 2);
            Assert.That(repository.Load(), Is.Null);
            Assert.Throws<ArgumentException>(() => repository.TrySave(lap));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("rectangle")]
        [TestCase("Suzuka")]
        public void MissingOrDifferentTrackIdentityIsDiscarded(string trackId)
        {
            BestLapData lap = CreateLap(4f);
            lap.TrackId = trackId;
            var repository = new BestLapRepository(new InMemoryStorage { Data = lap }, 2);
            Assert.That(repository.Load(), Is.Null);
        }

        [Test]
        public void CurrentLapCanReplaceAFasterIncompatibleRecord()
        {
            BestLapData old = CreateLap(1f);
            old.Version = 3;
            var storage = new InMemoryStorage { Data = old };
            var repository = new BestLapRepository(storage, 2);
            Assert.That(repository.TrySave(CreateLap(4f)), Is.True);
            Assert.That(repository.Load().Version, Is.EqualTo(BestLapData.CurrentVersion));
            Assert.That(repository.Load().TrackId, Is.EqualTo(BestLapData.DefaultTrackId));
        }

        [Test]
        public void RepositoryRequiresTheConfiguredTrackIdentity()
        {
            BestLapData lap = CreateLap(2f);
            lap.TrackId = "test-track";
            var repository = new BestLapRepository(new InMemoryStorage(), 2, "test-track");
            Assert.That(repository.TrySave(lap), Is.True);
            Assert.That(repository.Load().TrackId, Is.EqualTo("test-track"));
            Assert.Throws<ArgumentException>(() => new BestLapRepository(new InMemoryStorage(), 2, " "));
        }

        private static BestLapData CreateLap(float time)
        {
            return new BestLapData
            {
                Version = BestLapData.CurrentVersion,
                TrackId = BestLapData.DefaultTrackId,
                LapTime = time,
                Splits = new[] { time * 0.25f, time * 0.75f },
                Samples = new List<GhostSample>
                {
                    new GhostSample(0f, 0f, 0f, 0f), new GhostSample(time, 10f, 0f, 90f)
                }
            };
        }

        private sealed class InMemoryStorage : IBestLapStorage
        {
            public BestLapData Data { get; set; }
            public int SaveCount { get; private set; }

            public BestLapData Load()
            {
                return Copy(Data);
            }

            public void Save(BestLapData data)
            {
                Data = Copy(data);
                SaveCount++;
            }

            private static BestLapData Copy(BestLapData data)
            {
                return data == null ? null : new BestLapData
                {
                    Version = data.Version,
                    TrackId = data.TrackId,
                    LapTime = data.LapTime,
                    Splits = data.Splits == null ? null : (float[])data.Splits.Clone(),
                    Samples = data.Samples == null ? null : new List<GhostSample>(data.Samples)
                };
            }
        }
    }
}
