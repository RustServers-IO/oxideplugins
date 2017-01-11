using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeHatch.Damaging;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;
using CodeHatch.Inventory.Blueprints;
using Oxide.Core;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.ItemContainer;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Blocks.Networking.Events;

namespace Oxide.Plugins
{
    [Info("Grand Exchange", "Scorpyon", "1.3.4")]
    public class GrandExchange : ReignOfKingsPlugin
    {
        #region MODIFIABLE VARIABLES (For server admin)

        private const double Inflation = 1; // This is the inflation modifier. More means bigger jumps in price changes (Currently raises at approx 1%
		private const double MaxDeflation = 5; // This is the deflation modifier. This is the most that a price can drop below its average price to buy and above it's price to sell(Percentage)
		private const int PriceDeflationTime = 3600; // This dictates the number of seconds for each tick which brings the prices back towards their original values
// (DEPRECATED)		private const int goldRewardForPvp = 10000; // This is the maximum amount of gold that can be stolen from a player for killing them.
        private const int GoldStealPercentage = 20; // This is the maximum percentage of gold that can be stolen from a player
		private const int GoldRewardForPve = 20; // This is the maximum amount rewarded to a player for killing monsters, etc. (When harvesting the dead body)
		private bool _allowPvpGold = true; // Turns on/off gold for PVP
		private bool _allowPveGold = true; // Turns on/off gold for PVE
		private bool _tradeAreaIsSafe; // Determines whether the marked safe area is Safe against being attacked / PvP
        private const int PlayerShopStackLimit = 5; // Determines the maximum number of stacks of an item a player can have in their shop
        private const int PlayerShopMaxSlots = 10; // Determines the maximum number of individual items the player can stock (Prevents using this as a 'Bag of Holding' style Chest!!)
        #endregion

        #region Default Trade Values
        private Collection<string[]> LoadDefaultTradeValues()
        {
            var defaultTradeList = new Collection<string[]>
            {
                new [] {"Apple", "45000", "25"},
                new [] {"Baked Clay", "100000", "1000"},
                new [] {"Bandage", "300000", "25"},
                new [] {"Bear Hide", "1000000", "1000"},
                new [] {"Bent Horn", "1000000", "1000"},
                new [] {"Berries", "35000", "25"},
                new [] {"Blood", "100000", "1000"},
                new [] {"Bone", "50000", "1000"},
                new [] {"Bone Axe", "1050000", "1"},
                new [] {"Bone Dagger", "325000", "1"},
                new [] {"Bone Horn", "850000", "1"},
                new [] {"Bone Spiked Club", "2125000", "1"},
                new [] {"Bread", "35000", "25"},
                new [] {"Cabbage", "45000", "25"},
                new [] {"Candlestand", "1000000", "1"},
                new [] {"Carrot", "45000", "25"},
                new [] {"Chandelier", "3750000", "1"},
                new [] {"Charcoal", "100000", "1000"},
                new [] {"Chicken", "250000", "25"},
                new [] {"Clay", "10000", "1000"},
                new [] {"Clay Block", "100000", "1000"},
                new [] {"Clay Ramp", "100000", "1000"},
                new [] {"Clay Stairs", "100000", "1000"},
                new [] {"Cobblestone Block", "750000", "1000"},
                new [] {"Cobblestone Ramp", "750000", "1000"},
                new [] {"Cobblestone Stairs", "750000", "1000"},
                new [] {"Cooked Bird", "35000", "25"},
                new [] {"Cooked Meat", "35000", "25"},
                new [] {"Crossbow", "5215000", "1"},
                new [] {"Deer Leg Club", "250000", "1"},
                new [] {"Diamond", "5000000", "500000"},
                new [] {"Dirt", "10000", "20000"},
                new [] {"Driftwood Club", "10000", "1"},
                new [] {"Duck Feet", "350000", "25"},
                new [] {"Fang", "75000", "1000"},
                new [] {"Fat", "45000", "1000"},
                new [] {"Feather", "25000", "1000"},
                new [] {"Fire Water", "5000000", "25"},
                new [] {"Firepit", "925000", "1"},
                new [] {"Great FirePlace", "4250000", "1"},
                new [] {"Stone FirePlace", "3125000", "1"},
                new [] {"Flax", "25000", "1000"},
                new [] {"Flowers", "25000", "1000"},
                new [] {"Fluffy Bed", "7750000", "1"},
                new [] {"Fuse", "500000", "1"},
                new [] {"Grain", "15000", "1000"},
                new [] {"Granary", "51500000", "1"},
                new [] {"Guillotine", "21250000", "1"},
                new [] {"Ground Torch", "4750000", "1"},
                new [] {"Hanging Lantern", "800000", "1"},
                new [] {"Hanging Torch", "3750000", "1"},
                new [] {"Hay", "10000", "1000"},
                new [] {"Hay Bale Target", "5250000", "1"},
                new [] {"Heart", "50000", "25"},
                new [] {"Holdable Candle", "500000", "1"},
                new [] {"Holdable Lantern", "800000", "1"},
                new [] {"Holdable Torch", "700000", "1"},
                new [] {"Iron", "100000", "1000"},
                new [] {"Iron Axe", "1450000", "1"},
                new [] {"Iron Bar Window", "2500000", "1"},
                new [] {"Iron Battle Axe", "7800000", "1"},
                new [] {"Iron Chest", "5625000", "10"},
                new [] {"Iron Crest", "77500000", "1"},
                new [] {"Iron Door", "20000000", "1"},
                new [] {"Iron Flanged Mace", "1950000", "1"},
                new [] {"Iron Floor Torch", "5000000", "1"},
                new [] {"Iron Gate", "40000000", "1"},
                new [] {"Iron Halberd", "10600000", "1"},
                new [] {"Iron Hatchet", "4125000", "1"},
                new [] {"Iron Ingot", "1250000", "1000"},
                new [] {"Iron Javelin", "434000", "50"},
                new [] {"Iron Pickaxe", "8000000", "1"},
                new [] {"Iron Plate Boots", "1375000", "1"},
                new [] {"Iron Plate Gauntlets", "1375000", "1"},
                new [] {"Iron Plate Helmet", "3250000", "1"},
                new [] {"Iron Plate Pants", "2875000", "1"},
                new [] {"Iron Plate Vest", "2875000", "1"},
                new [] {"Iron Star Mace", "1850000", "1"},
                new [] {"Iron Sword", "3000000", "1"},
                new [] {"Iron Tipped Arrow", "153000", "100"},
                new [] {"Iron Wood Cutters Axe", "7750000", "1"},
                new [] {"Large Gallows", "9500000", "1"},
                new [] {"Leather Crest", "1175000", "1"},
                new [] {"Leather Hide", "175000", "1000"},
                new [] {"Light Leather Boots", "325000", "1"},
                new [] {"Light Leather Bracers", "325000", "1"},
                new [] {"Light Leather Helmet", "1025000", "1"},
                new [] {"Light Leather Pants", "750000", "1"},
                new [] {"Light Leather Vest", "750000", "1"},
                new [] {"Liver", "75000", "25"},
                new [] {"Lockpick", "5100000", "25"},
                new [] {"Log Block", "70000", "1000"},
                new [] {"Log Ramp", "70000", "1000"},
                new [] {"Log Stairs", "70000", "1000"},
                new [] {"Long Horn", "2925000", "1"},
                new [] {"Lumber", "2000", "1000"},
                new [] {"Meat", "15000", "25"},
                new [] {"Medium Banner", "850000", "1"},
                new [] {"Oil", "125000", "1000"},
                new [] {"Pillory", "750000", "1"},
                new [] {"Potion Of Antidote", "375000", "25"},
                new [] {"Potion Of Appearance", "0", "25"},
                new [] {"Rabbit Pelt", "50000", "25"},
                new [] {"Raw Bird", "15000", "25"},
                new [] {"Reinforced Wood (Iron) Block", "1750000", "1000"},
                new [] {"Reinforced Wood (Iron) Door", "5750000", "10"},
                new [] {"Reinforced Wood (Iron) Gate", "21750000", "10"},
                new [] {"Reinforced Wood (Iron) Ramp", "1750000", "1000"},
                new [] {"Reinforced Wood (Iron) Stairs", "1750000", "1000"},
                new [] {"Reinforced Wood (Steel) Door", "20750000", "10"},
                new [] {"Repair Hammer", "500000", "1"},
                new [] {"Roses", "50000", "25"},
                new [] {"Small Banner", "525000", "1"},
                new [] {"Small Gallows", "4900000", "1"},
                new [] {"Small Wall Lantern", "2500000", "1"},
                new [] {"Small Wall Torch", "2500000", "1"},
                new [] {"Sod Block", "50000", "1000"},
                new [] {"Sod Ramp", "50000", "1000"},
                new [] {"Sod Stairs", "50000", "1000"},
                new [] {"Splintered Club", "500000", "1"},
                new [] {"Spruce Branches Block", "20000", "1000"},
                new [] {"Spruce Branches Ramp", "20000", "1000"},
                new [] {"Spruce Branches Stairs", "20000", "1000"},
                new [] {"Standing Iron Torch", "5000000", "1"},
                new [] {"Steel Axe", "10000000", "1"},
                new [] {"Steel Battle Axe", "30450000", "1"},
                new [] {"Steel Battle War Hammer", "30750000", "1"},
                new [] {"Steel Bolt", "409000", "100"},
                new [] {"Steel Chest", "16760000", "10"},
                new [] {"Steel Compound", "325000", "1000"},
                new [] {"Steel Dagger", "5000000", "1"},
                new [] {"Steel Flanged Mace", "10750000", "1"},
                new [] {"Steel Greatsword", "25625000", "1"},
                new [] {"Steel Halberd", "40500000", "1"},
                new [] {"Steel Hatchet", "15875000", "1"},
                new [] {"Steel Ingot", "5000000", "1000"},
                new [] {"Steel Javelin", "1683000", "50"},
                new [] {"Steel Morning Star Mace", "30625000", "1"},
                new [] {"Steel Pickaxe", "30500000", "1"},
                new [] {"Steel Plate Boots", "5200000", "1"},
                new [] {"Steel Plate Gauntlets", "5200000", "1"},
                new [] {"Steel Plate Helmet", "15500000", "1"},
                new [] {"Steel Plate Pants", "10300000", "1"},
                new [] {"Steel Plate Vest", "10300000", "1"},
                new [] {"Steel Star Mace", "30750000", "1"},
                new [] {"Steel Sword", "10750000", "1"},
                new [] {"Steel Throwing Knife", "1717000", "1"},
                new [] {"Steel Tipped Arrow", "580000", "100"},
                new [] {"Steel War Hammer", "30750000", "1"},
                new [] {"Steel Wood Cutters Axe", "30250000", "1"},
                new [] {"Sticks", "10000", "1000"},
                new [] {"Stiff Bed", "1050000", "1"},
                new [] {"Stone", "25000", "1000"},
                new [] {"Stone Arch", "1000000", "1"},
                new [] {"Stone Arrow", "50000", "100"},
                new [] {"Stone Block", "3090000", "1000"},
                new [] {"Stone Cutter", "100000", "1"},
                new [] {"Stone Dagger", "250000", "1"},
                new [] {"Stone Hatchet", "475000", "1"},
                new [] {"Stone Javelin", "42000", "50"},
                new [] {"Stone Pickaxe", "1125000", "1"},
                new [] {"Stone Ramp", "3090000", "1000"},
                new [] {"Stone Slab", "3040000", "1000"},
                new [] {"Stone Slit Window", "3050000", "1"},
                new [] {"Stone Stairs", "3090000", "1000"},
                new [] {"Stone Sword", "1250000", "1"},
                new [] {"Stone Wood Cutters Axe", "900000", "1"},
                new [] {"Tabard", "1000000", "1"},
                new [] {"Tears Of The Gods", "5000000", "10"},
                new [] {"Thatch Block", "50000", "1000"},
                new [] {"Thatch Ramp", "50000", "1000"},
                new [] {"Thatch Stairs", "50000", "1000"},
                new [] {"Throwing Stone", "25000", "100"},
                new [] {"Tinker", "250000", "1"},
                new [] {"Wall Lantern", "3750000", "1"},
                new [] {"Wall Torch", "3750000", "1"},
                new [] {"Water", "5000", "1000"},
                new [] {"Whip", "675000", "1"},
                new [] {"Wood", "5000", "1000"},
                new [] {"Wolf Pelt", "750000", "1"},
                new [] {"Wood Arrow", "28000", "100"},
                new [] {"Wood Block", "150000", "1000"},
                new [] {"Wood Bracers", "175000", "1"},
                new [] {"Wood Chest", "500000", "10"},
                new [] {"Wood Door", "500000", "1"},
                new [] {"Wood Gate", "1500000", "1"},
                new [] {"Wood Helmet", "700000", "1"},
                new [] {"Wood Ramp", "150000", "1000"},
                new [] {"Wood Sandals", "175000", "1"},
                new [] {"Wood Shutters", "150000", "1"},
                new [] {"Wood Skirt", "525000", "1"},
                new [] {"Wood Stairs", "150000", "1000"},
                new [] {"Wood Vest", "525000", "1"},
                new [] {"Wooden Flute", "1250000", "1"},
                new [] {"Wooden Javelin", "18000", "1"},
                new [] {"Wooden Mace", "1050000", "1"},
                new [] {"Wooden Short Bow", "150000", "1"},
                new [] {"Wool", "50000", "1000"}
            };
			
			// Default list prices ::: "Resource Name" ; "Price (x priceModifier)" ; "Maximum stack size"
			// YOU CAN EDIT THESE PRICES, BUT TO SEE THEM IN GAME YOU WILL EITHER NEED TO USE /restoredefaultprices WHICH WILL RESET ALL PRICES TO THE ONES HERE, OR USE /removestoreitem TO REMOVE THE OLD VERSION FROM THE EXISTING TRADE LIST AND THEN /addstoreitem TO INCLUDE THE NEW ONE. MAKE SURE YOU PAY ATTENTION TO THE ALPHABETICAL ORDER HERE, TOO!

            SaveTradeData();
            return defaultTradeList;
        }

#endregion
		
