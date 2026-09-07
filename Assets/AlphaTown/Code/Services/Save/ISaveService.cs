namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Versioned game-state persistence.
    ///
    /// Generic in the payload type on purpose: Services owns *how* state is stored, Gameplay owns
    /// *what* is stored. That keeps the save pipeline free of any upward dependency.
    /// </summary>
    public interface ISaveService
    {
        int CurrentSchemaVersion { get; }

        bool Exists(string slot);

        bool TrySave<TData>(string slot, TData data) where TData : class;

        /// <summary>
        /// Returns false when there is no save, when it cannot be parsed, or when it was written
        /// by a newer build. Callers must treat false as "start fresh", never as "empty save".
        /// </summary>
        bool TryLoad<TData>(string slot, out TData data) where TData : class;

        bool Delete(string slot);
    }
}
