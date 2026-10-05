using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Ghostline.Core;
using UnityEngine;

namespace Ghostline.Game
{
    /// <summary>Maps immutable Core samples to JsonUtility-compatible fields and a versioned save file.</summary>
    public sealed class JsonFileBestLapStorage : IBestLapStorage
    {
        private readonly string _path;
        private readonly int _checkpointCount;
        private readonly string _trackId;

        public JsonFileBestLapStorage(int checkpointCount, string trackId = BestLapData.DefaultTrackId)
            : this(Path.Combine(Application.persistentDataPath, "ghostline-best-lap.json"), checkpointCount, trackId)
        {
        }

        public JsonFileBestLapStorage(string path, int checkpointCount, string trackId = BestLapData.DefaultTrackId)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A save path is required.", nameof(path));
            _path = path;
            if (checkpointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpointCount));
            _checkpointCount = checkpointCount;
            if (string.IsNullOrWhiteSpace(trackId))
                throw new ArgumentException("A track identity is required.", nameof(trackId));
            _trackId = trackId;
        }

        public BestLapData Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return null;
                SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(_path));
                if (file == null || file.Version != BestLapData.CurrentVersion
                    || !string.Equals(file.TrackId, _trackId, StringComparison.Ordinal)
                    || file.Samples == null || file.Splits == null)
                    return null;
                var data = new BestLapData { Version = file.Version, TrackId = file.TrackId, LapTime = file.LapTime, Splits = file.Splits };
                foreach (SampleData sample in file.Samples)
                {
                    if (sample == null)
                        return null;
                    data.Samples.Add(new GhostSample(sample.Time, sample.X, sample.Y, sample.Rotation));
                }
                return BestLapRepository.IsValid(data, _checkpointCount, _trackId) ? data : null;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is ArgumentException)
            {
                Debug.LogWarning($"Ghostline could not load its best lap: {exception.Message}");
                return null;
            }
        }

        public void Clear()
        {
            // File.Delete tolerates a missing file; do not mask access or sharing failures.
            try
            {
                File.Delete(_path);
            }
            catch (DirectoryNotFoundException)
            {
                // No parent directory also means no saved lap to clear.
            }
        }

        public void Save(BestLapData data)
        {
            if (!BestLapRepository.IsValid(data, _checkpointCount, _trackId))
                throw new ArgumentException("Cannot save invalid lap data.", nameof(data));
            var file = new SaveFile { Version = data.Version, TrackId = data.TrackId, LapTime = data.LapTime, Splits = data.Splits };
            foreach (GhostSample sample in data.Samples)
            {
                file.Samples.Add(new SampleData
                {
                    Time = sample.Time, X = sample.X, Y = sample.Y, Rotation = sample.Rotation
                });
            }
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            string temporaryPath = _path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(file, true));
                if (File.Exists(_path))
                    File.Replace(temporaryPath, _path, null);
                else
                    File.Move(temporaryPath, _path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        [Serializable]
        private sealed class SaveFile
        {
            public int Version;
            public string TrackId;
            public float LapTime;
            public float[] Splits;
            public List<SampleData> Samples = new List<SampleData>();
        }

        [Serializable]
        private sealed class SampleData
        {
            public float Time;
            public float X;
            public float Y;
            public float Rotation;
        }
    }
}