		// ================================================================================================================================
		// ================================================================================================================================
		// YOU SHOULDN'T NEED TO EDIT ANYTHING BELOW HERE =================================================================================
		// ================================================================================================================================
		// ================================================================================================================================
		
#region Server Variables (Do not modify!)

        void Log(string msg) => Puts($"{Title} : {msg}");
        
        private Collection<string[]> _tradeDefaults = new Collection<string[]>();
        // 0 - Resource name
        // 1 - Original Price
        // 2 - Max Stack size
        private Collection<string[]> _tradeList = new Collection<string[]>();
        // 0 - Resource name
        // 1 - Original Price
        // 2 - Max Stack size
        // 3 - Buy Price
        // 4 - Sell Price
        private Dictionary<string, int> _wallet = new Dictionary<string, int>();
        private Dictionary<ulong, int> _playerWallet = new Dictionary<ulong, int>();

		private const int PriceModifier = 1000; // Best not to change this unless you have to! I don't know what would happen to prices! 
		private readonly System.Random _random = new System.Random();

        //void Log(string msg) => Puts($"{Title} : {msg}");
		private const int MaxPossibleGold = 2100000000; // DO NOT RAISE THIS ANY HIGHER - 32-bit INTEGER FLOOD WARNING	

		private Collection<double[]> _markList = new Collection<double[]>();
        private Dictionary<string, double[]> _shopMarks = new Dictionary<string, double[]>();
        private Dictionary<ulong, double[]> _shopLocs = new Dictionary<ulong, double[]>();
        private double _sellPercentage = 50; // Use the /sellPercentage command to change this NOT here!

        private Dictionary<string, Collection<string[]>> _playerShop = new Dictionary<string, Collection<string[]>>();
        // 0 - Item name
        // 1 - Price
        // 2 - Amount
        private Dictionary<ulong, Collection<string[]>> _shopPlayer = new Dictionary<ulong, Collection<string[]>>();
        // 0 - Item name
        // 1 - Price
        // 2 - Amount

        private Collection<string> _tradeMasters = new Collection<string>();

#endregion

#region Save and Load Data Methods

        // SAVE DATA ===============================================================================================
        private void LoadTradeData()
        {
            _tradeDefaults = Interface.GetMod().DataFileSystem.ReadObject<Collection<string[]>>("SavedTradeDefaults");
            _tradeList = Interface.GetMod().DataFileSystem.ReadObject<Collection<string[]>>("SavedTradeList");
            _wallet = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, int>>("SavedTradeWallet");
            _playerWallet = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, int>>("SavedTradeWalletById");
            _markList = Interface.GetMod().DataFileSystem.ReadObject<Collection<double[]>>("SavedMarkList");
            _sellPercentage = Interface.GetMod().DataFileSystem.ReadObject<double>("SavedSellPercentage");
            _playerShop = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, Collection<string[]>>>("SavedPlayerShop");
            _shopPlayer = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, Collection<string[]>>>("SavedPlayerShopById");
            _tradeMasters = Interface.GetMod().DataFileSystem.ReadObject<Collection<string>>("SavedTradeMasters");
            _shopMarks = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, double[]>>("SavedPlayerShopMarks");
            _shopLocs = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, double[]>>("SavedPlayerShopLocs");
            _allowPveGold = Interface.GetMod().DataFileSystem.ReadObject<bool>("SavedPvEGoldStatus");
            _allowPvpGold = Interface.GetMod().DataFileSystem.ReadObject<bool>("SavedPvPGoldStatus");
        }

        private void SaveTradeData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedTradeDefaults", _tradeDefaults);
            Interface.GetMod().DataFileSystem.WriteObject("SavedTradeList", _tradeList);
            Interface.GetMod().DataFileSystem.WriteObject("SavedTradeWallet", _wallet);
            Interface.GetMod().DataFileSystem.WriteObject("SavedTradeWalletById", _playerWallet);
            Interface.GetMod().DataFileSystem.WriteObject("SavedMarkList", _markList);
            Interface.GetMod().DataFileSystem.WriteObject("SavedSellPercentage", _sellPercentage);
            Interface.GetMod().DataFileSystem.WriteObject("SavedPlayerShop", _playerShop);
            Interface.GetMod().DataFileSystem.WriteObject("SavedPlayerShopById", _shopPlayer);
            Interface.GetMod().DataFileSystem.WriteObject("SavedTradeMasters", _tradeMasters);
            Interface.GetMod().DataFileSystem.WriteObject("SavedPlayerShopMarks", _shopMarks);
            Interface.GetMod().DataFileSystem.WriteObject("SavedPlayerShopLocs", _shopLocs);
            Interface.GetMod().DataFileSystem.WriteObject("SavedPvEGoldStatus", _allowPveGold);
            Interface.GetMod().DataFileSystem.WriteObject("SavedPvPGoldStatus", _allowPvpGold);
        }
		
		private void OnPlayerConnected(Player player)
		{
			CheckWalletExists(player);
			CheckShopExists(player);


            // This should port over the previous player shop and wallet system to the new Id version 
            if (_playerShop.ContainsKey(player.Name.ToLower()))
            {
                if (!_shopPlayer.ContainsKey(player.Id))
                {
                    var shop = _playerShop[player.Name.ToLower()];
                    _shopPlayer.Add(player.Id, shop);
                }
                _playerShop.Remove(player.Name.ToLower());
            }

            if(_wallet.ContainsKey(player.Name.ToLower()))
            {
                if(!_playerWallet.ContainsKey(player.Id)) _playerWallet.Add(player.Id, _wallet[player.Name.ToLower()]);
                _wallet.Remove(player.Name.ToLower());
            }

            if(_shopMarks.ContainsKey(player.Name.ToLower()))
            {
                if (!_shopLocs.ContainsKey(player.Id)) _shopLocs.Add(player.Id, _shopMarks[player.Name.ToLower()]);
                _shopMarks.Remove(player.Name.ToLower());
            }
            // ---------------------------------------------------------------

			// Save the trade data
            SaveTradeData();
		}
		
		
		private void CheckWalletExists(Player player)
		{
			//Check if the player has a wallet yet
			if(_playerWallet.Count < 1) _playerWallet.Add(player.Id,0);
			if(!_playerWallet.ContainsKey(player.Id))
			{
				_playerWallet.Add(player.Id,0);
			}
		}

		private void CheckWalletExists(ulong playerId)
		{
			//Check if the player has a wallet yet
			if(_playerWallet.Count < 1) _playerWallet.Add(playerId,0);
			if(!_playerWallet.ContainsKey(playerId))
			{
				_playerWallet.Add(playerId,0);
			}
		}
        
		private void CheckShopExists(Player player)
		{
			//Check if the player has a wallet yet
			if(_shopPlayer.Count < 1) _shopPlayer.Add(player.Id,new Collection<string[]>());
			if(!_shopPlayer.ContainsKey(player.Id))
			{
				_shopPlayer.Add(player.Id,new Collection<string[]>());
			}
		}
		
        void Loaded()
        {
            LoadTradeData();
			_tradeDefaults = LoadDefaultTradeValues();

			//If there's no trade data stored, then set up the new trade data from the defaults
            if(_tradeList.Count < 1)
            {
                foreach(var item in _tradeDefaults)
                {
					var sellPrice = Int32.Parse(item[1]) * (_sellPercentage/100);
                    var newItem = new string[5]{ item[0], item[1], item[2], item[1], sellPrice.ToString() };
                    _tradeList.Add(newItem);
                }
            }
			
			// Start deflation timer
			timer.Repeat(PriceDeflationTime,0,DeflatePrices);
			
			//Make sure the sellPercentage hasn't been overwritten to 0 somehow!
			if(_sellPercentage == 0)
			{
				_sellPercentage = 50;
			}

            // Save the trade data
            SaveTradeData();
        }
        // ===========================================================================================================
		
#endregion

#region User Commands
        
		// View the items in a player's shop
        [ChatCommand("shophelp")]
        private void SeeTheShopCommands(Player player, string cmd)
        {
            PrintToChat(player, "[00FF00]/shop [FFFFFF] - View the current shop you are visiting.");
            PrintToChat(player, "[00FF00]/myshop [FFFFFF] - View your own shop.");
            PrintToChat(player, "[00FF00]/addshopmarker [FFFFFF] - Add a marker to create your own shop");
            PrintToChat(player, "[00FF00]/removeshopmarkers [FFFFFF] - Remove any markers you have made previously.");
            PrintToChat(player, "[00FF00]/addshopitem '<itemname>' <amount> [FFFFFF] - Add items to your shop stock.");
            PrintToChat(player, "[00FF00]/removeshopitem '<itemname>' <amount> [FFFFFF] - Remove items from your shop stock.");
            PrintToChat(player, "[00FF00]/setitemprice '<itemname>' <amount> [FFFFFF] - Set the price for your shop items.");
        }
        
		
        // View the items in a player's shop
        [ChatCommand("shop")]
        private void VisitAShop(Player player, string cmd)
        {
            ViewAPlayersShop(player, cmd);
        }
        
