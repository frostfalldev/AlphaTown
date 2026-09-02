namespace AlphaTown.Core.Randomness
{
    /// <summary>
    /// A hash, not a generator: the same inputs always give the same number, with no state to
    /// carry and nothing to seed.
    ///
    /// This is what makes variable outcomes safe in a timestamp-driven simulation. A harvest that
    /// completed while the app was closed is resolved on the next sync, and it has to yield the
    /// same amount it would have yielded had the player been watching — otherwise the result
    /// depends on when you happened to open the game, and a save written before a resync would
    /// disagree with one written after. Keying the roll on the facts of the event (which building,
    /// which recipe, the exact completion timestamp) gives an answer that is fixed the moment the
    /// order is started, however long it takes anyone to look at it.
    ///
    /// Deliberately not <c>UnityEngine.Random</c>: that is global mutable state shared with every
    /// effect and animation in the project, so the sequence a simulation saw would depend on what
    /// the renderer did that frame.
    /// </summary>
    public static class DeterministicRoll
    {
        /// <summary>SplitMix64's finaliser. Cheap, and mixes low bits well enough for small ranges.</summary>
        public static ulong Mix(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        /// <summary>Order-dependent combination of a string and a number into one seed.</summary>
        public static ulong Seed(string text, long number)
        {
            var hash = 1469598103934665603UL; // FNV-1a offset basis.
            if (text != null)
            {
                for (var i = 0; i < text.Length; i++)
                {
                    hash = (hash ^ text[i]) * 1099511628211UL;
                }
            }

            return Mix(hash ^ Mix(unchecked((ulong)number)));
        }

        /// <summary>Inclusive on both ends. Returns <paramref name="minInclusive"/> for an empty range.</summary>
        public static int Range(ulong seed, int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;

            var span = (ulong)(maxInclusive - minInclusive + 1);
            return minInclusive + (int)(Mix(seed) % span);
        }
    }
}
