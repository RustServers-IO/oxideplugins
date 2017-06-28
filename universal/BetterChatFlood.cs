using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BetterChatFlood", "Ryan", "1.0.2")]
    [Description("Puts a cooldown on peoples messages preventing them flooding the chat.")]
    internal class BetterChatFlood : RustPlugin
    {
        private ConfigFile _Config;
        [PluginReference] private Plugin BetterChat;
        private Dictionary<string, DateTime> cooldowns = new Dictionary<string, DateTime>();
        private Dictionary<string, int> thresholds = new Dictionary<string, int>();
        private bool canBetterChat = true;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Permission Name")]
            public string perm;

            [JsonProperty(PropertyName = "Cooldown Period (seconds)")]
            public float cooldown;

            [JsonProperty(PropertyName = "Number of messages before cooldown")]
            public int threshold;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    perm = "betterchatflood.bypass",
                    cooldown = 5,
                    threshold = 3
                };
            }
        }

        protected override void LoadDefaultConfig() => _Config = ConfigFile.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _Config = Config.ReadObject<ConfigFile>();
        }

        protected override void SaveConfig() => Config.WriteObject(_Config);

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Cooldown"] = "Try again in {0} seconds"
            }, this);
        }

        private void Unload() => cooldowns.Clear();

        private void Init()
        {
            SaveConfig();
            permission.RegisterPermission(_Config.perm, this);
        }

        private void OnServerInitialized()
        {
            if(!BetterChat)
                Unsubscribe(nameof(OnBetterChat));
            if (BetterChat)
            {
                bool isSupported = new Version($"{BetterChat.Version.Major}.{BetterChat.Version.Minor}.{BetterChat.Version.Patch}") < new Version("5.0.6") ? false : true;
                if (!isSupported)
                {
                    Unsubscribe(nameof(OnBetterChat));
                    PrintWarning("This plugin is only compatable with BetterChat version 5.0.6 or greater!");
                }
            }
        }

        private double GetNextMsgTime(IPlayer player)
        {
            if (cooldowns[player.Id].AddSeconds(_Config.cooldown) > DateTime.Now)
                return Math.Floor((cooldowns[player.Id].AddSeconds(_Config.cooldown) - DateTime.Now).TotalSeconds);
            return 0;
        }

        private object IsFlooding(IPlayer player, string action = null)
        {
            if (cooldowns.ContainsKey(player.Id))
            {
                if (permission.UserHasPermission(player.Id, _Config.perm)) return null;
                bool hasCooldown = GetNextMsgTime(player) != 0 ? true : false;
                if (hasCooldown)
                {
                    if (thresholds.ContainsKey(player.Id))
                    {
                        if (thresholds[player.Id] > _Config.threshold)
                        {
                            if (action != null)
                                player.Reply(lang.GetMessage("Cooldown", this, player.Id), GetNextMsgTime(player));
                            return true;
                        }
                        else if (action != null)
                        {
                            thresholds[player.Id] = ++thresholds[player.Id];
                            cooldowns.Remove(player.Id);
                            cooldowns.Add(player.Id, DateTime.Now);
                        }
                        return null;
                    }
                    else if (!thresholds.ContainsKey(player.Id))
                        thresholds.Add(player.Id, 1);
                    return null;
                }
                else if (!hasCooldown)
                    if (cooldowns.ContainsKey(player.Id))
                    {
                        cooldowns.Remove(player.Id);
                        if (thresholds.ContainsKey(player.Id))
                            thresholds.Remove(player.Id);
                    }
            }
            if (action != null && !cooldowns.ContainsKey(player.Id))
                cooldowns.Add(player.Id, DateTime.Now);
            return null;
        }

        private object OnUserChat(IPlayer player, string message) => IsFlooding(player, "chat");

        private object OnBetterChat(Dictionary<string, object> data) => IsFlooding(data["Player"] as IPlayer);
    }
}