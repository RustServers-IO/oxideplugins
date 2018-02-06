using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using System.Linq;
using System.Text.RegularExpressions;
using Rust;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("DynamicPVP", "CatMeat", "1.2.3", ResourceId = 2728)]
    [Description("Create temporary PVP zones around SupplyDrops, Tank and/or Heli (requires TruePVE and ZoneManager)")]

    public class DynamicPVP : RustPlugin
    {
        #region References
        [PluginReference]
        Plugin ZoneManager, TruePVE, ZoneDomes;
        #endregion

        #region Declarations
        bool starting = true;
        bool DynZoneEnabled = false;
        bool DynZoneDebug = false;
        bool PVPforSupply = true;
        bool PVPforTank = true;
        bool PVPforHeli = true;
        bool UseZoneDomes = false;
        bool BlockTeleport = true;
        bool DynDomes = false;
        bool ignoreSupplySignals = true;
        bool validcommand;

        float DynZoneDuration = 600;
        float DynZoneRadius = 100;
        float SupplySignalRadius = 50;
        float rtemp;
        float dtemp;

        string msg;
        string msgEnter = "Entering a PVP area!";
        string msgLeave = "Leaving a PVP area.";
        string debugfilename = "debug";

        List<BaseEntity> activeSupplySignals = new List<BaseEntity>();

        ConsoleSystem.Arg arguments;

        #endregion

        #region Initialization

        private bool ZoneCreateAllowed()
        {
            var ZoneManager = (Plugin)plugins.Find("ZoneManager");
            var TruePVE = (Plugin)plugins.Find("TruePVE");

            if ((TruePVE != null) && (ZoneManager != null))
                if (DynZoneEnabled)
                    return true;
            return false;
        }

        private bool DomeCreateAllowed()
        {
            var ZoneDomes = (Plugin)plugins.Find("ZoneDomes");

            if (ZoneDomes != null && UseZoneDomes)
                return true;
            return false;
        }

        void OnServerInitialized()
        {
            DebugPrint("ServerInitialized.", false);
            DebugPrint("Required plugins installed: " + ZoneCreateAllowed(), false);
            DebugPrint("Optional plugins installed: " + DomeCreateAllowed(), false);
            starting = false;
        }

        void Init()
        {
            LoadDefaultConfig();
        }

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        protected override void LoadDefaultConfig()
        {
            Config["DynZoneEnabled"] = DynZoneEnabled = GetConfig("DynZoneEnabled", true);
            Config["DynZoneDebug"] = DynZoneDebug = GetConfig("DynZoneDebug", false);
            Config["DynZoneDuration"] = DynZoneDuration = GetConfig("DynZoneDuration", 600);
            Config["DynZoneRadius"] = DynZoneRadius = GetConfig("DynZoneRadius", 100);
            Config["UseZoneDomes"] = UseZoneDomes = GetConfig("UseZoneDomes", false);
            Config["BlockTeleport"] = BlockTeleport = GetConfig("BlockTeleport", true);
            Config["PVPforSupply"] = PVPforSupply = GetConfig("PVPforSupply", true);
            Config["PVPforTank"] = PVPforTank = GetConfig("PVPforTank", true);
            Config["PVPforHeli"] = PVPforHeli = GetConfig("PVPforHeli", true);
            Config["msgEnter"] = msgEnter = GetConfig("msgEnter", "Entering a PVP area!");
            Config["msgLeave"] = msgLeave = GetConfig("msgLeave", "Leaving a PVP area.");
            Config["ignoreSupplySignals"] = ignoreSupplySignals = GetConfig("ignoreSupplySignals", true);

            SaveConfig();
        }
        #endregion

        #region Commands
        [ChatCommand("dynpvp")]
        private void cmdChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player?.net?.connection != null && player.net.connection.authLevel > 0)
            {
                if (args.Count() != 2)
                {
                    ProcessCommand(player, "list", "");
                    return;
                }
                ProcessCommand(player, args[0], args[1]);
            }
        }

        [ConsoleCommand("dynpvp")]
        private void cmdConsoleCommand(ConsoleSystem.Arg arg)
        {
            arguments = arg; //save for responding later
            if (arg.Args == null || arg.Args.Length != 2)
            {
                ProcessCommand(null, "list", "");
                return;
            }
            ProcessCommand(null, arg.Args[0], arg.Args[1]);
        }

        private void ProcessCommand(BasePlayer player, string command, string value)
        {
            command = command.Trim().ToLower();
            value = value.Trim().ToLower();
            validcommand = true;
            switch (command)
            {
                case "enabled":
                    switch (value)
                    {
                        case "true":
                            DynZoneEnabled = true;
                            break;
                        case "false":
                            DynZoneEnabled = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["DynZoneEnabled"] = DynZoneEnabled;
                        SaveConfig();
                    }
                    break;
                case "msgEnter":
                    msgEnter = value;
                    Config["msgEnter"] = msgEnter;
                    SaveConfig();
                    break;
                case "msgLeave":
                    msgLeave = value;
                    Config["msgLeave"] = msgLeave;
                    SaveConfig();
                    break;
                case "pvpforsupply":
                    switch (value)
                    {
                        case "true":
                            PVPforSupply = true;
                            break;
                        case "false":
                            PVPforSupply = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["PVPforSupply"] = PVPforSupply;

                        SaveConfig();
                    }
                    break;
                case "pvpfortank":
                    switch (value)
                    {
                        case "true":
                            PVPforTank = true;
                            break;
                        case "false":
                            PVPforTank = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["PVPforTank"] = PVPforTank;

                        SaveConfig();
                    }
                    break;
                case "pvpforheli":
                    switch (value)
                    {
                        case "true":
                            PVPforHeli = true;
                            break;
                        case "false":
                            PVPforHeli = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["PVPforHeli"] = PVPforHeli;

                        SaveConfig();
                    }
                    break;
                case "debug":
                    switch (value)
                    {
                        case "true":
                            DynZoneDebug = true;
                            break;
                        case "false":
                            DynZoneDebug = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["DynZoneDebug"] = DynZoneDebug;

                        SaveConfig();
                    }
                    break;
                case "ignore":
                    switch (value)
                    {
                        case "true":
                            ignoreSupplySignals = true;
                            break;
                        case "false":
                            ignoreSupplySignals = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["ignoreSupplySignals"] = ignoreSupplySignals;

                        SaveConfig();
                    }
                    break;
                case "dome":
                    switch (value)
                    {
                        case "true":
                            UseZoneDomes = true;
                            break;
                        case "false":
                            UseZoneDomes = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["UseZoneDomes"] = UseZoneDomes;

                        SaveConfig();
                    }
                    break;
                case "duration":

                    try
                    {
                        dtemp = Convert.ToSingle(value);
                    }
                    catch (Exception ex)
                    {
                        dtemp = 0;
                        validcommand = false;
                    }

                    if (dtemp > 0)
                    {
                        DynZoneDuration = dtemp;
                        Config["DynZoneDuration"] = DynZoneDuration;

                        SaveConfig();
                    }
                    break;
                case "radius":
                    try
                    {
                        rtemp = Convert.ToSingle(value);
                    }
                    catch (Exception ex)
                    {
                        rtemp = 0;
                        validcommand = false;
                    }

                    if (rtemp > 0)
                    {
                        DynZoneRadius = rtemp;
                        Config["DynZoneRadius"] = DynZoneRadius;

                        SaveConfig();
                    }
                    break;
                case "tpblock":
                    switch (value)
                    {
                        case "true":
                            BlockTeleport = true;
                            break;
                        case "false":
                            BlockTeleport = false;
                            break;
                        default:
                            validcommand = false;
                            break;
                    }
                    if (validcommand)
                    {
                        Config["BlockTeleport"] = BlockTeleport;

                        SaveConfig();
                    }
                    break;
                case "list":
                    // valid command but process later in method
                    break;
                default:
                    validcommand = false;
                    break;
            }
            if (validcommand)
            {
                if (command != "list")
                    RespondWith(player, "DynamicPVP: " + command + " set to " + value);
                else
                {
                    msg = "DynamicPVP current settings ===========";
                    msg = msg + "\n DynZoneEnabled: " + DynZoneEnabled.ToString();
                    msg = msg + "\n   DynZoneDebug: " + DynZoneDebug.ToString();
                    msg = msg + "\nDynZoneDuration: " + DynZoneDuration.ToString();
                    msg = msg + "\n  DynZoneRadius: " + DynZoneRadius.ToString();
                    msg = msg + "\n   UseZoneDomes: " + UseZoneDomes.ToString();
                    msg = msg + "\n  BlockTeleport: " + BlockTeleport.ToString();
                    msg = msg + "\n   PVPforSupply: " + PVPforSupply.ToString();
                    msg = msg + "\n     PVPforTank: " + PVPforTank.ToString();
                    msg = msg + "\n     PVPforHeli: " + PVPforHeli.ToString();
                    msg = msg + "\n       msgEnter: " + msgEnter;
                    msg = msg + "\n       msgLeave: " + msgLeave;
                    msg = msg + "\n  ignoreSignals: " + ignoreSupplySignals.ToString();

                    msg = msg + "\n=======================================\n";
                    RespondWith(player, msg);
                }
            }
            else
                RespondWith(player, "Syntax error!");
        }
        #endregion

        #region OxideHooks
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (starting)
                return;

            if (DynZoneEnabled && entity is SupplyDrop && PVPforSupply)
            {
                var DynDrop = entity as SupplyDrop;

                if (DynDrop == null || DynDrop.IsDestroyed)
                    return;

                var DynSpawnPosition = DynDrop.transform.position;
                DebugPrint("SupplyDrop spawned at " + DynSpawnPosition, false);
                var DynPosition = DynSpawnPosition;
                DynPosition.y = TerrainMeta.HeightMap.GetHeight(DynPosition);
                DebugPrint("SupplyDrop landing at " + DynPosition, false);

                if (IsProbablySupplySignal(DynPosition) && ignoreSupplySignals)
                    DebugPrint("PVP zone creation skipped.", false);
                else
                    CreateDynZone(DynPosition);
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (starting)
                return;

            if (DynZoneEnabled)
            {
                if (entity is BaseHelicopter && PVPforHeli)
                {
                    var DynHeli = entity as BaseHelicopter;

                    if (DynHeli == null || DynHeli.IsDestroyed)
                        return;

                    var DynSpawnPosition = DynHeli.transform.position;
                    var DynPosition = DynSpawnPosition;
                    DynPosition.y = TerrainMeta.HeightMap.GetHeight(DynPosition);
                    DebugPrint("PatrolHelicopter crash at " + DynPosition, false);

                    CreateDynZone(DynPosition);
                }

                if (entity is BradleyAPC && PVPforTank)
                {
                    var DynBradley = entity as BradleyAPC;

                    if (DynBradley == null || DynBradley.IsDestroyed)
                        return;

                    var DynSpawnPosition = DynBradley.transform.position;
                    var DynPosition = DynSpawnPosition;
                    DynPosition.y = TerrainMeta.HeightMap.GetHeight(DynPosition);
                    DebugPrint("BradleyAPC exploded at " + DynPosition, false);

                    CreateDynZone(DynPosition);
                }
            }
        }
        #endregion

        #region ZoneHandling
        void CreateDynZone(Vector3 DynPosition)
        {
            if (ZoneCreateAllowed())
            {
                string DynZoneID = DateTime.Now.ToString("HHmmssff");

                List<string> DynArgs = new List<string>();
                DynArgs.Add("name");
                DynArgs.Add("DynamicPVP");
                DynArgs.Add("radius");
                DynArgs.Add(DynZoneRadius.ToString());
                DynArgs.Add("enter_message");
                DynArgs.Add("Entering a PVP area!");
                DynArgs.Add("leave_message");
                DynArgs.Add("Leaving a PVP area.");
                DynArgs.Add("undestr");
                DynArgs.Add("true");
                if (BlockTeleport)
                {
                    DynArgs.Add("notp");
                    DynArgs.Add("true");
                }
                string[] DynZoneArgs = DynArgs.ToArray();

                var ZoneCreated = (bool)ZoneManager?.Call("CreateOrUpdateZone", DynZoneID, DynZoneArgs, DynPosition);

                if (ZoneCreated)
                {
                    DebugPrint("Created Zone: " + DynZoneID + " [" + DateTime.Now.ToString("HH:mm:ss") + "]", false);
                    if (DomeCreateAllowed())
                    {
                        bool DomeCreated = false;

                        DomeCreated = (bool)ZoneDomes?.Call("AddNewDome", null, DynZoneID);
                        if (DomeCreated)
                            DebugPrint("Dome created for Zone: " + DynZoneID, false);
                        else
                            DebugPrint("Dome NOT created for Zone: " + DynZoneID, true);
                    }
                    timer.Once(DynZoneDuration, () => { DeleteDynZone(DynZoneID); });

                    var MappingUpdated = (bool)TruePVE?.Call("AddOrUpdateMapping", DynZoneID, "exclude");

                    if (MappingUpdated)
                        DebugPrint("PVP enabled for Zone: " + DynZoneID + " " + DynPosition, false);
                    else
                        PrintWarning("PVP Mapping failed.");
                }
                else
                    PrintWarning("Zone creation failed.");
            }
        }

        void DeleteDynZone(string DynZoneID)
        {
            var MappingUpdated = (bool)TruePVE?.Call("RemoveMapping", DynZoneID);

            if (MappingUpdated) DebugPrint("PVP disabled for Zone: " + DynZoneID, false);

            if (DomeCreateAllowed())
            {
                var DomeDeleted = (bool)ZoneDomes?.Call("RemoveExistingDome", null, DynZoneID);

                if (DomeDeleted)
                    DebugPrint("Dome deleted for Zone: " + DynZoneID, false);
                else
                    PrintWarning("Dome NOT deleted for Zone: " + DynZoneID);
            }

            var ZoneDeleted = (bool)ZoneManager?.Call("EraseZone", DynZoneID);

            if (ZoneDeleted)
                DebugPrint("Deleted Zone: " + DynZoneID + " [" + DateTime.Now.ToString("HH:mm:ss") + "]", false);
            else
                PrintWarning("Zone deletion failed.");
        }
        #endregion

        #region Messaging

        void DebugPrint(string msg, bool warning)
        {
            if (DynZoneDebug)
            {
                switch (warning)
                {
                    case true:
                        PrintWarning(msg);
                        break;
                    case false:
                        Puts(msg);
                        break;
                }
                LogToFile(debugfilename, "[" + DateTime.Now.ToString() + "] | " + msg, this, true);
            }
        }

        void RespondWith(BasePlayer player, string msg)
        {
            if (player == null)
                arguments.ReplyWith(msg);
            else
                SendReply(player, msg);
            return;
        }

        #endregion

        #region SupplySignals

        bool IsProbablySupplySignal(Vector3 landingposition)
        {
            bool probable = false;

            // potential issues with signals thrown near each other (<40m)
            // definite issues with modifications that create more than one supply drop per cargo plane.
            // potential issues with player moving while throwing signal.

            DebugPrint($"Checking {activeSupplySignals.Count()} active supply signals", false);
            if (activeSupplySignals.Count() > 0)
            {
                foreach (BaseEntity supplysignal in activeSupplySignals.ToList())
                {
                    if (supplysignal == null)
                    {
                        activeSupplySignals.Remove(supplysignal);
                        continue;
                    }

                    Vector3 thrownposition = supplysignal.transform.position;
                    var xdiff = Math.Abs(thrownposition.x - landingposition.x);
                    var zdiff = Math.Abs(thrownposition.z - landingposition.z);

                    DebugPrint($"Known SupplySignal at {thrownposition} differing by {xdiff}, {zdiff}", false);

                    if (xdiff < SupplySignalRadius && zdiff < SupplySignalRadius)
                    {
                        probable = true;
                        activeSupplySignals.Remove(supplysignal);
                        DebugPrint("Found matching SupplySignal.", false);
                        DebugPrint($"Active supply signals remaining: {activeSupplySignals.Count()}", false);

                        break;
                    }
                }
            }
            if (!probable)
                DebugPrint($"No matches found, probably from a timed event cargo_plane", false);

            return probable;
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal))
                return;
            if (entity.net == null)
                entity.net = Network.Net.sv.CreateNetworkable();

            Vector3 position = entity.transform.position;

            if (activeSupplySignals.Contains(entity))
                return;
            SupplyThrown(player, entity, position);
            return;
        }

        void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || !(entity is SupplySignal))
                return;

            Vector3 position = entity.transform.position;

            if (activeSupplySignals.Contains(entity))
                return;
            SupplyThrown(player, entity, position);
            return;
        }

        void SupplyThrown(BasePlayer player, BaseEntity entity, Vector3 position)
        {
            Vector3 thrownposition = player.GetEstimatedWorldPosition();

            timer.Once(2.0f, () =>
            {
                if (entity == null)
                {
                    activeSupplySignals.Remove(entity);
                    return;
                }
            });

            timer.Once(2.3f, () =>
            {
                if (entity == null) return;
                activeSupplySignals.Add(entity);

                DebugPrint($"Detected SupplySignal thrown by '{player.displayName}' at: {thrownposition}", false);
                DebugPrint($"SupplySignal position of {position}", false);

            });
        }

        void OnAirdrop(CargoPlane plane, Vector3 dropPosition)
        {
            DebugPrint($"CargoPlane spawned, expecting drop at: {dropPosition}", false);
        }

        #endregion
    }
}