using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("ToolsGatherManager", "hoppel", "1.0.1")]

    class ToolsGatherManager : RustPlugin
    {

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (entity.ToPlayer()?.GetActiveItem() != null)
            {
                var toolName = entity.ToPlayer().GetActiveItem().info.shortname;
                if (config.tools.ContainsKey(toolName))
                {
                    item.amount = (int)(item.amount * config.tools[toolName]);
                }
            }

        }

        Dictionary<string, float> tools = new Dictionary<string, float>()
        {
            ["bone.club"] = 1,
            ["knife.bone"] = 1,
            ["longsword"] = 1,
            ["mace"] = 1,
            ["machete"] = 1,
            ["salvaged.cleaver"] = 1,
            ["salvaged.sword"] = 1,
            ["hatchet"] = 1,
            ["pickaxe"] = 1,
            ["rock"] = 1,
            ["axe.salvaged"] = 1,
            ["hammer.salvaged"] = 1,
            ["icepick.salvaged"] = 1,
            ["stonehatchet"] = 1,
            ["stone.pickaxe"] = 1
        };

        static Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Tools")]
            public Dictionary<string, float> tools;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    tools = new Dictionary<string, float>() {             ["bone.club"] = 1,
            ["knife.bone"] = 1,
            ["longsword"] = 1,
            ["mace"] = 1,
            ["machete"] = 1,
            ["salvaged.cleaver"] = 1,
            ["salvaged.sword"] = 1,
            ["hatchet"] = 1,
            ["pickaxe"] = 1,
            ["rock"] = 1,
            ["axe.salvaged"] = 1,
            ["hammer.salvaged"] = 1,
            ["icepick.salvaged"] = 1,
            ["stonehatchet"] = 1,
            ["stone.pickaxe"] = 1}
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
