using Rust;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Life Stealer", "redBDGR", "1.0.4")]
    [Description("Gives players health when they damage another player and optionally animals")]
    public class LifeStealer : RustPlugin
    {
        private const string permissionName = "lifestealer.use";

        private List<object> animals;
        private bool animalsEnabled;
        private bool staticHealEnabled;
        private double healPercent = 0.5;
        private float staticHealAmount = 5.0f;

        private static List<object> AnimalList()
        {
            var al = new List<object> { "boar", "horse", "stag", "chicken", "wolf", "bear" };
            return al;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (animals.Contains(entity.ShortPrefabName) && animalsEnabled) goto skip;
            if (!(entity is BasePlayer)) return;
            if (!(info.Initiator is BasePlayer)) return;
            skip:
            if (info.damageTypes.GetMajorityDamageType() == DamageType.Bleeding) return;

            var initPlayer = info.InitiatorPlayer;
            if (initPlayer == null) return;
            if (!permission.UserHasPermission(initPlayer.UserIDString, permissionName)) return;

            // Static Healing
            if (staticHealEnabled)
            {
                initPlayer.Heal(staticHealAmount);
                return;
            }

            // % Healing
            var healAmount = Convert.ToSingle(info.damageTypes.Total() * healPercent);
            if (!initPlayer.IsConnected) return;
            if (healAmount < 1) return;
            initPlayer.Heal(healAmount);
        }

        #region config

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                changed = true;
            }
            return value;
        }

        private void LoadVariables()
        {
            healPercent = Convert.ToSingle(GetConfig("Settings", "Heal Percent", 0.5f));
            staticHealAmount = Convert.ToSingle(GetConfig("Static Heal", "Static Heal Amount", 5.0f));
            staticHealEnabled = Convert.ToBoolean(GetConfig("Static Heal", "Static Heal Enabled", false));
            animalsEnabled = Convert.ToBoolean(GetConfig("Settings", "Animals Enabled", false));
            animals = (List<object>)GetConfig("Settings", "Enabled Animals", AnimalList());

            if (!changed) return;
            SaveConfig();
            changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
        }

        private bool changed;

        #endregion config
    }
}
