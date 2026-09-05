using System;
using System.Diagnostics;
using System.Globalization;
using AlphaTown.Core.Diagnostics;
using UnityEngine.Networking;

namespace AlphaTown.Services.Timing
{
    /// <summary>
    /// Reads the time from an HTTP response's <c>Date</c> header.
    ///
    /// Every HTTP server sends one, so this works against your own backend with no endpoint to
    /// build, and it is enough to defeat the actual threat: a player setting their device clock
    /// forward to finish timers.
    ///
    /// It is **not** an authoritative source. The header is unsigned, so anyone able to redirect
    /// the request — a proxy, a hosts file, a rooted device — can answer with whatever time they
    /// like. Replacing this with a signed timestamp from your own backend is the next hardening
    /// step, and nothing else has to change when it happens: only this class.
    /// </summary>
    public sealed class HttpDateHeaderTimeProvider : IServerTimeProvider
    {
        const int DefaultTimeoutSeconds = 10;

        readonly string _url;
        readonly int _timeoutSeconds;

        public HttpDateHeaderTimeProvider(string url, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            _url = url;
            _timeoutSeconds = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;
        }

        public void RequestTime(Action<ServerTimeSample> onComplete)
        {
            if (string.IsNullOrEmpty(_url))
            {
                Log.Warn("Time", "No time server URL configured.");
                onComplete?.Invoke(ServerTimeSample.Failed);
                return;
            }

            UnityWebRequest request;
            try
            {
                request = UnityWebRequest.Get(_url);
                request.timeout = _timeoutSeconds;
            }
            catch (Exception exception)
            {
                Log.Error("Time", "Could not build the time request: " + exception.Message);
                onComplete?.Invoke(ServerTimeSample.Failed);
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            // Completion event rather than a coroutine, so this needs no MonoBehaviour to run on.
            request.SendWebRequest().completed += _ =>
            {
                var sample = ServerTimeSample.Failed;
                try
                {
                    sample = Parse(request, stopwatch.Elapsed.Ticks);
                }
                catch (Exception exception)
                {
                    Log.Error("Time", "Could not read the time response: " + exception.Message);
                }
                finally
                {
                    request.Dispose();
                }

                onComplete?.Invoke(sample);
            };
        }

        static ServerTimeSample Parse(UnityWebRequest request, long roundTripTicks)
        {
            if (request.result != UnityWebRequest.Result.Success) return ServerTimeSample.Failed;

            var header = request.GetResponseHeader("Date");
            if (string.IsNullOrEmpty(header)) return ServerTimeSample.Failed;

            var styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;
            if (!DateTime.TryParse(header, CultureInfo.InvariantCulture, styles, out var utc))
                return ServerTimeSample.Failed;

            return ServerTimeSample.From(utc.Ticks, roundTripTicks);
        }
    }
}
