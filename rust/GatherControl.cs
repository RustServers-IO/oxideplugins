using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("GatherControl", "CaseMan", "1.2.1", ResourceId = 2477)]
    [Description("Control gather rates by day and night with permissions")]

    class GatherControl : RustPlugin
    {	
		#region Variables
	    [PluginReference]
        Plugin GUIAnnouncements;
		
		bool IsDay;	
		bool UseZeroIndexForDefaultGroup;
		bool UseMessageBroadcast;
		bool UseGUIAnnouncements;
		string BannerColor;
		string TextColor;
		public Dictionary<ulong, int> Temp = new Dictionary<ulong, int>();
		float Sunrise;
		float Sunset;		

		class PermData
        {
            public Dictionary<int, PermGroups> PermissionsGroups = new Dictionary<int, PermGroups>();
            public PermData(){}
        }

        class PermGroups
        {
			public float DayRateMultQuarry;
			public float DayRateMultPickup;
            public float DayRateMultResource;  
			public float DayRateMultCropGather;
			public float NightRateMultQuarry;
			public float NightRateMultPickup;            
            public float NightRateMultResource;
			public float NightRateMultCropGather;
			public string PermGroup;
			public PermGroups(){}
        }
		
		PermData permData;			
		#endregion
		#region Initialization
		void Init()
        {
            LoadDefaultConfig();
			permData = Interface.Oxide.DataFileSystem.ReadObject<PermData>("GatherControl");
			LoadDefaultData();
            foreach(var perm in permData.PermissionsGroups)
			{
                permission.RegisterPermission(perm.Value.PermGroup, this);
			}
			CheckPlayers();
        }
		void LoadDefaultData()
		{
			if(!permData.PermissionsGroups.ContainsKey(0))
			{
				var def = new PermGroups();
                def.DayRateMultQuarry = 2;
                def.DayRateMultPickup = 2;
                def.DayRateMultResource = 2;
				def.DayRateMultCropGather = 2;
                def.NightRateMultQuarry = 3;
                def.NightRateMultPickup = 3;
                def.NightRateMultResource = 3;
				def.NightRateMultCropGather =3;
				def.PermGroup = "gathercontrol.default";
				permData.PermissionsGroups.Add(0, def);
				Interface.Oxide.DataFileSystem.WriteObject("GatherControl", permData);
			}			
		}
		void OnPlayerInit(BasePlayer player)
		{
			CheckPlayer(player);
		}
		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			if(Temp.ContainsKey(player.userID)) Temp.Remove(player.userID);
		}
		#endregion
		#region Configuration
        protected override void LoadDefaultConfig()
        {
			Config["UseZeroIndexForDefaultGroup"] = UseZeroIndexForDefaultGroup = GetConfig("UseZeroIndexForDefaultGroup", true);
			Config["UseMessageBroadcast"] = UseMessageBroadcast = GetConfig("UseMessageBroadcast", true);
			Config["UseGUIAnnouncements"] = UseGUIAnnouncements = GetConfig("UseGUIAnnouncements", false);
			Config["BannerColor"] = BannerColor = GetConfig("BannerColor", "Blue");
			Config["TextColor"] = TextColor = GetConfig("TextColor", "Yellow");
			Config["Sunrise"] = Sunrise = GetConfig("Sunrise", 7);
			Config["Sunset"] = Sunset = GetConfig("Sunset", 19);
			SaveConfig();
		}
		#endregion		
		#region Localization
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["SunriseMessage"] = "Morning comes. Production rating is lowered.",
				["SunsetMessage"] = "Night is coming. Production rating is increased.",
				["GatherRateInfo"] = "Your gather multipliers:",
				["NoGatherRate"] = "You do not have gather multipliers!",
				["GatherRateInfoPlayer"] = "Player {0} have gather multipliers:",
				["NoGatherRatePlayer"] = "Player {0} do not have gather multipliers!",
				["DayRateResource"] = "Day resource gather multiplier",
				["DayRatePickup"] = "Day pickup multiplier",
				["DayRateQuarry"] = "Day multiplier of quarrying",
				["DayRateCropGather"] = "Day multiplier for your crop gather",
				["NightRateResource"] = "Night resource gather multiplier",
				["NightRatePickup"] = "Night pickup multiplier",
				["NightRateQuarry"] = "Night multiplier of quarrying",
				["NightRateCropGather"] = "Night multiplier for your crop gather",
				["InvalidSyntax"] = "Invalid syntax! Use: showrate <name/ID>",
            }, this);
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SunriseMessage"] = "Наступает утро. Рейтинг добычи уменьшен.",
                ["SunsetMessage"] = "Наступает ночь. Рейтинг добычи увеличен.",
				["GatherRateInfo"] = "Ваши множители добычи ресурсов:",
				["NoGatherRate"] = "У вас нет множителей добычи!",
				["GatherRateInfoPlayer"] = "У игрока {0} есть следующие множители:",
				["NoGatherRatePlayer"] = "У игрока {0} нет множителей добычи!",
				["DayRateResource"] = "Дневной множитель добычи ресурсов",
				["DayRatePickup"] = "Дневной множитель поднятия предметов",
				["DayRateQuarry"] = "Дневной множитель добычи карьеров",
				["DayRateCropGather"] = "Дневной множитель сбора своего урожая",
				["NightRateResource"] = "Ночной множитель добычи ресурсов",
				["NightRatePickup"] = "Ночной множитель поднятия предметов",
				["NightRateQuarry"] = "Ночной множитель добычи карьеров",
				["NightRateCropGather"] = "Ночной множитель сбора своего урожая",
				["InvalidSyntax"] = "Неправильный синтаксис. Используйте: showrate <имя/ID>",
            }, this, "ru");
        }
        #endregion
		#region Hooks
		private void CheckDay()
		{
			var time = TOD_Sky.Instance.Cycle.Hour;
			if(time >= Sunrise && time <= Sunset)
			{
				if(!IsDay && time>=Sunrise && time<=Sunrise + 0.1) MessageToAll("SunriseMessage");
				IsDay = true;	
			}
			else
			{
				if(IsDay && time>=Sunset && time<=Sunset + 0.1) MessageToAll("SunsetMessage");
				IsDay = false;
			}	
		}
		private void CheckPlayers()
		{
			foreach (var player in BasePlayer.activePlayerList) CheckPlayer(player);  
		}
		private void CheckPlayer(BasePlayer player)
		{
			if(player == null) return;
			int index=-1;
			if(Temp.ContainsKey(player.userID)) Temp.Remove(player.userID);
			index = CheckPlayerPerm(player, index);
			if(index >= 0)Temp.Add(player.userID, index);
		}
		private int CheckPlayerPerm(BasePlayer player, int index)
		{
			foreach (var perm in permData.PermissionsGroups)
            {
                if (permission.UserHasPermission(player.UserIDString, perm.Value.PermGroup) && perm.Key>=index) index = perm.Key;          				  
            }
			if(index==-1 && UseZeroIndexForDefaultGroup) index = 0;
			return index;			
		}	
		private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {			
            BasePlayer player = entity.ToPlayer();
			if(player == null) return;
			int gr = CheckPlayerPerms(player);
			if(gr >= 0) GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultResource, permData.PermissionsGroups[gr].NightRateMultResource);			
        }
		void OnCollectiblePickup(Item item, BasePlayer player)
		{
			if(player == null) return;
			int gr = CheckPlayerPerms(player);
			if(gr >= 0) GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultPickup, permData.PermissionsGroups[gr].NightRateMultPickup);	
		}
		void OnQuarryGather(MiningQuarry quarry, Item item)
		{
			int gr=-1;
			BasePlayer player = BasePlayer.FindByID(quarry.OwnerID);
            if(player == null)
			{
				BasePlayer player1 = BasePlayer.FindSleeping(quarry.OwnerID);
				if(player1 == null) return;
				gr = CheckPlayerPerm(player1, -1);
			}
			else
			{	
				gr = CheckPlayerPerms(player);
			}
			if(gr >= 0) GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultQuarry, permData.PermissionsGroups[gr].NightRateMultQuarry);
		}
		void OnCropGather(PlantEntity plant, Item item, BasePlayer player)
		{
			if(player == null) return;
			int gr = CheckPlayerPerms(player);
			if(gr >= 0) GatherMultiplier(item, permData.PermissionsGroups[gr].DayRateMultCropGather, permData.PermissionsGroups[gr].NightRateMultCropGather);
		}
		private int CheckPlayerPerms(BasePlayer player)
        {			
           	if(Temp.ContainsKey(player.userID))
			{ 
				return Temp[player.userID];
			}        
			return -1;	
        }
		private void GatherMultiplier(Item item, float daymult, float nightmult) 
        {			
			if(IsDay) item.amount = (int)(item.amount * daymult); 
			else item.amount = (int)(item.amount * nightmult); 
        }	
		void OnTick()
		{
			CheckDay();	
		}
		void OnUserPermissionGranted(string id, string permis)
		{
			OnChangePermsUser(id, permis);
		}
		void OnUserPermissionRevoked(string id, string permis)
		{
			OnChangePermsUser(id, permis);
		}
		void OnUserGroupAdded(string id, string name)
		{
			OnChangeUserGroup(id);
		}
		void OnUserGroupRemoved(string id, string name)
		{			
			OnChangeUserGroup(id);
		}
		void OnGroupPermissionGranted(string name, string permis)
		{
			OnChangePermsGroup(permis);
		}
		void OnGroupPermissionRevoked(string name, string permis)
		{
			OnChangePermsGroup(permis);
		}
		private void OnChangePermsGroup(string permis)
		{
			foreach(var perm in permData.PermissionsGroups)
			{
                if(perm.Value.PermGroup==permis) CheckPlayers();					
			}
		}	
		private void OnChangePermsUser(string id, string permis)
		{
			var player = BasePlayer.Find(id);
			if(player == null) return;
			foreach(var perm in permData.PermissionsGroups)
			{
                if(perm.Value.PermGroup==permis) CheckPlayer(player);					
			}
		}
		private void OnChangeUserGroup(string id)
		{
			var player = BasePlayer.Find(id);
			if(player == null) return;
			if(Temp.ContainsKey(player.userID))	CheckPlayer(player);
		}
		#endregion
		#region Commands
        [ChatCommand("showrate")]
        void ShowRate(BasePlayer player, string command, string[] args)
        {
			int gr = CheckPlayerPerms(player);
			if(gr >= 0)	SendReply(player, lang.GetMessage("GatherRateInfo", this, player.UserIDString) + GatherInfo(player, gr));	
			else SendReply(player, lang.GetMessage("NoGatherRate", this, player.UserIDString)); 
		}
		[ConsoleCommand("showrate")]
        private void conShowRate(ConsoleSystem.Arg arg)
		{
			if (arg.Args == null || arg.Args.Length <= 0)
			{
                Puts(lang.GetMessage("InvalidSyntax", this));
                return;
            }
			BasePlayer player = BasePlayer.Find(arg.Args[0]) ?? BasePlayer.FindSleeping(arg.Args[0]);
			if(player == null) return;
			int gr = CheckPlayerPerm(player, -1);
			if(gr >= 0)	Puts(string.Format(lang.GetMessage("GatherRateInfoPlayer", this, player.UserIDString), player.displayName) + GatherInfo(player, gr));
			else Puts(string.Format(lang.GetMessage("NoGatherRatePlayer", this), player.displayName));                  				          			
		}
		#endregion
		#region Helpers
		T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T) Convert.ChangeType(Config[name], typeof(T)); 
		void MessageToAll(string key)
        {		
			foreach (var player in BasePlayer.activePlayerList)
			{
				if(UseMessageBroadcast) SendReply(player, lang.GetMessage(key, this, player.UserIDString));
				if(GUIAnnouncements && UseGUIAnnouncements) GUIAnnouncements?.Call("CreateAnnouncement", lang.GetMessage(key, this, player.UserIDString), BannerColor, TextColor, player);
			}
        }
		private string GatherInfo(BasePlayer player, int gr)
		{
			string message = "";
			{
				message= string.Format("\n\r{0}: {8}\n\r{1}: {9}\n\r{2}: {10}\n\r{3}: {11}\n\r{4}: {12}\n\r{5}: {13}\n\r{6}: {14}\n\r{7}: {15}\n\r", 
					lang.GetMessage("DayRateResource", this, player.UserIDString),
					lang.GetMessage("DayRatePickup", this, player.UserIDString),
					lang.GetMessage("DayRateQuarry", this, player.UserIDString),
					lang.GetMessage("DayRateCropGather", this, player.UserIDString),
					lang.GetMessage("NightRateResource", this, player.UserIDString),
					lang.GetMessage("NightRatePickup", this, player.UserIDString),
					lang.GetMessage("NightRateQuarry", this, player.UserIDString),
					lang.GetMessage("NightRateCropGather", this, player.UserIDString),
					permData.PermissionsGroups[gr].DayRateMultResource,
					permData.PermissionsGroups[gr].DayRateMultPickup,
					permData.PermissionsGroups[gr].DayRateMultQuarry,
					permData.PermissionsGroups[gr].DayRateMultCropGather,
					permData.PermissionsGroups[gr].NightRateMultResource,
					permData.PermissionsGroups[gr].NightRateMultPickup,
					permData.PermissionsGroups[gr].NightRateMultQuarry,
					permData.PermissionsGroups[gr].NightRateMultCropGather
					);
			}
			return message;	
		}	
		#endregion
    }
}
