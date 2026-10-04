using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// Every player-facing string goes through <see cref="Get"/> by key. English only for now; Phase 7 replaces the
    /// table with a localization asset (with Arabic and right-to-left support) without changing call sites.
    /// </summary>
    public static class Strings
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "hud.gold", "Gold" },
            { "hud.unlimited", "Unlimited (until Phase 3)" },
            { "base.battle", "Battle" },
            { "base.buy", "Buy" },
            { "base.level", "L{0}" },
            { "base.boardFull", "The board is full. Merge or move items to free a slot." },
            { "base.notEnoughGold", "Not enough gold." },
            { "base.maxLevel", "Already at max level." },
            { "item.infantry", "Infantry" },
            { "item.artillery", "Artillery" },
            { "item.armor", "Armor" },
            { "item.goldmine", "Gold Mine" },
            { "item.barracks", "Barracks" },
            { "item.warehouse", "Warehouse" },
        };

        /// <summary>Text for <paramref name="key"/>; the key itself when missing so gaps are visible.</summary>
        public static string Get(string key) => key != null && English.TryGetValue(key, out var text) ? text : key ?? "";

        /// <summary>Formatted text for <paramref name="key"/>.</summary>
        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
