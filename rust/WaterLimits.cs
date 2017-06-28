using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Water Limits", "Wulf/lukespragg", "3.0.1", ResourceId = 2122)]
    [Description("Hurts or kills players that are in water under conditions")]
    public class WaterLimits : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Hurt player on contact (true/false)")]
            public bool HurtOnContact;

            [JsonProperty(PropertyName = "Hurt player on disconnect (true/false)")]
            public bool HurtOnDisconnect;

            [JsonProperty(PropertyName = "Hurt player over time (true/false)")]
            public bool HurtOverTime;

            [JsonProperty(PropertyName = "Kill player on contact (true/false)")]
            public bool KillOnContact;

            [JsonProperty(PropertyName = "Kill player on disconnect (true/false)")]
            public bool KillOnDisconnect;

            [JsonProperty(PropertyName = "Damage player amount (1 - 500)")]
            public int DamageAmount;

            [JsonProperty(PropertyName = "Damage player every (seconds)")]
            public int DamageEvery;

            [JsonProperty(PropertyName = "Underwater percent (1 - 100)")]
            public int UnderwaterPercent;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    HurtOnContact = false,
                    HurtOnDisconnect = true,
                    HurtOverTime = true,
                    KillOnContact = false,
                    KillOnDisconnect = false,
                    DamageAmount = 10,
                    DamageEvery = 10,
                    UnderwaterPercent = 50
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.UnderwaterPercent == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Initialization

        private readonly Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();

        private const string permExclude = "waterlimits.exclude";

        private void Init()
        {
            permission.RegisterPermission(permExclude, this);

            if (!config.KillOnContact) Unsubscribe(nameof(OnRunPlayerMetabolism));
            if (config.HurtOnContact && config.KillOnContact || config.HurtOnDisconnect && config.KillOnDisconnect)
            {
                LogWarning("Cannot have both hurt and kill set to true, please check your configuration");
                Interface.Oxide.UnloadPlugin(Name);
            }
        }

        #endregion

        #region Damage Handling

        private void HandleDamage(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permExclude)) return;

            if (config.HurtOnContact || config.HurtOnDisconnect)
            {
                if (config.HurtOverTime)
                {
                    timers[player.userID] = timer.Every(config.DamageEvery, () =>
                    {
                        if (player.IsDead() && timers.ContainsKey(player.userID))
                            timers[player.userID].Destroy();
                        else
                            player.Hurt(config.DamageAmount);
                    });
                    return;
                }

                player.Hurt(config.DamageAmount);
            }
            else if (config.KillOnContact || config.KillOnDisconnect) player.Kill();
        }

        #endregion

        #region Game Hooks

        private void OnServerInitialized()
        {
            foreach (var sleeper in BasePlayer.sleepingPlayerList.Where(s => IsInWater(s))) HandleDamage(sleeper);
        }

        private void OnPlayerConnected(Network.Message packet)
        {
            if (timers.ContainsKey(packet.connection.userid)) timers[packet.connection.userid].Destroy();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (IsInWater(player)) HandleDamage(player);
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism m, BaseCombatEntity entity)
        {
            var player = entity.ToPlayer();
            if (player != null && IsInWater(player)) HandleDamage(player);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (timers.ContainsKey(player.userID)) timers[player.userID].Destroy();
        }

        #endregion

        #region Helpers

        private bool IsInWater(BasePlayer player)
        {
            var modelState = player.modelState;
            return modelState != null && modelState.waterLevel > (config.UnderwaterPercent / 100f);
        }

        #endregion
    }
}
