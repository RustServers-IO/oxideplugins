using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ToolsGatherManager", "hoppel", "1.0.0")]

    class ToolsGatherManager : RustPlugin
    {
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity.ToPlayer()?.GetActiveItem() != null)
            {
                if (config.Tools.Contains(entity.ToPlayer().GetActiveItem().info.shortname))
                {
                    item.amount = (int)(item.amount * config.multiplier);
                }
            }
        }

        static Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Tools")]
            public List<string> Tools;
            [JsonProperty(PropertyName = "gather rate")]
            public float multiplier;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Tools = new List<string>() { "icepick.salvaged", "rock" },
                    multiplier = 1,
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Creating new config file.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
    }
}
