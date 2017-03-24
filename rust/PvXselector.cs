using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using System;                      //DateTime
using System.Collections.Generic;  //Required for Whilelist
using Oxide.Core.Libraries.Covalence; //Requrired for IPlayer stuff
//using System.Data;
//using System.Globalization;
using System.Linq;
//using System.Reflection;
using UnityEngine;
//using System.Collections;
//using ConVar;
//using Facepunch;
//using Network;
//using ProtoBuf;
//using System.Runtime.CompilerServices;
//using System.Text.RegularExpressions;
//using Oxide.Plugins;
using Oxide.Core.Configuration;
//using Oxide.Core.CSharp;
//using Rust.Registry;
//using RustNative;
using Oxide.Core.Database;
namespace Oxide.Plugins
{
    [Info("PvXSelector", "Alphawar", "0.9.6", ResourceId = 1817)]
    [Description("Player vs X Selector: Beta version 21/02/2017")]
    class PvXselector : RustPlugin
    {
        #region Data/PlayerJoin/ServerInit
        //Loaded goes first
        public static PvXselector instance;


        public static class Cooldowns
        {
            public static List<ulong> enemyOpModeWarning = new List<ulong>();
            public static List<ulong> menuGui = new List<ulong>();
        }


        void Loaded()
        {
            ModeSwitch.Ticket.Data.Initiate();
            ModeSwitch.Cooldown.Data.Initiate();
            Players.Data.Initiate();
            Containers.Data.Initiate();
            lang.RegisterMessages(Lang.List, this);
            PermissionHandle();
            LoadVariables();
            instance = this;
        }
        void OnServerInitialized()
        {
            LoadData();
            CheckPlayersRegistered();
            foreach (BasePlayer Player in BasePlayer.activePlayerList)
            {
                if (!Players.State.IsNPC(Player))
                {
                    GUI.Create.PlayerIndicator(Player);
                    ModeSwitch.Cooldown.Check(Player.userID);
                    if (HasPerm(Player, "admin"))
                    {
                        Players.Admins.AddPlayer(Player.userID);
                        GUI.Create.AdminIndicator(Player);
                    }
                    if (Players.State.IsNA(Player)) Timers.PvxChatNotification(Player);
                    UpdatePlayerChatTag(Player);
                }
            }
            foreach (ulong _key in Players.Data.playerData.Info.Keys)
            {
                if (Players.Data.playerData.Info[_key].FirstConnection == null) Players.Data.playerData.Info[_key].FirstConnection = DateTimeStamp();
                if (Players.Data.playerData.Info[_key].LatestConnection == null) Players.Data.playerData.Info[_key].LatestConnection = DateTimeStamp();
                if (Players.Data.playerData.Info[_key].FirstConnection == "null") Players.Data.playerData.Info[_key].FirstConnection = DateTimeStamp();
                if (Players.Data.playerData.Info[_key].LatestConnection == "null") Players.Data.playerData.Info[_key].LatestConnection = DateTimeStamp();
            }
            Timers.CooldownTickets();
        }

        void Unloaded()
        {
            foreach (var Player in BasePlayer.activePlayerList)
            {
                DestroyAllPvXUI(Player);
            }
            SaveData.All();
        }

        class SaveData
        {
            public static void All()
            {
                ModeSwitch.Cooldown.Data.Save();
                ModeSwitch.Ticket.Data.Save();
                Players.Data.Save();
                Containers.Data.Save();
            }
            public static void TicketData()
            {
                ModeSwitch.Ticket.Data.Save();
            }
            public static void ModeSwitchData()
            {
                ModeSwitch.Cooldown.Data.Save();
            }
            public static void SharedContainer()
            {
                Containers.Data.Save();
            }
        }

        void LoadData()
        {
            ModeSwitch.Ticket.Data.Load();
            ModeSwitch.Cooldown.Data.Load();
            Players.Data.Load();
            Containers.Data.Load();
        }
        void OnPlayerInit(BasePlayer Player)
        {
            if (Player == null) return;
            else if (Player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.Once(3f, () =>
                {
                    OnPlayerInit(Player);
                });
                return;
            }
            else PlayerLoaded(Player);
        }
        void PlayerLoaded(BasePlayer Player)
        {
            if (Players.State.IsNPC(Player)) return;
            if (Players.IsNew(Player.userID))
                Players.Add(Player);
            ModeSwitch.Cooldown.Check(Player.userID);
            GUI.Create.PlayerIndicator(Player);
            if (IsAdmin(Player))
            {
                Players.Admins.AddPlayer(Player.userID);
                GUI.Create.AdminIndicator(Player);
            }
            if (Players.Data.playerData.UnknownUsers.Contains(Player.userID))
            {
                UpdatePvXPlayerData(Player);
                Players.Data.playerData.UnknownUsers.Remove(Player.userID);
            }
            if (Players.Data.playerData.Sleepers.Contains(Player.userID))
            {
                UpdatePvXPlayerData(Player);
                Players.Data.playerData.Sleepers.Remove(Player.userID);
            }
            Players.Data.playerData.Info[Player.userID].LatestConnection = DateTimeStamp();
            UpdatePlayerChatTag(Player);
            SaveData.All();
            GUI.Create.PlayerIndicator(Player);
            if (Players.Data.playerData.Info[Player.userID].mode != Mode.NA) return;
            else if (ModeSwitch.Ticket.Data.ticketData.Notification.ContainsKey(Player.userID)) Messages.Chat(Players.Find.Iplayer(Player.userID), "TickClosLogin", ModeSwitch.Ticket.Data.ticketData.Notification[Player.userID]);
            else if (Players.Data.playerData.Info[Player.userID].mode == Mode.NA) Timers.PvxChatNotification(Player);
        }
        void OnPlayerDisconnected(BasePlayer Player)
        {
            if (IsAdmin(Player))
            {
                Players.Admins.RemovePlayer(Player.userID);
            }
            if (Players.Admins.Mode.ContainsPlayer(Player.userID))
            {
                Players.Admins.RemovePlayer(Player.userID);
            }
        }
        void OnPlayerRespawned(BasePlayer Player)
        {
        }

        void CheckPlayersRegistered()
        {
            foreach (BasePlayer Player in BasePlayer.activePlayerList)
                if (!(Players.Data.playerData.Info.ContainsKey(Player.userID)))
                    Players.Add(Player);

            foreach (BasePlayer Player in BasePlayer.sleepingPlayerList)
                if (!(Players.Data.playerData.Info.ContainsKey(Player.userID)))
                    Players.AddSleeper(Player);
        }


        static string pvxIndicator = "PvXPlayerStateIndicator";
        static string pvxMenuBack = "PvXMenu_Main_Background";
        static string pvxMenuBanner = "PvXMenu_Main_Banner";
        static string pvxMenuBannerTitle = "PvXMenu_Main_Banner_Title";
        static string pvxMenuBannerVersion = "PvXMenu_Main_Banner_Version";
        static string pvxMenuBannerModType = "PvXMenu_Main_Banner_ModType";
        static string pvxMenuSide = "PvXMenu_Main_Side";
        static string pvxMenuMain = "PvXMenu_Main";


        static string pvxIndicatorGui = "createPvXModeSelector";
        static string pvxPlayerUI = "pvxPlayerModeUI";
        static string pvxAdminUI = "pvxAdminTicketCountUI";
        static string UIMain = "PvXUI_Main";
        static string UIPanel = "PvXUI_Panel";
        static string[] GuiList = new string[] { pvxIndicatorGui,
            pvxPlayerUI, pvxAdminUI,
            UIMain, UIPanel, pvxMenuBanner,
            pvxIndicator, pvxMenuBack , pvxMenuMain,
            pvxMenuBannerTitle, pvxMenuBannerVersion,
            pvxMenuBannerModType
        };

        #endregion

