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
            { "hud.gems", "Gems" },
            { "hud.perSecond", "+{0}/s" },
            { "hud.hq", "HQ {0}" },
            { "common.ok", "OK" },
            { "common.close", "Close" },
            { "hq.title", "Headquarters" },
            { "hq.level", "Level {0}" },
            { "hq.current", "Now: board {0}x{1}, buy up to L{2}" },
            { "hq.next", "Next: board {0}x{1}, buy up to L{2}" },
            { "hq.max", "The HQ is at its highest level." },
            { "hq.upgrade", "Upgrade ({0})" },
            { "hq.notEnoughGold", "Not enough gold for the HQ upgrade." },
            { "hq.upgraded", "HQ upgraded to level {0}!" },
            { "offline.title", "Welcome back!" },
            { "offline.body", "Your gold mines earned {0} gold while you were away for {1}." },
            { "offline.capped", "Earnings stopped after {0}. Warehouses raise this limit." },
            { "offline.collect", "Collect" },
            { "time.hoursMinutes", "{0}h {1}m" },
            { "time.minutes", "{0}m" },
            { "base.battle", "Battle" },
            { "base.buy", "Buy" },
            { "base.level", "L{0}" },
            { "base.boardFull", "The board is full. Merge or move items to free a slot." },
            { "base.notEnoughGold", "Not enough gold." },
            { "base.maxLevel", "Already at max level." },
            { "battle.back", "Base" },
            { "battle.level", "Level {0}: {1}" },
            { "battle.enemy", "Enemy formation (scouted)" },
            { "battle.yours", "Your formation: {0}/{1} units. Front row is hit first." },
            { "battle.power", "Power {0} vs {1} (rough guide only: counters and placement matter more)" },
            { "battle.roster", "Your units: drag into the formation, or tap to add or remove" },
            { "battle.rosterEmpty", "No units on your board. Buy Infantry, Artillery or Armor at the base." },
            { "battle.auto", "Auto" },
            { "battle.clear", "Clear" },
            { "battle.fight", "Fight!" },
            { "battle.limit", "Deployment limit reached ({0} units)." },
            { "battle.emptyFormation", "Place at least one unit first." },
            { "battle.locked", "Win the previous level first." },
            { "battle.retreat", "Retreat" },
            { "battle.speed", "{0}x" },
            { "battle.time", "{0}:{1:00}" },
            { "battle.counters", "Infantry beats Artillery, Artillery beats Armor, Armor beats Infantry (+50% damage)." },
            { "result.victory", "Victory!" },
            { "result.defeat", "Defeat" },
            { "result.timeout", "Time ran out." },
            { "result.objWin", "Win the battle" },
            { "result.objNoLoss", "Lose no units" },
            { "result.objFast", "Win within {0}s" },
            { "result.reward", "Reward: {0} gold" },
            { "result.rewardGems", "Reward: {0} gold, {1} gems" },
            { "result.noReward", "No reward. Your units are safe on the board." },
            { "result.hint", "Tip: your {0} were mostly beaten by {1}. {2} counter {1}." },
            { "result.retry", "Retry" },
            { "result.next", "Next level" },
            { "result.base", "Base" },
            { "class.Infantry", "Infantry" },
            { "class.Artillery", "Artillery" },
            { "class.Armor", "Armor" },
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
