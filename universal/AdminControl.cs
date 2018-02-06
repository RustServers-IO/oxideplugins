/*
		[PluginReference]
		Plugin AdminControl;

		bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
		if(IsAdmin)
//
		IsOnwer
		IsAdmin
		IsMod
		IsHelper
//
	English - Use this reference to put as permission on another plugin.
	Portugues Brasileiro - Use essa referência para colocar como permissão em outra plugin.
*/
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System;

/*

MADE BY
.----           ---,---  .----
| __    |    |     |     |___
|   |   |    |     |     |
`^--'   `----^     |     `^---
By: ~Gute

*/

namespace Oxide.Plugins
{
	[Info("AdminControl", "Gute", "1.0.0")]
	[Description("Some profiles to have more control in your admins, mods among others.")]

	class AdminControl : RustLegacyPlugin
	{
		static string permissionCanRcon;
		static string permissionCanOwner;
		static string permissionCanAdmin;
		static string permissionCanMod;
		static string permissionCanHelper;

		static bool commandOwner;
		static bool commandAdmin;
		static bool commandMod;
		static bool commandHelper;
		static bool commandLogin;
		static bool commandChat;
		static bool commandAdd;
		static bool commandDell;

		static string OwnerColor;
		static string AdminColor;
		static string ModColor;
		static string HelperColor;
		static string VipColor;
		static string YoutubeColor;
		static string PromoterColor;

		static string OwnerTag;
		static string AdminTag;
		static string ModTag;
		static string HelperTag;
		static string VipTag;
		static string YoutubeTag;
		static string PromoterTag;

		void Init()
		{
			SetupConfig();
			SetupLang();
			SetupChatCommands();
			SetupPermissions();
			return;
		}

		void SetupPermissions()
		{
			permission.RegisterPermission(permissionCanOwner, this);
			permission.RegisterPermission(permissionCanAdmin, this);
			permission.RegisterPermission(permissionCanMod, this);
			permission.RegisterPermission(permissionCanHelper, this);
			return;
		}

		void SetupChatCommands()
		{
			if (commandOwner)
			cmd.AddChatCommand("owner", this, "cmdOwner");

			if (commandAdmin)
			cmd.AddChatCommand("admin", this, "cmdAdmin");

			if (commandMod)
			cmd.AddChatCommand("mod", this, "cmdMod");

			if (commandHelper)
			cmd.AddChatCommand("helper", this, "cmdHelper");

			if (commandLogin)
			cmd.AddChatCommand("login", this, "cmdLogin");

			if (commandChat)
			cmd.AddChatCommand("a", this, "cmdChat");

			if (commandAdd)
			cmd.AddChatCommand("add", this, "cmdAdd");

			if (commandDell)
			cmd.AddChatCommand("dell", this, "cmdDell");
		}

