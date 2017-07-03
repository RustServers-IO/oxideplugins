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
	[Info("SleeperLog", "P0LENT4", "1.0.0")]
	[Description("Plugin that saves data and the locations of unlogged players")]
	
	class SleeperLog : RustLegacyPlugin
	{
		static JsonSerializerSettings jsonsettings;
		static string tag           = "SleeperLog";
		const string permissionlog	= "SleeperLog.allow";
		static bool logall          = true;
		
		private Core.Configuration.DynamicConfigFile Data;
        void LoadData() { Data = Interface.GetMod().DataFileSystem.GetDatafile("SleeperLog"); }
        void SaveData() { Interface.GetMod().DataFileSystem.SaveDatafile("SleeperLog"); }
		void OnServerSave() { SaveData(); }
        
        Dictionary<string, object> GetPlayerdata(string userid)
        {
            if (Data[userid] == null)
                Data[userid] = new Dictionary<string, object>();
            return Data[userid] as Dictionary<string, object>;
        }
		
		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		void LoadDefaultMessages(){
			var message = new Dictionary<string, string>{
				{"Noaccess", "[color red][ ! ][color #0077FF]You can not use this command"},
				
				{"erro1", "[color red][ ! ][color #0077FF]Use /tps nick"},
				{"erro2", "[color red][ ! ][/color]Could not find the last position of {0}"},
				{"erro3", "[color red][ ! ][color #0077FF]Use /infoslp nick"},
				{"erro4", "[color red][ ! ][/color]Could not find the information for {0}"},
				
				{"Success1", "[color red][ ! ][/color]You were teleported to the position where {0} was disconnected"},
				
				{"Info", "[color red]--------------------------------------------------------------"},
				{"Info1", "[color green]Name: [color orange]{0}[color white] | [color green]IP: [color orange]{1}[color white] | [color green]ID: [color orange]{2}"},
				{"Info2", "[color green]location: [color orange]{0}"},
				{"Info3", "[color red]--------------------------------------------------------------"},
			}; 
			lang.RegisterMessages(message, this);
		}


		void OnServerInitialized(){
			CheckCfg<string>("Settings: Prefixo", ref tag);
			CheckCfg<bool>("Settings: Log the logs of all players", ref logall);
			permission.RegisterPermission(permissionlog, this);
			LoadDefaultMessages();
			LoadData();
			SaveData();
		}


		protected override void LoadDefaultConfig(){} 
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}


		bool Acess(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), permissionlog)) return true;
			return false;
		}
		
		private void OnPlayerConnected(NetUser netuser){
            if(logall){
                var Name = netuser.displayName.ToString();
			    var Ip = netuser.networkPlayer.externalIP;
			    var Id = netuser.userID.ToString();
			    var playerlog = GetPlayerdata(Name);
				if(playerlog.ContainsKey("id")){
					playerlog.Remove("id");
					playerlog.Remove("name");
					playerlog.Remove("ip");
					playerlog.Remove("PX");
					playerlog.Remove("PY");
					playerlog.Remove("PZ");
					playerlog.Add("id", Id);
				    playerlog.Add("name", Name);
				    playerlog.Add("ip", Ip);
				}
				else{
					playerlog.Add("id", Id);
				    playerlog.Add("name", Name);
				    playerlog.Add("ip", Ip);
				}
				
				if(Ip != "127.0.0.1"){
				    var url = string.Format("http://ip-api.com/json/" + Ip);
				    Interface.GetMod().GetLibrary<WebRequests>("WebRequests").EnqueueGet(url, (code, response) =>{ 
					    var jsonresponse = JsonConvert.DeserializeObject<Dictionary<string, object>>(response, jsonsettings);
					    var country = (jsonresponse["country"].ToString());
					    if(playerlog.ContainsKey("Country")){
							playerlog.Remove("Country");
							playerlog.Add("Country", country);
							SaveData();
						}
					    
				    }, this);
			    }
				SaveData();
            }
			
        }
		private void OnPlayerDisconnected(uLink.NetworkPlayer netPlayer){
			var netuser = netPlayer.GetLocalData() as NetUser;
			var Name = netuser.displayName.ToString();
			var playerlog = GetPlayerdata(Name);
			if(playerlog.ContainsKey("id")){
			    playerlog.Add("PX", netuser.playerClient.lastKnownPosition.x.ToString());
                playerlog.Add("PY", netuser.playerClient.lastKnownPosition.y.ToString());
                playerlog.Add("PZ", netuser.playerClient.lastKnownPosition.z.ToString());
			}
			SaveData();
		}
		[ChatCommand("tps")]
		void cmdTeleSleep(NetUser netuser, string command, string[] args){
			var Id = netuser.userID.ToString();
			if(!Acess(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Noaccess",Id)); return; }
			if(args.Length == 0){rust.SendChatMessage(netuser, tag, GetMessage("erro1", Id));return;}
			var playerdata = GetPlayerdata(args[0]);
			if (playerdata.ContainsKey("name")){
				float x = Convert.ToSingle(playerdata["PX"]);
                float y = Convert.ToSingle(playerdata["PY"]);
                float z = Convert.ToSingle(playerdata["PZ"]);
				var management = RustServerManagement.Get();
				management.TeleportPlayerToWorld(netuser.playerClient.netPlayer, new Vector3(x, y, z));
				rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Success1",Id), args[0])); 
				return;
			}
			else{
				rust.SendChatMessage(netuser, tag, string.Format(GetMessage("erro2",Id), args[0])); 
				return;
			}
		}
		[ChatCommand("infosl")]
		void cmdInfoSleep(NetUser netuser, string command, string[] args){
			var Id = netuser.userID.ToString();
			if(!Acess(netuser)) { rust.SendChatMessage(netuser, tag, GetMessage("Noaccess",Id)); return; }
			if(args.Length == 0){rust.SendChatMessage(netuser, tag, GetMessage("erro3", Id));return;}
			var playerdata = GetPlayerdata(args[0]);
			if (playerdata.ContainsKey("name")){
				float x = Convert.ToSingle(playerdata["PX"]);
                float y = Convert.ToSingle(playerdata["PY"]);
                float z = Convert.ToSingle(playerdata["PZ"]);
				var nome = playerdata["name"];
				var ip = playerdata["ip"];
				var idd = playerdata["id"];
				var localizacao = ((playerdata["PX"])+ " "+ (playerdata["PY"])+ " "+ (playerdata["PZ"]));
				rust.SendChatMessage(netuser, tag, GetMessage("Info",Id)); 
				rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Info1",Id), nome, ip, idd));
				rust.SendChatMessage(netuser, tag, string.Format(GetMessage("Info2",Id), localizacao)); 
				rust.SendChatMessage(netuser, tag, GetMessage("Info3",Id)); 
				return;
			}
			else{
				rust.SendChatMessage(netuser, tag, string.Format(GetMessage("erro4",Id), args[0])); 
				return;
			}
		}
		
	}
}