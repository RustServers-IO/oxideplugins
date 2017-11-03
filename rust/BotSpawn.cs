using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Oxide.Game.Rust;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using Facepunch; 
namespace Oxide.Plugins

{
    [Info("BotSpawn", "Steenamaroo", "1.2.7", ResourceId = 2580)]

    [Description("Spawn Bots with kits at monuments.")]
    
    class BotSpawn : RustPlugin

    {
        [PluginReference]
        Plugin Vanish, Kits;
     
        const string permAllowed = "botspawn.allowed";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        
        int no_of_AI = 0;
        System.Random rnd = new System.Random();
        static System.Random random = new System.Random();
        bool isInAir;

        #region Data
        class StoredData
        {
            public Dictionary<string, MonumentSettings> CustomProfiles = new Dictionary<string, MonumentSettings>();
            public StoredData()
            {
            }
        }

        StoredData storedData;
        #endregion
                
        public double GetRandomNumber(double minimum, double maximum)
        { 
            return random.NextDouble() * (maximum - minimum) + minimum;
        }
        
        void Init()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
            };
            var filter = RustExtension.Filter.ToList(); // Thanks Fuji. :)
            filter.Add("cover points");
            filter.Add("resulted in a conflict");
            RustExtension.Filter = filter.ToArray();
            no_of_AI = 0;
            Wipe();
            LoadConfigVariables();
            if (configData.Options.Cull_Default_Population)
            {
            NPCPlayerApex.Population = 0;
            NPCMurderer.Population = 0;
            }
        }
        
        void OnServerInitialized()
        {
            FindMonuments();
        }

        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission(permAllowed, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("BotSpawn");
            if (configData.Options.Reset)
            timer.Repeat(configData.Options.Reset_Timer, 0, () => cmdBotRespawn());
        }
        
        void Unload()
        {
            var filter = RustExtension.Filter.ToList();
            filter.Remove("OnServerInitialized");
            filter.Remove("cover points");
            filter.Remove("resulted in a conflict");
            RustExtension.Filter = filter.ToArray();
            Wipe();
        }
        
        void Wipe()
        {
	    foreach (var bot in TempRecord.NPCPlayers)
	    {
            if (bot.Value.bot != null)
            bot.Value.bot.Kill();
            else
            continue;
	    }
	    TempRecord.NPCPlayers.Clear();            
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2)
                    return false;
                    return true;
        }
    
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity as NPCPlayer != null)
            {
                NPCPlayerApex botapex = entity as NPCPlayerApex;
                
                if (TempRecord.NPCPlayers.ContainsKey(botapex))
                {
                    if (TempRecord.NPCPlayers[botapex].healthSurplus > 100)
                    {
                    TempRecord.NPCPlayers[botapex].healthSurplus = TempRecord.NPCPlayers[botapex].healthSurplus - (info.damageTypes.Total() / 2);
                    info.damageTypes.ScaleAll(0);
                    }
                    
                    NextTick(() =>
                    {
                        if (botapex == null)return;
                        if (info.damageTypes.Total() > entity.health && !configData.Options.Bots_Drop_Weapons)
                        {
                            botapex.svActiveItemID = 0u;
                            botapex.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                            return;
                        }
                    });
                }
            }
                            
            //bool chute;                       //commented awaiting changes in Rust.
            //NPCPlayerApex botapex;
            //if (entity is NPCPlayer)
            //{
            //    botapex = entity.GetComponent<NPCPlayerApex>();
            //        if (TempRecord.NPCPlayers.ContainsKey(botapex))
            //        {
            //            if (TempRecord.NPCPlayers[botapex].invincible)
            //            foreach (var child in botapex.children)
            //            if (child.ToString().Contains("parachute"))
            //            return true;
            //        }
            //}
            
            if (entity is NPCPlayer && info.Initiator is BasePlayer)
            {
                var canNetwork = Vanish?.Call("IsInvisible", info.Initiator); //bots wont retaliate to vanished players
                    if ((canNetwork is bool))
                    if ((bool)canNetwork)
                    {
                        info.Initiator = null;
                    }

                    if (configData.Options.Peace_Keeper)
                    {
                    var heldMelee = info.Weapon as BaseMelee;
                    var heldTorchWeapon = info.Weapon as TorchWeapon; 
                    if (heldMelee != null || heldTorchWeapon != null)
                    info.damageTypes.ScaleAll(0);
                    }//prevent melee farming with peacekeeper on

            }

            if (entity is NPCPlayer && info.Initiator is NPCPlayer && !(configData.Options.NPC_Retaliate)) //bots wont retaliate to friendly fire
            {
                info.Initiator = null;
            }
            
            if (info?.Initiator is NPCPlayer && entity is BasePlayer)
            {
                var attacker = info?.Initiator as NPCPlayer;
            
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
                        info.damageTypes.ScaleAll(bot.Value.damage);
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
                            TempRecord.DeadNPCPlayerIds.Add(bot.Value.botID);
                        }
                }
                if(respawnLocationName == "AirDrop")
                {
                UpdateRecords(Scientist);
                return;
                }
                foreach (var profile in TempRecord.MonumentProfiles)
                {
                    if(profile.Key == respawnLocationName)
                    {
                        timer.Once(profile.Value.Respawn_Timer, () => SpawnSci(profile.Key, profile.Value, null));
                        UpdateRecords(Scientist);
                    }
                }
            }
            else
            {
                return;
            }
        }
        
        // Facepunch.RandomUsernames
        public static string Get(ulong v) //credit fuji.
        {
            return Facepunch.RandomUsernames.Get((int)(v % 2147483647uL));
        }

		BaseEntity InstantiateSci(Vector3 position, Quaternion rotation, bool murd) // Spawn population spam fix - credit Fuji
        {
            string prefabname = "assets/prefabs/npc/scientist/scientist.prefab";
            if (murd == true)
            {
                prefabname ="assets/prefabs/npc/murderer/murderer.prefab";
            }

            var prefab = GameManager.server.FindPrefab(prefabname);
            GameObject gameObject = Instantiate.GameObject(prefab, position, rotation);
            gameObject.name = prefabname;
            SceneManager.MoveGameObjectToScene(gameObject, Rust.Server.EntityScene);
                if (gameObject.GetComponent<Spawnable>())
                    UnityEngine.Object.Destroy(gameObject.GetComponent<Spawnable>());
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);
			BaseEntity component = gameObject.GetComponent<BaseEntity>();
            return component;
        }

        void SpawnSci(string name, MonumentSettings settings, string type = null)
        {
            var murd = settings.Murderer;
            var pos = settings.Location;
            var zone = settings;

                int X = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                int Z = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                int dropX = rnd.Next(5, 10);
                int dropZ = rnd.Next(5, 10);
                int Y = 100;
                //int Y = rnd.Next(zone.Spawn_Height, (zone.Spawn_Height + 50));
                var CentrePos = new Vector3((pos.x + X),200,(pos.z + Z));    
                Quaternion rot = Quaternion.Euler(0, 0, 0);
                Vector3 newPos = (CalculateGroundPos(CentrePos));
                //if (zone.Chute)newPos =  newPos + new Vector3(0,Y,0);  //commented awaiting changes in Rust.
                //if (type == "AirDrop" && zone.Chute) newPos = new Vector3((pos.x + dropX),pos.y,(pos.z + dropZ));
				//NPCPlayer entity = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", newPos, rot, true) as NPCPlayer;

                NPCPlayer entity = (NPCPlayer)InstantiateSci(newPos, rot, murd);
				var botapex = entity.GetComponent<NPCPlayerApex>();                
                botapex.Spawn();

                if (zone.Disable_Radio)
                botapex.GetComponent<FacepunchBehaviour>().CancelInvoke(new Action(botapex.RadioChatter));
                                      
                TempRecord.NPCPlayers.Add(botapex, new botData()
                {
                spawnPoint = newPos,
                //invincible = zone.Invincible_In_Air,
                accuracy = zone.Bot_Accuracy,
                damage = zone.Bot_Damage,
                botID = entity.userID,
                bot = entity,
                healthSurplus = zone.BotHealth,
                monumentName = name,
                });

            
                no_of_AI++;
                
                if (zone.Kit != "default")
                {
                    entity.inventory.Strip(); 
                    Kits?.Call($"GiveKit", entity, zone.Kit);
                }
                
                if (zone.BotName == "randomname")
                {
                entity.displayName = Get(entity.userID);
                }
                else
                {
                entity.displayName = zone.BotName;
                }
                SetFiringRange(botapex, zone.Bot_Firing_Range);
                //if (zone.Chute)                   //commented awaiting changes in Rust.
                //{
                //var Chute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", newPos, rot);
                //Chute.gameObject.Identity();
                //Chute.SetParent(botapex, "parachute");
                //Chute.Spawn();
                //float x = Convert.ToSingle(GetRandomNumber(-0.16, 0.16));
                //float z = Convert.ToSingle(GetRandomNumber(-0.16, 0.16));
                //float varySpeed = Convert.ToSingle(GetRandomNumber(0.4, 0.8));
                //Drop(botapex, Chute, zone, x, varySpeed, z);
                //}         

		    int suicInt = rnd.Next((configData.Options.Suicide_Timer), (configData.Options.Suicide_Timer + 10));
		    
		    if (type == "AirDrop" || type == "Attack")
		    {
			timer.Once(suicInt, () =>
			{
			    if (TempRecord.NPCPlayers.ContainsKey(botapex))
			    {
                    Effect.server.Run("assets/prefabs/weapons/rocketlauncher/effects/rocket_explosion.prefab", botapex.transform.position);
                    OnEntityDeath(botapex);
                    botapex.Kill();
			    }
			    else return;
			});
		    }
        }

        void SetFiringRange(NPCPlayerApex botapex, int range)
        {
            if (botapex == null)
            {        
                TempRecord.NPCPlayers.Remove(botapex);
                return;
            }                
            AttackEntity heldEntity = botapex.GetHeldEntity() as AttackEntity;
            if (heldEntity != null)
            {
                    var heldMelee = heldEntity as BaseMelee;
                    var heldTorchWeapon = heldEntity as TorchWeapon;
                    if (heldMelee != null || heldTorchWeapon != null)
                       heldEntity.effectiveRange = 1; 
                    else
                        heldEntity.effectiveRange = range;
            return;
            }
            else
            {
                timer.Once(1, () => SetFiringRange(botapex, range));
            }      
        }
        //void Drop(NPCPlayer bot, BaseEntity Chute, MonumentSettings zone, float x, float varyY, float z)
        //{
        //    if (bot == null) return;
        //    isInAir = true;
        //    var gnd = (CalculateGroundPos(bot.transform.position));
        //    var botapex = bot.GetComponent<NPCPlayerApex>();
        //    if (bot.transform.position.y > gnd.y)
        //    {
        //        float logSpeed = ((bot.transform.position.y / 150f) + varyY);
        //        bot.transform.position = bot.transform.position + new Vector3(x, (-logSpeed), z);
        //        timer.Once(0.2f, () =>
        //        {
        //            if (TempRecord.NPCPlayers.ContainsKey(botapex))
        //            Drop(bot, Chute, zone, x, varyY, z);
        //        });
        //                        
        //    }
        //    else
        //    {
        //        isInAir = false;
        //        botapex.Resume();
        //        bot.RemoveChild(Chute);
        //        Chute.Kill();
        //    }
        //}

	void OnEntitySpawned(BaseEntity entity)
	{
        if (entity != null)
        {
            if (entity is DroppedItemContainer)
            {
                NextTick(() =>
                {
                    if (entity == null || entity.IsDestroyed) return;
                    var container = entity as DroppedItemContainer;
                    
                    ulong ownerID = container.playerSteamID;
                    if (ownerID == 0) return;
                    if (configData.Options.Remove_BackPacks)
                    {
                        foreach (var ID in TempRecord.DeadNPCPlayerIds)
                        {
                            if (ID.ToString() == ownerID.ToString())
                            {
                                entity.Kill();
                                TempRecord.DeadNPCPlayerIds.Remove(ownerID);
                                return;
                            }
                        }
                    }

                });
            }
	    
	    
            Vector3 dropLocation = new Vector3(0,0,0);
            if (!(entity.name.Contains("supply_drop")))
            return;
                
            dropLocation = (CalculateGroundPos(entity.transform.position));
            List<BaseEntity> entitiesWithinRadius = new List<BaseEntity>();
            Vis.Entities(dropLocation, 50f, entitiesWithinRadius);
            foreach (var BaseEntity in entitiesWithinRadius)
            {
                if (BaseEntity.name.Contains("grenade.smoke.deployed") && !(configData.Options.Supply_Enabled))
                return;
            }
                foreach (var profile in TempRecord.MonumentProfiles)
                {
                    if(profile.Key == "AirDrop" && profile.Value.Activate == true)
                    {
                        timer.Repeat(0f,profile.Value.Bots, () =>
                        {
                        profile.Value.Location = entity.transform.position;
                        SpawnSci(profile.Key, profile.Value, "AirDrop");
                        }
                        );
                    }
                }
        }
	}
        
        #region targeting
        
        object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity entity)//stops bots targetting animals
        {
            //if ((bool)isInAir && configData.Options.Ai_Falling_Disable)
            ///return 0f;
            BasePlayer victim = null;
            if (entity is BasePlayer)
            {
            victim = entity as BasePlayer; 
                    
                if (victim is NPCPlayer) //stop murderers attacking scientists.
                return 0f;
                if (configData.Options.Peace_Keeper)
                {
                if (victim.svActiveItemID == 0u)
                {
                return 0f;
                }
                else
                {
                    var heldWeapon = victim.GetHeldEntity() as BaseProjectile;
                    var heldFlame = victim.GetHeldEntity() as FlameThrower;
                    if (heldWeapon == null && heldFlame == null)
                    return 0f;
                }                   
                }


            if(!victim.userID.IsSteamId() && configData.Options.Ignore_HumanNPC)
            return 0f;
            }
            if (entity.name.Contains("agents/") && configData.Options.Ignore_Animals)
            return 0f;
            else
            return null;

        }
        
        object CanBradleyApcTarget(BradleyAPC bradley, BaseEntity target)//stops bradley targetting bots
        {
            if (target is NPCPlayer && configData.Options.APC_Safe)
            return false;
            return null;
        }
        
        object OnNpcTarget(BaseNpc npc, BaseEntity entity)//stops animals targetting bots
        {
            if (entity is NPCPlayer && configData.Options.Animal_Safe)
            return 0f;
            return null;
        }

        object CanBeTargeted(BaseCombatEntity player, MonoBehaviour turret)//stops autoturrets targetting bots
        {
            if (player is NPCPlayer && configData.Options.Turret_Safe)
            return false;
            return null;
        }
        
        #endregion
        void AttackPlayer(BasePlayer player, string name, MonumentSettings profile)
        {
        Vector3 location = (CalculateGroundPos(player.transform.position));
    
            timer.Repeat(0f,profile.Bots, () =>
            {
            profile.Location = location;
            SpawnSci(name, profile, "Attack");
            }
            );
        }
    
        static BasePlayer FindPlayerByName(string name)
        {
            BasePlayer result = null;
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
            if (current.displayName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                BasePlayer result2 = current;
                return result2;
            }
            if (current.UserIDString.Contains(name, CompareOptions.OrdinalIgnoreCase))
            {
                BasePlayer result2 = current;
                return result2;
            }
            if (current.displayName.Contains(name, CompareOptions.OrdinalIgnoreCase))
            {
                result = current;
            }
            }
            return result;
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
                foreach (Item item in player.inventory.containerBelt.itemList) 
                {
                    item.Remove();
                } 
                TempRecord.NPCPlayers.Remove(bot.Key);
                return;
                }
            }
        }

        static Vector3 CalculateGroundPos(Vector3 sourcePos) // credit Wulf & Nogrod 
        {
            RaycastHit hitInfo;

            if (UnityEngine.Physics.Raycast(sourcePos, Vector3.down, out hitInfo, 800f, LayerMask.GetMask("Terrain", "World", "Construction"), QueryTriggerInteraction.Ignore))
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
            int lighthouse = 0;
            int gasstation = 0;
            int spermket = 0;
            foreach (var gobject in allobjects)
            {
                if (gobject.name.Contains("autospawn/monument")) 
                {
                    var pos = gobject.transform.position;
                    if (gobject.name.Contains("powerplant_1"))
                    {
                    TempRecord.MonumentProfiles.Add("PowerPlant", new MonumentSettings
                    {
                        Activate = configData.Zones.Powerplant.Activate,
                        Murderer = configData.Zones.Powerplant.Murderer,
                        Bots = configData.Zones.Powerplant.Bots,
                        BotHealth = configData.Zones.Powerplant.BotHealth,
                        Radius = configData.Zones.Powerplant.Radius,
                        Kit = configData.Zones.Powerplant.Kit,
                        BotName = configData.Zones.Powerplant.BotName,
                        //Chute = configData.Zones.Powerplant.Chute,
                        Bot_Firing_Range = configData.Zones.Powerplant.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Powerplant.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Powerplant.Bot_Damage,
                        Disable_Radio = configData.Zones.Powerplant.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Powerplant.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Powerplant.Spawn_Height,
                        Respawn_Timer = configData.Zones.Powerplant.Respawn_Timer,
                        Location = pos,
                    });             
                    continue;
                    }
 
                    if (gobject.name.Contains("airfield_1"))
                    {
                    TempRecord.MonumentProfiles.Add("Airfield", new MonumentSettings
                    {
                        Activate = configData.Zones.Airfield.Activate,
                        Murderer = configData.Zones.Airfield.Murderer,
                        Bots = configData.Zones.Airfield.Bots,
                        BotHealth = configData.Zones.Airfield.BotHealth,
                        Radius = configData.Zones.Airfield.Radius,
                        Kit = configData.Zones.Airfield.Kit,
                        BotName = configData.Zones.Airfield.BotName,
                        //Chute = configData.Zones.Airfield.Chute,
                        Bot_Firing_Range = configData.Zones.Airfield.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Airfield.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Airfield.Bot_Damage,
                        Disable_Radio = configData.Zones.Airfield.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Airfield.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Airfield.Spawn_Height,
                        Respawn_Timer = configData.Zones.Airfield.Respawn_Timer,
                        Location = pos,
                    });    
                    continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                    TempRecord.MonumentProfiles.Add("Trainyard", new MonumentSettings
                    {
                        Activate = configData.Zones.Trainyard.Activate,
                        Murderer = configData.Zones.Trainyard.Murderer,
                        Bots = configData.Zones.Trainyard.Bots,
                        BotHealth = configData.Zones.Trainyard.BotHealth,
                        Radius = configData.Zones.Trainyard.Radius,
                        Kit = configData.Zones.Trainyard.Kit,
                        BotName = configData.Zones.Trainyard.BotName,
                        //Chute = configData.Zones.Trainyard.Chute,
                        Bot_Firing_Range = configData.Zones.Trainyard.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Trainyard.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Trainyard.Bot_Damage,
                        Disable_Radio = configData.Zones.Trainyard.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Trainyard.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Trainyard.Spawn_Height,
                        Respawn_Timer = configData.Zones.Trainyard.Respawn_Timer,
                        Location = pos,
                    });              
                    continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1")) 
                    {
                    TempRecord.MonumentProfiles.Add("Watertreatment", new MonumentSettings
                    {
                        Activate = configData.Zones.Watertreatment.Activate,
                        Murderer = configData.Zones.Watertreatment.Murderer,
                        Bots = configData.Zones.Watertreatment.Bots,
                        BotHealth = configData.Zones.Watertreatment.BotHealth,
                        Radius = configData.Zones.Watertreatment.Radius,
                        Kit = configData.Zones.Watertreatment.Kit,
                        BotName = configData.Zones.Watertreatment.BotName,
                        //Chute = configData.Zones.Watertreatment.Chute,
                        Bot_Firing_Range = configData.Zones.Watertreatment.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Watertreatment.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Watertreatment.Bot_Damage,
                        Disable_Radio = configData.Zones.Watertreatment.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Watertreatment.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Watertreatment.Spawn_Height,
                        Respawn_Timer = configData.Zones.Watertreatment.Respawn_Timer,
                        Location = pos,
                    });     
                    continue;
                    }

                    if (gobject.name.Contains("satellite_dish")) 
                    {
                    TempRecord.MonumentProfiles.Add("Satellite", new MonumentSettings
                    {
                        Activate = configData.Zones.Satellite.Activate,
                        Murderer = configData.Zones.Satellite.Murderer,
                        Bots = configData.Zones.Satellite.Bots,
                        BotHealth = configData.Zones.Satellite.BotHealth,
                        Radius = configData.Zones.Satellite.Radius,
                        Kit = configData.Zones.Satellite.Kit,
                        BotName = configData.Zones.Satellite.BotName,
                        //Chute = configData.Zones.Satellite.Chute,
                        Bot_Firing_Range = configData.Zones.Satellite.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Satellite.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Satellite.Bot_Damage,  
                        Disable_Radio = configData.Zones.AirDrop.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.AirDrop.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Satellite.Spawn_Height,
                        Respawn_Timer = configData.Zones.Satellite.Respawn_Timer,
                        Location = pos,
                    });   
                    continue;
                    } 

                    if (gobject.name.Contains("sphere_tank"))
                    {
                    TempRecord.MonumentProfiles.Add("Dome", new MonumentSettings
                    {
                        Activate = configData.Zones.Dome.Activate,
                        Murderer = configData.Zones.Dome.Murderer,
                        Bots = configData.Zones.Dome.Bots,
                        BotHealth = configData.Zones.Dome.BotHealth,
                        Radius = configData.Zones.Dome.Radius,
                        Kit = configData.Zones.Dome.Kit,
                        BotName = configData.Zones.Dome.BotName,
                        //Chute = configData.Zones.Dome.Chute,
                        Bot_Firing_Range = configData.Zones.Dome.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Dome.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Dome.Bot_Damage,
                        Disable_Radio = configData.Zones.Dome.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Dome.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Dome.Spawn_Height,
                        Respawn_Timer = configData.Zones.Dome.Respawn_Timer,
                        Location = pos,
                    }); 
                    continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                    TempRecord.MonumentProfiles.Add("Radtown", new MonumentSettings
                    {
                        Activate = configData.Zones.Radtown.Activate,
                        Murderer = configData.Zones.Radtown.Murderer,
                        Bots = configData.Zones.Radtown.Bots,
                        BotHealth = configData.Zones.Radtown.BotHealth,
                        Radius = configData.Zones.Radtown.Radius,
                        Kit = configData.Zones.Radtown.Kit,
                        BotName = configData.Zones.Radtown.BotName,
                        //Chute = configData.Zones.Radtown.Chute,
                        Bot_Firing_Range = configData.Zones.Radtown.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Radtown.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Radtown.Bot_Damage,
                        Disable_Radio = configData.Zones.Radtown.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Radtown.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Radtown.Spawn_Height,
                        Respawn_Timer = configData.Zones.Radtown.Respawn_Timer,
                        Location = pos,
                    });      
                    continue;
                    }
                    
                    if (gobject.name.Contains("launch_site"))
                    {
                    TempRecord.MonumentProfiles.Add("Launchsite", new MonumentSettings
                    {
                        Activate = configData.Zones.Launchsite.Activate,
                        Murderer = configData.Zones.Launchsite.Murderer,
                        Bots = configData.Zones.Launchsite.Bots,
                        BotHealth = configData.Zones.Launchsite.BotHealth,
                        Radius = configData.Zones.Launchsite.Radius,
                        Kit = configData.Zones.Launchsite.Kit,
                        BotName = configData.Zones.Launchsite.BotName,
                        //Chute = configData.Zones.Launchsite.Chute,
                        Bot_Firing_Range = configData.Zones.Launchsite.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Launchsite.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Launchsite.Bot_Damage,
                        Disable_Radio = configData.Zones.Launchsite.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Launchsite.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Launchsite.Spawn_Height,
                        Respawn_Timer = configData.Zones.Launchsite.Respawn_Timer,
                        Location = pos,
                    }); 
                    continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                    TempRecord.MonumentProfiles.Add("MilitaryTunnel", new MonumentSettings
                    {
                        Activate = configData.Zones.MilitaryTunnel.Activate,
                        Murderer = configData.Zones.MilitaryTunnel.Murderer,
                        Bots = configData.Zones.MilitaryTunnel.Bots,
                        BotHealth = configData.Zones.MilitaryTunnel.BotHealth,
                        Radius = configData.Zones.MilitaryTunnel.Radius,
                        Kit = configData.Zones.MilitaryTunnel.Kit,
                        BotName = configData.Zones.MilitaryTunnel.BotName,
                        //Chute = configData.Zones.MilitaryTunnel.Chute,
                        Bot_Firing_Range = configData.Zones.MilitaryTunnel.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.MilitaryTunnel.Bot_Accuracy,
                        Bot_Damage = configData.Zones.MilitaryTunnel.Bot_Damage,  
                        Disable_Radio = configData.Zones.MilitaryTunnel.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.MilitaryTunnel.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.MilitaryTunnel.Spawn_Height,
                        Respawn_Timer = configData.Zones.MilitaryTunnel.Respawn_Timer,
                        Location = pos,
                    }); 
                    continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    { 
                    TempRecord.MonumentProfiles.Add("Harbor1", new MonumentSettings
                    {
                        Activate = configData.Zones.Harbor1.Activate,
                        Murderer = configData.Zones.Harbor1.Murderer,
                        Bots = configData.Zones.Harbor1.Bots,
                        BotHealth = configData.Zones.Harbor1.BotHealth,
                        Radius = configData.Zones.Harbor1.Radius,
                        Kit = configData.Zones.Harbor1.Kit,
                        BotName = configData.Zones.Harbor1.BotName,
                        //Chute = configData.Zones.Harbor1.Chute,
                        Bot_Firing_Range = configData.Zones.Harbor1.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Harbor1.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Harbor1.Bot_Damage,  
                        Disable_Radio = configData.Zones.Harbor1.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Harbor1.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Harbor1.Spawn_Height,
                        Respawn_Timer = configData.Zones.Harbor1.Respawn_Timer,
                        Location = pos,
                    });     
                    continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                    TempRecord.MonumentProfiles.Add("Harbor2", new MonumentSettings
                    {
                        Activate = configData.Zones.Harbor2.Activate,
                        Murderer = configData.Zones.Harbor2.Murderer,
                        Bots = configData.Zones.Harbor2.Bots,
                        BotHealth = configData.Zones.Harbor2.BotHealth,
                        Radius = configData.Zones.Harbor2.Radius,
                        Kit = configData.Zones.Harbor2.Kit,
                        BotName = configData.Zones.Harbor2.BotName,
                        //Chute = configData.Zones.Harbor2.Chute,
                        Bot_Firing_Range = configData.Zones.Harbor2.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Harbor2.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Harbor2.Bot_Damage,  
                        Disable_Radio = configData.Zones.Harbor2.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Harbor2.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Harbor2.Spawn_Height,
                        Respawn_Timer = configData.Zones.Harbor2.Respawn_Timer,
                        Location = pos,
                    });             
                    continue;
                    }
                    
                    if (gobject.name.Contains("gas_station_1") && gasstation == 0)
                    {
                    TempRecord.MonumentProfiles.Add("GasStation", new MonumentSettings
                    {
                        Activate = configData.Zones.GasStation.Activate,
                        Murderer = configData.Zones.GasStation.Murderer,
                        Bots = configData.Zones.GasStation.Bots,
                        BotHealth = configData.Zones.GasStation.BotHealth,
                        Radius = configData.Zones.GasStation.Radius,
                        Kit = configData.Zones.GasStation.Kit,
                        BotName = configData.Zones.GasStation.BotName,
                        //Chute = configData.Zones.GasStation.Chute,
                        Bot_Firing_Range = configData.Zones.GasStation.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.GasStation.Bot_Accuracy,
                        Bot_Damage = configData.Zones.GasStation.Bot_Damage,  
                        Disable_Radio = configData.Zones.GasStation.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.GasStation.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.GasStation.Spawn_Height,
                        Respawn_Timer = configData.Zones.GasStation.Respawn_Timer,
                        Location = pos,
                    });
                    gasstation++;
                    continue;
                    }
              
                    if (gobject.name.Contains("gas_station_1") && gasstation == 1)
                    {
                    TempRecord.MonumentProfiles.Add("GasStation1", new MonumentSettings
                    {
                        Activate = configData.Zones.GasStation1.Activate,
                        Murderer = configData.Zones.GasStation1.Murderer,
                        Bots = configData.Zones.GasStation1.Bots,
                        BotHealth = configData.Zones.GasStation1.BotHealth,
                        Radius = configData.Zones.GasStation1.Radius,
                        Kit = configData.Zones.GasStation1.Kit,
                        BotName = configData.Zones.GasStation1.BotName,
                        //Chute = configData.Zones.GasStation1.Chute,
                        Bot_Firing_Range = configData.Zones.GasStation1.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.GasStation1.Bot_Accuracy,
                        Bot_Damage = configData.Zones.GasStation1.Bot_Damage,  
                        Disable_Radio = configData.Zones.GasStation1.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.GasStation1.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.GasStation1.Spawn_Height,
                        Respawn_Timer = configData.Zones.GasStation1.Respawn_Timer,
                        Location = pos,
                    });
                    gasstation++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("supermarket_1") && spermket == 0)
                    {
                    TempRecord.MonumentProfiles.Add("SuperMarket", new MonumentSettings
                    {
                        Activate = configData.Zones.SuperMarket.Activate,
                        Murderer = configData.Zones.SuperMarket.Murderer,
                        Bots = configData.Zones.SuperMarket.Bots,
                        BotHealth = configData.Zones.SuperMarket.BotHealth,
                        Radius = configData.Zones.SuperMarket.Radius,
                        Kit = configData.Zones.SuperMarket.Kit,
                        BotName = configData.Zones.SuperMarket.BotName,
                        //Chute = configData.Zones.SuperMarket.Chute,
                        Bot_Firing_Range = configData.Zones.SuperMarket.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.SuperMarket.Bot_Accuracy,
                        Bot_Damage = configData.Zones.SuperMarket.Bot_Damage,  
                        Disable_Radio = configData.Zones.SuperMarket.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.SuperMarket.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.SuperMarket.Spawn_Height,
                        Respawn_Timer = configData.Zones.SuperMarket.Respawn_Timer,
                        Location = pos,
                    });
                    spermket++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("supermarket_1") && spermket == 1)
                    {
                    TempRecord.MonumentProfiles.Add("SuperMarket1", new MonumentSettings
                    {
                        Activate = configData.Zones.SuperMarket1.Activate,
                        Murderer = configData.Zones.SuperMarket1.Murderer,
                        Bots = configData.Zones.SuperMarket1.Bots,
                        BotHealth = configData.Zones.SuperMarket1.BotHealth,
                        Radius = configData.Zones.SuperMarket1.Radius,
                        Kit = configData.Zones.SuperMarket1.Kit,
                        BotName = configData.Zones.SuperMarket1.BotName,
                        //Chute = configData.Zones.SuperMarket1.Chute,
                        Bot_Firing_Range = configData.Zones.SuperMarket1.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.SuperMarket1.Bot_Accuracy,
                        Bot_Damage = configData.Zones.SuperMarket1.Bot_Damage,  
                        Disable_Radio = configData.Zones.SuperMarket1.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.SuperMarket1.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.SuperMarket1.Spawn_Height,
                        Respawn_Timer = configData.Zones.SuperMarket1.Respawn_Timer,
                        Location = pos,
                    });
                    spermket++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("lighthouse") && lighthouse == 0)
                    {
                    TempRecord.MonumentProfiles.Add("Lighthouse", new MonumentSettings
                    {
                        Activate = configData.Zones.Lighthouse.Activate,
                        Murderer = configData.Zones.Lighthouse.Murderer,
                        Bots = configData.Zones.Lighthouse.Bots,
                        BotHealth = configData.Zones.Lighthouse.BotHealth,
                        Radius = configData.Zones.Lighthouse.Radius,
                        Kit = configData.Zones.Lighthouse.Kit,
                        BotName = configData.Zones.Lighthouse.BotName,
                        //Chute = configData.Zones.Lighthouse.Chute,
                        Bot_Firing_Range = configData.Zones.Lighthouse.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Lighthouse.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Lighthouse.Bot_Damage,
                        Disable_Radio = configData.Zones.Lighthouse.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Lighthouse.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Lighthouse.Spawn_Height,
                        Respawn_Timer = configData.Zones.Lighthouse.Respawn_Timer,
                        Location = pos,
                    });             
                    lighthouse++;
                    continue;
                    }
    
                    if (gobject.name.Contains("lighthouse") && lighthouse == 1)
                    {                        
                    TempRecord.MonumentProfiles.Add("Lighthouse1", new MonumentSettings
                    {
                        Activate = configData.Zones.Lighthouse1.Activate,
                        Murderer = configData.Zones.Lighthouse1.Murderer,
                        Bots = configData.Zones.Lighthouse1.Bots,
                        BotHealth = configData.Zones.Lighthouse1.BotHealth,
                        Radius = configData.Zones.Lighthouse1.Radius,
                        Kit = configData.Zones.Lighthouse1.Kit,
                        BotName = configData.Zones.Lighthouse1.BotName,
                        //Chute = configData.Zones.Lighthouse1.Chute,
                        Bot_Firing_Range = configData.Zones.Lighthouse1.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Lighthouse1.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Lighthouse1.Bot_Damage,
                        Disable_Radio = configData.Zones.Lighthouse1.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Lighthouse1.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Lighthouse1.Spawn_Height,
                        Respawn_Timer = configData.Zones.Lighthouse1.Respawn_Timer,
                        Location = pos,
                    });     
                    lighthouse++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("lighthouse") && lighthouse == 2)
                    {
                    TempRecord.MonumentProfiles.Add("Lighthouse2", new MonumentSettings
                    {
                        Activate = configData.Zones.Lighthouse2.Activate,
                        Murderer = configData.Zones.Lighthouse2.Murderer,
                        Bots = configData.Zones.Lighthouse2.Bots,
                        BotHealth = configData.Zones.Lighthouse2.BotHealth,
                        Radius = configData.Zones.Lighthouse2.Radius,
                        Kit = configData.Zones.Lighthouse2.Kit,
                        BotName = configData.Zones.Lighthouse2.BotName,
                        //Chute = configData.Zones.Lighthouse2.Chute,
                        Bot_Firing_Range = configData.Zones.Lighthouse2.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Lighthouse2.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Lighthouse2.Bot_Damage,
                        Disable_Radio = configData.Zones.Lighthouse2.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Lighthouse2.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Lighthouse2.Spawn_Height,
                        Respawn_Timer = configData.Zones.Lighthouse2.Respawn_Timer,
                        Location = pos,
                    });     
                    lighthouse++;
                    continue;
                    }

                    if (gobject.name.Contains("warehouse") && warehouse == 0)
                    {
                    TempRecord.MonumentProfiles.Add("Warehouse", new MonumentSettings
                    {
                        Activate = configData.Zones.Warehouse.Activate,
                        Murderer = configData.Zones.Warehouse.Murderer,
                        Bots = configData.Zones.Warehouse.Bots,
                        BotHealth = configData.Zones.Warehouse.BotHealth,
                        Radius = configData.Zones.Warehouse.Radius,
                        Kit = configData.Zones.Warehouse.Kit,
                        BotName = configData.Zones.Warehouse.BotName,
                        //Chute = configData.Zones.Warehouse.Chute,
                        Bot_Firing_Range = configData.Zones.Warehouse.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Warehouse.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Warehouse.Bot_Damage,
                        Disable_Radio = configData.Zones.Warehouse.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Warehouse.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Warehouse.Spawn_Height,
                        Respawn_Timer = configData.Zones.Warehouse.Respawn_Timer,
                        Location = pos,
                    });             
                    warehouse++;
                    continue;
                    }
    
                    if (gobject.name.Contains("warehouse") && warehouse == 1)
                    {                        
                    TempRecord.MonumentProfiles.Add("Warehouse1", new MonumentSettings
                    {
                        Activate = configData.Zones.Warehouse1.Activate,
                        Murderer = configData.Zones.Warehouse1.Murderer,
                        Bots = configData.Zones.Warehouse1.Bots,
                        BotHealth = configData.Zones.Warehouse1.BotHealth,
                        Radius = configData.Zones.Warehouse1.Radius,
                        Kit = configData.Zones.Warehouse1.Kit,
                        BotName = configData.Zones.Warehouse1.BotName,
                        //Chute = configData.Zones.Warehouse1.Chute,
                        Bot_Firing_Range = configData.Zones.Warehouse1.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Warehouse1.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Warehouse1.Bot_Damage,
                        Disable_Radio = configData.Zones.Warehouse1.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Warehouse1.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Warehouse1.Spawn_Height,
                        Respawn_Timer = configData.Zones.Warehouse1.Respawn_Timer,
                        Location = pos,
                    });     
                    warehouse++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("warehouse") && warehouse == 2)
                    {
                    TempRecord.MonumentProfiles.Add("Warehouse2", new MonumentSettings
                    {
                        Activate = configData.Zones.Warehouse2.Activate,
                        Murderer = configData.Zones.Warehouse2.Murderer,
                        Bots = configData.Zones.Warehouse2.Bots,
                        BotHealth = configData.Zones.Warehouse2.BotHealth,
                        Radius = configData.Zones.Warehouse2.Radius,
                        Kit = configData.Zones.Warehouse2.Kit,
                        BotName = configData.Zones.Warehouse2.BotName,
                        //Chute = configData.Zones.Warehouse2.Chute,
                        Bot_Firing_Range = configData.Zones.Warehouse2.Bot_Firing_Range,
                        Bot_Accuracy = configData.Zones.Warehouse2.Bot_Accuracy,
                        Bot_Damage = configData.Zones.Warehouse2.Bot_Damage,
                        Disable_Radio = configData.Zones.Warehouse2.Disable_Radio,
                        //Invincible_In_Air = configData.Zones.Warehouse2.Invincible_In_Air,
                        //Spawn_Height = configData.Zones.Warehouse2.Spawn_Height,
                        Respawn_Timer = configData.Zones.Warehouse2.Respawn_Timer,
                        Location = pos,
                    });     
                    warehouse++;
                    continue;
                    }
                    if (gobject.name.Contains("supermarket_1") && warehouse > 2)
                    continue;
                    if (gobject.name.Contains("supermarket_1") && lighthouse > 2)
                    continue;
                    if (gobject.name.Contains("supermarket_1") && gasstation > 1)
                    continue;
                    if (gobject.name.Contains("supermarket_1") && spermket > 1)
                    continue;
                }
            }
            TempRecord.MonumentProfiles.Add("AirDrop", new MonumentSettings
                {
                    Activate = configData.Zones.AirDrop.Activate,
                    Murderer = configData.Zones.AirDrop.Murderer,
                    Bots = configData.Zones.AirDrop.Bots,
                    BotHealth = configData.Zones.AirDrop.BotHealth,
                    Radius = configData.Zones.AirDrop.Radius,
                    Kit = configData.Zones.AirDrop.Kit,
                    BotName = configData.Zones.AirDrop.BotName,
                    //Chute = configData.Zones.AirDrop.Chute,
                    Bot_Firing_Range = configData.Zones.AirDrop.Bot_Firing_Range,
                    Bot_Accuracy = configData.Zones.AirDrop.Bot_Accuracy,
                    Bot_Damage = configData.Zones.AirDrop.Bot_Damage,
                    Disable_Radio = configData.Zones.AirDrop.Disable_Radio,
                    //Invincible_In_Air = configData.Zones.AirDrop.Invincible_In_Air,
                    Location = new Vector3(0,0,0),
                    Respawn_Timer = 10,
                    //Spawn_Height = 100,
                });     
            
            foreach (var profile in storedData.CustomProfiles)
            TempRecord.MonumentProfiles.Add(profile.Key, profile.Value);

            foreach (var profile in TempRecord.MonumentProfiles)
            {
                if (profile.Value.Kit != "default" && Kits == null)
                {
                    PrintWarning(lang.GetMessage("nokits", this));
                    return;
                }
            if(profile.Value.Activate == true && profile.Value.Bots > 0 && !profile.Key.Contains("AirDrop"))
            timer.Repeat(2,profile.Value.Bots, () => SpawnSci(profile.Key, profile.Value, null));
            }
        }
        #region Commands
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
            Puts($"There are {total} bots.");
        }
        
        [ConsoleCommand("bot.stats")]
        void cmdBotStats()
        {
            foreach(var botapex in GameObject.FindObjectsOfType<NPCPlayerApex>())
            {
                Puts($"agentTypeIndex = {botapex.agentTypeIndex}");
                Puts($"AttackTarget = {botapex.AttackTarget}");
                Puts($"IsStopped = {botapex.IsStopped}");
                Puts($"GuardPosition = {botapex.GuardPosition}");
                Puts($"AttackReady = {botapex.AttackReady()}");
                Puts($"WeaponAttackRange = {botapex.WeaponAttackRange()}");
                Puts($"Size = {botapex.Stats.Size}");
                Puts($"Speed = {botapex.Stats.Speed}");
                Puts($"Acceleration = {botapex.Stats.Acceleration}");
                Puts($"TurnSpeed = {botapex.Stats.TurnSpeed}");
                Puts($"VisionRange = {botapex.Stats.VisionRange}");
                Puts($"VisionCone = {botapex.Stats.VisionCone}");
                Puts($"Hostility = {botapex.Stats.Hostility}");
                Puts($"Defensiveness = {botapex.Stats.Defensiveness}");
                Puts($"AggressionRange = {botapex.Stats.AggressionRange}");
            }
        }        
        
        [ChatCommand("botspawn")]
        void botspawn(BasePlayer player, string command, string[] args)
        {
            if (HasPermission(player.UserIDString, permAllowed) || isAuth(player))
            if (args != null && args.Length == 1)
            {
                if (args[0] == "list")
                {
                var outMsg = lang.GetMessage("ListTitle", this);                   
                        
                foreach (var profile in storedData.CustomProfiles)
                {outMsg += $"\n{profile.Key}";}
                
                PrintToChat(player, outMsg);
                }
                else
                SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("error", this)); 
            }
            else if (args != null && args.Length == 2)
            {
                if (args[0] == "add")
                {
                    var name = args[1];
                    if (storedData.CustomProfiles.ContainsKey(name))
                    {
                        SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("alreadyexists", this),name);
                        return;
                    }
                    var customSettings = new MonumentSettings()
                    {
                        Activate = false,
                        BotName = "randomname",
                        Location = player.transform.position,
                    };
                    
                    storedData.CustomProfiles.Add(name, customSettings);
                    Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData);
                    SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("customsaved", this),player.transform.position);
                    //timer.Repeat(2,storedData.CustomProfiles[name].Bots, () => SpawnSci(name, customSettings, null));
                }
                
                else if (args[0] == "move")
                {
                    var name = args[1];
                    if (storedData.CustomProfiles.ContainsKey(name))
                    {
                        storedData.CustomProfiles[name].Location = player.transform.position;
                        Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData);
                        SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("custommoved", this),name);
                    }
                    else
                    SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("noprofile", this));
                }
                
                else if (args[0] == "remove")
                {
                    var name = args[1];
                    if (storedData.CustomProfiles.ContainsKey(name))
                    {
                        foreach (var bot in TempRecord.NPCPlayers)
                        {
                            if (bot.Value.monumentName == name)
                                if (bot.Value.bot != null)
                                bot.Value.bot.Kill();
                                else
                                continue;
                        }
                        TempRecord.MonumentProfiles.Remove(name);
                        storedData.CustomProfiles.Remove(name);
                        Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData);
                        SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("customremoved", this),name);
                    }
                    else
                    SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("noprofile", this));
                }
                else
                SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("error", this));
            }
            else if (args != null && args.Length == 3)
            {
                if (args[0] == "toplayer")
                {
                    var name = args[1];
                    var profile = args[2];
                    BasePlayer target = FindPlayerByName(name);
                    if (target == null)
                    {
                    SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("namenotfound", this),name);
                    return;                        
                    }

                        if (!(storedData.CustomProfiles.ContainsKey(profile)))
                        {
                        SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("noprofile", this));
                        return;
                        }
                            foreach (var entry in storedData.CustomProfiles)
                            {
                                if (entry.Key == profile)
                                {
                                AttackPlayer(target, entry.Key, entry.Value);
                                SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("deployed", this),profile, target.displayName);
                                }
                            }
  
                }
                else
                SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("error", this));
            }
            else
            SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("error", this));
        }
        #endregion
        
        #region Config
        private ConfigData configData;
        
        class TempRecord
        {
            public static Dictionary<NPCPlayerApex, botData> NPCPlayers = new Dictionary<NPCPlayerApex, botData>();
            public static Dictionary<string, MonumentSettings> MonumentProfiles = new Dictionary<string, MonumentSettings>();
            public static List<ulong> DeadNPCPlayerIds = new List<ulong>();
        }
        class botData
        {
            public Vector3 spawnPoint;
            //public bool invincible;
            public int accuracy;
            public float damage;
            public ulong botID;
            public BasePlayer bot;
            public string monumentName;
            public float healthSurplus;
        }
        class CustomSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public string Kit = "default";
            public string BotName = "randomname";
            //public bool Chute = false;
            public int Bot_Firing_Range = 20;
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;     
            //public bool Invincible_In_Air = true;
            //public int Spawn_Height = 100;
            public int Respawn_Timer = 60;
            public bool Disable_Radio = true;
        }
        class MonumentSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public string Kit = "default";
            public string BotName = "randomname";
            //public bool Chute = false;
            public int Bot_Firing_Range = 20;
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;     
            //public bool Invincible_In_Air = true;
            ///public int Spawn_Height = 100;
            public int Respawn_Timer = 60;
            public bool Disable_Radio = true;
            public Vector3 Location;
        }
        class AirDropSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public string Kit = "default";
            public string BotName = "randomname";
            //public bool Chute = false;
            public int Bot_Firing_Range = 20;
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;
            public bool Disable_Radio = true;
            //public bool Invincible_In_Air = true;
        }
        class Options
        {        
            public bool Ignore_Animals { get; set; }
            public bool APC_Safe { get; set; }
            public bool Reset { get; set; }
            public int Reset_Timer { get; set; }
            public bool Turret_Safe { get; set; }
            public bool Animal_Safe { get; set; }
            public int Suicide_Timer { get; set; }
            //public bool Ai_Falling_Disable { get; set; }
            public bool Bots_Drop_Weapons { get; set; }
            public bool Supply_Enabled { get; set; }
            public bool Cull_Default_Population { get; set; }
            public bool NPC_Retaliate { get; set; }
            public bool Remove_BackPacks { get; set; }
            public bool Ignore_HumanNPC { get; set; }
            public bool Peace_Keeper { get; set; }
        }
        class Zones
        {
            public CustomSettings Airfield { get; set; }
            public CustomSettings Dome { get; set; }
            public CustomSettings Powerplant { get; set; }
            public CustomSettings Radtown { get; set; }
            public CustomSettings Satellite { get; set; }
            public CustomSettings Trainyard { get; set; }
            public CustomSettings Watertreatment { get; set; }
            public CustomSettings Launchsite { get; set; }
            public CustomSettings MilitaryTunnel { get; set; }
            public CustomSettings Harbor1 { get; set; }
            public CustomSettings Harbor2 { get; set; }
            public CustomSettings Warehouse { get; set; }
            public CustomSettings Warehouse1 { get; set; }
            public CustomSettings Warehouse2 { get; set; }
            public CustomSettings GasStation { get; set; }
            public CustomSettings GasStation1 { get; set; }
            public CustomSettings SuperMarket { get; set; }
            public CustomSettings SuperMarket1 { get; set; }
            public CustomSettings Lighthouse { get; set; }
            public CustomSettings Lighthouse1 { get; set; }
            public CustomSettings Lighthouse2 { get; set; }
            public AirDropSettings AirDrop { get; set; }
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
                    Ignore_Animals = true,
                    APC_Safe = true,
                    Reset = true,
                    Turret_Safe = true,
                    Animal_Safe = true,
                    Suicide_Timer = 300,
                    Reset_Timer = 300,
                    //Ai_Falling_Disable = true,
                    Bots_Drop_Weapons = false,
                    Supply_Enabled = false,
                    Cull_Default_Population = true,
                    NPC_Retaliate = false,
                    Remove_BackPacks = true,
                    Ignore_HumanNPC = true,
                    Peace_Keeper = true,
               },
               Zones = new Zones
               {
                    Airfield = new CustomSettings{},
                    Dome = new CustomSettings{},
                    Powerplant = new CustomSettings{},
                    Radtown = new CustomSettings{},
                    Satellite = new CustomSettings{},
                    Trainyard = new CustomSettings{},
                    Watertreatment = new CustomSettings{},
                    Launchsite = new CustomSettings{},
                    MilitaryTunnel = new CustomSettings{},
                    Harbor1 = new CustomSettings{},
                    Harbor2 = new CustomSettings{},
                    GasStation = new CustomSettings{},
                    GasStation1 = new CustomSettings{},
                    SuperMarket = new CustomSettings{},
                    SuperMarket1 = new CustomSettings{},
                    Warehouse = new CustomSettings{},
                    Warehouse1 = new CustomSettings{},
                    Warehouse2 = new CustomSettings{},
                    Lighthouse = new CustomSettings{},
                    Lighthouse1 = new CustomSettings{},
                    Lighthouse2 = new CustomSettings{},
                    AirDrop = new AirDropSettings{}
               }
            };
            SaveConfig(config);
        }
        
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
        #region messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"Title", "BotSpawn : " },
            {"error", "/botspawn commands are - list - add - remove - move - toplayer" },
            {"customsaved", "Custom Location Saved @ {0}" },
            {"custommoved", "Custom Location {0} has been moved to your current position." },
            {"alreadyexists", "Custom Location already exists with the name {0}." },
            {"customremoved", "Custom Location {0} Removed." },
            {"deployed", "'{0}' bots deployed to {1}." },
            {"ListTitle", "Custom Locations" },
            {"noprofile", "There is no profile by that name in /data/BotSpawn.json" },
            {"namenotfound", "Player '{0}' was not found" },
            {"nokits", "Kits is not installed but you have declared custom kits." },
        };
        #endregion
    }
}