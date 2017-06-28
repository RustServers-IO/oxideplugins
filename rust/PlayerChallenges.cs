using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("PlayerChallenges", "k1lly0u", "2.0.2", ResourceId = 1442)]
    class PlayerChallenges : RustPlugin
    {
        #region Fields
        [PluginReference] Plugin BetterChat;
        [PluginReference] Plugin EventManager;
        [PluginReference] Plugin LustyMap;
        [PluginReference] Plugin Clans;
        [PluginReference] Plugin Friends;        

        ChallengeData chData;
        private DynamicConfigFile data;

        private Dictionary<ulong, StatData> statCache = new Dictionary<ulong, StatData>();
        private Dictionary<Challenges, LeaderData> titleCache = new Dictionary<Challenges, LeaderData>();
        private Dictionary<ulong, WoundedData> woundedData = new Dictionary<ulong, WoundedData>();

        private bool UIDisabled = false;
        #endregion

        #region UI Creation
        class PCUI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "Overlay",
                    panelName
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 0f)
            {               
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 0f)
            {                
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = fadein },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }            
        }
        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"dark", "0.1 0.1 0.1 0.98" },
            {"light", "0.7 0.7 0.7 0.3" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttonopen", "0.2 0.8 0.2 0.9" },
            {"buttoncompleted", "0 0.5 0.1 0.9" },
            {"buttonred", "0.85 0 0.35 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"grey8", "0.8 0.8 0.8 1.0" }
        };
        #endregion

        #region UI Leaderboard
        static string UIMain = "PCUI_Main";
        static string UIPanel = "PCUI_Panel";
        private void CreateMenu(BasePlayer player)
        {
            CloseMap(player);
            CuiHelper.DestroyUi(player, UIPanel);
            var MenuElement = PCUI.CreateElementContainer(UIMain, UIColors["dark"], "0 0", "1 1", true);
            PCUI.CreatePanel(ref MenuElement, UIMain, UIColors["light"], "0.005 0.93", "0.995 0.99");
            PCUI.CreateLabel(ref MenuElement, UIMain, "", $"<color={configData.Colors.MSG_ColorMain}>{MSG("UITitle").Replace("{Version}", Version.ToString())}</color>", 22, "0.05 0.93", "0.6 0.99", TextAnchor.MiddleLeft);
            
            CuiHelper.AddUi(player, MenuElement);
            CreateMenuContents(player, 0);
        }
        private void CreateMenuContents(BasePlayer player, int page = 0)
        {
            var MenuElement = PCUI.CreateElementContainer(UIPanel, "0 0 0 0", "0 0", "1 1");
            var elements = configData.ChallengeSettings.Where(x => x.Value.Enabled).OrderByDescending(x => x.Value.UIPosition).Reverse().ToArray();
            int count = page * 5;
            int number = 0;
            float dimension = 0.19f;
            for (int i = count; i < count + 5; i++)
            {
                if (elements.Length < i + 1) continue;
                float leftPos = 0.005f + (number * (dimension + 0.01f));
                AddMenuStats(ref MenuElement, UIPanel, elements[i].Key, leftPos, 0.01f, leftPos + dimension, 0.92f);
                number++;
            }

            if (page > 0) PCUI.CreateButton(ref MenuElement, UIPanel, UIColors["buttonbg"], "Previous", 16, "0.63 0.94", "0.73 0.98", $"PCUI_ChangePage {page - 1}");
            if (page < 3 && elements.Length > count + 5) PCUI.CreateButton(ref MenuElement, UIPanel, UIColors["buttonbg"], "Next", 16, "0.74 0.94", "0.84 0.98", $"PCUI_ChangePage {page + 1}");
            PCUI.CreateButton(ref MenuElement, UIPanel, UIColors["buttonbg"], "Close", 16, "0.85 0.94", "0.95 0.98", "PCUI_DestroyAll");
            CuiHelper.AddUi(player, MenuElement);
        }
        private void AddMenuStats(ref CuiElementContainer MenuElement, string panel, Challenges type, float left, float bottom, float right, float top)
        {
            if (configData.ChallengeSettings[type].Enabled)
            {
                PCUI.CreatePanel(ref MenuElement, UIPanel, UIColors["light"], $"{left} {bottom}", $"{right} {top}");
                PCUI.CreateLabel(ref MenuElement, UIPanel, "", GetLeaders(type), 16, $"{left + 0.005f} {bottom + 0.01f}", $"{right - 0.005f} {top - 0.01f}", TextAnchor.UpperLeft);
            }       
        }

        #region UI Commands       
        [ConsoleCommand("PCUI_ChangePage")]
        private void cmdChangePage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, UIPanel);
            var page = int.Parse(arg.GetString(0));
            CreateMenuContents(player, page);
        }
        [ConsoleCommand("PCUI_DestroyAll")]
        private void cmdDestroyAll(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;           
            DestroyUI(player);
            OpenMap(player);
        }
        #endregion

        #region UI Functions
        private string GetLeaders(Challenges type)
        {
            var listNames = $" -- <color={configData.Colors.MSG_ColorMain}>{MSG(type.ToString()).ToUpper()}</color>\n\n";

            var userStats = new List<KeyValuePair<string, int>>();

            foreach (var entry in statCache)
            {
                var name = entry.Value.DisplayName;                
                userStats.Add(new KeyValuePair<string, int>(name, entry.Value.Stats[type]));
            }                

            var leaders = userStats.OrderByDescending(a => a.Value).Take(25);

            int i = 1;

            foreach (var entry in leaders)
            {
                listNames += $"{i}.  - <color={configData.Colors.MSG_ColorMain}>{entry.Value}</color> -  {entry.Key}\n";
                i++;            
            }
            return listNames;
        }
        private object GetTypeFromString(string name)
        {
            foreach(var type in typeList)
            {
                if (type.ToString() == name)
                    return type;
            }
            return null;
        }
        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.DestroyUi(player, UIPanel);
        }
        #endregion
        #endregion

        #region External Calls
        private void CloseMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("DisableMaps", player);
            }
        }
        private void OpenMap(BasePlayer player)
        {
            if (LustyMap)
            {
                LustyMap.Call("EnableMaps", player);
            }
        }
        private bool IsPlaying(BasePlayer player)
        {
            if (EventManager)
            {
                var isPlaying = EventManager.Call("isPlaying", player);
                if (isPlaying is bool && (bool)isPlaying)
                    return true;
            }
            return false;
        }
        private bool IsClanmate(ulong playerId, ulong friendId)
        {
            if (!Clans) return false;
            object playerTag = Clans?.Call("GetClanOf", playerId);
            object friendTag = Clans?.Call("GetClanOf", friendId);
            if (playerTag is string && friendTag is string)
                if (playerTag == friendTag) return true;
            return false;
        }
        private bool IsFriend(ulong playerId, ulong friendId)
        {
            if (!Friends) return false;
            object isFriend = Friends?.Call("IsFriend", playerId, friendId);
            if (isFriend is bool && (bool)isFriend)
                return true;
            return false;            
        }
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("challenge_data");
            lang.RegisterMessages(Messages, this);
        }
        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            CheckValidData();

            RegisterTitles();
            RegisterGroups();
            AddAllUsergroups();  
                      
            SaveLoop();
            
            if (configData.Options.UseUpdateTimer)
                CheckUpdateTimer();
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);                   
        }        
        void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
            RemoveAllUsergroups();
        }
        void OnPluginLoaded(Plugin plugin)
        {
            if (plugin?.Title == "BetterChat")
                RegisterTitles();
        }
        void OnPlayerInit(BasePlayer player)
        {
            if (statCache.ContainsKey(player.userID))
            {
                if (statCache[player.userID].DisplayName != player.displayName)
                    statCache[player.userID].DisplayName = player.displayName;                             
            }
        }
        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (player == null || !configData.ChallengeSettings[Challenges.RocketsFired].Enabled) return;            
            AddPoints(player, Challenges.RocketsFired, 1);
        }
        void OnHealingItemUse(HeldEntity item, BasePlayer target)
        {
            var player = item.GetOwnerPlayer();
            if (player == null) return;
            if (player != target && configData.ChallengeSettings[Challenges.PlayersHealed].Enabled)
            {
                AddPoints(player, Challenges.PlayersHealed, 1);
            }            
        }
        void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            var player = task.owner;
            if (player == null) return;

            if (item.info.category == ItemCategory.Attire && configData.ChallengeSettings[Challenges.ClothesCrafted].Enabled)
                AddPoints(player, Challenges.ClothesCrafted, 1);
            if (item.info.category == ItemCategory.Weapon && configData.ChallengeSettings[Challenges.WeaponsCrafted].Enabled)
                AddPoints(player, Challenges.WeaponsCrafted, 1);
        }
        void OnPlantGather(PlantEntity plant, Item item, BasePlayer player)
        {
            if (player == null || !configData.ChallengeSettings[Challenges.PlantsGathered].Enabled) return;
            AddPoints(player, Challenges.PlantsGathered, 1);
        }
        void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            if (item == null) return;
            if (player == null || !configData.ChallengeSettings[Challenges.PlantsGathered].Enabled) return;
            if (plantShortnames.Contains(item?.info?.shortname))
                AddPoints(player, Challenges.PlantsGathered, 1);
        }
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null || dispenser == null) return;

            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree && configData.ChallengeSettings[Challenges.WoodGathered].Enabled)
                AddPoints(player, Challenges.WoodGathered, item.amount);

            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore && configData.ChallengeSettings[Challenges.RocksGathered].Enabled)
                AddPoints(player, Challenges.RocksGathered, item.amount);               
        }
        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            if (player == null || !configData.ChallengeSettings[Challenges.StructuresBuilt].Enabled) return;

            AddPoints(player, Challenges.StructuresBuilt, 1);
        }
        void CanBeWounded(BasePlayer player, HitInfo hitInfo)
        {
            if (player == null || hitInfo == null) return;

            var attacker = hitInfo.InitiatorPlayer;
            if (attacker != null)
            {
                if (attacker == player || IsPlaying(attacker) || IsFriend(attacker.userID, player.userID) || IsClanmate(attacker.userID, player.userID)) return;
                woundedData[player.userID] = new WoundedData {distance = Vector3.Distance(player.transform.position, attacker.transform.position), attackerId = attacker.userID };
            }            
        }
        void OnPlayerRecover(BasePlayer player)
        {
            if (woundedData.ContainsKey(player.userID))
                woundedData.Remove(player.userID);
        }
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                var attacker = info?.InitiatorPlayer;
                if (attacker == null) return;
                CheckEntry(attacker);
                if (entity is BasePlayer)
                {
                    var victim = entity.ToPlayer();

                    if (attacker == victim || IsPlaying(attacker) || IsFriend(attacker.userID, victim.userID) || IsClanmate(attacker.userID, victim.userID) || (configData.Options.IgnoreSleepers && victim.IsSleeping())) return;

                    var distance = Vector3.Distance(attacker.transform.position, entity.transform.position);
                    if (woundedData.ContainsKey(victim.userID))
                    {
                        var woundData = woundedData[victim.userID];
                        if (attacker.userID == woundData.attackerId)
                            distance = woundData.distance;
                        woundedData.Remove(victim.userID);
                    }
                    AddDistance(attacker, Challenges.PVPKillDistance, (int)distance);

                    if (info.isHeadshot && configData.ChallengeSettings[Challenges.Headshots].Enabled)
                        AddPoints(attacker, Challenges.Headshots, 1);
                    var weapon = info?.Weapon?.GetItem()?.info?.shortname;
                    if (!string.IsNullOrEmpty(weapon))
                    {
                        if (bladeShortnames.Contains(weapon) && configData.ChallengeSettings[Challenges.BladeKills].Enabled)
                            AddPoints(attacker, Challenges.BladeKills, 1);
                        else if (meleeShortnames.Contains(weapon) && configData.ChallengeSettings[Challenges.MeleeKills].Enabled)
                            AddPoints(attacker, Challenges.MeleeKills, 1);
                        else if (weapon == "bow.hunting" && configData.ChallengeSettings[Challenges.ArrowKills].Enabled)
                            AddPoints(attacker, Challenges.ArrowKills, 1);
                        else if (weapon == "pistol.revolver" && configData.ChallengeSettings[Challenges.RevolverKills].Enabled)
                            AddPoints(attacker, Challenges.RevolverKills, 1);
                        else if (configData.ChallengeSettings[Challenges.PlayersKilled].Enabled) AddPoints(attacker, Challenges.PlayersKilled, 1);
                    }
                }
                else if (entity.GetComponent<BaseNpc>() != null)
                {
                    var distance = Vector3.Distance(attacker.transform.position, entity.transform.position);
                    AddDistance(attacker, Challenges.PVEKillDistance, (int)distance);
                    AddPoints(attacker, Challenges.AnimalKills, 1);
                }
            }
            catch { }          
        }
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null || !configData.ChallengeSettings[Challenges.ExplosivesThrown].Enabled) return;
            if (entity.ShortPrefabName == "survey_charge.deployed" && configData.Options.IgnoreSurveyCharges) return;
            if (entity.ShortPrefabName == "grenade.smoke.deployed" && configData.Options.IgnoreSupplySignals) return;
            AddPoints(player, Challenges.ExplosivesThrown, 1);
        }
        void OnStructureRepair(BaseCombatEntity block, BasePlayer player)
        {
            if (player == null || !configData.ChallengeSettings[Challenges.StructuresRepaired].Enabled) return;
            if (block.health < block.MaxHealth())
                AddPoints(player, Challenges.StructuresRepaired, 1);
        }
        #endregion

        #region Hooks
        [HookMethod("CompletedQuest")]
        public void CompletedQuest(BasePlayer player)
        {
            CheckEntry(player);
            AddPoints(player, Challenges.QuestsCompleted, 1);            
        }
        #endregion

        #region Functions        
        private void AddPoints(BasePlayer player, Challenges type, int amount)
        {
            if (configData.Options.IgnoreAdmins && player.IsAdmin) return;
            CheckEntry(player);
            statCache[player.userID].Stats[type] += amount;            
            CheckForUpdate(player, type);
        }
        private void AddDistance(BasePlayer player, Challenges type, int amount)
        {
            if (configData.Options.IgnoreAdmins && player.IsAdmin) return;
            CheckEntry(player);
            if (statCache[player.userID].Stats[type] < amount)
                statCache[player.userID].Stats[type] = amount;
            CheckForUpdate(player, type);
        }
        private void CheckForUpdate(BasePlayer player, Challenges type)
        {
            if (titleCache[type].UserID == player.userID)
            {
                titleCache[type].Count = statCache[player.userID].Stats[type];
                return;
            }
            if (!configData.Options.UseUpdateTimer)
            {
                if (statCache[player.userID].Stats[type] > titleCache[type].Count)
                {
                    SwitchLeader(player.userID, titleCache[type].UserID, type);
                }
            }         
        }
        private void SwitchLeader(ulong newId, ulong oldId, Challenges type)
        {
            var name = GetGroupName(type);

            if (configData.Options.UseOxideGroups)
            {      
                if (oldId != 0U && permission.GroupExists(name))
                    RemoveUserFromGroup(name, oldId.ToString());
                if (newId != 0U && permission.GroupExists(name))
                    AddUserToGroup(name, newId.ToString());                
            }

            titleCache[type] = new LeaderData
            {
                Count = statCache[newId].Stats[type],
                DisplayName = statCache[newId].DisplayName,
                UserID = newId
            };

            if (configData.Options.AnnounceNewLeaders)
            {
                string message = MSG("newLeader")
                    .Replace("{playername}", $"<color={configData.Colors.MSG_ColorMain}>{statCache[newId].DisplayName}</color><color={configData.Colors.MSG_ColorMsg}>")
                    .Replace("{ctype}", $"</color><color={configData.Colors.MSG_ColorMain}>{MSG(type.ToString())}</color>");
                PrintToChat(message);
            }            
        }
      
        private void CheckUpdateTimer()
        {
            if ((GrabCurrentTime() - chData.LastUpdate) > configData.Options.UpdateTimer)
            {
                var updates = new Dictionary<Challenges, UpdateInfo>();
                foreach (var type in typeList)
                {
                    bool hasChanged = false;
                    UpdateInfo info = new UpdateInfo
                    {
                        newId = titleCache[type].UserID,
                        oldId = titleCache[type].UserID,
                        count = titleCache[type].Count
                    };
                    foreach (var player in statCache)
                    {
                        if (info.oldId == player.Key) continue;
                        if (player.Value.Stats[type] > info.count)
                        {
                            hasChanged = true;
                            info.newId = player.Key;
                            info.count = player.Value.Stats[type];
                        }
                    }
                    if (hasChanged)
                        SwitchLeader(info.newId, info.oldId, type);
                }               
            }
            else
            {
                var timeRemaining = ((configData.Options.UpdateTimer - (GrabCurrentTime() - chData.LastUpdate)) * 60) * 60;
                timer.Once((int)timeRemaining + 10, () => CheckUpdateTimer());
            }
        }
        class UpdateInfo
        {
            public ulong newId;
            public ulong oldId;
            public int count;
        }
        #endregion

        #region Chat Commands
        [ChatCommand("pc")]
        private void cmdPC(BasePlayer player, string command, string[] args)
        {
            if (!UIDisabled)
                CreateMenu(player);
            else SendReply(player, MSG("UIDisabled", player.UserIDString));
        }
        [ChatCommand("pc_wipe")]
        private void cmdPCWipe(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            RemoveAllUsergroups();
            titleCache = new Dictionary<Challenges, LeaderData>();
            statCache = new Dictionary<ulong, StatData>();            
            CheckValidData();
            SendReply(player, MSG("dataWipe", player.UserIDString));
            SaveData();
        }
        [ConsoleCommand("pc_wipe")]
        private void ccmdPCWipe(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null) return;
            RemoveAllUsergroups();
            titleCache = new Dictionary<Challenges, LeaderData>();
            statCache = new Dictionary<ulong, StatData>();
            CheckValidData();
            SendReply(arg, MSG("dataWipe"));
            SaveData();
        }
        #endregion

        #region Helper Methods
        private void CheckEntry(BasePlayer player)
        {
            if (!statCache.ContainsKey(player.userID))
            {
                statCache.Add(player.userID, new StatData
                {
                    DisplayName = player.displayName,
                    Stats = new Dictionary<Challenges, int>()
                });
                foreach (var type in typeList)
                    statCache[player.userID].Stats.Add(type, 0);
            }
        }
        private string GetGroupName(Challenges type) => configData.ChallengeSettings[type].Title;        
        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).Hours;
        #endregion

        #region Titles and Groups
        private void RegisterGroups()
        {
            if (!configData.Options.UseOxideGroups) return;           
            foreach (var type in typeList)
                RegisterGroup(type);
        }

        private void RegisterGroup(Challenges type)
        {
            var name = GetGroupName(type);
            if (!permission.GroupExists(name))
            {
                permission.CreateGroup(name, string.Empty, 0);                
            }
        }
        private void RegisterTitles()
        {
            if (!configData.Options.UseBetterChat || !BetterChat)
                return;                    
            BetterChat?.Call("API_RegisterThirdPartyTitle", new object[] { this, new Func<IPlayer, string>(GetPlayerTitles) });
        }
        private string GetPlayerTitles(IPlayer player)
        {
            if (!configData.Options.UseBetterChat) return string.Empty;
            string playerTitle = string.Empty;
            int count = 0;
            var titles = titleCache.OrderByDescending(x => configData.ChallengeSettings[x.Key].Priority).Reverse();
            foreach (var title in titles)
            {
                if (!configData.ChallengeSettings[title.Key].Enabled) continue;
                if (title.Value.UserID == 0U) continue;
                if (title.Value.UserID.ToString() == player.Id)
                {
                    playerTitle += $"{(count > 0 ? " " : "")}{configData.Options.TagFormat.Replace("{TAG}", GetGroupName(title.Key))}";
                    count++;
                    if (count >= configData.Options.MaximumTags)
                        break;
                }
            }
            return count == 0 ? string.Empty : $"[{configData.Colors.TitleColor}]{playerTitle}[/#]";
        }
        private void AddAllUsergroups()
        {
            if (configData.Options.UseOxideGroups)
            {
                foreach (var type in titleCache)
                {
                    var name = GetGroupName(type.Key);
                    if (titleCache[type.Key].UserID == 0 || !GroupExists(name)) continue;
                    if (!UserInGroup(name, titleCache[type.Key].UserID.ToString()))
                        AddUserToGroup(name, titleCache[type.Key].UserID.ToString());
                }
            }
        }
        private void RemoveAllUsergroups()
        {
            if (configData.Options.UseOxideGroups)
            {
                foreach (var type in titleCache)
                {
                    var name = GetGroupName(type.Key);
                    if (titleCache[type.Key].UserID == 0 || !GroupExists(name)) continue;
                    if (UserInGroup(name, titleCache[type.Key].UserID.ToString()))
                        RemoveUserFromGroup(name, titleCache[type.Key].UserID.ToString());
                }
            }
        }
        private bool GroupExists(string name) => permission.GroupExists(name);
        private bool UserInGroup(string name, string playerId) => permission.UserHasGroup(playerId, name);
        private void AddUserToGroup(string name, string playerId) => permission.AddUserGroup(playerId, name);
        private void RemoveUserFromGroup(string name, string playerId) => permission.RemoveUserGroup(playerId, name);
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public Dictionary<Challenges, ChallengeInfo> ChallengeSettings { get; set; }           
            public Options Options { get; set; } 
            public Colors Colors { get; set; }           
        }       
       
        class ChallengeInfo
        {
            public string Title;
            public bool Enabled;
            public int UIPosition;
            public int Priority;
        }
        class Options
        {
            public bool IgnoreSleepers;
            public bool UseBetterChat;
            public bool IgnoreAdmins;
            public bool IgnoreEventKills;
            public bool IgnoreSupplySignals;
            public bool IgnoreSurveyCharges;
            public bool AnnounceNewLeaders;
            public bool UseUpdateTimer;
            public bool UseOxideGroups;
            public int UpdateTimer;
            public int MaximumTags;
            public int SaveTimer;
            public string TagFormat;
        }
        class Colors
        {
            public string MSG_ColorMain;
            public string MSG_ColorMsg;
            public string TitleColor;
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                ChallengeSettings = new Dictionary<Challenges, ChallengeInfo>
                {
                    {
                        Challenges.AnimalKills, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Hunter",
                            UIPosition = 0,
                            Priority = 5
                        }
                    },
                    {
                        Challenges.ArrowKills, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Archer",
                            UIPosition = 1,
                            Priority = 11
                        }
                    },
                    {
                        Challenges.StructuresBuilt, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Architect",
                            UIPosition = 2,
                            Priority = 12
                        }
                    },
                    {
                        Challenges.ClothesCrafted, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Tailor",
                            UIPosition = 3,
                            Priority = 19
                        }
                    },
                    {
                        Challenges.ExplosivesThrown, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Bomb-tech",
                            UIPosition = 4,
                            Priority = 10
                        }
                    },
                    {
                        Challenges.Headshots, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Assassin",
                            UIPosition = 5,
                            Priority = 1
                        }
                    },
                    {
                        Challenges.PlayersHealed, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Medic",
                            UIPosition = 6,
                            Priority = 18
                        }
                    },
                    {
                        Challenges.PlayersKilled, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Murderer",
                            UIPosition = 7,
                            Priority = 2
                        }
                    },
                    {
                        Challenges.MeleeKills, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Fighter",
                            UIPosition = 8,
                            Priority = 3
                        }
                    },
                    {
                        Challenges.PlantsGathered, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Harvester",
                            UIPosition = 9,
                            Priority = 17
                        }
                    },
                    {
                        Challenges.PVEKillDistance, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Deadshot",
                            UIPosition = 10,
                            Priority = 6
                        }
                    },
                    {
                        Challenges.PVPKillDistance, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Sniper",
                            UIPosition = 11,
                            Priority = 4
                        }
                    },
                    {
                        Challenges.StructuresRepaired, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Handyman",
                            UIPosition = 12,
                            Priority = 13
                        }
                    },
                    {
                        Challenges.RevolverKills, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Gunslinger",
                            UIPosition = 13,
                            Priority = 7
                        }
                    },
                    {
                        Challenges.RocketsFired, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Rocketeer",
                            UIPosition = 14,
                            Priority = 8
                        }
                    },
                    {
                        Challenges.RocksGathered, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Miner",
                            UIPosition = 15,
                            Priority = 16
                        }
                    },
                    {
                        Challenges.BladeKills, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "BladeKillsman",
                            UIPosition = 16,
                            Priority = 9
                        }
                    },
                    {
                        Challenges.WeaponsCrafted, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Gunsmith",
                            UIPosition = 17,
                            Priority = 14
                        }
                    },
                    {
                        Challenges.WoodGathered, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Lumberjack",
                            UIPosition = 18,
                            Priority = 15
                        }
                    },
                    {
                        Challenges.QuestsCompleted, new ChallengeInfo
                        {
                            Enabled = true,
                            Title = "Adventurer",
                            UIPosition = 19,
                            Priority = 20
                        }
                    }
                },

                Options = new Options
                {
                    AnnounceNewLeaders = false,
                    IgnoreAdmins = true,
                    IgnoreSleepers = true,
                    IgnoreSupplySignals = false,
                    IgnoreSurveyCharges = false,
                    IgnoreEventKills = true,
                    MaximumTags = 2,
                    SaveTimer = 600,
                    TagFormat = "[{TAG}]",
                    UseBetterChat = true,
                    UseOxideGroups = false,
                    UseUpdateTimer = false,
                    UpdateTimer = 168
                },
                Colors = new Colors
                {
                    MSG_ColorMain = "orange",
                    MSG_ColorMsg = "#939393",
                    TitleColor = "#88E188"
                }                
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);        
        #endregion

        #region Data Management
        void SaveLoop() => timer.Once(configData.Options.SaveTimer, () => { SaveData(); SaveLoop(); });
        void SaveData()
        {
            chData.Stats = statCache;
            chData.Titles = titleCache;
            data.WriteObject(chData);
        }
        void LoadData()
        {
            try
            {
                chData = data.ReadObject<ChallengeData>();
                statCache = chData.Stats;
                titleCache = chData.Titles;
            }
            catch
            {
                chData = new ChallengeData();
            }
        }
        void CheckValidData()
        {
            if (titleCache.Count < typeList.Count)
            {
                foreach (var type in typeList)
                {
                    if (!titleCache.ContainsKey(type))
                        titleCache.Add(type, new LeaderData());
                }
            }
            foreach(var player in statCache)
            {
                foreach(var type in typeList)
                {
                    if (!player.Value.Stats.ContainsKey(type))
                        player.Value.Stats.Add(type, 0);
                }
            }
        }
        class ChallengeData
        {
            public Dictionary<ulong, StatData> Stats = new Dictionary<ulong, StatData>();
            public Dictionary<Challenges, LeaderData> Titles = new Dictionary<Challenges, LeaderData>();
            public double LastUpdate = 0;
        }   
        class StatData
        {
            public string DisplayName = string.Empty;
            public Dictionary<Challenges, int> Stats = new Dictionary<Challenges, int>();
        }    
        class LeaderData
        {
            public ulong UserID = 0U;
            public string DisplayName = null;
            public int Count = 0;
        }
        class WoundedData
        {
            public float distance;
            public ulong attackerId;
        }
        enum Challenges
        {
            AnimalKills, ArrowKills, ClothesCrafted, Headshots, PlantsGathered, PlayersHealed, PlayersKilled, MeleeKills, RevolverKills, RocketsFired, RocksGathered, BladeKills, StructuresBuilt, StructuresRepaired, ExplosivesThrown, WeaponsCrafted, WoodGathered, QuestsCompleted, PVPKillDistance, PVEKillDistance
        }

        #endregion

        #region Lists
        List<Challenges> typeList = new List<Challenges> { Challenges.AnimalKills, Challenges.ArrowKills, Challenges.ClothesCrafted, Challenges.Headshots, Challenges.PlantsGathered, Challenges.PlayersHealed, Challenges.PlayersKilled, Challenges.MeleeKills, Challenges.RevolverKills, Challenges.RocketsFired, Challenges.RocksGathered, Challenges.BladeKills, Challenges.StructuresBuilt, Challenges.StructuresRepaired, Challenges.ExplosivesThrown, Challenges.WeaponsCrafted, Challenges.WoodGathered, Challenges.QuestsCompleted, Challenges.PVEKillDistance, Challenges.PVPKillDistance };
        List<string> meleeShortnames = new List<string> { "bone.club", "hammer.salvaged", "hatchet", "icepick.salvaged", "knife.bone", "mace", "machete", "pickaxe", "rock", "stone.pickaxe", "stonehatchet", "torch" };
        List<string> bladeShortnames = new List<string> { "salvaged.sword", "salvaged.cleaver", "longsword", "axe.salvaged" };
        List<string> plantShortnames = new List<string> { "pumpkin", "cloth", "corn", "mushroom", "seed.hemp", "seed.corn", "seed.pumpkin" };
        #endregion

        #region Messaging
        private string MSG(string key, string id = null) => lang.GetMessage(key, this, id);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            {"newLeader", "{playername} has topped the leader board for most {ctype}" },
            {"AnimalKills", "animal kills" },
            {"ArrowKills", "kills with arrows" },
            {"ClothesCrafted", "clothes crafted" },
            {"Headshots", "headshots" },
            {"PlantsGathered", "plants gathered" },
            {"PlayersHealed", "players healed" },
            {"PlayersKilled", "players killed" },
            {"MeleeKills", "melee kills" },
            {"RevolverKills", "revolver kills" },
            {"RocketsFired", "rockets fired" },
            {"RocksGathered", "ore gathered" },
            {"BladeKills", "blade kills" },
            {"StructuresBuilt", "structures built" },
            {"StructuresRepaired", "structures repaired" },
            {"ExplosivesThrown", "explosives thrown" },
            {"WeaponsCrafted", "weapons crafted" },
            {"WoodGathered", "wood gathered" },
            {"PVEKillDistance", "longest PVE kill"},
            {"PVPKillDistance", "longest PVP kill" },
            {"QuestsCompleted", "quests completed" },
            {"UITitle", "Player Challenges   v{Version}" },
            {"UIDisabled", "The UI has been disabled as there is a error in the config. Please contact a admin" },
            {"dataWipe", "You have wiped all player stats and titles" }
        };
        #endregion
    }
}
