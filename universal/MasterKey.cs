using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("MasterKey", "Wulf/lukespragg", "0.6.2", ResourceId = 1151)]
    [Description("Gain access to any locked object and/or build anywhere with permission")]
    public class MasterKey : CovalencePlugin
    {
        #region Initialization

        private readonly DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetFile("MasterKey");
        private readonly string[] lockableTypes = { "box", "cell", "door", "gate", "hatch", "shop" };
        private Dictionary<string, bool> playerPrefs = new Dictionary<string, bool>();
        private const string permBuild = "masterkey.build";
        private bool logUsage;
        private bool showMessages;

        private new void LoadDefaultConfig()
        {
            Config["Log Usage (true/false)"] = logUsage = GetConfig("Log Usage (true/false)", true);
            Config["Show Messages (true/false)"] = showMessages = GetConfig("Show Messages (true/false)", true);
            SaveConfig();
        }

        private void Init()
        {
            LoadDefaultConfig();
            playerPrefs = dataFile.ReadObject<Dictionary<string, bool>>();

            permission.RegisterPermission(permBuild, this);
            foreach (var type in lockableTypes) permission.RegisterPermission($"{Title.ToLower()}.{type}", this);
        }

        #endregion Initialization

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MasterKeyDisabled"] = "Master key access is now disabled",
                ["MasterKeyEnabled"] = "Master key access is now enabled",
                ["MasterKeyUsed"] = "{0} ({1}) used master key at {2}",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["UnlockedWith"] = "Unlocked {0} with master key!"
            }, this);
        }

        #endregion Localization

        #region Chat Command

        [Command("masterkey", "mkey", "mk")]
        private void ChatCommand(IPlayer player, string command, string[] args)
        {
            foreach (var type in lockableTypes)
            {
                if (player.HasPermission($"{Title.ToLower()}.{type}")) continue;
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (!playerPrefs.ContainsKey(player.Id)) playerPrefs.Add(player.Id, true);
            playerPrefs[player.Id] = !playerPrefs[player.Id];
            dataFile.WriteObject(playerPrefs);

            player.Reply(playerPrefs[player.Id] ? Lang("MasterKeyEnabled", player.Id) : Lang("MasterKeyDisabled"));
        }

        #endregion Chat Command

        #region Build Anywhere

        /*private void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        {
            var player = entity as BasePlayer;
            if (player == null || !(trigger is BuildPrivilegeTrigger)) return;

            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return;
            if (!permission.UserHasPermission(player.UserIDString, permBuild)) return;

            NextTick(() =>
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.InBuildingPrivilege, true);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.HasBuildingPrivilege, true);
            });
            if (logUsage) Log(Lang("MasterKeyUsed", null, player.displayName, player.UserIDString, player.transform.position));
        }*/

        #endregion Build Anywhere

        #region Lock Access

        private object CanUseLockedEntity(BasePlayer player, BaseLock @lock)
        {
            if (!@lock.IsLocked()) return null;
            if (playerPrefs.ContainsKey(player.UserIDString) && !playerPrefs[player.UserIDString]) return null;

            var prefab = @lock.parentEntity.Get(true).ShortPrefabName;
            foreach (var type in lockableTypes)
            {
                if (!prefab.Contains(type)) continue;
                if (!permission.UserHasPermission(player.UserIDString, $"masterkey.{type}")) return null;

                if (showMessages) player.ChatMessage(Lang("UnlockedWith", player.UserIDString, type));
                if (logUsage) Log(Lang("MasterKeyUsed", null, player.displayName, player.UserIDString, player.transform.position));
                return true;
            }

            return null;
        }

        #endregion Lock Access

        #region Helpers

        private T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Log(string text) => LogToFile("usage", $"[{DateTime.Now}] {text}", this);

        #endregion Helpers
    }
}
