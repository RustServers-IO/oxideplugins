using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PillsHere", "Wulf/lukespragg", "3.1.0", ResourceId = 1723)]
    [Description("Recovers health, hunger, and/or hydration by set amounts on item use")]
    public class PillsHere : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Calories amount (0.0 - 500.0)")]
            public float CaloriesAmount;

            [JsonProperty(PropertyName = "Health amount (0.0 - 100.0)")]
            public float HealthAmount;

            [JsonProperty(PropertyName = "Hydration amount (0.0 - 250.0)")]
            public float HydrationAmount;

            [JsonProperty(PropertyName = "Item ID or short name to use")]
            public string ItemIdOrShortName;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    CaloriesAmount = 0f,
                    HealthAmount = 20f,
                    HydrationAmount = 0f,
                    ItemIdOrShortName = "1685058759"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.HealthAmount == null) LoadDefaultConfig();
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

        #endregion Configuration

        #region Initialization

        private const string permUse = "pillshere.use";

        private void Init() => permission.RegisterPermission(permUse, this);

        #endregion Initialization

        #region Healing

        private void OnItemUse(Item item)
        {
            var player = item.GetOwnerPlayer();
            if (player == null) return;

            if (item.info.itemid.ToString() != config.ItemIdOrShortName && item.info.name != config.ItemIdOrShortName) return;
            if (!permission.UserHasPermission(player.UserIDString, permUse)) return;

            player.Heal(config.HealthAmount);
            player.metabolism.calories.value += config.CaloriesAmount;
            player.metabolism.hydration.value = player.metabolism.hydration.lastValue + config.HydrationAmount;
        }

        #endregion Healing
    }
}
