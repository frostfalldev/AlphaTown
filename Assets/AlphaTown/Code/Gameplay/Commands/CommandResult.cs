namespace AlphaTown.Gameplay.Commands
{
    /// <summary>
    /// What happened when the player tapped something, and what to tell them if it did not work.
    ///
    /// The message is plain English rather than a localisation key. That is a deliberate limit of
    /// the vertical slice: failure text is the fastest thing to iterate on while the loop is still
    /// being tuned, and moving it behind keys before the wording has settled would mean rewriting
    /// the table every time a rule changes. TODO(localisation): swap <see cref="Message"/> for a
    /// key plus arguments before any build that ships outside the team.
    /// </summary>
    public readonly struct CommandResult
    {
        CommandResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        /// <summary>Empty on success unless there is something worth saying.</summary>
        public string Message { get; }

        public static CommandResult Ok(string message = "") => new CommandResult(true, message ?? string.Empty);

        public static CommandResult Fail(string message) => new CommandResult(false, message ?? "That did not work.");
    }
}
