using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust;
using UnityEngine;
using System.Reflection;
using Oxide.Core.Libraries.Covalence;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("PreventLooting", "CaseMan", "1.4.5", ResourceId = 2469)]
    [Description("Prevent looting by other players")]

    class PreventLooting : RustPlugin
    {	
		#region Variables
	    [PluginReference] Plugin Friends;
		[PluginReference] Plugin ZoneManager;	    
		
		bool UsePermission;
		bool UseFriendsAPI;
		bool AdminCanLoot;
		bool CanLootPlayer;
		bool CanLootCorpse;
		bool CanLootEntity;
		bool CanLootBackpack;
		bool UseZoneManager;
		bool UseExcludeEntities;
		bool UseCupboard;
		bool UseOnlyInCupboardRange;
		List<object> ZoneID;
		List<object> ExcludeEntities;
		string PLPerm = "preventlooting.use";
		string PlayerPerm = "preventlooting.player";
		string CorpsePerm = "preventlooting.corpse";
		string BackpackPerm = "preventlooting.backpack";
		string StoragePerm = "preventlooting.storage";
		string AdmPerm = "preventlooting.admin";
		List<string> neededShortNames = new List<string> {
			"fuelstorage",
			"hopperoutput",
			"crudeoutput"
		};
		private readonly Collider[] colBuffer = (Collider[])typeof(Vis).GetField("colBuffer", (BindingFlags.Static | BindingFlags.NonPublic)).GetValue(null);
		
		class StoredData
        {
            public Dictionary<ulong, EntityData> Data = new Dictionary<ulong, EntityData>();
            public StoredData()
            {
            }
        }

        class EntityData
        {
			public List<ulong> Share = new List<ulong>();
			public EntityData(){}
        }
		
		StoredData storedData;
		
		#endregion
		#region Initialization
		void Init()
        {
            LoadDefaultConfig();
			permission.RegisterPermission(PLPerm, this);
			permission.RegisterPermission(AdmPerm, this);
			permission.RegisterPermission(PlayerPerm, this);
			permission.RegisterPermission(CorpsePerm, this);
			permission.RegisterPermission(BackpackPerm, this);
			permission.RegisterPermission(StoragePerm, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("PreventLooting");
			
        }
		void OnServerSave() => Interface.Oxide.DataFileSystem.WriteObject("PreventLooting", storedData);
		void Unload() => Interface.Oxide.DataFileSystem.WriteObject("PreventLooting", storedData);
		#endregion
		#region Configuration
        protected override void LoadDefaultConfig()
        {
			Config["UsePermission"] = UsePermission = GetConfig("UsePermission", false);
			Config["UseFriendsAPI"] = UseFriendsAPI = GetConfig("UseFriendsAPI", true);
			Config["AdminCanLoot"] = AdminCanLoot = GetConfig("AdminCanLoot", true);
			Config["CanLootPlayer"] = CanLootPlayer = GetConfig("CanLootPlayer", false);
			Config["CanLootCorpse"] = CanLootCorpse = GetConfig("CanLootCorpse", false);
			Config["CanLootEntity"] = CanLootEntity = GetConfig("CanLootEntity", false);
			Config["CanLootBackpack"] = CanLootBackpack = GetConfig("CanLootBackpack", false);
			Config["UseZoneManager"] = UseZoneManager = GetConfig("UseZoneManager", false);			
			Config["ZoneID"] = ZoneID = GetConfig("ZoneID", new List<object>{"12345678"});
			Config["UseExcludeEntities"] = UseExcludeEntities = GetConfig("UseExcludeEntities", true);
			Config["ExcludeEntities"] = ExcludeEntities = GetConfig("ExcludeEntities", new List<object>{"mailbox.deployed"});
			Config["UseCupboard"] = UseCupboard = GetConfig("UseCupboard", false);
			Config["UseOnlyInCupboardRange"] = UseOnlyInCupboardRange = GetConfig("UseOnlyInCupboardRange", false);
			SaveConfig();
        }		
		#endregion		
		#region Localization
		
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["OnTryLootPlayer"] = "You can not loot players!",
				["OnTryLootCorpse"] = "You can not loot corpses of players!",
				["OnTryLootEntity"] = "You can not use this entity because it is not yours!",
				["OnTryLootBackpack"] = "You can not open this backup because it is not yours!",
				["NoAccess"] = "This entity is not yours!",
				["PlayerNotFound"] = "Player {0} not found!",
				["ShareAll"] = "All players were given permission to use this entity!",
				["SharePlayer"] = "The player {0} was given permission to use this entity!",
				["NoShare"] = "No permissions have been found for this entity!",
				["ListShare"] = "List of permissions for this entity:",
				["EntityNotFound"] = "You are not standing in front of the entity or away from it!",
				["HasShareAllList"] = "All players are allowed to use this entity!",
				["ShareClear"] = "All permissions for this entity have been deleted!",
				["HasShareAll"] = "All players already have permission to use this entity!",
				["HasSharePlayer"] = "Player {0} already has permission to use this entity!",
				["HasUnShareAll"] = "Permission to use this entity has not been issued to all players!",
				["HasUnSharePlayer"] = "Player {0} has not been granted permission to use this entity!",
				["WasUnShareAll"] = "All players have been removed permission for this entity!",
				["WasUnSharePlayer"] = "The permission to use this entity has been removed from player {0}!",
				["MultiplePlayerFind"]="Multiple players found:",
				["OwnEntity"]="This object is yours!",
				["NoPermission"]="You do not have enough rights to execute this command!",
            }, this);
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["OnTryLootPlayer"] = "Вы не можете обворовывать игроков!",
                ["OnTryLootCorpse"] = "Вы не можете обворовывать трупы игроков!",
                ["OnTryLootEntity"] = "Вы не можете использовать этот объект, потому что он вам не принадлежит!",
				["OnTryLootBackpack"] = "Вы не можете открыть чужой рюкзак!",
				["NoAccess"]="Этот объект не принадлежит вам!",
				["PlayerNotFound"]="Игрок с именем {0} не найден!",
				["ShareAll"]="Всем игрокам было выдано разрешение на использование этого объекта!",
				["SharePlayer"]="Игроку {0} было выдано разрешение на использование этого объекта!",
				["NoShare"]="Не найдено разрешений для этого объекта!",
				["ListShare"]="Список разрешений для этого объекта:",
				["EntityNotFound"]="Вы стоите не перед хранилищем или далеко от него!",
				["HasShareAllList"]="Всем игрокам разрешено использовать этот объект!",
				["ShareClear"]="Все разрешения для этого объекта были удалены!",
				["HasShareAll"]="Все игроки уже имеют разрешение на использование этого объекта!",
				["HasSharePlayer"]="Игрок {0} уже имеет разрешение на использование этого объекта!",
				["HasUnShareAll"]="Разрешение на использование этого объекта не было выдано для всех игроков!",
				["HasUnSharePlayer"]="Игроку {0} не было выдано разрешение на использование этого объекта!",	
				["WasUnShareAll"]="Всем игрокам было удалено разрешение на использование этого объекта!",
				["WasUnSharePlayer"]="Игроку {0} было удалено разрешение на использование этого объекта!",
				["MultiplePlayerFind"]="Найдено несколько игроков:",
				["OwnEntity"]="Этот объект принадлежит вам!",
				["NoPermission"]="У вас недостаточно прав для выполнения этой команды!",
            }, this, "ru");

        }
        #endregion
		#region Hooks
	
		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if(player.IsAdmin && AdminCanLoot) return;
			if(permission.UserHasPermission(player.userID.ToString(), AdmPerm)) return;
			if(UseZoneManager && ZoneManager != null)
			{
				foreach(var zoneID in ZoneID)
				{
					if((bool)ZoneManager.Call("isPlayerInZone", zoneID, player)) return;				
				}
			}
			if(entity is SupplyDrop) return;
			if(entity is LootableCorpse)
			{		
				if(IsFriend((entity as LootableCorpse).playerSteamID, player.userID)) return;
				if(UsePermission && !permission.UserHasPermission((entity as LootableCorpse).playerSteamID.ToString(), CorpsePerm)) return;
				if((entity as LootableCorpse).playerSteamID < 76561197960265728L || CanLootCorpse || player.userID == (entity as LootableCorpse).playerSteamID) return;
				StopLooting(player, "OnTryLootCorpse");
				return;
			}
			if(entity is BasePlayer)
			{
				if(IsFriend((entity as BasePlayer).userID, player.userID)) return;
				if(UsePermission && !permission.UserHasPermission((entity as BasePlayer).userID.ToString(), PlayerPerm)) return;
				if(player.userID == (entity as BasePlayer).userID || CanLootPlayer) return;
				StopLooting(player, "OnTryLootPlayer");
				return;
			}	
			if((entity is DroppedItemContainer) && entity.name.Contains("item_drop_backpack"))
			{
				if(IsFriend((entity as DroppedItemContainer).playerSteamID, player.userID)) return;
				if(UsePermission && !permission.UserHasPermission((entity as DroppedItemContainer).playerSteamID.ToString(), BackpackPerm)) return;
				if((entity as DroppedItemContainer).playerSteamID < 76561197960265728L || CanLootBackpack || player.userID == (entity as DroppedItemContainer).playerSteamID) return;
				StopLooting(player, "OnTryLootBackpack");	
				return;
			}
			var st = entity.GetComponent<StorageContainer>();
			if (neededShortNames.Contains(st?.inventory.entityOwner.ShortPrefabName))
			{
				List<BaseCombatEntity> entlist = new List<BaseCombatEntity>();
				Vis.Entities<BaseCombatEntity>(player.transform.position, 10f, entlist);
				foreach (BaseCombatEntity success in entlist)
				{	
					if (success is MiningQuarry)
					{
						var subs = (success as MiningQuarry).children;
						if (subs != null)
						{
							foreach (var sub in subs)
							{
								if(sub.GetComponent<StorageContainer>()) entity.OwnerID=(success as BaseEntity).OwnerID;
							}
						}
					}	
				}
			}
			if(IsFriend(entity.OwnerID, player.userID)) return;			
			if(UsePermission && !permission.UserHasPermission(entity.OwnerID.ToString(), StoragePerm)) return;		
			if(UseExcludeEntities)
			{
				if(ExcludeEntities.Contains(entity.ShortPrefabName)) return;
			}			
			if(storedData.Data.ContainsKey(entity.net.ID))
				{
					if(storedData.Data[entity.net.ID].Share.Contains(player.userID) || storedData.Data[entity.net.ID].Share.Contains(0)) return;
				}
			if(IsVendingOpen(player, entity) || IsDropBoxOpen(player, entity) || CanLootEntity) return;
			if(entity.OwnerID != player.userID && entity.OwnerID != 0)
			{								
				if(UseCupboard || UseOnlyInCupboardRange)
				{	
					Collider[] findcoll = Physics.OverlapBox(entity.transform.position, entity.bounds.extents/2f, entity.transform.rotation, LayerMask.GetMask("Trigger"));
					if(CheckAuthCupboard(entity, player, findcoll)) return;
				}
				StopLooting(player, "OnTryLootEntity");
			}
		}
		private void StopLooting(BasePlayer player, string message)
		{			
			NextTick(() => player.EndLooting());
			SendReply(player, lang.GetMessage(message, this, player.UserIDString));	
		}	
		
		bool IsVendingOpen(BasePlayer player, BaseEntity entity)
		{
			if(entity is VendingMachine) 
			{	
				VendingMachine shopFront = entity as VendingMachine;
				if(shopFront.PlayerInfront(player)) return true;
				return false;		
			}
			return false;
		}
		bool IsDropBoxOpen(BasePlayer player, BaseEntity entity)
		{
			if(entity is DropBox) 
			{	
				DropBox dropboxFront = entity as DropBox;
				if(dropboxFront.PlayerInfront(player)) return true;
				return false;		
			}
			return false;
		}		
		bool IsFriend(ulong playerid, ulong friend)
		{
			if (UseFriendsAPI && Friends != null)	
			{
				var fr = Friends.CallHook("AreFriends", playerid, friend);
                if (fr != null && (bool)fr) return true;
			}
			return false;
		}
		
		bool FindEntityFromRay(BasePlayer player, out object success)
        {
			success = null;
			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 2.2f))
				return false;
			success = hit.GetEntity();
			return true; 
        }
		bool CheckAuthCupboard(BaseEntity entity, BasePlayer player, Collider[] findcoll)
		{
			if(UseOnlyInCupboardRange && (findcoll == null || findcoll.Length == 0)) return true;
			if(UseCupboard)
			{
				List<BuildingPrivlidge> listcupbpriv = new List<BuildingPrivlidge>();
				foreach (Collider findentity in findcoll)
				{
					BuildingPrivlidge cupbpriv = findentity.GetComponentInParent<BuildingPrivlidge>();
					if (cupbpriv != null)
					{		
						listcupbpriv.Add(cupbpriv);
					}				
				}
				if(listcupbpriv.Any(p => p.IsAuthed(player))) return true;
			}
			return false;	
		}
		#endregion
		#region Commands
	
		[ChatCommand("share")]
        void Share(BasePlayer player, string command, string[] args)
        {	
		    if (UsePermission && !permission.UserHasPermission(player.UserIDString, PLPerm)) 
			{
				SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
				return;
			}
			ulong ID;
			string tgname="";
							
			if (args == null || args.Length <= 0)
			{
				ID=0;
			}
			else
			{
				var playersadd = covalence.Players.FindPlayers(args[0]).ToList();
				if(playersadd.Count > 1)
				{
					
					var message="<color=red>"+lang.GetMessage("MultiplePlayerFind", this, player.UserIDString)+"</color>\n";
					int i=0;
					foreach(var pl in playersadd)
					{
						i++;
						message+= string.Format("{0}. <color=orange>{1}</color> ({2})\n\r", i, pl.Name, pl.Id);
					}
					SendReply(player, message);
                	return;
                }
				var playeradd = covalence.Players.FindPlayer(args[0]);
				if(playeradd==null) 
				{
					SendReply(player, string.Format(lang.GetMessage("PlayerNotFound", this, player.UserIDString), "<color=orange>"+args[0]+"</color>")); 
					return;
				}
				ID=Convert.ToUInt64(playeradd.Id);
				tgname=playeradd.Name;				
			}
			object success;
			if (FindEntityFromRay(player, out success))
			{
				if (success is BaseEntity)
				{	
					if((success as BaseEntity).OwnerID == ID)
					{
						SendReply(player, lang.GetMessage("OwnEntity", this, player.UserIDString));
						return;
					}				
					if((success as BaseEntity).OwnerID != player.userID && (!player.IsAdmin || (player.IsAdmin &&!AdminCanLoot)))
					{
						SendReply(player, lang.GetMessage("NoAccess", this, player.UserIDString));
						return;
					}
					if(!storedData.Data.ContainsKey((success as BaseEntity).net.ID)) 
					{
						var data = new EntityData();
						data.Share = new List<ulong>();
						storedData.Data.Add((success as BaseEntity).net.ID, data);
						data.Share.Add(ID);
						if(ID==0) SendReply(player, lang.GetMessage("ShareAll", this, player.UserIDString));
						else SendReply(player, string.Format(lang.GetMessage("SharePlayer", this, player.UserIDString), "<color=orange>"+tgname+"</color>"));
					}	
					else 
					{
						if(storedData.Data[(success as BaseEntity).net.ID].Share.Contains(ID))
						{
							if(ID==0) SendReply(player, lang.GetMessage("HasShareAll", this, player.UserIDString));
							else SendReply(player, string.Format(lang.GetMessage("HasSharePlayer", this, player.UserIDString), "<color=orange>"+tgname+"</color>"));
						}
						else
						{
							storedData.Data[(success as BaseEntity).net.ID].Share.Add(ID);
							if(ID==0) SendReply(player, lang.GetMessage("ShareAll", this, player.UserIDString));
							else SendReply(player, string.Format(lang.GetMessage("SharePlayer", this, player.UserIDString), "<color=orange>"+tgname+"</color>"));
						}
					}
				}
		    }
			else
			{
				SendReply(player, lang.GetMessage("EntityNotFound", this, player.UserIDString));
			}	
        }
		
        [ChatCommand("unshare")]
        void Unshare(BasePlayer player, string command, string[] args)
        {
			if (UsePermission && !permission.UserHasPermission(player.UserIDString, PLPerm)) 
			{
				SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
				return;
			}
			ulong ID;
			string tgname="";
						
			if (args == null || args.Length <= 0)
			{
				ID=0;
			}
			else
			{	
				var playersremove = covalence.Players.FindPlayers(args[0]).ToList();
				if(playersremove.Count > 1)
				{
					var message="<color=red>"+lang.GetMessage("MultiplePlayerFind", this, player.UserIDString)+"</color>\n";
					int i=0;
					foreach(var pl in playersremove)
					{
						i++;
						message+= string.Format("{0}. <color=orange>{1}</color> ({2})\n\r", i, pl.Name, pl.Id);
					}
					SendReply(player, message);
                	return;
                }
				var playerremove = covalence.Players.FindPlayer(args[0]);
				if(playerremove==null) 
				{
					SendReply(player, string.Format(lang.GetMessage("PlayerNotFound", this, player.UserIDString), "<color=orange>"+args[0]+"</color>")); 
					return;
				}
				ID=Convert.ToUInt64(playerremove.Id);
				tgname=playerremove.Name;
			}
			object success;
			if (FindEntityFromRay(player, out success))			
			{
				if (success is BaseEntity)
				{
					if((success as BaseEntity).OwnerID != player.userID && (!player.IsAdmin || (player.IsAdmin &&!AdminCanLoot)))
					{
						SendReply(player, lang.GetMessage("NoAccess", this, player.UserIDString));
						return;
					}
					if(!storedData.Data.ContainsKey((success as BaseEntity).net.ID)) 
					{
						SendReply(player, lang.GetMessage("NoShare", this, player.UserIDString));
					}	
					else 
					{
						if(!storedData.Data[(success as BaseEntity).net.ID].Share.Contains(ID))
						{
							if(ID==0) SendReply(player, lang.GetMessage("HasUnShareAll", this, player.UserIDString));	
							else SendReply(player, string.Format(lang.GetMessage("HasUnSharePlayer", this, player.UserIDString), "<color=orange>"+tgname+"</color>"));	
						}
						else
						{
							storedData.Data[(success as BaseEntity).net.ID].Share.Remove(ID);
							if(storedData.Data[(success as BaseEntity).net.ID].Share.Count==0) storedData.Data.Remove((success as BaseEntity).net.ID);
							if(ID==0) SendReply(player, lang.GetMessage("WasUnShareAll", this, player.UserIDString));
							else SendReply(player, string.Format(lang.GetMessage("WasUnSharePlayer", this, player.UserIDString), "<color=orange>"+tgname+"</color>"));
						}
						Sharelist(player);
					}
				}
			}	
			else
			{
				SendReply(player, lang.GetMessage("EntityNotFound", this, player.UserIDString));
			}			
		}
		
		[ChatCommand("sharelist")]
        void Sharelist(BasePlayer player)
        {
		    if (UsePermission && !permission.UserHasPermission(player.UserIDString, PLPerm)) 
			{
				SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
				return;
			}			
			object success;
			if (FindEntityFromRay(player, out success))			
			{			
				if (success is BaseEntity)
				{
					if((success as BaseEntity).OwnerID != player.userID && (!player.IsAdmin || (player.IsAdmin &&!AdminCanLoot)))
					{
						SendReply(player, lang.GetMessage("NoAccess", this, player.UserIDString));
						return;
					}
					if(!storedData.Data.ContainsKey((success as BaseEntity).net.ID)) 
					{
						SendReply(player, lang.GetMessage("NoShare", this, player.UserIDString));
					}
					else
					{
						if(storedData.Data[(success as BaseEntity).net.ID].Share.Contains(0))
						{
							SendReply(player, lang.GetMessage("HasShareAllList", this, player.UserIDString));
							return;
						}	
						var message="<color=yellow>"+lang.GetMessage("ListShare", this, player.UserIDString)+"</color>\n";
						int i=0;
						foreach(var share in storedData.Data[(success as BaseEntity).net.ID].Share)
						{
							i++;
							message+= string.Format("{0}. <color=green>{1}</color> ({2})\n\r", i, covalence.Players.FindPlayer(share.ToString()).Name, covalence.Players.FindPlayer(share.ToString()).Id);
						}	
						SendReply(player, message);
					}
				}
			}
			else
			{
				SendReply(player, lang.GetMessage("EntityNotFound", this, player.UserIDString));
			}
		}
		
		[ChatCommand("shareclear")]
        void Shareclear(BasePlayer player)
        {
		    if (UsePermission && !permission.UserHasPermission(player.UserIDString, PLPerm)) 
			{
				SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
				return;
			}			
			object success;
			if (FindEntityFromRay(player, out success))			
			{
				if (success is BaseEntity)
				{
					if((success as BaseEntity).OwnerID != player.userID && (!player.IsAdmin || (player.IsAdmin &&!AdminCanLoot)))
					{
						SendReply(player, lang.GetMessage("NoAccess", this, player.UserIDString));
						return;
					}
					if(!storedData.Data.ContainsKey((success as BaseEntity).net.ID)) 
					{
						SendReply(player, lang.GetMessage("NoShare", this, player.UserIDString));
					}
					else
					{
						storedData.Data[(success as BaseEntity).net.ID].Share.Clear();
						if(storedData.Data[(success as BaseEntity).net.ID].Share.Count==0) storedData.Data.Remove((success as BaseEntity).net.ID);
						SendReply(player, lang.GetMessage("ShareClear", this, player.UserIDString));
					}
				}	
			}
			else
			{
				SendReply(player, lang.GetMessage("EntityNotFound", this, player.UserIDString));
			}
		}
		#endregion
		#region Helpers
		T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T) Convert.ChangeType(Config[name], typeof(T)); 
		#endregion
    }
}
