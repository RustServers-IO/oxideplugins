using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("GameTipAnnouncements", "redBDGR", "1.0.0")]
    [Description("Send notifications to players as gametips")]

    class GameTipAnnouncements : RustPlugin
    {
        private bool Changed;
        private const string permissionNameADMIN = "gametipannouncements.admin";

        private float defaultLengnth = 15f;

        private void Init()
        {
            permission.RegisterPermission(permissionNameADMIN, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["NoPermission"] = "You are not allowed to use this command!",
                ["sentgt Invalid Format"] = "Invalid format! /sendgt <playername/id> <message> <length>",
                ["sendgtall Invalid Format"] = "Invalid format! /sendgtall <message> <length>",
                ["No Player Found"] = "No players were found with this name / ID",

            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            defaultLengnth = Convert.ToSingle(GetConfig("Settings", "Default Fade Length", 15f));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        [ChatCommand("sendgt")]
        private void SendGameTipCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length != 2 && args.Length != 3)
            {
                player.ChatMessage(msg("sentgt Invalid Format", player.UserIDString));
                return;
            }
            float length = defaultLengnth;
            if (args.Length == 3)
                float.TryParse(args[2], out length);
            BasePlayer receiver = BasePlayer.Find(args[0]);
            if (receiver == null)
            {
                player.ChatMessage(msg("No Player Found", player.UserIDString));
                return;
            }
            CreateGameTip(args[1], receiver, length);
        }

        [ChatCommand("sendgtall")]
        private void SendGameTipAllCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                player.ChatMessage(msg("NoPermission", player.UserIDString));
                return;
            }
            if (args.Length != 1 && args.Length != 2)
            {
                player.ChatMessage(msg("sendgtall Invalid Format", player.UserIDString));
                return;
            }
            float length = defaultLengnth;
            if (args.Length == 2)
                float.TryParse(args[1], out length);
            CreateGameTipAll(args[0], length);
        }

        private void CreateGameTip(string text, BasePlayer player, float length = 30f)
        {
            if (player == null)
                return;
            player.SendConsoleCommand("gametip.hidegametip");
            player.SendConsoleCommand("gametip.showgametip", text);
            timer.Once(length, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private void CreateGameTipAll(string text, float length = 30f)
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
                CreateGameTip(text, player, length);
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}