		// View the items in a player's shop
        [ChatCommand("setitemprice")]
        private void SetPriceForItem(Player player, string cmd, string[] input)
        {
            SetThePriceOfAnItemInYourShop(player, cmd, input);
        }
        
        // View the items in a player's shop
        [ChatCommand("addshopmarker")]
        private void AddAShopMarker(Player player, string cmd)
        {
            AddAPlayerShopMarker(player, cmd);
        }
        
        // View the items in a player's shop
        [ChatCommand("removeshopmarkers")]
        private void RemoveAShopMarker(Player player, string cmd)
        {
            RemoveAPlayerShopMarker(player, cmd);
        }

        // View the items in your shop
        [ChatCommand("myshop")]
        private void CheckMyShopStock(Player player, string cmd)
        {
            ViewMyShop(player, cmd);
        }

        // Stock items in your shop
        [ChatCommand("addshopitem")]
        private void AddAnItemToThePlayerShop(Player player, string cmd,string[] input)
        {
            AddStockToThePlayerShop(player, cmd, input);
        }

        // Remove items in your shop
        [ChatCommand("removeshopitem")]
        private void RemoveAnItemToThePlayerShop(Player player, string cmd,string[] input)
        {
            RemoveStockToThePlayerShop(player, cmd, input);
        }
        
        // Add a new trademaster
        [ChatCommand("viewtrademasters")]
        private void SeeAllTheTradeMasters(Player player, string cmd)
        {
            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : The current Trade Masters are : ");
            foreach (var tradeMaster in _tradeMasters)
            {
                PrintToChat(player, "[00FF00]" + tradeMaster);
            }
        }
        
        // Add a new trademaster
        [ChatCommand("addtrademaster")]
        private void AddTradeMasterToList(Player player, string cmd,string[] input)
        {
            AddPlayerAsATradeMaster(player, cmd, input);
        }
        
        // Add a new trademaster
        [ChatCommand("removetrademaster")]
        private void RemoveTradeMasterToList(Player player, string cmd,string[] input)
        {
            RemovePlayerAsATradeMaster(player, cmd, input);
        }
        
		// Buying an item from the exchange
        [ChatCommand("listtradedefaults")]
        private void ListTheDefaultTrades(Player player, string cmd,string[] input)
        {
            DisplayDefaultTradeList(player, cmd, input);
        }

		// Buying an item from the exchange
        [ChatCommand("setprice")]
        private void AdminSetResourcePrice(Player player, string cmd,string[] input)
        {
            SetThePriceOfAnItem(player, cmd, input);
        }
		
		// Check my wallet
        [ChatCommand("wallet")]
        private void CheckHowMuchMoneyAPlayerhas(Player player, string cmd)
        {
            CheckHowMuchGoldICurrentlyHave(player, cmd);
        }

        // Change the current sell percentage amount
		[ChatCommand("setsellpercentage")]
        private void SetTheSellingPercentageAmount(Player player, string cmd, string[] input)
		{
		    SetTheNewSellPercentageAmount(player, cmd, input);
		}

        // Set a players gold to a specific amount
		[ChatCommand("setplayergold")]
        private void SetAPlayersGoldAmount(Player player, string cmd, string[] input)
		{
		    AdminSetAPlayersGoldAmount(player, cmd, input);
		}

		// Wipe all gold from EVERY player 
		[ChatCommand("removeallgold")]
        private void SetAllPlayersGoldAmount(Player player, string cmd)
		{
		    RemoveTheGoldFromAllPlayers(player, cmd);
		}
		
        // Get the current location
		[ChatCommand("loc")]
        private void LocationCommand(Player player, string cmd, string[] args)
		{
		    GetThePlayersCurrentLocation(player, cmd, args);
		}
		
		// USE /markadd <int> to designate marks for that position
		[ChatCommand("markadd")]
        private void MarkAreaForTrade(Player player, string cmd, string[] input)
		{
		    AddTheTradeAreaMark(player, cmd, input);
		}
		
        
		// Remove all marks that have been made
		[ChatCommand("markremoveall")]
        private void RemoveAllMarkedPoints(Player player, string cmd, string[] input)
		{
		    RemoveAllMarksForTradeArea(player, cmd, input);
		}
		
		// Toggle safe area mode for trade areas
		[ChatCommand("safetrade")]
        private void MakeTradeAreasSafe(Player player, string cmd)
		{
		    ToggleTheSafeTradingArea(player, cmd);
		}
		
        
		// Buying an item from the exchange
        [ChatCommand("givecredits")]
        private void AdminGiveCredits(Player player, string cmd,string[] input)
        {
            GiveGoldToAPlayer(player, cmd, input);
        }
		
		// Check a player's credits
        [ChatCommand("checkcredits")]
        private void AdminCheckPlayerCredits(Player player, string cmd,string[] input)
        {
            CheckTheGoldAPlayerHas(player, cmd, input);
        }
		
		// Remove an item from the store
        [ChatCommand("removestoreitem")]
        private void AdminRemoveItemFromStore(Player player, string cmd,string[] input)
        {
            RemoveAnItemFromTheExchange(player, cmd, input);
        }

		// Remove all items from the store
        [ChatCommand("removeallstoreitems")]
        private void AdminRemoveAllItemsFromStore(Player player, string cmd,string[] input)
        {
            RemoveAllExchangeItems(player, cmd, input);
        }

		// Enable the PvP gold stealing
        [ChatCommand("pvpgold")]
        private void AllowGoldForPvP(Player player, string cmd)
        {
            TogglePvpGoldStealing(player, cmd);
        }
			
		// Enable the PvE gold farming
        [ChatCommand("pvegold")]
        private void AllowGoldForPvE(Player player, string cmd)
        {
            TogglePveGoldFarming(player, cmd);
        }
			
		// Remove an item from the store
        [ChatCommand("restoredefaultprices")]
        private void RevertAllPricesToDefaultValues(Player player, string cmd)
        {
            RestoreTheDefaultExchangePrices(player, cmd);
        }

		// Add an item to the store
        [ChatCommand("addstoreitem")]
        private void AdminAddItemToStore(Player player, string cmd,string[] input)
        {
            AddANewItemToTheExchange(player,cmd,input);
        }
		

        // Buying an item from the exchange
        [ChatCommand("buy")]
        private void BuyAnItem(Player player, string cmd)
        {
            BuyAnItemOnTheExchange(player, cmd);
        }
		
        
        // Selling an item on the exchange
        [ChatCommand("sell")]
        private void SellAnItem(Player player, string cmd)
        {
            SellAnItemOnTheExchange(player, cmd);
        }

        
        // View the prices of items on the exchange
        [ChatCommand("store")]
        private void ViewTheExchangeStore(Player player, string cmd)
        {
            ShowThePlayerTheGrandExchangeStore(player, cmd);
        }

#endregion

#region Private Methods

        #region LOCATION COMMANDS

        private void MarkLocation(Player player, double[] locSet, int locPosition)
        {
            double posX = player.Entity.Position.x;
            double posZ = player.Entity.Position.z;

            locSet[locPosition] = posX;
            locSet[locPosition + 1] = posZ;

            PrintToChat(player, "Position has been marked at [00FF00]" + posX.ToString() + "[FFFFFF], [00FF00]" + posZ.ToString());
        }
		
        private bool BlocksAreTooFarApart(double posX1, double posZ1, double posX2, double posZ2)
        {
            if (Math.Abs(posX2 - posX1) > 15) return true;
            if (Math.Abs(posZ2 - posZ1) > 15) return true;
            return false;
        }

