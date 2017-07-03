/*
 * TODO: Add metabolism individual configuration settings for food
 */

using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Metabolism", "Wulf/lukespragg", "2.6.0", ResourceId = 680)]
    [Description("Modify or disable player metabolism stats and rates")]
    public class Metabolism : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Calories loss rate (0.0 - infinite)")]
            public float CaloriesLossRate;

            [JsonProperty(PropertyName = "Calories spawn amount (0.0 - 500.0)")]
            public float CaloriesSpawnAmount;

            [JsonProperty(PropertyName = "Health gain rate (0.0 - infinte)")]
            public float HealthGainRate;

            [JsonProperty(PropertyName = "Health spawn amount (0.0 - 100.0)")]
            public float HealthSpawnAmount;

            [JsonProperty(PropertyName = "Hydration loss rate (0.0 - infinite)")]
            public float HydrationLossRate;

            [JsonProperty(PropertyName = "Hydration spawn amount (0.0 - 250.0)")]
            public float HydrationSpawnAmount;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CaloriesLossRate = 0.03f,
                    CaloriesSpawnAmount = 500f,
                    HealthGainRate = 0.03f,
                    HealthSpawnAmount = 100f,
                    HydrationLossRate = 0.03f,
                    HydrationSpawnAmount = 250f
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.HealthGainRate == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Initialization

        private const string permBoost = "metabolism.boost";
        private const string permNone = "metabolism.none";
        private const string permSpawn = "metabolism.spawn";

        private void Init()
        {
            permission.RegisterPermission(permBoost, this);
            permission.RegisterPermission(permNone, this);
            permission.RegisterPermission(permSpawn, this);
        }

        #endregion

        #region Modify Metabolism

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permSpawn))
            {
                player.health = config.HealthSpawnAmount;
                player.metabolism.calories.value = config.CaloriesSpawnAmount;
                player.metabolism.hydration.value = config.HydrationSpawnAmount;
            }
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism m, BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            if (player == null) return;

            if (permission.UserHasPermission(player.UserIDString, permBoost))
            {
                player.health = Mathf.Clamp(player.health + config.HealthGainRate, 0f, 100f);
                m.calories.value = Mathf.Clamp(m.calories.value - config.CaloriesLossRate, m.calories.min, m.calories.max);
                m.hydration.value = Mathf.Clamp(m.hydration.value - config.HydrationLossRate, m.hydration.min, m.hydration.max);
            }
            else if (permission.UserHasPermission(player.UserIDString, permNone))
            {
                m.calories.value = m.calories.max;
                m.hydration.value = m.hydration.max;
            }
        }

        #endregion
    }
}
