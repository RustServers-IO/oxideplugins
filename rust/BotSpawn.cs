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
using ProtoBuf;
namespace Oxide.Plugins

{
    [Info("BotSpawn", "Steenamaroo", "1.3.3", ResourceId = 2580)]
    
    [Description("Spawn Bots with kits at monuments.")]
//population fix
	
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
            Scientist.Population = 0;
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
    
    
	void OnPlayerDropActiveItem(BasePlayer player, Item item)
	{
	    if (player as NPCPlayer != null)
            {
                NPCPlayerApex botapex = player as NPCPlayerApex;
                
            if (TempRecord.NPCPlayers.ContainsKey(botapex) && !configData.Options.Bots_Drop_Weapons)
            {
                item.Remove(0f);
                return;
                }
            }   
	}
    
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
	//bool chute;                       //commented awaiting changes in Rust.
	//NPCPlayerApex botapex;
	//if (entity is NPCPlayer)
	//{
	//    botapex = entity.GetComponent<NPCPlayerApex>();
	//	if (TempRecord.NPCPlayers.ContainsKey(botapex))
	//	{
	//	    if (TempRecord.NPCPlayers[botapex].invincible)
	//	    foreach (var child in botapex.children)
	//	    if (child.ToString().Contains("parachute"))
	//	    return true;
	//	}
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
            NPCPlayerApex Scientist = null;
            if (entity is NPCPlayerApex)
            {
                foreach (var bot in TempRecord.NPCPlayers)
                {
		    Scientist = entity as NPCPlayerApex;
                    if (bot.Value.botID == Scientist.userID)
                        {
                            no_of_AI--;
                            respawnLocationName = bot.Value.monumentName;
                            TempRecord.DeadNPCPlayerIds.Add(bot.Value.botID);
                            if (TempRecord.MonumentProfiles[respawnLocationName].Disable_Radio == true)
                            Scientist.DeathEffect = new GameObjectRef();
                        }
                }
                if(TempRecord.dontRespawn.Contains(Scientist.userID))
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
                TempRecord.dontRespawn.Remove(bot.Value.botID);
                return;
                }
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
            var pos = new Vector3 (settings.LocationX, settings.LocationY, settings.LocationZ);
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
                monumentName = name,
                });

                int suicInt = rnd.Next((configData.Options.Suicide_Timer), (configData.Options.Suicide_Timer + 10));
                
                if (type == "AirDrop" || type == "Attack")
                {
                TempRecord.dontRespawn.Add(botapex.userID);
                timer.Once(suicInt, () =>
                {
                    if (TempRecord.NPCPlayers.ContainsKey(botapex))
                    {
                        if (botapex != null)
                        {
                        OnEntityDeath(botapex);
                        Effect.server.Run("assets/prefabs/weapons/rocketlauncher/effects/rocket_explosion.prefab", botapex.transform.position);
                        botapex.Kill();
                        }
                        else
                        {
                            TempRecord.dontRespawn.Remove(botapex.userID);
                            TempRecord.NPCPlayers.Remove(botapex);
                            return;
                        }
                    }
                    else return; 
                });
                }
                no_of_AI++;

                if (zone.Kit != "default")
                {
                    object checkKit = (Kits.CallHook("GetKitInfo", zone.Kit, true));
                    if (checkKit == null)
                    {
                        PrintWarning("Kit does not exist - Defaulting to 'Scientist'.");
                        return;
                    }
                    else
                    {
                    entity.inventory.Strip(); 
                    Kits?.Call($"GiveKit", entity, zone.Kit, true);
                    }
                }

                entity.health = zone.BotHealth;
		
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

        }

        void SetFiringRange(NPCPlayerApex botapex, int range)
        {
            if (botapex == null)
            {        
                TempRecord.NPCPlayers.Remove(botapex);
                TempRecord.dontRespawn.Remove(botapex.userID);
                return;
            }

            var heldEntity = botapex.GetActiveItem();
            if (botapex.svActiveItemID != 0)
            {
                    List<int> weapons = new List<int>(); //check all their weapons
                    foreach (Item item in botapex.inventory.containerBelt.itemList)
                    {
                        if (item.GetHeldEntity() as BaseProjectile != null || item.GetHeldEntity() as BaseMelee != null || item.GetHeldEntity() as TorchWeapon != null)
                        {
                            weapons.Add(Convert.ToInt16(item.position));
                        }
                    }

                    if (weapons.Count == 0)
                    {
                        Puts("No suitable weapon found in kit.");
                        return;
                    }
                int index = rnd.Next(weapons.Count);
                
                foreach (Item item in botapex.inventory.containerBelt.itemList) //pick one at random
                {
                    
                    if (item.position == weapons[index])
                    {
                    var UID = botapex.inventory.containerBelt.GetSlot(weapons[index]).uid;
                    Item activeItem = item;
                    botapex.svActiveItemID = 0;
                    botapex.inventory.UpdatedVisibleHolsteredItems();
                    HeldEntity held = activeItem.GetHeldEntity() as HeldEntity;
                    botapex.svActiveItemID = UID;
                    botapex.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    held.SetHeld(true);
                    botapex.svActiveItemID = UID;
                    botapex.inventory.UpdatedVisibleHolsteredItems();
                    }
                }
                
                AttackEntity heldGun = botapex.GetHeldEntity() as AttackEntity;
                if (heldGun != null)
                {
                    var heldMelee = heldGun as BaseMelee;
                    var heldTorchWeapon = heldGun as TorchWeapon;
                    if (heldMelee != null || heldTorchWeapon != null)
                       heldGun.effectiveRange = 1; 
                    else
                        heldGun.effectiveRange = range;
                    return;
                }
		    }
            else
            {
                ti