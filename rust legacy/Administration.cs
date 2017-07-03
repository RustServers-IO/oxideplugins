/*
-- Administration.cs -----------------

--------------------------------------

-- Developed by Spicy.
-- http://steamcommunity.com/id/spicy_
-- https://spicee.xyz
-- 1,127 lines of code.
-- 9 hours development time.
-- Written on 24/07/2016 - 25/07/2016.

--------------------------------------
*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Administration", "Spicy", "1.0.1")]
    [Description("Full administration plugin.")]

    class Administration : RustLegacyPlugin
    {
        RustServerManagement serverManagement;

        static string permissionHasPlayedBefore;
        static string permissionIsMuted;
        static string permissionCanKick;
        static string permissionCanBan;
        static string permissionCanClose;
        static string permissionCanMute;
        static string permissionCanTeleport;
        static string permissionCanGive;
        static string permissionCanCallAirdrop;
        static string permissionCanPopupNotice;
        static string permissionCanChangeTime;
        static string permissionCanSpawnInvisibleSuit;
        static string permissionCanRunCommand;

        static bool messageJoin;
        static bool messageLeave;

        static bool commandInfo;
        static bool commandKick;
        static bool commandBan;
        static bool commandBanID;
        static bool commandUnban;
        static bool commandClose;
        static bool commandMute;
        static bool commandTeleport;
        static bool commandBring;
        static bool commandGive;
        static bool commandAirdrop;
        static bool commandPopup;
        static bool commandTime;
        static bool commandInvisible;
        static bool commandCommand;

        void OnServerInitialized()
        {
            SetupConfig();
            SetupLang();
            SetupChatCommands();
            SetupPermissions();

            serverManagement = RustServerManagement.Get();

            return;
        }

        void SetupConfig()
        {
            permissionHasPlayedBefore = Config.Get<string>("Settings", "permissionHasPlayedBefore");
            permissionIsMuted = Config.Get<string>("Settings", "permissionIsMuted");
            permissionCanKick = Config.Get<string>("Settings", "permissionCanKick");
            permissionCanBan = Config.Get<string>("Settings", "permissionCanBan");
            permissionCanClose = Config.Get<string>("Settings", "permissionCanClose");
            permissionCanMute = Config.Get<string>("Settings", "permissionCanMute");
            permissionCanTeleport = Config.Get<string>("Settings", "permissionCanTeleport");
            permissionCanGive = Config.Get<string>("Settings", "permissionCanGive");
            permissionCanCallAirdrop = Config.Get<string>("Settings", "permissionCanCallAirdrop");
            permissionCanPopupNotice = Config.Get<string>("Settings", "permissionCanPopupNotice");
            permissionCanChangeTime = Config.Get<string>("Settings", "permissionCanChangeTime");
            permissionCanSpawnInvisibleSuit = Config.Get<string>("Settings", "permissionCanSpawnInvisibleSuit");
            permissionCanRunCommand = Config.Get<string>("Settings", "permissionCanRunCommand");

            messageJoin = Config.Get<bool>("Settings", "messageJoin");
            messageLeave = Config.Get<bool>("Settings", "messageLeave");

            commandInfo = Config.Get<bool>("Settings", "commandInfo");
            commandKick = Config.Get<bool>("Settings", "commandKick");
            commandBan = Config.Get<bool>("Settings", "commandBan");
            commandBanID = Config.Get<bool>("Settings", "commandBanID");
            commandUnban = Config.Get<bool>("Settings", "commandUnban");
            commandClose = Config.Get<bool>("Settings", "commandClose");
            commandMute = Config.Get<bool>("Settings", "commandMute");
            commandTeleport = Config.Get<bool>("Settings", "commandTeleport");
            commandBring = Config.Get<bool>("Settings", "commandBring");
            commandGive = Config.Get<bool>("Settings", "commandGive");
            commandAirdrop = Config.Get<bool>("Settings", "commandAirdrop");
            commandPopup = Config.Get<bool>("Settings", "commandPopup");
            commandTime = Config.Get<bool>("Settings", "commandTime");
            commandInvisible = Config.Get<bool>("Settings", "commandInvisible");
            commandCommand = Config.Get<bool>("Settings", "commandCommand");
        }

        protected override void LoadDefaultConfig()
        {
            Config["Settings"] = new Dictionary<string, object>
            {
                {"permissionHasPlayedBefore", "administration.hasplayedbefore"},
                {"permissionIsMuted", "administration.ismuted"},
                {"permissionCanKick", "administration.cankick"},
                {"permissionCanBan", "administration.canban"},
                {"permissionCanClose", "administration.canclose"},
                {"permissionCanMute", "administration.canmute"},
                {"permissionCanTeleport", "administration.canteleport"},
                {"permissionCanGive", "administration.cangive"},
                {"permissionCanCallAirdrop", "administration.cancallairdrop"},
                {"permissionCanPopupNotice", "administration.canpopupnotice"},
                {"permissionCanChangeTime", "administration.canchangetime"},
                {"permissionCanSpawnInvisibleSuit", "administration.canspawninvisiblesuit"},
                {"permissionCanRunCommand", "administration.canruncommand"},

                {"messageJoin", true},
                {"messageLeave", true},

                {"commandInfo", true},
                {"commandKick", true},
                {"commandBan", true},
                {"commandBanID", true},
                {"commandUnban", true},
                {"commandClose", true },
                {"commandMute", true},
                {"commandTeleport", true},
                {"commandBring", true},
                {"commandGive", true},
                {"commandAirdrop", true},
                {"commandPopup", true},
                {"commandTime", true},
                {"commandInvisible", true},
                {"commandCommand", true}
            };
        }

        void SetupLang()
        {
            var messages = new Dictionary<string, string>
            {
                {"ChatPrefix", "Server"},

                {"InvalidSyntax", "Invalid syntax."},
                {"NoPermission", "You do not have permission to use this command."},
                {"NoPlayersFound", "No players were found with that name."},
                {"InvalidSteamID", "That's an invalid SteamID."},
                {"InvalidInteger", "That's an invalid integer."},
                {"InvalidItem", "That's an invalid item."},

                {"SuccessIcon", "✔"},
                {"FailureIcon", "✘"},

                {"NormalJoin", "has joined the server."},
                {"FirstTimeJoin", "has joined the server for the first time."},

                {"NormalLeave", "has left the server."},

                {"WarningMessage", "Welcome to the server!"},

                {"MutedMessage", "You are muted."},

                {"InfoPluginName", "Administration.cs."},
                {"InfoSpacer", "-----------------------------------"},
                {"InfoCredits", "Developed fully by Spicy."},
                {"InfoSteamProfile", "http://steamcommunity.com/id/spicy_"},
                {"InfoWebsite", "https://spicee.xyz"},
                {"InfoCodeLineCount", "1,000 lines of code."},
                {"InfoDevelopmentTime", "8 hours development time."},
                {"InfoDevelopmentDate", "Written on 24/07/2016."},

                {"DeathscreenKicked", "You were kicked from the server."},
                {"SuccessKickedNotice", "You kicked:"},
                {"SuccessKickedBroadcast", "was kicked from the server."},

                {"DeathscreenBanned", "You were banned from the server."},
                {"SuccessBannedNotice", "You banned:"},
                {"SuccessBannedBroadcast", "was banned from the server."},

                {"SuccessUnbanNotice", "You unbanned:"},
                {"SuccessUnbanBroadcast", "was unbanned from the server."},

                {"SuccessCloseNotice", "'s game has been closed."},

                {"SuccessMutedNotice", "You muted:"},
                {"SuccessMutedBroadcast", "was muted for"},

                {"SuccessUnmuteNotice", "You unmuted:"},
                {"SuccessUnmuteBroadcast", "was unmuted."},

                {"SuccessTeleportToPlayerNotice", "You teleported to:"},
                {"SuccessTeleportToPlayerBroadcast", "teleported to"},

                {"SuccessTeleportOtherPlayerNotice", "You teleported"},
                {"SuccessTeleportOtherPlayerBroadcast", "was teleported to"},

                {"SuccessTeleportToPositionNotice", "You teleported to coordinates:"},
                {"SuccessTeleportToPositionBroadcast", "teleported to coordinates:"},

                {"SuccessTeleportOtherPlayerToPositionNotice", "You teleported"},
                {"SuccessTeleportOtherPlayerToPositionBroadcast", "was teleported to coordinates:"},

                {"SuccessBringNotice", "was teleported to you."},
                {"SuccessBringBroadcast", "was teleported to"},

                {"SuccessGiveOneOfNotice", "You received a(n):"},
                {"SuccessGiveOneOfBroadcast", "received a(n):"},
                {"SuccessGiveMultipleOfNotice", "You received:"},
                {"SuccessGiveMultipleOfBroadcast", "received:"},

                {"SuccessAirdropCalledNotice", "Successfully called an airdrop."},
                {"SuccessAirdropCalledBroadcast", "Airdrop called in by:"},

                {"SuccessAirdropsCalledNotice", "airdrops called."},
                {"SuccessAirdropsCalledBroadcast", "airdrops were called by"},

                {"SuccessPopupNotice", "Successfully sent popup."},
                {"SuccessPopupBroadcast", "sent popup:"},

                {"SuccessTimeNotice", "Successfully set time to:"},
                {"SuccessTimeBroadcast", "Time was set to:"},

                {"SuccessInvisibleNotice", "Successfully given invisible suit."},
                {"SuccessInvisibleBroadcast", "was given an invisible suit."},

                {"SuccessCommandNotice", "Successfully ran command"},
                {"SuccessCommandBroadcast", "ran command"}
            };

            lang.RegisterMessages(messages, this);

            return;
        }

        void SetupChatCommands()
        {
            if (commandInfo)
                cmd.AddChatCommand("info", this, "cmdInfo");

            if (commandKick)
                cmd.AddChatCommand("kick", this, "cmdKick");

            if (commandBan)
                cmd.AddChatCommand("ban", this, "cmdBan");

            if (commandBanID)
                cmd.AddChatCommand("banid", this, "cmdBanID");

            if (commandUnban)
                cmd.AddChatCommand("unban", this, "cmdUnban");

            if (commandClose)
                cmd.AddChatCommand("close", this, "cmdClose");

            if (commandMute)
                cmd.AddChatCommand("mute", this, "cmdMute");

            if (commandTeleport)
                cmd.AddChatCommand("tp", this, "cmdTeleport");

            if (commandBring)
                cmd.AddChatCommand("bring", this, "cmdBring");

            if (commandGive)
                cmd.AddChatCommand("give", this, "cmdGive");

            if (commandAirdrop)
                cmd.AddChatCommand("airdrop", this, "cmdAirdrop");

            if (commandPopup)
                cmd.AddChatCommand("popup", this, "cmdPopup");

            if (commandTime)
                cmd.AddChatCommand("time", this, "cmdTime");

            if (commandInvisible)
                cmd.AddChatCommand("invisible", this, "cmdInvisible");

            if (commandCommand)
                cmd.AddChatCommand("command", this, "cmdCommand");
        }

        void SetupPermissions()
        {
            permission.RegisterPermission(permissionHasPlayedBefore, this);
            permission.RegisterPermission(permissionIsMuted, this);
            permission.RegisterPermission(permissionCanKick, this);
            permission.RegisterPermission(permissionCanBan, this);
            permission.RegisterPermission(permissionCanClose, this);
            permission.RegisterPermission(permissionCanMute, this);
            permission.RegisterPermission(permissionCanTeleport, this);
            permission.RegisterPermission(permissionCanGive, this);
            permission.RegisterPermission(permissionCanCallAirdrop, this);
            permission.RegisterPermission(permissionCanPopupNotice, this);
            permission.RegisterPermission(permissionCanChangeTime, this);
            permission.RegisterPermission(permissionCanSpawnInvisibleSuit, this);
            permission.RegisterPermission(permissionCanRunCommand, this);

            return;
        }

        void OnPlayerConnected(NetUser netUser)
        {
            if (!messageJoin)
                return;

            ulong netUserID = netUser.userID;

            if (!(permission.UserHasPermission(netUserID.ToString(), permissionHasPlayedBefore)))
            {
                rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{netUser.displayName} {lang.GetMessage("FirstTimeJoin", this)}");

                rust.RunClientCommand(netUser, $"deathscreen.reason \"{lang.GetMessage("WarningMessage", this)}\"");
                rust.RunClientCommand(netUser, "deathscreen.show");

                permission.GrantUserPermission(netUserID.ToString(), permissionHasPlayedBefore, this);

                Puts($"{netUser.displayName} [{netUserID.ToString()}] [{netUser.networkPlayer.ipAddress}] {lang.GetMessage("FirstTimeJoin", this)}");

                return;
            }

            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{netUser.displayName} {lang.GetMessage("NormalJoin", this)}");

            Puts($"{netUser.displayName} [{netUserID.ToString()}] [{netUser.networkPlayer.ipAddress}] {lang.GetMessage("NormalJoin", this)}");

            return;
        }

        void OnPlayerDisconnected(uLink.NetworkPlayer networkPlayer)
        {
            if (!messageLeave)
                return;

            NetUser netUser = networkPlayer.GetLocalData<NetUser>();

            ulong netUserID = netUser.userID;

            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{netUser.displayName} {lang.GetMessage("NormalLeave", this)}");

            Puts($"{netUser.displayName} [{netUserID.ToString()}] {lang.GetMessage("NormalLeave", this)}");

            return;
        }

        object OnPlayerChat(NetUser netUser, string message)
        {
            ulong netUserID = netUser.userID;

            if (permission.UserHasPermission(netUserID.ToString(), permissionIsMuted))
            {
                rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("MutedMessage", this));
                return false;
            }

            return null;
        }

        void cmdInfo(NetUser netUser, string command, string[] args)
        {
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoPluginName", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoSpacer", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoCredits", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoSteamProfile", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoWebsite", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoCodeLineCount", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoDevelopmentTime", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoDevelopmentDate", this));
            rust.SendChatMessage(netUser, lang.GetMessage("ChatPrefix", this), lang.GetMessage("InfoSpacer", this));

            return;
        }

        void cmdKick(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanKick)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            NetUser targetUser = rust.FindPlayer(args[0]);

            if (targetUser == null)
            {
                rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            rust.RunClientCommand(targetUser, $"deathscreen.reason \"{lang.GetMessage("DeathscreenKicked", this)}\"");
            rust.RunClientCommand(targetUser, "deathscreen.show");

            targetUser.Kick(NetError.Facepunch_Kick_Violation, true);

            rust.Notice(netUser, $"{lang.GetMessage("SuccessKickedNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessKickedBroadcast", this)}");

            Puts($"{targetUser.displayName} {lang.GetMessage("SuccessKickedBroadcast", this)}");

            return;
        }

        void cmdBan(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanBan)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            NetUser targetUser = rust.FindPlayer(args[0]);

            if (targetUser == null)
            {
                rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            rust.RunClientCommand(targetUser, $"deathscreen.reason \"{lang.GetMessage("DeathscreenBanned", this)}\"");
            rust.RunClientCommand(targetUser, "deathscreen.show");

            targetUser.Ban();
            targetUser.Kick(NetError.Facepunch_Kick_Ban, true);

            rust.Notice(netUser, $"{lang.GetMessage("SuccessBannedNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessBannedBroadcast", this)}");

            Puts($"{targetUser.displayName} {lang.GetMessage("SuccessBannedBroadcast", this)}");

            return;
        }

        void cmdBanID(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanBan)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            string targetUserIDString = args[0];

            if (targetUserIDString.Length != 17)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSteamID", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (rust.FindPlayer(targetUserIDString) == null)
            {
                rust.RunServerCommand($"banid {targetUserIDString} \"OfflinePlayer\" \"Reason\"");

                rust.Notice(netUser, $"{lang.GetMessage("SuccessBannedNotice", this)} {targetUserIDString}.", lang.GetMessage("SuccessIcon", this));
                rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUserIDString} {lang.GetMessage("SuccessBannedBroadcast", this)}");

                Puts($"{targetUserIDString} {lang.GetMessage("SuccessBannedBroadcast", this)}");

                return;
            }

            NetUser targetUser = rust.FindPlayer(targetUserIDString);

            targetUser.Ban();
            targetUser.Kick(NetError.Facepunch_Kick_Ban, true);

            rust.Notice(netUser, $"{lang.GetMessage("SuccessBannedNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessBannedBroadcast", this)}");

            Puts($"{targetUser.displayName} {lang.GetMessage("SuccessBannedBroadcast", this)}");

            return;
        }

        void cmdUnban(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanBan)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            string targetUserIDString = args[0];

            if (targetUserIDString.Length != 17)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSteamID", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            rust.RunServerCommand($"removeid {targetUserIDString}");

            rust.Notice(netUser, $"{lang.GetMessage("SuccessUnbanNotice", this)} {targetUserIDString}.", lang.GetMessage("SuccessIcon", this));
            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUserIDString} {lang.GetMessage("SuccessUnbanBroadcast", this)}");

            Puts($"{targetUserIDString} {lang.GetMessage("SuccessUnbanBroadcast", this)}");

            return;
        }

        void cmdClose(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanClose)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            NetUser targetUser = rust.FindPlayer(args[0]);

            if (targetUser == null)
            {
                rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            rust.RunClientCommand(targetUser, "quit");

            rust.Notice(netUser, $"{targetUser.displayName}{lang.GetMessage("SuccessCloseNotice", this)}", lang.GetMessage("SuccessIcon", this));

            Puts($"{targetUser.displayName}{lang.GetMessage("SuccessCloseNotice", this)}");

            return;
        }

        void cmdMute(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanMute)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 1)
            {
                NetUser targetUser = rust.FindPlayer(args[0]);

                ulong targetUserID = targetUser.userID;

                if (targetUser == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                if (permission.UserHasPermission(targetUserID.ToString(), permissionIsMuted))
                {
                    permission.RevokeUserPermission(targetUserID.ToString(), permissionIsMuted);

                    rust.Notice(netUser, $"{lang.GetMessage("SuccessUnmuteNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
                    rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    Puts($"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    return;
                }

                permission.GrantUserPermission(targetUserID.ToString(), permissionIsMuted, this);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessMutedNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
                rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessMutedBroadcast", this)} 5 minute(s).");

                timer.Once(300f, () =>
                {
                    if (!(permission.UserHasPermission(targetUserID.ToString(), permissionIsMuted)))
                    {
                        return;
                    }

                    permission.RevokeUserPermission(targetUserID.ToString(), permissionIsMuted);

                    rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    Puts($"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    return;
                });

                return;
            }

            if (args.Length == 2)
            {
                NetUser targetUser = rust.FindPlayer(args[0]);

                ulong targetUserID = targetUser.userID;

                if (targetUser == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                int time;

                if (!(Int32.TryParse(args[1], out time)))
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidInteger", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                if (permission.UserHasPermission(targetUserID.ToString(), permissionIsMuted))
                {
                    permission.RevokeUserPermission(targetUserID.ToString(), permissionIsMuted);

                    rust.Notice(netUser, $"{lang.GetMessage("SuccessUnmuteNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
                    rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    Puts($"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    return;
                }

                permission.GrantUserPermission(targetUserID.ToString(), permissionIsMuted, this);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessMutedNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
                rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessMutedBroadcast", this)} {time} minute(s).");

                timer.Once(time * 60, () =>
                {
                    if (!(permission.UserHasPermission(targetUserID.ToString(), permissionIsMuted)))
                    {
                        return;
                    }

                    permission.RevokeUserPermission(targetUserID.ToString(), permissionIsMuted);

                    rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    Puts($"{targetUser.displayName} {lang.GetMessage("SuccessUnmuteBroadcast", this)}");

                    return;
                });

                return;
            }
        }

        void cmdTeleport(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanTeleport)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 1)
            {
                NetUser targetUser = rust.FindPlayer(args[0]);

                if (targetUser == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                serverManagement.TeleportPlayerToPlayer(netUser.playerClient.netPlayer, targetUser.playerClient.netPlayer);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessTeleportToPlayerNotice", this)} {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));
                rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{netUser.displayName} {lang.GetMessage("SuccessTeleportToPlayerBroadcast", this)} {targetUser.displayName}.");

                Puts($"{netUser.displayName} {lang.GetMessage("SuccessTeleportToPlayerBroadcast", this)} {targetUser.displayName}.");

                return;
            }

            if (args.Length == 2)
            {
                NetUser targetUser1 = rust.FindPlayer(args[0]);
                NetUser targetUser2 = rust.FindPlayer(args[1]);

                if (targetUser1 == null || targetUser2 == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                serverManagement.TeleportPlayerToPlayer(targetUser1.playerClient.netPlayer, targetUser2.playerClient.netPlayer);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessTeleportOtherPlayerNotice", this)} {targetUser1.displayName} to {targetUser2.displayName}.", lang.GetMessage("SuccessIcon", this));
                rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser1.displayName} {lang.GetMessage("SuccessTeleportOtherPlayerBroadcast", this)} {targetUser2.displayName}.");

                Puts($"{targetUser1.displayName} {lang.GetMessage("SuccessTeleportOtherPlayerBroadcast", this)} {targetUser2.displayName}.");

                return;
            }

            if (args.Length == 3)
            {
                string inX = args[0], inY = args[1], inZ = args[2];

                int outX, outY, outZ;

                if (!(Int32.TryParse(inX, out outX) && Int32.TryParse(inY, out outY) && Int32.TryParse(inZ, out outZ)))
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidInteger", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                Vector3 destination = new Vector3((float)outX, (float)outY, (float)outZ);

                serverManagement.TeleportPlayerToWorld(netUser.playerClient.netPlayer, destination);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessTeleportToPositionNotice", this)} {outX}, {outY}, {outZ}.", lang.GetMessage("SuccessIcon", this));

                Puts($"{netUser.displayName} {lang.GetMessage("SuccessTeleportToPositionBroadcast", this)} {outX}, {outY}, {outZ}.");

                return;
            }

            if (args.Length == 4)
            {
                NetUser targetUser = rust.FindPlayer(args[0]);

                if (targetUser == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                string inX = args[1], inY = args[2], inZ = args[3];

                int outX, outY, outZ;

                if (!(Int32.TryParse(inX, out outX) && Int32.TryParse(inY, out outY) && Int32.TryParse(inZ, out outZ)))
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidInteger", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                Vector3 destination = new Vector3((float)outX, (float)outY, (float)outZ);

                serverManagement.TeleportPlayerToWorld(targetUser.playerClient.netPlayer, destination);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessTeleportOtherPlayerToPositionNotice", this)} {targetUser.displayName} to coordinates: {outX}, {outY}, {outZ}.", lang.GetMessage("SuccessIcon", this));

                Puts($"{targetUser.displayName} {lang.GetMessage("SuccessTeleportOtherPlayerToPositionBroadcast", this)} {outX}, {outY}, {outZ}.");

                return;
            }

            return;
        }

        void cmdBring(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanTeleport)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args.Length > 1 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            NetUser targetUser = rust.FindPlayer(args[0]);

            if (targetUser == null)
            {
                rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            serverManagement.TeleportPlayerToPlayer(targetUser.playerClient.netPlayer, netUser.playerClient.netPlayer);

            rust.Notice(netUser, $"{targetUser.displayName} {lang.GetMessage("SuccessBringNotice", this)}", lang.GetMessage("SuccessIcon", this));

            rust.BroadcastChat(lang.GetMessage("ChatPrefix", this), $"{targetUser.displayName} {lang.GetMessage("SuccessBringBroadcast", this)} {netUser.displayName}.");

            Puts($"{targetUser.displayName} {lang.GetMessage("SuccessBringBroadcast", this)} {netUser.displayName}.");

            return;
        }

        void cmdGive(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanGive)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args.Length < 2 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 2)
            {
                NetUser targetUser;

                if (args[0] == "me")
                {
                    targetUser = netUser;
                }
                else
                {
                    targetUser = rust.FindPlayer(args[0]);
                }

                if (targetUser == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                ItemDataBlock dataBlock = DatablockDictionary.GetByName(args[1]);

                if (dataBlock == null)
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidItem", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                Inventory inventory = targetUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();

                inventory.AddItemAmount(dataBlock, 1);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessGiveOneOfNotice", this)} {args[1]}.", lang.GetMessage("SuccessIcon", this));

                Puts($"{netUser.displayName} {lang.GetMessage("SuccessGiveOneOfBroadcast", this)} {args[1]}.");

                return;
            }

            if (args.Length == 3)
            {
                NetUser targetUser;

                if (args[0] == "me")
                {
                    targetUser = netUser;
                }
                else
                {
                    targetUser = rust.FindPlayer(args[0]);
                }

                if (targetUser == null)
                {
                    rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                ItemDataBlock dataBlock = DatablockDictionary.GetByName(args[1]);

                if (dataBlock == null)
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidItem", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                int amount;

                if (!(Int32.TryParse(args[2], out amount)))
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidInteger", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                Inventory inventory = targetUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();

                inventory.AddItemAmount(dataBlock, amount);

                rust.Notice(netUser, $"{lang.GetMessage("SuccessGiveMultipleOfNotice", this)} {args[1]} x {args[2]}.", lang.GetMessage("SuccessIcon", this));

                Puts($"{netUser.displayName} {lang.GetMessage("SuccessGiveMultipleOfBroadcast", this)} {args[1]} x {args[2]}.");

                return;
            }

            return;
        }

        void cmdAirdrop(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanCallAirdrop)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                SupplyDropZone.CallAirDrop();

                rust.Notice(netUser, lang.GetMessage("SuccessAirdropCalledNotice", this), lang.GetMessage("SuccessIcon", this));

                Puts($"{lang.GetMessage("SuccessAirdropCalledBroadcast", this)} {netUser.displayName}.");

                return;
            }

            if (args.Length == 1)
            {
                int amount;

                if (!(Int32.TryParse(args[0], out amount)))
                {
                    rust.Notice(netUser, lang.GetMessage("InvalidInteger", this), lang.GetMessage("FailureIcon", this));
                    return;
                }

                timer.Repeat(0, amount, () => SupplyDropZone.CallAirDrop());

                rust.Notice(netUser, $"{amount} {lang.GetMessage("SuccessAirdropsCalledNotice", this)}", lang.GetMessage("SuccessIcon", this));

                Puts($"{amount} {lang.GetMessage("SuccessAirdropsCalledBroadcast", this)} {netUser.displayName}.");

                return;
            }

            return;
        }

        void cmdPopup(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanPopupNotice)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            string message = "";

            foreach (string arg in args)
            {
                message = message + " " + arg;
            }

            foreach (NetUser _netUser in rust.GetAllNetUsers())
            {
                rust.Notice(_netUser, message, lang.GetMessage("SuccessIcon", this));
            }

            rust.Notice(netUser, lang.GetMessage("SuccessPopupNotice", this), lang.GetMessage("SuccessIcon", this));

            Puts($"{netUser.displayName} {lang.GetMessage("SuccessPopupBroadcast", this)} {message}.");

            return;
        }

        void cmdTime(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanChangeTime)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            int time;

            if (!(Int32.TryParse(args[0], out time)))
            {
                rust.Notice(netUser, lang.GetMessage("InvalidInteger", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            EnvironmentControlCenter.Singleton.SetTime((float)time);

            foreach (NetUser _netUser in rust.GetAllNetUsers())
            {
                rust.Notice(_netUser, $"{lang.GetMessage("SuccessTimeBroadcast", this)} {time}.", lang.GetMessage("SuccessIcon", this));
            }

            rust.Notice(netUser, $"{lang.GetMessage("SuccessTimeNotice", this)} {time}.", lang.GetMessage("SuccessIcon", this));

            Puts($"{lang.GetMessage("SuccessTimeBroadcast", this)} {time}.");

            return;
        }

        void cmdInvisible(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanSpawnInvisibleSuit)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            ItemDataBlock helmet = DatablockDictionary.GetByName("Invisible Helmet");
            ItemDataBlock vest = DatablockDictionary.GetByName("Invisible Vest");
            ItemDataBlock pants = DatablockDictionary.GetByName("Invisible Pants");
            ItemDataBlock boots = DatablockDictionary.GetByName("Invisible Boots");

            Inventory inventory = netUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();

            inventory.AddItemAmount(helmet, 1);
            inventory.AddItemAmount(vest, 1);
            inventory.AddItemAmount(pants, 1);
            inventory.AddItemAmount(boots, 1);

            rust.Notice(netUser, lang.GetMessage("SuccessInvisibleNotice", this), lang.GetMessage("SuccessIcon", this));

            Puts($"{netUser.displayName} {lang.GetMessage("SuccessInvisibleBroadcast", this)}");

            return;
        }

        void cmdCommand(NetUser netUser, string command, string[] args)
        {
            ulong netUserID = netUser.userID;

            if (!(netUser.CanAdmin() || permission.UserHasPermission(netUserID.ToString(), permissionCanRunCommand)))
            {
                rust.Notice(netUser, lang.GetMessage("NoPermission", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            if (args.Length == 0 || args.Length < 2 || args == null)
            {
                rust.Notice(netUser, lang.GetMessage("InvalidSyntax", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            NetUser targetUser = rust.FindPlayer(args[0]);

            if (targetUser == null)
            {
                rust.Notice(netUser, lang.GetMessage("NoPlayersFound", this), lang.GetMessage("FailureIcon", this));
                return;
            }

            rust.RunClientCommand(targetUser, args[1]);

            rust.Notice(netUser, $"{lang.GetMessage("SuccessCommandNotice", this)} '{args[1]}' on {targetUser.displayName}.", lang.GetMessage("SuccessIcon", this));

            Puts($"{netUser.displayName} {lang.GetMessage("SuccessCommandBroadcast", this)} '{args[1]}' on {targetUser.displayName}.");

            return;
        }
    }
}