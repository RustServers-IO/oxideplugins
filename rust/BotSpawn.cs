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
using Oxide.Game.Rust;
using Rust.Ai;
using System.Globalization;



namespace Oxide.Plugins

{
    [Info("BotSpawn", "Steenamaroo", "1.1.7", ResourceId = 2580)] //vanishfix

    [Description("Spawn Bots with kits at monuments.")]
    
    class BotSpawn : RustPlugin

    {
        [PluginReference]
        Plugin Vanish, Kits;
     
        const string permAllowed = "botspawn.allowed";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
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
        
        [ChatCommand("botspawn")]
        void botspawn(BasePlayer player, string command, string[] args)
        {
            if (HasPermission(player.UserIDString, permAllowed) || isAuth(player))
            if (args != null && args.Length == 1)
            {
                if (args[0] == "list")
                {
                var outMsg = "<color=orange>Custom Locations\n</color>";                   
                        
                foreach (var profile in storedData.CustomProfiles)
                {outMsg += $"{profile.Key}\n";}
                
                PrintToChat(player, outMsg);
                }
                else
                PrintToChat(player, "/botspawn commands are - list - add - remove - toplayer");
            }
            else if (args != null && args.Length == 2)
            {
                if (args[0] == "add")
                {
                    var name = args[1];
                    if (storedData.CustomProfiles.ContainsKey(name))
                    {
                        PrintToChat(player, $"Custom Location already exists with this name.");
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
                    PrintToChat(player, $"Custom Location Saved @ {player.transform.position}");
                    //timer.Repeat(2,storedData.CustomProfiles[name].Bots, () => SpawnSci(name, customSettings, null));
                }
                else if (args[0] == "remove")
                {
                    var name = args[1];
                    if (storedData.CustomProfiles.ContainsKey(name))
                    {
                        foreach (var bot in TempRecord.NPCPlayers)
                        {
                            if (bot.Value.monumentName == name)
                            try
                            {bot.Value.bot.Kill();}
                            catch{continue;}
                        }
                        TempRecord.MonumentProfiles.Remove(name);
                        storedData.CustomProfiles.Remove(name);
                        Interface.Oxide.DataFileSystem.WriteObject("BotSpawn", storedData);
                        PrintToChat(player, $"Custom Location Removed - {name}");
                    }
                }
                else
                PrintToChat(player, "/botspawn commands are - list - add - remove - toplayer");
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
                    PrintToChat(player, "Player was not found");
                    return;                        
                    }

                        if (!(storedData.CustomProfiles.ContainsKey(profile)))
                        {
                        PrintToChat(player, "There is no profile by that name in /data/BotSpawn.json");
                        return;
                        }
                            foreach (var entry in storedData.CustomProfiles)
                            {
                                if (entry.Key == profile)
                                {
                                AttackPlayer(player, entry.Key, entry.Value);
                                PrintToChat(player, $"'{profile}' bots deployed to {target.displayName}.");
                                }
                            }
  
                }
                else
                PrintToChat(player, "/botspawn commands are - list - add - remove - toplayer");
            }
            else
            PrintToChat(player, "/botspawn commands are - list - add - remove - toplayer"); 
        }
              
        int no_of_AI = 0;
        System.Random rnd = new System.Random();
        static System.Random random = new System.Random();
        
        public double GetRandomNumber(double minimum, double maximum)
        { 
            return random.NextDouble() * (maximum - minimum) + minimum;
        }
        
