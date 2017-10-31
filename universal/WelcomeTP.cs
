using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("WelcomeTP", "Ryan", "1.0.6", ResourceId = 2604)]
    [Description("Teleports players to a position if they're new")]

    class WelcomeTP : CovalencePlugin
    {
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private static ConfigFile CFile;

        private const string _perm = "welcometp.used";

        #region Config

        public class ConfigFile
        {
            public GenericPosition Position;

            public ConfigFile()
            {
                Position = new GenericPosition(0, 0, 0);
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            CFile = new ConfigFile();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                CFile = Config.ReadObject<ConfigFile>();
                if (CFile == null) Regenerate();
            }
            catch { Regenerate(); }
        }

        protected override void SaveConfig() => Config.WriteObject(CFile);

        private void Regenerate()
        {
            PrintWarning($"Configuration file at 'oxide/config/{Name}.json' seems to be corrupt! Regenerating...");
            CFile = new ConfigFile();
            SaveConfig();
        }

        #endregion

        #region Lang

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome"] = "Welcome to {0}, {1}! You've been teleported to our hub",
            }, this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(_perm, this);
        }

        private void OnUserConnected(IPlayer player)
        {
            if (!permission.UserHasPermission(player.Id, _perm))
            {
                player.Teleport(CFile.Position.X, CFile.Position.Y, CFile.Position.Z);
                permission.GrantUserPermission(player.Id, _perm, this);
                timer.Once(2f, () =>
                {
                    player.Reply(Lang("Welcome", player.Id, server.Name, player.Name));
                });
            }
        }

        #endregion
    }
}
