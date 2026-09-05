using System;

namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Wraps the game payload with the metadata needed to load it safely years from now.
    ///
    /// The payload stays a nested JSON string rather than a typed field so migrations can rewrite
    /// it without the current DTO classes being able to parse the old shape.
    /// </summary>
    [Serializable]
    public sealed class SaveEnvelope
    {
        public int SchemaVersion;
        public long SavedAtUtcTicks;
        public string AppVersion;
        public string Payload;
    }
}
