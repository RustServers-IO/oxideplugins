using System;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AutoChat", "Frenk92", "0.3.0", ResourceId = 2230)]
    [Description("Automatic clans/private chat switching")]
    class AutoChat : RustPlugin
    {
        [PluginReference]
        Plugin Friends, Clans, BetterChat;

        bool BC = true;
        const string PermAdmin = "autochat.admin";
        const string PermUse = "autochat.use";
        List<string> ChatType = new List<string>();
        
        #region Config
        bool Enabled = true;
        bool PlayerActive = false;
        Dictionary<string, List<string>> CustomChat = new Dictionary<string, List<string>>() { { "Test", new List<string> { "command1", "command2" } } };

        const string configVersion = "0.1.1";

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a configuration file.");
            Config.Clear();
        }

        void LoadConfigData()
        {
            var version = (string)Config["Version"];

            Enabled = SetConfig("Enabled", Enabled);
            PlayerActive = SetConfig("PlayerActive", PlayerActive);
            CustomChat = SetConfig("CustomChat", CustomChat);

            if(version != configVersion)
            {
                PrintWarning("Configuration is outdate. Update in progress...");

                SetConfig("Version", configVersion, true);
            }
        }

        T SetConfig<T>(string name, T data, bool edit=false)
        {
            if (Config[name] == null || edit)
            {
                Config[name] = data;
                SaveConfig();
                return data;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion

        #region Data
        Dictionary<ulong, PlayerChat> Users = new Dictionary<ulong, PlayerChat>();
        class PlayerChat
        {
            public string Name { get; set; }
            public bool Active { get; set; }

            public PlayerChat(string Name, bool Active)
            {
                this.Name = Name;
                this.Active = Active;
            }
        }

        Dictionary<ulong, ChatInfo> chatInfo = new Dictionary<ulong, ChatInfo>();
        class ChatInfo
        {
            public string Command { get; set; }
            public string Target { get; set; }

            public ChatInfo()
            {
                Command = "";
                Target = "";
            }
        }

        private void LoadData() { Users = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerChat>>("AutoChat"); }
        private void SaveData() { Interface.Oxide.DataFileSystem.WriteObject("AutoChat", Users); }
        #endregion

        #region Hooks
        void OnServerInitialized() { CheckPlugins(); }

        void Loaded()
        {
            LoadData();
            LoadConfigData();
            DefaultMessages();

            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);
        }

        void Unload() { chatInfo.Clear(); }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if(chatInfo.ContainsKey(player.userID))
                chatInfo.Remove(player.userID);
        }
        #endregion

        #region Commands
        [ChatCommand("ac")]
        private void cmdAutoChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0 || args == null) return;
            if (!Enabled && !HasPermission(player.UserIDString, PermAdmin))
            {
                MessageChat(player, Lang("IsDisabled", player.UserIDString));
                return;
            }
            if(!HasPermission(player.UserIDString, PermUse))
            {
                MessageChat(player, Lang("NoPerm", player.UserIDString));
                return;
            }

            try
            {
                switch (args[0])
                {
                    case "help":
                        {
                            if (HasPermission(player.UserIDString, PermAdmin))
                                MessageChat(player, Lang("Help", player.UserIDString) + Lang("HelpAdmin", player.UserIDString));
                            else
                                MessageChat(player, Lang("Help", player.UserIDString));
                            break;
                        }
                    case "active":
                        {
                            ChatInfo chatData;
                            var playerData = GetPlayerData(player, out chatData);
                            var flag = playerData.Active;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out flag))
                                {
                                    MessageChat(player, Lang("ErrorBool", player.UserIDString));
                                    break;
                                }
                                playerData.Active = flag;
                            }
                            else
                            {
                                if (flag)
                                    playerData.Active = false;
                                else
                                    playerData.Active = true;
                            }

                            if (playerData.Active)
                                MessageChat(player, Lang("Activated", player.UserIDString));
                            else
                            {
                                chatInfo.Remove(player.userID);
                                MessageChat(player, Lang("Deactivated", player.UserIDString));
                            }
                            SaveData();
                            break;
                        }
                    case "enable":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, Lang("NoPerm", player.UserIDString));
                                break;
                            }

                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out Enabled))
                                {
                                    MessageChat(player, Lang("ErrorBool", player.UserIDString));
                                    break;
                                }
                            }
                            else
                            {
                                if (Enabled)
                                    Enabled = false;
                                else
                                    Enabled = true;
                            }

                            if (Enabled)
                                MessageChat(player, Lang("Enabled", player.UserIDString));
                            else
                            {
                                chatInfo.Clear();
                                MessageChat(player, Lang("Disabled", player.UserIDString));
                            }
                            SetConfig("Enabled", Enabled, true);
                            break;
                        }
                    case "auto":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, Lang("NoPerm", player.UserIDString));
                                break;
                            }

                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out PlayerActive))
                                {
                                    MessageChat(player, Lang("ErrorBool", player.UserIDString));
                                    break;
                                }
                            }
                            else
                            {
                                if (PlayerActive)
                                    PlayerActive = false;
                                else
                                    PlayerActive = true;
                            }

                            if (PlayerActive)
                                MessageChat(player, Lang("AutoON", player.UserIDString));
                            else
                                MessageChat(player, Lang("AutoOFF", player.UserIDString));
                            SetConfig("PlayerActive", PlayerActive, true);
                            break;
                        }
                }
            }
            catch { }
        }

        [ChatCommand("g")]
        private void cmdGlobalChat(BasePlayer player, string command, string[] args)
        {
            if (chatInfo.ContainsKey(player.userID))
                chatInfo[player.userID] = new ChatInfo();

            if (args.Length == 0 || args == null)
            {
                MessageChat(player, Lang("GlobalChat", player.UserIDString));
                return;
            }

            var message = string.Empty;
            for (var i = 0; i < args.Length; i++)
                message = $"{message} {args[i]}";

            rust.RunClientCommand(player, "chat.say", message);
        }

        [ConsoleCommand("ac")]
        private void consAutoChat(ConsoleSystem.Arg arg)
        {
            if ((arg.Connection != null && arg.Connection.authLevel < 2) || arg.Args.Length == 0 || arg.Args == null) return;

            switch(arg.Args[0])
            {
                case "enable":
                    {
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out Enabled))
                            {
                                Puts(Lang("ErrorBool"));
                                break;
                            }
                        }
                        else
                        {
                            if (Enabled)
                                Enabled = false;
                            else
                                Enabled = true;
                        }

                        if (Enabled)
                            Puts(Lang("Enabled"));
                        else
                        {
                            chatInfo.Clear();
                            Puts(Lang("Disabled"));
                        }
                        SetConfig("Enabled", Enabled, true);
                        break;
                    }
                case "auto":
                    {
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out PlayerActive))
                            {
                                Puts(Lang("ErrorBool"));
                                break;
                            }
                        }
                        else
                        {
                            if (PlayerActive)
                                PlayerActive = false;
                            else
                                PlayerActive = true;
                        }

                        if (PlayerActive)
                            Puts(Lang("AutoON"));
                        else
                            Puts(Lang("AutoOFF"));
                        SetConfig("PlayerActive", PlayerActive, true);
                        break;
                    }
            }
        }
        #endregion

        #region Methods
        void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!Enabled || arg == null || arg.Connection == null || arg.Args == null) return;

            var str = arg.GetString(0);
            if (str.Length == 0 || str[0] != '/') return;

            var player = (BasePlayer)arg.Connection.player;
            if (!player || !HasPermission(player.UserIDString, PermUse)) return;

            ChatInfo chatData;
            var playerData = GetPlayerData(player, out chatData);

            var args = str.Split(' ');
            var command = args[0].Replace("/", "");
            if (!playerData.Active || !ChatType.Contains(command)) return;

            if (!chatData.Command.Contains(command))
                chatData.Command = command;

            if (command == "pm" || command == "m")
            {
                if(args.Length > 2)
                    chatData.Target = args[1];
            }
            else
                chatData.Target = "";
        }


        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (!Enabled) return null;

            var player = (BasePlayer)arg.Connection.player;
            if (!player || !HasPermission(player.UserIDString, PermUse)) return null;

            ChatInfo chatData;
            var playerData = GetPlayerData(player, out chatData);

            if (!playerData.Active || chatData.Command == "") return null;

            var message = arg.GetString(0, "text");
            if (chatData.Command == "pm" || chatData.Command == "m")
                message = $"{chatData.Target} {message}";

            rust.RunClientCommand(player, "chat.say", $"/{chatData.Command} {message}");

            if (BC)
                return null;
            else
                return false;
        }

        object OnBetterChat(Dictionary<string, object> data)
        {
            var player = (IPlayer)data["Player"];
            if (!Enabled || !HasPermission(player.Id, PermUse)) return data;
            var bPlayer = Game.Rust.RustCore.FindPlayerByIdString(player.Id);
            if (!bPlayer) return data;
            ChatInfo chatData;
            var playerData = GetPlayerData(bPlayer, out chatData);
            if (!playerData.Active || chatData.Command == "") return data;

            return false;
        }

        PlayerChat GetPlayerData(BasePlayer player, out ChatInfo chatData)
        {
            PlayerChat playerData;
            if(!Users.TryGetValue(player.userID, out playerData))
            {
                Users[player.userID] = playerData = new PlayerChat(player.displayName, PlayerActive);
                SaveData();
            }
            if (!chatInfo.TryGetValue(player.userID, out chatData))
                chatInfo[player.userID] = chatData = new ChatInfo();

            return playerData;
        }

        void CheckPlugins()
        {
            if (Clans)
            {
                ChatType.Add("c");
                if (Clans.ResourceId == 2087) //Universal Clans
                    ChatType.Add("a");
            }
            if (plugins.Exists("PrivateMessage"))
            {
                ChatType.Add("pm");
                ChatType.Add("r");
            }
            if (Friends && Friends.ResourceId == 2120) //Universal Friends
            {
                ChatType.Add("fm");
                ChatType.Add("f");
                ChatType.Add("pm");
                ChatType.Add("m");
                ChatType.Add("rm");
                ChatType.Add("r");
            }

            foreach (var p in CustomChat)
                if (plugins.Exists(p.Key))
                    foreach (var c in p.Value) ChatType.Add(c);

            if (ChatType.Count == 0)
            {
                Enabled = false;
                PrintWarning("AutoChat was disabled because weren't found supported plugins.");
            }

            if (BetterChat)
            {
                var v = Convert.ToInt32(BetterChat.Version.ToString().Split('.')[0]);
                if (v >= 5) BC = false;
            }
            else
                BC = false;
        }
        #endregion

        #region Messages & Utilities
        void DefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>            {
                ["Enabled"] = "AutoChat was enabled.",
                ["Disabled"] = "AutoChat was disabled.",
                ["Activated"] = "You have active the autochat.",
                ["Deactivated"] = "You have deactive the autochat.",
                ["AutoON"] = "AutoChat is now auto-activated for new players.",
                ["AutoOFF"] = "AutoChat is now auto-deactivated for new players.",
                ["GlobalChat"] = "You switched to the global chat.",
                ["NoPerm"] = "You don't have permission to use this command.",
                ["IsDisabled"] = "The plugin is disabled.",
                ["ErrorBool"] = "Error. Only \"true\" or \"false\".",
                ["Help"] = ">> AUTOCHAT HELP <<\n/ac active \"true/false:OPTIONAL\" - to active/deactive autochat.\n/g \"message:OPTIONAL\" - to send message and switch to global chat.",
                ["HelpAdmin"] = "\nAdmin Commands:\n/ac enable \"true/false:OPTIONAL\" - to enable/disable plugin.\n/ac auto \"true/false:OPTIONAL\" - to auto-active/deactive plugin for new players.",
            }, this);
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        void MessageChat(BasePlayer player, string message, string args = null) => PrintToChat(player, $"{message}", args);

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        #endregion
    }
}
