/*
 * TODO:
 * Add metabolism configuration settings for food
 */

using System;using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Metabolism", "Wulf/lukespragg", "2.4.1", ResourceId = 680)]
    [Description("Modifies player metabolism stats and rates")]

    class Metabolism : RustPlugin
    {
        #region Configuration

        const string permAllow = "metabolism.allow";

        bool caloriesUsage;
        bool healthUsage;
        bool hydrationUsage;
        bool usePermissions;

        float caloriesLossRate;
        float caloriesSpawnValue;
        float healthGainRate;
        float healthSpawnValue;
        float hydrationLossRate;
        float hydrationSpawnValue;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Calories Usage (true/false)"] = caloriesUsage = GetConfig("Calories Usage (true/false)", true);
            Config["Health Usage (true/false)"] = healthUsage = GetConfig("Health Usage (true/false)", true);
            Config["Hydration Usage (true/false)"] = hydrationUsage = GetConfig("Hydration Usage (true/false)", true);
            Config["Use Permissions (true/false)"] = usePermissions = GetConfig("Use Permissions (true/false)", false);

            // Settings
            Config["Calories Loss Rate (0.0 - infinite)"] = caloriesLossRate = GetConfig("Calories Loss Rate (0.0 - infinite)", 0.03f);
            Config["Calories Spawn Value (0.0 - 500.0)"] = caloriesSpawnValue = GetConfig("Calories Spawn Value (0.0 - 500.0)", 500f);
            Config["Health Gain Rate (0.0 - infinite)"] = healthGainRate = GetConfig("Health Gain Rate (0.0 - infinite)", 0.03f);
            Config["Health Spawn Value (0.0 to 100.0)"] = healthSpawnValue = GetConfig("Health Spawn Value (0.0 - 100.0)", 100f);
            Config["Hydration Loss Rate (0.0 - infinite)"] = hydrationLossRate = GetConfig("Hydration Loss Rate (0.0 - infinite)", 0.03f);
            Config["Hydration Spawn Value (0.0 - 250.0)"] = hydrationSpawnValue = GetConfig("Hydration Spawn Value (0.0 - 250.0)", 250f);

            // Cleanup
            Config.Remove("CaloriesLossRate");
            Config.Remove("CaloriesSpawnValue");
            Config.Remove("HealthGainRate");
            Config.Remove("HealthSpawnValue");
            Config.Remove("HydrationLossRate");
            Config.Remove("HydrationSpawnValue");

            SaveConfig();
        }

        void Init()        {            LoadDefaultConfig();
            permission.RegisterPermission(permAllow, this);        }        #endregion

        #region Modify Metabolism

        void Metabolize(BasePlayer player)
        {
            player.health = healthSpawnValue;
            player.metabolism.calories.value = caloriesSpawnValue;
            player.metabolism.hydration.value = hydrationSpawnValue;
        }        /*void OnConsumableUse(Item item)        {
            var player = item.GetOwnerPlayer();
            PrintWarning(item.info.name);
            PrintWarning(item.info.category.ToString());
            //if (item.info?.itemid != 1685058759 || player == null) return;
        }*/

        void OnPlayerRespawned(BasePlayer player) => Metabolize(player);        void OnRunPlayerMetabolism(PlayerMetabolism m, BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            if (player == null || usePermissions && !permission.UserHasPermission(player.UserIDString, permAllow)) return;

            player.health = healthUsage ? Mathf.Clamp(player.health + healthGainRate, 0f, 100f) : 100f;
            m.calories.value = caloriesUsage ? Mathf.Clamp(m.calories.value - caloriesLossRate, m.calories.min, m.calories.max) : m.calories.max;
            m.hydration.value = hydrationUsage ? Mathf.Clamp(m.hydration.value - hydrationLossRate, m.hydration.min, m.hydration.max) : m.hydration.max;
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}
