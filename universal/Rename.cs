using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Rename", "Wulf/lukespragg", "0.2.1", ResourceId = 1184)]
    [Description("Allows players with permission to instantly rename other players or self")]

    class Rename : CovalencePlugin
    {
        #region Initialization

        StoredData storedData;
        const string permOthers = "rename.others";
        const string permSelf = "rename.self";

        bool persistent;
        bool preventAdmin;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Persistent Renames (true/false)"] = persistent = GetConfig("Persistent Renames (true/false)", true);
            Config["Prevent Admin Renames (true/false)"] = preventAdmin = GetConfig("Prevent Admin Renames (true/false)", true);

            SaveConfig();
        }

        #region Stored Data

        class StoredData
        {
            public readonly HashSet<PlayerInfo> Renames = new HashSet<PlayerInfo>();
        }

        class PlayerInfo
        {
            public string Id;
            public string Old;
            public string New;

            public PlayerInfo()
            {
            }

            public PlayerInfo(IPlayer player, string newName)
            {
                Id = player.Id;
                Old = player.Name;
                New = newName;
            }
        }

        void LoadPersistentData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Title);
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, storedData);

        #endregion

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            LoadPersistentData();

            permission.RegisterPermission(permOthers, this);
            permission.RegisterPermission(permSelf, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Usage: {0} <name or id> <new name>",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerAdmin"] = "{0} is admin and cannot be renamed",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayerRenamed"] = "{0} was renamed to {1}",
                ["YouWereRenamed"] = "You were renamed to {0}"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "es");
        }

        #endregion

        void OnUserConnected(IPlayer player)
        {
            var rename = storedData.Renames.FirstOrDefault(r => r.Id == player.Id);
            if (!persistent || rename == null) return;

            Puts($"{player.Name} was renamed to {rename.New}");
            player.Rename(rename.New);
        }

        #region Commands

        [Command("rename")]
        void RenameCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length >= 2 && !player.HasPermission(permOthers) || !player.HasPermission(permSelf))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length == 0 || args.Length == 1 && player.Id == "server_console")
            {
                player.Reply(Lang("CommandUsage", player.Id, command));
                return;
            }

            var target = args.Length >= 2 ? players.FindPlayer(args[0]) : player;
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (args.Length >= 2 && preventAdmin && target.IsAdmin)
            {
                player.Reply(Lang("PlayerAdmin", player.Id, args[0].Sanitize()));
                return;
            }

            var newName = args.Length >= 2 ? args[1].Sanitize() : args[0].Sanitize();
            target.Message(Lang("YouWereRenamed", target.Id, newName));
            if (!Equals(target, player)) player.Reply(Lang("PlayerRenamed", player.Id, target.Name.Sanitize(), newName));

            if (persistent)
            {
                storedData.Renames.RemoveWhere(r => r.Id == target.Id);
                storedData.Renames.Add(new PlayerInfo(target, newName));
                SaveData();
            }

            target.Rename(newName);
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
