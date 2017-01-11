/*
TODO:
- Add "banlist" command support
- Add "banlistex" command support
- Add "bans" command support
- Add "kickall" command support
- Add "mutechat" command support
- Add "revoke" command support
- Add "unmutechat" command support
- Add messages if banned/unbanned already
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SecureAdmin", "Wulf/lukespragg", "1.0.2", ResourceId = 1449)]
    [Description("Restricts the basic admin commands to players with permission")]

    class SecureAdmin : CovalencePlugin
    {
        #region Initialization

        const string permAuth = "secureadmin.auth"; // grant, oxide.grant, revoke, oxide.revoke
        const string permBan = "secureadmin.ban"; // global.ban, global.banid, global.banlist, global.banlistex, global.listid, global.bans
        const string permKick = "secureadmin.kick"; // global.kick, global.kickall
        const string permSay = "secureadmin.say"; // global.mutechat, global.say, global.unmutechat
        const string permUnban = "secureadmin.unban"; // global.banlist, global.bans, global.unban

        bool protectAdmin;

        protected override void LoadDefaultConfig()
        {
            Config["Protect Admin (true/false)"] = protectAdmin = GetConfig("Protect Admin (true/false)", true);

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();

            permission.RegisterPermission(permAuth, this);
            permission.RegisterPermission(permBan, this);
            permission.RegisterPermission(permKick, this);
            permission.RegisterPermission(permSay, this);
            permission.RegisterPermission(permUnban, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NotFound"] = "No players were found with that name or ID",
                ["PlayerAdmin"] = "{0} is admin and cannot be banned or kicked",
                ["PlayerAuthed"] = "{0} has been given permission {1}",
                ["PlayerBanned"] = "{0} has been banned for {1}",
                ["PlayerKicked"] = "{0} has been kicked for {1}",
                ["PlayerUnbanned"] = "{0} has been unbanned",
                ["ReasonUnknown"] = "Unknown",
                ["UsageAuth"] = "Usage: {0} <name or id> <permission>",
                ["UsageBan"] = "Usage: {0} <name or id> <reason>",
                ["UsageKick"] = "Usage: {0} <name or id> <reason>",
                ["UsageSay"] = "Usage: {0} <message>",
                ["UsageUnban"] = "Usage: {0} <name or id>",
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
                ["NotAllowed"] = "Вы не можете использовать команду '{0}'",
                ["NotFound"] = "Игрока с таким никнеймом или ID не найдено",
                ["PlayerAdmin"] = "Игрок {0} является администратором и его нельзя кикать и банит",
                ["PlayerAuthed"] = "{0} получил привилегию на {1}",
                ["PlayerBanned"] = "{0} был забанен of {1}",
                ["PlayerKicked"] = "{0} был кикнут за {1}",
                ["PlayerUnbanned"] = "{0} был разбанен",
                ["ReasonUnknown"] = "Неизвестно",
                ["UsageAuth"] = "Используй: {0} <никнейм или ID> <привилегия>",
                ["UsageBan"] = "Используй: {0} <никнейм или ID> <причина>",
                ["UsageKick"] = "Используй: {0} <никнейм или ID> <причина>",
                ["UsageSay"] = "Используй: {0} <сообщение>",
                ["UsageUnban"] = "Используй: {0} <никнейм или ID>",
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // TODO
            }, this, "es");
        }

        #endregion

        #region Auth Command

        [Command("auth")]
        void AuthCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAuth))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 2)
            {
                player.Reply(Lang("UsageAuth", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null)
            {
                player.Reply(Lang("NotFound", player.Id, args[0]));
                return;
            }

            server.Command($"oxide.grant user {player.Id} {args[1]}");
            player.Reply(Lang("PlayerAuthed", player.Id, target.Name, args[1]));
        }

        #endregion

        #region Ban Command

        [Command("ban", "banid")]
        void BanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permBan))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageBan", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null)
            {
                player.Reply(Lang("NotFound", player.Id, args[0]));
                return;
            }

            if (protectAdmin && target.IsAdmin)
            {
                player.Reply(Lang("PlayerAdmin", player.Id, args[0]));
                return;
            }

            var reason = args.Length < 2 ? string.Join(" ", args.Skip(1).Select(v => v.ToString()).ToArray()) : Lang("ReasonUnknown", target.Id);
            target.Ban(reason);
            player.Reply(Lang("PlayerBanned", player.Id, player.Name, reason));
        }

        #endregion

        #region Kick Command

        [Command("kick")]
        void KickCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permKick))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageKick", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null || !target.IsConnected)
            {
                player.Reply(Lang("NotFound", player.Id, args[0]));
                return;
            }

            if (protectAdmin && target.IsAdmin)
            {
                player.Reply(Lang("PlayerAdmin", player.Id, args[0]));
                return;
            }

            var reason = args.Length < 2 ? string.Join(" ", args.Skip(1).Select(v => v.ToString()).ToArray()) : Lang("ReasonUnknown", target.Id);
            target.Kick(reason);
            player.Reply(Lang("PlayerKicked", player.Id, target.Name, reason));
        }

        #endregion

        #region Say Command

        [Command("say")]
        void SayCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permSay))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageSay", player.Id, command));
                return;
            }

            var message = string.Join(" ", args.Select(v => v.ToString()).ToArray());
            server.Broadcast(message);
        }

        #endregion

        #region Unban Command

        [Command("unban")]
        void UnbanCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUnban))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (args.Length < 1)
            {
                player.Reply(Lang("UsageUnban", player.Id, command));
                return;
            }

            var target = players.FindPlayer(args[0]);
            if (target == null)
            {
                player.Reply(Lang("NotFound", player.Id, args[0]));
                return;
            }

            target.Unban();
            player.Reply(Lang("PlayerUnbanned", player.Id, target.Name));
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
