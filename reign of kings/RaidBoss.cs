using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Damaging;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Inventory.Blueprints;
using Oxide.Core;
using Oxide.Core.Plugins;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.ItemContainer;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Engine.Core.Cache;

namespace Oxide.Plugins
{
    [Info("RaidBoss", "Scorpyon & D-Kay", "1.3.0",  ResourceId = 1142)]
    public class RaidBoss : ReignOfKingsPlugin
    {
        #region Variables

        List<ulong> BossList = new List<ulong>();

        private float damageTaken = 50;
        private float damageDone = 130;
        private int rewardAmount = 10000;

        private bool bossGold = true;
        private bool guildBossGold = false;

        [PluginReference("GrandExchange")]
        Plugin GrandExchange;

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadConfigData();
            LoadDefaultMessages();

            permission.RegisterPermission("RaidBoss.Toggle", this);
            permission.RegisterPermission("RaidBoss.Modify", this);
        }

        protected override void LoadDefaultConfig()
        {
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            bossGold = GetConfig("bossGold", true);
            guildBossGold = GetConfig("guildBossGold", false);
            damageTaken = GetConfig("damageTaken", 50);
            damageDone = GetConfig("damageDone", 130);
            rewardAmount = GetConfig("rewardAmount", 10000);
        }

        private void SaveConfigData()
        {
            Config["damageTaken"] = damageTaken;
            Config["damageDone"] = damageDone;
            Config["rewardAmount"] = rewardAmount;
            Config["bossGold"] = bossGold;
            Config["guildBossGold"] = guildBossGold;

            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPermission", "You do not have permission to use this command!" },
                { "InvalidArgs", "Incorrect usage. Type /bosshelp if you need information about how to use the commands." },
                { "ToggleBossGold", "[FF0000]RaidBoss[FFFFFF] : Boss gold was turned {0}." },
                { "ToggleBossGoldGuild", "[FF0000]RaidBoss[FFFFFF] : Boss gold for guild members was turned {0}." },
                { "ChangeRewardAmount", "[FF0000]RaidBoss[FFFFFF] : The reward amount has been changed to {0} gold." },
                { "NoPlayer", "[FF0000]RaidBoss[FFFFFF] : That player does not appear to be online right now." },
                { "AlreadyABoss", "[FF0000]RaidBoss[FFFFFF] : That player already is a boss." },
                { "NoBoss", "[FF0000]RaidBoss[FFFFFF] : There is no boss with that name." },
                { "BossAdded", "[FF0000]RaidBoss[FFFFFF] : {0} has been turned into a devastating evil knight by the Gods! Kill him quick!" },
                { "BossRemoved", "[FF0000]RaidBoss[FFFFFF] : An evil knight has been reduced to a mere mortal." },
                { "BossKilled", "[FF0000]RaidBoss[FFFFFF] : An evil knight has been killed!" },
                { "AllBossesGone", "[FF0000]RaidBoss[FFFFFF] : All evil knight are gone!" },
                { "NoBosses", "[FF0000]RaidBoss[FFFFFF] : There are no bosses." },
                { "BosslistTitle", "     Boss list :" },
                { "BosslistPlayer", "       {0}" },
                { "GuildmemberKill", "[FF0000]RaidBoss[FFFFFF] : You won't gain the reward for killing a guild member!" },
                { "GoldGained", "[FF0000]RaidBoss[FFFFFF] : [00FF00] {0} [FFFF00] gold[FFFFFF] reward received." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("bosshelp")]
        private void ShowBossHelp(Player player, string cmd, string[] input)
        {
            SendHelpText(player);
        }

        [ChatCommand("bossreward")]
        private void SetBossReward(Player player, string cmd, string[] input)
        {
            ToggleBossReward(player, input);
        }

        [ChatCommand("bossrewardguild")]
        private void SetBossRewardGuild(Player player, string cmd, string[] input)
        {
            ToggleGuildBossGold(player, input);
        }

        [ChatCommand("bossrewardamount")]
        private void SetBossRewardAmount(Player player, string cmd, string[] input)
        {
            SetRewardAmount(player, input);
        }

        [ChatCommand("bossadd")]
        private void SetARaidBoss(Player player, string cmd, string[] input)
        {
            SetBoss(player, input);
        }

        [ChatCommand("bossremove")]
        private void RemoveARaidBoss(Player player, string cmd, string[] input)
        {
            RemoveBoss(player, input);
        }

        [ChatCommand("bossremoveall")]
        private void RemoveAllTheBosses(Player player, string cmd)
        {
            RemoveAllBosses(player);
        }

        [ChatCommand("bosslist")]
        private void ListBosses(Player player, string cmd)
        {
            ShowBossList(player);
        }

        [ChatCommand("bossdamagetaken")]
        private void SetBossDamageTaken(Player player, string cmd, string[] input)
        {
            ChangeDamage(player, input, 1);
        }

        [ChatCommand("bossdamagedone")]
        private void SetBossDamageDone(Player player, string cmd, string[] input)
        {
            ChangeDamage(player, input, 2);
        }

        #endregion

        #region Command Functions

        private void ToggleBossReward(Player player, string[] input)
        {
            if (!player.HasPermission("RaidBoss.Toggle")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString())); return; }

