/*ToDo:  
1. Look into getting resources from salvage in a nodamage zone, and prefabs seem to give more resources.
 
 Known Bugs: 
 2 zones overlapping causes enter/exit messages to not work properly, but otherwise work fine.
 For some reason when using a treb, and a ballista is outside of the structure your attacking, it does 1500 damage to the structure(in a nodamage zone) if the blocks already have damage
 
  */
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using CodeHatch.Engine.Networking;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Common;
using CodeHatch.StarForge.Sleeping;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Blocks.Inventory;
using CodeHatch.Blocks;
using CodeHatch.ItemContainer;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.Networking.Events;
using CodeHatch.TerrainAPI;
using CodeHatch.Networking.Events.Players;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("ProtectedZone", "Mordeus", "1.2.1")]
    public class ProtectedZone : ReignOfKingsPlugin
    {
        [PluginReference]
        Plugin AntiLoot;

        private DynamicConfigFile ProtectedZoneData;
        private StoredData storedData;
        private readonly Dictionary<string, ZoneInfo> ZoneDefinitions = new Dictionary<string, ZoneInfo>();
        private Dictionary<Player, PlayerData> PData;
        //config
        private bool MessagesOn => GetConfig("MessagesOn", false);
        private bool ZoneCheckOn => GetConfig("ZoneCheckOn", false);
        private float MessageInterval => GetConfig("MessageInterval", 100f);
        private float ZoneCheckInterval => GetConfig("ZoneCheckInterval", 1f);
        private bool CrestCheckOn => GetConfig("CrestCheckOn", false);
        private bool AdminCanBuild => GetConfig("AdminCanBuild", true);
        private bool AdminCanKill => GetConfig("AdminCanKill", true);
        private bool UseAntiLoot => GetConfig("UseAntiLoot", false);
        List<Vector2> zones = new List<Vector2>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<string, Timer> ZoneCheckTimer = new Dictionary<string, Timer>();
        private bool AntiLootLoaded = false;

        #region Data
        protected override void LoadDefaultConfig()
        {
            Config["MessageInterval"] = MessageInterval;
            Config["ZoneCheckInterval"] = ZoneCheckInterval;
            Config["MessagesOn"] = MessagesOn;
            Config["ZoneCheckOn"] = ZoneCheckOn;
            Config["CrestCheckOn"] = CrestCheckOn;
            Config["AdminCanBuild"] = AdminCanBuild;
            Config["AdminCanKill"] = AdminCanKill;
            Config["UseAntiLoot"] = UseAntiLoot;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                { "notAllowed", "[F5D400]You are not allowed to do this![FFFFFF]" },
                { "areaProtected", "[F5D400]This area is Protected![FFFFFF]" },
                { "noBuild", "[F5D400]No building in this area![FFFFFF]" },
                { "noPlace", "[F5D400]You cannot place a {0} Here![FFFFFF]" },
                { "noPvP", "[F5D400]This area is Protected, no PvP![FFFFFF]" },
                { "noSleeper", "[F5D400]This area is Protected, You can not damage a sleeper![FFFFFF]" },
                { "noCrest", "[F5D400]This area is Protected, You can not damage a crest![FFFFFF]" },
                { "noRope", "[F5D400]This area is Protected, You can not rope another player![FFFFFF]" },
                { "noPVE", "[F5D400]This area is Protected, You can not kill{0}'s![FFFFFF]" },
                { "noPreFab", "[F5D400]This area is Protected, You can not damage a{0}![FFFFFF]" },
                { "noEntry", "[F5D400]You cannot enter this zone![FFFFFF]" },
                { "help", "[F5D400]type /zone help to open the help menu[FFFFFF]"},
                { "synError", "[F5D400]Syntax Error: [FFFFFF]Type '/zone help' to view available options" },
                { "nameExists", "[0000FF]The Name {0} already exists[FFFFFF]" },
                { "zoneAdded", "[4F9BFF]Zone [FFFFFF]{0}[4F9BFF] sucessfully added, named [FFFFFF]{1}." },
                { "zoneInfo", "[FFFFFF]This is ZoneID [4F9BFF]{0}[FFFFFF], Zone Name [4F9BFF]{1}[FFFFFF]" },
                { "zoneList", "[FFFFFF]ZoneID [4F9BFF]{0}[FFFFFF], Zone Name [4F9BFF]{1}[FFFFFF], Location [4F9BFF]{2}[FFFFFF]" },
                { "zoneEdited", "[4F9BFF]You have changed the {0} of ZoneID {1} to {2}.[FFFFFF]" },
                { "zoneLocError", "[F5D400]You are not standing in a zone.[FFFFFF]" },
                { "noZoneError",  "[F5D400]No Zones loaded.[FFFFFF]" },
                { "zoneError", "[F5D400]That zone does not exist.[FFFFFF]" },
                { "inZoneError", "[F5D400]You are currently to close to a zone, you cannot make another.[FFFFFF]" },
                { "zoneRemove", "[0000FF]ZoneID {0} was removed.[FFFFFF]" },
                { "zoneMessage", "[4F9BFF]You have entered {0} zone.[FFFFFF]" },
                { "zoneFlag1", "[4F9BFF]radius: [FFFFFF]{0}" },
                { "zoneFlag2", "[4F9BFF]nopvp Flag: [FFFFFF]{0}" },
                { "zoneFlag3", "[4F9BFF]nobuild Flag: [FFFFFF]{0}" },
                { "zoneFlag4", "[4F9BFF]nodamage Flag: [FFFFFF]{0}" },
                { "zoneFlag5", "[4F9BFF]nosleeperdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag6", "[4F9BFF]nocrestdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag7", "[4F9BFF]messageon Flag: [FFFFFF]{0}" },
                { "zoneFlag8", "[4F9BFF]entermessageon Flag: [FFFFFF]{0}" },
                { "zoneFlag9", "[4F9BFF]exitmessageon Flag: [FFFFFF]{0}" },
                { "zoneFlag10", "[4F9BFF]zonemessage: [FFFFFF]{0}" },
                { "zoneFlag11", "[4F9BFF]entermessage: [FFFFFF]{0}" },
                { "zoneFlag12", "[4F9BFF]exitmessage: [FFFFFF]{0}" },
                { "zoneFlag13", "[4F9BFF]noroping Flag: [FFFFFF]{0}" },
                { "zoneFlag14", "[4F9BFF]nopve Flag: [FFFFFF]{0}" },
                { "zoneFlag15", "[4F9BFF]noprefabdamage Flag: [FFFFFF]{0}" },
                { "zoneFlag16", "[4F9BFF]ejectplayer Flag: [FFFFFF]{0}" },
                { "zoneFlag17", "[4F9BFF]ejectsleeper Flag: [FFFFFF]{0}" },
                { "zoneFlag18", "[4F9BFF]nochestlooting Flag: [FFFFFF]{0}"  },
                { "zoneFlag19", "[4F9BFF]nostationlooting Flag: [FFFFFF]{0}"  },
                { "zoneFlag20", "[4F9BFF]nocampfirelooting Flag: [FFFFFF]{0}" },
                { "zoneFlag21", "[4F9BFF]notorchlooting Flag: [FFFFFF]{0}" },
                { "zoneFlag22", "[4F9BFF]nofireplacelooting Flag: [FFFFFF]{0}" },
                { "zoneFlag23", "[4F9BFF]nofurniturelooting Flag: [FFFFFF]{0}" },
                { "zoneFlag24", "[4F9BFF]novillagerlooting Flag: [FFFFFF]{0}" },
                { "logPvP", "player {0} attempted to a harm a player ,cancelling damage." },
                { "logSleeper", "player {0} attempted to kill a Sleeper ,cancelling damage." },
                { "logCrest", "player {0} attempted to damage a crest ,cancelling damage." },
                { "logNoBuild", "player {0} attempted to build in a no-build zone,cancelling placement."},
                { "logNoDamage", "player {0} attempted to damage a block ,cancelling damage."},
                { "logCrestPlace", "player {0} attempted to place a {1} in a no-build zone, cancelling placement."},
                { "logNoRope", "player {0} attempted to rope a player in a no-roping zone, cancelling."},
                { "logNoPVE", "player {0} attempted to kill a {1} in a no-pve zone, cancelling."},
                { "logNoPreFab", "player {0} attempted to damage a {1} in a no-prefab damage zone, cancelling."},
                { "logEjectSleeper", "player {0} ejected from a no-sleeper zone."},
                { "logEjectPlayer", "player {0} ejected from a no-player zone."},
                { "helpTitle", $"[4F9BFF]{Title}  v{Version}"},
                { "helpHelp", "[4F9BFF]/zone help[FFFFFF] - Display the help menu"},
                { "helpAdd", "[4F9BFF]/zone add <name> [FFFFFF]- Sets Zone."},
                { "helpList", "[4F9BFF]/zone list [FFFFFF]- Lists all zones"},
                { "helpRemove", "[4F9BFF]/zone remove <num> [FFFFFF]- Removes zone."},
                { "helpInfo", "[4F9BFF]/zone info [FFFFFF]- Zone info"},
                { "helpEdit", "[4F9BFF]/zone edit <name>[FFFFFF]- Edit zone values."},
                { "cslzoneAdded", "Zone {0} sucessfully addded, named {1}." },
                { "cslnameExists", "The Name {0} already exists" },
                { "cslinZoneError", "This is too close to a zone, you cannot make another." },
                { "cslformatError", "Invalid location format, use: \"x y z\" or here." },                               

        }, this);
        }
        void OnServerInitialized()
        {
            CacheAllOnlinePlayers();
            if (UseAntiLoot)
            {
                try
                {
                    AntiLoot.Call("isLoaded", null);
                    Puts("AntiLoot is loaded, you now have more options in ProtectedZone.");
                    AntiLootLoaded = true;

                }
                catch (Exception)
                {
                    PrintWarning("AntiLoot is missing. Change AntiLootEnabled in the config to false to use without.");
                    AntiLootLoaded = false;                    
                    return;
                }
            }
        }        
        private void Init()
        {
            LoadDefaultConfig();            
            ProtectedZoneData = Interface.Oxide.DataFileSystem.GetFile("ProtectedZone");
            ProtectedZoneData.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };
            LoadZones();
            LoadData();
            PData = new Dictionary<Player, PlayerData>();
        }
        private void LoadData()
        {

            ZoneDefinitions.Clear();
            try
            {
                ProtectedZoneData.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = ProtectedZoneData.ReadObject<StoredData>();
                Puts("Loaded {0} Zone definitions", storedData.ZoneDefinitions.Count);
            }
            catch
            {
                Puts("Failed to load StoredData");
                storedData = new StoredData();
            }
            ProtectedZoneData.Settings.NullValueHandling = NullValueHandling.Include;
            foreach (var zonedef in storedData.ZoneDefinitions)
                ZoneDefinitions[zonedef.Id] = zonedef;

        }
        private class StoredData
        {
            public readonly HashSet<ZoneInfo> ZoneDefinitions = new HashSet<ZoneInfo>();
        }
        private void SaveData()
        {
            ProtectedZoneData.WriteObject(storedData);
            PrintWarning("Saved ProtectedZone data");
        }
        #endregion
        #region Zone Definition
        public class ZoneInfo
        {
            public string ZoneName;
            public string Id;
            public Vector3 Location;            
            public float ZoneX;
            public float ZoneY;
            public float ZoneZ;
            public string ZoneCreatorName;
            public float ZoneRadius;
            public bool ZoneNoPVP = false;
            public bool ZoneNoBuild = false;
            public bool ZoneNoDamage = false;
            public bool ZoneNoSleeperDamage = false;
            public bool ZoneNoCrestDamage = false;
            public bool ZoneNoPlayerRoping = false;
            public bool ZoneNoPVE = false;
            public bool ZoneNoPreFabDamage = false;
            public bool ZoneEjectPlayer = false;
            public bool ZoneEjectSleeper = false;
            public bool ZoneNoChestLooting = false;
            public bool ZoneNoStationLooting = false;
            public bool ZoneNoCampfireLooting = false;
            public bool ZoneNoFireplaceLooting = false;
            public bool ZoneNoTorchLooting = false;
            public bool ZoneNoFurnitureLooting = false;
            public bool ZoneNoVillagerLooting = false;
            public bool ZoneMessageOn = false;
            public bool ZoneEnterMessageOn = false;
            public bool ZoneExitMessageOn = false;
            public string ZoneMessage = "This is a no PvP zone.";
            public string EnterZoneMessage = "You have entered a no PvP zone.";
            public string ExitZoneMessage = "You have exited a no PvP zone.";

            public ZoneInfo()
            {
            }

            public ZoneInfo(Vector3 position)
            {
                ZoneRadius = 20f;
                Location = position;
            }

        }
        #endregion
        #region Player Data
        class PlayerData
        {
            public ulong PlayerId;
            public bool EnterZone;
            public bool ExitZone;
            public bool InZone;
            public string ZoneId;
            public DateTime TimeEnterZone;

            public PlayerData(ulong playerId)
            {
                PlayerId = playerId;
                ZoneId = "0";
                EnterZone = false;
                ExitZone = false;
                InZone = false;
                TimeEnterZone = DateTime.Now;
            }
        }
        #endregion

        #region Commands

        [ChatCommand("zone")]
        private void ZoneCommand(Player player, string cmd, string[] args)
        {
            string playerId = player.Id.ToString();
            if (!player.HasPermission("admin"))
            {
                player.SendError(lang.GetMessage("notAllowed", this, playerId));
                return;
            }
            if (args == null || args.Length == 0)
            {
                player.SendError(lang.GetMessage("help", this, playerId));
                return;
            }
            switch (args[0])
            {
                case "help":
                    {

                        SendReply(player, lang.GetMessage("helpTitle", this, playerId));
                        SendReply(player, lang.GetMessage("helpHelp", this, playerId));
                        SendReply(player, lang.GetMessage("helpAdd", this, playerId));
                        SendReply(player, lang.GetMessage("helpList", this, playerId));
                        SendReply(player, lang.GetMessage("helpRemove", this, playerId));
                        SendReply(player, lang.GetMessage("helpInfo", this, playerId));
                        SendReply(player, lang.GetMessage("helpEdit", this, playerId));
                    }
                    return;

                case "add":
                    {
                        PlayerData Player = GetCache(player);
                        if (args.Length != 2)
                        {
                            SendReply(player, lang.GetMessage("helpAdd", this, playerId));
                            return;
                        }
                        string name = args[1];
                        foreach (var zoneDef in ZoneDefinitions)
                        {
                            if (zoneDef.Value.ZoneName == name)
                            {
                                SendReply(player, lang.GetMessage("nameExists", this, playerId), name);
                                return;
                            }
                            if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                            {
                                SendReply(player, lang.GetMessage("inZoneError", this, playerId));
                                return;
                            }
                        }
                        var newzoneinfo = new ZoneInfo(player.Entity.Position) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };
                        if (ZoneDefinitions.ContainsKey(newzoneinfo.Id)) storedData.ZoneDefinitions.Remove(ZoneDefinitions[newzoneinfo.Id]);
                        ZoneDefinitions[newzoneinfo.Id] = newzoneinfo;
                        storedData.ZoneDefinitions.Add(newzoneinfo);
                        SaveData();
                        float zonex = player.Entity.Position.x;
                        float zoney = player.Entity.Position.y;
                        float zonez = player.Entity.Position.z;
                        ZoneDefinitions[newzoneinfo.Id].ZoneX = zonex;
                        ZoneDefinitions[newzoneinfo.Id].ZoneZ = zonez;
                        ZoneDefinitions[newzoneinfo.Id].ZoneName = name;
                        ZoneDefinitions[newzoneinfo.Id].ZoneCreatorName = player.ToString();
                        SendReply(player, lang.GetMessage("zoneAdded", this, playerId), newzoneinfo.Id, name);
                        SaveData();
                        LoadZones();
                        return;
                    }
                case "list":
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        SendReply(player, lang.GetMessage("zoneList", this, playerId), zoneDef.Value.Id, zoneDef.Value.ZoneName, zoneDef.Value.Location);
                    }

                    return;
                case "remove":

                    var id = args[1];
                    if (ZoneDefinitions.ContainsKey(id))
                    {
                        storedData.ZoneDefinitions.Remove(ZoneDefinitions[id]);
                        SendReply(player, lang.GetMessage("zoneRemove", this, playerId), id);
                        SaveData();
                        LoadData();
                        LoadZones();
                    }
                    else
                        SendReply(player, lang.GetMessage("zoneError", this, playerId));
                    return;
                case "info":
                    int count = 0;
                    int zcount = 0;
                    foreach (var zoneDef in ZoneDefinitions)
                    {
                        count++;                        
                        if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                        {
                            zcount++;                            
                            SendReply(player, lang.GetMessage("zoneInfo", this, playerId), zoneDef.Value.Id, zoneDef.Value.ZoneName);
                            SendReply(player, lang.GetMessage("zoneFlag1", this, playerId), zoneDef.Value.ZoneRadius);
                            SendReply(player, lang.GetMessage("zoneFlag2", this, playerId), zoneDef.Value.ZoneNoPVP);
                            SendReply(player, lang.GetMessage("zoneFlag3", this, playerId), zoneDef.Value.ZoneNoBuild);
                            SendReply(player, lang.GetMessage("zoneFlag4", this, playerId), zoneDef.Value.ZoneNoDamage);
                            SendReply(player, lang.GetMessage("zoneFlag5", this, playerId), zoneDef.Value.ZoneNoSleeperDamage);
                            SendReply(player, lang.GetMessage("zoneFlag6", this, playerId), zoneDef.Value.ZoneNoCrestDamage);
                            SendReply(player, lang.GetMessage("zoneFlag7", this, playerId), zoneDef.Value.ZoneMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag8", this, playerId), zoneDef.Value.ZoneEnterMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag9", this, playerId), zoneDef.Value.ZoneExitMessageOn);
                            SendReply(player, lang.GetMessage("zoneFlag10", this, playerId), zoneDef.Value.ZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag11", this, playerId), zoneDef.Value.EnterZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag12", this, playerId), zoneDef.Value.ExitZoneMessage);
                            SendReply(player, lang.GetMessage("zoneFlag13", this, playerId), zoneDef.Value.ZoneNoPlayerRoping);
                            SendReply(player, lang.GetMessage("zoneFlag14", this, playerId), zoneDef.Value.ZoneNoPVE);
                            SendReply(player, lang.GetMessage("zoneFlag15", this, playerId), zoneDef.Value.ZoneNoPreFabDamage);
                            SendReply(player, lang.GetMessage("zoneFlag16", this, playerId), zoneDef.Value.ZoneEjectPlayer);
                            SendReply(player, lang.GetMessage("zoneFlag17", this, playerId), zoneDef.Value.ZoneEjectSleeper);
                            if (AntiLootLoaded)
                            {
                                SendReply(player, lang.GetMessage("zoneFlag18", this, playerId), zoneDef.Value.ZoneNoChestLooting);
                                SendReply(player, lang.GetMessage("zoneFlag19", this, playerId), zoneDef.Value.ZoneNoStationLooting);
                                SendReply(player, lang.GetMessage("zoneFlag20", this, playerId), zoneDef.Value.ZoneNoCampfireLooting);
                                SendReply(player, lang.GetMessage("zoneFlag21", this, playerId), zoneDef.Value.ZoneNoTorchLooting);
                                SendReply(player, lang.GetMessage("zoneFlag22", this, playerId), zoneDef.Value.ZoneNoFireplaceLooting);
                                SendReply(player, lang.GetMessage("zoneFlag23", this, playerId), zoneDef.Value.ZoneNoFurnitureLooting);
                                SendReply(player, lang.GetMessage("zoneFlag24", this, playerId), zoneDef.Value.ZoneNoVillagerLooting);
                            }          
                            return;
                        }                        
                    }
                    if (zcount == 0 && count >= 1)
                        SendReply(player, lang.GetMessage("zoneLocError", this, playerId));
                    if (count == 0)
                        SendReply(player, lang.GetMessage("noZoneError", this, playerId));

                    return;
                case "edit":
                    
                    
                    var currentzone = GetZoneId(player);
                     EditZone(currentzone.ToString(), args, player);
                       
                    

                    return;
                default:
                    break;
            }
            SendReply(player, lang.GetMessage("synError", this, playerId));
        }

        #endregion
        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            //if (!damageEvent.Damage.DamageSource.IsPlayer) return;
            if (damageEvent.Damage.DamageSource.Owner == null) return;
            if (damageEvent.Entity == null) return;
            var npc = IsNPC(damageEvent);
            //sleeper   
            var sleeper = damageEvent.Entity.GetComponentInChildren<PlayerSleeperObject>();
            Player attacker = damageEvent.Damage.DamageSource.Owner;            
            string victim;
            if (npc)
            {
                victim = damageEvent.Entity.name.ToString();
                string input = victim;
                string regex = "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))";
                string output = Regex.Replace(input, regex, "");
                victim = output;
            }
            else
                victim = damageEvent.Entity.Owner.DisplayName;            

            if (damageEvent.Damage.Amount < 0) return;
            if (attacker.HasPermission("admin") && AdminCanKill) return;           
            
            if (attacker != null || damageEvent.Entity.name.Contains("Crest") || sleeper == true || npc == true)
            {                
                if (victim == damageEvent.Damage.DamageSource.Owner.DisplayName) return; //allows dehydration and hunger, as well as healing.
                
                foreach (var zoneDef in ZoneDefinitions)
                {                    
                    if (IsEntityInZone(damageEvent.Entity.Position, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) || IsInZone(attacker, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius))
                    {                        
                        if (zoneDef.Value.ZoneNoPVP == true && sleeper == false && !damageEvent.Entity.name.Contains("Crest") && npc == false && damageEvent.Entity.IsPlayer && damageEvent.Damage.DamageSource.IsPlayer)
                        {
                            damageEvent.Cancel(lang.GetMessage("logPvP", this, attacker.ToString()), attacker);
                            Puts(lang.GetMessage("logPvP", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noPvP", this, attacker.ToString()));
                        }
                        if (zoneDef.Value.ZoneNoSleeperDamage == true && sleeper == true)
                        {
                            damageEvent.Cancel(lang.GetMessage("logSleeper", this, attacker.ToString()), attacker);
                            Puts(lang.GetMessage("logSleeper", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noSleeper", this, attacker.ToString()));

                        }
                        if (zoneDef.Value.ZoneNoPVE == true && npc == true || !damageEvent.Damage.DamageSource.IsPlayer && zoneDef.Value.ZoneNoPVE == true)
                        {
                            if (npc)
                            {
                                damageEvent.Cancel(lang.GetMessage("logNoPVE", this, attacker.ToString()), attacker, victim);
                                Puts(lang.GetMessage("logNoPVE", this, attacker.ToString()), attacker, victim);
                                damageEvent.Damage.Amount = 0f;
                                SendReply(attacker, lang.GetMessage("noPVE", this, attacker.ToString()), victim);
                            }
                            if (damageEvent.Entity.IsPlayer)
                            {
                                damageEvent.Cancel("NoPVE zone, damage cancelled");
                                //Puts(lang.GetMessage("logNoPVE", this, attacker.ToString()), attacker, victim);
                                damageEvent.Damage.Amount = 0f;
                            }

                        }
                        if (damageEvent.Entity.name.Contains("Crest") && zoneDef.Value.ZoneNoCrestDamage == true)
                        {
                            damageEvent.Cancel(lang.GetMessage("logCrest", this, attacker.ToString()), attacker);
                            Puts(lang.GetMessage("logCrest", this, attacker.ToString()), attacker);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noCrest", this, attacker.ToString()));
                        }
                        if (zoneDef.Value.ZoneNoPreFabDamage == true && !damageEvent.Entity.IsPlayer && damageEvent.Entity.GetBlueprint() && !damageEvent.Entity.name.Contains("Crest") && sleeper == false && npc == false)
                        {                            
                            victim = damageEvent.Entity.name;
                            string input = victim;
                            string regex = "(\\[.*\\])|(\".*\")|('.*')|(\\(.*\\))";
                            string output = Regex.Replace(input, regex, "");
                            victim = output;
                            damageEvent.Cancel(lang.GetMessage("logNoPreFab", this, attacker.ToString()), attacker, victim);
                            Puts(lang.GetMessage("logNoPreFab", this, attacker.ToString()), attacker, victim);
                            damageEvent.Damage.Amount = 0f;
                            SendReply(attacker, lang.GetMessage("noPreFab", this, attacker.ToString()), victim);

                        }
                    }
                }
            }
        }
        private void OnPlayerCapture(PlayerCaptureEvent Event)
        {            
            if (Event == null) return;
            if (Event.Captor == null) return;
            if (Event.TargetEntity == null) return;
            if (Event.Captor == Event.TargetEntity) return;
            if (!Event.Captor.IsPlayer) return;
            
            Player player = Event.Captor.Owner;
            string playerId = player.Id.ToString();
                        
            if (player.HasPermission("admin") && AdminCanKill) return;
            foreach (var zoneDef in ZoneDefinitions)
            {                
                if (IsInZone(Event.Entity.Owner, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {                    
                    if (zoneDef.Value.ZoneNoPlayerRoping == true)
                    {                        
                        if (CrestCheckOn)
                        {
                            if (!OwnsCrestArea(player))//allows crest owners to rope
                            {
                                Event.Cancel(lang.GetMessage("logNoRope", this, playerId), player);
                                Puts(lang.GetMessage("logNoRope", this, playerId), player);
                                SendReply(player, lang.GetMessage("noRope", this, playerId));
                            }
                            else
                                return;
                        }
                        else
                        {                            
                            Event.Cancel(lang.GetMessage("logNoRope", this, playerId), player);
                            Puts(lang.GetMessage("logNoRope", this, playerId), player);
                            SendReply(player, lang.GetMessage("noRope", this, playerId));
                        }
                    }
                }
            }
        }
        private void OnCubePlacement(CubePlaceEvent Event)
        {
            if (Event == null) return;
            if (Event.Entity == null) return;
            Player player = Event.Entity.Owner;
            string playerId = player.Id.ToString();
            if (player.HasPermission("admin") && AdminCanBuild) return;
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Entity.Owner, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {

                    if (zoneDef.Value.ZoneNoBuild == true)
                    {
                        if (Event.Material != CubeInfo.Air.MaterialID)
                        {
                            if (CrestCheckOn)
                            {
                                if (!OwnsCrestArea(player))//allows crest owners to rope//allows crest owners to remove/add blocks
                                {
                                    InventoryUtil.CollectTileset(Event.Sender, Event.Material, 1, Event.PrefabId);
                                    Event.Cancel(lang.GetMessage("logNoBuild", this, playerId), player);
                                    Puts(lang.GetMessage("logNoBuild", this, playerId), player);
                                    SendReply(player, lang.GetMessage("noBuild", this, playerId));
                                }
                                else
                                    return;
                            }
                            else
                            {
                                InventoryUtil.CollectTileset(Event.Sender, Event.Material, 1, Event.PrefabId);
                                Event.Cancel(lang.GetMessage("logNoBuild", this, playerId), player);
                                Puts(lang.GetMessage("logNoBuild", this, playerId), player);
                                SendReply(player, lang.GetMessage("noBuild", this, playerId));
                            }

                        }
                    }
                }
            }
        }
        private void OnCubeTakeDamage(CubeDamageEvent Event)
        {
            if (Event == null) return;            
            if (Event.Damage == null) return;
            if (Event.Damage.Amount <= 0f) return;
            if (Event.Damage.DamageSource == null) return;
            if (!Event.Damage.DamageSource.IsPlayer) return;
            Player player = Event.Damage.DamageSource.Owner;
            if (player == null) return;
            string playerId = player.Id.ToString();
            TilesetColliderCube centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(Event.Position);
            Vector3 pos;
            Vector3 Vect = new Vector3(0f, 0f, 0f);
            if (Event.Damage.point != Vect)
            pos = Event.Damage.point; //Use for treb and ballista
            else
            pos = Event.Damage.DamageSource.Position;
            
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsEntityInZone(pos, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {
                    if (zoneDef.Value.ZoneNoDamage == true)
                    {
                        Event.Cancel(lang.GetMessage("logNoDamage", this, playerId), player);
                        SalvageModifier component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                        if (component != null && !component.info.NotSalvageable)
                        {
                            component.info.SalvageAmount = 0;
                        }

                        Event.Damage.Amount = 0f;
                        Event.Damage.ImpactDamage = 0f;
                        Event.Damage.MiscDamage = 0f;
                        Puts(lang.GetMessage("logNoDamage", this, playerId), player);
                        SendReply(player, lang.GetMessage("areaProtected", this, playerId));
                        return;
                    }
                }
            }
        }
        private void OnObjectDeploy(NetworkInstantiateEvent Event)
        {
            Player player = Server.GetPlayerById(Event.SenderId);
            string playerId = player.Id.ToString();
            if (player == null) return;
            if (player.HasPermission("admin") && AdminCanBuild) return;
            foreach (var zoneDef in ZoneDefinitions)
            {
                if (IsInZone(Event.Sender, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {
                    if (zoneDef.Value.ZoneNoBuild == true)
                    {
                        InvItemBlueprint bp = InvDefinitions.Instance.Blueprints.GetBlueprintForID(Event.BlueprintId);
                        if (bp.Name.Contains("Crest"))
                        {
                            timer.In(1, () => ObjectRemove(player, Event.Position, bp.name));
                            Puts(lang.GetMessage("logCrestPlace", this, playerId), player, bp.Name);
                            SendReply(Event.Sender, lang.GetMessage("noPlace", this, playerId), bp.Name);
                        }

                    }

                }
            }
        }
        private void OnPlayerDisconnected(Player player)
        {
            string playerId = player.Id.ToString();
                    
            if (PData.ContainsKey(player))
            {
                PlayerData Player = GetCache(player);
                PData.Remove(player);
            }
            if (timers.ContainsKey(playerId))
            {
                timers[playerId].Destroy();
                timers.Remove(playerId);
            }
            if (ZoneCheckTimer.ContainsKey(playerId))
            {
                ZoneCheckTimer[playerId].Destroy();
                ZoneCheckTimer.Remove(playerId);
            }
        }        
        private void OnPlayerConnected(Player player)
        {            
		    string playerId = player.Id.ToString();
            if (!PData.ContainsKey(player)) 
            {
                PData.Add(player, new PlayerData (player.Id));                
            }
            if (ZoneCheckOn == true && player.Name != "Server" && player.Id != 9999999999) //fixes error
            {
                if (!ZoneCheckTimer.ContainsKey(playerId))
                {
                    ZoneCheckTimer.Add(playerId, timer.Repeat(ZoneCheckInterval, 0, () => CheckPlayerLocation(player)));
                }
            }

        }
        
        private void OnPlayerSleep(PlayerSleepEvent Event)
        {
            Entity entity = Event.Player.Entity;                      
            foreach (var zoneDef in ZoneDefinitions)
            {                
                if (IsInZone(entity.Owner, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                {                    
                    if (zoneDef.Value.ZoneEjectSleeper == true)
                    {                        
                        GameObject sleepGameObject = Event.SleepGameObject;                       
                        PlayerSleeperObject sleeperId = sleepGameObject.TryGetEntity().Get<PlayerSleeperObject>();
                        EjectSleeper(zoneDef.Value.ZoneRadius, zoneDef.Value.Location, sleeperId);
                        Puts(lang.GetMessage("logEjectSleeper", this, Event.SleeperId.ToString()), Event.DisplayName);
                    }
                }
            }
        }
        
        #region Functions
        private void ObjectRemove(Player player, Vector3 position, string itemname)
        {            
            foreach (var entity in Entity.TryGetAll())
            {
                if (entity.Position == position)
                {                    
                    EntityKiller.Kill(entity);
                    GiveInventoryStack(player, itemname);
                }
            }
        }
        
        void GiveInventoryStack(Player player, string itemname)
        {
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(itemname, true, true);           
            var invGameItemStack = new InvGameItemStack(blueprintForName, 1, null);
            ItemCollection.AutoMergeAdd(inventory.Contents, invGameItemStack);
        }
        void LoadZones()
        {           
            timer.In(1, () => {
                foreach (var zoneDef in ZoneDefinitions)
                    zones.Add(new Vector2(zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ));
            });

        }
        private void CheckPlayerLocation(Player player)
        {
            if (player.Name == "Server" && player.Id == 9999999999) return; //fixes error
            if (player == null) return;
            if (player.Entity == null) return; //fixed NRE  
            string playerId = player.Id.ToString();
            if (PData.ContainsKey(player))
            {
                foreach (var zoneDef in ZoneDefinitions)
                {
                    PlayerData Player = GetCache(player);
                    if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true && zoneDef.Value.ZoneEjectPlayer == true && !player.HasPermission("admin"))
                    {
                        EjectPlayer(zoneDef.Value.ZoneRadius, zoneDef.Value.Location, player);
                        SendReply(player, lang.GetMessage("noEntry", this, playerId));
                        Puts(lang.GetMessage("logEjectPlayer", this, playerId), player);
                    }
                    if (IsInZone(player, zoneDef.Value.ZoneX, zoneDef.Value.ZoneZ, zoneDef.Value.ZoneRadius) == true)
                    {
                        Player.ZoneId = zoneDef.Value.Id;

                        if (Player.EnterZone == false)
                        {
                            if (zoneDef.Value.ZoneEnterMessageOn == true && Player.EnterZone == false)
                            {
                                SendMessage(player, zoneDef.Value.EnterZoneMessage, false, true);
                            }
                            if (zoneDef.Value.ZoneMessageOn == true)
                            {
                                SendMessage(player, zoneDef.Value.ZoneMessage, true, true);
                            }
                            Player.EnterZone = true;
                            Player.ExitZone = false;
                            return;
                        }

                    }
                    else
                    {
                        if (Player.EnterZone == true && Player.ZoneId == zoneDef.Value.Id && Player.ExitZone == false)
                        