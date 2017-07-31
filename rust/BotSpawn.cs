// Requires: Kits
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
using static UnityEngine.Vector3;

namespace Oxide.Plugins

{
    [Info("BotSpawn", "Steenamaroo", "1.0.1", ResourceId = 2580)]

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
        
        class StoredData
        {
            public Dictionary<BasePlayer, string> bots = new Dictionary<BasePlayer, string>();
            public StoredData()
            {
            }
        }
        StoredData storedData; 
 
        void Init()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BotSpawn");
            LoadConfigVariables();
            foreach(var bot in storedData.bots) 
            bot.Key.Kill();
            storedData.bots.Clear();
            Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData); 
        }
        void OnServerInitialized()
        {
            FindMonuments();           
        }
        
        void Unload()
        {
            foreach(var bot in storedData.bots)
            { 
            bot.Key.Kill();
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
            if (storedData.bots.ContainsKey(player)) 
            {
                storedData.bots.Remove(player);
                Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData); 
            }
            else
            {
                if (zone !=null)
                storedData.bots.Add(player, zone.Name);
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
            if (bot.Key == entity)
                {
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
            SpawnSci(zone, pos);
            Update(Scientist, zone);

            
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
        void ListSci() 
        {
            foreach (var bot in storedData.bots)
            {
            Puts($"On List {bot}");
            }
            foreach(var bot in GameObject.FindObjectsOfType<NPCPlayer>())
            {
            Puts($"In Game {bot}"); 
            }
        }
        
        void SpawnSci(MonumentSettings zone, Vector3 pos)
        {
            BasePlayer Scientist = null;

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
                var entity = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", CentrePos, rot, true);            
                entity.Spawn();
                Scientist = entity as NPCPlayer; 
                Scientist.inventory.Strip();
                Kits?.Call($"GiveKit", Scientist, zone.Kit);
                Scientist.health = zone.BotHealth;
                Update(Scientist, zone);
                Vector3 newPos = CalculateGroundPos(CentrePos);
                //Vector3 test = CentrePos + new Vector3(10,0,10); 
                //Vector3 nextPos = Lerp(CentrePos, test, 10f); 
                //entity.transform.position = nextPos;
                entity.transform.position = newPos;
                timer.Once(0.1f, () => SpawnSci(zone, pos)); //delay to allow for random number to change

                //Scientist.isInAir = true;
                //Scientist.modelState.onground = false;
                return;
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
                            if (CheckKit(configData.Zones.Powerplant.Kit) == null)return;
                            SpawnSci(configData.Zones.Powerplant, pos);
                            PowerPlant = pos;
                        }
                            
                        continue;
                    }

                    if (gobject.name.Contains("airfield_1"))
                    {
                        if (configData.Zones.Airfield.Activate)
                        {
                            if (CheckKit(configData.Zones.Airfield.Kit) == null)return;
                            SpawnSci(configData.Zones.Airfield, pos);
                            Airfield = pos;
                        }
                        continue; 
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        if (configData.Zones.Trainyard.Activate)
                        {
                            if (CheckKit(configData.Zones.Trainyard.Kit) == null)return;
                            SpawnSci(configData.Zones.Trainyard, pos);
                            TrainYard = pos;
                        }
                        continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1"))
                    {
                        if (configData.Zones.WaterTreatment.Activate)
                        {
                            if (CheckKit(configData.Zones.WaterTreatment.Kit) == null)return;
                            SpawnSci(configData.Zones.WaterTreatment, pos);
                            WaterTreatment = pos;
                        }
                        continue;
                    }

                    if (gobject.name.Contains("satellite_dish"))
                    {

                        if (configData.Zones.Satellite.Activate)
                        {
                            if (CheckKit(configData.Zones.Satellite.Kit) == null)return;   
                            SpawnSci(configData.Zones.Satellite, pos);
                            Satellite = pos;
                        }
                        continue;
                    }

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        if (configData.Zones.Dome.Activate)
                        {
                            if (CheckKit(configData.Zones.Dome.Kit) == null)return;
                            SpawnSci(configData.Zones.Dome, pos);
                            Dome = pos;
                        }
                        continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        if (configData.Zones.Radtown.Activate)
                        {
                            if (CheckKit(configData.Zones.Radtown.Kit) == null)return;
                            SpawnSci(configData.Zones.Radtown, pos);
                            Radtown = pos;
                        }
                        continue;
                    }
                    
                    if (gobject.name.Contains("launch_site"))
                    {
                        if (configData.Zones.LaunchSite.Activate)
                        {
                            if (CheckKit(configData.Zones.LaunchSite.Kit) == null)return;
                            SpawnSci(configData.Zones.LaunchSite, pos);
                            LaunchSite = pos;
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
        }
        class Options
        {
            public bool Bots_Drop_Weapons { get; set; }
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
               },
               Zones = new Zones
               {
                   Airfield = new MonumentSettings
                   {
                       Activate = true,
                       Name = "Airfield",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
                   },
                   Dome = new MonumentSettings
                   {
                       Activate = true,
                       Name = "Dome",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
                   },
                   Powerplant = new MonumentSettings
                   {
                       Activate = true, 
                       Name = "Powerplant",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
                   },
                   Radtown = new MonumentSettings
                   {
                       Activate = true,
                       Name = "Radtown",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
                   },
                   Satellite = new MonumentSettings
                   {
                       Activate = true,
                       Name = "Satellite",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
                   },
                   Trainyard = new MonumentSettings
                   {
                       Activate = true,
                       Name = "Trainyard",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
                   },

                   WaterTreatment = new MonumentSettings
                   {
                       Activate = true,
                       Name = "WaterTreatment",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default" 
                   },
                   
                   LaunchSite = new MonumentSettings
                   {
                       Activate = true,
                       Name = "LaunchSite",
                       Bots = 10,
                       BotHealth = 100,
                       Radius = 200,
                       Kit = "default"
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


