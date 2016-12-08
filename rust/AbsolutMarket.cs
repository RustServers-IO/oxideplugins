﻿using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using System.Collections;
using System.IO;

namespace Oxide.Plugins
{
    [Info("AbsolutMarket", "Absolut", "1.5.0", ResourceId = 2118)]

    class AbsolutMarket : RustPlugin
    {

        [PluginReference]
        Plugin ServerRewards;

        [PluginReference]
        Plugin Economics;

        [PluginReference]
        Plugin ImageLibrary;

        MarketData mData;
        private DynamicConfigFile MData;

        CategoryData cData;
        private DynamicConfigFile CData;

        bool localimages = true;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";

        private List<ulong> MenuState = new List<ulong>();
        private List<ulong> SettingBox = new List<ulong>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private Dictionary<ulong, List<AMItem>> PlayerBoxContents = new Dictionary<ulong, List<AMItem>>();
        private Dictionary<ulong, AMItem> SalesItemPrep = new Dictionary<ulong, AMItem>();
        private Dictionary<ulong, List<Item>> PlayerInventory = new Dictionary<ulong, List<Item>>();
        private Dictionary<ulong, List<Item>> TransferableItems = new Dictionary<ulong, List<Item>>();
        private Dictionary<ulong, List<Item>> ItemsToTransfer = new Dictionary<ulong, List<Item>>();
        private Dictionary<ulong, bool> PlayerPurchaseApproval = new Dictionary<ulong, bool>();
        private Dictionary<ulong, AMItem> SaleProcessing = new Dictionary<ulong, AMItem>();

        #region Server Hooks

        void Loaded()
        {
            MData = Interface.Oxide.DataFileSystem.GetFile("AbsolutMarket_Data");
            CData = Interface.Oxide.DataFileSystem.GetFile("AbsolutMarket_Categorization");
            lang.RegisterMessages(messages, this);
        }

