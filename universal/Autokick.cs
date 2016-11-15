using System;
using System.Linq;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Autokick", "Exel80", "1.1.2", ResourceId = 2138)]
    [Description("Autokick help you change your server to \"maintenance break\" mode, if you need it!")]
    class Autokick : CovalencePlugin
    {
        #region Initialize
        public bool DEBUG = false;
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Loaded()
        {
            LoadConfigValues();

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Toggle"] = "Autokick is [#yellow]{0}[/#]",
                ["KickHelp"] = "When Autokick is [#yellow]{0}[/#], use [#yellow]{1}[/#] to kick all online players.",
                ["Kicked"] = "All online players has been kicked! Except players that have [#yellow]{0}[/#] permission or is admin.",
                ["Set"] = "Kick message is now setted to \"[#yellow]{0}[/#]\"",
                ["Message"] = "Kick message is \"[#yellow]{0}[/#]\"",
                ["ToggleHint"] = "Autokick must be [#yellow]{0}[/#], before can execute [#yellow]{1}[/#] command!",
                ["Usage"] = "[#cyan]Usage:[/#] [#silver]{0}[/#] [#grey]{1}[/#]"
            }, this);
        }
        #endregion

        #region Commands
        [Command("ak", "autokick")]
        private void cmdAK(IPlayer player, string command, string[] args)
        {
            // Checking that player has permission to use commands
            if (!hasPermission(player, "autokick.use"))
                return;

            string _name = player.Name;
            string _id = player.Id.ToString();

            // Check that args isn't empty.
            if (args?.Length < 1)
            {
                _chat(player, Lang("Usage", _id, $"/{command}", "on/off | kick | set | message"));
                return;
            }

            _debug(player, $"arg: {args[0]} - Name: {_name} - Id: {_id} - isAdmin: {player.IsAdmin}");

            switch (args[0])
            {
                case "on":
                    {
                        // Change Toggle from config file
                        _config.Settings["Enabled"] = "true";
                        Config["Settings", "Enabled"] = "true";

                        // Save config
                        Config.Save();

                        // Print Toggle
                        _chat(player, Lang("Toggle", _id, "ACTIVATED!") + "\n" + Lang("KickHelp", _id, "true", "/ak kick"));
                        _debug(player, $"Changed Toggle to {_config.Settings["Enabled"]}");
                    }
                    break;
                case "kick":
                    {
                        // Check if Toggle isn't False
                        if (_config.Settings["Enabled"] != "true")
                        {
                            _chat(player, Lang("ToggleHint", _id, "true", "/ak kick"));
                            return;
                        }
                        // Kick all players (Except if config allow auth 1 and/or 2 to stay)
                        foreach (IPlayer clients in players.Connected.ToList())
                            Kicker(clients);

                        _chat(player, Lang("Kicked", _id, "autokick.join"));
                    }
                    break;
                case "off":
                    {
                        // Change Toggle from config file
                        _config.Settings["Enabled"] = "false";
                        Config["Settings", "Enabled"] = "false";

                        // Save config
                        Config.Save();

                        // Print Toggle
                        _chat(player, Lang("Toggle", _id, "DE-ACTIVATED!"));
                        _debug(player, $"Changed Toggle to {_config.Settings["Enabled"]}");
                    }
                    break;
                case "set":
                    {
                        // Checking that args length isnt less then 5
                        if (args?.Length < 5)
                        {
                            _chat(player, Lang("Usage", _id, $"/{command}", "on/off | kick | set | message"));
                            return;
                        }

                        // Read all args to one string with space.
                        string _arg = string.Join(" ", args)?.Remove(0, 4);

                        // Change KickMessage from config file
                        _config.Settings["KickMessage"] = _arg;
                        Config["Settings", "KickMessage"] = _arg;

                        // Save config
                        Config.Save();

                        // Print KickMessage
                        _chat(player, Lang("Set", _id, _config.Settings["KickMessage"]));
                    }
                    break;
                case "message":
                    {
                        // Print KickMessage
                        _chat(player, Lang("Message", _id, _config.Settings["KickMessage"]));
                    }
                    break;
            }
        }
        #endregion

        #region PlayerJoin
        void OnPlayerInit(IPlayer client)
        {
            // If Autokick is enabled, then start timer (8sec)
            if (_config.Settings["Enabled"]?.ToLower() == "true")
            {
                timer.Once(8f, () =>
                {
                    try
                    {
                        foreach (IPlayer player in players.Connected.ToList())
                        {
                            string _name = player.Name;
                            string _id = player.Id.ToString();
                            string message = _config.Settings["KickMessage"];

                            if (DEBUG) Puts($"[Deubg] Name: {_name}, Id: {_id}, isAdmin: {player.IsAdmin}");

                            if (hasPermission(player, "autokick.join"))
                                return;

                            if (player.IsConnected)
                                player.Kick(message);
                        }
                    }
                    catch (Exception e) { PrintWarning($"{e.GetBaseException()}"); }
                });
            }
        }
        #endregion

        #region Kicker
        private void Kicker(IPlayer player)
        {
            try
            {
                if (_config.Settings["Enabled"]?.ToLower() == "true")
                {
                    string _name = player.Name;
                    string _id = player.Id.ToString();
                    string message = _config.Settings["KickMessage"];

                    if (DEBUG) Puts($"[Deubg] Name: {_name}, Id: {_id}");

                    if (hasPermission(player, "autokick.join"))
                        return;

                    if (player.IsConnected)
                        player.Kick(message);
                }
            }
            catch (Exception e) { PrintWarning($"{e.GetBaseException()}"); }
        }
        #endregion

        #region Helper
        private void _chat(IPlayer player, string msg) => player.Reply(covalence.FormatText($"{_config.Settings["Prefix"]} {msg}"));
        private void _debug(IPlayer player, string msg)
        {
            if(DEBUG)
                Puts($"[Debug] {player.Name} - {msg}");
        }
        bool hasPermission(IPlayer player, string permissionName)
        {
            if (player.IsAdmin) return true;
            return permission.UserHasPermission(player.Id.ToString(), permissionName);
        }
        #endregion

        #region Configuration Defaults
        PluginConfig DefaultConfig()
        {
            var defaultConfig = new PluginConfig
            {
                Settings = new Dictionary<string, string>
                {
                    { PluginSettings.Prefix, "[#cyan][AutoKick][/#]" },
                    { PluginSettings.KickMessage, "You have been kicked! Reason: Server is on maintenance break!" },
                    { PluginSettings.Enabled, "false" },
                }
            };
            return defaultConfig;
        }
        #endregion

        #region Configuration Setup
        private PluginConfig _config;
        private bool configChanged;

        class PluginSettings
        {
            public const string Prefix = "Prefix";
            public const string KickMessage = "KickMessage";
            public const string Enabled = "Enabled";
        }

        class PluginConfig
        {
            public Dictionary<string, string> Settings { get; set; }
        }

        protected override void LoadDefaultConfig() => Config.WriteObject(DefaultConfig(), true);

        void LoadConfigValues()
        {
            _config = Config.ReadObject<PluginConfig>();
            var defaultConfig = DefaultConfig();
            Merge(_config.Settings, defaultConfig.Settings);

            if (!configChanged) return;
            PrintWarning("Configuration file updated.");
            Config.WriteObject(_config);
        }

        void Merge<T1, T2>(IDictionary<T1, T2> current, IDictionary<T1, T2> defaultDict)
        {
            foreach (var pair in defaultDict)
            {
                if (current.ContainsKey(pair.Key)) continue;
                current[pair.Key] = pair.Value;
                configChanged = true;
            }
            var oldPairs = defaultDict.Keys.Except(current.Keys).ToList();
            foreach (var oldPair in oldPairs)
            {
                configChanged = true;
            }
        }
        #endregion
    }
}