		void SetupLang()
		{
            // English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"ChatTag", "AdminControl"},

				{"NoPermission", "You do not have permission to use this command."},
				{"NoExistent", "No player found with the existing name '{0}'."},
				{"NoCommand", "Command '{0}' does not exist."},

				{"OnBroadcast", "Player {0} active access to administration."},
				{"OffBroadcast", "Player {0} has disabled admin access."},

				{"AddOwner1", "Player {0} is already owner."},
				{"AddOwner2", "You have added {0} as owner."},
				{"AddOwner3", "{0} added you as owner."},
				{"AddAdmin1", "The player {0} is already admin."},
				{"AddAdmin2", "You have added {0} as admin."},
				{"AddAdmin3", "{0} added you as admin."},
				{"AddMod1", "Player {0} is already mod."},
				{"AddMod2", "You have added {0} as mod."},
				{"AddMod3", "{0} added you as mod."},
				{"AddHelper1", "Player {0} is already helper."},
				{"AddHelper2", "You have added {0} as helper."},
				{"AddHelper3", "{0} added you as helper."},
				{"AddVip1", "Player {0} is already vip."},
				{"AddVip2", "You have added {0} as vip."},
				{"AddVip3", "{0} added you as vip."},
				{"AddYt1", "Player {0} is already youtube."},
				{"AddYt2", "You have added {0} as youtube."},
				{"AddYt3", "{0} added you as youtube."},
				{"AddDv1", "Player {0} is already promoter."},
				{"AddDv2", "You have added {0} as promoter."},
				{"AddDv3", "{0} added you as promoter."},

				{"RemoveOwner1", "You have removed the permission of {0} as the owner."},
				{"RemoveOwner2", "{0} has removed its owner permission."},
				{"RemoveOwner3", "Player {0} is not owner."},
				{"RemoveAdmin1", "Você removeu a permissão de {0} como admin."},
				{"RemoveAdmin2", "{0} has removed its admin permission."},
				{"RemoveAdmin3", "Player {0} is not admin."},
				{"RemoveMod1", "You have removed the permission of {0} as the mod."},
				{"RemoveMod2", "{0} has removed its mod permission."},
				{"RemoveMod3", "Player {0} is not mod."},
				{"RemoveHelper1", "You have removed the permission of {0} as the helper."},
				{"RemoveHelper2", "{0} has removed its helper permission."},
				{"RemoveHelper3", "Player {0} is not helper."},
				{"RemoveVip1", "You have removed the permission of {0} as the vip."},
				{"RemoveVip2", "{0} has removed its vip permission."},
				{"RemoveVip3", "Player {0} is not vip."},
				{"RemoveYt1", "You have removed the permission of {0} as the youtube."},
				{"RemoveYt2", "{0} has removed its youtube permission."},
				{"RemoveYt3", "Player {0} is not youtube."},
				{"RemoveDv1", "You have removed the permission of {0} as the promoter."},
				{"RemoveDv2", "{0} has removed its promoter permission."},
				{"RemoveDv3", "Player {0} is not promoter."},

				{"Disconnected1", "Owner: {0} left the server."},
				{"Disconnected2", "Admin : {0} left the server."},
				{"Disconnected3", "Mod : {0} left the server."},
				{"Disconnected4", "Helper : {0} left the server."},

				{"Connected1", "Owner : {0} entered the server."},
				{"Connected2", "Admin : {0} entered the server."},
				{"Connected3", "Mod : {0} entered the server."},
				{"Connected4", "Helper : {0} entered the server."},

				{"SyntaxAdd", "Syntax: Use /add <user> <cmd>"},
				{"SyntaxDell", "Syntax: Use /dell <user> <cmd>"},

				{"Login", "{0} entered the administration with access to rcon."},
				{"SyntaxLogin", "Syntax: Use /login <password>"},
				{"KickRcon", "Player {0} was kicked when trying to access rcon."},

				{"SyntaxChat", "Syntax: Use /a <message>"},
				{"Chat", "{0} : {1}"}
			}, this);

