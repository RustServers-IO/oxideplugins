using System;
using System.Collections.Generic;
using Rust;

namespace Oxide.Plugins
{
    [Info("RespawnProtection", "sami37", "1.0.0")]
    [Description("RespawnProtection allow admin to set a respawn protection timer.")]
    public class RespawnProtection : RustPlugin
    {
        private int respawn;
        private Dictionary<ulong, DateTime> protectedPlayersList = new Dictionary<ulong, DateTime>();
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
            Config["Default Respawn Protection"] = 60;
            SaveConfig();
        }

        private void OnServerInitialized()
        {

            ReadFromConfig("Default Respawn Protection", ref respawn);
            SaveConfig();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Can't Hit", "You can't hit this player until protection is left. {0}"},
                {"Protected", "You have just respawned, you are protected for {0}s."},
                {"NoLongerProtected", "You are no longer protected, take care."}
            }, this);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity is BasePlayer && hitinfo?.Initiator is BasePlayer)
            {
                BasePlayer attacker = hitinfo.InitiatorPlayer;
                BasePlayer victim = entity as BasePlayer;
                if (protectedPlayersList.ContainsKey(victim.userID))
                {
                    DateTime now = DateTime.Now;
                    DateTime old;
                    protectedPlayersList.TryGetValue(victim.userID, out old);
                    TimeSpan wait = now - old;
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    SendReply(hitinfo.InitiatorPlayer,
                        string.Format(lang.GetMessage("Can't Hit", this, attacker.UserIDString), wait));
                }
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (protectedPlayersList.ContainsKey(player.userID)) 
                protectedPlayersList.Remove(player.userID);
            protectedPlayersList.Add(player.userID, DateTime.Now);
            SendReply(player, string.Format(lang.GetMessage("Protected", this, player.UserIDString), respawn));
            timer.Once(respawn, () =>
            {
                protectedPlayersList.Remove(player.userID);
                SendReply(player, lang.GetMessage("NoLongerProtected", this, player.UserIDString));
            });
        }
    }
}