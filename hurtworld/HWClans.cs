using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Plugins;
using Steamworks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using uLink;

namespace Oxide.Plugins
{
    #region Changelog
    /*
    Changelog 1.3.4
    Fixed:
        *   
    Added:
        *   New API methods.
        *   Player's ID to allow different language message files. (.en, .pt, .pl, .es, ...)
    Removed:
        *   Debug checks
    Changed:
        *   Code changes.
    */
    #endregion Changelog

    [Info("HWClans", "SouZa", "1.3.4", ResourceId = 1704)]
    class HWClans : HurtworldPlugin
    {
        #region Plugin References
        [PluginReference("BetterChat")]
        Plugin BetterChat;
        #endregion Plugin References

        #region Enums
        enum ELogType
        {
            Info,
            Warning,
            Error
        }
        #endregion

        #region [CLASSES]
        public class Clan
        {
            public Clan()
            {

            }

            public Clan(string ownerName, CSteamID ownerID, string clanTag)
            {
                this.ownerID = ownerID;
                this.clanTag = clanTag;
                this.clanTagColor = "white";
                this.members = new List<ClanMember>();
                ClanMember owner = new ClanMember(ownerName, ownerID, true);
                members.Add(owner);
                this.invites = new List<CSteamID>();
            }

            public Clan(CSteamID ownerID, string clanTag, string clanTagColor, List<ClanMember> members, List<CSteamID> invites)
            {
                this.ownerID = ownerID;
                this.clanTag = clanTag;
                this.clanTagColor = clanTagColor;
                this.members = members;
                this.invites = invites;
            }
            
            public CSteamID ownerID { get; set; }
            public string clanTag { get; set; }
            public string clanTagColor { get; set; }
            public List<ClanMember> members { get; set; }
            public List<CSteamID> invites { get; set; }
        }

        public class ClanMember
        {
            public ClanMember()
            {

            }

            public ClanMember(string name, CSteamID id, bool isModerator)
            {
                this.name = name;
                this.id = id;
                this.isModerator = isModerator;
            }

            public string name { get; set; }
            public CSteamID id { get; set; }
            public bool isModerator { get; set; }
        }
        #endregion

        #region Variables
        public Dictionary<string, Clan> ClansDict = new Dictionary<string, Clan>();             //ClanTag - Clan
        public Dictionary<ulong, string> PlayersDict = new Dictionary<ulong, string>();         //PlayerID - ClanTag
        //A list of all players using clan chat (activated with /tc command)
        public HashSet<ulong> usingClanChat = new HashSet<ulong>();
        public string PermissionPrefix { get; set; }
        #endregion Variables

        #region Methods
        void Loaded()
        {
            LoadConfig();
            LoadMessages();
            LoadData();
            LoadPermissions();
        }

        void Log(ELogType type, string message)
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

        #region Config | Messages
        protected override void LoadDefaultConfig()
        {
            Log(ELogType.Warning, "No config file found, generating a new one.");
        }

        new void LoadConfig()
        {
            SetConfig(false, "ClanPermissionByDefault", true);

            SetConfig(false, "GlobalChatOutput", "FormatHasClan", "{ClanTag} {PlayerName}: {Message}");
            SetConfig(false, "GlobalChatOutput", "FormatNotClan", "{PlayerName}: {Message}");
            SetConfig(false, "GlobalChatOutput", "PlayerNameColor", "#6495be");
            SetConfig(false, "GlobalChatOutput", "MessageColor", "white");

            SetConfig(false, "ClanChatOutput", "Format", "{Prefix}{PlayerName}: {Message}");
            SetConfig(false, "ClanChatOutput", "PrefixText", "[Clan Message] ");
            SetConfig(false, "ClanChatOutput", "PrefixColor", "orange");
            SetConfig(false, "ClanChatOutput", "PlayerNameColor", "#6495be");
            SetConfig(false, "ClanChatOutput", "MessageColor", "white");

            SetConfig(false, "ClanTag", "ShowAbovePlayerHead", false);
            SetConfig(false, "ClanTag", "DefaultColor", "yellow");
            SetConfig(false, "ClanTag", "AllowClanOwnerChangeColor", false);
            SetConfig(false, "ClanTag", "MaxLength", 5);
            SetConfig(false, "ClanTag", "WordFilterContains", new string[] { "admin"});
            SetConfig(false, "ClanTag", "WordFilterExact", new string[] { "mod", "m0d", "moderator"});

            SetConfig(false, "MaxMembers", 20);

            SaveConfig();
        }

