using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>Validates laps and persists only a strictly faster completed attempt.</summary>
    public sealed class BestLapRepository
    {
        private readonly IBestLapStorage _storage;
        private readonly int _checkpointCount;
        private readonly string _trackId;

        /// <summary>Creates a repository for a positive checkpoint count over nonnull storage.</summary>
        public BestLapRepository(IBestLapStorage storage, int checkpointCount, string trackId = BestLapData.DefaultTrackId)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            _checkpointCount = checkpointCount;
            if (string.IsNullOrWhiteSpace(trackId))
                throw new ArgumentException("A track identity is required.", nameof(trackId));
            _trackId = trackId;
        }

        /// <summary>Returns a defensive copy of valid saved data, or null when none is valid.</summary>
        public BestLapData Load()
        {
            BestLapData data = _storage.Load();
            return IsValid(data, _checkpointCount, _trackId) ? Copy(data) : null;
        }

        /// <summary>Saves the first or strictly faster valid lap; rejects invalid candidates.</summary>
        public bool TrySave(BestLapData candidate)
        {
            if (!IsValid(candidate, _checkpointCount, _trackId))
                throw new ArgumentException("Lap data must match the current version/track and include ordered start/finish samples and valid checkpoint splits.", nameof(candidate));
            BestLapData previous = Load();
            if (previous != null && candidate.LapTime >= previous.LapTime)
                return false;
            _storage.Save(Copy(candidate));
            return true;
        }

        /// <summary>Clears the saved best; storage failures propagate without changing the previous data.</summary>
        public void ClearBest()
        {
            _storage.Clear();
        }

        /// <summary>Checks version/track identity, duration, poses, and finite ordered checkpoint splits.</summary>
        public static bool IsValid(BestLapData data, int checkpointCount, string trackId = BestLapData.DefaultTrackId)
        {
            if (checkpointCount <= 0 || data == null || data.Version != BestLapData.CurrentVersion
                || string.IsNullOrWhiteSpace(trackId) || !string.Equals(data.TrackId, trackId, StringComparison.Ordinal)
                || float.IsNaN(data.LapTime) || float.IsInfinity(data.LapTime)
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
                Version = data.Version,
                TrackId = data.TrackId,
                LapTime = data.LapTime,
                Splits = (float[])data.Splits.Clone(),
                Samples = new List<GhostSample>(data.Samples)
            };
        }
    }
}