        private void AddTheTradeAreaMark(Player player, string cmd, string[] input)
        {
            var newLocSet = new double[4];
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "For now, only admins can alter locations.");
                return;
            }
            if (_markList.Count > 0)
            {
                if (_markList[0][2] != 0)
                {
                    PrintToChat(player, "You have already marked two locations. Please use /markremoveall to start again.");
                    return;
                }
                PrintToChat(player, "Adding the second and final position for this area.");
                MarkLocation(player, _markList[0], 2);
                SaveTradeData();
                return;
            }

            PrintToChat(player, "Adding the first corner position for this area.");
            _markList.Add(newLocSet);
            MarkLocation(player, _markList[0], 0);

            SaveTradeData();
        }

        private void RemoveAPlayerShopMarker(Player player, string cmd)
        {
            if (!_shopLocs.ContainsKey(player.Id))
            {
                PrintToChat(player, "You do not currently have any shop markers set.");
                return;
            }

            _shopLocs.Remove(player.Id);
            PrintToChat(player, "You have removed all of your shop markers. Your shop is not accessible until you place new markers down using /addshopmarker");

            SaveTradeData();
        }

        private void RemoveAllMarksForTradeArea(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "For now, only admins can alter locations.");
                return;
            }
            _markList = new Collection<double[]>();
            PrintToChat(player, "All marks have been removed.");

            SaveTradeData();
        }

        private void GetThePlayersCurrentLocation(Player player, string cmd, string[] args)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "For now, only admins can check locations.");
                return;
            }
            PrintToChat(player, string.Format("Current Location: x:{0} y:{1} z:{2}", player.Entity.Position.x.ToString(), player.Entity.Position.y.ToString(), player.Entity.Position.z.ToString()));
        }


        #endregion

        #region OBJECT DEPLOYMENT


        private void OnObjectDeploy(NetworkInstantiateEvent e)
        {
            Player player = Server.GetPlayerById(e.SenderId);
            if (player == null) return;
            InvItemBlueprint bp = InvDefinitions.Instance.Blueprints.GetBlueprintForID(e.BlueprintId);
            //PrintToChat(player, "You have placed a " + bp.Name + ".");
            //PrintToChat(player, e.Position.x.ToString());
            //PrintToChat(player, e.Position.z.ToString());
        }

        #endregion

        #region PLAYER SHOPS


        private ulong GetPlayerWhoOwnsThisShop(Player player)
        {
            // Is there a designated trade area?
            if (_shopLocs.Count < 1) return 0;
            var isInArea = false;
            foreach (var shop in _shopLocs)
            {
                var coords = shop.Value;
                var posX1 = coords[0];
                var posZ1 = coords[1];
                var posX2 = coords[2];
                var posZ2 = coords[3];

                var playerX = player.Entity.Position.x;
                var playerZ = player.Entity.Position.z;

                if ((playerX < posX1 && playerX > posX2) && (playerZ > posZ1 && playerZ < posZ2)) isInArea = true;
                if ((playerX < posX1 && playerX > posX2) && (playerZ < posZ1 && playerZ > posZ2)) isInArea = true;
                if ((playerX > posX1 && playerX < posX2) && (playerZ < posZ1 && playerZ > posZ2)) isInArea = true;
                if ((playerX > posX1 && playerX < posX2) && (playerZ > posZ1 && playerZ < posZ2)) isInArea = true;
                if (isInArea)
                {
                    return shop.Key;
                }
            }

            return 0;
        }

        private void AddAPlayerShopMarker(Player player, string cmd)
        {
            var newLocSet = new double[4];

            // Check if the player has a shop yet
            CheckIfPlayerOwnsAShop(player);

            // Check if the player has a shop marker set up
            if (!_shopLocs.ContainsKey(player.Id))
            {
                _shopLocs.Add(player.Id, newLocSet);
            }

            // Check if this is a marked area already
            if (GetPlayerWhoOwnsThisShop(player) != 0)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : There is already a shop in this area.");
                return;
            }

            var myMarks = _shopLocs[player.Id];

            if (myMarks[0] != 0)
            {
                if (myMarks[2] != 0)
                {
                    PrintToChat(player, "You have already marked two locations. Please use /removeshopmarkers to start again.");
                    return;
                }
                //Checkif the next block is close enough
                if (BlocksAreTooFarApart(myMarks[0], myMarks[1], player.Entity.Position.x, player.Entity.Position.z))
                {
                    PrintToChat(player, "This area is too big for your shop. It can only be a maximum size of 13x13 blocks.");
                    return;
                }

                PrintToChat(player, "Added the second and final corner position for your shop.");
                MarkLocation(player, myMarks, 2);
                SaveTradeData();
                return;
            }

            PrintToChat(player, "Adding the first corner position for your shop. You will now need to add the OPPOSITE corner for your shop as well.");
            _markList.Add(newLocSet);
            MarkLocation(player, myMarks, 0);

            SaveTradeData();
        }

        private void CheckIfPlayerOwnsAShop(Player player)
        {
            if (!_shopPlayer.ContainsKey(player.Id))
            {
                _shopPlayer.Add(player.Id, new Collection<string[]>());
            }
            SaveTradeData();
        }
		
        private void ViewAPlayersShop(Player player, string cmd)
		{
			ShowTheShopListForWhereThisPlayerIsStanding(player);
		}

        private void ViewThisShop(Player player)
        {
            ShowTheShopListForWhereThisPlayerIsStanding(player);
        }

        private void SetThePriceOfAnItemInYourShop(Player player, string cmd, string[] input)
		{
			// Find this player's shop
            if (!_shopPlayer.ContainsKey(player.Id))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You don't appear to currently own a shop.");
                return;
            }
			
            if(input.Length<2)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Please use the format: /setprice \"Item Name\" <amount>.");
                return;
            }

            var resource = Capitalise(input[0]);

			// Check if the item exists in the default list
            var found = false;
			var previousItem = new string[5];
            var defaultPrice = 0;
			for(var i=0; i<_tradeDefaults.Count; i++)
			{
				if(_tradeDefaults[i][0].ToLower() == resource.ToLower())
				{
					found = true;
				    defaultPrice = Int32.Parse(_tradeDefaults[i][1]);
					break;
				}
			}
			if(!found)
			{
				PrintToChat(player, resource + " does not appear to be a recognised item. Did you spell it correctly and use quotes around the item name?");
				return;
			}


            int amount;
            if (Int32.TryParse(input[1], out amount) == false)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You entered an invalid amount. Please use the format - /setitemprice '<item_name>' <amount>");
                return;
            }
			
			
			CheckShopExists(player);
			var myShop = _shopPlayer[player.Id];
			foreach(var item in myShop)
			{
				if(item[0].ToLower() == resource.ToLower())
				{
					item[1] = (amount * PriceModifier).ToString();
				}
			}
			
			SaveTradeData();
			PrintToChat(player,"You have updated your shop prices.");
		}

        private void ShowTheShopListForWhereThisPlayerIsStanding(Player player)
        {
            // Is the player in a shop area?
            var shopOwnerName = GetPlayerWhoOwnsThisShop(player);
            if (shopOwnerName == 0)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : There does not appear to be a shop here.");
                return;
            }
			
            // Find this player's shop
            if (!_shopPlayer.ContainsKey(shopOwnerName))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : This shop doesn't appear to be open right now.");
                return;
            }

            var myShop = _shopPlayer[shopOwnerName];

             // Are there any items on the store?
            if(myShop.Count < 1)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : This shop is currently closed for business and has no items available.");
                return;
            }

			// Get the player's wallet contents
			CheckWalletExists(player);
			var credits = _playerWallet[player.Id];
			
            var buyIcon = "[008888]";
            var sellIcon = "[008888]";
            var itemText = "";
            var itemsPerPage = 25;
			var singlePage = false;

			if(itemsPerPage > myShop.Count) 
			{
				singlePage = true;
				itemsPerPage = myShop.Count;
			}
			
            for(var i = 0; i<itemsPerPage;i++)
            {
				buyIcon = "[00FF00]";
                var resource = myShop[i][0];
				var stockAmount = Int32.Parse(myShop[i][2]);
                var buyPrice = Int32.Parse(myShop[i][1]) / PriceModifier;
				var buyPriceText = buyPrice.ToString();
                resource = Capitalise(resource);

                itemText = itemText + "[00FFFF]" + resource + " [FFFFFF]( [FF0000]" + stockAmount.ToString() + "[FFFFFF] );  Price: " + buyIcon + buyPriceText + "[FFFF00]g\n";
            }
			
			itemText = itemText + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString() + "[FFFF00]g";

            var shopName = "Local Store";

            // Show the Shop
			player.ShowConfirmPopup(shopName, itemText, "Buy", "Exit", (selection, dialogue, data) => BuyItemFromPlayerShop(player, shopOwnerName, myShop, selection, dialogue, data, itemsPerPage, itemsPerPage));

        }


        private void BuyItemFromPlayerShop(Player player, ulong shopOwnerName, Collection<string[]> myShop,
            Options selection, Dialogue dialogue, object contextData, int itemsPerPage, int currentItemCount)
        {
            if (selection != Options.Yes)
            {
                //Leave
                return;
            }

            //Open up the buy screen
			player.ShowInputPopup(shopOwnerName + "'s Store", "What [00FF00]item [FFFFFF]would you like to buy at this store?", "", "Submit", "Cancel", (options, dialogue1, data) => SelectItemToBeBoughtFromPlayer(player, shopOwnerName, options, dialogue1, data));
        }

        
		private void SelectItemToBeBoughtFromPlayer(Player player, ulong shopOwnerName, Options selection, Dialogue dialogue, object contextData)
		{
			if (selection == Options.Cancel)
            {
                //Leave
                return;
            }
			var requestedResource = dialogue.ValueMessage;
			var resourceFound = false;
			var resourceDetails = new string[3];

		    var myShop = _shopPlayer[shopOwnerName];
			
			// Get the resource's details
			foreach(var item in myShop)
			{
				if(item[0] == Capitalise(requestedResource))
				{
					resourceDetails = new string[3]{ item[0],item[1],item[2] };
					resourceFound = true;
				}
			}

            // CHeck the maximum stack size
		    var maxStack = 0;
		    foreach (var tradeDefault in _tradeDefaults)
		    {
		        if(tradeDefault[0] == Capitalise(requestedResource))
		        {
		            maxStack = Int32.Parse(tradeDefault[2]);
		        }
		    }
			
			// I couldn't find the resource you wanted!
			if(!resourceFound)
			{
				PrintToChat(player,"[FF0000]Grand Exchange[FFFFFF] : That item does not appear to currently be for sale at this store!");
				return;
			}
			
			// Open a popup with the resource details
			var message = "Yes, we have that!\n[00FF00]" + Capitalise(resourceDetails[0]) + "[FFFFFF] is currently selling for [00FFFF]" + (Int32.Parse(resourceDetails[1])/PriceModifier).ToString() + "[FFFF00]g[FFFFFF] per item.\nIt can be bought in stacks of up to [00FF00]" + maxStack.ToString() + "[FFFFFF].\n How much would you like to buy?";
			
			// Get the player's wallet contents
			CheckWalletExists(player);
			var credits = _playerWallet[player.Id];
			message = message + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();
			
			player.ShowInputPopup(shopOwnerName + "'s Store", message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectAmountToBeBoughtFromPlayerStore(player, shopOwnerName, maxStack, options, dialogue1, data, resourceDetails));
		}
		
        
		private void SelectAmountToBeBoughtFromPlayerStore(Player player, ulong shopOwnerName, int maxStack, Options selection, Dialogue dialogue, object contextData, string[] resourceDetails)
		{
			if (selection == Options.Cancel)
            {
                //Leave
                return;
            }
			var amountText = dialogue.ValueMessage;

			// Check if the amount is an integer
			int amount;
			if(Int32.TryParse(amountText,out amount) == false)
			{
				PrintToChat(player,"[FF0000]Grand Exchange[FFFFFF] : That does not appear to be a valid amount. Please enter a number between 1 and the maximum stack size.");
				return;
			}
			
			//Check if the amount is within the correct limits
			if(amount < 1 || amount > Int32.Parse(resourceDetails[2]) || amount > maxStack)
			{
				PrintToChat(player,"[FF0000]Grand Exchange[FFFFFF] : You can only purchase an amount between 1 and the maximum stack size or amount in the store.");
				return;
			}

            // Dict <string, Collect<string[]>>
		    double totalValue = (int)((double)(Int32.Parse(resourceDetails[1]) * amount) / 1000);

			var message = "Very good!\n[00FFFF]" + amount.ToString() + " [00FF00]" + Capitalise(resourceDetails[0]) + "[FFFFFF] will cost you a total of \n[FF0000]" + totalValue.ToString() + " [FFFF00]gold.[FFFFFF]\n Do you want to complete the purchase?";
			
			// Get the player's wallet contents
			CheckWalletExists(player);
			var credits = _playerWallet[player.Id];
			message = message + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();
			
			//Show Popup with the final price
			player.ShowConfirmPopup(shopOwnerName + "'s Store", message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerCanAffordThis(player, shopOwnerName, options, dialogue, data, resourceDetails, totalValue, amount));
		}
        
        private void ViewMyShop(Player player , string cmd )
        {
			//Check if I have a shop
			CheckIfPlayerOwnsAShop(player);
			
            // Is the player in a shop area?
            var shopOwnerName = GetPlayerWhoOwnsThisShop(player);
            if (shopOwnerName == 0)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You need to be in your shop to view the items there!");
                return;
            }

            var playerName = player.Name;

            // Build the shop if it doesn't exist
            CheckShopExists(player);

            //Is there anything in the shop right now?
            if (_shopPlayer[player.Id].Count < 1)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Your shop is currently empty.");
                return;
            }
            ViewThisShop(player);
        }

		private void RemoveStockToThePlayerShop(Player player, string cmd , string[] input)
		{
			//Check if I have a shop
			CheckIfPlayerOwnsAShop(player);

            var playerName = player.Name;
            // Is the player in a shop area?
            var shopOwnerName = GetPlayerWhoOwnsThisShop(player);
            if (shopOwnerName == 0 || shopOwnerName != player.Id)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You need to be in your shop to remove the items from there!");
                return;
            }
			
            if (input.Length < 2)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Please use the format - /removeshopitem '<item_name>' <amount>");
                return;
            }

            int amount;
			if(Int32.TryParse(input[1], out amount) == false)
			{	
				PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You entered an invalid amount. Please use the format - /removeshopitem '<item_name>' <amount>");
				return;
			}
			
            var resource = Capitalise(input[0]);

			// Check if the item exists in the default list
            var found = false;
			var previousItem = new string[5];
            var defaultPrice = 0;
			for(var i=0; i<_tradeDefaults.Count; i++)
			{
				if(_tradeDefaults[i][0].ToLower() == resource.ToLower())
				{
					found = true;
				    defaultPrice = Int32.Parse(_tradeDefaults[i][1]);
					break;
				}
			}
			if(!found)
			{
				PrintToChat(player, resource + " does not appear to be a recognised item. Did you spell it correctly and use quotes around the item name?");
				return;
			}
			
			//Check if the item is in the shop
			var stock = _shopPlayer[player.Id];
			for(var i=0; i < stock.Count; i++)
			{
				if(stock[i][0].ToLower() == resource.ToLower())
				{
					if(Int32.Parse(stock[i][2]) < amount)
					{
						PrintToChat(player, "You don't appear to have that much of the resource in the shop!");
						return;
					}
					//Is there space?
					var inventory = player.GetInventory().Contents;
					
					if(inventory.FreeSlotCount < 5)
					{
						PrintToChat(player, "You will need 5 empty slots to guarantee space for shop item stacks. Sorry, this is important!");
						return;
					}
					
					// Give the item!
					var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resource, true, true);
					var invGameItemStack = new InvGameItemStack(blueprintForName, amount, null);
					ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
					
					//Remove from shop
					var currentStock = Int32.Parse(stock[i][2]);
					if(currentStock > amount) 
					{
						stock[i][2] = (currentStock - amount).ToString();
					}
					else stock.RemoveAt(i);
					
					PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You have withdrawn the items from the shop.");
					SaveTradeData();
				}
			}
		}
		
        private void AddStockToThePlayerShop(Player player , string cmd , string[] input)
        {
			//Check if I have a shop
			CheckIfPlayerOwnsAShop(player);

            var playerName = player.Name;
            var playerId = player.Id;
            // Is the player in a shop area?
            var shopOwnerName = GetPlayerWhoOwnsThisShop(player);
            if (shopOwnerName == 0 || shopOwnerName != playerId)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You need to be in your shop to view the items there!");
                return;
            }

            if (input.Length < 2)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Please use the format - /addshopitem '<item_name>' <amount>");
                return;
            }

            int amount;
			if(Int32.TryParse(input[1], out amount) == false)
			{	
				PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You entered an invalid amount. Please use the format - /addshopitem '<item_name>' <amount>");
				return;
			}

            var resource = Capitalise(input[0]);

			// Check if the item exists in the default list
            var found = false;
			var previousItem = new string[5];
            var defaultPrice = 0;
			for(var i=0; i<_tradeDefaults.Count; i++)
			{
				if(_tradeDefaults[i][0].ToLower() == resource.ToLower())
				{
					found = true;
				    defaultPrice = Int32.Parse(_tradeDefaults[i][1]);
					break;
				}
			}
			if(!found)
			{
				PrintToChat(player, resource + " does not appear in the original defaults list. If you want this item added, please ask an admin to add it to the defaults list first. [FF0000]Note :[FFFFFF] It MUST be a real item that exists in the game currently.");
				return;
			}

            // We have the item and the amount. Does the player have this in his inventory to stock?
            if(CanRemoveResource(player, resource, amount) == false)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You don't appear to have that much resource in your inventory right now.");
				return;
            }
            
            // Add this resource to the player's shop
            if (_shopPlayer.ContainsKey(playerId))
            {
                var shop = _shopPlayer[playerId];
                var stock = new string[3];

                //If the item already exists, add it to the current stock
                var position = 0;
                var itemFound = false;
				
                foreach (var item in shop)
                {
                    if (item[0].ToLower() == resource.ToLower())
                    {
                        itemFound = true;
                        var stackLimit = 0;
                        var stockAmount = Int32.Parse(item[2]);
                        foreach (var defaultItem in _tradeDefaults)
                        {
                            if (defaultItem[0].ToLower() == item[0].ToLower())
                            {
                                stackLimit = Int32.Parse(defaultItem[2]);
                            }
                        }
                        
						
                        //If the limit is full
                        var stockMaxLimit = PlayerShopStackLimit * stackLimit;
                        if (Int32.Parse(item[2]) >= stockMaxLimit)
                        {
                            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You cannot stock any more of that item right now.");
            				return;
                        }
                        
                        //If this will hit the limit
                        if (stockAmount + amount > stockMaxLimit)
                        {
                            amount = stockMaxLimit - stockAmount;
                            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Your shop is now fully stocked with " + resource);
                        }
                        var finalAmount = stockAmount + amount;

                        stock[0] = resource;
                        item[1] = defaultPrice.ToString();
                        item[2] = finalAmount.ToString();
                    }
                }
                if (!itemFound)
                {
								
					// If the shop has it's full limit of items
					if(shop.Count >= PlayerShopMaxSlots)
					{
						PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You cannot any new items to your store.");
            			return;
					}

                    stock[0] = resource;
                    stock[1] = defaultPrice.ToString();
                    stock[2] = amount.ToString();
                    shop.Add(stock);
                }
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You have updated your shop inventory!");
            }

            //Remove the resource from the player's inventory
            RemoveItemsFromInventory(player, resource, amount);

			//Save the data
			SaveTradeData();
        }

        #endregion

        #region GRAND EXCHANGE


        private void SelectItemToBeBought(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            if (selection == Options.Cancel)
            {
                //Leave
                return;
            }
            var requestedResource = dialogue.ValueMessage;
            var resourceFound = false;
            var resourceDetails = new string[5];

            // Get the resource's details
            foreach (var item in _tradeList)
            {
                if (item[0] == Capitalise(requestedResource))
                {
                    resourceDetails = new string[5] { item[0], item[1], item[2], item[3], item[4] };
                    resourceFound = true;
                }
            }

            // I couldn't find the resource you wanted!
            if (!resourceFound)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : That item does not appear to currently be for sale!");
                return;
            }

            // Open a popup with the resource details
            var message = "Of course!\n[00FF00]" + Capitalise(resourceDetails[0]) + "[FFFFFF] is currently selling for [00FFFF]" + (Int32.Parse(resourceDetails[3]) / 1000).ToString() + "[FFFF00]g[FFFFFF] per item.\nIt can be bought in stacks of up to [00FF00]" + resourceDetails[2].ToString() + "[FFFFFF].\n How much would you like to buy?";

            // Get the player's wallet contents
            CheckWalletExists(player);
            var credits = _playerWallet[player.Id];
            message = message + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();

            player.ShowInputPopup("Grand Exchange", message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectAmountToBeBought(player, options, dialogue1, data, resourceDetails));
        }

        private void SelectAmountToBeBought(Player player, Options selection, Dialogue dialogue, object contextData, string[] resourceDetails)
        {
            if (selection == Options.Cancel)
            {
                //Leave
                return;
            }
            var amountText = dialogue.ValueMessage;

            // Check if the amount is an integer
            int amount;
            if (Int32.TryParse(amountText, out amount) == false)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : That does not appear to be a valid amount. Please enter a number between 1 and the maximum stack size.");
                return;
            }

            //Check if the amount is within the correct limits
            if (amount < 1 || amount > Int32.Parse(resourceDetails[2]))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You can only purchase an amount between 1 and the maximum stack size.");
                return;
            }

            double totalValue = (double)GetPriceForThisItem("buy", resourceDetails[0], amount);

            var message = "Very good!\n[00FFFF]" + amount.ToString() + " [00FF00]" + Capitalise(resourceDetails[0]) + "[FFFFFF] will cost you a total of \n[FF0000]" + totalValue + " [FFFF00]gold.[FFFFFF]\n Do you want to complete the purchase?";

            // Get the player's wallet contents
            CheckWalletExists(player);
            var credits = _playerWallet[player.Id];
            message = message + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();

            //Show Popup with the final price
            player.ShowConfirmPopup("Grand Exchange", message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerCanAffordThis(player, 0, options, dialogue, data, resourceDetails, totalValue, amount));
        }

        private void CheckIfThePlayerHasTheResourceToSell(Player player, Options selection, Dialogue dialogue, object contextData, string[] resourceDetails, int totalValue, int amount)
        {
            if (selection != Options.Yes)
            {
                //Leave
                return;
            }

            if (!CanRemoveResource(player, resourceDetails[0], amount))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : It looks like you don't have the goods! What are you trying to pull here?");
                return;
            }

            // Take the item!
            RemoveItemsFromInventory(player, resourceDetails[0], amount);

            // Give the payment
            GiveGold(player, totalValue);

            // Fix themarket price adjustment
            AdjustMarketPrices("sell", resourceDetails[0], amount);

            // Tell the player
            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : " + amount.ToString() + " " + resourceDetails[0] + "has been removed from your inventory and your wallet has been credited for the sale.");
            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Thanks for your custom, friend! Please come again!");

            //Save the data
            SaveTradeData();
        }

        private bool CanRemoveResource(Player player, string resource, int amount)
        {
            // Check player's inventory
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);

            // Check how much the player has
            var foundAmount = 0;
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if (item.Name == resource)
                {
                    foundAmount = foundAmount + item.StackAmount;
                }
            }

            if (foundAmount >= amount) return true;
            return false;
        }

        public void RemoveItemsFromInventory(Player player, string resource, int amount)
        {
            var inventory = player.GetInventory().Contents;

            // Check how much the player has
            var amountRemaining = amount;
            var removeAmount = amountRemaining;
            foreach (InvGameItemStack item in inventory.Where(item => item != null))
            {
                if (item.Name == resource)
                {
                    removeAmount = amountRemaining;

                    //Check if there is enough in the stack
                    if (item.StackAmount < amountRemaining)
                    {
                        removeAmount = item.StackAmount;
                    }

                    amountRemaining = amountRemaining - removeAmount;

                    inventory.SplitItem(item, removeAmount, true);
                    if (amountRemaining <= 0) return;
                }
            }
        }

        private void CheckIfThePlayerCanAffordThis(Player player, ulong shopOwnerName, Options selection, Dialogue dialogue, object contextData, string[] resourceDetails, double totalValue, int amount)
        {
            if (selection != Options.Yes)
            {
                //Leave
                return;
            }

            if (!CanRemoveGold(player, (int)totalValue))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : It looks like you don't have the gold for this transaction, I'm afraid!");
                return;
            }

            //Check if there is space in the player's inventory
            var inventory = player.GetInventory().Contents;
            if (inventory.FreeSlotCount < 1)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You need a free inventory slot to purchase items, I'm afraid. Come back when you have made some space.");
                return;
            }

            // Check the items haven't just been bought
            if (shopOwnerName != 0)
            {
                var thisShop = _shopPlayer[shopOwnerName];
                if (thisShop.Count < 1)
                {
                    PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : I'm terribly sorry, but someone has just bought that item!");
                    return;
                }
                foreach (var shopItem in thisShop)
                {
                    if (shopItem[0].ToLower() == resourceDetails[0].ToLower())
                    {
                        if (Int32.Parse(shopItem[2]) < amount)
                        {
                            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : I'm terribly sorry, but someone has just bought that item!");
                            return;
                        }
                    }
                }
            }


            // Give the item!
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(resourceDetails[0], true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForName, amount, null);
            ItemCollection.AutoMergeAdd(inventory, invGameItemStack);

            // Take the payment
            RemoveGold(player, (int)totalValue);

            // Pay the shopkeeper
            if (shopOwnerName != 0)
            {
                GiveGold(shopOwnerName, (int)totalValue);
            }

            if (shopOwnerName == 0)
            {
                // Fix themarket price adjustment
                AdjustMarketPrices("buy", resourceDetails[0], amount);
            }
            else
            {
                // Remove the items from the shop
                var myShop = _shopPlayer[shopOwnerName];

                for (var i = 0; i < myShop.Count; i++)
                {
                    if (myShop[i][0].ToLower() == resourceDetails[0].ToLower())
                    {
                        //If there is enough in this stack
                        if (Int32.Parse(myShop[i][2]) >= amount)
                        {
                            myShop[i][2] = (Int32.Parse(myShop[i][2]) - amount).ToString();

                            //Remove the stock if run out
                            if (myShop[i][2] == "0")
                            {
                                myShop.RemoveAt(i);
                            }

                            SaveTradeData();
                            break;
                        }
                    }
                }
            }

            // Tell the player
            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : " + amount.ToString() + " " + resourceDetails[0] + " has been added to your inventory and your wallet has been debited the appropriate amount.");
            PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : Congratulations on your purchase. Please come again!");

            //Save the data
            SaveTradeData();
        }


        private int GetPriceForThisItem(string type, string resource, int amount)
        {
            var position = 3;
            if (type == "sell") position = 4;

            var total = 0;
            foreach (var item in _tradeList)
            {
                if (item[0].ToLower() == resource.ToLower())
                {
                    total = (int)(amount * (double)(Int32.Parse(item[position]) / PriceModifier));
                }
            }
            return total;
        }

        private void SelectItemToBeSold(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            if (selection == Options.Cancel)
            {
                //Leave
                return;
            }
            var requestedResource = dialogue.ValueMessage;
            var resourceFound = false;
            var resourceDetails = new string[5];

            // Get the resource's details
            foreach (var item in _tradeList)
            {
                if (item[0] == Capitalise(requestedResource))
                {
                    resourceDetails = new string[5] { item[0], item[1], item[2], item[3], item[4] };
                    resourceFound = true;
                }
            }

            // I couldn't find the resource you wanted!
            if (!resourceFound)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : I don't think I am currently able to take that item at this time.'");
                return;
            }

            // Open a popup with the resource details
            var message = "Hmmm!\nI believe that [00FF00]" + Capitalise(resourceDetails[0]) + "[FFFFFF] is currently being purchased for [00FFFF]" + (Int32.Parse(resourceDetails[4]) / 1000).ToString() + "[FFFF00]g[FFFFFF] per item.\nI'd be happy to buy this item in stacks of up to [00FF00]" + resourceDetails[2].ToString() + "[FFFFFF].\n How much did you want to sell?";

            // Get the player's wallet contents
            CheckWalletExists(player);
            var credits = _playerWallet[player.Id];
            message = message + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();

            player.ShowInputPopup("Grand Exchange", message, "", "Submit", "Cancel", (options, dialogue1, data) => SelectAmountToBeSold(player, options, dialogue1, data, resourceDetails));
        }

        private void SelectAmountToBeSold(Player player, Options selection, Dialogue dialogue, object contextData, string[] resourceDetails)
        {
            if (selection == Options.Cancel)
            {
                //Leave
                return;
            }
            var amountText = dialogue.ValueMessage;

            // Check if the amount is an integer
            int amount;
            if (Int32.TryParse(amountText, out amount) == false)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : That does not appear to be a valid amount. Please enter a number between 1 and the maximum stack size.");
                return;
            }

            //Check if the amount is within the correct limits
            if (amount < 1 || amount > Int32.Parse(resourceDetails[2]))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You can only sell an amount of items between 1 and the maximum stack size for that item.");
                return;
            }

            var totalValue = GetPriceForThisItem("sell", resourceDetails[0], amount);

            var message = "I suppose I can do that.\n[00FFFF]" + amount.ToString() + " [00FF00]" + Capitalise(resourceDetails[0]) + "[FFFFFF] will give you a total of \n[FF0000]" + totalValue + " [FFFF00]gold.[FFFFFF]\n Do you want to complete the sale?";

            // Get the player's wallet contents
            CheckWalletExists(player);
            var credits = _playerWallet[player.Id];
            message = message + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();

            //Show Popup with the final price
            player.ShowConfirmPopup("Grand Exchange", message, "Submit", "Cancel", (options, dialogue1, data) => CheckIfThePlayerHasTheResourceToSell(player, options, dialogue, data, resourceDetails, totalValue, amount));
        }


        private void ShowThePlayerTheGrandExchangeStore(Player player, string cmd)
        {
            // Are there any items on the store?
            if (_tradeList.Count == 0)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : The Grand Exchange is currently closed for business. Please try again later.");
                return;
            }
            //Log("Trade: Prices have been found!");

            // Get the player's wallet contents
            CheckWalletExists(player);
            var credits = _playerWallet[player.Id];

            // Check if player exists (For Unit Testing)
            var buyIcon = "[008888]";
            var sellIcon = "[008888]";
            var itemText = "";
            var itemsPerPage = 25;
            var singlePage = false;
            if (itemsPerPage > _tradeList.Count)
            {
                singlePage = true;
                itemsPerPage = _tradeList.Count;
            }

            for (var i = 0; i < itemsPerPage; i++)
            {
                buyIcon = "[008888]";
                sellIcon = "[008888]";
                var resource = _tradeList[i][0];
                var originalPrice = Int32.Parse(_tradeList[i][1]) / PriceModifier;
                var originalSellPrice = (int)((double)originalPrice * (_sellPercentage / 100));
                var stackLimit = Int32.Parse(_tradeList[i][2]);
                var buyPrice = Int32.Parse(_tradeList[i][3]) / PriceModifier;
                var sellPrice = Int32.Parse(_tradeList[i][4]) / PriceModifier;

                if (buyPrice >= originalPrice) buyIcon = "[00FF00]";
                if (buyPrice > originalPrice + (originalPrice * 0.1)) buyIcon = "[888800]";
                if (buyPrice > originalPrice + (originalPrice * 0.2)) buyIcon = "[FF0000]";
                if (sellPrice <= originalSellPrice) sellIcon = "[00FF00]";
                if (sellPrice < originalSellPrice - (originalSellPrice * 0.1)) sellIcon = "[888800]";
                if (sellPrice < originalSellPrice - (originalSellPrice * 0.2)) sellIcon = "[FF0000]";
                // if(buyPrice < originalPrice + (originalPrice * 0.3)) buyIcon = "[00FF00]";
                // if(sellPrice > originalPrice) sellIcon = "[FF0000]";
                // if(sellPrice < originalPrice) sellIcon = "[00FF00]";
                var buyPriceText = buyPrice.ToString();
                var sellPriceText = sellPrice.ToString();


                itemText = itemText + "[888800]" + Capitalise(resource) + "[FFFFFF]; Buy: " + buyIcon + buyPriceText + "[FFFF00]g  [FFFFFF]Sell: " + sellIcon + sellPriceText + "[FFFF00]g\n";
            }

            itemText = itemText + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();

            if (singlePage)
            {
                player.ShowPopup("Grand Exchange", itemText, "Exit", (selection, dialogue, data) => DoNothing(player, selection, dialogue, data));
                return;
            }

            //Display the Popup with the price
            player.ShowConfirmPopup("Grand Exchange", itemText, "Next Page", "Exit", (selection, dialogue, data) => ContinueWithTradeList(player, selection, dialogue, data, itemsPerPage, itemsPerPage));
        }

        private void ContinueWithTradeList(Player player, Options selection, Dialogue dialogue, object contextData, int itemsPerPage, int currentItemCount)
        {
            if (selection != Options.Yes)
            {
                //Leave
                return;
            }

            if ((currentItemCount + itemsPerPage) > _tradeList.Count)
            {
                itemsPerPage = _tradeList.Count - currentItemCount;
            }

            // Get the player's wallet contents
            CheckWalletExists(player);
            var credits = _playerWallet[player.Id];

            var buyIcon = "[008888]";
            var sellIcon = "[008888]";
            var itemText = "";

            for (var i = currentItemCount; i < itemsPerPage + currentItemCount; i++)
            {
                buyIcon = "[008888]";
                sellIcon = "[008888]";
                var resource = _tradeList[i][0];
                var originalPrice = Int32.Parse(_tradeList[i][1]) / PriceModifier;
                var stackLimit = Int32.Parse(_tradeList[i][2]);
                var buyPrice = Int32.Parse(_tradeList[i][3]) / PriceModifier;
                var sellPrice = Int32.Parse(_tradeList[i][4]) / PriceModifier;

                if (buyPrice >= originalPrice) buyIcon = "[00FF00]";
                if (buyPrice > originalPrice + (originalPrice * 0.1)) buyIcon = "[888800]";
                if (buyPrice > originalPrice + (originalPrice * 0.2)) buyIcon = "[FF0000]";
                if (sellPrice <= originalPrice) sellIcon = "[00FF00]";
                if (sellPrice < originalPrice - (originalPrice * 0.1)) sellIcon = "[888800]";
                if (sellPrice < originalPrice - (originalPrice * 0.2)) sellIcon = "[FF0000]";
                var buyPriceText = buyPrice.ToString();
                var sellPriceText = sellPrice.ToString();


                itemText = itemText + "[888800]" + Capitalise(resource) + "[FFFFFF]; Buy: " + buyIcon + buyPriceText + "[FFFF00]g  [FFFFFF]Sell: " + sellIcon + sellPriceText + "[FFFF00]g\n";
            }

            itemText = itemText + "\n\n[FF0000]Gold Available[FFFFFF] : [00FF00]" + credits.ToString();

            currentItemCount = currentItemCount + itemsPerPage;

            // Display the Next page
            if (currentItemCount < _tradeList.Count)
            {
                player.ShowConfirmPopup("Grand Exchange", itemText, "Next Page", "Exit", (options, dialogue1, data) => ContinueWithTradeList(player, options, dialogue1, data, itemsPerPage, currentItemCount));
            }
            else
            {
                PlayerExtensions.ShowPopup(player, "Grand Exchange", itemText, "Yes", (newselection, dialogue2, data) => DoNothing(player, newselection, dialogue2, data));
            }
        }

        private void DoNothing(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            //Do nothing
        }

        private void ForcePriceAdjustment()
        {
            foreach (var item in _tradeList)
            {
                var originalPrice = Int32.Parse(item[1]);
                item[4] = (originalPrice * (_sellPercentage / 100)).ToString();
            }
            SaveTradeData();
        }

        private void DeflatePrices()
        {
            int newBuyPrice = 0;
            int newSellPrice = 0;
            int priceBottomShelf = 0;
            int priceTopShelf = 0;
            string resource = "";

            foreach (var item in _tradeList)
            {
                var buyPrice = Int32.Parse(item[3]);
                var sellPrice = Int32.Parse(item[4]);
                var maxStackSize = Int32.Parse(item[2]);
                var originalPrice = Int32.Parse(item[1]);

                double inflationModifier = Inflation / 100;
                double deflationModifier = MaxDeflation / 100;
                double stackModifier = 1;
                newBuyPrice = (int)(buyPrice - ((originalPrice * inflationModifier) * stackModifier));
                newSellPrice = (int)(sellPrice + ((originalPrice * inflationModifier) * stackModifier));

                // Make sure it doesn't fall below expected levels
                priceBottomShelf = (int)(originalPrice - ((originalPrice * deflationModifier) * stackModifier));
                priceTopShelf = (int)((double)(originalPrice + ((originalPrice * deflationModifier) * stackModifier)) * (double)(_sellPercentage / 100));

                if (newBuyPrice < priceBottomShelf) newBuyPrice = priceBottomShelf;
                if (newSellPrice > priceTopShelf) newSellPrice = priceTopShelf;

                //Update the current price
                item[3] = newBuyPrice.ToString();
                item[4] = newSellPrice.ToString();

                resource = item[0];
            }

            // Save the trade data
            SaveTradeData();
        }

        private void AdjustMarketPrices(string type, string resource, int amount)
        {
            var recordNumber = 0;
            var newResource = new string[5];
            double inflationModifier = 0;
            double buyPrice = 0;
            double sellPrice = 0;
            double stackModifier = 0;

            for (var i = 0; i < _tradeList.Count; i++)
            {
                if (_tradeList[i][0].ToLower() == resource.ToLower())
                {
                    var originalPrice = Int32.Parse(_tradeList[i][1]);
                    var newBuyPrice = Int32.Parse(_tradeList[i][3]);
                    var newSellPrice = Int32.Parse(_tradeList[i][4]);
                    var maxStackSize = Int32.Parse(_tradeList[i][2]);
                    recordNumber = i;

                    //Update for "Buy"
                    if (type == "buy")
                    {
                        //When resource is bought, increase buy price and decrease sell price for EVERY single item bought
                        inflationModifier = Inflation / 100;
                        sellPrice = (double)newSellPrice;
                        buyPrice = (double)newBuyPrice;
                        stackModifier = (double)amount / (double)maxStackSize;
                        newBuyPrice = (int)(buyPrice + ((originalPrice * inflationModifier) * stackModifier));
                        //newSellPrice = (int)(sellPrice + ((originalPrice * inflationModifier) * stackModifier));
                        //if(newSellPrice > originalPrice) newSellPrice = originalPrice;
                        //Adjust by the sellPercentage
                        //newSellPrice = (int)(newSellPrice * (double)(sellPercentage / 100));
                    }

                    //Update for "Sell"
                    if (type == "sell")
                    {
                        //When resource is sold, increase sell price and decrease buy price for EVERY single item bought
                        inflationModifier = Inflation / 100;
                        sellPrice = (double)newSellPrice;
                        buyPrice = (double)newBuyPrice;
                        stackModifier = (double)amount / (double)maxStackSize;
                        newSellPrice = (int)(sellPrice - ((originalPrice * inflationModifier) * stackModifier));
                    }

                    // Make sure prices don't go below 1gp!
                    if (newBuyPrice <= (1 * PriceModifier)) newBuyPrice = (1 * PriceModifier);
                    if (newSellPrice <= (1 * PriceModifier)) newSellPrice = (1 * PriceModifier);

                    newResource = new string[5] { _tradeList[i][0], _tradeList[i][1], _tradeList[i][2], newBuyPrice.ToString(), newSellPrice.ToString() };
                }
            }

            if (newResource.Length < 1) return;
            _tradeList.RemoveAt(recordNumber);
            _tradeList.Insert(recordNumber, newResource);

            // Save the data
            SaveTradeData();
        }
		
        private bool PlayerIsInTheRightTradeArea(Player player)
        {
            // Is there a designated trade area?
            if (_markList.Count < 1) return true;
            var isInArea = false;
            foreach (var area in _markList)
            {
                var posX1 = area[0];
                var posZ1 = area[1];
                var posX2 = area[2];
                var posZ2 = area[3];

                var playerX = player.Entity.Position.x;
                var playerZ = player.Entity.Position.z;

                if ((playerX < posX1 && playerX > posX2) && (playerZ > posZ1 && playerZ < posZ2)) isInArea = true;
                if ((playerX < posX1 && playerX > posX2) && (playerZ < posZ1 && playerZ > posZ2)) isInArea = true;
                if ((playerX > posX1 && playerX < posX2) && (playerZ < posZ1 && playerZ > posZ2)) isInArea = true;
                if ((playerX > posX1 && playerX < posX2) && (playerZ > posZ1 && playerZ < posZ2)) isInArea = true;
            }

            return isInArea;
        }
		
        private void RestoreTheDefaultExchangePrices(Player player, string cmd)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            _tradeList = new Collection<string[]>();
            foreach (var item in _tradeDefaults)
            {
                var sellPrice = (int)(Int32.Parse(item[1]) * (_sellPercentage / 100));
                var newItem = new string[5] { item[0], item[1], item[2], item[1], sellPrice.ToString() };
                _tradeList.Add(newItem);
            }
            PrintToChat(player, "Grand Exchange prices have been reset to default values");


            //Save the data
            SaveTradeData();
        }

        private void AddANewItemToTheExchange(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            // Check the current store
            var resource = input.JoinToString(" ");
            var found = false;
            for (var i = 0; i < _tradeList.Count; i++)
            {
                if (_tradeList[i][0].ToLower() == resource.ToLower())
                {
                    found = true;
                    break;
                }
            }
            if (found)
            {
                PrintToChat(player, resource + "  already exists in the store!");
                return;
            }

            found = false;
            // Check if the item exists in the defaults
            var previousItem = new string[5];
            for (var i = 0; i < _tradeDefaults.Count; i++)
            {
                if (_tradeDefaults[i][0].ToLower() == resource.ToLower())
                {
                    found = true;
                    previousItem = new string[5] { _tradeDefaults[i][0], _tradeDefaults[i][1], _tradeDefaults[i][2], _tradeDefaults[i][1], _tradeDefaults[i][1] };
                    if (_tradeList.Count < i) i = _tradeList.Count;
                    _tradeList.Insert(i, previousItem);
                    PrintToChat(player, resource + " has been added to the store!");
                    ForcePriceAdjustment();
                    break;
                }
            }
            if (!found)
            {
                PrintToChat(player, resource + " does not appear in the original defaults list. If you want this item added, please add it to the defaults list first. (In the plugin) Note - It MUST be a real item or the system may crash!");
                return;
            }

            //Save the data
            SaveTradeData();
        }

        private void BuyAnItemOnTheExchange(Player player, string cmd)
        {
            //Is player in the trade hub area?
            if (!PlayerIsInTheRightTradeArea(player))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You cannot trade outside of the designated trade area!");
                return;
            }

            //Open up the buy screen
            player.ShowInputPopup("Grand Exchange", "What [00FF00]item [FFFFFF]would you like to buy on the [00FFFF]Grand Exchange[FFFFFF]?", "", "Submit", "Cancel", (options, dialogue1, data) => SelectItemToBeBought(player, options, dialogue1, data));
        }

        private void SellAnItemOnTheExchange(Player player, string cmd)
        {
            //Is player in the trade hub area?
            if (!PlayerIsInTheRightTradeArea(player))
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You cannot trade outside of the designated trade area!");
                return;
            }

            //Open up the sell screen
            player.ShowInputPopup("Grand Exchange", "What [00FF00]item [FFFFFF]would you like to sell on the [00FFFF]Grand Exchange[FFFFFF]?", "", "Submit", "Cancel", (options, dialogue1, data) => SelectItemToBeSold(player, options, dialogue1, data));
        }


        private void DisplayDefaultTradeList(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "This is not for you. Don't even try it, thieving scumbag!");
                return;
            }
            foreach (var item in _tradeDefaults)
            {
                PrintToChat(player, item[0]);
            }
        }

        private void SetThePriceOfAnItem(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "This is not for you. Don't even try it, thieving scumbag!");
                return;
            }

            if (input.Length < 2)
            {
                PrintToChat(player, "Usage: Type /setprice \"Resource Name\" <amount>");
                return;
            }

            var resource = input[0];
            var priceText = input[1];
            int price;
            if (Int32.TryParse(priceText, out price) == false)
            {
                PrintToChat(player, "Bad amount value entered!");
                return;
            }

            priceText = (price * 1000).ToString();


            foreach (var item in _tradeList)
            {
                if (item[0].ToLower() == resource.ToLower())
                {
                    item[1] = priceText;
                    item[3] = priceText;
                    item[4] = priceText;
                }
            }

            PrintToChat(player, "Changing price of " + resource + " to " + priceText);

            ForcePriceAdjustment();

            // Save the trade data
            SaveTradeData();
        }

        private void RemoveAnItemFromTheExchange(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            var resource = input.JoinToString(" ");
            int position = -1;
            for (var i = 0; i < _tradeList.Count; i++)
            {
                if (_tradeList[i][0].ToLower() == resource.ToLower())
                {
                    position = i;
                    break;
                }
            }
            if (position >= 0)
            {
                _tradeList.RemoveAt(position);
                PrintToChat(player, resource + "  has been removed from the store.");
            }
            else PrintToChat(player, "Could not find that item in the store to remove it.");


            //Save the data
            SaveTradeData();
        }

        private void RemoveAllExchangeItems(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            _tradeList = new Collection<string[]>();

            PrintToChat(player, "The exchange store has been wiped! Use /addstoreitem <itemname> to start filling it again.");

            //Save the data
            SaveTradeData();
        }

        private void AddPlayerAsATradeMaster(Player player,string cmd,string[] input )
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            var playerName = input.JoinToString(" ");
            // Check player exists
            var target = Server.GetPlayerByName(playerName);
            if (target == null)
            {
                PrintToChat(player, "That player does not appear to be online right now.");
                return;
            }

            //Check if player is already on the list
            foreach (var tradeMaster in _tradeMasters)
            {
                if (tradeMaster.ToLower() == playerName.ToLower())
                {
                    PrintToChat(player, "That player is already a trade master.");
                    return;
                }
            }

            // Add the player to the list
            _tradeMasters.Add(playerName.ToLower());
            PrintToChat(player, "You have added " + playerName + " as a Trade Master.");

            SaveTradeData();
        }

        private void RemovePlayerAsATradeMaster(Player player,string cmd,string[] input )
        {
            if (!player.HasPermission("admin"))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            var playerName = input.JoinToString(" ");
            // Check player exists
            
            //Check if player is already on the list
            var position = -1;
            for(var i=0; i<_tradeMasters.Count;i++)
            {
                if (_tradeMasters[i] == playerName.ToLower())
                {
                    position = i;
                    break;
                }
            }

            if (position < 0)
            {
                PrintToChat(player, playerName + " does not appear to be on the list!");
                return;
            }

            _tradeMasters.RemoveAt(position);
            PrintToChat(player, playerName + " has had their Grand Exchange priveleges revoked!");
            SaveTradeData();
        }

        private bool PlayerIsATradeMaster(string playerName)
        {
            foreach (var tradeMaster in _tradeMasters)
            {
                if (tradeMaster.ToLower() == playerName.ToLower()) return true;
            }
            return false;
        }


        private void SetTheNewSellPercentageAmount(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            int percentage;
            if (Int32.TryParse(input[0], out percentage) == false)
            {
                PrintToChat(player, "You entered an invalid amount. Please enter an amount from 1-100%");
                return;
            }

            _sellPercentage = (double)percentage;
            //Adjust the prices
            ForcePriceAdjustment();
            PrintToChat(player, "The Sell percentage has been set to " + percentage.ToString());

            SaveTradeData();
        }


        private void AdminSetAPlayersGoldAmount(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }
            var playerName = Capitalise(input[0]);
            var target = Server.GetPlayerByName(playerName);
            if (target == null)
            {
                PrintToChat(player, "That player doesn't appear to be online at this moment.");
                return;
            }

            int amount;
            if (Int32.TryParse(input[1], out amount) == false)
            {
                PrintToChat(player, "You entered an invalid amount. Please enter in the format: /setplayergold 'Name_In_Quotes' <amount>");
                return;
            }
            var targetId = Server.GetPlayerByName(playerName).Id;
            CheckWalletExists(target);
            _playerWallet[targetId] = amount;
            PrintToChat(player, "You have set gold amount for " + playerName + " to " + amount.ToString());
            SaveTradeData();
        }

        private void RemoveTheGoldFromAllPlayers(Player player, string cmd)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            _playerWallet = new Dictionary<ulong, int>();

            PrintToChat(player, "All players' gold has been removed!");

            SaveTradeData();
        }


        #endregion

        #region GOLD AND CURRENCY


        private void GiveGold(Player player, int amount)
        {
            var playerName = player.Name.ToLower();
            var currentGold = _playerWallet[player.Id];
            if (currentGold + amount > MaxPossibleGold)
            {
                PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You cannot gain any more gold than you now have. Congratulations. You are the richest player. Goodbye.");
                currentGold = MaxPossibleGold;
            }
            else currentGold = currentGold + amount;
			
			CheckWalletExists(player);
            _playerWallet[player.Id] = currentGold;
			SaveTradeData();
        }

        private void GiveGold(ulong playerId, int amount)
        {
            if (!_playerWallet.ContainsKey(playerId)) return;
            var currentGold = _playerWallet[playerId];
            if (currentGold + amount > MaxPossibleGold)
            {
                var player = Server.GetPlayerById(playerId);
                if (player != null) PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : You cannot gain any more gold than you now have. Congratulations. You are the richest player. Goodbye.");
                currentGold = MaxPossibleGold;
            }
            else currentGold = currentGold + amount;

			CheckWalletExists(playerId);
            _playerWallet[playerId] = currentGold;
			SaveTradeData();
        }

        private bool CanRemoveGold(Player player, int amount)
        {
            var currentGold = _playerWallet[player.Id];
            if (currentGold - amount < 0) return false;
            return true;
        }
        
        private void RemoveGold(Player player, int amount)
        {
            var currentGold = _playerWallet[player.Id];
            currentGold = currentGold - amount;

            _playerWallet[player.Id] = currentGold;
        }
        
        private void RemoveGold(ulong playerId, int amount)
        {
            var currentGold = _playerWallet[playerId];
            currentGold = currentGold - amount;

            _playerWallet[playerId] = currentGold;
        }
		
        private void TogglePvpGoldStealing(Player player, string cmd)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }
            if (_allowPvpGold)
            {
                _allowPvpGold = false;
                PrintToChat(player, "PvP gold stealing is now [FF0000]OFF");
                return;
            }
            _allowPvpGold = true;
            PrintToChat(player, "PvP gold stealing is now [00FF00]ON");
            SaveTradeData();
        }

        private void TogglePveGoldFarming(Player player, string cmd)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }
            if (_allowPveGold)
            {
                _allowPveGold = false;
                PrintToChat(player, "PvE gold farming is now [FF0000]OFF");
                return;
            }
            _allowPveGold = true;
            PrintToChat(player, "PvP gold farming is now [00FF00]ON");
            SaveTradeData();
        }


        private void CheckHowMuchGoldICurrentlyHave(Player player, string cmd)
        {
            CheckWalletExists(player);
			var walletAmount = _playerWallet[player.Id];
			PrintToChat(player,"You currently have [00FF00]" + walletAmount.ToString() + "[FFFF00]g[FFFFFF].");
        }


        private void GiveGoldToAPlayer(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "This is not for you. Don't even try it, thieving scumbag!");
                return;
            }

            if (input.Length < 2)
            {
                PrintToChat(player, "Enter a player name followed by the amount to give.");
                return;
            }

            int amount;
            if (Int32.TryParse(input[1], out amount) == false)
            {
                PrintToChat(player, "That was not a recognised amount!");
                return;
            }

            var playerName = input[0];
            var target = Server.GetPlayerByName(playerName);

            if (target == null)
            {
                PrintToChat(player, "That player doesn't appear to be online right now!");
                return;
            }

            PrintToChat(player, "Giving " + amount.ToString() + " gold to " + playerName);
            PrintToChat(target, "You have received an Admin gift of " + amount.ToString() + " gold.");

            CheckWalletExists(target);
            GiveGold(target, amount);

            // Save the trade data
            SaveTradeData();
        }

        private void CheckTheGoldAPlayerHas(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }

            var playerName = input.JoinToString(" ");

            var target = Server.GetPlayerByName(playerName);
            if (target == null)
            {
                PrintToChat(player, "That player doesn't appear to be online right now!");
                return;
            }

            CheckWalletExists(target);
            var goldAmount = _playerWallet[target.Id];
            PrintToChat(player, target.Name + " currently has " + goldAmount.ToString() + " gold.");
        }


        #endregion

        #region SAFE TRADING

        private void ToggleTheSafeTradingArea(Player player, string cmd)
        {
            if (!player.HasPermission("admin") && !PlayerIsATradeMaster(player.Name.ToLower()))
            {
                PrintToChat(player, "Only admins can use this command.");
                return;
            }
		    if (_tradeAreaIsSafe)
		    {
		        _tradeAreaIsSafe = false;
		        PrintToChat(player, "Trading areas are now open to PvP and attacks.");
		    }
		    else
		    {
		        _tradeAreaIsSafe = true;
                PrintToChat(player, "Trading areas are now safe.");
		    }
			
			SaveTradeData();
        }

        #endregion

        #region PLAYER CREATURE AND OBJECT DAMAGE

        // Give credits when a player is killed
		private void OnEntityDeath(EntityDeathEvent deathEvent)
		{
		    if (deathEvent.Entity == null) return;
			if(deathEvent.Entity.Owner == null) return;
			if(deathEvent.Entity.Owner.Name == "server") return;
			
			if(_allowPvpGold)
			{
				if (deathEvent.Entity.IsPlayer)
				{
				    if (deathEvent.KillingDamage == null)
				    {
				        Log("deathEvent.KillingDamage was null here!"); 
                        return;
				    }
				    if (deathEvent.KillingDamage.DamageSource == null)
				    {
                        Log("deathEvent.KillingDamage.DamageSource was null here!"); 
				        return;
				    }
                    
                    var killer = deathEvent.KillingDamage.DamageSource.Owner;
					var player = deathEvent.Entity.Owner;

				    if (player.Id == 0 || killer.Id == 0)
				    {
				        Log("Player or Killer had no id!!");
				        return;
				    }

				    if (player == null)
				    {
                        Log("player variable was null here.");
				        return;
				    }
				    if (killer == null)
				    {
                        Log("killer variable was null here");
				        return;
				    }

					// Make sure player didn't kill themselves
					if(player == killer) return;
					
					if (player.GetGuild() == null || killer.GetGuild() == null)
					{
                        Log("The player or the killer guild was null here.");
					    return;
					}

					// Make sure the player is not in the same guild
					if(player.GetGuild().Name == killer.GetGuild().Name)
					{
						PrintToChat(player, "[FF0000]Grand Exchange[FFFFFF] : There is no honour - or more importantly, gold! - in killing a member of your own guild!");
						return;
					}
					
					// Make sure it was a player that killed them
					if(deathEvent.KillingDamage.DamageSource.IsPlayer)
					{
						// Get the inventory
						//var inventory = killer.GetInventory();
						
						// Check victims wallet
						CheckWalletExists(player);
						// Check the player has a wallet
						CheckWalletExists(killer);
						// Give the rewards to the player
						var victimGold = _playerWallet[player.Id];
					    var goldReward = (int)(victimGold * (double)(GoldStealPercentage / 100));
						var goldAmount = _random.Next(0,goldReward);
						if(goldAmount > victimGold) goldAmount = victimGold;
						GiveGold(killer,goldAmount);
						RemoveGold(player,goldAmount);
						
						if(goldAmount == 0)
						{
							PrintToChat(killer, "[FF00FF]" + player.Name + "[FFFFFF] had no gold for you to steal!");
						}
						else
						{
							// Notify everyone
							PrintToChat("[FF00FF]" + killer.Name + "[FFFFFF] has stolen [00FF00]" + goldAmount.ToString() + "[FFFF00] gold [FFFFFF] from the dead body of [00FF00]" + player.Name + "[FFFFFF]!");
						}
						
					}
				}
			}
			
			
			//Save the data
			SaveTradeData();
        }
		
		private void OnEntityHealthChange(EntityDamageEvent damageEvent) 
		{
		    if (damageEvent.Entity == null)
		    {
                Log("damageEvent.Entity was null!");
		        return;
		    }

			if(_allowPveGold)
			{
				if (!damageEvent.Entity.IsPlayer)
				{
					var victim = damageEvent.Entity;
					Health h = victim.TryGet<Health>();
                    if (h.ToString().Contains("Plague Villager")) return;
                    if (h.ToString().Contains("Trebuchet")) return;
                    if (h.ToString().Contains("Ballista")) return;

					if (!h.IsDead) return;

				    if (damageEvent.Damage == null)
				    {
				        Log("Damage was null here!");
                        return;
				    }
				    if (damageEvent.Damage.DamageSource == null)
				    {
				        Log("DamageSource was null here!");
                        return;
				    }
				    if (damageEvent.Damage.DamageSource.Owner == null)
				    {
				        Log("damageEvent.Damage.DamageSource.Owner WAS NULL HERE!");
                        return;
				    }

					var hunter = damageEvent.Damage.DamageSource.Owner;
					
					// Give the rewards to the player
					var goldAmount = _random.Next(2,GoldRewardForPve);
					GiveGold(hunter,goldAmount);
					
					// Notify everyone
					PrintToChat(hunter, "[00FF00]" + goldAmount.ToString() + "[FFFF00] gold[FFFFFF] collected.");
					
					SaveTradeData();
				}
			}
			if (damageEvent.Entity.IsPlayer)
			{
				if(_tradeAreaIsSafe)
				{
				    if (damageEvent.Damage.DamageSource.Owner == null)
				    {
				        Log("damageEvent.Damage.DamageSource.Owner WAS NULL IN THE TRADE AREA SECTION!");
                        return;
				    }
					if(PlayerIsInTheRightTradeArea(damageEvent.Damage.DamageSource.Owner))
					{
						if(damageEvent.Damage.DamageSource.IsPlayer && damageEvent.Entity != damageEvent.Damage.DamageSource)
						{
							damageEvent.Damage.Amount = 0f;
							PrintToChat(damageEvent.Damage.DamageSource.Owner, "[FF0000]Grand Exchange[FFFFFF] : You cannot attack people in a designated trade area, scoundrel!");
						}
					}
				}
			}
		}

        
        //private void OnCubeTakeDamage(CubeDamageEvent cubeDamageEvent)
        //{
        //    var player = cubeDamageEvent.Damage.DamageSource.Owner;

        //    // If in the GE Area
        //    if (PlayerIsInTheRightTradeArea(player))
        //    {
        //        // IF its a player attacking the base
        //        if (cubeDamageEvent.Damage.Amount > 50 && cubeDamageEvent.Damage.DamageSource.Owner is Player)
        //        {
        //            bool trebuchet = cubeDamageEvent.Damage.Damager.name.ToString().Contains("Trebuchet");
        //            bool ballista = cubeDamageEvent.Damage.Damager.name.ToString().Contains("Ballista");
        //            if (trebuchet || ballista)
        //            {
        //                cubeDamageEvent.Damage.Amount = 0f;
        //            }
        //            var message = "[FF0000]Grand Exchange : [00FF00]" + player.DisplayName + "[FFFFFF]! Attacking the Grand Exchange is ill-advised! Decist immediately!";
        //            //PrintToChat(message);
        //            //Log(message);
        //        }
        //    }
        //}
        #endregion

        #region UTILITY METHODS
        // Capitalise the Starting letters
		private string Capitalise(string word)
		{
			var finalText = "";
			finalText = Char.ToUpper(word[0]).ToString();
			var spaceFound = 0;
			for(var i=1; i<word.Length;i++)
			{
				if(word[i] == ' ')
				{
					spaceFound = i + 1;
				}
				if(i == spaceFound)
				{
					finalText = finalText + Char.ToUpper(word[i]).ToString();
				}
				else finalText = finalText + word[i].ToString();
			}
			return finalText;
        }
        #endregion

#endregion
    }
}
