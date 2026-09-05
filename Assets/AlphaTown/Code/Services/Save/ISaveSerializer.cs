namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Object to string. Swappable so the payload format can change — to compressed binary, say —
    /// without the save pipeline or any DTO changing.
    /// </summary>
    public interface ISaveSerializer
    {
        string Serialize<TValue>(TValue value) where TValue : class;

        bool TryDeserialize<TValue>(string text, out TValue value) where TValue : class;
    }
}
