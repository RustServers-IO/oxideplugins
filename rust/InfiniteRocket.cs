using Oxide.Core.Plugins;
using System;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("InfiniteRocket", "deniskozlov", "0.2.1", ResourceId = 2265)]
    [Description("Launched rocket never will explode by time only when it does collide with something")]
    public class InfiniteRocket : RustPlugin
    {
        //That's what player launched last
        private Dictionary<string, TimedExplosive> lastLanched = new Dictionary<string, TimedExplosive>();
        private static object _lock = new object();

        const string permExplode = "infiniterocket.explode";
        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var explosive = entity as TimedExplosive;
            if (!explosive)
            {
                return;
            }

            AddToRocketLaunch(player.UserIDString, explosive);

            //Cancel timer explode invocation
            explosive.CancelInvoke("Explode");

            if (explodeTimer != null)
            {
                if (explodeTimer is int)
                {
                    explosive.Invoke("Explode", (int)explodeTimer);
                }
                else if (explodeTimer is float)
                {
                    explosive.Invoke("Explode", (float)explodeTimer);
                }
                else
                {
                    //Parsing parameter value if it's not a numeric type
                    var strValue = explodeTimer as string;
                    if (string.IsNullOrEmpty(strValue))
                    {
                        PrintWarning("Invalid parameter for config parameter ExplodeTimer");
                    }
                    if (strValue.ToLower() != "infinite")
                    {
                        float flValue;
                        int intValue;
                        if (float.TryParse(strValue, out flValue))
                        {
                            explosive.Invoke("Explode", flValue);
                        }
                        else if(int.TryParse(strValue, out intValue))
                        {
                            explosive.Invoke("Explode", intValue);
                        }
                        else
                        {
                            PrintWarning("Invalid parameter for config parameter ExplodeTimer");
                        }
                    }
                }
            }
        }

        [ChatCommand("explode")]
        void cmdChatchangeowner(BasePlayer player, string command, string[] args)
        {
            if (usePermissions && !IsAllowed(player.UserIDString, permExplode))
            {
                SendReply(player, Lang("NotAllowed", player.UserIDString));
                return;
            }
            var lastRocket = GetRocketLaunch(player.UserIDString);

            if (lastRocket == null)
            {
                SendReply(player, Lang("HasntLaunched", player.UserIDString));
                return;
            }
            float time = 0;
            if (args != null && args.Length > 1)
            {
                SendReply(player, Lang("Usage", player.UserIDString, command));
                return;
            }

            if (args != null && args.Length > 0 && (args[0].ToLower() == "h" || args[0].ToLower() == "help" || args[0] == "?"))
            {
                SendReply(player, Lang("Usage", player.UserIDString, command));
                return;
            }
            if (args != null && args.Length > 0)
            {
                float.TryParse(args[0], out time);
            }

            lastRocket.Invoke("Explode", time);
            DeleteRocketLaunch(player.UserIDString);
        }

        private object explodeTimer;
        private bool usePermissions;
        protected override void LoadDefaultConfig()
        {
            Config["ExplodeTimer"] = explodeTimer = GetConfig("ExplodeTimer", "infinite");
            Config["UsePermissions"] = usePermissions = GetConfig("UsePermissions", false);
            SaveConfig();
        }

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>            {
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["HasntLaunched"] = "Hasn't a launched rocket",
                ["Usage"] = "/{0} to explode rocket right now\n/{0} N to explode rocket after number of seconds"
            }, this);
        }

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)Convert.ChangeType(Config[name], typeof(T));
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm);

        private void AddToRocketLaunch(string playerId, TimedExplosive explosive)
        {
            if (!lastLanched.ContainsKey(playerId))
                lastLanched.Add(playerId, explosive);
            else
                lastLanched[playerId] = explosive;
        }

        private void DeleteRocketLaunch(string playerId)
        {
            if (lastLanched.ContainsKey(playerId))
                lastLanched.Remove(playerId);
        }

        private TimedExplosive GetRocketLaunch(string playerId)
        {
            TimedExplosive result = null;
            if (lastLanched.ContainsKey(playerId))
                result = lastLanched[playerId];

            return result;
        }
    }
}
