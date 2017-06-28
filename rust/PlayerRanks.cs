// Reference: Oxide.Core.MySql
using System.Collections.Generic;
using System;
using System.Text;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Core.Database; 
using Oxide.Core.Configuration;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins 
{
    [Info("PlayerRanks", "Steenamaroo", "1.2.0", ResourceId = 2359)]
    class PlayerRanks : RustPlugin
    {                                                              
        [PluginReference]
        Plugin Clans, Friends, EventManager, PlaytimeTracker, Economics;

        private Dictionary<uint, Dictionary<ulong, int>> HeliAttackers = new Dictionary<uint, Dictionary<ulong, int>>();
        private Dictionary<ulong, WoundedData> woundedData = new Dictionary<ulong, WoundedData>();      
        
        const string permAllowed = "playerranks.allowed";
        bool HasPermission(string id, string perm) => permission.UserHasPermission(id, perm);
        
        class DataStorage
        {
            public Dictionary<ulong, PRDATA> PlayerRankData = new Dictionary<ulong, PRDATA>();  
            public DataStorage() { }
        }
            
        class PRDATA 
        {
            public bool Admin;
            public ulong UserID;
            public string Name;
            public string TimePlayed = "0";
            public string Status = "offline";
            public int Economics = 0;
            public int Recent = 0;
            public int PVPKills = 0;
            public double PVPDistance = 0.0;
            public int PVEKills = 0;
            public double PVEDistance = 0.0;	    
            public int Deaths = 0;
            public int BarrelsDestroyed = 0;
            public int HeliHits = 0;
            public int HeliKills = 0;
            public int Suicides = 0;
            public int TimesWounded = 0;
            public int ExplosivesThrown = 0;
            public int ArrowsFired = 0;
            public int BulletsFired = 0;
            public int TurretsDestroyed = 0;        
            public int SleepersKilled = 0;            
            public int NPCsKilled = 0;
            public int RocketsLaunched = 0;
            public int TimesHealed = 0;
            public double KDR = 0.0;
            public double SDR = 0.0;
            
            //intense options
            public int StructuresBuilt = 0;
            public int ItemsDeployed = 0;
            public int ItemsCrafted = 0;
            public int EntitiesRepaired = 0;
            public int StructuresDemolished = 0; 
            public int ResourcesGathered = 0;
            public int StructuresUpgraded = 0; 
        }

        class WoundedData
        {
        public float distance;
        public ulong attackerId;
        }
        
        DataStorage data;
        private DynamicConfigFile PRData;

        void OnServerInitialized()
        {
            lang.RegisterMessages(messages, this);
            permission.RegisterPermission(permAllowed, this);
            PRData = Interface.Oxide.DataFileSystem.GetFile("PlayerRanks");
            LoadData();
            LoadVariables();
            cmd.AddChatCommand($"{chatCommandAlias}", this, "cmdTarget");
            CheckDependencies();
            if (useTimedTopList)
                {
                    timer.Once(TimedTopListTimer * 60, () => pvpkills());
                }
            foreach(var entry in data.PlayerRankData) 
            {
                entry.Value.Status = "offline";
            }
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerInit(player);
            }
            timer.Every(saveTimer * 60, () =>
            {
            SaveData();
            Puts("Player Ranks Local Database Was Saved.");
            if (useMySQL)
            {
                LoadMySQL(); 
            }
            }
            );//publicly reports saving on the timer
        }
     
        private void CheckDependencies()
        {
            if (Friends == null)
            {
                if (useFriendsAPI)
                {
                    PrintWarning($"FriendsAPI could not be found! Disabling friends feature");
                    useFriendsAPI = false; 
                }
            }
            if (Clans == null)
            {
                if (useClans)
                {
                    PrintWarning($"Clans could not be found! Disabling clans feature");
                    useClans = false;
                }
            }
            if (PlaytimeTracker == null)
            {
                    PrintWarning($"PlayTime Tracker is not installed. Please install it and reload.");
            }
            if (Economics == null)
            {
                    PrintWarning($"Economics is not installed. Category will show 0 for all players.");
            }
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            Config.Clear();
            LoadVariables();            
        }
 
        void OnPlayerInit(BasePlayer player)
        {
            topsOpen = false;
            int maxNum = 0;
            if (data.PlayerRankData.Count != 0)
            {
                maxNum = data.PlayerRankData.Max(recent =>
                {
                    if (recent.Value != null) return recent.Value.Recent;
                    return 0;
                });
            }
            if (!data.PlayerRankData.ContainsKey(player.userID))
            {
                
                maxNum++;
                
                data.PlayerRankData.Add(player.userID, new PRDATA()
                {
                    Admin = false,
                    UserID = player.userID,
                    Name = player.displayName,
                    TimePlayed = "0",
                    Status = "online",
                    Recent = maxNum,
                    Economics = 0,
                    PVPKills = 0,
                    PVPDistance = 0.0,
                    PVEKills = 0,
                    PVEDistance = 0.0,
                    Deaths = 0,
                    BarrelsDestroyed = 0,
                    HeliHits = 0,
                    HeliKills = 0,
                    Suicides = 0,
                    TimesWounded = 0,
                    TimesHealed = 0,
                    ArrowsFired = 0,
                    BulletsFired = 0,
                    TurretsDestroyed = 0,
                    SleepersKilled = 0,            
                    NPCsKilled = 0,
                    RocketsLaunched = 0,
                    ExplosivesThrown = 0,
                    KDR = 0,
                    SDR = 0,
                    
                    //intense options
                    StructuresBuilt = 0,
                    ItemsDeployed = 0,
                    ItemsCrafted = 0,
                    EntitiesRepaired = 0,
                    StructuresDemolished = 0,
                    ResourcesGathered = 0,
                    StructuresUpgraded = 0,
                });
            }
            else
            {
                data.PlayerRankData[player.userID].Name = player.displayName;
                data.PlayerRankData[player.userID].Status = "online";
                
                if (Economics)
                {
                data.PlayerRankData[player.userID].Economics = Convert.ToInt32(Economics?.CallHook("GetPlayerMoney", player.userID));
                }
                else
                {
                    data.PlayerRankData[player.userID].Economics = 0;
                }
                
                maxNum++;
                data.PlayerRankData[player.userID].Recent = maxNum;

            
            

            }
            if (isAuth(player))
            {
            data.PlayerRankData[player.userID].Admin = true;
            }
        }

        private string GetPlaytimeClock(double time)
        {
            TimeSpan dateDifference = TimeSpan.FromSeconds((float)time);
            var days = dateDifference.Days;
            var hours = dateDifference.Hours;
            hours += (days * 24);
            var mins = dateDifference.Minutes;
            var secs = dateDifference.Seconds;
            return string.Format("{0:00}:{1:00}:{2:00}", hours, mins, secs); //credit K1lly0u
        }
        
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo, HitInfo info)
        {
            var victim = entity as BasePlayer;
            if (hitinfo?.Initiator == null && entity is BasePlayer)
            {
                    if (woundedData.ContainsKey(victim.userID))
                    {
                        BasePlayer attacker = BasePlayer.FindByID(woundedData[victim.userID].attackerId);
                        var distance = woundedData[victim.userID].distance;
                        if (!victim.userID.IsSteamId())
                        {
                            if (attacker != null)
                                {
                                if (data.PlayerRankData.ContainsKey(attacker.userID))
                                        data.PlayerRankData[attacker.userID].NPCsKilled++;
                                    if (distance > data.PlayerRankData[attacker.userID].PVEDistance)
                                        data.PlayerRankData[attacker.userID].PVEDistance = Math.Round(distance, 2);
                                }
                                woundedData.Remove(victim.userID);
                                return;
                        }
            
                        if (victim.userID.IsSteamId())
                        {
                            ProcessDeath((BasePlayer)victim);
                            if (attacker != null)
                                {
                                    if (data.PlayerRankData.ContainsKey(attacker.userID))
                                        data.PlayerRankData[attacker.userID].PVPKills++;
                                    if (distance > data.PlayerRankData[attacker.userID].PVPDistance)
                                        data.PlayerRankData[attacker.userID].PVPDistance = Math.Round(distance, 2);
                                }
                            woundedData.Remove(victim.userID);
                            return;
                        }
            
                    }
                        String [] stringArray = {"Cold", "Drowned", "Heat", "Suicide", "Generic", "Posion", "Radiation", "Thirst", "Hunger", "Fall"};
                        if (stringArray.Any(victim.lastDamage.ToString().Contains))
                            {
                                ProcessDeath((BasePlayer)victim);
                                ProcessSuicide((BasePlayer)victim);
                                return;
                            }
                            else
                            {
                                ProcessDeath((BasePlayer)victim);
                            }
                            return;
            }
                                     
            if (entity is BaseHelicopter)  
                {
                    BasePlayer player = null;
                    player = BasePlayer.FindByID(GetMajorityAttacker(entity.net.ID));
                    ProcessHeliKills((BasePlayer)player);
                    HeliAttackers.Remove(entity.net.ID);
                    return;
                }       

            if (hitinfo?.Initiator is BasePlayer)
            {
                var attacker = hitinfo.Initiator as BasePlayer;           
                if (entity.name.Contains("agents/"))
                    {
                        if (attacker.userID.IsSteamId())
                        {
                            ProcessPVEKills((BasePlayer)hitinfo.Initiator, (BaseEntity)entity);
                            return;    
                        }
                        return;
                    }
            
                if (entity.name.Contains("barrel") && attacker.userID.IsSteamId())	    
                    {
                        ProcessBarrelsDestroyed((BasePlayer)hitinfo.Initiator, (BaseEntity)entity);
                        return;
                    }
                if (entity.name.Contains("turret") && attacker.userID.IsSteamId())  
                    {                                                                                 
                        var player = hitinfo.Initiator.ToPlayer();
                        ProcessTurretsDestroyed((BasePlayer)player);
                        return;
                    }
                if (!attacker.userID.IsSteamId() && entity is BasePlayer && victim.userID.IsSteamId())
                    {
                        ProcessDeath((BasePlayer)entity);
                        return;
                    }
                if (victim is BasePlayer && !victim.userID.IsSteamId() && attacker.userID.IsSteamId())
                    {
                        ProcessNPCsKilled((BasePlayer)hitinfo.Initiator, (BaseEntity)entity);
                        return;
                    } 
                if (entity is BasePlayer && victim.userID.IsSteamId())
                {
                    ProcessDeath((BasePlayer)entity);
                    
                    if (hitinfo.Initiator != entity)
                        ProcessKill((BasePlayer)hitinfo.Initiator, (BasePlayer)entity);
                    
                    if (victim.IsSleeping())
                        ProcessSleepersKilled((BasePlayer)attacker, (BasePlayer)victim);

                    if (hitinfo.Initiator == entity)
                        ProcessSuicide((BasePlayer)hitinfo.Initiator);
                        return;
                }
            }
            if (victim is BasePlayer && victim.userID.IsSteamId())
            {
                ProcessDeath((BasePlayer)victim);
                return;
            }
        }
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, Item item)
        {
            if (player.userID.IsSteamId() && player is BasePlayer)
                {
                    if (!(player.GetActiveItem().info.displayName.english == "Supply Signal")) 
                    {
                        ProcessExplosivesThrown((BasePlayer)player);
                    }
                } 
        }
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod)
        {
            if (mod.ToString().Contains("arrow"))
                ProcessArrowsFired((BasePlayer)player);

            if (mod.ToString().Contains("ammo"))
                ProcessBulletsFired((BasePlayer)player);
        }
        
        void OnRocketLaunched(BasePlayer player)
        {
            ProcessRocketsLaunched((BasePlayer)player);
        }
        
        void OnEntityBuilt(Planner plan, GameObject objectBlock)
             {
                if (useIntenseOptions)
                    {
                        BasePlayer player = plan.GetOwnerPlayer();
                        if (player.GetActiveItem().info.displayName.english == "Building Plan")
                            {
                                ProcessStructuresBuilt((BasePlayer)player);
                                return;
                            }
                            ProcessItemsDeployed((BasePlayer)player);
                            return;
                    }
             }
             
        void OnStructureDemolish(BaseCombatEntity entity, BasePlayer player)
        {
            ProcessStructuresDemolished((BasePlayer)player);
            return;
        }
        
        void OnItemCraft(ItemCraftTask item)
        {
           if (useIntenseOptions)
               {
                   BasePlayer crafter = item.owner;
                   ProcessItemsCrafted((BasePlayer)crafter);
                   return;
               }
        }
             
        void OnStructureRepair(BaseCombatEntity entity, BasePlayer player) 
        {
           if (useIntenseOptions)
           {
                ProcessEntitiesRepaired((BasePlayer)player);
                return;
           }
        }
        
        void OnHealingItemUse(HeldEntity item, BasePlayer target)
        {
            ProcessTimesHealed((BasePlayer)target);
            return;
        }
        void OnItemUse(Item item)
        {
            BasePlayer player = item?.GetOwnerPlayer();
            if (item.info.displayName.english == "Large Medkit" && player == null)
            {
                return;
            }
            if (item.info.displayName.english == "Large Medkit")
            {
                ProcessTimesHealed((BasePlayer)player);
                return;
            }
        }


        void CanBeWounded(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null) return;
            var attacker = hitInfo.InitiatorPlayer;
            if (attacker != null)
            {
                if (attacker == player || IsFriend(attacker.userID, player.userID) || IsClanmate(attacker.userID, player.userID)) return;
                woundedData[player.userID] = new WoundedData {distance = Vector3.Distance(player.transform.position, attacker.transform.position), attackerId = attacker.userID };
                {
                    NextTick(() => 
                    {       
                        if (player.IsWounded())
                            {
                            ProcessTimesWounded((BasePlayer)player);
                            }
                    });
                } 
            }            
        }
        void OnPlayerRecover(BasePlayer player)
        {
            if (woundedData.ContainsKey(player.userID))
                woundedData.Remove(player.userID);
        }

        void OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            ProcessStructuresUpgraded((BasePlayer)player);
        }

        void OnEntityTakeDamage(BaseEntity entity, HitInfo hitinfo)
        {                                                  
                if (hitinfo?.Initiator is BasePlayer && entity is BaseHelicopter)
                    {
                        var heli = entity.GetComponent<BaseHelicopter>();
                        var player = hitinfo.Initiator.ToPlayer();
                        NextTick(() =>
                        {
                            if (heli == null) return;
                            if (!HeliAttackers.ContainsKey(heli.net.ID))
                                HeliAttackers.Add(heli.net.ID, new Dictionary<ulong, int>());
                            if (!HeliAttackers[heli.net.ID].ContainsKey(player.userID))
                                    HeliAttackers[heli.net.ID].Add(player.userID, 1);
                            else
                            {
                                HeliAttackers[heli.net.ID][player.userID]++;
                                ProcessHeliHits((BasePlayer)player, (BaseEntity)entity);
                                GetMajorityAttacker(heli.net.ID);
                            }
                        });
                    }
        }	
	
        private ulong GetMajorityAttacker(uint id)
        {
            ulong majorityPlayer = 0U;
            if (HeliAttackers.ContainsKey(id))
            {
                Dictionary<ulong, int> majority = HeliAttackers[id].OrderByDescending(pair => pair.Value).Take(1).ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var name in majority)
                {
                    majorityPlayer = name.Key;
                }
            }
            return majorityPlayer;
        }	 
        
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (useIntenseOptions)          
                ProcessResourcesGathered((BasePlayer)player, item.amount);
        }
        
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (useIntenseOptions)
               {
                BasePlayer player = entity?.ToPlayer();
                ProcessResourcesGathered(player, item.amount);
               }
        }

        void Unload() 
        {
        foreach (BasePlayer current in BasePlayer.activePlayerList)
        {
            CuiHelper.DestroyUi(current, "ranksgui");
            topsOpen = false; 
        }
        SaveData();
            if (useMySQL)
            {
                LoadMySQL();
            }
        }
        #region processes    

        private void ProcessKill(BasePlayer player, BasePlayer victim) 
        {
            if (useClans)
                if (victim != null)
                if (IsClanmate(player.userID, victim.userID))
                return;

            if (useFriendsAPI)
                if (victim != null)
                if (IsFriend(player.userID, victim.userID))
                return;

            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
            { 
                data.PlayerRankData[player.userID].PVPKills++;
                if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVPDistance)
                    data.PlayerRankData[player.userID].PVPDistance = Math.Round(victim.Distance(player.transform.position), 2);

                if ((data.PlayerRankData[player.userID].Deaths) > 0)
                {
                    var KDR = System.Convert.ToDouble(data.PlayerRankData[player.userID].PVPKills) / (data.PlayerRankData[player.userID].Deaths);
                    data.PlayerRankData[player.userID].KDR = Math.Round(KDR, 2);
                }
                else
                {
                    data.PlayerRankData[player.userID].KDR = (data.PlayerRankData[player.userID].PVPKills);
                }
            }
        }
        private void ProcessPVEKills(BasePlayer player, BaseEntity victim)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
            {
                data.PlayerRankData[player.userID].PVEKills++;
                if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVEDistance)
                {
                    data.PlayerRankData[player.userID].PVEDistance = Math.Round(victim.Distance(player.transform.position), 2);
                }
            }
        }
        private void ProcessDeath(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
            {
                data.PlayerRankData[player.userID].Deaths++;

                var SDR = System.Convert.ToDouble(data.PlayerRankData[player.userID].Suicides) / (data.PlayerRankData[player.userID].Deaths);
                data.PlayerRankData[player.userID].SDR = Math.Round(SDR, 2);

                var KDR = System.Convert.ToDouble(data.PlayerRankData[player.userID].PVPKills) / (data.PlayerRankData[player.userID].Deaths);
                data.PlayerRankData[player.userID].KDR = Math.Round(KDR, 2);
            }
        }
        private void ProcessBarrelsDestroyed(BasePlayer player, BaseEntity victim)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].BarrelsDestroyed++;
        }
        
        private void ProcessHeliHits(BasePlayer player, BaseEntity victim)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
            {
                data.PlayerRankData[player.userID].HeliHits++;
            }
        }
        
        private void ProcessHeliKills(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].HeliKills++;
        }
        
        private void ProcessSuicide(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
                
            if (data.PlayerRankData.ContainsKey(player.userID))
            {
                data.PlayerRankData[player.userID].Suicides++;

                    if ((data.PlayerRankData[player.userID].Deaths) > 0)
                    {
                        var SDR = System.Convert.ToDouble(data.PlayerRankData[player.userID].Suicides) / (data.PlayerRankData[player.userID].Deaths);
                        data.PlayerRankData[player.userID].SDR = Math.Round(SDR, 2);
                    }
                    else
                    {
                        data.PlayerRankData[player.userID].SDR = (data.PlayerRankData[player.userID].Suicides);
                    }
                    if ((data.PlayerRankData[player.userID].Deaths) > 0)
                    {
                        var KDR = System.Convert.ToDouble(data.PlayerRankData[player.userID].PVPKills) / (data.PlayerRankData[player.userID].Deaths);
                        data.PlayerRankData[player.userID].KDR = Math.Round(KDR, 2);
                    }
                    else
                    {
                        data.PlayerRankData[player.userID].KDR = (data.PlayerRankData[player.userID].PVPKills);
                    }
            }
        }
        private void ProcessTimesWounded(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
                if (data.PlayerRankData.ContainsKey(player.userID))
                    data.PlayerRankData[player.userID].TimesWounded++;
        }
        
        private void ProcessExplosivesThrown(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].ExplosivesThrown++;
        }
        
        private void ProcessArrowsFired(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].ArrowsFired++;
        }
        
        private void ProcessBulletsFired(BasePlayer player)
        {
            if (blockEvents)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                if ((bool)isPlaying)
                return;
            }
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].BulletsFired++;
        }
        
        private void ProcessTurretsDestroyed(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].TurretsDestroyed++;
        }
        private void ProcessSleepersKilled(BasePlayer player, BaseEntity victim)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].SleepersKilled++;
                
            if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVPDistance)
                data.PlayerRankData[player.userID].PVPDistance = Math.Round(victim.Distance(player.transform.position), 2);
        }
        private void ProcessNPCsKilled(BasePlayer player, BaseEntity victim)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
            {
            data.PlayerRankData[player.userID].NPCsKilled++;
            }
            if (victim.Distance(player.transform.position) > data.PlayerRankData[player.userID].PVEDistance)
                data.PlayerRankData[player.userID].PVEDistance = Math.Round(victim.Distance(player.transform.position), 2);
        }
        
        private void ProcessStructuresBuilt(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
            data.PlayerRankData[player.userID].StructuresBuilt++;
        }
        
        private void ProcessStructuresDemolished(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].StructuresDemolished++;
        }
        
        private void ProcessItemsDeployed(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].ItemsDeployed++;
        }
        
        private void ProcessItemsCrafted(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].ItemsCrafted++;
        }
        
        private void ProcessEntitiesRepaired(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
            data.PlayerRankData[player.userID].EntitiesRepaired++;
        }
        
        private void ProcessRocketsLaunched(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].RocketsLaunched++;
        }
        
        private void ProcessTimesHealed(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
                data.PlayerRankData[player.userID].TimesHealed++;
        }

        private void ProcessResourcesGathered(BasePlayer player, int amount = 0)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
            data.PlayerRankData[player.userID].ResourcesGathered+=amount;
        }
    
        private void ProcessStructuresUpgraded(BasePlayer player)
        {
            if (data.PlayerRankData.ContainsKey(player.userID))
            data.PlayerRankData[player.userID].StructuresUpgraded++;
        } 

        private void BroadcastToAll(string msg, string keyword) => PrintToChat(fontColor1 + keyword + " </color>" + fontColor2 + msg + "</color>");
        
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
        if (!Clans || !useClans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if (playerTag is string && friendTag is string)
            if (playerTag == friendTag) return true;
            return false;
        }

        private bool IsFriend(ulong playerID, ulong friendID)
        {
            if (!Friends || !useFriendsAPI) return false;
            bool isFriend = (bool)Friends?.Call("IsFriend", playerID, friendID);
            return isFriend;
        }
        #endregion

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            data.PlayerRankData[player.userID].Status = "offline";
            if (isAuth(player))
            {
            data.PlayerRankData[player.userID].Admin = true;
            }
            CuiHelper.DestroyUi(player, "ranksgui");
            topsOpen = false;
        }

        void UseUI(BasePlayer player, string msg, string msg1)
        {
            string guiString = String.Format("0.1 0.1 0.1 {0}", guitransparency);
            var elements = new CuiElementContainer();
            var mainName = elements.Add(new CuiPanel
            {
                Image =
                {
                    Color = guiString
                },
                RectTransform =
                {
                    AnchorMin = "0.3 0.1",
                    AnchorMax = "0.7 0.9"
                },
                CursorEnabled = true
            }, "Overlay", "ranksgui"); 

                    elements.Add(new CuiElement
                    {  
                        Parent = "ranksgui",
                        Components =
                        {
                            new CuiRectTransformComponent
                            {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1"
                            }
                        }
                    });
            var Close = new CuiButton
            {
                Button =
                {
                    Command = "Close",
                    Color = closeColor
                },
                RectTransform =
                {
                    AnchorMin = "0.4 0.02",
                    AnchorMax = "0.6 0.082"
                },
                Text =
                {
                    Text = "Close",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
            };
                elements.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = msg1,
                        FontSize = 26,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 0.98"
                    }
                            }, mainName);
                            elements.Add(new CuiLabel
                            {
                    Text =
                    {
                        Text = msg,
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0.10",
                        AnchorMax = "1 0.9"
                    }
                    }, mainName);
                    elements.Add(Close, mainName);
                    CuiHelper.AddUi(player, elements);
        }         

       #region console commands
        [ConsoleCommand("Close")]
        private void Close(ConsoleSystem.Arg arg)
        { 
            var player = arg.Connection.player as BasePlayer;
            CuiHelper.DestroyUi(player, "ranksgui");
            topsOpen = false;
            return;

        }
        [ConsoleCommand("ToggleTops")]
        private void cmdToggleTops(ConsoleSystem.Arg arg)
        { 
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            cmdTarget(player, "pr", new String[] { "tops" });
        }
        
        [ConsoleCommand("playerranks.save")]
        private void cmdSave(ConsoleSystem.Arg arg)
        {
            if (useMySQL)
            {
                LoadMySQL(); 
            }
            SaveData();
            Puts("PlayerRanks database was saved.");
        }
        
        #endregion
       #region chat commands
        [ChatCommand("pr")] 
        void cmdTarget(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("prtop", this, player.UserIDString) + " : </color>" + fontColor3 + lang.GetMessage("prtop2", this, player.UserIDString) + "</color> \n", fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>");

                        outMsg += string.Format(fontColor1 + lang.GetMessage("prcat", this, player.UserIDString) + " : </color>" + fontColor3 + lang.GetMessage("prcat2", this, player.UserIDString) + "</color>", fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "\n </color>");


                SendReply(player, outMsg); 
                return;
            }
	    
            var d = data.PlayerRankData[player.userID]; 
            switch (args[0].ToLower())
            
            {
            case "tops":

		    string msg1 = string.Format(fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + fontColor2 + "{0}" + "</color> \n", d.Name);
                    string topsMsgGUI = string.Format(string.Format(""));
                    if(usepvpkills)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("PVPKills", this, player.UserIDString)+ ": </color>" + fontColor1 + "{0}", d.PVPKills), 1.0) + "</color> \n";         
                    if(usepvpdistance)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("PVPDistance", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.PVPDistance), 1.0) + "</color> \n";       
                    if(usepvekills)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("PVEKills", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.PVEKills), 1.0) + "</color> \n";
                    if(usepvedistance)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("PVEDistance", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.PVEDistance), 1.0) + "</color> \n";
                    if(usenpcskilled)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("NPCsKilled", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.NPCsKilled), 1.0) + "</color> \n";
                    if(usedeaths)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("Deaths", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.Deaths), 1.0) + "</color> \n";
                    if(usesuicides)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("Suicides", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.Suicides), 1.0) + "</color> \n";
                    if(usetimeswounded)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("TimesWounded", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.TimesWounded), 1.0) + "</color> \n";
                    if(usekdr)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("KDR", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.KDR), 1.0) + "</color> \n";
                    if(usesdr)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("SDR", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.SDR), 1.0) + "</color> \n";
                    if(usehelikills)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("HeliKills", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.HeliKills), 1.0) + "</color> \n";
                    if(usehelihits)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("HeliHits", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.HeliHits), 1.0) + "</color> \n";
                    if(usebarrelsdestroyed)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("BarrelsDestroyed", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.BarrelsDestroyed), 1.0) + "</color> \n";
                    if(useexplosivesthrown)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("ExplosivesThrown", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.ExplosivesThrown), 1.0) + "</color> \n";
                    if(usearrowsfired)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("ArrowsFired", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.ArrowsFired), 1.0) + "</color> \n";
                    if(usebulletsfired)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("BulletsFired", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}",d.BulletsFired), 1.0) + "</color> \n";
                    if(useturretsdestroyed)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("TurretsDestroyed", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}",d.TurretsDestroyed), 1.0) + "</color> \n";
                    if(usesleeperskilled)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("SleepersKilled", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.SleepersKilled), 1.0) + "</color> \n";
                    if(userocketslaunched)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("RocketsLaunched", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.RocketsLaunched), 1.0) + "</color> \n";
                    if(usetimeshealed)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("TimesHealed", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.TimesHealed), 1.0) + "</color> \n";
                    if(usestructuresbuilt)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("StructuresBuilt", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.StructuresBuilt), 1.0) + "</color> \n";
                    if(useitemsdeployed)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("ItemsDeployed", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.ItemsDeployed), 1.0) + "</color> \n";
                    if(useitemscrafted)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("ItemsCrafted", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.ItemsCrafted), 1.0) + "</color> \n";
                    if(useentitiesrepaired)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("EntitiesRepaired", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.EntitiesRepaired), 1.0) + "</color> \n";
                    if(usestructuresdemolished)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("StructuresDemolished", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.StructuresDemolished), 1.0) + "</color> \n";
                    if(useresourcesgathered)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("ResourcesGathered", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.ResourcesGathered), 1.0) + "</color> \n";
                    if(usestructuresupgraded)topsMsgGUI += string.Format(string.Format(fontColor3 + lang.GetMessage("StructuresUpgraded", this, player.UserIDString) + ": </color>" + fontColor1 + "{0}", d.StructuresUpgraded), 1.0) + "</color> \n";
                    
                    if (topsOpen)
                    {
                        CuiHelper.DestroyUi(player, "ranksgui");
                        topsOpen = false;
                        return;
                    }
                    else
                    {
                        UseUI(player, topsMsgGUI.ToString(), msg1.ToString());
                        topsOpen = true;
                    }
                    return;
            
            case "pvpkills":
                if (usepvpkills)
                {
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.PVPKills).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVPKills);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                     if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("PVPKills", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "pvpdistance":
                if (usepvpdistance)
                {
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.PVPDistance).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVPDistance);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("PVPDistance", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    } 
                }
                return;
            case "pvekills":
                if (usepvekills)
                {
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.PVEKills).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVEKills);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("PVEKills", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    } 
                }
                return;
            case "pvedistance":
                if (usepvedistance)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.PVEDistance).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVEDistance);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("PVEDistance", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "deaths":
                if (usedeaths)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value); 
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.Deaths).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.Deaths);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("Deaths", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return; 
            case "barrelsdestroyed": case "barrels":
                if (usebarrelsdestroyed)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.BarrelsDestroyed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.BarrelsDestroyed);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("BarrelsDestroyed", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "helicopterhits": case "helihits":
                if (usehelihits)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.HeliHits).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.HeliHits);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("HeliHits", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "helicopterkills": case "helikills":
                if (usehelikills)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.HeliKills).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.HeliKills);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("HeliKills", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "suicides":
                if (usesuicides)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.Suicides).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.Suicides);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("Suicides", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "timeswounded": case "wounded":
                if (usetimeswounded)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.TimesWounded).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.TimesWounded);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("TimesWounded", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "explosivesthrown":  case "explosives":
                if (useexplosivesthrown)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ExplosivesThrown).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ExplosivesThrown);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("ExplosivesThrown", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "arrowsfired": case "arrows":
                if (usearrowsfired)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ArrowsFired).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ArrowsFired);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("ArrowsFired", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "bulletsfired": case "bullets":
                if (usebulletsfired)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.BulletsFired).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.BulletsFired);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("BulletsFired", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "turretsdestroyed": case "turrets":
                if (useturretsdestroyed)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.TurretsDestroyed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.TurretsDestroyed);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("TurretsDestroyed", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "sleeperskilled": case "sleepers":
                if (usesleeperskilled)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.SleepersKilled).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.SleepersKilled);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("SleepersKilled", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "npcskilled": case "npcs":
                if (usenpcskilled)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.NPCsKilled).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.NPCsKilled);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("NPCsKilled", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "rocketslaunched": case "rockets":
                if (userocketslaunched)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.RocketsLaunched).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.RocketsLaunched);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("RocketsLaunched", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "timeshealed": case "healed":
                if (usetimeshealed)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.TimesHealed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.TimesHealed);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("TimesHealed", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "kdr":
                if (usekdr)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.KDR).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.KDR);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("KDR", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "sdr":
                if (usesdr)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = PrivateTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.SDR).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.SDR);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("SDR", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                }
                return;
            case "structuresbuilt": case "structures":
                if (usestructuresbuilt)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.StructuresBuilt).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.StructuresBuilt);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("StructuresBuilt", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                            SendReply(player, outMsg); 
                    }
                        }
                }
                return;
            case "itemsdeployed": case "deployed":
                if (useitemsdeployed)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ItemsDeployed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ItemsDeployed);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("ItemsDeployed", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }
                                if (outMsg != "")
                                    SendReply(player, outMsg); 
                            }
                        }
                }
                return;
            case "itemscrafted": case "crafted":
                if (useitemscrafted)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ItemsCrafted).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ItemsCrafted);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("ItemsCrafted", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }
                                if (outMsg != "")
                                    SendReply(player, outMsg); 
                            }
                        }
                }
                return;
            case "entitiesrepaired": case "repaired":
                if (useentitiesrepaired)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.EntitiesRepaired).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.EntitiesRepaired);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("EntitiesRepaired", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }
                                if (outMsg != "")
                                    SendReply(player, outMsg); 
                            }
                        }
                }
                return;
            case "structuresdemolished": case "demolished":
                if (usestructuresdemolished)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.StructuresDemolished).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.StructuresDemolished);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("StructuresDemolished", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }
                                if (outMsg != "")
                                    SendReply(player, outMsg); 
                            }
                        }
                }
                return;
            case "resourcesgathered":  case "gathered": case "resources":
                if (useresourcesgathered)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ResourcesGathered).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ResourcesGathered);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("ResourcesGathered", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }
                                if (outMsg != "")
                                    SendReply(player, outMsg); 
                            }
                        }
                }
                return;
            case "structuresupgraded": case "upgraded":
                if (usestructuresupgraded)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = PrivateTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.StructuresUpgraded).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.StructuresUpgraded);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this)  + lang.GetMessage("StructuresUpgraded", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }
                                if (outMsg != "")
                                    SendReply(player, outMsg); 
                            }
                        }
                }
                return;            
            case "save":
                if (HasPermission(player.UserIDString, permAllowed))
                {
                    if (useMySQL)
                    {
                        LoadMySQL(); 
                    }
                    SaveData();
                    SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("save", this, player.UserIDString));
                    OnServerInitialized();
                }
                return;            
        
            case "wipe":
                if (HasPermission(player.UserIDString, permAllowed))
                {
                    data.PlayerRankData.Clear();
                    SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("wipe", this, player.UserIDString));
                    SaveData();
                    OnServerInitialized();
                }
                return;
                
            case "del":
                if (HasPermission(player.UserIDString, permAllowed))
                    {
                        if (args.Length == 2)
                        {
                                string s = args[1];
                                ulong result;
                                if (ulong.TryParse(s, out result))
                                {
                                    ulong arg = Convert.ToUInt64(args[1]);
                                    if (data.PlayerRankData.ContainsKey(arg))
                                    {
                                    data.PlayerRankData.Remove(arg);
                                    SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("dbremoved", this, player.UserIDString));
                                    PRData.WriteObject(data);
                                    SaveData();
                                    OnServerInitialized();
                                    }
                                    else
                                    {
                                    SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("noentry", this, player.UserIDString));
                                    }
                                }
                                else
                                {
                                    SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("syntax", this, player.UserIDString));
                                }
                        }
                    }
                    return;
   
   
                case "wipecategory":
         
                        if (args.Length == 2)
                        {
                            if (HasPermission(player.UserIDString, permAllowed))
                            {
                                //var category = args[1].ToString();
                                String [] stringArray = {"pvpkills", "pvpdistance", "pvekills", "pvedistance", "npcskilled", "deaths", "suicides", "timeswounded", "kdr", "sdr", "helihits", "helikills", "barrelsdestroyed", "explosivesthrown", "arrowsfired", "bulletsfired", "turretsdestroyed", "sleeperskilled", "rocketslaunched", "timeshealed", "structuresbuilt", "itemsdeployed", "itemscrafted", "entitiesrepaired", "structuresdemolished", "resourcesgathered", "structuresupgraded"};
                                    if (stringArray.Any(args[1].ToString().Contains))
                                        {
                                            foreach (var Entry in data.PlayerRankData)
                                            {
                                            if (args[1].ToString() == "pvpkills")
                                            data.PlayerRankData[Entry.Key].PVPKills = 0;
                                            if (args[1].ToString() == "pvpdistance")
                                            data.PlayerRankData[Entry.Key].PVPDistance = 0;
                                            if (args[1].ToString() == "pvekills")
                                            data.PlayerRankData[Entry.Key].PVEKills = 0;
                                            if (args[1].ToString() == "pvedistance")
                                            data.PlayerRankData[Entry.Key].PVEDistance = 0;
                                            if (args[1].ToString() == "npcskilled")
                                            data.PlayerRankData[Entry.Key].NPCsKilled = 0;
                                            if (args[1].ToString() == "deaths")
                                            data.PlayerRankData[Entry.Key].Deaths = 0;
                                            if (args[1].ToString() == "barrelsdestroyed")
                                            data.PlayerRankData[Entry.Key].BarrelsDestroyed = 0;
                                            if (args[1].ToString() == "helihits")
                                            data.PlayerRankData[Entry.Key].HeliHits = 0;
                                            if (args[1].ToString() == "helikills")
                                            data.PlayerRankData[Entry.Key].HeliKills = 0;
                                            if (args[1].ToString() == "suicides")
                                            data.PlayerRankData[Entry.Key].Suicides = 0;
                                            if (args[1].ToString() == "timeswounded")
                                            data.PlayerRankData[Entry.Key].TimesWounded = 0;
                                            if (args[1].ToString() == "explosivesthrown")
                                            data.PlayerRankData[Entry.Key].ExplosivesThrown = 0;
                                            if (args[1].ToString() == "arrowsfired")
                                            data.PlayerRankData[Entry.Key].ArrowsFired = 0;
                                            if (args[1].ToString() == "bulletsfired")
                                            data.PlayerRankData[Entry.Key].BulletsFired = 0;
                                            if (args[1].ToString() == "turretsdestroyed")
                                            data.PlayerRankData[Entry.Key].TurretsDestroyed = 0;
                                            if (args[1].ToString() == "sleeperskilled")
                                            data.PlayerRankData[Entry.Key].SleepersKilled = 0;
                                            if (args[1].ToString() == "rocketslaunched")
                                            data.PlayerRankData[Entry.Key].RocketsLaunched = 0;
                                            if (args[1].ToString() == "timeshealed")
                                            data.PlayerRankData[Entry.Key].TimesHealed = 0;
                                            if (args[1].ToString() == "structuresbuilt")
                                            data.PlayerRankData[Entry.Key].StructuresBuilt = 0;
                                            if (args[1].ToString() == "itemsdeployed")
                                            data.PlayerRankData[Entry.Key].ItemsDeployed = 0;
                                            if (args[1].ToString() == "itemscrafted")
                                            data.PlayerRankData[Entry.Key].ItemsCrafted = 0;
                                            if (args[1].ToString() == "entitiesrepaired")
                                            data.PlayerRankData[Entry.Key].EntitiesRepaired = 0;
                                            if (args[1].ToString() == "structuresdemolished")
                                            data.PlayerRankData[Entry.Key].StructuresDemolished = 0;
                                            if (args[1].ToString() == "resourcesgathered")
                                            data.PlayerRankData[Entry.Key].ResourcesGathered = 0;
                                            if (args[1].ToString() == "structuresupgraded")
                                            data.PlayerRankData[Entry.Key].StructuresUpgraded = 0;
                                            if (args[1].ToString() == "kdr")
                                            data.PlayerRankData[Entry.Key].KDR = 0;
                                            if (args[1].ToString() == "sdr")
                                            data.PlayerRankData[Entry.Key].SDR = 0;
                                            }
                                            PRData.WriteObject(data);
                                            SaveData();
                                            OnServerInitialized();
                                            SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("category", this, player.UserIDString));
                                        }
                                        else
                                        {
                                            SendReply(player, fontColor1 + lang.GetMessage("title", this, player.UserIDString) + "</color>" + lang.GetMessage("nocategory", this, player.UserIDString));
                                        }
                            }
                        }
                        return;
            }
        }
 
         void pvpkills()
            {
                if (usepvpkills)
                {
                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.PVPKills).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVPKills);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("PVPKills", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => pvpdistance()); 
                    }
                    else
                    {
                    timer.Once(10, () => pvpdistance());
                    }
                }
                else
                timer.Once(10, () => pvpdistance());
            }
         void pvpdistance()
            {
                if (usepvpdistance)
                {
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.PVPDistance).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVPDistance);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("PVPDistance", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => pvekills());
                    }
                    else
                    timer.Once(10, () => pvekills());
                }
                else
                timer.Once(10, () => pvekills());
            }
         void pvekills()
            {
                if (usepvekills)
                {
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.PVEKills).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVEKills);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("PVEKills", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => pvedistance());
                    }
                    else
                    timer.Once(10, () => pvedistance());
                }
                else
                timer.Once(10, () => pvedistance());
            }
         void pvedistance()
            {
                if (usepvedistance)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.PVEDistance).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.PVEDistance);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("PVEDistance", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => deaths());
                    }
                    else
                    timer.Once(10, () => deaths());
                }
                else
                timer.Once(10, () => deaths());
            }
         void deaths()
            {
                if (usedeaths)
                {                
                     var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.Deaths).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.Deaths);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("Deaths", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => barrelsdestroyed());
                    }
                    else
                    timer.Once(10, () => barrelsdestroyed());
                }
                else
                timer.Once(10, () => barrelsdestroyed());
            }
         void barrelsdestroyed()
            {
                if (usebarrelsdestroyed)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.BarrelsDestroyed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.BarrelsDestroyed);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("BarrelsDestroyed", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => helihits());
                    }
                    else
                    timer.Once(10, () => helihits());
                }
                else
                timer.Once(10, () => helihits());
            }
         void helihits()
            {
                if (usehelihits)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.HeliHits).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.HeliHits);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("HeliHits", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => helikills());
                    }
                    else
                    timer.Once(10, () => helikills());
                }
                else
                timer.Once(10, () => helikills());
            }
         void helikills()
            {
                if (usehelikills)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.HeliKills).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.HeliKills);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("HeliKills", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => suicides());
                    }
                    else
                    timer.Once(10, () => suicides());
                }
                else
                timer.Once(10, () => suicides());
            }
         void suicides()
            {
                if (usesuicides)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.Suicides).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.Suicides);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("Suicides", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => timeswounded());
                    }
                    else
                    timer.Once(10, () => timeswounded());
                }
                else
                timer.Once(10, () => timeswounded());
            }
         void timeswounded()
            {
                if (usetimeswounded)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.TimesWounded).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.TimesWounded);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("TimesWounded", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => explosivesthrown());
                    }
                    else
                    timer.Once(10, () => explosivesthrown());
                }
                else
                timer.Once(10, () => explosivesthrown());
            }
         void explosivesthrown()
            {
                if (usetimeswounded)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ExplosivesThrown).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ExplosivesThrown);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("ExplosivesThrown", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => arrowsfired());
                    }
                    else
                    timer.Once(10, () => arrowsfired());
                }
                else
                timer.Once(10, () => arrowsfired());
            }
         void arrowsfired()
            {
                if (usearrowsfired)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ArrowsFired).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ArrowsFired);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("ArrowsFired", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => bulletsfired());
                    }
                    else
                    timer.Once(10, () => bulletsfired());
                }
                else
                timer.Once(10, () => bulletsfired());
            }
         void bulletsfired()
            {
                if (usebulletsfired)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.BulletsFired).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.BulletsFired);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("BulletsFired", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => turretsdestroyed());
                    }
                    else
                    timer.Once(10, () => turretsdestroyed());
                }
                else
                timer.Once(10, () => turretsdestroyed());
            }
         void turretsdestroyed()
            {
                if (useturretsdestroyed)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.TurretsDestroyed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.TurretsDestroyed);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("TurretsDestroyed", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => sleeperskilled());
                    }
                    else
                    timer.Once(10, () => sleeperskilled());
                }
                else
                timer.Once(10, () => sleeperskilled());
            }
         void sleeperskilled()
            {
                if (usesleeperskilled)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.SleepersKilled).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.SleepersKilled);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("SleepersKilled", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => npcskilled());
                    }
                    else
                    timer.Once(10, () => npcskilled());
                }
                else
                timer.Once(10, () => npcskilled());
            }
         void npcskilled()
            {
                if (usenpcskilled)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.NPCsKilled).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.NPCsKilled);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("NPCsKilled", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => rocketslaunched());
                    }
                    else
                    timer.Once(10, () => rocketslaunched());
                }
                else
                timer.Once(10, () => rocketslaunched());
            }
         void rocketslaunched()
            {
                if (userocketslaunched)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.RocketsLaunched).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.RocketsLaunched);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("RocketsLaunched", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => timeshealed());
                    }
                    else
                    timer.Once(10, () => timeshealed());
                }
                else
                timer.Once(10, () => timeshealed());
            }
         void timeshealed()
            {
                if (usetimeshealed)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.TimesHealed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.TimesHealed);
                    top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("TimesHealed", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => kdr());
                    }
                    else
                    timer.Once(10, () => kdr());
                }
                else
                timer.Once(10, () => kdr());
            }
         void kdr()
            {
                if (usekdr)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.KDR).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.KDR);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("KDR", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => sdr());
                    }
                    else
                    timer.Once(10, () => sdr());
                }
                else
                timer.Once(10, () => sdr());
            }
         void sdr()
            {
                if (usesdr)
                {                
                    var dictToUse = data.PlayerRankData;
                    int amount = TimedTopListAmount;
                    if (allowadmin == false)
                    {
                        dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                    }
                    Dictionary<string, double> top = dictToUse.OrderByDescending(pair => pair.Value.SDR).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.SDR);
                    if (top.Count > 0)
                    {
                        var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("SDR", this), 1.0) + "</color> \n";
                        foreach (var name in top)
                        {
                            outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                        }
                        if (outMsg != "")
                        Server.Broadcast(outMsg);
                        timer.Once(TimedTopListTimer * 60, () => structuresbuilt());
                    }
                    else
                    timer.Once(10, () => structuresbuilt());
                }
                else
                timer.Once(10, () => structuresbuilt());
            }
         void structuresbuilt()
            {
                if (usestructuresbuilt)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.StructuresBuilt).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.StructuresBuilt);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("StructuresBuilt", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => itemsdeployed());
                            }
                            else
                            timer.Once(10, () => itemsdeployed());
                        }
                        else
                        timer.Once(10, () => itemsdeployed());
                }
                else
                timer.Once(10, () => itemsdeployed());
            }
         void itemsdeployed()
            {
                if (useitemsdeployed)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ItemsDeployed).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ItemsDeployed);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("ItemsDeployed", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => itemscrafted());
                            }
                            else
                            timer.Once(10, () => itemscrafted());
                        }
                        else
                        timer.Once(10, () => itemscrafted());
                }
                else
                timer.Once(10, () => itemscrafted());
            }
         void itemscrafted()
            {
                if (useitemscrafted)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ItemsCrafted).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ItemsCrafted);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("ItemsCrafted", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => entitiesrepaired());
                            }
                            else
                            timer.Once(10, () => entitiesrepaired());
                        }
                        else
                        timer.Once(10, () => entitiesrepaired());
                }
                else
                timer.Once(10, () => entitiesrepaired());
            }
         void entitiesrepaired()
            {
                if (useentitiesrepaired)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.EntitiesRepaired).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.EntitiesRepaired);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("EntitiesRepaired", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => structuresdemolished());
                            }
                            else
                            timer.Once(10, () => structuresdemolished());
                        }
                        else
                        timer.Once(10, () => structuresdemolished());
                }
                else
                timer.Once(10, () => structuresdemolished());
            }
         void structuresdemolished()
            {
                if (usestructuresdemolished)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.StructuresDemolished).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.StructuresDemolished);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("StructuresDemolished", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => resourcesgathered());
                            }
                            else
                            timer.Once(10, () => resourcesgathered());
                        }
                        else
                        timer.Once(10, () => resourcesgathered());
                }
                else
                timer.Once(10, () => resourcesgathered());
            }
         void resourcesgathered()
            {
                if (useresourcesgathered)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.ResourcesGathered).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.ResourcesGathered);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("ResourcesGathered", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => structuresupgraded());
                            }
                            else
                            timer.Once(10, () => structuresupgraded());
                        }
                        else
                        timer.Once(10, () => structuresupgraded());
                }
                else
                timer.Once(10, () => structuresupgraded());
            }
         void structuresupgraded()
            {
                if (usestructuresupgraded)
                {
                    if (useIntenseOptions)
                        {
                            var dictToUse = data.PlayerRankData;
                            int amount = TimedTopListAmount;
                            if (allowadmin == false)
                            {
                                dictToUse = data.PlayerRankData.Where(pair => pair.Value.Admin == false).ToDictionary(val => val.Key, val => val.Value);
                            }
                            Dictionary<string, int> top = dictToUse.OrderByDescending(pair => pair.Value.StructuresUpgraded).Take(amount).ToDictionary(pair => pair.Value.Name, pair => pair.Value.StructuresUpgraded);
                            top = top.Where(kvp => kvp.Value > 0).ToDictionary(x => x.Key, x => x.Value);
                            if (top.Count > 0)
                            {
                                var outMsg = string.Format(fontColor1 + lang.GetMessage("title", this) + "</color>" + fontColor2 + lang.GetMessage("bestHits", this) + lang.GetMessage("StructuresUpgraded", this), 1.0) + "</color> \n";
                                foreach (var name in top)
                                {
                                    outMsg += string.Format(fontColor3 + "{0} : " + "</color>" + fontColor1 + "{1}" + "</color>" + "\n", name.Key, name.Value);
                                }       
                                if (outMsg != "")
                                Server.Broadcast(outMsg);
                                timer.Once(TimedTopListTimer * 60, () => pvpkills());
                            }
                            else
                            timer.Once(10, () => pvpkills());
                        }
                        else
                        timer.Once(10, () => pvpkills());
                }
                else
                timer.Once(10, () => pvpkills());
            }
        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2)
                    return false;
                    return true;
        }
        
        
