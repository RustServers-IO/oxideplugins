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
	[Info("ConnectingMSG", "mcnovinho08", "0.1.1")]
	[Description("Differentiated connection messages for your server!")]
	
	class ConnectingMSG : RustLegacyPlugin
	{		

		static JsonSerializerSettings jsonsettings;
		
		static string chatPrefix = "Connecting";
		
		static bool MensagensPersonalizadas = true;
		
		static bool MensagemConnect = true;
		static bool MensagemDisconnect = true;
		
		
		void OnServerInitialized()
		{
			CheckCfg<string>("Settings: ChatPrefix", ref chatPrefix);
			CheckCfg<bool>("Settings: Message Connect", ref MensagemConnect);
			CheckCfg<bool>("Settings: Differentiated Connection Messages", ref MensagensPersonalizadas);
			CheckCfg<bool>("Settings: Connecting : {Player} Logged in to the Server! Country: {1} City: {2}|{3}", ref Mensagem1);
			CheckCfg<bool>("Settings: Connecting : {Player} Logged in to the Server! Country: {1} City: {2} State: {3}", ref Mensagem2);
			CheckCfg<bool>("Settings: Connecting : {Player} Logged in to the Server! Country: {1} City: {2}", ref Mensagem3);
			CheckCfg<bool>("Settings: Connecting : {Player} Logged in to the Server! City: {1}", ref Mensagem4);
			CheckCfg<bool>("Settings: Connecting : {Player} Logged in to the Server! State: {1}", ref Mensagem5);
			CheckCfg<bool>("Settings: Message Disconnect", ref MensagemDisconnect);
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
				{"PlayerConnect1", "[color orange]{0}[color clear] Logged in to the Server! Country: [color orange]{1}[color clear] City: [color orange]{2}[color clear]|[color orange]{3}"},
				{"PlayerConnect2", "[color orange]{0}[color clear] Logged in to the Server! Country: [color orange]{1}[color clear] City: [color orange]{2}[color clear] State: [color orange]{3}"},
				{"PlayerConnect3", "[color orange]{0}[color clear] Logged in to the Server! Country: [color orange]{1}[color clear] City: [color orange]{2} !"},
				{"PlayerConnect4", "[color orange]{0}[color clear] Logged in to the Server! City: [color orange]{1} !"},
				{"PlayerConnect5", "[color orange]{0}[color clear] Logged in to the Server! State: [color orange]{1} !"},
				{"PlayerDisconnect", "[color orange]{0}[color clear] Out of Server !"},
				{"PlayerConnectN", "[color orange]{0}[color clear] Logged in to the Server !"}
				
			};
			lang.RegisterMessages(message, this);
		}
		
		static bool Mensagem1 = true;
		static bool Mensagem2 = false;
		static bool Mensagem3 = false;
		static bool Mensagem4 = false;
		static bool Mensagem5 = false;

		public void OnPlayerConnected(NetUser netuser)
		{
			string Name = netuser.displayName;
			string Ip = netuser.networkPlayer.externalIP;
			string ID = netuser.userID.ToString();
			if(Ip != "127.0.0.1"){
				var url = string.Format("http://ip-api.com/json/" + Ip);
				Interface.GetMod().GetLibrary<WebRequests>("WebRequests").EnqueueGet(url, (code, response) =>{ 
					var jsonresponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
					var country = (jsonresponse["country"].ToString());
					var countryCode = (jsonresponse["countryCode"].ToString());
					var region = (jsonresponse["region"].ToString());
					var regionName = (jsonresponse["regionName"].ToString());
                    var city = (jsonresponse["city"].ToString());
					if (MensagensPersonalizadas) { // mensagem de conexão personalizadas
					
				    // mensagens de conexão diferentes
					if (Mensagem1) { rust.BroadcastChat(chatPrefix, string.Format(GetMessage("PlayerConnect1"), Name, country, city, region)); }
					else if (Mensagem2) { rust.BroadcastChat(chatPrefix, string.Format(GetMessage("PlayerConnect2"), Name, country, city, regionName)); }
				    else if (Mensagem3) { rust.BroadcastChat(chatPrefix, string.Format(GetMessage("PlayerConnect3"), Name, country, city)); }
				    else if (Mensagem4) { rust.BroadcastChat(chatPrefix, string.Format(GetMessage("PlayerConnect4"), Name, city)); }
				    else if (Mensagem5) { rust.BroadcastChat(chatPrefix, string.Format(GetMessage("PlayerConnect5"), Name, regionName)); } }
					
					else {  // mensagem de conexão normal
					rust.BroadcastChat(chatPrefix, string.Format(GetMessage("PlayerConnectN"), Name)); return; }
					
				}, this);
			}
			
		}

	   public void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer){
			var netuser = netPlayer.GetLocalData() as NetUser;
            if(MensagemDisconnect)
			rust.BroadcastChat(chatPrefix, string.Format(GetMessage("MensagemDisconnect"), netuser.displayName));
		}
	}
}