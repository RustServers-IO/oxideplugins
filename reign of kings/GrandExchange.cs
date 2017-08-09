using CodeHatch.Blocks;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Networking;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.Inventory.Blueprints.Components;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.UserInterface.Dialogues;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Grand Exchange", "D-Kay && Scorpyon", "2.2.1", ResourceId = 1145)]
    public class GrandExchange : ReignOfKingsPlugin
    {
        #region Variables

        private const int MaxPossibleGold = int.MaxValue;
        private static double SellPercentage = 50;
        private static double Inflation = 1; // This is the inflation modifier. More means bigger jumps in price changes (Currently raises at approx 1%
        private static double MaxDeflation = 5; // This is the deflation modifier. This is the most that a price can drop below its average price to buy and above it's price to sell(Percentage)
        private int PriceDeflationTime = 3600; // This dictates the number of seconds for each tick which brings the prices back towards their original values
        private double GoldStealPercentage = 20; // This is the maximum percentage of gold that can be stolen from a player
        private int GoldRewardForPve = 20; // This is the maximum amount rewarded to a player for killing monsters, etc. (When harvesting the dead body)
        private bool PvpGold = true; // Turns on/off gold for PVP
        private bool PveGold = true; // Turns on/off gold for PVE
        private bool SafeTrade = false; // Determines whether the marked safe area is Safe against being attacked / PvP
        private static int PlayerShopStackLimit = 5; // Determines the maximum number of stacks of an item a player can have in their shop
        private static int PlayerShopMaxSlots = 10; // Determines the maximum number of individual items the player can stock (Prevents using this as a 'Bag of Holding' style Chest!!)
        private bool CanUseGE = true;
        private bool UseDeflation = true;

        private class GrandExchangeData
        {
            public float X1 { get; set; } = 0f;
            public float Z1 { get; set; } = 0f;
            public float X2 { get; set; } = 0f;
            public float Z2 { get; set; } = 0f;
            public SortedDictionary<string, TradeData> TradeList { get; set; } = new SortedDictionary<string, TradeData>();

            public GrandExchangeData() { }

            public GrandExchangeData(Vector3 pos1, Vector3 pos2)
            {
                X1 = pos1.x;
                Z1 = pos1.z;
                X2 = pos2.x;
                Z2 = pos2.z;
            }

            public void AddPosition(Vector3 position, int type)
            {
                switch (type)
                {
                    case 1:
                        X1 = position.x;
                        Z1 = position.z;
                        break;
                    case 2:
                        X2 = position.x;
                        Z2 = position.z;
                        break;
                }
            }

            public void AddItem(string resource, int price)
            {
                TradeList.Add(resource, new TradeData(price));
            }

            public int HasPosition()
            {
                if (X1 == 0f || Z1 == 0f) return 0;
                if (X2 == 0f || Z2 == 0f) return 1;
                return 2;
            }

            public void RemoveGEMarks()
            {
                X1 = 0f;
                Z1 = 0f;
                X2 = 0f;
                Z2 = 0f;
            }

            public bool IsInTradeArea(Vector3 position)
            {
                if (HasPosition() != 2) return true;

                if ((position.x < X1 && position.x > X2) && (position.z > Z1 && position.z < Z2)) return true;
                if ((position.x < X1 && position.x > X2) && (position.z < Z1 && position.z > Z2)) return true;
                if ((position.x > X1 && position.x < X2) && (position.z < Z1 && position.z > Z2)) return true;
                if ((position.x > X1 && position.x < X2) && (position.z > Z1 && position.z < Z2)) return true;

                return false;
            }
        }
        private class TradeData
        {
            public int OriginalPrice { get; set; } = 0;
            public int BuyPrice { get; set; } = 0;
            public int SellPrice { get; set; } = 0;

            public TradeData() { }

            public TradeData(int originalPrice)
            {
                OriginalPrice = originalPrice;
                BuyPrice = originalPrice;
                SellPrice = (int)(originalPrice * (SellPercentage / 100));
            }

            public void SetPrice(int price)
            {
                OriginalPrice = price;
                BuyPrice = price;
                SellPrice = (int)(price * (SellPercentage / 100));
            }

            public int GetPrice(int amount, int type)
            {
                var price = 0;
                switch (type)
                {
                    case 1:
                        price = BuyPrice;
                        break;
                    case 2:
                        price = SellPrice;
                        break;
                }

                return price * amount;
            }

            public void UpdatePrices(int stackLimit, int amount, int type)
            {
                switch (type)
                {
                    case 1:
                        BuyPrice = (int)(BuyPrice + ((OriginalPrice * (Inflation / 100)) * (amount / stackLimit)));
                        if (BuyPrice < 1) BuyPrice = 1;
                        break;
                    case 2:
                        SellPrice = (int)(SellPrice - ((OriginalPrice * (Inflation / 100)) * (amount / stackLimit)));
                        if (SellPrice < 1) SellPrice = 1;
                        break;
                }
            }

            public void DeflatePrice()
            {
                var inflationModifier = Inflation / 100;
                var deflationModifier = MaxDeflation / 100;
                var stackModifier = 1.0;
                var newBuyPrice = (int)(BuyPrice - ((OriginalPrice * inflationModifier) * stackModifier));
                var newSellPrice = (int)(SellPrice + ((OriginalPrice * inflationModifier) * stackModifier));

                var priceBottomShelf = (int)(OriginalPrice - ((OriginalPrice * deflationModifier) * stackModifier));
                var priceTopShelf = (int)((OriginalPrice + ((OriginalPrice * deflationModifier) * stackModifier)) * (SellPercentage / 100));

                if (newBuyPrice < priceBottomShelf) newBuyPrice = priceBottomShelf;
                if (newSellPrice > priceTopShelf) newSellPrice = priceTopShelf;

                BuyPrice = newBuyPrice;
                SellPrice = newSellPrice;
            }
        }
        private class PlayerData
        {
            public string Name { get; set; } = "";
            public long Gold { get; set; } = 0;
            public ShopData Shop { get; set; } = new ShopData();

            public PlayerData() { }

            public PlayerData(string name)
            {
                Name = name;
            }

            public PlayerData(string name, int gold)
            {
                Name = name;
                Gold = gold;
            }
        }
        private class ShopData
        {
            public string Name { get; set; } = "Local Store";
            public SortedDictionary<string, ItemData> ItemList { get; set; } = new SortedDictionary<string, ItemData>();
            public float X1 { get; set; } = 0f;
            public float Z1 { get; set; } = 0f;
            public float X2 { get; set; } = 0f;
            public float Z2 { get; set; } = 0f;

            public ShopData() { }

            public ShopData(string name)
            {
                Name = name;
                ItemList = new SortedDictionary<string, ItemData>();
            }

            public ShopData(string name, SortedDictionary<string, ItemData> itemList)
            {
                Name = name;
                ItemList = itemList;
            }

            public int HasPosition()
            {
                if (X1 == 0 || Z1 == 0) return 0;
                if (X2 == 0f || Z2 == 0f) return 1;
                return 2;
            }

            public void AddPosition(Vector3 position, int type)
            {
                switch (type)
                {
                    case 1:
                        X1 = position.x;
                        Z1 = position.z;
                        break;
                    case 2:
                        X2 = position.x;
                        Z2 = position.z;
                        break;
                }
            }

            public Vector3 GetPosition(int type)
            {
                switch (type)
                {
                    case 1:
                        return new Vector3(X1, 0, Z1);
                    case 2:
                        return new Vector3(X2, 0, Z2);
                }
                return new Vector3();
            }

            public void RemoveShop()
            {
                X1 = 0f;
                Z1 = 0f;
                X2 = 0f;
                Z2 = 0f;
            }

            public bool IsInShopArea(Vector3 position)
            {
                if (HasPosition() != 2) return false;

                if ((position.x < X1 && position.x > X2) && (position.z > Z1 && position.z < Z2)) return true;
                if ((position.x < X1 && position.x > X2) && (position.z < Z1 && position.z > Z2)) return true;
                if ((position.x > X1 && position.x < X2) && (position.z < Z1 && position.z > Z2)) return true;
                if ((position.x > X1 && position.x < X2) && (position.z > Z1 && position.z < Z2)) return true;

                return false;
            }

            public void RemoveItem(string item, int amount)
            {
                if (ItemList[item].Amount == amount) ItemList.Remove(item);
                else ItemList[item].Amount -= amount;
            }

            public int AddItem(string resource, int price, int amount)
            {
                var stackLimit = GetStackLimit(resource);
                if (ItemList.ContainsKey(resource))
                {
                    if (ItemList[resource].Amount + amount > PlayerShopStackLimit * stackLimit) return 1;
                    ItemList[resource].AddAmount(amount);
                    if (price > 0) ItemList[resource].Price = price;
                    return 2;
                }

                if (ItemList.Count >= PlayerShopMaxSlots) return 0;
                if (amount > PlayerShopStackLimit * stackLimit) return 1;

                ItemList.Add(resource, new ItemData(price, amount));
                return 2;
            }
        }
        private class ItemData
        {
            public int Price { get; set; } = 0;
            public int Amount { get; set; } = 0;

            public ItemData() { }

            public ItemData(int price, int amount)
            {
                Price = price;
                Amount = amount;
            }

            public int GetAmount()
            {
                return Amount;
            }

            public long GetPrice(long amount)
            {
                return Price * amount;
            }

            public void AddAmount(int amount)
            {
                Amount += amount;
            }
        }

        private GrandExchangeData _GEData = new GrandExchangeData();
        private Dictionary<ulong, PlayerData> _PlayerData = new Dictionary<ulong, PlayerData>();
        //private static SortedDictionary<string, int> _ItemList = new SortedDictionary<string, int>();

        private readonly System.Random _Random = new System.Random();
        
        #region Default Trade List
        private SortedDictionary<string, int> _DefaultTradeList = new SortedDictionary<string, int>()
        {
            { "Apple", 25 },
            { "Bear Hide", 6250 },
            { "Beet", 38 },
            { "Berries", 38 },
            { "Bread", 150 },
            { "Cabbage",  25},
            { "Carrot", 38 },
            { "Charcoal", 15 },
            { "Clay", 10 },
            { "Cobblestone Block", 625 },
            { "Diamond", 6250 },
            { "Dirt", 5 },
            { "Flax", 25 },
            { "Iron", 25 },
            { "Iron Ingot", 350 },
            { "Leather Hide", 25 },
            { "Log Block", 500 },
            { "Lumber", 4 },
            { "Oil", 20 },
            { "Onion", 38 },
            { "Reinforced Wood (Iron) Block", 700 },
            { "Steel Compound", 75 },
            { "Steel Ingot", 1000 },
            { "Stone", 20 },
            { "Stone Block", 3500 },
            { "Stone Slab", 2700 },
            { "Water", 20 },
            { "Wood", 10 },
            { "Wood Block", 250 },
        };
        #endregion

        #endregion

        #region Save and Load Data

        void Loaded()
        {
            LoadTradeData();
            LoadConfigData();
            LoadDefaultMessages();

            timer.Repeat(PriceDeflationTime, 0, DeflatePrices);

            if (SellPercentage == 0) SellPercentage = 50;
            SaveConfigData();

            permission.RegisterPermission("GrandExchange.Modify.Settings", this);
            permission.RegisterPermission("GrandExchange.Modify.Itemlist", this);
            permission.RegisterPermission("GrandExchange.Show", this);
        }

        private void LoadTradeData()
        {
            _GEData = Interface.Oxide.DataFileSystem.ReadObject<GrandExchangeData>("GrandExchangeData");
            _PlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("GrandExchangePlayerData");
        }

        private void SaveTradeData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("GrandExchangeData", _GEData);
            Interface.Oxide.DataFileSystem.WriteObject("GrandExchangePlayerData", _PlayerData);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            PvpGold = GetConfig("Gold", "PvpGold", true);
            PveGold = GetConfig("Gold", "PveGold", true);
            GoldRewardForPve = GetConfig("Gold", "GoldRewardForPve", 75);
            GoldStealPercentage = GetConfig("Gold", "GoldStealPercentage", 30);
            CanUseGE = GetConfig("Trading", "CanUseGE", true);
            SafeTrade = GetConfig("Trading", "SafeTrade", true);
            UseDeflation = GetConfig("Trading", "UseDeflation", true);
            PlayerShopStackLimit = GetConfig("Trading", "PlayerShopStackLimit", 5);
            PlayerShopMaxSlots = GetConfig("Trading", "PlayerShopMaxSlots", 10);
            SellPercentage = GetConfig("Trading", "SellPercentage", 50);
            Inflation = GetConfig("Trading", "Inflation", 1);
            MaxDeflation = GetConfig("Trading", "MaxDeflation", 5);
        }

        private void SaveConfigData()
        {
            Config["Gold", "PvpGold"] = PvpGold;
            Config["Gold", "PveGold"] = PveGold;
            Config["Gold", "GoldRewardForPve"] = GoldRewardForPve;
            Config["Gold", "GoldStealPercentage"] = GoldStealPercentage;
            Config["Trading", "CanUseGE"] = CanUseGE;
            Config["Trading", "SafeTrade"] = SafeTrade;
            Config["Trading", "UseDeflation"] = UseDeflation;
            Config["Trading", "PlayerShopStackLimit"] = PlayerShopStackLimit;
            Config["Trading", "PlayerShopMaxSlots"] = PlayerShopMaxSlots;
            Config["Trading", "SellPercentage"] = SellPercentage;
            Config["Trading", "Inflation"] = Inflation;
            Config["Trading", "MaxDeflation"] = MaxDeflation;

            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                { "No Permission", "You don't have permission to use this command."},
                { "No Marks", "There are no marks to remove."},
                { "Invalid Args", "Oops, something went wrong! Please use /gehelp to see if you used the correct format."},
                { "Invalid Amount", "That is not a recognised amount."},
                { "Invalid Player", "That player could not be found."},
                { "Location", "Current Location: x:{0} y:{1} z:{2}" },

                { "Toggle Pvp Gold", "PvP gold farming was turned {0}." },
                { "Toggle Pve Gold", "PvE gold farming was turned {0}." },
                { "Toggle Safe Trade", "Pvp and attacks in trading areas was turned {0}." },
                { "Toggle Grand Exchange", "Player access to the Grand Exchange was turned {0}" },
                { "Toggle Deflation", "Price deflation over time was turned {0}" },

                { "Trade Defense", "You cannot attack people in a designated trade area, scoundrel!" },

                { "Default Itemlist", "{0}: {1}." },

                { "Chat Title", "[FF0000]Grand Exchange[FFFFFF] : " },

                { "Store Closed", "The Grand Exchange is closed for business. Please try again later." },
                { "Shop Closed", "This shop doesn't appear to be open." },

                { "Sellpercentage Set", "The Sell percentage has been set to {0}%." },
                { "Inflation Set", "The inflation percentage has been set to {0}%." },
                { "Max Deflation Set", "The max deflation percentage has been set to {0}%." },

                { "Wallet Own", "You have [00FF00]{0}[FFFF00] gold[FFFFFF]."},
                { "Wallet Other", "{0} has {1} gold."},

                { "Gold Available", "[FF0000]Gold Available[FFFFFF] : [00FF00]{0}." },
                { "Gold Maxed", "You cannot gain any more gold than you now have. Congratulations. You are the richest player. Goodbye." },
                { "Gold Give", "Giving {0} gold to {1}." },
                { "Gold Remove", "Removing {0} gold from {1}." },
                { "Gold Remove All", "All players' gold has been removed!" },
                { "Gold Gained", "You have gained {0} gold." },
                { "Gold Lost", "You have lost {0} gold." },
                { "Gold Set", "You have set the gold amount for {0} to {1}." },
                { "Gold Collected", "[00FF00]{0}[FFFF00] gold[FFFFFF] collected." },
                { "Gold Kill", "[FF00FF]{0}[FFFFFF] had no gold for you to steal." },
                { "Gold Kill Global", "[FF00FF]{0}[FFFFFF] has stolen [00FF00]{1}[FFFF00] gold [FFFFFF] from the dead body of [00FF00]{2}[FFFFFF]!" },
                { "Gold Guild", "There is no honor (or more importantly, gold) in killing a member of your own guild!" },
                { "Gold Send Steal", "Don't try to steal gold from others!" },
                { "Gold Send Not Enough", "You can't send more gold then you have!" },
                { "Gold Send", "Sending {0} gold to {1}." },
                { "Gold Received", "You received {0} gold from {1}." },

                { "Popup Title", "Grand Exchange" },

                { "Store Item Data", "[888800]{0}[FFFFFF]; Buy: {1}{2}[FFFF00]g  [FFFFFF]Sell: {3}{4}[FFFF00]g" },
                { "Shop Item Data", "[00FFFF]{0} [FFFFFF]( [FF0000]{1}[FFFFFF] );  Price: {2}{3}[FFFF00]g" },

                { "Shop Empty", "The shop is currently empty." },
                { "No Store", "You cannot trade outside of the designated trade area."},
                { "No Shop", "There is no shop here."},
                { "No Shop Own", "You need to be in your shop to do this."},

                { "Store Wipe", "The Grand Exhange has been emptied out." },
                { "Store Reset", "The Grand Exchange has been reset to it's default values." },
                { "Store Price Reset", "The prices of the items in the Grand Exchange have been reset to their default values." },

                { "Store Buy Item", "What [00FF00]item [FFFFFF]would you like to buy on the [00FFFF]Grand Exchange[FFFFFF]?"},
                { "Store Buy No Item", "I'm afraid that item is currently not for sale."},
                { "Store Buy Amount", "Of course!\n [00FF00]{0}[FFFFFF] is currently selling for [00FFFF]{1}[FFFF00]g[FFFFFF] per item.\n How much would you like to buy?"},
                { "Store Buy Amount Wrong", "I'm afraid we cannot fulfill an order of that size."},
                { "Store Buy Confirm", "Very good!\n [00FFFF]{0} [00FF00]{1}[FFFFFF] will cost you a total of [FF0000]{2} [FFFF00]gold.[FFFFFF]\n Do you want to complete the purchase?"},
                { "Store Buy No Gold", "It looks like you don't have enough gold for this transaction."},
                { "Store Buy No Inventory Space", "I'm afraid you don't have enough inventory slots free. Come back when you have freed up some space."},
                { "Store Buy Complete", "{0} {1} has been added to your inventory and your wallet has been debited the appropriate amount."},
                { "Store Buy Finish", "Congratulations on your purchase. Please come again!"},

                { "Store Sell Item", "What [00FF00]item [FFFFFF]would you like to sell on the [00FFFF]Grand Exchange[FFFFFF]?"},
                { "Store Sell No Item", "Sorry, we currently can't take that item from you."},
                { "Store Sell Amount", "Hmmm!\n I believe that [00FF00]{0}[FFFFFF] is currently being purchased for [00FFFF]{1}[FFFF00]g[FFFFFF] per item.\n How much did you want to sell?"},
                { "Store Sell Amount Wrong", "I'm afraid we cannot fulfill an order of that size."},
                { "Store Sell Confirm", "I suppose I can do that.\n [00FFFF]{0} [00FF00]{1}[FFFFFF] will give you a total of [FF0000]{2} [FFFF00]gold.[FFFFFF]\n Do you want to complete the sale?"},
                { "Store Sell No Resources", "It looks like you don't have the goods! What are you trying to pull here?"},
                { "Store Sell Complete", "{0} {1} has been removed from your inventory and your wallet has been credited for the sale."},
                { "Store Sell Finish", "Thanks for your custom, friend! Please come again!"},

                { "Shop Buy Item", "What [00FF00]item [FFFFFF]would you like to buy at this shop?"},
                { "Shop Buy No Item", "I'm afraid that item is currently not for sale."},
                { "Shop Buy Amount", "Yes, we have that!\n[00FF00]{0}[FFFFFF] is currently selling for [00FFFF]{1}[FFFF00]g[FFFFFF] per item.\n The maximum amount we have available is [00FF00]{2}[FFFFFF].\n How much would you like to buy?"},
                { "Shop Buy Amount Wrong", "I'm afraid we cannot fulfill an order of that size."},
                { "Shop Buy Confirm", "Very good!\n [00FFFF]{0} [00FF00]{1}[FFFFFF] will cost you a total of [FF0000]{2} [FFFF00]gold.[FFFFFF]\n Do you want to complete the purchase?"},
                { "Shop Buy No Gold", "It looks like you don't have enough gold for this transaction."},
                { "Shop Buy No Inventory Space", "I'm afraid you don't have enough inventory slots free. Come back when you have freed up some space."},
                { "Shop Buy Complete", "{0} {1} has been added to your inventory and your wallet has been debited the appropriate amount."},
                { "Shop Buy Finish", "Congratulations on your purchase. Please come again!"},

                { "Store Mark Exists", "You have already marked two locations. Please use /ge.removemarks to start again."},
                { "Store Mark First", "Added the first corner position for the Grand Exchange."},
                { "Store Mark Second", "Added the second and final position for the Grand Exchange."},
                { "Store Mark Removed", "All marks for the Grand Exchange have been removed."},

                { "Shop Mark Occupied", "There already exists a shop in this area."},
                { "Shop Mark Limit", "This area is too big for your shop. It can only be a maximum size of 13x13 blocks."},
                { "Shop Mark Exists", "You have already marked two locations. Please use /ge.removemarks to start again."},
                { "Shop Mark First", "Added the first corner position for your shop. Now you will need to add the OPPOSITE corner for your shop as well."},
                { "Shop Mark Second", "Added the second and final corner position for your shop."},
                { "Shop Mark Removed", "You have removed all of your shop markers. Your shop is not accessible until you place new markers using /addshopmarker."},

                { "Position Mark", "Position has been marked at [00FF00]{0}[FFFFFF], [00FF00]{1}."},

                { "Store Item Exists", "{0} is already in the Grand Exchange."},
                { "Store Item Added", "{0} has been added to the Grand Exchange."},
                { "Store Item Removed", "{0} has been removed from the Grand Exchange."},
                { "Store Item Price Changed", "Changing price of {0} to {1}." },
                { "Store No Item", "{0} does not appear to be in the Grand Exchange."},

                { "Shop Item Added", "{0} has been added to your shop."},
                { "Shop Item Removed", "{0} has been removed from your shop."},
                { "Shop Item Max", "You have reached the max amount you can store of this item."},
                { "Shop Prices updated", "You have updated your shop prices." },
                { "Shop No Item", "{0} does not appear to be in the store." },
                { "Shop No Item Text", "{0}  does not appear to be a recognised item. Did you spell it correct?"},
                { "Shop No Resources", "You don't appear to have that much of the resource in the shop."},
                { "Shop No Inventory Resources", "You don't appear to have that much of the resource in your inventory."},
                { "Shop No Inventory Space", "You don't have enough space in your inventory. Please make sure you have at least {0} free slots." },
                { "Shop Full", "You cannot stock any more items in your shop."},

                { "Item Non-Existing", "{0} does not appear in our item database."}
            }, this);

            //GetMessage("Chat Title", player) + ;
            //GetMessage("Popup Title", player);
            //GetMessage("No Permission", player);
            //GetMessage("Invalid Args", player);
            //GetMessage("Invalid Amount", player);
            //GetMessage("Invalid Player", player);
            //GetMessage("", player);
            //string.Format(GetMessage("", player), )
        }

        #endregion

        #region Commands

        [ChatCommand("gehelp")]
        private void SendPlayerHelpText(Player player, string cmd)
        {
            SendHelpText(player);
        }

        [ChatCommand("wallet")]
        private void CheckOwnGold(Player player, string cmd)
        {
            CheckWallet(player);
        }

        [ChatCommand("topgold")]
        private void ShowTopPlayerGold(Player player, string cmd, string[] input)
        {
            CheckPlayerExists(player);

            if (input.Length > 0)
            {
                if (input[0].ToLower() != "all") return;
                var topPlayers = new Dictionary<ulong, PlayerData>(_PlayerData);
                var topListMax = 10;
                if (topPlayers.Keys.Count < 10) topListMax = topPlayers.Keys.Count;
                for (var i = 0; i < topListMax; i++)
                {
                    var topGoldAmount = 0L;
                    var target = new KeyValuePair<ulong, PlayerData>();
                    foreach (var data in topPlayers)
                    {
                        if (data.Value.Gold < topGoldAmount) continue;
                        target = data;
                        topGoldAmount = data.Value.Gold;
                    }
                    PrintToChat(player, $"{i + 1}. {target.Value.Name} : {target.Value.Gold} gold");
                    topPlayers.Remove(target.Key);
                }
            }
            else
            {
                var onlinePlayers = Server.ClientPlayers;
                var topList = onlinePlayers.Count;

                for (var i = 0; i < topList; i++)
                {
                    var topGoldAmount = 0L;
                    Player topPlayer = null;

                    foreach (var oPlayer in onlinePlayers)
                    {
                        CheckPlayerExists(oPlayer);
                        if (_PlayerData[oPlayer.Id].Gold < topGoldAmount) continue;
                        topGoldAmount = _PlayerData[oPlayer.Id].Gold;
                        topPlayer = oPlayer;
                    }

                    if (topPlayer == null) continue;

                    PrintToChat(player, $"{i + 1}. {topPlayer.DisplayName} : {topGoldAmount} gold.");
                    onlinePlayers.Remove(topPlayer);
                }
            }
        }

        [ChatCommand("store")]
        private void ViewTExchangeStore(Player player, string cmd)
        {
            ShowExchangeStore(player);
        }

        [ChatCommand("buy")]
        private void BuyItem(Player player, string cmd)
        {
            BuyItemExchange(player);
        }

        [ChatCommand("sell")]
        private void SellItem(Player player, string cmd)
        {
            SellItemExchange(player);
        }

        [ChatCommand("sendgold")]
        private void SendCredits(Player player, string cmd, string[] input)
        {
            CheckPlayerExists(player);

            player.SendMessage(" ");

            if (input.Length < 2) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }
            player.SendMessage("Checked input format.");

            int amount;
            if (!int.TryParse(input[0], out amount)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }
            player.SendMessage("Checked amount is valid.");
            
            if (amount < 1) { PrintToChat(player, GetMessage("Gold Send Steal", player)); return; }
            player.SendMessage($"Checked amount is not below zero ({amount})");

            if (_PlayerData[player.Id].Gold < amount) { PrintToChat(player, GetMessage("Gold Send Not Enough", player)); return; }
            player.SendMessage($"Checked player has enough gold ({_PlayerData[player.Id]}).");
            
            string playerName = input.Skip(1).JoinToString(" ");

            Player target = Server.GetPlayerByName(playerName);

            if (target == null) { PrintToChat(player, GetMessage("Invalid Player", player)); return; }
            player.SendMessage("Checked target is online.");

            CheckPlayerExists(target);

            PrintToChat(player, string.Format(GetMessage("Gold Send", player), amount, target.DisplayName));
            PrintToChat(target, string.Format(GetMessage("Gold Received", player), amount, player.DisplayName));

            GiveGold(target, amount);
            player.SendMessage($"Gave {amount} gold to {target.Name}.");
            RemoveGold(player, amount);
            player.SendMessage($"Removed {amount} gold from {player.Name}.");

            SaveTradeData();
        }

        [ChatCommand("ge.addmark")]
        private void AddExchangeMark(Player player, string cmd, string[] input)
        {
            AddExchangeMark(player);
        }

        [ChatCommand("ge.removemarks")]
        private void RemoveTheExchangeMarks(Player player, string cmd, string[] input)
        {
            RemoveExchangeMarks(player, input);
        }

        [ChatCommand("ge.additem")]
        private void AddAnExchangeItem(Player player, string cmd, string[] input)
        {
            AddExchangeItem(player, input);
        }

        [ChatCommand("ge.removeitem")]
        private void RemoveANExchangeItem(Player player, string cmd, string[] input)
        {
            RemoveExchangeItem(player, input);
        }

        [ChatCommand("ge.removeallitems")]
        private void RemoveAllTheExchangeItems(Player player, string cmd, string[] input)
        {
            RemoveAllExchangeItems(player, input);
        }

        [ChatCommand("ge.setprice")]
        private void SetTheExchangeItemPrice(Player player, string cmd, string[] input)
        {
            SetExchangeItemPrice(player, input);
        }

        [ChatCommand("ge.defaultitems")]
        private void ShowTheDefaultTradeList(Player player, string cmd, string[] input)
        {
            ShowDefaultTradeList(player, input);
        }

        [ChatCommand("ge.restoredefaultitems")]
        private void RestoreDefaultGEItems(Player player, string cmd)
        {
            RestoreDefaultItems(player);
        }

        [ChatCommand("ge.restoredefaultprices")]
        private void RestoreDefaultGEPrices(Player player, string cmd)
        {
            RestoreDefaultPrices(player);
        }

        [ChatCommand("ge.restoreshops")]
        private void RestoreThePlayerShops(Player player, string cmd)
        {
            RestoreShops(player);
        }

        [ChatCommand("ge.pvp")]
        private void TogglePvpGoldGain(Player player, string cmd)
        {
            TogglePvpGold(player);
        }

        [ChatCommand("ge.pve")]
        private void TogglePveGoldGain(Player player, string cmd)
        {
            TogglePveGold(player);
        }

        [ChatCommand("ge.safetrade")]
        private void ToggleSafeTradingProtection(Player player, string cmd)
        {
            ToggleSafeTrading(player);
        }

        [ChatCommand("ge.toggle")]
        private void TogglePlayerAccessGE(Player player, string cmd)
        {
            ToggleAccessGE(player);
        }

        [ChatCommand("ge.deflation")]
        private void ToggleDeflationModifier(Player player, string cmd)
        {
            ToggleDeflation(player);
        }

        [ChatCommand("ge.setinflation")]
        private void SetInflation(Player player, string cmd, string[] input)
        {
            SetInflationModifier(player, input);
        }

        [ChatCommand("ge.setmaxdeflation")]
        private void SetMaxDeflation(Player player, string cmd, string[] input)
        {
            SetMaxDeflationModifier(player, input);
        }

        [ChatCommand("ge.setsellpercentage")]
        private void SetTheSellPercentage(Player player, string cmd, string[] input)
        {
            SetSellPercentage(player, input);
        }

        [ChatCommand("loc")]
        private void GetPlayerLocation(Player player, string cmd, string[] input)
        {
            GetLocation(player, input);
        }

        [ChatCommand("setgold")]
        private void SetThePlayerGold(Player player, string cmd, string[] input)
        {
            SetPlayerGold(player, input);
        }

        [ChatCommand("resetgold")]
        private void RemoveAllTheGold(Player player, string cmd)
        {
            RemoveAllGold(player);
        }

        [ChatCommand("givegold")]
        private void GiveAPlayerGold(Player player, string cmd, string[] input)
        {
            GivePlayerGold(player, input);
        }

        [ChatCommand("removegold")]
        private void AdminRemoveCredits(Player player, string cmd, string[] input)
        {
            RemovePlayerGold(player, input);
        }

        [ChatCommand("checkgold")]
        private void AdminCheckPlayerCredits(Player player, string cmd, string[] input)
        {
            CheckPlayerGold(player, input);
        }

        [ChatCommand("shop")]
        private void ViewThisShop(Player player, string cmd)
        {
            ViewShop(player);
        }

        [ChatCommand("myshop")]
        private void ViewMyShopItems(Player player, string cmd)
        {
            ViewMyShop(player);
        }

        [ChatCommand("addshopmark")]
        private void AddAShopMarker(Player player, string cmd)
        {
            AddShopMarker(player);
        }

        [ChatCommand("removeshopmarks")]
        private void RemoveAShopMarker(Player player, string cmd)
        {
            RemoveShopMarker(player);
        }

        [ChatCommand("addshopitem")]
        private void AddAShopItem(Player player, string cmd, string[] input)
        {
            AddShopItem(player, input);
        }

        [ChatCommand("removeshopitem")]
        private void RemoveAShopItem(Player player, string cmd, string[] input)
        {
            RemoveShopItem(player, input);
        }

        [ChatCommand("setitemprice")]
        private void SetTheShopItemPrice(Player player, string cmd, string[] input)
        {
            SetShopItemPrice(player, input);
        }

        [ChatCommand("setshopname")]
        private void SetPlayerShopName(Player player, string cmd, string[] input)
        {
            SetShopName(player, input);
        }

        #endregion

        #region Command Functions

        private void ShowDefaultTradeList(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            foreach (KeyValuePair<string, int> item in _DefaultTradeList) PrintToChat(player, string.Format(GetMessage("Default Itemlist", player), item.Key, item.Value)); 
        }

        private void ShowExchangeStore(Player player)
        {
            CheckPlayerExists(player);

            if (!CanUseGE && !player.HasPermission("GrandExchange.Show")) { PrintToChat(player, GetMessage("Store Closed", player)); return; }

            if (_GEData.TradeList.Count == 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Closed", player)); return; }

            string buyIcon = "";
            string sellIcon = "";
            string itemText = "";
            int itemsPerPage = 25;
            bool singlePage = false;
            if (itemsPerPage > _GEData.TradeList.Count)
            {
                singlePage = true;
                itemsPerPage = _GEData.TradeList.Count;
            }

            for (int i = 0; i < itemsPerPage; i++)
            {
                buyIcon = "[008888]";
                sellIcon = "[008888]";
                KeyValuePair<string, TradeData> resource = _GEData.TradeList.GetAt(i);
                int originalSellPrice = (int)(resource.Value.OriginalPrice * (SellPercentage / 100));

                if (resource.Value.BuyPrice >= resource.Value.OriginalPrice) buyIcon = "[00FF00]";
                if (resource.Value.BuyPrice > resource.Value.OriginalPrice + (resource.Value.OriginalPrice * 0.1)) buyIcon = "[888800]";
                if (resource.Value.BuyPrice > resource.Value.OriginalPrice + (resource.Value.OriginalPrice * 0.2)) buyIcon = "[FF0000]";
                if (resource.Value.SellPrice <= originalSellPrice) sellIcon = "[00FF00]";
                if (resource.Value.SellPrice < originalSellPrice - (originalSellPrice * 0.1)) sellIcon = "[888800]";
                if (resource.Value.SellPrice < originalSellPrice - (originalSellPrice * 0.2)) sellIcon = "[FF0000]";

                itemText += string.Format(GetMessage("Store Item Data", player), resource.Key, buyIcon, resource.Value.BuyPrice, sellIcon, resource.Value.SellPrice);
                itemText += "\n";
            }

            itemText += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);

            if (singlePage) { player.ShowPopup(GetMessage("Popup Title", player), itemText, "Exit"); return; }

            player.ShowConfirmPopup(GetMessage("Popup Title", player), itemText, "Next Page", "Exit", (selection, dialogue, data) => ContinueWithTradeList(player, selection, dialogue, data, itemsPerPage, itemsPerPage));
        }

        private void ViewShop(Player player)
        {
            CheckPlayerExists(player);

            ShowShopList(player, 1);
        }

        private void ViewMyShop(Player player)
        {
            CheckPlayerExists(player);

            ShowShopList(player, 2);
        }

        private void BuyItemExchange(Player player)
        {
            CheckPlayerExists(player);

            if (!CanUseGE && !player.HasPermission("GrandExchange.Show")) { PrintToChat(player, GetMessage("Store Closed", player)); return; }

            if (!_GEData.IsInTradeArea(player.Entity.Position))
            {
                PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Store", player));
                return;
            }

            player.ShowInputPopup(GetMessage("Popup Title", player), GetMessage("Store Buy Item", player), "", "Submit", "Cancel", (options, dialogue1, data) => SelectExchangeItem(player, options, dialogue1, 1));
        }

        private void SellItemExchange(Player player)
        {
            CheckPlayerExists(player);

            if (!CanUseGE && !player.HasPermission("GrandExchange.Show")) { PrintToChat(player, GetMessage("Store Closed", player)); return; }

            if (!_GEData.IsInTradeArea(player.Entity.Position)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Store", player)); return; }

            player.ShowInputPopup(GetMessage("Popup Title", player), GetMessage("Store Sell Item", player), "", "Submit", "Cancel", (options, dialogue1, data) => SelectExchangeItem(player, options, dialogue1, 2));
        }

        private void CheckWallet(Player player)
        {
            CheckPlayerExists(player);

            PrintToChat(player, string.Format(GetMessage("Wallet Own", player), _PlayerData[player.Id].Gold));
        }

        private void CheckPlayerGold(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            string playerName = input.JoinToString(" ");
            Player target = Server.GetPlayerByName(playerName);
            if (target == null)
            {
                foreach (KeyValuePair<ulong, PlayerData> data in _PlayerData)
                {
                    if (data.Value.Name.ToLower().Contains(playerName.ToLower()))
                    {
                        PrintToChat(player, string.Format(GetMessage("Wallet Other", player), data.Value.Name, _PlayerData[data.Key].Gold));
                        return;
                    }
                }
                PrintToChat(player, GetMessage("Invalid Player", player));
                return;
            }
            else
            {
                CheckPlayerExists(target);
                PrintToChat(player, string.Format(GetMessage("Wallet Other", player), target.DisplayName, _PlayerData[target.Id].Gold));
            }
        }

        private void GivePlayerGold(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (input.Length < 1) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            int amount = 0;
            if (!int.TryParse(input[0], out amount)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }

            string playerName;
            ulong playerId = 0;

            if (input.Length > 1)
            {
                playerName = input.JoinToString(" ");
                playerName = playerName.Substring(playerName.IndexOf(' ') + 1);

                foreach (KeyValuePair<ulong, PlayerData> data in _PlayerData)
                {
                    if (data.Value.Name.ToLower().Contains(playerName.ToLower()))
                    {
                        playerName = data.Value.Name;
                        playerId = data.Key;
                    }
                }
                
                if (playerId == 0) { PrintToChat(player, GetMessage("Invalid Player", player)); return; }
            }
            else
            {
                playerName = player.DisplayName;
                playerId = player.Id;
            }

            PrintToChat(player, string.Format(GetMessage("Gold Give", player), amount, playerName));

            if (Server.GetPlayerByName(playerName) != null) PrintToChat(Server.GetPlayerByName(playerName), string.Format(GetMessage("Gold Gained", Server.GetPlayerByName(playerName)), amount));

            GiveGold(playerId, amount);

            SaveTradeData();
        }

        private void RemovePlayerGold(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (input.Length < 1) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            int amount = 0;
            if (!int.TryParse(input[0], out amount)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }

            string playerName = "";
            ulong playerId = 0;

            if (input.Length > 1)
            {
                playerName = input.JoinToString(" ");
                playerName = playerName.Substring(playerName.IndexOf(' ') + 1);

                foreach (KeyValuePair<ulong, PlayerData> data in _PlayerData)
                {
                    if (data.Value.Name.ToLower().Contains(playerName.ToLower()))
                    {
                        playerName = data.Value.Name;
                        playerId = data.Key;
                    }
                }

                if (playerId == 0) { PrintToChat(player, GetMessage("Invalid Player", player)); return; }
            }
            else
            {
                playerName = player.DisplayName;
                playerId = player.Id;
            }

            PrintToChat(player, string.Format(GetMessage("Gold Remove", player), amount, playerName));

            if (Server.GetPlayerByName(playerName) != null) PrintToChat(Server.GetPlayerByName(playerName), string.Format(GetMessage("Gold Lost", Server.GetPlayerByName(playerName)), amount));

            RemoveGold(playerId, amount);

            SaveTradeData();
        }

        private void AddExchangeMark(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (_GEData.HasPosition() == 2) { PrintToChat(player, GetMessage("Store Mark Exists", player)); return; }

            if (_GEData.HasPosition() == 0)
            {
                _GEData.AddPosition(player.Entity.Position, 1);
                PrintToChat(player, GetMessage("Store Mark First", player));
            }
            else
            {
                _GEData.AddPosition(player.Entity.Position, 2);
                PrintToChat(player, GetMessage("Store Mark Second", player));
            }

            PrintToChat(player, string.Format(GetMessage("Position Mark", player), player.Entity.Position.x, player.Entity.Position.z));
            SaveTradeData();
        }

        private void AddShopMarker(Player player)
        {
            CheckPlayerExists(player);

            if (_PlayerData[player.Id].Shop.HasPosition() == 2) { PrintToChat(player, GetMessage("Shop Mark Exists", player)); return; }

            if (GetShopOwner(player.Entity.Position) != 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Mark Occupied", player)); return; }

            if (_PlayerData[player.Id].Shop.HasPosition() != 1)
            {
                _PlayerData[player.Id].Shop.AddPosition(player.Entity.Position, 1);
                PrintToChat(player, GetMessage("Shop Mark First", player));
            }
            else
            {
                if (BlocksAreTooFarApart(_PlayerData[player.Id].Shop.GetPosition(1), player.Entity.Position)) { PrintToChat(player, GetMessage("Shop Mark Limit", player)); return; }

                _PlayerData[player.Id].Shop.AddPosition(player.Entity.Position, 2);
                PrintToChat(player, GetMessage("Shop Mark Second", player));
            }

            SaveTradeData();
        }

        private void AddExchangeItem(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (input.Length < 2 || input.Length > 2) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            string resource = Capitalise(input[0]);

            if (_GEData.TradeList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Store Item Exists", player), resource)); return; }
            var stackLimit = GetStackLimit(resource);
            if (stackLimit < 1) { PrintToChat(player, string.Format(GetMessage("Item Non-Existing", player), resource)); return; }

            int price;
            if (!int.TryParse(input[1], out price)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }
            if (price < 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            _GEData.TradeList.Add(resource, new TradeData(price));

            PrintToChat(player, string.Format(GetMessage("Store Item Added", player), resource));

            SaveTradeData();
        }

        private void AddShopItem(Player player, string[] input)
        {
            CheckPlayerExists(player);
            
            if (_PlayerData[player.Id].Shop.HasPosition() != 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Shop Own", player)); return; }

            if (input.Length < 2 || input.Length > 3) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Args", player)); return; }

            string resource = Capitalise(input[0]);
            var stackLimit = GetStackLimit(resource);
            if (stackLimit < 1) { PrintToChat(player, string.Format(GetMessage("Item Non-Existing", player), resource)); return; }

            int amount;
            if (!int.TryParse(input[1], out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            var price = 0;
            if (input.Length == 3) if (!int.TryParse(input[2], out price)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }
            if (price < 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            if (!CanRemoveResource(player, resource, amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop No Inventory Resources", player)); return; }

            switch (_PlayerData[player.Id].Shop.AddItem(resource, price, amount))
            {
                case 0:
                    PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Full", player));
                    return;
                case 1:
                    PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Item Max", player));
                    return;
                case 2:
                    break;
            }

            PrintToChat(player, GetMessage("Chat Title", player) + string.Format(GetMessage("Shop Item Added", player), resource));

            RemoveItemsFromInventory(player.GetInventory().Contents, resource, stackLimit, amount);

            SaveTradeData();
        }

        private void RemoveExchangeMarks(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (_GEData.HasPosition() == 0) { PrintToChat(player, GetMessage("No Marks", player)); return; }

            _GEData.RemoveGEMarks();
            PrintToChat(player, GetMessage("Store Mark Removed", player));

            SaveTradeData();
        }

        private void RemoveShopMarker(Player player)
        {
            CheckPlayerExists(player);

            if (_PlayerData[player.Id].Shop.HasPosition() == 0) { PrintToChat(player, GetMessage("No Marks", player)); return; }

            _PlayerData[player.Id].Shop.RemoveShop();
            PrintToChat(player, GetMessage("Shop Mark Removed", player));

            SaveTradeData();
        }

        private void RemoveAllGold(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            foreach (KeyValuePair<ulong, PlayerData> data in _PlayerData)
            {
                data.Value.Gold = 0;
            }

            PrintToChat(player, GetMessage("Gold Remove All", player));

            SaveTradeData();
        }

        private void RemoveExchangeItem(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            var resource = Capitalise(input.JoinToString(" "));

            if (!_GEData.TradeList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Store No Item", player), resource)); return; }

            _GEData.TradeList.Remove(resource);
            PrintToChat(player, string.Format(GetMessage("Store Item Removed", player), resource));

            SaveTradeData();
        }

        private void RemoveShopItem(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (input.Length > 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Args", player)); return; }
            
            if (_PlayerData[player.Id].Shop.HasPosition() != 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Shop Own", player)); return; }

            var resource = Capitalise(input[0]);

            if (!_PlayerData[player.Id].Shop.ItemList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Shop No Item Text", player), resource)); return; }

            int amount;

            if (input.Length < 2) amount = _PlayerData[player.Id].Shop.ItemList[resource].Amount;
            else if (!int.TryParse(input[1], out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            if (_PlayerData[player.Id].Shop.ItemList[resource].Amount < amount) { PrintToChat(player, GetMessage("Shop No Resources", player)); return; }

            var inventory = player.GetInventory().Contents;
            var stackLimit = GetStackLimit(resource);
            var stacks = (int)Math.Ceiling((double)amount / stackLimit);

            if (inventory.FreeSlotCount < stacks) { PrintToChat(player, string.Format(GetMessage("Shop No Inventory Space", player), stacks)); return; }

            AddItemsToInventory(inventory, resource, stackLimit, amount);

            _PlayerData[player.Id].Shop.ItemList.Remove(resource);

            PrintToChat(player, GetMessage("Chat Title", player) + string.Format(GetMessage("Shop Item Removed", player), resource));
            SaveTradeData();
        }

        private void RemoveAllExchangeItems(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            _GEData.TradeList = new SortedDictionary<string, TradeData>();

            PrintToChat(player, GetMessage("Store Wipe", player));

            SaveTradeData();
        }

        private void GetLocation(Player player, string[] args)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            PrintToChat(player, string.Format(GetMessage("Location", player), player.Entity.Position.x, player.Entity.Position.y, player.Entity.Position.z));
        }

        private void RestoreDefaultItems(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            _GEData.TradeList = new SortedDictionary<string, TradeData>();
            foreach (KeyValuePair<string, int> item in _DefaultTradeList) _GEData.AddItem(item.Key, item.Value);

            PrintToChat(player, GetMessage("Store Reset", player));

            SaveTradeData();
        }

        private void RestoreDefaultPrices(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            ForcePriceAdjustment();

            PrintToChat(player, GetMessage("Store Price Reset", player));

            SaveTradeData();
        }

        private void RestoreShops(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            foreach (KeyValuePair<ulong, PlayerData> data in _PlayerData)
            {
                data.Value.Shop.Name = "Local Store";
                data.Value.Shop.RemoveShop();
            }

            SaveTradeData();
        }

        private void SetPlayerGold(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (input.Length < 1) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            int amount = 0;
            if (!int.TryParse(input[0], out amount)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }
            
            string playerName = "";
            ulong playerId = 0;

            if (input.Length > 1)
            {
                playerName = input.JoinToString(" ");
                playerName = playerName.Substring(playerName.IndexOf(' ') + 1);

                foreach (KeyValuePair<ulong, PlayerData> data in _PlayerData)
                {
                    if (data.Value.Name.ToLower().Contains(playerName.ToLower()))
                    {
                        playerName = data.Value.Name;
                        playerId = data.Key;
                    }
                }

                if (playerId == 0) { PrintToChat(player, GetMessage("Invalid Player", player)); return; }
            }
            else
            {
                playerName = player.DisplayName;
                playerId = player.Id;
            }

            _PlayerData[playerId].Gold = amount;
            PrintToChat(player, string.Format(GetMessage("Gold Set", player), playerName, amount));

            SaveTradeData();
        }

        private void SetInflationModifier(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (!double.TryParse(input[0], out Inflation)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }

            PrintToChat(player, string.Format(GetMessage("Inflation Set", player), Inflation));

            SaveConfigData();
        }

        private void SetMaxDeflationModifier(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (!double.TryParse(input[0], out MaxDeflation)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }

            PrintToChat(player, string.Format(GetMessage("Max Deflation Set", player), MaxDeflation));

            SaveConfigData();
        }

        private void SetSellPercentage(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            double percentage = 0;
            if (double.TryParse(input[0], out percentage) == false) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }

            SellPercentage = percentage;

            ForcePriceAdjustment();
            PrintToChat(player, string.Format(GetMessage("Sellpercentage Set", player), percentage));

            SaveConfigData();
        }

        private void SetExchangeItemPrice(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Itemlist")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (input.Length < 2 || input.Length > 2) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            string resource = Capitalise(input[0]);

            if (!_GEData.TradeList.ContainsKey(resource)) { PrintToChat(player, GetMessage("Item Non-Existing", player)); return; }

            int price = 0;
            if (!int.TryParse(input[1], out price)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }
            if (price < 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            _GEData.TradeList[resource].SetPrice(price);

            PrintToChat(player, string.Format(GetMessage("Store Item Price Changed", player), resource, price));

            SaveTradeData();
        }

        private void SetShopItemPrice(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (_PlayerData[player.Id].Shop.HasPosition() != 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Shop Own", player)); return; }

            if (input.Length != 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Args", player)); return; }

            string resource = Capitalise(input[0]);

            if (!_PlayerData[player.Id].Shop.ItemList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Shop No Item", player), resource)); return; }

            int amount = 0;
            if (!int.TryParse(input[1], out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }
            if (amount < 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            _PlayerData[player.Id].Shop.ItemList[resource].Price = amount;

            SaveTradeData();
            PrintToChat(player, GetMessage("Shop Prices updated", player));
        }

        private void SetShopName(Player player, string[] input)
        {
            CheckPlayerExists(player);

            if (_PlayerData[player.Id].Shop.HasPosition() != 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Shop Own", player)); return; }

            if (input.Length < 1) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Args", player)); return; }

            _PlayerData[player.Id].Shop.Name = input.JoinToString(" ");
        }

        private void TogglePveGold(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (PveGold) { PveGold = false; PrintToChat(player, string.Format(GetMessage("Toggle Pve Gold", player), "[FF0000]OFF")); }
            else { PveGold = true; PrintToChat(player, string.Format(GetMessage("Toggle Pve Gold", player), "[00FF00]ON")); }

            SaveConfigData();
        }

        private void TogglePvpGold(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (PvpGold) { PvpGold = false; PrintToChat(player, string.Format(GetMessage("Toggle Pvp Gold", player), "[FF0000]OFF")); }
            else { PvpGold = true; PrintToChat(player, string.Format(GetMessage("Toggle Pvp Gold", player), "[00FF00]ON")); }

            SaveConfigData();
        }

        private void ToggleSafeTrading(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (SafeTrade) { SafeTrade = false; PrintToChat(player, string.Format(GetMessage("Toggle Safe Trade", player), "[00FF00]ON")); }
            else { SafeTrade = true; PrintToChat(player, string.Format(GetMessage("Toggle Safe Trade", player), "[FF0000]OFF")); }

            SaveConfigData();
        }

        private void ToggleAccessGE(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (CanUseGE) { CanUseGE = false; PrintToChat(player, string.Format(GetMessage("Toggle Grand Exchange", player), "[FF0000]OFF")); }
            else { CanUseGE = true; PrintToChat(player, string.Format(GetMessage("Toggle Grand Exchange", player), "[00FF00]ON")); }

            SaveConfigData();
        }

        private void ToggleDeflation(Player player)
        {
            CheckPlayerExists(player);

            if (!player.HasPermission("GrandExchange.Modify.Settings")) { PrintToChat(player, GetMessage("No Permission", player)); return; }

            if (UseDeflation) { UseDeflation = false; PrintToChat(player, string.Format(GetMessage("Toggle Deflation", player), "[FF0000]OFF")); }
            else { UseDeflation = true; PrintToChat(player, string.Format(GetMessage("Toggle Deflation", player), "[00FF00]ON")); }

            SaveConfigData();
        }

        #endregion

        #region System Functions

        private void ContinueWithTradeList(Player player, Options selection, Dialogue dialogue, object contextData, int itemsPerPage, int currentItemCount)
        {
            if (selection != Options.Yes) return;

            if ((currentItemCount + itemsPerPage) > _GEData.TradeList.Count) itemsPerPage = _GEData.TradeList.Count - currentItemCount;

            string buyIcon = "";
            string sellIcon = "";
            string itemText = "";

            for (int i = currentItemCount; i < itemsPerPage + currentItemCount; i++)
            {
                buyIcon = "[008888]";
                sellIcon = "[008888]";
                KeyValuePair<string, TradeData> resource = _GEData.TradeList.GetAt(i);
                int originalSellPrice = (int)(resource.Value.OriginalPrice * (SellPercentage / 100));

                if (resource.Value.BuyPrice >= resource.Value.OriginalPrice) buyIcon = "[00FF00]";
                if (resource.Value.BuyPrice > resource.Value.OriginalPrice + (resource.Value.OriginalPrice * 0.1)) buyIcon = "[888800]";
                if (resource.Value.BuyPrice > resource.Value.OriginalPrice + (resource.Value.OriginalPrice * 0.2)) buyIcon = "[FF0000]";
                if (resource.Value.SellPrice <= originalSellPrice) sellIcon = "[00FF00]";
                if (resource.Value.SellPrice < originalSellPrice - (originalSellPrice * 0.1)) sellIcon = "[888800]";
                if (resource.Value.SellPrice < originalSellPrice - (originalSellPrice * 0.2)) sellIcon = "[FF0000]";


                itemText += string.Format(GetMessage("Store Item Data", player), resource.Key, buyIcon, resource.Value.BuyPrice, sellIcon, resource.Value.SellPrice);
                itemText += "\n";
            }

            itemText += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);

            currentItemCount = currentItemCount + itemsPerPage;

            if (currentItemCount < _GEData.TradeList.Count)
            {
                player.ShowConfirmPopup(GetMessage("Popup Title", player), itemText, "Next Page", "Exit", (options, dialogue1, data) => ContinueWithTradeList(player, options, dialogue1, data, itemsPerPage, currentItemCount));
            }
            else
            {
                PlayerExtensions.ShowPopup(player, GetMessage("Popup Title", player), itemText, "Yes");
            }
        }

        private void SelectExchangeItem(Player player, Options selection, Dialogue dialogue, int type)
        {
            if (selection == Options.Cancel) return;

            var resource = Capitalise(dialogue.ValueMessage);

            if (!_GEData.TradeList.ContainsKey(resource))
            {
                switch (type)
                {
                    case 1:
                        PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy No Item", player));
                        break;
                    case 2:
                        PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Sell No Item", player));
                        break;
                }
                return;
            }

            if (_GEData.TradeList[resource] == null) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            string message;
            switch (type)
            {
                case 1:
                    message = string.Format(GetMessage("Store Buy Amount", player), resource, _GEData.TradeList[resource].BuyPrice);
                    player.ShowInputPopup(GetMessage("Popup Title", player), message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectExchangeAmount(player, options, dialogue1, resource, 1));
                    break;
                case 2:
                    message = string.Format(GetMessage("Store Sell Amount", player), resource, _GEData.TradeList[resource].SellPrice);
                    player.ShowInputPopup(GetMessage("Popup Title", player), message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectExchangeAmount(player, options, dialogue1, resource, 2));
                    break;
            }
        }

        private void SelectExchangeAmount(Player player, Options selection, Dialogue dialogue, string resource, int type)
        {
            if (selection == Options.Cancel) return;

            string amountText = dialogue.ValueMessage;

            int amount = 0;
            if (!int.TryParse(amountText, out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            if (amount < 1)
            {
                switch (type)
                {
                    case 1:
                        PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy Amount Wrong", player));
                        break;
                    case 2:
                        PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Sell Amount Wrong", player));
                        break;
                }
                return;
            }

            int totalValue = 0;
            string message = "";

            switch (type)
            {
                case 1:
                    totalValue = _GEData.TradeList[resource].GetPrice(amount, 1);
                    message = string.Format(GetMessage("Store Buy Confirm", player), amount, resource, totalValue);
                    message += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);
                    player.ShowConfirmPopup(GetMessage("Popup Title", player), message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerCanAffordThis(player, options, dialogue, resource, totalValue, amount));
                    break;
                case 2:
                    totalValue = _GEData.TradeList[resource].GetPrice(amount, 2);
                    message = string.Format(GetMessage("Store Sell Confirm", player), amount, resource, totalValue);
                    message += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);
                    player.ShowConfirmPopup(GetMessage("Popup Title", player), message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerHasTheResourceToSell(player, options, dialogue, resource, totalValue, amount));
                    break;
            }
        }

        private void CheckIfThePlayerCanAffordThis(Player player, Options selection, Dialogue dialogue, string resource, int totalValue, int amount)
        {
            if (selection != Options.Yes) return;

            if (!CanRemoveGold(player, totalValue)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy No Gold", player)); return; }

            var inventory = player.GetInventory().Contents;
            var stackLimit = GetStackLimit(resource);

            var stacks = (int)Math.Ceiling((double)amount / stackLimit);
            if (inventory.FreeSlotCount < stacks) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy No Inventory Space", player)); return; }

            AddItemsToInventory(inventory, resource, stackLimit, amount);
            RemoveGold(player, totalValue);

            _GEData.TradeList[resource].UpdatePrices(stackLimit, amount, 1);

            PrintToChat(player, GetMessage("Chat Title", player) + string.Format(GetMessage("Store Buy Complete", player), amount, resource));
            PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy Finish", player));

            SaveTradeData();
        }

        private void CheckIfThePlayerHasTheResourceToSell(Player player, Options selection, Dialogue dialogue, string resource, int totalValue, int amount)
        {
            if (selection != Options.Yes) return;

            if (!CanRemoveResource(player, resource, amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Sell No Resources", player)); return; }
            
            var stackLimit = GetStackLimit(resource);
            RemoveItemsFromInventory(player.GetInventory().Contents, resource, stackLimit, amount);

            GiveGold(player, totalValue);

            _GEData.TradeList[resource].UpdatePrices(stackLimit, amount, 2);

            PrintToChat(player, GetMessage("Chat Title", player) + string.Format(GetMessage("Store Sell Complete", player), amount, resource));
            PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Sell Finish", player));

            SaveTradeData();
        }

        private void ShowShopList(Player player, int type)
        {
            ulong shopOwner = type == 1 ? GetShopOwner(player.Entity.Position) : player.Id;

            if (shopOwner == 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Shop", player)); return; }
            
            if (_PlayerData[shopOwner].Shop == null || _PlayerData[shopOwner].Shop.ItemList == null) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Closed", player)); return; }
            
            if (_PlayerData[shopOwner].Shop.ItemList.Count < 1) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage(type == 1? "Shop Closed": "Shop Empty", player)); return; }

            string buyIcon = "[008888]";
            string itemText = "";

            foreach (KeyValuePair<string, ItemData> item in _PlayerData[shopOwner].Shop.ItemList)
            {
                buyIcon = "[00FF00]";

                itemText += string.Format(GetMessage("Shop Item Data", player), item.Key, item.Value.Amount, buyIcon, item.Value.Price);
                itemText += "\n";
            }

            itemText += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);

            player.ShowConfirmPopup(_PlayerData[shopOwner].Shop.Name.IsNullEmptyOrWhite() ? "Local Store" : _PlayerData[shopOwner].Shop.Name, itemText, "Buy", "Exit", (selection, dialogue, data) => BuyItemFromPlayerShop(player, shopOwner, selection));
        }

        private void BuyItemFromPlayerShop(Player player, ulong shopOwner, Options selection)
        {
            if (selection != Options.Yes) return;
            
            player.ShowInputPopup(_PlayerData[shopOwner].Shop.Name.IsNullEmptyOrWhite() ? "Local Store" : _PlayerData[shopOwner].Shop.Name, GetMessage("Shop Buy Item", player), "", "Submit", "Cancel", (options, dialogue1, data) => SelectItemToBeBoughtFromPlayer(player, shopOwner, options, dialogue1));
        }

        private void SelectItemToBeBoughtFromPlayer(Player player, ulong shopOwner, Options selection, Dialogue dialogue)
        {
            if (selection == Options.Cancel) return;

            string resource = Capitalise(dialogue.ValueMessage);

            if (!_PlayerData[shopOwner].Shop.ItemList.ContainsKey(resource)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy No Item", player)); return; }

            string message = string.Format(GetMessage("Shop Buy Amount", player), resource, _PlayerData[shopOwner].Shop.ItemList[resource].Price, _PlayerData[shopOwner].Shop.ItemList[resource].GetAmount());

            message += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);

            player.ShowInputPopup(_PlayerData[shopOwner].Shop.Name.IsNullEmptyOrWhite() ? "Local Store" : _PlayerData[shopOwner].Shop.Name, message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectAmountToBeBoughtFromPlayerStore(player, shopOwner, options, dialogue1, resource));
        }

        private void SelectAmountToBeBoughtFromPlayerStore(Player player, ulong shopOwner, Options selection, Dialogue dialogue, string resource)
        {
            if (selection == Options.Cancel) return;

            var amountText = dialogue.ValueMessage;

            int amount;
            if (!int.TryParse(amountText, out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            if (amount < 1 || amount > _PlayerData[shopOwner].Shop.ItemList[resource].GetAmount()) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy Amount Wrong", player)); return; }

            var totalValue = _PlayerData[shopOwner].Shop.ItemList[resource].GetPrice(amount);

            var message = string.Format(GetMessage("Shop Buy Confirm", player), amount, resource, totalValue);

            message += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);

            player.ShowConfirmPopup(_PlayerData[shopOwner].Shop.Name.IsNullEmptyOrWhite() ? "Local Store" : _PlayerData[shopOwner].Shop.Name, message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerCanAffordThis(player, options, shopOwner, resource, totalValue, amount));
        }

        private void CheckIfThePlayerCanAffordThis(Player player, Options selection, ulong shopOwner, string resource, long totalValue, int amount)
        {
            if (selection != Options.Yes) return;

            if (!CanRemoveGold(player, totalValue)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy No Gold", player)); return; }

            var inventory = player.GetInventory().Contents;
            var stackLimit = GetStackLimit(resource);

            var stacks = (int)Math.Ceiling((double)amount / stackLimit);
            if (inventory.FreeSlotCount < stacks) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy No Inventory Space", player)); return; }

            AddItemsToInventory(inventory, resource, stackLimit, amount);

            RemoveGold(player, totalValue);
            GiveGold(shopOwner, totalValue);
            _PlayerData[shopOwner].Shop.RemoveItem(resource, amount);

            PrintToChat(player, GetMessage("Chat Title", player) + string.Format(GetMessage("Shop Buy Complete", player), amount, resource));
            PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy Finish", player));

            SaveTradeData();
        }

        private bool BlocksAreTooFarApart(Vector3 pos1, Vector3 pos2)
        {
            if (Math.Abs(pos2.x - pos1.x) > 15) return true;
            if (Math.Abs(pos2.z - pos1.z) > 15) return true;
            return false;
        }

        private void CheckPlayerExists(Player player)
        {
            if (!_PlayerData.ContainsKey(player.Id)) _PlayerData.Add(player.Id, new PlayerData(player.DisplayName));
            else
            {
                if (_PlayerData[player.Id].Name.IsNullOrEmpty()) _PlayerData[player.Id].Name = player.Name;
                if (_PlayerData[player.Id].Name != player.Name) _PlayerData[player.Id].Name = player.Name;
            }
            if (_PlayerData[player.Id].Shop == null) _PlayerData[player.Id].Shop = new ShopData();
            SaveTradeData();
        }

        private bool CanRemoveGold(Player player, long amount)
        {
            if (_PlayerData[player.Id].Gold - amount < 0) return false;
            return true;
        }

        private bool CanRemoveResource(Player player, string resource, int amount)
        {
            ItemCollection inventory = player.GetInventory().Contents;

            InvItemBlueprint blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
            InvGameItemStack invGameItemStack = new InvGameItemStack(blueprintForName, amount, null);

            int totalAmount = 0;

            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if (item.Name != resource) continue;
                totalAmount += item.StackAmount;
            }

            if (totalAmount < amount) return false;
            return true;
        }

        private bool IsAnimal(Entity e)
        {
            if (e.Has<MonsterEntity>() || e.Has<CritterEntity>()) return true;
            return false;
        }

        private ulong GetShopOwner(Vector3 position)
        {
            if (_PlayerData.Count < 1) return 0;

            foreach (KeyValuePair<ulong, PlayerData> shop in _PlayerData)
            {
                if (shop.Value.Shop == null) continue;
                if (shop.Value.Shop.IsInShopArea(position)) return shop.Key;
            }

            return 0;
        }

        private void GiveGold(Player player, long amount)
        {
            if (_PlayerData[player.Id].Gold + amount > MaxPossibleGold)
            {
                PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Gold Maxed", player));
                _PlayerData[player.Id].Gold = MaxPossibleGold;
            }
            else _PlayerData[player.Id].Gold += amount;

            SaveTradeData();
        }

        private void GiveGold(ulong playerId, long amount)
        {
            if (_PlayerData[playerId].Gold + amount > MaxPossibleGold) _PlayerData[playerId].Gold = MaxPossibleGold;
            else _PlayerData[playerId].Gold += amount;

            SaveTradeData();
        }

        private void RemoveGold(Player player, long amount)
        {
            _PlayerData[player.Id].Gold -= amount;

            if (_PlayerData[player.Id].Gold < 0L) _PlayerData[player.Id].Gold = 0L;
        }

        private void RemoveGold(ulong playerId, long amount)
        {
            _PlayerData[playerId].Gold -= amount;

            if (_PlayerData[playerId].Gold < 0L) _PlayerData[playerId].Gold = 0L;
        }

        private void AddItemsToInventory(ItemCollection inventory, string resource, int stackLimit, int amount)
        {
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
            var stacks = (int)Math.Ceiling((double)amount / stackLimit);
            var amountRemaining = amount;
            for (var i = 0; i < stacks; i++)
            {
                var invGameItemStack = new InvGameItemStack(blueprintForName, amountRemaining, null);
                inventory.AddItem(invGameItemStack, true);
                amountRemaining -= stackLimit;
            }
        }

        private void RemoveItemsFromInventory(ItemCollection inventory, string resource, int stackLimit, int amount)
        {
            var amountRemaining = amount;
            foreach (var item in inventory.Where(item => item != null))
            {
                if (!string.Equals(item.Name, resource, StringComparison.CurrentCultureIgnoreCase)) continue;

                var removeAmount = amountRemaining;
                if (item.StackAmount < removeAmount) removeAmount = item.StackAmount;
                inventory.SplitItem(item, removeAmount);
                amountRemaining -= removeAmount;
            }
        }

        private static int GetStackLimit(string name)
        {
            var blueprint = InvDefinitions.Instance.Blueprints.GetBlueprintForName(name, true, true);
            var containerManagement = blueprint.TryGet<ContainerManagement>();
            return containerManagement?.StackLimit ?? 0;
        }

        private void DeflatePrices()
        {
            if (!UseDeflation) return;

            foreach (KeyValuePair<string, TradeData> item in _GEData.TradeList)
            {
                item.Value.DeflatePrice();
            }

            SaveTradeData();
        }

        private void ForcePriceAdjustment()
        {
            foreach (KeyValuePair<string, TradeData> item in _GEData.TradeList)
            {
                item.Value.BuyPrice = item.Value.OriginalPrice;
                item.Value.SellPrice = (int)(item.Value.OriginalPrice * (SellPercentage / 100));
            }
            SaveTradeData();
        }

        private Vector3 ConvertPosCube(Vector3Int positionCube)
        {
            if (positionCube.x != 0)
            {
                return new Vector3(positionCube.x * 1.2f, positionCube.y * 1.2f, positionCube.z * 1.2f);
            }
            return new Vector3();
        }

        #endregion

        #region Hooks

        private void OnPlayerConnected(Player player)
        {
            CheckPlayerExists(player);

            SaveTradeData();
        }

        private void OnPlayerDisconnected(Player player)
        {
            CheckPlayerExists(player);

            SaveTradeData();
        }

        private void OnEntityDeath(EntityDeathEvent e)
        {
            #region Null Checks
            if (e == null) return;
            if (e.Cancelled) return;
            if (e.KillingDamage == null) return;
            if (e.KillingDamage.DamageSource == null) return;
            if (!e.KillingDamage.DamageSource.IsPlayer) return;
            if (e.KillingDamage.DamageSource.Owner == null) return;
            if (e.Entity == null) return;
            if (e.Entity == e.KillingDamage.DamageSource) return;
            #endregion

            var killer = e.KillingDamage.DamageSource.Owner;
            CheckPlayerExists(killer);

            int goldReward;
            if (!e.Entity.IsPlayer)
            {
                var entity = e.Entity;
                if (IsAnimal(entity) && PveGold)
                {
                    goldReward = _Random.Next(2, GoldRewardForPve);
                    GiveGold(killer, goldReward);

                    PrintToChat(killer, string.Format(GetMessage("Gold Collected", killer), goldReward));
                }
            }
            else
            {
                if (!PvpGold) return;

                if (e.Entity.Owner == null) return;
                var victim = e.Entity.Owner;
                CheckPlayerExists(victim);

                if (victim.Id == 0 || killer.Id == 0) return;
                if (victim.GetGuild() == null || killer.GetGuild() == null) return;
                if (victim.GetGuild().Name == killer.GetGuild().Name) { PrintToChat(killer, GetMessage("Chat Title", killer) + GetMessage("Gold Guild", killer)); return; }

                var victimGold = _PlayerData[victim.Id].Gold;
                goldReward = (int)(victimGold * (GoldStealPercentage / 100));
                var goldAmount = (long)_Random.Next(0, goldReward);
                if (goldAmount > victimGold) goldAmount = victimGold;

                if (goldAmount == 0) PrintToChat(killer, string.Format(GetMessage("Gold Kill", killer), victim.Name));
                else
                {
                    GiveGold(killer, goldAmount);
                    RemoveGold(victim, goldAmount);

                    PrintToChat(string.Format(GetMessage("Gold Kill Global", killer), killer.DisplayName, goldAmount, victim.DisplayName));
                }
            }

            SaveTradeData();
        }

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            #region Null Checks
            if (e == null) return;
            if (e.Cancelled) return;
            if (e.Damage == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            if (e.Damage.DamageSource.Owner == null) return;
            if (e.Entity == null) return;
            if (e.Entity == e.Damage.DamageSource) return;
            #endregion
            if (!SafeTrade) return;
            if (_GEData.HasPosition() != 2) return;
            if (!_GEData.IsInTradeArea(e.Entity.Position)) return;

            e.Cancel();
            e.Damage.Amount = 0f;
            if (e.Entity.IsPlayer) PrintToChat(e.Damage.DamageSource.Owner, GetMessage("Chat Title", e.Damage.DamageSource.Owner) + GetMessage("Trade Defense", e.Damage.DamageSource.Owner));
        }

        private void OnCubeTakeDamage(CubeDamageEvent e)
        {
            #region Null Checks
            if (e == null) return;
            if (e.Cancelled) return;
            if (e.Position == null) return;
            if (e.Damage == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            #endregion

            var centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(e.Position);
            SalvageModifier component = null;
            if (centralPrefabAtLocal != null) component = centralPrefabAtLocal.GetComponent<SalvageModifier>();

            if (!SafeTrade || _GEData.HasPosition() != 2 || !_GEData.IsInTradeArea(ConvertPosCube(e.Position)))
            {
                if (component != null) component.info.NotSalvageable = false;
                return;
            }

            if (component != null) component.info.NotSalvageable = true;
            e.Cancel();
            e.Damage.Amount = 0f;
        }

        private void SendHelpText(Player player)
        {
            PrintToChat(player, "[0000FF]Grand Exchange Commands[FFFFFF]");
            PrintToChat(player, "[00ff00]/gehelp[FFFFFF] - Show the list of commands.");
            PrintToChat(player, "[00ff00]/wallet[FFFFFF] - Show how much gold you currently have.");
            PrintToChat(player, "[00ff00]/topgold <optional:all>[FFFFFF] - Show a list of the online players and their gold amount. Use '/topgold all' to get a list with the richest players.");
            PrintToChat(player, "[00ff00]/shop[FFFFFF] - Show the list of items you can buy from a player created shop.");
            PrintToChat(player, "[00ff00]/store[FFFFFF] - Show the list of items you can buy on the Grand Exchange and their price.");
            PrintToChat(player, "[00ff00]/buy[FFFFFF] - Buy an item from the Grand Exchange,");
            PrintToChat(player, "[00ff00]/sell[FFFFFF] - Sell and item to the Grand Exchange.");
            PrintToChat(player, "[00ff00]/sendgold <amount> <player name>[FFFFFF] - Send an amount of gold from your account to another player.");
            if (player.HasPermission("GrandExchange.Modify.Itemlist"))
            {
                PrintToChat(player, "[00ff00]/ge.additem <item name> <price>[FFFFFF] - Add an item to the Grand Exchange (must excist in the item database in the config file).");
                PrintToChat(player, "[00ff00]/ge.removeitem <item name>[FFFFFF] - Remove an item from the Grand Exchange.");
                PrintToChat(player, "[00ff00]/ge.removeallitems[FFFFFF] - Remove all items from the Grand Exchange.");
                PrintToChat(player, "[00ff00]/ge.setprice <item name> <price>[FFFFFF] - Set the price of an item in the Grand Exchange.");
                PrintToChat(player, "[00ff00]/ge.defaultitems[FFFFFF] - Show a list of all the default items and prices of the Grand Exchange.");
                PrintToChat(player, "[00ff00]/ge.restoredefaultitems[FFFFFF] - Restores the Grand Exchange item list to the default item list.");
                PrintToChat(player, "[00ff00]/ge.restoredefaultprices[FFFFFF] - Restores the Grand Exchange item prices to their original values.");
                PrintToChat(player, "[00ff00]/ge.setsellpercentage <percentage>[FFFFFF] - Set the sell percentage for the items in the Grand Exchange.");
                PrintToChat(player, "[00ff00]/ge.setinflation <percentage>[FFFFFF] - Set the inflation percentage for the items in the Grand Exchange.");
                PrintToChat(player, "[00ff00]/ge.setmaxdeflation <percentage>[FFFFFF] - Set the max deflation percentage for the items in the Grand Exchange.");
            }
            if (player.HasPermission("GrandExchange.Modify.Settings"))
            {
                PrintToChat(player, "[00ff00]/ge.addmark[FFFFFF] - Set a mark for the Grand Exchange area.");
                PrintToChat(player, "[00ff00]/ge.removemarks[FFFFFF] - Remove the marks for the Grand Exchange area.");
                PrintToChat(player, "[00ff00]/ge.pvp[FFFFFF] - Toggle the pvp gold stealing.");
                PrintToChat(player, "[00ff00]/ge.pve[FFFFFF] - Toggle the pve gold gain.");
                PrintToChat(player, "[00ff00]/ge.safetrade[FFFFFF] - Toggle whether the trading areas are safe or not.");
                PrintToChat(player, "[00ff00]/ge.toggle[FFFFFF] - Toggle the access of players without the 'grandexchange.show' for the /buy, /sell and /store commands.");
                PrintToChat(player, "[00ff00]/ge.deflation[FFFFFF] - Toggle the drop of item prices over time.");
                PrintToChat(player, "[00ff00]/ge.restoreshops[FFFFFF] - Resets the name and location of all player created shops.");
                PrintToChat(player, "[00ff00]/loc[FFFFFF] - Show your current coordinates.");
                PrintToChat(player, "[00ff00]/setgold <amount> <optional:player name>[FFFFFF] - Set the gold of a player. If no player name is given then your own gold amount will be set.");
                PrintToChat(player, "[00ff00]/resetgold[FFFFFF] - Remove the gold of every player in the database.");
                PrintToChat(player, "[00ff00]/givegold <amount> <optional:player name>[FFFFFF] - Give gold to a player. If no player name is given then you gain the amount of gold.");
                PrintToChat(player, "[00ff00]/removegold <amount> <optional:player name>[FFFFFF] - Remove gold of a player. If no player name is given then you lose the amount of gold.");
                PrintToChat(player, "[00ff00]/checkgold <player name>[FFFFFF] - Show the gold amount of a player.");
            }

            PrintToChat(player, "[2020FF]Player Shop Commands[FFFFFF]");
            PrintToChat(player, "[00ff00]/myshop[FFFFFF] - Show the item list of your own shop.");
            PrintToChat(player, "[00ff00]/addshopmark[FFFFFF] - Set a mark for your own shop. The shop may not be bigger then 13 by 13 blocks.");
            PrintToChat(player, "[00ff00]/removeshopmarks[FFFFFF] - Remove the marks for your show.");
            PrintToChat(player, "[00ff00]/addshopitem <item name> <amount> <optional if already excists:price>[FFFFFF] - Add the amount of an item to your shop. You must have this item and the correct amount in your inventory.");
            PrintToChat(player, "[00ff00]/removeshopitem <item name> <optional:amount>[FFFFFF] - Remove the amount of an item from your shop. You must have enough space in your inventory.");
            PrintToChat(player, "[00ff00]/setitemprice <item name> <price>[FFFFFF] - Change the price of an item in your shop.");
            PrintToChat(player, "[00ff00]/setshopname <name>[ffffff] - Changes the name of your shop.");
        }

        #endregion

        #region Utility

        private string Capitalise(string word)
        {
            string[] splitWord = word.Split(' ');

            for (int i = 0; i < splitWord.Count(); i++)
            {
                if (splitWord[i].StartsWith("(")) splitWord[i] = $"({splitWord[i].Substring(1, 1).ToUpper()}{splitWord[i].Substring(2).ToLower()}";
                else splitWord[i] = splitWord[i].Substring(0, 1).ToUpper() + splitWord[i].Substring(1).ToLower();
            }

            return splitWord.JoinToString(" ");
        }

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
            }
            object value = null;
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, (player?.Id.ToString()));

        #endregion
    }
}