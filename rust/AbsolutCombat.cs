﻿using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("AbsolutCombat", "Absolut", "2.3.2", ResourceId = 2103)]

    class AbsolutCombat : RustPlugin
    {
        #region Fields

        [PluginReference]
        Plugin EventManager, ServerRewards, Economics, BetterChat, ImageLibrary;

        Gear_Weapon_Data gwData;
        private DynamicConfigFile GWData;

        SavedPlayer playerData;
        private DynamicConfigFile PlayerData;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";

        private Dictionary<ulong, Timer> PlayerGearSetTimer = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, Timer> PlayerWeaponSetTimer = new Dictionary<ulong, Timer>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<ulong, PurchaseItem> PendingPurchase = new Dictionary<ulong, PurchaseItem>();
        private Dictionary<ulong, PurchaseItem> SetSelection = new Dictionary<ulong, PurchaseItem>();
        Dictionary<string, List<ulong>> ItemSkins = new Dictionary<string, List<ulong>>();

        private Dictionary<ulong, GearCollectionCreation> NewGearCollection = new Dictionary<ulong, GearCollectionCreation>();
        private Dictionary<ulong, WeaponCollectionCreation> NewWeaponCollection = new Dictionary<ulong, WeaponCollectionCreation>();

        private Dictionary<ulong, string> SavingCollection = new Dictionary<ulong, string>();

        private Dictionary<ulong, screen> ACUIInfo = new Dictionary<ulong, screen>();
        class screen
        {
            public bool open;
            public bool admin;
            public bool weapon;
            public bool gear;
            public string GearSet;
            public string WeaponSet;
            public int GearIndex;
            public int WeaponIndex;
            public int page;
        }

        //corpses///
        private readonly string corpsePrefab = "assets/prefabs/player/player_corpse.prefab";
        private uint corpsePrefabId;
        #endregion

        #region Hooks
        void Loaded()
        {
            GWData = Interface.Oxide.DataFileSystem.GetFile("AbsolutCombat_GWData");
            PlayerData = Interface.Oxide.DataFileSystem.GetFile("AbsolutCombat_PlayerData");
            lang.RegisterMessages(messages, this);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                    DestroyACPlayer(player);
            foreach (var timer in timers)
                timer.Value.Destroy();
            timers.Clear();
            SaveData();
        }

        void OnServerInitialized()
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning("No Image Library.. load ImageLibrary to use this Plugin", Name);
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            LoadVariables();
            if (configData.UseServerRewards)
            {
                try
                {
                    ServerRewards.Call("isLoaded", null);
                }
                catch (Exception)
                {
                    PrintWarning(GetMSG("NOSR", Name));
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            }
            if (configData.UseEconomics)
            {
                try
                {
                    Economics.Call("isLoaded", null);
                    if (configData.UseServerRewards && configData.UseEconomics)
                    {
                        PrintWarning(GetMSG("BOTHERROR", Name));
                        Interface.Oxide.UnloadPlugin(Name);
                        return;
                    }
                }
                catch (Exception)
                {
                    PrintWarning(GetMSG("NOECO", Name));
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            }
            LoadData();
            AddImage("http://i.imgur.com/OjuRPqa.png", "down", (ulong)ResourceId);
            AddImage("http://i.imgur.com/Eu9QHKr.png", "up", (ulong)ResourceId);
            InitializeSkins();
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                OnPlayerInit(p);
            }
            timers.Add("info", timer.Once(900, () => InfoLoop()));
            timers.Add("save", timer.Once(600, () => SaveLoop()));
            timers.Add("cond", timer.Once(120, () => CondLoop()));
        }


        private void InitializeSkins()
        {
            foreach (var itemDef in ItemManager.GetItemDefinitions())
            {
                List<ulong> skins;
                skins = new List<ulong> { 0 };
                skins.AddRange(ItemSkinDirectory.ForItem(itemDef).Select(skin => Convert.ToUInt64(skin.id)));
                List<ulong> templist = GetImageList(itemDef.shortname);
                if (templist != null && templist.Count >= 1)
                    foreach (var entry in templist.Where(k => !skins.Contains(k)))
                        skins.Add(entry);
                ItemSkins.Add(itemDef.shortname, skins);
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                player.Command($"bind {configData.MenuKeyBinding} \"OpenACUI\"");
                InitializeACPlayer(player);
                GetSendMSG(player, "ACInfo", configData.MenuKeyBinding.ToUpper());
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (EventManager)
            {
                object isPlaying = EventManager?.Call("isPlaying", new object[] { player });
                if (isPlaying is bool)
                    if ((bool)isPlaying)
                        return;
            }
            DestroyUI(player);
            if (!playerData.players.ContainsKey(player.userID))
            {
                InitializeACPlayer(player);
                return;
            }
            CheckCollections(player);
                GiveGearCollection(player);
                GiveWeaponCollection(player);
            player.health = 100f;
        }

        private bool DataCheck (BasePlayer player)
        {
            if (!playerData.players.ContainsKey(player.userID))
                return false;
            return true;
        }

        private void OnEntityDeath(BaseEntity entity, HitInfo hitInfo)
        {
            try
            {
                var attacker = hitInfo.Initiator.ToPlayer() as BasePlayer;
                var victim = entity.ToPlayer();
                if (entity is BasePlayer && hitInfo.Initiator is BasePlayer)
                {
                    if (entity as BasePlayer == null || hitInfo == null) return;
                    if (!DataCheck(attacker) || !DataCheck(victim)) return;
                    if (victim.userID != attacker.userID)
                    {
                        if (EventManager)
                        {
                            object isPlaying = EventManager?.Call("isPlaying", new object[] { attacker });
                            if (isPlaying is bool)
                                if ((bool)isPlaying)
                                    return;
                        }
                        playerData.players[attacker.userID].kills += 1;
                        playerData.players[attacker.userID].TotalKills += 1;
                        playerData.players[victim.userID].deaths += 1;
                        playerData.players[victim.userID].TotalDeaths += 1;
                        if (configData.UseServerRewards)
                            SRAction(attacker.userID, configData.KillReward, "ADD");
                        else if (configData.UseEconomics)
                                ECOAction(attacker.userID, configData.KillReward, "ADD");
                            else
                            playerData.players[attacker.userID].money += configData.KillReward;
                        playerData.players[attacker.userID].GearCollectionKills[playerData.players[attacker.userID].Gear.collectionname] += 1;
                        playerData.players[attacker.userID].WeaponCollectionKills[playerData.players[attacker.userID].Weapons.collectionname] += 1;
                        SendDeathNote(attacker, victim);
                        PlayerHUD(attacker);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void SRAction(ulong ID, int amount, string action)
        {
            if (action == "ADD")
                ServerRewards?.Call("AddPoints", new object[] { ID, amount });
            if (action == "REMOVE")
                ServerRewards?.Call("TakePoints", new object[] { ID, amount });
        }

        private void ECOAction(ulong ID, int amount, string action)
        {
            if (action == "ADD")
                Economics.Call("DepositS", ID.ToString(), amount);
            if (action == "REMOVE") 
                Economics.Call("WithdrawS", ID.ToString(), amount);
        }

        object OnBetterChat(IPlayer iplayer, string message)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return message;
            if (SavingCollection.ContainsKey(player.userID))
            {
                CollectionCreationChat(player, message.Split(' '));
                return true;
            }
            return message;
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChat) return null;
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return null;
            if (SavingCollection.ContainsKey(player.userID))
            {
                CollectionCreationChat(player, arg.Args);
                return true;
            }
            return null;
        }

        private void CollectionCreationChat(BasePlayer player, string[] Args)
        {
            if (Args.Contains("quit"))
            {
                ExitSetCreation(player);
                return;
            }
            var args = string.Join(" ", Args);
            var name = args;
            if (SavingCollection[player.userID] == "gear")
            NewGearCollection[player.userID].setname = name;
            else if (SavingCollection[player.userID] == "weapon")
                NewWeaponCollection[player.userID].setname = name;
            SaveCollect(player);
        }

        //void OnEntitySpawned(BaseNetworkable entity)
        //{
        //    if (entity != null)
        //    {
        //        corpsePrefabId = StringPool.Get(corpsePrefab);
        //        if (configData.UseEnviroControl)
        //        {
        //            if (entity.prefabID == corpsePrefabId)
        //            {
        //                entity.Kill();
        //            }
        //            var collectible = entity as CollectibleEntity;
        //            if (collectible != null)
        //            {
        //                collectible.itemList = null;
        //            }
        //            var worldItem = entity as WorldItem;
        //            if (worldItem != null)
        //            {
        //                worldItem.allowPickup = false;
        //            }
        //            var Heli = entity as BaseHelicopter;
        //            if (Heli != null)
        //            {
        //                Heli.Kill();
        //            }
        //            var Plane = entity as CargoPlane;
        //            if (Plane != null)
        //            {
        //                Plane.Kill();
        //            }
        //        }
        //    }
        //}

        //private void OnLootEntity(BasePlayer looter, BaseEntity target)
        //{
        //    if (configData.UseEnviroControl && !isAuth(looter))
        //    {
        //        if ((target as StorageContainer)?.transform.position == Vector3.zero) return;
        //        timer.Once(0.01f, looter.EndLooting);
        //    }
        //}

        //void OnPlantGather(PlantEntity Plant, Item item, BasePlayer player)
        //{
        //    if (configData.UseEnviroControl && !isAuth(player))
        //    {
        //        item.amount = 0;
        //    }
        //}


        //void OnCollectiblePickup(Item item, BasePlayer player)
        //{
        //    if (configData.UseEnviroControl && !isAuth(player))
        //    {
        //        item.amount = 0;
        //    }
        //}
        //void OnDispenserGather(ResourceDispenser Dispenser, BaseEntity entity, Item item)
        //{
        //    BasePlayer player = entity.ToPlayer();
        //    if (configData.UseEnviroControl && !isAuth(player))
        //    {
        //        item.amount = 0;
        //    }
        //}

        //object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        //{
        //    if (configData.UseEnviroControl && !isAuth(crafter))
        //    {
        //        task.cancelled = true;
        //    }
        //    return null;
        //}
        #endregion

        #region Functions
        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (shortname.Contains("http")) return shortname;
            if (skin == 99) skin = (ulong)ResourceId;
            return GetImage(shortname, skin, true);
        }

        public string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname.ToLower(), skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname.ToLower(), skin);
        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary.Call("GetImageList", shortname.ToLower());
        public bool isReady() => (bool)ImageLibrary?.Call("IsReady");

        private void InitializeACPlayer(BasePlayer player)
        {
            if (!playerData.players.ContainsKey(player.userID))
                playerData.players.Add(player.userID, new Player { deaths = 0, kills = 0, TotalDeaths = 0, TotalKills = 0, money = 0, GearCollectionKills = new Dictionary<string, int>(), WeaponSelection = new Dictionary<string, Dictionary<string, List<string>>>(), Gear = new CurrentGear(), Weapons = new CurrentWeapons(), GearCollections = new Dictionary<string, List<string>>(), WeaponCollections = new Dictionary<string, Dictionary<string, List<string>>>(), WeaponCollectionKills = new Dictionary<string, int>() });
            else
            {
                playerData.players[player.userID].kills = 0;
                playerData.players[player.userID].deaths = 0;
            }
            CheckCollections(player);
        }

        void CheckCollections(BasePlayer player)
        {
            if (!playerData.players.ContainsKey(player.userID)) { InitializeACPlayer(player); return; }
            //if (gwData.GearSets != null || gwData.GearSets.Count() == 0)
            foreach (var entry in gwData.GearSets.Where(kvp => kvp.Value.cost == 0))
                    if (!playerData.players[player.userID].GearCollections.ContainsKey(entry.Key))
                    {
                        playerData.players[player.userID].GearCollections.Add(entry.Key, new List<string>());
                        playerData.players[player.userID].GearCollectionKills.Add(entry.Key, 0);
                        foreach (var gear in gwData.GearSets[entry.Key].set.Where(kvp => kvp.free == true))
                            playerData.players[player.userID].GearCollections[entry.Key].Add(gear.shortname);
                    }
            //if (gwData.WeaponSets != null || gwData.WeaponSets.Count() == 0)
                foreach (var entry in gwData.WeaponSets.Where(kvp => kvp.Value.cost == 0))
                    if (!playerData.players[player.userID].WeaponCollections.ContainsKey(entry.Key))
                    {
                        playerData.players[player.userID].WeaponCollections.Add(entry.Key, new Dictionary<string, List<string>>());
                        playerData.players[player.userID].WeaponCollectionKills.Add(entry.Key, 0);
                    foreach (var weapons in gwData.WeaponSets[entry.Key].set.Where(kvp => kvp.free == true))
                    {
                        playerData.players[player.userID].WeaponCollections[entry.Key].Add(weapons.shortname, new List<string>());
                        foreach (var attachment in weapons.attachments.Where(kvp=>kvp.Value.free == true))
                            playerData.players[player.userID].WeaponCollections[entry.Key][weapons.shortname].Add(attachment.Value.shortname);
                    }
                }
            List<string> gearset = new List<string>();
            List<string> weaponset = new List<string>();
            //Puts("1");
            foreach (var entry in playerData.players[player.userID].GearCollections)
                if (!gwData.GearSets.ContainsKey(entry.Key))
                {
                    gearset.Add(entry.Key);
                    if (playerData.players[player.userID].Gear.collectionname == entry.Key)
                    {
                        StripGear(player);
                        playerData.players[player.userID].Gear = null;
                    }
                }
            //Puts("2");
            foreach (var entry in playerData.players[player.userID].WeaponCollections)
                if (!gwData.WeaponSets.ContainsKey(entry.Key))
                {
                    weaponset.Add(entry.Key);
                    if (playerData.players[player.userID].Weapons.collectionname == entry.Key)
                    {
                        StripWeapons(player);
                        playerData.players[player.userID].Weapons = null;
                    }
                }
            //Puts("3");
            if (gearset != null)
                foreach (var entry in gearset)
                {
                    playerData.players[player.userID].GearCollections.Remove(entry);
                    playerData.players[player.userID].GearCollectionKills.Remove(entry);
                }
            //Puts("4");
            if (weaponset != null)
                foreach (var entry in weaponset)
                {
                    playerData.players[player.userID].WeaponCollections.Remove(entry);
                    playerData.players[player.userID].WeaponCollectionKills.Remove(entry);
                }
            //Puts("5");
            if (playerData.players[player.userID].Gear != null)
            {
                if (string.IsNullOrEmpty(playerData.players[player.userID].Gear.collectionname)) playerData.players[player.userID].Gear.gear = null;
                else if (!gwData.GearSets.ContainsKey(playerData.players[player.userID].Gear.collectionname))
                {
                    playerData.players[player.userID].Gear.collectionname = null;
                    playerData.players[player.userID].Gear.gear = null;
                }
                if (string.IsNullOrEmpty(playerData.players[player.userID].Gear.collectionname) && playerData.players[player.userID].GearCollections.Count() > 0)
                {
                    playerData.players[player.userID].Gear.collectionname = playerData.players[player.userID].GearCollections.First().Key;
                        GiveGearCollection(player);
                }
            }
            //Puts("6");
            if (playerData.players[player.userID].Weapons != null)
            {
                if (string.IsNullOrEmpty(playerData.players[player.userID].Weapons.collectionname)) playerData.players[player.userID].Weapons.weapons = null;
                else if (!gwData.WeaponSets.ContainsKey(playerData.players[player.userID].Weapons.collectionname))
                {
                    playerData.players[player.userID].Weapons.collectionname = null;
                    playerData.players[player.userID].Weapons.weapons = null;
                }
                if (string.IsNullOrEmpty(playerData.players[player.userID].Weapons.collectionname) && playerData.players[player.userID].WeaponCollections.Count() > 0 || playerData.players[player.userID].Weapons.weapons == null && playerData.players[player.userID].WeaponCollections.Count() > 0)
                {
                    if (playerData.players[player.userID].WeaponSelection.ContainsKey(playerData.players[player.userID].WeaponCollections.First().Key))
                    {
                        playerData.players[player.userID].Weapons.collectionname = "N00b";
                        playerData.players[player.userID].WeaponSelection.Add(playerData.players[player.userID].Weapons.collectionname, new Dictionary<string, List<string>>());
                        foreach (var entry in playerData.players[player.userID].WeaponCollections[playerData.players[player.userID].Weapons.collectionname])
                            playerData.players[player.userID].WeaponSelection[playerData.players[player.userID].Weapons.collectionname].Add(entry.Key, new List<string>());
                            GiveWeaponCollection(player);
                    }
                }
            }
            //Puts("7");
            PlayerHUD(player);
        }

        private object CheckPoints(ulong ID) => ServerRewards?.Call("CheckPoints", ID);

        void DestroyACPlayer(BasePlayer player)
        {
            if (player == null) return;
            {
                DestroyUI(player);
                player.Command($"bind {configData.MenuKeyBinding} \"\"");
                player.Command($"bind tab \"inventory.toggle\"");
                ACUIInfo.Remove(player.userID);
            }
        }


        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyACPlayer(player);
            SaveData();
        }

        private string GetLang(string msg)
        {
            if (messages.ContainsKey(msg))
                return lang.GetMessage(msg, this);
            else return msg;
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(GetLang(message), arg1, arg2, arg3);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private string GetMSG(string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this), arg1, arg2, arg3);
            return msg;
        }

        static void TPPlayer(BasePlayer player, Vector3 destination)
        {
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "StartLoading", null, null, null, null, null);
            StartSleeping(player);
            player.MovePosition(destination);
            if (player.net?.connection != null)
                player.ClientRPCPlayer(null, player, "ForcePositionTo", destination);
            if (player.net?.connection != null)
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate(false);
            if (player.net?.connection == null) return;
            try { player.ClearEntityQueue(null); } catch { }
            player.SendFullSnapshot();
        }
        static void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
                return;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
                BasePlayer.sleepingPlayerList.Add(player);
            player.CancelInvoke("InventoryUpdate");
        }

        [ConsoleCommand("OpenACUI")]
        private void cmdOpenACUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (ACUIInfo.ContainsKey(player.userID))
                if (!ACUIInfo[player.userID].open)
                {
                    ACUIInfo[player.userID].open = true;
                    OpenACUI(player);
                }
                else
                    DestroyACPanel(player);
            else
                OpenACUI(player);
        }

        private void OpenACUI(BasePlayer player)
        {
            if (!ACUIInfo.ContainsKey(player.userID))
                ACUIInfo.Add(player.userID, new screen { admin = false, open = true, GearIndex = 0, GearSet = "", WeaponIndex = 0, WeaponSet = "", page = 0});
            ACPanel(player);
            GearListPanel(player);
            WeaponListPanel(player);
            if (ACUIInfo[player.userID].weapon)
                WeaponPanel(player);
            else if (ACUIInfo[player.userID].gear)
                GearPanel(player);
        }


        public void DestroyUI(BasePlayer player)
        {
            DestroyACPanel(player);
            CuiHelper.DestroyUi(player, PanelOnScreen);
            CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
            CuiHelper.DestroyUi(player, PanelStats);
        }


        [ConsoleCommand("UI_DestroyACPanel")]
        private void cmdUI_DestroyACPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyACPanel(player);
        }

        public void DestroyACPanel(BasePlayer player)
        {
            if (ACUIInfo.ContainsKey(player.userID))
                ACUIInfo[player.userID].open = false;
            CuiHelper.DestroyUi(player, PanelAC);
            CuiHelper.DestroyUi(player, GPanel);
            CuiHelper.DestroyUi(player, WPanel);
            CuiHelper.DestroyUi(player, GLPanel);
            CuiHelper.DestroyUi(player, WLPanel);
            CuiHelper.DestroyUi(player, APanel);
        }

        public void Broadcast(string message, string userid = "0") => PrintToChat(message);

        private void SendDeathNote(BasePlayer player, BasePlayer victim)
        {
            string colorAttacker = "";
            string colorVictim = "";

            colorAttacker = "<color=#e60000>";
            colorVictim = "<color=#3366ff>";
            if (configData.BroadcastDeath)
            {
                string formatMsg = colorAttacker + player.displayName + "</color>" + GetLang("DeathMessage") + colorVictim + victim.displayName + "</color>";
                Broadcast(formatMsg);
            }
        }


        private void SaveCollect(BasePlayer player)
        {
            if (!SavingCollection.ContainsKey(player.userID)) return;
            var index = 0;
            var name = "";
            bool used = true;
            var type = SavingCollection[player.userID];
            if (type == "gear")
            {
                if (gwData.GearSets.Count == 0)
                    index = 0;
                else
                {
                    List<int> AllIndexes = new List<int>();
                    foreach (var entry in gwData.GearSets) AllIndexes.Add(entry.Value.index);
                        while (used == true)
                    {
                        if (AllIndexes.Contains(index)) index++;
                        else used = false;
                    }
                }
                List<Gear> gearlist = new List<Gear>();
                foreach (var entry in NewGearCollection[player.userID].collection.set)
                    gearlist.Add(entry.Value);
                name = NewGearCollection[player.userID].setname;
                var newset = new GearSet { cost = NewGearCollection[player.userID].collection.cost, killsrequired = NewGearCollection[player.userID].collection.killsrequired, set = gearlist, index = index };
                gwData.GearSets.Add(name, newset);
                NewGearCollection.Remove(player.userID);
            }
            else if(type == "weapon")
            {
                if (gwData.WeaponSets.Count == 0)
                    index = 0;
                else
                {
                    List<int> AllIndexes = new List<int>();
                    foreach (var entry in gwData.WeaponSets) AllIndexes.Add(entry.Value.index);
                    while (used == true)
                    {
                        if (AllIndexes.Contains(index)) index++;
                        else used = false;
                    }
                }
                List<Weapon> gearlist = new List<Weapon>();
                foreach (var entry in NewWeaponCollection[player.userID].collection.set)
                    gearlist.Add(entry.Value);
                name = NewWeaponCollection[player.userID].setname;
                var newset = new WeaponSet { cost = NewWeaponCollection[player.userID].collection.cost, killsrequired = NewWeaponCollection[player.userID].collection.killsrequired, set = gearlist, index = index };
                gwData.WeaponSets.Add(name, newset);
                NewWeaponCollection.Remove(player.userID);
            }
            SavingCollection.Remove(player.userID);
            GetSendMSG(player, "NewCollectionCreated", type.ToUpper(), name);
            DestroyACPanel(player);
            SaveData();
            foreach (var p in BasePlayer.activePlayerList)
                CheckCollections(p);
        }


        private void ExitSetCreation(BasePlayer player)
        {
            if (!SavingCollection.ContainsKey(player.userID)) return;
            SavingCollection.Remove(player.userID);
            DestroyACPanel(player);
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 1)
                    return false;
            return true;
        }

        private List<BasePlayer> FindPlayer(string arg)
        {
            var foundPlayers = new List<BasePlayer>();
            ulong steamid;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (var p in BasePlayer.activePlayerList)
                if (p != null)
                {
                    if (steamid != 0L)
                        if (p.userID == steamid)
                        {
                            foundPlayers.Add(p);
                            return foundPlayers;
                        }
                    string lowername = p.displayName.ToLower();
                    if (lowername.Contains(lowerarg))
                        foundPlayers.Add(p);
                }
            return foundPlayers;
        }

        [ConsoleCommand("addmoney")]
        private void cmdaddmoney(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                if (arg.Connection.authLevel < 1)
                {
                    if (arg.Connection.player != null)
                    {
                        var player = arg.Connection.player as BasePlayer;
                        GetSendMSG(player, "NotAuthorized");
                    }
                    return;
                }
            chatAddMoney(null, "addmoney", arg.Args);
        }

        [ConsoleCommand("takemoney")]
        private void cmdtakemoney(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                if (arg.Connection.authLevel < 1)
                {
                    if (arg.Connection.player != null)
                    {
                        var player = arg.Connection.player as BasePlayer;
                        GetSendMSG(player, "NotAuthorized");
                    }
                    return;
                }
            chattakemoney(null, "takemoney", arg.Args);
        }

        [ConsoleCommand("addkills")]
        private void cmdaddkills(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                if (arg.Connection.authLevel < 1)
                {
                    if (arg.Connection.player != null)
                    {
                        var player = arg.Connection.player as BasePlayer;
                        GetSendMSG(player, "NotAuthorized");
                    }
                    return;
                }
            chatAddKills(null, "addkills", arg.Args);
        }

        [ConsoleCommand("takekills")]
        private void cmdtakekills(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                if (arg.Connection.authLevel < 1)
                {
                    if (arg.Connection.player != null)
                    {
                        var player = arg.Connection.player as BasePlayer;
                        GetSendMSG(player, "NotAuthorized");
                    }
                    return;
                }
            chatTakeKills(null, "takekills", arg.Args);
        }

        [ChatCommand("addmoney")]
        private void chatAddMoney(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                if (!isAuth(player))
                {
                    GetSendMSG(player, "NotAuthorized");
                    return;
                }
            }
            if (args.Length == 2)
            {
                int amount;
                if (!int.TryParse(args[1], out amount))
                {
                    if (player == null)
                        Puts(GetMSG("INVALIDENTRY", args[1]));
                    else
                        GetSendMSG(player, "INVALIDENTRY", args[1]);
                    return;
                }
                var partialPlayerName = args[0];
                var foundPlayers = FindPlayer(partialPlayerName);
                if (foundPlayers.Count() == 0)
                {
                    if (player == null)
                        Puts(GetMSG("NoPlayers", args[0]));
                    else
                        GetSendMSG(player, "NoPlayers", args[0]);
                    return;
                }
                if (foundPlayers.Count() > 1)
                {
                    if (player == null)
                        Puts(GetMSG("MultiplePlayers", args[0]));
                    else
                        GetSendMSG(player, "MultiplePlayers", args[0]);
                    return;
                }
                if (foundPlayers[0] != null)
                {
                    ulong requestor = 0;
                    if (player != null)
                        requestor = player.userID;
                    AddMoney(foundPlayers[0].userID, amount, true, requestor);
                    PlayerHUD(foundPlayers[0]);
                }
            }
            else
            {
                if (player == null)
                    Puts(GetMSG("ArgumentsIncorrect", "/addmoney PLAYERNAME AMOUNT", "/addmoney Absolut 20"));
                else
                    GetSendMSG(player, "ArgumentsIncorrect", "/addmoney PLAYERNAME AMOUNT", "/addmoney Absolut 20");
            }
        }

        [ChatCommand("addkills")]
        private void chatAddKills(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                if (!isAuth(player))
                {
                    GetSendMSG(player, "NotAuthorized");
                    return;
                }
            }
            if (args.Length == 4)
            {
                int amount;
                if (!int.TryParse(args[3], out amount))
                {
                    if (player == null)
                        Puts(GetMSG("INVALIDENTRY", args[1]));
                    else
                        GetSendMSG(player, "INVALIDENTRY", args[1]);
                    return;
                }
                var partialPlayerName = args[0];
                var type = args[1];
                var collection = args[2];
                var foundPlayers = FindPlayer(partialPlayerName);
                if (foundPlayers.Count() == 0)
                {
                    if (player == null)
                        Puts(GetMSG("NoPlayers", args[0]));
                    else
                        GetSendMSG(player, "NoPlayers", args[0]);
                    return;
                }
                if (foundPlayers.Count() > 1)
                {
                    if (player == null)
                        Puts(GetMSG("MultiplePlayers", args[0]));
                    else
                        GetSendMSG(player, "MultiplePlayers", args[0]);
                    return;
                }
                if (foundPlayers[0] != null)
                {
                    ulong requestor = 0;
                    if (player != null)
                        requestor = player.userID;
                    if (type == "weapon")
                    {
                        if (gwData.WeaponSets.ContainsKey(collection) && playerData.players[foundPlayers[0].userID].WeaponCollections.ContainsKey(collection))
                            AddKills(foundPlayers[0].userID, amount, type, collection, requestor);
                        }
                    else if (type == "gear")
                    {
                            if (gwData.GearSets.ContainsKey(collection) && playerData.players[foundPlayers[0].userID].GearCollections.ContainsKey(collection))
                                AddKills(foundPlayers[0].userID, amount, type, collection, requestor);
                        }
                    PlayerHUD(foundPlayers[0]);
                }
            }
            else
            {
                if (player == null)
                    Puts(GetMSG("ArgumentsIncorrect", "/addkills PLAYERNAME TYPE COLLECTION AMOUNT", "/addkills Absolut weapon starter 20"));
                else
                    GetSendMSG(player, "ArgumentsIncorrect", "/addkills PLAYERNAME TYPE COLLECTION AMOUNT", "/addkills Absolut weapon starter 20");
            }
        }

        [ChatCommand("takekills")]
        private void chatTakeKills(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                if (!isAuth(player))
                {
                    GetSendMSG(player, "NotAuthorized");
                    return;
                }
            }
            if (args.Length == 4)
            {
                int amount;
                if (!int.TryParse(args[3], out amount))
                {
                    if (player == null)
                        Puts(GetMSG("INVALIDENTRY", args[1]));
                    else
                        GetSendMSG(player, "INVALIDENTRY", args[1]);
                    return;
                }
                var partialPlayerName = args[0];
                var type = args[1];
                var collection = args[2];
                var foundPlayers = FindPlayer(partialPlayerName);
                if (foundPlayers.Count() == 0)
                {
                    if (player == null)
                        Puts(GetMSG("NoPlayers", args[0]));
                    else
                        GetSendMSG(player, "NoPlayers", args[0]);
                    return;
                }
                if (foundPlayers.Count() > 1)
                {
                    if (player == null)
                        Puts(GetMSG("MultiplePlayers", args[0]));
                    else
                        GetSendMSG(player, "MultiplePlayers", args[0]);
                    return;
                }
                if (foundPlayers[0] != null)
                {
                    ulong requestor = 0;
                    if (player != null)
                        requestor = player.userID;
                    if (type == "weapon")
                    {
                        if (gwData.WeaponSets.ContainsKey(collection) && playerData.players[foundPlayers[0].userID].WeaponCollections.ContainsKey(collection))
                            TakeKills(foundPlayers[0].userID, amount, type, collection, requestor);
                    }
                    else if (type == "gear")
                    {
                        if (gwData.GearSets.ContainsKey(collection) && playerData.players[foundPlayers[0].userID].GearCollections.ContainsKey(collection))
                            TakeKills(foundPlayers[0].userID, amount, type, collection, requestor);
                    }
                    PlayerHUD(foundPlayers[0]);
                }
            }
            else
            {
                if (player == null)
                    Puts(GetMSG("ArgumentsIncorrect", "/takekills PLAYERNAME TYPE COLLECTION AMOUNT", "/takekills Absolut weapon starter 20"));
                else
                    GetSendMSG(player, "ArgumentsIncorrect", "/takekills PLAYERNAME TYPE COLLECTION AMOUNT", "/takekills Absolut weapon starter 20");
            }
        }


        [ChatCommand("takemoney")]
        private void chattakemoney(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                if (!isAuth(player))
                {
                    GetSendMSG(player, "NotAuthorized");
                    return;
                }
            }
            if (args.Length == 2)
            {
                int amount;
                if (!int.TryParse(args[1], out amount))
                {
                    if (player == null)
                        Puts(GetMSG("INVALIDENTRY", args[1]));
                    else
                        GetSendMSG(player, "INVALIDENTRY", args[1]);
                    return;
                }
                var partialPlayerName = args[0];
                var foundPlayers = FindPlayer(partialPlayerName);
                if (foundPlayers.Count() == 0)
                {
                    if (player == null)
                        Puts(GetMSG("NoPlayers", args[0]));
                    else
                        GetSendMSG(player, "NoPlayers", args[0]);
                    return;
                }
                if (foundPlayers.Count() > 1)
                {
                    if (player == null)
                        Puts(GetMSG("MultiplePlayers", args[0]));
                    else
                        GetSendMSG(player, "MultiplePlayers", args[0]);
                    return;
                }
                if (foundPlayers[0] != null)
                {
                    ulong requestor = 0;
                    if (player != null)
                        requestor = player.userID;
                    TakeMoney(foundPlayers[0].userID, amount, true, requestor);
                    PlayerHUD(foundPlayers[0]);
                }
            }
            else
            {
                if (player == null)
                    Puts(GetMSG("ArgumentsIncorrect", "/takemoney PLAYERNAME AMOUNT", "/takemoney Absolut 20"));
                else
                    GetSendMSG(player, "ArgumentsIncorrect", "/takemoney PLAYERNAME AMOUNT", "/takemoney Absolut 20");
            }
        }

        #endregion

        #region UI Creation
        private string PanelAC = "PanelAC";
        private string GLPanel = "GLPanel";
        private string WLPanel = "WLPanel";
        private string GPanel = "GPanel";
        private string WPanel = "WPanel";
        private string APanel = "APanel";
        private string PanelOnScreen = "OnScreen";
        private string PanelPurchaseConfirmation = "PurchaseConfirmation";
        private string PanelStats = "StatsPanel";

        public class UI
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
                    new CuiElement().Parent,
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
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (img.StartsWith("http") || img.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOverlay(ref CuiElementContainer container, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                //if (configdata.DisableUI_FadeIn)
                //    fadein = 0;
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }
            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string DistanceA, string DistanceB, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = DistanceA + " " + DistanceB, Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "1 1 1 0.3" },
            {"light", ".564 .564 .564 1.0" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"brown", "0.3 0.16 0.0 1.0" },
            {"yellow", "0.9 0.9 0.0 1.0" },
            {"orange", "1.0 0.65 0.0 1.0" },
            {"blue", "0.2 0.6 1.0 1.0" },
            {"red", "1.0 0.1 0.1 1.0" },
            {"white", "1 1 1 1" },
            {"limegreen", "0.42 1.0 0 1.0" },
            {"green", "0.28 0.82 0.28 1.0" },
            {"grey", "0.85 0.85 0.85 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"customblue", "0.454 0.77 1.0 1.0" },
            {"CSorange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> TextColors = new Dictionary<string, string>
        {
            {"limegreen", "<color=#6fff00>" }
        };


        private Dictionary<Slot, Vector2> LeftGearSlotPos = new Dictionary<Slot, Vector2>
        {
            { Slot.head, new Vector2(.4f, .65f) },
            { Slot.chest, new Vector2(.27f, .45f) },
            { Slot.legs, new Vector2(.27f, .25f) },
            { Slot.hands, new Vector2(.27f, .05f) },
        };

        private Dictionary<Slot, Vector2> RightGearSlotPos = new Dictionary<Slot, Vector2>
        {
            { Slot.chest2, new Vector2(.49f, .45f) },
            { Slot.legs2, new Vector2(.49f, .25f) },
            { Slot.feet, new Vector2(.49f, .05f) },
        };

        private Dictionary<Slot, Vector2> GearSlotPos = new Dictionary<Slot, Vector2>
        {
            { Slot.head, new Vector2(.4f, .65f) },
            { Slot.chest, new Vector2(.27f, .45f) },
            { Slot.chest2, new Vector2(.49f, .45f) },
            { Slot.legs, new Vector2(.27f, .25f) },
            { Slot.legs2, new Vector2(.49f, .25f) },
            { Slot.feet, new Vector2(.49f, .05f) },
            { Slot.hands, new Vector2(.27f, .05f) },
        };

        private Dictionary<Slot, Vector2> WeaponSlotPos = new Dictionary<Slot, Vector2>
        {
            { Slot.main, new Vector2(.27f, .45f) },
            { Slot.secondary, new Vector2(.49f, .45f) },
    };

        private Dictionary<Slot, Vector2> MainAttachmentSlotsPos = new Dictionary<Slot, Vector2>
        {
            { Slot.attachment1, new Vector2(.29f, .75f) },
            { Slot.attachment2, new Vector2(.29f, .45f) },
            { Slot.attachment3, new Vector2(.29f, .15f) },
    };

        private Dictionary<Slot, Vector2> SecondaryAttachmentSlotsPos = new Dictionary<Slot, Vector2>
        {
            { Slot.attachment1, new Vector2(.53f, .75f) },
            { Slot.attachment2, new Vector2(.53f, .45f) },
            { Slot.attachment3, new Vector2(.53f, .15f) },
    };
        private Dictionary<Slot, Vector2> AccessoriesSlotsPos = new Dictionary<Slot, Vector2>
        {
            { Slot.accessories1, new Vector2(.7f, .85f) },
            { Slot.accessories2, new Vector2(.7f, .7f) },
        };
        private Dictionary<Slot, Vector2> AmmunitionSlotsPos = new Dictionary<Slot, Vector2>
        {
            { Slot.ammunitionMain, new Vector2(.13f, 1.2f) },
            { Slot.ammunitionSecondary, new Vector2(.7f, 1.2f) },

        };
        #endregion 

        #region UI Panels

        void ACPanel(BasePlayer player)
        {
            //CuiHelper.DestroyUi(player, PanelAC);
            //var element = UI.CreateElementContainer(PanelAC, UIColors["dark"], "0.15 0.2", "0.87 0.8", true);
            //UI.CreatePanel(ref element, PanelAC, UIColors["header"], "0.01 0.02", "0.97 0.98");
            //RENDER IMAGE FROM TNT OR WHATEVER I USE...
            //CuiHelper.AddUi(player, element);
        }


        void GearListPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, GLPanel);
            var element = UI.CreateElementContainer(GLPanel, UIColors["dark"], "0.15 0.5", "0.25 0.8",true);
            UI.CreatePanel(ref element, GLPanel, UIColors["light"], $"0.05 0.03", $".95 .97");
            UI.CreateTextOutline(ref element, GLPanel, UIColors["white"], UIColors["black"], GetLang("GearCollection"), 14, "1", "1", "0.1 0.85", "0.9 0.94", TextAnchor.MiddleCenter);
            if (gwData.GearSets.Count() >= 1)
            {
                if (ACUIInfo[player.userID].GearIndex != 0)
                {
                    UI.LoadImage(ref element, GLPanel, TryForImage("up"), "0.25 0.9", "0.75 1");
                    UI.CreateButton(ref element, GLPanel, "0 0 0 0", "", 12, "0.25 0.9", "0.75 1", $"UI_GearIndexShownChange {ACUIInfo[player.userID].GearIndex - 1}");
                }
                if (ACUIInfo[player.userID].GearIndex + 6 < gwData.GearSets.Max(kvp => kvp.Value.index))
                {
                    UI.LoadImage(ref element, GLPanel, TryForImage("down"), "0.25 -.05", "0.75 0.05");
                    UI.CreateButton(ref element, GLPanel, "0 0 0 0", "", 12, "0.25 -.05", "0.75 0.05", $"UI_GearIndexShownChange {ACUIInfo[player.userID].GearIndex + 1}");
                }
                foreach (var entry in gwData.GearSets)
                {
                    if (entry.Value.index < ACUIInfo[player.userID].GearIndex) continue;
                    if (entry.Value.index > ACUIInfo[player.userID].GearIndex + 6) continue;
                    var pos = CalcSetButtons(entry.Value.index - ACUIInfo[player.userID].GearIndex);
                    if (ACUIInfo[player.userID].GearSet == entry.Key) UI.CreatePanel(ref element, GLPanel, UIColors["yellow"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    else if (playerData.players[player.userID].GearCollections.ContainsKey(entry.Key)) UI.CreatePanel(ref element, GLPanel, UIColors["green"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    else UI.CreatePanel(ref element, GLPanel, UIColors["red"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref element, GLPanel, "0 0 0 0", entry.Key, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeGearSet {entry.Key}", TextAnchor.MiddleCenter);
                }
            }
            if (ACUIInfo[player.userID].admin)
                if (!NewGearCollection.ContainsKey(player.userID))
                    UI.CreateButton(ref element, GLPanel, UIColors["blue"], GetLang("CreateCollection"), 10, "0.1 -0.11", "0.9 -0.01", $"UI_CreateGearSet");
            else
                    UI.CreateButton(ref element, GLPanel, UIColors["red"], GetLang("CancelCollection"), 10,"0.1 -0.11", "0.9 -0.01", $"UI_CancelGearSet");
            CuiHelper.AddUi(player, element);
        }

        void WeaponListPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, WLPanel);
            var element = UI.CreateElementContainer(WLPanel, UIColors["dark"], "0.25 0.5", "0.35 0.8", true);
            UI.CreatePanel(ref element, WLPanel, UIColors["light"], $"0.05 0.03", $".95 .97");
            UI.CreateTextOutline(ref element, WLPanel, UIColors["white"], UIColors["black"], GetLang("WeaponCollection"), 14, "1", "1","0.1 0.85", "0.9 0.94", TextAnchor.MiddleCenter);
            if (gwData.WeaponSets.Count() >= 1)
            {
                if (ACUIInfo[player.userID].WeaponIndex != 0)
                {
                    UI.LoadImage(ref element, WLPanel, TryForImage("up"), "0.25 0.9", "0.75 1");
                    UI.CreateButton(ref element, WLPanel, "0 0 0 0", "", 12, "0.25 0.9", "0.75 1", $"UI_WeaponIndexShownChange {ACUIInfo[player.userID].WeaponIndex - 1}");
                }
                if (ACUIInfo[player.userID].WeaponIndex + 6 < gwData.WeaponSets.Max(kvp => kvp.Value.index))
                {
                    UI.LoadImage(ref element, WLPanel, TryForImage("down"), "0.25 -.05", "0.75 0.05");
                    UI.CreateButton(ref element, WLPanel, "0 0 0 0", "", 12, "0.25 -.05", "0.75 0.05", $"UI_WeaponIndexShownChange {ACUIInfo[player.userID].WeaponIndex + 1}");
                }
                foreach (var entry in gwData.WeaponSets)
                {
                    if (entry.Value.index < ACUIInfo[player.userID].WeaponIndex) continue;
                    if (entry.Value.index > ACUIInfo[player.userID].WeaponIndex + 6) continue;
                    var pos = CalcSetButtons(entry.Value.index - ACUIInfo[player.userID].WeaponIndex);
                    if (ACUIInfo[player.userID].WeaponSet == entry.Key) UI.CreatePanel(ref element, WLPanel, UIColors["yellow"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    else if (playerData.players[player.userID].WeaponCollections.ContainsKey(entry.Key)) UI.CreatePanel(ref element, WLPanel, UIColors["green"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    else UI.CreatePanel(ref element, WLPanel, UIColors["red"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref element, WLPanel, "0 0 0 0", entry.Key, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_ChangeWeaponSet {entry.Key}", TextAnchor.MiddleCenter);
                }
            }
            if (isAuth(player))
                UI.CreateButton(ref element, WLPanel, UIColors["orange"], GetLang("ToggleAdminView"), 12, "0.1 1.01", "0.9 1.15", "UI_SwitchAdminView", TextAnchor.MiddleCenter);
            if (ACUIInfo[player.userID].admin)
                if (!NewWeaponCollection.ContainsKey(player.userID))
                    UI.CreateButton(ref element, WLPanel, UIColors["blue"], GetLang("CreateCollection"), 10, "0.1 -0.11", "0.9 -0.01", $"UI_CreateWeaponSet");
            else
                    UI.CreateButton(ref element, WLPanel, UIColors["red"], GetLang("CancelCollection"), 10, "0.1 -0.11", "0.9 -0.01", $"UI_CancelWeaponSet");
            CuiHelper.AddUi(player, element);
        }

        private float[] CalcSetButtons(int number)
        {
            Vector2 position = new Vector2(0.055f, 0.74f);
            Vector2 dimensions = new Vector2(0.87f, 0.1f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 7)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        void GearPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, GPanel);
            var element = UI.CreateElementContainer(GPanel, "0 0 0 0", "0.35 0.2", "0.75 0.8", true);
            if (NewGearCollection.ContainsKey(player.userID) && ACUIInfo[player.userID].admin)
            {
                Vector2 min = new Vector2(0f, 0f);
                Vector2 dimension = new Vector2(.2f, .15f);
                Vector2 offset2 = new Vector2(0.25f, 0f);
                Vector2 altmin;
                Vector2 max;
                Vector2 altmax;
                Dictionary<Slot, string> Usedslots = new Dictionary<Slot, string>();
                List<Slot> Unusedslots = new List<Slot>();
                foreach (var block in LeftGearSlotPos)
                {
                    foreach (var entry in NewGearCollection[player.userID].collection.set.Where(kvp => kvp.Value.slot == block.Key))
                        Usedslots.Add(entry.Value.slot, entry.Value.shortname);
                }
                foreach (var block in LeftGearSlotPos)
                    if (!Usedslots.ContainsKey(block.Key))
                        Unusedslots.Add(block.Key);
                foreach (var entry in Usedslots)
                {
                    min = LeftGearSlotPos[entry.Key];
                    max = min + dimension;
                    altmin = min - offset2;
                    altmax = altmin + dimension;

                    UI.LoadImage(ref element, GPanel, TryForImage(entry.Value, NewGearCollection[player.userID].collection.set[entry.Value].skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry.Key)} gear", TextAnchor.MiddleCenter);
                    if (NewGearCollection[player.userID].collection.set[entry.Value].free)
                    {
                        UI.CreatePanel(ref element, GPanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("Free"), 16, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                    }
                    else
                    {
                        if (NewGearCollection[player.userID].collection.set[entry.Value].price == 0 && NewGearCollection[player.userID].collection.set[entry.Value].killsrequired == 0)
                        {
                            UI.CreatePanel(ref element, GPanel, UIColors["red"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("ClickToDetail", NewGearCollection[player.userID].collection.set[entry.Value].price.ToString(), NewGearCollection[player.userID].collection.set[entry.Value].killsrequired.ToString()), 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        }
                        else
                        {
                            UI.CreatePanel(ref element, GPanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("ItemGearCost", NewGearCollection[player.userID].collection.set[entry.Value].price.ToString(), NewGearCollection[player.userID].collection.set[entry.Value].killsrequired.ToString()), 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        }
                    }
                    UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", $"UI_AddItemAttributes {entry.Value} gear", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, GPanel, UIColors["red"], GetLang("Remove"), 12, $"{altmin.x - .05f} {altmin.y + .05f}", $"{altmin.x} {altmax.y - .05f}", $"UI_RemoveItem {entry.Value} gear", TextAnchor.MiddleCenter);
                }
                foreach (var entry in Unusedslots)
                {
                    min = LeftGearSlotPos[entry];
                    max = min + dimension;
                    UI.CreatePanel(ref element, GPanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreatePanel(ref element, GPanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                    if (NewGearCollection[player.userID].collection.set.Count() < 6)
                        UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry)} gear", TextAnchor.MiddleCenter);
                    else UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("CollectionFull"), 12, "1", "1", $"{min.x} {min.y}", $"{max.x} {max.y}");
                }
                Usedslots.Clear();
                Unusedslots.Clear();
                foreach (var block in RightGearSlotPos)
                {
                    foreach (var entry in NewGearCollection[player.userID].collection.set.Where(kvp => kvp.Value.slot == block.Key))
                        Usedslots.Add(entry.Value.slot, entry.Value.shortname);
                }
                foreach (var block in RightGearSlotPos)
                    if (!Usedslots.ContainsKey(block.Key))
                        Unusedslots.Add(block.Key);
                foreach (var entry in Usedslots)
                {
                    min = RightGearSlotPos[entry.Key];
                    max = min + dimension;
                    altmin = min + offset2;
                    altmax = altmin + dimension;

                    UI.LoadImage(ref element, GPanel, TryForImage(entry.Value, NewGearCollection[player.userID].collection.set[entry.Value].skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry.Key)} gear", TextAnchor.MiddleCenter);
                    if (NewGearCollection[player.userID].collection.set[entry.Value].free)
                    {
                        UI.CreatePanel(ref element, GPanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("Free"), 16, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                    }
                    else
                    {
                        if (NewGearCollection[player.userID].collection.set[entry.Value].price == 0 && NewGearCollection[player.userID].collection.set[entry.Value].killsrequired == 0)
                        {
                            UI.CreatePanel(ref element, GPanel, UIColors["red"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("ClickToDetail", NewGearCollection[player.userID].collection.set[entry.Value].price.ToString(), NewGearCollection[player.userID].collection.set[entry.Value].killsrequired.ToString()), 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        }
                        else
                        {
                            UI.CreatePanel(ref element, GPanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("ItemGearCost", NewGearCollection[player.userID].collection.set[entry.Value].price.ToString(), NewGearCollection[player.userID].collection.set[entry.Value].killsrequired.ToString()), 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        }
                    }
                    UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", $"UI_AddItemAttributes {entry.Value} gear", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, GPanel, UIColors["red"], GetLang("Remove"), 12, $"{altmin.x + .2f} {altmin.y + .05f}", $"{altmin.x + .25f} {altmax.y - .05f}", $"UI_RemoveItem {entry.Value} gear", TextAnchor.MiddleCenter);
                }
                foreach (var entry in Unusedslots)
                {
                    min = RightGearSlotPos[entry];
                    max = min + dimension;
                    UI.CreatePanel(ref element, GPanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreatePanel(ref element, GPanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                    if (NewGearCollection[player.userID].collection.set.Count() < 6)
                        UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry)} gear", TextAnchor.MiddleCenter);
                    else UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("CollectionFull"), 12, "1", "1", $"{min.x} {min.y}", $"{max.x} {max.y}");
                }

                UI.CreateButton(ref element, GPanel, UIColors["green"], GetMSG("SaveCollection"), 18, "0.1 0.86", "0.9 0.91", $"UI_SaveCollect gear");
            }
            else
            {
                var set = ACUIInfo[player.userID].GearSet;
                if (!PendingPurchase.ContainsKey(player.userID))
                    PendingPurchase.Add(player.userID, new PurchaseItem { });
                else
                    PendingPurchase[player.userID].gear.Clear();
                var money = playerData.players[player.userID].money;
                if (configData.UseServerRewards)
                    if (CheckPoints(player.userID) is int)
                        money = (int)CheckPoints(player.userID);
                else if (configData.UseEconomics)
                    money = Convert.ToInt32(Economics.CallHook("GetPlayerMoney", player.userID));
                if (set == "") return;
                UI.CreateTextOutline(ref element, GPanel, UIColors["black"], UIColors["white"], GetMSG("BuySubMenu", set.ToUpper()), 20, "1", "1", "0.1 0.94", "0.9 0.99");
                foreach (var entry in gwData.GearSets.Where(kvp => kvp.Key == set))
                {
                    foreach (var block in LeftGearSlotPos)
                    {
                        var min = block.Value;
                        var max = block.Value + new Vector2(.2f, .15f);
                        UI.CreatePanel(ref element, GPanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                        UI.CreatePanel(ref element, GPanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                    }

                    foreach (var block in RightGearSlotPos)
                    {
                        var min = block.Value;
                        var max = block.Value + new Vector2(.2f, .15f);
                        UI.CreatePanel(ref element, GPanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                        UI.CreatePanel(ref element, GPanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                    }
                    foreach (var item in entry.Value.set)
                    {
                        string info = "";
                        PendingPurchase[player.userID].gear.Add(item.shortname, item);
                        Vector2 min = new Vector2(0f, 0f);
                        Vector2 dimension = new Vector2(.2f, .15f);
                        Vector2 offset2 = new Vector2(0.25f, 0f);
                        Vector2 altmin;

                        if (LeftGearSlotPos.ContainsKey(item.slot))
                        {
                            min = LeftGearSlotPos[item.slot];
                            altmin = min - offset2;
                        }
                        else if (RightGearSlotPos.ContainsKey(item.slot))
                        {
                            min = RightGearSlotPos[item.slot];
                            altmin = min + offset2;
                        }
                        else continue;
                        Vector2 max = min + dimension;
                        Vector2 altmax = altmin + dimension;


                        if (playerData.players[player.userID].GearCollections.ContainsKey(set))
                        {
                            if (playerData.players[player.userID].Gear.collectionname == set)
                            {
                                UI.CreateButton(ref element, GPanel, UIColors["green"], GetMSG("CurrentlyEquipped"), 18, "0.1 0.86", "0.9 0.91", $"UI_ProcessSelection set {set}");
                            }
                            else
                            {
                                UI.CreateButton(ref element, GPanel, UIColors["green"], GetMSG("SelectCollection", set.ToUpper()), 18, "0.1 0.86", "0.9 0.91", $"UI_ProcessSelection set {set}");
                            }
                            UI.CreateTextOutline(ref element, GPanel, UIColors["white"], UIColors["green"], GetMSG("CurrentGearKills", playerData.players[player.userID].GearCollectionKills[set].ToString()), 16, "1", "1", "0.1 0.81", "0.9 0.86");

                            var RequiredKills = playerData.players[player.userID].GearCollectionKills[set];
                            if (playerData.players[player.userID].GearCollections[set].Contains(item.shortname))
                            {
                                UI.LoadImage(ref element, GPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                UI.CreatePanel(ref element, GPanel, UIColors["green"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                info = GetLang("Owned");
                                UI.CreateLabel(ref element, GPanel, UIColors["white"], info, 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                            }
                            else if (money >= item.price && RequiredKills >= item.killsrequired)
                            {
                                UI.LoadImage(ref element, GPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                UI.LoadImage(ref element, GPanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                UI.CreatePanel(ref element, GPanel, UIColors["red"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                info = GetMSG("ItemGearCost", item.price.ToString(), item.killsrequired.ToString());
                                UI.CreateTextOutline(ref element, GPanel, UIColors["white"], UIColors["green"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                UI.CreateButton(ref element, GPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_PrepPurchase {item.shortname} gear", TextAnchor.MiddleCenter);
                            }
                            else
                            {
                                UI.LoadImage(ref element, GPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                UI.LoadImage(ref element, GPanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                UI.CreatePanel(ref element, GPanel, UIColors["grey"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                info = GetMSG("ItemGearCost", item.price.ToString(), item.killsrequired.ToString());
                                UI.CreateLabel(ref element, GPanel, UIColors["red"], info, 12, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                            }
                        }
                        else
                        {
                            UI.LoadImage(ref element, GPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                            UI.LoadImage(ref element, GPanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                            UI.CreatePanel(ref element, GPanel, UIColors["grey"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                            info = GetMSG("ItemGearCost", item.price.ToString(), item.killsrequired.ToString());
                            UI.CreateLabel(ref element, GPanel, UIColors["red"], info, 12, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);

                            if (money >= gwData.GearSets[set].cost && playerData.players[player.userID].kills >= gwData.GearSets[set].killsrequired)
                            {
                                UI.CreateButton(ref element, GPanel, UIColors["blue"], GetMSG("UnlockCollection", gwData.GearSets[set].cost.ToString()), 16, "0.1 0.86", "0.9 0.91", $"UI_PurchasingPanel gear {set}", TextAnchor.MiddleCenter);
                            }
                            else
                            {
                                if (playerData.players[player.userID].kills != 0)
                                {
                                    var percent = System.Convert.ToDouble(gwData.WeaponSets[set].killsrequired / playerData.players[player.userID].kills);
                                    if (percent * 100 > 75)
                                        UI.CreateTextOutline(ref element, GPanel, UIColors["white"], UIColors["yellow"], GetMSG("CostOfGC", gwData.GearSets[set].cost.ToString(), gwData.GearSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                    else if (percent * 100 > 25 && percent * 100 < 76)
                                        UI.CreateTextOutline(ref element, GPanel, UIColors["white"], UIColors["orange"], GetMSG("CostOfGC", gwData.GearSets[set].cost.ToString(), gwData.GearSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                    else if (percent * 100 > 0 && percent * 100 < 26)
                                        UI.CreateTextOutline(ref element, GPanel, UIColors["white"], UIColors["red"], GetMSG("CostOfGC", gwData.GearSets[set].cost.ToString(), gwData.GearSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                }
                                else UI.CreateTextOutline(ref element, GPanel, UIColors["white"], UIColors["red"], GetMSG("CostOfGC", gwData.GearSets[set].cost.ToString(), gwData.GearSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                            }
                        }
                    }
                }
                if (ACUIInfo[player.userID].admin)
                    UI.CreateButton(ref element, GPanel, UIColors["red"], GetLang("Delete"), 14, "0.05 0.8", "0.15 0.85", $"UI_DeleteGearSet");
            }
            CuiHelper.AddUi(player, element);
        }


        void WeaponPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, WPanel);
            var element = UI.CreateElementContainer(WPanel, "0 0 0 0", "0.35 0.2", "0.75 0.8", true);
            //Puts("STARTING - Weapons");
            if (NewWeaponCollection.ContainsKey(player.userID) && ACUIInfo[player.userID].admin)
            {
                Vector2 min = new Vector2(0f, 0f);
                Vector2 dimension = new Vector2(.2f, .15f);
                Vector2 offset2 = new Vector2(0f, .16f);
                Vector2 altmin;
                Vector2 max;
                Vector2 altmax;
                Dictionary<Slot, string> UsedWeaponslots = new Dictionary<Slot, string>();
                List<Slot> UnusedWeaponslots = new List<Slot>();
                foreach (var block in WeaponSlotPos)
                {
                    foreach (var entry in NewWeaponCollection[player.userID].collection.set.Where(kvp => kvp.Value.slot == block.Key))
                        UsedWeaponslots.Add(entry.Value.slot, entry.Value.shortname);
                }
                foreach (var block in WeaponSlotPos)
                    if (!UsedWeaponslots.ContainsKey(block.Key))
                        UnusedWeaponslots.Add(block.Key);
                foreach (var entry in UsedWeaponslots)
                {
                    min = WeaponSlotPos[entry.Key];
                    max = min + dimension;
                    altmin = min + offset2;
                    altmax = altmin + dimension;

                    UI.LoadImage(ref element, WPanel, TryForImage(entry.Value, NewWeaponCollection[player.userID].collection.set[entry.Value].skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreateButton(ref element, WPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry.Key)} weapon", TextAnchor.MiddleCenter);
                    if (NewWeaponCollection[player.userID].collection.set[entry.Value].free)
                    {
                        UI.CreatePanel(ref element, WPanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        UI.CreateTextOutline(ref element, WPanel, UIColors["black"], UIColors["white"], GetMSG("Free"), 16, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                    }
                    else
                    {
                        if (NewWeaponCollection[player.userID].collection.set[entry.Value].price == 0 && NewWeaponCollection[player.userID].collection.set[entry.Value].killsrequired == 0)
                        {
                            UI.CreatePanel(ref element, WPanel, UIColors["red"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            UI.CreateTextOutline(ref element, WPanel, UIColors["black"], UIColors["white"], GetMSG("ClickToDetail", NewWeaponCollection[player.userID].collection.set[entry.Value].price.ToString(), NewWeaponCollection[player.userID].collection.set[entry.Value].killsrequired.ToString()), 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        }
                        else
                        {
                            UI.CreatePanel(ref element, WPanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            UI.CreateTextOutline(ref element, WPanel, UIColors["black"], UIColors["white"], GetMSG("ItemWeaponCost", NewWeaponCollection[player.userID].collection.set[entry.Value].price.ToString(), NewWeaponCollection[player.userID].collection.set[entry.Value].killsrequired.ToString()), 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                        }
                    }
                    UI.CreateButton(ref element, WPanel, "0 0 0 0", "", 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", $"UI_AddItemAttributes {entry.Value} weapon", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, WPanel, UIColors["red"], GetLang("Remove"), 12, $"{altmin.x + .075f} {altmax.y}", $"{altmax.x - .075f} {altmax.y + .05f}", $"UI_RemoveItem {entry.Value} weapon", TextAnchor.MiddleCenter);
                }
                foreach (var entry in UnusedWeaponslots)
                {
                    min = WeaponSlotPos[entry];
                    max = min + dimension;
                    UI.CreatePanel(ref element, WPanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreatePanel(ref element, WPanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                    UI.CreateButton(ref element, WPanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry)} weapon", TextAnchor.MiddleCenter);
                }
                UI.CreateButton(ref element, WPanel, UIColors["green"], GetMSG("SaveCollection"), 18, "0.1 0.86", "0.9 0.91", $"UI_SaveCollect weapon");
            }
            else
            {
                //Puts("NON CREATION");
                var set = ACUIInfo[player.userID].WeaponSet;
                if (!PendingPurchase.ContainsKey(player.userID))
                    PendingPurchase.Add(player.userID, new PurchaseItem { });
                else
                    PendingPurchase[player.userID].weapon.Clear();
                //Puts("1");
                var money = playerData.players[player.userID].money;
                if (configData.UseServerRewards)
                {
                    if (CheckPoints(player.userID) is int)
                        money = (int)CheckPoints(player.userID);
                }
                else if (configData.UseEconomics)
                    money = Convert.ToInt32(Economics.CallHook("GetPlayerMoney", player.userID)); if (set == "") return;
                UI.CreateTextOutline(ref element, WPanel, UIColors["black"], UIColors["white"], GetMSG("BuySubMenu", set.ToUpper()), 20, "1", "1", "0.1 0.94", "0.9 0.99");
                //Puts("2");
                foreach (var block in WeaponSlotPos)
                {
                    var min = block.Value;
                    var max = block.Value + new Vector2(.2f, .15f);
                    UI.CreatePanel(ref element, WPanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                    UI.CreatePanel(ref element, WPanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                }
                //Puts("3");
                foreach (var entry in gwData.WeaponSets.Where(kvp => kvp.Key == set))
                {
                    if (playerData.players[player.userID].WeaponSelection == null)
                        playerData.players[player.userID].WeaponSelection.Add(entry.Key, new Dictionary<string, List<string>>());
                    else if (!playerData.players[player.userID].WeaponSelection.ContainsKey(set))
                    {
                        playerData.players[player.userID].WeaponSelection.Clear();
                        playerData.players[player.userID].WeaponSelection.Add(entry.Key, new Dictionary<string, List<string>>());
                    }
                    //Puts("4");
                    foreach (var item in entry.Value.set)
                    {
                        //Puts("5");
                        string info = "";
                        PendingPurchase[player.userID].weapon.Add(item.shortname, item);
                        Vector2 min = new Vector2(0f, 0f);
                        Vector2 dimension = new Vector2(.2f, .15f);
                        Vector2 offset2 = new Vector2(0f, .15f);

                        if (WeaponSlotPos.ContainsKey(item.slot))
                        {
                            min = WeaponSlotPos[item.slot];
                        }
                        Vector2 max = min + dimension;
                        Vector2 altmin = min + offset2;
                        Vector2 altmax = altmin + dimension;
                        if (playerData.players[player.userID].WeaponCollections != null)
                        {
                            //Puts("6");
                            if (playerData.players[player.userID].WeaponCollections.ContainsKey(set))
                            {
                                //Puts("7");
                                if (playerData.players[player.userID].WeaponCollections[set].ContainsKey(item.shortname))
                                {
                                    //Puts("8");
                                    if (!playerData.players[player.userID].WeaponSelection[entry.Key].ContainsKey(item.shortname))
                                    {
                                        //Puts("9");
                                        playerData.players[player.userID].WeaponSelection[entry.Key].Add(item.shortname, new List<string>());
                                    }
                                }
                                if (playerData.players[player.userID].Weapons != null)
                                    if (playerData.players[player.userID].Weapons.collectionname == set)
                                    {
                                        UI.CreateButton(ref element, WPanel, UIColors["green"], GetMSG("CurrentlyEquipped"), 18, "0.1 0.86", "0.9 0.91", $"UI_ProcessSelection weapon {set}");
                                    }
                                    else
                                    {
                                        UI.CreateButton(ref element, WPanel, UIColors["green"], GetMSG("SelectCollection", set.ToUpper()), 18, "0.1 0.86", "0.9 0.91", $"UI_ProcessSelection weapon {set}");
                                    }
                                UI.CreateTextOutline(ref element, WPanel, UIColors["white"], UIColors["green"], GetMSG("CurrentWeaponKills", playerData.players[player.userID].WeaponCollectionKills[set].ToString()), 16, "1", "1", "0.1 0.81", "0.9 0.86");
                                var RequiredKills = playerData.players[player.userID].WeaponCollectionKills[set];
                                if (playerData.players[player.userID].WeaponCollections[set].ContainsKey(item.shortname))
                                {
                                    UI.LoadImage(ref element, WPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                    UI.CreatePanel(ref element, WPanel, UIColors["green"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                    info = GetLang("Owned");
                                    UI.CreateLabel(ref element, WPanel, UIColors["white"], info, 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                                }
                                else if (money >= item.price && RequiredKills >= item.killsrequired)
                                {
                                    UI.LoadImage(ref element, WPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                    UI.LoadImage(ref element, WPanel, TryForImage("lock"), $"{min.x+.01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                    UI.CreatePanel(ref element, WPanel, UIColors["red"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                    info = GetMSG("ItemWeaponCost", item.price.ToString(), item.killsrequired.ToString());
                                    UI.CreateTextOutline(ref element, WPanel, UIColors["white"], UIColors["green"], info, 16, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                    UI.CreateButton(ref element, WPanel, "0 0 0 0", "", 12, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_PrepPurchase {item.shortname} weapon", TextAnchor.MiddleCenter);
                                }
                                else
                                {
                                    UI.LoadImage(ref element, WPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                    UI.LoadImage(ref element, WPanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                    UI.CreatePanel(ref element, WPanel, UIColors["grey"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                    info = GetMSG("ItemWeaponCost", item.price.ToString(), item.killsrequired.ToString());
                                    UI.CreateLabel(ref element, WPanel, UIColors["red"], info, 12, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                                }
                            }
                            else
                            {
                                //Puts("10");
                                UI.LoadImage(ref element, WPanel, TryForImage(item.shortname, item.skin), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                UI.LoadImage(ref element, WPanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                UI.CreatePanel(ref element, WPanel, UIColors["grey"], $"{altmin.x} {altmin.y + .025f}", $"{altmax.x} {altmax.y - .025f}");
                                info = GetMSG("ItemWeaponCost", item.price.ToString(), item.killsrequired.ToString());
                                UI.CreateLabel(ref element, WPanel, UIColors["red"], info, 12, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);

                                if (money >= gwData.WeaponSets[set].cost && playerData.players[player.userID].kills >= gwData.WeaponSets[set].killsrequired)
                                {
                                    UI.CreateButton(ref element, WPanel, UIColors["blue"], GetMSG("UnlockCollection", gwData.WeaponSets[set].cost.ToString()), 16, "0.1 0.86", "0.9 0.91", $"UI_PurchasingPanel weapon {set}", TextAnchor.MiddleCenter);

                                }
                                else
                                {
                                    if (playerData.players[player.userID].kills != 0)
                                    {
                                        var percent = System.Convert.ToDouble(gwData.WeaponSets[set].killsrequired / playerData.players[player.userID].kills);
                                        if (percent * 100 > 75)
                                            UI.CreateTextOutline(ref element, WPanel, UIColors["white"], UIColors["yellow"], GetMSG("CostOfWC", gwData.WeaponSets[set].cost.ToString(), gwData.WeaponSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                        else if (percent * 100 > 25 && percent * 100 < 76)
                                            UI.CreateTextOutline(ref element, WPanel, UIColors["white"], UIColors["orange"], GetMSG("CostOfWC", gwData.WeaponSets[set].cost.ToString(), gwData.WeaponSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                        else if (percent * 100 > 0 && percent * 100 < 26)
                                            UI.CreateTextOutline(ref element, WPanel, UIColors["white"], UIColors["red"], GetMSG("CostOfWC", gwData.WeaponSets[set].cost.ToString(), gwData.WeaponSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                    }
                                    else UI.CreateTextOutline(ref element, WPanel, UIColors["white"], UIColors["red"], GetMSG("CostOfWC", gwData.WeaponSets[set].cost.ToString(), gwData.WeaponSets[set].killsrequired.ToString()), 16, "1", "1", "0.1 0.86", "0.9 0.91");
                                }
                            }

                        }
                    }
                }
            }
            //Puts("DONE");
            CuiHelper.AddUi(player, element);
            AttachmentPanel(player);
        }

        private void AttachmentPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, APanel);
            var element = UI.CreateElementContainer(APanel, "0 0 0 0", "0.35 0.2", "0.75 0.45", true);
            if (NewWeaponCollection.ContainsKey(player.userID) && ACUIInfo[player.userID].admin)
            {
                Vector2 min = new Vector2(0f, 0f);
                Vector2 dimension = new Vector2(.125f, .2f);
                Vector2 offset2 = new Vector2(0.175f, 0f);
                Vector2 altmin;
                Vector2 max;
                Vector2 altmax;
                Dictionary<Slot, string> UsedAttachmentslots = new Dictionary<Slot, string>();
                List<Slot> UnusedAttachmentslots = new List<Slot>();
                Dictionary<Slot, string> UsedWeaponSlots = new Dictionary<Slot, string>();
                foreach (var block in WeaponSlotPos)
                {
                    foreach (var entry in NewWeaponCollection[player.userID].collection.set.Where(kvp => kvp.Value.slot == block.Key))
                        UsedWeaponSlots.Add(entry.Value.slot, entry.Value.shortname);
                }

                if (UsedWeaponSlots.ContainsKey(Slot.main))
                {
                    var item = ItemManager.Create(ItemManager.FindItemDefinition(UsedWeaponSlots[Slot.main]), 1, 0);
                    var held = item.GetHeldEntity() as BaseProjectile;
                    if (held != null /*item.contents.capacity != 0*/)
                    {
                        foreach (var block in MainAttachmentSlotsPos)
                        {
                            foreach (var entry in NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments.Where(kvp => kvp.Value.slot == block.Key))
                                UsedAttachmentslots.Add(entry.Value.slot, entry.Value.shortname);
                        }
                        foreach (var block in MainAttachmentSlotsPos)
                            if (!UsedAttachmentslots.ContainsKey(block.Key))
                                UnusedAttachmentslots.Add(block.Key);
                        foreach (var entry in UsedAttachmentslots)
                        {
                            min = MainAttachmentSlotsPos[entry.Key];
                            max = min + dimension;
                            altmin = min - offset2;
                            altmax = altmin + dimension;

                            UI.LoadImage(ref element, APanel, TryForImage(entry.Value, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry.Key)} attachment {UsedWeaponSlots[Slot.main]}", TextAnchor.MiddleCenter);
                            if (NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].free)
                            {
                                UI.CreatePanel(ref element, APanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], GetMSG("Free"), 16, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            }
                            else
                            {
                                if (NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].cost == 0 && NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].killsrequired == 0)
                                {
                                    UI.CreatePanel(ref element, APanel, UIColors["red"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                    UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], GetMSG("ClickToDetail", NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].cost.ToString(), NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].killsrequired.ToString()), 10, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                }
                                else
                                {
                                    UI.CreatePanel(ref element, APanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                    UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], GetMSG("ItemWeaponCost", NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].cost.ToString(), NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].attachments[entry.Value].killsrequired.ToString()), 10, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                }
                            }
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", $"UI_AddItemAttributes {entry.Value} attachment {UsedWeaponSlots[Slot.main]}", TextAnchor.MiddleCenter);
                            UI.CreateButton(ref element, APanel, UIColors["red"], GetLang("Remove"), 8, $"{altmin.x - .05f} {altmin.y + .05f}", $"{altmin.x} {altmax.y - .05f}", $"UI_RemoveItem {entry.Value} attachment {UsedWeaponSlots[Slot.main]}", TextAnchor.MiddleCenter);
                        }
                        foreach (var entry in UnusedAttachmentslots)
                        {
                            min = MainAttachmentSlotsPos[entry];
                            max = min + dimension;
                            UI.CreatePanel(ref element, APanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                            UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry)} attachment {UsedWeaponSlots[Slot.main]}", TextAnchor.MiddleCenter);
                        }
                    }
                }
                UsedAttachmentslots.Clear();
                UnusedAttachmentslots.Clear();
                if (UsedWeaponSlots.ContainsKey(Slot.secondary))
                {
                    var item = ItemManager.Create(ItemManager.FindItemDefinition(UsedWeaponSlots[Slot.secondary]), 1, 0);
                    var held = item.GetHeldEntity() as BaseProjectile;
                    if (held != null /*&& item.contents.capacity != 0*/)
                    {
                        foreach (var block in SecondaryAttachmentSlotsPos)
                        {
                            foreach (var entry in NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments.Where(kvp => kvp.Value.slot == block.Key))
                                UsedAttachmentslots.Add(entry.Value.slot, entry.Value.shortname);
                        }
                        foreach (var block in SecondaryAttachmentSlotsPos)
                            if (!UsedAttachmentslots.ContainsKey(block.Key))
                                UnusedAttachmentslots.Add(block.Key);
                        foreach (var entry in UsedAttachmentslots)
                        {
                            min = SecondaryAttachmentSlotsPos[entry.Key];
                            max = min + dimension;
                            altmin = min + offset2;
                            altmax = altmin + dimension;

                            UI.LoadImage(ref element, APanel, TryForImage(entry.Value, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry.Key)} attachment {UsedWeaponSlots[Slot.secondary]}", TextAnchor.MiddleCenter);
                            if (NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].free)
                            {
                                UI.CreatePanel(ref element, APanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], GetMSG("Free"), 16, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                            }
                            else
                            {
                                if (NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].cost == 0 && NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].killsrequired == 0)
                                {
                                    UI.CreatePanel(ref element, APanel, UIColors["red"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                    UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], GetMSG("ClickToDetail", NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].cost.ToString(), NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].killsrequired.ToString()), 10, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                }
                                else
                                {
                                    UI.CreatePanel(ref element, APanel, UIColors["green"], $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                    UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], GetMSG("ItemWeaponCost", NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].cost.ToString(), NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].attachments[entry.Value].killsrequired.ToString()), 10, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                }
                            }
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", $"UI_AddItemAttributes {entry.Value} attachment {UsedWeaponSlots[Slot.secondary]}", TextAnchor.MiddleCenter);
                            UI.CreateButton(ref element, APanel, UIColors["red"], GetLang("Remove"), 8, $"{altmin.x + .125f} {altmin.y + .05f}", $"{altmin.x + .175f} {altmax.y - .05f}", $"UI_RemoveItem {entry.Value} attachment {UsedWeaponSlots[Slot.secondary]}", TextAnchor.MiddleCenter);
                        }
                        foreach (var entry in UnusedAttachmentslots)
                        {
                            min = SecondaryAttachmentSlotsPos[entry];
                            max = min + dimension;
                            UI.CreatePanel(ref element, APanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                            UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), entry)} attachment {UsedWeaponSlots[Slot.secondary]}", TextAnchor.MiddleCenter);
                        }
                    }
                }
                /////////////////////////////////////////
                Dictionary<Slot, string> UsedAmmunitionSlots = new Dictionary<Slot, string>();
                List<Slot> UnusedAmmunitionSlots = new List<Slot>();
                dimension = new Vector2(.125f, .175f);
                if (UsedWeaponSlots.ContainsKey(Slot.main))
                {
                    var held = ItemManager.Create(ItemManager.FindItemDefinition(UsedWeaponSlots[Slot.main]), 1, 0).GetHeldEntity() as BaseProjectile;
                    if (held != null)
                    {
                            min = AmmunitionSlotsPos[Slot.ammunitionMain];
                            max = min + dimension;
                        UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                        if (NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].ammoType != null)
                        {
                            UI.LoadImage(ref element, APanel, TryForImage(NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.main]].ammoType, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                        }
                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), Slot.ammunitionMain)} ammo {UsedWeaponSlots[Slot.main]}", TextAnchor.MiddleCenter);
                    }
                }
                UsedAmmunitionSlots.Clear();
                UnusedAmmunitionSlots.Clear();
                if (UsedWeaponSlots.ContainsKey(Slot.secondary))
                {
                    var held = ItemManager.Create(ItemManager.FindItemDefinition(UsedWeaponSlots[Slot.secondary]), 1, 0).GetHeldEntity() as BaseProjectile;
                    if (held != null)
                    {
                        min = AmmunitionSlotsPos[Slot.ammunitionSecondary];
                        max = min + dimension;
                        altmin = min - offset2;
                        altmax = altmin - dimension;
                        UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                        if (NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].ammoType != null)
                        {
                            UI.LoadImage(ref element, APanel, TryForImage(NewWeaponCollection[player.userID].collection.set[UsedWeaponSlots[Slot.secondary]].ammoType, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                        }
                        UI.CreateButton(ref element, APanel, "0 0 0 0", "", 16, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), Slot.ammunitionMain)} ammo {UsedWeaponSlots[Slot.secondary]}", TextAnchor.MiddleCenter);
                    }
                }
            }
            else
            {
                var set = ACUIInfo[player.userID].WeaponSet;
                if (!PendingPurchase.ContainsKey(player.userID))
                    PendingPurchase.Add(player.userID, new PurchaseItem { });
                else
                    PendingPurchase[player.userID].attachment.Clear();
                if (set == "") return;
                var money = playerData.players[player.userID].money;
                if (configData.UseServerRewards)
                {
                    if (CheckPoints(player.userID) is int)
                        money = (int)CheckPoints(player.userID);
                }
                else if (configData.UseEconomics)
                    money = Convert.ToInt32(Economics.CallHook("GetPlayerMoney", player.userID)); foreach (var entry in gwData.WeaponSets.Where(kvp => kvp.Key == set))
                {
                    foreach (var item in entry.Value.set)
                    {
                        string info = "";
                        if (item.slot == Slot.main)
                            if (!string.IsNullOrEmpty(item.ammoType))
                            {
                                Vector2 dimension = new Vector2(.125f, .175f);
                                Vector2 offset = new Vector2(.002f, .003f);
                                Vector2 pos = AmmunitionSlotsPos[Slot.ammunitionMain];
                                UI.CreatePanel(ref element, APanel, UIColors["black"], $"{pos.x} {pos.y}", $"{pos.x + dimension.x} {pos.y + dimension.y}");
                                UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{pos.x + offset.x} {pos.y + offset.y}", $"{pos.x + dimension.x - offset.x} {pos.y + dimension.y - offset.y}");
                                UI.LoadImage(ref element, APanel, TryForImage(item.ammoType, 0), $"{pos.x} {pos.y}", $"{pos.x + dimension.x} {pos.y + dimension.y}");
                            }

                        if (item.slot == Slot.secondary)
                            if (!string.IsNullOrEmpty(item.ammoType))
                            {
                                Vector2 dimension = new Vector2(.125f, .175f);
                                Vector2 offset = new Vector2(.002f, .003f);
                                Vector2 pos = AmmunitionSlotsPos[Slot.ammunitionSecondary];
                                UI.CreatePanel(ref element, APanel, UIColors["black"], $"{pos.x} {pos.y}", $"{pos.x + dimension.x} {pos.y + dimension.y}");
                                UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{pos.x + offset.x} {pos.y + offset.y}", $"{pos.x + dimension.x - offset.x} {pos.y + dimension.y - offset.y}");
                                UI.LoadImage(ref element, APanel, TryForImage(item.ammoType, 0), $"{pos.x} {pos.y}", $"{pos.x + dimension.x} {pos.y + dimension.y}");
                            }

                        if (item.attachments.Count() > 0)
                        {
                            if (item.slot == Slot.main)
                            {
                                foreach (var block in MainAttachmentSlotsPos)
                                {
                                    Vector2 min = block.Value;
                                    Vector2 max = block.Value + new Vector2(.125f, .2f);
                                    UI.CreatePanel(ref element, APanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                                    UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                                }

                            }
                            if (item.slot == Slot.secondary)
                            {
                                foreach (var block in SecondaryAttachmentSlotsPos)
                                {
                                    Vector2 min = block.Value;
                                    Vector2 max = block.Value + new Vector2(.125f, .2f);
                                    UI.CreatePanel(ref element, APanel, UIColors["black"], $"{min.x} {min.y}", $"{max.x} {max.y}");
                                    UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{min.x + 0.002f} {min.y + 0.003f}", $"{max.x - 0.002f} {max.y - 0.003f}");
                                }
                            }
                            foreach (var attachment in item.attachments)
                            {
                                if (!PendingPurchase[player.userID].attachment.ContainsKey(item.slot))
                                    PendingPurchase[player.userID].attachment.Add(item.slot, new Dictionary<string, Attachment>());
                                PendingPurchase[player.userID].attachment[item.slot].Add(attachment.Key, attachment.Value);
                                Vector2 offset2 = new Vector2(0.175f, 0f);
                                Vector2 min = new Vector2(0f, 0f);
                                Vector2 max;
                                Vector2 altmin = new Vector2(0f, 0f);
                                Vector2 altmax;
                                if (item.slot == Slot.main)
                                {
                                    min = MainAttachmentSlotsPos[attachment.Value.slot];
                                    altmin = min - offset2;
                                }
                                else if (item.slot == Slot.secondary)
                                {
                                    min = SecondaryAttachmentSlotsPos[attachment.Value.slot];
                                    altmin = min + offset2;
                                }
                                Vector2 dimension = new Vector2(.125f, .2f);
                                Vector2 dimension1 = new Vector2(.15f, .3f);
                                max = min + dimension;
                                altmax = altmin + dimension1;
                                if (playerData.players[player.userID].WeaponCollections.ContainsKey(set))
                                {
                                    var weapon = ItemManager.Create(ItemManager.FindItemDefinition(item.shortname), 1, 0);
                                    if (playerData.players[player.userID].WeaponCollections[set].ContainsKey(item.shortname))
                                    {
                                        if (playerData.players[player.userID].WeaponCollections[set][item.shortname].Contains(attachment.Value.shortname))
                                        {
                                            if (!playerData.players[player.userID].WeaponSelection[entry.Key][item.shortname].Contains(attachment.Value.shortname))
                                            {
                                                if (playerData.players[player.userID].WeaponSelection[entry.Key][item.shortname].Count < weapon.contents.capacity)
                                                {
                                                    if (playerData.players[player.userID].WeaponSelection[entry.Key][item.shortname].Count == 0)
                                                    {
                                                        UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname,0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                                        info = GetLang("Unequipped");
                                                        UI.CreatePanel(ref element, APanel, UIColors["white"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                                        UI.CreateTextOutline(ref element, APanel, UIColors["red"], UIColors["black"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                                        UI.CreateButton(ref element, APanel, "0 0 0 0", "", 14, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_ProcessAttachment add {item.shortname} {attachment.Value.shortname} {set}", TextAnchor.MiddleCenter);
                                                    }
                                                    else
                                                        foreach (var a in playerData.players[player.userID].WeaponSelection[entry.Key][item.shortname])
                                                        {
                                                            if (DefaultAttachments[a].location != DefaultAttachments[attachment.Value.shortname].location)
                                                            {
                                                                UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                                                info = GetLang("Unequipped");
                                                                UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                                                UI.CreateTextOutline(ref element, APanel, UIColors["red"], UIColors["black"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                                                UI.CreateButton(ref element, APanel, "0 0 0 0", "", 14, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_ProcessAttachment add {item.shortname} {attachment.Value.shortname} {set}", TextAnchor.MiddleCenter);
                                                            }
                                                            else if (DefaultAttachments[a].location == DefaultAttachments[attachment.Value.shortname].location)
                                                            {
                                                                UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                                                info = GetLang("PositionFull");
                                                                UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                                                UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["red"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                                            }
                                                        }
                                                }
                                                else
                                                {
                                                    UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                                    info = GetLang("GunFull");
                                                    UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                                    UI.CreateTextOutline(ref element, APanel, UIColors["white"], UIColors["red"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                                }
                                            }
                                            else
                                            {
                                                UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                                info = GetLang("Equipped");
                                                UI.CreatePanel(ref element, APanel, UIColors["green"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                                UI.CreateTextOutline(ref element, APanel, UIColors["green"], UIColors["black"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                                UI.CreateButton(ref element, APanel, "0 0 0 0", "", 14, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_ProcessAttachment remove {item.shortname} {attachment.Value.shortname} {set}", TextAnchor.MiddleCenter);
                                            }
                                        }
                                        else if (money >= attachment.Value.cost && playerData.players[player.userID].WeaponCollectionKills[set] >= attachment.Value.killsrequired)
                                        {
                                            UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                            UI.LoadImage(ref element, APanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                            UI.CreatePanel(ref element, APanel, UIColors["red"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                            info = GetMSG("ItemWeaponCost", attachment.Value.cost.ToString(), attachment.Value.killsrequired.ToString());
                                            UI.CreateTextOutline(ref element, APanel, UIColors["black"], UIColors["white"], info, 12, "1", "1", $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}");
                                            UI.CreateButton(ref element, APanel, "0 0 0 0", "", 10, $"{min.x} {min.y}", $"{max.x} {max.y}", $"UI_PrepPurchase {item.shortname} {attachment.Value.shortname}", TextAnchor.MiddleCenter);
                                        }
                                        else
                                        {
                                            UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                            UI.LoadImage(ref element, APanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                            UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                            info = GetMSG("ItemWeaponCost", attachment.Value.cost.ToString(), attachment.Value.killsrequired.ToString());
                                            UI.CreateLabel(ref element, APanel, UIColors["red"], info, 10, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                                        }
                                    }
                                    else
                                    {
                                        UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                        UI.LoadImage(ref element, APanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                        UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                        info = GetMSG("ItemWeaponCost", attachment.Value.cost.ToString(), attachment.Value.killsrequired.ToString());
                                        UI.CreateLabel(ref element, APanel, UIColors["red"], info, 10, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                                    }
                                }
                                else
                                {
                                    UI.LoadImage(ref element, APanel, TryForImage(attachment.Value.shortname, 0), $"{min.x} {min.y}", $"{max.x} {max.y}");
                                    UI.LoadImage(ref element, APanel, TryForImage("lock"), $"{min.x + .01f} {min.y + .01f}", $"{max.x - .01f} {max.y - .01f}");
                                    UI.CreatePanel(ref element, APanel, UIColors["grey"], $"{altmin.x} {altmin.y }", $"{altmax.x} {altmax.y - .03f}");
                                    info = GetMSG("ItemWeaponCost", attachment.Value.cost.ToString(), attachment.Value.killsrequired.ToString());
                                    UI.CreateLabel(ref element, APanel, UIColors["red"], info, 10, $"{altmin.x} {altmin.y}", $"{altmax.x} {altmax.y}", TextAnchor.MiddleCenter);
                                }
                            }
                        }
                    }
                }
                if (ACUIInfo[player.userID].admin)
                    UI.CreateButton(ref element, APanel, UIColors["red"], GetLang("Delete"), 14, "0.8 1.9", "0.92 2.03", $"UI_DeleteWeaponSet");
            }
            CuiHelper.AddUi(player, element);
        }

        private void PurchaseConfirmation(BasePlayer player, string item)
        {
            var pending = PendingPurchase[player.userID];
            var itemname = item;
            var itemshortname = item;
            ulong itemskin = 0;
            var currentGearSet = item;
            var itemprice = pending.setprice.ToString();
            if (pending.gearpurchase == true)
            {
                if (pending.set == false)
                {
                    itemname = pending.gear[item].shortname;
                    itemshortname = pending.gear[item].shortname;
                    itemprice = pending.gear[item].price.ToString();
                    currentGearSet = pending.setname;
                    itemskin = pending.gear[item].skin;
                }
            }
            else if (pending.weaponpurchase == true)
            {
                if (pending.set == false)
                {
                    if (pending.attachmentpurchase == true)
                    {
                        itemname = pending.attachment[pending.weapon[item].slot][pending.attachmentName].shortname;
                        itemshortname = pending.attachment[pending.weapon[item].slot][pending.attachmentName].shortname;
                        itemprice = pending.attachment[pending.weapon[item].slot][pending.attachmentName].cost.ToString();
                        currentGearSet = pending.setname;
                    }
                    else
                    {
                        itemname = pending.weapon[item].shortname;
                        itemshortname = pending.weapon[item].shortname;
                        itemprice = pending.weapon[item].price.ToString();
                        currentGearSet = pending.setname;
                        itemskin = pending.weapon[item].skin;
                    }
                }
            }
            CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
            var element = UI.CreateElementContainer(PanelPurchaseConfirmation,"0 0 0 1", "0.4 0.3", "0.6 0.6", true);
            UI.CreatePanel(ref element, PanelPurchaseConfirmation, UIColors["header"], "0.03 0.02", "0.97 0.98");
            if (pending.set == false)
            {
                UI.CreateTextOutline(ref element, PanelPurchaseConfirmation, UIColors["white"], UIColors["black"], GetMSG("PurchaseInfo", itemname, itemprice), 18, "1", "1", "0.1 0.6", "0.9 0.95");
                UI.LoadImage(ref element, PanelPurchaseConfirmation, TryForImage(itemshortname, itemskin), "0.35 0.275", "0.65 0.575");
            }
            else UI.CreateTextOutline(ref element, PanelPurchaseConfirmation, UIColors["white"], UIColors["black"], GetMSG("PurchaseSetInfo", itemname, itemprice), 18, "1", "1", "0.1 0.3", "0.9 0.89"); 
            UI.CreateButton(ref element, PanelPurchaseConfirmation, UIColors["buttongreen"], "Yes", 18, "0.2 0.05", "0.475 0.25", $"UI_Purchase {item}");
            UI.CreateButton(ref element, PanelPurchaseConfirmation, UIColors["buttonred"], "No", 18, "0.525 0.05", "0.8 0.25", $"UI_DestroyPurchaseConfirmation");
            CuiHelper.AddUi(player, element);
        }

        void OnScreen(BasePlayer player, string msg)
        {
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateElementContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.3 0.35", "0.7 0.65", false);
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["white"], UIColors["green"], msg, 32, "1", "1", "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timer.Once(3, () => CuiHelper.DestroyUi(player, PanelOnScreen));
        }

        void PlayerHUD(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelStats);
            string money = playerData.players[player.userID].money.ToString();
            var element = UI.CreateElementContainer(PanelStats, "0 0 0 0", "0.35 0.93", "0.65 1.0", false);
            if (playerData.players[player.userID].Weapons != null && playerData.players[player.userID].Weapons.collectionname != null)
            {
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud1", playerData.players[player.userID].WeaponCollectionKills[playerData.players[player.userID].Weapons.collectionname].ToString()), 12, "1", "1", "0.05 0.66", "0.35 0.99", TextAnchor.MiddleLeft);
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud4", playerData.players[player.userID].Weapons.collectionname.ToUpper()), 12, "1", "1", "0.36 0.66", "0.95 0.99", TextAnchor.MiddleLeft);
            }
            if (playerData.players[player.userID].Gear != null && playerData.players[player.userID].Gear.collectionname != null)
            {
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud2", playerData.players[player.userID].GearCollectionKills[playerData.players[player.userID].Gear.collectionname].ToString()), 12, "1", "1", "0.05 0.33", "0.35 0.66", TextAnchor.MiddleLeft);
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud5", playerData.players[player.userID].Gear.collectionname.ToUpper()), 12, "1", "1", "0.36 0.33", "0.95 0.66", TextAnchor.MiddleLeft);
            }
            if (configData.UseServerRewards)
            {
                if (CheckPoints(player.userID) is int)
                {
                    money = CheckPoints(player.userID).ToString();
                }
                else money = "0";
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud3a", money), 12, "1", "1", "0.05 0.0", "0.35 0.33", TextAnchor.MiddleLeft);
            }
            else if (configData.UseEconomics)
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud3a", Economics.CallHook("GetPlayerMoney", player.userID).ToString()), 12, "1", "1", "0.05 0.0", "0.35 0.33", TextAnchor.MiddleLeft);
            else
                UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud3b", money), 12, "1", "1", "0.05 0.0", "0.35 0.33", TextAnchor.MiddleLeft);
            UI.CreateTextOutline(ref element, PanelStats, UIColors["black"], UIColors["white"], GetMSG("Hud6", playerData.players[player.userID].kills.ToString()), 12, "1", "1", "0.36 0.0", "0.95 0.33", TextAnchor.MiddleLeft);
            CuiHelper.AddUi(player, element);
        }

        private void SelectIfFree(BasePlayer player, string item, string type)
        {
            CuiHelper.DestroyUi(player, PanelAC);
            var element = UI.CreateElementContainer(PanelAC, UIColors["dark"], "0.4 0.3", "0.6 0.6", true);
            UI.CreatePanel(ref element, PanelAC, UIColors["light"], "0.03 0.02", "0.97 0.98");
            if (item == "collection")
                UI.CreateLabel(ref element, PanelAC, UIColors["limegreen"], GetMSG("UnlockCollectionFree"), 16, "0.1 0.5", "0.9 .98", TextAnchor.UpperCenter);     
                    else 
            UI.CreateLabel(ref element, PanelAC, UIColors["limegreen"], GetMSG("UnlockFree"), 16, "0.1 0.5", "0.9 .98", TextAnchor.UpperCenter);
            UI.CreateButton(ref element, PanelAC, UIColors["buttongreen"], "Yes", 18, "0.2 0.05", "0.475 0.25", $"UI_Free true {item} {type}");
            UI.CreateButton(ref element, PanelAC, UIColors["buttonred"], "No", 18, "0.525 0.05", "0.8 0.25", $"UI_Free false {item} {type}");
            CuiHelper.AddUi(player, element);
        }

        private void NumberPad(BasePlayer player, string cmd, string title, string item, string type)
        {
            CuiHelper.DestroyUi(player, PanelAC);
            var element = UI.CreateElementContainer(PanelAC, UIColors["dark"], "0.35 0.3", "0.65 0.7", true);
            UI.CreatePanel(ref element, PanelAC, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelAC, UIColors["limegreen"], GetMSG(title, item), 16, "0.1 0.85", "0.9 .98", TextAnchor.UpperCenter);
            var n = 1;
            var i = 0;
                while (n < 10)
                {
                    CreateNumberPadButton(ref element, PanelAC, i, n, cmd, item, type); i++; n++;
                }
                while (n >= 10 && n < 25)
                {
                    CreateNumberPadButton(ref element, PanelAC, i, n, cmd, item, type); i++; n += 5;
                }
                while (n >= 25 && n < 200)
                {
                    CreateNumberPadButton(ref element, PanelAC, i, n, cmd, item, type); i++; n += 25;
                }
                while (n >= 200 && n <= 950)
                {
                    CreateNumberPadButton(ref element, PanelAC, i, n, cmd, item, type); i++; n += 50;
                }
                while (n >= 1000 && n <= 10000)
                {
                    CreateNumberPadButton(ref element, PanelAC, i, n, cmd, item, type); i++; n += 500;
                }
            //}
            CuiHelper.AddUi(player, element);
        }

        private void CreateNumberPadButton(ref CuiElementContainer container, string panelName, int i, int number, string command, string item, string type)
        {
            var pos = CalcNumButtonPos(i);
            UI.CreateButton(ref container, panelName, UIColors["buttonbg"], number.ToString(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} {number} {item} {type}");
        }


        #endregion

        #region UI Calculations

        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.75f);
            Vector2 dimensions = new Vector2(0.15f, 0.15f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.01f + dimensions.x) * (number - 6);
                offsetY = (-0.002f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.002f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.002f - dimensions.y) * 3;
            }
            if (number > 23 && number < 30)
            {
                offsetX = (0.01f + dimensions.x) * (number - 24);
                offsetY = (-0.002f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcNumButtonPos(int number)
        {
            Vector2 position = new Vector2(0.05f, 0.75f);
            Vector2 dimensions = new Vector2(0.09f, 0.10f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 9)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            if (number > 8 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 9);
                offsetY = (-0.02f - dimensions.y) * 1;
            }
            if (number > 17 && number < 27)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.02f - dimensions.y) * 2;
            }
            if (number > 26 && number < 36)
            {
                offsetX = (0.01f + dimensions.x) * (number - 27);
                offsetY = (-0.02f - dimensions.y) * 3;
            }
            if (number > 35 && number < 45)
            {
                offsetX = (0.01f + dimensions.x) * (number - 36);
                offsetY = (-0.02f - dimensions.y) * 4;
            }
            if (number > 44 && number < 54)
            {
                offsetX = (0.01f + dimensions.x) * (number - 45);
                offsetY = (-0.02f - dimensions.y) * 5;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }


        #endregion

        #region UI Commands



        [ConsoleCommand("UI_AddItemAttributes")]
        private void cmdUI_AddGearAttributes(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var item = arg.Args[0];
            var type = arg.Args[1];
            if (type == "attachment")
                NewWeaponCollection[player.userID].collection.currentweapon = arg.Args[2];
            if (gwData.Items[Slot.chest].Contains(item))
            {
                NewGearCollection[player.userID].collection.set[item].free = true;
                DestroyACPanel(player);
                OpenACUI(player);
            }
            else SelectIfFree(player, item, type);
        }

        [ConsoleCommand("UI_RemoveItem")]
        private void cmdUI_RemoveItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var item = arg.Args[0];
            var type = arg.Args[1];
            if (type == "attachment")
                NewWeaponCollection[player.userID].collection.set[arg.Args[2]].attachments.Remove(item);
            else if (type == "gear")
                NewGearCollection[player.userID].collection.set.Remove(item);
            else if (type == "weapon")
                NewWeaponCollection[player.userID].collection.set.Remove(item);
            DestroyACPanel(player);
            OpenACUI(player);
        }
        [ConsoleCommand("UI_SelectCollectionItem")]
        private void cmdUI_SelectGear(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            Slot slot = (Slot)Enum.Parse(typeof(Slot), arg.Args[0]);
            string type = arg.Args[1];
            var page = 0;
            if (type == "attachment" || type == "ammo")
                NewWeaponCollection[player.userID].collection.currentweapon = arg.Args[2];
            else if (arg.Args.Length > 2)
            {
                page = Convert.ToInt32(arg.Args[2]);
            }
            DestroyACPanel(player);
            SelectGearSlotItem(player, slot,type, page);
        }

        private void SelectGearSlotItem(BasePlayer player, Slot slot, string type, int page = 0)
        {
            var element = UI.CreateElementContainer(PanelAC, "0 0 0 0", "0.275 0.25", "0.725 0.75", true);
            UI.CreateLabel(ref element, PanelAC, UIColors["black"], $"{TextColors["limegreen"]} {GetLang("SelectCollectionItem")}", 20, "0.05 .9", "1 1", TextAnchor.MiddleCenter);
            int entriesallowed = 30;
            int remainingentries = gwData.Items[slot].Count() - (page * entriesallowed);
            {
                if (remainingentries > entriesallowed)
                {
                    UI.CreateButton(ref element, PanelAC, UIColors["blue"], GetLang("Next"), 18, "0.87 0.03", "0.97 0.085", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), slot)} {type} { page + 1}");
                }
                if (page > 0)
                {
                    UI.CreateButton(ref element, PanelAC, UIColors["buttonred"], GetLang("Back"), 18, "0.73 0.03", "0.83 0.085", $"UI_SelectCollectionItem {Enum.GetName(typeof(Slot), slot)} {type} { page - 1}");
                }
            }
            int shownentries = page * entriesallowed;
            int i = 0;
            int n = 0;
            foreach (var entry in gwData.Items[slot])
            {
                if (type == "attachment")
                    if (NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments.ContainsKey(entry)) continue;
                else if (type == "weapon")
                    if (NewWeaponCollection[player.userID].collection.set.ContainsKey(entry)) continue;
                i++;
                if (i < shownentries + 1) continue;
                else if (i <= shownentries + entriesallowed)
                {
                    var pos = CalcButtonPos(n);
                    UI.LoadImage(ref element, PanelAC, TryForImage(entry, 0), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                    UI.CreateButton(ref element, PanelAC, "0 0 0 0", "", 14, $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}", $"UI_AddItem {entry} {Enum.GetName(typeof(Slot), slot)} {type}", TextAnchor.MiddleCenter);
                    n++;
                }
            }
            CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_AddItem")]
        private void cmdUI_AddItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var item = arg.Args[0];
            Slot slot = (Slot)Enum.Parse(typeof(Slot), arg.Args[1]);
            var type = arg.Args[2];
            DestroyACPanel(player);
            var existingitem = "";
            if (type == "gear")
            {
                foreach (var entry in NewGearCollection[player.userID].collection.set.Where(kvp => kvp.Value.slot == slot))
                    existingitem = entry.Key;
                if (existingitem != "")
                    NewGearCollection[player.userID].collection.set.Remove(existingitem);
                if (slot == Slot.chest)
                    NewGearCollection[player.userID].collection.set.Add(item, new Gear { shortname = item, slot = slot, container = "wear", amount = 1, free = true , skin = 0});
                else
                    NewGearCollection[player.userID].collection.set.Add(item, new Gear { shortname = item, slot = slot, container = "wear", amount = 1 , skin = 0});
            }
            else if (type == "weapon")
            {
                string ammo = null;
                foreach (var entry in NewWeaponCollection[player.userID].collection.set.Where(kvp => kvp.Value.slot == slot))
                    existingitem = entry.Key;
                if (existingitem != "")
                    NewWeaponCollection[player.userID].collection.set.Remove(existingitem);
                var gun = BuildItem(item, 1).GetHeldEntity() as BaseProjectile;
                if (gun != null)
                {
                    ammo = gun.primaryMagazine.ammoType.shortname;
                }
                    if (slot == Slot.main)
                    NewWeaponCollection[player.userID].collection.set.Add(item, new Weapon { shortname = item, slot = slot, container = "belt", amount = 1, free = true, ammoType = ammo ,skin = 0});
                else
                    NewWeaponCollection[player.userID].collection.set.Add(item, new Weapon { shortname = item, slot = slot, container = "belt", amount = 1 , ammoType = ammo, skin = 0});
            }
            else if (type == "attachment")
            {
                foreach (var entry in NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments.Where(kvp => kvp.Value.slot == slot))
                    existingitem = entry.Key;
                if (existingitem != "")
                    NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments.Remove(existingitem);
                NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments.Add(item, new Attachment { shortname = item, slot = slot, location = DefaultAttachments[item].location });
            }
            else if (type == "ammo")
                NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].ammoType = item;
            //DestroyACPanel(player);
            //OpenACUI(player);
            ACUIInfo[player.userID].page = 0;
            SelectSkin(player, item, type);
        }

        [ConsoleCommand("UI_ChangeSkinPage")]
        private void cmdUI_ChangeSkinPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            ACUIInfo[player.userID].page = Convert.ToInt32(arg.Args[0]);
            SelectSkin(player, arg.Args[1], arg.Args[2]);
        }

        private void SelectSkin(BasePlayer player, string item, string type)
        {
            if (ItemSkins.ContainsKey(item) && ItemSkins[item].Count() > 1)
            {
                CuiHelper.DestroyUi(player, PanelAC);
                var element = UI.CreateElementContainer(PanelAC, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
                UI.CreatePanel(ref element, PanelAC, UIColors["light"], "0.01 0.02", "0.99 0.98");
                UI.CreateLabel(ref element, PanelAC, UIColors["black"], $"{TextColors["limegreen"]} {GetMSG("GearSkin", item)}", 20, "0.05 .9", "1 1", TextAnchor.MiddleCenter);
                var page = ACUIInfo[player.userID].page;
                var skinlist = ItemSkins[item];
                int entriesallowed = 30;
                int remainingentries = skinlist.Count - (page * entriesallowed);
                {
                    if (remainingentries > entriesallowed)
                    {
                        UI.CreateButton(ref element, PanelAC, UIColors["blue"], "Next", 18, "0.87 0.03", "0.97 0.085", $"UI_ChangeSkinPage {page + 1} {item} {type}");
                    }
                    if (page > 0)
                    {
                        UI.CreateButton(ref element, PanelAC, UIColors["buttonred"], "Back", 18, "0.73 0.03", "0.83 0.085", $"UI_ChangeSkinPage {page - 1} {item} {type}");
                    }
                }
                int shownentries = page * entriesallowed;
                int i = 0;
                int n = 0;
                foreach (var entry in skinlist)
                {
                    i++;
                    if (i < shownentries + 1) continue;
                    else if (i <= shownentries + entriesallowed)
                    {
                        {
                            var pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelAC, TryForImage(item, entry), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelAC, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectSkin {item} {entry} {type}");
                            n++;
                        }
                    }
                }
                CuiHelper.AddUi(player, element);
            }
            else
            {
                if (type == "gear")
                    NewGearCollection[player.userID].collection.set[item].skin = 0;
                else if (type == "weapon")
                    NewWeaponCollection[player.userID].collection.set[item].skin = 0;
                DestroyACPanel(player);
                OpenACUI(player);
            }
        }

        [ConsoleCommand("UI_SetItemKillRequirement")]
        private void cmdUI_SetGearKillRequirement(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int kills = Convert.ToInt32(arg.Args[0]);
            var item = arg.Args[1];
            var type = arg.Args[2];
            if (type == "gear")
                NewGearCollection[player.userID].collection.set[item].killsrequired = kills;
            else if (type == "weapon")
                NewWeaponCollection[player.userID].collection.set[item].killsrequired = kills;
            else if (type == "attachment")
                NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments[item].killsrequired = kills;
            DestroyACPanel(player);
            NumberPad(player, "UI_SetItemPrice", "SelectPrice", item, type);
        }

        [ConsoleCommand("UI_SetItemPrice")]
        private void cmdUI_SetGearPrice(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            var item = arg.Args[1];
            var type = arg.Args[2];
            if (type == "gear")
                NewGearCollection[player.userID].collection.set[item].price = amount;
            else if (type == "weapon")
                NewWeaponCollection[player.userID].collection.set[item].price = amount;
            else if (type == "attachment")
                NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments[item].cost  = amount;
            DestroyACPanel(player);
            OpenACUI(player);
        }

        [ConsoleCommand("UI_SetCollectionCost")]
        private void cmdUI_SetCollectionCost(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int cost = Convert.ToInt32(arg.Args[0]);
            string type = arg.Args[1];
            if (type == "gear")
                NewGearCollection[player.userID].collection.cost = cost;
            else if (type == "weapon")
                NewWeaponCollection[player.userID].collection.cost = cost;
            DestroyACPanel(player);
            NumberPad(player, "UI_SetCollectionKills", "CollectionKills", "", type);
        }

        [ConsoleCommand("UI_SetCollectionKills")]
        private void cmdUI_SetCollectionKills(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            string type = arg.Args[1];
            if (type == "gear")
                NewGearCollection[player.userID].collection.killsrequired = amount;
            else if (type == "weapon")
                NewWeaponCollection[player.userID].collection.killsrequired = amount;
            DestroyACPanel(player);
            SetCollectionName(player);
        }

        private void SetCollectionName(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelAC);
            var element = UI.CreateElementContainer(PanelAC, UIColors["dark"], "0.4 0.3", "0.6 0.6");
            UI.CreatePanel(ref element, PanelAC, UIColors["light"], "0.03 0.02", "0.97 0.98");
            UI.CreateLabel(ref element, PanelAC, UIColors["limegreen"], GetMSG("CollectionName"), 16, "0.1 0.5", "0.9 .98", TextAnchor.UpperCenter);
            CuiHelper.AddUi(player, element);
        }

        [ConsoleCommand("UI_Free")]
        private void cmdUI_Free(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var answer = arg.Args[0];
            var item = arg.Args[1];
            var type = arg.Args[2];
            if (item == "collection")
            {
                if (answer == "true")
                {
                    if (type == "gear")
                        NewGearCollection[player.userID].collection.free = true;
                    else if (type == "weapon")
                        NewWeaponCollection[player.userID].collection.free = true;
                    DestroyACPanel(player);
                    SetCollectionName(player);
                }
                else if (answer == "false")
                {
                    if (type == "gear")
                        NewGearCollection[player.userID].collection.free = false;
                    else if (type == "weapon") NewWeaponCollection[player.userID].collection.free = false;
                    NumberPad(player, "UI_SetCollectionCost", "CollectionCost", " ", type);
                }
            }
            else
            {
                if (answer == "true")
                {
                    if (type == "gear")
                        NewGearCollection[player.userID].collection.set[item].free = true;
                    else if (type == "weapon")
                        NewWeaponCollection[player.userID].collection.set[item].free = true;
                    else if (type == "attachment")
                        NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments[item].free = true;
                }
                else if (answer == "false")
                    if (type == "gear")
                        NewGearCollection[player.userID].collection.set[item].free = false;
                    else if (type == "weapon") NewWeaponCollection[player.userID].collection.set[item].free = false;
                    else if (type == "attachment")
                        NewWeaponCollection[player.userID].collection.set[NewWeaponCollection[player.userID].collection.currentweapon].attachments[item].free = false;
                DestroyACPanel(player);
                if (answer == "false")
                    NumberPad(player, "UI_SetItemKillRequirement", "SelectKillsRequired", item, type);
                else
                    OpenACUI(player);
            }
        }

        [ConsoleCommand("UI_ChangeGearSet")]
        private void cmdUI_ChangeGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = string.Join(" ",arg.Args);
            ACUIInfo[player.userID].GearSet = set;
            ACUIInfo[player.userID].gear = true;
            ACUIInfo[player.userID].weapon = false;
            ACUIInfo[player.userID].WeaponSet = "";
            CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
            CuiHelper.DestroyUi(player, WPanel);
            CuiHelper.DestroyUi(player, APanel);
            WeaponListPanel(player);
            GearListPanel(player);
            GearPanel(player);
        }

        [ConsoleCommand("UI_ChangeWeaponSet")]
        private void cmdUI_ChangeWeaponSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = string.Join(" ", arg.Args);
            ACUIInfo[player.userID].WeaponSet = set;
            ACUIInfo[player.userID].weapon = true;
            ACUIInfo[player.userID].gear = false;
            ACUIInfo[player.userID].GearSet = "";
            CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
            CuiHelper.DestroyUi(player, GPanel);
            GearListPanel(player);
            WeaponListPanel(player);
            WeaponPanel(player);
        }

        [ConsoleCommand("UI_SwitchAdminView")]
        private void cmdUI_SwitchAdminView(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (!isAuth(player))
            {
                GetSendMSG(player, "NotAuthorized");
                return;
            }
            if (ACUIInfo[player.userID].admin)
            {
                GetSendMSG(player, "ExitAdminView");
                ACUIInfo[player.userID].admin = false;
            }
            else
            {
                GetSendMSG(player, "EnterAdminView");
                ACUIInfo[player.userID].admin = true;
            }
                DestroyACPanel(player);
                OpenACUI(player);
        }

        [ConsoleCommand("UI_GearIndexShownChange")]
        private void cmdUI_GearIndexShownChange(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var index = Convert.ToInt32(arg.Args[0]);
            ACUIInfo[player.userID].GearIndex = index;
            GearListPanel(player);
        }

        [ConsoleCommand("UI_WeaponIndexShownChange")]
        private void cmdUI_WeaponIndexShownChange(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var index = Convert.ToInt32(arg.Args[0]);
            ACUIInfo[player.userID].WeaponIndex = index;
            WeaponListPanel(player);
        }

        [ConsoleCommand("UI_DestroyPurchaseConfirmation")]
        private void cmdUI_DestroyPurchaseConfirmation(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
        }

        [ConsoleCommand("UI_ProcessAttachment")]
        private void cmdUI_ProcessAttachment(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var request = arg.Args[0];
            var weapon = arg.Args[1];
            var attachment = arg.Args[2];
            var set = string.Join(" ", arg.Args.Skip(3).ToArray());
            ProcessAttachment(player, request, set, weapon, attachment);
        }

        void ProcessAttachment(BasePlayer player, string request, string set, string weapon, string attachment)
        {
            switch (request)
            {
                case "clear":
                    playerData.players[player.userID].WeaponSelection[set][weapon].Clear();
                    AttachmentPanel(player);
                    break;
                case "add":
                    playerData.players[player.userID].WeaponSelection[set][weapon].Add(attachment);
                    AttachmentPanel(player);
                    break;
                case "remove":
                    playerData.players[player.userID].WeaponSelection[set][weapon].Remove(attachment);
                    AttachmentPanel(player);
                    break;
            }
        }


        [ConsoleCommand("UI_ProcessSelection")]
        private void cmdUI_ProcessSelection(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var type = arg.Args[0];
            var set = string.Join(" ", arg.Args.Skip(1).ToArray());
            ProcessSelection(player, type, set);
        }

        void ProcessSelection(BasePlayer player, string type, string set)
        {
            switch (type)
            {
                case "set":
                    playerData.players[player.userID].Gear.collectionname = set;
                    SelectionGearCollection(player, set);
                    break;
                case "weapon":
                    playerData.players[player.userID].Weapons.collectionname = set;
                    SelectWeapons(player);
                    break;
            }
        }

        [ConsoleCommand("UI_PurchasingPanel")]
        private void cmdPurchasePanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (arg.Args[0] == "gear")
            {
                PendingPurchase[player.userID].gearpurchase = true;
                PendingPurchase[player.userID].weaponpurchase = false;
                PendingPurchase[player.userID].attachmentpurchase = false;
            }
            else
            {
                PendingPurchase[player.userID].weaponpurchase = true;
                PendingPurchase[player.userID].gearpurchase = false;
                PendingPurchase[player.userID].attachmentpurchase = false;
            }
            var item = string.Join(" ",arg.Args.Skip(1).ToArray());
            PurchaseConfirmation(player, item);
        }

        [ConsoleCommand("UI_PrepPurchase")]
        private void cmdUI_PrepPurchase(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            PendingPurchase[player.userID].set = false;
            var item = arg.Args[0];
            var type = arg.Args[1];
            if (type == "gear")
            {
                PendingPurchase[player.userID].gearpurchase = true;
                PendingPurchase[player.userID].weaponpurchase = false;
                PendingPurchase[player.userID].attachmentpurchase = false;
                PendingPurchase[player.userID].setname = ACUIInfo[player.userID].GearSet;
                PendingPurchase[player.userID].setprice = gwData.GearSets[ACUIInfo[player.userID].GearSet].cost;
                PendingPurchase[player.userID].setkillrequirement = gwData.GearSets[ACUIInfo[player.userID].GearSet].killsrequired;
            }
            else if (type == "weapon")
            {
                PendingPurchase[player.userID].weaponpurchase = true;
                PendingPurchase[player.userID].gearpurchase = false;
                PendingPurchase[player.userID].attachmentpurchase = false;
                PendingPurchase[player.userID].setname = ACUIInfo[player.userID].WeaponSet;
                PendingPurchase[player.userID].setprice = gwData.WeaponSets[ACUIInfo[player.userID].WeaponSet].cost;
                PendingPurchase[player.userID].setkillrequirement = gwData.WeaponSets[ACUIInfo[player.userID].WeaponSet].killsrequired;
            }
            else
            {
                PendingPurchase[player.userID].gearpurchase = false;
                PendingPurchase[player.userID].weaponpurchase = true;
                PendingPurchase[player.userID].attachmentpurchase = true;
                PendingPurchase[player.userID].setname = ACUIInfo[player.userID].WeaponSet;
                PendingPurchase[player.userID].attachmentName = arg.Args[1];
            }
            PurchaseConfirmation(player, item);
        }

        [ConsoleCommand("UI_Purchase")]
        private void cmdUI_Purchase(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var item = string.Join(" ", arg.Args);
            Purchase(player, item);
        }

        void Purchase(BasePlayer player, string item)
        {
            var money = playerData.players[player.userID].money;
            var pending = PendingPurchase[player.userID];
            if (pending.gearpurchase == true)
            {
                if (pending.set == false)
                {
                    playerData.players[player.userID].GearCollections[pending.setname].Add(item);
                    DestroyACPanel(player);
                    if (configData.UseServerRewards)
                        SRAction(player.userID, pending.gear[item].price, "REMOVE");
                    else if (configData.UseEconomics)
                        ECOAction(player.userID, pending.gear[item].price, "REMOVE");
                    else
                        money -= pending.gear[item].price;
                    CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
                    OnScreen(player, GetMSG("purchaseitem", pending.gear[item].shortname, pending.setname));
                    timer.Once(3, () => { OpenACUI(player); });
                }
                else
                {
                    playerData.players[player.userID].GearCollections.Add(item, new List<string>());
                    playerData.players[player.userID].GearCollectionKills.Add(item, 0);
                    foreach (var entry in gwData.GearSets[item].set.Where(kvp => kvp.free == true))
                        playerData.players[player.userID].GearCollections[item].Add(entry.shortname);
                    DestroyACPanel(player);
                    if (configData.UseServerRewards)
                        SRAction(player.userID, gwData.GearSets[item].cost, "REMOVE");
                    else if (configData.UseEconomics)
                        ECOAction(player.userID, gwData.GearSets[item].cost, "REMOVE");
                    else
                        money -= gwData.GearSets[item].cost;
                    PlayerHUD(player);
                    CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
                    OnScreen(player, GetMSG("purchaseset", item));
                    timer.Once(3, () => { OpenACUI(player); });
                }
            }
            else if (pending.weaponpurchase == true)
            {
                if (pending.set == false)
                {
                    if (pending.attachmentpurchase == true)
                    {
                        playerData.players[player.userID].WeaponCollections[pending.setname][item].Add(pending.attachmentName);
                        DestroyACPanel(player);
                        if (configData.UseServerRewards)
                            SRAction(player.userID, pending.attachment[pending.weapon[item].slot][pending.attachmentName].cost, "REMOVE");
                        else if (configData.UseEconomics)
                            ECOAction(player.userID, pending.attachment[pending.weapon[item].slot][pending.attachmentName].cost, "REMOVE");
                        else
                            money -= pending.attachment[pending.weapon[item].slot][pending.attachmentName].cost;
                        CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
                        OnScreen(player, GetMSG("purchaseattachment", pending.attachment[pending.weapon[item].slot][pending.attachmentName].shortname, pending.weapon[item].shortname));
                        timer.Once(3, () => { OpenACUI(player); });
                    }
                    else
                    {
                        playerData.players[player.userID].WeaponCollections[pending.setname].Add(item, new List<string>());
                        DestroyACPanel(player);
                        if (configData.UseServerRewards)
                            SRAction(player.userID, pending.weapon[item].price, "REMOVE");
                        else if (configData.UseEconomics)
                            ECOAction(player.userID, pending.weapon[item].price, "REMOVE");
                        else
                            money -= pending.weapon[item].price;
                        CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
                        OnScreen(player, GetMSG("purchaseweapon", pending.weapon[item].shortname, pending.setname));
                        timer.Once(3, () => { OpenACUI(player); });
                    }
                }
                else
                {
                    playerData.players[player.userID].WeaponCollections.Add(item, new Dictionary<string, List<string>>());
                    playerData.players[player.userID].WeaponCollectionKills.Add(item, 0);
                    foreach (var entry in gwData.WeaponSets[item].set.Where(kvp => kvp.free == true))
                        playerData.players[player.userID].WeaponCollections[item].Add(entry.shortname, new List<string>());
                    DestroyACPanel(player);
                    if (configData.UseServerRewards)
                        SRAction(player.userID, gwData.WeaponSets[item].cost, "REMOVE");
                    else if (configData.UseEconomics)
                        ECOAction(player.userID, gwData.WeaponSets[item].cost, "REMOVE");
                    else
                        money -= gwData.WeaponSets[item].cost;
                    PlayerHUD(player);
                    CuiHelper.DestroyUi(player, PanelPurchaseConfirmation);
                    OnScreen(player, GetMSG("purchaseweaponset", item));
                    timer.Once(3, () => { OpenACUI(player); });
                }
            }
        }

        [ConsoleCommand("UI_SaveCollect")]
        private void cmdUI_SaveCollect(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var type = arg.Args[0];
            if (SavingCollection.ContainsKey(player.userID))
                SavingCollection.Remove(player.userID);
            SavingCollection.Add(player.userID, type);
            if (type == "gear")
            {
                foreach (var entry in NewGearCollection[player.userID].collection.set)
                    if (entry.Value.price == 0 && entry.Value.killsrequired == 0)
                        entry.Value.free = true;
            }
            if (type == "weapon")
            {
                foreach (var entry in NewWeaponCollection[player.userID].collection.set)
                {
                    if (entry.Value.price == 0 && entry.Value.killsrequired == 0)
                        entry.Value.free = true;
                    foreach (var attachment in entry.Value.attachments)
                        if (attachment.Value.cost == 0 && attachment.Value.killsrequired == 0)
                            attachment.Value.free = true;
                }
            }
            SelectIfFree(player, "collection", type);
        }

        [ConsoleCommand("UI_CreateGearSet")]
        private void cmdUI_CreateGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CreateGearSet(player);
        }
        [ConsoleCommand("UI_CancelGearSet")]
        private void cmdUI_CancelGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewGearCollection.ContainsKey(player.userID))
                NewGearCollection.Remove(player.userID);
            DestroyACPanel(player);
            OpenACUI(player);
        }

        [ConsoleCommand("UI_CancelWeaponSet")]
        private void cmdUI_CancelWeaponSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            if (NewWeaponCollection.ContainsKey(player.userID))
                NewWeaponCollection.Remove(player.userID);
            DestroyACPanel(player);
            OpenACUI(player);
        }

        [ConsoleCommand("UI_CreateWeaponSet")]
        private void cmdUI_CreateWeaponSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            CreateWeaponSet(player);
        }

        [ConsoleCommand("UI_DeleteGearSet")]
        private void cmdUI_DeleteGearSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = ACUIInfo[player.userID].GearSet;
            if (gwData.GearSets.ContainsKey(set))
            {
                gwData.GearSets.Remove(set);
                foreach (var entry in ACUIInfo)
                {
                    if (entry.Value.GearSet == set)
                        entry.Value.GearSet = "";
                }
                foreach (BasePlayer p in BasePlayer.activePlayerList)
                {
                    if (ACUIInfo[player.userID].open)
                    { DestroyACPanel(p); OpenACUI(p); }
                    CheckCollections(p);
                }
            }
            OpenACUI(player);
        }

        [ConsoleCommand("UI_DeleteWeaponSet")]
        private void cmdUI_DeleteWeaponSet(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var set = ACUIInfo[player.userID].WeaponSet;
            if (gwData.WeaponSets.ContainsKey(set))
            {
                gwData.WeaponSets.Remove(set);
                foreach (var entry in ACUIInfo)
                {
                    if (entry.Value.WeaponSet == set)
                        entry.Value.WeaponSet = "";
                }
                foreach (BasePlayer p in BasePlayer.activePlayerList)
                {
                    if (ACUIInfo[player.userID].open)
                    { DestroyACPanel(p); OpenACUI(p); }
                    CheckCollections(p);
                }
            }
            OpenACUI(player);
        }

        [ConsoleCommand("UI_SelectSkin")]
        private void cmdUI_SelectSkin(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;
            var item = arg.Args[0];
            ulong skin;
            if (!ulong.TryParse(arg.Args[1], out skin)) skin = 0;
            var type = arg.Args[2];
            if (type == "gear")
                NewGearCollection[player.userID].collection.set[item].skin = skin;
            else if (type == "weapon")
                NewWeaponCollection[player.userID].collection.set[item].skin = skin;
            DestroyACPanel(player);
            OpenACUI(player);
        }



        #endregion

        #region Item Management

        private void SelectionGearCollection(BasePlayer player, string name)
        {
            if (!PlayerGearSetTimer.ContainsKey(player.userID))
            {
                StripGear(player);
                GiveGearCollection(player);
                PlayerHUD(player);
                DestroyACPanel(player);
                TimerPlayerGearSetselection(player);
            }
            else
            {
                GetSendMSG(player, "GearSetCooldown", playerData.players[player.userID].Gear.collectionname);
                PlayerHUD(player);
            }
        }

        private void TimerPlayerGearSetselection(BasePlayer player)
        {
            if (PlayerGearSetTimer.ContainsKey(player.userID))
            {
                PlayerGearSetTimer.Remove(player.userID);
            }
            else PlayerGearSetTimer.Add(player.userID, timer.Once(configData.Cooldown * 60, () => TimerPlayerGearSetselection(player)));
        }

        private void StripWeapons(BasePlayer player)
        {
            if (playerData.players[player.userID].Weapons.weapons == null) return;
            foreach (var item in player.inventory.AllItems())
                if (playerData.players[player.userID].Weapons.weapons.Contains(item.uid))
                    item.RemoveFromContainer();
        }

        private void StripGear(BasePlayer player)
        {
            if (playerData.players[player.userID].Gear.gear == null) return;
            foreach (var item in player.inventory.AllItems())
                if (playerData.players[player.userID].Gear.gear.Contains(item.uid))
                    item.RemoveFromContainer();
        }


        private void SelectWeapons(BasePlayer player)
        {
            if (!PlayerWeaponSetTimer.ContainsKey(player.userID))
            {
                StripWeapons(player);
                GiveWeaponCollection(player);
                PlayerHUD(player);
                DestroyACPanel(player);
                TimerPlayerWeaponselection(player);
            }
            else
            {
                GetSendMSG(player, "WeaponSetCooldown", playerData.players[player.userID].Weapons.collectionname);
                PlayerHUD(player);
            }
        }

        private void TimerPlayerWeaponselection(BasePlayer player)
        {
            if (PlayerWeaponSetTimer.ContainsKey(player.userID))
            {
                PlayerWeaponSetTimer.Remove(player.userID);
            }
            else PlayerWeaponSetTimer.Add(player.userID, timer.Once(configData.Cooldown * 60, () => TimerPlayerGearSetselection(player)));
        }

        private void GiveGearCollection(BasePlayer player)
        {
            if (playerData.players[player.userID].Gear.collectionname == null) return;
            if (playerData.players[player.userID].Gear.gear != null)
                playerData.players[player.userID].Gear.gear.Clear();
            else playerData.players[player.userID].Gear.gear = new List<uint>();
            var set = gwData.GearSets[playerData.players[player.userID].Gear.collectionname];
            foreach (var item in set.set)
                if (playerData.players[player.userID].GearCollections[playerData.players[player.userID].Gear.collectionname].Contains(item.shortname))
                {
                    var gear = BuildSet(item);
                    playerData.players[player.userID].Gear.gear.Add(gear.uid);
                    GiveItem(player, gear, item.container);
                }
            PlayerHUD(player);
        }
        private void GiveWeaponCollection(BasePlayer player)
        {
            if (playerData.players[player.userID].Weapons.collectionname == null || playerData.players[player.userID].WeaponSelection.Count() < 1 /*|| playerData.players[player.userID].Weapons.weapons == null*/) return;
            if (playerData.players[player.userID].Weapons.weapons != null)
                playerData.players[player.userID].Weapons.weapons.Clear();
            else playerData.players[player.userID].Weapons.weapons = new List<uint>();
            foreach (var entry in playerData.players[player.userID].WeaponSelection[playerData.players[player.userID].Weapons.collectionname])
                foreach (var weapon in gwData.WeaponSets[playerData.players[player.userID].Weapons.collectionname].set.Where(kvp => kvp.shortname == entry.Key))
                    BuildWeapon(weapon, player);
            PlayerHUD(player);
        }

        private void BuildWeapon(Weapon weapon, BasePlayer player)
        {
            if (weapon == null) return;
            var definition = ItemManager.FindItemDefinition(weapon.shortname);
            if (definition != null)
            {
                var item = ItemManager.Create(definition, weapon.amount, weapon.skin);
                if (item != null)
                {
                    var held = item.GetHeldEntity() as BaseProjectile;
                    if (held != null)
                    {
                        if (!string.IsNullOrEmpty(weapon.ammoType))
                        {
                            var ammoType = ItemManager.FindItemDefinition(weapon.ammoType);
                            if (ammoType != null)
                                held.primaryMagazine.ammoType = ammoType;
                        }
                        held.primaryMagazine.contents = held.primaryMagazine.capacity;
                    }
                    if (weapon.ammo == 0) weapon.ammo = held.primaryMagazine.capacity * configData.DefaultAmmoReloads;
                    if (weapon.ammo < configData.DefaultAmmoReloads * 4)
                        weapon.ammo = 128;
                    if (string.IsNullOrEmpty(weapon.ammoType))
                    {
                        var ammo = BuildAmmo(held.primaryMagazine.ammoType.shortname, weapon.ammo);
                        playerData.players[player.userID].Weapons.weapons.Add(ammo.uid);
                        GiveItem(player,ammo, "");
                    }
                    else
                    {
                        var ammo = BuildAmmo(weapon.ammoType, weapon.ammo);
                        playerData.players[player.userID].Weapons.weapons.Add(ammo.uid);
                        GiveItem(player, ammo, "");
                    }
                    if (playerData.players[player.userID].WeaponSelection[playerData.players[player.userID].Weapons.collectionname][weapon.shortname] == null) { GiveItem(player, item, weapon.container); return; }
                    foreach (var attachment in playerData.players[player.userID].WeaponSelection[playerData.players[player.userID].Weapons.collectionname][weapon.shortname])
                    {
                        var att = BuildItem(attachment);
                        att.MoveToContainer(item.contents);
                        playerData.players[player.userID].Weapons.weapons.Add(att.uid);
                    }
                }
                playerData.players[player.userID].Weapons.weapons.Add(item.uid);
                GiveItem(player, item, weapon.container);
                return;
            }
            Puts("Error making item: " + weapon.shortname);
            return;
        }

        private Item BuildAmmo(string shortname, int amount)
        {
            var definition = ItemManager.FindItemDefinition(shortname);
            if (definition != null)
            {
                var item = ItemManager.Create(definition, amount, 0);
                if (item != null)
                    return item;
            }
            Puts("Error making ammo!");
            return null;
        }

        private Item BuildSet(Gear gear)
        {
            var definition = ItemManager.FindItemDefinition(gear.shortname);
            if (definition != null)
            {
                var item = ItemManager.Create(definition, gear.amount, gear.skin);
                if (item != null)
                    return item;
            }
            Puts("Error making item: " + gear.shortname);
            return null;
        }

        private Item BuildItem(string shortname, int amount = 1, ulong skin = 0)
        {
            var definition = ItemManager.FindItemDefinition(shortname);
            if (definition != null)
            {
                var item = ItemManager.Create(definition, amount, skin);
                if (item != null)
                    return item;
            }
            Puts("Error making attachment: " + shortname);
            return null;
        }

        public void GiveItem(BasePlayer player, Item item, string container)
        {
            if (item == null) return;
            ItemContainer cont;
            switch (container)
            {
                case "wear":
                    cont = player.inventory.containerWear;
                    break;
                case "belt":
                    cont = player.inventory.containerBelt;
                    break;
                default:
                    cont = player.inventory.containerMain;
                    break;
            }
            player.inventory.GiveItem(item, cont);
        }

        public void CreateGearSet(BasePlayer player)
        {
            if (NewGearCollection.ContainsKey(player.userID))
                NewGearCollection.Remove(player.userID);
            NewGearCollection.Add(player.userID, new GearCollectionCreation());
            ACUIInfo[player.userID].gear = true;
            ACUIInfo[player.userID].weapon = false;
            DestroyACPanel(player);
            OpenACUI(player);
        }

        public void CreateWeaponSet(BasePlayer player)
        {
            if (NewWeaponCollection.ContainsKey(player.userID))
                NewWeaponCollection.Remove(player.userID);
            NewWeaponCollection.Add(player.userID, new WeaponCollectionCreation());
            ACUIInfo[player.userID].gear = false;
            ACUIInfo[player.userID].weapon = true;
            DestroyACPanel(player);
            OpenACUI(player);
        }

        #endregion

        #region Classes
        enum Slot
        {
            head,
            chest,
            chest2,
            legs,
            legs2,
            feet,
            hands,
            main,
            secondary,
            attachment1,
            attachment2,
            attachment3,
            accessories1,
            accessories2,
            weapon,
            ammunitionMain,
            ammunitionSecondary
        }

        class Gear_Weapon_Data
        {
            public Dictionary<string, GearSet> GearSets = new Dictionary<string, GearSet>();
            public Dictionary<string, WeaponSet> WeaponSets = new Dictionary<string, WeaponSet>();
            public Dictionary<Slot, List<string>> Items = new Dictionary<Slot, List<string>>();
        }

        class SavedPlayer
        {
            public Dictionary<ulong, Player> players = new Dictionary<ulong, Player>();
        }

        class Player
        {
            public int kills;
            public int deaths;
            public int money;
            public int TotalKills;
            public int TotalDeaths;
            public CurrentWeapons Weapons = new CurrentWeapons();
            public CurrentGear Gear = new CurrentGear();
            public Dictionary<string, Dictionary<string, List<string>>> WeaponSelection = new Dictionary<string, Dictionary<string, List<string>>>();
            public Dictionary<string, List<string>> GearCollections = new Dictionary<string, List<string>>();
            public Dictionary<string, Dictionary<string, List<string>>> WeaponCollections = new Dictionary<string, Dictionary<string, List<string>>>();
            public Dictionary<string, int> GearCollectionKills = new Dictionary<string, int>();
            public Dictionary<string, int> WeaponCollectionKills = new Dictionary<string, int>();
        }

        class GearSet
        {
            public int index;
            public int cost;
            public int killsrequired;
            public List<Gear> set = new List<Gear>();
        }
        
        class CurrentWeapons
        {
            public string collectionname;
            public List<uint> weapons;
        }

        class CurrentGear
        {
            public string collectionname;
            public List<uint> gear;
        }

        class WeaponCollectionCreation
        {
            public string setname;
            public CreationWeaponSet collection = new CreationWeaponSet();
        }

        class GearCollectionCreation
        {
            public string setname;
            public CreationGearSet collection = new CreationGearSet();
        }


        class CreationGearSet
        {
            public int index;
            public int cost;
            public int killsrequired;
            public bool free;
            public Dictionary<string, Gear> set = new Dictionary<string, Gear>();
        }

        class CreationWeaponSet
        {
            public int index;
            public int cost;
            public int killsrequired;
            public bool free;
            public string currentweapon;
            public Dictionary<string, Weapon> set = new Dictionary<string, Weapon>();
        }

        class WeaponSet
        {
            public int index;
            public int cost;
            public int killsrequired;
            public List<Weapon> set = new List<Weapon>();
        }


        class Attachment
        {
            public bool free;
            public string shortname;
            public int cost;
            public int killsrequired;
            public Slot slot;
            public string location;
        }

        class PurchaseItem
        {
            public Dictionary<string, Gear> gear = new Dictionary<string, Gear>();
            public Dictionary<string, Weapon> weapon = new Dictionary<string, Weapon>();
            public Dictionary<Slot,Dictionary<string, Attachment>> attachment = new Dictionary<Slot, Dictionary<string, Attachment>>();
            public string setname;
            public bool set = true;
            public bool weaponpurchase;
            public bool gearpurchase;
            public bool attachmentpurchase;
            public string attachmentName;
            public int setprice;
            public int setkillrequirement;
        }

        class Gear
        {
            public bool free;
            public string shortname;
            public Slot slot;
            public int price;
            public ulong skin;
            public int killsrequired;
            public int amount;
            public string container;
        }

        class Weapon
        {
            public bool free;
            public string shortname;
            public ulong skin;
            public Slot slot;
            public string container;
            public int killsrequired;
            public int price;
            public int amount;
            public int ammo;
            public string ammoType;
            public Dictionary<string, Attachment> attachments = new Dictionary<string, Attachment>();
        }

        #endregion

        #region Timers

        private void SaveLoop()
        {
            if (timers.ContainsKey("save"))
            {
                timers["save"].Destroy();
                timers.Remove("save");
            }
            SaveData();
            timers.Add("save", timer.Once(600, () => SaveLoop()));
        }

        private void InfoLoop()
        {
            if (timers.ContainsKey("info"))
            {
                timers["info"].Destroy();
                timers.Remove("info");
            }
            if (configData.InfoInterval == 0) return;
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                GetSendMSG(p, "ACInfo", configData.MenuKeyBinding.ToUpper());
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }

        private void CondLoop()
        {
            if (timers.ContainsKey("cond"))
            {
                timers["cond"].Destroy();
                timers.Remove("cond");
            }
            if (!configData.PersistentCondition) return;
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                p.metabolism.calories.value = 500;
                p.metabolism.hydration.value = 250;
            }
            timers.Add("cond", timer.Once(120, () => CondLoop()));
        }


        #endregion

        #region External Hooks

        [HookMethod("GetPlayerKills")]
        public int GetPlayerKills(ulong PlayerID)
        {
            if (!playerData.players.ContainsKey(PlayerID)) return 0;
            return playerData.players[PlayerID].kills;
        }
        [HookMethod("GetPlayerTotalKills")]
        public int GetPlayerTotalKills(ulong PlayerID)
        {
            if (!playerData.players.ContainsKey(PlayerID)) return 0;
            return playerData.players[PlayerID].TotalKills;
        }
        [HookMethod("GetPlayerDeaths")]
        public int GetPlayerDeaths(ulong PlayerID)
        {
            if (!playerData.players.ContainsKey(PlayerID)) return 0;
            return playerData.players[PlayerID].deaths;
        }
        [HookMethod("GetPlayerTotalDeaths")]
        public int GetPlayerTotalDeaths(ulong PlayerID)
        {
            if (!playerData.players.ContainsKey(PlayerID)) return 0;
            return playerData.players[PlayerID].TotalDeaths;
        }

        [HookMethod("GiveCollections")]
        public bool GiveCollections(BasePlayer player)
        {
            GiveGearCollection(player);
            GiveWeaponCollection(player);
            return true;
        }

        [HookMethod("AddMoney")]
        object AddMoney(ulong TargetID, int amount, bool notify = true, ulong RequestorID = 0)
        {
            if (!playerData.players.ContainsKey(TargetID)) return false;
            try
            {
                playerData.players[TargetID].money += amount;
                BasePlayer target = BasePlayer.FindByID(TargetID);
                PlayerHUD(target);
                if (notify)
                    GetSendMSG(target, "AddMoney", amount.ToString());
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "MoneyAdded", target.displayName, amount.ToString());
                }
                return true;
            }
            catch
            {
                playerData.players[TargetID].money += amount;
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "MoneyAddedOffline", amount.ToString());
                }
                return true;
            }
        }

        [HookMethod("TakeMoney")]
        object TakeMoney(ulong TargetID, int amount, bool notify = true, ulong RequestorID = 0)
        {
            if (!playerData.players.ContainsKey(TargetID)) return false;
            try
            {
                playerData.players[TargetID].money -= amount;
                BasePlayer target = BasePlayer.FindByID(TargetID);
                PlayerHUD(target);
                if (notify)
                    GetSendMSG(target, "TakeMoney", amount.ToString());
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "MoneyTaken", target.displayName, amount.ToString());
                }
                return true;
            }
            catch
            {
                playerData.players[TargetID].money -= amount;
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "MoneyTakenOffline", amount.ToString());
                }
                return true;
            }
        }

        object AddKills(ulong TargetID, int amount, string type, string collection, ulong RequestorID = 0)
        {
            if (!playerData.players.ContainsKey(TargetID)) return false;
            try
            {
                playerData.players[TargetID].money -= amount;
                BasePlayer target = BasePlayer.FindByID(TargetID);
                if (type == "gear")
                    playerData.players[TargetID].GearCollectionKills[collection] += amount;
                if (type == "weapon")
                    playerData.players[TargetID].WeaponCollectionKills[collection] += amount;
                GetSendMSG(target, "AddKills", amount.ToString(), collection.ToUpper());
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "KillsAdded", target.displayName, amount.ToString(), collection.ToUpper());
                }
                return true;
            }
            catch
            {
                if (type == "gear")
                    playerData.players[TargetID].GearCollectionKills[collection] += amount;
                if (type == "weapon")
                    playerData.players[TargetID].WeaponCollectionKills[collection] += amount;
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "KillsAddedOffline", TargetID.ToString(), amount.ToString(), collection.ToUpper());
                }
                return true;
            }
        }

        object TakeKills(ulong TargetID, int amount, string type, string collection, ulong RequestorID = 0)
        {
            if (!playerData.players.ContainsKey(TargetID)) return false;
            try
            {
                playerData.players[TargetID].money -= amount;
                BasePlayer target = BasePlayer.FindByID(TargetID);
                if (type == "gear")
                {
                    playerData.players[TargetID].GearCollectionKills[collection] -= amount;
                    if (playerData.players[TargetID].GearCollectionKills[collection] < 0)
                        playerData.players[TargetID].GearCollectionKills[collection] = 0;
                }
                if (type == "weapon")
                {
                    playerData.players[TargetID].WeaponCollectionKills[collection] -= amount;
                    if (playerData.players[TargetID].WeaponCollectionKills[collection] < 0)
                        playerData.players[TargetID].WeaponCollectionKills[collection] = 0;
                }
                GetSendMSG(target, "TakeKills", amount.ToString(), collection.ToUpper());
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "KillsTakenOffline", TargetID.ToString(), amount.ToString(), collection.ToUpper());
                }
                return true;
            }
            catch
            {
                if (type == "gear")
                {
                    playerData.players[TargetID].GearCollectionKills[collection] -= amount;
                    if (playerData.players[TargetID].GearCollectionKills[collection] < 0)
                        playerData.players[TargetID].GearCollectionKills[collection] = 0;
                }
                if (type == "weapon")
                {
                    playerData.players[TargetID].WeaponCollectionKills[collection] -= amount;
                    if (playerData.players[TargetID].WeaponCollectionKills[collection] < 0)
                        playerData.players[TargetID].WeaponCollectionKills[collection] = 0;
                }
                if (RequestorID != 0)
                {
                    BasePlayer requestor = BasePlayer.FindByID(RequestorID);
                    GetSendMSG(requestor, "KillsTakenOffline", TargetID.ToString(), amount.ToString(), collection.ToUpper());
                }
                return true;
            }
        }
        #endregion

        #region GWData Management
        private Dictionary<Slot, List<string>> DefaultItems = new Dictionary<Slot, List<string>>
            {
                {Slot.chest, new List<string>
                {
                    "tshirt",
                    "tshirt.long",
                    "shirt.collared",
                    "shirt.tanktop",
                    "jacket",
                    "jacket.snow",
                    "hoodie",
                    "burlap.shirt",
                    "hazmat.jacket",
                    "hazmatsuit",
                }
                },
            {Slot.chest2, new List<string>
            {
                "wood.armor.jacket",
                "roadsign.jacket",
                "metal.plate.torso",
                "bone.armor.suit",
                "attire.hide.vest",
                "attire.hide.poncho",
                "attire.hide.helterneck",
            }
            },
            {Slot.legs, new List<string>
                {
                    "pants",
                    "pants.shorts",
                    "hazmat.pants",
                    "burlap.trousers",
                    "attire.hide.pants",
                    "attire.hide.skirt",
                }
                },
            {Slot.legs2, new List<string>
                {
                    "wood.armor.pants",
                    "roadsign.kilt",
                }
                },
            {Slot.head, new List<string>
                {
                    "mask.bandana",
                    "mask.balaclava",
                    "hat.cap",
                    "hat.beenie",
                    "bucket.helmet",
                    "hat.boonie",
                    "santahat",
                    "riot.helmet",
                    "metal.facemask",
                    "hazmat.helmet",
                    "hat.miner",
                    "hat.candle",
                    "coffeecan.helmet",
                    "burlap.headwrap",
                }
                },
            {Slot.hands, new List<string>
                {
                    "burlap.gloves",
                    "hazmat.gloves",
                }
                },
            {Slot.feet, new List<string>
                {
                    "shoes.boots",
                    "hazmat.boots",
                    "burlap.shoes",
                    "attire.hide.boots",
                }
                },
                {Slot.secondary, new List<string>
                {
                    "pistol.revolver",
                    "pistol.semiauto",
                    "rifle.ak",
                    "rifle.bolt",
                    "shotgun.pump",
                    "shotgun.waterpipe",
                    "rifle.lr300",
                    "crossbow",
                    "smg.thompson",
                    "spear.wooden",
                    "spear.stone",
                    "smg.2",
                    "shotgun.double",
                    "salvaged.sword",
                    "salvaged.cleaver",
                    "rocket.launcher",
                    "rifle.semiauto",
                    "pistol.eoka",
                    "machete",
                    "mace",
                    "longsword",
                    "lmg.m249",
                    "knife.bone",
                    "flamethrower",
                    "bow.hunting",
                    "bone.club",
                    "pistol.m92",
                    "smg.mp5",
}
                },
                {Slot.main, new List<string>
                {
                    "pistol.revolver",
                    "pistol.semiauto",
                    "rifle.ak",
                    "rifle.bolt",
                    "shotgun.pump",
                    "shotgun.waterpipe",
                    "rifle.lr300",
                    "crossbow",
                    "smg.thompson",
                    "spear.wooden",
                    "spear.stone",
                    "smg.2",
                    "shotgun.double",
                    "salvaged.sword",
                    "salvaged.cleaver",
                    "rocket.launcher",
                    "rifle.semiauto",
                    "pistol.eoka",
                    "machete",
                    "mace",
                    "longsword",
                    "lmg.m249",
                    "knife.bone",
                    "flamethrower",
                    "bow.hunting",
                    "bone.club",
                    "pistol.m92",
                    "smg.mp5",
                }
                },
            {Slot.attachment1, new List<string>
                {
                    "weapon.mod.lasersight",
                    "weapon.mod.flashlight",
                    "weapon.mod.silencer",
                    "weapon.mod.muzzlebrake",
                    "weapon.mod.muzzleboost",
                    "weapon.mod.small.scope",
                    "weapon.mod.holosight",
                }
                },
                {Slot.attachment2, new List<string>
                {
                    "weapon.mod.lasersight",
                    "weapon.mod.flashlight",
                    "weapon.mod.silencer",
                    "weapon.mod.muzzlebrake",
                    "weapon.mod.muzzleboost",
                    "weapon.mod.small.scope",
                    "weapon.mod.holosight",
                }
                },

                {Slot.attachment3, new List<string>
                {
                    "weapon.mod.lasersight",
                    "weapon.mod.flashlight",
                    "weapon.mod.silencer",
                    "weapon.mod.muzzlebrake",
                    "weapon.mod.muzzleboost",
                    "weapon.mod.small.scope",
                    "weapon.mod.holosight",
                }
                },

            {Slot.accessories1, new List<string>
                {
                    "bandage",
                    "syringe.medical",
                    "largemedkit",
                    "grenade.beancan",
                    "grenade.f1",
                    "flare",
                    "fun.guitar",
                    "building.planner",
                    "hammer",
                    "explosive.timed",
                    "explosive.satchel",
                }
                },
            {Slot.accessories2, new List<string>
                {
                    "bandage",
                    "syringe.medical",
                    "largemedkit",
                    "grenade.beancan",
                    "grenade.f1",
                    "flare",
                    "fun.guitar",
                    "building.planner",
                    "hammer",
                    "explosive.timed",
                    "explosive.satchel",
                }
                },
            {Slot.ammunitionMain, new List<string>
                {
                    "ammo.rifle.hv",
                    "ammo.rifle.explosive",
                    "ammo.rifle.incendiary",
                    "ammo.rifle",
                    "ammo.pistol.hv",
                    "ammo.pistol.fire",
                    "ammo.pistol",
                    "ammo.handmade.shell",
                    "ammo.rocket.basic",
                    "ammo.rocket.hv",
                    "ammo.rocket.fire",
                    "ammo.rocket.smoke",
                    "ammo.shotgun",
                    "ammo.shotgun.slug",
                    "arrow.hv",
                    "arrow.wooden",
            }
                },
            {Slot.ammunitionSecondary, new List<string>
                {
                    "ammo.rifle.hv",
                    "ammo.rifle.explosive",
                    "ammo.rifle.incendiary",
                    "ammo.rifle",
                    "ammo.pistol.hv",
                    "ammo.pistol.fire",
                    "ammo.pistol",
                    "ammo.handmade.shell",
                    "ammo.rocket.basic",
                    "ammo.rocket.hv",
                    "ammo.rocket.fire",
                    "ammo.rocket.smoke",
                    "ammo.shotgun",
                    "ammo.shotgun.slug",
            }
                }
            };

        private Dictionary<string, Attachment> DefaultAttachments = new Dictionary<string, Attachment>
        {
                        { "weapon.mod.muzzleboost", new Attachment
                        {
                            location = "front",
                        }
                        },
                        { "weapon.mod.flashlight", new Attachment
                        {
                            location = "middle",
                        }
                        },
                        { "weapon.mod.silencer", new Attachment
                        {
                            location = "front",
                            }
                        },
                        { "weapon.mod.muzzlebrake", new Attachment
                        {
                            location = "front",
                            }
                        },
                        { "weapon.mod.small.scope", new Attachment
                        {
                            location = "back",
                            }
                        },
                        { "weapon.mod.lasersight", new Attachment
                        {
                            location = "middle",
                            }
                        },
                        { "weapon.mod.holosight", new Attachment
                        {
                            location = "back",
                        }
                        }
        };

        private Dictionary<string, GearSet> DefaultGearSets = new Dictionary<string, GearSet>
                {
                    {"Starter", new GearSet{cost = 0, killsrequired = 0, index = 0, set = new List<Gear>
                    { new Gear
                        {
                        shortname = "tshirt",
                        skin = 10056,
                        slot = Slot.chest,
                        container = "wear",
                        amount = 1,
                        killsrequired = 0,
                        price = 0,
                        },
                        new Gear
                        {
                       shortname = "pants",
                        skin = 10001,
                        slot = Slot.legs,
                        container = "wear",
                        amount = 1,
                        killsrequired = 0,
                        price = 0,
                        },
                        new Gear
                        {
                        shortname = "shoes.boots",
                        skin = 10044,
                        slot = Slot.feet,
                        container = "wear",
                        amount = 1,
                        killsrequired = 0,
                        price = 0,
                        },
                        new Gear
                        {
                       shortname = "hat.cap",
                        skin = 10055,
                        slot = Slot.head,
                        container = "wear",
                        amount = 1,
                        killsrequired = 0,
                        price = 0,
                        },
                        new Gear
                        {
                        shortname = "burlap.gloves",
                        skin = 10128,
                        slot = Slot.hands,
                        container = "wear",
                        amount = 1,
                        killsrequired = 0,
                        price = 0,
                        }
                     } } },
                {"First", new GearSet{cost = 0, killsrequired = 10, index = 1, set = new List<Gear>
                    { new Gear
                        {
                        shortname = "hoodie",
                        skin = 10129,
                        slot = Slot.chest,
                        container = "wear",
                        amount = 1,
                        killsrequired = 5,
                        price = 5,
                        },
                        new Gear
                        {
                       shortname = "pants",
                        skin = 10001,
                        slot = Slot.legs,
                        container = "wear",
                        amount = 1,
                        killsrequired = 5,
                        price = 5,
                        },
                        new Gear
                        {
                        shortname = "shoes.boots",
                        skin = 10023,
                        slot = Slot.feet,
                        container = "wear",
                        amount = 1,
                        killsrequired = 5,
                        price = 5,
                        },
                        new Gear
                        {
                       shortname = "coffeecan.helmet",
                        skin = 0,
                        slot = Slot.head,
                        container = "wear",
                        amount = 1,
                        killsrequired = 5,
                        price = 5,
                        },
                        new Gear
                        {
                        shortname = "wood.armor.pants",
                        skin = 0,
                        slot = Slot.legs2,
                        container = "wear",
                        amount = 1,
                        killsrequired = 5,
                        price = 5,
                        },
                        new Gear
                        {
                        shortname = "wood.armor.jacket",
                        skin = 0,
                        slot = Slot.chest2,
                        container = "wear",
                        amount = 1,
                        killsrequired = 5,
                        price = 5,
                        }
                     } } }
        };

        private Dictionary<string, WeaponSet> DefaultWeaponSets = new Dictionary<string, WeaponSet>
                {
                    {"Starter", new WeaponSet{cost = 0, killsrequired = 0, index = 0, set = new List<Weapon>
                    { new Weapon
                        {
                        shortname = "bow.hunting",
                        skin = 0,
                        slot = Slot.main,
                        container = "belt",
                        amount = 1,
                        ammo = 128,
                        ammoType = "arrow.wooden",
                        killsrequired = 0,
                        price = 0,
                        },
                        new Weapon
                        {
                        shortname = "knife.bone",
                        skin = 0,
                        slot = Slot.secondary,
                        container = "belt",
                        amount = 1,
                        ammo = 0,
                        ammoType = "",
                        killsrequired = 0,
                        price = 0,
                        }
                     } } },
                {"First", new WeaponSet{cost = 0, killsrequired = 0, index = 1, set = new List<Weapon>
                    { new Weapon
                        {
                        shortname = "smg.thompson",
                        skin = 10120,
                        slot = Slot.main,
                        container = "belt",
                        amount = 1,
                        ammo = 128,
                        ammoType = "ammo.pistol",
                        killsrequired = 10,
                        price = 20,
                        attachments = new Dictionary<string, Attachment>
                        {
                            {"weapon.mod.muzzleboost", new Attachment {cost = 5, killsrequired = 5, slot = Slot.attachment1,location = "front", shortname = "weapon.mod.muzzleboost" } },
                            {"weapon.mod.lasersight", new Attachment {cost = 5, killsrequired = 5,  slot = Slot.attachment2,location = "middle", shortname = "weapon.mod.lasersight" } },
                            {"weapon.mod.holosight", new Attachment {cost = 5, killsrequired = 5,  slot = Slot.attachment3,location = "back", shortname = "weapon.mod.holosight" } }
                        } },
                        new Weapon
                        {
                        shortname = "pistol.semiauto",
                        skin = 10087,
                        slot = Slot.secondary,
                        container = "belt",
                        amount = 1,
                        ammo = 128,
                        ammoType = "ammo.pistol",
                        killsrequired = 10,
                        price = 20,
                        attachments = new Dictionary<string, Attachment>
                        {
                            {"weapon.mod.flashlight", new Attachment {cost = 5, killsrequired = 5,  slot = Slot.attachment1,location = "middle", shortname = "weapon.mod.flashlight" } },
                            {"weapon.mod.lasersight", new Attachment {cost = 5, killsrequired = 5,  slot = Slot.attachment2,location = "middle", shortname = "weapon.mod.lasersight" } },
                            {"weapon.mod.silencer", new Attachment {cost = 5, killsrequired = 5, slot = Slot.attachment3,location = "front", shortname = "weapon.mod.silencer" } }
                        } } } } }
        };

        void SaveData()
        {
            GWData.WriteObject(gwData);
            PlayerData.WriteObject(playerData);
        }

        void LoadData()
        {
            try
            {
                playerData = PlayerData.ReadObject<SavedPlayer>();
                if (playerData == null || playerData.players == null)
                {
                    Puts("Corrupt Data file....creating new datafile");
                    playerData = new SavedPlayer();
                }
            }
            catch
            {
                Puts("Couldn't load the Absolut Combat Saved Player Data, creating a new datafile");
                playerData = new SavedPlayer();
            }
            try
            {
                gwData = GWData.ReadObject<Gear_Weapon_Data>();
                if (gwData == null)
                {
                    Puts("Corrupt Data file....creating new datafile");
                    gwData = new Gear_Weapon_Data();
                }
            }
            catch
            {
                Puts("Couldn't load the Absolut Combat Gear and Weapons Data, creating a new datafile");
                gwData = new Gear_Weapon_Data();
            }
            if (gwData.Items == null || gwData.Items.Count() == 0)
                LoadDefaultItemsList();
            SaveData();
        }

        void LoadDefaultItemsList()
        {
            gwData.Items = DefaultItems;
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public int InfoInterval { get; set; }
            public int KillReward { get; set; }
            public bool BroadcastDeath { get; set; }
            public int Cooldown { get; set; }
            public bool PersistentCondition { get; set; }
            public string MenuKeyBinding { get; set; }
            public bool UseServerRewards { get; set; }
            public bool UseEconomics { get; set; }
            public int DefaultAmmoReloads { get; set; }
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
                KillReward = 5,
                BroadcastDeath = true,
                Cooldown = 10,
                InfoInterval = 15,
                PersistentCondition = true,
                MenuKeyBinding = "p",
                UseServerRewards = false,
                UseEconomics = false,
                DefaultAmmoReloads = 6,
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "Absolut Combat: " },
            {"ACInfo", "This server is running Absolut Combat. Press '{0}' to access the Menu. You need kills and money to unlock equipment! Goodluck!"},
            {"purchaseitem", "You have successfully purchased: {0} from the {1} Gear Collection." },
            {"purchaseset", "You have successfully unlocked the {0} Gear Collection" },
            {"purchaseattachment", "You have successfully purchased: {0} for the {1}." },
            {"purchaseweapon", "You have successfully purchased the {0} from the {1} Weapon Collection" },
            {"purchaseweaponset", "You have successfully unlocked the {0} Weapon Collection" },
            {"BuySubMenu", "Collection: {0}" },
            {"CurrentGearKills", "Gear Kills:{0}" },
            {"CurrentWeaponKills", "Weapon Kills:{0}" },
            {"TotalKills", "Total Kills: {0}" },
            {"CurrentlyEquipped", "This is Currently Equipped"},
            {"GearCollection", "Gear Collections" },
            {"WeaponCollection", "Weapon Collections" },
            {"PurchaseInfo", "Are you sure you want to purchase: {0} for ${1}?" },
            {"PurchaseSetInfo", "Are you sure you want to purchase the Collection: {0} for ${1}?" },
            {"DeathMessage", " has killed " },
            {"SelectCollectionItem", "Please Select an Item for this slot.</color>" },
            {"GearSkin", "Please select a Skin for {0}</color>" },
            {"UnlockFree", "Do you want this item to unlock for free when the Collection is purchased?" },
            {"UnlockCollectionFree", "Do you want this Collection to be FREE?" },
            {"INVALIDENTRY", "The given value: {0} is invalid for this input!" },
            {"INVALIDSLOT", "Invalid Slot Provided!" },
            {"Back", "Back" },
            {"Cancel", "Cancel" },
            {"Delete", "Delete" },
            {"ToggleAdminView", "Toggle Admin View" },
            {"Hud1", "Current Weapon Kills: {0}"},
            {"Hud2", "Current Gear Kills: {0}"},
            {"Hud3a", "SR Points: {0}"},
            {"Hud3b", "Money: {0}"},
            {"Hud4", "Current Weapon Collection: {0}"},
            {"Hud5", "Current Gear Collection: {0}"},
            {"Hud6", "Total Kills: {0}"},
            {"GearSetCooldown", "You have changed your Gear Collection to: {0} however you are on cooldown and will not get the equipment until respawn" },
            {"WeaponSetCooldown", "You have changed your Weapon Collection to: {0} however you are on cooldown and will not get the equipment until respawn" },
            {"AddMoney", "You have been given {0} in Absolut Combat Money!" },
            {"MoneyAdded", "{0} has successfully been given {1} in Absolut Combat Money!" },
            {"KillsAdded", "{0} has successfully been given {1} Kills for the {2} Collection." },
            {"KillsTaken", "{1} Kills for the {2} Collection have been taken from {0}." },
            {"KillsAddedOffline", "SteamID {0} has successfully been given {1} Kills for the {2} Collection but the player is currently offline."},
            {"KillsTakenOffline", "{1} Kills for the {2} Collection have been taken from SteamID {0} but the player is currently offline."},
            {"MoneyAddedOffline", "{0} has successfully been given {1} but the player is currently offline."},
            {"NotACPlayer", "The provided User ID does not match an Absolut Combat Player " },
            {"AddMoneyError", "There was an error when trying to give money" },
            {"TakeMoney", "{0} in Absolut Combat Money has been taken from you!" },
            {"MoneyTaken", "{1} in Absolut Combat Money has been taken from {0}!" },
            {"MoneyTakenOffline", "{0} has successfully been taken but the player is currently offline."},
            {"TakeMoneyError", "There was an error when trying to take money" },
            {"AddKillsError", "There was an error adding kills." },
            {"ClearAttachments", "Clear Attachments: {0}" },
            {"NewSet", "You have successfully created Collection: {0}" },
            {"NotAuthorized", "You are not authorized to use this function" },
            {"ArgumentsIncorrect", "You have provided the wrong format. You must specify: {0}. For Example: {1}" },
            {"NoPlayers", "No players found that match {0}" },
            {"MultiplePlayers", "Multiple players found that match {0}" },
            {"NOSR", "ServerRewards is missing. Unloading {0} as it will not work without ServerRewards. Consider setting Use_ServerRewards to false..." },
            {"NOECO","Economics is missing. Unloading {0} as it will not work without Economics. Consider setting Use_Economics to false..." },
            {"BOTHERROR", "Both ServerRewards and Economics are enabled and loaded. This will cause errors so please disable one in the Config." },
            {"PurchaseMenuPlayer", "Purchase Menu               Balance: {0}               Kills: {1}" },
            {"SelectionMenuPlayer", "Selection Menu               Balance: {0}               Kills: {1}" },
            {"EnterAdminView", "You have entered Admin View." },
            {"ExitAdminView", "You have exited Admin View." },
            {"SelectCollection", "Equip Collection: {0}?" },
            {"CostOfGC", "This Gear Collection Requires ${0} & {1} Kills" },
            {"CostOfWC", "This Weapon Collection Requires ${0} & {1} Kills" },
            {"UnlockCollection", "Unlock Collection for ${0} Money?" },
            {"ItemGearCost", "Requires:\n{1} Gear Kills\n& ${0}" },
            {"ItemWeaponCost", "Requires:\n{1} Weapon Kills\n& ${0}" },
            {"Owned", "Owned" },
            {"Unequipped", "Unequipped" },
            {"Equipped", "Equipped" },
            {"GunFull", "Gun Full" },
            {"SelectToEquip", "Select to Equip" },
            {"SelectToUnequip", "Select to Unequip" },
            {"PositionFull", "Slot Unavailable" },
            {"SelectKillsRequired", "Please Select the Kill Requirement to Unlock: {0}" },
            {"SelectPrice", "Please Select the Price to Unlock: {0}" },
            {"CollectionName", "Please provide a name for the new collection. You can also type 'quit' to exit." },
            {"CollectionCost", "Please Select the Price to Unlock this Collection" },
            {"CollectionKills", "Please Select the Kills to Unlock this Collection" },
            {"CreateCollection", "Create New Collection?" },
            {"CancelCollection", "Cancel Collection Creation?" },
            {"SaveCollection", "Save Collection?" },
            {"Free", "FREE" },
            {"NewCollectionCreated", "You have successfully created {0} Collection: {1}" },
            {"ClickToDetail", "Set Item Cost" },
            {"Remove", "X" },
            {"AddKills", "You have been given {0} Kills for the {1} Collection" },
            {"TakeKills", "{0} Kills for the {1} Collection have been taken from you" }
        };
        #endregion
    }
}
