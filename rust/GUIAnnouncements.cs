using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;

using System.Collections;

using UnityEngine;

using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("GUIAnnouncements", "JoeSheep", "1.22.69", ResourceId = 1222)]
    [Description("Creates announcements with custom messages by command across the top of every player's screen in a banner.")]

    public class GUIAnnouncements : RustPlugin
    {
        #region Configuration

        #region Permissions
        const string PermAnnounce = "GUIAnnouncements.announce";
        const string PermAnnounceToggle = "GUIAnnouncements.toggle";
        const string PermAnnounceJoinLeave = "GUIAnnouncements.AnnounceJoinLeave";
        #endregion
        #region Global Declerations
        private Dictionary<ulong, string> Exclusions = new Dictionary<ulong, string>();
        private List<ulong> JustJoined = new List<ulong>();
        private List<ulong> GlobalTimerList = new List<ulong>();
        private Dictionary<BasePlayer, Timer> PrivateTimers = new Dictionary<BasePlayer, Timer>();
        private Dictionary<BasePlayer, Timer> NewPlayerPrivateTimers = new Dictionary<BasePlayer, Timer>();
        private Dictionary<BasePlayer, Timer> PlayerRespawnedTimers = new Dictionary<BasePlayer, Timer>();
        private Timer PlayerTimer;
        private Timer GlobalTimer;
        private Timer NewPlayerTimer;
        private Timer PlayerRespawnedTimer;
        private Timer RealTimeTimer;
        private Timer SixtySecondsTimer;
        private Timer AutomaticAnnouncementsTimer;
        private Timer GetNextRestartTimer;
        private string LastHitPlayer = String.Empty;
        private List<uint> HeliNetIDs = new List<uint>();
        private bool ConfigUpdated;
        private List<DateTime> RestartTimes = new List<DateTime>();
        private Dictionary<DateTime, TimeSpan> CalcNextRestartDict = new Dictionary<DateTime, TimeSpan>();
        private DateTime NextRestart;
        private int LastHour;
        private int LastMinute;
        private bool RestartCountdown;
        private IEnumerator<string> ATALEnum;
        private bool RestartJustScheduled = false;
        private bool RestartScheduled = false;
        private string RestartReason = String.Empty;
        private List<string> RestartAnnouncementsWhenStrings;
        private DateTime ScheduledRestart;
        private TimeSpan AutomaticTimedAnnouncementsRepeat;
        private bool RestartSuspended = false;
        private bool DontCheckNextRestart = false;
        private bool MuteBans = false;

        string BannerTintGrey = "0.1 0.1 0.1 0.7";
        string BannerTintRed = "0.5 0.1 0.1 0.7";
        string BannerTintOrange = "0.95294 0.37255 0.06275 0.7";
        string BannerTintYellow = "1 0.92 0.016 0.7";
        string BannerTintGreen = "0.1 0.4 0.1 0.5";
        string BannerTintCyan = "0 1 1 0.7";
        string BannerTintBlue = "0.09020 0.07843 0.71765 0.7";
        string BannerTintPurple = "0.53333 0.07843 0.77647 0.7";
        string TextRed = "0.5 0.2 0.2";
        string TextOrange = "0.8 0.5 0.1";
        string TextYellow = "1 0.92 0.016";
        string TextGreen = "0 1 0";
        string TextCyan = "0 1 1";
        string TextBlue = "0.09020 0.07843 0.71765";
        string TextPurple = "0.53333 0.07843 0.77647";
        string TextWhite = "1 1 1";
        string BannerAnchorMaxX = "1.026 ";
        string BannerAnchorMaxY = "0.9743";
        string BannerAnchorMinX = "-0.027 ";
        string BannerAnchorMinY = "0.915";
        string TextAnchorMaxX = "0.868 ";
        string TextAnchorMaxY = "0.9743";
        string TextAnchorMinX = "0.131 ";
        string TextAnchorMinY = "0.915";

        #endregion
        //============================================================================================================
        #region Config Option Declerations

        //Color List
        public string bannerColorList { get; private set; } = "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
        public string textColorList { get; private set; } = "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple";

        //Airdrop Announcements
        public bool airdropAnnouncement { get; private set; } = true;
        public bool airdropAnnouncementLocation { get; private set; } = true;
        public string airdropAnnouncementText { get; private set; } = "Airdrop en route!";
        public string airdropAnnouncementTextWithCoords { get; private set; } = "Airdrop en route to x{x}, z{z}";
        public string airdropAnnouncementBannerColor { get; private set; } = "Green";
        public string airdropAnnouncementTextColor { get; private set; } = "Yellow";

        //Automatic Timed Announcements
        public bool automaticTimedAnnouncements { get; private set; } = false;
        public static List<object> automaticTimedAnnouncementsList { get; private set; } = new List<object>
        {
            "Automatic Timed Announcement 1",
            "Automatic Timed Announcement 2",
            "Automatic Timed Announcement 3"
        };
        public string automaticTimedAnnouncementsRepeat { get; private set; } = "00:30:00";
        public string automaticTimedAnnouncementsBannerColor { get; private set; } = "Grey";
        public string automaticTimedAnnouncementsTextColor { get; private set; } = "White";

        //Christmas Stocking Refill Announcement
        public bool stockingRefillAnnouncement { get; private set; } = false;
        public string stockingRefillAnnouncementText { get; private set; } = "Santa has refilled your stockings. Go check what you've got!";
        public string stockingRefillAnnouncementBannerColor { get; private set; } = "Green";
        public string stockingRefillAnnouncementTextColor { get; private set; } = "Red";

        //General Settings
        public float announcementDuration { get; private set; } = 10f;
        public int fontSize { get; private set; } = 18;
        public float fadeOutTime { get; private set; } = 0.5f;
        public float fadeInTime { get; private set; } = 0.5f;
        public static float adjustVPosition { get; private set; } = 0.0f;

        //Global Join/Leave Announcements
        public bool globalLeaveAnnouncements { get; private set; } = false;
        public bool globalJoinAnnouncements { get; private set; } = false;
        public bool globalJoinLeavePermissionOnly { get; private set; } = true;
        public string globalLeaveText { get; private set; } = "{rank} {playername} has left.";
        public string globalJoinText { get; private set; } = "{rank} {playername} has joined.";
        public string globalLeaveAnnouncementBannerColor { get; private set; } = "Grey";
        public string globalLeaveAnnouncementTextColor { get; private set; } = "White";
        public string globalJoinAnnouncementBannerColor { get; private set; } = "Grey";
        public string globalJoinAnnouncementTextColor { get; private set; } = "White";


        //Helicopter Announcements
        public bool helicopterSpawnAnnouncement { get; private set; } = true;
        public bool helicopterDespawnAnnouncement { get; private set; } = false;
        public bool helicopterDestroyedAnnouncement { get; private set; } = true;
        public bool helicopterDestroyedAnnouncementWithDestroyer { get; private set; } = true;
        public string helicopterSpawnAnnouncementText { get; private set; } = "Patrol Helicopter Inbound!";
        public string helicopterDespawnAnnouncementText { get; private set; } = "The patrol helicopter has left.";
        public string helicopterDestroyedAnnouncementText { get; private set; } = "The patrol helicopter has been taken down!";
        public string helicopterDestroyedAnnouncementWithDestroyerText { get; private set; } = "{playername} got the last shot on the helicopter taking it down!";
        public string helicopterSpawnAnnouncementBannerColor { get; private set; } = "Red";
        public string helicopterSpawnAnnouncementTextColor { get; private set; } = "Orange";
        public string helicopterDestroyedAnnouncementBannerColor { get; private set; } = "Red";
        public string helicopterDestroyedAnnouncementTextColor { get; private set; } = "White";
        public string helicopterDespawnAnnouncementBannerColor { get; private set; } = "Red";
        public string helicopterDespawnAnnouncementTextColor { get; private set; } = "White";

        //New Player Announcements
        public bool newPlayerAnnouncements { get; private set; } = true;
        public string newPlayerAnnouncementsBannerColor { get; private set; } = "Grey";
        public string newPlayerAnnouncementsTextColor { get; private set; } = "White";
        public Dictionary<int, List<object>> newPlayerAnnouncementsList { get; private set; } = new Dictionary<int, List<object>>
        {
            {1, new List<object>{ "1st Join {rank} {playername} New player announcement 1.", "1st Join {rank} {playername} New player announcement 2.", "1st Join {rank} {playername} New player announcement 3." } },
            {2, new List<object>{ "2nd Join {rank} {playername} New player announcement 1.", "2nd Join {rank} {playername} New player announcement 2.", "2nd Join {rank} {playername} New player announcement 3." } },
            {3, new List<object>{ "3rd Join {rank} {playername} New player announcement 1.", "3rd Join {rank} {playername} New player announcement 2.", "3rd Join {rank} {playername} New player announcement 3." } },
        };

        //Player Banned Announcement
        public bool playerBannedAnnouncement { get; private set; } = false;
        public string playerBannedAnnouncmentText { get; private set; } = "{playername} has been banned. {reason}.";
        public string playerBannedAnnouncementBannerColor { get; private set; } = "Grey";
        public string playerBannedAnnouncementTextColor { get; private set; } = "Red";

        //Respawn Announcements
        public bool respawnAnnouncements { get; private set; } = false;
        public string respawnAnnouncementsBannerColor { get; private set; } = "Grey";
        public string respawnAnnouncementsTextColor { get; private set; } = "White";
        public List<object> respawnAnnouncementsList { get; private set; } = new List<object>
        {
                    "{playername} Respawn announcement 1.",
                    "{playername} Respawn announcement 2.",
                    "{playername} Respawn announcement 3."
        };

        //Restart Announcements
        public bool restartAnnouncements { get; private set; } = false;
        public string restartAnnouncementsFormat { get; private set; } = "Restarting in {time}";
        public string restartAnnouncementsBannerColor { get; private set; } = "Grey";
        public string restartAnnouncementsTextColor { get; private set; } = "White";
        public List<object> restartTimes { get; private set; } = new List<object>
        {
            "08:00:00",
            "20:00:00"
        };
        public List<object> restartAnnouncementsWhen { get; private set; } = new List<object>
        {
            "12:00:00",
            "11:00:00",
            "10:00:00",
            "09:00:00",
            "08:00:00",
            "07:00:00",
            "06:00:00",
            "05:00:00",
            "04:00:00",
            "03:00:00",
            "02:00:00",
            "01:00:00",
            "00:45:00",
            "00:30:00",
            "00:15:00",
            "00:05:00"
        };
        public bool restartServer { get; private set; } = false;

        //Third Party Plugin Support
        public bool doNotOverlayLustyMap { get; private set; } = false;
        public string lustyMapPosition { get; private set; } = "Left";

        //Welcome Announcement
        public string welcomeAnnouncementText { get; private set; } = "Welcome {playername}";
        public string welcomeBackAnnouncementText { get; private set; } = "Welcome back {playername}";
        public string welcomeAnnouncementBannerColor { get; private set; } = "Grey";
        public string welcomeAnnouncementTextColor { get; private set; } = "White";
        public float welcomeAnnouncementDuration { get; private set; } = 20f;
        public bool welcomeAnnouncement { get; private set; } = true;
        public bool welcomeBackAnnouncement { get; private set; } = true;
        #endregion

        //============================================================================================================
        #region LoadConfig
        private void LoadGUIAnnouncementsConfig()
        {
            bannerColorList = GetConfig("A List Of Available Colors To Use (DO NOT CHANGE)", "Banner Colors", bannerColorList);
            if (bannerColorList != "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple")
            {
                PrintWarning("Banner color list changed. Reverting changes.");
                Config["A List Of Available Colors To Use(DO NOT CHANGE)", "Banner Colors"] = "Grey, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
                ConfigUpdated = true;
            }
            textColorList = GetConfig("A List Of Available Colors To Use (DO NOT CHANGE)", "Text Colors", textColorList);
            if (textColorList != "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple")
            {
                PrintWarning("Text color list changed. Reverting changes.");
                Config["A List Of Available Colors To Use(DO NOT CHANGE)", "Text Colors"] = "White, Red, Orange, Yellow, Green, Cyan, Blue, Purple";
                ConfigUpdated = true;
            }

            //Airdrop Announcements
            airdropAnnouncementText = GetConfig("Public Airdrop Announcements", "Text", airdropAnnouncementText);
            airdropAnnouncementTextWithCoords = GetConfig("Public Airdrop Announcements", "Text With Coords", airdropAnnouncementTextWithCoords);
            airdropAnnouncementBannerColor = GetConfig("Public Airdrop Announcements", "Banner Color", airdropAnnouncementBannerColor);
            airdropAnnouncementTextColor = GetConfig("Public Airdrop Announcements", "Text Color", airdropAnnouncementTextColor);
            airdropAnnouncement = GetConfig("Public Airdrop Announcements", "Enabled", true);
            airdropAnnouncementLocation = GetConfig("Public Airdrop Announcements", "Show Location", false);

            //Automatic Timed Announcements
            automaticTimedAnnouncements = GetConfig("Public Automatic Timed Announcements", "Enabled", false);
            automaticTimedAnnouncementsList = GetConfig("Public Automatic Timed Announcements", "Announcement List", automaticTimedAnnouncementsList);
            automaticTimedAnnouncementsBannerColor = GetConfig("Public Automatic Timed Announcements", "Banner Color", automaticTimedAnnouncementsBannerColor);
            automaticTimedAnnouncementsTextColor = GetConfig("Public Automatic Timed Announcements", "Text Color", automaticTimedAnnouncementsTextColor);
            automaticTimedAnnouncementsRepeat = GetConfig("Public Automatic Timed Announcements", "Show Every (HH:MM:SS)", automaticTimedAnnouncementsRepeat);
            if (!TimeSpan.TryParse(automaticTimedAnnouncementsRepeat, out AutomaticTimedAnnouncementsRepeat))
            {
                PrintWarning("Config: \"Automatic Timed Announcements - Show Every (HH:MM:SS)\" is not of the correct format HH:MM:SS, or has numbers out of range and should not be higher than 23:59:59. Resetting to default.");
                Config["Automatic Timed Announcements", "Show Every (HH:MM:SS)"] = "00:30:00";
                ConfigUpdated = true;
            }

            //Christmas Stocking Refill Announcement
            stockingRefillAnnouncement = GetConfig("Public Christmas Stocking Refill Announcement", "Enabled", false);
            stockingRefillAnnouncementText = GetConfig("Public Christmas Stocking Refill Announcement", "Text", stockingRefillAnnouncementText);
            stockingRefillAnnouncementBannerColor = GetConfig("Public Christmas Stocking Refill Announcement", "Banner Color", stockingRefillAnnouncementBannerColor);
            stockingRefillAnnouncementTextColor = GetConfig("Public Christmas Stocking Refill Announcement", "Text Color", stockingRefillAnnouncementTextColor);

            //General Settings:
            announcementDuration = GetConfig("General Settings", "Announcement Duration", 10f);
            if (announcementDuration == 0)
            {
                PrintWarning("Config: \"General Settings - Announcement Duration\" set to 0, resetting to 10f.");
                Config["Formatting", "AnnouncementShowDuration"] = 10f;
                ConfigUpdated = true;
            }
            fontSize = GetConfig("General Settings", "Font Size", 18);
            if (fontSize > 33 | fontSize == 0)
            {
                PrintWarning("Config: \"General Settings - Font Size\" greater than 28 or 0, resetting to 18.");
                Config["General Settings", "Font Size"] = 18;
                ConfigUpdated = true;
            }
            fadeInTime = GetConfig("General Settings", "Fade In Time", 0.5f);
            if (fadeInTime > announcementDuration / 2)
            {
                PrintWarning("Config: \"General Settings - Fade In Time\" is greater than half of AnnouncementShowDuration, resetting to half of AnnouncementShowDuration.");
                Config["General Settings", "Fade In Time"] = announcementDuration / 2;
                ConfigUpdated = true;
            }
            fadeOutTime = GetConfig("General Settings", "Fade Out Time", 0.5f);
            if (fadeOutTime > announcementDuration / 2)
            {
                PrintWarning("Config: \"General Settings - Fade Out Time\" is greater than half of AnnouncementShowDuration, resetting to half of AnnouncementShowDuration.");
                Config["General Settings", "Fade Out Time"] = announcementDuration / 2;
                ConfigUpdated = true;
            }
            adjustVPosition = GetConfig("General Settings", "Adjust Vertical Position", 0.0f);
            if (adjustVPosition != 0f)
            {
                BannerAnchorMaxY = (float.Parse("0.9743") + adjustVPosition).ToString();
                BannerAnchorMinY = (float.Parse("0.915") + adjustVPosition).ToString();
                TextAnchorMaxY = (float.Parse("0.9743") + adjustVPosition).ToString();
                TextAnchorMinY = (float.Parse("0.915") + adjustVPosition).ToString();
            }

            //Global Join/Leave Announcements
            globalLeaveAnnouncements = GetConfig("Public Join/Leave Announcements", "Leave Enabled", false);
            globalLeaveText = GetConfig("Public Join/Leave Announcements", "Leave Text", globalLeaveText);
            globalLeaveAnnouncementBannerColor = GetConfig("Public Join/Leave Announcements", "Leave Banner Color", globalLeaveAnnouncementBannerColor);
            globalLeaveAnnouncementTextColor = GetConfig("Public Join/Leave Announcements", "Leave Text Color", globalLeaveAnnouncementTextColor);
            globalJoinAnnouncements = GetConfig("Public Join/Leave Announcements", "Join Enabled", false);
            globalJoinText = GetConfig("Public Join/Leave Announcements", "Join Text", globalJoinText);
            globalJoinAnnouncementBannerColor = GetConfig("Public Join/Leave Announcements", "Join Banner Color", globalJoinAnnouncementBannerColor);
            globalJoinAnnouncementTextColor = GetConfig("Public Join/Leave Announcements", "Join Text Color", globalJoinAnnouncementTextColor);
            globalJoinLeavePermissionOnly = GetConfig("Public Join/Leave Announcements", "Announce Only Players With Permission", globalJoinLeavePermissionOnly);

            //Helicopter Announcements
            helicopterSpawnAnnouncement = GetConfig("Public Helicopter Announcements", "Spawn", true);
            helicopterSpawnAnnouncementText = GetConfig("Public Helicopter Announcements", "Spawn Text", helicopterSpawnAnnouncementText);
            helicopterSpawnAnnouncementBannerColor = GetConfig("Public Helicopter Announcements", "Spawn Banner Color", helicopterSpawnAnnouncementBannerColor);
            helicopterSpawnAnnouncementTextColor = GetConfig("Public Helicopter Announcements", "Spawn Text Color", helicopterSpawnAnnouncementTextColor);
            helicopterDespawnAnnouncement = GetConfig("Public Helicopter Announcements", "Despawn", helicopterDespawnAnnouncement);
            helicopterDespawnAnnouncementText = GetConfig("Public Helicopter Announcements", "Despawn Text", helicopterDespawnAnnouncementText);
            helicopterDespawnAnnouncementBannerColor = GetConfig("Public Helicopter Announcements", "Despawn Banner Color", helicopterDespawnAnnouncementBannerColor);
            helicopterDespawnAnnouncementTextColor = GetConfig("Public Helicopter Announcements", "Despawn Text Color", helicopterDespawnAnnouncementTextColor);
            helicopterDestroyedAnnouncement = GetConfig("Public Helicopter Announcements", "Destroyed", true);
            helicopterDestroyedAnnouncementWithDestroyer = GetConfig("Public Helicopter Announcements", "Show Destroyer", true);
            helicopterDestroyedAnnouncementText = GetConfig("Public Helicopter Announcements", "Destroyed Text", helicopterDestroyedAnnouncementText);
            helicopterDestroyedAnnouncementWithDestroyerText = GetConfig("Public Helicopter Announcements", "Destroyed Text With Destroyer", helicopterDestroyedAnnouncementWithDestroyerText);
            helicopterDestroyedAnnouncementBannerColor = GetConfig("Public Helicopter Announcements", "Destroyed Banner Color", helicopterDestroyedAnnouncementBannerColor);
            helicopterDestroyedAnnouncementTextColor = GetConfig("Public Helicopter Announcements", "Destroyed Text Color", helicopterDestroyedAnnouncementTextColor);

            //New Player Announcements
            newPlayerAnnouncements = GetConfig("Private New Player Announcements", "Enabled", false);
            newPlayerAnnouncementsBannerColor = GetConfig("Private New Player Announcements", "Banner Color", newPlayerAnnouncementsBannerColor);
            newPlayerAnnouncementsTextColor = GetConfig("Private New Player Announcements", "Text Color", newPlayerAnnouncementsTextColor);
            newPlayerAnnouncementsList = GetConfig("Private New Player Announcements", "Announcements List (Show On This Many Joins : List To Show)", newPlayerAnnouncementsList);

            //Player Banned Announcement
            playerBannedAnnouncement = GetConfig("Public Player Banned Announcement", "Enabled", false);
            playerBannedAnnouncmentText = GetConfig("Public Player Banned Announcement", "Text", playerBannedAnnouncmentText);
            playerBannedAnnouncementBannerColor = GetConfig("Public Player Banned Announcement", "Banner Color", playerBannedAnnouncementBannerColor);
            playerBannedAnnouncementTextColor = GetConfig("Public Player Banned Announcement", "Text Color", playerBannedAnnouncementTextColor);

            //Respawn Announcements
            respawnAnnouncements = GetConfig("Private Respawn Announcements", "Enabled", false);
            respawnAnnouncementsBannerColor = GetConfig("Private Respawn Announcements", "Banner Color", respawnAnnouncementsBannerColor);
            respawnAnnouncementsTextColor = GetConfig("Private Respawn Announcements", "Text Color", respawnAnnouncementsTextColor);
            respawnAnnouncementsList = GetConfig("Private Respawn Announcements", "Announcements List", respawnAnnouncementsList);

            //Restart Announcements
            restartAnnouncements = GetConfig("Public Restart Announcements", "Enabled", restartAnnouncements);
            restartTimes = GetConfig("Public Restart Announcements", "Restart At (HH:MM:SS)", restartTimes);
            restartAnnouncementsBannerColor = GetConfig("Public Restart Announcements", "Banner Color", restartAnnouncementsBannerColor);
            restartAnnouncementsTextColor = GetConfig("Public Restart Announcements", "Text Color", restartAnnouncementsTextColor);
            restartAnnouncementsWhen = GetConfig("Public Restart Announcements", "Announce With Time Left (HH:MM:SS)", restartAnnouncementsWhen);
            restartServer = GetConfig("Public Restart Announcements", "Restart My Server", false);
            restartAnnouncementsFormat = GetConfig("Public Restart Announcements", "Text", restartAnnouncementsFormat);

            //Third Party Plugin Support
            doNotOverlayLustyMap = GetConfig("Third Party Plugin Support", "Do Not Overlay LustyMap", false);
            lustyMapPosition = GetConfig("Third Party Plugin Support", "LustyMap Position (Left/Right)", "Left");
            if (lustyMapPosition.ToLower() != "left" && lustyMapPosition.ToLower() != "right" || lustyMapPosition == string.Empty || lustyMapPosition == null)
            {
                PrintWarning("Config LustyMapPosition is not left or right, resetting to left.");
                Config["Third Party Plugin Support", "LustyMapPosition"] = "Left";
                ConfigUpdated = true;
            }
            if (doNotOverlayLustyMap == true)
            {
                if (lustyMapPosition.ToLower() == "left")
                    BannerAnchorMinX = "0.131 ";
                if (lustyMapPosition.ToLower() == "right")
                    BannerAnchorMaxX = "0.868 ";
            }

            //Welcome Announcements
            welcomeAnnouncement = GetConfig("Private Welcome Announcements", "Enabled", true);
            welcomeBackAnnouncement = GetConfig("Private Welcome Announcements", "Show Welcome Back If Player Has Been Here Before", true);
            welcomeAnnouncementText = GetConfig("Private Welcome Announcements", "Welcome Text", welcomeAnnouncementText);
            welcomeBackAnnouncementText = GetConfig("Private Welcome Announcements", "Welcome Back Text", welcomeBackAnnouncementText);
            welcomeAnnouncementBannerColor = GetConfig("Private Welcome Announcements", "Banner Color", welcomeAnnouncementBannerColor);
            welcomeAnnouncementTextColor = GetConfig("Private Welcome Announcements", "Text Color", welcomeAnnouncementTextColor);
            welcomeAnnouncementDuration = GetConfig("Private Welcome Announcements", "Duration", 20f);
            if (welcomeAnnouncementDuration == 0)
            {
                PrintWarning("Config: \"Welcome Announcement - Duration\" set to 0, resetting to 20f.");
                Config["Formatting", "WelcomeAnnouncementDuration"] = 20f;
                ConfigUpdated = true;
            }

            if (!ConfigUpdated) return;
            Puts("Configuration file has been updated.");
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("A new configuration file has been created.");

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                ConfigUpdated = true;
            }
            if (data.TryGetValue(setting, out value))
            {
                if (setting == "Announcements List (Show On This Many Joins : List To Show)")
                {
                    var keyType = typeof(T).GetGenericArguments()[0];
                    var valueType = typeof(T).GetGenericArguments()[1];
                    var dict = (IDictionary)Activator.CreateInstance(typeof(T));
                    foreach (var key in ((IDictionary)value).Keys)
                    {
                        dict.Add(Convert.ChangeType(key, keyType), Convert.ChangeType(((IDictionary)value)[key], valueType));
                    }
                    return (T)dict;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            value = defaultValue;
            data[setting] = value;
            ConfigUpdated = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private List<string> ConvertObjectListToString(object value)
        {
            if (value is List<object>)
            {
                List<object> list = (List<object>)value;
                List<string> strings = list.Select(s => (string)s).ToList();
                return strings;
            }
            else return (List<string>)value;
        }
        #endregion
        #endregion
        //============================================================================================================
        #region PlayerData

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject("GUIAnnouncementsPlayerData", storedData);

        void LoadSavedData()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("GUIAnnouncementsPlayerData");
            if (storedData == null)
            {
                PrintWarning("GUIAnnouncement's datafile is null. Recreating data file...");
                storedData = new StoredData();
                SaveData();
                timer.Once(5, () =>
                {
                    PrintWarning("Reloading...");
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, "reload GUIAnnouncements");
                });
            }
        }

        class StoredData
        {
            public Dictionary<ulong, PlayerData> PlayerData = new Dictionary<ulong, PlayerData>();
            public StoredData()
            {
            }
        }

        class PlayerData
        {
            public string Name;
            public string UserID;
            public int TimesJoined;
            public bool Dead;
            public PlayerData()
            {
            }
        }
        
        void CreatePlayerData(BasePlayer player)
        {
            var Data = new PlayerData();
            Data.Name = player.displayName;
            Data.UserID = player.userID.ToString();
            Data.TimesJoined = 0;
            storedData.PlayerData.Add(player.userID, Data);
            SaveData();
        }

        StoredData storedData;
        void OnServerSave() => SaveData();

        private Dictionary<ulong, AnnouncementInfo> AnnouncementsData = new Dictionary<ulong, AnnouncementInfo>();

        class AnnouncementInfo
        {
            public string BannerTintColor;
            public string TextColor;
            public string AnnouncementType;
            public AnnouncementInfo()
            {
            }
        }

        void StoreAnnouncementData(BasePlayer player, string BannerTintColor, string TextColor, string AnnouncementType = null)
        {
            var Data = new AnnouncementInfo();
            Data.BannerTintColor = BannerTintColor;
            Data.TextColor = TextColor;
            Data.AnnouncementType = AnnouncementType;
            AnnouncementsData.Add(player.userID, Data);
        }

        #endregion
        //============================================================================================================
        #region Localization

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"ChatCommandAnnounce", "announce"},
                    {"ChatCommandAnnounceTo", "announceto"},
                    {"ChatCommandAnnounceToGroup", "announcetogroup"},
                    {"ChatCommandAnnounceTest", "announcetest"},
                    {"ChatCommandDestroyAnnouncement", "destroyannouncement"},
                    {"ChatCommandMuteBans", "announcemutebans" },
                    {"ChatCommandAnnouncementsToggle", "announcementstoggle" },
                    {"ChatCommandScheduleRestart", "announceschedulerestart" },
                    {"ChatCommandSuspendRestart", "announcesuspendrestart" },
                    {"ChatCommandResumeRestart", "announceresumerestart" },
                    {"ChatCommandGetNextRestart", "announcegetnextrestart" },
                    {"ChatCommandCancelScheduledRestart", "announcecancelscheduledrestart" },
                    {"ChatCommandCancelRestart", "announcecancelrestart" },
                    {"ChatCommandHelp", "announcehelp"},
                    {"ConsoleCommandAnnounce", "announce.announce"},
                    {"ConsoleCommandAnnounceTo", "announce.announceto"},
                    {"ConsoleCommandAnnounceToGroup", "announce.announcetogroup" },
                    {"ConsoleCommandDestroyAnnouncement", "announce.destroy"},
                    {"ConsoleCommandMuteBans", "announce.mutebans" },
                    {"ConsoleCommandAnnouncementsToggle", "announce.toggle"},
                    {"ConsoleCommandScheduleRestart", "announce.schedulerestart" },
                    {"ConsoleCommandSuspendRestart", "announce.suspendrestart" },
                    {"ConsoleCommandResumeRestart", "announce.resumerestart" },
                    {"ConsoleCommandGetNextRestart", "announce.getnextrestart" },
                    {"ConsoleCommandCancelScheduledRestart", "announce.cancelscheduledrestart" },
                    {"ConsoleCommandCancelRestart", "announce.cancelrestart" },
                    {"ConsoleCommandHelp", "announce.help"},
                    {"PlayerNotFound", "Player {playername} not found, check the name and if they are online."},
                    {"GroupNotFound", "Group {group} not found, check the name." },
                    {"NoPermission", "You do not possess the required permissions."},
                    {"ChatCommandAnnounceUsage", "Usage: /announce <message>."},
                    {"ChatCommandAnnounceToUsage", "Usage: /announceto <player> <message>."},
                    {"ChatCommandAnnounceToGroupUsage", "Usage: /announcetogroup <group> <message>"},
                    {"ChatCommandAnnouncementsToggleUsage", "Usage: /announcementstoggle [player]."},
                    {"ChatCommandScheduleRestartUsage", "Usage: /announceschedulerestart <hh:mm:ss>." },
                    {"ChatCommandCancelScheduledRestartUsage", "Usage: /announcecancelscheduledrestart" },
                    {"ConsoleCommandAnnounceUsage", "Usage: announce.announce <message>."},
                    {"ConsoleCommandAnnounceToUsage", "Usage: announce.announceto <player> <message>."},
                    {"ConsoleCommandAnnounceToGroupUsage", "Usage: announce.announcetogroup <group> <message>." },
                    {"ConsoleCommandAnnouncementsToggleUsage", "Usage: announce.toggle <player>."},
                    {"ConsoleCommandScheduleRestartUsage", "Usage: announce.schedulerestart <hh:mm:ss>." },
                    {"ConsoleCommandCancelScheduledRestartUsage", "Usage: announce.cancelscheduledrestart." },
                    {"RestartAlreadyScheduled", "A restart has already been scheduled for {time}, please cancel that restart first with /announcecancelscheduledrestart or announce.cancelscheduledrestart." },
                    {"LaterThanNextRestart", "Restart not scheduled. Your time will be scheduled later than the next restart at {time}, please make sure you schedule a restart before {time}." },
                    {"RestartNotScheduled", "A restart has not been scheduled for you to cancel." },
                    {"ScheduledRestartCancelled", "A manually scheduled restart for {time} has been cancelled." },
                    {"Excluded", "{playername} has been excluded from announcements."},
                    {"ExcludedTo", "You have been excluded from announcements."},
                    {"Included", "{playername} is being included in announcements."},
                    {"IncludedTo", "You are being included in announcements."},
                    {"IsExcluded", "{playername} is currently excluded from announcements."},
                    {"YouAreExcluded", "You are excluded from announcements and cannot see that test announcement"},
                    {"BansMuted", "Ban announcements have been muted." },
                    {"BansUnmuted", "Ban announcements have been unmuted." },
                    {"PlayerHelp", "Chat commands: /announcementstoggle"},
                    {"AnnounceHelp", "Chat commands: /announce <message>, /announceto <player> <message>, /announcetogroup <group> <message>, /announcementstoggle [player], /destroyannouncement, /announceschedulerestart <time> [reason], /announcecancelscheduledrestart, /announcesuspendrestart, /announceresumerestart, /announcecancelrestart | Console commands: announce.announce <message>, announce.announceto <player> <message>, announce.announcetogroup <group> <message>, announce.toggle <player>, announce.destroy, announce.schedulerestart <time> [reason], announce.cancelscheduledrestart, announce.suspendrestart, announce.resumerestart, announce.cancelrestart."},
                    {"GetNextRestart", "Next restart is in {time1} at {time2}" },
                    {"RestartSuspendedChat", "The next restart at {time} has been suspended. Type /announceresumerestart to resume that restart." },
                    {"RestartSuspendedConsole", "The next restart at {time} has been suspended. Type announce.resumerestart to resume that restart." },
                    {"RestartResumed", "The previously suspended restart at {time} has been resumed." },
                    {"SuspendedRestartPassed", "The previously suspended restart at {time} has passed." },
                    {"NoRestartCountdown", "No 60 second restart countdown is currently active." },
                    {"Hours", "hours" },
                    {"Hour", "hour" },
                    {"Minutes", "minutes" },
                    {"Seconds", "seconds" },
                    {"Second", "second" },
            }, this);
        }

        #endregion
        //============================================================================================================
        #region Initialization

        void OnServerInitialized()
        {
            #if !RUST
            throw new NotSupportedException("This plugin does not support this game.");
            #endif

            LoadGUIAnnouncementsConfig();
            LoadSavedData();
            LoadDefaultMessages();
            permission.RegisterPermission(PermAnnounce, this);
            permission.RegisterPermission(PermAnnounceToggle, this);
            permission.RegisterPermission(PermAnnounceJoinLeave, this);
            
            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (!storedData.PlayerData.ContainsKey(activePlayer.userID))
                {
                    CreatePlayerData(activePlayer);
                    storedData.PlayerData[activePlayer.userID].TimesJoined = storedData.PlayerData[activePlayer.userID].TimesJoined + 1;
                    SaveData();
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (!storedData.PlayerData.ContainsKey(sleepingPlayer.userID))
                {
                    CreatePlayerData(sleepingPlayer);
                    storedData.PlayerData[sleepingPlayer.userID].TimesJoined = storedData.PlayerData[sleepingPlayer.userID].TimesJoined + 1;
                    SaveData();
                }
            }
            
            if (automaticTimedAnnouncements)
            {
                List<string> automaticTimedAnnouncementsList = ConvertObjectListToString(Config.Get("Public Automatic Timed Announcements", "Announcement List"));
                ATALEnum = automaticTimedAnnouncementsList.GetEnumerator();
                AutomaticAnnouncementsTimer = timer.Repeat((float)AutomaticTimedAnnouncementsRepeat.TotalSeconds, 0, () =>
                {
                    AutomaticTimedAnnouncements();
                });
            }

            if (restartAnnouncements)
				RestartAnnouncementsStart();

            cmd.AddChatCommand(Lang("ChatCommandAnnounce"), this, "cmdAnnounce");
            cmd.AddChatCommand(Lang("ChatCommandAnnounceTo"), this, "cmdAnnounceTo");
            cmd.AddChatCommand(Lang("ChatCommandAnnounceToGroup"), this, "cmdAnnounceToGroup");
            cmd.AddChatCommand(Lang("ChatCommandAnnounceTest"), this, "cmdAnnounceTest");
            cmd.AddChatCommand(Lang("ChatCommandDestroyAnnouncement"), this, "cmdDestroyAnnouncement");
            cmd.AddChatCommand(Lang("ChatCommandMuteBans"), this, "cmdMuteBans");
            cmd.AddChatCommand(Lang("ChatCommandAnnouncementsToggle"), this, "cmdAnnouncementsToggle");
            cmd.AddChatCommand(Lang("ChatCommandScheduleRestart"), this, "cmdScheduleRestart");
            cmd.AddChatCommand(Lang("ChatCommandSuspendRestart"), this, "cmdSuspendRestart");
            cmd.AddChatCommand(Lang("ChatCommandResumeRestart"), this, "cmdResumeRestart");
            cmd.AddChatCommand(Lang("ChatCommandGetNextRestart"), this, "cmdGetNextRestart");
            cmd.AddChatCommand(Lang("ChatCommandCancelScheduledRestart"), this, "cmdCancelScheduledRestart");
            cmd.AddChatCommand(Lang("ChatCommandCancelRestart"), this, "cmdCancelRestart");
            cmd.AddChatCommand(Lang("ChatCommandHelp"), this, "cmdAnnounceHelp");
            cmd.AddConsoleCommand(Lang("ConsoleCommandAnnounce"), this, "ccmdAnnounce");
            cmd.AddConsoleCommand(Lang("ConsoleCommandAnnounceTo"), this, "ccmdAnnounceTo");
            cmd.AddConsoleCommand(Lang("ConsoleCommandAnnounceToGroup"), this, "ccmdAnnounceToGroup");
            cmd.AddConsoleCommand(Lang("ConsoleCommandDestroyAnnouncement"), this, "ccmdAnnounceDestroy");
            cmd.AddConsoleCommand(Lang("ConsoleCommandMuteBans"), this, "ccmdMuteBans");
            cmd.AddConsoleCommand(Lang("ConsoleCommandAnnouncementsToggle"), this, "ccmdAnnouncementsToggle");
            cmd.AddConsoleCommand(Lang("ConsoleCommandScheduleRestart"), this, "ccmdScheduleRestart");
            cmd.AddConsoleCommand(Lang("ConsoleCommandSuspendRestart"), this, "ccmdSuspendRestart");
            cmd.AddConsoleCommand(Lang("ConsoleCommandResumeRestart"), this, "ccmdResumeRestart");
            cmd.AddConsoleCommand(Lang("ConsoleCommandGetNextRestart"), this, "ccmdGetNextRestart");
            cmd.AddConsoleCommand(Lang("ConsoleCommandCancelScheduledRestart"), this, "ccmdCancelScheduledRestart");
            cmd.AddConsoleCommand(Lang("ConsoleCommandCancelRestart"), this, "ccmdCancelRestart");
            cmd.AddConsoleCommand(Lang("ConsoleCommandHelp"), this, "ccmdAnnounceHelp");
        }
        #endregion
        //============================================================================================================
        #region GUI

        public void CreateAnnouncement(string Msg, string bannerTintColor, string textColor, BasePlayer player = null, bool isWelcomeAnnouncement = false, bool isRestartAnnouncement = false, string group = null)
        {
            var GUIBANNER = new CuiElementContainer();
            GUIBANNER.Add(new CuiElement
            {
                Name = "AnnouncementBanner",
                Components =
                        {
                            new CuiImageComponent {Color = ConvertBannerColor(bannerTintColor), FadeIn = fadeInTime},
                            new CuiRectTransformComponent {AnchorMin = BannerAnchorMinX + BannerAnchorMinY, AnchorMax = BannerAnchorMaxX + BannerAnchorMaxY}
                        },
                FadeOut = fadeOutTime
            });

            var GUITEXT = new CuiElementContainer();
            GUITEXT.Add(new CuiElement
            {
                Name = "AnnouncementText",
                Components =
                        {
                             new CuiTextComponent {Text = Msg, FontSize = fontSize, Align = TextAnchor.MiddleCenter, FadeIn = fadeInTime, Color = ConvertTextColor(textColor)},
                             new CuiRectTransformComponent {AnchorMin = TextAnchorMinX + TextAnchorMinY, AnchorMax = TextAnchorMaxX + TextAnchorMaxY}
                        },
                FadeOut = fadeOutTime
            });
            if (player == null)
            {
                var e = BasePlayer.activePlayerList.GetEnumerator();
                for (var i = 0; e.MoveNext(); i++)
                {
                    if (!Exclusions.ContainsKey(e.Current.userID))
                    {
                        if (group == null)
                        {
                            destroyAllTimers(e.Current);
                            GlobalTimerList.Add(e.Current.userID);
                            if (AnnouncementsData.ContainsKey(e.Current.userID))
                            {
                                if (AnnouncementsData[e.Current.userID].BannerTintColor != bannerTintColor)
                                {
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                    CuiHelper.AddUi(e.Current, GUIBANNER);
                                }
                                CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                CuiHelper.AddUi(e.Current, GUITEXT);
                                AnnouncementsData.Remove(e.Current.userID);
                            }
                            else
                            {
                                CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                CuiHelper.AddUi(e.Current, GUIBANNER);
                                CuiHelper.AddUi(e.Current, GUITEXT);
                            }
                            StoreAnnouncementData(e.Current, bannerTintColor, textColor);
                        }
                        else if (group != null)
                        {
                            if (permission.GetUserGroups(e.Current.UserIDString).Any(group.ToLower().Contains))
                            {
                                destroyAllTimers(e.Current);
                                GlobalTimerList.Add(e.Current.userID);
                                if (AnnouncementsData.ContainsKey(e.Current.userID))
                                {
                                    if (AnnouncementsData[e.Current.userID].BannerTintColor != bannerTintColor)
                                    {
                                        CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                        CuiHelper.AddUi(e.Current, GUIBANNER);
                                    }
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                    CuiHelper.AddUi(e.Current, GUITEXT);
                                    AnnouncementsData.Remove(e.Current.userID);
                                }
                                else
                                {
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementBanner");
                                    CuiHelper.DestroyUi(e.Current, "AnnouncementText");
                                    CuiHelper.AddUi(e.Current, GUIBANNER);
                                    CuiHelper.AddUi(e.Current, GUITEXT);
                                }
                                StoreAnnouncementData(e.Current, bannerTintColor, textColor);
                            }
                        }
                    }
                    else if (isRestartAnnouncement)
                    {
                        SendReply(e.Current, Msg, e.Current.userID);
                    }
                }
                GlobalTimer = timer.Once(announcementDuration, () => destroyGlobalGUI());
                return;
            }
            if (player != null)
            {
                destroyPrivateTimer(player);
                if (AnnouncementsData.ContainsKey(player.userID))
                {
                    if (AnnouncementsData[player.userID].BannerTintColor != bannerTintColor)
                    {
                        CuiHelper.DestroyUi(player, "AnnouncementBanner");
                        CuiHelper.AddUi(player, GUIBANNER);
                    }
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                    CuiHelper.AddUi(player, GUITEXT);
                    AnnouncementsData.Remove(player.userID);
                }
                else
                {
                    CuiHelper.DestroyUi(player, "AnnouncementBanner");
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                    CuiHelper.AddUi(player, GUIBANNER);
                    CuiHelper.AddUi(player, GUITEXT);
                }
                if (JustJoined.Contains(player.userID) && welcomeAnnouncement && isWelcomeAnnouncement)
                {
                    JustJoined.Remove(player.userID);
                    PrivateTimers[player] = timer.Once(welcomeAnnouncementDuration, () => destroyPrivateGUI(player));
                }
                else
                {
                    PrivateTimers[player] = timer.Once(announcementDuration, () => destroyPrivateGUI(player));
                }
                StoreAnnouncementData(player, bannerTintColor, textColor);
            }
        }

        #endregion
        //============================================================================================================
        #region Functions

        void OnPlayerInit(BasePlayer player)
        {
            if (welcomeAnnouncement || newPlayerAnnouncements || respawnAnnouncements)
            {
                JustJoined.Add(player.userID);
            }
            if (!storedData.PlayerData.ContainsKey(player.userID))
            {
                CreatePlayerData(player);
            }
            if (storedData.PlayerData.ContainsKey(player.userID))
            {
                storedData.PlayerData[player.userID].TimesJoined = storedData.PlayerData[player.userID].TimesJoined + 1;
                SaveData();
            }
            if (JustJoined.Contains(player.userID) && globalJoinAnnouncements)
            {
                string Group = permission.GetUserGroups(player.UserIDString)[0];
                if (globalJoinLeavePermissionOnly && hasPermission(player, PermAnnounceJoinLeave))
                    CreateAnnouncement(globalJoinText.Replace("{playername}", player.displayName).Replace("{rank}", char.ToUpper(Group[0]) + Group.Substring(1)), globalJoinAnnouncementBannerColor, globalJoinAnnouncementTextColor);
                else if (!globalJoinLeavePermissionOnly)
                    CreateAnnouncement(globalJoinText.Replace("{playername}", player.displayName).Replace("{rank}", char.ToUpper(Group[0]) + Group.Substring(1)), globalJoinAnnouncementBannerColor, globalJoinAnnouncementTextColor);
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (JustJoined.Contains(player.userID))
            {
                JustJoined.Remove(player.userID);
            }
            NewPlayerPrivateTimers.TryGetValue(player, out NewPlayerTimer);
            if (NewPlayerTimer != null && !NewPlayerTimer.Destroyed)
            {
                NewPlayerTimer.Destroy();
            }
            PlayerRespawnedTimers.TryGetValue(player, out PlayerRespawnedTimer);
            if (PlayerRespawnedTimer != null && !PlayerRespawnedTimer.Destroyed)
            {
                PlayerRespawnedTimer.Destroy();
            }
			if (GlobalTimerList.Contains(player.userID))
			{
				GlobalTimerList.Remove(player.userID);
			}
            destroyPrivateGUI(player);
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!storedData.PlayerData.ContainsKey(player.userID))
            {
                CreatePlayerData(player);
                storedData.PlayerData[player.userID].TimesJoined = storedData.PlayerData[player.userID].TimesJoined + 1;
                SaveData();
            }
            if (JustJoined.Contains(player.userID))
            {
                if (welcomeAnnouncement)
                {
                    WelcomeAnnouncement(player);
                    if (!newPlayerAnnouncements && storedData.PlayerData[player.userID].Dead == true && respawnAnnouncements)
                    {
                        storedData.PlayerData[player.userID].Dead = false;
                        timer.Once(welcomeAnnouncementDuration, () => RespawnedAnnouncements(player));
                    }
                }
                if (newPlayerAnnouncements)
                {
                    if (newPlayerAnnouncementsList.ContainsKey(storedData.PlayerData[player.userID].TimesJoined) || newPlayerAnnouncementsList.ContainsKey(0))
                    {
                        if (welcomeAnnouncement)
                        {
                            timer.Once(welcomeAnnouncementDuration, () => NewPlayerAnnouncements(player));
                        }
                        else
                        {
                            NewPlayerAnnouncements(player);
                        }
                    }
                    else
                    if (storedData.PlayerData[player.userID].Dead == true && respawnAnnouncements)
                    {
                        RespawnedAnnouncements(player);
                        storedData.PlayerData[player.userID].Dead = false;
                    }
                }
                if (!newPlayerAnnouncements && !welcomeAnnouncement && storedData.PlayerData[player.userID].Dead == true && respawnAnnouncements)
                {
                    RespawnedAnnouncements(player);
                    storedData.PlayerData[player.userID].Dead = false;
                }
            }
            else
            if (!JustJoined.Contains(player.userID) && storedData.PlayerData[player.userID].Dead == true && respawnAnnouncements)
            {
                RespawnedAnnouncements(player);
                storedData.PlayerData[player.userID].Dead = false;
            }
            if (!JustJoined.Contains(player.userID) && storedData.PlayerData[player.userID].Dead == true && !welcomeAnnouncement && !newPlayerAnnouncements && respawnAnnouncements)
            {
                RespawnedAnnouncements(player);
                storedData.PlayerData[player.userID].Dead = false;
            }
            if (storedData.PlayerData[player.userID].Dead == true && !respawnAnnouncements)
            {
                storedData.PlayerData[player.userID].Dead = false;
            }
        }

        void destroyAllGUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (AnnouncementsData.ContainsKey(player.userID))
                {
                    AnnouncementsData.Remove(player.userID);
                }
                destroyAllTimers(player);
				CuiHelper.DestroyUi(player, "AnnouncementBanner");
                CuiHelper.DestroyUi(player, "AnnouncementText");
            }
        }

        void destroyAllTimers(BasePlayer player)
        {
            if (GlobalTimer != null && !GlobalTimer.Destroyed)
            {
                GlobalTimer.Destroy();
            }
            if (GlobalTimerList.Contains(player.userID))
            {
                GlobalTimerList.Remove(player.userID);
            }
            PrivateTimers.TryGetValue(player, out PlayerTimer);
            if (PlayerTimer != null && !PlayerTimer.Destroyed)
            {
                PlayerTimer.Destroy();
            }
        }

        void destroyGlobalGUI()
        {
			if (GlobalTimer != null && !GlobalTimer.Destroyed)
            {
                GlobalTimer.Destroy();
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (AnnouncementsData.ContainsKey(player.userID))
                {
                    AnnouncementsData.Remove(player.userID);
                }
                if (GlobalTimerList.Contains(player.userID))
                {
                    GlobalTimerList.Remove(player.userID);
					CuiHelper.DestroyUi(player, "AnnouncementBanner");
                    CuiHelper.DestroyUi(player, "AnnouncementText");
                }
            }
        }

        void destroyPrivateGUI(BasePlayer player)
        {
            if (AnnouncementsData.ContainsKey(player.userID))
            {
                AnnouncementsData.Remove(player.userID);
            }
            destroyPrivateTimer(player);
			CuiHelper.DestroyUi(player, "AnnouncementBanner");
            CuiHelper.DestroyUi(player, "AnnouncementText");
        }

        void destroyPrivateTimer(BasePlayer player)
        {
            if (GlobalTimerList.Contains(player.userID))
            {
                GlobalTimerList.Remove(player.userID);
            }
            PrivateTimers.TryGetValue(player, out PlayerTimer);
            if (PlayerTimer != null && !PlayerTimer.Destroyed)
            {
                PlayerTimer.Destroy();
            }
        }

        void Unload()
        {
			destroyAllGUI();
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                NewPlayerPrivateTimers.TryGetValue(player, out NewPlayerTimer);
                if (NewPlayerTimer != null && !NewPlayerTimer.Destroyed)
                    NewPlayerTimer.Destroy();
                PlayerRespawnedTimers.TryGetValue(player, out PlayerRespawnedTimer);
                if (PlayerRespawnedTimer != null && !PlayerRespawnedTimer.Destroyed)
                    PlayerRespawnedTimer.Destroy();
            }
            if (SixtySecondsTimer != null && !SixtySecondsTimer.Destroyed)
                SixtySecondsTimer.Destroy();
            if (AutomaticAnnouncementsTimer != null && !AutomaticAnnouncementsTimer.Destroyed)
                AutomaticAnnouncementsTimer.Destroy();
            if (RealTimeTimer != null && !RealTimeTimer.Destroyed)
                RealTimeTimer.Destroy();
            SaveData();
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BaseHelicopter && info.Initiator is BasePlayer && helicopterDestroyedAnnouncementWithDestroyer)
                    LastHitPlayer = info.Initiator.ToPlayer().displayName;
        }

        string ConvertBannerColor(string BColor)
        {
            if (BColor.ToLower() == "grey")
                return BannerTintGrey;
            if (BColor.ToLower() == "red")
                return BannerTintRed;
            if (BColor.ToLower() == "orange")
                return BannerTintOrange;
            if (BColor.ToLower() == "yellow")
                return BannerTintYellow;
            if (BColor.ToLower() == "green")
                return BannerTintGreen;
            if (BColor.ToLower() == "cyan")
                return BannerTintCyan;
            if (BColor.ToLower() == "blue")
                return BannerTintBlue;
            if (BColor.ToLower() == "purple")
                return BannerTintPurple;
            PrintWarning("Banner color not found. Please check config");
            return BannerTintGrey;
        }

        string ConvertTextColor(string TColor)
        {
            if (TColor.ToLower() == "red")
                return TextRed;
            if (TColor.ToLower() == "orange")
                return TextOrange;
            if (TColor.ToLower() == "yellow")
                return TextYellow;
            if (TColor.ToLower() == "green")
                return TextGreen;
            if (TColor.ToLower() == "cyan")
                return TextCyan;
            if (TColor.ToLower() == "blue")
                return TextBlue;
            if (TColor.ToLower() == "purple")
                return TextPurple;
            if (TColor.ToLower() == "white")
                return TextWhite;
            PrintWarning("Text color not found. Please check config");
            return TextWhite;
        }

        private static BasePlayer FindPlayer(string IDName)
        {
            foreach (BasePlayer targetPlayer in BasePlayer.activePlayerList)
            {
                if (targetPlayer.UserIDString == IDName)
                    return targetPlayer;
                if (targetPlayer.displayName.Contains(IDName, CompareOptions.OrdinalIgnoreCase))
                    return targetPlayer;
            }
            return null;
        }

        private bool hasPermission(BasePlayer player, string perm)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), perm))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return false;
            }
            return true;
        }
		
		void RestartAnnouncementsStart()
		{
            if (RealTimeTimer != null && !RealTimeTimer.Destroyed)
                RealTimeTimer.Destroy();
            if (restartAnnouncements)
            {
                List<string> restartTimesList = ConvertObjectListToString(restartTimes);
                RestartTimes = restartTimesList.Select(date => DateTime.Parse(date)).ToList();
            }
            RestartAnnouncementsWhenStrings = ConvertObjectListToString(restartAnnouncementsWhen);
            List<TimeSpan> RestartAnnouncementsWhen = RestartAnnouncementsWhenStrings.Select(date => TimeSpan.Parse(date)).ToList();
            GetNextRestart(RestartTimes);
            RealTimeTimer = timer.Repeat(0.5f, 0, () => RestartAnnouncements(RestartAnnouncementsWhen));
        }
		

        void GetNextRestart(List<DateTime> DateTimes)
        {
            var e = DateTimes.GetEnumerator();
            for (var i = 0; e.MoveNext(); i++)
            {
                if (DateTime.Compare(DateTime.Now, e.Current) < 0)
                {
                    CalcNextRestartDict.Add(e.Current, e.Current.Subtract(DateTime.Now));
                }
                if (DateTime.Compare(DateTime.Now, e.Current) > 0)
                {
                    CalcNextRestartDict.Add(e.Current.AddDays(1), e.Current.AddDays(1).Subtract(DateTime.Now));
                }
            }
            NextRestart = CalcNextRestartDict.Aggregate((l, r) => l.Value < r.Value ? l : r).Key;
            CalcNextRestartDict.Clear();
            Puts("Next restart is in " + NextRestart.Subtract(DateTime.Now).ToShortString() + " at " + NextRestart.ToLongTimeString());
        }
		
		string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
        //============================================================================================================
        #region Auto Announcements

        void RestartAnnouncements(List<TimeSpan> RestartAnnouncementsWhen)
        {
            var currentTime = DateTime.Now;
            if (NextRestart <= currentTime)
            {
                if (RestartSuspended)
                {
                    Puts(Lang("SuspendedRestartPassed").Replace("{time}", NextRestart.ToLongTimeString()));
                    RestartSuspended = false;
                }
                if (GetNextRestartTimer == null && GetNextRestartTimer.Destroyed && DontCheckNextRestart == false)
                {
                    DontCheckNextRestart = true;
                    GetNextRestartTimer = timer.Once(3f, () =>
                    {
                        RestartAnnouncementsStart();
                        DontCheckNextRestart = false;
                    });
                }
                return;
            }
            if (!RestartSuspended)
            {
                TimeSpan timeLeft = NextRestart.Subtract(currentTime);
                string secondsString = String.Empty;
                int hoursLeft = timeLeft.Hours;
                int minutesLeft = timeLeft.Minutes;
                int secondsLeft = timeLeft.Seconds;
                if ((!RestartCountdown && RestartAnnouncementsWhenStrings.Contains(timeLeft.ToShortString()) && ((LastHour != currentTime.Hour) || (LastMinute != currentTime.Minute))) || RestartJustScheduled)
                {
                    string timeLeftString = String.Empty;
                    if (RestartJustScheduled)
                        RestartJustScheduled = false;
                    if (hoursLeft > 1)
                    {
                        timeLeftString = timeLeftString + hoursLeft + " " + Lang("Hours");
                        LastHour = currentTime.Hour;
                    }
                    if (hoursLeft == 1)
                    {
                        timeLeftString = timeLeftString + hoursLeft + " " + Lang("Hour");
                        LastHour = currentTime.Hour;
                    }
                    if (minutesLeft > 0)
                    {
                        timeLeftString = timeLeftString + minutesLeft + " " + Lang("Minutes");
                        LastMinute = currentTime.Minute;
                    }
                    if (String.IsNullOrEmpty(RestartReason))
                    {
                        Puts(restartAnnouncementsFormat.Replace("{time}", timeLeftString));
                        CreateAnnouncement(restartAnnouncementsFormat.Replace("{time}", timeLeftString), restartAnnouncementsBannerColor, restartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                    else
                    {
                        Puts(restartAnnouncementsFormat.Replace("{time}", timeLeftString) + " " + RestartReason);
                        CreateAnnouncement(restartAnnouncementsFormat.Replace("{time}", timeLeftString) + " " + RestartReason, restartAnnouncementsBannerColor, restartAnnouncementsTextColor, isRestartAnnouncement: true);
                    }
                }
                if (timeLeft <= new TimeSpan(00, 01, 00) && !RestartCountdown)
                {
                    int countDown = timeLeft.Seconds;
                    RestartCountdown = true;
                    if (String.IsNullOrEmpty(RestartReason))
                    {
                        CreateAnnouncement(restartAnnouncementsFormat.Replace("{time}", countDown.ToString() + " seconds"), restartAnnouncementsBannerColor, restartAnnouncementsTextColor);
                    }
                    else
                    {
                        CreateAnnouncement(restartAnnouncementsFormat.Replace("{time}", countDown.ToString() + " seconds" + " : " + RestartReason), restartAnnouncementsBannerColor, restartAnnouncementsTextColor);
                    }
                    SixtySecondsTimer = timer.Repeat(1, countDown + 1, () =>
                        {
                            if (countDown == 1)
                                secondsString = " " + Lang("Second");
                            else
                                secondsString = " " + Lang("Seconds");
                            if (String.IsNullOrEmpty(RestartReason))
                            {
                                CreateAnnouncement(restartAnnouncementsFormat.Replace("{time}", countDown.ToString() + secondsString), restartAnnouncementsBannerColor, restartAnnouncementsTextColor);
                            }
                            else
                            {
                                CreateAnnouncement(restartAnnouncementsFormat.Replace("{time}", countDown.ToString() + secondsString + " seconds" + " : " + RestartReason), restartAnnouncementsBannerColor, restartAnnouncementsTextColor);
                            }
                            countDown = countDown - 1;
                            if (countDown == 0)
                            {
                                Puts("Restart countdown finished.");
                                if (RestartScheduled && RealTimeTimer != null && RealTimeTimer.Destroyed)
                                {
                                    RestartScheduled = false;
                                    RealTimeTimer.Destroy();
                                }
                                if (restartServer)
                                {
                                    rust.RunServerCommand("save");
                                    timer.Once(3, () => rust.RunServerCommand("restart 0"));
                                }
                            }
                        });
                }
            }
        }

        void OnUserBanned(string id, string name, string IP, string reason)
        {
            if (playerBannedAnnouncement && !MuteBans)
            {
                CreateAnnouncement(playerBannedAnnouncmentText.Replace("{playername}", name).Replace("{reason}", reason), playerBannedAnnouncementBannerColor, playerBannedAnnouncementTextColor);
            }
        }


        void OnPlayerDisconnect(BasePlayer player)
        {
            if (globalLeaveAnnouncements)
            {
                string Group = permission.GetUserGroups(player.UserIDString)[0];
                if (globalJoinLeavePermissionOnly && hasPermission(player, PermAnnounceJoinLeave))
                {
                    CreateAnnouncement(globalLeaveText.Replace("{playername}", player.displayName).Replace("{rank}", char.ToUpper(Group[0]) + Group.Substring(1)), globalJoinAnnouncementBannerColor, globalJoinAnnouncementTextColor);
                }
                else
                {
                    CreateAnnouncement(globalLeaveText.Replace("{playername}", player.displayName).Replace("{rank}", char.ToUpper(Group[0]) + Group.Substring(1)), globalLeaveAnnouncementBannerColor, globalLeaveAnnouncementTextColor);
                }
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (helicopterSpawnAnnouncement && entity is BaseHelicopter)
            {
                CreateAnnouncement(helicopterSpawnAnnouncementText, helicopterSpawnAnnouncementBannerColor, helicopterSpawnAnnouncementTextColor);
            }
            
            if (stockingRefillAnnouncement && entity is XMasRefill)
            {
                CreateAnnouncement(stockingRefillAnnouncementText, stockingRefillAnnouncementBannerColor, stockingRefillAnnouncementTextColor);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (helicopterDestroyedAnnouncement && entity is BaseHelicopter)
            {
                var entityNetID = entity.net.ID;
                if (helicopterDespawnAnnouncement)
                    HeliNetIDs.Add(entityNetID);
                if (helicopterDestroyedAnnouncementWithDestroyer)
                {
                    CreateAnnouncement(helicopterDestroyedAnnouncementWithDestroyerText.Replace("{playername}", LastHitPlayer), helicopterDestroyedAnnouncementBannerColor, helicopterDestroyedAnnouncementTextColor);
                    LastHitPlayer = String.Empty;
                }
                else
                {
                    CreateAnnouncement(helicopterDestroyedAnnouncementText, helicopterDestroyedAnnouncementBannerColor, helicopterDestroyedAnnouncementTextColor);
                }
            }
            if (entity is BasePlayer)
            {
                if (storedData.PlayerData.ContainsKey(entity.ToPlayer().userID))
                {
                    storedData.PlayerData[entity.ToPlayer().userID].Dead = true;
                    SaveData();
                }
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity is BaseHelicopter)
            {
                var entityNetID = entity.net.ID;
                timer.Once(2, () =>
                {
                    if (HeliNetIDs.Contains(entityNetID))
                        HeliNetIDs.Remove(entityNetID);
                    else if (helicopterDespawnAnnouncement)
                        CreateAnnouncement(helicopterDespawnAnnouncementText, helicopterDespawnAnnouncementBannerColor, helicopterDespawnAnnouncementTextColor);
                });
            }
        }

        void OnAirdrop(CargoPlane plane, Vector3 location)
        {
            if (airdropAnnouncement)
            {
                if (airdropAnnouncementLocation)
                {
                    string x = location.x.ToString(), z = location.z.ToString();
                    CreateAnnouncement(airdropAnnouncementTextWithCoords.Replace("{x}", x).Replace("{z}", z), airdropAnnouncementBannerColor, airdropAnnouncementTextColor);
                }
                else CreateAnnouncement(airdropAnnouncementText, airdropAnnouncementBannerColor, airdropAnnouncementTextColor);
            }
        }

        void WelcomeAnnouncement(BasePlayer player)
        {
            if (welcomeAnnouncement)
            {
                if (welcomeBackAnnouncement && storedData.PlayerData[player.userID].TimesJoined > 1)
                {
                    CreateAnnouncement(welcomeBackAnnouncementText.Replace("{playername}", player.displayName), welcomeAnnouncementBannerColor, welcomeAnnouncementTextColor, player, true);
                }
                else
                {
                    CreateAnnouncement(welcomeAnnouncementText.Replace("{playername}", player.displayName), welcomeAnnouncementBannerColor, welcomeAnnouncementTextColor, player, true);
                }
            }
        }

        void NewPlayerAnnouncements(BasePlayer player)
        {
			if (JustJoined.Contains(player.userID))
            {
                JustJoined.Remove(player.userID);
            }
            if (newPlayerAnnouncementsList.ContainsKey(storedData.PlayerData[player.userID].TimesJoined) || newPlayerAnnouncementsList.ContainsKey(0))
            {
                List<string> AnnouncementList = new List<string>();
                if (newPlayerAnnouncementsList.ContainsKey(storedData.PlayerData[player.userID].TimesJoined) && !newPlayerAnnouncementsList.ContainsKey(0))
                    AnnouncementList = ConvertObjectListToString(newPlayerAnnouncementsList[storedData.PlayerData[player.userID].TimesJoined]);
                if (newPlayerAnnouncementsList.ContainsKey(0))
                    AnnouncementList = ConvertObjectListToString(newPlayerAnnouncementsList[0]);
                if (AnnouncementList.Count > 0)
                {
                    string Group = permission.GetUserGroups(player.UserIDString)[0];
                    List<string>.Enumerator e = AnnouncementList.GetEnumerator();
                    if (storedData.PlayerData[player.userID].Dead == true && respawnAnnouncements)
                    {
                        PlayerRespawnedTimers[player] = timer.Once(announcementDuration * AnnouncementList.Count, () => RespawnedAnnouncements(player));
                        storedData.PlayerData[player.userID].Dead = false;
                        SaveData();
                    }
                    if (e.MoveNext())
                    {
                        Puts(e.Current);
                        CreateAnnouncement(e.Current.Replace("{playername}", player.displayName).Replace("{rank}", char.ToUpper(Group[0]) + Group.Substring(1)), newPlayerAnnouncementsBannerColor, newPlayerAnnouncementsTextColor, player);
                        if (AnnouncementList.Count > 1)
                        {
                            NewPlayerPrivateTimers[player] = timer.Repeat(announcementDuration, AnnouncementList.Count - 1, () =>
                            {
                                if (e.MoveNext())
                                {
                                    Puts(e.Current);
                                    CreateAnnouncement(e.Current.Replace("{playername}", player.displayName).Replace("{rank}", char.ToUpper(Group[0]) + Group.Substring(1)), newPlayerAnnouncementsBannerColor, newPlayerAnnouncementsTextColor, player);
                                }
                            });
                        }
                    }
                }
            }
        }

        void RespawnedAnnouncements(BasePlayer player)
        {
            if(JustJoined.Contains(player.userID))
            {
                JustJoined.Remove(player.userID);
            }
            List<string> respawnAnnouncementsList = ConvertObjectListToString(Config.Get("Automatic Announcements", "RespawnAnnouncementsList"));
            List<string>.Enumerator e = respawnAnnouncementsList.GetEnumerator();
            e.MoveNext();
            CreateAnnouncement(e.Current, respawnAnnouncementsBannerColor, respawnAnnouncementsTextColor, player);
            PlayerRespawnedTimers[player] = timer.Repeat(announcementDuration, respawnAnnouncementsList.Count - 1, () =>
            {
                e.MoveNext();
                CreateAnnouncement(e.Current.Replace("{playername}", player.displayName), respawnAnnouncementsBannerColor, respawnAnnouncementsTextColor, player);
            });
        }

        void AutomaticTimedAnnouncements()
        {
            if (ATALEnum.MoveNext() == false)
            {
                ATALEnum.Reset();
                ATALEnum.MoveNext();
            }
            CreateAnnouncement(ATALEnum.Current, automaticTimedAnnouncementsBannerColor, automaticTimedAnnouncementsTextColor);
        }

        #endregion
        //============================================================================================================
        #region Commands

        void cmdAnnounce(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (args?.Length > 0)
                {
                    string Msg = "";
                    for (int i = 0; i < args.Length; i++)
                        Msg = Msg + " " + args[i];
                    CreateAnnouncement(Msg, "Grey", "White");
                }
                else SendReply(player, Lang("ChatCommandAnnounceUsage", player.UserIDString));
            }
        }

        void ccmdAnnounce(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (arg?.Args?.Length > 0)
                {
                    string Msg = "";
                    for (int i = 0; i < arg.Args.Length; i++)
                        Msg = Msg + " " + arg.Args[i];
                    CreateAnnouncement(Msg, "Grey", "White");
                }
                else SendReply(arg, Lang("ConsoleCommandAnnounceUsage", arg.Connection?.userid.ToString()));
            }
        }

        void cmdAnnounceTo(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (args?.Length > 1)
                {
                    string targetPlayer = args[0].ToLower(), Msg = "";
                    for (int i = 1; i < args.Length; i++)
                        Msg = Msg + " " + args[i];
                    BasePlayer targetedPlayer = FindPlayer(targetPlayer);
                    if (targetedPlayer != null)
                    {
                        if (!Exclusions.ContainsKey(targetedPlayer.userID))
                        {
                            CreateAnnouncement(Msg, "Grey", "White", targetedPlayer);
                        }
                        else SendReply(player, Lang("IsExcluded", player.UserIDString).Replace("{playername}", targetedPlayer.displayName));
                    }
                    else SendReply(player, Lang("PlayerNotFound", player.UserIDString).Replace("{playername}", targetPlayer));
                }
                else SendReply(player, Lang("ChatCommandAnnounceToUsage", player.UserIDString));
            }
        }

        void ccmdAnnounceTo(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (arg?.Args?.Length > 1)
                {
                    string targetPlayer = arg.Args[0].ToLower(), Msg = "";
                    for (int i = 1; i < arg.Args.Length; i++)
                        Msg = Msg + " " + arg.Args[i];
                    BasePlayer targetedPlayer = FindPlayer(targetPlayer);
                    if (targetedPlayer != null)
                    {
                        if (!Exclusions.ContainsKey(targetedPlayer.userID))
                        {
                            CreateAnnouncement(Msg, "Grey", "White", targetedPlayer);
                        }
                        else SendReply(arg, Lang("IsExcluded", arg.Connection?.userid.ToString()).Replace("{playername}", targetedPlayer.displayName));
                    }
                    else SendReply(arg, Lang("PlayerNotFound", arg.Connection?.userid.ToString()).Replace("{playername}", targetPlayer));
                }
                else SendReply(arg, Lang("ConsoleCommandAnnounceToUsage", arg.Connection?.userid.ToString()));
            }
        }

        void cmdAnnounceToGroup(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (args?.Length > 1)
                {
                    string targetGroup = args[0].ToLower(), Msg = "";
                    if (permission.GroupExists(targetGroup))
                    {
                        for (int i = 1; i < args.Length; i++)
                            Msg = Msg + " " + args[i];
                        CreateAnnouncement(Msg, "Grey", "White", group: targetGroup);
                    }
                    else SendReply(player, Lang("GroupNotFound", player.UserIDString).Replace("{group}", targetGroup));
                }
                else SendReply(player, Lang("ChatCommandAnnounceToGroupUsage", player.UserIDString));
            }
        }

        void ccmdAnnounceToGroup(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (arg?.Args?.Length > 1)
                {
                    string targetGroup = arg.Args[0].ToLower(), Msg = "";
                    Puts(arg.Args.Length.ToString());
                    if (permission.GroupExists(targetGroup))
                    {
                        for (int i = 1; i < arg.Args.Length; i++)
                            Msg = Msg + " " + arg.Args[i];
                        CreateAnnouncement(Msg, "Grey", "White", group:targetGroup);
                    }
                    else SendReply(arg, Lang("GroupNotFound", arg.Connection?.userid.ToString()).Replace("{group}", targetGroup));
                }
                else SendReply(arg, Lang("ConsoleCommandAnnounceToGroupUsage", arg.Connection?.userid.ToString()));
            }
        }

        void cmdAnnounceTest(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (!Exclusions.ContainsKey(player.userID))
                {
                    CreateAnnouncement("GUIAnnouncements Test Announcement", "Grey", "White", player);
                }
                else SendReply(player, Lang("YouAreExcluded", player.UserIDString));
            }
        }

        void cmdDestroyAnnouncement(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                destroyAllGUI();
            }
        }

        void ccmdAnnounceDestroy(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                destroyAllGUI();
            }
        }

        void cmdMuteBans(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (MuteBans)
                {
                    MuteBans = false;
                    SendReply(player, Lang("BansUnmuted", player.UserIDString));
                    return;
                }
                if (!MuteBans)
                {
                    MuteBans = true;
                    SendReply(player, Lang("BansMuted", player.UserIDString));
                    return;
                }
            }
        }

        void ccmdMuteBans(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (MuteBans)
                {
                    MuteBans = false;
                    SendReply(arg, Lang("BansUnmuted", arg.Connection?.userid.ToString()));
                    return;
                }
                if (!MuteBans)
                {
                    MuteBans = true;
                    SendReply(arg, Lang("BansMuted", arg.Connection?.userid.ToString()));
                    return;
                }
            }
        }

        void cmdAnnouncementsToggle(BasePlayer player, string cmd, string[] args)
        {
            if (args == null) //Self
            {
                if (Exclusions.ContainsKey(player.userID)) //Include
                {
                    Exclusions.Remove(player.userID);
                    SendReply(player, Lang("IncludedTo", player.UserIDString));
                    return;
                }
                else
                {
                    if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounceToggle) || hasPermission(player, PermAnnounce)) //Exclude
                    {
                        Exclusions.Add(player.userID, player.displayName);
                        SendReply(player, Lang("ExcludedTo", player.UserIDString));
                        return;
                    }
                }
            }
            if (args.Length > 0) //Not Self
            {
                if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
                {
                    string targetPlayer = args[0].ToLower();
                    ulong targetPlayerUID64; ulong.TryParse(targetPlayer, out targetPlayerUID64);
                    BasePlayer targetedPlayer = FindPlayer(targetPlayer);
                    var GetKey = Exclusions.FirstOrDefault(x => x.Value.Contains(targetPlayer, CompareOptions.OrdinalIgnoreCase)).Key;
                    if (Exclusions.ContainsKey(GetKey) || Exclusions.ContainsKey(targetPlayerUID64)) //Include
                    {
                        string PlayerName = Exclusions[GetKey];
                        Exclusions.Remove(GetKey); Exclusions.Remove(targetPlayerUID64);
                        SendReply(player, Lang("Included", player.UserIDString).Replace("{playername}", PlayerName));
                        if (targetedPlayer != null)
                        {
                            SendReply(targetedPlayer, Lang("IncludedTo", targetedPlayer.UserIDString));
                        }
                    }
                    else
                    if (targetedPlayer != null) //Exclude
                    {
                        Exclusions.Add(targetedPlayer.userID, targetedPlayer.displayName);
                        SendReply(player, Lang("Excluded", player.UserIDString).Replace("{playername}", targetedPlayer.displayName));
                        SendReply(targetedPlayer, Lang("ExcludedTo", targetedPlayer.UserIDString));
                    }
                    else SendReply(player, Lang("PlayerNotFound", player.UserIDString));
                }
            }
        }

        void ccmdAnnouncementsToggle(ConsoleSystem.Arg arg, string[] args)
        {
            if (arg?.Args?.Length > 0) //Not Self
            {
                if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
                {
                    string targetPlayer = arg.Args[0].ToLower();
                    ulong targetPlayerUID64; ulong.TryParse(targetPlayer, out targetPlayerUID64);
                    BasePlayer targetedPlayer = FindPlayer(targetPlayer);
                    var GetKey = Exclusions.FirstOrDefault(x => x.Value.Contains(targetPlayer, CompareOptions.OrdinalIgnoreCase)).Key;
                    if (Exclusions.ContainsKey(GetKey) || Exclusions.ContainsKey(targetPlayerUID64)) //Include
                    {
                        string PlayerName = Exclusions[GetKey];
                        Exclusions.Remove(GetKey); Exclusions.Remove(targetPlayerUID64);
                        SendReply(arg, Lang("Included", arg.Connection?.userid.ToString()).Replace("{playername}", PlayerName));
                        if (targetedPlayer != null)
                        {
                            SendReply(targetedPlayer, Lang("IncludedTo", targetedPlayer.UserIDString));
                        }
                    }
                    else
                        if (targetedPlayer != null) //Exclude
                    {
                        Exclusions.Add(targetedPlayer.userID, targetedPlayer.displayName);
                        SendReply(arg, Lang("Excluded", arg.Connection?.userid.ToString()).Replace("{playername}", targetedPlayer.displayName));
                        SendReply(targetedPlayer, Lang("ExcludedTo", targetedPlayer.UserIDString));
                    }
                    else SendReply(arg, Lang("PlayerNotFound", arg.Connection?.userid.ToString()));
                }
            }
            else SendReply(arg, Lang("ConsoleCommandAnnouncementsToggleUsage", arg.Connection?.userid.ToString()));
        }

        void cmdScheduleRestart(BasePlayer player, string cmd, string[] args)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (args?.Length > 0)
                {
                    if (!RestartCountdown)
                    {
                        if (!RestartScheduled)
                        {
                            var currentTime = DateTime.Now;
                            TimeSpan scheduleRestart;
                            if (TimeSpan.TryParse(args[0], out scheduleRestart))
                            {
                                if (restartAnnouncements && currentTime.Add(scheduleRestart) > NextRestart)
                                {
                                    SendReply(player, Lang("LaterThanNextRestart", player.UserIDString).Replace("{time}", NextRestart.ToShortTimeString()));
                                    return;
                                }
                                if (args.Length > 1)
                                {
                                    RestartReason = "";
                                    for (int i = 1; i < args.Length; i++)
                                        RestartReason = RestartReason + " " + args[i];
                                }
                                if (String.IsNullOrEmpty(RestartReason))
                                {
                                    Puts("Restart scheduled in " + scheduleRestart.ToShortString());
                                }
                                else
                                {
                                    Puts("Restart scheduled in " + scheduleRestart.ToShortString() + ": " + RestartReason.ToString());
                                }
                                RestartTimes.Add(currentTime.Add(scheduleRestart + new TimeSpan(00, 00, 01)));
                                ScheduledRestart = currentTime.Add(scheduleRestart + new TimeSpan(00, 00, 01));
                                RestartScheduled = true;
                                RestartJustScheduled = true;
                                if (!restartAnnouncements)
                                    RestartAnnouncementsStart();
                                else GetNextRestart(RestartTimes);
                            }
                            else SendReply(player, Lang("ChatCommandScheduleRestartUsage", player.UserIDString));
                        }
                        else SendReply(player, Lang("RestartAlreadyScheduled").Replace("{time}", NextRestart.ToShortTimeString()));
                    }
                }
                else SendReply(player, Lang("ChatCommandScheduleRestartUsage", player.UserIDString));
            }
        }

        void ccmdScheduleRestart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (arg?.Args?.Length > 0)
                {
                    if (!RestartCountdown)
                    {
                        if (!RestartScheduled)
                        {
                            var currentTime = DateTime.Now;
                            TimeSpan scheduleRestart; TimeSpan.TryParse(arg.Args[0], out scheduleRestart);
                            if (restartAnnouncements && currentTime.Add(scheduleRestart) > NextRestart)
                            {
                                SendReply(arg, Lang("LaterThanNextRestart", arg.Connection?.userid.ToString()).Replace("{time}", NextRestart.ToShortTimeString()));
                                return;
                            }
                            if (arg?.Args?.Length > 1)
                            {
                                RestartReason = "";
                                for (int i = 1; i < arg.Args.Length; i++)
                                    RestartReason = RestartReason + " " + arg.Args[i];
                            }
                            if (String.IsNullOrEmpty(RestartReason))
                            {
                                Puts("Restart scheduled in " + scheduleRestart.ToShortString());
                            }
                            else
                            {
                                Puts("Restart scheduled in " + scheduleRestart.ToShortString() + ": " + RestartReason.ToString());
                            }
                            RestartTimes.Add(currentTime.Add(scheduleRestart + new TimeSpan(00, 00, 01)));
                            ScheduledRestart = currentTime.Add(scheduleRestart + new TimeSpan(00, 00, 01));
                            RestartScheduled = true;
                            RestartJustScheduled = true;
                            if (!restartAnnouncements)
                                RestartAnnouncementsStart();
                            else GetNextRestart(RestartTimes);
                        }
                        else SendReply(arg, Lang("RestartAlreadyScheduled", arg.Connection?.userid.ToString()).Replace("{time}", NextRestart.ToShortTimeString()));
                    }
                }
                else SendReply(arg, Lang("ChatCommandScheduleRestartUsage", arg.Connection?.userid.ToString()));
            }
        }

        void cmdCancelScheduledRestart(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (RestartScheduled)
                {
                    RestartReason = String.Empty;
                    RestartTimes.Remove(ScheduledRestart);
                    RestartScheduled = false;
                    if (restartAnnouncements)
                        GetNextRestart(RestartTimes);
                    Puts(Lang("ScheduledRestartCancelled").Replace("{time}", ScheduledRestart.ToShortTimeString()));
                    SendReply(player, (Lang("ScheduledRestartCancelled", player.UserIDString).Replace("{time}", ScheduledRestart.ToShortTimeString())));
                }
                else SendReply(player, Lang("RestartNotScheduled", player.UserIDString));
            }
        }

        void ccmdCancelScheduledRestart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (RestartScheduled)
                {
                    RestartReason = String.Empty;
                    RestartTimes.Remove(ScheduledRestart);
                    RestartScheduled = false;
                    if (restartAnnouncements)
                        GetNextRestart(RestartTimes);
                    SendReply(arg, (Lang("ScheduledRestartCancelled", arg.Connection?.userid.ToString()).Replace("{time}", ScheduledRestart.ToShortTimeString())));
                }
                else SendReply(arg, Lang("RestartNotScheduled", arg.Connection?.userid.ToString()));
            }
        }

        void cmdSuspendRestart(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                RestartSuspended = true;
                SendReply(player, Lang("RestartSuspendedChat", player.UserIDString).Replace("{time}", NextRestart.ToLongTimeString()));
            }
        }

        void ccmdSuspendRestart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                RestartSuspended = true;
                SendReply(arg, Lang("RestartSuspendedConsole", arg.Connection?.userid.ToString()).Replace("{time}", NextRestart.ToLongTimeString()));
            }
        }

        void cmdResumeRestart(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                RestartSuspended = false;
                SendReply(player, Lang("RestartResumed", player.UserIDString).Replace("{time}", NextRestart.ToLongTimeString()));
            }
        }

        void ccmdResumeRestart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                RestartSuspended = false;
                SendReply(arg, Lang("RestartResumed", arg.Connection?.userid.ToString()).Replace("{time}", NextRestart.ToLongTimeString()));
            }
        }

        void cmdGetNextRestart(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                var timeLeft = NextRestart.Subtract(DateTime.Now);
                SendReply(player, Lang("GetNextRestart", player.UserIDString).Replace("{time1}", timeLeft.ToShortString()).Replace("{time2}", NextRestart.ToLongTimeString()));
            }
        }

        void ccmdGetNextRestart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                var timeLeft = NextRestart.Subtract(DateTime.Now);
                SendReply(arg, Lang("GetNextRestart", arg.Connection?.userid.ToString()).Replace("{time1}", timeLeft.ToShortString()).Replace("{time2}", NextRestart.ToLongTimeString()));
            }
        }

        void cmdCancelRestart(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                if (SixtySecondsTimer != null && !SixtySecondsTimer.Destroyed)
                {
                    SixtySecondsTimer.Destroy();
                    SendReply(player, Lang("RestartTimerCanceled", player.UserIDString));
                    PrintWarning(Lang("RestartTimeCanceled"));
                    timer.Once(60, () => RestartCountdown = false);
                }
                else SendReply(player, Lang("NoRestartCountdown", player.UserIDString));
            }
        }

        void ccmdCancelRestart(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                if (SixtySecondsTimer != null && !SixtySecondsTimer.Destroyed)
                {
                    SixtySecondsTimer.Destroy();
                    SendReply(arg, Lang("RestartTimerCanceled", arg.Connection?.userid.ToString()));
                    PrintWarning(Lang("RestartTimeCanceled"));
					timer.Once(60, () => RestartCountdown = false);
                }
                else SendReply(arg, Lang("NoRestartCountdown", arg.Connection?.userid.ToString()));
            }
        }

        void cmdAnnounceHelp(BasePlayer player, string cmd)
        {
            if (player.net.connection.authLevel > 0 || hasPermission(player, PermAnnounce))
            {
                SendReply(player, Lang("AnnounceHelp", player.UserIDString));
            }
            else
                if (hasPermission(player, PermAnnounceToggle))
            {
                SendReply(player, Lang("PlayerHelp", player.UserIDString));
            }
        }

        void ccmdAnnounceHelp(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin || hasPermission(arg.Connection.player as BasePlayer, PermAnnounce))
            {
                SendReply(arg, Lang("AnnounceHelp", arg.Connection?.userid.ToString()));
            }
            else
                if (hasPermission(arg.Connection.player as BasePlayer, PermAnnounceToggle))
            {
                SendReply(arg, Lang("PlayerHelp", arg.Connection?.userid.ToString()));
            }
        }
        #endregion
    }
}