using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins{
        [Info("ADSystem", "mcnovinho08", "1.1.0")]
        [Description("Player Ads for Players")]
		
        class ADSystem : RustLegacyPlugin{

		[PluginReference]
		Plugin MoneySystem;

		[PluginReference]
		Plugin AdminControl;
		
		static string chatPrefix = "ADSystem";
		
		const string PermiVIP = "adsystem.vip";
		
		static string Habilitado = "[color green]Enabled"; //
		static string Desabilitado = "[color red]Disabled"; //
		
	    static int QuantiaPorAnuncio = 1500;
	    static int QuantiaPorAnuncioVIP = 500;
		
		static bool MoneySystemOn = false;
		static bool MoneySystemVIPOn = false;
		
		static bool SystemOnOf = true;
		
       static float TimeadSystem = 30;
       static List<string> OnAdsPlayers = new List<string>();		
		
		void Init() { 
		CheckCfg<string>("Settings: Chat Prefix:", ref chatPrefix);
		CheckCfg<bool>("Settings: System ON|OFF:", ref SystemOnOf);
		CheckCfg<string>("Settings: Activad:", ref Habilitado);
		CheckCfg<string>("Settings: Disabled:", ref Desabilitado);
		CheckCfg<int>("Settings: Cost Per Announcement Player:", ref QuantiaPorAnuncio);
		CheckCfg<int>("Settings: Cost Per Announcement VIP:", ref QuantiaPorAnuncioVIP);
		CheckCfg<bool>("Settings: Cost To Announce Player ON | OFF:", ref MoneySystemOn);
		CheckCfg<bool>("Settings: Cost for VIP Advertising ON | OFF:", ref MoneySystemVIPOn);
		CheckCfg<float>("Settings: Time for the Player Use the Command:", ref TimeadSystem);
		permission.RegisterPermission(PermiVIP, this);
		LoadDefaultMessages();
		SaveConfig();
		}

		bool AcessViP(NetUser netuser){
			if(netuser.CanAdmin())return true; 
			if(permission.UserHasPermission(netuser.playerClient.userID.ToString(), PermiVIP)) return true;
			return false;
		}
		
		protected override void LoadDefaultConfig(){}  
		private void CheckCfg<T>(string Key, ref T var){
			if(Config[Key] is T)
			var = (T)Config[Key];  
			else
			Config[Key] = var;
		}
		
			
		string GetMessage(string key, string steamid = null) => lang.GetMessage(key, this, steamid);
		void LoadDefaultMessages(){
			var messages = new Dictionary<string, string>{
				{"SystemOnOf", "The system is currently: {0}"},
				{"Time", "You have to wait {0} minutes, to use this command again!"},
				{"MoneySystemNull", "This action can not be completed without the MoneySystem Plugin, I recommend disabling the use!"},
				{"MoneyInvalid", "You do not have {0} to use this command!"},
				{"MessageNull", "You have not set any messages, then this action has been canceled!"},
				{"AD", "{0}: {1}"}
			}; 
			lang.RegisterMessages(messages, this);
		}
		
		[ChatCommand("ad")]
		void Anuncio(NetUser netuser, string command, string[] args)
		{
			string ID = netuser.userID.ToString();
			if (!SystemOnOf) { rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("SystemOnOf", ID), Desabilitado)); return; }
			string Usuario = netuser.displayName;
			string message = "";
			foreach (string arg in args)
            {
                message = message + " " + arg;
            }
			if (message == "") { rust.SendChatMessage(netuser, chatPrefix, GetMessage("MessageNull", ID)); return; }
			if(OnAdsPlayers.Contains(ID)) { rust.SendChatMessage(netuser, chatPrefix, string.Format(GetMessage("Time", ID), TimeadSystem)); 	return; }
			OnAdsPlayers.Add(ID);
			timer.Once(TimeadSystem * 60, ()=> { OnAdsPlayers.Remove(ID); });
			if (!AcessViP(netuser)) {
		    if (MoneySystemVIPOn){
			object thereturn = (object)MoneySystem?.Call("canMoney", new object[] {netuser});
            if(thereturn != null)return;// Se return for deferente a null nao vai poder usar para ivitar bugs.
            if(MoneySystem == null){rust.Notice(netuser, GetMessage("MoneySystemNull", ID)); return;}
            int totalMoney = (int)MoneySystem?.Call("GetTotalMoney", ID);
            if(totalMoney < QuantiaPorAnuncioVIP){ rust.Notice(netuser, string.Format(GetMessage("MoneyInvalid", ID), QuantiaPorAnuncioVIP)); return;}
            MoneySystem?.Call("TakeMoney", netuser, QuantiaPorAnuncioVIP);
			rust.BroadcastChat(chatPrefix, string.Format(GetMessage("AD"), Usuario, message));
			}
			else{ rust.BroadcastChat(chatPrefix, string.Format(GetMessage("AD"), Usuario, message)); }		
			}
            else{
		    if (MoneySystemOn){
			object thereturn = (object)MoneySystem?.Call("canMoney", new object[] {netuser});
            if(thereturn != null)return;// Se return for deferente a null nao vai poder usar para ivitar bugs.
            if(MoneySystem == null){rust.Notice(netuser, GetMessage("MoneySystemNull", ID)); return;}
            int totalMoney = (int)MoneySystem?.Call("GetTotalMoney", ID);
            if(totalMoney < QuantiaPorAnuncio){ rust.Notice(netuser, string.Format(GetMessage("MoneyInvalid", ID), QuantiaPorAnuncio)); return;}
            MoneySystem?.Call("TakeMoney", netuser, QuantiaPorAnuncio);
			rust.BroadcastChat(chatPrefix, string.Format(GetMessage("AD"), Usuario, message));
			}
			else{ rust.BroadcastChat(chatPrefix, string.Format(GetMessage("AD"), Usuario, message)); } }			
		}

		
}
}