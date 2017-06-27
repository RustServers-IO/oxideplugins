using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("NoRaid", "Ryan", "1.0.0")]
    [Description("Prevents players destroying buildings of those they're not associated with")]

    class NoRaid : RustPlugin
    {
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id),
            args);

        [PluginReference] private Plugin Friends;
        [PluginReference] private Plugin Clans;

        #region Config

        ConfigFile configFile;

        class ConfigFile
        {
            public FriendsAPI FriendsApi;

            public RustIOClans Clans;


            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    FriendsApi = new FriendsAPI()
                    {
                        Enabled = true
                    },
                    Clans = new RustIOClans()
                    {
                        Enabled = true
                    }
                };
            }
        }

        class FriendsAPI
        {
            public bool Enabled { get; set; }
        }

        class RustIOClans
        {
            public bool Enabled { get; set; }
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file...");
            configFile = ConfigFile.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            configFile = Config.ReadObject<ConfigFile>();
        }
        protected override void SaveConfig() => Config.WriteObject(configFile);

        #endregion

        #region Lang

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantDamage"] = "You cannot damage that entity because you are not associated with the building owner",
            }, this);
        }

        #endregion

        #region Hooks

        void Loaded()
        {
            SaveConfig();
            if (!Friends && configFile.FriendsApi.Enabled)
            {
                configFile.FriendsApi.Enabled = false;
                PrintWarning("FriendsAPI not detected, disabling FriendsAPI integration");
            }
            if (!Clans && configFile.Clans.Enabled)
            {
                configFile.Clans.Enabled = false;
                PrintWarning("RustIO Clans not detected, disabling RustIO Clans integration");
            }
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BuildingBlock)
            {
                var target = BasePlayer.FindByID(entity.OwnerID);
                var player = info?.Initiator?.ToPlayer();

                if (!target || !player)
                    return null;
                if (player.userID == target.userID)
                {
                    return null;
                }
                if (configFile.FriendsApi.Enabled &&
                    (bool)Friends?.Call("HasFriend", target.UserIDString, player.UserIDString))
                {
                    return null;
                }
                if (configFile.Clans.Enabled && (string)Clans?.Call("GetClanOf", player.UserIDString) != null &&
                    (string)Clans?.Call("GetClanOf", target.UserIDString) != null && (string)Clans?.Call("GetClanOf", player.UserIDString) == 
                    (string)Clans?.Call("GetClanOf", target.UserIDString))
                {
                    return null;
                }

                PrintToChat(player, Lang("CantDamage", player.UserIDString));
                return true;
            }
            return null;
        }

        #endregion
    }
}