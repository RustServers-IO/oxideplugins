using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using RustProto;

namespace Oxide.Plugins
{
	[Info("PvpStart", "#KodakPlay!!!", "1.0.4")]
    [Description("Plugin targeted to specific locations with Pvp kit spawn.")]
    public class PvpStart : RustLegacyPlugin
	{
		static bool small  			 = true;
		static bool big    			 = true;
		static bool hangar 			 = true;
		static bool p250   		     = true;
		static bool montain          = true;
		
		
		bool on                   = false;
		
		
		void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"Prefixo", "Oxide"},
				{"msg1", "GodMod active for 5s‼"}, 
				{"msg2", "Get Inventory Items!"},
				{"msg3", "Go‼"},
				{"Small", "Teleporting to Small in10s"},
				{"Big", "Teleporting to Big in 10s"},
				{"Hangar", "Teleporting to Hangar in 10s"},
				{"P250", "Teleporting to Area Of P250 in 10s"},
				{"Montain", "Teleporting to Mountain em 10s"},
		    };
            lang.RegisterMessages(messages, this);
        }	
		
		
		void Loaded() 
		{
			
			CheckCfg<bool>("Settings: Small", ref small);
			CheckCfg<bool>("Settings: Big", ref big);
			CheckCfg<bool>("Settings: Hangar", ref hangar);
			CheckCfg<bool>("Settings: P250", ref p250);
			CheckCfg<bool>("Settings: Montain", ref montain);
			
			SaveConfig();
			LoadDefaultMessages();
		}
		


		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}
		
		
		
		void ItemsSmall (NetUser netuser)
		{
			PegarItem(netuser, "9mm Ammo", 1500, "mochila");
			PegarItem(netuser, "556 Ammo", 1500, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "M4", 2, "mochila");
			PegarItem(netuser, "Holo sight", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Revolver", 2, "mochila");
			PegarItem(netuser, "Bolt Action Rifle", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Bandage", 20, "mochila");
			PegarItem(netuser, "Small Rations", 250, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Pipe Shotgun", 1, "mochila");
			PegarItem(netuser, "Handmade Shell", 250, "mochila");
			PegarItem(netuser, "P250", 1, "mochila");
			PegarItem(netuser,"Kevlar Helmet",1,"roupa");
			//PegarItem(netuser,"Cloth Vest",1,"roupa");
			PegarItem(netuser,"Leather Pants",1,"roupa");
			PegarItem(netuser,"Invisible Boots",1,"roupa");
			/// Customize Items For ^^
		}
		
		void ItemsBig(NetUser netuser)
		{
			PegarItem(netuser, "9mm Ammo", 1500, "mochila");
			PegarItem(netuser, "556 Ammo", 1500, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "M4", 2, "mochila");
			PegarItem(netuser, "Holo sight", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Revolver", 2, "mochila");
			PegarItem(netuser, "Bolt Action Rifle", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Bandage", 20, "mochila");
			PegarItem(netuser, "Small Rations", 250, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Pipe Shotgun", 1, "mochila");
			PegarItem(netuser, "Handmade Shell", 250, "mochila");
			PegarItem(netuser, "P250", 1, "mochila");	
			PegarItem(netuser,"Kevlar Helmet",1,"roupa");
			//PegarItem(netuser,"Cloth Vest",1,"roupa");
			PegarItem(netuser,"Leather Pants",1,"roupa");
			PegarItem(netuser,"Invisible Boots",1,"roupa");
			/// Customize Items For ^^
		}
		
		void ItemsHangar(NetUser netuser)
		{
			PegarItem(netuser, "9mm Ammo", 1500, "mochila");
			PegarItem(netuser, "556 Ammo", 1500, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "M4", 2, "mochila");
			PegarItem(netuser, "Holo sight", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Revolver", 2, "mochila");
			PegarItem(netuser, "Bolt Action Rifle", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Bandage", 20, "mochila");
			PegarItem(netuser, "Small Rations", 250, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Pipe Shotgun", 1, "mochila");
			PegarItem(netuser, "Handmade Shell", 250, "mochila");
			PegarItem(netuser, "P250", 1, "mochila");
			PegarItem(netuser,"Kevlar Helmet",1,"roupa");
			//PegarItem(netuser,"Cloth Vest",1,"roupa");
			PegarItem(netuser,"Leather Pants",1,"roupa");
			PegarItem(netuser,"Invisible Boots",1,"roupa");
			/// Customize Items For ^^
		}
		
		
		void ItemsP250(NetUser netuser)
		{
			PegarItem(netuser, "9mm Ammo", 1500, "mochila");
			PegarItem(netuser, "556 Ammo", 1500, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "M4", 2, "mochila");
			PegarItem(netuser, "Holo sight", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Revolver", 2, "mochila");
			PegarItem(netuser, "Bolt Action Rifle", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Bandage", 20, "mochila");
			PegarItem(netuser, "Small Rations", 250, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Pipe Shotgun", 1, "mochila");
			PegarItem(netuser, "Handmade Shell", 250, "mochila");
			PegarItem(netuser, "P250", 1, "mochila");
			PegarItem(netuser,"Kevlar Helmet",1,"roupa");
			//PegarItem(netuser,"Cloth Vest",1,"roupa");
			PegarItem(netuser,"Leather Pants",1,"roupa");
			PegarItem(netuser,"Invisible Boots",1,"roupa");			
			/// Customize Items For ^^
		}
		
		void ItemsMontain(NetUser netuser)
		{
			PegarItem(netuser, "9mm Ammo", 1500, "mochila");
			PegarItem(netuser, "556 Ammo", 1500, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "M4", 2, "mochila");
			PegarItem(netuser, "Holo sight", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Revolver", 2, "mochila");
			PegarItem(netuser, "Bolt Action Rifle", 2, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Bandage", 20, "mochila");
			PegarItem(netuser, "Small Rations", 250, "mochila");
			PegarItem(netuser, "Large Medkit", 10, "mochila");
			PegarItem(netuser, "Pipe Shotgun", 1, "mochila");
			PegarItem(netuser, "Handmade Shell", 250, "mochila");
			PegarItem(netuser, "P250", 1, "mochila");
			PegarItem(netuser,"Kevlar Helmet",1,"roupa");
			//PegarItem(netuser,"Cloth Vest",1,"roupa");
			PegarItem(netuser,"Leather Pants",1,"roupa");
			PegarItem(netuser,"Invisible Boots",1,"roupa");
			/// Customize Items For ^^
		}
		
		[ChatCommand("small")]
		void CmdSmall(NetUser netuser, string command, string[] args)
		{
			if(small){
				OnPlayerSpawn(netuser);
				Small(netuser);
			}
			timer.Once(11f,() =>  
			{
				ItemsSmall(netuser);
			});
			timer.Once(15,() =>  
			{
				Limpar(netuser);
				ItemsSmall(netuser);
			});
		}
		
		[ChatCommand("big")]
		void CmdBig(NetUser netuser, string command, string[] args)
		{
			if(big){
				OnPlayerSpawn(netuser);
				Big(netuser);
			}
			timer.Once(11f,() =>  
			{
				ItemsBig(netuser);
			});	
			timer.Once(15,() =>  
			{
				Limpar(netuser);
				ItemsSmall(netuser);
			});			
		}
		
		[ChatCommand("hangar")]
		void CmdHangar(NetUser netuser, string command, string[] args)
		{
			if(hangar){
				OnPlayerSpawn(netuser);
				Hangar(netuser);
			}
			timer.Once(11f,() =>  
			{
				ItemsHangar(netuser);
			});
			timer.Once(15,() =>  
			{
				Limpar(netuser);
				ItemsSmall(netuser);
			});
		}
		
		
		[ChatCommand("p250")]
		void CmdP250(NetUser netuser, string command, string[] args)
		{
			if(p250){
				OnPlayerSpawn(netuser);
				P250(netuser);
			}
			timer.Once(11f,() =>  
			{
				ItemsP250(netuser);
			});
			timer.Once(15,() =>  
			{
				Limpar(netuser);
				ItemsSmall(netuser);
			});
		} 
		
		[ChatCommand("montain")]
		void CmdMontain(NetUser netuser, string command, string[] args)
		{
			if(montain){
				OnPlayerSpawn(netuser);
				Montain(netuser);

			}
			timer.Once(11f,() =>  
			{
				ItemsMontain(netuser);
			});
			timer.Once(15,() =>  
			{
				Limpar(netuser);
				ItemsSmall(netuser);
			});
		}
		
		void OnPlayerSpawn(NetUser netuser)
		{
			timer.Once(10.1f, () =>
			{
				Limpar(netuser);
				ModoDeus(netuser);
				rust.Notice(netuser, GetMessage("msg1", netuser.userID.ToString())); 
				PegarItem(netuser,"Invisible Helmet",1,"roupa");
				PegarItem(netuser,"Invisible Vest",1,"roupa");
				PegarItem(netuser,"Invisible Pants",1,"roupa");
				PegarItem(netuser,"Invisible Boots",1,"roupa");
				SpawnKill(netuser);
			});
			
			timer.Once(12f,() =>  
			{
				rust.Notice(netuser,  GetMessage("msg2", netuser.userID.ToString())); 
			});
			
			timer.Once(15,() =>  
			{
				ModoDeusOf(netuser);
				rust.Notice(netuser,  GetMessage("msg3", netuser.userID.ToString())); 
			});
		}	
		
			
		void ModoDeus(NetUser targetuser)
		{
			if (!on) { targetuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(true); } else { targetuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false); }
		}
		void ModoDeusOf(NetUser targetuser)
		{
			if (!on) { targetuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false); } else { targetuser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false); }
		}
		
		void PegarItem(NetUser netUser, string item, int quantidade, string slot)
		{
			Inventory inv = netUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();		
			Inventory.Slot.Preference pref;
			if (slot == "roupa")
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor, false, Inventory.Slot.KindFlags.Armor);			
			else if (slot == "mao")
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.KindFlags.Belt);			
			else if(slot == "mochila")
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.KindFlags.Belt);	
			else
				pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.KindFlags.Belt);
			
			ItemDataBlock Item = DatablockDictionary.GetByName(item);
			Inventory inventario = netUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			inventario.AddItemAmount(Item, quantidade, pref);
			return;
		}
		
		void SpawnKill(NetUser netuser)	
		{
			rust.RunClientCommand(netuser, "input.bind Duck 1 None");
            rust.RunClientCommand(netuser, "input.bind Jump 3 None");
            rust.RunClientCommand(netuser, "input.bind Fire 3 None");
            rust.RunClientCommand(netuser, "input.bind AltFire 7 None");
            rust.RunClientCommand(netuser, "input.bind Up 6 RightArrow");
            rust.RunClientCommand(netuser, "input.bind Down 6 LeftArrow");
            rust.RunClientCommand(netuser, "input.bind Left 6 UpArrow");
            rust.RunClientCommand(netuser, "input.bind Right 7 DownArrow");
            rust.RunClientCommand(netuser, "input.bind Flashlight 8 Insert");
			timer.Once(5f, () =>
			{
        	rust.RunClientCommand(netuser, "config.load");
			});
		}
		
		void Small (NetUser netUser)
		{
			rust.SendChatMessage(netUser, GetMessage("Prefixo", netUser.userID.ToString()), GetMessage("Small", netUser.userID.ToString())); 
			timer.Once(10f, () =>
			{
				float x = Single.Parse("6078");
				float y = Single.Parse("377");
				float z = Single.Parse("-3564");
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netUser.playerClient.netPlayer, new Vector3(x, y, z));
			});
		}
		
		void Big (NetUser netUser)
		{
			rust.SendChatMessage(netUser, GetMessage("Prefixo", netUser.userID.ToString()), GetMessage("Big", netUser.userID.ToString())); 
			timer.Once(10f, () =>
			{
				float x = Single.Parse("5411");
				float y = Single.Parse("340");
				float z = Single.Parse("-5367");
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netUser.playerClient.netPlayer, new Vector3(x, y, z));
			});
		}
		
		void Hangar (NetUser netUser)
		{
			rust.SendChatMessage(netUser, GetMessage("Prefixo", netUser.userID.ToString()), GetMessage("Hangar", netUser.userID.ToString())); 
			timer.Once(10f, () =>
			{
				float x = Single.Parse("6746");
				float y = Single.Parse("349");
				float z = Single.Parse("-4246");
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netUser.playerClient.netPlayer, new Vector3(x, y, z));
			});
		}
		
		
		void P250 (NetUser netUser)
		{
			rust.SendChatMessage(netUser, GetMessage("Prefixo", netUser.userID.ToString()), GetMessage("P250", netUser.userID.ToString())); 
			timer.Once(10f, () =>
			{
				float x = Single.Parse("5441");
				float y = Single.Parse("310");
				float z = Single.Parse("5576");
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netUser.playerClient.netPlayer, new Vector3(x, y, z));
			});
		}
		
		void Montain (NetUser netUser)
		{
			rust.SendChatMessage(netUser, GetMessage("Prefixo", netUser.userID.ToString()), GetMessage("Montain", netUser.userID.ToString())); 
			timer.Once(10f, () =>
			{
				float x = Single.Parse("2480");
				float y = Single.Parse("1258");
				float z = Single.Parse("-4095");
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netUser.playerClient.netPlayer, new Vector3(x, y, z));
			});
		}
		
		void Limpar(NetUser netuser)
		{
            var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			inv.Clear();
		}	
		
		
        string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
	}
}