            switch (input[0])
            {
                case "on":
                    bossGold = true;
                    PrintToChat(player, string.Format(GetMessage("ToggleBossGold", player.Id.ToString()), "on"));
                    break;
                case "off":
                    bossGold = false;
                    PrintToChat(player, string.Format(GetMessage("ToggleBossGold", player.Id.ToString()), "off"));
                    break;
            }
        }

        private void ToggleGuildBossGold(Player player, string[] input)
        {
            if (!player.HasPermission("RaidBoss.Toggle")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString())); return; }

            switch (input[0])
            {
                case "on":
                    guildBossGold = true;
                    PrintToChat(player, string.Format(GetMessage("ToggleBossGoldGuild", player.Id.ToString()), "on"));
                    break;
                case "off":
                    guildBossGold = false;
                    PrintToChat(player, string.Format(GetMessage("ToggleBossGoldGuild", player.Id.ToString()), "on"));
                    break;
            }
        }

        private void SetRewardAmount(Player player, string[] input)
        {
            if (!player.HasPermission("RaidBoss.modify")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString())); return; }

            int newAmount = 0;
            if (!int.TryParse(input[0], out newAmount)) return;

            rewardAmount = newAmount;
            SaveConfigData();
            PrintToChat(player, string.Format(GetMessage("ChangeRewardAmount", player.Id.ToString()), rewardAmount.ToString()));
        }

        private void SetBoss(Player player, string[] input)
        {
            if (!player.HasPermission("RaidBoss.Modify")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString())); return; }

            string playerName = input.JoinToString(" ");
            Player targetPlayer = Server.GetPlayerByName(playerName);

            if (targetPlayer == null) { PrintToChat(player, GetMessage("NoPlayer", player.Id.ToString())); return; }
            if (BossList.Contains(targetPlayer.Id)) { PrintToChat(player, GetMessage("AlreadyABoss", player.Id.ToString())); return; }
            
            BossList.Add(targetPlayer.Id);
            PrintToChat(string.Format(GetMessage("BossAdded", player.Id.ToString()), targetPlayer.DisplayName));
        }

        private void RemoveBoss(Player player, string[] input)
        {
            if (!player.HasPermission("RaidBoss.Modify")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString())); return; }

            string playerName = input.JoinToString(" ");
            Player targetPlayer = Server.GetPlayerByName(playerName);

            if (targetPlayer == null) { GetMessage("NoPlayer", player.Id.ToString()); return; }
            if (!BossList.Contains(targetPlayer.Id)) { PrintToChat(player, GetMessage("NoBoss", player.Id.ToString())); return; }

            BossList.Remove(targetPlayer.Id);
            PrintToChat(GetMessage("BossRemoved", player.Id.ToString()));
            CheckAllBossesAreGone();
        }

        private void RemoveAllBosses(Player player)
        {
            if (!player.HasPermission("RaidBoss.Modify")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (BossList.Count() < 1) { PrintToChat(GetMessage("NoBosses", player.Id.ToString())); return; }

            PrintToChat(GetMessage("AllBossesGone", player.Id.ToString()));
            BossList = new List<ulong>();
        }

        private void ShowBossList(Player player)
        {
            if (BossList.Count < 1) { PrintToChat(player, GetMessage("AllBossesGone", player.Id.ToString())); return; }

            PrintToChat(player, GetMessage("BosslistTitle", player.Id.ToString()));
            foreach (ulong id in BossList)
            {
                Player boss = Server.GetPlayerById(id);
                PrintToChat(player, string.Format(GetMessage("BosslistPlayer", player.Id.ToString()), boss.DisplayName));
            }
        }

        private void ChangeDamage(Player player, string[] input, int type)
        {
            if (!player.HasPermission("RaidBoss.modify")) { PrintToChat(player, GetMessage("NoPermission", player.Id.ToString())); return; }
            if (input.Length < 1) { PrintToChat(player, GetMessage("InvalidArgs", player.Id.ToString())); return; }

            float newAmount = 0;
            if (!float.TryParse(input[0], out newAmount)) return;

            switch (type)
            {
                case 1:
                    damageTaken = newAmount;
                    break;
                case 2:
                    damageDone = newAmount;
                    break;
            }

            SaveConfigData();
        }

        #endregion

        #region System Functions

        private void CheckAllBossesAreGone()
        {
            if (BossList.Count < 1) PrintToChat(GetMessage("AllBossesGone"));
        }

        #endregion

        #region Hooks

        private void SendHelpText(Player player)
        {
            PrintToChat(player, "[0000FF]Raid Boss[FFFFFF]");
            PrintToChat(player, "[00FF00]/bosslist[FFFFFF] - Show a list of which players are bosses.");
            if (player.HasPermission("raidboss.toggle"))
            {
                PrintToChat(player, "[00FF00]/bossreward (on/off)[FFFFFF] - Toggle the gold reward for killing a boss.");
                PrintToChat(player, "[00FF00]/boss (on/off)[FFFFFF] - Toggle the gold reward for the boss getting killed by one of his guild members.");
            }
            if (player.HasPermission("raidboss.modify"))
            {
                PrintToChat(player, "[00FF00]/bossadd (playername)[FFFFFF] - Turn a player into a boss.");
                PrintToChat(player, "[00FF00]/bossremove (playername)[FFFFFF] - Remove a boss.");
                PrintToChat(player, "[00FF00]/bossremoveall[FFFFFF] - Remove all bosses.");
                PrintToChat(player, "[00FF00]/bossdamagetaken (percentage)[FFFFFF] - Changes the amount of damage a boss takes from a player.");
                PrintToChat(player, "[00FF00]/bossdamagedone (percentage)[FFFFFF] - Changes the amount of damage a boss does to a player.");
            }
        }

        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            #region Null Checks
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (damageEvent.Entity == null) return;
            if (damageEvent.Entity == damageEvent.Damage.DamageSource) return;
            #endregion

            Entity victim = damageEvent.Entity;
            Entity damager = damageEvent.Damage.DamageSource;
            float damage = damageEvent.Damage.Amount;
            int type = 0;

            if (BossList.Contains(victim.Owner.Id)) type = 1;
            else if (BossList.Contains(damager.Owner.Id)) type = 2;
            else return;
            
            if (damage > 0 // taking damage
                && victim.IsPlayer // entity taking damage is player
                && damager.IsPlayer // entity delivering damage is a player
                && victim != damager // entity taking damage is not taking damage from self
                )
            {
                switch (type)
                {
                    case 1:
                        damage = damage * (damageTaken / 100);
                        break;
                    case 2:
                        damage = damage * (damageDone / 100);
                        break;
                }
                damageEvent.Damage.Amount = damage;
            }
        }

        private void OnEntityDeath(EntityDeathEvent deathEvent)
        {
            Player boss = deathEvent.Entity.Owner;
            if (boss == null) return;
            Player killer = deathEvent.KillingDamage.DamageSource.Owner;
            if (killer == null) return;

            if (BossList.Contains(boss.Id))
            {
                BossList.Remove(boss.Id);
                PrintToChat(GetMessage("BossKilled"));
                CheckAllBossesAreGone();

                if (bossGold)
                {
                    if (!guildBossGold)
                    {
                        if (boss.GetGuild().Name != killer.GetGuild().Name)
                        {
                            PrintToChat(killer, GetMessage("GuildmemberKill", killer.Id.ToString()));
                            return;
                        }
                    }
                    if (plugins.Exists("GrandExchange"))
                    {
                        GrandExchange.Call("GiveGold", new object[] { deathEvent.KillingDamage.DamageSource.Owner, rewardAmount });
                        PrintToChat(killer, string.Format(GetMessage("GoldGained", killer.Id.ToString()), rewardAmount.ToString()));
                    }
                }
            }
        }

        private void OnPlayerDisconnected(Player player)
        {
            if (BossList.Contains(player.Id)) BossList.Remove(player.Id);
            CheckAllBossesAreGone();
        }

        #endregion

        #region Utility

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}
