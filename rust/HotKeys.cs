using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("HotKeys", "rustservers.io", "0.0.5", ResourceId = 2135)]
    class HotKeys : RustPlugin
    {
        private Dictionary<string, Dictionary<string, object>> keys = new Dictionary<string, Dictionary<string, object>>();
        private bool ResetDefaultKeysOnJoin;

        Dictionary<string, string> defaultRustBinds = new Dictionary<string, string>()
        {
            {"f1", "consoletoggle"},
            {"backquote", "consoletoggle"},
            {"f7", "bugreporter"},
            {"w", "+forward"},
            {"s", "+backward"},
            {"a", "+left"},
            {"d", "+right"},
            {"mouse0", "+attack"},
            {"mouse1", "+attack2"},
            {"mouse2", "+attack3"},
            {"1", "+slot1"},
            {"2", "+slot2"},
            {"3", "+slot3"},
            {"4", "+slot4"},
            {"5", "+slot5"},
            {"6", "+slot6"},
            {"7", "+slot7"},
            {"8", "+slot8"},
            {"leftshift", "+sprint"},
            {"rightshift", "+sprint"},
            {"leftalt", "+altlook"},
            {"r", "+reload"},
            {"space", "+jump"},
            {"leftcontrol", "+duck"},
            {"e", "+use"},
            {"v", "+voice"},
            {"t", "chat.open"},
            {"return", "chat.open"},
            {"mousewheelup", "+invprev"},
            {"mousewheeldown", "+invnext"},
            {"tab", "inventory.toggle "},
        };

        void Loaded()
        {
            CheckConfig();
            keys.Add("default", GetConfig("Settings", "default", GetDefaultKeys()));

            foreach (var group in permission.GetGroups())
            {
                if (group != "default")
                {
                    var groupKeys = GetConfig("Settings", group, GetEmptyKeys());
                    if (groupKeys != null && groupKeys.Count > 0)
                    {
                        keys.Add(group, groupKeys);
                    }
                }
            }
            ResetDefaultKeysOnJoin = GetConfig("Settings", "ResetDefaultKeysOnJoin", true);

            BindAll();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (ResetDefaultKeysOnJoin)
            {
                BindDefaultKeys(player);
            }
            BindKeys(player);
        }

        [ConsoleCommand("hotkey.bind")]
        private void ccHotKeyBind(ConsoleSystem.Arg arg)
        {
            string group = "default";
            if (arg.Connection != null && arg.Connection.authLevel < 1)
            {
                return;
            }

            if (arg.Args.Length == 1)
            {
                string keyCombo = arg.Args[0].Trim();
                if(keys[group].ContainsKey(keyCombo)) {
                    SendReply(arg, keyCombo + ": " + keys[group][keyCombo].ToString());
                    SaveBinds();
                    BindAll();
                } else {
                    SendReply(arg, "[HotKeys] No such binding");
                }
            } else if(arg.Args.Length == 2) {
                string keyCombo = arg.Args[0].Trim();
                string bind = arg.Args[1].Trim();

                if (keys[group].ContainsKey(keyCombo))
                {
                    SendReply(arg, "[HotKeys] Replaced " + keyCombo + ": " + bind);
                    keys[group][keyCombo] = bind;
                }
                else
                {
                    SendReply(arg, "[HotKeys] Bound " + keyCombo + ": " + bind);
                    keys[group].Add(keyCombo, bind);
                }

                SaveBinds();
                BindAll();
            }
            else
            {
                SendReply(arg, "[HotKeys] Invalid Syntax. hotkey.bind \"keyCombo\" [bind]");
            }
        }

        [ConsoleCommand("hotkey.unbind")]
        private void ccHotKeyUnbind(ConsoleSystem.Arg arg)
        {
            string group = "default";
            if (arg.Connection != null && arg.Connection.authLevel < 1)
            {
                return;
            }

            if (arg.Args.Length == 1)
            {
                string keyCombo = arg.Args[0].Trim();

                if (keys[group].ContainsKey(keyCombo))
                {
                    string bind = keys[group][keyCombo].ToString();
                    keys[group].Remove(keyCombo);
                    if (defaultRustBinds.ContainsKey(keyCombo))
                    {
                        SendReply(arg, "[HotKeys] Reverted " + keyCombo + ": " + defaultRustBinds[keyCombo]);
                    }
                    else
                    {
                        SendReply(arg, "[HotKeys] Unbound " + keyCombo + ": " + bind);
                    }
                    
                    SaveBinds();
                    UnbindAll(keyCombo);
                }
            }
            else
            {
                SendReply(arg, "[HotKeys] Invalid Syntax. hotkey.unbind \"keyCombo\"");
            }
        }

        void BindAll()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (ResetDefaultKeysOnJoin)
                {
                    BindDefaultKeys(player);
                }
                BindKeys(player);
            }
        }

        void UnbindAll(string keyCombo)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                UnbindKey(player, keyCombo);
            }
        }

        void BindDefaultKeys(BasePlayer player)
        {
            foreach (KeyValuePair<string, string> kvp in defaultRustBinds)
            {
                player.SendConsoleCommand("bind " + kvp.Key + " " + kvp.Value.ToString());
            }
        }

        void BindKeys(BasePlayer player)
        {
            foreach (KeyValuePair<string, Dictionary<string, object>> kvp in keys)
            {
                var group = kvp.Key;
                var binds = kvp.Value;

                if (binds != null && binds.Count > 0)
                {
                    if (permission.UserHasGroup(player.UserIDString, group))
                    {
                        foreach (KeyValuePair<string, object> kvp2 in binds)
                        {
                            player.SendConsoleCommand("bind " + kvp2.Key + " " + kvp2.Value.ToString());
                        }
                    }
                }
            }
            
        }

        void UnbindKey(BasePlayer player, string keyCombo)
        {
            string defaultRustBind = "";
            if (defaultRustBinds.ContainsKey(keyCombo))
            {
                defaultRustBind = defaultRustBinds[keyCombo];
            }
            player.SendConsoleCommand("bind " + keyCombo + " \"" + defaultRustBind + "\"");
        }

        void SaveBinds()
        {
            Config["Settings", "default"] = keys;
            Config.Save();
        }

        void LoadDefaultConfig()
        {
            Config["Settings", "default"] = GetDefaultKeys();
            Config["Settings", "ResetDefaultKeysOnJoin"] = GetConfig("Settings","ResetDefaultKeysOnJoin", true);

            Config["VERSION"] = Version.ToString();
        }

        void CheckConfig()
        {
            if (Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig();
            }
            else if (GetConfig<string>("VERSION", "") != Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig();
            }
        }

        protected void ReloadConfig()
        {
            Config["VERSION"] = Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            Config["Settings", "ResetDefaultKeysOnJoin"] = GetConfig("Settings", "ResetDefaultKeysOnJoin", true);
            if (Config["Settings", "default"] == null)
            {
                Config["Settings", "default"] = GetConfig("Settings", "Keys", GetDefaultKeys());
            }
            // END NEW CONFIGURATION OPTIONS

            PrintToConsole("Upgrading configuration file");
            SaveConfig();
        }

        void OnUserGroupAdded(string id, string name)
        {
            Dictionary<string, object> binds;
            if(keys.TryGetValue(name, out binds)) {
                BasePlayer player = BasePlayer.Find(id);
                if(player is BasePlayer) {
                    BindKeys(player);
                }
            }
        }

        Dictionary<string, object> GetDefaultKeys()
        {
            return new Dictionary<string, object>()
            {
                {"i", "inventory.toggle"},
                {"c", "duck"},
                {"z", "+attack;+duck"},
                {"f", "forward;sprint"},
            };
        }

        Dictionary<string, object> GetEmptyKeys()
        {
            return new Dictionary<string, object>()
            {
            };
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private T GetConfig<T>(string name, string name2, T defaultValue)
        {
            if (Config[name, name2] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name, name2], typeof(T));
        }
    }
}
