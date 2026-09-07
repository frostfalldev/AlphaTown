namespace AlphaTown.Data.Validation
{
    public enum ContentIssueSeverity
    {
        /// <summary>Playable, but almost certainly not what was meant.</summary>
        Warning = 0,

        /// <summary>Broken. Something references what is not there, or cannot ever run.</summary>
        Error = 1
    }

    /// <summary>One thing wrong with the content, in the words a person needs to go and fix it.</summary>
    public readonly struct ContentIssue
    {
        public readonly ContentIssueSeverity Severity;

        /// <summary>The definition at fault, so the message names something findable in the project.</summary>
        public readonly string Subject;

        public readonly string Message;

        public ContentIssue(ContentIssueSeverity severity, string subject, string message)
        {
            Severity = severity;
            Subject = subject;
            Message = message;
        }

        public override string ToString() =>
            (Severity == ContentIssueSeverity.Error ? "ERROR  " : "warn   ") + Subject + ": " + Message;
    }
}
