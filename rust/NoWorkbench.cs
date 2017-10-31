using Newtonsoft.Json;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoWorkbench", "k1lly0u", "0.1.41", ResourceId = 2645)]
    class NoWorkbench : RustPlugin
    {
        TriggerBase triggerBase;

        #region Oxide Hooks       
        private void OnServerInitialized()
        {           
            LoadVariables();

            triggerBase = new GameObject().AddComponent<TriggerBase>();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player == null)
                return;

            player.ClientRPCPlayer(null, player, "craftMode", 1);
            player.EnterTrigger(triggerBase);
            
            if (configData.NoBlueprints)
                player.blueprints.UnlockAll();
        }

        void OnEntityLeave(TriggerBase trigger, BaseEntity entity)
        {            
            BasePlayer player = entity.ToPlayer();

            if (player != null && trigger == triggerBase)
                player.EnterTrigger(triggerBase);
        }

        private bool CanCraft(PlayerBlueprints blueprints, ItemDefinition definition, int skinId)
        {
            if (blueprints == null || definition == null)
                return false;

            if (skinId != 0 && !blueprints.steamInventory.HasItem(skinId))
                return false;

            if (!blueprints.IsParentUnlocked(definition))
                return false;

            if (blueprints.HasUnlocked(definition))
                return true;

            return false;
        }

        private void OnItemCraft(ItemCraftTask task, BasePlayer player, Item item)
        {
            float time = ItemCrafter.GetScaledDuration(task.blueprint, 3) + 10;
            
            typeof(BasePlayer).GetField("nextCheckTime", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, Time.realtimeSinceStartup + time);
            typeof(BasePlayer).GetField("cachedCraftLevel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, 3f);
        }

        private void OnItemCraftCancelled(ItemCraftTask task) => UpdateCheckTime(task.owner);

        private void OnItemCraftFinished(ItemCraftTask task, Item item) => UpdateCheckTime(task.owner);

        private void UpdateCheckTime(BasePlayer player)
        {
            if (player != null)
            {
                if (player.inventory.crafting.queue.Count == 0)                
                    typeof(BasePlayer).GetField("nextCheckTime", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, Time.realtimeSinceStartup + Random.Range(0.4f, 0.5f));                
            }
        }

        private void Unload()
        {
            if (triggerBase != null)
                UnityEngine.Object.Destroy(triggerBase);
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Disable the need for blueprints")]
            public bool NoBlueprints { get; set; }            
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                NoBlueprints = false
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}
