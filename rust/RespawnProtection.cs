using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("RespawnProtection", "sami37", "1.2.0")]
    [Description("RespawnProtection allow admin to set a respawn protection timer.")]
    public class RespawnProtection : RustPlugin
    {
        [PluginReference("Economics")]
        Plugin Economy;

        private int Respawn;
        private bool Enabled;
        private bool Punish;
        private int AmountPerHit;
        private int AmountPerKill;
        private Dictionary<ulong, DateTime> protectedPlayersList = new Dictionary<ulong, DateTime>();
        Dictionary<ulong, HitInfo> LastWounded = new Dictionary<ulong, HitInfo>();
        private void ReadFromConfig<T>(string key, ref T var)
        {
            if (Config[key] != null)
            {
                var = (T) Convert.ChangeType(Config[key], typeof (T));
            }
            Config[key] = var;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            Config["Godmode Enabled"] = true;
            Config["Default Respawn Protection"] = 60;
            Config["Punish fresh respawn kill (Enabled)"] = false;
            Config["Money withdraw per hit"] = 1;
            Config["Money withdraw per kill"] = 100;
            SaveConfig();
        }

        private void OnServerInitialized()
        {

            ReadFromConfig("Default Respawn Protection", ref Respawn);
            ReadFromConfig("Godmode Enabled", ref Enabled);
            ReadFromConfig("Punish fresh respawn kill (Enabled)", ref Punish);
            ReadFromConfig("Money withdraw per hit", ref AmountPerHit);
            ReadFromConfig("Money withdraw per kill", ref AmountPerKill);
            SaveConfig();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Can't Hit", "You can't hit this player until protection is left. {0}"},
                {"Protected", "You have just respawned, you are protected for {0}s."},
                {"NoLongerProtected", "You are no longer protected, take care."},
                {"TryKill", "You are trying to kill a fresh respawned player, you lost {0} money."},
                {"Killed", "You just kill a fresh respawned player, you lost {0} money."}
            }, this);
        }

        HitInfo TryGetLastWounded(ulong uid, HitInfo info)
        {
            if (LastWounded.ContainsKey(uid))
            {
                HitInfo output = LastWounded[uid];
                LastWounded.Remove(uid);
                return output;
            }

            return info;
        }

        void OnEntityDeath(BaseCombatEntity victim, HitInfo info)
        {
            if (victim.ToPlayer() != null)
                if (victim.ToPlayer().IsWounded())
                    info = TryGetLastWounded(victim.ToPlayer().userID, info);

            if (info?.InitiatorPlayer == null && (victim.name?.Contains("autospawn") ?? false))
                return;
            if (info?.InitiatorPlayer != null && Economy != null && Economy.IsLoaded)
            {
                SendReply(info.InitiatorPlayer, string.Format(lang.GetMessage("TryKill", this, info.InitiatorPlayer.UserIDString), AmountPerKill));
                Economy?.CallHook("Withdraw", info.InitiatorPlayer.userID,
                    AmountPerKill);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo != null)
            {
                BasePlayer attacker = hitinfo.InitiatorPlayer;
                if (entity?.ToPlayer() != null && attacker != null)
                {
                    BasePlayer victim = entity.ToPlayer();
                    if (victim != null)
                    {
                        NextTick(() =>
                        {
                            if (entity.ToPlayer().IsWounded())
                                LastWounded[entity.ToPlayer().userID] = hitinfo;
                        });

                        if (!Enabled && !Punish)
                            return;
                        if (Punish)
                            if (Economy != null && Economy.IsLoaded)
                            {
                                SendReply(attacker,
                                    string.Format(lang.GetMessage("TryKill", this, attacker.UserIDString), AmountPerHit));
                                Economy?.CallHook("Withdraw", attacker.userID,
                                    AmountPerKill);
                            }
                        if (protectedPlayersList.ContainsKey(attacker.userID))
                        {
                            protectedPlayersList.Remove(attacker.userID);
                            if (Enabled)
                                SendReply(attacker,
                                    lang.GetMessage("NoLongerProtected", this, attacker.UserIDString));
                        }
                        if (protectedPlayersList.ContainsKey(victim.userID))
                        {
                            DateTime now = DateTime.Now;
                            DateTime old = protectedPlayersList[victim.userID];
                            TimeSpan wait = now - old;
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            if (Enabled)
                                SendReply(attacker,
                                    string.Format(lang.GetMessage("Can't Hit", this, attacker.UserIDString),
                                        wait));
                        }
                        if (entity is BuildingBlock && protectedPlayersList.ContainsKey(attacker.userID))
                        {
                            protectedPlayersList.Remove(attacker.userID);
                            if (Enabled)
                                SendReply(attacker,
                                    lang.GetMessage("NoLongerProtected", this, attacker.UserIDString));
                        }
                    }
                }
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (protectedPlayersList.ContainsKey(player.userID)) 
                protectedPlayersList.Remove(player.userID);
            protectedPlayersList.Add(player.userID, DateTime.Now);
            if(Enabled)
                SendReply(player, string.Format(lang.GetMessage("Protected", this, player.UserIDString), Respawn));
            timer.Once(Respawn, () =>
            {
                protectedPlayersList.Remove(player.userID);
                if (Enabled)
                    SendReply(player, lang.GetMessage("NoLongerProtected", this, player.UserIDString));
            });
        }
		
        bool PlayerRespawn(ulong UserID)
        {
            var baseplayer = BasePlayer.Find(UserID.ToString());
            if (baseplayer == null) return false;
            if (protectedPlayersList.ContainsKey(UserID))
                protectedPlayersList.Remove(UserID);
            protectedPlayersList.Add(UserID, DateTime.Now);
            if (Enabled)
                SendReply(baseplayer, string.Format(lang.GetMessage("Protected", this, UserID.ToString()), Respawn));
            timer.Once(Respawn, () =>
            {
                if (Enabled)
                    protectedPlayersList.Remove(UserID);
                SendReply(baseplayer, lang.GetMessage("NoLongerProtected", this, UserID.ToString()));
            });
            return true;
        }
		
        private bool AddProtection(ulong UserID)
        {
            return PlayerRespawn(UserID);
        }
    }
}