        #region Config Helpers
        void SetConfig(bool replace, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            stringArgs.RemoveAt(args.Length - 1);

            if (replace || Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }
        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get<T>(stringArgs.ToArray()), typeof(T));
        }
        string ListToString(List<string> list, int first, string seperator)
        {
            return String.Join(seperator, list.Skip(first).ToArray());
        }
        #endregion Config Helpers

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"clanCmds_AVAILABLE", "Available Clan Commands"},
                {"clanCmds_PLAYER", "Player Commands"},
                {"clanCmds_OWNER", "Owner Commands"},
                {"clanCmds_MODERATOR", "Moderator Commands"},
                {"clanCmds_ADMIN", "Admin Commands"},
                {"clanCmds_PLAYER_clanInfo", " - Displays relevant information about your current clan."},
                {"clanCmds_PLAYER_message", " - Sends a message to all online clan members."},
                {"clanCmds_PLAYER_toogleChat", " - Toogles clan chat."},
                {"clanCmds_PLAYER_create", " - Create a new clan."},
                {"clanCmds_PLAYER_join", " - Joins a clan you have been invited to."},
                {"clanCmds_PLAYER_leave", " - Leaves your current clan."},
                {"clanCmds_PLAYER_showInvites", " - Shows clan invites."},
                {"clanCmds_MODERATOR_invite", " - Invites a player to your clan."},
                {"clanCmds_MODERATOR_kick", " - Kicks a member from your clan."},
                {"clanCmds_OWNER_tagcolor", " - Changes Clan Tag color."},
                {"clanCmds_OWNER_promote", " - Promotes a member to moderator."},
                {"clanCmds_OWNER_demote", " - Demotes a moderator to member."},
                {"clanCmds_OWNER_disband", " - Disbands your clan (no undo)."},
                {"clanCmds_ADMIN_list", " - Lists all current clans."},
                {"clanCmds_ADMIN_inspect", " - Inspect a clan."},
                {"clanCmds_ADMIN_invite", " - Invites a player to a clan."},
                {"clanCmds_ADMIN_kick", " - Kicks a player from a clan."},
                {"clanCmds_ADMIN_disband", " - Disbands a clan (no undo)."},
                {"clanCmds_ADMIN_promote", " - Promotes a member to moderator."},
                {"clanCmds_ADMIN_demote", " - Demotes a moderator to member."},
                {"clanCmds_ADMIN_owner", " - Changes clan owner."},
                {"clanCmds_ADMIN_clanTag", " - Changes clan tag."},
                {"msg_permission", "You don't have permission to use this command." },
                {"msg_SERVER", "SERVER "},
                {"msg_INFO", "INFO "},
                {"msg_CLAN", "CLAN " },
                {"msg_clanUsage", "Type /clan help for proper Clan Commands usage."},
                {"msg_usingClanChat", "Using clan chat."},
                {"msg_usingGlobalChat", "Using global chat."},
                {"msg_canNotLeaveClan", "Clan owner can't leave clan, only disband it."},
                {"msg_hasLeftClan", "{playerName} has left the clan."},
                {"msg_doNotHaveClan", "You don't have a clan."},
                {"msg_disbandClan", "The owner {playerName} has disbanded the clan."},
                {"msg_adminDisbandClan", "The Admin {playerName} has disbanded the clan {ClanTag}."},
                {"msg_alreadyBelongToClan", "You already belong to a clan."},
                {"msg_clanTagExist", "Clan TAG already exists."},
                {"msg_clanTagLengthLimit", "Clan TAG length ({length}) exceeds the limit defined ({limit})."},
                {"msg_clanTagSpecialChar", "Clan TAG can't contain special characters."},
                {"msg_clanTagSetColorNotAllowed", "This server doesn't allow clan owners to change its Clan Tag color." },
                {"msg_clanTagNewColor", "You changed the color of your Clan Tag." },
                {"msg_clanTagFilterContain", "The clan tag contains a blocked word: {block}." },
                {"msg_clanTagFilterExact", "The clan tag {block} is a blocked tag." },
                {"msg_createClan", "You created a new clan: {clanTAG}"},
                {"msg_clanNotExist", "This clan does not exist."},
                {"msg_clanNotInviteYou", "This clan has not invited you."},
                {"msg_hasJoinedClan", "{playerName} has joined the Clan!"},
                {"msg_notClanMod", "You are not a Clan Moderator."},
                {"msg_notClanOwner", "You are not a Clan Owner."},
                {"msg_inviteToClan", "You invited {playerName} to your clan."},
                {"msg_hasBeenInvited", "You have been invited to the clan: {clanTAG}"},
                {"msg_alreadyInvited", "Your clan already invited {playerName}."},
                {"msg_playerNotOnline", "{playerName} is not online."},
                {"msg_notClanMember", "{playerName} is not a clan member."},
                {"msg_canNotKickSelf", "You can not kick youself."},
                {"msg_kickFromClan", "You have been removed from your Clan."},
                {"msg_hasBeenKicked", "{playerName} has been removed from the Clan."},
                {"msg_ownerCanNotPromoteDemoteSelf", "You are the owner. Can't promote/demote yourself."},
                {"msg_hasBeenPromoted", "{playerName} has been promoted to Clan Moderator."},
                {"msg_hasBeenDemoted", "{playerName} has been demoted from Clan Moderator."},
                {"msg_canNotBePromoted", "{playerName} is already a Clan Moderator."},
                {"msg_canNotBeDemoted", "{playerName} can not be demoted. He is not a Clan Moderator."},
                {"msg_onlyOwnerCanChangeRanks", "Only clan owner can change members rank."},
                {"msg_notAdmin", "You are not a server Admin."},
                {"msg_noClanInvites", "You don't have clan invites."},
                {"msg_newOwner", "{playerName} is the new clan owner."},
                {"msg_newClanTag", "{newTag} is the new ClanTag."},
                {"msg_memberLimit", "The clan is full." }
            }, this);
        }
        #endregion Config | Messages

        #region Data
        void LoadData()
        {
            var _clans = Interface.GetMod().DataFileSystem.ReadObject<Collection<Clan>>("ClansData");
            foreach (var item in _clans)
            {
                var ownerID = item.ownerID;
                var clanTag = item.clanTag;
                var clanTagColor = item.clanTagColor;
                var members = item.members;
                var invites = item.invites;
                
                ClansDict[clanTag] = new Clan(ownerID, clanTag, clanTagColor, members, invites);
            }
            
        }
        void SaveData()
        {
            List<Clan> tmp = new List<Clan>();
            foreach(Clan clan in ClansDict.Values)
            {
                tmp.Add(clan);
            }
            Interface.GetMod().DataFileSystem.WriteObject("ClansData", tmp);
        }
        #endregion Data

        #region Permissions
        void LoadPermissions()
        {
            PermissionPrefix = Regex.Replace(Title, "[^0-9a-zA-Z]+", string.Empty).ToLower();
            RegisterPermission("use");

            if (GetConfig(false, "ClanPermissionByDefault"))
                permission.GrantGroupPermission("default", "hwclans.use", this);
        }
        public void RegisterPermission(params string[] paramArray)
        {
            var perms = ArrayToString(paramArray, ".");
            permission.RegisterPermission(
                perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}",
                this);
        }

        public bool HasPermission(PlayerSession session, params string[] paramArray)
        {
            var perms = ArrayToString(paramArray, ".");
            return permission.UserHasPermission(session.SteamId.m_SteamID.ToString(),
                perms.StartsWith(PermissionPrefix) ? $"{perms}" : $"{PermissionPrefix}.{perms}");
        }

        public string ArrayToString(string[] array, string separator)
        {
            return string.Join(separator, array);
        }
        #endregion Permissions

        #region PlayerInformation
        bool IsValidSession(PlayerSession session)
        {
            return session != null && session?.SteamId != null && session.IsLoaded && session.Name != null && session.Identity != null &&
                   session.WorldPlayerEntity?.transform?.position != null;
        }
        
        PlayerSession getSessionBySteamId(string steamid)
        {
            foreach (CSteamID id in GameManager.Instance.GetIdentifierMap().Keys)
                if (id.ToString() == steamid)
                    return GameManager.Instance.GetIdentifierMap()[id].ConnectedSession;

            return null;
        }
        
        PlayerSession getSession(string identifier)
        {
            foreach (PlayerIdentity identity in GameManager.Instance.GetIdentifierMap().Values)
            {
                if (identity.Name.ToLower().Contains(identifier.ToLower()) || identity.SteamId.ToString().Equals(identifier))
                {
                    return identity.ConnectedSession;
                }
            }

            return null;
        }
        #endregion PlayerInformation

        #region Helpers
        //Credits @LaserHydra
        string RemoveTags(string phrase)
        {
            //	Forbidden formatting tags
            List<string> forbiddenTags = new List<string>{
                "</color>",
                "</size>",
                "<b>",
                "</b>",
                "<i>",
                "</i>"
            };

            //	Replace Color Tags
            phrase = new Regex("(<color=.+?>)").Replace(phrase, "");

            //	Replace Size Tags
            phrase = new Regex("(<size=.+?>)").Replace(phrase, "");

            foreach (string tag in forbiddenTags)
                phrase = phrase.Replace(tag, "");

            return phrase;
        }

        string GetMsg(string key, string userID) => lang.GetMessage(key, this, userID);

        string Color(string text, string color)
        {
            switch (color)
            {
                case "myRed":
                    return "<color=#ff0000ff>" + text + "</color>";

                case "myGreen":
                    return "<color=#00ff00ff>" + text + "</color>";

                case "myBlue":
                    return "<color=#00ffffff>" + text + "</color>";

                case "chatBlue":
                    return "<color=#6495be>" + text + "</color>";

                default:
                    return "<color=" + color + ">" + text + "</color>";
            }
        }
        bool clanTagFilter(PlayerSession session, string tag)
        {
            if (session.IsAdmin)
                return true;
            //1st - check if clanTag length is <= ClanTag MaxLength defined in the config file
            int clanTagLength = tag.Length;
            int clanTagLengthLimit = (int)GetConfig(5, "ClanTag", "MaxLength");
            if (clanTagLength > clanTagLengthLimit)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagLengthLimit", session.SteamId.ToString()).Replace("{length}", clanTagLength + "").Replace("{limit}", clanTagLengthLimit + ""));
                return false;
            }
            //2nd - check if clanTag doesn't contain special characters
            Regex r = new Regex("^[a-zA-Z0-9]*$");
            if (!r.IsMatch(tag))
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagSpecialChar", session.SteamId.ToString()));
                return false;
            }
            //3rd - check if clanTag doesn't contain words blocked in config file
            var filterContains = Config.Get<List<string>>("ClanTag", "WordFilterContains");
            var filterExact = Config.Get<List<string>>("ClanTag", "WordFilterExact");

            tag = tag.ToLower();
            foreach (string block in filterContains)
            {
                string word = block.ToLower();
                if (tag.Contains(word))
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagFilterContain", session.SteamId.ToString()).Replace("{block}", Color(word, "red")));
                    return false;
                }
            }
            foreach (string block in filterExact)
            {
                string word = block.ToLower();
                if (tag.Equals(word))
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagFilterExact", session.SteamId.ToString()).Replace("{block}", Color(word, "red")));
                    return false;
                }
            }

            return true;
        }
        void setClanTagAbovePlayerHead(PlayerSession session, string clanTag, string clanTagColor, bool add)
        {
            bool tagAboveHead = GetConfig(false, "ClanTag", "ShowAbovePlayerHead");
            if (!tagAboveHead)
                return;

            string tmp = "";
            if (add)
            {
                tmp = "<color=" + clanTagColor + ">[" + clanTag + "]</color> " + session.Name;
            }
            else
            {
                tmp = session.Name;
            }
            session.WorldPlayerEntity.GetComponent<HurtMonoBehavior>().RPC("UpdateName", uLink.RPCMode.OthersExceptOwnerBuffered, tmp);
        }
        string createGlobalChatMessage(string playerName, string content, object theClan)
        {
            string format = "";
            //defaultVariable will be used if config not found.
            string defaultMessage = "";
            if ((Clan)theClan != null)
            {
                defaultMessage = "[{ClanTag}] {PlayerName} {Message}";
                format = "FormatHasClan";
            }
            else {
                defaultMessage = "{PlayerName} {Message}";
                format = "FormatNotClan";
            }
            string defaultClanTagColor = "yellow";
            string defaultPlayerNameColor = "#6495be";
            string defaultMessageColor = "white";
            //Config Variables
            string clanTagColor;
            if ((Clan)theClan != null)
                clanTagColor = ((Clan)theClan).clanTagColor;
            else
                clanTagColor = GetConfig(defaultClanTagColor, "ClanTag", "DefaultColor");
            string playerNameColor = GetConfig(defaultPlayerNameColor, "GlobalChatOutput", "PlayerNameColor");
            string messageColor = GetConfig(defaultMessageColor, "GlobalChatOutput", "MessageColor");
            //The output format
            string outputClanTag;
            if ((Clan)theClan == null)
                outputClanTag = "[This Should Not Display]";
            else
                outputClanTag = "[" + ((Clan)theClan).clanTag + "]";
            string outputPlayerName = Color(playerName, playerNameColor);
            string outputMessage = Color(content, messageColor);
            //The final message ready to send
            string message = GetConfig(defaultMessage, "GlobalChatOutput", format).Replace("{ClanTag}", Color(outputClanTag, clanTagColor)).Replace("{PlayerName}", outputPlayerName).Replace("{Message}", outputMessage);

            return message;
        }
        string createClanChatMessage(string playerName, string content)
        {
            //defaultVariable will be used if config not found.
            string defaultMessage = "{Prefix}{PlayerName} {Message}";
            string defaultPrefixText = "CLAN ";
            string defaultPrefixColor = "orange";
            string defaultPlayerNameColor = "#6495be";
            string defaultMessageColor = "white";
            //Config Variables
            string prefixText = GetConfig(defaultPrefixText, "ClanChatOutput", "PrefixText");
            string prefixColor = GetConfig(defaultPrefixColor, "ClanChatOutput", "PrefixColor");
            string playerNameColor = GetConfig(defaultPlayerNameColor, "ClanChatOutput", "PlayerNameColor");
            string messageColor = GetConfig(defaultMessageColor, "ClanChatOutput", "MessageColor");
            //The output format
            string outputPrefix = Color(prefixText, prefixColor);
            string outputPlayerName = Color(playerName, playerNameColor);
            string outputMessage = Color(content, messageColor);
            //The final message ready to send
            string message = GetConfig(defaultMessage, "ClanChatOutput", "Format").Replace("{Prefix}", outputPrefix).Replace("{PlayerName}", outputPlayerName).Replace("{Message}", outputMessage);

            return message;
        }
        void clanChatMessage(PlayerSession pluginDev, Clan clan, string message)
        {
            //PlayerSession pluginDev was for debug.

            List<PlayerSession> clanSessions = getMembersOnline_byClan(clan);
            foreach (PlayerSession session in clanSessions)
            {
                hurt.SendChatMessage(session, message);
            }
        }
        void clanInfo(PlayerSession session, Clan myClan)
        {
            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + "Clan Information");
            hurt.SendChatMessage(session, Color("Clan TAG - ", "orange") + Color(myClan.clanTag, myClan.clanTagColor));
            string clanMembers = "";

            foreach (ClanMember member in myClan.members)
            {
                PlayerSession memberSession = getSessionBySteamId(member.id.ToString());

                string status = Color(" (Off)", "#ff3232");
                if (IsValidSession(memberSession))
                    status = Color(" (On)", "#4fcc00");

                string memberName =
                    member.id == myClan.ownerID ? memberName = Color(member.name, "myGreen") :
                    member.isModerator ? memberName = Color(member.name, "myBlue") :
                    memberName = member.name;
                /*
                if (member.id == myClan.ownerID)
                    memberName = Color(member.name, "myGreen");
                else if (member.isModerator)
                    memberName = Color(member.name, "myBlue");
                else
                    memberName = member.name;
                */

                clanMembers += (memberName + status + " | ");
            }
            hurt.SendChatMessage(session, Color("Clan Members -", "orange") + " | " + clanMembers);
        }
        Clan getClan(PlayerSession session)
        {
            Clan myClan = null;
            string clanTag = null;

            PlayersDict.TryGetValue(session.SteamId.m_SteamID, out clanTag);

            if (clanTag != null)
                ClansDict.TryGetValue(clanTag, out myClan);
            else
            {
                myClan = findClan(session);
                if (myClan != null)
                    PlayersDict[session.SteamId.m_SteamID] = myClan.clanTag;
            }

            return myClan;
        }
        Clan findClan(PlayerSession session)
        {
            foreach (Clan clan in ClansDict.Values)
            {
                foreach (ClanMember member in clan.members)
                {
                    if (member.id == session.SteamId)
                        return clan;
                }
            }
            return null;
        }
        Clan findClan(ulong playerid)
        {
            foreach (Clan clan in ClansDict.Values)
            {
                foreach (ClanMember member in clan.members)
                {
                    if ((ulong)member.id == playerid)
                        return clan;
                }
            }
            return null;
        }
        bool isModeratorOfClan(Clan clan, PlayerSession session)
        {
            foreach (ClanMember member in clan.members)
            {
                if (member.id == session.SteamId)
                    return member.isModerator;
            }
            return false;
        }
        //Check if player is using clan chat (activated with /tc command)
        bool playerIsUsingClanChat(CSteamID playerID) => usingClanChat.Contains(playerID.m_SteamID);
        bool playerIsUsingClanChat_byUlongID(ulong playerSteamId) => usingClanChat.Contains(playerSteamId);
        #endregion Helpers

        #endregion Methods
        
        #region Chat Commands
        [ChatCommand("clan")]
        void cmdClan(PlayerSession session, string command, string[] args)
        {
            //Check if user has permission to use </clan> commands.
            if (!permission.UserHasPermission(session.SteamId.ToString(), "hwclans.use") && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER", session.SteamId.ToString()), "myRed") + GetMsg("msg_permission", session.SteamId.ToString()));
                return;
            }

            switch (args.Length)
            {
                case 0:
                    cmdClanInfo(session);
                    break;
                case 1:
                    if (args[0] == "help" || args[0] == "h")
                        cmdClanHelp(session);
                    else if (args[0] == "invites")
                        cmdClanInvites(session);
                    else if (args[0] == "list")
                        cmdClanList(session);
                    else if (args[0] == "leave")
                        cmdClanLeave(session);
                    else if (args[0] == "disband")
                        cmdClanDisband(session, args, false);
                    else if (args[0] == "version")
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + "Version 1.3.2");
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanUsage", session.SteamId.ToString()));
                    break;
                case 2:
                    if (args[0] == "help")
                        cmdClanShowCmds(session, args);
                    else if (args[0] == "inspect")
                        cmdClanInspect(session, args);
                    else if (args[0] == "tagcolor")
                        cmdClanTagColor(session, args);
                    else if (args[0] == "create")
                        cmdClanCreate(session, args);
                    else if (args[0] == "join")
                        cmdClanJoin(session, args);
                    else if (args[0] == "invite")
                        cmdClanInvite(session, args, false);
                    else if (args[0] == "kick")
                        cmdClanKick(session, args, false);
                    else if (args[0] == "promote" || args[0] == "demote")
                        cmdClanPromoteDemote(session, args, false);
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanUsage", session.SteamId.ToString()));
                    break;
                case 3:
                     if (args[0] == "admin" && args[1] == "disband")
                        cmdClanDisband(session, args, true);
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanUsage", session.SteamId.ToString()));
                    break;
                case 4:
                    if (args[0] == "admin" && args[1] == "invite")
                        cmdClanInvite(session, args, true);
                    else if (args[0] == "admin" && args[1] == "kick")
                        cmdClanKick(session, args, true);
                    else if (args[0] == "admin" && args[1] == "promote")
                        cmdClanPromoteDemote(session, args, true);
                    else if (args[0] == "admin" && args[1] == "demote")
                        cmdClanPromoteDemote(session, args, true);
                    else if (args[0] == "admin" && args[1] == "owner")
                        cmdClanOwner(session, args);
                    else if (args[0] == "admin" && args[1] == "tag")
                        cmdClanTag(session, args);
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanUsage", session.SteamId.ToString()));
                    break;
                default:
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanUsage", session.SteamId.ToString()));
                    break;
            }
        }

        [ChatCommand("c")]
        void cmdClanChat(PlayerSession session, string command, string[] args)
        {
            //Check if user has permission to use </c> command
            if (!permission.UserHasPermission(session.SteamId.ToString(), "hwclans.use"))
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER", session.SteamId.ToString()), "myRed") + GetMsg("msg_permission", session.SteamId.ToString()));
                return;
            }
            
            Clan myClan = getClan(session);
            if (myClan != null)
            {
                string content = "";
                foreach (string word in args)
                    content += word + " ";

                /*
                if (BetterChat != null)
                {
                    BetterChat.Call("OnPlayerChat", session, content, true);
                    return;
                }
                */

                string clanMessage = createClanChatMessage(session.Name, content);
                
                clanChatMessage(session, myClan, clanMessage);
                Puts("[" + myClan.clanTag + "] " + RemoveTags(clanMessage));
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
        }

        [ChatCommand("tc")]
        void cmdClanChatToogle(PlayerSession session, string command, string[] args)
        {
            //Check if user has permission to use </tc> command
            if (!permission.UserHasPermission(session.SteamId.ToString(), "hwclans.use"))
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_SERVER", session.SteamId.ToString()), "myRed") + GetMsg("msg_permission", session.SteamId.ToString()));
                return;
            }

            Clan myClan = getClan(session);
            if(myClan != null)
            {
                if (playerIsUsingClanChat_byUlongID(session.SteamId.m_SteamID))
                {
                    usingClanChat.Remove(session.SteamId.m_SteamID);
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_usingGlobalChat", session.SteamId.ToString()));
                    return;
                }

                usingClanChat.Add(session.SteamId.m_SteamID);
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_usingClanChat", session.SteamId.ToString()));
                return;
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
        }

        #region Chat Command Help Methods
        // [/clan]
        void cmdClanInfo(PlayerSession session)
        {
            Clan myClan = getClan(session);

            if (myClan != null)
            {
                clanInfo(session, myClan);
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
        }
        // [/clan help|h]
        void cmdClanHelp(PlayerSession session)
        {
            string[] clanCmds = {
                Color("/clan help player - ", "orange") + GetMsg("clanCmds_PLAYER", session.SteamId.ToString()),
                Color("/clan help owner - ", "orange") + GetMsg("clanCmds_OWNER", session.SteamId.ToString()),
                Color("/clan help mod - ", "orange") + GetMsg("clanCmds_MODERATOR", session.SteamId.ToString()),
                Color("/clan help admin - ", "orange") + GetMsg("clanCmds_ADMIN", session.SteamId.ToString())
            };
            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("clanCmds_AVAILABLE", session.SteamId.ToString()));
            foreach (string cmd in clanCmds)
                hurt.SendChatMessage(session, cmd);
        }
        // [/clan invites]
        void cmdClanInvites(PlayerSession session)
        {
            string invites = "";
            int count = 0;
            foreach (Clan clan in ClansDict.Values)
            {
                foreach (CSteamID id in clan.invites)
                {
                    if (id == session.SteamId)
                    {
                        invites += Color(clan.clanTag, clan.clanTagColor) + " | ";
                        count++;
                        break;
                    }
                }
            }
            if (count != 0)
                hurt.SendChatMessage(session, Color("Clan Invites -", "orange") + " | " + invites);
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_noClanInvites", session.SteamId.ToString()));
        }
        // [/clan list]
        void cmdClanList(PlayerSession session)
        {
            if (!session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }
            string clanList = "";
            foreach (Clan clan in ClansDict.Values)
            {
                string clanTAG = clan.clanTag;
                string tagColor = clan.clanTagColor;
                clanList += (Color(clanTAG, tagColor) + " | ");
            }
            hurt.SendChatMessage(session, Color("Clan List -", "orange") + " | " + clanList);
        }
        // [/clan leave]
        void cmdClanLeave(PlayerSession session)
        {
            Clan myClan = getClan(session);
            if (myClan != null)
            {
                if (myClan.ownerID == session.SteamId)
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_canNotLeaveClan", session.SteamId.ToString()));
                    return;
                }
                foreach (ClanMember member in myClan.members)
                {
                    if (member.id == session.SteamId)
                    {
                        // if member usingClanChat, change to usingGlobalChat
                        if (playerIsUsingClanChat_byUlongID(member.id.m_SteamID))
                            usingClanChat.Remove(member.id.m_SteamID);
                        // remove entry from playerClans
                        if (PlayersDict.ContainsKey(session.SteamId.m_SteamID))
                            PlayersDict.Remove(session.SteamId.m_SteamID);
                        //can leave clan
                        clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_hasLeftClan", session.SteamId.ToString()).Replace("{playerName}", Color(session.Name, "chatBlue")));
                        myClan.members.Remove(member);
                        SaveData();
                        //remove ClanTag before player name above character's head
                        setClanTagAbovePlayerHead(session, null, null, false);
                        break;
                    }
                }
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
        }
        // [/clan disband]
        // [/clan admin disband <ClanTag>]
        void cmdClanDisband(PlayerSession session, string[] args, bool adminCmd)
        {
            if (adminCmd && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }

            Clan myClan = null;

            if (adminCmd)
                ClansDict.TryGetValue(args[2], out myClan);
            else
                myClan = getClan(session);

            if (myClan != null)
            {
                if (myClan.ownerID == session.SteamId || adminCmd)
                {
                    //foreach member: if member usingClanChat, change to usingGlobalChat
                    //foreach member: remove [ClanTag] before playerName, above character head.
                    foreach (ClanMember member in myClan.members)
                    {
                        if (playerIsUsingClanChat_byUlongID(member.id.m_SteamID))
                            usingClanChat.Remove(member.id.m_SteamID);

                        PlayerSession memberSession = getSession(member.id.ToString());
                        // remove entry from playerClans
                        if (PlayersDict.ContainsKey(member.id.m_SteamID))
                            PlayersDict.Remove(member.id.m_SteamID);
                        //remove ClanTag before player name above character's head, for each clan member
                        if (memberSession != null)
                            setClanTagAbovePlayerHead(memberSession, null, null, false);
                    }

                    //can disband
                    if (adminCmd)
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_adminDisbandClan", session.SteamId.ToString()).Replace("{playerName}", Color(session.Name, "chatBlue")).Replace("{ClanTag}", Color(myClan.clanTag, myClan.clanTagColor)));
                        clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_adminDisbandClan", session.SteamId.ToString()).Replace("{playerName}", Color(session.Name, "chatBlue")).Replace("{ClanTag}", Color(myClan.clanTag, myClan.clanTagColor)));
                    }
                    else
                        clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_disbandClan", session.SteamId.ToString()).Replace("{playerName}", Color(session.Name, "chatBlue")));
                    ClansDict.Remove(myClan.clanTag);
                    SaveData();
                }
                else
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanOwner", session.SteamId.ToString()));
            }
            else if (!adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
            else if (adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
        }
        // [/clan cmds player|owner|mod|admin
        void cmdClanShowCmds(PlayerSession session, string[] args)
        {
            string[] cmds_PLAYER = {
                Color(GetMsg("clanCmds_PLAYER", session.SteamId.ToString()), "orange"),
                Color("/clan", "orange") + GetMsg("clanCmds_PLAYER_clanInfo", session.SteamId.ToString()),
                Color("/c <Message>", "orange") + GetMsg("clanCmds_PLAYER_message", session.SteamId.ToString()),
                Color("/tc", "orange") + GetMsg("clanCmds_PLAYER_toogleChat", session.SteamId.ToString()),
                Color("/clan create <ClanTag>", "orange") + GetMsg("clanCmds_PLAYER_create", session.SteamId.ToString()),
                Color("/clan join <ClanTag>", "orange") + GetMsg("clanCmds_PLAYER_join", session.SteamId.ToString()),
                Color("/clan leave", "orange") + GetMsg("clanCmds_PLAYER_leave", session.SteamId.ToString()),
                Color("/clan invites", "orange") + GetMsg("clanCmds_PLAYER_showInvites", session.SteamId.ToString())
            };
            string[] cmds_OWNER = {
                Color(GetMsg("clanCmds_OWNER", session.SteamId.ToString()), "orange"),
                Color("/clan tagcolor <TagColor>", "orange") + GetMsg("clanCmds_OWNER_tagcolor", session.SteamId.ToString()),
                Color("/clan promote <Player>", "orange") + GetMsg("clanCmds_OWNER_promote", session.SteamId.ToString()),
                Color("/clan demote <Player>", "orange") + GetMsg("clanCmds_OWNER_demote", session.SteamId.ToString()),
                Color("/clan disband", "orange") + GetMsg("clanCmds_OWNER_disband", session.SteamId.ToString())
            };
            string[] cmds_MOD = {
                Color(GetMsg("clanCmds_MODERATOR", session.SteamId.ToString()), "orange"),
                Color("/clan invite <Player>", "orange") + GetMsg("clanCmds_MODERATOR_invite", session.SteamId.ToString()),
                Color("/clan kick <Player>", "orange") + GetMsg("clanCmds_MODERATOR_kick", session.SteamId.ToString())
            };
            string[] cmds_ADMIN = {
                Color(GetMsg("clanCmds_ADMIN", session.SteamId.ToString()), "orange"),
                Color("/clan list", "orange") + GetMsg("clanCmds_ADMIN_list", session.SteamId.ToString()),
                Color("/clan inspect <ClanTag>", "orange") + GetMsg("clanCmds_ADMIN_inspect", session.SteamId.ToString()),
                Color("/clan admin invite <Player> <ClanTag>", "orange") + "\n" + GetMsg("clanCmds_ADMIN_invite", session.SteamId.ToString()),
                Color("/clan admin kick <Player> <ClanTag>", "orange") + "\n" + GetMsg("clanCmds_ADMIN_kick", session.SteamId.ToString()),
                Color("/clan admin disband <ClanTag>", "orange") + GetMsg("clanCmds_ADMIN_disband", session.SteamId.ToString()),
                Color("/clan admin promote <Player> <ClanTag>", "orange") + "\n" + GetMsg("clanCmds_ADMIN_promote", session.SteamId.ToString()),
                Color("/clan admin demote <Player> <ClanTag>", "orange") + "\n" + GetMsg("clanCmds_ADMIN_demote", session.SteamId.ToString()),
                Color("/clan admin owner <Player> <ClanTag>", "orange") + GetMsg("clanCmds_ADMIN_owner", session.SteamId.ToString()),
                Color("/clan admin tag <ClanTag> <NewTag>", "orange") + GetMsg("clanCmds_ADMIN_clanTag", session.SteamId.ToString())
            };

            string[] output;

            switch (args[1])
            {
                case "player": output = cmds_PLAYER; break;
                case "owner": output = cmds_OWNER; break;
                case "mod": output = cmds_MOD; break;
                case "admin": output = cmds_ADMIN; break;
                default: output = cmds_PLAYER; break;
            }
            foreach (string cmd in output)
                hurt.SendChatMessage(session, cmd);
        }
        // [/clan inspect <ClanTag>]
        void cmdClanInspect(PlayerSession session, string[] args)
        {
            if (!session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }
            string clanTag = args[1];
            Clan myClan = null;
            ClansDict.TryGetValue(clanTag, out myClan);
            if (myClan != null)
            {
                clanInfo(session, myClan);
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
        }
        // [/clan tagcolor <Color>]
        void cmdClanTagColor(PlayerSession session, string[] args)
        {
            //Check if user has clan
            Clan clan = getClan(session);
            if (clan != null)
            {
                //Check if user is clan owner
                if (clan.ownerID == session.SteamId)
                {
                    //Check on the config file if this command is allowed
                    bool allowed = GetConfig(false, "ClanTag", "AllowClanOwnerChangeColor");
                    if (!allowed && !session.IsAdmin)
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagSetColorNotAllowed", session.SteamId.ToString()));
                        return;
                    }

                    string newColor = args[1];
                    clan.clanTagColor = newColor;
                    SaveData();
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagNewColor", session.SteamId.ToString()));

                    //Update clan tag color above all clan members player head
                    foreach (ClanMember member in clan.members)
                    {
                        PlayerSession memberSession = getSession(member.id.ToString());
                        if (memberSession != null)
                        {
                            setClanTagAbovePlayerHead(memberSession, clan.clanTag, clan.clanTagColor, true);
                        }
                    }
                }
                else
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanOwner", session.SteamId.ToString()));
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
        }
        // [/clan create <ClanTag>]
        void cmdClanCreate(PlayerSession session, string[] args)
        {
            //check if player is already on a clan
            if (getClan(session) != null)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_alreadyBelongToClan", session.SteamId.ToString()));
                return;
            }
            //check if clanTag already exists
            if (ClansDict.ContainsKey(args[1]))
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanTagExist", session.SteamId.ToString()));
                return;
            }
            var ownerID = session.SteamId;
            var ownerName = session.Name;
            var clanTag = args[1];
            //Check if clan tag pass in the filter system
            bool result = clanTagFilter(session, clanTag);
            if (!result)
            {
                //Message to player already sent inside clanTagFiler function.
                return;
            }

            //Can create clan
            Clan newClan = new Clan(ownerName, ownerID, clanTag);
            ClansDict[newClan.clanTag] = newClan;
            PlayersDict[session.SteamId.m_SteamID] = newClan.clanTag;
            //Set ClanTagColor to the default color in config
            string tagColor = GetConfig("yellow", "ClanTag", "DefaultColor");
            if (newClan != null)
                newClan.clanTagColor = tagColor;
            SaveData();
            hurt.SendChatMessage(session, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_createClan", session.SteamId.ToString()).Replace("{clanTAG}", Color(clanTag, tagColor)));
            //add ClanTag before player name above character's head
            setClanTagAbovePlayerHead(session, clanTag, tagColor, true);
        }
        // [/clan join <ClanTag>]
        void cmdClanJoin(PlayerSession session, string[] args)
        {
            //check if player already belongs to a clan
            if (getClan(session) != null)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_alreadyBelongToClan", session.SteamId.ToString()));
                return;
            }
            //check if the clan has invited the player
            Clan clan2join = null;
            ClansDict.TryGetValue(args[1], out clan2join);
            if (clan2join == null)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
                return;
            }
            else if (!clan2join.invites.Contains(session.SteamId))
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotInviteYou", session.SteamId.ToString()));
                return;
            }
            //Can join clan!
            //check for other clan invitations and remove them
            foreach (Clan clan in ClansDict.Values)
                foreach (CSteamID invited in clan.invites)
                    if (invited == session.SteamId)
                    {
                        clan.invites.Remove(session.SteamId);
                        break;
                    }
            clan2join.members.Add(new ClanMember(session.Name, session.SteamId, false));
            SaveData();
            //Update PlayersDict
            PlayersDict[session.SteamId.m_SteamID] = clan2join.clanTag;

            clanChatMessage(session, clan2join, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_hasJoinedClan", session.SteamId.ToString()).Replace("{playerName}", Color(session.Name, "chatBlue")));
            //add ClanTag before player name above character's head
            setClanTagAbovePlayerHead(session, clan2join.clanTag, clan2join.clanTagColor, true);
        }
        // [/clan invite <Player>]
        // [/clan admin invite <Player> <ClanTag>]
        void cmdClanInvite(PlayerSession session, string[] args, bool adminCmd)
        {
            if (adminCmd && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }

            PlayerSession invited = null;
            Clan myClan = null;
            if (adminCmd)
            {
                invited = getSession(args[2]);
                ClansDict.TryGetValue(args[3], out myClan);
            }
            else
            {
                invited = getSession(args[1]);
                myClan = getClan(session);
            }

            if (myClan != null)
            {
                if (!adminCmd && !isModeratorOfClan(myClan, session))
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanMod", session.SteamId.ToString()));
                    return;
                }
                if (invited != null)
                {
                    //Check clan member limit
                    int limit = GetConfig(20, "MaxMembers");
                    int current = myClan.members.Count;

                    if (current == limit)
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_memberLimit", session.SteamId.ToString()));
                        return;
                    }

                    if (!myClan.invites.Contains(invited.SteamId))
                    {
                        myClan.invites.Add(invited.SteamId);
                        SaveData();
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_inviteToClan", session.SteamId.ToString()).Replace("{playerName}", Color(invited.Name, "chatBlue")));
                        hurt.SendChatMessage(invited, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_hasBeenInvited", session.SteamId.ToString()).Replace("{clanTAG}", Color(myClan.clanTag, "chatBlue")));
                    }
                    else
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_alreadyInvited", session.SteamId.ToString()).Replace("{playerName}", Color(invited.Name, "chatBlue")));
                }
                else
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_playerNotOnline", session.SteamId.ToString()).Replace("{playerName}", Color(args[1], "chatBlue")));
            }
            else if (!adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
            else if (adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
        }
        // [/clan kick <Player>]
        // [/clan admin kick <Player> <ClanTag>]
        void cmdClanKick(PlayerSession session, string[] args, bool adminCmd)
        {
            if (adminCmd && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }

            string player2kick;
            Clan myClan = null;

            if (adminCmd)
            {
                player2kick = args[2];
                ClansDict.TryGetValue(args[3], out myClan);
            }
            else
            {
                player2kick = args[1];
                myClan = getClan(session);
            }

            if (myClan != null)
            {
                if (!adminCmd && !isModeratorOfClan(myClan, session))
                {
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanMod", session.SteamId.ToString()));
                    return;
                }
                int counter = 0;
                ClanMember member2kick = null;
                foreach (ClanMember member in myClan.members)
                    if (member.name.ToLower().Contains(player2kick.ToLower()))
                    {
                        counter++;
                        member2kick = member;
                    }
                if (counter == 0)
                {
                    //No player on clan with given name
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanMember", session.SteamId.ToString()).Replace("{playerName}", Color(player2kick, "chatBlue")));
                    return;
                }
                else if (counter > 1)
                {
                    //Two or more players with given name

                    //TODO fix?
                }
                else if (counter == 1)
                {
                    //Check if player kicking itself
                    if (member2kick.name.Equals(session.Name))
                    {
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_canNotKickSelf", session.SteamId.ToString()));
                        return;
                    }
                    //Player found. Can be removed
                    myClan.members.Remove(member2kick);
                    SaveData();

                    PlayerSession player2kickSession = getSession(member2kick.name);
                    // remove entry from playerClans
                    if (PlayersDict.ContainsKey(member2kick.id.m_SteamID))
                        PlayersDict.Remove(member2kick.id.m_SteamID);
                    //if usingClanChat, change to usingGlobalChat
                    if (IsValidSession(player2kickSession))
                    {
                        if (playerIsUsingClanChat_byUlongID(player2kickSession.SteamId.m_SteamID))
                            usingClanChat.Remove(player2kickSession.SteamId.m_SteamID);
                        hurt.SendChatMessage(player2kickSession, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_kickFromClan", session.SteamId.ToString()));
                        //remove ClanTag before player name above character's head
                        setClanTagAbovePlayerHead(player2kickSession, null, null, false);
                    }
                    clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_hasBeenKicked", session.SteamId.ToString()).Replace("{playerName}", Color(member2kick.name, "chatBlue")));
                }
            }
            else if (!adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
            else if (adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
        }
        // [/clan promote|demote <Player>]
        // [/clan admin promote|demote <Player> <ClanTag>]
        void cmdClanPromoteDemote(PlayerSession session, string[] args, bool adminCmd)
        {
            if (adminCmd && !session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }

            Clan myClan = null;
            string player2PromoteOrDemote;
            string operation = null;
            if (adminCmd)
                operation = args[1];
            else
                operation = args[0];

            if (adminCmd)
            {
                player2PromoteOrDemote = args[2];
                ClansDict.TryGetValue(args[3], out myClan);
            }
            else
            {
                player2PromoteOrDemote = args[1];
                myClan = getClan(session);
            }

            if (myClan != null)
            {
                if (adminCmd || myClan.ownerID == session.SteamId)
                {
                    //is owner, can promote or demote member
                    int counter = 0;
                    ClanMember member2PromoteOrDemote = null;
                    foreach (ClanMember member in myClan.members)
                        if (member.name.Equals(player2PromoteOrDemote))
                        {
                            counter++;
                            member2PromoteOrDemote = member;
                        }
                    if (counter == 0)
                    {
                        //No player on clan with given name
                        hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanMember", session.SteamId.ToString()).Replace("{playerName}", Color(player2PromoteOrDemote, "chatBlue")));
                        return;
                    }
                    else if (counter > 1)
                    {
                        //Two or more players with given name

                        //TODO Fix?
                    }
                    else if (counter == 1)
                    {
                        //Check if member2PromoteOrDemote is owner itself. Fail in case.
                        if (member2PromoteOrDemote.id == myClan.ownerID)
                        {
                            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_ownerCanNotPromoteDemoteSelf", session.SteamId.ToString()));
                            return;
                        }
                        //Player found. Can be promoted or demoted
                        if (!member2PromoteOrDemote.isModerator)
                        {
                            if (operation == "promote")
                            {
                                //promote sucess
                                member2PromoteOrDemote.isModerator = true;
                                SaveData();
                                clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_hasBeenPromoted", session.SteamId.ToString()).Replace("{playerName}", Color(member2PromoteOrDemote.name, "chatBlue")));
                            }
                            else
                            {
                                //demote fail
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_canNotBeDemoted", session.SteamId.ToString()).Replace("{playerName}", Color(member2PromoteOrDemote.name, "chatBlue")));
                                return;
                            }
                        }
                        else
                        {
                            if (operation == "demote")
                            {
                                //demote sucess
                                member2PromoteOrDemote.isModerator = false;
                                SaveData();
                                clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_hasBeenDemoted", session.SteamId.ToString()).Replace("{playerName}", Color(member2PromoteOrDemote.name, "chatBlue")));
                            }
                            else
                            {
                                //promote fail
                                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_canNotBePromoted", session.SteamId.ToString()).Replace("{playerName}", Color(member2PromoteOrDemote.name, "chatBlue")));
                                return;
                            }
                        }
                    }
                }
                else    //is not owner, can not promote member
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_onlyOwnerCanChangeRanks", session.SteamId.ToString()));
            }
            else if (!adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));
            else if (adminCmd)
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
        }
        // [/clan admin owner <Player> <ClanTag>
        void cmdClanOwner(PlayerSession session, string[] args)
        {
            if (!session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }

            Clan myClan = null;
            string ownerName = args[2];
            ClansDict.TryGetValue(args[3], out myClan);

            if (myClan != null)
            {
                PlayerSession newOwner = getSession(ownerName);
                if (newOwner != null && newOwner.IsLoaded && newOwner.SteamId != null && newOwner.Name != null)
                {
                    foreach (ClanMember member in myClan.members)
                    {
                        if (member.id == newOwner.SteamId)
                        {
                            myClan.ownerID = newOwner.SteamId;
                            clanChatMessage(session, myClan, Color(GetMsg("msg_CLAN", session.SteamId.ToString()), "orange") + GetMsg("msg_newOwner", session.SteamId.ToString()).Replace("{playerName}", newOwner.Name));
                            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_newOwner", session.SteamId.ToString()).Replace("{playerName}", newOwner.Name));
                            return;
                        }
                        else
                            hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notClanMember", session.SteamId.ToString()).Replace("{playerName}", Color(args[2], "chatBlue")));
                    }
                }
                else
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_playerNotOnline", session.SteamId.ToString()).Replace("{playerName}", Color(args[2], "chatBlue")));
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
        }
        // [/clan admin tag <ClanTag> <NewTag>]
        void cmdClanTag(PlayerSession session, string[] args)
        {
            if (!session.IsAdmin)
            {
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_notAdmin", session.SteamId.ToString()));
                return;
            }

            Clan myClan = null;
            ClansDict.TryGetValue(args[2], out myClan);

            if (myClan != null)
            {
                myClan.clanTag = args[3];
                //Update clan tag color above all clan members player head
                foreach (ClanMember member in myClan.members)
                {
                    PlayerSession memberSession = getSession(member.id.ToString());
                    if (memberSession != null && memberSession.IsLoaded)
                    {
                        setClanTagAbovePlayerHead(memberSession, myClan.clanTag, myClan.clanTagColor, true);
                    }
                }

                clanChatMessage(session, myClan, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_newClanTag", session.SteamId.ToString()).Replace("{newTag}", args[3]));
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_newClanTag", session.SteamId.ToString()).Replace("{newTag}", args[3]));
                return;
            }
            else
                hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_clanNotExist", session.SteamId.ToString()));
        }
        #endregion Chat Command Help Methods
        #endregion Chat Commands

        #region Hooks
        object OnPlayerChat(PlayerSession session, string message)
        {
            Clan myClan = getClan(session);

            //Check if using clan chat.
            if (isChattingOnClan_bySession(session))
            {
                if (myClan != null)
                {
                    string clanMessage = createClanChatMessage(session.Name, message);

                    clanChatMessage(session, myClan, clanMessage);
                    Puts("[" + myClan.clanTag + "] " + RemoveTags(clanMessage));
                }
                else
                    hurt.SendChatMessage(session, Color(GetMsg("msg_INFO", session.SteamId.ToString()), "orange") + GetMsg("msg_doNotHaveClan", session.SteamId.ToString()));

                return false;
            }

            //Here means is using global chat.

            //For BetterChat Plugin Compatibility
            //The output format is made on BetterChat
            if (plugins.Exists("BetterChat"))
                return null;
            
            string globalMessage;
            if (myClan != null)
                globalMessage = createGlobalChatMessage(session.Name, message, myClan);
            else
                globalMessage = createGlobalChatMessage(session.Name, message, null);
            
            hurt.BroadcastChat(globalMessage);
            Puts(RemoveTags(globalMessage));

            return false;
        }

        void OnPlayerInit(PlayerSession player)
        {
            if (player != null && player.IsLoaded)
            {
                Clan clan = getClan(player);
                timer.Repeat(2f, 3, () =>
                {
                    if (player != null && clan != null)
                        setClanTagAbovePlayerHead(player, clan.clanTag, clan.clanTagColor, true);
                });

                //Add entry to playerClans Dictionary.
                if (clan != null && !PlayersDict.ContainsKey(player.SteamId.m_SteamID))
                    PlayersDict.Add(player.SteamId.m_SteamID, clan.clanTag);
            }
        }
        
        void OnPlayerDisconnected(PlayerSession player)
        {
            //Removes player SteamId in the usingClanChat list
            if (usingClanChat.Contains(player.SteamId.m_SteamID))
                usingClanChat.Remove(player.SteamId.m_SteamID);
            
            /*
            //Remove entry from playerClans Dictionary
            PlayersDict.Remove(player);
            */
        }
        #endregion Hooks
        
        #region API
        string getClanTag(PlayerSession session)
        {
            return getClanTag_bySession(session);
        }
        string getClanTag_bySession(PlayerSession session)
        {
            if (!IsValidSession(session))
                return null;

            Clan myClan = getClan(session);
            return getClanTag_byClan(myClan);
        }
        string getClanTag_byUlongID(ulong playerid)
        {
            Clan myClan = findClan(playerid);
            return getClanTag_byClan(myClan);
        }
        string getClanTag_byClan(Clan clan)
        {
            if (clan != null)
                return "[" + clan.clanTag + "]";
            else
                return null;
        }
        string getClanTagColor(PlayerSession session)
        {
            return getClanTagColor_bySession(session);
        }
        string getClanTagColor_bySession(PlayerSession session)
        {
            if (!IsValidSession(session))
                return null;
            
            Clan clan = getClan(session);
            return getClanTagColor_byClan(clan);
        }
        string getClanTagColor_byUlongID(ulong playerid)
        {
            Clan clan = findClan(playerid);
            return getClanTagColor_byClan(clan);
        }
        string getClanTagColor_byClan(Clan clan)
        {
            if (clan != null)
                return clan.clanTagColor;
            return GetConfig("yellow", "ClanTag", "DefaultColor");
        }
        List<PlayerSession> getMembersOnline_bySession(PlayerSession session)
        {
            Clan myClan = getClan(session);
            return getMembersOnline_byClan(myClan);
        }
        List<PlayerSession> getMembersOnline_byUlongID(ulong playerid)
        {
            Clan myClan = findClan(playerid);
            return getMembersOnline_byClan(myClan);
        }
        List<PlayerSession> getMembersOnline_byClan(Clan clan)
        {
            List<PlayerSession> membersOnline = new List<PlayerSession>();
            if (clan != null)
            {
                foreach (ClanMember cm in clan.members)
                {
                    PlayerSession player = getSessionBySteamId(cm.id.ToString());
                    if (IsValidSession(player))
                        membersOnline.Add(player);
                }
            }
            return membersOnline;
        }
        bool isChattingOnClan(PlayerSession session)
        {
            return isChattingOnClan_bySession(session);
        }
        bool isChattingOnClan_bySession(PlayerSession session)
        {
            return isChattingOnClan_byUlongID(session.SteamId.m_SteamID);
        }
        bool isChattingOnClan_byUlongID(ulong playerID)
        {
            return usingClanChat.Contains(playerID);
        }
        ulong getClanId(PlayerSession session)
        {
            return getClanId_bySession(session);
        }
        ulong getClanId_bySession(PlayerSession session)
        {
            Clan clan = getClan(session);
            return getClanId_byClan(clan);

        }
        ulong getClanId_byUlongID(ulong playerid)
        {
            Clan clan = findClan(playerid);
            return getClanId_byClan(clan);
        }
        ulong getClanId_byClan(Clan clan)
        {
            if (clan != null)
                return (ulong)clan.ownerID;

            return 0;
        }
        List<ulong> getClanMembersID(ulong playerID)
        {
            Clan myClan = findClan(playerID);
            List<ulong> members = new List<ulong>();
            if (myClan != null)
            {
                foreach (ClanMember cm in myClan.members)
                {
                    members.Add(cm.id.m_SteamID);
                }
            }
            return members;
        }
        #endregion
    }
}