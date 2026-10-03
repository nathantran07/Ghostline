using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>A serializable transport object for a completed lap and its replay samples.</summary>
    [Serializable]
    public sealed class BestLapData
    {
        public const int CurrentVersion = 4;
        public const string DefaultTrackId = "suzuka";
        /// <summary>Gameplay/save revision; missing legacy metadata stays at version zero.</summary>
        public int Version;
        /// <summary>The circuit this lap and its ghost were recorded on.</summary>
        public string TrackId;
        /// <summary>The completed lap duration in seconds.</summary>
        public float LapTime;
        /// <summary>The lap-clock seconds at each checkpoint, in gate order; excludes the finish.</summary>
        public float[] Splits;
        /// <summary>The ordered poses, including time zero and the finish time.</summary>
        public List<GhostSample> Samples = new List<GhostSample>();
    }
}
