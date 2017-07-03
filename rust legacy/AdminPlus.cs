using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Admin Plus", "P0LENT4", "1.1.0")]
	[Description("Plugin to make administrative work easier")]

	class AdminPlus : RustLegacyPlugin 
	{
		static string tag = "Oxide";
		
		string GetMessage(string key, string Id = null) => lang.GetMessage(key, this, Id);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>
			{
				{"Access", "You can not execute this command"},
				{"Help1", "/tpadmin [name] (Teleports you 30units away from the target)"},
				{"Help2", "/tpback (Teleports you back to where you were)"},
				{"Help3", "/admin on (Informs that it is planted and receives administrative items...)"},
				{"Help4", "/admin off (Informs you that you are no longer on duty...)"},
				{"Help5", "/admin metal (Spawns metal building parts)"},
				{"Help6", "/admin wood (Spawns wood building parts)"},
				{"Help7", "/admin uber (Spawns uber items)"},
				{"Help8", "/admin kevlar (Spawns kevlar)"},
				{"Help9", "/admin clear (Clears your inventory)"},
				{"Help10", "/admin weapons (Gives you weapons + Ammo)"},
				
				{"Dutyon", "{0} is now on duty! Let him/her know if you need anything!"},
				{"Dutyoff", "{0} is now off duty! Please direct questions to another admin!"},
				
				{"SuccessWood", "Wood building parts spawned!"},
				{"SuccessMetal", "Metal building parts spawned!"},
				{"SuccessWeapons", "Weapons plus Ammo spawned!"},
				{"SuccessUber", "Uber items spawned!"},
				{"SuccessKevlar", "I hope you have a legitimate reason why need this!"},
				{"SuccessTele1", "[color green]You were teleported to [color red]{0}"},
				{"SuccessTele2", "[color green]You have returned to your last position."},
				{"Successhora", "Now it's [color green] {0} [/color] Hours"},
				{"SuccessPerm1", "Permission {0} granted to {1}!"},
				{"SuccessPerm2", "You have received {0} permission from {1}!"},
				{"Successunp", "Permission {0} Revoked to {1}!"},
				{"Successung", "You removed {0} from the group {1}"},
				{"Successgroup1", "You added {0} to group {1}!"},
				{"Successgroup2", "{0} added you to group {1}!"},
				
				{"erro1", "Use /tpadmin playername"},
				{"erro2", "Player not found"},
				{"erro3", "Could not find your last location."},
				{"erro4", "Use /addp nickname permission"},
				{"erro5", "Use /unp nickname permission"},
				{"erro6", "Use /addg nickname group"},
				{"erro7", "Use /ung nickname group"}
			}; 
			lang.RegisterMessages(message, this);
		}
		
		void OnServerInitialized(){
			CheckCfg<string>("Settings: Prefix", ref tag);
			LoadDefaultMessages();
			SaveConfig();
            permission.RegisterPermission("adminplus.use", this);

		}
		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}
		
		static Dictionary<string, Vector3> teleportBack = new Dictionary<string, Vector3>();
		
		
		bool Acesso(NetUser netUser)
        {
            if (netUser.CanAdmin()) { return true; }
			if(permission.UserHasPermission(netUser.playerClient.userID.ToString(), "adminplus")) return true;
            return false;
        }
		
		[ChatCommand("admin")]
		void cmdAdmin(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if(args.Length == 0)
			{
				help(netuser);
			}
			switch(args[0])
			{
				case "on":
				
				GiveItem(netuser, "Invisible Helmet", 1);
				GiveItem(netuser, "Invisible Vest", 1);
				GiveItem(netuser, "Invisible Pants", 1);
				GiveItem(netuser, "Invisible Boots", 1);
				GiveItem(netuser, "Uber Hunting Bow", 1);
				GiveItem(netuser, "ber Hatchet", 1);
				GiveItem(netuser, "Arrow", 1);
				GiveItem(netuser, "P250", 1);
                rust.BroadcastChat(tag, string.Format(GetMessage("Dutyon"), netuser.displayName));
				
				break;
				case "off":
				
				rust.BroadcastChat(tag, string.Format(GetMessage("Dutyoff"), netuser.displayName));
				
				break;
				case "wood":
				
				GiveItem(netuser, "Wood Pillar", 250);
				GiveItem(netuser, "Wood Foundation", 250);
				GiveItem(netuser, "Wood Doorway", 250);
				GiveItem(netuser, "Wood Window", 250);
				GiveItem(netuser, "Wood Stairs", 250);
				GiveItem(netuser, "Wood Ramp", 250);
				GiveItem(netuser, "Wood Ceiling", 250);
				GiveItem(netuser, "Metal Door", 15);
				GiveItem(netuser, "Metal Window Bars", 15);
                rust.SendChatMessage(netuser, tag, GetMessage("SuccessWood", Id));
				
				break;
				case "metal":
				
				GiveItem(netuser, "Metal Pillar", 250);
				GiveItem(netuser, "Metal Foundation", 250);
				GiveItem(netuser, "Metal Doorway", 250);
				GiveItem(netuser, "Metal Window", 250);
				GiveItem(netuser, "Metal Stairs", 250);
				GiveItem(netuser, "Metal Ramp", 250);
				GiveItem(netuser, "Metal Ceiling", 250);
				GiveItem(netuser, "Metal Door", 15);
				GiveItem(netuser, "Metal Window Bars", 15);
                rust.SendChatMessage(netuser, tag, GetMessage("SuccessMetal", Id));
				
				break;
				case "weapons":
				
				GiveItem(netuser, "Bolt Action Rifle", 1);
				GiveItem(netuser, "M4", 1);
				GiveItem(netuser, "MP5A4", 1);
				GiveItem(netuser, "9mm Pistol", 1);
				GiveItem(netuser, "P250", 1);
				GiveItem(netuser, "Shotgun", 1);
				GiveItem(netuser, "556 Ammo", 250);
				GiveItem(netuser, "9mm Ammo", 250);
				GiveItem(netuser, "Shotgun Shells", 250);
                rust.SendChatMessage(netuser, tag, GetMessage("SuccessWeapons", Id));
				
				break;
				case "uber":
				
				GiveItem(netuser, "Uber Hatchet", 1);
				GiveItem(netuser, "Uber Hunting Bow", 1);
				GiveItem(netuser, "Arrow", 40);
                rust.SendChatMessage(netuser, tag, GetMessage("SuccessUber", Id));
				
				break;
				case "kevlar":
				
				GiveItem(netuser, "Kevlar Helmet", 1);
				GiveItem(netuser, "Kevlar Vest", 1);
				GiveItem(netuser, "Kevlar Pants", 1);
				GiveItem(netuser, "Kevlar Boots", 1);
				
                rust.SendChatMessage(netuser, tag, GetMessage("SuccessKevlar", Id));
				
				break;
				case "clear":
				
				var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
				inv.Clear();
				
				break;
				default:
				{
					help(netuser);
					break;
				}
				
			}
			
		}
		[ChatCommand("tpadmin")]
		void cmdTpAdmin(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if (args.Length == 0) { rust.SendChatMessage(netuser, tag, GetMessage("erro1", Id)); return; }
			if (args.Length == 1)
			{
				NetUser targetuser = rust.FindPlayer(args[0]);
				if (targetuser == null)
                {
                    rust.Notice(netuser, tag, GetMessage("erro2", Id));
                    return;
                }
                if (!teleportBack.ContainsKey(Id)) 
				{
					teleportBack.Add(Id, netuser.playerClient.lastKnownPosition);
				}
				var management = RustServerManagement.Get();
                management.TeleportPlayerToPlayer(netuser.playerClient.netPlayer, targetuser.playerClient.netPlayer);
				rust.SendChatMessage(netuser, tag, string.Format(GetMessage("SuccessTele1", Id), targetuser.displayName));
                return;
			}
		}
		[ChatCommand("tpback")]
		void cmdTpback(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if(teleportBack.ContainsKey(Id))
			{
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, teleportBack[Id]);
				rust.SendChatMessage(netuser, tag, GetMessage("SuccessTele2", Id));
			}
			else{
				rust.SendChatMessage(netuser, tag, GetMessage("erro3", Id));//
			}
		}
		
		[ChatCommand("hour")]
		void cmdhour(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
            rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Successhora", Id), EnvironmentControlCenter.Singleton.GetTime().ToString()));
		}
		
		[ChatCommand("addp")]
		void cmdAddp(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			NetUser targetuser = rust.FindPlayer(args[0]);
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if(args.Length != 2) {rust.SendChatMessage(netuser, tag, GetMessage("erro4", Id)); return;}
			if (targetuser == null) { rust.SendChatMessage(netuser, tag, GetMessage("erro2", Id)); return; }
			var permissao = args[1];
			
			rust.RunServerCommand("oxide.grant user "+targetuser.playerClient.userID.ToString()+ " "+ permissao);
			rust.SendChatMessage(netuser, tag, string.Format(GetMessage("SuccessPerm1", Id), permissao, targetuser.displayName));
			rust.SendChatMessage(targetuser, tag, string.Format(GetMessage("SuccessPerm2", Id), permissao, netuser.displayName));
			
		}
		[ChatCommand("unp")]
		void cmdUnp(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			NetUser targetuser = rust.FindPlayer(args[0]);
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if(args.Length != 2) {rust.SendChatMessage(netuser, tag, GetMessage("erro5", Id)); return;}
			if (targetuser == null) { rust.SendChatMessage(netuser, tag, GetMessage("erro2", Id)); return; }
			var permissao = args[1];
			
			rust.RunServerCommand("oxide.revoke user "+targetuser.playerClient.userID.ToString()+ " "+ permissao);
			rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Successunp", Id), permissao, targetuser.displayName));
			
			
		}
		[ChatCommand("addg")]
		void cmdAddg(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			NetUser targetuser = rust.FindPlayer(args[0]);
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if(args.Length != 2) {rust.SendChatMessage(netuser, tag, GetMessage("erro6", Id)); return;}
			if (targetuser == null) { rust.SendChatMessage(netuser, tag, GetMessage("erro2", Id)); return; }
			var grupo = args[1];
			
			rust.RunServerCommand("oxide.usergroup add "+targetuser.playerClient.userID.ToString()+ " "+ grupo);
			rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Successgroup1", Id),targetuser.displayName, grupo));
			rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Successgroup2", Id),netuser.displayName, grupo));
			
		}
		[ChatCommand("ung")]
		void cmdUng(NetUser netuser, string command, string[] args)
		{
			var Id = netuser.userID.ToString();
			NetUser targetuser = rust.FindPlayer(args[0]);
			if (!Acesso(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Access", Id)); return; }
			if(args.Length != 2) {rust.SendChatMessage(netuser, tag, GetMessage("erro7", Id)); return;}
			if (targetuser == null) { rust.SendChatMessage(netuser, tag, GetMessage("erro2", Id)); return; }
			var grupo = args[1];
			
			rust.RunServerCommand("oxide.usergroup remove "+targetuser.playerClient.userID.ToString()+ " "+ grupo);
			rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Successung", Id), targetuser.displayName, grupo));
			
		}
		
		void GiveItem(NetUser netUser, string item, int quantidade)
		{
			ItemDataBlock Item = DatablockDictionary.GetByName(item);
			Inventory inventario = netUser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
			inventario.AddItemAmount(Item, quantidade);
			return;
		}
		
		void help(NetUser netuser)
		{
			var Id = netuser.userID.ToString();
			rust.SendChatMessage(netuser, tag, GetMessage("Help1", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help2", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help3", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help4", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help5", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help6", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help7", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help8", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help9", Id));
			rust.SendChatMessage(netuser, tag, GetMessage("Help10", Id));
			return;
		}
		
		
	}
}