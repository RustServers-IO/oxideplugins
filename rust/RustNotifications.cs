using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("RustNotifications", "seanbyrne88", "0.8.2")]
    [Description("Configurable Notifications for Rust Events")]
    class RustNotifications : RustPlugin
    {

        [PluginReference]
        Plugin Slack;

        [PluginReference]
        Plugin Discord;

        private static NotificationConfigContainer Settings;

        private List<NotificationCooldown> UserLastNotified;

        private string SlackMethodName;
        private string DiscordMethodName;

        #region oxide methods
        void Init()
        {
            LoadConfigValues();
        }

        void OnPlayerInit(BasePlayer player)
        {
            SendPlayerConnectNotification(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            SendPlayerDisconnectNotification(player, reason);
        }

        void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            SendBaseAttackedNotification(attacker, info);
        }
        #endregion

        #region chat commands
        [ChatCommand("rustNotifyResetConfig")]
        void CommandResetConfig(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin())
            {
                LoadDefaultConfig();
                LoadDefaultMessages();
                LoadConfigValues();
            }
            else
            {
                SendReply(player, lang.GetMessage("CommandReplyNotAdmin", this, player.UserIDString));
            }
        }

        [ChatCommand("rustNotifyResetMessages")]
        void CommandResetMessages(BasePlayer player, string command, string[] args)
        {
            if(player.IsAdmin())
            {
                LoadDefaultMessages();
            }
            else
            {
                SendReply(player, lang.GetMessage("CommandReplyNotAdmin", this, player.UserIDString));
            }
        }

        //args[0] = Slack|Discord|Server|All, args[1] Value (Between 0 and 100)
        [ChatCommand("rustNotifySetHealthThreshold")]
        void CommandSetHealthThreshold(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin())
            {
                if (args.Length == 2)
                {
                    int ThresholdPercentageHealthRemaining = int.Parse(args[1]);

                    if (args[0] == "All")
                    {
                        Settings.ServerConfig.ThresholdPercentageHealthRemaining = ThresholdPercentageHealthRemaining;
                        Settings.DiscordConfig.ThresholdPercentageHealthRemaining = ThresholdPercentageHealthRemaining;
                        Settings.SlackConfig.ThresholdPercentageHealthRemaining = ThresholdPercentageHealthRemaining;
                    }
                    else if (args[0] == "Server")
                    {
                        Settings.ServerConfig.ThresholdPercentageHealthRemaining = ThresholdPercentageHealthRemaining;
                    }
                    else if (args[0] == "Discord")
                    {
                        Settings.DiscordConfig.ThresholdPercentageHealthRemaining = ThresholdPercentageHealthRemaining;
                    }
                    else if (args[0] == "Slack")
                    {
                        Settings.SlackConfig.ThresholdPercentageHealthRemaining = ThresholdPercentageHealthRemaining;
                    }

                    //save config
                    Config.WriteObject<NotificationConfigContainer>(Settings);

                    SendReply(player, lang.GetMessage("CommandReplyThresholdHealthSet", this, player.UserIDString).Replace("{Value}", ThresholdPercentageHealthRemaining.ToString()));

                }
                else
                {
                    SendReply(player, lang.GetMessage("CommandReplyThresholdHealthInvalidArgs", this, player.UserIDString));
                }
            }
            else
            {
                SendReply(player, lang.GetMessage("CommandReplyNotAdmin", this, player.UserIDString));
            }
        }

        #endregion

        #region private methods

        private string GetDisplayNameByID(ulong UserID)
        {
            IPlayer player = this.covalence.Players.FindPlayer(UserID.ToString());
            // BasePlayer player = BasePlayer.Find(UserID.ToString());
            if(player == null)
            {
                PrintWarning(String.Format("Tried to find player with ID {0} but they weren't in active or sleeping player list", UserID.ToString()));
                return "Unknown";
            }
            else
            {
                return player.Name;
            }
        }

        private bool IsPlayerActive(ulong UserID)
        {
            if (BasePlayer.activePlayerList.Exists(x => x.userID == UserID))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool IsPlayerNotificationCooledDown(ulong UserID, NotificationType NotificationType, int CooldownInSeconds)
        {
            if (UserLastNotified.Exists(x => x.NotificationType == NotificationType && x.PlayerID == UserID))
            {
                //check notification time per user, per notificationType, if it's cooled down send a message
                DateTime LastNotificationTime = UserLastNotified.Find(x => x.NotificationType == NotificationType && x.PlayerID == UserID).LastNotifiedAt;
                if ((DateTime.Now - LastNotificationTime).TotalSeconds > CooldownInSeconds)
                {
                    UserLastNotified.Find(x => x.NotificationType == NotificationType && x.PlayerID == UserID).LastNotifiedAt = DateTime.Now;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                UserLastNotified.Add(new NotificationCooldown() { PlayerID = UserID, NotificationType = NotificationType, LastNotifiedAt = DateTime.Now });
                return true;
            }
        }
        #endregion

        #region notifications
        private void SendSlackNotification(BasePlayer player, string MessageText)
        {
            if (Settings.SlackConfig.Active)
            {
                Slack.Call(SlackMethodName, MessageText, BasePlayerToIPlayer(player));
            }
        }

        private void SendDiscordNotification(BasePlayer player, string MessageText)
        {
            if (Settings.DiscordConfig.Active)
            {
                Discord.Call(DiscordMethodName, MessageText);
            }
        }

        private IPlayer BasePlayerToIPlayer(BasePlayer player)
        {
            return covalence.Players.FindPlayerById(player.UserIDString);
        }

        private void SendPlayerConnectNotification(BasePlayer player)
        {
            if (Settings.SlackConfig.DoNotifyWhenPlayerConnects)
            {
                //string MessageText = Lang("PlayerConnectedMessageTemplate", player.UserIDString).Replace("{DisplayName}", player.displayName);
                string MessageText = lang.GetMessage("PlayerConnectedMessageTemplate", this, player.UserIDString).Replace("{DisplayName}", player.displayName);
                SendSlackNotification(player, MessageText);
            }

            if (Settings.DiscordConfig.DoNotifyWhenPlayerConnects)
            {
                string MessageText = lang.GetMessage("PlayerConnectedMessageTemplate", this, player.UserIDString).Replace("{DisplayName}", player.displayName);
                SendDiscordNotification(player, MessageText);
            }
        }

        private void SendPlayerDisconnectNotification(BasePlayer player, string reason)
        {
            if (Settings.SlackConfig.DoNotifyWhenPlayerDisconnects)
            {
                string MessageText = lang.GetMessage("PlayerDisconnectedMessageTemplate", this, player.UserIDString).Replace("{DisplayName}", player.displayName).Replace("{Reason}", reason);
                SendSlackNotification(player, MessageText);
            }

            if (Settings.DiscordConfig.DoNotifyWhenPlayerDisconnects)
            {
                string MessageText = lang.GetMessage("PlayerDisconnectedMessageTemplate", this, player.UserIDString).Replace("{DisplayName}", player.displayName).Replace("{Reason}", reason);
                SendDiscordNotification(player, MessageText);
            }
        }

        private void SendBaseAttackedNotification(BasePlayer player, HitInfo info)
        {
            if (info.HitEntity != null)
            {
                //First check if the HitEntity is owned by a player.
                if (info.HitEntity.OwnerID != 0)
                {
                    string MessageText = lang.GetMessage("BaseAttackedMessageTemplate", this, player.UserIDString)
                                                            .Replace("{Attacker}", player.displayName)
                                                            .Replace("{Owner}", GetDisplayNameByID(info.HitEntity.OwnerID))
                                                            .Replace("{Weapon}", info.Weapon.ShortPrefabName.Replace(".entity", ""))
                                                            .Replace("{Damage}", info.damageTypes.Total().ToString());

                    //get structure's percentage health remaining for check against threshold..
                    int PercentHealthRemaining = (int)((info.HitEntity.Health() / info.HitEntity.MaxHealth()) * 100);

                    //get damage
                    int DamageInflicted = (int)(info.damageTypes.Total());

                    if (IsPlayerActive(info.HitEntity.OwnerID) && IsPlayerNotificationCooledDown(info.HitEntity.OwnerID, NotificationType.ServerNotification, Settings.ServerConfig.NotificationCooldownInSeconds))
                    {
                        if(PercentHealthRemaining <= Settings.ServerConfig.ThresholdPercentageHealthRemaining)// && DamageInflicted >= Settings.ServerConfig.ThresholdDamageInflicted)
                        {
                            BasePlayer p = BasePlayer.activePlayerList.Find(x => x.userID == info.HitEntity.OwnerID);
                            PrintToChat(p, MessageText);
                        }
                    }
                    else
                    {
                        //Slack
                        if (Settings.SlackConfig.DoNotifyWhenBaseAttacked && IsPlayerNotificationCooledDown(info.HitEntity.OwnerID, NotificationType.SlackNotification, Settings.SlackConfig.NotificationCooldownInSeconds))
                        {
                            if (PercentHealthRemaining <= Settings.SlackConfig.ThresholdPercentageHealthRemaining)// && DamageInflicted >= Settings.SlackConfig.ThresholdDamageInflicted)
                            {
                                SendSlackNotification(player, MessageText);
                            }
                        }
                        //Discord
                        if (Settings.DiscordConfig.DoNotifyWhenBaseAttacked && IsPlayerNotificationCooledDown(info.HitEntity.OwnerID, NotificationType.DiscordNotification, Settings.DiscordConfig.NotificationCooldownInSeconds))
                        {
                            if (PercentHealthRemaining <= Settings.DiscordConfig.ThresholdPercentageHealthRemaining)// && DamageInflicted >= Settings.DiscordConfig.ThresholdDamageInflicted)
                            {
                                SendDiscordNotification(player, MessageText);
                            }
                        }
                    }
                }
            }
        }
        #endregion notifications

        #region localization
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"PlayerConnectedMessageTemplate", "{DisplayName} has joined the server"},
                    {"PlayerDisconnectedMessageTemplate", "{DisplayName} has left the server, reason: {Reason}"},
                    {"BaseAttackedMessageTemplate", "{Attacker} has attacked a structure built by {Owner}"},
                    {"CommandReplyNotAdmin", "Must be admin to use server commands" },
                    {"CommandReplyThresholdHealthSet", "Health Threshold has been set to {Value}" },
                    {"CommandReplyThresholdHealthInvalidArgs", "Error, Usage: \"rustNotifyThresholdHealthSet <type:All|Server|Discord|Slack> <value:Between 0 & 100>" },
                    {"TestMessage", "Hello World"}
                }, this);
        }
        #endregion

        #region config
        NotificationConfigContainer DefaultConfigContainer()
        {
            return new NotificationConfigContainer
            {
                ServerConfig = DefaultServerNotificationConfig(),
                SlackConfig = DefaultClientNotificationConfig(),
                DiscordConfig = DefaultClientNotificationConfig()
            };
        }

        ServerNotificationConfig DefaultServerNotificationConfig()
        {
            return new ServerNotificationConfig
            {
                Active = true,
                DoNotifyWhenBaseAttacked = true,
                NotificationCooldownInSeconds = 60,
                //ThresholdDamageInflicted = 0, //default to 0 so it sends after every hit.
                ThresholdPercentageHealthRemaining = 100 //default to 100 so it sends after every hit
            };
        }

        ClientNotificationConfig DefaultClientNotificationConfig()
        {
            return new ClientNotificationConfig
            {
                DoLinkSteamProfile = true,
                Active = false,
                DoNotifyWhenPlayerConnects = true,
                DoNotifyWhenPlayerDisconnects = true,
                DoNotifyWhenBaseAttacked = true,
                NotificationCooldownInSeconds = 60
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(DefaultConfigContainer(), true);

            PrintWarning("Default Configuration File Created");
            LoadDefaultMessages();
            PrintWarning("Default Language File Created");

            UserLastNotified = new List<NotificationCooldown>();
        }

        protected void LoadConfigValues()
        {
            Settings = Config.ReadObject<NotificationConfigContainer>();

            UserLastNotified = new List<NotificationCooldown>();

            if (Settings.SlackConfig.DoLinkSteamProfile)
                SlackMethodName = "FancyMessage";
            else
                SlackMethodName = "SimpleMessage";

            DiscordMethodName = "SendMessage";

            //Config.WriteObject<NotificationConfigContainer>(Settings);
        }
        #endregion

        #region classes
        private class ServerNotificationConfig
        {
            public bool Active { get; set; }
            public bool DoNotifyWhenBaseAttacked { get; set; }
            public int NotificationCooldownInSeconds { get; set; }
            //public int ThresholdDamageInflicted { get; set; }
            public int ThresholdPercentageHealthRemaining { get; set; }
        }

        private class ClientNotificationConfig : ServerNotificationConfig
        {
            public bool DoLinkSteamProfile { get; set; }
            public bool DoNotifyWhenPlayerConnects { get; set; }
            public bool DoNotifyWhenPlayerDisconnects { get; set; }
        }
        
        private class NotificationConfigContainer
        {
            public ServerNotificationConfig ServerConfig { get; set; }
            public ClientNotificationConfig SlackConfig { get; set; }
            public ClientNotificationConfig DiscordConfig { get; set; }
        }

        private enum NotificationType
        {
            SlackNotification,
            DiscordNotification,
            ServerNotification
        }

        private class NotificationCooldown
        {
            public NotificationType NotificationType { get; set; }
            public ulong PlayerID { get; set; }
            public DateTime LastNotifiedAt { get; set; }
        }

        #endregion
    }
}