        #region Config/Permision/Plugin Ref
        //None Adjustable
        private string PvXModtype = "Developer Edition";//Regular Oxide Donator Patreon Developer
        private bool DebugMode;
        //Core
        private bool TicketSystem;
        private bool CooldownSystem;
        private int CooldownTime;
        private float playerIndicatorMinWid;
        private float playerIndicatorMinHei;
        private float adminIndicatorMinWid;
        private float adminIndicatorMinHei;
        //Players
        private bool PvEAttackPvE;
        private bool PvEAttackPvP;
        private bool PvPAttackPvE;
        private bool PvPAttackPvP;
        private bool PvELootPvE;
        private bool PvELootPvP;
        private bool PvPLootPvE;
        private bool PvPLootPvP;
        private bool PvEUsePvPDoor;
        private bool PvPUsePvEDoor;
        private float PvEDamagePvE;
        private float PvEDamagePvP;
        private float PvPDamagePvE;
        private float PvPDamagePvP;
        private float PvEDamagePvEStruct;
        private float PvEDamagePvPStruct;
        private float PvPDamagePvEStruct;
        private float PvPDamagePvPStruct;
        //Metabolism
        private float PvEFoodLossRate;
        private float PvEWaterLossRate;
        private float PvEHealthGainRate;
        private float PvEFoodSpawn;
        private float PvEWaterSpawn;
        private float PvEHealthSpawn;
        private float PvPFoodLossRate;
        private float PvPWaterLossRate;
        private float PvPHealthGainRate;
        private float PvPFoodSpawn;
        private float PvPWaterSpawn;
        private float PvPHealthSpawn;
        //NPC
        private bool NPCAttackPvE;
        private bool NPCAttackPvP;
        private bool PvEAttackNPC;
        private bool PvPAttackNPC;
        private float NPCDamagePvE;
        private float NPCDamagePvP;
        private float PvEDamageNPC;
        private float PvPDamageNPC;
        private bool PvELootNPC;
        private bool PvPLootNPC;
        //Animal
        private float PvEDamageAnimals;
        private float PvPDamageAnimals;
        private float NPCDamageAnimals;
        private float AnimalsDamagePvE;
        private float AnimalsDamagePvP;
        private float AnimalsDamageNPC;
        //Turret
        private bool TurretPvETargetPvE;
        private bool TurretPvETargetPvP;
        private bool TurretPvPTargetPvE;
        private bool TurretPvPTargetPvP;
        private bool TurretPvETargetNPC;
        private bool TurretPvPTargetNPC;
        private bool TurretPvETargetAnimal;
        private bool TurretPvPTargetAnimal;
        private float TurretPvEDamagePvEAmnt;
        private float TurretPvEDamagePvPAmnt;
        private float TurretPvPDamagePvEAmnt;
        private float TurretPvPDamagePvPAmnt;
        private float TurretPvEDamageNPCAmnt;
        private float TurretPvPDamageNPCAmnt;
        private float TurretPvEDamageAnimalAmnt;
        private float TurretPvPDamageAnimalAmnt;
        //Helicopter
        private bool HeliTargetPvE;
        private bool HeliTargetPvP;
        private bool HeliTargetNPC;
        private float HeliDamagePvE;
        private float HeliDamagePvP;
        private float HeliDamageNPC;
        private float HeliDamagePvEStruct;
        private float HeliDamagePvPStruct;
        private float HeliDamageAnimal;
        private float HeliDamageByPvE;
        private float HeliDamageByPvP;
        //Fire
        private float FireDamagePvE;
        private float FireDamagePvP;
        private float FireDamageNPC;
        private float FireDamagePvEStruc;
        private float FireDamagePvPStruc;
        //Others
        public static bool DisableUI_FadeIn;
        private string ChatPrefixColor;
        private string ChatPrefix;
        private string ChatMessageColor;
        private string PvXNAColour;
        private string PvXPvEColour;
        private string PvXPvPColour;
        /*
         *
         * END Of Config
         *  
        */

        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new configuration file!");
            Config.Clear();
            LoadVariables();
        }
        void LoadVariables() //Stores Default Values, calling GetConfig passing: menu, dataValue, defaultValue
        {
            //Core
            TicketSystem = Convert.ToBoolean(GetConfig(ConfigLists.Core, "01: Ticket System", true));
            CooldownSystem = Convert.ToBoolean(GetConfig(ConfigLists.Core, "02: Cooldown System", true));
            CooldownTime = Convert.ToInt32(GetConfig(ConfigLists.Core, "03: Cooldown Time (Seconds)", 3600));
            playerIndicatorMinWid = Convert.ToSingle(GetConfig(ConfigLists.Core, "04: player Gui width anchor", 0.484));
            playerIndicatorMinHei = Convert.ToSingle(GetConfig(ConfigLists.Core, "05: player Gui height anchor", 0.111));
            adminIndicatorMinHei = Convert.ToSingle(GetConfig(ConfigLists.Core, "06: admin Gui height anchor", 0.055));
            adminIndicatorMinWid = Convert.ToSingle(GetConfig(ConfigLists.Core, "07: admin Gui width anchor", 0.166));

            //Players
            PvEAttackPvE = Convert.ToBoolean(GetConfig(ConfigLists.Player, "01: PvE v PvE", false));
            PvEAttackPvP = Convert.ToBoolean(GetConfig(ConfigLists.Player, "02: PvE v PvP", false));
            PvPAttackPvE = Convert.ToBoolean(GetConfig(ConfigLists.Player, "03: PvP v PvE", false));
            PvPAttackPvP = Convert.ToBoolean(GetConfig(ConfigLists.Player, "04: PvP v PvP", true));
            PvELootPvE = Convert.ToBoolean(GetConfig(ConfigLists.Player, "05: PvE Loot PvE", true));
            PvELootPvP = Convert.ToBoolean(GetConfig(ConfigLists.Player, "06: PvE Loot PvP", false));
            PvPLootPvE = Convert.ToBoolean(GetConfig(ConfigLists.Player, "07: PvP Loot PvE", false));
            PvPLootPvP = Convert.ToBoolean(GetConfig(ConfigLists.Player, "08: PvP Loot PvP", true));
            PvEUsePvPDoor = Convert.ToBoolean(GetConfig(ConfigLists.Player, "09: PvE Use PvPDoor", false));
            PvPUsePvEDoor = Convert.ToBoolean(GetConfig(ConfigLists.Player, "10: PvP Use PvEDoor", false));
            PvEDamagePvE = Convert.ToSingle(GetConfig(ConfigLists.Player, "11: PvE Damage PvE", 0.0));
            PvEDamagePvP = Convert.ToSingle(GetConfig(ConfigLists.Player, "12: PvE Damage PvP", 0.0));
            PvPDamagePvE = Convert.ToSingle(GetConfig(ConfigLists.Player, "13: PvP Damage PvE", 0.0));
            PvPDamagePvP = Convert.ToSingle(GetConfig(ConfigLists.Player, "14: PvP Damage PvP", 1.0));
            PvEDamagePvEStruct = Convert.ToSingle(GetConfig(ConfigLists.Player, "18: PvEDamagePvEStruct", 0.0));
            PvEDamagePvPStruct = Convert.ToSingle(GetConfig(ConfigLists.Player, "18: PvEDamagePvPStruct", 0.0));
            PvPDamagePvEStruct = Convert.ToSingle(GetConfig(ConfigLists.Player, "18: PvPDamagePvEStruct", 0.0));
            PvPDamagePvPStruct = Convert.ToSingle(GetConfig(ConfigLists.Player, "18: PvPDamagePvPStruct", 1.0));
            //Metabolism
            PvEFoodLossRate = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "01: PvEFoodLossRate", 0.03));
            PvEWaterLossRate = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "02: PvEWaterLossRate", 0.03));
            PvEHealthGainRate = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "03: PvEHealthGainRate", 0.03));
            PvEFoodSpawn = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "04: PvEFoodSpawn", 100.0));
            PvEWaterSpawn = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "05: PvEWaterSpawn", 250.00));
            PvEHealthSpawn = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "06: PvEHealthSpawn", 500.00));
            PvPFoodLossRate = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "07: PvPFoodLossRate", 0.03));
            PvPWaterLossRate = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "08: PvPWaterLossRate", 0.03));
            PvPHealthGainRate = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "09: PvPHealthGainRate", 0.03));
            PvPFoodSpawn = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "10: PvPFoodSpawn", 100.0));
            PvPWaterSpawn = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "11: PvPWaterSpawn", 250.0));
            PvPHealthSpawn = Convert.ToSingle(GetConfig(ConfigLists.Metabolism, "12: PvPHealthSpawn", 500.0));
            //NPC
            NPCAttackPvE = Convert.ToBoolean(GetConfig(ConfigLists.NPC, "01: NPC Attack PvE", true));
            NPCAttackPvP = Convert.ToBoolean(GetConfig(ConfigLists.NPC, "02: NPC Attack PvP", true));
            PvEAttackNPC = Convert.ToBoolean(GetConfig(ConfigLists.NPC, "03: PvE Attack NPC", true));
            PvPAttackNPC = Convert.ToBoolean(GetConfig(ConfigLists.NPC, "04: PvP Attack NPC", true));
            NPCDamagePvE = Convert.ToSingle(GetConfig(ConfigLists.NPC, "05: NPC Damage PvE", 1.0));
            NPCDamagePvP = Convert.ToSingle(GetConfig(ConfigLists.NPC, "06: NPC Damage PvP", 1.0));
            PvEDamageNPC = Convert.ToSingle(GetConfig(ConfigLists.NPC, "07: PvE Damage NPC", 1.0));
            PvPDamageNPC = Convert.ToSingle(GetConfig(ConfigLists.NPC, "08: PvP Damage NPC", 1.0));
            PvELootNPC = Convert.ToBoolean(GetConfig(ConfigLists.NPC, "09: PvE Loot NPC", true));
            PvPLootNPC = Convert.ToBoolean(GetConfig(ConfigLists.NPC, "10: PvP Loot NPC", true));
            //Animal
            PvEDamageAnimals = Convert.ToSingle(GetConfig(ConfigLists.Animals, "1: PvE Damage Animals", 1.0f));
            PvPDamageAnimals = Convert.ToSingle(GetConfig(ConfigLists.Animals, "2: PvP Damage Animals", 1.0f));
            NPCDamageAnimals = Convert.ToSingle(GetConfig(ConfigLists.Animals, "3: NPC Damage Animals", 1.0f));
            AnimalsDamagePvE = Convert.ToSingle(GetConfig(ConfigLists.Animals, "4: Animals Damage PvE", 1.0f));
            AnimalsDamagePvP = Convert.ToSingle(GetConfig(ConfigLists.Animals, "5: Animals Damage PvP", 1.0f));
            AnimalsDamageNPC = Convert.ToSingle(GetConfig(ConfigLists.Animals, "6: Animals Damage NPC", 1.0f));
            //Turret
            TurretPvETargetPvE = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "01: TurretPvETargetPvE", true));
            TurretPvETargetPvP = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "02: TurretPvETargetPvP", false));
            TurretPvPTargetPvE = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "03: TurretPvPTargetPvE", false));
            TurretPvPTargetPvP = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "04: TurretPvPTargetPvP", true));
            TurretPvETargetNPC = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "05: TurretPvETargetNPC", true));
            TurretPvPTargetNPC = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "06: TurretPvPTargetNPC", true));
            TurretPvETargetAnimal = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "07: TurretPvETargetAnimal", true));
            TurretPvPTargetAnimal = Convert.ToBoolean(GetConfig(ConfigLists.Turret, "08: TurretPvPTargetAnimal", true));
            TurretPvEDamagePvEAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "09: TurretPvEDamagePvEAmnt", 1.0f));
            TurretPvEDamagePvPAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "10: TurretPvEDamagePvPAmnt", 0.0f));
            TurretPvPDamagePvEAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "11: TurretPvPDamagePvEAmnt", 0.0f));
            TurretPvPDamagePvPAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "12: TurretPvPDamagePvPAmnt", 1.0f));
            TurretPvEDamageNPCAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "13: TurretPvEDamageNPCAmnt", 1.0f));
            TurretPvPDamageNPCAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "14: TurretPvPDamageNPCAmnt", 1.0f));
            TurretPvEDamageAnimalAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "15: TurretPvEDamageAnimal", 1.0f));
            TurretPvPDamageAnimalAmnt = Convert.ToSingle(GetConfig(ConfigLists.Turret, "16: TurretPvPDamageAnimal", 1.0f));
            //Helicopter
            HeliTargetPvE = Convert.ToBoolean(GetConfig(ConfigLists.Heli, "01: HeliTargetPvE", false));
            HeliTargetPvP = Convert.ToBoolean(GetConfig(ConfigLists.Heli, "02: HeliTargetPvP", true));
            HeliTargetNPC = Convert.ToBoolean(GetConfig(ConfigLists.Heli, "03: HeliTargetNPC", false));
            HeliDamagePvE = Convert.ToSingle(GetConfig(ConfigLists.Heli, "04: HeliDamagePvE", 0.0));
            HeliDamagePvP = Convert.ToSingle(GetConfig(ConfigLists.Heli, "05: HeliDamagePvP", 1.0));
            HeliDamageNPC = Convert.ToSingle(GetConfig(ConfigLists.Heli, "06: HeliDamageNPC", 0.0));
            HeliDamagePvEStruct = Convert.ToSingle(GetConfig(ConfigLists.Heli, "07: HeliDamagePvEStruct", 0.0));
            HeliDamagePvPStruct = Convert.ToSingle(GetConfig(ConfigLists.Heli, "08: HeliDamagePvPStruct", 1.0));
            HeliDamageAnimal = Convert.ToSingle(GetConfig(ConfigLists.Heli, "09: HeliDamageAnimal", 1.0));
            HeliDamageByPvE = Convert.ToSingle(GetConfig(ConfigLists.Heli, "10: HeliDamageByPvE", 0.0));
            HeliDamageByPvP = Convert.ToSingle(GetConfig(ConfigLists.Heli, "11: HeliDamageByPvp", 1.0));
            //fire
            FireDamagePvE = Convert.ToSingle(GetConfig(ConfigLists.Fire, "1: FireDamagePvE", 0.1));
            FireDamagePvP = Convert.ToSingle(GetConfig(ConfigLists.Fire, "2: FireDamagePvP", 1.0));
            FireDamageNPC = Convert.ToSingle(GetConfig(ConfigLists.Fire, "3: FireDamageNPC", 1.0));
            FireDamagePvEStruc = Convert.ToSingle(GetConfig(ConfigLists.Fire, "4: FireDamagePvEStruc", 0.0));
            FireDamagePvPStruc = Convert.ToSingle(GetConfig(ConfigLists.Fire, "5: FireDamagePvPStruc", 1.0));
            //others
            DisableUI_FadeIn = Convert.ToBoolean(GetConfig(ConfigLists.Settings, "01: DisableUI Fadein", false));
            DebugMode = Convert.ToBoolean(GetConfig(ConfigLists.Settings, "02: DebugMode", false));
            ChatPrefix = Convert.ToString(GetConfig(ConfigLists.Core, "03: ChatPrefix", "PvX"));
            ChatPrefixColor = Convert.ToString(GetConfig(ConfigLists.Settings, "04: ChatPrefixColor", "008800"));
            ChatMessageColor = Convert.ToString(GetConfig(ConfigLists.Settings, "05: ChatMessageColor", "yellow"));
            PvXNAColour = Convert.ToString(GetConfig(ConfigLists.Settings, "06: PvXNAColour", "#yellow"));
            PvXPvEColour = Convert.ToString(GetConfig(ConfigLists.Settings, "07: PvXPvEColour", "#green"));
            PvXPvPColour = Convert.ToString(GetConfig(ConfigLists.Settings, "08: PvXPvPColour", "#red"));
        }

        object GetConfig(string menu, string dataValue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(dataValue, out value))
            {
                value = defaultValue;
                data[dataValue] = value;
            }
            return value;
        }
        T GetConfig<T>(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);

            if (Config.Get(stringArgs.ToArray()) == null)
            {
                Config.Set(args);
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        private bool HasPerm(BasePlayer Player, string perm, string reason = null)
        {
            string regPerm = Title.ToLower() + "." + perm; //pvxselector.admin
            if (permission.UserHasPermission(Player.UserIDString, regPerm)) return true;
            if (reason != "null")
                SendReply(Player, reason);
            return false;
        }
        private bool IsMod(BasePlayer Player)
        {
            string regPerm = Title.ToLower() + "." + "moderator";
            if (permission.UserHasPermission(Player.UserIDString, regPerm)) return true;
            else if (IsAdmin(Player)) return true;
            else return false;
        }
        private bool IsAdmin(BasePlayer Player)
        {
            string regPerm = Title.ToLower() + "." + "admin";
            if (permission.UserHasPermission(Player.UserIDString, regPerm)) return true;
            else return false;
        }

        void PermissionHandle()
        {
            string[] Permissionarray = { "admin", "moderator", "wipe" }; //DO NOT EVER TOUCH THIS EVER!!!!!!
            foreach (string i in Permissionarray)
            {
                string regPerm = Title.ToLower() + "." + i;
                Puts("Checking if " + regPerm + " is registered.");
                if (!permission.PermissionExists(regPerm))
                {
                    permission.RegisterPermission(regPerm, this);
                    Puts(regPerm + " is registered.");
                }
                else
                {
                    Puts(regPerm + " is already registered.");
                }
            }
        }

        class ConfigLists
        {
            public static readonly ConfigLists Core = new ConfigLists("1: Core", 1, MenuItemEnum.Core);
            public static readonly ConfigLists Player = new ConfigLists("2: Player", 2, MenuItemEnum.Player);
            public static readonly ConfigLists Metabolism = new ConfigLists("3: Metabolism", 3, MenuItemEnum.Metabolism);
            public static readonly ConfigLists NPC = new ConfigLists("4: NPC", 4, MenuItemEnum.NPC);
            public static readonly ConfigLists Animals = new ConfigLists("5: Animals", 5, MenuItemEnum.Animals);
            public static readonly ConfigLists Turret = new ConfigLists("6: Turrets", 6, MenuItemEnum.Turret);
            public static readonly ConfigLists Heli = new ConfigLists("7: Heli", 7, MenuItemEnum.Heli);
            public static readonly ConfigLists Fire = new ConfigLists("8: Fire", 8, MenuItemEnum.Fire);
            public static readonly ConfigLists Settings = new ConfigLists("9: Settings", 9, MenuItemEnum.Settings);

            public string Value;
            public int Index;
            public MenuItemEnum EnumValue;

            private ConfigLists(string value, int index, MenuItemEnum enumValue)
            {
                Value = value;
                Index = index;
                EnumValue = enumValue; //possible to remove extra value
            }

            public static implicit operator string(ConfigLists configLists)
            {
                return configLists.Value;
            }
        }
        public enum MenuItemEnum //Possible to remove exta value
        {
            Core,
            Player,
            Metabolism,
            NPC,
            Animals,
            Turret,
            Heli,
            Fire,
            Settings
        }
        #endregion

        #region UI Creation
        class QUI
        {
            public static CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool useCursor = false, string parent = "Hud")
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
            public static void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            public static void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, float fadein = 1.0f, TextAnchor align = TextAnchor.MiddleCenter)
            {
                if (DisableUI_FadeIn)
                    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            public static void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, float fadein = 1.0f, TextAnchor align = TextAnchor.MiddleCenter)
            {
                if (DisableUI_FadeIn)
                    fadein = 0;
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
            public static void LoadImage(ref CuiElementContainer container, string panel, string png, string aMin, string aMax)
            {
                container.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }
            public static void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                if (DisableUI_FadeIn)
                    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
        }
        class UIColours
        {
            public static readonly UIColours Black_100 = new UIColours("0.00 0.00 0.00 1.00"); //Black
            public static readonly UIColours Black_050 = new UIColours("0.00 0.00 0.00 0.50");
            public static readonly UIColours Black_015 = new UIColours("0.00 0.00 0.00 0.15");
            public static readonly UIColours Grey2_100 = new UIColours("0.20 0.20 0.20 1.00"); //Grey 2
            public static readonly UIColours Grey2_050 = new UIColours("0.20 0.20 0.20 0.50");
            public static readonly UIColours Grey2_015 = new UIColours("0.20 0.20 0.20 0.15");
            public static readonly UIColours Grey5_100 = new UIColours("0.50 0.50 0.50 1.00"); //Grey 5
            public static readonly UIColours Grey5_050 = new UIColours("0.50 0.50 0.50 0.50");
            public static readonly UIColours Grey5_015 = new UIColours("0.50 0.50 0.50 0.15");
            public static readonly UIColours Grey8_100 = new UIColours("0.80 0.80 0.80 1.00"); //Grey 8
            public static readonly UIColours Grey8_050 = new UIColours("0.80 0.80 0.80 0.50");
            public static readonly UIColours Grey8_015 = new UIColours("0.80 0.80 0.80 0.15");
            public static readonly UIColours White_100 = new UIColours("1.00 1.00 1.00 1.00"); //White
            public static readonly UIColours White_050 = new UIColours("1.00 1.00 1.00 0.50");
            public static readonly UIColours White_015 = new UIColours("1.00 1.00 1.00 0.15");
            public static readonly UIColours Red_100 = new UIColours("0.70 0.20 0.20 1.00");   //Red
            public static readonly UIColours Red_050 = new UIColours("0.70 0.20 0.20 0.50");
            public static readonly UIColours Red_015 = new UIColours("0.70 0.20 0.20 0.15");
            public static readonly UIColours Green_100 = new UIColours("0.20 0.70 0.20 1.00");  //Green
            public static readonly UIColours Green_050 = new UIColours("0.20 0.70 0.20 0.50");
            public static readonly UIColours Green_015 = new UIColours("0.20 0.70 0.20 0.15");
            public static readonly UIColours Blue_100 = new UIColours("0.20 0.20 0.70 1.00");  //Blue
            public static readonly UIColours Blue_050 = new UIColours("0.20 0.20 0.70 0.50");
            public static readonly UIColours Blue_015 = new UIColours("0.20 0.20 0.70 0.15");
            public static readonly UIColours Yellow_100 = new UIColours("0.90 0.90 0.20 1.00");  //Yellow
            public static readonly UIColours Yellow_050 = new UIColours("0.90 0.90 0.20 0.50");
            public static readonly UIColours Yellow_015 = new UIColours("0.90 0.90 0.20 0.15");
            public static readonly UIColours Gold_100 = new UIColours("0.745 0.550 0.045 1.00"); //Gold

            public string Value;
            public int Index;

            private UIColours(string value)
            {
                Value = value;
            }

            public static implicit operator string(UIColours uiColours)
            {
                return uiColours.Value;
            }
        }

        private void DestroyAllPvXUI(BasePlayer player)
        {
            foreach (string _v in GuiList)
            {
                CuiHelper.DestroyUi(player, _v);
            }
            //DestroyEntries(player);
        }
        private void DestroyPvXUI(BasePlayer player, string _ui)
        {
            CuiHelper.DestroyUi(player, _ui);
        }
        #endregion

        #region GUIs
        class GUI
        {
            public static void CreateSignature(ref CuiElementContainer container, string panel, string color, string text, int size, float fadein = 1.0f, TextAnchor align = TextAnchor.LowerLeft)
            {
                float widthPadding = 0.017f;
                float heightPadding = 0.013f;

                float textWidth = 0.966f;
                float textHeight = 0.040f;

                float minWidth = widthPadding;
                float maxWidth = widthPadding + textWidth;
                float minHeight = heightPadding;
                float maxHeight = heightPadding + textHeight;

                QUI.CreateLabel(ref container,
                    panel,
                    color,
                    text,
                    size,
                    $"{minWidth} {minHeight}",
                    $"{maxWidth} {maxHeight}",
                    fadein,
                    align);
            }

            public static class Create
            {
                public static void AdminIndicator(BasePlayer Player)
                {
                    Vector2 dimension = new Vector2(0.174F, 0.028F);
                    Vector2 posMin = new Vector2(instance.adminIndicatorMinWid, instance.adminIndicatorMinHei);
                    Vector2 posMax = posMin + dimension;
                    var adminCountContainer = QUI.CreateElementContainer(
                        pvxAdminUI,
                        UIColours.Black_050,
                        $"{posMin.x} {posMin.y}",
                        $"{posMax.x} {posMax.y}");
                    QUI.CreateLabel(ref adminCountContainer, pvxAdminUI, UIColours.White_100, "PvX Tickets", 10, "0.0 0.1", "0.3 0.90");
                    QUI.CreateLabel(ref adminCountContainer, pvxAdminUI, UIColours.White_100, string.Format("Open: {0}", ModeSwitch.Ticket.Data.ticketData.Tickets.Count.ToString()), 10, "0.301 0.1", "0.65 0.90");
                    QUI.CreateLabel(ref adminCountContainer, pvxAdminUI, UIColours.White_100, string.Format("Closed: {0}", ModeSwitch.Ticket.Data.logData.Logs.Count.ToString()), 10, "0.651 0.1", "1 0.90");

                    CuiHelper.AddUi(Player, adminCountContainer);
                }
                public static void PlayerIndicator(BasePlayer Player)
                {
                    Vector2 dimension = new Vector2(0.031F, 0.028F);
                    Vector2 posMin = new Vector2(instance.playerIndicatorMinWid, instance.playerIndicatorMinHei);
                    Vector2 posMax = posMin + dimension;
                    var indicatorContainer = QUI.CreateElementContainer(
                        pvxIndicator,
                        UIColours.Black_050,
                        "0.48 0.11",
                        "0.52 0.14"
                        );
                    if (Players.Data.playerData.Info[Player.userID].mode == Mode.NA)
                        indicatorContainer = QUI.CreateElementContainer(
                            pvxIndicator,
                            UIColours.Red_100,
                            "0.48 0.11",
                            "0.52 0.14");
                    else if (ModeSwitch.Ticket.Data.ticketData.Tickets.ContainsKey(Player.userID))
                        indicatorContainer = QUI.CreateElementContainer(
                            pvxIndicator,
                            UIColours.Yellow_015,
                            "0.48 0.11",
                            "0.52 0.14");
                    if (Players.Admins.Mode.ContainsPlayer(Player.userID))
                    {
                        QUI.CreateLabel(
                            ref indicatorContainer,
                            pvxIndicator,
                            UIColours.Green_100,
                            Players.Data.playerData.Info[Player.userID].mode,
                            15,
                            "0.1 0.1",
                            "0.90 0.99");
                    }
                    else
                    {
                        QUI.CreateLabel(ref indicatorContainer,
                            pvxIndicator,
                            UIColours.White_100,
                            Players.Data.playerData.Info[Player.userID].mode,
                            15,
                            "0.1 0.1",
                            "0.90 0.99");
                    }
                    CuiHelper.AddUi(Player, indicatorContainer);
                }

                public static void MenuButton(ref CuiElementContainer container, string panel, string color, string text, int size, int location, string command, float fadein = 1.0f, TextAnchor align = TextAnchor.MiddleCenter)
                {
                    float widthPadding = 0.108f;
                    float heightPadding = 0.013f;

                    float buttonWidth = 0.784f;
                    float buttonHeight = 0.107f;

                    float buttonPadding = 0.027f;

                    float minWidth = widthPadding;
                    float maxWidth = widthPadding + buttonWidth;
                    float minHeight = 1.00f - (buttonHeight + heightPadding + ((buttonHeight + buttonPadding) * location));
                    float maxHeight = 1.00f - (heightPadding + ((buttonHeight + buttonPadding) * location));

                    QUI.CreateButton(ref container,
                        panel,
                        color,
                        text,
                        size,
                        $"{minWidth} {minHeight}",
                        $"{maxWidth} {maxHeight}",
                        command,
                        fadein,
                        align);
                }
                public static void MenuText(ref CuiElementContainer container, string panel, string color, string text, int size, int location, float fadein = 1.0f, TextAnchor align = TextAnchor.LowerLeft)
                {
                    float widthPadding = 0.017f;
                    float heightPadding = 0.013f;

                    float textWidth = 0.966f;
                    float textHeight = 0.040f;

                    float textPadding = 0.001f;

                    float minWidth = widthPadding;
                    float maxWidth = widthPadding + textWidth;
                    float minHeight = 1.00f - (textHeight + heightPadding + ((textHeight + textPadding) * location));
                    float maxHeight = 1.00f - (heightPadding + ((textHeight + textPadding) * location));

                    QUI.CreateLabel(ref container,
                        panel,
                        color,
                        text,
                        size,
                        $"{minWidth} {minHeight}",
                        $"{maxWidth} {maxHeight}",
                        fadein,
                        align);
                }
                public static void ContentButton(ref CuiElementContainer container, string panel, string color, string text, int size, int location, string command, float fadein = 1.0f, TextAnchor align = TextAnchor.MiddleCenter)
                {
                    float widthPadding = 0.017f;
                    float heightPadding = 0.013f;

                    float buttonWidth = 0.1724f;
                    float buttonHeight = 0.068f;

                    float buttonPadding = 0.017f;

                    float minWidth = 1f - (buttonPadding + buttonWidth + ((buttonWidth + buttonPadding) * location));
                    float maxWidth = 1f - (buttonPadding + ((buttonWidth + buttonPadding) * location));
                    float minHeight = heightPadding;
                    float maxHeight = widthPadding + buttonHeight;

                    QUI.CreateButton(ref container,
                        panel,
                        color,
                        text,
                        size,
                        $"{minWidth} {minHeight}",
                        $"{maxWidth} {maxHeight}",
                        command,
                        fadein,
                        align);
                }

                public static class Menu
                {
                    public static void Background(BasePlayer player)
                    {
                        var MainGui = QUI.CreateElementContainer(pvxMenuBack, UIColours.Black_100, "0.297 0.125", "0.703 0.958", true);
                        CuiHelper.AddUi(player, MainGui);
                    }
                    public static void Title(BasePlayer player)
                    {
                        var BannerGui = QUI.CreateElementContainer(pvxMenuBanner, UIColours.Grey2_100, "0.297 0.824", "0.703 0.958", true);

                        var BannerTitle = QUI.CreateElementContainer(pvxMenuBannerTitle, UIColours.Grey5_100, "0.302 0.833", "0.495 0.949");
                        QUI.CreateLabel(ref BannerTitle, pvxMenuBannerTitle, UIColours.Black_100, "PvX Selector", 35, "0 0", "1 1", 1);

                        var BannerVersion = QUI.CreateElementContainer(pvxMenuBannerVersion, UIColours.Grey5_100, "0.500 0.833", "0.594 0.949");
                        QUI.CreateLabel(ref BannerVersion, pvxMenuBannerVersion, UIColours.Black_100, "Version", 25, "0.0 0.6", "1.0 1.0", 1);
                        QUI.CreateLabel(ref BannerVersion, pvxMenuBannerVersion, UIColours.Black_100, instance.Version.ToString(), 21, "0.0 0.0", "1.0 0.6", 1);

                        var BannerModType = QUI.CreateElementContainer(pvxMenuBannerModType, UIColours.Gold_100, "0.599 0.833", "0.698 0.949");
                        QUI.CreateLabel(ref BannerModType, pvxMenuBannerModType, UIColours.Black_100,
                            instance.PvXModtype, 25, "0.0 0.0", "1.0 1.0", 1);

                        CuiHelper.AddUi(player, BannerGui);
                        CuiHelper.AddUi(player, BannerTitle);
                        CuiHelper.AddUi(player, BannerVersion);
                        CuiHelper.AddUi(player, BannerModType);
                    }
                    public static void Selector(BasePlayer player)
                    {
                        var SideMenuGui = QUI.CreateElementContainer(pvxMenuSide, UIColours.Grey2_100, "0.607 0.125", "0.703 0.815", true);

                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Grey5_100, "<color=black>Welcome</color>", 15, 0, "PvXMenuCmd Welcome");
                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Grey5_100, "<color=black>Settings</color>", 15, 1, "PvXMenuCmd Settings");
                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Grey5_100, "<color=black>Character</color>", 15, 2, "PvXMenuCmd Character");
                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Green_100, "<color=black>Players</color>", 15, 3, "PvXMenuCmd Players");
                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Green_100, "<color=black>Tickets</color>", 15, 4, "PvXMenuCmd Tickets");
                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Green_100, "<color=black>Settings</color>", 15, 5, "PvXMenuCmd Admin Settings");
                        GUI.Create.MenuButton(ref SideMenuGui, pvxMenuSide, UIColours.Green_100, "<color=black>Debug</color>", 15, 6, "PvXMenuCmd Admin Debug");
                        QUI.CreateButton(ref SideMenuGui, pvxMenuSide, UIColours.Red_100, "<color=black>X</color>", 15, "0.108 0.013", "0.892 0.047", "PvXMenuCmd Close");
                        CuiHelper.AddUi(player, SideMenuGui);
                    }
                    public static class Content
                    {
                        public static class WelcomePages
                        {
                            public static void Page1(BasePlayer Player)
                            {
                                var MenuGui = QUI.CreateElementContainer(
                                    pvxMenuMain,
                                    UIColours.Grey2_100,
                                    "0.297 0.125",
                                    "0.602 0.815",
                                    true);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L01), 16, 0);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L02), 16, 1);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L03), 16, 2);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L04), 16, 3);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L05), 16, 4);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L06), 16, 5);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L07), 16, 6);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L08), 16, 7);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L09), 16, 8);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L10), 16, 9);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L11), 16, 10);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L12), 16, 11);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L13), 16, 12);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L14), 16, 13);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L15), 16, 14);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L16), 16, 15);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L17), 16, 16);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L18), 16, 17);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L19), 16, 18);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L20), 16, 19);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P1L21), 16, 20);
                                CreateSignature(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Created by Alphawar", 16, 20);
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, ">", 20, 0, "PvXMenuCmd WelcomePage 1 Next");
                                CuiHelper.AddUi(Player, MenuGui);
                            }
                            public static void Page2(BasePlayer Player)
                            {
                                var MenuGui = QUI.CreateElementContainer(
                                    pvxMenuMain,
                                    UIColours.Grey2_100,
                                    "0.297 0.125",
                                    "0.602 0.815",
                                    true);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L01), 16, 0);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L02), 16, 1);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L03), 16, 2);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L04), 16, 3);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L05), 16, 4);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L06), 16, 5);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L07), 16, 6);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L08), 16, 7);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L09), 16, 8);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L10), 16, 9);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L11), 16, 10);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L12), 16, 11);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L13), 16, 12);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L14), 16, 13);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L15), 16, 14);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L16), 16, 15);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L17), 16, 16);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L18), 16, 17);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L19), 16, 18);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L20), 16, 19);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P2L21), 16, 20);
                                CreateSignature(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Created by Alphawar", 16, 20);
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, ">", 20, 0, "PvXMenuCmd WelcomePage 2 Next");
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, "<", 20, 1, "PvXMenuCmd WelcomePage 2 Back");
                                CuiHelper.AddUi(Player, MenuGui);
                            }
                            public static void Page3(BasePlayer Player)
                            {
                                var MenuGui = QUI.CreateElementContainer(
                                    pvxMenuMain,
                                    UIColours.Grey2_100,
                                    "0.297 0.125",
                                    "0.602 0.815",
                                    true);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L01), 16, 0);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L02), 16, 1);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L03), 16, 2);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L04), 16, 3);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L05), 16, 4);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L06), 16, 5);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L07), 16, 6);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L08), 16, 7);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L09), 16, 8);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L10), 16, 9);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L11), 16, 10);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L12), 16, 11);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L13), 16, 12);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L14), 16, 13);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L15), 16, 14);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L16), 16, 15);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L17), 16, 16);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L18), 16, 17);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L19), 16, 18);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L20), 16, 19);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.P3L21), 16, 20);
                                CreateSignature(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Created by Alphawar", 16, 20);
                                if (instance.IsMod(Player)) Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, ">", 20, 0, "PvXMenuCmd WelcomePage 3 Next");
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, "<", 20, 1, "PvXMenuCmd WelcomePage 3 Back");
                                CuiHelper.AddUi(Player, MenuGui);
                            }
                            public static void Page4(BasePlayer Player)
                            {
                                var MenuGui = QUI.CreateElementContainer(
                                    pvxMenuMain,
                                    UIColours.Grey2_100,
                                    "0.297 0.125",
                                    "0.602 0.815",
                                    true);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L01), 16, 0);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L02), 16, 1);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L03), 16, 2);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L04), 16, 3);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L05), 16, 4);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L06), 16, 5);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L07), 16, 6);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L08), 16, 7);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L09), 16, 8);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L10), 16, 9);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L11), 16, 10);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L12), 16, 11);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L13), 16, 12);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L14), 16, 13);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L15), 16, 14);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L16), 16, 15);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L17), 16, 16);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L18), 16, 17);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L19), 16, 18);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L20), 16, 19);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP1L21), 16, 20);
                                CreateSignature(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Created by Alphawar", 16, 20);
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, ">", 20, 0, "PvXMenuCmd WelcomePage 4 Next");
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, "<", 20, 1, "PvXMenuCmd WelcomePage 4 Back");
                                CuiHelper.AddUi(Player, MenuGui);
                            }
                            public static void Page5(BasePlayer Player)
                            {
                                var MenuGui = QUI.CreateElementContainer(
                                    pvxMenuMain,
                                    UIColours.Grey2_100,
                                    "0.297 0.125",
                                    "0.602 0.815",
                                    true);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L01), 16, 0);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L02), 16, 1);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L03), 16, 2);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L04), 16, 3);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L05), 16, 4);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L06), 16, 5);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L07), 16, 6);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L08), 16, 7);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L09), 16, 8);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L10), 16, 9);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L11), 16, 10);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L12), 16, 11);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L13), 16, 12);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L14), 16, 13);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L15), 16, 14);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L16), 16, 15);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L17), 16, 16);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L18), 16, 17);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L19), 16, 18);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L20), 16, 19);
                                Create.MenuText(ref MenuGui, pvxMenuMain, UIColours.Black_100, Messages.Get(Lang.Menu.WelcomePage.AP2L21), 16, 20);
                                CreateSignature(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Created by Alphawar", 16, 20);
                                Create.ContentButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, "<", 20, 1, "PvXMenuCmd WelcomePage 5 Back");
                                CuiHelper.AddUi(Player, MenuGui);
                            }
                        }

                        public static void Character(BasePlayer Player)
                        {
                            var MenuGui = QUI.CreateElementContainer(
                                pvxMenuMain,
                                UIColours.Grey2_100,
                                "0.297 0.125",
                                "0.602 0.815",
                                true);
                            String PvE = Mode.PvE;
                            String PvP = Mode.PvP;

                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "Player Info", 50, "0.034 0.852", "0.966 0.973");

                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.034 0.758", "0.379 0.826");
                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Name:", 18, "0.052 0.758", "0.379 0.826", 1, TextAnchor.MiddleLeft);
                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.397 0.758", "0.966 0.826");
                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Black_100, Player.displayName, 18, "0.397 0.758", "0.966 0.826");

                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.034 0.678", "0.379 0.745");
                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Steam ID:", 18, "0.052 0.678", "0.379 0.745", 1, TextAnchor.MiddleLeft);
                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.397 0.678", "0.966 0.745");
                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Black_100, Player.UserIDString, 18, "0.397 0.678", "0.966 0.745");

                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.034 0.597", "0.379 0.664");
                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Black_100, "Current Mode:", 18, "0.052 0.597", "0.379 0.664", 1, TextAnchor.MiddleLeft);
                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.397 0.597", "0.966 0.664");
                            QUI.CreateLabel(ref MenuGui, pvxMenuMain, UIColours.Black_100, Players.Data.playerData.Info[Player.userID].mode, 18, "0.397 0.597", "0.966 0.664");


                            QUI.CreatePanel(ref MenuGui, pvxMenuMain, UIColours.Grey5_100, "0.034 0.027", "0.966 0.255");
                            QUI.CreateButton(ref MenuGui, pvxMenuMain, UIColours.Blue_100, "Set Shared Chest", 22, "0.052 0.188", "0.491 0.242", "PvXMenuCmd AddToShared");
                            QUI.CreateButton(ref MenuGui, pvxMenuMain, UIColours.Blue_100, "remove Shared Chest", 22, "0.509 0.188", "0.948 0.242", "PvXMenuCmd RemoveFromShared");
                            QUI.CreateButton(ref MenuGui, pvxMenuMain, UIColours.Green_100, Mode.PvE, 22, "0.052 0.040", "0.491 0.174", $"PvXMenuCmd {PvE}");
                            QUI.CreateButton(ref MenuGui, pvxMenuMain, UIColours.Red_100, Mode.PvP, 22, "0.509 0.040", "0.948 0.174", $"PvXMenuCmd {PvP}");

                            CuiHelper.AddUi(Player, MenuGui);
                        }

                        public static void Serversettings(BasePlayer Player)
                        {

                        }
                    }
                }
            }
            public static class Destroy
            {
                public static void All(BasePlayer Player)
                {
                    CuiHelper.DestroyUi(Player, pvxMenuBack);
                    CuiHelper.DestroyUi(Player, pvxMenuBanner);
                    CuiHelper.DestroyUi(Player, pvxMenuBannerTitle);
                    CuiHelper.DestroyUi(Player, pvxMenuBannerVersion);
                    CuiHelper.DestroyUi(Player, pvxMenuBannerModType);
                    CuiHelper.DestroyUi(Player, pvxMenuSide);
                    CuiHelper.DestroyUi(Player, pvxMenuMain);
                }
                public static void Content(BasePlayer Player)
                {
                    CuiHelper.DestroyUi(Player, pvxMenuMain);
                }
            }
            public static class Update
            {
                public static void AdminIndicator()
                {
                    if (Players.Admins.Count() == 0) return;
                    foreach (ulong PlayerID in Players.Admins.Get())
                    {
                        BasePlayer Player = Players.Find.BasePlayer(PlayerID);
                        CuiHelper.DestroyUi(Player, pvxAdminUI);
                        Create.AdminIndicator(Player);
                    }
                }
                public static void PlayerIndicator(BasePlayer Player)
                {
                    CuiHelper.DestroyUi(Player, pvxIndicator);
                    Create.PlayerIndicator(Player);
                }
            }
        }
        private void CreatePvXMenu(BasePlayer player)
        {
            GUI.Create.Menu.Background(player);
            GUI.Create.Menu.Title(player);
            GUI.Create.Menu.Selector(player);
            GUI.Create.Menu.Content.WelcomePages.Page1(player);
        }

        #endregion

        #region Looting Functions

        //ItemContainer.CanAcceptResult CanAcceptItem(ItemContainer container, Item item)
        //{
        //    //Puts(container.uid.ToString());
        //    //Puts("Bla");
        //    if (container.playerOwner != null)
        //    {
        //        BasePlayer Player = container.playerOwner;

        //        if (Containers.IsInSharedChest(Player.userID))
        //        {
        //            item.ClearOwners();
        //            return ItemContainer.CanAcceptResult.CanAccept;
        //        }
        //        List<Item.OwnerFraction> _itemOwners = item.owners;
        //        if (_itemOwners == null) return ItemContainer.CanAcceptResult.CanAccept;
        //        if (_itemOwners.Count < 1) return ItemContainer.CanAcceptResult.CanAccept;
        //        ulong _ownerID = _itemOwners[0].userid;
        //        if (_ownerID == 0) return ItemContainer.CanAcceptResult.CanAccept;
        //        if (_ownerID == Player.userID) return ItemContainer.CanAcceptResult.CanAccept;
        //        if (Players.State.IsNPC(_ownerID)) return ItemContainer.CanAcceptResult.CanAccept;
        //        if (SameOnlyCheck(container.playerOwner.userID, _ownerID)) return ItemContainer.CanAcceptResult.CanAccept;
        //        else
        //        {
        //            Messages.Chat(instance.covalence.Players.FindPlayerById(container.playerOwner.UserIDString), "notAllwPickup", Players.Data.playerData.Info[_ownerID].mode);
        //            return ItemContainer.CanAcceptResult.CannotAccept;
        //        }
        //    }
        //    else return ItemContainer.CanAcceptResult.CanAccept;
        //}
        private object CanLootPlayer(BasePlayer Target, BasePlayer Looter)
        {
            if (Players.State.IsNPC(Target)) return CanLootNPC(Looter);
            else if (Players.Admins.Mode.ContainsPlayer(Looter.userID)) return null;
            else if (Players.State.IsNA(Looter.userID)) return false;
            else if (Players.State.IsNA(Target.userID)) return false;
            else if (IsGod(Looter)) return null;
            else if (IsGod(Target)) return null;
            else if (AreInEvent(Looter, Target)) return null;
            else if (Players.State.IsPvE(Looter) && Players.State.IsPvE(Target) && PvELootPvE) return null;
            else if (Players.State.IsPvE(Looter) && Players.State.IsPvP(Target) && PvELootPvP) return null;
            else if (Players.State.IsPvP(Looter) && Players.State.IsPvE(Target) && PvPLootPvE) return null;
            else if (Players.State.IsPvP(Looter) && Players.State.IsPvP(Target) && PvPLootPvP) return null;

            return false;
        }
        private void OnLootPlayer(BasePlayer Looter, BasePlayer Target)
        {
            if (Players.State.IsNPC(Target)){NpcLootHandle(Looter);return;}
            else if (Players.Admins.Mode.ContainsPlayer(Looter.userID)) return;
            else if (Players.State.IsNA(Looter.userID)) NextTick(Looter.EndLooting);
            else if (Players.State.IsNA(Target.userID)) NextTick(Looter.EndLooting);
            else if (IsGod(Looter)) return;
            else if (IsGod(Target)) return;
            else if (AreInEvent(Looter, Target)) return;
            else if ((Players.State.IsPvE(Looter)) && (Players.State.IsPvE(Target)) && (PvELootPvE)) return;
            else if ((Players.State.IsPvE(Looter)) && (Players.State.IsPvP(Target)) && (PvELootPvP)) return;
            else if ((Players.State.IsPvP(Looter)) && (Players.State.IsPvE(Target)) && (PvPLootPvE)) return;
            else if ((Players.State.IsPvP(Looter)) && (Players.State.IsPvP(Target)) && (PvPLootPvP)) return;
            else NextTick(Looter.EndLooting);
        }
        private void OnLootEntity(BasePlayer Looter, BaseEntity Target)
        {
            if (Target is BaseCorpse)
            {
                var Corpse = Target?.GetComponent<PlayerCorpse>() ?? null;
                if (Corpse != null)
                {
                    if (Players.State.IsNPC(Corpse)) { NpcLootHandle(Looter); return; }
                    ulong CorpseID = Corpse.playerSteamID;
                    if (CorpseID == Looter.userID) return;
                    BasePlayer CorpseBP = Players.Find.BasePlayer(CorpseID);
                    if (CorpseBP != null)
                    {
                        if (Players.Admins.Mode.ContainsPlayer(Looter.userID)) return;
                        else if (Players.State.IsNA(Looter.userID)) NextTick(Looter.EndLooting);
                        else if (Players.State.IsNA(CorpseBP.userID)) NextTick(Looter.EndLooting);
                        else if (IsGod(Looter)) return;
                        else if (AreInEvent(Looter, CorpseBP)) return;
                        else if (Players.State.IsPvE(Looter) && Players.State.IsPvE(CorpseBP) && PvELootPvE) return;
                        else if (Players.State.IsPvE(Looter) && Players.State.IsPvP(CorpseBP) && PvELootPvP) return;
                        else if (Players.State.IsPvP(Looter) && Players.State.IsPvE(CorpseBP) && PvPLootPvE) return;
                        else if (Players.State.IsPvP(Looter) && Players.State.IsPvP(CorpseBP) && PvPLootPvP) return;
                        else NextTick(Looter.EndLooting);
                    }
                }
            }
            else if (Target is StorageContainer)
            {
                StorageContainer Container = (StorageContainer)Target;
                BasePlayer ContainerBP = Players.Find.BasePlayer(Container.OwnerID);
                if (Container == null) return;
                else if (Container.OwnerID == 0) return;
                else if (Container.OwnerID == Looter.userID)
                {
                    if (PvXselector.Containers.AddContainer(Container, Looter.userID))
                        NextTick(Looter.EndLooting);
                    else if (PvXselector.Containers.RemoveContainer(Container, Looter.userID))
                        NextTick(Looter.EndLooting);
                    if (PvXselector.Containers.IsShared(Container))
                    {
                        PvXselector.Containers.AddPlayerToInSharedChest(Looter.userID);
                        return;
                    }
                }
                else if (PvXselector.Containers.IsShared(Container))
                {
                    PvXselector.Containers.AddPlayerToInSharedChest(Looter.userID);
                    return;
                }
                else if (ContainerBP != null) return;
                else if (Players.Admins.Mode.ContainsPlayer(Looter.userID)) return;
                else if (Players.State.IsNA(Looter.userID)) NextTick(Looter.EndLooting);
                else if (Players.State.IsNA(ContainerBP.userID)) NextTick(Looter.EndLooting);
                else if (IsGod(Looter)) return;
                else if (IsGod(ContainerBP)) return;
                else if (AreInEvent(Looter, ContainerBP)) return;
                else if (Players.State.IsPvE(Looter) && Players.State.IsPvE(ContainerBP) && PvELootPvE) return;
                else if (Players.State.IsPvE(Looter) && Players.State.IsPvP(ContainerBP) && PvELootPvP) return;
                else if (Players.State.IsPvP(Looter) && Players.State.IsPvE(ContainerBP) && PvPLootPvE) return;
                else if (Players.State.IsPvP(Looter) && Players.State.IsPvP(ContainerBP) && PvPLootPvP) return;
                else NextTick(Looter.EndLooting);
            }
            else return;
        }
        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            //Puts("Container is type {0}", container.GetType());
            //Puts("Container is type {0}", container.entityOwner);
            //Puts("Container is type {0}", container.playerOwner);
            //Puts(container.GetType().ToString());
            if (container.entityOwner != null) return;
            if (container.playerOwner != null)
            {
                BasePlayer Player = container.playerOwner;
                //if (Players.Adminmode.ContainsPlayer(Player.userID)) item.ClearOwners();
            }
        }
        void OnPlayerLootEnd(PlayerLoot inventory)
        {
            BasePlayer player;
            if ((player = inventory.GetComponent<BasePlayer>()) == null)
                return;
            if (Containers.IsInSharedChest(player.userID)) Containers.RemovePlayerFromInSharedChest(player.userID);
        }

        #endregion

        #region Building Functions
        private List<object> BuildEntityList = new List<object>() {
            typeof(AutoTurret),typeof(Barricade),typeof(BaseCombatEntity),
            typeof(BaseOven),typeof(BearTrap),typeof(BuildingBlock),
            typeof(BuildingPrivlidge),typeof(CeilingLight),typeof(Door),
            typeof(Landmine),typeof(LiquidContainer),typeof(ReactiveTarget),
            typeof(RepairBench),typeof(ResearchTable),typeof(Signage),
            typeof(SimpleBuildingBlock),typeof(SleepingBag),typeof(StabilityEntity),
            typeof(StorageContainer),typeof(SurvivalFishTrap),typeof(WaterCatcher),
            typeof(WaterPurifier)};
        private List<object> BasePartEntityList = new List<object>() {
            typeof(BaseOven),typeof(BuildingBlock),typeof(BuildingPrivlidge),
            typeof(CeilingLight),typeof(Door),typeof(LiquidContainer),
            typeof(RepairBench),typeof(ResearchTable),typeof(Signage),
            typeof(SimpleBuildingBlock),typeof(SleepingBag),typeof(StabilityEntity),
            typeof(StorageContainer),typeof(SurvivalFishTrap),typeof(WaterCatcher),
            typeof(WaterPurifier)};
        private List<object> CombatPartEntityList = new List<object>() {
            typeof(AutoTurret),typeof(Barricade),typeof(BearTrap),typeof(Landmine),
            typeof(ReactiveTarget),typeof(BaseCombatEntity)};
        
        void OnEntitySpawned(BaseNetworkable _entity)
        {
            if (_entity is BaseEntity)
            {
                BaseEntity _base = (BaseEntity)_entity;
                if (_base.OwnerID == 0) return;
                else if (Players.Admins.Mode.ContainsPlayer(_base.OwnerID))
                    _base.OwnerID = 0;
            }
        }

        #endregion

        #region Compatibility Functions

        [PluginReference]
        Plugin Vanish;
        [PluginReference]
        Plugin Skills;

        bool CheckInvis(BasePlayer Player)
        {
            var isInvisible = Vanish?.Call("IsInvisible", Player);
            var isStealthed = Skills?.Call("isStealthed", Player);
            if (isInvisible != null && (bool)isInvisible)
            {
                return true;
            }
            else if (isStealthed != null && (bool)isStealthed)
            {
                return true;
            }
            else return false;
        }

        [PluginReference]
        private Plugin BetterChat;

        void UpdatePlayerChatTag(BasePlayer player) => SetChatTag(player);


        public void SetChatTag(BasePlayer player)
        {
            IPlayer iplayer = covalence.Players.FindPlayerById(player.ToString());
            BetterChat?.Call("API_RegisterThirdPartyTitle", new object[] { this, new Func<IPlayer, string>(GetClanTag) });
        }
        private string GetClanTag(IPlayer iPlayer)
        {
            string value;
            string mode = Mode.Error;
            ulong PlayerID = Convert.ToUInt64(iPlayer.Id);
            if (Players.State.IsNA(PlayerID))
            {
                mode = Mode.NA;
                value = $"[{PvXNAColour}][{mode}][/#]";
            }
            else if (Players.State.IsPvE(PlayerID))
            {
                mode = Mode.PvE;
                value = $"[{PvXPvEColour}][{mode}][/#]";
            }
            else if (Players.State.IsPvP(PlayerID))
            {
                mode = Mode.PvP;
                value = $"[{PvXPvPColour}][{mode}][/#]";
            }
            else value = $"[{PvXNAColour}][{mode}][/#]";
            return value;
        }

        //Possible betterchat functions
        //void VerifyColors() Will need to impliment for when I set colour in Config
        //{
        //    bool hasChanged = false;
        //    if (configData.Messaging.ClanChat.StartsWith("<color="))
        //    {
        //        configData.Messaging.ClanChat = configData.Messaging.ClanChat.Replace("<color=", "").Replace(">", "");
        //        hasChanged = true;
        //    }


        //    if (configData.Messaging.Main.StartsWith("<color="))
        //    {
        //        configData.Messaging.Main = configData.Messaging.Main.Replace("<color=", "").Replace(">", "");
        //        hasChanged = true;
        //    }

        //    if (configData.Messaging.MSG.StartsWith("<color="))
        //    {
        //        configData.Messaging.MSG = configData.Messaging.MSG.Replace("<color=", "").Replace(">", "");
        //        hasChanged = true;
        //    }

        //    if (configData.Options.ClanTagColor.StartsWith("<color="))
        //    {
        //        configData.Options.ClanTagColor = configData.Options.ClanTagColor.Replace("<color=", "").Replace(">", "");
        //        hasChanged = true;
        //    }

        //    if (!configData.Options.ClanTagColor.StartsWith("#"))
        //    {
        //        configData.Options.ClanTagColor = $"#{configData.Options.ClanTagColor}";
        //        hasChanged = true;
        //    }

        //    if (hasChanged)
        //        SaveConfig(configData);
        //}

        //private bool GroupExists(string name) => (bool)BetterChat?.Call("API_GroupExists", (name.ToLower()));
        //private bool NewGroup(string name) => (bool)BetterChat?.Call("API_AddGroup", (name.ToLower()));

        [PluginReference]
        public Plugin HumanNPC;

        void NpcDamageHandle(BasePlayer _NPC, HitInfo HitInfo)
        {
            BasePlayer _attacker = (BasePlayer)HitInfo.Initiator;
            if (Players.State.IsNPC(_attacker)) return;
            if ((Players.Data.playerData.Info[_attacker.userID].mode == Mode.PvP) && (PvPAttackNPC == true)) return;
            if ((Players.Data.playerData.Info[_attacker.userID].mode == Mode.PvE) && (PvEAttackNPC == true)) return;
            else ModifyDamage(HitInfo, 0);
        }
        void NpcAttackHandle(BasePlayer _target, HitInfo HitInfo)
        {
            if (Players.State.IsNPC(_target)) return;
            if ((Players.Data.playerData.Info[_target.userID].mode == Mode.PvP) && (NPCAttackPvP == true)) return;
            if ((Players.Data.playerData.Info[_target.userID].mode == Mode.PvE) && (NPCAttackPvE == true)) return;
            else ModifyDamage(HitInfo, 0);
        }

        bool CanLootNPC(BasePlayer Player)
        {
            if ((PvELootNPC == true) && (PvPLootNPC == true)) return true;
            else if ((Players.Data.playerData.Info[Player.userID].mode == Mode.PvP) && (PvPLootNPC == true)) return true;
            else if ((Players.Data.playerData.Info[Player.userID].mode == Mode.PvE) && (PvELootNPC == true)) return true;
            else return false;
        }
        void NpcLootHandle(BasePlayer Player)
        {
            if ((PvELootNPC == true) && (PvPLootNPC == true)) return;
            if ((Players.Data.playerData.Info[Player.userID].mode == Mode.PvP) && (PvPLootNPC == true)) return;
            if ((Players.Data.playerData.Info[Player.userID].mode == Mode.PvE) && (PvELootNPC == true)) return;
            Messages.Chat(Players.Find.Iplayer(Player.userID), "Not allowed to loot");
            NextTick(Player.EndLooting);
        }

        [PluginReference]
        private Plugin EventManager;
        bool IsInEvent(BasePlayer Player1)
        {
            if (EventManager == null) return false;
            bool _var = (bool)EventManager?.Call("isPlaying", Player1);
            if (_var == true) return true;
            return false;
        }

        bool AreInEvent(BasePlayer Player1, BasePlayer Player2)
        {
            if (EventManager == null) return false;
            bool _var1 = (bool)EventManager?.Call("isPlaying", Player1);
            bool _var2 = (bool)EventManager?.Call("isPlaying", Player2);
            if (_var1 == true && _var1 == _var2) return true;
            return false;
        }


        [PluginReference]
        private Plugin Godmode;
        private bool CheckIsGod(string Player) => (bool)Godmode?.Call("IsGod", Player);

        private bool IsGod(ulong Player)
        {
            if (Godmode == null) return false;
            return CheckIsGod(Player.ToString());
        }
        private bool IsGod(BasePlayer Player)
        {
            if (Godmode == null)
            {
                return false;
            }

            if (Player == null)
            {
                return false;
            }

            return CheckIsGod(Player.UserIDString);
        }


        #endregion

        #region Door Functions
        void OnDoorOpened(Door _door, BasePlayer Player)
        {
            if (_door == null) return;
            if (_door.OwnerID == 0) return;
            if (!(SameOnlyCheck(Player.userID, _door.OwnerID)))
            {
                _door.SetFlag(BaseEntity.Flags.Open, false);
                _door.SendNetworkUpdateImmediate();
            }
        }
        #endregion

        #region PvX Check/Find Functions
        private bool PvPOnlyCheck(ulong Player1, ulong Player2)
        {
            if (Players.State.IsNA(Player1)) return false;
            if (Players.State.IsNA(Player2)) return false;
            if ((Players.Data.playerData.Info[Player1].mode == Mode.PvP) && (Players.Data.playerData.Info[Player2].mode == Mode.PvP)) return true;
            return false;
        }
        private bool PvEOnlyCheck(ulong Player1, ulong Player2)
        {
            if (Players.State.IsNA(Player1)) return false;
            if (Players.State.IsNA(Player2)) return false;
            if ((Players.Data.playerData.Info[Player1].mode == Mode.PvE) && (Players.Data.playerData.Info[Player2].mode == Mode.PvE)) return true;
            return false;
        }
        private bool SameOnlyCheck(ulong Player1, ulong Player2)
        {
            if (Players.State.IsNA(Player1)) return false;
            if (Players.State.IsNA(Player2)) return false;
            if (Players.Data.playerData.Info[Player1].mode == Players.Data.playerData.Info[Player2].mode) return true;
            return false;
        }
        



        bool BaseplayerCheck(BasePlayer _attacker, BasePlayer _victim)
        {
            if (_attacker == _victim) return true;
            if (IsGod(_victim)) return true;
            if (IsGod(_attacker)) return true;
            if (AreInEvent(_attacker, _victim)) return true;
            return false;
        }

        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (!char.IsDigit(c))
                {
                    //Puts("Character Detected Returning false");
                    return false;
                }
            }
            //Puts("Detected no Characters Returning true");
            return true;
        }


        class Mode
        {
            public string Value;

            public static readonly Mode PvP = new Mode("PvP");
            public static readonly Mode PvE = new Mode("PvE");
            public static readonly Mode NA = new Mode("NA");
            public static readonly Mode Error = new Mode("Error");

            private Mode(string value)
            {
                Value = value;
            }
            public static implicit operator string(Mode mode)
            {
                return mode.Value;
            }
        }
        #endregion

        #region Hooks
        public void UpdatePvXPlayerData(BasePlayer Player)
        {
            Players.Data.playerData.Info[Player.userID].username = Player.displayName;
            Players.Data.playerData.Info[Player.userID].LatestConnection = DateTimeStamp();
        }
        bool IsPvEUlong(ulong PlayerID)
        {
            if (PlayerID == 0 || Players.State.IsNPC(PlayerID)) return false;
            BasePlayer Player = Players.Find.BasePlayer(PlayerID);
            if (Player == null) return false;
            if (!Players.Data.playerData.Info.ContainsKey(PlayerID))
            {
                Players.Add(Player);
                return false;
            }
            if (Players.Data.playerData.Info[PlayerID].mode == Mode.PvE) return true;
            else return false;
        }
        bool IsPvEBaseplayer(BasePlayer Player)
        {
            ulong PlayerID = Player.userID;
            if (PlayerID == 0 || Players.State.IsNPC(PlayerID)) return false;
            if (Player == null) return false;
            if (!Players.Data.playerData.Info.ContainsKey(PlayerID))
            {
                Players.Add(Player);
                return false;
            }
            if (Players.Data.playerData.Info[PlayerID].mode == Mode.PvE) return true;
            else return false;
        }
        #endregion

        #region Chat/Console Handles
        [ChatCommand("pvx")]
        void PvXChatCmd(BasePlayer Player, string cmd, string[] args)
        {
            if (!Cooldowns.menuGui.Contains(Player.userID))
            {
                Cooldowns.menuGui.Add(Player.userID);
                timer.Once(2f, () => Cooldowns.menuGui.Remove(Player.userID));
            }
            else return;
            if ((args == null) || (args.Length == 0))
            {
                CreatePvXMenu(Player);
                return;
            }
            switch (args[0].ToLower())
            {
                case "admin": //meed to transfer accept/dec;ome/list function
                    AdminFunction(Player, args);
                    return;
                case "change": //Completed
                    ChangeFunction(Player);
                    return;
                case "debug":
                    DebugFunction();
                    return;
                case "developer":
                    DeveloperFunction();
                    return;
                case "help":
                    HelpFunction(Player);
                    return;
                case "select":
                    SelectFunction(Player, args);
                    return;
                case "ticket":
                    TicketFunction(Player, args);
                    return;
                case "gui":
                    GuiFunction(Player, args);
                    return;
                default:
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "ComndList");
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx select, /pvx change, /pvx ticket /pvx gui");
                    if (HasPerm(Player, "admin")) Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx select, /pvx admin");
                    return;
            }
        }


        //[ChatCommand("pvxaddlist")]
        //void qwetryugsfdgvhjkfdsvjfdskhdfjkdsfhghkdfjksf(BasePlayer Player, string cmd, string[] args)
        //{
        //    Puts("Size of AddContainerMode: {0}", Containers.AddContainerMode.Count);
        //    Puts("List of AddContainerMode:");
        //    foreach (ulong test in Containers.AddContainerMode)
        //    {
        //        Puts(test.ToString());
        //    }
        //    Puts("End");
        //}
        [ChatCommand("pvxhide")]
        void HideAllGui(BasePlayer Player, string cmd, string[] args)
        {
            DestroyAllPvXUI(Player);
        }
        [ChatCommand("pvxshow")]
        void ShowGUI(BasePlayer Player, string cmd, string[] args)
        {
            GUI.Create.PlayerIndicator(Player);
            if (HasPerm(Player, "admin")) GUI.Create.AdminIndicator(Player);
        }


        [ConsoleCommand("PvXMenuCmd")]
        void PvXGuiCmds(ConsoleSystem.Arg arg)
        {
            if (arg.Connection.userid == 0) return;
            if (arg.Args == null || arg.Args.Length == 0) return;
            BasePlayer Player = (BasePlayer)arg.Connection.player;
            if (Player == null) return;
            if (arg.Args[0] == "Close")
            {
                GUI.Destroy.All(Player);
                return;
            }
            if (arg.Args[0] == "Welcome")
            {
                GUI.Destroy.Content(Player);
                GUI.Create.Menu.Content.WelcomePages.Page1(Player);
                return;
            }
            if (arg.Args[0] == "WelcomeAdmin")
            {
                GUI.Destroy.Content(Player);
                GUI.Create.Menu.Content.WelcomePages.Page4(Player);
                return;
            }
            if (arg.Args[0] == "WelcomePage")
            {
                int CurrentPage = Convert.ToInt32(arg.Args[1]);

                GUI.Destroy.Content(Player);
                if (arg.Args[2] == "Next")
                {
                    if (CurrentPage == 1) GUI.Create.Menu.Content.WelcomePages.Page2(Player);
                    if (CurrentPage == 2) GUI.Create.Menu.Content.WelcomePages.Page3(Player);
                    if (CurrentPage == 3) GUI.Create.Menu.Content.WelcomePages.Page4(Player);
                    if (CurrentPage == 4) GUI.Create.Menu.Content.WelcomePages.Page5(Player);
                }
                if (arg.Args[2] == "Back")
                {
                    if (CurrentPage == 2) GUI.Create.Menu.Content.WelcomePages.Page1(Player);
                    if (CurrentPage == 3) GUI.Create.Menu.Content.WelcomePages.Page2(Player);
                    if (CurrentPage == 4) GUI.Create.Menu.Content.WelcomePages.Page3(Player);
                    if (CurrentPage == 5) GUI.Create.Menu.Content.WelcomePages.Page4(Player);
                }
            }
            if (arg.Args[0] == "Character")
            {
                GUI.Destroy.Content(Player);
                GUI.Create.Menu.Content.Character(Player);
                return;
            }
            if (arg.Args[0] == "AddToShared")
            {
                Containers.AddToAddContainerList(Player.userID);
                GUI.Destroy.All(Player);
            }
            if (arg.Args[0] == "RemoveFromShared")
            {
                Containers.AddToRemoveContainerList(Player.userID);
                GUI.Destroy.All(Player);
            }
            if (arg.Args[0] == Mode.PvE)
            {
                if (Players.State.IsNA(Player))
                {
                    string playermode = Mode.PvE;
                    Players.Data.playerData.Info[Player.userID].mode = playermode;
                    GUI.Update.PlayerIndicator(Player);
                    UpdatePlayerChatTag(Player);
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "Selected: {0}", playermode);
                }
                else if (Players.HasTicket(Player.userID) == true)
                {
                    Messages.Chat(Players.Find.Iplayer(Player.userID), Lang.Ticket.AlreadyExists); return;
                }
                else
                {
                    Players.ChangeMode.Check(Player, Mode.PvE);
                }

                GUI.Destroy.Content(Player);
                GUI.Create.Menu.Content.Character(Player);
                return;
            }
            if (arg.Args[0] == Mode.PvP)
            {
                if (Players.State.IsNA(Player))
                {
                    string playermode = Mode.PvP;
                    Players.Data.playerData.Info[Player.userID].mode = playermode;
                    GUI.Update.PlayerIndicator(Player);
                    UpdatePlayerChatTag(Player);
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "Selected: {0}", playermode);
                }
                else if (Players.HasTicket(Player.userID) == true)
                {
                    Messages.Chat(Players.Find.Iplayer(Player.userID), Lang.Ticket.AlreadyExists); return;
                }
                else Players.ChangeMode.Check(Player, Mode.PvP);
                GUI.Destroy.Content(Player);
                GUI.Create.Menu.Content.Character(Player);
                return;
            }
        }
        [ConsoleCommand("pvx.cmd")]
        void PvXConsoleCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            if (arg.Args == null || arg.Args.Length == 0) return;
            ModeSwitch.Ticket.ConsoleList();
            ModeSwitch.Ticket.ConsoleListLogs();
        }
        #endregion

        #region Chat Functions
        //chat
        void AdminFunction(BasePlayer Player, string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), "IncoFormPleaUse");
                Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx admin [list/accept/decline/display]");
                return;
            }
            string _cmd = args[1].ToLower(); // admin, accept, 1
            if (!(HasPerm(Player, "admin", "MissPerm"))) return;
            if (_cmd == "count") ModeSwitch.Ticket.Count(Player);
            if (_cmd == "list") ModeSwitch.Ticket.List(Player);
            if (_cmd == "mode") Players.Admins.Mode.Toggle(Player.userID);
            if ((_cmd == "display") && (args.Length == 3))
            {
                if (IsDigitsOnly(args[2]))
                    ModeSwitch.Ticket.Info(Player, Convert.ToInt32(args[2]));
            }
            if ((_cmd == "accept") && (args.Length == 3))
            {
                if ((IsDigitsOnly(args[2])) && (ModeSwitch.Ticket.Data.ticketData.Link.ContainsKey(Convert.ToInt32(args[2]))))
                    ModeSwitch.Ticket.Accept(Player, Convert.ToInt32(args[2]));
                else if (!(ModeSwitch.Ticket.Data.ticketData.Link.ContainsKey(Convert.ToInt32(args[2]))))
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "TickNotAvail", args[2]);
                else
                {
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "IncoFormPleaUse");
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx admin accept #");
                }
            }
            if ((_cmd == "decline") && (args.Length == 3))
            {
                if ((IsDigitsOnly(args[2])) && (ModeSwitch.Ticket.Data.ticketData.Link.ContainsKey(Convert.ToInt32(args[2]))))
                    ModeSwitch.Ticket.Decline(Player, Convert.ToInt32(args[2]));
                else if (!(ModeSwitch.Ticket.Data.ticketData.Link.ContainsKey(Convert.ToInt32(args[2]))))
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "TickNotAvail", args[2]);
                else
                {
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "IncoFormPleaUse");
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx admin decline #");
                }
            }
        }
        void ChangeFunction(BasePlayer Player)
        {
            if (Players.HasTicket(Player.userID) == true)
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), Lang.Ticket.AlreadyExists); return;
            }
            else if (Players.State.IsNA(Player))
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), "IncoFormPleaUse");
                Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx select [pvp/pve]");
                return;
            }
            else if (Players.State.IsPvP(Player)) ModeSwitch.Ticket.Create(Player, Mode.PvE);
            else if (Players.State.IsPvE(Player)) ModeSwitch.Ticket.Create(Player, Mode.PvP);
            else Messages.PutsRcon("Error: 27Q1 - Please inform Dev");
            return;
        }
        void SelectFunction(BasePlayer Player, string[] args)
        {
            if ((args.Length != 2) && (args[1] != Mode.PvE) && (args[1] != Mode.PvP))
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), "IncoFormPleaUse");
                Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx select [pvp/pve]");
            }
            else if (Players.Data.playerData.Info[Player.userID].mode == Mode.NA)
            {
                if (args[1].ToLower() == Mode.PvP) Players.Data.playerData.Info[Player.userID].mode = Mode.PvP;
                if (args[1].ToLower() == Mode.PvE) Players.Data.playerData.Info[Player.userID].mode = Mode.PvE;
                GUI.Update.PlayerIndicator(Player);
            }
        }
        void TicketFunction(BasePlayer Player, string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), "IncoFormPleaUse");
                Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx ticket cancel");
                Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx ticket reason ''reason on ticket''");
                return;
            }
            string _cmd = args[1].ToLower();
            if (Players.Data.playerData.Info[Player.userID].ticket == false)
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), "NoActTick");
                return;
            }
            if (_cmd == "cancel")
            {
                ModeSwitch.Ticket.Cancel(Player);
            }
            if ((_cmd == "reason") && (args.Length == 3))
            {
                ModeSwitch.Ticket.Data.ticketData.Tickets[Player.userID].reason = args[2];
                Messages.Chat(Players.Find.Iplayer(Player.userID), "RSNChan");
                Messages.Chat(Players.Find.Iplayer(Player.userID), args[2]);
                SaveData.All();
                return;
            }
        }
        void GuiFunction(BasePlayer Player, string[] args)
        {
            if (args.Length == 1)
            {
                Messages.Chat(Players.Find.Iplayer(Player.userID), "ComndList");
                Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx gui pvx on/off");
                if (HasPerm(Player, "admin")) Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx gui admin on/off");
                return;
            }
            if (!(args.Length == 3)) return;
            if ((args[1].ToLower() == "admin") && (HasPerm(Player, "admin")))
            {
                if (args[2].ToLower() == "on") GUI.Create.AdminIndicator(Player);
                else if (args[2].ToLower() == "off") DestroyPvXUI(Player, pvxAdminUI);
                return;
            }
            else if (args[1].ToLower() == "pvx")
            {
                if (args[2].ToLower() == "on") GUI.Create.PlayerIndicator(Player);
                else if (args[2].ToLower() == "off") DestroyPvXUI(Player, pvxPlayerUI);
                return;
            }
            return;
        }
        void DebugFunction()
        { }
        void DeveloperFunction()
        { }
        void HelpFunction(BasePlayer Player)
        {
            Messages.Chat(Players.Find.Iplayer(Player.userID), "Plugin: PvX");
            Messages.Chat(Players.Find.Iplayer(Player.userID), "Description: {0}", Description);
            Messages.Chat(Players.Find.Iplayer(Player.userID), "Version {0}", Version);
            Messages.Chat(Players.Find.Iplayer(Player.userID), "Mod Developer: Alphawar");
            Messages.Chat(Players.Find.Iplayer(Player.userID), " ");
            Messages.Chat(Players.Find.Iplayer(Player.userID), "ComndList");
            Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx select, /pvx change, /pvx ticket /pvx gui");
            if (HasPerm(Player, "admin")) Messages.Chat(Players.Find.Iplayer(Player.userID), "/pvx select, /pvx admin");
        }

        //console
        #endregion

        #region OnEntityTakeDamage
        void OnEntityTakeDamage(BaseCombatEntity Target, HitInfo HitInfo)
        {
            BaseEntity _attacker = HitInfo.Initiator;
            object _n = Target.GetType();

            /*
            if (_target is BasePlayer && 1 == 1){
                BasePlayer _test = (BasePlayer)_target;
                if (_test.userID == 76561198006265515) testvar(_target, HitInfo);}
            else if (BuildEntityList.Contains(_n) && 1 == 1){
                if (_target.OwnerID == 76561198006265515) testvar(_target, HitInfo);}
            */

            if (_attacker is BasePlayer && Target is BasePlayer) PlayerVPlayer((BasePlayer)Target, (BasePlayer)_attacker, HitInfo);                               //Player V Player
            else if (_attacker is BasePlayer && BuildEntityList.Contains(_n) && !(_n is AutoTurret)) PlayerVBuilding(Target, (BasePlayer)_attacker, HitInfo);      //Player V Building

            else if (_attacker is BasePlayer && Target is BaseHelicopter) PlayerVHeli((BasePlayer)_attacker, HitInfo);                                             //Player V Heli
            else if ((_attacker is BaseHelicopter || (_attacker is FireBall && _attacker.ShortPrefabName == "napalm")) && Target is BasePlayer) HeliVPlayer((BasePlayer)Target, HitInfo);
            else if ((_attacker is BaseHelicopter || (_attacker is FireBall && _attacker.ShortPrefabName == "napalm")) && BuildEntityList.Contains(_n)) HeliVBuilding(Target, HitInfo);
            else if ((_attacker is BaseHelicopter || (_attacker is FireBall && _attacker.ShortPrefabName == "napalm")) && Target is BaseNPC) HeliVAnimal((BaseNPC)Target, HitInfo);


            else if (_attacker is BasePlayer && Target is AutoTurret) PlayerVTurret((AutoTurret)Target, (BasePlayer)_attacker, HitInfo);                          //Player V Turret
            else if (_attacker is AutoTurret && Target is BasePlayer) TurretVPlayer((BasePlayer)Target, (AutoTurret)_attacker, HitInfo);                          //Turret V Player
            else if (_attacker is AutoTurret && Target is AutoTurret) TurretVTurret((AutoTurret)Target, (AutoTurret)_attacker, HitInfo);                          //Turret V Turret
            else if (_attacker is AutoTurret && Target is BaseNPC) TurretVAnimal((BaseNPC)Target, (AutoTurret)_attacker, HitInfo);                                //Turret V Animal

            else if (_attacker is BasePlayer && Target is BaseNPC) PlayerVAnimal((BasePlayer)_attacker, HitInfo);                                                  //Player V Animal
            else if (_attacker is BaseNPC && Target is BasePlayer) AnimalVPlayer((BasePlayer)Target, HitInfo);
            else if (_attacker is FireBall)
            {
                FireBall _fire = (FireBall)_attacker;
                if (Target is BasePlayer) FireVPlayer((BasePlayer)Target, HitInfo);
                else if (BuildEntityList.Contains(_n)) FireVBuilding(Target, HitInfo);
            }


            //if (HitInfo.Initiator is BaseTrap)
            //if (HitInfo.Initiator is Barricade)
            //if (HitInfo.WeaponPrefab.ShortPrefabName == "rocket_heli" ||
            //HitInfo.WeaponPrefab.ShortPrefabName == "rocket_heli_napalm")
            //if (HitInfo.Initiator != null && HitInfo.Initiator.ShortPrefabName == "napalm")
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is StorageContainer)
            {
                if (Containers.IsShared((StorageContainer)entity))
                {
                    Containers.RemoveContainer((StorageContainer)entity);
                }
            }
        }

        //void Testvar(BaseCombatEntity _target, HitInfo HitInfo)
        //{
        //    Type typeInformation = HitInfo.Initiator.GetType();
        //    BaseHelicopter
        //    _attacker is FireBall && _attacker.ShortPrefabName = fireball_small
        //}
        void PlayerVPlayer(BasePlayer Victim, BasePlayer Attacker, HitInfo HitInfo)
        {
            //Puts("Calling PvP");
            if (BaseplayerCheck(Attacker, Victim)) return;
            if (Players.State.IsNPC(Attacker))
            {
                if (Players.State.IsNPC(Victim)) return;
                else if (Players.State.IsPvE(Victim) && NPCAttackPvE) ModifyDamage(HitInfo, NPCDamagePvE);
                else if (Players.State.IsPvP(Victim) && NPCAttackPvP) ModifyDamage(HitInfo, NPCDamagePvP);
                else ModifyDamage(HitInfo, 0);
            }
            else if (Players.State.IsPvE(Attacker))
            {
                if (Players.State.IsNPC(Victim)) if (PvEAttackNPC) ModifyDamage(HitInfo, PvEDamageNPC); else ModifyDamage(HitInfo, 0);
                else if (Players.State.IsPvE(Victim) && PvEAttackPvE) ModifyDamage(HitInfo, PvEDamagePvE);
                else if (Players.State.IsPvP(Victim) && PvEAttackPvP) ModifyDamage(HitInfo, PvEDamagePvP);
                else ModifyDamage(HitInfo, 0);
            }
            else if (Players.State.IsPvP(Attacker))
            {
                if (Players.State.IsNPC(Victim)) if (PvPAttackNPC) ModifyDamage(HitInfo, PvPDamageNPC); else ModifyDamage(HitInfo, 0);
                else if (Players.State.IsPvE(Victim) && PvPAttackPvE) ModifyDamage(HitInfo, PvPDamagePvE);
                else if (Players.State.IsPvP(Victim) && PvPAttackPvP) ModifyDamage(HitInfo, PvPDamagePvP);
                else ModifyDamage(HitInfo, 0);
            }
            if (Players.Data.playerData.Info[Victim.userID].mode == Mode.PvE)
            {
                if (!Cooldowns.enemyOpModeWarning.Contains(Attacker.userID))
                {
                    Cooldowns.enemyOpModeWarning.Add(Attacker.userID);
                    timer.Once(2f, () => Cooldowns.enemyOpModeWarning.Remove(Attacker.userID));
                    Messages.Chat(Players.Find.Iplayer(Attacker.userID), lang.GetMessage("PvETarget", this, Attacker.UserIDString));
                }
                Victim.EndLooting();
            }
            //if (_victim.userID == 76561198006265515)
            //{
            //    Puts("AttackerBP: {0}", _attacker);
            //    Puts("VARE: {0}", HitInfo.Initiator);
            //    Puts("VARE: {0}", HitInfo.InitiatorPlayer);
            //}
            return;
        }
        void PlayerVBuilding(BaseEntity Target, BasePlayer Attacker, HitInfo Hitinfo)
        {
            //Puts("Calling PvB");
            ulong _victim = Target.OwnerID;
            if (Target.OwnerID == 0) return;
            if (IsInEvent(Attacker)) return;
            if (Target.OwnerID == Attacker.userID) return;
            if (IsGod(Target.OwnerID)) return;
            if (IsGod(Attacker)) return;
            if (Players.State.IsNPC(Attacker))
            {
                if (Players.State.IsNPC(_victim)) return;
                else if (Players.State.IsPvE(_victim) && NPCAttackPvE) ModifyDamage(Hitinfo, NPCDamagePvE);
                else if (Players.State.IsPvP(_victim) && NPCAttackPvP) ModifyDamage(Hitinfo, NPCDamagePvP);
                else ModifyDamage(Hitinfo, 0);
            }
            else if (Players.State.IsPvE(Attacker))
            {
                if (Players.State.IsNPC(_victim)) if (PvEAttackNPC) ModifyDamage(Hitinfo, PvEDamageNPC); else ModifyDamage(Hitinfo, 0);
                else if (AreInEvent(Attacker, Attacker)) return;
                else if (Players.State.IsPvE(_victim) && PvEAttackPvE) ModifyDamage(Hitinfo, PvEDamagePvE);
                else if (Players.State.IsPvP(_victim) && PvEAttackPvP) ModifyDamage(Hitinfo, PvEDamagePvP);
                else ModifyDamage(Hitinfo, 0);
            }
            else if (Players.State.IsPvP(Attacker))
            {
                if (Players.State.IsNPC(_victim)) if (PvPAttackNPC) ModifyDamage(Hitinfo, PvPDamageNPC); else ModifyDamage(Hitinfo, 0);
                else if (AreInEvent(Attacker, Attacker)) return;
                else if (Players.State.IsPvE(_victim) && PvPAttackPvE) ModifyDamage(Hitinfo, PvPDamagePvE);
                else if (Players.State.IsPvP(_victim) && PvPAttackPvP) ModifyDamage(Hitinfo, PvPDamagePvP);
                else ModifyDamage(Hitinfo, 0);
            }
            if (Players.Data.playerData.Info[_victim].mode == Mode.PvE)
            {
                if (!Cooldowns.enemyOpModeWarning.Contains(Attacker.userID))
                {
                    Cooldowns.enemyOpModeWarning.Add(Attacker.userID);
                    timer.Once(2f, () => Cooldowns.enemyOpModeWarning.Remove(Attacker.userID));
                    Messages.Chat(Players.Find.Iplayer(Attacker.userID), lang.GetMessage("PvETarget", this, Attacker.UserIDString));
                }
            }
        }

        void PlayerVHeli(BasePlayer Attacker, HitInfo HitInfo)
        {
            //Puts("Calling PvH");
            if (Players.State.IsNPC(Attacker)) return;
            else if (IsGod(Attacker)) return;
            else if (IsInEvent(Attacker)) return;
            else if (Players.State.IsPvE(Attacker) && HeliTargetPvE) ModifyDamage(HitInfo, HeliDamageByPvE);
            else if (Players.State.IsPvP(Attacker) && HeliTargetPvP) ModifyDamage(HitInfo, HeliDamageByPvP);
            else ModifyDamage(HitInfo, 0);
        }
        void HeliVPlayer(BasePlayer Victim, HitInfo HitInfo)
        {
            //Puts("Calling HvP");
            if (Players.State.IsNPC(Victim)) return;
            else if (IsGod(Victim)) return;
            else if (IsInEvent(Victim)) return;
            else if (Players.State.IsPvE(Victim) && HeliTargetPvE) ModifyDamage(HitInfo, HeliDamagePvE);
            else if (Players.State.IsPvP(Victim) && HeliTargetPvP) ModifyDamage(HitInfo, HeliDamagePvP);
            else ModifyDamage(HitInfo, 0);
        }
        void HeliVBuilding(BaseEntity Target, HitInfo HitInfo)
        {
            //Puts("Calling HvB");
            ulong _ownerID = Target.OwnerID;
            if (Players.State.IsNPC(_ownerID)) return;
            else if (IsGod(_ownerID)) return;
            else if (Players.State.IsPvE(_ownerID) && HeliTargetPvE) ModifyDamage(HitInfo, HeliDamagePvEStruct);
            else if (Players.State.IsPvP(_ownerID) && HeliTargetPvP) ModifyDamage(HitInfo, HeliDamagePvPStruct);
            else ModifyDamage(HitInfo, 0);
        }
        void HeliVAnimal(BaseNPC Target, HitInfo HitInfo)
        {
            //Puts("Calling HvA");
            ModifyDamage(HitInfo, HeliDamageAnimal);
        }

        void PlayerVTurret(AutoTurret Target, BasePlayer Attacker, HitInfo HitInfo)
        {
            //Puts("Calling PvT");
            ulong _ownerID = Target.OwnerID;
            if (IsGod(Attacker)) return;
            else if (IsInEvent(Attacker)) return;
            else if (Players.State.IsNPC(Attacker) && Players.State.IsPvE(_ownerID)) ModifyDamage(HitInfo, TurretPvEDamageNPCAmnt);
            else if (Players.State.IsNPC(Attacker) && Players.State.IsPvP(_ownerID)) ModifyDamage(HitInfo, TurretPvPDamageNPCAmnt);
            else if (Players.State.IsPvE(Attacker) && Players.State.IsPvE(_ownerID)) ModifyDamage(HitInfo, TurretPvEDamagePvEAmnt);
            else if (Players.State.IsPvE(Attacker) && Players.State.IsPvP(_ownerID)) ModifyDamage(HitInfo, TurretPvEDamagePvPAmnt);
            else if (Players.State.IsPvP(Attacker) && Players.State.IsPvE(_ownerID)) ModifyDamage(HitInfo, TurretPvPDamagePvEAmnt);
            else if (Players.State.IsPvP(Attacker) && Players.State.IsPvP(_ownerID)) ModifyDamage(HitInfo, TurretPvPDamagePvPAmnt);
            else ModifyDamage(HitInfo, 0);
        }
        void TurretVPlayer(BasePlayer Target, AutoTurret Attacker, HitInfo HitInfo)
        {
            //Puts("Calling TvP");
            ulong _attackerID = Attacker.OwnerID;
            if (IsGod(Target)) return;
            else if (IsInEvent(Target)) return;
            else if (Players.State.IsPvE(_attackerID) && Players.State.IsNPC(Target)) ModifyDamage(HitInfo, TurretPvEDamageNPCAmnt);
            else if (Players.State.IsPvP(_attackerID) && Players.State.IsNPC(Target)) ModifyDamage(HitInfo, TurretPvPDamageNPCAmnt);
            else if (Players.State.IsPvE(_attackerID) && Players.State.IsPvE(Target)) ModifyDamage(HitInfo, TurretPvEDamagePvEAmnt);
            else if (Players.State.IsPvE(_attackerID) && Players.State.IsPvP(Target)) ModifyDamage(HitInfo, TurretPvEDamagePvPAmnt);
            else if (Players.State.IsPvP(_attackerID) && Players.State.IsPvE(Target)) ModifyDamage(HitInfo, TurretPvPDamagePvEAmnt);
            else if (Players.State.IsPvP(_attackerID) && Players.State.IsPvP(Target)) ModifyDamage(HitInfo, TurretPvPDamagePvPAmnt);
            else ModifyDamage(HitInfo, 0);
        }
        void TurretVTurret(AutoTurret Target, AutoTurret Attacker, HitInfo HitInfo)
        {
            //Puts("Calling TvT");
            ulong _targetID = Target.OwnerID;
            ulong _attackerID = Target.OwnerID;
            if (Players.State.IsPvE(_attackerID) && Players.State.IsPvE(_targetID)) ModifyDamage(HitInfo, TurretPvEDamagePvEAmnt);
            else if (Players.State.IsPvE(_attackerID) && Players.State.IsPvP(_targetID)) ModifyDamage(HitInfo, TurretPvEDamagePvPAmnt);
            else if (Players.State.IsPvP(_attackerID) && Players.State.IsPvE(_targetID)) ModifyDamage(HitInfo, TurretPvPDamagePvEAmnt);
            else if (Players.State.IsPvP(_attackerID) && Players.State.IsPvP(_targetID)) ModifyDamage(HitInfo, TurretPvPDamagePvPAmnt);
            else ModifyDamage(HitInfo, 0);
        }
        void TurretVAnimal(BaseNPC Target, AutoTurret Attacker, HitInfo HitInfo)
        {
            //Puts("Calling TvA");
            ulong _turretOwner = Attacker.OwnerID;
            if (Players.State.IsPvE(_turretOwner) && TurretPvETargetAnimal) ModifyDamage(HitInfo, TurretPvEDamageAnimalAmnt);
            else if (Players.State.IsPvP(_turretOwner) && TurretPvPTargetAnimal) ModifyDamage(HitInfo, TurretPvPDamageAnimalAmnt);
            else ModifyDamage(HitInfo, 0);
        }

        void PlayerVAnimal(BasePlayer Attacker, HitInfo HitInfo)
        {
            //Puts("Calling PvA");
            if (IsGod(Attacker)) return;
            else if (IsInEvent(Attacker)) return;
            else if (Players.State.IsNPC(Attacker)) ModifyDamage(HitInfo, NPCDamageAnimals);
            else if (Players.State.IsPvE(Attacker)) ModifyDamage(HitInfo, PvEDamageAnimals);
            else if (Players.State.IsPvP(Attacker)) ModifyDamage(HitInfo, PvPDamageAnimals);
            else ModifyDamage(HitInfo, 0);
        }
        void AnimalVPlayer(BasePlayer Target, HitInfo HitInfo)
        {
            //Puts("Calling AvP");
            if (IsGod(Target)) return;
            else if (IsInEvent(Target)) return;
            else if (Players.State.IsNPC(Target)) ModifyDamage(HitInfo, AnimalsDamageNPC);
            else if (Players.State.IsPvE(Target)) ModifyDamage(HitInfo, AnimalsDamagePvE);
            else if (Players.State.IsPvP(Target)) ModifyDamage(HitInfo, AnimalsDamagePvP);
            else if (Players.State.IsNA(Target)) ModifyDamage(HitInfo, 1);
            else ModifyDamage(HitInfo, 0);
        }

        void FireVPlayer(BasePlayer Target, HitInfo HitInfo)
        {
            if (Players.State.IsNPC(Target)) return;
            else if (IsGod(Target)) return;
            else if (IsInEvent(Target)) return;
            else if (Players.State.IsPvE(Target)) ModifyDamage(HitInfo, FireDamagePvE);
            else if (Players.State.IsPvP(Target)) ModifyDamage(HitInfo, FireDamagePvP);
            else ModifyDamage(HitInfo, 0);
        }
        void FireVBuilding(BaseEntity Target, HitInfo HitInfo)
        {
            //Puts("Calling FvB");
            if (Players.State.IsPvE(Target.OwnerID)) ModifyDamage(HitInfo, FireDamagePvEStruc);
            else if (Players.State.IsPvP(Target.OwnerID)) ModifyDamage(HitInfo, FireDamagePvPStruc);
            else ModifyDamage(HitInfo, 0);
        }
        #endregion

        #region CanBeTargeted
        private object CanBeTargeted(BaseCombatEntity _target, MonoBehaviour turret)
        {
            if (turret is HelicopterTurret && _target is BasePlayer && HeliTargetPlayer((BasePlayer)_target)) return null;
            else if (turret is AutoTurret && _target is BasePlayer && TurretTargetPlayer((BasePlayer)_target, (AutoTurret)turret)) return null;
            else if (turret is AutoTurret && _target is BaseNPC && TurretTargetAnimals((BaseNPC)_target, (AutoTurret)turret)) return null;
            else return false;
        }

        bool HeliTargetPlayer(BasePlayer _target)
        {
            if (Players.State.IsNPC(_target) && HeliTargetNPC) return true;
            else if (CheckInvis(_target)) return true;
            else if (Players.State.IsPvE(_target) && HeliTargetPvE) return true;
            else if (Players.State.IsPvP(_target) && HeliTargetPvP) return true;
            return false;
        }
        bool TurretTargetPlayer(BasePlayer _target, AutoTurret _attacker)
        {
            ulong _OwnerID = _attacker.OwnerID;
            if (!Players.State.IsNPC(_target) && CheckInvis(_target)) return true;
            else if (Players.State.IsPvE(_OwnerID) && Players.State.IsNPC(_target) && TurretPvETargetNPC) return true;
            else if (Players.State.IsPvE(_OwnerID) && Players.State.IsPvE(_target) && TurretPvETargetPvE) return true;
            else if (Players.State.IsPvE(_OwnerID) && Players.State.IsPvP(_target) && TurretPvETargetPvP) return true;
            else if (Players.State.IsPvP(_OwnerID) && Players.State.IsNPC(_target) && TurretPvPTargetNPC) return true;
            else if (Players.State.IsPvP(_OwnerID) && Players.State.IsPvE(_target) && TurretPvPTargetPvE) return true;
            else if (Players.State.IsPvP(_OwnerID) && Players.State.IsPvP(_target) && TurretPvPTargetPvP) return true;
            return false;
        }
        bool TurretTargetAnimals(BaseNPC _target, AutoTurret _attacker)
        {
            ulong _OwnerID = _attacker.OwnerID;
            if (Players.State.IsPvE(_OwnerID) && TurretPvETargetAnimal) return true;
            if (Players.State.IsPvP(_OwnerID) && TurretPvPTargetAnimal) return true;
            return false;
        }
        #endregion

        #region Classes
        public static class Messages
        {
            public static string Get(string langMsg, params object[] args)
            {
                return instance.lang.GetMessage(langMsg, instance);
            }
            public static void Chat(IPlayer player, string langMsg, params object[] args)
            {
                string message = instance.lang.GetMessage(langMsg, instance, player.Id);
                player.Reply($"<color={instance.ChatPrefixColor}>{instance.ChatPrefix}</color>: <color={instance.ChatMessageColor}>{message}</color>", args);
            }
            public static void ChatGlobal(string langMsg, params object[] args)
            {
                string message = instance.lang.GetMessage(langMsg, instance);
                instance.PrintToChat($"<color={instance.ChatPrefixColor}>{instance.ChatPrefix}</color>: <color={instance.ChatMessageColor}>{message}</color>", args);
            }
            public static void PutsRcon(string langMsg, params object[] args)
            {
                string message = instance.lang.GetMessage(langMsg, instance);
                instance.Puts(string.Format(message, args));
                
            }
        }
        
        public class Lang
        {
            public class Menu
            {
                public class WelcomePage
                {
                    public static readonly WelcomePage P1L01 = new WelcomePage("Page1Line1");
                    public static readonly WelcomePage P1L02 = new WelcomePage("Page1Line2");
                    public static readonly WelcomePage P1L03 = new WelcomePage("Page1Line3");
                    public static readonly WelcomePage P1L04 = new WelcomePage("Page1Line4");
                    public static readonly WelcomePage P1L05 = new WelcomePage("Page1Line5");
                    public static readonly WelcomePage P1L06 = new WelcomePage("Page1Line6");
                    public static readonly WelcomePage P1L07 = new WelcomePage("Page1Line7");
                    public static readonly WelcomePage P1L08 = new WelcomePage("Page1Line8");
                    public static readonly WelcomePage P1L09 = new WelcomePage("Page1Line9");
                    public static readonly WelcomePage P1L10 = new WelcomePage("Page1Line10");
                    public static readonly WelcomePage P1L11 = new WelcomePage("Page1Line11");
                    public static readonly WelcomePage P1L12 = new WelcomePage("Page1Line12");
                    public static readonly WelcomePage P1L13 = new WelcomePage("Page1Line13");
                    public static readonly WelcomePage P1L14 = new WelcomePage("Page1Line14");
                    public static readonly WelcomePage P1L15 = new WelcomePage("Page1Line15");
                    public static readonly WelcomePage P1L16 = new WelcomePage("Page1Line16");
                    public static readonly WelcomePage P1L17 = new WelcomePage("Page1Line17");
                    public static readonly WelcomePage P1L18 = new WelcomePage("Page1Line18");
                    public static readonly WelcomePage P1L19 = new WelcomePage("Page1Line19");
                    public static readonly WelcomePage P1L20 = new WelcomePage("Page1Line20");
                    public static readonly WelcomePage P1L21 = new WelcomePage("Page1Line21");

                    public static readonly WelcomePage P2L01 = new WelcomePage("Page2Line1");
                    public static readonly WelcomePage P2L02 = new WelcomePage("Page2Line2");
                    public static readonly WelcomePage P2L03 = new WelcomePage("Page2Line3");
                    public static readonly WelcomePage P2L04 = new WelcomePage("Page2Line4");
                    public static readonly WelcomePage P2L05 = new WelcomePage("Page2Line5");
                    public static readonly WelcomePage P2L06 = new WelcomePage("Page2Line6");
                    public static readonly WelcomePage P2L07 = new WelcomePage("Page2Line7");
                    public static readonly WelcomePage P2L08 = new WelcomePage("Page2Line8");
                    public static readonly WelcomePage P2L09 = new WelcomePage("Page2Line9");
                    public static readonly WelcomePage P2L10 = new WelcomePage("Page2Line10");
                    public static readonly WelcomePage P2L11 = new WelcomePage("Page2Line11");
                    public static readonly WelcomePage P2L12 = new WelcomePage("Page2Line12");
                    public static readonly WelcomePage P2L13 = new WelcomePage("Page2Line13");
                    public static readonly WelcomePage P2L14 = new WelcomePage("Page2Line14");
                    public static readonly WelcomePage P2L15 = new WelcomePage("Page2Line15");
                    public static readonly WelcomePage P2L16 = new WelcomePage("Page2Line16");
                    public static readonly WelcomePage P2L17 = new WelcomePage("Page2Line17");
                    public static readonly WelcomePage P2L18 = new WelcomePage("Page2Line18");
                    public static readonly WelcomePage P2L19 = new WelcomePage("Page2Line19");
                    public static readonly WelcomePage P2L20 = new WelcomePage("Page2Line20");
                    public static readonly WelcomePage P2L21 = new WelcomePage("Page2Line21");

                    public static readonly WelcomePage P3L01 = new WelcomePage("Page3Line1");
                    public static readonly WelcomePage P3L02 = new WelcomePage("Page3Line2");
                    public static readonly WelcomePage P3L03 = new WelcomePage("Page3Line3");
                    public static readonly WelcomePage P3L04 = new WelcomePage("Page3Line4");
                    public static readonly WelcomePage P3L05 = new WelcomePage("Page3Line5");
                    public static readonly WelcomePage P3L06 = new WelcomePage("Page3Line6");
                    public static readonly WelcomePage P3L07 = new WelcomePage("Page3Line7");
                    public static readonly WelcomePage P3L08 = new WelcomePage("Page3Line8");
                    public static readonly WelcomePage P3L09 = new WelcomePage("Page3Line9");
                    public static readonly WelcomePage P3L10 = new WelcomePage("Page3Line10");
                    public static readonly WelcomePage P3L11 = new WelcomePage("Page3Line11");
                    public static readonly WelcomePage P3L12 = new WelcomePage("Page3Line12");
                    public static readonly WelcomePage P3L13 = new WelcomePage("Page3Line13");
                    public static readonly WelcomePage P3L14 = new WelcomePage("Page3Line14");
                    public static readonly WelcomePage P3L15 = new WelcomePage("Page3Line15");
                    public static readonly WelcomePage P3L16 = new WelcomePage("Page3Line16");
                    public static readonly WelcomePage P3L17 = new WelcomePage("Page3Line17");
                    public static readonly WelcomePage P3L18 = new WelcomePage("Page3Line18");
                    public static readonly WelcomePage P3L19 = new WelcomePage("Page3Line19");
                    public static readonly WelcomePage P3L20 = new WelcomePage("Page3Line20");
                    public static readonly WelcomePage P3L21 = new WelcomePage("Page3Line21");

                    public static readonly WelcomePage AP1L01 = new WelcomePage("AdminPage1Line1");
                    public static readonly WelcomePage AP1L02 = new WelcomePage("AdminPage1Line2");
                    public static readonly WelcomePage AP1L03 = new WelcomePage("AdminPage1Line3");
                    public static readonly WelcomePage AP1L04 = new WelcomePage("AdminPage1Line4");
                    public static readonly WelcomePage AP1L05 = new WelcomePage("AdminPage1Line5");
                    public static readonly WelcomePage AP1L06 = new WelcomePage("AdminPage1Line6");
                    public static readonly WelcomePage AP1L07 = new WelcomePage("AdminPage1Line7");
                    public static readonly WelcomePage AP1L08 = new WelcomePage("AdminPage1Line8");
                    public static readonly WelcomePage AP1L09 = new WelcomePage("AdminPage1Line9");
                    public static readonly WelcomePage AP1L10 = new WelcomePage("AdminPage1Line10");
                    public static readonly WelcomePage AP1L11 = new WelcomePage("AdminPage1Line11");
                    public static readonly WelcomePage AP1L12 = new WelcomePage("AdminPage1Line12");
                    public static readonly WelcomePage AP1L13 = new WelcomePage("AdminPage1Line13");
                    public static readonly WelcomePage AP1L14 = new WelcomePage("AdminPage1Line14");
                    public static readonly WelcomePage AP1L15 = new WelcomePage("AdminPage1Line15");
                    public static readonly WelcomePage AP1L16 = new WelcomePage("AdminPage1Line16");
                    public static readonly WelcomePage AP1L17 = new WelcomePage("AdminPage1Line17");
                    public static readonly WelcomePage AP1L18 = new WelcomePage("AdminPage1Line18");
                    public static readonly WelcomePage AP1L19 = new WelcomePage("AdminPage1Line19");
                    public static readonly WelcomePage AP1L20 = new WelcomePage("AdminPage1Line20");
                    public static readonly WelcomePage AP1L21 = new WelcomePage("AdminPage1Line21");

                    public static readonly WelcomePage AP2L01 = new WelcomePage("AdminPage2Line1");
                    public static readonly WelcomePage AP2L02 = new WelcomePage("AdminPage2Line2");
                    public static readonly WelcomePage AP2L03 = new WelcomePage("AdminPage2Line3");
                    public static readonly WelcomePage AP2L04 = new WelcomePage("AdminPage2Line4");
                    public static readonly WelcomePage AP2L05 = new WelcomePage("AdminPage2Line5");
                    public static readonly WelcomePage AP2L06 = new WelcomePage("AdminPage2Line6");
                    public static readonly WelcomePage AP2L07 = new WelcomePage("AdminPage2Line7");
                    public static readonly WelcomePage AP2L08 = new WelcomePage("AdminPage2Line8");
                    public static readonly WelcomePage AP2L09 = new WelcomePage("AdminPage2Line9");
                    public static readonly WelcomePage AP2L10 = new WelcomePage("AdminPage2Line10");
                    public static readonly WelcomePage AP2L11 = new WelcomePage("AdminPage2Line11");
                    public static readonly WelcomePage AP2L12 = new WelcomePage("AdminPage2Line12");
                    public static readonly WelcomePage AP2L13 = new WelcomePage("AdminPage2Line13");
                    public static readonly WelcomePage AP2L14 = new WelcomePage("AdminPage2Line14");
                    public static readonly WelcomePage AP2L15 = new WelcomePage("AdminPage2Line15");
                    public static readonly WelcomePage AP2L16 = new WelcomePage("AdminPage2Line16");
                    public static readonly WelcomePage AP2L17 = new WelcomePage("AdminPage2Line17");
                    public static readonly WelcomePage AP2L18 = new WelcomePage("AdminPage2Line18");
                    public static readonly WelcomePage AP2L19 = new WelcomePage("AdminPage2Line19");
                    public static readonly WelcomePage AP2L20 = new WelcomePage("AdminPage2Line20");
                    public static readonly WelcomePage AP2L21 = new WelcomePage("AdminPage2Line21");

                    public string Value;
                    private WelcomePage(string welcomePage) { Value = this.ToString() + "-" + welcomePage; }
                    public static implicit operator string(WelcomePage welcomePage) { return welcomePage.Value; }
                }
            }
            public class ModInit
            {
                public static readonly ModInit CntFindData = new ModInit("CantFindData");
                public static readonly ModInit LoadingData = new ModInit("CreateNewData");

                public string Value;
                private ModInit(string modInit) { Value = this.ToString() + "-" + modInit; }
                public static implicit operator string(ModInit modInit) { return modInit.Value; }
            }
            public class Ticket
            {
                public static readonly Ticket Accepted = new Ticket("Accepted");
                public static readonly Ticket Canceled = new Ticket("Canceled");
                public static readonly Ticket Created = new Ticket("Created");
                public static readonly Ticket Declined = new Ticket("Declined");
                public static readonly Ticket AcceptedAdmin = new Ticket("AcceptedAdmin");
                public static readonly Ticket DeclinedAdmin = new Ticket("DeclinedAdmin");
                public static readonly Ticket AlreadyExists = new Ticket("AlreadyExists");

                public string Value;
                private Ticket(string ticket) { Value = this.ToString() + "-" + ticket; }
                public static implicit operator string(Ticket ticket) { return ticket.Value; }
            }
            //public class Menu
            //{
            //    public static readonly Menu Accepted = new Menu("Accepted");

            //    public string Value;
            //    private Menu(string menu) { Value = this.ToString() + "-" + menu; }
            //    public static implicit operator string(Menu menu) { return menu.Value; }
            //}
            public class Chat
            {
                public static readonly Chat Accepted = new Chat("Accepted");

                public string Value;
                private Chat(string chat) { Value = this.ToString() + "-" + chat; }
                public static implicit operator string(Chat chat) { return chat.Value; }
            }
            
            public static readonly Dictionary<string, string> List = new Dictionary<string, string>()
            {
                { "xx", "xxx" },
                { ModInit.CntFindData, "Couldn't load {0} File, creating new {0}"},
                { ModInit.LoadingData, "Trying to load {0}"},
                {"notAllwPickup", "You can't pick this item as owner is: {0}" },
                {"AdmModeRem", "You have deactivated Admin Mode" },
                {"AdmModeAdd", "You are now in Admin mode" },
                {"lvlRedxpSav", "Your Level was reduced, Lost xp has been saved." },
                {"lvlIncrxpRes", "Your Level has been increased, Lost xp Restored." },
                {"numbonly", "Incorrect format: Included letter in ticketID" },
                {Ticket.Created, "You have created a ticket" },
                {Ticket.Canceled, "You have Canceled your ticket" },
                {Ticket.Accepted, "Your Ticket has been accepted" },
                {Ticket.Declined, "Your Ticket has been declined" },
                {Ticket.AcceptedAdmin, "Your Ticket accepted the ticket" },
                {Ticket.DeclinedAdmin, "Your Ticket decline the ticket" },
                {Ticket.AlreadyExists, "You have already requested to change, Please conctact your admin" },
                {"TickClosLogin", "Welcome back, Your ticket was {0}" },
                {"TickList", "Ticket#: {0}, User: {1}" },
                {"TickDet", "Ticket Details:" },
                {"TickID", "Ticket ID: {0}" },
                {"TickName", "Username: {0}" },
                {"TickStmID", "SteamID: {0}" },
                {"TickSelc", "Selected: {0}" },
                {"TickRsn", "Reason: {0}" },
                {"TickDate", "Ticket Created: {0}" },
                {"TickCnt", "Open Tickets: {0}" },
                {"TickNotAvail", "Ticket#:{0} Does not exist" },
                {"ComndList", "PvX Command List:" },
                {"CompTickCnt", "Closed Tickets: {0}" },
                {"IncoForm", "Incorrect format" },
                {"IncoFormPleaUse", "Incorrect format Please Use:" },
                {"TicketDefaultReason", "Change Requested Via Chat" },
                {"NoTicket", "There are no tickets to display" },
                {"PvETarget", "You are attacking a PvE player" },
                {"NoActTick", "You do not have an active ticket" },
                {"RSNChan", "You have changed your tickets reason." },
                {"PvEStructure", "That structure belongs to a PvE player" },
                {"TargisGod", "You are attacking a god" },
                {"YouisGod", "You are attacking a god" },
                {"MissPerm", "You do not have the required permision" },
                {Menu.WelcomePage.P1L01, "Welcome To PvX Selector." },
                {Menu.WelcomePage.P1L02, "" },
                {Menu.WelcomePage.P1L03, "This mod allows you to select either PvP or PvE, this" },
                {Menu.WelcomePage.P1L04, "means you get to play on the server how you want to." },
                {Menu.WelcomePage.P1L05, "" },
                {Menu.WelcomePage.P1L06, "If you have not yet selected a mode please click on the" },
                {Menu.WelcomePage.P1L07, "character button and choose your mode, you can always" },
                {Menu.WelcomePage.P1L08, "change it later." },
                {Menu.WelcomePage.P1L09, "" },
                {Menu.WelcomePage.P1L10, "If you would like to see the servers configuration for the" },
                {Menu.WelcomePage.P1L11, "mod please click on the Settings button." },
                {Menu.WelcomePage.P1L12, "" },
                {Menu.WelcomePage.P1L13, "The Players panel allows you to see online players and" },
                {Menu.WelcomePage.P1L14, "what mode they are playing as." },
                {Menu.WelcomePage.P1L15, "" },
                {Menu.WelcomePage.P1L16, "If you have any questions please contact a server admin." },
                {Menu.WelcomePage.P1L17, "" },
                {Menu.WelcomePage.P1L18, "" },
                {Menu.WelcomePage.P1L19, "" },
                {Menu.WelcomePage.P1L20, "" },
                {Menu.WelcomePage.P1L21, "" },

                {Menu.WelcomePage.P2L01, "" },
                {Menu.WelcomePage.P2L02, "" },
                {Menu.WelcomePage.P2L03, "" },
                {Menu.WelcomePage.P2L04, "" },
                {Menu.WelcomePage.P2L05, "" },
                {Menu.WelcomePage.P2L06, "" },
                {Menu.WelcomePage.P2L07, "" },
                {Menu.WelcomePage.P2L08, "" },
                {Menu.WelcomePage.P2L09, "" },
                {Menu.WelcomePage.P2L10, "" },
                {Menu.WelcomePage.P2L11, "" },
                {Menu.WelcomePage.P2L12, "" },
                {Menu.WelcomePage.P2L13, "" },
                {Menu.WelcomePage.P2L14, "" },
                {Menu.WelcomePage.P2L15, "" },
                {Menu.WelcomePage.P2L16, "" },
                {Menu.WelcomePage.P2L17, "" },
                {Menu.WelcomePage.P2L18, "" },
                {Menu.WelcomePage.P2L19, "" },
                {Menu.WelcomePage.P2L20, "" },
                {Menu.WelcomePage.P2L21, "" },

                {Menu.WelcomePage.P3L01, "" },
                {Menu.WelcomePage.P3L02, "" },
                {Menu.WelcomePage.P3L03, "" },
                {Menu.WelcomePage.P3L04, "" },
                {Menu.WelcomePage.P3L05, "" },
                {Menu.WelcomePage.P3L06, "" },
                {Menu.WelcomePage.P3L07, "" },
                {Menu.WelcomePage.P3L08, "" },
                {Menu.WelcomePage.P3L09, "" },
                {Menu.WelcomePage.P3L10, "" },
                {Menu.WelcomePage.P3L11, "" },
                {Menu.WelcomePage.P3L12, "" },
                {Menu.WelcomePage.P3L13, "" },
                {Menu.WelcomePage.P3L14, "" },
                {Menu.WelcomePage.P3L15, "" },
                {Menu.WelcomePage.P3L16, "" },
                {Menu.WelcomePage.P3L17, "" },
                {Menu.WelcomePage.P3L18, "" },
                {Menu.WelcomePage.P3L19, "" },
                {Menu.WelcomePage.P3L20, "" },
                {Menu.WelcomePage.P3L21, "" },

                {Menu.WelcomePage.AP1L01, "Hello Admins and Moderators" },
                {Menu.WelcomePage.AP1L02, "" },
                {Menu.WelcomePage.AP1L03, "This page is here to provide you information about" },
                {Menu.WelcomePage.AP1L04, "the admin and moderator buttons." },
                {Menu.WelcomePage.AP1L05, "" },
                {Menu.WelcomePage.AP1L06, "Moderators+" },
                {Menu.WelcomePage.AP1L07, "Players Panel: This panel will be different from what" },
                {Menu.WelcomePage.AP1L08, "regular players see, the main difference is this provides" },
                {Menu.WelcomePage.AP1L09, "aditional information about a player including ticket count." },
                {Menu.WelcomePage.AP1L10, "" },
                {Menu.WelcomePage.AP1L11, "Tickets: This panel will allow you to view all tickets, from" },
                {Menu.WelcomePage.AP1L12, "this menu you will be able to accept or decline them." },
                {Menu.WelcomePage.AP1L13, "" },
                {Menu.WelcomePage.AP1L14, "Admin" },
                {Menu.WelcomePage.AP1L15, "Settings: Allows you to make changes to the mod on the fly," },
                {Menu.WelcomePage.AP1L16, "these changes are then saved to the config." },
                {Menu.WelcomePage.AP1L17, "" },
                {Menu.WelcomePage.AP1L18, "Debug: At this time it does not have a function, but I plan" },
                {Menu.WelcomePage.AP1L19, "to allow admins to fix and reset data files, possible I will" },
                {Menu.WelcomePage.AP1L20, "add the capability to enable debug chat in that pannel" },
                {Menu.WelcomePage.AP1L21, "" },

                {Menu.WelcomePage.AP2L01, "" },
                {Menu.WelcomePage.AP2L02, "" },
                {Menu.WelcomePage.AP2L03, "" },
                {Menu.WelcomePage.AP2L04, "" },
                {Menu.WelcomePage.AP2L05, "" },
                {Menu.WelcomePage.AP2L06, "" },
                {Menu.WelcomePage.AP2L07, "" },
                {Menu.WelcomePage.AP2L08, "" },
                {Menu.WelcomePage.AP2L09, "" },
                {Menu.WelcomePage.AP2L10, "" },
                {Menu.WelcomePage.AP2L11, "" },
                {Menu.WelcomePage.AP2L12, "" },
                {Menu.WelcomePage.AP2L13, "" },
                {Menu.WelcomePage.AP2L14, "" },
                {Menu.WelcomePage.AP2L15, "" },
                {Menu.WelcomePage.AP2L16, "" },
                {Menu.WelcomePage.AP2L17, "" },
                {Menu.WelcomePage.AP2L18, "" },
                {Menu.WelcomePage.AP2L19, "" },
                {Menu.WelcomePage.AP2L20, "" },
                {Menu.WelcomePage.AP2L21, "" },
            };
        }

        public class ModeSwitch//Ticket ReWrite Required??
        {
            public class Cooldown
            {
                public static bool Check(ulong PlayerID)
                {
                    if (Data.cooldownData.Cooldowns.ContainsKey(PlayerID))
                        if (Passed(PlayerID)) return true;
                        else return false;
                    else Add(PlayerID);
                    return true;
                }
                public static void Set(IPlayer Player)
                {
                    Data.cooldownData.Cooldowns[Convert.ToUInt64(Player.Id)] = Time();
                }

                static void Add(ulong PlayerID)
                {
                    Data.cooldownData.Cooldowns.Add(PlayerID, Time());
                }
                static bool Passed(ulong PlayerID)
                {
                    double CooldownValue = (Data.cooldownData.Cooldowns[PlayerID] + instance.CooldownTime);
                    if (Time() > CooldownValue) return true;
                    else return false;
                }
                static double Time()
                {
                    return (DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                }
                public static class Data
                {
                    public static CooldownStorage cooldownData;
                    public static DynamicConfigFile CooldownData;

                    public class CooldownStorage
                    {
                        public Dictionary<ulong, double> Cooldowns = new Dictionary<ulong, double>();
                    }
                    public static void Initiate()
                    {
                        Data.CooldownData = Interface.Oxide.DataFileSystem.GetFile("PvX/CooldownData");
                    }
                    public static void Load()
                    {
                        try
                        {
                            cooldownData = CooldownData.ReadObject<CooldownStorage>();
                            Messages.PutsRcon(Lang.ModInit.LoadingData, "Cooldown Data");
                        }
                        catch
                        {
                            Messages.PutsRcon(Lang.ModInit.CntFindData, "Cooldown Data");
                            cooldownData = new CooldownStorage();
                        }
                    }
                    public static void Save()
                    {
                        CooldownData.WriteObject(cooldownData);
                    }
                }
            }//Completed
            public class Ticket
            {
                public static void Create(BasePlayer Player, string selection)
                {
                    int _TicketNumber = GetNewID();
                    string _username = Player.displayName;
                    string _requested = selection;
                    string _reason = instance.lang.GetMessage("TicketDefaultReason", instance, Player.UserIDString);
                    Data.ticketData.Link.Add(_TicketNumber, Player.userID);
                    Data.ticketData.Tickets.Add(Player.userID, new Data.Ticket
                    {
                        CreatedTimeStamp = instance.DateTimeStamp(),
                        reason = instance.lang.GetMessage("TicketDefaultReason", instance, Player.UserIDString),
                        requested = selection,
                        TicketNumber = _TicketNumber,
                        UserId = Player.UserIDString,
                        Username = Player.displayName
                    });
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "TickCrea");
                    Players.Data.playerData.Info[Player.userID].ticket = true;
                    SaveData.All();
                    GUI.Update.AdminIndicator();
                    GUI.Update.PlayerIndicator(Player);
                }
                public static void Cancel(BasePlayer Player)
                {
                    if (Players.Data.playerData.Info[Player.userID].ticket == false) return;
                    int _ticketNumber = Data.ticketData.Tickets[Player.userID].TicketNumber;
                    Data.ticketData.Link.Remove(_ticketNumber);
                    Data.ticketData.Tickets.Remove(Player.userID);
                    Players.Data.playerData.Info[Player.userID].ticket = false;
                    SaveData.All();
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "TickCanc");
                    GUI.Update.PlayerIndicator(Player);
                    GUI.Update.AdminIndicator();
                    return;
                }
                public static void Accept(BasePlayer Admin, int TicketID)//Update required to fix Baseplayer NRE
                {
                    ulong _UserID = Data.ticketData.Link[TicketID];
                    AddToLog(Admin, TicketID, true);
                    Messages.Chat(Players.Find.Iplayer(Admin.userID), Lang.Ticket.AcceptedAdmin);
                    Players.Data.playerData.Info[_UserID].ticket = false;
                    Players.Data.playerData.Info[_UserID].mode = Data.ticketData.Tickets[_UserID].requested;
                    SaveData.All();
                    BasePlayer Player = Players.Find.BasePlayer(_UserID);
                    if (Player != null && Player.IsConnected)
                    {
                        Messages.Chat(Players.Find.Iplayer(Player.userID), Lang.Ticket.Accepted);
                        GUI.Update.PlayerIndicator(Player);
                        instance.UpdatePlayerChatTag(Player);
                    }
                    else if (Player != null && !Player.IsConnected)
                    {
                        Data.ticketData.Notification.Add(Player.userID, "Accepted");
                    }
                    else
                    {
                        Data.ticketData.Notification.Add(Player.userID, "Accepted");
                    }
                    Data.ticketData.Tickets.Remove(_UserID);
                    Data.ticketData.Link.Remove(TicketID);
                    SaveData.All();
                    GUI.Update.AdminIndicator();
                    if (Player != null && Player.IsConnected) GUI.Update.PlayerIndicator(Player);
                }
                public static void Decline(BasePlayer Admin, int TicketID)//updated: Fixed Baseplayer NRE
                {
                    ulong _UserID = Data.ticketData.Link[TicketID];
                    AddToLog(Admin, TicketID, false);
                    Messages.Chat(Players.Find.Iplayer(Admin.userID), Lang.Ticket.Accepted);
                    Players.Data.playerData.Info[_UserID].ticket = false;
                    Data.ticketData.Tickets.Remove(_UserID);
                    Data.ticketData.Link.Remove(TicketID);
                    SaveData.All();
                    GUI.Update.AdminIndicator();
                    BasePlayer Player = Players.Find.BasePlayer(_UserID);
                    if (Player != null && Player.IsConnected)
                    {
                        Messages.Chat(Players.Find.Iplayer(Player.userID), Lang.Ticket.Declined);
                        GUI.Update.PlayerIndicator(Player);
                    }
                    else Data.ticketData.Notification.Add(Player.userID, "Declined");
                }
                public static void Count(BasePlayer Player)
                {
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "TickCnt", Data.ticketData.Link.Count);
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "CompTickCnt", Data.logData.Logs.Count);
                }
                public static void Fix(BasePlayer Player)
                {

                }
                public static void List(BasePlayer Player)
                {
                    if (Data.ticketData.Link.Count > 0)
                    {
                        foreach (var ticket in Data.ticketData.Tickets)
                        {
                            ulong _key = ticket.Key;
                            Messages.Chat(Players.Find.Iplayer(Player.userID), "TickList", Data.ticketData.Tickets[_key].TicketNumber, Data.ticketData.Tickets[_key].Username);
                        }
                    }
                }
                public static void Info(BasePlayer Player, int TicketID)
                {
                    if (Data.ticketData.Link.ContainsKey(TicketID))
                    {
                        ulong _key = Data.ticketData.Link[TicketID];
                        //DateTime _date = DateTime.FromOADate(Ticket.Data.ticketData.Tickets[_key].timeStamp);
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickDet");
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickID", TicketID);
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickName", Data.ticketData.Tickets[_key].Username);
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickStmID", _key);
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickSelc", Data.ticketData.Tickets[_key].requested);
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickRsn", Data.ticketData.Tickets[_key].reason);
                        Messages.Chat(Players.Find.Iplayer(Player.userID), "TickDate", Data.ticketData.Tickets[_key].CreatedTimeStamp);
                    }
                    else Messages.Chat(Players.Find.Iplayer(Player.userID), "TickNotAvail", TicketID);
                }
                public static void AddToLog(BasePlayer _admin, int TicketID, bool Selection)
                {
                    ulong _UserID = Data.ticketData.Link[TicketID];
                    int _logID = NewLogID();
                    Data.logData.Logs.Add(_logID, new Data.Log
                    {
                        Accepted = Selection,
                        ClosedTimeStamp = instance.DateTimeStamp(),
                        AdminId = _admin.UserIDString,
                        CreatedTimeStamp = Data.ticketData.Tickets[_UserID].CreatedTimeStamp,
                        reason = Data.ticketData.Tickets[_UserID].reason,
                        requested = Data.ticketData.Tickets[_UserID].requested,
                        UserId = Data.ticketData.Tickets[_UserID].UserId,
                        AdminName = _admin.displayName,
                        Username = Data.ticketData.Tickets[_UserID].Username,
                    });
                }
                public static void ConsoleList()
                {
                    foreach (ulong _ticket in Data.ticketData.Tickets.Keys)
                    {
                        instance.Puts("    ");
                        instance.Puts("    ");
                        Messages.PutsRcon("TickDet");
                        Messages.PutsRcon("TickID", Data.ticketData.Tickets[_ticket].TicketNumber);
                        Messages.PutsRcon("TickName", Data.ticketData.Tickets[_ticket].Username);
                        Messages.PutsRcon("TickStmID", _ticket);
                        Messages.PutsRcon("TickSelc", Data.ticketData.Tickets[_ticket].requested);
                        Messages.PutsRcon("TickRsn", Data.ticketData.Tickets[_ticket].reason);
                        Messages.PutsRcon("TickDate", Data.ticketData.Tickets[_ticket].CreatedTimeStamp);
                    }
                }
                public static void ConsoleListLogs()
                {
                    foreach (int _ticket in Data.logData.Logs.Keys)
                    {
                        instance.Puts("    ");
                        instance.Puts("    ");
                        instance.Puts("Log Ticket");
                        instance.Puts("Accepted: {0}", Data.logData.Logs[_ticket].Accepted);
                        instance.Puts("CreatedTimeStamp: {0}", Data.logData.Logs[_ticket].CreatedTimeStamp);
                        instance.Puts("ClosedTimeStamp: {0}", Data.logData.Logs[_ticket].ClosedTimeStamp);
                        instance.Puts("Username: {0}", Data.logData.Logs[_ticket].Username);
                        instance.Puts("UserId: {0}", Data.logData.Logs[_ticket].UserId);
                        instance.Puts("AdminName: {0}", Data.logData.Logs[_ticket].AdminName);
                        instance.Puts("AdminId: {0}", Data.logData.Logs[_ticket].AdminId);
                        instance.Puts("Requested: {0}", Data.logData.Logs[_ticket].requested);
                        instance.Puts("Reason: {0}", Data.logData.Logs[_ticket].reason);
                    }
                }
                public static Dictionary<ulong,Data.Ticket> GetTickets()
                {
                    return Data.ticketData.Tickets;
                }

                
                static int GetNewID()
                {
                    for (int _i = 1; _i <= 500; _i++)
                    {
                        if (Data.ticketData.Link.ContainsKey(_i)) { }//Place Debug code in future
                        else
                        {
                            //Puts("Key {0} doesnt exist, Returning ticket number", _i); //debug
                            return _i;
                        }
                    }
                    return 0;
                }
                static int NewLogID()
                {
                    for (int _i = 1; _i <= 500; _i++)
                    {
                        if (Data.logData.Logs.ContainsKey(_i)) { }
                        else
                        {
                            //Puts("Key {0} doesnt exist, Returning ticket number", _i); //debug
                            return _i;
                        }
                    }
                    return 0;
                }

                public static class Data
                {
                    public static TicketStorage ticketData;
                    public static DynamicConfigFile TicketData;
                    public static LogStorage logData;
                    public static DynamicConfigFile LogData;

                    public class TicketStorage
                    {
                        public Dictionary<int, ulong> Link = new Dictionary<int, ulong>();
                        public Dictionary<ulong, Ticket> Tickets = new Dictionary<ulong, Ticket>();
                        public Dictionary<ulong, string> Notification = new Dictionary<ulong, string>();
                    }
                    public class LogStorage
                    {
                        public Dictionary<int, Log> Logs = new Dictionary<int, Log>();
                    }

                    public class Ticket
                    {
                        public int TicketNumber;
                        public string Username;
                        public string UserId;
                        public string requested;
                        public string reason;
                        public string CreatedTimeStamp;
                    }
                    public class Log
                    {
                        public string UserId;
                        public string AdminId;
                        public string Username;
                        public string AdminName;
                        public string requested;
                        public string reason;
                        public bool Accepted;
                        public string CreatedTimeStamp;
                        public string ClosedTimeStamp;
                    }

                    public static void Initiate()
                    {
                        Data.TicketData = Interface.Oxide.DataFileSystem.GetFile("PvX/TicketData");
                        Data.LogData = Interface.Oxide.DataFileSystem.GetFile("PvX/TicketLog");
                    }
                    public static void Load()
                    {
                        try
                        {
                            ticketData = TicketData.ReadObject<TicketStorage>();
                            Messages.PutsRcon(Lang.ModInit.LoadingData, "Ticket Data");
                        }
                        catch
                        {
                            Messages.PutsRcon(Lang.ModInit.CntFindData, "Ticket Data");
                            ticketData = new TicketStorage();
                        }
                        try
                        {
                            logData = LogData.ReadObject<LogStorage>();
                            Messages.PutsRcon(Lang.ModInit.LoadingData, "Log Data");
                        }
                        catch
                        {
                            Messages.PutsRcon(Lang.ModInit.CntFindData, "Log Data");
                            logData = new LogStorage();
                        }
                    }
                    public static void Save()
                    {
                        TicketData.WriteObject(Data.ticketData);
                        LogData.WriteObject(logData);
                    }
                }
            }
        }

        public class Players
        {
            public static class Data
            {
                public static PlayerStorage playerData;
                public static DynamicConfigFile PlayerData;
                public static List<ulong> OnlineAdmins = new List<ulong>();
                public static List<ulong> AdminModeEnabled = new List<ulong>();
                
                public class PlayerStorage
                {
                    public Hash<ulong, PlayerInfo> Info = new Hash<ulong, PlayerInfo>();
                    public List<ulong> Sleepers = new List<ulong>();
                    public List<ulong> UnknownUsers = new List<ulong>();
                    public Dictionary<ulong, double> Cooldown = new Dictionary<ulong, double>();
                }
                public class PlayerInfo
                {
                    public string username;
                    public string FirstConnection;
                    public string LatestConnection;
                    public string mode;
                    public bool ticket;
                }



                public static void Initiate()
                {
                    Data.PlayerData = Interface.Oxide.DataFileSystem.GetFile("PvX/PlayerData");
                }
                public static void Load()
                {
                    try
                    {
                        playerData = PlayerData.ReadObject<PlayerStorage>();
                        Messages.PutsRcon(Lang.ModInit.LoadingData, "Player Data");
                    }
                    catch
                    {
                        Messages.PutsRcon(Lang.ModInit.CntFindData, "Player Data");
                        playerData = new PlayerStorage();
                    }
                }
                public static void Save()
                {
                    PlayerData.WriteObject(Data.playerData);
                }
            }
            public class Find
            {
                public static IPlayer Iplayer(ulong ID)
                {
                    return instance.covalence.Players.FindPlayerById(ID.ToString());
                }
                public static BasePlayer BasePlayer(ulong ID)
                {
                    BasePlayer BasePlayer = BasePlayer.FindByID(ID);
                    if (BasePlayer == null) BasePlayer = BasePlayer.FindSleeping(ID);
                    return BasePlayer;
                }
            }
            public class ChangeMode
            {
                public static void Check(BasePlayer Player, string selection)
                {
                    bool tick = instance.TicketSystem;
                    bool cool = instance.CooldownSystem;
                    if (tick && !(cool))
                    {
                        if (ModeSwitch.Ticket.Data.ticketData.Tickets.ContainsKey(Player.userID)) return;
                        ModeSwitch.Ticket.Create(Player, selection);
                    }
                    if (tick && cool)
                    {
                        if (ModeSwitch.Ticket.Data.ticketData.Tickets.ContainsKey(Player.userID)) return;
                        if (ModeSwitch.Cooldown.Check(Player.userID)) Change(Find.Iplayer(Player.userID), selection);
                        else ModeSwitch.Ticket.Create(Player, selection);
                    }
                    if (!(tick) && cool)
                    {
                        if (ModeSwitch.Cooldown.Check(Player.userID)) Change(Find.Iplayer(Player.userID), selection);
                    }
                }
                public static void Change(IPlayer Player, string selection)
                {
                    ulong PlayerID = Convert.ToUInt64(Player.Id);
                    Data.playerData.Info[PlayerID].mode = selection;
                    ModeSwitch.Cooldown.Set(Player);
                    SaveData.All();




                    //Checks if connected, if so sends message and update gui
                    if (Player != null && Player.IsConnected)
                    {
                        Messages.Chat(Player, Lang.Ticket.Accepted);
                        GUI.Update.PlayerIndicator(Find.BasePlayer(Convert.ToUInt64(Player.Id)));
                        instance.UpdatePlayerChatTag(Find.BasePlayer(Convert.ToUInt64(Player.Id)));
                    }
                    SaveData.All();
                    if (Player != null && Player.IsConnected) GUI.Update.PlayerIndicator(Find.BasePlayer(Convert.ToUInt64(Player.Id)));
                }
            }
            public class State
            {
                //private readonly object HumanNPC;

                public static bool IsNA(ulong Player)
                {
                    if (Data.playerData.Info[Player].mode == Mode.NA) return true;
                    else return false;
                }
                public static bool IsNA(BaseCombatEntity _BaseCombat)
                {
                    BasePlayer Player = (BasePlayer)_BaseCombat;
                    if (Data.playerData.Info[Player.userID].mode == Mode.NA) return true;
                    else return false;
                }
                public static bool IsPvP(ulong Player)
                {
                    if (!Data.playerData.Info.ContainsKey(Player))
                    {
                        BasePlayer PlayerBP = Find.BasePlayer(Player);
                        if (PlayerBP == null)
                        {
                            AddOffline(Player);
                        }
                        else Add(PlayerBP);
                        return false;
                    }
                    if (Data.playerData.Info[Player].mode == Mode.PvP) return true;
                    else return false;
                }
                public static bool IsPvP(BaseCombatEntity _BaseCombat)
                {
                    BasePlayer Player = (BasePlayer)_BaseCombat;
                    if (!Data.playerData.Info.ContainsKey(Player.userID))
                    {
                        Add(Player);
                        return false;
                    }
                    if (Data.playerData.Info[Player.userID].mode == Mode.PvP) return true;
                    else return false;
                }
                public static bool IsPvE(ulong Player)
                {
                    if (!Data.playerData.Info.ContainsKey(Player))
                    {
                        BasePlayer PlayerBP = Find.BasePlayer(Player);
                        if (PlayerBP == null)
                        {
                            AddOffline(Player);
                        }
                        else Add(PlayerBP);
                        return false;
                    }
                    if (Data.playerData.Info[Player].mode == Mode.PvE) return true;
                    else return false;
                }
                public static bool IsPvE(BasePlayer Player)
                {
                    if (!Data.playerData.Info.ContainsKey(Player.userID))
                    {
                        Add(Player);
                        return false;
                    }
                    if (Data.playerData.Info[Player.userID].mode == Mode.PvE) return true;
                    else return false;
                }
                public static bool IsPvE(BaseCombatEntity _BaseCombat)
                {
                    BasePlayer Player = (BasePlayer)_BaseCombat;
                    if (!Data.playerData.Info.ContainsKey(Player.userID))
                    {
                        Add(Player);
                        return false;
                    }
                    if (Data.playerData.Info[Player.userID].mode == Mode.PvE) return true;
                    else return false;
                }
                public static bool IsNPC(ulong _test)
                {
                    if (instance.HumanNPC == null) return false;
                    else if (_test < 76560000000000000L) return true;
                    else return false;
                }
                public static bool IsNPC(BaseCombatEntity Player)
                {
                    BasePlayer _test = (BasePlayer)Player;
                    if (instance.HumanNPC == null) return false;
                    else if (_test.userID < 76560000000000000L) return true;
                    else return false;
                }
                public static bool IsNPC(PlayerCorpse _test)
                {
                    if (instance.HumanNPC == null) return false;
                    else if (_test.playerSteamID < 76560000000000000L) return true;
                    else return false;
                }
            }
            public static class Admins
            {
                public static List<ulong> Get()
                {
                    return Data.OnlineAdmins;
                }
                public static int Count()
                {
                    return Data.OnlineAdmins.Count();
                }
                public static bool AddPlayer(ulong playerID)
                {
                    if (Data.OnlineAdmins.Contains(playerID)) return false;
                    else Data.OnlineAdmins.Add(playerID);
                    return true;
                }
                public static bool RemovePlayer(ulong playerID)
                {
                    if (!Data.OnlineAdmins.Contains(playerID)) return false;
                    else Data.OnlineAdmins.Remove(playerID);
                    return true;
                }
                public class Mode
                {
                    public static bool ContainsPlayer(ulong playerID)
                    {
                        if (Data.AdminModeEnabled.Contains(playerID)) return true;
                        else return false;
                    }
                    public static bool Toggle(ulong playerID)
                    {
                        if (ContainsPlayer(playerID))
                            RemovePlayer(playerID);
                        else AddPlayer(playerID);
                        return true;
                    }
                    private static bool AddPlayer(ulong playerID)
                    {
                        if (Data.AdminModeEnabled.Contains(playerID)) return false;
                        else Data.AdminModeEnabled.Add(playerID);
                        Messages.Chat(Find.Iplayer(playerID), "AdmModeAdd");
                        return true;
                    }
                    private static bool RemovePlayer(ulong playerID)
                    {
                        if (!Data.AdminModeEnabled.Contains(playerID)) return false;
                        else Data.AdminModeEnabled.Remove(playerID);
                        Messages.Chat(Find.Iplayer(playerID), "AdmModeRem");
                        return true;
                    }
                }
            }

            public static void Add(BasePlayer Player)
            {
                Data.playerData.Info.Add(Player.userID, new Data.PlayerInfo
                {
                    username = Player.displayName,
                    mode = Mode.NA,
                    ticket = false,
                    FirstConnection = instance.DateTimeStamp(),
                    LatestConnection = instance.DateTimeStamp(),
                });
                Timers.PvxChatNotification(Player);
            }
            public static void AddSleeper(BasePlayer Player)
            {
                Data.playerData.Info.Add(Player.userID, new Data.PlayerInfo
                {
                    username = Player.displayName,
                    mode = Mode.NA,
                    ticket = false,
                    FirstConnection = instance.DateTimeStamp(),
                    LatestConnection = "Sleeper",
                });
                Data.playerData.Sleepers.Add(Player.userID);
            }
            public static void AddOffline(ulong _userID)
            {
                Data.playerData.Info.Add(_userID, new Data.PlayerInfo
                {
                    username = "UNKNOWN",
                    mode = Mode.NA,
                    ticket = false,
                    FirstConnection = instance.DateTimeStamp(),
                    LatestConnection = "UNKNOWN",
                });
                Data.playerData.UnknownUsers.Add(_userID);
            }
            public static bool HasTicket(ulong Player)
            {
                if (Data.playerData.Info[Player].ticket == true) return true;
                else return false;
            }
            public static string GetMode(ulong player)
            {
                return Data.playerData.Info[player].mode;
            }
            public static bool IsNew(ulong PlayerID)
            {
                if (Players.Data.playerData.Info.ContainsKey(PlayerID)) return false;
                return true;
            }
        }

        public class Timers
        {
            public static void PvxChatNotification(BasePlayer Player)
            {
                if (Players.State.IsNA(Player))
                {
                    Messages.Chat(Players.Find.Iplayer(Player.userID), "This server is running PvX, Please type /pvx for more info");
                    instance.timer.Once(10, () => PvxChatNotification(Player));
                }
            }

            public static void CooldownTickets()
            {
                bool tick = instance.TicketSystem;
                bool cool = instance.CooldownSystem;
                if (tick && cool)
                {
                    Dictionary<ulong, ModeSwitch.Ticket.Data.Ticket> TicketList = ModeSwitch.Ticket.GetTickets();
                    foreach (ulong PlayerID in TicketList.Keys)
                    {
                        if (ModeSwitch.Cooldown.Check(PlayerID))
                        {
                            IPlayer Iplayer = Players.Find.Iplayer(PlayerID);
                            BasePlayer FakeBP = new BasePlayer { name = "Cooldown Timer", displayName = "Cooldown Timer", userID = 0, UserIDString = "0" };
                            ModeSwitch.Ticket.Accept(FakeBP, TicketList[PlayerID].TicketNumber);
                            ModeSwitch.Cooldown.Set(Iplayer);
                        }
                    }
                }
                instance.timer.Once(60 * 5, () => CooldownTickets());
            }
        }

        public class Permisions
        {
            bool ContainsPlayer(ulong Player, string Perm)
            {
                IPlayer iPlayer = instance.covalence.Players.FindPlayerById(Player.ToString());
                return iPlayer.HasPermission(Perm);
            }
            void AddPlayer(ulong Player, string Perm)
            {
                IPlayer iPlayer = instance.covalence.Players.FindPlayerById(Player.ToString());
                iPlayer.GrantPermission(Perm);
            }
            void RemovePlayer(ulong Player, string Perm)
            {
                IPlayer iPlayer = instance.covalence.Players.FindPlayerById(Player.ToString());
                iPlayer.RevokePermission(Perm);
            }
        }


        public class Containers
        {
            public static List<ulong> AddContainerMode = new List<ulong>();
            private static List<ulong> RemoveContainerMode = new List<ulong>();
            private static List<ulong> PlayersInSharedChest = new List<ulong>();
            private static List<string> ValidChestTypes = new List<string>() { "box.wooden.large", "woodbox_deployed" };//"small_stash_deployed"

            //Adding Removing Container Handle

            public static bool AddContainer(StorageContainer _Container, ulong Player)
            {
                if (AddContainerMode.Count == 0)
                    return false;
                if (AddContainerMode.Contains(Player))
                {
                    if (Data.sharedContainerData.SharedContainers.ContainsKey(Player))
                        ReplaceContainer(_Container, Player);
                    else
                        Data.sharedContainerData.SharedContainers.Add(Player, _Container.net.ID);
                    //_Container.skinID = 20201;
                    AddContainerMode.Remove(Player);
                    SaveData.SharedContainer();
                    return true;
                }
                return false;
            }
            public static bool RemoveContainer(StorageContainer _Container, ulong Player = 0)
            {
                if (RemoveContainerMode.Count == 0) return false;
                else if (RemoveContainerMode.Contains(Player))
                {
                    if (Data.sharedContainerData.SharedContainers.ContainsKey(Player))
                        Data.sharedContainerData.SharedContainers.Remove(Player);
                    RemoveContainerMode.Remove(Player);
                    SaveData.SharedContainer();
                    return true;
                }
                return false;
            }
            public static bool ReplaceContainer(StorageContainer _Container, ulong Player)
            {
                if (Data.sharedContainerData.SharedContainers[Player] == _Container.net.ID)
                    return false;
                else
                    Data.sharedContainerData.SharedContainers[Player] = _Container.net.ID;
                return true;
            }
            public static bool AddToAddContainerList(ulong Player)
            {
                if (AddContainerMode.Contains(Player)) return false;
                else AddContainerMode.Add(Player);
                return true;
            }
            public static bool AddToRemoveContainerList(ulong Player)
            {
                if (RemoveContainerMode.Contains(Player)) return false;
                else RemoveContainerMode.Add(Player);
                return true;
            }

            //Using Shared Container Handle
            public static bool IsShared(StorageContainer _Container)
            {
                if (IsChestValidType(_Container))
                {
                    if (Data.sharedContainerData.SharedContainers.ContainsKey(_Container.OwnerID))
                    {
                        if (Data.sharedContainerData.SharedContainers[_Container.OwnerID] == _Container.net.ID)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            public static bool IsInSharedChest(ulong Player)
            {
                if (PlayersInSharedChest.Contains(Player)) return true;
                else return false;
            }
            public static void AddPlayerToInSharedChest(ulong Player)
            {
                PlayersInSharedChest.Add(Player);
            }
            public static void RemovePlayerFromInSharedChest(ulong Player)
            {
                PlayersInSharedChest.Remove(Player);
            }


            //Private
            private static bool IsChestValidType(StorageContainer _container)
            {
                if (ValidChestTypes.Contains(_container.ShortPrefabName)) return true;
                else return false;
            }

            public class Data
            {

                public static SharedContainerStorage sharedContainerData;
                public static DynamicConfigFile SharedContainerData;

                public class SharedContainerStorage
                {
                    public Dictionary<ulong, uint> SharedContainers = new Dictionary<ulong, uint>();
                }
                public static void Initiate()
                {
                    Data.SharedContainerData = Interface.Oxide.DataFileSystem.GetFile("PvX/SharedContainerData");
                }
                public static void Load()
                {
                    try
                    {
                        sharedContainerData = SharedContainerData.ReadObject<SharedContainerStorage>();
                        Messages.PutsRcon(Lang.ModInit.LoadingData, "Shared Container Data");
                    }
                    catch
                    {
                        Messages.PutsRcon(Lang.ModInit.CntFindData, "Shared Container Data");
                        sharedContainerData = new SharedContainerStorage();
                    }
                }
                public static void Save()
                {
                    SharedContainerData.WriteObject(sharedContainerData);
                }
            }
        }

        void OnLootItem(BasePlayer player, Item item)
        {

        }

        #endregion

        //void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
        //{
        //    if(trigger is BuildPrivilegeTrigger)
        //        trigger.
        //    Puts("OnEntityEnter works!");
        //}
        //void OnEntityLeave(TriggerBase trigger, BaseEntity entity)
        //{
        //    Puts("OnEntityLeave works!");
        //}


        void ModifyDamage(HitInfo HitInfo, float scale)
        {
            if (scale == 0f)
            {
                HitInfo.damageTypes = new DamageTypeList();
                HitInfo.DoHitEffects = false;
                HitInfo.HitMaterial = 0;
                HitInfo.PointStart = Vector3.zero;
                HitInfo.PointEnd = Vector3.zero;
            }
            else if (scale == 1) return;
            else
            {
                //Puts("Modify Damabe by: {0}", scale);
                HitInfo.damageTypes.ScaleAll(scale);
            }
        }

        string DateTimeStamp()
        {
            return DateTime.Now.ToString("HH:mm dd-MM-yyyy");
        }
        double GetTimeStamp()
        {
            return (DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Debug /////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////


        int DebugLevel = 0;
        void DebugMessage(int _minDebuglvl, string _msg)
        {
            if (DebugLevel >= _minDebuglvl)
            {
                Puts(_msg);
                if (DebugLevel == 3 && _minDebuglvl == 1)
                {
                    PrintToChat(_msg);
                }
            }
        }
        void OnRunPlayerMetabolism(PlayerMetabolism metabolism)
        {
            //if (metabolism.bleeding.GetType)
            //if (metabolism.heartrate) return;
            //if (metabolism.hydration) return;
            //if (metabolism.calories) return;
        }
    }
}

//Ticket accepted should be fixed for offline/dead players, now add update mechanism on playerinit
// config color + opacity
// Fix up/Shorten hooks eg: 


