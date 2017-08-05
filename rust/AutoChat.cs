using System;
using System.Globalization;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AutoChat", "Frenk92", "0.4.2", ResourceId = 2230)]
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
        const string configVersion = "0.2.1";

        ConfigData _config;
        class ConfigData
        {
            public bool Enabled { get; set; }
            public bool PlayerActive { get; set; }
            public Dictionary<string, List<string>> CustomChat { get; set; }
            public UIConfig UISettings { get; set; }
            public string Version { get; set; }
        }

        class UIConfig
        {
            public string BackgroundColor { get; set; }
            public string TextColor { get; set; }
            public string AnchorMin { get; set; }
            public string AnchorMax { get; set; }
        }

        ConfigData DefaultConfig()
        {
            var config = new ConfigData
            {
                Enabled = true,
                PlayerActive = false,
                CustomChat = new Dictionary<string, List<string>>() { { "Test", new List<string> { "command1", "command2" } } },
                UISettings = new UIConfig
                {
                    BackgroundColor = "0.29 0.49 0.69 0.5",
                    TextColor = "#0000FF",
                    AnchorMin = "0 0.125",
                    AnchorMax = "0.012 0.1655"
                },
                Version = configVersion
            };
            return config;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a configuration file.");
            Config.Clear();
            _config = DefaultConfig();
            SaveConfigData();
        }

        void UpdateConfig()
        {
            LoadConfigData();
            if (_config.Version == configVersion) return;

            var oldConfig = _config;
            _config = DefaultConfig();

            SetConfig(oldConfig.Enabled, _config.Enabled);
            SetConfig(oldConfig.PlayerActive, _config.PlayerActive);
            SetConfig(oldConfig.CustomChat, _config.CustomChat);
            if (oldConfig.UISettings != null)
            {
                SetConfig(oldConfig.UISettings.BackgroundColor, _config.UISettings.BackgroundColor);
                SetConfig(oldConfig.UISettings.TextColor, _config.UISettings.TextColor);
                SetConfig(oldConfig.UISettings.AnchorMin, _config.UISettings.AnchorMin);
                SetConfig(oldConfig.UISettings.AnchorMax, _config.UISettings.AnchorMax);
            }

            SaveConfigData();
        }

        void SetConfig<T>(T oldSet, T newSet)
        {
            if (oldSet != null)
                newSet = oldSet;
        }

        private void LoadConfigData() => _config = Config.ReadObject<ConfigData>();
        private void SaveConfigData() => Config.WriteObject(_config, true);
        #endregion

        #region Data
        Dictionary<ulong, string> chatUser = new Dictionary<ulong, string>();

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

        private void LoadData() { Users = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerChat>>("AutoChat"); }
        private void SaveData() { Interface.Oxide.DataFileSystem.WriteObject("AutoChat", Users); }

        PlayerChat GetPlayerData(BasePlayer player)
        {
            PlayerChat playerData;
            if (!Users.TryGetValue(player.userID, out playerData))
            {
                Users[player.userID] = playerData = new PlayerChat(player.displayName, _config.PlayerActive);
                SaveData();
            }
            if (!chatUser.ContainsKey(player.userID) && Users[player.userID].Active) chatUser.Add(player.userID, "g");

            return playerData;
        }
        #endregion

        #region Hooks
        void OnServerInitialized() { CheckPlugins(); }

        void Loaded()
        {
            LoadData();
            UpdateConfig();
            DefaultMessages();

            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);

            if (_config.Enabled)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if ((!Users.ContainsKey(p.userID) && !_config.PlayerActive) || 
                        (p.IsSleeping() || p.IsWounded() || p.IsDead())) continue;
                    GetPlayerData(p);
                    ToggleUI(p, true);
                }
            }
        }

        void Unload()
        {
            chatUser.Clear();

            foreach (var p in chatUI) DestroyUI(p);
            chatUI.Clear();

        }

        void OnPlayerSleep(BasePlayer player) { ToggleUI(player); }

        void OnPlayerSleepEnded(BasePlayer player) { ToggleUI(player, true); }

        void OnPlayerWound(BasePlayer player) { ToggleUI(player); }

        void OnPlayerRecover(BasePlayer player) { ToggleUI(player, true); }

        void OnPlayerDie(BasePlayer player, HitInfo info) { ToggleUI(player); }

        void OnPlayerInit(BasePlayer player)
        {
            if (!_config.Enabled || (!_config.PlayerActive && !Users.ContainsKey(player.userID))) return;
			GetPlayerData(player);
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (chatUser.ContainsKey(player.userID))
                chatUser.Remove(player.userID);

            ToggleUI(player);
        }
        #endregion

        #region Commands
        [ChatCommand("ac")]
        private void cmdAutoChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0 || args == null) return;
            if (!_config.Enabled && !HasPermission(player.UserIDString, PermAdmin))
            {
                MessageChat(player, Lang("IsDisabled", player.UserIDString));
                return;
            }
            if (!HasPermission(player.UserIDString, PermUse))
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
                            var playerData = GetPlayerData(player);
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
                            {
								chatUser.Add(player.userID, "g");
                                ToggleUI(player, true);
                                MessageChat(player, Lang("Activated", player.UserIDString));
                            }
                            else
                            {
                                ToggleUI(player);
                                chatUser.Remove(player.userID);
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

                            var enabled = _config.Enabled;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out enabled))
                                {
                                    MessageChat(player, Lang("ErrorBool", player.UserIDString));
                                    break;
                                }
                                _config.Enabled = enabled;
                            }
                            else
                            {
                                if (enabled)
                                    _config.Enabled = false;
                                else
                                    _config.Enabled = true;
                            }

                            if (enabled)
                            {
                                MessageChat(player, Lang("Enabled", player.UserIDString));
                                foreach (var p in BasePlayer.activePlayerList)
                                {
                                    if ((!Users.ContainsKey(p.userID) && !_config.PlayerActive) ||
                                        (p.IsSleeping() || p.IsWounded() || p.IsDead())) continue;
                                    GetPlayerData(p);
                                    ToggleUI(p, true);
                                }
                            }
                            else
                            {
                                chatUser.Clear();
                                foreach (var p in chatUI) ToggleUI(p);
                                MessageChat(player, Lang("Disabled", player.UserIDString));
                            }
                            SaveConfigData();
                            break;
                        }
                    case "auto":
                        {
                            if (!HasPermission(player.UserIDString, PermAdmin))
                            {
                                MessageChat(player, Lang("NoPerm", player.UserIDString));
                                break;
                            }

                            var pa = _config.PlayerActive;
                            if (args.Length > 1)
                            {
                                if (!bool.TryParse(args[1], out pa))
                                {
                                    MessageChat(player, Lang("ErrorBool", player.UserIDString));
                                    break;
                                }
                                _config.PlayerActive = pa;
                            }
                            else
                            {
                                if (pa)
                                    _config.PlayerActive = false;
                                else
                                    _config.PlayerActive = true;
                            }

                            if (pa)
                                MessageChat(player, Lang("AutoON", player.UserIDString));
                            else
                                MessageChat(player, Lang("AutoOFF", player.UserIDString));
                            SaveConfigData();
                            break;
                        }
                }
            }
            catch { }
        }

        [ChatCommand("g")]
        private void cmdGlobalChat(BasePlayer player, string command, string[] args)
        {
            if (!_config.Enabled || !isActive(player.userID)) return;
            var flag = false;
            if (args.Length == 0 || args == null)
            {
                if (chatUser[player.userID] == "g") return;
                MessageChat(player, Lang("GlobalChat", player.UserIDString));
                flag = true;
            }
            if (flag || chatUser[player.userID] != "g")
            {
                chatUser[player.userID] = "g";
                UpdateUI(player);
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

            switch (arg.Args[0])
            {
                case "enable":
                    {
                        var enabled = _config.Enabled;
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out enabled))
                            {
                                Puts(Lang("ErrorBool"));
                                break;
                            }
                            _config.Enabled = enabled;
                        }
                        else
                        {
                            if (enabled)
                                _config.Enabled = false;
                            else
                                _config.Enabled = true;
                        }

                        if (enabled)
                        {
                            Puts(Lang("Enabled"));
                            foreach (var p in BasePlayer.activePlayerList)
                            {
                                if ((!Users.ContainsKey(p.userID) && !_config.PlayerActive) ||
                                    (p.IsSleeping() || p.IsWounded() || p.IsDead())) continue;
                                GetPlayerData(p);
                                ToggleUI(p, true);
                            }
                        }
                        else
                        {
                            chatUser.Clear();
                            foreach (var p in chatUI) ToggleUI(p);
                            Puts(Lang("Disabled"));
                        }
                        SaveConfigData();
                        break;
                    }
                case "auto":
                    {
                        var pa = _config.PlayerActive;
                        if (arg.Args.Length > 1)
                        {
                            if (!bool.TryParse(arg.Args[1], out pa))
                            {
                                Puts(Lang("ErrorBool"));
                                break;
                            }
                            _config.PlayerActive = pa;
                        }
                        else
                        {
                            if (pa)
                                _config.PlayerActive = false;
                            else
                                _config.PlayerActive = true;
                        }

                        if (pa)
                            Puts(Lang("AutoON"));
                        else
                            Puts(Lang("AutoOFF"));
                        SaveConfigData();
                        break;
                    }
            }
        }
        #endregion

        #region Methods
        void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (!_config.Enabled || arg == null || arg.Connection == null || arg.Args == null) return;

            var str = arg.GetString(0);
            if (str.Length == 0 || str[0] != '/') return;

            var player = (BasePlayer)arg.Connection.player;
            if (!player || !HasPermission(player.UserIDString, PermUse) || !isActive(player.userID)) return;

            var args = str.Split(' ');
            var command = args[0].Replace("/", "");
            var cmdtarget = command + " $target";
            if (!ChatType.Contains(command) && !ChatType.Contains(cmdtarget)) return;

            if (!chatUser[player.userID].Contains(command) || (ChatType.Contains(cmdtarget) && !chatUser[player.userID].Equals(command + " " + args[1])))
            {
                chatUser[player.userID] = command + (ChatType.Contains(cmdtarget) ? $" {args[1]}" : "");
                UpdateUI(player);
            }
        }


        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (!_config.Enabled) return null;

            var player = (BasePlayer)arg.Connection.player;
            if (!player || !HasPermission(player.UserIDString, PermUse) || !isActive(player.userID)) return null;

            var cmd = chatUser[player.userID];
            if (cmd == "g") return null;

            var message = arg.GetString(0, "text");
            rust.RunClientCommand(player, "chat.say", $"/{cmd} {message}");

            if (BC)
                return null;
            else
                return false;
        }

        object OnBetterChat(Dictionary<string, object> data)
        {
            var player = (IPlayer)data["Player"];
            if (!_config.Enabled || !HasPermission(player.Id, PermUse)) return data;
            var bPlayer = Game.Rust.RustCore.FindPlayerByIdString(player.Id);
            if (!bPlayer || !isActive(bPlayer.userID) || chatUser[bPlayer.userID] == "g") return data;

            return false;
        }
        #endregion

        #region Messages
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
                ["NoPlugins"] = "The plugin was disabled because weren't found supported plugins.",
                ["ListPlugins"] = "Supported plugins: {0}{1}",
                ["Help"] = ">> AUTOCHAT HELP <<\n/ac active \"true/false:OPTIONAL\" - to active/deactive autochat.\n/g \"message:OPTIONAL\" - to send message and switch to global chat.",
                ["HelpAdmin"] = "\nAdmin Commands:\n/ac enable \"true/false:OPTIONAL\" - to enable/disable plugin.\n/ac auto \"true/false:OPTIONAL\" - to auto-active/deactive plugin for new players.",
            }, this);
        }

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        void MessageChat(BasePlayer player, string message, string args = null) => PrintToChat(player, $"{message}", args);
        #endregion

        #region Utilities
        void CheckPlugins()
        {
            var list = new List<string>();
            if (Clans)
            {
                ChatType.Add("c");
                if (Clans.ResourceId == 2087) //Universal Clans
                {
                    ChatType.Add("a");
                    list.Add("Clans");
                }
                else
                    list.Add("Rust:IO Clans");
            }
            if (plugins.Exists("PrivateMessage"))
            {
                ChatType.Add("pm $target");
                ChatType.Add("r");
                list.Add("PrivateMessage");
            }
            if (Friends && Friends.ResourceId == 2120) //Universal Friends
            {
                ChatType.Add("fm");
                ChatType.Add("f");
                ChatType.Add("pm $target");
                ChatType.Add("m $target");
                ChatType.Add("rm");
                ChatType.Add("r");
                list.Add("Friends");
            }

            var lcus = new List<string>();
            foreach (var p in _config.CustomChat)
                if (plugins.Exists(p.Key))
                {
                    foreach (var c in p.Value) ChatType.Add(c);
                    lcus.Add(p.Key);
                }

            if (ChatType.Count == 0)
            {
                _config.Enabled = false;
                PrintWarning(Lang("NoPlugins"));
            }
            else
            {
                Puts(Lang("ListPlugins", null, string.Join(", ", list.ToArray()), lcus.Count != 0 ? "\nCustomChat: " + string.Join(", ", lcus.ToArray()) : null));
                list.Clear();
                lcus.Clear();
            }

            if (BetterChat)
            {
                var v = Convert.ToInt32(BetterChat.Version.ToString().Split('.')[0]);
                if (v >= 5) BC = false;
            }
            else
                BC = false;
        }

        bool isActive(ulong id) => Users.ContainsKey(id) && Users[id].Active;

        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        #endregion

        #region CUI
        static string cuiJson = @"[
        {
            ""name"": ""backAC"",
            ""parent"": ""Hud"",
            ""components"": [
              {
                ""type"": ""UnityEngine.UI.Image"",
                ""color"": ""{BackColor}""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""{AnchorMin}"",
                ""anchormax"": ""{AnchorMax}""
              }
            ]
          },
          {
            ""name"": ""lblAC"",
            ""parent"": ""backAC"",
            ""components"": [
              {
                ""text"": ""{Command}"",
                ""type"": ""UnityEngine.UI.Text"",
                ""color"": ""{TextColor}"",
                ""fontSize"": 15,
                ""align"": ""MiddleCenter""
              },
              {
                ""type"": ""RectTransform"",
                ""anchormin"": ""0 0"",
                ""anchormax"": ""1 1""
              }
            ]
          }
        ]";

        List<BasePlayer> chatUI = new List<BasePlayer>();
        
        void ToggleUI(BasePlayer player, bool show=false)
        {
            if (!_config.Enabled || (!isActive(player.userID) && show)) return;
            if (!chatUI.Contains(player) && show)
            {
                AddUI(player);
                chatUI.Add(player);
            }
            else if (chatUI.Contains(player) && !show)
            {
                DestroyUI(player);
                chatUI.Remove(player);
            }
        }

        void AddUI(BasePlayer player)
        {
            var backColor = Color(_config.UISettings.BackgroundColor);
            var textColor = Color(_config.UISettings.TextColor);
            var command = chatUser[player.userID].Split(' ')[0];
            var cui = cuiJson.Replace("{BackColor}", backColor)
                            .Replace("{TextColor}", textColor)
                            .Replace("{AnchorMin}", _config.UISettings.AnchorMin)
                            .Replace("{AnchorMax}", _config.UISettings.AnchorMax)
                            .Replace("{Command}", command);
            CuiHelper.AddUi(player, cui);
        }

        void UpdateUI(BasePlayer player)
        {
            DestroyUI(player);
            AddUI(player);
        }

        void DestroyUI(BasePlayer player) => CuiHelper.DestroyUi(player, "backAC");

        public static string Color(string hexColor)
        {
            if (!hexColor.StartsWith("#")) return hexColor;
            if (hexColor.StartsWith("#")) hexColor = hexColor.TrimStart('#');
            int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} 1";
        }
        #endregion
    }
}
