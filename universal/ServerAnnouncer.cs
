using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Server Announcer", "austinv900", "1.0.6", ResourceId = 2198)]
    [Description("Allows you to send messages as the server with custom prefix")]
    public class ServerAnnouncer : CovalencePlugin
    {
        #region  Initialization

        private const string Permission = "ServerAnnouncer.Allowed";

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(Permission, this);
        }

        #endregion

        #region Configuration

        public static class Configuration
        {
            public static string ConsoleName = "[ServerConsole]";
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file");

        private new void LoadConfig()
        {
            GetConfig(ref Configuration.ConsoleName, "General", "ConsoleName");
            SaveConfig();
        }

        #endregion

        #region Localizations

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["MessageFormat"] = "{0}: {1}",
                ["NoAccess"] = "You are not allowed to use this command",
                ["NoMessage"] = "You did not specify a message"
            }, this, "en");
        }

        #endregion

        #region Commands

        [Command("say", "server.say")]
        private void Say(IPlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) { player.Reply(Lang("NoAccess", player)); return; }

            if (args.Length == 0) { player.Reply(Lang("NoMessage", player)); return; }

            foreach (var user in players.Connected)
            {
                var msg = Lang("MessageFormat", user, Configuration.ConsoleName, string.Join(" ", args));
                user.Reply(msg);
            }
        }

        #endregion

        #region Helpers

        private bool IsAdmin(IPlayer player) => permission.UserHasGroup(player.Id, "admin") || permission.UserHasPermission(player.Id, Permission) || player.IsAdmin;

        private string Lang(string key, IPlayer player, params object[] args) => string.Format(lang.GetMessage(key, this, player.Id), args);

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        #endregion
    }
}
