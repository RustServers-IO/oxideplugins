using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using System.Linq;
using System.Text.RegularExpressions;
using Rust;

namespace Oxide.Plugins
{
    [Info("DynamicPVP", "CatMeat", "1.0.2", ResourceId = 2728)]
    [Description("Creates temporary PVP zones around supply drops (requires TruePVE and ZoneManager)")]

    public class DynamicPVP : RustPlugin
    {
        #region References
        [PluginReference]
        Plugin ZoneManager, TruePVE, ZoneDomes;
        #endregion

        #region Declarations
        bool DynZoneEnabled;
        bool DynZoneDebug;
        float DynZoneDuration;
        float DynZoneRadius;
        bool UseZoneDomes;
        bool BlockTeleport;
        bool DynDomes;
        bool validcommand;
        float rtemp;
        float dtemp;
        string msg;
        #endregion

        #region Initialization
        void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == "TruePVE")
            {
                DynZoneEnabled = false;
                TruePVE = null;
            }
            if (plugin.Name == "ZoneManager")
            {
                DynZoneEnabled = false;
                ZoneManager = null;
            }
            if (plugin.Name == "ZoneDomes")
            {
                ZoneDomes = null;
            }
            SetFlags();
        }

        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin.Name == "TruePVE")
            {
                TruePVE = plugin;
            }
            if (plugin.Name == "ZoneManager")
            {
                ZoneManager = plugin;
            }
            if (plugin.Name == "ZoneDomes")
            {
                ZoneDomes = plugin;
            }
            SetFlags();
        }

        void SetFlags()
        {
            if (ZoneManager == null)
            {
                PrintWarning("'ZoneManager' not found!");
                DynZoneEnabled = false;
            }
            if (TruePVE == null)
            {
                PrintWarning("'TruePVE' not found!");
                DynZoneEnabled = false;
            }
            if (ZoneDomes == null)
            {
                DynDomes = false;
            }
            else
            {
                if (UseZoneDomes)
                {
                    DynDomes = true;
                }
                else
                {
                    DynDomes = false;
                    PrintWarning("'ZoneDomes' not found!");
                }
            }
        }

        void OnServerInitialized()
        {
            SetFlags();
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
            SaveConfig();
        }
        #endregion

        #region Commands
        [ChatCommand("dynpvp")]
        private void cmdChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player?.net?.connection != null)
            {
                if (player.net.connection.authLevel > 0)
                {
                    var x = args.Count();

                    if (x == 2)
                    {
                        ProcessCommand(player, args[0], args[1]);
                    }
                    else
                    {
                        ProcessCommand(player, "help", "");
                    }
                }
            }
        }

        [ConsoleCommand("dynpvp")]
        private void cmdConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 2)
            {
                arg.ReplyWith("Syntax error!");
                ProcessCommand(null, "help", "");
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
                case "help":
                    validcommand = false;
                    break;
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
                default:
                    validcommand = false;
                    break;
            }
            if (validcommand)
            {
                msg = "DynamicPVP: " + command + " set to " + value;

                if (player != null)
                {
                    msg = "DynamicPVP: " + command + " set to " + value;
                    SendReply(player, msg);
                }
                else
                {
                    Puts(msg);
                }
            }
            else
            {
                msg = "Syntax error!";

                if (player != null)
                {
                    SendReply(player, msg);
                }
                else
                {
                    Puts(msg);
                }
            }
        }
        #endregion

        #region OxideHooks
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (DynZoneEnabled)
            {
                if (entity is SupplyDrop)
                {
                    var DynDrop = entity as SupplyDrop;

                    if (DynDrop == null || DynDrop.IsDestroyed) return;

                    var DynSpawnPosition = DynDrop.transform.position;
                    var DynPosition = DynSpawnPosition;
                    DynPosition.y = 00.0f;
                    if (DynZoneDebug)
                    {
                        Puts("SupplyDrop landing at " + DynPosition);
                    }
                    CreateDynZone(DynPosition);
                }
            }
        }
        #endregion

        #region ZoneHandling
        void CreateDynZone(Vector3 DynPosition)
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
            if (BlockTeleport)
            {
                DynArgs.Add("notp");
                DynArgs.Add("true");
            }
            string[] DynZoneArgs = DynArgs.ToArray();

            var ZoneCreated = (bool)ZoneManager?.Call("CreateOrUpdateZone", DynZoneID, DynZoneArgs, DynPosition);

            if (ZoneCreated)
            {
                if (DynZoneDebug)
                {
                    Puts("Created Zone: " + DynZoneID + " [" + DateTime.Now.ToString("HH:mm:ss") + "]");
                }
                if (DynDomes)
                {
                    bool DomeCreated = false;

                    DomeCreated = (bool)ZoneDomes?.Call("AddNewDome", null, DynZoneID);
                    if (DomeCreated)
                    {
                        if (DynZoneDebug)
                        {
                            Puts("Dome created for Zone: " + DynZoneID);
                        }
                    }
                    else
                    {
                        PrintWarning("Dome NOT created for Zone: " + DynZoneID);
                    }
                }
                timer.Once(DynZoneDuration, () => { DeleteDynZone(DynZoneID); });

                var MappingUpdated = (bool)TruePVE?.Call("AddOrUpdateMapping", DynZoneID, "exclude");

                if (MappingUpdated)
                {
                    if (DynZoneDebug) { Puts("PVP enabled for Zone: " + DynZoneID + " " + DynPosition); }
                }
                else
                {
                    PrintWarning("PVP Mapping failed.");
                }
            }
            else
            {
                PrintWarning("Zone creation failed.");
            }
        }

        void DeleteDynZone(string DynZoneID)
        {
            var MappingUpdated = (bool)TruePVE?.Call("RemoveMapping", DynZoneID);

            if (MappingUpdated)
            {
                if (DynZoneDebug) { Puts("PVP disabled for Zone: " + DynZoneID); }
            }

            if (DynDomes)
            {
                var DomeDeleted = (bool)ZoneDomes?.Call("RemoveExistingDome", null, DynZoneID);

                if (DynZoneDebug)
                {
                    if (DomeDeleted)
                    {
                        Puts("Dome deleted for Zone: " + DynZoneID);
                    }
                    else
                    {
                        Puts("Dome NOT deleted for Zone: " + DynZoneID);
                    }
                }
            }

            var ZoneDeleted = (bool)ZoneManager?.Call("EraseZone", DynZoneID);

            if (ZoneDeleted)
            {
                if (DynZoneDebug) { Puts("Deleted Zone: " + DynZoneID + " [" + DateTime.Now.ToString("HH:mm:ss") + "]"); }
            }
            else
            {
                PrintWarning("Zone deletion failed.");
            }
        }
        #endregion
    }
}