﻿using System;
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
    [Info("BotSpawn", "Steenamaroo", "1.0.6", ResourceId = 2580)]

    [Description("Spawn Bots with kits.")]
    
    class BotSpawn : RustPlugin

    {
        [PluginReference]
        Plugin Kits;
        
        private ConfigData configData; 
        
        Vector3 Airfield = new Vector3(0,0,0);
        Vector3 Dome = new Vector3(0,0,0);
        Vector3 PowerPlant = new Vector3(0,0,0);
        Vector3 Radtown = new Vector3(0,0,0);
        Vector3 Satellite = new Vector3(0,0,0);
        Vector3 TrainYard = new Vector3(0,0,0);
        Vector3 WaterTreatment = new Vector3(0,0,0);
        Vector3 LaunchSite = new Vector3(0,0,0); 
        
        int no_of_AI = 0;
        
        class StoredData
        {
            public Dictionary<ulong, string> bots = new Dictionary<ulong, string>();
            public StoredData()
            {
            }
        }
        StoredData storedData; 
 
        void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BotSpawn");
            storedData?.bots.Clear();
            Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData); 
            LoadConfigVariables();
        }
        void OnServerInitialized()
        {
            FindMonuments();           
        }
        
        void Unload()
        {
            foreach(var bot in GameObject.FindObjectsOfType<NPCPlayer>())
            {
            if (storedData.bots.ContainsKey(bot.userID))
                {
                bot.Kill();
                }
            }

            storedData.bots.Clear(); 
            Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData); 
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
        
        void Update(BasePlayer player, MonumentSettings zone)
        {
            if (storedData.bots.ContainsKey(player.userID)) 
            {
                storedData.bots.Remove(player.userID);
                Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData); 
            }
            else
            {
                if (zone !=null)
                storedData.bots.Add(player.userID, zone.Name);
                Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData);
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
        
        void OnEntityDeath(BaseEntity entity)
        {
            BasePlayer Scientist = null;
            if (entity is NPCPlayer)
            {
            Scientist = entity as NPCPlayer;
            MonumentSettings zone = configData.Zones.Dome;
            Vector3 pos = new Vector3(0,0,0);     
            foreach (var bot in storedData.bots)
            {
            if (bot.Key == Scientist.userID)
                {
                    no_of_AI--;
                    if (bot.Value == "Airfield")
                    {
                        pos = Airfield;
                        zone = configData.Zones.Airfield;
                    }
                    if (bot.Value == "Dome")
                    {
                        pos = Dome;
                        zone = configData.Zones.Dome;
                    }
                    if (bot.Value == "PowerPlant")
                    {
                        pos = PowerPlant;
                        zone = configData.Zones.Powerplant;
                    }
                    if (bot.Value == "Radtown")
                    {
                        pos = Radtown;
                        zone = configData.Zones.Radtown;
                    }
                    if (bot.Value == "Satellite")
                    {
                        pos = Satellite;
                        zone = configData.Zones.Satellite;
                    }
                    if (bot.Value == "TrainYard")
                    {
                        pos = TrainYard;
                        zone = configData.Zones.Trainyard;
                    }
                    if (bot.Value == "WaterTreatment")
                    {
                        pos = WaterTreatment;
                        zone = configData.Zones.WaterTreatment;
                    }
                    if (bot.Value == "LaunchSite")
                    {
                        pos = LaunchSite;
                        zone = configData.Zones.LaunchSite;
                    }
                }
            }
            Update(Scientist, zone); 
            timer.Once(configData.Options.Respawn_Timer, () => SpawnSci(zone, pos, true));

            
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
        
        void SpawnSci(MonumentSettings zone, Vector3 pos, bool single)
        {
            BasePlayer Scientist = null;
                if (no_of_AI > configData.Options.Upper_Bot_Limit)return;
                else
                {
                    var BotCount = 0;
                    foreach (var pair in storedData.bots)
                    {
                        if (pair.Value == zone.Name)
                        BotCount++;
                    }
                    if (BotCount == zone.Bots)
                    {  
                        return;   
                    }
                    System.Random rnd = new System.Random(); 
                    int X = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                    int Z = rnd.Next((-zone.Radius/2), (zone.Radius/2)); 
                    var CentrePos = new Vector3((pos.x + X),30,(pos.z + Z));    
                    var rot = new Quaternion (0,0,0,0);
                    Vector3 newPos = CalculateGroundPos(CentrePos);
                    var entity = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", newPos, rot, true);            
                    entity.Spawn();
                    no_of_AI++;
                    Scientist = entity as NPCPlayer; 
                    if (zone.Kit != "default")
                    {
                        Scientist.inventory.Strip(); 
                        Kits?.Call($"GiveKit", Scientist, zone.Kit);
                    }
                    Scientist.health = zone.BotHealth;
                    Scientist.displayName = zone.BotName; 
                    Update(Scientist, zone);
                    if (single) return;
                    timer.Once(0.1f, () => SpawnSci(zone, pos, false)); //delay to allow for random number to change
                    return;
                }

        } 
        
        static Vector3 CalculateGroundPos(Vector3 sourcePos) // credit Wulf & Nogrod
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        } 
  
       private void FindMonuments() // credit K1lly0u 
        {
            
            var allobjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument")) 
                {
                    var pos = gobject.transform.position;
 
                    if (gobject.name.Contains("powerplant_1"))
                    {
                        if (configData.Zones.Powerplant.Activate)
                        {
                            if (configData.Zones.Powerplant.Kit != "default" && (CheckKit(configData.Zones.Powerplant.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.Powerplant, pos, false);
                                PowerPlant = pos;
                                }
                        } 
                        continue;
                    }
 
                    if (gobject.name.Contains("airfield_1"))
                    {
                        if (configData.Zones.Airfield.Activate)
                        {
                            if (configData.Zones.Airfield.Kit != "default" && (CheckKit(configData.Zones.Airfield.Kit) == null))
                            continue;
                            else
                            {
                            SpawnSci(configData.Zones.Airfield, pos, false);
                            Airfield = pos; 
                            }
                        }
                        continue; 
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        if (configData.Zones.Trainyard.Activate)
                        {
                            if (configData.Zones.Trainyard.Kit != "default" && (CheckKit(configData.Zones.Trainyard.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.Trainyard, pos, false);
                                TrainYard = pos;
                                }
                        }
                        continue; 
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        if (configData.Zones.WaterTreatment.Activate)
                        {
                            if (configData.Zones.WaterTreatment.Kit != "default" && (CheckKit(configData.Zones.WaterTreatment.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.WaterTreatment, pos, false);
                                WaterTreatment = pos;
                                }
                        }
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish")) 
                    {

                        if (configData.Zones.Satellite.Activate)
                        {
                            if (configData.Zones.Satellite.Kit != "default" && (CheckKit(configData.Zones.Satellite.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.Satellite, pos, false);
                                Satellite = pos;
                                }
                        }
                        continue;
                    } 

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        if (configData.Zones.Dome.Activate)
                        {
                            if (configData.Zones.Dome.Kit != "default" && (CheckKit(configData.Zones.Dome.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.Dome, pos, false);
                                Dome = pos;
                                }
                        }
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        if (configData.Zones.Radtown.Activate)
                        {
                            if (configData.Zones.Radtown.Kit != "default" && (CheckKit(configData.Zones.Radtown.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.Radtown, pos, false);
                                Radtown = pos;
                                }
                        }
                        continue;
                    }
                    
                    if (gobject.name.Contains("launch_site"))
                    {
                        if (configData.Zones.LaunchSite.Activate)
                        {
                            if (configData.Zones.LaunchSite.Kit != "default" && (CheckKit(configData.Zones.LaunchSite.Kit) == null))
                                continue;
                                else
                                {
                                SpawnSci(configData.Zones.LaunchSite, pos, false);
                                LaunchSite = pos;
                                }
                        } 
                        continue;  
                    }
                }                
            }
        }

        #region Config
         class MonumentSettings
        {
            public bool Activate;
            public string Name;
            public int Bots;
            public int BotHealth;
            public int Radius;
            public string Kit;
            public string BotName;
            
        }
        class Options
        {
            public bool Bots_Drop_Weapons { get; set; }
            public int Upper_Bot_Limit { get; set; }
            public int Respawn_Timer { get; set; }
        }
        class Zones
        {
            public MonumentSettings Airfield { get; set; }
            public MonumentSettings Dome { get; set; }
            public MonumentSettings Powerplant { get; set; }
            public MonumentSettings Radtown { get; set; }
            public MonumentSettings Satellite { get; set; }
            public MonumentSettings Trainyard { get; set; }
            public MonumentSettings WaterTreatment { get; set; }
            public MonumentSettings LaunchSite { get; set; }
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
                    Upper_Bot_Limit = 80,
                    Respawn_Timer = 60,
               },
               Zones = new Zones
               {
                   Airfield = new MonumentSettings
                   { 
                       Activate = false,
                       Name = "Airfield",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Airfield Bot"
                   },
                   Dome = new MonumentSettings
                   {
                       Activate = false,
                       Name = "Dome",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Dome Bot"
                   },
                   Powerplant = new MonumentSettings
                   {
                       Activate = false, 
                       Name = "Powerplant",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Powerplant Bot"
                   },
                   Radtown = new MonumentSettings
                   {
                       Activate = false,
                       Name = "Radtown",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Radtown Bot"
                   },
                   Satellite = new MonumentSettings
                   {
                       Activate = false,
                       Name = "Satellite",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Satellite Bot"
                   },
                   Trainyard = new MonumentSettings
                   {
                       Activate = false,
                       Name = "Trainyard",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "Trainyard Bot"
                   },

                   WaterTreatment = new MonumentSettings
                   {
                       Activate = false,
                       Name = "WaterTreatment",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "WaterTreatment Bot"
                   },
                   
                   LaunchSite = new MonumentSettings
                   {
                       Activate = false,
                       Name = "LaunchSite",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default",
                       BotName = "LaunchSite Bot"
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

