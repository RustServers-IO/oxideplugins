using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries;
using Oxide.Game.Hurtworld.Libraries;

namespace Oxide.Plugins
{
    [Info("PrivateMessage", "Lizzaran", "1.2.0")]
    [Description("A simple private messaging plugin for players.")]
    public class PrivateMessage : HurtworldPlugin
    {
        #region Enums

        internal enum ELogType
        {
            Info,
            Warning,
            Error
        }

        #endregion Enums

        #region Classes

        internal class Helpers
        {
            private readonly Hurtworld _hurtworld;
            private readonly Lang _language;
            private readonly Permission _permission;
            private readonly HurtworldPlugin _plugin;

            public Helpers(Lang lang, HurtworldPlugin plugin, Hurtworld hurt,
                Permission permission)
            {
                _language = lang;
                _plugin = plugin;
                _hurtworld = hurt;
                _permission = permission;
            }

            public string PermissionPrefix { get; set; }

            public void RegisterPermission(params string[] paramArray)
            {
                var perms = ArrayToString(paramArray, ".");
                _permission.RegisterPermission(
                    perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}",
                    _plugin);
            }

            public bool HasPermission(PlayerSession session, params string[] paramArray)
            {
                var perms = ArrayToString(paramArray, ".");
                return _permission.UserHasPermission(GetPlayerId(session),
                    perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}");
            }

            public bool IsValidSession(PlayerSession session)
            {
                return session != null && session.IsLoaded && !string.IsNullOrEmpty(session.Name);
            }

            public bool StringContains(string source, string toCheck, StringComparison comp)
            {
                return source.IndexOf(toCheck, comp) >= 0;
            }

            public string GetPlayerId(PlayerSession session)
            {
                return session.SteamId.ToString();
            }

            public string ArrayToString(string[] array, string separator)
            {
                return string.Join(separator, array);
            }

            public PlayerSession GetPlayerSessionById(PlayerSession session, string id)
            {
                var player =
                    GameManager.Instance.GetSessions()
                        .Values.Where(IsValidSession)
                        .FirstOrDefault(s => GetPlayerId(s).Equals(id));
                if (player == null)
                {
                    _hurtworld.SendChatMessage(session,
                        _language.GetMessage("Misc - Player Not Found", _plugin, GetPlayerId(session)));
                }
                return player;
            }

            public PlayerSession GetPlayerSession(PlayerSession session, string search)
            {
                var sessions = GameManager.Instance.GetSessions().Values.Where(IsValidSession).ToList();
                var player =
                    sessions.FirstOrDefault(
                        s => s.Name.Equals(search, StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    return player;
                }
                var players =
                    (from pSession in sessions
                        where
                            StringContains(pSession.Name, search, StringComparison.OrdinalIgnoreCase)
                        select pSession).ToList();
                switch (players.Count)
                {
                    case 0:
                        _hurtworld.SendChatMessage(session,
                            _language.GetMessage("Misc - Player Not Found", _plugin, GetPlayerId(session)));
                        break;
                    case 1:
                        player = players.First();
                        break;
                    default:
                        _hurtworld.SendChatMessage(session,
                            _language.GetMessage("Misc - Multiple Players Found", _plugin, GetPlayerId(session))
                                .Replace("{players}", ArrayToString(players.Select(p => p.Name).ToArray(), ", ")));
                        break;
                }
                return player;
            }
        }

        #endregion Classes

        #region Variables

        private Helpers _helpers;
        private readonly Dictionary<string, string> _replies = new Dictionary<string, string>();

        #endregion Variables

        #region Methods

        // ReSharper disable once UnusedMember.Local
        private void Loaded()
        {
            _helpers = new Helpers(lang, this, hurt, permission)
            {
                PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower()
            };

            LoadPermissions();
            LoadMessages();
        }

        private void LoadPermissions()
        {
            _helpers.RegisterPermission("use");
        }

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Misc - No Permission", "You don't have the permission to use this command."},
                {"Misc - Syntax", "Syntax: {syntax}"},
                {"Misc - Player Not Found", "The player can't be found."},
                {"Misc - Multiple Players Found", "Multiple matching players found:\n{players}"},
                {"Message - From", "[Private Message] {player}: {message}"},
                {"Message - To", "[Private Message] To {player}: {message}"},
                {"Message - No Reply", "You have no one to reply."}
            }, this);
        }

        internal void Log(ELogType type, string message)
        {
            switch (type)
            {
                case ELogType.Info:
                    Puts(message);
                    break;
                case ELogType.Warning:
                    PrintWarning(message);
                    break;
                case ELogType.Error:
                    PrintError(message);
                    break;
            }
        }

        private void SendMessage(PlayerSession fromSession, PlayerSession toSession, string message)
        {
            hurt.SendChatMessage(toSession,
                lang.GetMessage("Message - From", this, _helpers.GetPlayerId(toSession))
                    .Replace("{player}", fromSession.Name)
                    .Replace("{message}", message));
            hurt.SendChatMessage(fromSession,
                lang.GetMessage("Message - To", this, _helpers.GetPlayerId(fromSession))
                    .Replace("{player}", toSession.Name)
                    .Replace("{message}", message));
            Log(ELogType.Info,
                lang.GetMessage("Message - From", this, _helpers.GetPlayerId(toSession))
                    .Replace("{player}", fromSession.Name)
                    .Replace("{message}", message));
        }

        #endregion Methods

        #region Commands

        // ReSharper disable UnusedMember.Local
        // ReSharper disable UnusedParameter.Local

        [ChatCommand("pm")]
        private void CommandPm(PlayerSession session, string command, string[] args)
        {
            if (!_helpers.HasPermission(session, "use"))
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Misc - No Permission", this, _helpers.GetPlayerId(session)));
                return;
            }
            if (args.Length < 2)
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Misc - Syntax", this, _helpers.GetPlayerId(session))
                        .Replace("{syntax}", "/pm <player> <message>"));
                return;
            }
            var pSession = _helpers.GetPlayerSession(session, args[0]);
            if (pSession == null)
            {
                return;
            }
            _replies[_helpers.GetPlayerId(pSession)] = _helpers.GetPlayerId(session);
            SendMessage(session, pSession, string.Join(" ", args.Skip(1).ToArray()));
        }

        [ChatCommand("r")]
        private void CommandReply(PlayerSession session, string command, string[] args)
        {
            if (!_helpers.HasPermission(session, "use"))
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Misc - No Permission", this, _helpers.GetPlayerId(session)));
                return;
            }
            if (args.Length < 1)
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Misc - Syntax", this, _helpers.GetPlayerId(session))
                        .Replace("{syntax}", "/r <message>"));
                return;
            }
            if (!_replies.ContainsKey(_helpers.GetPlayerId(session)))
            {
                hurt.SendChatMessage(session,
                    lang.GetMessage("Message - No Reply", this, _helpers.GetPlayerId(session)));
                return;
            }
            var pSession = _helpers.GetPlayerSessionById(session, _replies[_helpers.GetPlayerId(session)]);
            if (pSession == null)
            {
                return;
            }
            SendMessage(session, pSession, string.Join(" ", args));
        }

        // ReSharper restore UnusedParameter.Local
        // ReSharper restore UnusedMember.Local

        #endregion Commands
    }
}