public static string RemoveSurrogatePairs(string str, string replacementCharacter = "?")
{
    if (str == null)
    {
        return null;
    }

    StringBuilder sb = null;

    for (int i = 0; i < str.Length; i++)
    {
        char ch = str[i];

        if (char.IsSurrogate(ch))
        {
            if (sb == null)
            {
                sb = new StringBuilder(str, 0, i, str.Length);
            }

            sb.Append(replacementCharacter);

            // If there is a high+low surrogate, skip the low surrogate
            if (i + 1 < str.Length && char.IsHighSurrogate(ch) && char.IsLowSurrogate(str[i + 1]))
            {
                i++;
            }
        }
        else if (sb != null)
        {
            sb.Append(ch);
        }
    }

    return sb == null ? str : sb.ToString();
}        
        
            Core.MySql.Libraries.MySql Sql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>(); 
            Connection Sql_conn;
        
        void LoadMySQL()
        {

        if (Sql_conn != null)
        {
        Sql.CloseDb(Sql_conn);    
        }
            try
            {
                Sql_conn = Sql.OpenDb(sql_host, sql_port, sql_db, sql_user, sql_pass, this); 

                if (Sql_conn == null || Sql_conn.Con == null) 
                {
                    Puts("Player Ranks MySQL connection has failed. Please check your credentials.");     
                    return; 
                }
                Sql.Insert(Core.Database.Sql.Builder.Append($"CREATE TABLE IF NOT EXISTS {tablename} ( `UserID` VARCHAR(17) NOT NULL, `Name` LONGTEXT NOT NULL, `PVPKills` INT(11) NOT NULL, `PVPDistance` DOUBLE NOT NULL, `PVEKills` INT(11) NOT NULL, `PVEDistance` DOUBLE NOT NULL, `Deaths` INT(11) NOT NULL, `BarrelsDestroyed` INT(11) NOT NULL, `HeliHits` INT(11) NOT NULL, `HeliKills` INT(11) NOT NULL, `Suicides` INT(11) NOT NULL, `TimesWounded` INT(11) NOT NULL, `ExplosivesThrown` INT(11) NOT NULL, `ArrowsFired` INT(11) NOT NULL, `BulletsFired` INT(11) NOT NULL, `TurretsDestroyed` INT(11) NOT NULL, `SleepersKilled` INT(11) NOT NULL, `NPCsKilled` INT(11) NOT NULL, `RocketsLaunched` INT(11) NOT NULL, `TimesHealed` INT(11) NOT NULL, `KDR` DOUBLE NOT NULL, `SDR` DOUBLE NOT NULL, `StructuresBuilt` INT(11) NOT NULL, `ItemsDeployed` INT(11) NOT NULL, `ItemsCrafted` INT(11) NOT NULL, `EntitiesRepaired` INT(11) NOT NULL, `StructuresDemolished` INT(11) NOT NULL, `ResourcesGathered` INT(11) NOT NULL, `StructuresUpgraded` INT(11) NOT NULL, `Status` VARCHAR(11) NOT NULL, `TimePlayed` VARCHAR(11) NOT NULL, `Recent` INT(11) NOT NULL, `Economics` INT(11) NOT NULL, PRIMARY KEY (`UserID`) );"), Sql_conn);
            } 
            catch (Exception e)  
            { 
                Puts("Player Ranks did not succesfully create a table.");    
            }  

            foreach(var c in data.PlayerRankData) 
            {
            Sql.Insert(Core.Database.Sql.Builder.Append($"INSERT INTO {tablename} ( `UserID`, `Name`, `PVPKills`, `PVPDistance`, `PVEKills`, `PVEDistance`, `Deaths`, `BarrelsDestroyed`, `HeliHits`, `HeliKills`, `Suicides`, `TimesWounded`, `ExplosivesThrown`, `ArrowsFired`, `BulletsFired`, `TurretsDestroyed`, `SleepersKilled`, `NPCsKilled`, `RocketsLaunched`, `TimesHealed`, `KDR`, `SDR`, `StructuresBuilt`, `ItemsDeployed`, `ItemsCrafted`, `EntitiesRepaired`, `StructuresDemolished`, `ResourcesGathered`, `StructuresUpgraded`, `Status`, `TimePlayed`, `Recent`, `Economics`) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17, @18, @19, @20, @21, @22, @23, @24, @25, @26, @27, @28, @29, @30, @31, @32) ON DUPLICATE KEY UPDATE Name = @1, PVPKills = @2, PVPDistance = @3, PVEKills = @4, PVEDistance = @5, Deaths = @6, BarrelsDestroyed = @7, HeliHits = @8, HeliKills = @9, Suicides = @10, TimesWounded = @11, ExplosivesThrown = @12, ArrowsFired = @13, BulletsFired = @14, TurretsDestroyed = @15, SleepersKilled = @16, NPCsKilled = @17, RocketsLaunched = @18, TimesHealed = @19, KDR = @20, SDR = @21, StructuresBuilt = @22, ItemsDeployed = @23, ItemsCrafted = @24, EntitiesRepaired = @25, StructuresDemolished = @26, ResourcesGathered = @27, StructuresUpgraded = @28, Status = @29, TimePlayed = @30, Recent = @31, Economics = @32", c.Value.UserID, RemoveSurrogatePairs(c.Value.Name, ""), c.Value.PVPKills, c.Value.PVPDistance, c.Value.PVEKills, c.Value.PVEDistance, c.Value.Deaths, c.Value.BarrelsDestroyed, c.Value.HeliHits, c.Value.HeliKills, c.Value.Suicides, c.Value.TimesWounded, c.Value.ExplosivesThrown, c.Value.ArrowsFired, c.Value.BulletsFired, c.Value.TurretsDestroyed, c.Value.SleepersKilled, c.Value.NPCsKilled, c.Value.RocketsLaunched, c.Value.TimesHealed, c.Value.KDR, c.Value.SDR, c.Value.StructuresBuilt, c.Value.ItemsDeployed, c.Value.ItemsCrafted, c.Value.EntitiesRepaired, c.Value.StructuresDemolished, c.Value.ResourcesGathered, c.Value.StructuresUpgraded, c.Value.Status, c.Value.TimePlayed, c.Value.Recent, c.Value.Economics), Sql_conn); 
            }
            Puts("Player Ranks MySQL Database Was Saved.");
            
        }
        #endregion

        #region config 

        static bool useFriendsAPI = true;
        static bool useClans = true;
        static bool blockEvents = true;
        static bool useIntenseOptions = true;
        static int TimedTopListTimer = 10;
        static int TimedTopListAmount = 3;
        static int PrivateTopListAmount = 5;
        static bool useTimedTopList = true;
        static int saveTimer = 30;
        static string chatCommandAlias = "ranks";
        static string fontColor1 = "<color=orange>";
        static string fontColor2 = "<color=#939393>";
        static string fontColor3 = "<color=white>";
        static string closeColor = "0.7 0.32 0.17 1";
        static double guitransparency = 0.5;
        static bool allowadmin = false;
        static bool usepvpkills = true;
        static bool usepvpdistance = true;
        static bool usepvekills = true;
        static bool usepvedistance = true;
        static bool usedeaths = true;
        static bool usebarrelsdestroyed = true;
        static bool usehelihits = true;
        static bool usehelikills = true;
        static bool usesuicides = true;
        static bool usetimeswounded = true;
        static bool useexplosivesthrown = true;
        static bool usearrowsfired = true;
        static bool usebulletsfired = true;
        static bool useturretsdestroyed = true;
        static bool usesleeperskilled = true;
        static bool usenpcskilled = true;
        static bool userocketslaunched = true;
        static bool usetimeshealed = true;
        static bool usestructuresbuilt = true;
        static bool useitemsdeployed = true;
        static bool useitemscrafted = true;
        static bool useentitiesrepaired = true;
        static bool usestructuresdemolished = true;
        static bool useresourcesgathered = true;
        static bool usestructuresupgraded = true; 
        static bool usekdr = true;
        static bool usesdr = true;                

        static bool useMySQL = false;
        static string sql_host = "";
        static int sql_port = 3306;
        static string sql_db = "";
        static string sql_user = "";
        static string sql_pass = "";
        static string tablename = "playerranksdb";
        
        private bool topsOpen;
        
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            CheckCfg("Options - Use FriendsAPI", ref useFriendsAPI);
            CheckCfg("Options - Use Clans", ref useClans);
            CheckCfg("Options - Block Events", ref blockEvents);
            CheckCfg("Options - Use Intense Options", ref useIntenseOptions);
            CheckCfg("Options - Use Random top table", ref useTimedTopList);
            CheckCfg("Options - Random Top List timer", ref TimedTopListTimer);
            CheckCfg("Options - Amount of results in public timed top-list.", ref TimedTopListAmount);
            CheckCfg("Options - Amount of results in private on-command top-list.", ref PrivateTopListAmount);
            CheckCfg("Options - Chat Command Alias", ref chatCommandAlias);
            CheckCfg("Options - GUI Transparency 0-1", ref guitransparency);
            CheckCfg("Options - Close Button Colour", ref closeColor);
            CheckCfg("Options - Save Timer", ref saveTimer);
         
         
            CheckCfg("Messages - Title and stats color", ref fontColor1);
            CheckCfg("Messages - Message color", ref fontColor2);
            CheckCfg("Messages - Category color", ref fontColor3);
            CheckCfg("Messages - Allow Public Admin Stats", ref allowadmin);

            CheckCfg("Categories - PVP Kills", ref usepvpkills);
            CheckCfg("Categories - PVP Distance", ref usepvpdistance);
            CheckCfg("Categories - PVE Kills", ref usepvekills);
            CheckCfg("Categories - PVE Distance", ref usepvedistance);
            CheckCfg("Categories - Deaths", ref usedeaths);
            CheckCfg("Categories - Barrels Destroyed", ref usebarrelsdestroyed);
            CheckCfg("Categories - Heli Hits", ref usehelihits);
            CheckCfg("Categories - Heli Kills", ref usehelikills);
            CheckCfg("Categories - Suicides", ref usesuicides);
            CheckCfg("Categories - Times Wounded", ref usetimeswounded);
            CheckCfg("Categories - Explosives Thrown", ref useexplosivesthrown);
            CheckCfg("Categories - Arrows Fired", ref usearrowsfired);
            CheckCfg("Categories - Bullets Fired", ref usebulletsfired);
            CheckCfg("Categories - Turrets Destroyed", ref useturretsdestroyed);
            CheckCfg("Categories - Sleepers Killed", ref usesleeperskilled);
            CheckCfg("Categories - NPCs Killed", ref usenpcskilled);
            CheckCfg("Categories - Rockets Launched", ref userocketslaunched);
            CheckCfg("Categories - Times Healed", ref usetimeshealed);
            CheckCfg("Categories - Structures Built", ref usestructuresbuilt);
            CheckCfg("Categories - Items Deployed", ref useitemsdeployed);
            CheckCfg("Categories - Items Crafted", ref useitemscrafted);
            CheckCfg("Categories - Structures Repaired", ref useentitiesrepaired);
            CheckCfg("Categories - Structures Demolished", ref usestructuresdemolished);
            CheckCfg("Categories - Resources Gathered", ref useresourcesgathered);
            CheckCfg("Categories - Structures Upgraded", ref usestructuresupgraded);
            CheckCfg("Categories - Kills To Deaths Ratio", ref usekdr);
            CheckCfg("Categories - Suicides To Deaths Ratio", ref usesdr);

            CheckCfg("MySQL - Use MySQL", ref useMySQL);
            CheckCfg("MySQL - Host", ref sql_host);
            CheckCfg("MySQL - Port", ref sql_port);
            CheckCfg("MySQL - Database Name", ref sql_db);
            CheckCfg("MySQL - Username", ref sql_user);
            CheckCfg("MySQL - Password", ref sql_pass);
            CheckCfg("MySQL - Table Name", ref tablename);
            
        }
          
        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        #endregion

        #region classes and data storage

        void SaveData()
        {
            foreach(var entry in data.PlayerRankData)   
            {
                entry.Value.Status = "offline";
            }
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if (data.PlayerRankData.ContainsKey(player.userID))
                {  
                    data.PlayerRankData[player.userID].Status = "online";
                    var time = PlaytimeTracker?.Call("GetPlayTime", player.UserIDString); 
                    if (time is double)
                    {
                        var playTime = GetPlaytimeClock((double)time);
                        if (!string.IsNullOrEmpty(playTime))
                            data.PlayerRankData[player.userID].TimePlayed = playTime;
                    }
                }
            }
            PRData.WriteObject(data); //silently saves the database at other times, such as pr del...pr wipe etc
        }

        void LoadData()
        {
            try
            {
            data = Interface.GetMod().DataFileSystem.ReadObject<DataStorage>("PlayerRanks");
            }

            catch
            {
            data = new DataStorage();
            }
        }
    
        #endregion

        #region messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "PlayerRanks: " },
            {"wipe", "PlayerRanks database wiped."},
            {"save", "PlayerRanks database saved."},
            {"del", "PlayerRanks for this player were wiped."},
            {"prtop", "/pr tops" },
            {"prtop2", "Displays all of your top stats." },
            {"prcat", "/pr *category*" },
            {"prcat2", "Displays top stats for the given category." }, 
            {"bestHits", "Top " },
            {"playername", "{0} :" },
            {"topstat", " {1}" },
            {"dbremoved", "Details for this ID have been removed." },           
            {"noentry", "There is no entry in the databse for this ID." },
            {"syntax", "ID must be 17 digits." },
            {"category", "Stats for this category have been removed." },
            {"nocategory", "This is not a recognised category." },           
            
            {"PVPKills", "PVP Kills " }, 
            {"PVPDistance", "PVP Distance " },
            {"PVEKills", "PVE Kills " },
            {"PVEDistance", "PVE Distance " },
            {"Deaths", "Deaths " },
            {"BarrelsDestroyed", "Barrels Destroyed " },
            {"HeliHits", "Heli Hits " },
            {"HeliKills", "Heli Kills " },
            {"Suicides", "Suicides " },
            {"TimesWounded", "Times Wounded " },
            {"ExplosivesThrown", "Explosives Thrown " },
            {"ArrowsFired", "Arrows Fired " },
            {"BulletsFired", "Bullets Fired " },
            {"TurretsDestroyed", "Turrets Destroyed " },
            {"SleepersKilled", "Sleepers Killed " },
            {"NPCsKilled", "NPCs Killed " },            
            {"RocketsLaunched", "Rockets Launched " },
            {"TimesHealed", "Times Healed " },
            {"KDR", "KDR " },
            {"SDR", "SDR " },
            //intense options
            {"StructuresBuilt", "Structures Built " },
            {"ItemsDeployed", "Items Deployed " },
            {"ItemsCrafted", "Items Crafted " },
            {"EntitiesRepaired", "Entities Repaired " },
            {"StructuresDemolished", "Structures Demolished " },
            {"ResourcesGathered", "Resources Gathered " },
            {"StructuresUpgraded", "Structures Upgraded " }, 
  
        };
        #endregion
    }
}