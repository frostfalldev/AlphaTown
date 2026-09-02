namespace AlphaTown.Services.Save
{
    /// <summary>
    /// Upgrades a save payload one schema version forward.
    ///
    /// Migrations operate on raw JSON, never on the current DTO types — by definition the old
    /// save has a shape the current classes cannot represent. Once written, a migration is
    /// frozen: players return after a year and their save has to walk the whole chain.
    /// </summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }

        int ToVersion { get; }

        string Migrate(string payloadJson);
    }
}
