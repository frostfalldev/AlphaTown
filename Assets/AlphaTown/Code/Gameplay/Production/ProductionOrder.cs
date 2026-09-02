using System;

namespace AlphaTown.Gameplay.Production
{
    /// <summary>
    /// One unit of work in a producer's queue.
    ///
    /// Everything is an absolute timestamp, never a countdown. That is the whole trick behind
    /// offline progression: an order that finished while the app was closed needs no simulation
    /// to notice, and a save restored on another device lands on the same answer.
    ///
    /// Serialized directly into save data. If it ever grows a runtime-only field, split the DTO out.
    /// </summary>
    [Serializable]
    public struct ProductionOrder
    {
        public string RecipeId;

        /// <summary>When the player queued it. An order can never start before this.</summary>
        public long EnqueuedAtTicks;

        /// <summary>Zero until a slot frees up and the order actually starts.</summary>
        public long StartedAtTicks;

        public long CompletesAtTicks;

        public bool IsStarted => StartedAtTicks > 0;

        public long RemainingTicks(long nowTicks)
        {
            if (!IsStarted) return CompletesAtTicks;
            var remaining = CompletesAtTicks - nowTicks;
            return remaining > 0 ? remaining : 0;
        }
    }
}
