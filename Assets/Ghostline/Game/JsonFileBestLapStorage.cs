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
        private const int CurrentVersion = 1;
        private readonly string _path;

        public JsonFileBestLapStorage()
            : this(Path.Combine(Application.persistentDataPath, "ghostline-best-lap.json"))
        {
        }

        public JsonFileBestLapStorage(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A save path is required.", nameof(path));
            _path = path;
        }

        public BestLapData Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return null;
                SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(_path));
                if (file == null || file.Version != CurrentVersion || file.Samples == null)
                    return null;
                var data = new BestLapData { LapTime = file.LapTime };
                foreach (SampleData sample in file.Samples)
                {
                    if (sample == null)
                        return null;
                    data.Samples.Add(new GhostSample(sample.Time, sample.X, sample.Y, sample.Rotation));
                }
                return BestLapRepository.IsValid(data) ? data : null;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is ArgumentException)
            {
                Debug.LogWarning($"Ghostline could not load its best lap: {exception.Message}");
                return null;
            }
        }

        public void Save(BestLapData data)
        {
            if (!BestLapRepository.IsValid(data))
                throw new ArgumentException("Cannot save invalid lap data.", nameof(data));
            var file = new SaveFile { Version = CurrentVersion, LapTime = data.LapTime };
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
            public float LapTime;
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
