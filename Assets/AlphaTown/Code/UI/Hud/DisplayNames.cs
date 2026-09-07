using System.Text;
using AlphaTown.Data.Catalog;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// Readable names for the slice, derived from localisation keys.
    ///
    /// Definitions carry keys like <c>item.golden_wheat</c> rather than display text, because
    /// nothing in the simulation should hold a string a player reads. There is no localisation
    /// table yet, so this turns the key back into something legible: "Golden Wheat".
    ///
    /// TODO(localisation): replace every call with a real string table lookup. The keys are
    /// already correct, so that change is a swap here and nowhere else.
    /// </summary>
    public static class DisplayNames
    {
        public static string ForItem(IGameDatabase database, string itemId)
        {
            if (database != null && database.TryGetItem(itemId, out var item)) return Pretty(item.DisplayNameKey);
            return Pretty(itemId);
        }

        public static string ForBuilding(IGameDatabase database, string buildingId)
        {
            if (database != null && database.TryGetBuilding(buildingId, out var building))
                return Pretty(building.DisplayNameKey);

            return Pretty(buildingId);
        }

        public static string ForCurrency(IGameDatabase database, string currencyId)
        {
            if (database != null && database.TryGetCurrency(currencyId, out var currency))
                return Pretty(currency.DisplayNameKey);

            return Pretty(currencyId);
        }

        /// <summary>"item.golden_wheat" becomes "Golden Wheat". Empty in, empty out.</summary>
        public static string Pretty(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            var lastDot = key.LastIndexOf('.');
            var tail = lastDot >= 0 && lastDot < key.Length - 1 ? key.Substring(lastDot + 1) : key;

            var builder = new StringBuilder(tail.Length + 4);
            var startOfWord = true;

            for (var i = 0; i < tail.Length; i++)
            {
                var character = tail[i];

                if (character == '_' || character == '-')
                {
                    builder.Append(' ');
                    startOfWord = true;
                    continue;
                }

                // An id written in camelCase gets its word breaks back too.
                if (!startOfWord && char.IsUpper(character) && i > 0 && !char.IsUpper(tail[i - 1]))
                    builder.Append(' ');

                builder.Append(startOfWord ? char.ToUpperInvariant(character) : character);
                startOfWord = false;
            }

            return builder.ToString();
        }

        /// <summary>A duration a player reads at a glance: "3d 4h", "12m 30s", "8s".</summary>
        public static string Duration(System.TimeSpan span)
        {
            if (span <= System.TimeSpan.Zero) return "ready";
            if (span.TotalDays >= 1d) return (int)span.TotalDays + "d " + span.Hours + "h";
            if (span.TotalHours >= 1d) return (int)span.TotalHours + "h " + span.Minutes + "m";
            if (span.TotalMinutes >= 1d) return (int)span.TotalMinutes + "m " + span.Seconds + "s";

            return span.Seconds + "s";
        }

        public static string DurationFromTicks(long remainingTicks) =>
            Duration(System.TimeSpan.FromTicks(remainingTicks > 0L ? remainingTicks : 0L));
    }
}
