using AlphaTown.Core.Spatial;

namespace AlphaTown.Gameplay.Expansion
{
    /// <summary>
    /// Land was bought. Permanent — there is no matching "lost" event, and nothing in the
    /// simulation ever takes a region back.
    /// </summary>
    public readonly struct TownExpandedEvent
    {
        public readonly string ExpansionId;
        public readonly GridRect Region;

        public TownExpandedEvent(string expansionId, GridRect region)
        {
            ExpansionId = expansionId;
            Region = region;
        }
    }
}
