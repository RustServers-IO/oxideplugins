using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins

{
    [Info("BotSpawn", "Steenamaroo", "1.0.9", ResourceId = 2580)] //storedData removed

    [Description("Spawn Bots with kits at monuments.")]
    
    class BotSpawn : RustPlugin

    {
        [PluginReference]
        Plugin Kits;
        
        private ConfigData configData; 
        
        int no_of_AI = 0;
        System.Random rnd = new System.Random();
        
        class TempRecord
        {
            public static Dictionary<NPCPlayerApex, botData> NPCPlayers = new Dictionary<NPCPlayerApex, botData>();
            public static Dictionary<MonumentSettings, MonumentNameLocation> MonumentProfiles = new Dictionary<MonumentSettings, MonumentNameLocation>();
        }
        class botData
        {
            public Vector3 spawnPoint;
            public int accuracy;
            public ulong botID;
            public BasePlayer bot;
            public string monumentName;
        }
        class MonumentNameLocation
        {
            public string Name;
            public Vector3 Location;
        }
        
        void Init()
        {
            no_of_AI = 0;
            Wipe();
            LoadConfigVariables();
        }
        
        void OnServerInitialized()
        {
            FindMonuments();
        }

        void Loaded()
        {
        if (configData.Options.Reset)
        timer.Repeat(900f, 0, () => cmdBotRespawn());
        }
        
        void Unload()
        {
            Wipe();
        }
        
        void Wipe()
        {
            foreach (var bot in TempRecord.NPCPlayers)
            {
            bot.Key.Kill();
            }
            TempRecord.NPCPlayers.Clear();            
        }
        
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info?.Initiator is NPCPlayer && entity is BasePlayer)
            {
                var attacker = info?.Initiator as NPCPlayer;
                
                AttackEntity heldEntity = attacker.GetHeldEntity() as AttackEntity;
                if (heldEntity != null)
                heldEntity.effectiveRange =  configData.Options.Bot_Firing_Range;
                    
                foreach (var bot in TempRecord.NPCPlayers)
                {
                    if (bot.Value.botID == attacker.userID)
                    {
                    System.Random rnd = new System.Random(); 
                    int rand = rnd.Next(1, 10);
                        if (bot.Value.accuracy < rand)
                        {
                        return true;
                        }
                        else
                        {
                        info.damageTypes.ScaleAll(configData.Options.Bot_Damage);
                        return null;
                        }
                    }
                }
            }
            return null;
        }
        
        void OnEntityDeath(BaseEntity entity)
        {
            string respawnLocationName = "";
            BasePlayer Scientist = null;
            if (entity is NPCPlayer)
            {
            Scientist = entity as NPCPlayer;
            foreach (var bot in TempRecord.NPCPlayers)
            {
            if (bot.Value.botID == Scientist.userID)
                {
                    no_of_AI--;
                    respawnLocationName = bot.Value.monumentName;
                }
            }
            
            foreach (var profile in TempRecord.MonumentProfiles)
            if(profile.Value.Name == respawnLocationName)
            {
                timer.Once(configData.Options.Respawn_Timer, () => SpawnSci(profile.Key, profile.Value)); 
            }
            UpdateRecords(Scientist);
 
            foreach (Item item in Scientist.inventory.containerBelt.itemList) 
                {
                item.Remove();
                }  
            }
            else
            {
                return;
            }
        }
        
		void OnPlayerDie(BasePlayer player, HitInfo info) 
		{
            if (!configData.Options.Bots_Drop_Weapons)
            {
                if (player == null || player.svActiveItemID == 0u)
                    return;
                player.svActiveItemID = 0u;
                player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }
		}
        
        void SpawnSci(MonumentSettings settings, MonumentNameLocation profile)
        {
            var pos = profile.Location;
            var zone = settings;
            BasePlayer Scientist = null;
            if (no_of_AI == configData.Options.Upper_Bot_Limit)
            return;
            else
            {
                int X = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                int Z = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                var CentrePos = new Vector3((pos.x + X),100,(pos.z + Z));    
                var rot = new Quaternion (0,0,0,0);
                Vector3 newPos = CalculateGroundPos(CentrePos);
                var entity = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", newPos, rot, true) as NPCPlayer;
                entity.Spawn();
                no_of_AI++;
                if (zone.Kit != "default")
                {
                    entity.inventory.Strip(); 
                    Kits?.Call($"GiveKit", entity, zone.Kit);
                }
                entity.health = zone.BotHealth;
                entity.displayName = zone.BotName;
                var botapex = entity.GetComponent<NPCPlayerApex>();
                TempRecord.NPCPlayers.Add(botapex, new botData()
                {
                    spawnPoint = newPos,
                    accuracy = configData.Options.Bot_Accuracy,
                    botID = entity.userID,
                    bot = entity,
                    monumentName = profile.Name,
                });   
            }
        }
        
        public object CheckKit(string name) //credit K1lly0u
        {
            object success = Kits?.Call("isKit", name);
            if ((success is bool))
                if (!(bool)success)
                {
                    PrintWarning("BotSpawn : The Specified Kit Does Not Exist. Please update your config and reload.");
                    return null;
                }
            return true;
        }
        
        void UpdateRecords(BasePlayer player)
        {
            foreach (var bot in TempRecord.NPCPlayers)
            {
                if (bot.Value.botID == player.userID)
                {
                TempRecord.NPCPlayers.Remove(bot.Key);
                return;
                }
            }
        }
        
        void OnTick()
        {
            if (configData.Options.Ignore_Animals)
            {
                NPCPlayer activeBot;
                NPCPlayerApex botapex;
                foreach(var bot in TempRecord.NPCPlayers)
                {
                    if (bot.Key.AttackTarget != null)
                    {
                        if (bot.Key.AttackTarget.name.Contains("agents/")) /////////////////////////not exactly economical - I know.
                        {
                            bot.Key.AttackTarget = null; 
                        }
                    }
                }
            }
        }

        static Vector3 CalculateGroundPos(Vector3 sourcePos) // credit Wulf & Nogrod 
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, 300f, LayerMask.GetMask("Terrain", "World", "Construction"), QueryTriggerInteraction.Ignore))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        } 
  
       private void FindMonuments() // credit K1lly0u 
        {
            TempRecord.MonumentProfiles.Clear();
            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            int warehouse = 0;
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument")) 
                {
                    var pos = gobject.transform.position;
                    if (gobject.name.Contains("powerplant_1"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Powerplant, new MonumentNameLocation(){
                        Name = "PowerPlant",
                        Location = pos,
                    });             
                    continue;
                    }
 
                    if (gobject.name.Contains("airfield_1"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Airfield, new MonumentNameLocation(){
                        Name = "Airfield",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Trainyard, new MonumentNameLocation(){
                        Name = "Trainyard",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Watertreatment, new MonumentNameLocation(){
                        Name = "Watertreatment",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("satellite_dish")) 
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Satellite, new MonumentNameLocation(){
                        Name = "Satellite",
                        Location = pos,
                    });             
                    continue;
                    } 

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Dome, new MonumentNameLocation(){
                        Name = "Dome",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Radtown, new MonumentNameLocation(){
                        Name = "Radtown",
                        Location = pos,
                    });             
                    continue;
                    }
                    
                    if (gobject.name.Contains("launch_site"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Launchsite, new MonumentNameLocation(){
                        Name = "Launchsite",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.MilitaryTunnel, new MonumentNameLocation(){
                        Name = "MilitaryTunnel",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Harbor1, new MonumentNameLocation(){
                        Name = "Harbor1",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Harbor2, new MonumentNameLocation(){
                        Name = "Harbor2",
                        Location = pos,
                    });             
                    continue;
                    }

                    if (gobject.name.Contains("warehouse") && warehouse == 0)
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Warehouse, new MonumentNameLocation(){
                        Name = "Warehouse",
                        Location = pos,
                    });
                    warehouse++;
                    continue;
                    }
    
                    if (gobject.name.Contains("warehouse") && warehouse == 1)
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Warehouse1, new MonumentNameLocation(){
                        Name = "Warehouse1",
                        Location = pos,
                    });
                    warehouse++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("warehouse") && warehouse == 2)
                    {
                        TempRecord.MonumentProfiles.Add(configData.Zones.Warehouse2, new MonumentNameLocation(){
                        Name = "Warehouse2",
                        Location = pos,
                    });
                    warehouse++;
                    continue;
                    } 
                }
            }
            foreach (var profile in TempRecord.MonumentProfiles)
            if(profile.Key.Activate == true)
            {
            var i = 0;
                while (i < profile.Key.Bots)
                {
                    SpawnSci(profile.Key, profile.Value);
                    i++;
                }
            }
        }
        #region Console Commands
        [ConsoleCommand("bot.respawn")]
        void cmdBotRespawn()
        {
                Unload();
                Init();
                OnServerInitialized();
        }
        
        [ConsoleCommand("bot.count")]
        void cmdBotCount()
        {
            int total = 0;
            foreach(var pair in TempRecord.NPCPlayers)
            {
                total++;
            }
            Puts($"There are {total} bots of a maximum {configData.Options.Upper_Bot_Limit}.");
        }
        #endregion
        
        #region Config
        class MonumentSettings
        {
            public bool Activate;
            public int Bots;
            public int BotHealth;
            public int Radius;
            public string Kit;
            public string BotName;
            public int BotRadius;
            
        }

        class Options
        {
            public bool Bots_Drop_Weapons { get; set; }
            public int Upper_Bot_Limit { get; set; }
            public int Respawn_Timer { get; set; }
            public int Bot_Firing_Range { get; set; }
            public int Bot_Accuracy { get; set; }
            public float Bot_Damage { get; set; }
            public bool Ignore_Animals { get; set; }
            public bool Reset { get; set; }
        }
        class Zones
        {
            public MonumentSettings Airfield { get; set; }
            public MonumentSettings Dome { get; set; }
            public MonumentSettings Powerplant { get; set; }
            public MonumentSettings Radtown { get; set; }
            public MonumentSettings Satellite { get; set; }
            public MonumentSettings Trainyard { get; set; }
            public MonumentSettings Watertreatment { get; set; }
            public MonumentSettings Launchsite { get; set; }
            public MonumentSettings MilitaryTunnel { get; set; }
            public MonumentSettings Harbor1 { get; set; }
            public MonumentSettings Harbor2 { get; set; }
            public MonumentSettings Warehouse { get; set; }
            public MonumentSettings Warehouse1 { get; set; }
            public MonumentSettings Warehouse2 { get; set; }
        }

        class ConfigData
        {
            public Options Options { get; set; }
            public Zones Zones { get; set; }
        }
        
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file"); 
            var config = new ConfigData
            {
               Options = new Options
               {
                    Bots_Drop_Weapons = true,
                    Upper_Bot_Limit = 120,
                    Respawn_Timer = 60,
                    Bot_Firing_Range = 20,
                    Bot_Accuracy = 5,
                    Bot_Damage = 0.1f,
                    Ignore_Animals = true,
                    Reset = true,
               },
               Zones = new Zones
               {
                   Airfield = new MonumentSettings
                   { 
                       Activate = false,
                       Bots = 15,
                       BotHealth = 100,
                       Radius = 300,
                       Kit = "default",
                       BotName = "Airfield Bot",
                       BotRadius = 10
                   },
                   Dome = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 5,
                       BotHealth = 100,
                       Radius = 150,
                       Kit = "default",
                       BotName = "Dome Bot",
                       BotRadius = 10
                   },
                   Powerplant = new MonumentSettings
                   {
                       Activate = false, 
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Powerplant Bot",
                       BotRadius = 10
                   },
                   Radtown = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Radtown Bot",
                       BotRadius = 10
                   },
                   Satellite = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 5,
                       BotHealth = 100,
                       Radius = 150,
                       Kit = "default",
                       BotName = "Satellite Bot",
                       BotRadius = 10
                   },
                   Trainyard = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Trainyard Bot",
                       BotRadius = 10
                   },

                   Watertreatment = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "WaterTreatment Bot",
                       BotRadius = 10
                   },
    
                   Launchsite = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 15,
                       BotHealth = 100,
                       Radius = 300,
                       Kit = "default",
                       BotName = "LaunchSite Bot",
                       BotRadius = 10
                   },
                   
                   MilitaryTunnel = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Military Tunnel Bot",
                       BotRadius = 10
                   },
                   
                   Harbor1 = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Harbor Bot",
                       BotRadius = 10
                   },
                   
                   Harbor2 = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Harbor Bot",
                       BotRadius = 10
                   },
                     
                   Warehouse = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 5,
                       BotHealth = 100,
                       Radius = 100,
                       Kit = "default",
                       BotName = "Warehouse Bot",
                       BotRadius = 10
                   },
                     
                   Warehouse1 = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 5,
                       BotHealth = 100,
                       Radius = 100,
                       Kit = "default",
                       BotName = "Warehouse Bot",
                       BotRadius = 10
                   },
                     
                   Warehouse2 = new MonumentSettings
                   {
                       Activate = false,
                       Bots = 5,
                       BotHealth = 100,
                       Radius = 100,
                       Kit = "default",
                       BotName = "Warehouse Bot",
                       BotRadius = 10
                   }
               }
        };
        SaveConfig(config);
        }
        
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion      
    }
}