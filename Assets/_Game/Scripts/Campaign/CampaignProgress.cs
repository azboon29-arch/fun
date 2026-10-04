using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Json;
using Game.Core.Save;
using Game.Logic.Battle;

namespace Game.Logic.Campaign
{
    /// <summary>Best stars per level. Saves under the "campaign" section.</summary>
    public sealed class CampaignProgress : ISaveSection
    {
        /// <summary>Save section key.</summary>
        public const string SaveKey = "campaign";

        private const string StarsKey = "stars";

        private readonly CampaignConfig _config;
        private readonly EventBus _bus;
        private readonly Dictionary<string, int> _stars = new Dictionary<string, int>();

        /// <summary>Creates empty progress.</summary>
        public CampaignProgress(CampaignConfig config, EventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <inheritdoc />
        public string Key => SaveKey;

        /// <summary>Best stars on <paramref name="levelId"/> (0 = never won).</summary>
        public int Stars(string levelId) => levelId != null && _stars.TryGetValue(levelId, out var n) ? n : 0;

        /// <summary>Sum of best stars across all levels.</summary>
        public int TotalStars
        {
            get
            {
                var total = 0;
                foreach (var n in _stars.Values) total += n;
                return total;
            }
        }

        /// <summary>True when the level has been won at least once.</summary>
        public bool IsCleared(string levelId) => Stars(levelId) > 0;

        /// <summary>The first level is always open; every other one opens when the one before it is won.</summary>
        public bool IsUnlocked(string levelId)
        {
            var index = _config.IndexOf(levelId);
            if (index < 0) return false;
            return index == 0 || IsCleared(_config.Levels[index - 1].Id);
        }

        /// <summary>The first level not yet won, or the last level when all are won.</summary>
        public LevelDefinition CurrentLevel
        {
            get
            {
                foreach (var level in _config.Levels) if (!IsCleared(level.Id)) return level;
                return _config.Levels.Count > 0 ? _config.Levels[_config.Levels.Count - 1] : null;
            }
        }

        /// <summary>Records a result. Returns how many stars are new (above the previous best).</summary>
        public int Record(string levelId, int stars)
        {
            var previous = Stars(levelId);
            if (stars <= previous) return 0;
            _stars[levelId] = stars;
            _bus.Publish(new GameStateChanged(SaveKey));
            return stars - previous;
        }

        /// <inheritdoc />
        public JsonObject Capture()
        {
            var stars = new JsonObject();
            foreach (var pair in _stars) stars.Set(pair.Key, pair.Value);
            return new JsonObject().Set(StarsKey, stars);
        }

        /// <inheritdoc />
        public void Restore(JsonObject data)
        {
            _stars.Clear();
            var stars = data?.GetObject(StarsKey);
            if (stars == null) return;
            foreach (var key in stars.Keys)
            {
                var n = stars.GetInt(key);
                // Unknown level ids are kept, so removing a level from data and adding it back loses nothing.
                if (n > 0) _stars[key] = Math.Min(n, BattleResult.MaxStars);
            }
        }
    }
}
