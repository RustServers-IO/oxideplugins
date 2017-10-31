/*
 * Feature List:
 * Community ban kicking -- done
 * VAC ban kicking -- done
 * Recent ban kicking -- done
 * Game ban kicking -- done
 * Trade ban kicking -- done
 * Private profile kicking -- done
 * Limited account kicking -- done
 * Profile not setup kicking -- done
 * Not enough hours kicking -- done
 * Shared game kicking -- done
 * Not enough games kicking -- done
 * Checking ownerID's account for all of the above -- not yet
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("SteamChecks", "Spicy", "3.0.1", ResourceId = 2113)]
    [Description("Check Steam servers to grab information about players")]
    internal class SteamChecks : CovalencePlugin
    {
        #region Globals

        private uint appId;
        private Regex regex;
        private const string RootUrl = "http://api.steampowered.com/";
        private const string XmlUrl = "http://steamcommunity.com/profiles/{0}/?xml=1";
        private const string BansStr = "ISteamUser/GetPlayerBans/v1/?key={0}&steamids={1}";
        private const string PlayerStr = "ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}";
        private const string SharingStr = "IPlayerService/IsPlayingSharedGame/v0001/?key={0}&steamid={1}&appid_playing={2}";
        private const string GamesStr = "IPlayerService/GetOwnedGames/v0001/?key={0}&steamid={1}";

        #endregion

        #region Cache

        private bool isSharing;
        private bool isPrivate;

        #endregion

        #region Config

        private string apiKey;

        private bool kickCommunityBan;
        private bool kickVacBan;
        private bool kickGameBan;
        private bool kickTradeBan;
        private bool kickRecentBan;
        private bool kickPrivateProfile;
        private bool kickLimitedAccount;
        private bool kickHoursPlayed;
        private bool kickFamilyShare;
        private bool kickFamilyOwner;
        private bool kickNoProfile;
        private bool kickGameCount;

        private int thresholdVacBan;
        private int thresholdGameBan;
        private int thresholdRecentBan;
        private int thresholdHoursPlayed;
        private int thresholdGameCount;

        private bool broadcastCommunityBan;
        private bool broadcastVacBan;
        private bool broadcastGameBan;
        private bool broadcastTradeBan;
        private bool broadcastRecentBan;
        private bool broadcastPrivateProfile;
        private bool broadcastLimitedAccount;
        private bool broadcastHoursPlayed;
        private bool broadcastFamilyShare;
        private bool broadcastFamilyOwner;
        private bool broadcastNoProfile;
        private bool broadcastGameCount;

        private List<string> whitelist;

        protected override void LoadDefaultConfig()
        {
            Config["ApiKey"] = "";
            Config["Kicking"] = new Dictionary<string, bool>
            {
                ["CommunityBan"] = true,
                ["VacBan"] = true,
                ["GameBan"] = true,
                ["TradeBan"] = true,
                ["RecentBan"] = true,
                ["PrivateProfile"] = true,
                ["LimitedAccount"] = true,
                ["HoursPlayed"] = true,
                ["FamilyShare"] = true,
                ["FamilyOwner"] = true,
                ["NoProfile"] = true,
                ["GameCount"] = true
            };
            Config["Thresholds"] = new Dictionary<string, int>
            {
                ["VacBan"] = 2,
                ["GameBan"] = 2,
                ["RecentBan"] = 365,
                ["HoursPlayed"] = 25,
                ["GameCount"] = 1
            };
            Config["Broadcasting"] = new Dictionary<string, bool>
            {
                ["CommunityBan"] = true,
                ["VacBan"] = true,
                ["GameBan"] = true,
                ["TradeBan"] = true,
                ["RecentBan"] = true,
                ["PrivateProfile"] = true,
                ["LimitedAccount"] = true,
                ["HoursPlayed"] = true,
                ["FamilyShare"] = true,
                ["FamilyOwner"] = true,
                ["NoProfile"] = true,
                ["GameCount"] = true
            };
            Config["Whitelist"] = new List<string>
            {
                "76561198103592543"
            };
        }

        private void InitialiseConfig()
        {
            apiKey = Config.Get<string>("ApiKey");

            kickCommunityBan = Config.Get<bool>("Kicking", "CommunityBan");
            kickVacBan = Config.Get<bool>("Kicking", "VacBan");
            kickGameBan = Config.Get<bool>("Kicking", "GameBan");
            kickTradeBan = Config.Get<bool>("Kicking", "TradeBan");
            kickRecentBan = Config.Get<bool>("Kicking", "RecentBan");
            kickPrivateProfile = Config.Get<bool>("Kicking", "PrivateProfile");
            kickLimitedAccount = Config.Get<bool>("Kicking", "LimitedAccount");
            kickHoursPlayed = Config.Get<bool>("Kicking", "HoursPlayed");
            kickFamilyShare = Config.Get<bool>("Kicking", "FamilyShare");
            kickFamilyOwner = Config.Get<bool>("Kicking", "FamilyOwner");
            kickNoProfile = Config.Get<bool>("Kicking", "NoProfile");
            kickGameCount = Config.Get<bool>("Kicking", "GameCount");

            thresholdVacBan = Config.Get<int>("Thresholds", "VacBan");
            thresholdGameBan = Config.Get<int>("Thresholds", "GameBan");
            thresholdRecentBan = Config.Get<int>("Thresholds", "RecentBan");
            thresholdHoursPlayed = Config.Get<int>("Thresholds", "HoursPlayed");
            thresholdGameCount = Config.Get<int>("Thresholds", "GameCount");

            broadcastCommunityBan = Config.Get<bool>("Broadcasting", "CommunityBan");
            broadcastVacBan = Config.Get<bool>("Broadcasting", "VacBan");
            broadcastGameBan = Config.Get<bool>("Broadcasting", "GameBan");
            broadcastTradeBan = Config.Get<bool>("Broadcasting", "TradeBan");
            broadcastRecentBan = Config.Get<bool>("Broadcasting", "RecentBan");
            broadcastPrivateProfile = Config.Get<bool>("Broadcasting", "PrivateProfile");
            broadcastLimitedAccount = Config.Get<bool>("Broadcasting", "LimitedAccount");
            broadcastHoursPlayed = Config.Get<bool>("Broadcasting", "HoursPlayed");
            broadcastFamilyShare = Config.Get<bool>("Broadcasting", "FamilyShare");
            broadcastFamilyOwner = Config.Get<bool>("Broadcasting", "FamilyOwner");
            broadcastNoProfile = Config.Get<bool>("Broadcasting", "NoProfile");
            broadcastGameCount = Config.Get<bool>("Broadcasting", "GameCount");

            whitelist = Config.Get<List<string>>("Whitelist");
        }

        #endregion

        #region Lang

        private void InitialiseLang()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Broadcast"] = "Kicking {0}... ({1})",
                ["Console"] = "Kicking {0}... ({1})",

                ["ErrorUnknown"] = "Unknown error occurred while connecting to the Steam API. Error code: {0}.",
                ["ErrorInvalidKey"] = "The Steam API key provided is invalid.",
                ["ErrorServiceUnavailable"] = "The Steam API is currently unavailable",
                ["ErrorPrivateProfile"] = "This player has a private profile, therefore SteamChecks cannot check their games/hours.",
                ["ErrorFamilyShare"] = "This player is family sharing, therefore SteamChecks cannot check their games/hours.",

                ["KickCommunityBan"] = "You have a Steam Community ban on record.",
                ["KickVacBan"] = "You have too many VAC bans on record.",
                ["KickGameBan"] = "You have too many Game bans on record.",
                ["KickTradeBan"] = "You have a Steam Trade ban on record.",
                ["KickRecentBan"] = "You have a recent VAC ban on record.",
                ["KickPrivateProfile"] = "Your Steam profile state is set to private.",
                ["KickLimitedAccount"] = "Your Steam account is limited.",
                ["KickHoursPlayed"] = "You haven't played enough hours.",
                ["KickFamilyShare"] = "You are Family Sharing.",
                ["KickFamilyOwner"] = "The account from which you are borrowing fails our checks.",
                ["KickNoProfile"] = "Your Steam profile hasn't been set up.",
                ["KickGameCount"] = "You don't have enough Steam games.",

                ["SteamChecksBypassed"] = "This player is whitelisted, therefore they bypass SteamChecks."
            }, this);
        }

        #endregion

        #region Enum

        private enum ResponseCode
        {
            Valid = 200,
            InvalidKey = 403,
            Unavailable = 503,
        }

        #endregion

        #region Deserialisation

        private class Bans
        {
            [JsonProperty("players")]
            public Player[] Players;

            public class Player
            {
                [JsonProperty("CommunityBanned")]
                public bool CommunityBanned;
                [JsonProperty("VACBanned")]
                public bool VacBanned;
                [JsonProperty("NumberOfVACBans")]
                public int NumberOfVacBans;
                [JsonProperty("DaysSinceLastBan")]
                public int DaysSinceLastBan;
                [JsonProperty("NumberOfGameBans")]
                public int NumberOfGameBans;
                [JsonProperty("EconomyBan")]
                public string EconomyBan;
            }
        }

        private class Summaries
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("players")]
                public Player[] Players;

                public class Player
                {
                    [JsonProperty("steamid")]
                    public string SteamId;
                    [JsonProperty("communityvisibilitystate")]
                    public int CommunityVisibilityState;
                    [JsonProperty("profilestate")]
                    public int ProfileState;
                    [JsonProperty("personaname")]
                    public string PersonaName;
                    [JsonProperty("lastlogoff")]
                    public double LastLogOff;
                    [JsonProperty("commentpermission")]
                    public int CommentPermission;
                    [JsonProperty("profileurl")]
                    public string ProfileUrl;
                    [JsonProperty("avatar")]
                    public string Avatar;
                    [JsonProperty("avatarmedium")]
                    public string AvatarMedium;
                    [JsonProperty("avatarfull")]
                    public string AvatarFull;
                    [JsonProperty("personastate")]
                    public int PersonaState;
                    [JsonProperty("realname")]
                    public string RealName;
                    [JsonProperty("primaryclanid")]
                    public string PrimaryClanId;
                    [JsonProperty("timecreated")]
                    public double TimeCreated;
                    [JsonProperty("personastateflags")]
                    public int PersonaStateFlags;
                    [JsonProperty("loccountrycode")]
                    public string LocCountryCode;
                    [JsonProperty("locstatecode")]
                    public string LocStateCode;
                }
            }
        }

        private class Sharing
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("lender_steamid")]
                public ulong LenderSteamId;
            }
        }

        private class Games
        {
            [JsonProperty("response")]
            public Content Response;

            public class Content
            {
                [JsonProperty("game_count")]
                public int GameCount;
                [JsonProperty("games")]
                public Game[] Games;

                public class Game
                {
                    [JsonProperty("appid")]
                    public uint AppId;
                    [JsonProperty("playtime_2weeks")]
                    public int PlaytimeTwoWeeks;
                    [JsonProperty("playtime_forever")]
                    public int PlaytimeForever;
                }
            }
        }

        #endregion

        #region Helpers

        #region General

        private bool IsValidRequest(ResponseCode code)
        {
            switch (code)
            {
                case ResponseCode.Valid:
                    return true;
                case ResponseCode.InvalidKey:
                    Puts(Lang("ErrorInvalidKey"));
                    return false;
                case ResponseCode.Unavailable:
                    Puts(Lang("ErrorServiceUnavailable"));
                    return false;
                default:
                    Puts(string.Format(Lang("ErrorUnknown"), code.ToString()));
                    return false;
            }
        }

        private void Kick(IPlayer player, string key, bool shouldBroadcast)
        {
            player.Kick(Lang(key, player.Id));
            Puts(Lang("Console"), player.Name.Sanitize(), Lang(key));

            if (shouldBroadcast)
                foreach (var _player in players.Connected)
                    _player.Message(Lang("Broadcast", _player.Id), player.Name.Sanitize(), Lang(key, _player.Id));
        }

        private string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

        private T Deserialise<T>(string json) => JsonConvert.DeserializeObject<T>(json);

        private bool NoKey() => string.IsNullOrEmpty(apiKey);

        #endregion

        #region Bans

        private bool IsCommunityBanned(Bans b) => b.Players[0].CommunityBanned;
        private bool IsVacBanned(Bans b) => b.Players[0].VacBanned;
        private bool IsTradeBanned(Bans b) => b.Players[0].EconomyBan == "banned";
        private int VacBanCount(Bans b) => b.Players[0].NumberOfVacBans;
        private int GameBanCount(Bans b) => b.Players[0].NumberOfGameBans;
        private int DaysSinceBan(Bans b) => b.Players[0].DaysSinceLastBan;

        #endregion

        #region Summaries

        private string GetSteamId(Summaries s) => s.Response.Players[0].SteamId;
        private int GetCommunityVisibilityState(Summaries s) => s.Response.Players[0].CommunityVisibilityState;
        private int GetProfileState(Summaries s) => s.Response.Players[0].ProfileState;
        private string GetPersonaName(Summaries s) => s.Response.Players[0].PersonaName;
        private DateTime GetLastLogOff(Summaries s) => new DateTime(1970, 1, 1).AddSeconds(s.Response.Players[0].LastLogOff);
        private int GetCommentPermission(Summaries s) => s.Response.Players[0].CommentPermission;
        private string GetProfileUrl(Summaries s) => s.Response.Players[0].ProfileUrl;
        private string GetAvatar(Summaries s) => s.Response.Players[0].Avatar;
        private string GetAvatarMedium(Summaries s) => s.Response.Players[0].AvatarMedium;
        private string GetAvatarFull(Summaries s) => s.Response.Players[0].AvatarFull;
        private int GetPersonaState(Summaries s) => s.Response.Players[0].PersonaState;
        private string GetRealName(Summaries s) => s.Response.Players[0].RealName;
        private string GetPrimaryClanId(Summaries s) => s.Response.Players[0].PrimaryClanId;
        private DateTime GetTimeCreated(Summaries s) => new DateTime(1970, 1, 1).AddSeconds(s.Response.Players[0].TimeCreated);
        private int GetPersonaStateFlags(Summaries s) => s.Response.Players[0].PersonaStateFlags;
        private string GetLocCountryCode(Summaries s) => s.Response.Players[0].LocCountryCode;
        private string GetLocStateCode(Summaries s) => s.Response.Players[0].LocStateCode;

        #endregion

        #region Sharing

        private ulong GetLenderSteamId(Sharing s) => s.Response.LenderSteamId;
        private bool IsSharing(Sharing s) => GetLenderSteamId(s) > 0;

        #endregion

        #region Games

        private int GetGameCount(Games g) => g.Response.GameCount;
        private int GetPlaytimeTwoWeeks(Games g) => g.Response.Games.FirstOrDefault(x => x.AppId == appId).PlaytimeTwoWeeks;
        private int GetPlaytimeForever(Games g) => g.Response.Games.FirstOrDefault(x => x.AppId == appId).PlaytimeForever;

        #endregion

        #endregion

        #region Init

        private void Init()
        {
            appId = covalence.ClientAppId;
            regex = new Regex("<isLimitedAccount>(.*)</isLimitedAccount>");
            InitialiseConfig();
            InitialiseLang();

            if (NoKey())
                Puts(Lang("ErrorInvalidKey"));
        }

        #endregion

        #region Connection

        private void OnUserConnected(IPlayer player)
        {
            if (whitelist.Contains(player.Id))
            {
                Puts(Lang("SteamChecksBypassed"));
                return;
            }

            if (NoKey())
            {
                Puts(Lang("ErrorInvalidKey"));
                return;
            }

            StartChecking(player);
        }

        #endregion

        #region Checking

        private void StartChecking(IPlayer player) => CheckBans(player);

        private void CheckBans(IPlayer player)
        {
            webrequest.Enqueue(string.Format(RootUrl + BansStr, apiKey, player.Id), null, (code, response) =>
            {
                if (!IsValidRequest((ResponseCode)code))
                    return;

                var bans = Deserialise<Bans>(response);

                if (IsCommunityBanned(bans) && kickCommunityBan)
                {
                    Kick(player, "KickCommunityBan", broadcastCommunityBan);
                    return;
                }

                if (VacBanCount(bans) > thresholdVacBan && kickVacBan)
                {
                    Kick(player, "KickVacBan", broadcastVacBan);
                    return;
                }

                if (GameBanCount(bans) > thresholdGameBan && kickGameBan)
                {
                    Kick(player, "KickGameBan", broadcastGameBan);
                    return;
                }

                if (IsTradeBanned(bans) && kickTradeBan)
                {
                    Kick(player, "KickTradeBan", broadcastTradeBan);
                    return;
                }

                if (IsVacBanned(bans) && DaysSinceBan(bans) < thresholdRecentBan)
                {
                    Kick(player, "KickRecentBan", broadcastRecentBan);
                    return;
                }

                CheckSummaries(player);
            }, this);
        }

        private void CheckSummaries(IPlayer player)
        {
            webrequest.Enqueue(string.Format(RootUrl + PlayerStr, apiKey, player.Id), null, (code, response) =>
            {
                if (!IsValidRequest((ResponseCode)code))
                    return;

                var summaries = Deserialise<Summaries>(response);
                isPrivate = GetCommunityVisibilityState(summaries) < 3;

                if (isPrivate && kickPrivateProfile)
                {
                    Kick(player, "KickPrivateProfile", broadcastPrivateProfile);
                    return;
                }

                CheckXml(player);
            }, this);
        }

        private void CheckXml(IPlayer player)
        {
            webrequest.Enqueue(string.Format(XmlUrl, player.Id), null, (code, response) =>
            {
                if (!IsValidRequest((ResponseCode)code))
                    return;

                var status = response.ToLower().Contains("this user has not yet set up their steam community profile");

                if (status && kickNoProfile)
                {
                    Kick(player, "KickNoProfile", broadcastNoProfile);
                    return;
                }

                if (!status && regex.Match(response).Groups[1].ToString() == "1" && kickLimitedAccount)
                {
                    Kick(player, "KickLimitedAccount", broadcastLimitedAccount);
                    return;
                }

                CheckSharing(player);
            }, this);
        }

        private void CheckSharing(IPlayer player)
        {
            webrequest.Enqueue(string.Format(RootUrl + SharingStr, apiKey, player.Id, appId), null, (code, response) =>
            {
                if (!IsValidRequest((ResponseCode)code))
                    return;

                var sharing = Deserialise<Sharing>(response);
                isSharing = IsSharing(sharing);

                if (isSharing && kickFamilyShare)
                {
                    Kick(player, "KickFamilyShare", broadcastFamilyShare);
                    return;
                }

                CheckGames(player);
            }, this);
        }

        private void CheckGames(IPlayer player)
        {
            if (isSharing)
            {
                Puts(Lang("ErrorFamilyShare"));
                return;
            }

            if (isPrivate)
            {
                Puts(Lang("ErrorPrivateProfile"));
                return;
            }

            webrequest.Enqueue(string.Format(RootUrl + GamesStr, apiKey, player.Id), null, (code, response) =>
            {
                if (!IsValidRequest((ResponseCode)code))
                    return;

                var games = Deserialise<Games>(response);

                if (GetGameCount(games) < thresholdGameCount && kickGameCount)
                {
                    Kick(player, "KickGameCount", broadcastGameCount);
                    return;
                }

                if (GetPlaytimeForever(games) / 60 < thresholdHoursPlayed && kickHoursPlayed)
                {
                    Kick(player, "KickHoursPlayed", broadcastHoursPlayed);
                    return;
                }
            }, this);
        }

        #endregion
    }
}