        void Init()
        {
        var filter = RustExtension.Filter.ToList(); // Thanks Fuji. :)
        filter.Add("cover points");
        filter.Add("resulted in a conflict");
        RustExtension.Filter = filter.ToArray();
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
                bot.Key.Kill();
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
                        timer.Once(configData.Options.Respawn_Timer, () => SpawnSci(profile.Key, profile.Value, null));
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
        public static string Get(ulong v)
        {
            return Facepunch.RandomUsernames.Get((int)(v % 2147483647uL));
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
        
        void SpawnSci(string name, MonumentSettings settings, string type = null)
        {
            var pos = settings.Location;
            var zone = settings;
            if (no_of_AI == configData.Options.Upper_Bot_Limit)
            return;
            else
            {
                int X = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                int Z = rnd.Next((-zone.Radius/2), (zone.Radius/2));
                int dropX = rnd.Next(5, 10);
                int dropZ = rnd.Next(5, 10);
                int Y = rnd.Next(zone.Spawn_Height, (zone.Spawn_Height + 50));
                var CentrePos = new Vector3((pos.x + X),200,(pos.z + Z));    
                Quaternion rot = Quaternion.Euler(0, 0, 0);
                Vector3 newPos = (CalculateGroundPos(CentrePos));
                if (zone.Chute)newPos =  newPos + new Vector3(0,Y,0);
                if (type == "AirDrop" && zone.Chute) newPos = new Vector3((pos.x + dropX),pos.y,(pos.z + dropZ));
                NPCPlayer entity = GameManager.server.CreateEntity("assets/prefabs/npc/scientist/scientist.prefab", newPos, rot, true) as NPCPlayer;
                entity.Spawn();
                no_of_AI++;

                if (zone.Kit != "default")
                {
                    entity.inventory.Strip(); 
                    Kits?.Call($"GiveKit", entity, zone.Kit);
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
                var botapex = entity.GetComponent<NPCPlayerApex>();
                if (zone.Chute)
                {
                var Chute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", newPos, rot);
                Chute.gameObject.Identity();
                Chute.SetParent(botapex, "paraChute_attach");
                Chute.Spawn();
                float x = Convert.ToSingle(GetRandomNumber(-0.16, 0.16));
                float z = Convert.ToSingle(GetRandomNumber(-0.16, 0.16));
                float varySpeed = Convert.ToSingle(GetRandomNumber(0.4, 0.8));
                Drop(botapex, Chute, zone, x, varySpeed, z);
                }
                TempRecord.NPCPlayers.Add(botapex, new botData()
                {
                    spawnPoint = newPos,
                    accuracy = zone.Bot_Accuracy,
                    damage = zone.Bot_Damage,
                    botID = entity.userID,
                    bot = entity,
                    monumentName = name,
                });
                 if(zone.Chute && configData.Options.Ai_Falling_Disable)timer.Once(1, () =>botapex.Pause());
                   timer.Once(6, () =>       //find hook on weapon drawn????
                    {
                        AttackEntity heldEntity = botapex?.GetHeldEntity() as AttackEntity;
                        if (heldEntity != null)
                        {
                        heldEntity.effectiveRange = zone.Bot_Firing_Range;
                        }
                    });

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
        }

        void Drop(NPCPlayer bot, BaseEntity Chute, MonumentSettings zone, float x, float varyY, float z)
        {
            var gnd = (CalculateGroundPos(bot.transform.position));
            var botapex = bot.GetComponent<NPCPlayerApex>();
            if (bot.transform.position.y > gnd.y)
            {
                float logSpeed = ((bot.transform.position.y / 150f) + varyY);
                bot.transform.position = bot.transform.position + new Vector3(x, -logSpeed, z);
                timer.Once(0.2f, () =>
                {
                    if (TempRecord.NPCPlayers.ContainsKey(botapex))
                    {
                    Drop(bot, Chute, zone, x, varyY, z);
                        if (zone.Invincible_In_Air)
                        bot.health = zone.BotHealth;
                    }
                });
            }
            else
            {
                botapex.Resume();
                bot.RemoveChild(Chute);
                Chute.Kill();
            }
        }
		void OnEntitySpawned(BaseEntity entity)
		{
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
                try
                {
                TempRecord.MonumentProfiles.Add("AirDrop", configData.Zones.AirDrop);
                }
                catch{}

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
        

	int lastFrame = Time.frameCount;
	
	void OnTick()
	{
	    if (Time.frameCount - lastFrame > 10)
		DoAction();
	}
	
	void DoAction()
	{
        foreach(var bot in TempRecord.NPCPlayers)
        {
            if (bot.Key.AttackTarget != null)
            {
                if (bot.Key.AttackTarget.name.Contains("agents/") && configData.Options.Ignore_Animals) /////////////////////////not exactly economical - I know.
                {
                bot.Key.AttackTarget = null;  
                }
                    if (bot.Key.AttackTarget != null && bot.Key.AttackTarget is BasePlayer)
                    {
                        var canNetwork = Vanish?.Call("IsInvisible", bot.Key.AttackTarget);
                            if ((canNetwork is bool))
                            if ((bool)canNetwork)
                            {
                                bot.Key.AttackTarget = null;  
                            }
                        }
                    }
        }
	    lastFrame = Time.frameCount;
	}

        static Vector3 CalculateGroundPos(Vector3 sourcePos) // credit Wulf & Nogrod 
        {
            RaycastHit hitInfo;

            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, 800f, LayerMask.GetMask("Terrain", "World", "Construction"), QueryTriggerInteraction.Ignore))
            {
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        } 
  
        object CanBeTargeted(BaseCombatEntity player, MonoBehaviour turret)//stops autoturrets targetting bots
        {
            if (player is NPCPlayer && configData.Options.Turret_Safe)
            return false;
            return null;
        }

        object CanNpcAttack(BaseNpc npc, BaseEntity target) //nulls animal damage to bots
        {
            if (target is NPCPlayer && configData.Options.Animal_Safe)
            return true;
            return null;
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
                        TempRecord.MonumentProfiles.Add("PowerPlant", configData.Zones.Powerplant);
                        TempRecord.MonumentProfiles["PowerPlant"].Location = pos;                
                    continue;
                    }
 
                    if (gobject.name.Contains("airfield_1"))
                    {
                        TempRecord.MonumentProfiles.Add("Airfield", configData.Zones.Airfield);
                        TempRecord.MonumentProfiles["Airfield"].Location = pos;         
                    continue;
                    }

                    if (gobject.name.Contains("trainyard_1"))
                    {
                        TempRecord.MonumentProfiles.Add("Trainyard", configData.Zones.Trainyard);
                        TempRecord.MonumentProfiles["Trainyard"].Location = pos;            
                    continue;
                    }

                    if (gobject.name.Contains("water_treatment_plant_1")) 
                    {
                        TempRecord.MonumentProfiles.Add("Watertreatment", configData.Zones.Watertreatment);
                        TempRecord.MonumentProfiles["Watertreatment"].Location = pos;       
                    continue;
                    }

                    if (gobject.name.Contains("satellite_dish")) 
                    {
                        TempRecord.MonumentProfiles.Add("Satellite", configData.Zones.Satellite);
                        TempRecord.MonumentProfiles["Satellite"].Location = pos;   
                    continue;
                    } 

                    if (gobject.name.Contains("sphere_tank"))
                    {
                        TempRecord.MonumentProfiles.Add("Dome", configData.Zones.Dome);
                        TempRecord.MonumentProfiles["Dome"].Location = pos;   
                    continue;
                    }

                    if (gobject.name.Contains("radtown_small_3"))
                    {
                        TempRecord.MonumentProfiles.Add("Radtown", configData.Zones.Radtown);
                        TempRecord.MonumentProfiles["Radtown"].Location = pos;       
                    continue;
                    }
                    
                    if (gobject.name.Contains("launch_site"))
                    {
                        TempRecord.MonumentProfiles.Add("Launchsite", configData.Zones.Launchsite);
                        TempRecord.MonumentProfiles["Launchsite"].Location = pos; 
                    continue;
                    }

                    if (gobject.name.Contains("military_tunnel_1"))
                    {

                        TempRecord.MonumentProfiles.Add("MilitaryTunnel", configData.Zones.MilitaryTunnel);
                        TempRecord.MonumentProfiles["MilitaryTunnel"].Location = pos; 
                    continue;
                    }

                    if (gobject.name.Contains("harbor_1"))
                    {
                        TempRecord.MonumentProfiles.Add("Harbor1", configData.Zones.Harbor1);
                        TempRecord.MonumentProfiles["Harbor1"].Location = pos;        
                    continue;
                    }

                    if (gobject.name.Contains("harbor_2"))
                    {
                        TempRecord.MonumentProfiles.Add("Harbor2", configData.Zones.Harbor2);
                        TempRecord.MonumentProfiles["Harbor2"].Location = pos;         
                    continue;
                    }

                    if (gobject.name.Contains("warehouse") && warehouse == 0)
                    {
                        TempRecord.MonumentProfiles.Add("Warehouse", configData.Zones.Warehouse);
                        TempRecord.MonumentProfiles["Warehouse"].Location = pos;
                    warehouse++;
                    continue;
                    }
    
                    if (gobject.name.Contains("warehouse") && warehouse == 1)
                    {                        
                        TempRecord.MonumentProfiles.Add("Warehouse1", configData.Zones.Warehouse1);
                        TempRecord.MonumentProfiles["Warehouse1"].Location = pos;
                    warehouse++;
                    continue;
                    }
                    
                    if (gobject.name.Contains("warehouse") && warehouse == 2)
                    {
                        TempRecord.MonumentProfiles.Add("Warehouse2", configData.Zones.Warehouse2);
                        TempRecord.MonumentProfiles["Warehouse2"].Location = pos;
                    warehouse++;
                    continue;
                    } 
                }
            }
            
            foreach (var profile in storedData.CustomProfiles)
            {
            TempRecord.MonumentProfiles.Add(profile.Key, profile.Value);
            }
            
            foreach (var profile in TempRecord.MonumentProfiles)
            {
            if(profile.Value.Activate == true && profile.Value.Bots > 0)
            timer.Repeat(2,profile.Value.Bots, () => SpawnSci(profile.Key, profile.Value, null));
            }
        }
        #region Console Commands - mostly for debug
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
        #endregion
        
        #region Config
        private ConfigData configData;
        
        class TempRecord
        {
            public static Dictionary<NPCPlayerApex, botData> NPCPlayers = new Dictionary<NPCPlayerApex, botData>();
            public static Dictionary<string, MonumentSettings> MonumentProfiles = new Dictionary<string, MonumentSettings>();
        }
        class botData
        {
            public Vector3 spawnPoint;
            public int accuracy;
            public float damage;
            public ulong botID;
            public BasePlayer bot;
            public string monumentName;
        }
        class MonumentSettings
        {
            public bool Activate = false;
            public int Bots = 5;
            public int BotHealth = 100;
            public int Radius = 100;
            public string Kit = "default";
            public string BotName = "randomname";
            public int BotRadius = 100;
            public bool Chute = false;
            public int Bot_Firing_Range = 20;
            public int Bot_Accuracy = 4;
            public float Bot_Damage = 0.4f;     
            public bool Invincible_In_Air = true;
            public int Spawn_Height = 100;      
            public Vector3 Location;
        }
        class Options
        {        
            public int Upper_Bot_Limit { get; set; }
            public int Respawn_Timer { get; set; }
            public bool Ignore_Animals { get; set; }
            public bool Reset { get; set; }
            public int Reset_Timer { get; set; }
            public bool Turret_Safe { get; set; }
            public bool Animal_Safe { get; set; }
            public int Suicide_Timer { get; set; }
            public bool Ai_Falling_Disable { get; set; }
            public bool Bots_Drop_Weapons { get; set; }
            public bool Supply_Enabled { get; set; }
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
            public MonumentSettings AirDrop { get; set; }
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
                    Upper_Bot_Limit = 120,
                    Respawn_Timer = 60,
                    Ignore_Animals = true,
                    Reset = true,
                    Turret_Safe = true,
                    Animal_Safe = true,
                    Suicide_Timer = 300,
                    Reset_Timer = 300,
                    Ai_Falling_Disable = true,
                    Bots_Drop_Weapons = true,
                    Supply_Enabled = true,
               },
               Zones = new Zones
               {
                   Airfield = new MonumentSettings{},
                   Dome = new MonumentSettings{},
                   Powerplant = new MonumentSettings{},
                   Radtown = new MonumentSettings{},
                   Satellite = new MonumentSettings{},
                   Trainyard = new MonumentSettings{},
                   Watertreatment = new MonumentSettings{},
                   Launchsite = new MonumentSettings{},
                   MilitaryTunnel = new MonumentSettings{},
                   Harbor1 = new MonumentSettings{},
                   Harbor2 = new MonumentSettings{},
                   Warehouse = new MonumentSettings{},
                   Warehouse1 = new MonumentSettings{},
                   Warehouse2 = new MonumentSettings{},
                   AirDrop = new MonumentSettings{
                    Activate = false,
                    Bots = 10,
                    BotHealth = 150,
                    Radius = 100,
                    Kit = "default",
                    BotName = "randomname",
                    BotRadius = 100,
                    Chute = true,
                    Bot_Firing_Range = 20,
                    Bot_Accuracy = 5,
                    Bot_Damage = 0.5f,  
                    Invincible_In_Air = false,
                    Spawn_Height = 100,    
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