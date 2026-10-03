using System;
using System.Collections.Generic;

namespace Ghostline.Core
{
    /// <summary>A serializable transport object for a completed lap and its replay samples.</summary>
    [Serializable]
    public sealed class BestLapData
    {
        /// <summary>The completed lap duration in seconds.</summary>
        public float LapTime;
        /// <summary>The lap-clock seconds at each checkpoint, in gate order; excludes the finish.</summary>
        public float[] Splits;
        /// <summary>The ordered poses, including time zero and the finish time.</summary>
        public List<GhostSample> Samples = new List<GhostSample>();
    }
}
