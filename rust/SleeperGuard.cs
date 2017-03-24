using System;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SleeperGuard", "Wulf/lukespragg", "0.4.0", ResourceId = 1454)]
    [Description("Protects sleeping players from being hurt, killed, or looted")]

    class SleeperGuard : CovalencePlugin
    {
        #region Initialization

        readonly Hash<string, long> sleepers = new Hash<string, long>();
        readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);

        const string permDamage = "sleeperguard.damage";
        const string permLoot = "sleeperguard.loot";

        int damageDelay;
        int lootDelay;

        protected override void LoadDefaultConfig()
        {
            // Settings
            Config["Damage Protection Delay (Seconds, 0 to Disable)"] = damageDelay = GetConfig("Damage Protection Delay (Seconds, 0 to Disable)", 30);
            Config["Loot Protection Delay (Seconds, 0 to Disable)"] = lootDelay = GetConfig("Loot Protection Delay (Seconds, 0 to Disable)", 30);

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permDamage, this);
            permission.RegisterPermission(permLoot, this);

            UpdateSleepers();
        }

        #endregion

        #region Damage Protection

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var target = entity.ToPlayer();
            if (target == null || !target.IsSleeping()) return;
            if (Timestamp - sleepers[target.UserIDString] < damageDelay) return;

            if (permission.UserHasPermission(target.UserIDString, permDamage)) NullifyDamage(ref info);
        }

        void NullifyDamage(ref HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        #endregion

        #region Loot Protection

        object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (!target.IsSleeping() || !permission.UserHasPermission(target.UserIDString, permLoot)) return null;
            if (Timestamp - sleepers[target.UserIDString] < lootDelay) return false;

            return false;
        }

        #endregion

        #region Sleeper Handling

        void UpdateSleepers()
        {
            foreach (var sleeper in BasePlayer.sleepingPlayerList)
            {
                if (!sleepers.ContainsKey(sleeper.UserIDString)) sleepers.Add(sleeper.UserIDString, Timestamp);
                else sleepers.Remove(sleeper.UserIDString);
            }
        }

        void OnPlayerSleep(BasePlayer player) => sleepers.Add(player.UserIDString, Timestamp);

        void OnPlayerSleepEnded(BasePlayer player) => sleepers.Remove(player.UserIDString);

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        long Timestamp => (long)DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        #endregion
    }
}
