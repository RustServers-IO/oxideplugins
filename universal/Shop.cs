using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Hurtworld.Libraries;
using Oxide.Core.Extensions;

using System.Collections.Generic;
using System;
using System.Linq;
using Steamworks;
using Newtonsoft.Json;
using uLink;

namespace Oxide.Plugins
{
    [Info("Shop", "RD156", "1.1.2")]
    [Description("NPC for buy and sell")]

    class Shop : HurtworldPlugin
	{
		Plugin  Banks;
		int		fail = 0;
		Store store;
		// function of base	
		void Init() {}
		void Loaded()
		{
			Banks = (Plugin)plugins.Find("Banks");
			if (Banks != null)
                fail = 1;
			else
				fail = 0;

			LoadDataStore();
			LoadMessages();
		}
		void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Player not found", "<color=#ff0000>Player not found :</color>"},
				{"Fail Load Plugin Banks", "<color=#ff0000> Failled to load Plugin : Banks </color>"},
				{"Item not found", "<color=#ff0000>Item not found.</color>"},
				{"No Permission", "<color=#FF1000>You don't Have permission </color> :("},
				{"Item Already Shop", "<color=#ff8000>This Item is already in Shop</color>"},
				{"Syntax Error", "<color=#ff8000>Syntax Error</color> :"},
				{"You no money", "<color=#ff8000>You don't have Money</color>"},
				{"You no place", "<color=#ff8000>You don't have Place in you inventory</color>"},
				{"Item not buy", "<color=#ff8000>NPC don't Sell this Item</color>"},
				{"Item not sell", "<color=#ff8000>This Item is not in sell</color>"},
				{"You sell nothing", "<color=#ff8000>You have nothing for sell</color>"},
				{"You sell nothing inventory", "<color=#ff8000>You have nothing for sell in your inventory</color>"},
				
				{"Begin Shop Help", "<color=#40E047>--------------------------------SHOP----------------------------------------</color>"},
				{"Begin Buy Help", "<color=#40E047>-----------------------------------BUY--------------------------------------</color>"},
				{"Begin Sell Help", "<color=#40E047>------------------------SELL------------------------------------------------</color>"},
				{"Help Shop Add", "1 : <color=#ff8000> shop add </color> [ID Item] [Number In Stack] [Name Item] [Price Sell] [Price Buy] [Is Sell] [Is Buy]"},
				{"Help Shop Edit", "2 : <color=#ff8000> shop edit </color> [ID Item] [Number In Stack] [Name Item] [Price Sell] [Price Buy] [Is Sell] [Is Buy]"},
				{"Help Shop View", "3 : <color=#ff8000> shop view </color> [ID Item]"},
				{"Help Shop View All", "4 : <color=#ff8000> shop view </color>"},
				{"Help Buy buy", "<color=#40E047> 3 : </color> /buy [ID_item] [Number]"},
				{"Help Buy buy example 1", "<color=#40E047> 1 : </color> /buy  p 1 (for display page 1)"},
				{"Help Buy buy example 2", "<color=#40E047> 2 : </color> /buy  p 2 (for display page 2 ...)"},
				{"Help Sell View", "<color=#40E047> 1 : </color>: /sell view"},
				{"Help Sell Numero", "<color=#40E047> 2 : </color>: /sell [number]"},
				{"Help Sell Item", "<color=#40E047> 3 : </color>: /sell item [id_item] [number]"},
				{"Help Sell Info Cases", "You can sell just the 5 first case in your inventory(not fast inventory or fast bare)"},
				{"Help Sell Example", "Example : 48 : 15 * 2 = 30 = > /sell [Number Case] "},
				{"End Help", "<color=#40E047>----------------------------------------------------------------------------</color>"},
				
				{"Item Id", "<color=#ff0000> Item {id} </color> :"},
				{"Item Name", "<color=#ff0000>Name</color> {name_item}"},
				{"Item Price Sell", "Price of Sell : {price_sell}"},
				{"Item Price Buy", "Price of Buy : {price_buy}"},
				{"Item Max In Stack", "Maximum In Stack : {stack}"},
				{"Is Buy", "In Buy : {is_buy}"},
				{"Is Sell", "In Sell : {is_sell}"},
				
