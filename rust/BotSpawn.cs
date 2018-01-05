using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using UnityEngine.Playables;
using Oxide.Game.Rust;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using Facepunch;


namespace Oxide.Plugins
//comments are wide to the right --->
{
    [Info("BotSpawn", "Steenamaroo", "1.4.3", ResourceId = 2580)]
    
    [Description("Spawn tailored AI with kits at monuments and custom locations.")]

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
            var filter = RustExtension.Filter.ToList();                                                                                                         //Thanks Fuji. :)
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
            if (bot == null)
            {
                TempRecord.NPCPlayers.Remove(bot);
                Wipe();
                return;
            }
            var comp = bot.GetComponent<BasePlayer>();
            if (comp != null)
            comp.Kill();
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
            NPCPlayerApex Scientist = null; 
            
            if (entity is NPCPlayerApex)
               {
                    Scientist = entity as NPCPlayerApex;
                    
                    if (!TempRecord.NPCPlayers.Contains(Scientist))
                    return null;
                    
                    if (info.Initiator is BasePlayer)
                    {
                        var damagedbot = entity as NPCPlayer;
                        var canNetwork = Vanish?.Call("IsInvisible", info.Initiator);                                                                           //bots wont retaliate to vanished players
                            if ((canNetwork is bool))
                            if ((bool)canNetwork)
                            {
                                info.Initiator = null;
                            }
        
                            if (configData.Options.Peace_Keeper)                                                                                                //prevent melee farming with peacekeeper on
                            {
                            var heldMelee = info.Weapon as BaseMelee;
                            var heldTorchWeapon = info.Weapon as TorchWeapon; 
                            if (heldMelee != null || heldTorchWeapon != null)
                            info.damageTypes.ScaleAll(0);
                            }
                            
                            var bData = Scientist.GetComponent<botData>();
                            float multiplier = 100f / bData.health;
                            info.damageTypes.ScaleAll(multiplier);
                    }
               }

            if (info?.Initiator is NPCPlayer && entity is BasePlayer)                                                                                           //add in bot accuracy
            {
                var attacker = info.Initiator as NPCPlayerApex;
            
                if (TempRecord.NPCPlayers.Contains(attacker))
                {
                    var bData = attacker.GetComponent<botData>();

                    System.Random rnd = new System.Random();
                    int rand = rnd.Next(1, 10);
                        if (bData.accuracy < rand)                                                                                                              //scale bot attack damage
                        {
                        return true;
                        }
                        else
                        {
                        info.damageTypes.ScaleAll(bData.damage);
                        return null;    
                        }
                }
            }
            return null;
        }
        
        void OnPlayerDie(BasePlayer player)
        {
            string respawnLocationName = "";
            NPCPlayerApex Scientist = null;
            if (player is NPCPlayerApex)
            {
                Scientist = player as NPCPlayerApex;
                if (!TempRecord.NPCPlayers.Contains(Scientist))
                return;
            
                if (TempRecord.NPCPlayers.Contains(Scientist))                                                                                                      //kill radio effects
                {                     
                    var bData = Scientist.GetComponent<botData>();
                    Item activeItem = player.GetActiveItem();
                    if (bData.dropweapon == true)
                    {
                        using (TimeWarning timeWarning = TimeWarning.New("PlayerBelt.DropActive", 0.1f))
                        {
                            activeItem.Drop(player.eyes.position, new Vector3(), new Quaternion());
                            player.svActiveItemID = 0;
                            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        }
                    }
                    no_of_AI--;
                    respawnLocationName = bData.monumentName;
                    TempRecord.DeadNPCPlayerIds.Add(Scientist.userID);
                    if (TempRecord.MonumentProfiles[respawnLocationName].Disable_Radio == true)
                    Scientist.DeathEffect = new GameObjectRef();

                    if(bData.respawn == false)
                    {
                    UnityEngine.Object.Destroy(Scientist.GetComponent<botData>());
                    UpdateRecords(Scientist);
                    return;
                    }
                    foreach (var profile in TempRecord.MonumentProfiles)
                    {
                        if(profile.Key == respawnLocationName)
                        {
                            timer.Once(profile.Value.Respawn_Timer, () => SpawnBots(profile.Key, profile.Value, null));
                            UnityEngine.Object.Destroy(Scientist.GetComponent<botData>());
                            UpdateRecords(Scientist);
                        }
                    }
                }  
            }
        }
      
        void UpdateRecords(NPCPlayerApex player)
        {
            if (TempRecord.NPCPlayers.Contains(player))
            {
                TempRecord.NPCPlayers.Remove(player);
                return;
            }
        }
	
        // Facepunch.RandomUsernames
        public static string Get(ulong v)                                                                                                                       //credit Fujikura.
        {
            return Facepunch.RandomUsernames.Get((int)(v % 2147483647uL));
        }

        BaseEntity InstantiateSci(Vector3 position, Quaternion rotation, bool murd)                                                                             //Spawn population spam fix - credit Fujikura
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

        void SpawnBots(string name, MonumentSettings settings, string type = null)
        {

            var murd = settings.Murderer;
            var pos = new Vector3 (settings.LocationX, settings.LocationY, settings.LocationZ);
            var zone = settings;

                int X = rnd.Next((-zone.Radius/2), (zone.Radius/2));                                                                                            //no need for /2
                int Z = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                int dropX = rnd.Next(5, 10);
                int dropZ = rnd.Next(5, 10);
                int Y = 100;
                var CentrePos = new Vector3((pos.x + X),200,(pos.z + Z));    
                Quaternion rot = Quaternion.Euler(0, 0, 0);
                Vector3 newPos = (CalculateGroundPos(CentrePos));
                NPCPlayer entity = (NPCPlayer)InstantiateSci(newPos, rot, murd);
                    
                var botapex = entity.GetComponent<NPCPlayerApex>();
                               
                var bData = botapex.gameObject.AddComponent<botData>();        
             
                
                TempRecord.NPCPlayers.Add(botapex);

                botapex.GuardPosition = newPos;
                
                if (zone.Roam_Range < 20)
                zone.Roam_Range = 20;
                botapex.Spawn();
                
                bData.spawnPoint = newPos;
                bData.accuracy = zone.Bot_Accuracy;
                bData.damage = zone.Bot_Damage;
                bData.health = zone.BotHealth;
                bData.range = (zone.Aggression_Range / 3f);
                bData.monumentName = name;
                bData.respawn = true;
                bData.roamRange = zone.Roam_Range;
                bData.dropweapon = zone.Weapon_Drop;
                bData.keepAttire = zone.Keep_Default_Loadout;
                                
                int suicInt = rnd.Next((configData.Options.Suicide_Timer), (configData.Options.Suicide_Timer + 10));                                            //slightly randomise suicide de-spawn time
                
                if (type == "AirDrop" || type == "Attack")
                {
                bData.respawn = false;
                timer.Once(suicInt, () =>
                {
                    if (TempRecord.NPCPlayers.Contains(botapex))
                    {
                        if (botapex != null)
                        {
                        Effect.server.Run("assets/prefabs/weapons/rocketlauncher/effects/rocket_explosion.prefab", botapex.transform.position); //fix for manual OnEntityDeath
                        HitInfo nullHit = new HitInfo();
                        nullHit.damageTypes.Add(Rust.DamageType.Explosion, 10000);
                        botapex.Hurt(nullHit);
                        }
                        else
                        {
                            TempRecord.NPCPlayers.Remove(botapex);
                            Puts("This Shouldn't Happen");
                            return;
                        }
                    }
                    else return; 
                });
                }

                
                int kitRnd;
                if (zone.Kit.Count != 0)
                {
                    kitRnd = rnd.Next(zone.Kit.Count);
                    if (zone.Kit[kitRnd] != null)
                    {
                        object checkKit = (Kits.CallHook("GetKitInfo", zone.Kit[kitRnd], true));
                        if (checkKit == null)
                        {
                            PrintWarning($"Kit {zone.Kit[kitRnd]} does not exist - Defaulting to Scientist or Murderer.");
                        }
                        else
                        {
                            bool weaponInBelt = false;
                            if (checkKit != null && checkKit is JObject)
                            {
                                List<string> contentList = new List<string>();
                                JObject kitContents = checkKit as JObject;
                
                                JArray items = kitContents["items"] as JArray;
                                foreach (var weap in items)
                                {
                                    JObject item = weap as JObject;
                
                                    if (item["container"].ToString() == "belt")
                                        weaponInBelt = true;
                                }
                            }
                            if (!weaponInBelt)
                            {
                                PrintWarning($"Kit {zone.Kit[kitRnd]} has no items in belt - Defaulting to Scientist or Murderer.");
                            }
                            else
                            {
                                if(bData.keepAttire == false)
                                    entity.inventory.Strip(); 
                            Kits?.Call($"GiveKit", entity, zone.Kit[kitRnd], true);
                            TempRecord.kitList.Add(botapex.userID, zone.Kit[kitRnd]);
                            }
                        }
                    }
                }

                no_of_AI++;

                    foreach (Item item in botapex.inventory.containerBelt.itemList)                                                                             //store organised weapons lists
                    {
                        var held = item.GetHeldEntity();

                        if (held as HeldEntity != null)
                        {
                            if (held as BaseMelee != null || held as TorchWeapon != null)
                            bData.MeleeWeapons.Add(item);
                                else
                                {
                                    if (held as BaseProjectile != null)
                                    {
                                    bData.AllProjectiles.Add(item);
                                    if (held.name.Contains("m92") || held.name.Contains("pistol") || held.name.Contains("python") || held.name.Contains("waterpipe"))
                                    bData.CloseRangeWeapons.Add(item);
                                        else if (held.name.Contains("bolt"))
                                        bData.LongRangeWeapons.Add(item);
                                            else
                                            bData.MediumRangeWeapons.Add(item);
                                    }
                                }
                        }
                    }

                    if (zone.BotName == "randomname")
                    entity.displayName = Get(entity.userID);
                    else
                    entity.displayName = zone.BotName;
            
                    botapex.Stats.VisionRange = bData.range;
                    entity.health = zone.BotHealth;
            
                    if (zone.Disable_Radio)
                    botapex.GetComponent<FacepunchBehaviour>().CancelInvoke(new Action(botapex.RadioChatter));
            
                    timer.Once(1, () => SelectWeapon(botapex, null, false));
        }
        
	    void OnEntitySpawned(BaseEntity entity)
        {
            if (entity != null)
            {
                if (entity is NPCPlayerCorpse)
                {                
                    var corpse = entity as NPCPlayerCorpse;
                    if (!configData.Options.Allow_Rust_Loot)
                            NextTick(() =>
                               {
                                corpse.containers[0].Clear();
                                corpse.containers[1].Clear();
                                corpse.containers[2].Clear();
                               }
                               );
                            
                    string kit;
                    if (TempRecord.kitList.ContainsKey(corpse.playerSteamID))
                    {
                    kit = TempRecord.kitList[corpse.playerSteamID];
                    if (kit == null)
                    return;
                    string[] checkKit = (Kits.CallHook("GetKitContents", kit)) as string[];
                    
                        var tempbody = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", (corpse.transform.position - new Vector3(0,-100,0)), corpse.transform.rotation).ToPlayer();
                        tempbody.Spawn();
            
                        Kits?.Call($"GiveKit", tempbody, kit, true);
    
                        NextTick(() =>
                        {
                            var source = new ItemContainer[] { tempbody.inventory.containerMain, tempbody.inventory.containerWear, tempbody.inventory.containerBelt };
    
                                for (int i = 0; i < (int)source.Length; i++)
                                    {
                                        Item[] array = source[i].itemList.ToArray();
                                        for (int j = 0; j < (int)array.Length; j++)
                                        {
                                            Item item = array[j];
                                            if (!item.MoveToContainer(corpse.containers[i], -1, true))
                                            {
                                                item.Remove(0f);
                                            }
                                        }
                                    }
                                    tempbody.Kill();
    
                            if (configData.Options.Wipe_Belt)
                            corpse.containers[2].Clear();
                            if (configData.Options.Wipe_Clothing)
                            corpse.containers[1].Clear();
			    
                            TempRecord.kitList.Remove(corpse.playerSteamID);
                        });
                    }  
                }
                
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
                            if (TempRecord.DeadNPCPlayerIds.Contains(ownerID))
                                {
                                    entity.Kill();
                                    TempRecord.DeadNPCPlayerIds.Remove(ownerID);
                                    return;
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
                
                foreach (var BaseEntity in entitiesWithinRadius)                                                                                                //check for smoking grenade at proposed airdrop spawn location
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
                        profile.Value.LocationX = entity.transform.position.x;
                        profile.Value.LocationY = entity.transform.position.y;
                        profile.Value.LocationZ = entity.transform.position.z;
                        SpawnBots(profile.Key, profile.Value, "AirDrop");
                        }
                        );
                    }
                }
            }
        }
        
        void SelectWeapon(NPCPlayerApex npcPlayer, BasePlayer victim, bool hasAttacker)
        {
            if (npcPlayer == null)
            return;
        
            if (npcPlayer.svActiveItemID == 0)
            {
                return;
            }

                var active = npcPlayer.GetActiveItem();
                HeldEntity heldEntity1 = null;
                AttackEntity heldGun = null;
                
                if (active != null)
                heldEntity1 = active.GetHeldEntity() as HeldEntity;
                if (heldEntity1 != null)
                heldGun = npcPlayer.GetHeldEntity() as AttackEntity;
            
                var bData = npcPlayer.GetComponent<botData>();
                
                
                if (hasAttacker == false)
                {
                    List<int> weapons = new List<int>();                                                                                                        //check all their weapons
                    foreach (Item item in npcPlayer.inventory.containerBelt.itemList)
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
                    var currentTime = TOD_Sky.Instance.Cycle.Hour;

                    if (currentTime > 20 || currentTime < 8)
                    {
                        foreach (Item item in npcPlayer.inventory.containerBelt.itemList)                                                                          
                        {
                            HeldEntity held = item.GetHeldEntity() as HeldEntity;
                            
                            if (item.ToString().Contains("flashlight"))
                            {
                            if (heldEntity1 != null)
                            heldEntity1.SetHeld(false);
                            var UID = item.uid;
                            
                            ChangeWeapon(npcPlayer, held, UID);
                            return;
                            }
                        }
                    }
                    else
                    {
                        foreach (Item item in npcPlayer.inventory.containerBelt.itemList)                                                                           //pick one at random to start with
                        {
                            HeldEntity held = item.GetHeldEntity() as HeldEntity;
                            
                            if (item.position == weapons[index])
                            {
                            if (heldEntity1 != null)
                            heldEntity1.SetHeld(false);
                            var UID = npcPlayer.inventory.containerBelt.GetSlot(weapons[index]).uid;
                            
                            ChangeWeapon(npcPlayer, held, UID);
                            return;
                            }
                        }
                    }
                }

                if (hasAttacker == true)
                {
                        bData.canChangeWeapon ++;
                        
                        if (bData.canChangeWeapon > 3)
                        {
                        bData.canChangeWeapon = 0;                

                            if (npcPlayer == null)
                            return;
                        
                            float distance = Vector3.Distance(npcPlayer.transform.position, victim.transform.position);
                            int noOfAvailableWeapons = 0;
                            int selectedWeapon;
                            Item chosenWeapon = null;
                            HeldEntity held = null;
                            int newCurrentRange = 0;
                            var currentTime = TOD_Sky.Instance.Cycle.Hour;
                            bool night = false;
                            
                            if (currentTime > 20 || currentTime < 8)
                                night = true;

                            if (npcPlayer.AttackTarget == null && night)
                            {
                                foreach (var weap in bData.MeleeWeapons)
                                {
                                    if (weap.ToString().Contains("flashlight"))
                                    {
                                        chosenWeapon = weap;
                                        newCurrentRange = 1;
                                    }
                                }
                            }
                            else
                            {
                                if (distance < 2f && bData.MeleeWeapons != null)
                                {
                                    foreach (var weap in bData.MeleeWeapons)
                                    {
                                    noOfAvailableWeapons++;
                                    }
                                    if (noOfAvailableWeapons > 0)
                                    {
                                        selectedWeapon = rnd.Next(bData.MeleeWeapons.Count);
                                        chosenWeapon = bData.MeleeWeapons[selectedWeapon];
                                        newCurrentRange = 1;
                                    }
                                }
                                else if (distance > 1f && distance < 10f && bData.CloseRangeWeapons != null)
                                {
                                    foreach (var weap in bData.CloseRangeWeapons)
                                    {
                                    noOfAvailableWeapons++;
                                    }
                                    if (noOfAvailableWeapons > 0)
                                    {
                                        selectedWeapon = rnd.Next(bData.CloseRangeWeapons.Count);
                                        chosenWeapon = bData.CloseRangeWeapons[selectedWeapon];
                                        newCurrentRange = 2;
                                    }
                                }
                                else if (distance > 9f && distance < 30f && bData.MediumRangeWeapons != null)
                                {
                                    foreach (var weap in bData.MediumRangeWeapons)
                                    {
                                        noOfAvailableWeapons++;
                                    }
                                    if (noOfAvailableWeapons > 0)
                                    {
                                        selectedWeapon = rnd.Next(bData.MediumRangeWeapons.Count);
                                        chosenWeapon = bData.MediumRangeWeapons[selectedWeapon];
                                        newCurrentRange = 3;
                                    }
                                }
                                else if (distance > 29 && bData.LongRangeWeapons != null)
                                {
                                    foreach (var weap in bData.LongRangeWeapons)
                                    {
                                    noOfAvailableWeapons++;
                                    }
                                    if (noOfAvailableWeapons > 0)
                                    {
                                        selectedWeapon = rnd.Next(bData.LongRangeWeapons.Count);
                                        chosenWeapon = bData.LongRangeWeapons[selectedWeapon];
                                        newCurrentRange = 4;
                                    }
                                }
                                
                                if (chosenWeapon == null)                                                                                                       //if no weapon suited to range, pick any random bullet weapon
                                {                                                                                                                               //prevents sticking with melee @>2m when no pistol is available
                                    foreach (var weap in bData.AllProjectiles)
                                    {
                                    noOfAvailableWeapons++;
                                    }
                                    if (noOfAvailableWeapons > 0)
                                    {
                                        selectedWeapon = rnd.Next(bData.AllProjectiles.Count);
                                        chosenWeapon = bData.AllProjectiles[selectedWeapon];
                                        newCurrentRange = 5;
                                    }
                                }
                            }     
                            if (chosenWeapon == null) return;
                            
                            if (newCurrentRange == bData.currentWeaponRange)
                            return;
                            else
                            bData.currentWeaponRange = newCurrentRange;
                            held = chosenWeapon.GetHeldEntity() as HeldEntity;
                            if (heldEntity1.name == held.name) return;
   
                            heldEntity1.SetHeld(false);
                            var UID = chosenWeapon.uid;
                            
                            ChangeWeapon(npcPlayer, held, UID);
                        }
                }
                else
                {
                    timer.Once(1, () => SelectWeapon(npcPlayer, victim, false));
                }
        }
        
        void ChangeWeapon(NPCPlayer npcPlayer, HeldEntity held, uint UID)
        {
            npcPlayer.svActiveItemID = 0;
            npcPlayer.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            npcPlayer.inventory.UpdatedVisibleHolsteredItems();
            
            npcPlayer.svActiveItemID = UID;
            npcPlayer.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            held.SetHeld(true);
            npcPlayer.svActiveItemID = UID;
            npcPlayer.inventory.UpdatedVisibleHolsteredItems();
            
            AttackEntity heldGun = npcPlayer.GetHeldEntity() as AttackEntity;
                if (heldGun != null)
                {
                    if (heldGun as BaseMelee != null || heldGun as TorchWeapon != null)
                        heldGun.effectiveRange = 2;
                    else if (held.name.Contains("bolt"))
                        heldGun.effectiveRange = 800f;
                    else
                        heldGun.effectiveRange = 200f;
                    return;      
                }
        }
        #region targeting
        
        object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity entity)
        {
            if (!TempRecord.NPCPlayers.Contains(npcPlayer))
            return null;

            if (npcPlayer == null || entity == null)
            return null;
            BasePlayer victim = null;
            if (entity is BasePlayer)
            {
            victim = entity as BasePlayer; 
            SelectWeapon(npcPlayer, victim, true); 
            var currentTime = TOD_Sky.Instance.Cycle.Hour;
            
            
            var active = npcPlayer.GetActiveItem();
            
            HeldEntity heldEntity1 = null;
            HeldEntity attackerheldEntity1 = null;
            if (active != null)
            heldEntity1 = active.GetHeldEntity() as HeldEntity;
		    
            if (heldEntity1 != null)
            {
                if (currentTime > 20 || currentTime < 8)
                heldEntity1.SetLightsOn(true);
                else
                heldEntity1.SetLightsOn(false);
            }
    
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
                
            if (TempRecord.NPCPlayers.Contains(npcPlayer))
            {
                var bData = npcPlayer.GetComponent<botData>();
                var profile = bData.monumentName;
            }

            if(!victim.userID.IsSteamId() && configData.Options.Ignore_HumanNPC)                                                                                //stops bots targeting humannpc
            return 0f;
            }
            if (entity.name.Contains("agents/") && configData.Options.Ignore_Animals)                                                                           //stops bots targeting animals
            return 0f;
            else
            return null;

        }
        
        object CanBradleyApcTarget(BradleyAPC bradley, BaseEntity target)                                                                                       //stops bradley targeting bots
        {
            if (target is NPCPlayer && configData.Options.APC_Safe)
            return false;
            return null;
        }
        
        object OnNpcTarget(BaseNpc npc, BaseEntity entity)                                                                                                      //stops animals targeting bots
        {                                                                                                                                                       //at present this is not working
            if (entity is NPCPlayer && configData.Options.Animal_Safe)
            return 0f;
            return null;
        }

        object CanBeTargeted(BaseCombatEntity player, MonoBehaviour turret)                                                                                     //stops autoturrets targetting bots
        {
            if (player is NPCPlayer && configData.Options.Turret_Safe)
            return false;
            return null;
        }
        
        #endregion
        void AttackPlayer(BasePlayer player, string name, MonumentSettings profile)
        {
        Vector3 location = (CalculateGroundPos(player.transform.position));
    
            timer.Repeat(1f,profile.Bots, () =>
            {
            profile.LocationX = location.x;
            profile.LocationY = location.y;
            profile.LocationZ = location.z;
            SpawnBots(name, profile, "Attack");
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

        static Vector3 CalculateGroundPos(Vector3 sourcePos)                                                                                                    //credit Wulf & Nogrod 
        {
            RaycastHit hitInfo;

            if (UnityEngine.Physics.Raycast(sourcePos, Vector3.down, out hitInfo, 800f, LayerMask.GetMask("Terrain", "World", "Construction"), QueryTriggerInteraction.Ignore))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        } 
  
        private void FindMonuments() 										                                                                                    //credit K1lly0u 
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
                    if (gobject.name.Contains("mining_quarry_a"))
                    {
                    AddProfile("QuarryA", configData.Zones.QuarryA, pos);
                    continue;
                    }
                    if (gobject.name.Contains("mining_quarry_b"))
                    {
                    AddProfile("QuarryB", configData.Zones.QuarryB, pos);     
                    continue;
                    }
                    if (gobject.name.Contains("mining_quarry_c"))
                    {
                    AddProfile("QuarryC", configData.Zones.QuarryB, pos);    
                    continue;
                    }
                    if (gobject.name.Contains("powerplant_1"))
                    {
                    AddProfile("PowerPlant", configData.Zones.PowerPlant, pos);                 
                    continue;
                    }
 
                    if (gobject.name.Contains("airfield_1"))
                    {
                    AddProfile("Airfield", configData.Zones.Airfield, pos);     
                    continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                    AddProfile("Trainyard", configData.Zones.Trainyard, pos);             
                    continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1")) 
                    {
                    AddProfile("Watertreatment", configData.Zones.Watertreatment, pos);    
                    continue;
                    }

                    if (gobject.name.Contains("satellite_dish")) 
                    {
                    AddProfile("Satellite", configData.Zones.Satellite, pos);  
                    continue;
                    } 

                    if (gobject.name.Contains("sphere_tank"))
                    {
                    AddProfile("Dome", configData.Zones.Dome, pos);
                    continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                    AddProfile("Radtown", configData.Zones.Radtown, pos); 
                    continue;
                    }
                    
                    if (gobject.name.Contains("launch_site"))
                    {
                    AddProfile("Launchsite", configData.Zones.Launchsite, pos);
                    continue;
                    }

                    if (gobject.name.Contains("junkyard"))
                    {
                    AddProfile("Junkyard", configData.Zones.Junkyard, pos);
                    continue;
                    }
		    
                    if (gobject.name.Contains("military_tunnel_1"))
                    {
                    AddProfile("MilitaryTunnel", configData.Zones.MilitaryTunnel, pos);
                    continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    { 
                    AddProfile("Harbor1", configData.Zones.Harbor1, pos);   
                    continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                    AddProfile("Harbor2", configData.Zones.Harbor2, pos);          
                    continue;
                    }
                    
                    if (gobject.name.Contains("gas_station_1") && gasstation == 0)
                    {
                    AddProfile("GasStation", configData.Zones.GasStation, pos);
                    gasstation++;
                    continue;
                    }
              
                    if (gobject.name.Contains("gas_station_1") && gasstation == 1)
                    {
                    AddProfile("GasStation1", configData.Zones.GasStation1, pos);      
                    gasstation++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("supermarket_1") && spermket == 0)
                    {
                    AddProfile("SuperMarket", configData.Zones.SuperMarket, pos);       
                    spermket++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("supermarket_1") && spermket == 1)
                    {
                    AddProfile("SuperMarket1", configData.Zones.SuperMarket1, pos); 
                    spermket++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("lighthouse") && lighthouse == 0)
                    {
                    AddProfile("Lighthouse", configData.Zones.Lighthouse, pos);          
                    lighthouse++;
                    continue;
                    }
    
                    if (gobject.name.Contains("lighthouse") && lighthouse == 1)
                    {                        
                    AddProfile("Lighthouse1", configData.Zones.Lighthouse1, pos);    
                    lighthouse++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("lighthouse") && lighthouse == 2)
                    {
                    AddProfile("Lighthouse2", configData.Zones.Lighthouse2, pos);   
                    lighthouse++;
                    continue;
                    }

                    if (gobject.name.Contains("warehouse") && warehouse == 0)
                    {
                    AddProfile("Warehouse", configData.Zones.Warehouse, pos);             
                    warehouse++;
                    continue;
                    }
    
                    if (gobject.name.Contains("warehouse") && warehouse == 1) 
                    {                        
                    AddProfile("Warehouse1", configData.Zones.Warehouse1, pos); 
                    warehouse++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("warehouse") && warehouse == 2)
                    {
                    AddProfile("Warehouse2", configData.Zones.Warehouse2, pos);           
                    warehouse++;
                    continue;
                    }
		    
                    if (gobject.name.Contains("warehouse") && warehouse > 2)
                    continue;
                    if (gobject.name.Contains("lighthouse") && lighthouse > 2)
                    continue;
                    if (gobject.name.Contains("gas_station_1") && gasstation > 1)
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
                    Bot_Accuracy = configData.Zones.AirDrop.Bot_Accuracy,
                    Bot_Damage = configData.Zones.AirDrop.Bot_Damage,
                    Disable_Radio = configData.Zones.AirDrop.Disable_Radio,
                    LocationX = 0f,
                    LocationY = 0f,
                    LocationZ = 0f,
                    Respawn_Timer = 10,
                    Aggression_Range = configData.Zones.AirDrop.Aggression_Range,
		    Roam_Range = configData.Zones.AirDrop.Roam_Range,
		    Weapon_Drop = configData.Zones.AirDrop.Weapon_Drop,
		    Keep_Default_Loadout = configData.Zones.AirDrop.Keep_Default_Loadout,
                });        
            
            foreach (var profile in storedData.CustomProfiles)
            TempRecord.MonumentProfiles.Add(profile.Key, profile.Value);

            foreach (var profile in TempRecord.MonumentProfiles)
            {
                if (profile.Value.Kit == null && Kits == null)
                {
                    PrintWarning(lang.GetMessage("nokits", this));
                    return;
                }
            if(profile.Value.Activate == true && profile.Value.Bots > 0 && !profile.Key.Contains("AirDrop"))
            timer.Repeat(2,profile.Value.Bots, () => SpawnBots(profile.Key, profile.Value, null));
            }
        }
	
	
        void AddProfile(string name, CustomSettings monument, Vector3 pos)                                                                                      //bring config data into live data
        {
            TempRecord.MonumentProfiles.Add(name, new MonumentSettings
            {
            Activate = monument.Activate,
            Murderer = monument.Murderer,
            Bots = monument.Bots,
            BotHealth = monument.BotHealth,
            Radius = monument.Radius,
            Kit = monument.Kit,
            BotName = monument.BotName,
            Bot_Accuracy = monument.Bot_Accuracy, 
            Bot_Damage = monument.Bot_Damage,
            Disable_Radio = monument.Disable_Radio,
            Respawn_Timer = monument.Respawn_Timer,
            LocationX = pos.x,
            LocationY = pos.y,
            LocationZ = pos.z,
            Aggression_Range = monument.Aggression_Range,
	    Roam_Range = monument.Roam_Range,
            Weapon_Drop = monument.Weapon_Drop,
            Keep_Default_Loadout = monument.Keep_Default_Loadout,
            });  
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
        
        [ConsoleCommand("botspawn.reset")]                                                                                                                      //debug precaution - Kill all bots, whether from this plug or not.
        void cmdDefaultBots()
        {
            var allofem = UnityEngine.Object.FindObjectsOfType<botData>();
            foreach (var gobject in allofem)
            {
                 UnityEngine.Object.Destroy(gobject);
            }
            var allobjects = UnityEngine.Object.FindObjectsOfType<NPCPlayer>();
            foreach (var gobject in allobjects)
            {
                 gobject.Kill();
            }
                 Unload();
                 Init();
                 OnServerInitialized();
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
                    Vector3 pos = player.transform.position;

                    var customSettings = new MonumentSettings()
                    {
                        Activate = false,
                        BotName = "randomname",
                        LocationX = pos.x,
                        LocationY = pos.y,
                        LocationZ = pos.z,
                    };
                    
                    storedData.CustomProfiles.Add(name, customSettings);
                    Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData);
                    SendReply(player, "<color=orange>" + lang.GetMessage("Title", this) + "</color>" + lang.GetMessage("customsaved", this),player.transform.position);
                }
                
                else if (args[0] == "move")
                {
                    var name = args[1];
                    if (storedData.CustomProfiles.ContainsKey(name))
                    {
                        storedData.CustomProfiles[name].LocationX = player.transform.position.x;
                        storedData.CustomProfiles[name].LocationY = player.transform.position.y;
                        storedData.CustomProfiles[name].LocationZ = player.transform.position.z;
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
                            if (bot == null)
                                return;
                            var bData = bot.GetComponent<botData>();
                            if (bData.monumentName == name)
                            if (bData.transform.parent.gameObject != null)
                            bData.transform.parent.gameObject.GetComponent<BasePlayer>().Kill();
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
            public static List<NPCPlayerApex> NPCPlayers = new List<NPCPlayerApex>();
            public static Dictionary<string, MonumentSettings> MonumentProfiles = new Dictionary<string, MonumentSettings>();
            public static List<ulong> DeadNPCPlayerIds = new List<ulong>();
            public static Dictionary<ulong, string> kitList = new Dictionary<ulong, string>();
        }
        public class botData : MonoBehaviour
        {
            public Vector3 spawnPoint;
            public bool invincible;
            public int canChangeWeapon;
            public float enemyDistance;
            public int currentWeaponRange;
            public List<Item> AllProjectiles = new List<Item>();
            public List<Item> MeleeWeapons = new List<Item>();
            public List<Item> CloseRangeWeapons = new List<Item>();
            public List<Item> MediumRangeWeapons = new List<Item>();
            public List<Item> LongRangeWeapons = new List<Item>();
            public int accuracy;
            public float damage;
            public float range;
            public int health;
            public string monumentName;
            public bool dropweapon;
            public bool respawn;
            public int roamRange;
            public bool goingHome;
            public bool keepAttire;
            
            NPCPlayerApex botapex;
                void Start()
                {
                    botapex = this.GetComponent<NPCPlayerApex>();
                }
                void Update()
                {
                    if (botapex.AttackTarget == null && (Vector3.Distance(botapex.transform.position, botapex.GuardPosition) > roamRange))
                    {
                        goingHome = true;
                    }
                    if (Vector3.Distance(botapex.transform.position, botapex.GuardPosition) > (10) && goingHome == true)
                    {
                        if (botapex.GetNavAgent.isOnNavMesh)
                        botapex.GetNavAgent.SetDestination(botapex.GuardPosition);
                    }
                    else
                    goingHome = false;
                }
        }
        class CustomSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public List<string> Kit = new List<string>();
            public string BotName = "randomname";
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;  
            public int Respawn_Timer = 60;
            public bool Disable_Radio = true;
            public int Aggression_Range = 40;
            public int Roam_Range = 40;
            public bool Weapon_Drop = true;
            public bool Keep_Default_Loadout = false;
        }
        class MonumentSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public List<string> Kit = new List<string>();
            public string BotName = "randomname";
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;     
            public int Respawn_Timer = 60;
            public bool Disable_Radio = true;
            public float LocationX;
            public float LocationY;
            public float LocationZ;
            public int Aggression_Range = 40;
            public int Roam_Range = 40;
            public bool Weapon_Drop = true;
            public bool Keep_Default_Loadout = false;
        }
        class AirDropSettings
        {
            public bool Activate = false;
            public bool Murderer = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public List<string> Kit = new List<string>();
            public string BotName = "randomname";
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;
            public bool Disable_Radio = true;
            public int Aggression_Range = 40;
            public int Roam_Range = 40;
            public bool Weapon_Drop = true;
            public bool Keep_Default_Loadout = false;
	    
        }
        class Options
        {
            public bool Ignore_Animals { get; set; }
            public bool APC_Safe { get; set; }
            public bool Turret_Safe { get; set; }
            public bool Animal_Safe { get; set; }
            public int Suicide_Timer { get; set; }
            public bool Supply_Enabled { get; set; }
            public bool Cull_Default_Population { get; set; }
            public bool Remove_BackPacks { get; set; }
            public bool Ignore_HumanNPC { get; set; }
            public bool Peace_Keeper { get; set; }
            public bool Allow_Rust_Loot { get; set; }
            public bool Wipe_Belt { get; set; }
            public bool Wipe_Clothing { get; set; }
        }
        class Zones
        {
            public CustomSettings Airfield { get; set; }
            public CustomSettings Dome { get; set; }
            public CustomSettings PowerPlant { get; set; }
            public CustomSettings Radtown { get; set; }
            public CustomSettings Satellite { get; set; }
            public CustomSettings Trainyard { get; set; }
            public CustomSettings Watertreatment { get; set; }
            public CustomSettings Launchsite { get; set; }
            public CustomSettings MilitaryTunnel { get; set; }
            public CustomSettings Junkyard { get; set; }
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
            public CustomSettings QuarryA { get; set; }
            public CustomSettings QuarryB { get; set; }
            public CustomSettings QuarryC { get; set; }
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
                    Allow_Rust_Loot = true,
                    Wipe_Belt = true,
                    Wipe_Clothing = true,
                    Ignore_Animals = true,
                    APC_Safe = true,
                    Turret_Safe = true,
                    Animal_Safe = true,
                    Suicide_Timer = 300,
                    Supply_Enabled = false,
                    Cull_Default_Population = true,
                    Remove_BackPacks = true,
                    Ignore_HumanNPC = true,
                    Peace_Keeper = true,
               },
               Zones = new Zones
               {
                    Airfield = new CustomSettings{},
                    Dome = new CustomSettings{},
                    PowerPlant = new CustomSettings{},
                    Radtown = new CustomSettings{},
                    Satellite = new CustomSettings{},
                    Trainyard = new CustomSettings{},
                    Watertreatment = new CustomSettings{},
                    Launchsite = new CustomSettings{},
                    MilitaryTunnel = new CustomSettings{},
                    Junkyard = new CustomSettings{},
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
                    QuarryA = new CustomSettings{},
                    QuarryB = new CustomSettings{},
                    QuarryC = new CustomSettings{},
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