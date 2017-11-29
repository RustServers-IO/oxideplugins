using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
	[Info("BlockedSystem", "mcnovinho08 & Gute", "0.2.0")]
	[Description("Block colors, words, anti-flood.")]

	class BlockedSystem : RustLegacyPlugin
	{
		[PluginReference]
		Plugin AdminControl;

		static string ChatTag;

		static string permissionCanBlocked;

		static float TimeAntiSpawnChat;

		static bool SystemAntiSpawnChat;
		static bool SystemAntiNames;
		static bool SystemAntiColors;
		static bool SystemAntiSymbols;
		static bool SystemAntiAdvertising;
		static bool SystemAntiIpSevers;

		void Init()
		{
			SetupConfig();
			SetupLang();
			SetupPermissions();
			SetupCheck();
			return;
		}

		void SetupPermissions()
		{
			permission.RegisterPermission(permissionCanBlocked, this);
			return;
		}

		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		void SetupLang()
		{
            // English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"SystemAntiSpawnChat1", "Spawn Chat {0} seg forbidden in this server."},
				{"SystemAntiNames1", "Phrase {0} forbidden in this server."},
				{"SystemAntiSymbols1", "Symbol {0} forbidden in this server."},
				{"SystemAntiColors1", "Color {0} forbidden in this server."},
				{"SystemAntiAdvertising1", "Advertising {0} forbidden in this server."},
				{"SystemAntiIpSevers1", "Advertising {0} Ips forbidden in this server."}
			}, this);

            // Portugues Brasileiro
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"SystemAntiSpawnChat1", "Spawn Chat {0} seg proibido neste servidor."},
				{"SystemAntiNames1", "Palavra {0} proibida neste servidor."},
				{"SystemAntiSymbols1", "Símbolo {0} proibido neste servidor."},
				{"SystemAntiColors1", "Cor {0} proibida neste servidor."},
				{"SystemAntiAdvertising1", "Publicidade {0} proibida neste servidor."},
				{"SystemAntiIpSevers1", "Publicidade {0} Ips proibidas neste servidor."}
			}, this, "pt-br");
			return;
		}

		static List<string> AntiSpawnChat	= new List<string>();

		public static List<object> AntiNames = new List<object>()
		{"ff", "kk", "noob", "fuck", "abuser", "bist"};

		public static List<object> AntiColors = new List<object>()
		{"[", "]", "color"};

		public static List<object> AntiSymbols = new List<object>()
		{"✪", "☢", "♚"};

		public static List<object> AntiAdvertising = new List<object>()
		{"http", "https", ":/", ".w", "ww"};

		public static List<object> AntiIpSevers = new List<object>()
		{"net.connect", ":0", ":1", ":2", ":3", ":4", ":5", ":6", ":7", ":8", ":9"};

		protected override void LoadDefaultConfig()
		{
			Config["SettingsPermission"] = new Dictionary<string, object>
			{
				{"permissionCanBlocked", "blockedsystem.allow"}
			};
			Config["SettingsChatName"] = new Dictionary<string, object>
			{
				{"ChatTag", "BlockedSystem"}
			};
				Config["SettingsExtras"] = new Dictionary<string, object>
			{
				{"TimeAntiSpawnChat", 3}
			};
			Config["SettingsSystem"] = new Dictionary<string, object>
			{
				{"SystemAntiSpawnChat", true},
				{"SystemAntiNames", true},
				{"SystemAntiColors", true},
				{"SystemAntiSymbols", true},
				{"SystemAntiAdvertising", true},
				{"SystemAntiIpSevers", true}
			};
		}

		void SetupConfig()
		{
			ChatTag = Config.Get<string>("SettingsChatName", "ChatTag");

			permissionCanBlocked = Config.Get<string>("SettingsPermission", "permissionCanBlocked");

			TimeAntiSpawnChat = Config.Get<float>("SettingsExtras", "TimeAntiSpawnChat");

			SystemAntiSpawnChat = Config.Get<bool>("SettingsSystem", "SystemAntiSpawnChat");
			SystemAntiNames = Config.Get<bool>("SettingsSystem", "SystemAntiNames");
			SystemAntiColors = Config.Get<bool>("SettingsSystem", "SystemAntiColors");
			SystemAntiSymbols = Config.Get<bool>("SettingsSystem", "SystemAntiSymbols");
			SystemAntiAdvertising = Config.Get<bool>("SettingsSystem", "SystemAntiAdvertising");
			SystemAntiIpSevers = Config.Get<bool>("SettingsSystem", "SystemAntiIpSevers");
		}

		void SetupCheck()
		{
			CheckCfg<List<object>>("Settings: Block Words:", ref AntiNames);
			CheckCfg<List<object>>("Settings: Lock Colors:", ref AntiColors);
			CheckCfg<List<object>>("Settings: Lock Symbols:", ref AntiSymbols);
			CheckCfg<List<object>>("Settings: Lock Advertising:", ref AntiAdvertising);
			CheckCfg<List<object>>("Settings: Lock IpSevers:", ref AntiIpSevers);
			SaveConfig();
		}

		private void CheckCfg<T>(string Key, ref T var)
		{
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}

		object OnPlayerChat(NetUser netUser, string message)
		{
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			{
				if (!IsPlayerChat(netUser, message)) return false;
				return null;
			}
		}

		bool IsPlayerChat(NetUser netUser, string message)
		{
			bool IsHelper = (bool)AdminControl?.Call("IsHelper", netUser);
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			{
				if ((netUser.CanAdmin() || IsHelper || permission.UserHasPermission(Id, permissionCanBlocked))) return true;
				{
					if(SystemAntiSpawnChat)
					{
						if(AntiSpawnChat.Contains(netUser.userID.ToString()))
						{
							rust.SendChatMessage(netUser, ChatTag, string.Format(lang.GetMessage("SystemAntiSpawnChat1", this, Id), TimeAntiSpawnChat));
							return false;
						}
							else
							{
								AntiSpawnChat.Add(netUser.userID.ToString());
								timer.Once(TimeAntiSpawnChat, ()=>{
								AntiSpawnChat.Remove(netUser.userID.ToString());
							});
						}
					}
					if(SystemAntiNames)
					{
						foreach(string pair in AntiNames)
						{
							if(message.Contains(pair.ToUpper()) || message.Contains(pair.ToLower()))
							{
								rust.SendChatMessage(netUser, ChatTag, string.Format(lang.GetMessage("SystemAntiNames1", this, Id), pair));
								return false;
							}
						}
					}
				}
				if(SystemAntiSymbols)
				{
					foreach(string pair in AntiSymbols)
					{
						if(message.Contains(pair.ToUpper()) || message.Contains(pair.ToLower()))
						{
							rust.SendChatMessage(netUser, ChatTag, string.Format(lang.GetMessage("SystemAntiSymbols1", this, Id), pair));
							return false;
						}
					}
				}
				if(SystemAntiColors)
				{
					foreach(string pair in AntiColors)
					{
						if(message.Contains(pair.ToUpper()) || message.Contains(pair.ToLower()))
						{
							rust.SendChatMessage(netUser, ChatTag, string.Format(lang.GetMessage("SystemAntiColors1", this, Id), pair));
							return false;
						}
					}
				}
				if(SystemAntiAdvertising)
				{
					foreach(string pair in AntiAdvertising)
					{
						if(message.Contains(pair.ToUpper()) || message.Contains(pair.ToLower()))
						{
							rust.SendChatMessage(netUser, ChatTag, string.Format(lang.GetMessage("SystemAntiAdvertising1", this, Id), pair));
							return false;
						}
					}
				}
				if(SystemAntiIpSevers)
				{
					foreach(string pair in AntiIpSevers)
					{
						if(message.Contains(pair.ToUpper()) || message.Contains(pair.ToLower()))
						{
							rust.SendChatMessage(netUser, ChatTag, string.Format(lang.GetMessage("SystemAntiIpSevers1", this, Id), pair));
							return false;
						}
					}
				}
			}
			return true;
		}
	}
}