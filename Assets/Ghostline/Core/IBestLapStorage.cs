namespace Ghostline.Core
{
    /// <summary>Separates best-lap rules from persistence and serialization.</summary>
    public interface IBestLapStorage
    {
        /// <summary>Loads saved data, or returns null when unavailable or unreadable.</summary>
        BestLapData Load();

        /// <summary>Persists a lap; failures may throw and must be handled by the application adapter.</summary>
        void Save(BestLapData data);
    }
}
