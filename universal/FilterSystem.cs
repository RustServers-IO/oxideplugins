using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Oxide.Core.Libraries;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("FilterSystem", "mcnovinho08", "1.0.0")]
	[Description("System filter players")]
	
	class FilterSystem : RustLegacyPlugin
	{		
		
		void OnServerInitialized()
		{
			CheckCfg<string>("Settings: chat Prefix:", ref chatPrefix);
			CheckCfg<bool>("Settings: Caracter Block:", ref CaracterBlock);
			CheckCfg<bool>("Settings: Name Block:", ref NameBlock);
			CheckCfg<bool>("Settings: Kick Small Nick:", ref KickSmallNick);
			CheckCfg<int>("Settings: Min Caracteres Nick:", ref minNick);
			CheckCfg<List<object>>("Settings: Names Blocks", ref nomes);
			CheckCfg<List<object>>("Settings: Caracteres Blocks", ref caracteres);
			LoadDefaultMessages();
			SaveConfig();//
			
		}
		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}
		

		string GetMessage(string key, string Id = null) => lang.GetMessage(key, this, Id);
		void LoadDefaultMessages()
		{
			var message = new Dictionary<string, string>
			{//
				{"InvalidCaractes", "Character {0} is locked, change it and try to log in!"},
				{"InvalidCaractesGlobal", "{0} was kicked, because it has a [color red] Locked feature!"},
				{"InvalidNick", "Nick {0} is locked, I changed it and tried to log in!"},
				{"InvalidNickGlobal", "{0} has been kicked, so it's using a Nick [color red] Blocked!"},
				{"InvalidNickPeq", "{0} was kicked, for this one using a very small nickname!"}
				
			};
			lang.RegisterMessages(message, this);
		}
		
		static JsonSerializerSettings jsonsettings;
		static string chatPrefix = "FilterSystem";
		
		public static List<object> nomes = new List<object>(){"testing", "testing2", "oxide"};
		public static List<object> caracteres = new List<object>(){"@", "$"};
		
		static int minNick = 3;
		
		// Caracter Filter
		static bool CaracterBlock = true;
		// Name Filter
		static bool NameBlock = true;
		// Small Nick
		static bool KickSmallNick = true;
		
		
		void OnPlayerConnected(NetUser netuser)
		{
			string ID = netuser.userID.ToString();
			var Name = netuser.displayName.ToString();
			
			// caracteres bloquear
			if (CaracterBlock){
		    foreach(string value in caracteres)
		    {
                if(netuser.displayName.ToString().ToLower().Contains(value)){
                    rust.BroadcastChat(chatPrefix, string.Format(GetMessage("InvalidCaractesGlobal"), netuser.displayName));
					rust.RunClientCommand(netuser, $"deathscreen.reason \"{string.Format(GetMessage("InvalidCaractes", ID), value)}\"");
                    rust.RunClientCommand(netuser, "deathscreen.show");
                    netuser.Kick(NetError.Facepunch_Kick_RCON, true);
                    return;
                }
            }}
     
	        // nicks bloqueados
			if (NameBlock) {
            foreach(string value in nomes)
			{
                if(netuser.displayName.ToString().ToLower().Contains(value))
				{
					
                    rust.BroadcastChat(chatPrefix, string.Format(GetMessage("InvalidNickGlobal"), netuser.displayName));
					rust.RunClientCommand(netuser, $"deathscreen.reason \"{string.Format(GetMessage("InvalidNick", ID), value)}\"");
                    rust.RunClientCommand(netuser, "deathscreen.show");
                    netuser.Kick(NetError.Facepunch_Kick_RCON, true);
                    return;
                }
            } }
		
		    // nome menor que 3 block
			if (KickSmallNick) {
		    if (Name.Length < minNick)
            {
                rust.BroadcastChat(chatPrefix, string.Format(GetMessage("InvalidNickPeq"), netuser.displayName));
                netuser.Kick(NetError.Facepunch_Kick_RCON, true);
                return;
            }
		} }


	}
}