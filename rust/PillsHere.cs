using System;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("PillsHere", "Wulf/lukespragg", "3.0.2", ResourceId = 1723)]
    [Description("Recovers health, hunger, and thirst by set amounts when using rad pills")]

    class PillsHere : CovalencePlugin
    {
        #region Initialization

        const string permUse = "pillshere.use";

        float healAmount;
        float hungerAmount;
        float thirstAmount;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Health Amount"] = healAmount = GetConfig("Health Amount", 20f);
            Config["Hunger Amount"] = hungerAmount = GetConfig("Hunger Amount", 0f);
            Config["Thirst Amount"] = thirstAmount = GetConfig("Thirst Amount", 0f);

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permUse, this);
        }

        #endregion

        #region Healing

        readonly FieldInfo lastValue = typeof(MetabolismAttribute).GetField("lastValue", BindingFlags.Instance | BindingFlags.NonPublic);

        void OnConsumableUse(Item item)
        {
            var player = item.GetOwnerPlayer();
            if (item.info?.itemid != 1685058759 || player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, permUse)) return;

            player.Heal(healAmount);
            player.metabolism.calories.value += hungerAmount;

            var oldHydration = (float)lastValue.GetValue(player.metabolism.hydration);
            player.metabolism.hydration.value = oldHydration + thirstAmount;
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}
