using System;
using System.Collections.Generic;
using Rust;

namespace Oxide.Plugins
{
    [Info("LifeStealer", "redBDGR", "1.0.3")]
    [Description("Give players health when they damage someone")]

    class LifeStealer : RustPlugin
    {
        #region config

        void LoadVariables()
        {
            healPercent = Convert.ToSingle(GetConfig("Settings", "Heal Percent", 0.5f));
            staticHealAmount = Convert.ToSingle(GetConfig("Static Heal", "Static Heal Amount", 5.0f));
            staticHealEnabled = Convert.ToBoolean(GetConfig("Static Heal", "Static Heal Enabled", false));
            AnimalsEnabled = Convert.ToBoolean(GetConfig("Settings", "Animals Enabled", false));
            Animals = (List<object>)GetConfig("Settings", "Enabled Animals", AnimalList());

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionName, this);
        }

        bool Changed = false;

        #endregion

        public double healPercent = 0.5;
        public float staticHealAmount = 5.0f;
        public bool staticHealEnabled = false;
        public const string permissionName = "lifestealer.use";
        public bool AnimalsEnabled = false;

        static List<object> AnimalList()
        {
            var al = new List<object>();
            al.Add("boar");
            al.Add("horse");
            al.Add("stag");
            al.Add("chicken");
            al.Add("wolf");
            al.Add("bear");
            return al;
        }
        List<object> Animals;

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (Animals.Contains(entity.ShortPrefabName)) goto skip;
            if (!(entity is BasePlayer)) return;
            if (!(info.Initiator is BasePlayer)) return;
            skip:
            if (info.damageTypes.GetMajorityDamageType() == DamageType.Bleeding) return;

            BasePlayer initPlayer = info.InitiatorPlayer;
            if (initPlayer == null) return;
            if (!permission.UserHasPermission(initPlayer.UserIDString, permissionName)) return;
            
            // Static Healing
            if (staticHealEnabled)
            {
                initPlayer.Heal(staticHealAmount);
                return;
            }

            // % Healing
            float healAmount = Convert.ToSingle(info.damageTypes.Total() * healPercent);
            if (!initPlayer.IsConnected) return;
            if (healAmount < 1) return;
            initPlayer.Heal(healAmount);
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
    }
}
