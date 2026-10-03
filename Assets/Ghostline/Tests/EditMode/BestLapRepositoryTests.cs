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
            var repository = new BestLapRepository(storage);
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
            new BestLapRepository(storage).TrySave(CreateLap(4f));
            BestLapData loaded = new BestLapRepository(storage).Load();
            Assert.That(loaded.LapTime, Is.EqualTo(4f));
            Assert.That(loaded.Samples.Count, Is.EqualTo(2));
            Assert.That(loaded.Samples[1].X, Is.EqualTo(10f));
            loaded.Samples.Clear();
            Assert.That(new BestLapRepository(storage).Load().Samples.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidStoredDataFallsBackToNoLapAndInvalidCandidatesAreRejected()
        {
            var storage = new InMemoryStorage { Data = new BestLapData() };
            var repository = new BestLapRepository(storage);
            Assert.That(repository.Load(), Is.Null);
            Assert.Throws<ArgumentException>(() => repository.TrySave(new BestLapData()));
            storage.Data = CreateLap(3f);
            storage.Data.Samples[1] = new GhostSample(2f, 0f, 0f, 0f);
            Assert.That(repository.Load(), Is.Null);
        }

        private static BestLapData CreateLap(float time)
        {
            return new BestLapData
            {
                LapTime = time,
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
                    LapTime = data.LapTime,
                    Samples = data.Samples == null ? null : new List<GhostSample>(data.Samples)
                };
            }
        }
    }
}
