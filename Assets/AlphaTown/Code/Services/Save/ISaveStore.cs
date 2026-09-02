namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Raw persistence: strings in, strings out. Knows nothing about schema or game state.
    ///
    /// TODO(live-ops): cloud save implements this same interface. Expect a composite store that
    /// writes local-first and reconciles with the remote copy on login, with an explicit
    /// conflict policy — silently picking the newer timestamp loses player progress.
    /// </summary>
    public interface ISaveStore
    {
        bool Exists(string key);

        bool TryRead(string key, out string contents);

        bool TryWrite(string key, string contents);

        bool Delete(string key);
    }
}
