using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins
{
    [Info("FortWars", "Sami37 - Naleen", "1.0.1", ResourceId = 1618)]
    class FortWars : RustPlugin
    {

        // FW Values
        private bool BuildPhase;
        private bool StartedGame;
        private bool FWEnabled;
        private int TimeBuild = 1200;
        private int TimeFight = 2400;
        private int TimeHeli = 600;
        private int TimeDropBuild = 300;
        private int TimeDropFight = 300;
        private int CraftBuild = 10;
        private int CraftFight = 600;
        private int HeliSpeed = 110;
        private int HeliHP = 200;
        private int HeliHPRudder = 30;
        private int BuildGatherMulti = 900;
        private int FightGatherMulti = 12;
        private int DropBuild = 0;
        private int DropFight = 1;


        public string PhaseStr { get; private set; }


        ////////////////////////////////////////////////////////////
        // Messages ////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            {"NotEnabled", "Fort Wars is disabled." },
            {"NoConfig", "Creating a new config file." },
            {"Title", "<color=orange>Fort Wars</color> : "},
            {"NoPerms", "You are not authorized to use this command."},
            {"BuildPhase", "Build Phase."},
            {"BuildPhaseTime", "{0} minutes of Build Phase remaining."},
            {"FightPhase", "Fight Phase."},
            {"FightPhaseTime", "{0} minutes of Fight Phase remaining."},
            {"HeliBuild", "It's Build phase, give them a chance."},
            {"HeliSpawn", "Spawning {0} helicopters."},
            {"LowBuildRate", "Build Rates are Lowered."},
            {"MoreHelicopters", "Helicopter spawns increased."},
            {"LowGatherRate", "Gathering rate lowered."},
            {"HiGatherRate", "Gathering rate Increased."},
            {"DropSpawn", "Spawning {0} Cargo Planes."},
            {"MoreCargoDrop", "Cargo Plane spawns increased."},
            {"Help", "Syntax: /{0} {1}."}
        };

        //Crafting
        private float CraftingRate { get; set; }

        List<ItemBlueprint> blueprintDefinitions = new List<ItemBlueprint>();

        private Dictionary<string, float> Blueprints { get; } = new Dictionary<string, float>();

        List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

        private List<string> Items { get; } = new List<string>();

        //Timers
        private List<Timer> AutoTimers = new List<Timer>();
        DateTime PhaseStart;

        //Resource Gather
        private int GatherRate { get; set; }
        private float GatherPC = 100;

        //Cargo
        private int MinX { get; set; }
        private int MaxX { get; set; }

        private int MinY { get; set; }
        private int MaxY { get; set; }

        private int MinZ { get; set; }
        private int MaxZ { get; set; }

        ////////////////////////////////////////////////////////////
        // Oxide Hooks /////////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        void Loaded()
        {
            LoadConfigVariables();
            lang.RegisterMessages(LangMessages, this);
            LoadPermissions();
            SaveConfig();
        }

        void OnServerInitialized()
        {
            int iWorldHalfSize = Convert.ToInt32(World.Size / 2);
            MinX = -iWorldHalfSize + 300;
            MaxX = iWorldHalfSize - 300;
            MinZ = -iWorldHalfSize + 300;
            MaxZ = iWorldHalfSize - 300;
            MinY = 250;
            MaxY = 400;
            blueprintDefinitions = ItemManager.bpList;
            foreach (var bp in blueprintDefinitions)
                Blueprints.Add(bp.targetItem.shortname, bp.time);

            itemDefinitions = ItemManager.itemList;
            Puts(itemDefinitions.Count.ToString());
            foreach (var itemdef in itemDefinitions)
                Items.Add(itemdef.displayName.english);

            CraftingRate = 100;
            GatherRate = 100;
            FWEnabled = true;
            UpdateCraftingRate();
            StartBuildPhase();
        }

        void LoadPermissions()
        {
            permission.RegisterPermission("FortWars.UseAll", this);
            permission.RegisterPermission("FortWars.UseHeli", this);
            permission.RegisterPermission("FortWars.UseFight", this);
            permission.RegisterPermission("FortWars.UseBuild", this);
            permission.RegisterPermission("FortWars.UseEnable", this); 
            permission.RegisterPermission("FortWars.UseDrop", this);
        }

        void Unload()
        {
            foreach(var players in BasePlayer.activePlayerList)
                SendReply(players, lang.GetMessage("NotEnabled", this, players.UserIDString));
            DestroyTimers();
            foreach (var bp in blueprintDefinitions)
                bp.time = Blueprints[bp.targetItem.shortname];
            CraftingRate = 100f;
            GatherRate = 100;
            UpdateCraftingRate();
        }

        private void StartBuildPhase()
        {
            DestroyTimers();
            BuildPhase = true;

            BroadcastToChat(lang.GetMessage("Title", this) +
                        lang.GetMessage("BuildPhase", this));
            
            BroadcastToChat(string.Format(lang.GetMessage("Title", this) + 
                lang.GetMessage("BuildPhaseTime", this), 
                TimeBuild / 60));

            //Build Rate
            CraftingRate = CraftBuild;

            //Gather Rate
            BroadcastToChat(string.Format(lang.GetMessage("Title", this) +
                lang.GetMessage("HiGatherRate", this),
                TimeBuild / 60));
            GatherRate = BuildGatherMulti;

            //Plane Wave
            StartDropWaves();

            //Update
            UpdateGatherRate();
            UpdateCraftingRate();

            //Timers
            PhaseStart = DateTime.Now.AddMinutes(TimeBuild/60d);
            AutoTimers.Add(timer.Once(TimeBuild, StartFightPhase));
        }

        private void StartFightPhase()
        {
            DestroyTimers();
            BuildPhase = false;

            BroadcastToChat(lang.GetMessage("Title", this) +
                        lang.GetMessage("FightPhase", this));

            BroadcastToChat(string.Format(lang.GetMessage("Title", this) +
                lang.GetMessage("FightPhaseTime", this),
                TimeFight / 60));

            //Heli Wave
            StartHeliWaves();

            // Low Build
            BroadcastToChat(lang.GetMessage("Title", this) +
                        lang.GetMessage("LowBuildRate", this));
            CraftingRate = CraftFight;

            //Low Gather
            BroadcastToChat(lang.GetMessage("Title", this) +
                        lang.GetMessage("LowGatherRate", this));
            GatherRate = FightGatherMulti;

            //Updates
            UpdateGatherRate();
            UpdateCraftingRate();

            //Timers
            PhaseStart = DateTime.Now.AddMinutes(TimeBuild / 60d);
            AutoTimers.Add(timer.Once(TimeFight, StartBuildPhase));

        }

        private void StartHeliWaves()
        {
            BroadcastToChat(lang.GetMessage("Title", this) +
                        lang.GetMessage("MoreHelicopters", this));
            callHeli();
            AutoTimers.Add(timer.Once(TimeHeli, StartHeliWaves));

        }

        private void StartDropWaves()
        {
            if (DropBuild != 0 || DropFight != 0) { 
                BroadcastToChat(lang.GetMessage("Title", this) +
                            lang.GetMessage("MoreCargoDrop", this));
                callDrop();
                if (DropBuild >= 1 && BuildPhase)
                    AutoTimers.Add(timer.Once(TimeDropBuild, StartDropWaves));
                else if (DropFight >= 1 && !BuildPhase)
                    AutoTimers.Add(timer.Once(TimeDropFight, StartDropWaves));
            }

        }

        private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (!entity.ToPlayer()) return;

            var amount = item.amount;

            item.amount = (int)(item.amount * GatherPC);

            dispenser.containedItems.Single(x => x.itemid == item.info.itemid).amount = (int)(amount * 1.5);

            if (dispenser.containedItems.Single(x => x.itemid == item.info.itemid).amount < 0)
                item.amount += (int)dispenser.containedItems.Single(x => x.itemid == item.info.itemid).amount;
        }

        ////////////////////////////////////////////////////////////
        // HeliCopter Spawn ////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        #region Helicopter
        void OnEntitySpawned(BaseNetworkable entity)
        {
            //994850627 is the prefabID of a heli.
            if (entity?.prefabID == 994850627)
            {
                BaseHelicopter heli = (BaseHelicopter)entity;
                heli.maxCratesToSpawn = 2;
                heli.bulletDamage = 10f;
                typeof(PatrolHelicopterAI).GetField("maxRockets", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(entity.GetComponent<PatrolHelicopterAI>(), 20);
            }
        }

        private void callHeli(int num = 1)
        {
            int i = 0;
            while (i < num)
            {
                BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab");
                if (!entity)
                    return;
                PatrolHelicopterAI heliAI = entity.GetComponent<PatrolHelicopterAI>();
                heliAI.maxSpeed = HeliSpeed;     //helicopter speed
                BaseCombatEntity BEntity = entity as BaseCombatEntity;
                if (BEntity != null)
                {
                    //Change the health & weakpoint(s) heath
                    BEntity.startHealth = HeliHP;
                    BaseHelicopter Heli = entity as BaseHelicopter;
                    if (Heli != null)
                    {
                        var weakspots = Heli.weakspots;
                        weakspots[0].maxHealth = HeliHP/2;
                        weakspots[0].health = HeliHP/2;
                        weakspots[1].maxHealth = HeliHPRudder;
                        weakspots[1].health = HeliHPRudder;
                    }
                    entity.Spawn();
                }
                i++;
            }
        }
        #endregion
        private void callDrop(int num = 1)
        {
            int i = 0;
            while (i < num)
            {
                BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/npc/cargo plane/cargo_plane.prefab");
                
                if (!entity)
                    return;
                CargoPlane cargoI = entity.GetComponent<CargoPlane>();
                cargoI.InitDropPosition(GetRandomWorldPos());
                entity.Spawn();
                i++;
            }
        }
        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        ////////////////////////////////////////////////////////////
        // Config //////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////   
        private void LoadConfigVariables()
        {

            SetConfig("Time", "Build", TimeBuild);
            SetConfig("Time", "Fight", TimeFight);
            SetConfig("Time", "Heli", TimeHeli);
            SetConfig("Time", "Drop Build", TimeDropBuild);
            SetConfig("Time", "Drop Fight", TimeDropFight);
            SetConfig("Craft", "Build", CraftBuild);
            SetConfig("Craft", "Fight", CraftFight);
            SetConfig("Drop", "Build", DropBuild);
            SetConfig("Drop", "Fight", DropFight);
            SetConfig("Heli", "Speed", HeliSpeed);
            SetConfig("Heli", "HP", HeliHP);
            SetConfig("Heli", "HPRudder", HeliHPRudder);
            SetConfig("Gather", "Build", BuildGatherMulti);
            SetConfig("Gather", "Fight", FightGatherMulti);

            SaveConfig();

            TimeBuild = GetConfig(TimeBuild, "Time", "Build");
            TimeFight = GetConfig(TimeFight, "Time", "Fight");
            TimeHeli = GetConfig(TimeHeli, "Time", "Heli");
            TimeDropBuild = GetConfig(TimeDropBuild, "Time", "Drop Build");
            TimeDropFight = GetConfig(TimeDropFight, "Time", "Drop Fight");

            CraftBuild = GetConfig(CraftBuild, "Craft", "Build");
            CraftFight = GetConfig(CraftFight, "Craft", "Fight");

            DropBuild = GetConfig(DropBuild, "Drop", "Build");
            DropFight = GetConfig(DropFight, "Drop", "Fight");

            HeliSpeed = GetConfig(HeliSpeed, "Heli", "Speed");
            HeliHP = GetConfig(HeliHP, "Heli", "HP");
            HeliHPRudder = GetConfig(HeliHPRudder, "Heli", "HPRudder");

            BuildGatherMulti = GetConfig(BuildGatherMulti, "Gather", "Build");
            FightGatherMulti = GetConfig(FightGatherMulti, "Gather", "Fight");

            Puts("Configuration file loaded.");
        }

        ////////////////////////////////////////////////////////////
        // Console Commands ////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        #region console commands
        [ChatCommand("phase")]
        private void chatcmdPhase(BasePlayer player, string command, string[] arg)
        {
            PhaseStr = "Not Enabled";

            TimeSpan timeRemaining = new TimeSpan();
            if (FWEnabled)
            {

                timeRemaining = PhaseStart.Subtract(DateTime.Now);
                PhaseStr = lang.GetMessage(BuildPhase ? "BuildPhaseTime" : "FightPhaseTime", this, player.UserIDString);
            }
            SendReply(player, PhaseStr, (int)timeRemaining.TotalMinutes + 1);
        }

        [ChatCommand("hell")]
        private void chatcmdHell(BasePlayer player, string command, string[] arg)
        {
            if (!IsAllowed(player, "FortWars.UseAll", false))
                if (!IsAllowed(player, "FortWars.UseHeli")) return;

            int num = 1;
            PhaseStr = lang.GetMessage("NotEnabled", this, player.UserIDString);
            if (FWEnabled)
            {
                PhaseStr = lang.GetMessage("HeliBuild", this, player.UserIDString);
                if (!BuildPhase)
                {
                    if (arg == null || arg.Length != 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage("Help", this, player.UserIDString), "hell", "<number of heli>"));
                        return;
                    }
                    
                    bool result = Int32.TryParse(arg[0], out num);
                    if (!result)
                        num = 1;
                    callHeli(num);
                    PhaseStr =
                        lang.GetMessage("Title", this) + 
                        lang.GetMessage("HeliSpawn", this, player.UserIDString);

                }
            }
            SendReply(player, PhaseStr, num.ToString());
        }

        [ChatCommand("drop")]
        private void chatcmdDrop(BasePlayer player, string command, string[] arg)
        {
            if (!IsAllowed(player, "FortWars.UseAll", false))
                if (!IsAllowed(player, "FortWars.UseDrop")) return;
            if (arg == null || arg.Length != 1)
            {
                SendReply(player, string.Format(lang.GetMessage("Help", this, player.UserIDString), "drop", "<number of airdrop>"));
                return;
            }
            int num = 1;
            PhaseStr = lang.GetMessage("NotEnabled", this, player.UserIDString);
            if (FWEnabled)
            {
                bool result = Int32.TryParse(arg[0], out num);
                if (!result)
                    num = 1;
                callDrop(num);
                PhaseStr =
                    lang.GetMessage("Title", this) +
                    lang.GetMessage("DropSpawn", this, player.UserIDString);

            }
            SendReply(player, PhaseStr, num.ToString());
        }

        [ConsoleCommand("fw.fight")]
        void ccmdFight(ConsoleSystem.Arg arg)
        {
            if (!IsAllowed(arg, "FortWars.UseAll", false))
                if (!IsAllowed(arg, "FortWars.UseFight", true)) return;

            StartFightPhase();
        }

        [ConsoleCommand("fw.build")]
        void ccmdBuild(ConsoleSystem.Arg arg)
        {
            if(!IsAllowed(arg, "FortWars.UseAll", false))
                if(!IsAllowed(arg, "FortWars.UseBuild", true)) return;

            StartBuildPhase();
        }

        [ConsoleCommand("fw.enable")]
        void ccmdEnable(ConsoleSystem.Arg arg)
        {
            if (!IsAllowed(arg, "FortWars.UseAll", false))
                if (!IsAllowed(arg, "FortWars.UseEnable")) return;

            var rate = arg.GetInt(0);
            if (rate == 1)
            {
                FWEnabled = true;
                StartBuildPhase();
                return;
            }
            if (rate == 0)
            {
                FWEnabled = false;
                arg.ReplyWith(lang.GetMessage("NotEnabled", this));
                foreach (var players in BasePlayer.activePlayerList)
                {
                    SendReply(players, lang.GetMessage("NotEnabled", this, players.UserIDString));
                }
                DestroyTimers();
            }

        }

        #endregion
        ////////////////////////////////////////////////////////////
        // Utilities ///////////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        #region Utilities 
        void UpdateGatherRate()
        {
            GatherPC = GatherRate / 100;
            if (GatherPC < 1) GatherPC = 1;
        }

        void DestroyTimers()
        {
            foreach (Timer eventimer in AutoTimers)
            {
                eventimer.Destroy();
            }
            
            AutoTimers.Clear();
        }

        bool IsAllowed(BasePlayer player, string perm, bool bmsg = true)
        {
            if (permission.UserHasPermission(player.userID.ToString(), perm)) return true;
            if (bmsg)
                SendReply(player, lang.GetMessage("NoPerms", this, player.UserIDString));
            return false;
        }

        bool IsAllowed(ConsoleSystem.Arg arg, string perm, bool bmsg = true)
        {
            if (permission.UserHasPermission(arg.Player().userID.ToString(), perm)) return true;
            if(bmsg)
                SendReply(arg, lang.GetMessage("NoPerms", this, arg.Player().UserIDString));
            return false;
        }

        ////////////////////////////////////////////////////////////
        // Update Crafting /////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        private void UpdateCraftingRate()
        {
            foreach (var bp in blueprintDefinitions)
            {
                bp.time = Blueprints[bp.targetItem.shortname] * CraftingRate / 100;
            }
        }

        ////////////////////////////////////////////////////////////
        // Chat Broadcast //////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        void BroadcastToChat(string msg)
        {
            Puts(msg);
            rust.BroadcastChat(msg);
        }

        ////////////////////////////////////////////////////////////
        // Random //////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////
        private Vector3 GetRandomWorldPos()
        {
            var x = Random.Range(MinX, MaxX + 1) + 1;
            var y = Random.Range(MinY, MaxY + 1);
            var z = Random.Range(MinZ, MaxZ + 1) + 1;

            return new Vector3(x, y, z);
        }

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);
            if (Config.Get(stringArgs.ToArray()) == null)
                Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }
        #endregion
    }
}

