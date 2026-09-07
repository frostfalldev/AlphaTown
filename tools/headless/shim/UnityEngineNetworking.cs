using System;

namespace UnityEngine
{
    /// <summary>Stand-in for Unity's async operation. Nothing here performs real I/O.</summary>
    public class AsyncOperation
    {
        public event Action<AsyncOperation> completed;
        public void Complete() => completed?.Invoke(this);
        public bool isDone => true;
    }
}

namespace UnityEngine.Networking
{
    public class UnityWebRequestAsyncOperation : AsyncOperation { }

    /// <summary>
    /// Compile-only stand-in. The tests drive time synchronisation through FakeServerTimeProvider,
    /// so no test exercises this path — which is itself worth knowing: the real HTTP provider is
    /// unverified either here or in the Editor.
    /// </summary>
    public class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress = 0, Success = 1, ConnectionError = 2, ProtocolError = 3, DataProcessingError = 4 }

        public int timeout { get; set; }
        public Result result { get; set; } = Result.ConnectionError;
        public string error { get; set; } = "headless stub";

        public static UnityWebRequest Get(string url) => new UnityWebRequest();

        public string GetResponseHeader(string name) => null;

        public UnityWebRequestAsyncOperation SendWebRequest() => new UnityWebRequestAsyncOperation();

        public void Dispose() { }
    }
}