            // Portugues Brasileiro
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"ChatTag", "AdminControl"},

				{"NoPermission", "Você não tem permissão para usa esse comando."},
				{"NoExistent", "Nenhum jogador encontrado com o nome '{0}' existente."},
				{"NoCommand", "Comando '{0}' não existente."},

				{"OnBroadcast", "O jogador {0} ativo seu acesso à administração."},
				{"OffBroadcast", "O jogador {0} desativo seu acesso à administração."},

				{"AddOwner1", "O jogador {0} ja é owner."},
				{"AddOwner2", "Você adicionou {0} como owner."},
				{"AddOwner3", "{0} adicionou você como owner."},
				{"AddAdmin1", "O jogador {0} ja é admin."},
				{"AddAdmin2", "Você adicionou {0} como admin."},
				{"AddAdmin3", "{0} adicionou você como admin."},
				{"AddMod1", "O jogador {0} ja é mod."},
				{"AddMod2", "Você adicionou {0} como mod."},
				{"AddMod3", "{0} adicionou você como mod."},
				{"AddHelper1", "O jogador {0} ja é helper."},
				{"AddHelper2", "Você adicionou {0} como helper."},
				{"AddHelper3", "{0} adicionou você como helper."},
				{"AddVip1", "O jogador {0} ja é vip."},
				{"AddVip2", "Você adicionou {0} como vip."},
				{"AddVip3", "{0} adicionou você como vip."},
				{"AddYt1", "O jogador {0} ja é yt."},
				{"AddYt2", "Você adicionou {0} como yt."},
				{"AddYt3", "{0} adicionou você como yt."},
				{"AddDv1", "O jogador {0} ja é dv."},
				{"AddDv2", "Você adicionou {0} como dv."},
				{"AddDv3", "{0} adicionou você como dv."},

				{"RemoveOwner1", "Você removeu a permissão de {0} como owner."},
				{"RemoveOwner2", "{0} removeu sua permissão de owner."},
				{"RemoveOwner3", "O jogador {0} não é owner."},
				{"RemoveAdmin1", "Você removeu a permissão de {0} como admin."},
				{"RemoveAdmin2", "{0} removeu sua permissão de admin."},
				{"RemoveAdmin3", "O jogador {0} não é admin."},
				{"RemoveMod1", "Você removeu a permissão de {0} como mod."},
				{"RemoveMod2", "{0} removeu sua permissão de mod."},
				{"RemoveMod3", "O jogador {0} não é mod."},
				{"RemoveHelper1", "Você removeu a permissão de {0} como helper."},
				{"RemoveHelper2", "{0} removeu sua permissão de helper."},
				{"RemoveHelper3", "O jogador {0} não é helper."},
				{"RemoveVip1", "Você removeu a permissão de {0} como vip."},
				{"RemoveVip2", "{0} removeu sua permissão de vip."},
				{"RemoveVip3", "O jogador {0} não é vip."},
				{"RemoveYt1", "Você removeu a permissão de {0} como yt."},
				{"RemoveYt2", "{0} removeu sua permissão de yt."},
				{"RemoveYt3", "O jogador {0} não é yt."},
				{"RemoveDv1", "Você removeu a permissão de {0} como dv."},
				{"RemoveDv2", "{0} removeu sua permissão de dv."},
				{"RemoveDv3", "O jogador {0} não é dv."},

				{"Disconnected1", "Owner : {0} saiu do servidor."},
				{"Disconnected2", "Admin : {0} saiu do servidor."},
				{"Disconnected3", "Mod : {0} saiu do servidor."},
				{"Disconnected4", "Helper : {0} saiu do servidor."},

				{"Connected1", "Owner : {0} entrou no servidor."},
				{"Connected2", "Admin : {0} entrou no servidor."},
				{"Connected3", "Mod : {0} entrou no servidor."},
				{"Connected4", "Helper : {0} entrou no servidor."},

				{"SyntaxAdd", "Syntax: Use /add <user> <cmd>"},
				{"SyntaxDell", "Syntax: Use /dell <user> <cmd>"},

				{"Login", "{0} entrou na administração com acesso a rcon."},
				{"SyntaxLogin", "Syntax: Use /login <senha>"},
				{"KickRcon", "Jogador {0} foi kick ao tentar acessar rcon."},

				{"SyntaxChat", "Syntax: Use /a <msg>"},
				{"Chat", "{0} : {1}"}
			}, this, "pt-br");
			return;
		}

		protected override void LoadDefaultConfig()
		{
			Config["SettingsPermission"] = new Dictionary<string, object>
			{
				{"permissionCanRcon", "admincontrol.rcon"},
				{"permissionCanOwner", "admincontrol.canowner"},
				{"permissionCanAdmin", "admincontrol.canadmin"},
				{"permissionCanMod", "admincontrol.canmod"},
				{"permissionCanHelper", "admincontrol.canhelper"}
			};
				Config["SettingsCommand"] = new Dictionary<string, object>
			{
				{"commandOwner", true},
				{"commandAdmin", true},
				{"commandMod", true},
				{"commandHelper", true},
				{"commandLogin", true},
				{"commandChat", true},
				{"commandDell", true},
				{"commandAdd", true}
			};
				Config["SettingsTag"] = new Dictionary<string, object>
			{
				{"OwnerTag", "[Owner]"},
				{"AdminTag", "[Admin]"},
				{"ModTag", "[Mod]"},
				{"HelperTag", "[Helper]"},
				{"VipTag", "[VIP]"},
				{"YoutubeTag", "[YT]"},
				{"PromoterTag", "[DV]"}
			};
				Config["SettingsColor"] = new Dictionary<string, object>
			{
				{"OwnerColor", "[Color Lime]"},
				{"AdminColor", "[Color Magenta]"},
				{"ModColor", "[Color Orange]"},
				{"HelperColor", "[Color Cyan]"},
				{"VipColor", "[Color Yellow]"},
				{"YoutubeColor", "[Color Red]"},
				{"PromoterColor", "[Color Purple]"}
			};
		}

		void SetupConfig()
		{
			permissionCanRcon = Config.Get<string>("SettingsPermission", "permissionCanRcon");
			permissionCanOwner = Config.Get<string>("SettingsPermission", "permissionCanOwner");
			permissionCanAdmin = Config.Get<string>("SettingsPermission", "permissionCanAdmin");
			permissionCanMod = Config.Get<string>("SettingsPermission", "permissionCanMod");
			permissionCanHelper = Config.Get<string>("SettingsPermission", "permissionCanHelper");

			commandOwner = Config.Get<bool>("SettingsCommand", "commandOwner");
			commandAdmin = Config.Get<bool>("SettingsCommand", "commandAdmin");
			commandMod = Config.Get<bool>("SettingsCommand", "commandMod");
			commandHelper = Config.Get<bool>("SettingsCommand", "commandHelper");
			commandLogin = Config.Get<bool>("SettingsCommand", "commandLogin");
			commandChat = Config.Get<bool>("SettingsCommand", "commandChat");
			commandAdd = Config.Get<bool>("SettingsCommand", "commandAdd");
			commandDell = Config.Get<bool>("SettingsCommand", "commandDell");

			OwnerTag = Config.Get<string>("SettingsTag", "OwnerTag");
			AdminTag = Config.Get<string>("SettingsTag", "AdminTag");
			ModTag = Config.Get<string>("SettingsTag", "ModTag");
			HelperTag = Config.Get<string>("SettingsTag", "HelperTag");
			VipTag = Config.Get<string>("SettingsTag", "VipTag");
			YoutubeTag = Config.Get<string>("SettingsTag", "YoutubeTag");
			PromoterTag = Config.Get<string>("SettingsTag", "PromoterTag");

			OwnerColor = Config.Get<string>("SettingsColor", "OwnerColor");
			AdminColor = Config.Get<string>("SettingsColor", "AdminColor");
			ModColor = Config.Get<string>("SettingsColor", "ModColor");
			HelperColor = Config.Get<string>("SettingsColor", "HelperColor");
			VipColor = Config.Get<string>("SettingsColor", "VipColor");
			YoutubeColor = Config.Get<string>("SettingsColor", "YoutubeColor");
			PromoterColor = Config.Get<string>("SettingsColor", "PromoterColor");
		}

		private Core.Configuration.DynamicConfigFile Data;
		void LoadData(){Profile = Interface.GetMod().DataFileSystem.ReadObject<ProfileData>("AdminControl");}
		void SaveData(){Interface.GetMod().DataFileSystem.WriteObject("AdminControl", Profile);}
		void OnServerSave(){SaveData();}
		void Unload(){SaveData();}
		void Loaded(){LoadData();}

		ProfileData Profile;
		class ProfileData
		{
			public Dictionary<string, string> owner = new Dictionary<string, string>();
			public Dictionary<string, string> admin = new Dictionary<string, string>();
			public Dictionary<string, string> mod = new Dictionary<string, string>();
			public Dictionary<string, string> helper = new Dictionary<string, string>();
			public Dictionary<string, string> vip = new Dictionary<string, string>();
			public Dictionary<string, string> yt = new Dictionary<string, string>();
			public Dictionary<string, string> dv = new Dictionary<string, string>();
		}

		private void OnPlayerConnected(NetUser netUser)
		{
			var Id = netUser.userID.ToString();
			var username = netUser.displayName;
			{
				foreach(PlayerClient player in PlayerClient.All)
			{
				var playerId = player.userID.ToString();
				if (Profile.owner.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Connected1", this, Id), username));
				return;

				if (Profile.admin.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Connected2", this, Id), username));
				return;

				if (Profile.mod.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Connected3", this, Id), username));
				return;

				if (Profile.helper.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Connected4", this, Id), username));
				return;
				}
			}
		}

		private void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer)
		{
			NetUser netUser = netPlayer.GetLocalData() as NetUser;
			var Id = netUser.userID.ToString();
			var username = netUser.displayName;
			{
				foreach(PlayerClient player in PlayerClient.All)
			{
				var playerId = player.userID.ToString();
				if (Profile.owner.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Disconnected1", this, Id), username));
				return;

				if (Profile.admin.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Disconnected2", this, Id), username));
				return;

				if (Profile.mod.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Disconnected3", this, Id), username));
				return;

				if (Profile.helper.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Disconnected4", this, Id), username));
				return;
				}
			}
		}

		bool IsOwner(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			if(netUser.CanAdmin())return true;
			if(Profile.owner.ContainsKey(userid))return true;
			return false;
		}

		bool IsAdmin(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			if(netUser.CanAdmin())return true;
			if(Profile.owner.ContainsKey(userid))return true;
			if(Profile.admin.ContainsKey(userid))return true;
			return false;
		}

		bool IsMod(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			if(netUser.CanAdmin())return true;
			if(Profile.owner.ContainsKey(userid))return true;
			if(Profile.admin.ContainsKey(userid))return true;
			if(Profile.mod.ContainsKey(userid))return true;
			return false;
		}

		bool IsHelper(NetUser netUser)
		{
			var userid = netUser.userID.ToString();
			if(netUser.CanAdmin())return true;
			if(Profile.owner.ContainsKey(userid))return true;
			if(Profile.admin.ContainsKey(userid))return true;
			if(Profile.mod.ContainsKey(userid))return true;
			if(Profile.helper.ContainsKey(userid))return true;
			return false;
		}

		object OnPlayerChat(NetUser netUser, string message)
		{
			var username = netUser.displayName.ToString();
			var userid = netUser.userID.ToString();
			{
				if(Profile.owner.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(OwnerTag + username);
				string msg =  rust.QuoteSafe(OwnerColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				if(Profile.admin.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(AdminTag + username);
				string msg =  rust.QuoteSafe(AdminColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				if(Profile.mod.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(ModTag + username);
				string msg =  rust.QuoteSafe(ModColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				if(Profile.helper.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(HelperTag + username);
				string msg =  rust.QuoteSafe(HelperColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				if(Profile.vip.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(VipTag + username);
				string msg =  rust.QuoteSafe(VipColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				if(Profile.yt.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(YoutubeTag + username);
				string msg =  rust.QuoteSafe(YoutubeColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				if(Profile.dv.ContainsKey(userid))
			{
				string name = rust.QuoteSafe(PromoterTag + username);
				string msg =  rust.QuoteSafe(PromoterColor + message);
				ConsoleNetworker.Broadcast(string.Concat("chat.add ", name, " ", msg));
				return false;
			}
				return null;
			}
		}

		void cmdOwner(NetUser netUser, string command, string[] args)
		{
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			if (!(netUser.CanAdmin() || permission.UserHasPermission(Id, permissionCanOwner)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
				return;
			}
				if(Profile.owner.ContainsKey(Id))
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OffBroadcast", this, Id), username));
				Profile.owner.Remove(Id);
				netUser.SetAdmin(false);
				SaveData();
			}
				else
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OnBroadcast", this, Id), username));
				Profile.owner.Add(Id, username);
				netUser.SetAdmin(true);
				SaveData();
			}
				return;
		}

		void cmdAdmin(NetUser netUser, string command, string[] args)
		{
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			if (!(netUser.CanAdmin() || permission.UserHasPermission(Id, permissionCanAdmin)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
				return;
			}
				if(Profile.admin.ContainsKey(Id))
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OffBroadcast", this, Id), username));
				Profile.admin.Remove(Id);
				SaveData();
			}
				else
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OnBroadcast", this, Id), username));
				Profile.admin.Add(Id, username);
				SaveData();
			}
				return;
		}

		void cmdMod(NetUser netUser, string command, string[] args)
		{
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			if (!(netUser.CanAdmin() || permission.UserHasPermission(Id, permissionCanMod)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
				return;
			}
				if(Profile.mod.ContainsKey(Id))
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OffBroadcast", this, Id), username));
				Profile.mod.Remove(Id);
				SaveData();
			}
				else
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OnBroadcast", this, Id), username));
				Profile.mod.Add(Id, username);
				SaveData();
			}
				return;
		}

		void cmdHelper(NetUser netUser, string command, string[] args)
		{
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			if (!(netUser.CanAdmin() || permission.UserHasPermission(Id, permissionCanHelper)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
				return;
			}
				if(Profile.helper.ContainsKey(Id))
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OffBroadcast", this, Id), username));
				Profile.helper.Remove(Id);
				SaveData();
			}
				else
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("OnBroadcast", this, Id), username));
				Profile.helper.Add(Id, username);
				SaveData();
			}
				return;
		}

		void cmdChat(NetUser netUser, string command, string[] args)
		{
			var Iid = netUser.userID.ToString();
			var username = netUser.displayName;
			if(!IsHelper(netUser))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Iid), lang.GetMessage("NoPermission", this, Iid));
				return;
			}
				if (args.Length == 0)
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Iid), lang.GetMessage("SyntaxChat", this, Iid));
				return;
			}
				string message = string.Join(" ", args);
				foreach(PlayerClient player in PlayerClient.All)
			{
				var id = player.userID.ToString();
				if (netUser.CanAdmin() || Profile.owner.ContainsKey(id) || Profile.admin.ContainsKey(id) || Profile.owner.ContainsKey(id) || Profile.mod.ContainsKey(id) || Profile.helper.ContainsKey(id))
				rust.SendChatMessage(netUser, "[AdminChat]" ,string.Format(lang.GetMessage("Chat", this, Iid), username, message));
			}
		}

		void cmdLogin(NetUser netUser, string command, string[] args)
		{
			var Id = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(args.Length == 0)
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("SyntaxLogin", this, Id));
				return;
			}
				string rcon = (args[0]);
				if(rcon == permissionCanRcon)
				foreach(PlayerClient player in PlayerClient.All)
			{
				var playerId = player.userID.ToString();
				netUser.SetAdmin(true);
				if (netUser.CanAdmin() || Profile.owner.ContainsKey(playerId) || Profile.admin.ContainsKey(playerId) || Profile.owner.ContainsKey(playerId) || Profile.mod.ContainsKey(playerId) || Profile.helper.ContainsKey(playerId))
				rust.SendChatMessage(player.netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Login", this, Id), username));
				return;
			}
				else
			{
				rust.BroadcastChat(lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("KickRcon", this, Id), username));
				netUser.Kick(NetError.Facepunch_Kick_Violation, true);
			}
		}

		void cmdAdd(NetUser netUser, string command, string[] args)
		{
			var Id = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(!IsOwner(netUser))
			{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
					return;
				}
					else if (args.Length != 2)
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("SyntaxAdd", this, Id));
				}
					else
				{
					NetUser targetUser = rust.FindPlayer(args[0]);
					var name = targetUser.displayName;
					var steamId = targetUser.userID.ToString();
					if (targetUser != null)
				{
					if (args[1] == "owner")
				{
					if(Profile.owner.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddOwner1", this, Id), name));
					return;
				}
					Profile.owner.Add(steamId, name);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "admincontrol.canowner"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddOwner2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddOwner3", this, Id), username));
				}
					else if (args[1] == "admin")
				{
					if(Profile.admin.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddAdmin1", this, Id), name));
					return;
				}
					Profile.admin.Add(steamId, name);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "admincontrol.canadmin"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddAdmin2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddAdmin3", this, Id), username));
				}
					else if (args[1] == "mod")
				{
					if(Profile.mod.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddMod1", this, Id), name));
					return;
				}
					Profile.mod.Add(steamId, name);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "admincontrol.canmod"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddMod2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddMod3", this, Id), username));
				}
					else if (args[1] == "helper")
				{
					if(Profile.helper.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddHelper1", this, Id), name));
					return;
				}
					Profile.helper.Add(steamId, name);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "admincontrol.canhelper"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddHelper2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddHelper3", this, Id), username));
				}
					else if (args[1] == "vip")
				{
					if(Profile.vip.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddVip1", this, Id), name));
					return;
				}
					Profile.vip.Add(steamId, name);
					//rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "*****"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddVip2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddVip3", this, Id), username));
				}
					else if (args[1] == "yt")
				{
					if(Profile.yt.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddYt1", this, Id), name));
					return;
				}
					Profile.yt.Add(steamId, name);
					//rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "*****"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddYt2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("AddYt3", this, Id), username));
				}
					else if (args[1] == "dv")
				{
					if(Profile.dv.ContainsKey(steamId))
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Adddv1", this, Id), name));
					return;
				}
					Profile.dv.Add(steamId, name);
					//rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "grant user", steamId, "*****"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Adddv2", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Adddv3", this, Id), username));
				}
					else
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("NoCommand", this, Id), args[1]));
					return;
					}
				}
			}
		}

		void cmdDell(NetUser netUser, string command, string[] args)
		{
			var Id = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			if(!IsOwner(netUser))
			{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
					return;
				}
					else if (args.Length != 2)
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("SyntaxDell", this, Id));
					return;
				}
					else
				{
					NetUser targetUser = rust.FindPlayer(args[0]);
					var name = targetUser.displayName;
					var steamId = targetUser.userID.ToString();
					if (targetUser != null)
				{
					if (args[1] == "owner")
				{
					if(Profile.owner.ContainsKey(steamId))
				{
					Profile.owner.Remove(steamId);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "admincontrol.canowner"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveOwner1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveOwner2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveOwner3", this, Id), name));
					return;
				}
					else if (args[1] == "admin")
				{
					if(Profile.admin.ContainsKey(steamId))
				{
					Profile.admin.Remove(steamId);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "admincontrol.canadmin"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveAdmin1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveAdmin2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveAdmin3", this, Id), name));
					return;
				}
					else if (args[1] == "mod")
				{
					if(Profile.mod.ContainsKey(steamId))
				{
					Profile.mod.Remove(steamId);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "admincontrol.canmod"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveMod1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveMod2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveMod3", this, Id), name));
					return;
				}
					else if (args[1] == "helper")
				{
					if(Profile.helper.ContainsKey(steamId))
				{
					Profile.helper.Remove(steamId);
					rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "admincontrol.canhelper"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveHelper1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveHelper2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveHelper3", this, Id), name));
					return;
				}
					else if (args[1] == "vip")
				{
					if(Profile.vip.ContainsKey(steamId))
				{
					Profile.vip.Remove(steamId);
					//rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "*****"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveVip1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveVip2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveVip3", this, Id), name));
					return;
				}
					else if (args[1] == "yt")
				{
					if(Profile.yt.ContainsKey(steamId))
				{
					Profile.yt.Remove(steamId);
					//rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "*****"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveYt1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveYt2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveYt3", this, Id), name));
					return;
				}
					else if (args[1] == "dv")
				{
					if(Profile.dv.ContainsKey(steamId))
				{
					Profile.dv.Remove(steamId);
					//rust.RunServerCommand(string.Format("{0} \"{1}\" \"{2}\"", "revoke user", steamId, "*****"));
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveDv1", this, Id), name));
					rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveDv2", this, Id), username));
					return;
				}
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("RemoveDv3", this, Id), name));
					return;
				}
					else
				{
					rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("NoCommand", this, Id), args[1]));
					return;
					}
				}
			}
		}
	}
}