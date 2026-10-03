namespace Ghostline.Core
{
    /// <summary>A measured closed-track interval where requested width could not be retained.</summary>
    public readonly struct TrackWidthLimit
    {
        public TrackWidthLimit(float fromDistance, float toDistance, float minimumWidth)
        {
            FromDistance = fromDistance;
            ToDistance = toDistance;
            MinimumWidth = minimumWidth;
        }

        public float FromDistance { get; }
        public float ToDistance { get; }
        public float MinimumWidth { get; }
    }
}
