using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Server Announcer", "austinv900", "1.0.3", ResourceId = 2198)]
    [Description("Allows you to send messages as the server with custom prefix")]

    class ServerAnnouncer : CovalencePlugin
    {

        #region Oxide Hooks
        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(Permission, this);
        }
        #endregion

        #region Configuration
        string ServerName;
        string Permission = "serverannouncer.";
        protected override void LoadDefaultConfig()
        {
            SetConfig("Server Chat Name", "[ServerConsole]");
            SetConfig("Permission", "allowed");
            SaveConfig();
            ServerName = GetConfig("[ServerConsole]", "Server Chat Name");
            Permission += GetConfig("allowed", "Permission");
        }
        #endregion

        #region Localizations
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["MessageFormat"] = "{0}: {1}",
                ["NoAccess"] = "You are not allowed to use this command",
                ["NoMessage"] = "You did not specify a message"
            }, this);
        }
        #endregion

        #region Commands
        [Command("say")]
        void cmdSay(IPlayer pl, string command, string[] args)
        {
            if (!IsAdmin(pl)) { pl.Reply(Lang("NoAccess", pl)); return; }
            if (args.Length == 0) { pl.Reply(Lang("NoMessage", pl)); return; }

            var msg = string.Join(" ", args);

            ServerSay(msg);
        }
        #endregion

        #region Plugin Methods;
        void ServerSay(string message)
        {
            foreach (var pl in players.Connected)
            {
                pl.Reply(Lang("MessageFormat", pl, ServerName, message));
            }
        }
        #endregion

        #region Helpers
        bool IsAdmin(IPlayer player) => permission.UserHasGroup(player.Id, "admin") || permission.UserHasPermission(player.Id, Permission) || player.IsAdmin;
        string Lang(string Key, IPlayer player, params object[] args) => string.Format(lang.GetMessage(Key, this, player.Id), args);
        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)System.Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }
        #endregion
    }
}
