using System;
using System.Linq;
using System.Collections.Generic;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Entities;
using Oxide.Core;
using CodeHatch.ItemContainer;
using UnityEngine;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.Blocks;
using CodeHatch.Thrones.Weapons.Salvage;

namespace Oxide.Plugins
{
    [Info("Grand Exchange", "D-Kay && Scorpyon", "2.1.1", ResourceId = 1145)]
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
                TradeList.Add(resource, new TradeData(price, _ItemList[resource]));
            }

            public int HasPosition()
            {
                if (X1 == 0f || Z1 == 0f) return 0;
                else if (X2 == 0f || Z2 == 0f) return 1;
                else return 2;
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
            public int MaxStackSize { get; set; } = 0;
            public int BuyPrice { get; set; } = 0;
            public int SellPrice { get; set; } = 0;

            public TradeData() { }

            public TradeData(int originalPrice, int maxStackSize)
            {
                OriginalPrice = originalPrice;
                MaxStackSize = maxStackSize;
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
                int price = 0;
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

            public int GetStacks(int amount)
            {
                return (int)Math.Ceiling((double)amount / MaxStackSize);
            }

            public void UpdatePrices(int amount, int type)
            {
                switch (type)
                {
                    case 1:
                        BuyPrice = (int)(BuyPrice + ((OriginalPrice * (Inflation / 100)) * (amount / MaxStackSize)));
                        if (BuyPrice < 1) BuyPrice = 1;
                        break;
                    case 2:
                        SellPrice = (int)(SellPrice - ((OriginalPrice * (Inflation / 100)) * (amount / MaxStackSize)));
                        if (SellPrice < 1) SellPrice = 1;
                        break;
                }
            }

            public void DeflatePrice()
            {
                double inflationModifier = Inflation / 100;
                double deflationModifier = MaxDeflation / 100;
                double stackModifier = 1;
                int newBuyPrice = (int)(BuyPrice - ((OriginalPrice * inflationModifier) * stackModifier));
                int newSellPrice = (int)(SellPrice + ((OriginalPrice * inflationModifier) * stackModifier));

                int priceBottomShelf = (int)(OriginalPrice - ((OriginalPrice * deflationModifier) * stackModifier));
                int priceTopShelf = (int)((OriginalPrice + ((OriginalPrice * deflationModifier) * stackModifier)) * (SellPercentage / 100));

                if (newBuyPrice < priceBottomShelf) newBuyPrice = priceBottomShelf;
                if (newSellPrice > priceTopShelf) newSellPrice = priceTopShelf;

                BuyPrice = newBuyPrice;
                SellPrice = newSellPrice;
            }
        }
        private class PlayerData
        {
            public string Name { get; set; } = "";
            public int Gold { get; set; } = 0;
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
                else if (X2 == 0f || Z2 == 0f) return 1;
                else return 2;
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
                if (ItemList.ContainsKey(resource))
                {
                    if (!ItemList[resource].AddAmount(amount)) return 1;
                    if (price > 0) ItemList[resource].Price = price;
                    return 2;
                }

                if (ItemList.Count >= PlayerShopMaxSlots) return 0;
                if (amount > PlayerShopStackLimit * _ItemList[resource]) return 1;

                ItemList.Add(resource, new ItemData(price, amount, _ItemList[resource]));
                return 2;
            }
        }
        private class ItemData
        {
            public int Price { get; set; } = 0;
            public int Amount { get; set; } = 0;
            public int MaxStackSize { get; set; } = 0;

            public ItemData() { }

            public ItemData(int price, int amount, int maxStackSize)
            {
                Price = price;
                Amount = amount;
                MaxStackSize = maxStackSize;
            }

            public int GetAmount()
            {
                if (Amount < MaxStackSize) return Amount;
                return MaxStackSize;
            }

            public int GetPrice(int amount)
            {
                return Price * amount;
            }

            public int GetStacks()
            {
                return (int)Math.Ceiling((double)Amount / MaxStackSize);
            }

            public bool AddAmount(int amount)
            {
                if (Amount + amount > PlayerShopStackLimit * MaxStackSize) return false;

                Amount += amount;
                return true;
            }
        }

        private GrandExchangeData _GEData = new GrandExchangeData();
        private Dictionary<ulong, PlayerData> _PlayerData = new Dictionary<ulong, PlayerData>();
        private static SortedDictionary<string, int> _ItemList = new SortedDictionary<string, int>();

        private readonly System.Random _Random = new System.Random();

        #region Full Item List
        private Dictionary<string, object> _DefaultItemList = new Dictionary<string, object>()
        {
            { "Advanced Fletcher", 1 },
            { "African Mask", 1 },
            { "Amberjack Fish", 1 },
            { "Anvil", 1 },
            { "Apple", 25 },
            { "Apple Seed", 100 },
            { "Archery Target", 25 },
            { "Asian Mask", 1 },
            { "Asian Tribal Mask", 1 },
            { "Baked Clay", 1000 },
            { "Ballista", 1 },
            { "Ballista Bolt", 1000 },
            { "Bandage", 25 },
            { "Banquet Table", 25 },
            { "Bascinet Helmet", 1 },
            { "Bascinet Pointed Helmet", 1 },
            { "Bass Fish", 1 },
            { "Bat Wing", 1000 },
            { "Bean Seed", 100 },
            { "Bear Hide", 1000 },
            { "Bear Skin Rug", 25 },
            { "Beet", 25 },
            { "Beet Seed", 100 },
            { "Bell Gong", 25 },
            { "Bellows", 1 },
            { "Bent Horn", 1 },
            { "Berries", 25 },
            { "Berry Seed", 100 },
            { "Bladed Pillar", 25 },
            { "Blood", 1000 },
            { "Bone", 1000 },
            { "Bone Axe", 1 },
            { "Bone Dagger", 1 },
            { "Bone Horn", 1 },
            { "Bone Longbow", 1 },
            { "Bone Spiked Club", 1 },
            { "Bread", 25 },
            { "Bug Net", 1 },
            { "Burnt Bird", 25 },
            { "Burnt Meat", 25 },
            { "Butterfly", 100 },
            { "Cabbage", 25 },
            { "Cabbage Seed", 100 },
            { "Campfire", 1 },
            { "Candle", 1 },
            { "Candle Stand", 25 },
            { "Carrot", 25 },
            { "Carrot Seed", 100 },
            { "Cat Mask", 1 },
            { "Chandelier", 25 },
            { "Chapel De Fer Helmet", 1 },
            { "Chapel De Fer Rounded Helmet", 1 },
            { "Charcoal", 1000 },
            { "Clay", 1000 },
            { "Clay Block", 1000 },
            { "Clay Corner", 1000 },
            { "Clay Inverted Corner", 1000 },
            { "Clay Ramp", 1000 },
            { "Clay Stairs", 1000 },
            { "Cobblestone Block", 1000 },
            { "Cobblestone Corner", 1000 },
            { "Cobblestone Inverted Corner", 1000 },
            { "Cobblestone Ramp", 1000 },
            { "Cobblestone Stairs", 1000 },
            { "Cooked Beans", 25 },
            { "Cooked Bird", 25 },
            { "Cooked Meat", 25 },
            { "Crossbow", 1 },
            { "Deer Head Trophy", 25 },
            { "Deer Leg Club", 1 },
            { "Defensive Barricade", 25 },
            { "Diamond", 1000 },
            { "Dirt", 1000 },
            { "Djembe Drum", 1 },
            { "Driftwood Club", 1 },
            { "Duck Feet", 1000 },
            { "Executioners Axe", 1 },
            { "Fang", 1000 },
            { "Fat", 1000 },
            { "Feather", 1000 },
            { "Fern", 1000 },
            { "Fern Bracers", 1 },
            { "Fern Helmet", 1 },
            { "Fern Sandals", 1 },
            { "Fern Skirt", 1 },
            { "Fern Vest", 1 },
            { "Fire Fly", 100 },
            { "Fire Water", 1000 },
            { "Firepit", 1 },
            { "Fishing Rod", 1 },
            { "Flat Top Helmet", 1 },
            { "Flax", 1000 },
            { "Fletcher", 1 },
            { "Flour", 10 },
            { "Flower Bracers", 1 },
            { "Flower Helmet", 1 },
            { "Flower Sandals", 1 },
            { "Flower Skirt", 1 },
            { "Flower Vest", 1 },
            { "Flowers", 1000 },
            { "Fluffy Bed", 1 },
            { "Fly", 100 },
            { "Forest Sprite", 1 },
            { "Fuse", 1000 },
            { "Gazebo", 1 },
            { "Grain", 1000 },
            { "Grain Seed", 100 },
            { "Granary", 1 },
            { "Grave Mask", 1 },
            { "Great Fireplace", 25 },
            { "Ground Torch", 25 },
            { "Guillotine", 1 },
            { "Hanging Lantern", 25 },
            { "Hanging Torch", 25 },
            { "Hay", 1000 },
            { "Hay Bale Target", 25 },
            { "Hay Bracers", 1 },
            { "Hay Helmet", 1 },
            { "Hay Sandals", 1 },
            { "Hay Skirt", 1 },
            { "Hay Vest", 1 },
            { "Heart", 1000 },
            { "High Quality Bed", 1 },
            { "High Quality Bench", 25 },
            { "High Quality Cabinet", 25 },
            { "Hoe", 1 },
            { "Iron", 1000 },
            { "Iron Axe", 1 },
            { "Iron Bar Window", 10 },
            { "Iron Battle Axe", 1 },
            { "Iron Battle Hammer", 1 },
            { "Iron Bear Trap", 25 },
            { "Iron Buckler", 1 },
            { "Iron Chest", 10 },
            { "Iron Crest", 1 },
            { "Iron Dagger", 1 },
            { "Iron Door", 10 },
            { "Iron Flanged Mace", 1 },
            { "Iron Floor Torch", 25 },
            { "Iron Forked Spear", 1 },
            { "Iron Gate", 10 },
            { "Iron Halberd", 1 },
            { "Iron Hatchet", 1 },
            { "Iron Heater", 1 },
            { "Iron Ingot", 1000 },
            { "Iron Javelin", 50 },
            { "Iron Morning Star Mace", 1 },
            { "Iron Pickaxe", 1 },
            { "Iron Plate Boots", 1 },
            { "Iron Plate Gauntlets", 1 },
            { "Iron Plate Helmet", 1 },
            { "Iron Plate Pants", 1 },
            { "Iron Plate Vest", 1 },
            { "Iron Shackles", 1 },
            { "Iron Spear", 1 },
            { "Iron Spikes", 25 },
            { "Iron Spikes (Hidden)", 25 },
            { "Iron Star Mace", 1 },
            { "Iron Sword", 1 },
            { "Iron Throwing Axe", 50 },
            { "Iron Throwing Battle Axe", 50 },
            { "Iron Throwing Knife", 50 },
            { "Iron Tipped Arrow", 100 },
            { "Iron Totem", 1 },
            { "Iron Tower", 1 },
            { "Iron War Hammer", 1 },
            { "Iron Wood Cutters Axe", 1 },
            { "Japanese Demon", 1 },
            { "Japanese Mask", 1 },
            { "Jester Hat (Green & Pink)", 1 },
            { "Jester Hat (Orange & Black)", 1 },
            { "Jester Hat (Rainbow)", 1 },
            { "Jester Hat (Red)", 1 },
            { "Jester Mask (Gold & Blue)", 1 },
            { "Jester Mask (Gold & Red)", 1 },
            { "Jester Mask (White & Blue)", 1 },
            { "Jester Mask (White & Gold)", 1 },
            { "Kettle Board Helmet", 1 },
            { "Kettle Hat", 1 },
            { "Koi Fish", 1 },
            { "Large Gallows", 1 },
            { "Large Iron Cage", 1 },
            { "Large Iron Hanging Cage", 1 },
            { "Large Wood Billboard", 10 },
            { "Leather Crest", 1 },
            { "Leather Hide", 1000 },
            { "Light Leather Boots", 1 },
            { "Light Leather Bracers", 1 },
            { "Light Leather Helmet", 1 },
            { "Light Leather Pants", 1 },
            { "Light Leather Vest", 1 },
            { "Liver", 1000 },
            { "Lockpick", 50 },
            { "Log Block", 1000 },
            { "Log Corner", 1000 },
            { "Log Fence", 1000 },
            { "Log Inverted Corner", 1000 },
            { "Log Ramp", 1000 },
            { "Log Stairs", 1000 },
            { "Long Horn", 1 },
            { "Long Wood Drawbridge", 10 },
            { "Lord's Bath", 25 },
            { "Lord's Bed", 1 },
            { "Lord's Large Chair", 25 },
            { "Lord's Small Chair", 25 },
            { "Low Quality Bed", 1 },
            { "Low Quality Bench", 25 },
            { "Low Quality Chair", 25 },
            { "Low Quality Fence", 1000 },
            { "Low Quality Shelf", 25 },
            { "Low Quality Stool", 25 },
            { "Low Quality Table", 25 },
            { "Lumber", 1000 },
            { "Meat", 1000 },
            { "Medium Banner", 10 },
            { "Medium Quality Bed", 1 },
            { "Medium Quality Bench", 25 },
            { "Medium Quality Bookcase", 25 },
            { "Medium Quality Chair", 25 },
            { "Medium Quality Dresser", 25 },
            { "Medium Quality Stool", 25 },
            { "Medium Quality Table", 25 },
            { "Medium Steel Hanging Sign", 10 },
            { "Medium Stick Billboard", 10 },
            { "Medium Wood Billboard", 10 },
            { "Nasal Helmet", 1 },
            { "Oil", 1000 },
            { "Onion", 25 },
            { "Onion Seed", 100 },
            { "Pillory", 1 },
            { "Pine Cone", 100 },
            { "Plague Doctor Mask", 1 },
            { "Poplar Seed", 100 },
            { "Potion Of Antidote", 25 },
            { "Potion Of Appearance", 25 },
            { "Rabbit Pelt", 1000 },
            { "Raw Bird", 1000 },
            { "Reinforced Wood (Iron) Block", 1000 },
            { "Reinforced Wood (Iron) Corner", 1000 },
            { "Reinforced Wood (Iron) Door", 10 },
            { "Reinforced Wood (Iron) Gate", 10 },
            { "Reinforced Wood (Iron) Inverted Corner", 1000 },
            { "Reinforced Wood (Iron) Ramp", 1000 },
            { "Reinforced Wood (Iron) Stairs", 1000 },
            { "Reinforced Wood (Iron) Trap Door", 10 },
            { "Reinforced Wood (Steel) Door", 10 },
            { "Repair Hammer", 1 },
            { "Rocking Horse", 25 },
            { "Rope", 1 },
            { "Roses", 1000 },
            { "Salmon Fish", 1 },
            { "Sawmill", 1 },
            { "Scythe", 1 },
            { "Shardana Mask", 1 },
            { "Sharp Rock", 50 },
            { "Siegeworks", 1 },
            { "Simple Helmet", 1 },
            { "Small Banner", 10 },
            { "Small Gallows", 1 },
            { "Small Iron Cage", 1 },
            { "Small Iron Hanging Cage", 1 },
            { "Small Steel Hanging Sign", 10 },
            { "Small Steel Signpost", 10 },
            { "Small Stick Signpost", 10 },
            { "Small Wall Lantern", 25 },
            { "Small Wall Torch", 25 },
            { "Small Wood Hanging Sign", 10 },
            { "Small Wood Signpost", 10 },
            { "Smelter", 1 },
            { "Smithy", 1 },
            { "Sod Block", 1000 },
            { "Sod Corner", 1000 },
            { "Sod Inverted Corner", 1000 },
            { "Sod Ramp", 1000 },
            { "Sod Stairs", 1000 },
            { "Spinning Wheel", 1 },
            { "Splintered Club", 1 },
            { "Spruce Branches Block", 1000 },
            { "Spruce Branches Corner", 1000 },
            { "Spruce Branches Inverted Corner", 1000 },
            { "Spruce Branches Ramp", 1000 },
            { "Spruce Branches Stairs", 1000 },
            { "Standing Iron Torch", 25 },
            { "Steel Axe", 1 },
            { "Steel Battle Axe", 1 },
            { "Steel Battle Hammer", 1 },
            { "Steel Bolt", 100 },
            { "Steel Buckler", 1 },
            { "Steel Cage", 1 },
            { "Steel Chest", 10 },
            { "Steel Compound", 1000 },
            { "Steel Crest", 1 },
            { "Steel Dagger", 1 },
            { "Steel Flanged Mace", 1 },
            { "Steel Greatsword", 1 },
            { "Steel Halberd", 1 },
            { "Steel Hatchet", 1 },
            { "Steel Heater", 1 },
            { "Steel Ingot", 1000 },
            { "Steel Javelin", 50 },
            { "Steel Morning Star Mace", 1 },
            { "Steel Pickaxe", 1 },
            { "Steel Picture Frame", 10 },
            { "Steel Plate Boots", 1 },
            { "Steel Plate Gauntlets", 1 },
            { "Steel Plate Helmet", 1 },
            { "Steel Plate Pants", 1 },
            { "Steel Plate Vest", 1 },
            { "Steel Spear", 1 },
            { "Steel Star Mace", 1 },
            { "Steel Sword", 1 },
            { "Steel Throwing Battle Axe", 50 },
            { "Steel Throwing Knife", 50 },
            { "Steel Tipped Arrow", 100 },
            { "Steel Tower", 1 },
            { "Steel War Hammer", 1 },
            { "Steel Wood Cutters Axe", 1 },
            { "Sticks", 1000 },
            { "Stiff Bed", 1 },
            { "Stone", 1000 },
            { "Stone Arch", 10 },
            { "Stone Arrow", 100 },
            { "Stone Block", 1000 },
            { "Stone Corner", 1000 },
            { "Stone Cutter", 1 },
            { "Stone Dagger", 1 },
            { "Stone Fireplace", 25 },
            { "Stone Hatchet", 1 },
            { "Stone Inverted Corner", 1000 },
            { "Stone Javelin", 50 },
            { "Stone Pickaxe", 1 },
            { "Stone Ramp", 1000 },
            { "Stone Slab", 1000 },
            { "Stone Slit Window", 10 },
            { "Stone Spear", 1 },
            { "Stone Stairs", 1000 },
            { "Stone Sword", 1 },
            { "Stone Throwing Axe", 50 },
            { "Stone Throwing Knife", 50 },
            { "Stone Totem", 1 },
            { "Stone Wood Cutters Axe", 1 },
            { "Tabard", 1 },
            { "Tannery", 1 },
            { "Tears Of The Gods", 1000 },
            { "Thatch Block", 1000 },
            { "Thatch Corner", 1000 },
            { "Thatch Inverted Corner", 1000 },
            { "Thatch Ramp", 1000 },
            { "Thatch Stairs", 1000 },
            { "Theater Mask (Gold & Red)", 1 },
            { "Theater Mask (White & Blue)", 1 },
            { "Theater Mask (White & Gold)", 1 },
            { "Theater Mask (White & Red)", 1 },
            { "Theatre Mask (Comedy)", 1 },
            { "Theatre Mask (Tragedy)", 1 },
            { "Throwing Stone", 50 },
            { "Tinker", 1 },
            { "Torch", 1 },
            { "Trebuchet", 1 },
            { "Trebuchet Hay Bale", 50 },
            { "Trebuchet Stone", 50 },
            { "Wall Lantern", 25 },
            { "Wall Torch", 25 },
            { "War Drum", 1 },
            { "Wasp", 100 },
            { "Water", 1000 },
            { "Watering Pot", 1 },
            { "Well", 1 },
            { "Wenceslas Helmet", 1 },
            { "Whip", 1 },
            { "Wolf Pelt", 1000 },
            { "Wood", 1000 },
            { "Wood Arrow", 100 },
            { "Wood Barricade", 25 },
            { "Wood Block", 1000 },
            { "Wood Bracers", 1 },
            { "Wood Buckler", 1 },
            { "Wood Cage", 1 },
            { "Wood Chest", 10 },
            { "Wood Corner", 1000 },
            { "Wood Door", 10 },
            { "Wood Drawbridge", 10 },
            { "Wood Flute", 1 },
            { "Wood Gate", 10 },
            { "Wood Heater", 1 },
            { "Wood Helmet", 1 },
            { "Wood Inverted Corner", 1000 },
            { "Wood Javelin", 50 },
            { "Wood Ledge", 1000 },
            { "Wood Mace", 1 },
            { "Wood Picture Frame", 10 },
            { "Wood Ramp", 1000 },
            { "Wood Sandals", 1 },
            { "Wood Short Bow", 1 },
            { "Wood Shutters", 10 },
            { "Wood Skirt", 1 },
            { "Wood Spear", 1 },
            { "Wood Spikes", 25 },
            { "Wood Stairs", 1000 },
            { "Wood Stick", 1 },
            { "Wood Sword", 1 },
            { "Wood Totem", 1 },
            { "Wood Tower", 1 },
            { "Wood Vest", 1 },
            { "Woodworking", 1 },
            { "Wool", 1000 },
            { "Work Bench", 1 },
            { "Worms", 100 }
        };
        #endregion
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

            Dictionary<string, object> list = GetConfig("Database", "Items", _DefaultItemList);
            foreach (KeyValuePair<string, object> item in list)
            {
                if (_ItemList.ContainsKey(item.Key)) continue;
                _ItemList.Add(item.Key, Convert.ToInt32(item.Value));
            }
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
            Config["Database", "Items"] = _ItemList;

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
                { "Store Buy Amount", "Of course!\n [00FF00]{0}[FFFFFF] is currently selling for [00FFFF]{1}[FFFF00]g[FFFFFF] per item.\n It can be bought in stacks of up to [00FF00]{2}[FFFFFF].\n How much would you like to buy?"},
                { "Store Buy Amount Wrong", "I'm afraid we cannot fulfill an order of that size."},
                { "Store Buy Confirm", "Very good!\n [00FFFF]{0} [00FF00]{1}[FFFFFF] will cost you a total of [FF0000]{2} [FFFF00]gold.[FFFFFF]\n Do you want to complete the purchase?"},
                { "Store Buy No Gold", "It looks like you don't have enough gold for this transaction."},
                { "Store Buy No Inventory Space", "I'm afraid you don't have enough inventory slots free. Come back when you have freed up some space."},
                { "Store Buy Complete", "{0} {1} has been added to your inventory and your wallet has been debited the appropriate amount."},
                { "Store Buy Finish", "Congratulations on your purchase. Please come again!"},

                { "Store Sell Item", "What [00FF00]item [FFFFFF]would you like to sell on the [00FFFF]Grand Exchange[FFFFFF]?"},
                { "Store Sell No Item", "Sorry, we currently can't take that item from you."},
                { "Store Sell Amount", "Hmmm!\n I believe that [00FF00]{0}[FFFFFF] is currently being purchased for [00FFFF]{1}[FFFF00]g[FFFFFF] per item.\n I'd be happy to buy this item in stacks of up to [00FF00]{2}[FFFFFF].\n How much did you want to sell?"},
                { "Store Sell Amount Wrong", "I'm afraid we cannot fulfill an order of that size."},
                { "Store Sell Confirm", "I suppose I can do that.\n [00FFFF]{0} [00FF00]{1}[FFFFFF] will give you a total of [FF0000]{2} [FFFF00]gold.[FFFFFF]\n Do you want to complete the sale?"},
                { "Store Sell No Resources", "It looks like you don't have the goods! What are you trying to pull here?"},
                { "Store Sell Complete", "{0} {1} has been removed from your inventory and your wallet has been credited for the sale."},
                { "Store Sell Finish", "Thanks for your custom, friend! Please come again!"},

                { "Shop Buy Item", "What [00FF00]item [FFFFFF]would you like to buy at this shop?"},
                { "Shop Buy No Item", "I'm afraid that item is currently not for sale."},
                { "Shop Buy Amount", "Yes, we have that!\n[00FF00]{0}[FFFFFF] is currently selling for [00FFFF]{1}[FFFF00]g[FFFFFF] per item.\nIt can be bought in stacks of up to [00FF00]{2}[FFFFFF].\n How much would you like to buy?"},
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
                if (input[0].ToLower() == "all")
                {
                    Dictionary<ulong, PlayerData> TopPlayers = new Dictionary<ulong, PlayerData>(_PlayerData);
                    int topListMax = 10;
                    if (TopPlayers.Keys.Count < 10) topListMax = TopPlayers.Keys.Count;
                    for (int i = 0; i < topListMax; i++)
                    {
                        int TopGoldAmount = 0;
                        KeyValuePair<ulong, PlayerData> target = new KeyValuePair<ulong, PlayerData>();
                        foreach (KeyValuePair<ulong, PlayerData> data in TopPlayers)
                        {
                            if (data.Value.Gold >= TopGoldAmount)
                            {
                                target = data;
                                TopGoldAmount = data.Value.Gold;
                            }
                        }
                        PrintToChat(player, $"{i + 1}. {target.Value.Name} : {target.Value.Gold} gold");
                        TopPlayers.Remove(target.Key);
                    }
                }
            }
            else
            {
                List<Player> onlinePlayers = Server.ClientPlayers as List<Player>;

                int topList = onlinePlayers.Count;

                for (int i = 0; i < topList; i++)
                {
                    int topGoldAmount = 0;
                    Player topPlayer = null;

                    foreach (Player oPlayer in onlinePlayers)
                    {
                        CheckPlayerExists(oPlayer);
                        if (_PlayerData[oPlayer.Id].Gold >= topGoldAmount)
                        {
                            topGoldAmount = _PlayerData[oPlayer.Id].Gold;
                            topPlayer = oPlayer;
                        }
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

            if (input.Length < 2) { PrintToChat(player, GetMessage("Invalid Args", player)); return; }

            int amount = 0;
            if (!int.TryParse(input[0], out amount)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }

            if (amount < 1) { PrintToChat(player, GetMessage("Gold Send Steal", player)); return; }

            if (_PlayerData[player.Id].Gold < amount) { PrintToChat(player, GetMessage("Gold Send Not Enough", player)); return; }

            string text = input.JoinToString(" ");
            string playerName = text.Substring(text.IndexOf(' ') + 1);

            Player target = Server.GetPlayerByName(playerName);

            if (target == null) { PrintToChat(player, GetMessage("Invalid Player", player)); return; }

            CheckPlayerExists(target);

            PrintToChat(player, string.Format(GetMessage("Gold Send", player), amount, target.DisplayName));
            PrintToChat(target, string.Format(GetMessage("Gold Received", player), amount, player.DisplayName));

            GiveGold(target, amount);
            RemoveGold(player, amount);

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
            if (!_ItemList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Item Non-Existing", player), resource)); return; }

            int price = 0;
            if (!int.TryParse(input[1], out price)) { PrintToChat(player, GetMessage("Invalid Amount", player)); return; }
            if (price < 0) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            _GEData.TradeList.Add(resource, new TradeData(price, _ItemList[resource]));

            PrintToChat(player, string.Format(GetMessage("Store Item Added", player), resource));

            SaveTradeData();
        }

        private void AddShopItem(Player player, string[] input)
        {
            CheckPlayerExists(player);
            
            if (_PlayerData[player.Id].Shop.HasPosition() != 2) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("No Shop Own", player)); return; }

            if (input.Length < 2 || input.Length > 3) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Args", player)); return; }

            string resource = Capitalise(input[0]);
            if (!_ItemList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Item Non-Existing", player), resource)); return; }

            int amount = 0;
            if (!int.TryParse(input[1], out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            int price = 0;
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

            RemoveItemsFromInventory(player, resource, amount);

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

            string resource = Capitalise(input.JoinToString(" "));

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

            string resource = Capitalise(input[0]);

            if (!_PlayerData[player.Id].Shop.ItemList.ContainsKey(resource)) { PrintToChat(player, string.Format(GetMessage("Shop No Item Text", player), resource)); return; }

            int amount = 0;

            if (input.Length < 2) amount = _PlayerData[player.Id].Shop.ItemList[resource].Amount;
            else if (!int.TryParse(input[1], out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            if (_PlayerData[player.Id].Shop.ItemList[resource].Amount < amount) { PrintToChat(player, GetMessage("Shop No Resources", player)); return; }

            ItemCollection inventory = player.GetInventory().Contents;

            if (inventory.FreeSlotCount < _PlayerData[player.Id].Shop.ItemList[resource].GetStacks()) { PrintToChat(player, string.Format(GetMessage("Shop No Inventory Space", player), _PlayerData[player.Id].Shop.ItemList[resource].GetStacks())); return; }

            InvItemBlueprint blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
            InvGameItemStack invGameItemStack = new InvGameItemStack(blueprintForName, amount, null);
            ItemCollection.AutoMergeAdd(inventory, invGameItemStack);

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

            if (SafeTrade) { SafeTrade = false; PrintToChat(player, string.Format(GetMessage("Toggle Safe Trade", player), "[FF0000]OFF")); }
            else { SafeTrade = true; PrintToChat(player, string.Format(GetMessage("Toggle Safe Trade", player), "[00FF00]ON")); }

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

            string resource = Capitalise(dialogue.ValueMessage);

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

            string message = "";
            switch (type)
            {
                case 1:
                    message = string.Format(GetMessage("Store Buy Amount", player), resource, _GEData.TradeList[resource].BuyPrice, _GEData.TradeList[resource].MaxStackSize);
                    player.ShowInputPopup(GetMessage("Popup Title", player), message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectExchangeAmount(player, options, dialogue1, resource, 1));
                    break;
                case 2:
                    message = string.Format(GetMessage("Store Sell Amount", player), resource, _GEData.TradeList[resource].SellPrice, _GEData.TradeList[resource].MaxStackSize);
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

            ItemCollection inventory = player.GetInventory().Contents;

            int stacks = _GEData.TradeList[resource].GetStacks(amount);
            if (inventory.FreeSlotCount < stacks) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy No Inventory Space", player)); return; }

            InvItemBlueprint blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
            int amountRemaining = amount;
            for (int i = 0; i < stacks; i++)
            {
                InvGameItemStack invGameItemStack = new InvGameItemStack(blueprintForName, amountRemaining, null);
                ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
                amountRemaining -= _ItemList[resource];
            }

            RemoveGold(player, totalValue);

            _GEData.TradeList[resource].UpdatePrices(amount, 1);

            PrintToChat(player, GetMessage("Chat Title", player) + string.Format(GetMessage("Store Buy Complete", player), amount, resource));
            PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Buy Finish", player));

            SaveTradeData();
        }

        private void CheckIfThePlayerHasTheResourceToSell(Player player, Options selection, Dialogue dialogue, string resource, int totalValue, int amount)
        {
            if (selection != Options.Yes) return;

            if (!CanRemoveResource(player, resource, amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Store Sell No Resources", player)); return; }

            RemoveItemsFromInventory(player, resource, amount);

            GiveGold(player, totalValue);
            
            _GEData.TradeList[resource].UpdatePrices(amount, 2);

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

            string amountText = dialogue.ValueMessage;

            int amount = 0;
            if (!int.TryParse(amountText, out amount)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Invalid Amount", player)); return; }

            if (amount < 1 || amount > _PlayerData[shopOwner].Shop.ItemList[resource].GetAmount()) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy Amount Wrong", player)); return; }

            int totalValue = _PlayerData[shopOwner].Shop.ItemList[resource].GetPrice(amount);

            string message = string.Format(GetMessage("Shop Buy Confirm", player), amount, resource, totalValue);

            message += "\n\n" + string.Format(GetMessage("Gold Available", player), _PlayerData[player.Id].Gold);

            player.ShowConfirmPopup(_PlayerData[shopOwner].Shop.Name.IsNullEmptyOrWhite() ? "Local Store" : _PlayerData[shopOwner].Shop.Name, message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerCanAffordThis(player, options, shopOwner, resource, totalValue, amount));
        }

        private void CheckIfThePlayerCanAffordThis(Player player, Options selection, ulong shopOwner, string resource, int totalValue, int amount)
        {
            if (selection != Options.Yes) return;

            if (!CanRemoveGold(player, totalValue)) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy No Gold", player)); return; }

            ItemCollection inventory = player.GetInventory().Contents;

            int stacks = _PlayerData[shopOwner].Shop.ItemList[resource].GetStacks();
            if (inventory.FreeSlotCount < stacks) { PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Shop Buy No Inventory Space", player)); return; }

            InvItemBlueprint blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
            int amountRemaining = amount;
            for (int i = 0; i < stacks; i++)
            {
                InvGameItemStack invGameItemStack = new InvGameItemStack(blueprintForName, amountRemaining, null);
                ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
                amountRemaining -= _ItemList[resource];
            }

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

        //private void CheckExchangeItemList()
        //{
        //    if (_GEData.TradeList == null) _GEData.TradeList = new SortedDictionary<string, TradeData>();
        //    if (_GEData.TradeList.Count > 0) return;

        //    foreach (KeyValuePair<string, TradeData> item in _DefaultTradeList)
        //    {
        //        TradeData newItem = new TradeData(item.Value.OriginalPrice, _ItemList[item.Key], SellPercentage);
        //        _GEData.TradeList.Add(item.Key, newItem);
        //    }

        //    SaveTradeData();
        //}

        private bool CanRemoveGold(Player player, int amount)
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

        private void GiveGold(Player player, int amount)
        {
            if (_PlayerData[player.Id].Gold + amount > MaxPossibleGold)
            {
                PrintToChat(player, GetMessage("Chat Title", player) + GetMessage("Gold Maxed", player));
                _PlayerData[player.Id].Gold = MaxPossibleGold;
            }
            else _PlayerData[player.Id].Gold += amount;

            SaveTradeData();
        }

        private void GiveGold(ulong playerId, int amount)
        {
            if (_PlayerData[playerId].Gold + amount > MaxPossibleGold) _PlayerData[playerId].Gold = MaxPossibleGold;
            else _PlayerData[playerId].Gold += amount;

            SaveTradeData();
        }

        private void RemoveGold(Player player, int amount)
        {
            _PlayerData[player.Id].Gold -= amount;

            if (_PlayerData[player.Id].Gold < 0) _PlayerData[player.Id].Gold = 0;
        }

        private void RemoveGold(ulong playerId, int amount)
        {
            _PlayerData[playerId].Gold -= amount;

            if (_PlayerData[playerId].Gold < 0) _PlayerData[playerId].Gold = 0;
        }

        public void RemoveItemsFromInventory(Player player, string resource, int amount)
        {
            ItemCollection inventory = player.GetInventory().Contents;

            int removeAmount = 0;
            int amountRemaining = amount;

            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if (item.Name != resource) continue;

                removeAmount = amountRemaining;
                if (item.StackAmount < removeAmount) removeAmount = item.StackAmount;
                inventory.SplitItem(item, removeAmount);
                amountRemaining = amountRemaining - removeAmount;
            }
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
        }

        private void OnEntityDeath(EntityDeathEvent e)
        {
            #region Null Checks
            if (e == null) return;
            if (e.KillingDamage == null) return;
            if (e.KillingDamage.DamageSource == null) return;
            if (!e.KillingDamage.DamageSource.IsPlayer) return;
            if (e.KillingDamage.DamageSource.Owner == null) return;
            if (e.Entity == null) return;
            if (e.Entity == e.KillingDamage.DamageSource) return;
            #endregion

            Player killer = e.KillingDamage.DamageSource.Owner;
            CheckPlayerExists(killer);

            int goldReward = 0;
            if (!e.Entity.IsPlayer)
            {
                Entity entity = e.Entity;
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
                Player victim = e.Entity.Owner;
                CheckPlayerExists(victim);

                if (victim.Id == 0 || killer.Id == 0) return;
                if (victim.GetGuild() == null || killer.GetGuild() == null) return;
                if (victim.GetGuild().Name == killer.GetGuild().Name) { PrintToChat(killer, GetMessage("Chat Title", killer) + GetMessage("Gold Guild", killer)); return; }

                int victimGold = _PlayerData[victim.Id].Gold;
                goldReward = (int)(victimGold * (double)(GoldStealPercentage / 100));
                int goldAmount = _Random.Next(0, goldReward);
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
            if (e.Position == null) return;
            if (e.Damage == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            #endregion

            TilesetColliderCube centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(e.Position);
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