using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>Validates laps and persists only a strictly faster completed attempt.</summary>
    public sealed class BestLapRepository
    {
        private readonly IBestLapStorage _storage;

        /// <summary>Creates a repository over a nonnull storage implementation.</summary>
        public BestLapRepository(IBestLapStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>Returns a defensive copy of valid saved data, or null when none is valid.</summary>
        public BestLapData Load()
        {
            BestLapData data = _storage.Load();
            return IsValid(data) ? Copy(data) : null;
        }

        /// <summary>Saves the first or strictly faster valid lap; rejects invalid candidates.</summary>
        public bool TrySave(BestLapData candidate)
        {
            if (!IsValid(candidate))
                throw new ArgumentException("Lap data must include ordered start and finish samples.", nameof(candidate));
            BestLapData previous = Load();
            if (previous != null && candidate.LapTime >= previous.LapTime)
                return false;
            _storage.Save(Copy(candidate));
            return true;
        }

        /// <summary>Checks positive finite duration, ordered samples, and matching start/finish times.</summary>
        public static bool IsValid(BestLapData data)
        {
            if (data == null || float.IsNaN(data.LapTime) || float.IsInfinity(data.LapTime)
                || data.LapTime <= 0f || data.Samples == null || data.Samples.Count < 2)
                return false;
            if (data.Samples[0].Time != 0f
                || Math.Abs(data.Samples[data.Samples.Count - 1].Time - data.LapTime) > 0.0001f)
                return false;
            try
            {
                new GhostRecording(data.Samples);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static BestLapData Copy(BestLapData data)
        {
            return new BestLapData
            {
                LapTime = data.LapTime,
                Samples = new List<GhostSample>(data.Samples)
            };
        }
    }
}