        void Unload()
        {
            foreach (var entry in timers)
                entry.Value.Destroy();
            MenuState.Clear();
            timers.Clear();
            PlayerBoxContents.Clear();
            SalesItemPrep.Clear();
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                DestroyMarketPanel(p);
                DestroyPurchaseScreen(p);
            }
            SaveData();
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (MenuState.Contains(player.userID))
                MenuState.Remove(player.userID);
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                player.Command($"bind {configData.MarketMenuKeyBinding} \"UI_ToggleMarketScreen\"");
                GetSendMSG(player, "AMInfo", configData.MarketMenuKeyBinding);
                if (!mData.names.ContainsKey(player.userID))
                    mData.names.Add(player.userID, player.displayName);
                else
                {
                    var length = player.displayName.Count();
                    if (length > 30)
                    {
                        mData.names[player.userID] = player.displayName.Substring(0, 30);
                    }
                    else mData.names[player.userID] = player.displayName;
                }
                SendMessages(player);
            }
        }

        void OnServerInitialized()
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning($"ImageLibrary is missing. Unloading {Name} as it will not work without ImageLibrary.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            LoadVariables();
            if (configData.ServerRewards && configData.Economics)
            {
                PrintWarning($"You can not have Economics and Server Rewards enabled. Disable one and reload.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            if (configData.ServerRewards)
                try
                {
                    ServerRewards.Call("isLoaded", null);
                }
                catch (Exception)
                {
                    PrintWarning($"ServerRewards is missing. Unloading {Name} as it will not work without ServerRewards or change the config option to false.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            if (configData.Economics)
                try
                {
                    Economics.Call("isLoaded", null);
                }
                catch (Exception)
                {
                    PrintWarning($"Economics is missing. Unloading {Name} as it will not work without Economics or change the config option to false.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
            if (!permission.PermissionExists("AbsolutMarket.admin"))
                permission.RegisterPermission("AbsolutMarket.admin", this);
            LoadData();
            timers.Add("info", timer.Once(configData.InfoInterval, () => InfoLoop()));
            timers.Add("save", timer.Once(600, () => SaveLoop()));
            SaveData();
            AddNeededImages();
            RefreshBackgrounds();
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerInit(p);
        }

        #endregion

        #region Player Hooks

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null) return;
            if (gameobject.GetComponent<BaseEntity>() != null)
            {
                BaseEntity element = gameobject.GetComponent<BaseEntity>();
                var entityowner = element.OwnerID;
                var ID = element.net.ID;
                if (!SettingBox.Contains(entityowner)) return;
                if (element.PrefabName == "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab" || element.PrefabName == "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab")
                {
                    BasePlayer player = BasePlayer.FindByID(entityowner);
                    TradeBoxConfirmation(player, ID);
                }
            }
        }

        private void OnEntityDeath(BaseEntity entity, HitInfo hitInfo)
        {
            if (entity is StorageContainer)
            {
                uint ID = entity.net.ID;
                if (mData.TradeBox.ContainsKey(entity.OwnerID))
                {
                    if (ID == mData.TradeBox[entity.OwnerID])
                    {
                        Dictionary<uint, string> listings = new Dictionary<uint, string>();
                        foreach (var entry in mData.MarketListings.Where(kvp => kvp.Value.seller == entity.OwnerID))
                            listings.Add(entry.Key, entry.Value.shortname);
                        foreach (var entry in listings)
                            RemoveListing(entity.OwnerID, entry.Value, entry.Key, "TradeBoxDestroyed");
                        listings.Clear();
                        mData.TradeBox.Remove(entity.OwnerID);
                        BasePlayer owner = BasePlayer.FindByID(entity.OwnerID);
                        if (BasePlayer.activePlayerList.Contains(owner))
                            GetSendMSG(owner, "TradeBoxDestroyed");
                    }
                    SaveData();
                }
                return;
            }
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return null;
            bool isPreping = false;
            AMItem item;
            var args = string.Join(" ", arg.Args);
            if (SalesItemPrep.ContainsKey(player.userID))
            {
                isPreping = true;
            }
            if (SettingBox.Contains(player.userID))
            {
                if (args.Contains("quit"))
                {
                    SettingBox.Remove(player.userID);
                    GetSendMSG(player, "ExitedBoxMode");
                    return true;
                }
            }
            if (isPreping)
            {
                item = SalesItemPrep[player.userID];
                if (args.Contains("quit"))
                {
                    CancelListing(player);
                    return true;
                }
                switch (item.stepNum)
                {
                    case 0:
                        var name = string.Join(" ", arg.Args);
                        item.name = name;
                        item.stepNum = 99;
                        SellItems(player, 1);
                        return true;
                }
            }
            return null;
        }



        #endregion

        #region Functions

        private StorageContainer GetTradeBox(ulong Buyer)
        {
            List<StorageContainer> Containers = new List<StorageContainer>();
            if (mData.TradeBox.ContainsKey(Buyer))
            {
                uint ID = mData.TradeBox[Buyer];
                foreach (StorageContainer Cont in StorageContainer.FindObjectsOfType<StorageContainer>())
                {
                    if (Cont.net.ID == ID)
                        return Cont;
                }
            }
            return null;
        }

        private bool GetTradeBoxContents(BasePlayer player)
        {
            if (player == null) return false;
            ulong seller = player.userID;
            if (GetTradeBox(seller) != null)
            {
                StorageContainer box = GetTradeBox(seller);
                if (GetItems(box.inventory).Count() == 0)
                {
                    if (!ServerRewards || !configData.ServerRewards || mData.Blacklist.Contains("SR"))
                    {
                        GetSendMSG(player, "TradeBoxEmpty");
                        return false;
                    }
                    else
                        if (CheckPoints(player.userID) is int)
                        if ((int)CheckPoints(player.userID) < 1)
                        {
                            GetSendMSG(player, "TradeBoxEmptyNoSR");
                            return false;
                        }
                }
                if (PlayerBoxContents.ContainsKey(seller)) PlayerBoxContents.Remove(seller);
                PlayerBoxContents.Add(seller, new List<AMItem>());
                PlayerBoxContents[seller].AddRange(GetItems(box.inventory));
                var bl = 0;
                var c = 0;
                var listed = 0;
                foreach (var entry in PlayerBoxContents[seller])
                {
                    c++;
                    if (mData.Blacklist.Contains(entry.shortname))
                        bl++;
                    if (mData.MarketListings.ContainsKey(entry.ID))
                        listed++;
                    foreach (var cat in Categorization.Where(k => k.Value.Contains(entry.shortname)))
                    {
                        entry.cat = cat.Key;
                        break;
                    }
                }
                if (bl == c)
                {
                    if (!ServerRewards || !configData.ServerRewards || mData.Blacklist.Contains("SR"))
                    {
                        GetSendMSG(player, "AllItemsAreBL");
                        return false;
                    }
                    else
                    if (CheckPoints(player.userID) is int)
                        if ((int)CheckPoints(player.userID) < 1)
                        {
                            GetSendMSG(player, "AllItemsAreBLNoSR");
                            return false;
                        }
                }
                if (c == listed)
                {
                    if (!ServerRewards || !configData.ServerRewards || mData.Blacklist.Contains("SR"))
                    {
                        GetSendMSG(player, "AllItemsAreListed");
                        return false;
                    }
                    else
                    if (CheckPoints(player.userID) is int)
                        if ((int)CheckPoints(player.userID) < 1)
                        {
                            GetSendMSG(player, "AllItemsAreListedNoSR");
                            return false;
                        }
                }
                return true;
            }
            else GetSendMSG(player, "NoTradeBox"); return false;
        }

        private bool BoxCheck(BasePlayer player, uint item)
        {
            if (player == null) return false;
            ulong seller = player.userID;
            if (GetTradeBox(seller) != null)
            {
                StorageContainer box = GetTradeBox(seller);
                if (GetItems(box.inventory).Count() == 0)
                {
                    GetSendMSG(player, "TradeBoxEmpty");
                    return false;
                }
                foreach (var entry in box.inventory.itemList)
                {
                    if (entry.uid == item)
                        return true;
                }
                return false;
            }
            else GetSendMSG(player, "NoTradeBox"); return false;
        }

        private void AddMessages(ulong player, string message, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            try
            {
                BasePlayer Online = BasePlayer.FindByID(player);
                if (BasePlayer.activePlayerList.Contains(Online))
                    GetSendMSG(Online, message, GetLang(arg1), GetLang(arg2), GetLang(arg3), GetLang(arg4));
            }
            catch
            {
                if (!mData.OutstandingMessages.ContainsKey(player))
                    mData.OutstandingMessages.Add(player, new List<Unsent>());
                mData.OutstandingMessages[player].Add(new Unsent { message = message, arg1 = arg1, arg2 = arg2, arg3 = arg3, arg4 = arg4 });
                SaveData();
            }
        }

        private void SendMessages(BasePlayer player)
        {
            if (mData.OutstandingMessages.ContainsKey(player.userID))
            {
                foreach (var entry in mData.OutstandingMessages[player.userID])
                {
                    GetSendMSG(player, entry.message, GetLang(entry.arg1), GetLang(entry.arg2), GetLang(entry.arg3), GetLang(entry.arg4));
                }
                mData.OutstandingMessages.Remove(player.userID);
            }
        }

        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (localimages)
                if (skin == 99)
                    return GetImage(shortname, (ulong)ResourceId);
                else return GetImage(shortname, skin);
            else if (skin == 99)
                return GetImageURL(shortname, (ulong)ResourceId);
            else return GetImageURL(shortname, skin);
        }

        public string GetImageURL(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImageURL", shortname, skin);
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);


        private IEnumerable<AMItem> GetItems(ItemContainer container)
        {
            return container.itemList.Select(item => new AMItem
            {
                amount = item.amount,
                skin = item.skin,
                cat = Category.Other,
                pricecat = Category.Other,
                shortname = item.info.shortname,
                condition = item.condition,
                ID = item.uid,
            });
        }

        private IEnumerable<Item> GetItemsOnly(ItemContainer container)
        {
            return container.itemList;
        }

        private void XferPurchase(ulong buyer, uint ID, ItemContainer from, ItemContainer to)
        {
            foreach (Item item in from.itemList.Where(kvp => kvp.uid == ID))
                ItemsToTransfer[buyer].Add(item);
            //item.MoveToContainer(to);
        }

        private void XferCost(Item item, BasePlayer player, uint Listing)
        {
            //Puts("Starting");
            ItemContainer from = player.inventory.containerMain;
            if (player.inventory.containerBelt.itemList.Contains(item))
            {
                from = player.inventory.containerBelt;
                //Puts($"{from} belt");
            }
            else if (player.inventory.containerWear.itemList.Contains(item))
            {
                from = player.inventory.containerWear;
                //Puts($"{from} wear");
            }
            else
                //Puts($"{from} main");
            if (mData.MarketListings[Listing].priceAmount > 0)
            {
                //Puts("TRying");
                foreach (Item item1 in from.itemList.Where(kvp => kvp == item))
                {
                    //Puts("Item found)");
                    if (mData.MarketListings[Listing].priceAmount >= item1.amount)
                    {
                        //Puts("1");
                        ItemsToTransfer[player.userID].Add(item1);
                        mData.MarketListings[Listing].priceAmount -= item1.amount;
                        //Puts($"{item1} moved... price amount: {mData.MarketListings[Listing].priceAmount} item amount:{item1.amount}");
                    }
                    else
                    {
                        Item item2 = item1.SplitItem(mData.MarketListings[Listing].priceAmount);
                        ItemsToTransfer[player.userID].Add(item2);
                        mData.MarketListings[Listing].priceAmount = 0;
                        //Puts($"SPLITTING: {item2} moved... price amount: {mData.MarketListings[Listing].priceAmount} item amount:{item2.amount}");
                    }
                    break;
                }
            }
        }

        private Item BuildCostItems(string shortname, int amount)
        {
            var definition = ItemManager.FindItemDefinition(shortname);
            if (definition != null)
            {
                var item1 = ItemManager.Create(definition, amount, 0);
                if (item1 != null)
                    return item1;
            }
            Puts("Error making purchase cost item(s)");
            return null;
        }

        void OnItemRemovedFromContainer(ItemContainer cont, Item item)
        {
            if (mData.MarketListings == null || mData.MarketListings.Count < 1) return;
            if (mData.TradeBox == null || mData.TradeBox.Count < 1) return;
            if (cont.entityOwner != null)
                if (mData.TradeBox.ContainsValue(cont.entityOwner.net.ID))
                    if (mData.TradeBox.ContainsKey(cont.entityOwner.OwnerID))
                        if (mData.TradeBox[cont.entityOwner.OwnerID] == cont.entityOwner.net.ID)
                            if (mData.MarketListings.ContainsKey(item.uid))
                            {
                                var name = "";
                                if (configData.UseUniqueNames && item.name != "")
                                    name = mData.MarketListings[item.uid].name;
                                else name = mData.MarketListings[item.uid].shortname;
                                RemoveListing(cont.entityOwner.OwnerID, name, item.uid, "FromBox");
                                mData.MarketListings.Remove(item.uid);
                            }
        }

        private void RemoveListing(ulong seller, string name, uint ID, string reason = "")
        {
            AddMessages(seller, "ItemRemoved", name.ToUpper(), reason);
            mData.MarketListings.Remove(ID);
        }

        private void CancelListing(BasePlayer player)
        {
            DestroyMarketPanel(player);
            if (SalesItemPrep.ContainsKey(player.userID))
                SalesItemPrep.Remove(player.userID);
            if (PlayerBoxContents.ContainsKey(player.userID))
                PlayerBoxContents.Remove(player.userID);
            GetSendMSG(player, "ItemListingCanceled");
        }

        private void SRAction(ulong ID, int amount, string action)
        {
            if (action == "ADD")
                ServerRewards?.Call("AddPoints", new object[] { ID, amount });
            if (action == "REMOVE")
                ServerRewards?.Call("TakePoints", new object[] { ID, amount });
        }

        private object CheckPoints(ulong ID) => ServerRewards?.Call("CheckPoints", ID);

        private void NumberPad(BasePlayer player, string cmd)
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            var element = UI.CreateElementContainer(PanelMarket, UIColors["dark"], "0.35 0.3", "0.65 0.7", true);
            UI.CreatePanel(ref element, PanelMarket, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], GetLang("Select Amount"), 20, "0.1 0.85", "0.9 .98", TextAnchor.UpperCenter);
            var n = 1;
            var i = 0;
            while (n < 10)
            {
                CreateNumberPadButton(ref element, PanelMarket, i, n, cmd); i++; n++;
            }
            while (n >= 10 && n < 25)
            {
                CreateNumberPadButton(ref element, PanelMarket, i, n, cmd); i++; n += 5;
            }
            while (n >= 25 && n < 200)
            {
                CreateNumberPadButton(ref element, PanelMarket, i, n, cmd); i++; n += 25;
            }
            while (n >= 200 && n <= 950)
            {
                CreateNumberPadButton(ref element, PanelMarket, i, n, cmd); i++; n += 50;
            }
            while (n >= 1000 && n <= 10000)
            {
                CreateNumberPadButton(ref element, PanelMarket, i, n, cmd); i++; n += 500;
            }
            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Quit"), 10, "0.03 0.02", "0.13 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), Category.All)}");
            CuiHelper.AddUi(player, element);
        }

        private void CreateNumberPadButton(ref CuiElementContainer container, string panelName, int i, int number, string command)
        {
            var pos = CalcNumButtonPos(i);
            UI.CreateButton(ref container, panelName, UIColors["buttonbg"], number.ToString(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"{command} {number}");
        }

        private string GetLang(string msg)
        {
            if (messages.ContainsKey(msg))
                return lang.GetMessage(msg, this);
            else return msg;
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this), arg1, arg2, arg3, arg4);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        private string GetMSG(string message, string arg1 = "", string arg2 = "", string arg3 = "", string arg4 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this), arg1, arg2, arg3, arg4);
            return msg;
        }

        public void DestroyMarketPanel(BasePlayer player)
        {
            if (MenuState.Contains(player.userID))
                MenuState.Remove(player.userID);
            CuiHelper.DestroyUi(player, PanelMarket);
        }

        public void DestroyPurchaseScreen(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelPurchase);
        }

        private void ToggleMarketScreen(BasePlayer player)
        {
            if (MenuState.Contains(player.userID))
            {
                MenuState.Remove(player.userID);
                DestroyMarketPanel(player);
                DestroyPurchaseScreen(player);
                return;
            }
            MenuState.Add(player.userID);
            MarketMainScreen(player, 0, Category.All);
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 1 && !permission.UserHasPermission(player.UserIDString, "AbsolutMarket.admin"))
                    return false;
            return true;
        }

        #endregion

        #region UI Creation

        private string PanelMarket = "PanelMarket";
        private string PanelPurchase = "PanelPurchase";

        public class UI
        {
            static bool localimage = true;
            static public CuiElementContainer CreateElementContainer(string panel, string color, string aMin, string aMax, bool cursor = false)
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
                    panel
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer element, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                element.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer element, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer element, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (UI.localimage)
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/generic textures/fulltransparent.tga" },
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
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/generic textures/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOverlay(ref CuiElementContainer element, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                //if (configdata.DisableUI_FadeIn)
                //    fadein = 0;
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

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
            {"limegreen", "0.42 1.0 0 1.0" },
            {"blue", "0.2 0.6 1.0 1.0" },
            {"red", "1.0 0.1 0.1 1.0" },
            {"white", "1 1 1 1" },
            {"green", "0.28 0.82 0.28 1.0" },
            {"grey", "0.85 0.85 0.85 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"CSorange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> TextColors = new Dictionary<string, string>
        {
            {"limegreen", "<color=#6fff00>" }
        };

        #endregion

        #region UI Panels

        void MarketMainScreen(BasePlayer player, int page = 0, Category cat = Category.All)
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            if (!mData.mode.ContainsKey(player.userID))
                mData.mode.Add(player.userID, false);
            var i = 0;
            var c = 0;
            bool seller = false;
            double count = 0;
            string Background = TryForImage("NEVERDELETE");
            if (cat == Category.All)
                count = mData.MarketListings.Count();
            else count = mData.MarketListings.Where(kvp => kvp.Value.cat == cat).Count();
            var element = UI.CreateElementContainer(PanelMarket, "0 0 0 0", "0.2 0.15", "0.8 0.85", true);
            UI.CreatePanel(ref element, PanelMarket, "0 0 0 0", "0 0", "1 1");
            int entriesallowed = 9;
            double remainingentries = count - (page * (entriesallowed - 1));
            double totalpages = (Math.Floor(count / (entriesallowed - 1)));
            if (mData.mode[player.userID] == false)
            {
                if (mData.background.ContainsKey(player.userID))
                        Background = TryForImage(mData.background[player.userID]);
                UI.LoadImage(ref element, PanelMarket, Background, "0 0", "1 1");
                if (page <= totalpages - 1)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("LAST"), "0.8 0.02", "0.85 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 18, "0.8 0.02", "0.85 0.075", $"UI_MarketMainScreen {totalpages} {Enum.GetName(typeof(Category), cat)}");
                }
                if (remainingentries > entriesallowed)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("NEXT"), "0.74 0.02", "0.79 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 18, "0.74 0.02", "0.79 0.075", $"UI_MarketMainScreen {page + 1} {Enum.GetName(typeof(Category), cat)}");
                }
                if (page > 0)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("BACK"), "0.68 0.02", "0.73 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 18, "0.68 0.02", "0.73 0.075", $"UI_MarketMainScreen {page - 1} {Enum.GetName(typeof(Category), cat)}");
                }
                if (page > 1)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("FIRST"), "0.62 0.02", "0.67 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 18, "0.62 0.02", "0.67 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), cat)}");
                }

                //UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], GetLang("Filters"), 22, "0.14 0.08", "0.24 0.14");
                foreach (Category ct in Enum.GetValues(typeof(Category)))
                {
                    var loc = FilterButton(c);
                        if (cat == ct)
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("UFILTER"), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                            UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], Enum.GetName(typeof(Category), ct), 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", TextAnchor.MiddleCenter);
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), ct)}");
                            c++;
                        }
                        else
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("OFILTER"), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                            UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], Enum.GetName(typeof(Category), ct), 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", TextAnchor.MiddleCenter);
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), ct)}");
                            c++;
                        }
                }
                UI.LoadImage(ref element, PanelMarket, TryForImage("box.wooden.large", 0), $"0.05 0.9", "0.15 1");
                UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], "", 12, $"0.05 0.9", "0.15 1", TextAnchor.MiddleCenter);

                if (!SettingBox.Contains(player.userID))
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", GetLang("TradeBoxAssignment"), 12, $"0.05 0.9", "0.15 1", $"UI_SetBoxMode");
                else
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", GetLang("TradeBoxAssignment"), 12, $"0.05 0.9", "0.15 1", $"UI_CancelTradeBox");



                UI.LoadImage(ref element, PanelMarket, TryForImage("SELL"), $"0.35 0.9", "0.65 1.0");
                UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], GetLang("ListItem"), 12, $"0.35 0.9", "0.65 1.0", TextAnchor.MiddleCenter);
                UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"0.35 0.9", "0.65 1.0", $"UI_MarketSellScreen {0}");

                UI.LoadImage(ref element, PanelMarket, TryForImage("OFILTER"), "0.66 0.9", "0.75 1");
                UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], GetLang("ChangeMode"), 12, "0.66 0.9", "0.75 1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, "0.66 0.9", "0.75 1", $"UI_Mode {1}");

                UI.LoadImage(ref element, PanelMarket, TryForImage("OFILTER"), "0.76 0.9", "0.86 1");
                UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], GetLang("ChangeTheme"), 12, "0.76 0.9", "0.86 1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, "0.76 0.9", "0.86 1", $"UI_MarketBackgroundMenu {0}");

                if (isAuth(player))
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("UFILTER"), "0.87 0.9", "0.97 1");
                    UI.CreateLabel(ref element, PanelMarket, UIColors["dark"], GetLang("AdminPanel"), 12, "0.87 0.9", "0.97 1", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, "0.87 0.9", "0.97 1", $"UI_AdminPanel");
                }
                int shownentries = page * entriesallowed;
                int n = 0;
                if (cat == Category.All)
                {
                    foreach (var item in mData.MarketListings)
                    {
                        seller = false;
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            if (item.Value.seller == player.userID)
                            {
                                seller = true;
                            }
                            CreateMarketListingButton(ref element, PanelMarket, item.Value, seller, n);

                            n++;
                        }
                    }
                }
                else
                    foreach (var item in mData.MarketListings.Where(kvp => kvp.Value.cat == cat))
                    {
                        seller = false;
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            if (item.Value.seller == player.userID)
                            {
                                seller = true;
                            }
                            CreateMarketListingButton(ref element, PanelMarket, item.Value, seller, n);
                            n++;
                        }
                    }
            }
            else
            {
                UI.CreatePanel(ref element, PanelMarket, UIColors["dark"], "0. 0", "1 1");
                if (page <= totalpages - 1)
                {
                    UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("Last"), 18, "0.8 0.02", "0.85 0.075", $"UI_MarketMainScreen {totalpages} {Enum.GetName(typeof(Category), cat)}");
                }
                if (remainingentries > entriesallowed)
                {
                    UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("Next"), 18, "0.74 0.02", "0.79 0.075", $"UI_MarketMainScreen {page + 1} {Enum.GetName(typeof(Category), cat)}");
                }
                if (page > 0)
                {
                    UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("Back"), 18, "0.68 0.02", "0.73 0.075", $"UI_MarketMainScreen {page - 1} {Enum.GetName(typeof(Category), cat)}");
                }
                if (page > 1)
                {
                    UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("First"), 18, "0.62 0.02", "0.67 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), cat)}");
                }

                foreach (Category ct in Enum.GetValues(typeof(Category)))
                {
                    var loc = FilterButton(c);
                        if (cat == ct)
                        {
                            UI.CreateButton(ref element, PanelMarket, UIColors["red"], Enum.GetName(typeof(Category), ct), 12, $"{loc[0]} {loc[1] + .02f}", $"{loc[2]} {loc[3] + .02f}", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), ct)}");
                            c++;
                        }
                        else
                        {
                            UI.CreateButton(ref element, PanelMarket, UIColors["header"], Enum.GetName(typeof(Category), ct), 12, $"{loc[0]} {loc[1] + .02f}", $"{loc[2]} {loc[3] + .02f}", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), ct)}");
                            c++;
                        }
                }
                if (!SettingBox.Contains(player.userID))
                    UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("TradeBoxAssignment"), 12, $"0.05 0.92", "0.15 .98", $"UI_SetBoxMode");
                else
                    UI.CreateButton(ref element, PanelMarket, UIColors["red"], GetLang("TradeBoxAssignment"), 12, $"0.05 0.92", "0.15 .98", $"UI_CancelTradeBox");



                UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("ListItem"), 12, $"0.35 0.92", "0.65 .98", $"UI_MarketSellScreen {0}");

                UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("ChangeMode"), 12, "0.66 0.92", "0.75 .98", $"UI_Mode {0}");

                UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("ChangeTheme"), 12, "0.76 0.92", "0.86 .98", $"UI_MarketBackgroundMenu {0}");

                if (isAuth(player))
                {
                    UI.CreateButton(ref element, PanelMarket, UIColors["header"], GetLang("AdminPanel"), 12, "0.87 0.92", "0.97 .98", $"UI_AdminPanel");
                }
                int shownentries = page * entriesallowed;
                int n = 0;
                if (cat == Category.All)
                {
                    foreach (var item in mData.MarketListings)
                    {
                        seller = false;
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            if (item.Value.seller == player.userID)
                            {
                                seller = true;
                            }
                            CreateMarketListingButtonSimple(ref element, PanelMarket, item.Value, seller, n);
                            n++;
                        }
                    }
                }
                else
                    foreach (var item in mData.MarketListings.Where(kvp => kvp.Value.cat == cat))
                    {
                        seller = false;
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            if (item.Value.seller == player.userID)
                            {
                                seller = true;
                            }
                            CreateMarketListingButtonSimple(ref element, PanelMarket, item.Value, seller, n);
                            n++;
                        }
                    }
            }
            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Close"), 16, "0.87 0.02", "0.97 0.075", $"UI_DestroyMarketPanel");
            CuiHelper.AddUi(player, element);
        }

        private void CreateMarketListingButton(ref CuiElementContainer container, string panelName, AMItem item, bool seller, int num)
        {
            var pos = MarketEntryPos(num);
            var name = item.shortname;
            if (configData.UseUniqueNames && item.name != "")
                name = item.name;
            if (item.shortname == "SR")
            {
                name = "SR Points";
                item.skin = (ulong)ResourceId;
            }
            UI.CreatePanel(ref container, panelName, UIColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");

            //SALE ITEM
            UI.LoadImage(ref container, panelName, TryForImage(item.shortname, item.skin), $"{pos[0] + 0.001f} {pos[3] - 0.125f}", $"{pos[0] + 0.1f} {pos[3] - 0.005f}");
            UI.CreateLabel(ref container, panelName, UIColors["dark"], name, 12, $"{pos[0] + .1f} {pos[3] - .04f}", $"{pos[2] - .001f} {pos[3] - .001f}", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetMSG("Amount", item.amount.ToString()), 12, $"{pos[0] + .1f} {pos[3] - .07f}", $"{pos[2] - .001f} {pos[3] - .041f}", TextAnchor.MiddleLeft);

            if (item.cat != Category.Money)
            {
                Item actual = BuildCostItems(item.shortname, 1);
                if (actual.condition != 0)
                {
                    var percent = System.Convert.ToDouble(item.condition / actual.condition);
                    var xMax = (pos[0] + .1f) + (.175f * percent);
                    var ymin = pos[3] - .11f;
                    var ymax = pos[3] - .08f;
                    UI.CreatePanel(ref container, panelName, UIColors["buttonbg"], $"{pos[0] + .1f} {ymin}", $"{pos[0] + .275f} {ymax}");
                    if (percent * 100 > 75)
                        UI.CreatePanel(ref container, panelName, UIColors["green"], $"{pos[0] + .1f} {ymin}", $"{xMax} {ymax}");
                    else if (percent * 100 > 25 && percent * 100 < 76)
                        UI.CreatePanel(ref container, panelName, UIColors["yellow"], $"{pos[0] + .1f} {ymin}", $"{xMax} {ymax}");
                    else if (percent * 100 > 0 && percent * 100 < 26)
                        UI.CreatePanel(ref container, panelName, UIColors["red"], $"{pos[0] + .1f} {ymin}", $"{xMax} {ymax}");
                    UI.CreateLabel(ref container, panelName, "1 1 1 1", GetMSG("ItemCondition", Math.Round(percent * 100).ToString()), 9, $"{pos[0] + .1f} {ymin}", $"{pos[0] + .275f} {ymax}", TextAnchor.MiddleLeft);
                }
            }

            UI.LoadImage(ref container, PanelMarket, TryForImage("ARROW"), $"{pos[0] + .08f} {pos[1] + .07f}", $"{pos[0] + .2f} {pos[1] + .135f}");
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetLang("InExchange"), 14, $"{ pos[0] + .08f} {pos[1] + .07f}", $"{pos[0] + .2f} {pos[1] + .135f}", TextAnchor.UpperCenter);

            //COST ITEM
            if (item.priceItemshortname == "SR")
            {
                name = "SR Points";
                UI.LoadImage(ref container, panelName, TryForImage(item.priceItemshortname), $"{pos[2] - 0.125f} {pos[1] + 0.01f}", $"{pos[2] - 0.005f} {pos[1] + 0.125f}");
            }
            else
            {
                name = item.priceItemshortname;
                UI.LoadImage(ref container, panelName, TryForImage(item.priceItemshortname, 0), $"{pos[2] - 0.125f} {pos[1] + 0.01f}", $"{pos[2] - 0.005f} {pos[1] + 0.125f}");
            }
            UI.CreateLabel(ref container, panelName, UIColors["dark"], name, 12, $"{pos[0] + 0.005f} {pos[1] + 0.03f}", $"{pos[0] + 0.175f} {pos[1] + 0.06f}", TextAnchor.MiddleRight);
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetMSG("Amount", item.priceAmount.ToString()), 12, $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[0] + 0.175f} {pos[1] + 0.0299f}", TextAnchor.MiddleRight);
            if (mData.names.ContainsKey(item.seller))
                name = mData.names[item.seller];
            else name = "NONE";
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetMSG("Seller", name), 12, $"{pos[0] + .001f} {pos[3] - .2f}", $"{pos[2] - .1f} {pos[3] - .14f}", TextAnchor.MiddleLeft);

            if (seller == true)
            {
                UI.LoadImage(ref container, PanelMarket, TryForImage("UFILTER"), $"{pos[0] + .02f} {pos[3] - .15f}", $"{pos[0] + .08f} {pos[3] - .1f}");
                UI.CreateLabel(ref container, panelName, UIColors["dark"], GetLang("removelisting"), 10, $"{pos[0] + .02f} {pos[3] - .15f}", $"{pos[0] + .08f} {pos[3] - .1f}", TextAnchor.MiddleCenter);
                UI.CreateButton(ref container, panelName, "0 0 0 0", "", 40, $"{pos[0] + .02f} {pos[3] - .15f}", $"{pos[0] + .08f} {pos[3] - .1f}", $"UI_RemoveListing {item.ID}");
            }
            else
            {
                UI.CreateButton(ref container, panelName, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_BuyConfirm {item.ID}");
            }
        }

        private void CreateMarketListingButtonSimple(ref CuiElementContainer container, string panelName, AMItem item, bool seller, int num)
        {
            var pos = MarketEntryPos(num);
            var name = item.shortname;
            if (configData.UseUniqueNames && item.name != "")
                name = item.name;
            if (item.shortname == "SR")
            {
                name = "SR Points";
                item.skin = (ulong)ResourceId;
            }
            UI.CreatePanel(ref container, panelName, UIColors["white"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");

            //SALE ITEM
            UI.LoadImage(ref container, panelName, TryForImage(item.shortname, item.skin), $"{pos[0] + 0.001f} {pos[3] - 0.125f}", $"{pos[0] + 0.1f} {pos[3] - 0.005f}");
            UI.CreateLabel(ref container, panelName, UIColors["dark"], name, 12, $"{pos[0] + .1f} {pos[3] - .04f}", $"{pos[2] - .001f} {pos[3] - .001f}", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetMSG("Amount", item.amount.ToString()), 12, $"{pos[0] + .1f} {pos[3] - .07f}", $"{pos[2] - .001f} {pos[3] - .041f}", TextAnchor.MiddleLeft);

            if (item.cat != Category.Money)
            {
                Item actual = BuildCostItems(item.shortname, 1);
                if (actual.condition != 0)
                {
                    var percent = System.Convert.ToDouble(item.condition / actual.condition);
                    //var xMax = (pos[0] + .1f) + (.175f * percent);
                    var ymin = pos[3] - .12f;
                    var ymax = pos[3] - .07f;
                    if (percent * 100 > 75)
                    UI.CreateLabel(ref container, panelName, UIColors["green"], GetMSG("ItemCondition", Math.Round(percent * 100).ToString()), 12, $"{pos[0] + .1f} {ymin}", $"{pos[0] + .275f} {ymax}", TextAnchor.MiddleLeft);
                    else if (percent * 100 > 25 && percent * 100 < 76)
                        UI.CreateLabel(ref container, panelName, UIColors["yellow"], GetMSG("ItemCondition", Math.Round(percent * 100).ToString()), 12, $"{pos[0] + .1f} {ymin}", $"{pos[0] + .275f} {ymax}", TextAnchor.MiddleLeft);
                    else if (percent * 100 > 0 && percent * 100 < 26)
                        UI.CreateLabel(ref container, panelName, UIColors["red"], GetMSG("ItemCondition", Math.Round(percent * 100).ToString()), 12, $"{pos[0] + .1f} {ymin}", $"{pos[0] + .275f} {ymax}", TextAnchor.MiddleLeft);
                }
            }

            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetLang("InExchange"), 14, $"{ pos[0] + .08f} {pos[1] + .07f}", $"{pos[0] + .2f} {pos[1] + .135f}", TextAnchor.UpperCenter);

            //COST ITEM
            if (item.priceItemshortname == "SR")
            {
                name = "SR Points";
                UI.LoadImage(ref container, panelName, TryForImage(item.priceItemshortname), $"{pos[2] - 0.125f} {pos[1] + 0.01f}", $"{pos[2] - 0.005f} {pos[1] + 0.125f}");
            }
            else
            {
                name = item.priceItemshortname;
                UI.LoadImage(ref container, panelName, TryForImage(item.priceItemshortname, 0), $"{pos[2] - 0.125f} {pos[1] + 0.01f}", $"{pos[2] - 0.005f} {pos[1] + 0.125f}");
            }
            UI.CreateLabel(ref container, panelName, UIColors["dark"], name, 12, $"{pos[0] + 0.005f} {pos[1] + 0.03f}", $"{pos[0] + 0.175f} {pos[1] + 0.06f}", TextAnchor.MiddleRight);
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetMSG("Amount", item.priceAmount.ToString()), 12, $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[0] + 0.175f} {pos[1] + 0.0299f}", TextAnchor.MiddleRight);
            if (mData.names.ContainsKey(item.seller))
                name = mData.names[item.seller];
            else name = "NONE";
            UI.CreateLabel(ref container, panelName, UIColors["dark"], GetMSG("Seller", name), 12, $"{pos[0] + .001f} {pos[3] - .2f}", $"{pos[2] - .1f} {pos[3] - .14f}", TextAnchor.MiddleLeft);

            if (seller == true)
            {
                UI.LoadImage(ref container, PanelMarket, TryForImage("UFILTER"), $"{pos[0] + .02f} {pos[3] - .15f}", $"{pos[0] + .08f} {pos[3] - .1f}");
                UI.CreateLabel(ref container, panelName, UIColors["dark"], GetLang("removelisting"), 10, $"{pos[0] + .02f} {pos[3] - .15f}", $"{pos[0] + .08f} {pos[3] - .1f}", TextAnchor.MiddleCenter);
                UI.CreateButton(ref container, panelName, "0 0 0 0", "", 40, $"{pos[0] + .02f} {pos[3] - .15f}", $"{pos[0] + .08f} {pos[3] - .1f}", $"UI_RemoveListing {item.ID}");
            }
            else
            {
                UI.CreateButton(ref container, panelName, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_BuyConfirm {item.ID}");
            }
        }

        void MarketSellScreen(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            if (GetTradeBox(player.userID) == null)
            {
                GetSendMSG(player, "YourNoTradeBoxBuying");
                MarketMainScreen(player);
                return;
            }
            if (GetTradeBoxContents(player) == false)
            {
                MarketMainScreen(player);
                return;
            }
            float[] pos;
            var i = 0;
            var element = UI.CreateElementContainer(PanelMarket, "0 0 0 0", "0.275 0.25", "0.725 0.75", true);
            //var count = PlayerBoxContents[player.userID].Count();
            UI.CreateLabel(ref element, PanelMarket, UIColors["black"], $"{TextColors["limegreen"]} {GetLang("SelectItemToSell")}", 20, "0.05 .9", "1 1", TextAnchor.MiddleCenter);
            if (GetTradeBoxContents(player) != false)
            {
                foreach (AMItem item in PlayerBoxContents[player.userID].Where(bl => !mData.Blacklist.Contains(bl.shortname) && !mData.MarketListings.ContainsKey(bl.ID)))
                {
                    pos = CalcButtonPos(i);
                                UI.LoadImage(ref element, PanelMarket, TryForImage(item.shortname, item.skin), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                    if (TryForImage(item.shortname, item.skin) == TryForImage("BLAHBLAH"))
                        UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], item.shortname.ToUpper(), 16, $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}", TextAnchor.LowerCenter);
                    if (item.amount > 9999)
                        UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], item.amount.ToString(), 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleCenter);
                    else if (item.amount > 1)
                        UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], item.amount.ToString(), 16, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectSalesItem {item.ID}"); i++;
                }
            }
            if (configData.ServerRewards && ServerRewards)
            {
                if (!mData.Blacklist.Contains("SR"))
                {
                    if (CheckPoints(player.userID) is int)
                        if ((int)CheckPoints(player.userID) > 0)
                        {
                            pos = CalcButtonPos(i);
                            UI.CreatePanel(ref element, PanelMarket, "1 1 1 1", $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.LoadImage(ref element, PanelMarket, TryForImage("SR"), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectSR"); i++;
                        }
                }
            }
            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Back"), 16, "0.03 0.02", "0.13 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), Category.All)}");
            CuiHelper.AddUi(player, element);
        }

        private void SellItems(BasePlayer player, int step = 0, int page = 0)
        {
            AMItem SalesItem;

            var i = 0;
            var name = "";
            var element = UI.CreateElementContainer(PanelMarket, "0 0 0 0", "0.3 0.3", "0.7 0.9");
            switch (step)
            {
                case 0:
                    CuiHelper.DestroyUi(player, PanelMarket);
                    SalesItem = SalesItemPrep[player.userID];
                    if (SalesItem == null) return;
                    UI.CreateLabel(ref element, PanelMarket, UIColors["black"], $"{TextColors["limegreen"]} {GetMSG("SetName", SalesItem.shortname)}", 20, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    break;
                case 1:
                    CuiHelper.DestroyUi(player, PanelMarket);
                    SalesItem = SalesItemPrep[player.userID];
                    if (SalesItem == null) return;
                    if (configData.UseUniqueNames && SalesItem.name != "")
                        name = SalesItem.name;
                    else name = SalesItem.shortname;
                    double count = 0;
                    foreach (var item in ItemManager.itemList.Where(a => !mData.Blacklist.Contains(a.shortname)))
                        count++;
                    UI.CreatePanel(ref element, PanelMarket, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002", true);
                    UI.CreateLabel(ref element, PanelMarket, UIColors["black"], $"{TextColors["limegreen"]} {GetMSG("SetpriceItemshortname", name)}", 20, "0.05 .95", ".95 1", TextAnchor.MiddleCenter);
                    double entriesallowed = 30;
                    double remainingentries = count - (page * (entriesallowed - 1));
                    double totalpages = (Math.Floor(count / (entriesallowed - 1)));
                    {
                        if (page <= totalpages - 1)
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("LAST"), "0.8 0.02", "0.85 0.075");
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.8 0.02", "0.85 0.075", $"UI_SellItems {totalpages}");
                        }
                        if (remainingentries > entriesallowed)
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("NEXT"), "0.74 0.02", "0.79 0.075");
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.74 0.02", "0.79 0.075", $"UI_SellItems {page + 1}");
                        }
                        if (page > 0)
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("BACK"), "0.68 0.02", "0.73 0.075");
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.68 0.02", "0.73 0.075", $"UI_SellItems {page - 1}");
                        }
                        if (page > 1)
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("FIRST"), "0.62 0.02", "0.67 0.075");
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.62 0.02", "0.67 0.075", $"UI_SellItems {0}");
                        }
                    }
                    int n = 0;
                    var pos = CalcButtonPos(n);
                    double shownentries = page * entriesallowed;
                    if (page == 0)
                    {
                        if (configData.ServerRewards == true && ServerRewards)
                        {
                            UI.LoadImage(ref element, PanelMarket, TryForImage("SR"), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectpriceItemshortname SR");
                            n++;
                            i++;
                        }
                    }
                    foreach (var item in ItemManager.itemList.Where(a => !mData.Blacklist.Contains(a.shortname)))
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelMarket, TryForImage(item.shortname, 0), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                            if (TryForImage(item.shortname, 0) == TryForImage("BLAHBLAH")) UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], item.shortname, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleCenter);
                            UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SelectpriceItemshortname {item.shortname}");
                            n++;
                        }
                    }
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Quit"), 16, "0.03 0.02", "0.13 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), Category.All)}");
                    break;
                default:
                    CuiHelper.DestroyUi(player, PanelMarket);
                    SalesItem = SalesItemPrep[player.userID];
                    if (SalesItem == null) return;
                    if (configData.UseUniqueNames && SalesItem.name != "")
                        name = SalesItem.name;
                    else name = SalesItem.shortname;
                    UI.CreatePanel(ref element, PanelMarket, "0 0 0 0", $".0001 0.0001", $"0.0002 0.0002", true);
                    UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], GetLang("NewItemInfo"), 20, "0.05 .8", ".95 .95");
                    string ItemDetails = GetMSG("ItemDetails", SalesItem.amount.ToString(), name, SalesItem.priceAmount.ToString(), SalesItem.priceItemshortname);
                    UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], ItemDetails, 20, "0.1 0.1", "0.9 0.65", TextAnchor.MiddleLeft);
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonbg"], GetLang("ListItem"), 18, "0.2 0.05", "0.4 0.15", $"UI_ListItem", TextAnchor.MiddleCenter);
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("CancelListing"), 18, "0.6 0.05", "0.8 0.15", $"UI_CancelListing");
                    break;
            }
            CuiHelper.AddUi(player, element);
        }

        private void PurchaseConfirmation(BasePlayer player, uint index)
        {
            CuiHelper.DestroyUi(player, PanelPurchase);
            AMItem purchaseitem = mData.MarketListings[index];
            var name = "";
            if (configData.UseUniqueNames && purchaseitem.name != "")
                name = purchaseitem.name;
            else name = purchaseitem.shortname;
            var element = UI.CreateElementContainer(PanelPurchase, UIColors["dark"], "0.425 0.35", "0.575 0.65", true);
            UI.CreatePanel(ref element, PanelPurchase, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelPurchase, MsgColor, GetMSG("PurchaseConfirmation", name), 20, "0.05 0.75", "0.95 0.95");
            Vector2 position = new Vector2(0.375f, 0.45f);
            Vector2 dimensions = new Vector2(0.25f, 0.25f);
            Vector2 posMin = position;
            Vector2 posMax = posMin + dimensions;
            if (purchaseitem.shortname == "SR")
            {
                name = "SR Points";
                purchaseitem.skin = (ulong)ResourceId;
            }
            if (TryForImage(purchaseitem.shortname, purchaseitem.skin) == TryForImage("BLAHBLAH")) UI.CreateLabel(ref element, PanelPurchase, UIColors["limegreen"], purchaseitem.shortname, 14, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", TextAnchor.MiddleCenter);
            UI.LoadImage(ref element, PanelPurchase, TryForImage(purchaseitem.shortname, purchaseitem.skin), $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}");
            if (purchaseitem.amount > 1)
                UI.CreateLabel(ref element, PanelPurchase, UIColors["limegreen"], $"x {purchaseitem.amount}", 14, $"{posMin.x} {posMin.y}", $"{posMax.x} {posMax.y}", TextAnchor.MiddleCenter);
            if (mData.MarketListings[index].cat != Category.Money)
            {
                Item item = BuildCostItems(mData.MarketListings[index].shortname, mData.MarketListings[index].amount);

                if (item.condition != 0)
                {
                    var percent = System.Convert.ToDouble(purchaseitem.condition / item.condition);
                    var xMax = .1f + (0.8f * percent);
                    var ymin = 0.3;
                    var ymax = 0.4;
                    UI.CreatePanel(ref element, PanelPurchase, UIColors["buttonbg"], $"0.1 {ymin}", $"0.9 {ymax}");
                    UI.CreatePanel(ref element, PanelPurchase, UIColors["green"], $"0.1 {ymin}", $"{xMax} {ymax}");
                    UI.CreateLabel(ref element, PanelPurchase, "1 1 1 1", GetMSG("ItemCondition", Math.Round(percent * 100).ToString()), 20, $"0.1 {ymin}", $"0.9 {ymax}", TextAnchor.MiddleLeft);
                }

                UI.CreateButton(ref element, PanelPurchase, UIColors["buttongreen"], GetLang("Yes"), 14, "0.25 0.05", "0.45 0.2", $"UI_ProcessItem {index}");
            }
            else UI.CreateButton(ref element, PanelPurchase, UIColors["buttongreen"], GetLang("Yes"), 14, "0.25 0.05", "0.45 0.2", $"UI_ProcessMoney {index}");
            UI.CreateButton(ref element, PanelPurchase, UIColors["buttonred"], GetLang("No"), 14, "0.55 0.05", "0.75 0.2", $"UI_DestroyPurchaseScreen");
            CuiHelper.AddUi(player, element);
        }

        private void TradeBoxConfirmation(BasePlayer player, uint ID)
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            var element = UI.CreateElementContainer(PanelMarket, UIColors["dark"], "0.425 0.45", "0.575 0.55", true);
            UI.CreatePanel(ref element, PanelMarket, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelMarket, MsgColor, GetLang("TradeBoxCreation"), 14, "0.05 0.56", "0.95 0.9");
            UI.CreateButton(ref element, PanelMarket, UIColors["buttongreen"], GetLang("Yes"), 14, "0.05 0.25", "0.475 0.55", $"UI_SaveTradeBox {ID}");
            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("No"), 14, "0.525 0.25", "0.95 0.55", $"UI_CancelTradeBox");
            CuiHelper.AddUi(player, element);
        }

        private void AdminPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            var i = 0;
            var element = UI.CreateElementContainer(PanelMarket, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
            UI.CreatePanel(ref element, PanelMarket, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelMarket, MsgColor, GetLang("AdminPanel"), 75, "0.05 0", "0.95 1");
            var loc = CalcButtonPos(i);
            UI.CreateButton(ref element, PanelMarket, UIColors["CSorange"], GetLang("BlackListingADD"), 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_BlackList {0} add"); i++;
            loc = CalcButtonPos(i);
            UI.CreateButton(ref element, PanelMarket, UIColors["CSorange"], GetLang("BlackListingREMOVE"), 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_BlackList {0} remove"); i++;
            i = 23;
            loc = CalcButtonPos(i);
            UI.CreateButton(ref element, PanelMarket, UIColors["CSorange"], GetLang("ClearMarket"), 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_ClearMarket"); i++;

            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Back"), 16, "0.03 0.02", "0.13 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), Category.All)}");
            CuiHelper.AddUi(player, element);
        }

        private void MarketBackgroundMenu(BasePlayer player, int page = 0)
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            if (!mData.background.ContainsKey(player.userID))
                mData.background.Add(player.userID, "NONE");
            var i = 0;
            var element = UI.CreateElementContainer(PanelMarket, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
            UI.CreatePanel(ref element, PanelMarket, UIColors["light"], "0.01 0.02", "0.99 0.98");
            UI.CreateLabel(ref element, PanelMarket, MsgColor, GetLang("SelectTheme"), 20, "0 .9", "1 1");
            var count = configData.CustomBackgrounds.Count();
            double entriesallowed = 30;
            double remainingentries = count - (page * (entriesallowed - 1));
            double totalpages = (Math.Floor(count / (entriesallowed - 1)));
            {
                if (page <= totalpages - 1)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("LAST"), "0.8 0.02", "0.85 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.8 0.02", "0.85 0.075", $"UI_MarketBackgroundMenu {totalpages}");
                }
                if (remainingentries > entriesallowed)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("NEXT"), "0.74 0.02", "0.79 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.74 0.02", "0.79 0.075", $"UI_MarketBackgroundMenu {page + 1}");
                }
                if (page > 0)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("BACK"), "0.68 0.02", "0.73 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.68 0.02", "0.73 0.075", $"UI_MarketBackgroundMenu {page - 1}");
                }
                if (page > 1)
                {
                    UI.LoadImage(ref element, PanelMarket, TryForImage("FIRST"), "0.62 0.02", "0.67 0.075");
                    UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 16, "0.62 0.02", "0.67 0.075", $"UI_MarketBackgroundMenu {0}");
                }
            }

            double shownentries = page * entriesallowed;
            int n = 0;
            foreach (var entry in configData.CustomBackgrounds)
            {
                i++;
                if (i < shownentries + 1) continue;
                else if (i <= shownentries + entriesallowed)
                {
                    var loc = CalcButtonPos(n);
                    if (mData.background[player.userID] != entry.Key)
                    {
                        UI.LoadImage(ref element, PanelMarket, TryForImage(entry.Key), $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}");
                        UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{loc[0]} {loc[1]}", $"{loc[2]} {loc[3]}", $"UI_ChangeBackground {entry.Key}");
                        n++;
                    }
                }
            }
            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Back"), 16, "0.03 0.02", "0.13 0.075", $"UI_MarketMainScreen {0} {Enum.GetName(typeof(Category), Category.All)}");
            CuiHelper.AddUi(player, element);
        }

        private void BlackListing(BasePlayer player, int page = 0, string action = "add")
        {
            CuiHelper.DestroyUi(player, PanelMarket);
            var i = 0;
            double count = ItemManager.itemList.Count();
            var element = UI.CreateElementContainer(PanelMarket, UIColors["dark"], "0.3 0.3", "0.7 0.9", true);
            UI.CreatePanel(ref element, PanelMarket, UIColors["light"], "0.01 0.02", "0.99 0.98");
            int entriesallowed = 30;
            int shownentries = page * entriesallowed;
            int n = 0;
            if (action == "add")
            {
                UI.CreateLabel(ref element, PanelMarket, UIColors["black"], $"{TextColors["limegreen"]} {GetLang("SelectItemToBlacklist")}", 75, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    foreach (var entry in ItemManager.itemList.Where(bl => !mData.Blacklist.Contains(bl.shortname)).OrderBy(kvp => kvp.shortname))
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            var pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelMarket, TryForImage(entry.shortname, 0), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                        if (TryForImage(entry.shortname, 0) == TryForImage("BLAHBLAH")) UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], entry.shortname, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleCenter);
                        UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_BackListItem add {entry.shortname}");
                            n++;
                        }
                    }
            }
            else if (action == "remove")
            {
                count = mData.Blacklist.Count();
                UI.CreateLabel(ref element, PanelMarket, UIColors["black"], $"{TextColors["limegreen"]} {GetLang("SelectItemToUnBlacklist")}", 75, "0.05 0", ".95 1", TextAnchor.MiddleCenter);
                    foreach (var entry in mData.Blacklist.OrderBy(kvp => kvp))
                    {
                        i++;
                        if (i < shownentries + 1) continue;
                        else if (i <= shownentries + entriesallowed)
                        {
                            var pos = CalcButtonPos(n);
                            UI.LoadImage(ref element, PanelMarket, TryForImage(entry, 0), $"{pos[0] + 0.005f} {pos[1] + 0.005f}", $"{pos[2] - 0.005f} {pos[3] - 0.005f}");
                        if (TryForImage(entry, 0) == TryForImage("BLAHBLAH")) UI.CreateLabel(ref element, PanelMarket, UIColors["limegreen"], entry, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", TextAnchor.MiddleCenter);
                        UI.CreateButton(ref element, PanelMarket, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_BackListItem remove {entry}");
                            n++;
                        }
                    }
            }
            double remainingentries = count - (page * (entriesallowed - 1));
            double totalpages = (Math.Floor(count / (entriesallowed - 1)));
            {
                if (page <= totalpages - 1)
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonbg"], GetLang("Last"), 16, "0.87 0.02", "0.97 0.075", $"UI_BlackList {totalpages} {action}");
                if (remainingentries > entriesallowed)
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonbg"], GetLang("Next"), 16, "0.73 0.02", "0.83 0.075", $"UI_BlackList {page + 1} {action}");
                if (page > 0)
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Back"), 16, "0.59 0.02", "0.69 0.075", $"UI_BlackList {page - 1} {action}");
                if (page > 1)
                    UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("First"), 16, "0.45 0.02", "0.55 0.075", $"UI_BlackList {0} {action}");
            }
            UI.CreateButton(ref element, PanelMarket, UIColors["buttonred"], GetLang("Back"), 16, "0.03 0.02", "0.13 0.075", $"UI_AdminPanel");
            CuiHelper.AddUi(player, element);
        }

        #endregion

        #region UI Calculations

        private float[] MarketEntryPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.66f);
            Vector2 dimensions = new Vector2(0.3f, 0.25f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 3)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            if (number > 2 && number < 6)
            {
                offsetX = 0.315f;
                offsetY = (-0.01f - dimensions.y) * (number - 3);
            }
            if (number > 5 && number < 9)
            {
                offsetX = 0.315f * 2;
                offsetY = (-0.01f - dimensions.y) * (number - 6);
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] FilterButton(int number)
        {
            Vector2 position = new Vector2(0.01f, 0.0f);
            Vector2 dimensions = new Vector2(0.08f, 0.04f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 2)
            {
                offsetY = (0.005f + dimensions.y) * number;
            }
            if (number > 1 && number < 4)
            {
                offsetX = (0.01f + dimensions.x) * 1;
                offsetY = (0.005f + dimensions.y) * (number - 2);
            }
            if (number > 3 && number < 6)
            {
                offsetX = (0.01f + dimensions.x) * 2;
                offsetY = (0.005f + dimensions.y) * (number - 4);
            }
            if (number > 5 && number < 8)
            {
                offsetX = (0.01f + dimensions.x) * 3;
                offsetY = (0.005f + dimensions.y) * (number - 6);
            }
            if (number > 7 && number < 10)
            {
                offsetX = (0.01f + dimensions.x) * 4;
                offsetY = (0.005f + dimensions.y) * (number - 8);
            }
            if (number > 9 && number < 12)
            {
                offsetX = (0.01f + dimensions.x) * 5;
                offsetY = (0.005f + dimensions.y) * (number - 10);
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] BackgroundButton(int number)
        {
            Vector2 position = new Vector2(0.3f, 0.97f);
            Vector2 dimensions = new Vector2(0.035f, 0.03f);
            float offsetY = 0;
            float offsetX = 0;
            offsetX = (0.005f + dimensions.x) * number;
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.02f, 0.78f);
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
                offsetY = (-0.025f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.01f + dimensions.x) * (number - 12);
                offsetY = (-0.025f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.01f + dimensions.x) * (number - 18);
                offsetY = (-0.025f - dimensions.y) * 3;
            }
            if (number > 23 && number < 30)
            {
                offsetX = (0.01f + dimensions.x) * (number - 24);
                offsetY = (-0.025f - dimensions.y) * 4;
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

        [ConsoleCommand("UI_SaveTradeBox")]
        private void cmdUI_SaveTradeBox(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyMarketPanel(player);
            uint ID;
            if (!uint.TryParse(arg.Args[0], out ID))
            {
                GetSendMSG(player, "NoTradeBox");
                return;
            }
            if (mData.TradeBox.ContainsKey(player.userID))
            {
                Dictionary<uint, string> listings = new Dictionary<uint, string>();
                foreach (var entry in mData.MarketListings.Where(kvp => kvp.Value.seller == player.userID))
                    listings.Add(entry.Key, entry.Value.shortname);
                foreach (var entry in listings)
                    RemoveListing(player.userID, entry.Value, entry.Key, "TradeBoxChanged");
                listings.Clear();
                mData.TradeBox.Remove(player.userID);
            }
            mData.TradeBox.Add(player.userID, ID);
            if (SettingBox.Contains(player.userID)) SettingBox.Remove(player.userID);
            SaveData();
        }

        [ConsoleCommand("UI_CancelTradeBox")]
        private void cmdUI_CancelTradeBox(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyMarketPanel(player);
            if (SettingBox.Contains(player.userID))
            {
                SettingBox.Remove(player.userID);
                GetSendMSG(player, "ExitedBoxMode");
            }
        }

        [ConsoleCommand("UI_DestroyMarketPanel")]
        private void cmdUI_DestroyBoxConfirmation(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyMarketPanel(player);
        }

        [ConsoleCommand("UI_DestroyPurchaseScreen")]
        private void cmdUI_DestroyPurchaseScreen(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyPurchaseScreen(player);
        }

        [ConsoleCommand("UI_ToggleMarketScreen")]
        private void cmdUI_ToggleMarketScreen(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            ToggleMarketScreen(player);
        }

        [ConsoleCommand("UI_SelectSalesItem")]
        private void cmdUI_SelectSalesItem(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyMarketPanel(player);
            uint ID;
            if (!uint.TryParse(arg.Args[0], out ID))
                GetSendMSG(player, "INVALIDENTRY", arg.Args[0]);
            if (SalesItemPrep.ContainsKey(player.userID))
                SalesItemPrep.Remove(player.userID);
            SalesItemPrep.Add(player.userID, new AMItem());
            foreach (var entry in PlayerBoxContents[player.userID].Where(k => k.ID == ID))
            {
                SalesItemPrep[player.userID] = entry;
            }
            PlayerBoxContents.Remove(player.userID);
            SalesItemPrep[player.userID].seller = player.userID;
            SalesItemPrep[player.userID].stepNum = 0;
            if (configData.UseUniqueNames)
                SellItems(player);
            else
                SellItems(player, 1);
        }

        [ConsoleCommand("UI_SelectSR")]
        private void cmdUI_SelectSR(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyMarketPanel(player);
            if (SalesItemPrep.ContainsKey(player.userID))
                SalesItemPrep.Remove(player.userID);
            SalesItemPrep.Add(player.userID, new AMItem());
            PlayerBoxContents.Remove(player.userID);
            SalesItemPrep[player.userID].cat = Category.Money;
            SalesItemPrep[player.userID].shortname = "SR";
            SalesItemPrep[player.userID].skin = 0;
            SalesItemPrep[player.userID].ID = GetRandomNumber();
            SalesItemPrep[player.userID].seller = player.userID;
            SalesItemPrep[player.userID].stepNum = 0;
            NumberPad(player, "UI_SRAmount");
        }

        [ConsoleCommand("UI_SRAmount")]
        private void cmdUI_SRAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            var currentSRlisted = 0;
            if (CheckPoints(player.userID) is int)
                if ((int)CheckPoints(player.userID) >= amount)
                {
                    foreach (var entry in mData.MarketListings.Where(kvp => kvp.Value.seller == player.userID && kvp.Value.shortname == "SR"))
                        currentSRlisted += entry.Value.amount;
                    if ((int)CheckPoints(player.userID) - currentSRlisted >= amount)
                    {
                        SalesItemPrep[player.userID].amount = amount;
                        DestroyMarketPanel(player);
                        if (configData.UseUniqueNames)
                        {
                            SellItems(player);
                            return;
                        }
                        else
                        {
                            SellItems(player, 1);
                            return;
                        }
                    }
                }
            GetSendMSG(player, "NotEnoughSRPoints");
        }


        [ConsoleCommand("UI_SelectpriceItemshortname")]
        private void cmdUI_SelectpriceItemshortname(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            string priceItemshortname = arg.Args[0];
            SalesItemPrep[player.userID].priceItemshortname = priceItemshortname;
            foreach (var cat in Categorization)
            {
                if (cat.Value.Contains(priceItemshortname))
                {
                    SalesItemPrep[player.userID].pricecat = cat.Key;
                    break;
                }
                else
                {
                    SalesItemPrep[player.userID].pricecat = Category.Other;
                    continue;
                }
            }
            SalesItemPrep[player.userID].stepNum = 1;
            NumberPad(player, "UI_SelectPriceAmount");
        }

        [ConsoleCommand("UI_SelectPriceAmount")]
        private void cmdUI_SelectPriceAmount(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int amount = Convert.ToInt32(arg.Args[0]);
            SalesItemPrep[player.userID].priceAmount = amount;
            DestroyMarketPanel(player);
            SellItems(player, 99);
        }


        private uint GetRandomNumber()
        {
            var random = new System.Random();
            uint number = (uint)random.Next(0, int.MaxValue);
            return number;
        }

        [ConsoleCommand("UI_ListItem")]
        private void cmdUI_ListItem(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            if (SalesItemPrep[player.userID].cat != Category.Money)
            {
                if (GetTradeBoxContents(player) == false) return;
                if (!mData.MarketListings.ContainsKey(SalesItemPrep[player.userID].ID))
                {
                    if (BoxCheck(player, SalesItemPrep[player.userID].ID))
                    {
                        var solditem = SalesItemPrep[player.userID].shortname;
                        mData.MarketListings.Add(SalesItemPrep[player.userID].ID, SalesItemPrep[player.userID]);
                        if (SalesItemPrep.ContainsKey(player.userID))
                            SalesItemPrep.Remove(player.userID);
                        if (PlayerBoxContents.ContainsKey(player.userID))
                            PlayerBoxContents.Remove(player.userID);
                        GetSendMSG(player, "NewItemListed", solditem);
                        DestroyMarketPanel(player);
                        MarketMainScreen(player);
                        return;
                    }
                    GetSendMSG(player, "ItemNotInBox");
                }
                GetSendMSG(player, "ItemAlreadyListed");
                CancelListing(player);
            }
            else
            {
                var money = "";
                mData.MarketListings.Add(SalesItemPrep[player.userID].ID, SalesItemPrep[player.userID]);
                if (SalesItemPrep[player.userID].shortname == "SR")
                    money = "Server Rewards Points";
                GetSendMSG(player, "NewMoneyListed", money, SalesItemPrep[player.userID].amount.ToString());
                if (SalesItemPrep.ContainsKey(player.userID))
                    SalesItemPrep.Remove(player.userID);
                if (PlayerBoxContents.ContainsKey(player.userID))
                    PlayerBoxContents.Remove(player.userID);
                DestroyMarketPanel(player);
                MarketMainScreen(player);
            }
        }

        [ConsoleCommand("UI_CancelListing")]
        private void cmdUI_CancelListing(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            CancelListing(player);
        }

        [ConsoleCommand("UI_ChangeBackground")]
        private void cmdUI_ChangeBackground(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            mData.background[player.userID] = arg.Args[0];
            MarketMainScreen(player);
        }

        [ConsoleCommand("UI_MarketMainScreen")]
        private void cmdUI_MainMarketScreen(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (!int.TryParse(arg.Args[0], out page)) return;
            Category cat;
            cat = (Category)Enum.Parse(typeof(Category), arg.Args[1]);
            MarketMainScreen(player, page, cat);
        }

        [ConsoleCommand("UI_MarketBackgroundMenu")]
        private void cmdUI_MarketBackgroundMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (!int.TryParse(arg.Args[0], out page)) return;
            MarketBackgroundMenu(player, page);
        }

        [ConsoleCommand("UI_BlackList")]
        private void cmdUI_BlackList(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (!int.TryParse(arg.Args[0], out page)) return;
            var action = arg.Args[1];
            BlackListing(player, page, action);
        }

        [ConsoleCommand("UI_ClearMarket")]
        private void cmdUI_ClearMarket(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null || !isAuth(player))
                return;
            var count = mData.MarketListings.Count();
            mData.MarketListings.Clear();
            GetSendMSG(player, "MarketCleared", count.ToString());
        }

        [ConsoleCommand("UI_AdminPanel")]
        private void cmdUI_AdminPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            AdminPanel(player);
        }

        [ConsoleCommand("UI_MarketSellScreen")]
        private void cmdUI_MarketSellScreen(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (!int.TryParse(arg.Args[0], out page)) return;
            MarketSellScreen(player, page);
        }

        [ConsoleCommand("UI_Mode")]
        private void cmdUI_Mode(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int action;
            if (!int.TryParse(arg.Args[0], out action)) return;
            if (action == 0)
                mData.mode[player.userID] = false;
            if (action == 1)
                mData.mode[player.userID] = true;
            MarketMainScreen(player);
        }



        [ConsoleCommand("UI_SetBoxMode")]
        private void cmdUI_SetBoxMode(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            if (!SettingBox.Contains(player.userID)) SettingBox.Add(player.userID);
            DestroyMarketPanel(player);
            GetSendMSG(player, "TradeBoxMode");
        }


        [ConsoleCommand("UI_BuyConfirm")]
        private void cmdUI_BuyConfirm(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyPurchaseScreen(player);
            uint ID;
            if (!uint.TryParse(arg.Args[0], out ID)) return;
            AMItem purchaseitem = mData.MarketListings[ID];
            var name = "";
            if (configData.UseUniqueNames && purchaseitem.name != "")
                name = purchaseitem.name;
            else name = purchaseitem.shortname;
            ulong buyer = player.userID;
            ulong seller = mData.MarketListings[ID].seller;
            if (PlayerInventory.ContainsKey(buyer))
                PlayerInventory.Remove(buyer);
            PlayerInventory.Add(buyer, new List<Item>());
            if (GetTradeBox(seller) != null && GetTradeBox(buyer) != null)
            {
                StorageContainer buyerbox = GetTradeBox(buyer);
                StorageContainer sellerbox = GetTradeBox(seller);
                if (!buyerbox.inventory.IsFull() && !sellerbox.inventory.IsFull())
                {
                    if (mData.MarketListings[ID].cat != Category.Money)
                    {
                        var c = 0;
                        foreach (Item item in sellerbox.inventory.itemList.Where(kvp => kvp.uid == ID))
                        {
                            c += item.amount;
                            if (item.condition != purchaseitem.condition)
                            {
                                RemoveListing(seller, name, purchaseitem.ID, "ItemCondChange");
                                MarketMainScreen(player);
                                return;
                            }
                            if (item.amount != purchaseitem.amount)
                            {
                                RemoveListing(seller, name, purchaseitem.ID, "ItemQuantityChange");
                                MarketMainScreen(player);
                                return;
                            }
                            if (c < purchaseitem.amount)
                            {
                                RemoveListing(seller, name, purchaseitem.ID, "ItemGoneChange");
                                MarketMainScreen(player);
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (purchaseitem.shortname == "SR")
                            if ((int)CheckPoints(purchaseitem.seller) < purchaseitem.amount)
                            {
                                RemoveListing(seller, name, purchaseitem.ID, "NotEnoughSRPoints");
                                MarketMainScreen(player);
                                return;
                            }
                    }
                    if (mData.MarketListings[ID].pricecat != Category.Money)
                    {
                        var amount = 0;
                        PlayerInventory[buyer].AddRange(GetItemsOnly(player.inventory.containerWear));
                        PlayerInventory[buyer].AddRange(GetItemsOnly(player.inventory.containerMain));
                        PlayerInventory[buyer].AddRange(GetItemsOnly(player.inventory.containerBelt));
                        foreach (var entry in PlayerInventory[buyer].Where(kvp => kvp.info.shortname == purchaseitem.priceItemshortname))
                        {
                            amount += entry.amount;
                            if (amount >= purchaseitem.priceAmount)
                            {
                                PurchaseConfirmation(player, ID);
                                return;
                            }
                        }
                        GetSendMSG(player, "NotEnoughPurchaseItem", purchaseitem.priceItemshortname, purchaseitem.priceAmount.ToString());
                        return;
                    }
                    else
                    {
                        if (purchaseitem.priceItemshortname == "SR")
                            if ((int)CheckPoints(player.userID) >= purchaseitem.priceAmount)
                            {
                                PurchaseConfirmation(player, ID);
                                return;
                            }
                            else
                            {
                                GetSendMSG(player, "NotEnoughPurchaseItem", purchaseitem.priceItemshortname, purchaseitem.priceAmount.ToString());
                                return;
                            }
                    }
                }
                else
                {
                    if (buyerbox.inventory.IsFull())
                        GetSendMSG(player, "YourTradeBoxFullBuying");
                    else if (sellerbox.inventory.IsFull())
                        GetSendMSG(player, "SellerTradeBoxFullBuying");
                }
            }
            else
            {
                if (GetTradeBox(buyer) == null)
                    GetSendMSG(player, "YourNoTradeBoxBuying");
                else if (GetTradeBox(seller) == null)
                    GetSendMSG(player, "SellerNoTradeBoxBuying");
            }
        }


        [ConsoleCommand("UI_ProcessMoney")]
        private void cmdUI_ProcessMoney(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyPurchaseScreen(player);
        }

        [ConsoleCommand("UI_ProcessItem")]
        private void cmdUI_ProcessItem(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyPurchaseScreen(player);
            uint ID;
            if (!uint.TryParse(arg.Args[0], out ID)) return;
            AMItem purchaseitem = mData.MarketListings[ID];
            ulong buyer = player.userID;
            ulong seller = mData.MarketListings[ID].seller;
            if (PlayerInventory.ContainsKey(buyer))
                PlayerInventory.Remove(buyer);
            if (TransferableItems.ContainsKey(buyer))
                TransferableItems.Remove(buyer);
            if (PlayerPurchaseApproval.ContainsKey(buyer))
                PlayerPurchaseApproval.Remove(buyer);
            TransferableItems.Add(buyer, new List<Item>());
            PlayerInventory.Add(buyer, new List<Item>());
            PlayerPurchaseApproval.Add(buyer, false);
            if (GetTradeBox(seller) != null && GetTradeBox(buyer) != null)
            {
                StorageContainer buyerbox = GetTradeBox(buyer);
                StorageContainer sellerbox = GetTradeBox(seller);
                if (!buyerbox.inventory.IsFull() && !sellerbox.inventory.IsFull())
                {
                    if (purchaseitem.pricecat != Category.Money)
                    {
                        var amount = 0;
                        PlayerInventory[buyer].AddRange(GetItemsOnly(player.inventory.containerWear));
                        PlayerInventory[buyer].AddRange(GetItemsOnly(player.inventory.containerMain));
                        PlayerInventory[buyer].AddRange(GetItemsOnly(player.inventory.containerBelt));
                        foreach (var entry in PlayerInventory[buyer].Where(kvp => kvp.info.shortname == purchaseitem.priceItemshortname))
                        {
                            amount += entry.amount;
                            TransferableItems[buyer].Add(entry);
                            if (amount >= purchaseitem.priceAmount)
                            {
                                PlayerPurchaseApproval[buyer] = true;
                                break;
                            }
                            else continue;
                        }
                    }
                    else
                    {
                        if (purchaseitem.priceItemshortname == "SR")
                            if ((int)CheckPoints(player.userID) >= purchaseitem.priceAmount)
                            {
                                PlayerPurchaseApproval[buyer] = true;
                            }
                    }
                    if (PlayerPurchaseApproval[buyer] == true)
                    {
                        if (ItemsToTransfer.ContainsKey(buyer))
                            ItemsToTransfer.Remove(buyer);
                        ItemsToTransfer.Add(buyer, new List<Item>());
                        if (purchaseitem.pricecat != Category.Money)
                        {
                            foreach (var entry in TransferableItems[buyer])
                                XferCost(entry, player, ID);
                            foreach (Item item in ItemsToTransfer[buyer])
                                item.MoveToContainer(sellerbox.inventory);
                            ItemsToTransfer[buyer].Clear();
                        }
                        else
                        {
                            SRAction(buyer, purchaseitem.priceAmount, "REMOVE");
                            SRAction(seller, purchaseitem.priceAmount, "ADD");
                        }
                        if (purchaseitem.cat != Category.Money)
                        {
                            XferPurchase(buyer, ID, sellerbox.inventory, buyerbox.inventory);
                        }
                        else
                        {
                            SRAction(seller, purchaseitem.amount, "REMOVE");
                            SRAction(buyer, purchaseitem.amount, "ADD");
                        }
                        GetSendMSG(player, "NewPurchase", purchaseitem.shortname, purchaseitem.amount.ToString());
                        AddMessages(seller, "NewSale", purchaseitem.shortname, purchaseitem.amount.ToString());
                        mData.MarketListings.Remove(ID);
                        if (ItemsToTransfer[buyer].Count > 0)
                            foreach (Item item in ItemsToTransfer[buyer])
                                item.MoveToContainer(buyerbox.inventory);
                        MarketMainScreen(player);
                    }
                    else
                    {
                        GetSendMSG(player, "NotEnoughPurchaseItem", purchaseitem.priceItemshortname, purchaseitem.priceAmount.ToString());
                    }
                }
                else
                {
                    if (buyerbox.inventory.IsFull())
                        GetSendMSG(player, "YourTradeBoxFullBuying");
                    else if (sellerbox.inventory.IsFull())
                        GetSendMSG(player, "SellerTradeBoxFullBuying");
                    MarketMainScreen(player);
                }
            }
            else
            {
                if (GetTradeBox(buyer) == null)
                    GetSendMSG(player, "YourNoTradeBoxBuying");
                else if (GetTradeBox(seller) == null)
                    GetSendMSG(player, "SellerNoTradeBoxBuying");
            }
        }

        [ConsoleCommand("UI_RemoveListing")]
        private void cmdUI_RemoveListing(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            uint ID;
            if (!uint.TryParse(arg.Args[0], out ID)) return;
            var name = "";
            if (configData.UseUniqueNames && mData.MarketListings[ID].name != "")
                name = mData.MarketListings[ID].name;
            else name = mData.MarketListings[ID].shortname;
            RemoveListing(player.userID, name, ID, "SellerRemoval");
            MarketMainScreen(player);
        }

        [ConsoleCommand("UI_SellItems")]
        private void cmdUI_SellItems(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int page;
            if (!int.TryParse(arg.Args[0], out page)) return;
            SellItems(player, 1, page);
        }

        [ConsoleCommand("UI_BackListItem")]
        private void cmdUI_BackListItem(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            var action = arg.Args[0];
            var item = arg.Args[1];
            if (action == "add")
                mData.Blacklist.Add(item);
            else if (action == "remove")
                mData.Blacklist.Remove(item);
            AdminPanel(player);
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
                GetSendMSG(p, "AMInfo", configData.MarketMenuKeyBinding);
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }

        private void SetBoxFullNotification(string ID)
        {
            timers.Add(ID, timer.Once(5 * 60, () => timers.Remove(ID)));
        }

        #endregion

        #region Classes
        class MarketData
        {
            public Dictionary<uint, AMItem> MarketListings = new Dictionary<uint, AMItem>();
            public Dictionary<ulong, uint> TradeBox = new Dictionary<ulong, uint>();
            public Dictionary<ulong, string> background = new Dictionary<ulong, string>();
            public Dictionary<ulong, bool> mode = new Dictionary<ulong, bool>();
            public Dictionary<ulong, List<Unsent>> OutstandingMessages = new Dictionary<ulong, List<Unsent>>();
            public List<string> Blacklist = new List<string>();
            public Dictionary<ulong, string> names = new Dictionary<ulong, string>();
        }

        class CategoryData
        {
            public Dictionary<Category, List<string>> Categorization = new Dictionary<Category, List<string>>();
        }

        class Unsent
        {
            public string message;
            public string arg1;
            public string arg2;
            public string arg3;
            public string arg4;
        }

        enum Category
        {
            Weapons,
            Armor,
            Attire,
            Ammunition,
            Medical,
            Tools,
            Building,
            Resources,
            Other,
            All,
            Food,
            Money,
            Components,
        }

        class AMItem
        {
            public string name;
            public string shortname;
            public ulong skin;
            public uint ID;
            public Category cat;
            public bool approved;
            public Category pricecat;
            public string priceItemshortname;
            public int priceItemID;
            public int priceAmount;
            public int amount;
            public int stepNum;
            public ulong seller;
            public float condition;
        }

        #endregion

        #region Backgrounds

        private void AddNeededImages()
        {
            foreach (var entry in UIElements)
                    AddImage(entry.Value, entry.Key, (ulong)ResourceId);
        }

        [ConsoleCommand("refreshbackgrounds")]
        private void cmdrefreshbackgrounds(ConsoleSystem.Arg arg)
        {
                RefreshBackgrounds();
        }

        private void RefreshBackgrounds()
        {
            var i = 0;
            foreach (var entry in configData.CustomBackgrounds)
            {
                if (!HasImage(entry.Key, (ulong)ResourceId))
                { AddImage(entry.Value, entry.Key, (ulong)ResourceId); i++; }
            }
            if (i > 0)
                Puts(GetMSG("BckAdded", i.ToString()));
        }

        #endregion

        #region Absolut Market Data Management

        private Dictionary<string, string> UIElements = new Dictionary<string, string>
        {
            { "ARROW", "http://www.freeiconspng.com/uploads/red-arrow-curved-5.png" },
            {"FIRST", "http://cdn.mysitemyway.com/etc-mysitemyway/icons/legacy-previews/icons/simple-black-square-icons-arrows/126517-simple-black-square-icon-arrows-double-arrowhead-left.png" },
            {"BACK", "https://image.freepik.com/free-icon/back-left-arrow-in-square-button_318-76403.png" },
            {"NEXT", "https://image.freepik.com/free-icon/right-arrow-square-button-outline_318-76302.png"},
            {"LAST","http://cdn.mysitemyway.com/etc-mysitemyway/icons/legacy-previews/icons/matte-white-square-icons-arrows/124577-matte-white-square-icon-arrows-double-arrowhead-right.png" },
            {"OFILTER", "https://pixabay.com/static/uploads/photo/2016/01/23/11/41/button-1157299_960_720.png" },
            {"UFILTER","https://pixabay.com/static/uploads/photo/2016/01/23/11/42/button-1157301_960_720.png" },
            {"ADMIN", "https://pixabay.com/static/uploads/photo/2016/01/23/11/26/button-1157269_960_720.png" },
            {"MISC","https://pixabay.com/static/uploads/photo/2015/07/25/07/55/the-button-859343_960_720.png" },
            {"SELL","https://pixabay.com/static/uploads/photo/2015/07/25/08/03/the-button-859350_960_720.png" },
            {"SR", "http://oxidemod.org/data/resource_icons/1/1751.jpg?1456924271" },
            {"ECO", "http://oxidemod.org/data/resource_icons/0/717.jpg?1465675504" },
        };

        private Dictionary<Category, List<string>> Categorization = new Dictionary<Category, List<string>>
        {
            {Category.Money, new List<string>
            {
                {"SR"},
                {"ECO" },
            }
            },
            {Category.Attire, new List<string>
            {
            {"tshirt"},
            {"pants"},
            {"shoes.boots"},
            {"tshirt.long"},
            {"mask.bandana"},
            {"mask.balaclava"},
            {"jacket.snow"},
            {"jacket"},
            {"hoodie"},
            {"hat.cap"},
            {"hat.beenie"},
            {"burlap.gloves"},
            {"burlap.shirt"},
            {"hat.boonie"},
            {"santahat"},
            {"hazmat.pants"},
            {"hazmat.jacket"},
            {"hazmat.helmet"},
            {"hazmat.gloves"},
            {"hazmat.boots"},
            {"hazmatsuit" },
            {"hat.miner"},
            {"hat.candle"},
            {"burlap.trousers"},
            {"burlap.shoes"},
            {"burlap.headwrap"},
            {"shirt.tanktop"},
            {"shirt.collared"},
            {"pants.shorts"},
            }
            },
            {Category.Armor, new List<string>
            {
            {"bucket.helmet"},
            {"wood.armor.pants"},
            {"wood.armor.jacket"},
            {"roadsign.kilt"},
            {"roadsign.jacket"},
            {"riot.helmet"},
            {"metal.plate.torso"},
            {"metal.facemask"},
            {"coffeecan.helmet"},
            {"bone.armor.suit"},
            {"attire.hide.vest"},
            {"attire.hide.skirt"},
            {"attire.hide.poncho"},
            {"attire.hide.pants"},
            {"attire.hide.helterneck"},
            {"attire.hide.boots"},
            {"deer.skull.mask"},
            }
            },
            {Category.Weapons, new List<string>
            {
            {"pistol.revolver"},
            {"pistol.semiauto"},
            {"rifle.ak"},
            {"rifle.bolt"},
            {"shotgun.pump"},
            {"shotgun.waterpipe"},
            {"rifle.lr300"},
            {"pistol.m92" },
            {"crossbow"},
            {"smg.thompson"},
            {"weapon.mod.small.scope"},
            {"weapon.mod.silencer"},
            {"weapon.mod.muzzlebrake"},
            {"weapon.mod.muzzleboost"},
            {"weapon.mod.lasersight"},
            {"weapon.mod.holosight"},
            {"weapon.mod.flashlight"},
            {"spear.wooden"},
            {"spear.stone"},
            {"smg.2"},
            {"shotgun.double"},
            {"salvaged.sword"},
            {"salvaged.cleaver"},
            {"rocket.launcher"},
            {"rifle.semiauto"},
            {"pistol.eoka"},
            {"machete"},
            {"mace"},
            {"longsword"},
            {"lmg.m249"},
            {"knife.bone"},
            {"flamethrower"},
            {"bow.hunting"},
            {"bone.club"},
            {"grenade.f1"},
            {"grenade.beancan"},
            }
            },
            {Category.Ammunition, new List<string>
            {
            {"ammo.handmade.shell"},
            {"ammo.pistol"},
             {"ammo.pistol.fire"},
            {"ammo.pistol.hv"},
            {"ammo.rifle"},
            {"ammo.rifle.explosive"},
            {"ammo.rifle.hv"},
            {"ammo.rifle.incendiary"},
            {"ammo.rocket.basic"},
            {"ammo.rocket.fire"},
            {"ammo.rocket.hv"},
            {"ammo.rocket.smoke"},
            {"ammo.shotgun"},
            {"ammo.shotgun.slug"},
            {"arrow.hv"},
            {"arrow.wooden"},
            }
            },

            {Category.Medical, new List<string>
            {
            {"bandage"},
            {"syringe.medical"},
            { "largemedkit"},
            { "antiradpills"},
            }
            },


            {Category.Building, new List<string>
            {
            {"bed"},
            {"box.wooden"},
            {"box.wooden.large"},
            {"ceilinglight"},
            {"door.double.hinged.metal"},
            {"door.double.hinged.toptier"},
            {"door.double.hinged.wood"},
            {"door.hinged.metal"},
            {"door.hinged.toptier"},
            {"door.hinged.wood"},
            {"floor.grill"},
            {"floor.ladder.hatch"},
            {"gates.external.high.stone"},
            {"gates.external.high.wood"},
            {"shelves"},
            {"shutter.metal.embrasure.a"},
            {"shutter.metal.embrasure.b"},
            {"shutter.wood.a"},
            {"sign.hanging"},
            {"sign.hanging.banner.large"},
            {"sign.hanging.ornate"},
            {"sign.pictureframe.landscape"},
            {"sign.pictureframe.portrait"},
            {"sign.pictureframe.tall"},
            {"sign.pictureframe.xl"},
            {"sign.pictureframe.xxl"},
            {"sign.pole.banner.large"},
            {"sign.post.double"},
            {"sign.post.single"},
            {"sign.post.town"},
            {"sign.post.town.roof"},
            {"sign.wooden.huge"},
            {"sign.wooden.large"},
            {"sign.wooden.medium"},
            {"sign.wooden.small"},
            {"jackolantern.angry"},
            {"jackolantern.happy"},
            {"ladder.wooden.wall"},
            {"lantern"},
            {"lock.code"},
            {"mining.quarry"},
            {"wall.external.high"},
            {"wall.external.high.stone"},
            {"wall.frame.cell"},
            {"wall.frame.cell.gate"},
            {"wall.frame.fence"},
            {"wall.frame.fence.gate"},
            {"wall.frame.shopfront"},
            {"wall.window.bars.metal"},
            {"wall.window.bars.toptier"},
            {"wall.window.bars.wood"},
            {"lock.key"},
            { "barricade.concrete"},
            {"barricade.metal"},
            { "barricade.sandbags"},
            { "barricade.wood"},
            { "barricade.woodwire"},
            { "barricade.stone"},
            }
            },

            {Category.Resources, new List<string>
            {
            {"charcoal"},
            {"cloth"},
            {"crude.oil"},
            {"fat.animal"},
            {"hq.metal.ore"},
            {"lowgradefuel"},
            {"metal.fragments"},
            {"metal.ore"},
            {"leather"},
            {"metal.refined"},
            {"wood"},
            {"seed.corn"},
            {"seed.hemp"},
            {"seed.pumpkin"},
            {"stones"},
            {"sulfur"},
            {"sulfur.ore"},
            {"gunpowder"},
            {"researchpaper"},
            {"explosives"},
            }
            },

            {Category.Tools, new List<string>
            {
            {"botabag"},
            {"box.repair.bench"},
            {"bucket.water"},
            {"explosive.satchel"},
            {"explosive.timed"},
            {"flare"},
            {"fun.guitar"},
            {"furnace"},
            {"furnace.large"},
            {"hatchet"},
            {"icepick.salvaged"},
            {"axe.salvaged"},
            {"pickaxe"},
            {"research.table"},
            {"small.oil.refinery"},
            {"stone.pickaxe"},
            {"stonehatchet"},
            {"supply.signal"},
            {"surveycharge"},
            {"target.reactive"},
            {"tool.camera"},
            {"water.barrel"},
            {"water.catcher.large"},
            {"water.catcher.small"},
            {"water.purifier"},
            {"torch"},
            {"stash.small"},
            {"sleepingbag"},
            {"hammer.salvaged"},
            {"hammer"},
            {"blueprintbase"},
            {"fishtrap.small"},
            {"building.planner"},
            }
            },

            {Category.Other, new List<string>
            {
            {"cctv.camera"},
            {"pookie.bear"},
            {"targeting.computer"},
            {"trap.bear"},
            {"trap.landmine"},
            {"autoturret"},
            {"spikes.floor"},
            {"note"},
            {"paper"},
            {"map"},
            {"campfire"},
            {"blueprintbase" },
            }
            },

            {Category.Components, new List<string>
            {
            {"bleach"},
            {"ducttape"},
            {"propanetank"},
            {"gears"},
            {"glue"},
            {"metalblade"},
            {"metalpipe"},
            {"metalspring"},
            {"riflebody"},
            {"roadsigns"},
            {"rope"},
            {"sewingkit"},
            {"sheetmetal"},
            {"smgbody"},
            {"sticks"},
            {"tarp"},
            {"techparts"},
            {"semibody" },
            }
            },


            {Category.Food, new List<string>
            {
            { "wolfmeat.cooked"},
            {"waterjug"},
            {"water.salt"},
            {"water"},
            {"smallwaterbottle"},
            {"pumpkin"},
            {"mushroom"},
            {"meat.pork.cooked"},
            {"humanmeat.cooked"},
            {"granolabar"},
            {"fish.cooked"},
            {"chocholate"},
            {"chicken.cooked"},
            {"candycane"},
            {"can.tuna"},
            {"can.beans"},
            {"blueberries"},
            {"black.raspberries"},
            {"bearmeat.cooked"},
            {"apple"},
            }
            }
        };

        void SaveData()
        {
            MData.WriteObject(mData);
        }

        void LoadData()
        {
            try
            {
                mData = MData.ReadObject<MarketData>();
                if (mData == null)
                    mData = new MarketData();
            }
            catch
            {
                Puts("Couldn't load the Absolut Market Data, creating a new datafile");
                mData = new MarketData();
            }
            try
            {
                cData = CData.ReadObject<CategoryData>();
                if (cData == null || cData.Categorization == null || cData.Categorization.Count < 1)
                {
                    cData = new CategoryData();
                    cData.Categorization = Categorization;
                    Puts("Default Category Data Loaded");
                }
            }
            catch
            {
                Puts("Couldn't load the Absolut Market Categorization File, creating a new datafile");
                cData = new CategoryData();
                cData.Categorization = Categorization;
            }
            CData.WriteObject(cData);
        }

        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public string MarketMenuKeyBinding { get; set; }
            public bool UseUniqueNames { get; set; }
            public bool ServerRewards { get; set; }
            public int InfoInterval { get; set; }
            public bool Economics { get; set; }
            public Dictionary<string, string> CustomBackgrounds {get;set;}


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
                MarketMenuKeyBinding = "b",
                UseUniqueNames = false,
                InfoInterval = 15,
                ServerRewards = false,
                Economics = false,
                CustomBackgrounds = new Dictionary<string, string>
        {
            { "NEVERDELETE", "http://www.intrawallpaper.com/static/images/r4RtXBr.png" },
            { "default2", "http://www.intrawallpaper.com/static/images/background-wallpapers-32_NLplhCS.jpg" },
            { "default3", "http://www.intrawallpaper.com/static/images/Light-Wood-Background-Wallpaper_JHG6qot.jpg" },
            { "default4", "http://www.intrawallpaper.com/static/images/White-Background-BD1.png" },
            { "default5", "http://www.intrawallpaper.com/static/images/Red_Background_05.jpg" },
            { "default6", "http://www.intrawallpaper.com/static/images/White-Background-BD1.png" },
            { "default7", "http://www.intrawallpaper.com/static/images/abstract-hd-wallpapers-1080p_gDn0G81.jpg" },
            { "default8", "http://www.intrawallpaper.com/static/images/Background-HD-High-Quality-C23.jpg" },
            { "default10", "http://www.intrawallpaper.com/static/images/wood_background_hd_picture_3_169844.jpg" },
            { "default11", "http://www.intrawallpaper.com/static/images/518079-background-hd.jpg" },
            { "default12", "http://www.intrawallpaper.com/static/images/special_flashy_stars_background_03_hd_pictures_170805.jpg" },
            { "default13", "http://www.intrawallpaper.com/static/images/maxresdefault_jKFJl8g.jpg" },
            { "default14", "http://www.intrawallpaper.com/static/images/maxresdefault15.jpg" },
                }
        };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "Absolut Market: " },
            {"AMInfo", "This server is running Absolut Market. Press '{0}' to access the Market Menu and to set a Trade Box. Happy Trading!"},
            {"NoTradeBox", "Error finding target Trade Box!" },
            {"TradeBoxDestroyed", "Your Trade Box has been destroyed!" },
            {"TradeBoxEmpty", "Your Trade Box is empty... place items in it to sell them" },
            {"TradeBoxEmptyNoSR", "Your Trade Box is empty and you have 0 Server Rewards Points...Load Items or Get Points to continue" },
            {"TradeBoxFull", "Your Trade Box is full! Clear room first." },
            {"TradeBoxCreation", "Make this your Trade Box?" },
            {"TradeBoxCanceled", "You have " },
            {"Yes", "Yes?" },
            {"No", "No?" },
            {"SetName", "Please Provide a Name for this Item: {0}</color>" },
            {"SetpriceItemshortname", "Please Select an Item you want in return for {0}</color>"  },
            {"SetPriceAmount", "Please type the amount of {0} required to buy the {1}</color>" },
            {"ItemDetails", "You are listing: {0}: {1}\n          For {2} {3}" },
            {"ItemName", "" },
            {"SelectItemToSell", "Please select an Item from your Trade Box to sell...</color>" },
            {"ListItem", "List Item?" },
            {"CancelListing", "Cancel Listing?" },
            {"ItemListingCanceled", "You have successfully canceled item listing!" },
            {"NewItemListed", "You have successfully listed {0}!" },
            {"NewMoneyListed", "You have successfully listed {1} {0}" },
            {"ItemNotInBox", "It appears the item you are trying to list is no longer in the Trade Box. Listing Canceled..." },
            {"NotEnoughPurchaseItem", "You do not have enough {0}. You need {1}!" },
            {"TradeBoxMode", "You are now in Trade Box Selection Mode. Place a large or small wooden box at anytime to make it your Trade Box. Type quit at anytime to leave this mode." },
            {"ExitedBoxMode", "You have successfully exited Trade Box Selection Mode." },
            {"TradeBoxAssignment", "Set\nTrade Box" },
            {"ItemBeingSold","For Sale" },
            {"Purchasecost", "Cost" },
            {"NewItemInfo", "Listing Item Details" },
            {"removelisting", "Remove?" },
            {"YourTradeBoxFullBuying","Your Trade Box is Full!"},
            {"SellerTradeBoxFullBuying", "Seller's Trade Box is Full!" },
            {"YourNoTradeBoxBuying","You do not have a Trade Box!" },
            {"SellerNoTradeBoxBuying","Seller does not have a Trade Box!" },
            {"NewPurchase", "You have successfully purchased {1} {0}" },
            {"NewSale", "You have successfully sold {1} {0}" },
            {"Next", "Next" },
            {"Back", "Back" },
            {"First", "First" },
            {"Last", "Last" },
            {"Close", "Close"},
            {"Quit", "Quit"},
            {"PurchaseConfirmation", "Would you like to purchase:\n{0}?" },
            {"ItemCondition", "Item Condition: {0}%" },
            {"ConditionWarning", "Some items do not have a condition and will reflect as 0" },
            {"ItemAlreadyListed", "This item already appears to be listed!" },
            {"ItemRemoved", "{0} has been removed from the Absolut Market because {1}" },
            {"FromBox", "it was removed from the Trade Box!" },
            {"ItemCondChange", "the condition of the item has changed." },
            {"ItemQuantityChange", "the quantity of the item has changed." },
            {"TradeBoxChanged", "you have set a new Trade Box." },
            {"ItemGoneChange", "the item is not in the Seller's box." },
            {"SelectItemToBlacklist", "Select an item to Blacklist...</color>" },
            {"SelectItemToUnBlacklist", "Select an item to Remove from Blacklist...</color>" },
            {"AdminPanel", "Admin Menu" },
            {"BlackListingADD", "Add\nBacklist Item" },
            {"BlackListingREMOVE", "Remove\nBacklist Item" },
            {"ClearMarket", "Clear Market" },
            {"ChangeTheme", "Change Theme" },
            {"SelectTheme", "Select a Theme" },
            {"Amount", "Amount: {0}" },
            {"Name", "Name: {0}" },
            {"NotEnoughSRPoints", "You do not have enough ServerReward Points!" },
            {"ImgReload", "Images have been wiped and reloaded!" },
            {"ImgRefresh", "Images have been refreshed !" },
            {"BckAdded", "{0} Background Images have been added!" },
            {"Seller", "         Seller\n{0}" },
            {"InExchange", "In Exchange\nFor" },
            {"SellerRemoval", "you removed it." },
            {"AllItemsAreBL", "All the items in your box are BlackListed!" },
            {"AllItemsAreBLNoSR", "All the items in your box are BlackListed and you have 0 Server Rewards Points!" },
            {"AllItemsAreListed", "All the items in your box are already listed! Add more and try again." },
            {"AllItemsAreListedNoSR", "All the items in your box are already listed and you have 0 Server Rewards Points!" },
            {"ChangeMode", "Change Mode" },
            {"MarketCleared", "Market Cleared of {0} listings" }
        };
        #endregion
    }
}