using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>Validates laps and persists only a strictly faster completed attempt.</summary>
    public sealed class BestLapRepository
    {
        private readonly IBestLapStorage _storage;
        private readonly int _checkpointCount;

        /// <summary>Creates a repository for a positive checkpoint count over nonnull storage.</summary>
        public BestLapRepository(IBestLapStorage storage, int checkpointCount)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            _checkpointCount = checkpointCount;
        }

        /// <summary>Returns a defensive copy of valid saved data, or null when none is valid.</summary>
        public BestLapData Load()
        {
            BestLapData data = _storage.Load();
            return IsValid(data, _checkpointCount) ? Copy(data) : null;
        }

        /// <summary>Saves the first or strictly faster valid lap; rejects invalid candidates.</summary>
        public bool TrySave(BestLapData candidate)
        {
            if (!IsValid(candidate, _checkpointCount))
                throw new ArgumentException("Lap data must include ordered start/finish samples and valid checkpoint splits.", nameof(candidate));
            BestLapData previous = Load();
            if (previous != null && candidate.LapTime >= previous.LapTime)
                return false;
            _storage.Save(Copy(candidate));
            return true;
        }

        /// <summary>Checks duration, start/finish samples, and ordered finite splits for every checkpoint.</summary>
        public static bool IsValid(BestLapData data, int checkpointCount)
        {
            if (checkpointCount <= 0 || data == null || float.IsNaN(data.LapTime) || float.IsInfinity(data.LapTime)
                || data.LapTime <= 0f || data.Samples == null || data.Samples.Count < 2)
                return false;
            if (data.Splits == null || data.Splits.Length != checkpointCount)
                return false;
            float previousSplit = 0f;
            foreach (float split in data.Splits)
            {
                if (float.IsNaN(split) || float.IsInfinity(split) || split < previousSplit || split > data.LapTime)
                    return false;
                previousSplit = split;
            }
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
                Splits = (float[])data.Splits.Clone(),
                Samples = new List<GhostSample>(data.Samples)
            };
        }
    }
}
