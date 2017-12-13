using Oxide.Core;
using System;
using System.Collections.Generic;
using Rust;

namespace Oxide.Plugins
{
    [Info("WipeProtection", "Slydelix", 1.1, ResourceId = 2722)]
    class WipeProtection : RustPlugin
    {
        //TODO (?) BROADCAST WHEN RAID BLOCK IS OVER
        Dictionary<string, string> raidtools = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire" },
            {"ammo.rocket.hv", "rocket_hv" },
            {"ammo.rocket.basic", "rocket_basic" },
            {"explosive.timed", "explosive.timed.deployed" },
            {"surveycharge", "survey_charge.deployed" },
            {"explosive.satchel", "explosive.satchel.deployed" },
            {"grenade.beancan", "grenade.beancan.deployed" },
            {"grenade.f1", "grenade.f1.deployed" }
        };

        float wipeprotecctime;
        bool refund;

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config["Wipe protection time (hours)"] = wipeprotecctime = GetConfig("Wipe protection time (hours)", 24f);
            Config["Refund explosives"] = refund = GetConfig("Refund explosives", true);
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"console_manual", "Manually setting {0} as wipe time and {1} as time after which raiding is possible"},
                {"console_auto", "Detected wipe, setting {0} as wipe time and {1} as time after which raiding is possible"},
                {"console_stopped", "Everything is now raidable"},
                {"dataFileWiped", "Data file successfully wiped"},
                {"refunded", "Your '{0}' was refunded."},
                {"wipe_blocked", "This entity cannot be destroyed because all raiding is currently blocked."}
            }, this);
        }

        #endregion
        #region DataFile
        class StoredData
        {
            public bool wipeprotection;
            public string lastwipe;
            public string RaidStartTime;

            public StoredData()
            {

            }
        }

        StoredData storedData;

        #endregion
        #region Hooks

        void Unload() => SaveFile();

        void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            LoadDefaultConfig();
            CheckTime();
        }

        void CheckTime()
        {
            timer.Every(30f, () => {
                if (!storedData.wipeprotection) return;
                if (DateTime.Now >= Convert.ToDateTime(storedData.RaidStartTime))
                {
                    storedData.wipeprotection = false;
                    SaveFile();
                    return;
                }
            });
        }

        void OnNewSave(string filename)
        {
            DateTime now = DateTime.Now;
            DateTime rs = Convert.ToDateTime(storedData.lastwipe).AddHours(wipeprotecctime);
            storedData.wipeprotection = true;
            storedData.lastwipe = now.ToString();
            storedData.RaidStartTime = rs.ToString();
            SaveFile();
            PrintWarning(lang.GetMessage("console_auto", this, null), now, rs);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || entity == null || hitinfo?.InitiatorPlayer == null || entity.OwnerID == hitinfo?.InitiatorPlayer?.userID || entity?.OwnerID == 0 || hitinfo?.WeaponPrefab?.ShortPrefabName == null) return null;
            if (!(entity is BuildingBlock || entity is Door || entity.PrefabName.Contains("deployable"))) return null;

            BasePlayer attacker = hitinfo.InitiatorPlayer;

            string name = hitinfo?.WeaponPrefab?.ShortPrefabName;

            if (WipeProtected())
            {
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
                hitinfo.HitMaterial = 0;
                msgPlayer(attacker, entity);
                Refund(attacker, name, entity);
                return true;
            }

            return null;
        }

        #endregion
        #region Functions

        bool WipeProtected()
        {
            if (!storedData.wipeprotection) return false;
            if (DateTime.Now < (Convert.ToDateTime(storedData.RaidStartTime))) return true;

            return false;
        }

        void msgPlayer(BasePlayer attacker, BaseEntity entity)
        {
            if (WipeProtected())
            {
                SendReply(attacker, lang.GetMessage("wipe_blocked", this, attacker.UserIDString));
                return;
            }
        }

        void Refund(BasePlayer attacker, string name, BaseEntity ent)
        {
            if (name == "Null") return;
            //Possibly most f**ked up thing I've ever made
            if (refund)
            {
                foreach (var entry in raidtools)
                {
                    if (name == entry.Value)
                    {
                        Item item = ItemManager.CreateByName(entry.Key, 1);
                        attacker.GiveItem(item);
                        SendReply(attacker, lang.GetMessage("refunded", this, attacker.UserIDString), item.info.displayName.english);
                    }
                }
            }
        }

        void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);

        #endregion
        #region Commands
        [ConsoleCommand("wipeprotection.manual")]
        void wipeStartCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            DateTime now = DateTime.Now;
            DateTime rs = DateTime.Now.AddHours(wipeprotecctime);
            storedData.wipeprotection = true;
            storedData.lastwipe = now.ToString();
            storedData.RaidStartTime = rs.ToString();
            SaveFile();

            Puts(lang.GetMessage("console_manual", this, null), now, rs);
            return;
        }

        [ConsoleCommand("wipeprotection.stop")]
        void wipeEndCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            storedData.wipeprotection = false;
            SaveFile();
            Puts(lang.GetMessage("console_stopped", this, null));
            return;
        }
        #endregion
    }
}