				{"You have buy", "<color=#40E047>You Have Buy</color> {number} {name_item} for {price} {name_money}"},
				{"You have sell", "<color=#40E047>You have sell</color> {number} {name_item} for {price} {name_money}"},
				{"You have tryto sell", "<color=#40E047>You have sell</color> {number} / {number_want} {name_item} for {price} {name_money}"},
				{"Item view buy", "<color=#ff0000>Item {id} </color> : {name_item} <color=#ff8000> {price_buy} </color>"},
				{"Item view sell", "<color=#ff0000>Item {id} </color> : {name_item} <color=#40E047> {price_sell} </color>"},
				{"Item view no buy and no sell", "<color=#ff0000>Item {id} </color> : {name_item} <color=#40E047> No sell </color> / <color=#ff8000> No buy </color>"},
				{"Item view buy and sell", "<color=#ff0000>Item {id} </color> : {name_item} <color=#ff8000> {price_buy} </color> / <color=#40E047> {price_sell}</color>"},
				{"Sell view", "<color=#40E047>Numero {case}</color>: {name_item} * {number} = <color=#40E047>{price}</color> {name_money}"}	
				
            }, this);
        }
		// OBJ
		
		class Store
		{
			public Dictionary<int, StoreItem> StoreItems = new Dictionary<int, StoreItem>();
			public Store(){}
		}
		class Categorie
		{
			public Dictionary<int, string> List_Categorie = new Dictionary<int, string>();
			public Dictionary<int, int> Item_Categorie = new Dictionary<int, int>();
			public Categorie(){}
		}
		class StoreItem
		{
			public int id_item;
			public int max_item;
			public string name_item;
			public int price_sell; // price sell NPC
			public int price_buy; // price buy NPC
			public bool is_sell; // if NPC sell
			public bool is_buy; // if le NPC buy
			public StoreItem(int id, int max, string name, int sell, int buy, bool bool_is_sell, bool bool_is_buy)
			{
				id_item = id;
				max_item = max;
				name_item = name;
				price_sell = sell;
				price_buy = buy;
				is_sell = bool_is_sell;
				is_buy = bool_is_buy;
			}
		}
		//Command
		void shop(PlayerSession session_my, string command, string[] args)
		{
			if (args != null && args.Length >= 1)
			{
				if (args[0] == "add" && args.Length >= 8)
					shop_add(session_my, args);
				else if (args[0] == "edit" && args.Length >= 8)
					shop_edit(session_my, args);
				else if (args[0] == "sudo" && args.Length >= 8)
					shop_sudo(session_my, args);
				else if (args[0] == "view")
					shop_view(session_my, args);
				else if (args[0] == "p")
					buy_p(session_my, args);
				else
				{
					shop_help(session_my);
				}
			}
			else
				shop_help(session_my);
		}		
		void buy(PlayerSession session_my, string command, string[] args)
		{
			if (args != null && args.Length >= 1)
			{
				if (args[0] == "help")
					buy_help(session_my);
				else if (args[0] == "p")
					buy_p(session_my, args);
				else
					buy_item(session_my, args);
			}
			else
				buy_help(session_my);

		}
		bool sell(PlayerSession session_my, string command, string[] args)
		{
			if (args != null && args.Length >= 1)
			{
				if (args[0] == "view")
					sell_view(session_my, command, args);
				else if (args[0] == "item")
					sell_item_item(session_my, command, args);
				else
				{
					sell_item(session_my, command, args);
				}
			}
			else
				sell_help(session_my);
			return (true);
		}
		//fonction

		PlayerSession FindSession(string nameOrIdOrIp)
        {
            var sessions = GameManager.Instance.GetSessions();
            PlayerSession session = null;
            foreach (var i in sessions)
            {
                if (nameOrIdOrIp.Equals(i.Value.Name, StringComparison.OrdinalIgnoreCase) ||
                    nameOrIdOrIp.Equals(i.Value.SteamId.ToString()) || nameOrIdOrIp.Equals(i.Key.ipAddress))
                {
                    session = i.Value;
                    break;
                }
            }
            return session;
        }
		int getInvPlaceFree(PlayerSession session)
		{
			var targetInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var itemmanager = Singleton<GlobalItemManager>.Instance;
            for (var i = 0; i < targetInventory.Capacity; i++)
			{
				if (targetInventory.Items[i] == null)
				{
					if ((i >= 0 && i < 8) ||i >= 16)
						return (i);
				}
			}
			return (-1);
		}
		int getNbInvPlaceFree(PlayerSession session)
		{
			int nb = 0;
			var targetInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var itemmanager = Singleton<GlobalItemManager>.Instance;
            for (var i = 0; i < targetInventory.Capacity; i++)
			{
				if (targetInventory.Items[i] == null)
				{
					if ((i >= 0 && i < 8) || i >= 16)
						nb = nb + 1;
				}
			}
			return (nb);
		}
		void shop_help(PlayerSession session)
		{
			hurt.SendChatMessage(session, $"{Display_message("Begin Shop Help", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Shop Add", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Shop Edit", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Shop View", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Shop View All", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("End Help", session.SteamId.ToString())}");
		}
		bool shop_view(PlayerSession session_my, string[] args)
		{
			int id;
			
			if (args.Length > 1)
			{
				try
				{
					id = int.Parse(args[1]);
				}
				catch(Exception ex)
				{
					id = FindIDItemByName(args[1]);
				}
				if (id < 0)
				{
					hurt.SendChatMessage(session_my, $"{Display_message("Item not found", session_my.SteamId.ToString())}");
				}
				else if (id == 0)
					shop_view_all(session_my);
				else
					shop_view_item(session_my, id);
			}
			else
				shop_view_all(session_my);
			return (true);
		}
		bool shop_view_all(PlayerSession session_my)
		{
			foreach (var item in store.StoreItems)
            {
				if (item.Value.is_buy == true && item.Value.is_sell == true)
				{
					hurt.SendChatMessage(session_my, $"{Display_message("Item view buy and sell", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item).Replace("{price_buy}",item.Value.price_buy.ToString()).Replace("{price_sell}",item.Value.price_sell.ToString())}");
				}
				else if (item.Value.is_buy == true)
				{
				    hurt.SendChatMessage(session_my, $"{Display_message("Item view buy", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item).Replace("{price_buy}",item.Value.price_buy.ToString())}");
				}
				else if (item.Value.is_sell == true)
				{
					hurt.SendChatMessage(session_my, $"{Display_message("Item view buy and sell", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item).Replace("{price_sell}",item.Value.price_sell.ToString())}");
				}
				else
				{
					hurt.SendChatMessage(session_my, $"{Display_message("Item view no buy and no sell", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item)}");
				}
			}
			return (true);
		}
		bool shop_view_limit(PlayerSession session_my, int begin, int end)
		{
			int i = 0;
			
			foreach (var item in store.StoreItems)
            {
				if (i >= begin && i < end)
				{
					if (item.Value.is_buy == true && item.Value.is_sell == true)
					{
						hurt.SendChatMessage(session_my, $"{Display_message("Item view buy and sell", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item).Replace("{price_buy}",item.Value.price_buy.ToString()).Replace("{price_sell}",item.Value.price_sell.ToString())}");
					}
					else if (item.Value.is_buy == true)
					{
						hurt.SendChatMessage(session_my, $"{Display_message("Item view buy", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item).Replace("{price_buy}",item.Value.price_buy.ToString())}");
					}
					else if (item.Value.is_sell == true)
					{
						hurt.SendChatMessage(session_my, $"{Display_message("Item view buy and sell", session_my.SteamId.ToString()).Replace("{id}",item.Value.id_item.ToString()).Replace("{name_item}",item.Value.name_item).Replace("{price_sell}",item.Value.price_sell.ToString())}");
					}
				}
				i = i + 1;
			}
			return (true);
		}
		bool shop_view_item(PlayerSession session_my, int id)
		{
			if (id > 0 && store.StoreItems.ContainsKey(id) == true)
			{
				var item = store.StoreItems[id];
				hurt.SendChatMessage(session_my, $"{Display_message("Item Id", session_my.SteamId.ToString()).Replace("{id}",item.id_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",item.name_item)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",item.max_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",item.price_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",item.price_buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",item.is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",item.is_buy.ToString())}");
				return (true);
			}
			return (true);
		}
		bool shop_add(PlayerSession session_my, string[] args)
		{
			int id = 0;
			int max = 0;
			string name = "";
			int sell = 0;
			int buy = 0;
			bool bool_is_sell = false;
			bool bool_is_buy = false;
			if (session_my.IsAdmin != true)
			{
				hurt.SendChatMessage(session_my, $"{Display_message("No Permission", session_my.SteamId.ToString())}");
				return (false);
			}
			try
			{
				id = int.Parse(args[1]);
				max = int.Parse(args[2]);
				name = args[3];
				sell = int.Parse(args[4]);
				buy = int.Parse(args[5]);
				if (args[6] == "true" || int.Parse(args[6]) == 1)
					bool_is_sell = true;
				if (args[7] == "true" || int.Parse(args[7]) == 1)
					bool_is_buy = true;
			}
			catch(Exception ex)
			{
				id = 0;
				max = 0;
				name = "";
				sell = 0;
				buy = 0;
				bool_is_sell = false;
				bool_is_buy = false;
			}
			if (id > 0 && store.StoreItems.ContainsKey(id) != true)
			{
				StoreItem new_item = new StoreItem(id, max, name, sell, buy, bool_is_sell, bool_is_buy);
				store.StoreItems[id] = new_item;				
				hurt.SendChatMessage(session_my, $"{Display_message("Item view", session_my.SteamId.ToString()).Replace("{id}",id.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",name)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",max.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",bool_is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",bool_is_buy.ToString())}");
				SaveDataStore();
				return (true);
			}
			else if (id > 0)
			{
				hurt.SendChatMessage(session_my, $"{Display_message("Item Already Shop", session_my.SteamId.ToString())}");
				return (false);
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Display_message("Syntax Error", session_my.SteamId.ToString())}" + $"{Display_message("Help Shop Add", session_my.SteamId.ToString())}");
				return (false);
			}
		}
		bool shop_edit(PlayerSession session_my, string[] args)
		{
			int id = 0;
			int max = 0;
			string name = "";
			int sell = 0;
			int buy = 0;
			bool bool_is_sell = false;
			bool bool_is_buy = false;
			if (session_my.IsAdmin != true)
			{
				hurt.SendChatMessage(session_my, $"{Display_message("No Permission", session_my.SteamId.ToString())}");
				return (false);
			}
			try
			{
				id = int.Parse(args[1]);
				max = int.Parse(args[2]);
				name = args[3];
				sell = int.Parse(args[4]);
				buy = int.Parse(args[5]);
				if (args[6] == "true" || int.Parse(args[6]) == 1)
					bool_is_sell = true;
				if (args[7] == "true" || int.Parse(args[7]) == 1)
					bool_is_buy = true;
			}
			catch(Exception ex)
			{
				id = 0;
				max = 0;
				name = "";
				sell = 0;
				buy = 0;
				bool_is_sell = false;
				bool_is_buy = false;
			}
			if (id > 0 && store.StoreItems.ContainsKey(id) == true)
			{
				var item = store.StoreItems[id];				
				
				hurt.SendChatMessage(session_my, $"{Display_message("Item Id", session_my.SteamId.ToString()).Replace("{id}",item.id_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",item.name_item)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",item.max_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",item.price_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",item.price_buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",item.is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",item.is_buy.ToString())}");
				store.StoreItems[id].id_item = id;
				store.StoreItems[id].name_item = name;
				store.StoreItems[id].max_item = max;
				store.StoreItems[id].price_sell = sell;
				store.StoreItems[id].price_buy = buy;
				store.StoreItems[id].is_sell = bool_is_sell;
				store.StoreItems[id].is_buy = bool_is_buy;
				hurt.SendChatMessage(session_my, $"{Display_message("Item Id", session_my.SteamId.ToString()).Replace("{id}",item.id_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",item.name_item)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",item.max_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",item.price_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",item.price_buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",item.is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",item.is_buy.ToString())}");
				SaveDataStore();
				return (true);
			}
			else if (id > 0)
			{
				hurt.SendChatMessage(session_my, $"{Display_message("Item not found", session_my.SteamId.ToString())}");
				return (false);
			}
			else
			{
				hurt.SendChatMessage(session_my, $"{Display_message("Syntax Error", session_my.SteamId.ToString())}" + $"{Display_message("Help Shop Add", session_my.SteamId.ToString())}");
				return (false);
			}
		}
		bool shop_sudo(PlayerSession session_my, string[] args)
		{
			int id = 0;
			int max = 0;
			string name = "";
			int sell = 0;
			int buy = 0;
			bool bool_is_sell = false;
			bool bool_is_buy = false;
			if (session_my.IsAdmin != true)
			{
				hurt.SendChatMessage(session_my, $"{Display_message("No Permission", session_my.SteamId.ToString())}");
				return (false);
			}
			try
			{
				id = int.Parse(args[1]);
				max = int.Parse(args[2]);
				name = args[3];
				sell = int.Parse(args[4]);
				buy = int.Parse(args[5]);
				if (args[6] == "true" || int.Parse(args[6]) == 1)
					bool_is_sell = true;
				if (args[7] == "true" || int.Parse(args[7]) == 1)
					bool_is_buy = true;
			}
			catch(Exception ex)
			{
				id = 0;
				max = 0;
				name = "";
				sell = 0;
				buy = 0;
				bool_is_sell = false;
				bool_is_buy = false;
			}
			if (id > 0 && store.StoreItems.ContainsKey(id) != true)
			{
				StoreItem new_item = new StoreItem(id, max, name, sell, buy, bool_is_sell, bool_is_buy);
				store.StoreItems[id] = new_item;
				hurt.SendChatMessage(session_my, $"{Display_message("Item Id", session_my.SteamId.ToString()).Replace("{id}",id.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",name)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",max.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",bool_is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",bool_is_buy.ToString())}");
				
				SaveDataStore();
				return (true);
			}
			else if (id > 0)
			{
				var item = store.StoreItems[id];
				hurt.SendChatMessage(session_my, $"{Display_message("Item Id", session_my.SteamId.ToString()).Replace("{id}",item.id_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",item.name_item)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",item.max_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",item.price_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",item.price_buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",item.is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",item.is_buy.ToString())}");
				store.StoreItems[id].id_item = id;
				store.StoreItems[id].name_item = name;
				store.StoreItems[id].max_item = max;
				store.StoreItems[id].price_sell = sell;
				store.StoreItems[id].price_buy = buy;
				store.StoreItems[id].is_buy = bool_is_buy;
				store.StoreItems[id].is_sell = bool_is_sell;
				hurt.SendChatMessage(session_my, $"{Display_message("Item Id", session_my.SteamId.ToString()).Replace("{id}",item.id_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Name", session_my.SteamId.ToString()).Replace("{name_item}",item.name_item)}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Max In Stack", session_my.SteamId.ToString()).Replace("{stack}",item.max_item.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Sell", session_my.SteamId.ToString()).Replace("{price_sell}",item.price_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Item Price Buy", session_my.SteamId.ToString()).Replace("{price_buy}",item.price_buy.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Sell", session_my.SteamId.ToString()).Replace("{is_sell}",item.is_sell.ToString())}");
				hurt.SendChatMessage(session_my, $"{Display_message("Is Buy", session_my.SteamId.ToString()).Replace("{is_buy}",item.is_buy.ToString())}");
				SaveDataStore();
				return (true);
			}
			else
			{
				shop_help(session_my);
				return (false);
			}
		}
		void buy_p(PlayerSession session, string[] args)
		{
			int nb = 0;
			if (args != null && args.Length >= 2)
			{
				try
				{
					nb = int.Parse(args[1]);
				}
				catch(Exception ex)
				{
					nb = 0;
				}
				if (nb > 0)
					shop_view_limit(session, (nb - 1) * 5, nb * 5);
				else
					shop_view_limit(session, 0, 5);
			}
			else
				shop_view_limit(session, 0, 5);
		}
		void buy_help(PlayerSession session)
		{			
			hurt.SendChatMessage(session, $"{Display_message("Begin Buy Help", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Buy buy example 1", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Buy buy example 2", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Buy buy", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("End Help", session.SteamId.ToString())}");
		}
		bool give_item(PlayerSession session, StoreItem item, int number)
		{
			int inv_case = 0;
			int i = 0;
			
			while ((inv_case = getInvPlaceFree(session)) != -1 && number > 0 && i < 50)
			{
				if (number > item.max_item)
				{
					number = number - item.max_item;
					add_item(session, inv_case, item.id_item, item.max_item);
				}
				else
				{
					add_item(session, inv_case, item.id_item, number);
					number = 0;
				}
				i = i + 1;
			}
			return (true);
		}
		bool add_item(PlayerSession session, int inv_case, int id, int nb)
		{
            var targetInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var itemmanager = Singleton<GlobalItemManager>.Instance;
			if (inv_case < targetInventory.Capacity && targetInventory.Items[inv_case] == null)
			{
				
				var item = itemmanager.GetItem(id);
				var iitem = new ItemInstance(item, nb);
				targetInventory.Items[inv_case] = iitem;
				
			}
			var itemManager = GlobalItemManager.Instance;
            itemManager.GiveItem(session.Player, itemManager.GetItem(22), 0);
			return (true);
		}
		StoreItem getItemByID(int id)
		{
			if (id > 0 && store.StoreItems.ContainsKey(id) == true)
			{
				var item = store.StoreItems[id];
				return (item);
			}
			return (null);
		}
		int FindIDItemByName(string name)
		{
			foreach (var item in store.StoreItems)
            {
                if (item.Value.name_item == name)
					return (item.Value.id_item);
            }
			return (-1);
		}
		bool buy_item(PlayerSession session, string[] args)
		{
			if (args != null && args.Length >= 2)
			{
				int id = 0;
				int number = 0;
				int place = 0;
				int price = 0;
				string name_money = "";
				
				name_money = Banks.Call<string>("get_name_money");
				try
				{
					id = int.Parse(args[0]);
				}
				catch(Exception ex)
				{
					id = FindIDItemByName(args[0]);
				}
				try
				{
					number = int.Parse(args[1]);
				}
				catch(Exception ex)
				{
					number = 1;
				}
				if (id > 0)
				{
					StoreItem item = getItemByID(id);
					if (item == null)
					{
						hurt.SendChatMessage(session, $"{Display_message("Item not sell", session.SteamId.ToString())}");
						return (false);
					}
					else
					{
						if (item.is_sell == true)
						{
							if (number < 0)
								number = 0;
							if (item.max_item > 0)
							{
								place = number / item.max_item;
								if (number % item.max_item > 0)
									place = place + 1;	
							}
							else
							{
								place = number;
							}
							if (getNbInvPlaceFree(session) >= place)
							{
								price = number * item.price_sell;
								if (Banks.Call<int>("lost_money_poket", price, session) == 1)
								{
									give_item(session, item, number);
									hurt.SendChatMessage(session, $"{Display_message("You have buy", session.SteamId.ToString()).Replace("{number}",number.ToString()).Replace("{name_item}",item.name_item).Replace("{price}",price.ToString()).Replace("{name_money}",name_money.ToString())}");
								}				
								else
								{
									hurt.SendChatMessage(session, $"{Display_message("You no money", session.SteamId.ToString())}");
								}
							}
							else
							{
								hurt.SendChatMessage(session, $"{Display_message("You no place", session.SteamId.ToString())}");
							}
							return (true);
						}
						else
						{
							hurt.SendChatMessage(session, $"{Display_message("Item not sell", session.SteamId.ToString())}");
						}
					}
				}
				else
				{
					hurt.SendChatMessage(session, $"{Display_message("Item not sell", session.SteamId.ToString())}");
				}
			}
			return (false);
		}
		void sell_item_item(PlayerSession session, string command, string[] args)
		{
			int id_item;
			int number;
			int rest_number;
			StoreItem item;
			string name_money = "";
			int money = 0;
			
			Puts("test");
			if (args.Length >= 3)
			{
				try
				{
					id_item = int.Parse(args[1]);
					number = int.Parse(args[2]);
				}
				catch(Exception ex)
				{
					id_item = 0;
					number = 0;
				}
				item = getItemByID(id_item);
				if (item != null && item.is_buy == true)
				{
					name_money = Banks.Call<string>("get_name_money");
					rest_number = sell_item_item_parcours(session, number, id_item);
					if (rest_number == 0)
					{
						money = number * item.price_buy;
						if (money < 0)
							money = 0;
						hurt.SendChatMessage(session, $"{Display_message("You have sell", session.SteamId.ToString()).Replace("{number}",number.ToString()).Replace("{name_item}",item.name_item).Replace("{price}",money.ToString()).Replace("{name_money}",name_money.ToString())}");
						Banks.Call("add_money_poket", money, session);
					}
					else if (rest_number > 0)
					{
						money = (number - rest_number) * item.price_buy;
						if (money < 0)
							money = 0;
						hurt.SendChatMessage(session, $"{Display_message("You have tryto sell", session.SteamId.ToString()).Replace("{number}",(number - rest_number).ToString()).Replace("{number_want}",number.ToString()).Replace("{name_item}",item.name_item).Replace("{price}",money.ToString()).Replace("{name_money}",name_money.ToString())}");
						Banks.Call("add_money_poket", money, session);
					}

				}
				else
				{
					hurt.SendChatMessage(session, $"{Display_message("Item not buy", session.SteamId.ToString())}.");
				}
				
				hurt.SendChatMessage(session, "argument ok");
			}
			else
			{
				hurt.SendChatMessage(session, $"{Display_message("Help Sell Item", session.SteamId.ToString())}");
			}
		}
		int sell_item_item_parcours(PlayerSession session, int number, int id)
		{
			int rest_number;
			
			rest_number = number;
			var targetInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var itemmanager = Singleton<GlobalItemManager>.Instance;
			int item_in_stack = 0;
			var itemManager = GlobalItemManager.Instance;
			for (var i = 0; i < targetInventory.Capacity; i++)
			{
				if (targetInventory.Items[i] != null)
				{
					if (targetInventory.Items[i].Item.ItemId == id && (i < 8 || i >= 16))
					{
						item_in_stack = targetInventory.Items[i].StackSize;
						if (item_in_stack > number)
						{
							targetInventory.Items[i].StackSize = item_in_stack - number;
							rest_number = 0;
							targetInventory.Items[i] = null;
							itemManager.GiveItem(session.Player, itemManager.GetItem(id), item_in_stack - number);
							return (rest_number);
						}
						else if (item_in_stack == number)
						{
							targetInventory.Items[i] = null;
							rest_number = 0;
							return (rest_number);
						}
						else
						{
							rest_number = rest_number - targetInventory.Items[i].StackSize;
							targetInventory.Items[i] = null;
						}
					}
					
					itemManager.GiveItem(session.Player, itemManager.GetItem(22), 0);
				}
			}
			return (rest_number);
		}
		void sell_item(PlayerSession session, string command, string[] args)
		{
			var targetInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var itemmanager = Singleton<GlobalItemManager>.Instance;
			int id = 0;
			int case_item = -1;
			StoreItem item;
			int money = 0;
			int nb_item= 0;
			string name_money = "";
			if (args.Length >= 1)
			{
				try
				{
					case_item = int.Parse(args[0]);
				}
				catch(Exception ex)
				{
					case_item = -1;
				}
				if (case_item >= 0 && case_item < targetInventory.Capacity && case_item != 12)
				{
					if (targetInventory.Items[case_item] != null)
					{
						id = targetInventory.Items[case_item].Item.ItemId;
						item = getItemByID(id);
						if (item.is_buy == true)
						{
							name_money = Banks.Call<string>("get_name_money");
							nb_item = targetInventory.Items[case_item].StackSize;
							money = nb_item * item.price_buy;
							hurt.SendChatMessage(session, $"{Display_message("You have sell", session.SteamId.ToString()).Replace("{number}",nb_item.ToString()).Replace("{name_item}",item.name_item).Replace("{price}",money.ToString()).Replace("{name_money}",name_money.ToString())}");
							targetInventory.Items[case_item] = null;
							Banks.Call("add_money_poket", money, session);
						}
						else
						{
							hurt.SendChatMessage(session, $"{Display_message("Item not buy", session.SteamId.ToString())}.");
						}
					}
					else
					{
						hurt.SendChatMessage(session, $"{Display_message("You sell nothing", session.SteamId.ToString())}.");
					}
				}
				else
				{
					hurt.SendChatMessage(session, $"{Display_message("You sell nothing inventory", session.SteamId.ToString())}");
				}
			}
			else
				sell_help(session);

			var itemManager = GlobalItemManager.Instance;
            itemManager.GiveItem(session.Player, itemManager.GetItem(22), 0);
		}
		bool sell_view(PlayerSession session, string command, string[] args)
        {
            var targetInventory = session.WorldPlayerEntity.GetComponent<PlayerInventory>();
			var itemmanager = Singleton<GlobalItemManager>.Instance;
			int id = 0;
			StoreItem item;
			int nb_item = 0;
			string name_money = "";
            for (var i = 16; i < 21; i++)
			{
				if (targetInventory.Items[i] != null)
				{
					id = targetInventory.Items[i].Item.ItemId;
					item = getItemByID(id);
					nb_item = targetInventory.Items[i].StackSize;
					if (item != null)
					{
						name_money = Banks.Call<string>("get_name_money");
						hurt.SendChatMessage(session, $"{Display_message("Sell view", session.SteamId.ToString()).Replace("{case}",i.ToString()).Replace("{number}",nb_item.ToString()).Replace("{name_item}",item.name_item).Replace("{price}",(item.price_buy * nb_item).ToString()).Replace("{name_money}",name_money)}");

					}
				}
				
			}
            return (true);
        }
		void sell_help(PlayerSession session)
		{
			hurt.SendChatMessage(session, $"{Display_message("Begin Sell Help", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Sell View", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Sell Numero", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Sell Item", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Sell Info Cases", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("Help Sell Example", session.SteamId.ToString())}");
			hurt.SendChatMessage(session, $"{Display_message("End Help", session.SteamId.ToString())}");
		}
		
		// Load and Save
		void SaveDataStore()
		{
			Interface.Oxide.DataFileSystem.WriteObject("Store", store);
		}
		void LoadDataStore()
		{
			var get_store = Interface.Oxide.DataFileSystem.GetFile("Store");
			try
			{
				store = get_store.ReadObject<Store>();
			}
			catch
			{
				store = new Store();
			}
		}
		
		//Display_message
		string Display_message(string key, string id)
		{
			return lang.GetMessage(key, this, id);
		}
		//Liste des Commandes
		[ChatCommand("shop")]
		void my_shop(PlayerSession session, string command, string[] args)
		{
			if (fail == 0)
				hurt.SendChatMessage(session, $"{Display_message("Fail Load Plugin Banks", session.SteamId.ToString())}");
			else
				shop(session, command, args);
		}
		[ChatCommand("buy")]
		void my_buy(PlayerSession session, string command, string[] args)
		{
			if (fail == 0)
				hurt.SendChatMessage(session, $"{Display_message("Fail Load Plugin Banks", session.SteamId.ToString())}");
			else
				buy(session, command, args);
		}	
		[ChatCommand("sell")]
		void my_sell(PlayerSession session, string command, string[] args)
		{
			if (fail == 0)
				hurt.SendChatMessage(session, $"{Display_message("Fail Load Plugin Banks", session.SteamId.ToString())}");
			else
				sell(session, command, args);
		}
	}
}