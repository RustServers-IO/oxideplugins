﻿using System;
using System.Linq;
using System.Collections.Generic;
using Rust;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MagicHammer", "Norn/Werkrat", "0.5.2", ResourceId = 1375)]
    [Description("Hit stuff with the hammer and do things.")]
    public class MagicHammer : RustPlugin
    {
        int MODE_REPAIR = 1;
        int MODE_DESTROY = 2;
        int MAX_MODES = 2;
		int Modes_Enabled = 0;
		int RETURNED;
		
        [PluginReference]
        Plugin PopupNotifications;
        class StoredData
        {
            public Dictionary<ulong, MagicHammerInfo> Users = new Dictionary<ulong, MagicHammerInfo>();
            public StoredData()
            {
            }
        }

        class MagicHammerInfo
        {
            public ulong UserId;
            public int Mode;
            public bool Enabled;
            public bool Messages_Enabled;
            public MagicHammerInfo()
            {
            }
        }

        StoredData hammerUserData;
		static FieldInfo buildingPrivilege = typeof(BasePlayer).GetField("buildingPrivilege", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        void Loaded()
        {
			if (!permission.PermissionExists("magichammer.allowed")) permission.RegisterPermission("magichammer.allowed", this);
            hammerUserData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title + "_users");
		}
		void OnPlayerInit(BasePlayer player)
        {
            InitPlayerData(player);
        }
        bool InitPlayerData(BasePlayer player)
        {
            if(CanMagicHammer(player))
            {
                MagicHammerInfo p = null;
                if (hammerUserData.Users.TryGetValue(player.userID, out p) == false)
                {
                    var info = new MagicHammerInfo();
                    info.Enabled = false;
                    info.Mode = MODE_REPAIR; //Repair
                    info.UserId = player.userID;
                    info.Messages_Enabled = true;
                    hammerUserData.Users.Add(player.userID, info);
                    Interface.GetMod().DataFileSystem.WriteObject(this.Title + "_users", hammerUserData);
                    Puts("Adding entry " + player.userID.ToString());
                }
            }
            else
            {
                MagicHammerInfo p = null;
                if (hammerUserData.Users.TryGetValue(player.userID, out p))
                {
                    Puts("Removing " + player.userID + " from magic hammer data, cleaning up...");
                    hammerUserData.Users.Remove(player.userID);
                }
            }
            return false;
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Updating configuration file...");
            Config.Clear();
            Config["iProtocol"] = Protocol.network;
            Config["bUsePopupNotifications"] = false;
            Config["bMessagesEnabled"] = true;
			Config["bChargeForRepairs"] = true;
			Config["nTimeSinceAttacked"] = 8;
			Config["nModesEnabled(1=repair only, 2=destroy only, 3=both enabled)"] = 1;
            Config["tMessageRepaired"] = "Entity: <color=#F2F5A9>{entity_name}</color> health <color=#2EFE64>updated</color> from <color=#FF4000>{current_hp}</color>/<color=#2EFE64>{new_hp}</color>.";
            Config["tMessageDestroyed"] = "Entity: <color=#F2F5A9>{entity_name}</color> <color=#FF4000>destroyed</color>.";
            Config["tMessageUsage"] = "/mh <enabled/mode>.";
            Config["tHammerEnabled"] = "Status: {hammer_status}.";
            Config["tHammerMode"] = "You have switched to: {hammer_mode} mode.";
			Config["tMessageModeDisabled"] = "{disabled_mode} mode is currently <color=#FF4000>disabled</color>";
            Config["tHammerModeText"] = "Choose your mode: 1 = <color=#2EFE64>repair</color>, 2 = <color=#FF4000>destroy</color>.";
            Config["tNoAccessCupboard"] = "You <color=#FF4000>don't</color> have access to all the tool cupboards around you.";
            Config["bDestroyCupboardCheck"] = true;
            SaveConfig();
        }
        bool CanMagicHammer(BasePlayer player)
        {
            if (permission.UserHasPermission(player.userID.ToString(), "magichammer.allowed")) return true;
            return false;
        }
        private void PrintToChatEx(BasePlayer player, string result, string tcolour = "#F5A9F2")
        {
            if (Convert.ToBoolean(Config["bMessagesEnabled"]))
            {
                if (Convert.ToBoolean(Config["bUsePopupNotifications"]))
                {
                    PopupNotifications?.Call("CreatePopupNotification", "<color=" + tcolour + ">" + this.Title.ToString() + "</color>\n" + result, player);
                }
                else
                {
                    PrintToChat(player, "<color=\"" + tcolour + "\">[" + this.Title.ToString() + "]</color> " + result);
                }
            }
        }
        void Unload()
        {
            Puts("Saving MagicHammer database...");
            SaveData();
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Title+"_users", hammerUserData);
        }
        int GetPlayerHammerMode(BasePlayer player)
        {
            MagicHammerInfo p = null;
            if (hammerUserData.Users.TryGetValue(player.userID, out p))
            {
                return p.Mode;
            }
            return -1;
        }
        bool SetPlayerHammerMode(BasePlayer player, int mode)
        {
            MagicHammerInfo p = null;
            if (hammerUserData.Users.TryGetValue(player.userID, out p))
            {
                p.Mode = mode;
                return true;
            }
            return false;
        }
        bool SetPlayerHammerStatus(BasePlayer player, bool enabled)
        {
            MagicHammerInfo p = null;
            if (hammerUserData.Users.TryGetValue(player.userID, out p))
            {
                p.Enabled = enabled;
                return true;
            }
            return false;
        }
        bool MagicHammerEnabled(BasePlayer player)
        {
            MagicHammerInfo p = null;
            if (hammerUserData.Users.TryGetValue(player.userID, out p))
            {
                return p.Enabled;
            }
            return false;
        }
        [ChatCommand("mh")]
        void cmdMH(BasePlayer player, string cmd, string[] args)
        {
            if (CanMagicHammer(player))
            {
                MagicHammerInfo p = null;
                if (hammerUserData.Users.TryGetValue(player.userID, out p) == false)
                {
                    InitPlayerData(player);
                }
                if (args.Length == 0 || args.Length > 2)
                {
                    PrintToChatEx(player, Config["tMessageUsage"].ToString());
                    if (player.net.connection.authLevel >= 1)
                    {
                        // Future Admin Cmds
                    }
                }
                else if (args[0] == "mode")
                {
                    if (args.Length == 1)
                    {
                        PrintToChatEx(player, Config["tHammerModeText"].ToString());
                    }
                    else if (args.Length == 2 && System.Text.RegularExpressions.Regex.IsMatch(args[1], @"^\d+$"))
                    {
                        int mode = Convert.ToInt16(args[1]);
                        if (mode >= 1 && mode <= MAX_MODES)
                        {
                            string mode_text = "null";
                            if (mode == MODE_REPAIR)
                            {
								mode_text = "<color=#2EFE64>repair</color>";
                            }
                            else if (mode == MODE_DESTROY)
                            {
								mode_text = "<color=#FF4000>destroy</color>";
                            }
							SetPlayerHammerMode(player, mode);
							string parsed_config = Config["tHammerMode"].ToString();
							parsed_config = parsed_config.Replace("{hammer_mode}", mode_text);
							PrintToChatEx(player, parsed_config);
                        }
                        else
                        {
                            PrintToChatEx(player, "Valid modes: 1 - " + MAX_MODES.ToString() + "."); // Invalid Mode
                        }
                    }
					else if (args.Length == 2 && !System.Text.RegularExpressions.Regex.IsMatch(args[1], @"^\d+$"))
						PrintToChatEx(player, "Valid modes: 1 - " + MAX_MODES.ToString() + "."); // Invalid Mode
                }
                else if (args[0] == "enabled")
                {
                    if (MagicHammerEnabled(player))
                    {
                        string parsed_config = Config["tHammerEnabled"].ToString();
                        parsed_config = parsed_config.Replace("{hammer_status}", "<color=#FF4000>disabled</color>");
                        PrintToChatEx(player, parsed_config);
                        SetPlayerHammerStatus(player, false);
                    }
                    else
                    {
                        string parsed_config = Config["tHammerEnabled"].ToString();
                        parsed_config = parsed_config.Replace("{hammer_status}", "<color=#2EFE64>enabled</color>");
                        PrintToChatEx(player, parsed_config);
                        SetPlayerHammerStatus(player, true);
                    }
                }
            }
        }
        void OnStructureRepairEx(BaseCombatEntity entity, BasePlayer player)
        {
            if (CanMagicHammer(player) && MagicHammerEnabled(player))
            {
                int mode = GetPlayerHammerMode(player);
                if(mode != -1)
                {
					string block_shortname = entity.ShortPrefabName;
					string block_displayname = entity.ShortPrefabName;
					if (mode == MODE_REPAIR)
                    {
						if (Modes_Enabled == 1 || Modes_Enabled == 3)
						{
							RETURNED = 0;
							float current_health = entity.health;
							var ret = repairStructure(entity, player);
							if (ret == null) 
							{
								RETURNED = 1;
								return;
							}
							string parsed_config = Config["tMessageRepaired"].ToString();
							parsed_config = parsed_config.Replace("{current_hp}", current_health.ToString());
							parsed_config = parsed_config.Replace("{new_hp}", entity.health.ToString());
							if (block_displayname.Length == 0)
							{
								parsed_config = parsed_config.Replace("{entity_name}", block_shortname);
							}
							else
							{
								parsed_config = parsed_config.Replace("{entity_name}", block_displayname);
							}
							PrintToChatEx(player, parsed_config);
						}
						else
						{
							string parsed_config = Config["tMessageModeDisabled"].ToString();
							parsed_config = parsed_config.Replace("{disabled_mode}", "repair");
							PrintToChatEx(player, parsed_config);
							return;
						}
					}
					else if (mode == MODE_DESTROY)
					{
						if (Modes_Enabled == 2 || Modes_Enabled == 3)
						{
							if (Convert.ToBoolean(Config["bDestroyCupboardCheck"]))
							{
								if (hasTotalAccess(player))
								{
									string parsed_config = Config["tMessageDestroyed"].ToString();
									if (block_displayname.Length == 0)
									{
										parsed_config = parsed_config.Replace("{entity_name}", block_shortname);
									}
									else
									{
										parsed_config = parsed_config.Replace("{entity_name}", block_displayname);
									}
									PrintToChatEx(player, parsed_config);
									RemoveEntity(entity);
								}
								else
								{
									PrintToChatEx(player, Config["tNoAccessCupboard"].ToString());
								}
							}
							else
							{
								string parsed_config = Config["tMessageDestroyed"].ToString();
								if (block_displayname.Length == 0)
								{
									parsed_config = parsed_config.Replace("{entity_name}", block_shortname);
								}
								else
								{
									parsed_config = parsed_config.Replace("{entity_name}", block_displayname);
								}
								PrintToChatEx(player, parsed_config);
								RemoveEntity(entity);
							}
						}
						else
						{
							string parsed_config = Config["tMessageModeDisabled"].ToString();
							parsed_config = parsed_config.Replace("{disabled_mode}", "destroy");
							PrintToChatEx(player, parsed_config);
							return;
						}
                    }
                }
            }
        }
        static void RemoveEntity(BaseCombatEntity entity)
        {
            if (entity == null) return;
            entity.KillMessage();
        }
        static bool hasTotalAccess(BasePlayer player)
        {
            List<BuildingPrivlidge> playerpriv = buildingPrivilege.GetValue(player) as List<BuildingPrivlidge>;
            if (playerpriv.Count == 0)
            {
                return false;
            }
            foreach (BuildingPrivlidge priv in playerpriv.ToArray())
            {
                List<ProtoBuf.PlayerNameID> authorized = priv.authorizedPlayers;
                bool flag1 = false;
                foreach (ProtoBuf.PlayerNameID pni in authorized.ToArray())
                {
                    if (pni.userid == player.userID)
                        flag1 = true;
                }
                if (!flag1)
                {
                    return false;
                }
            }
            return true;
        }
		private object repairStructure(BaseCombatEntity entity, BasePlayer player)
		{
			if (entity.SecondsSinceAttacked <= (int)Config["nTimeSinceAttacked"])
				return null;
			if (!(bool)Config["bChargeForRepairs"])
			{
				entity.health = entity.MaxHealth();
				entity.OnRepair();
				entity.SendNetworkUpdateImmediate(true);
				return false;
			}
			
			float hp = entity.health;
			int i = 0;
			Dictionary<int,int> charge = new Dictionary<int,int>();
			while (hp < entity.MaxHealth())
			{
				if (i >= 30)
				{
					Puts("Breaking loop -- Something went wrong");
					break;
				}
				i += 1;
				float single = 50f;
				float single1 = entity.MaxHealth() - hp;
				single1 = Mathf.Clamp(single1, 0f, single);
				float single2 = single1 / entity.MaxHealth();
				if (single2 == 0f)
					return false;
				List<ItemAmount> itemAmounts = entity.RepairCost(single2);
				if (itemAmounts == null)
					return false;
				float single3 = itemAmounts.Sum<ItemAmount>((ItemAmount x) => x.amount);
				if (single3 <= 0f)
					hp = entity.MaxHealth(); 
				else
				{
					float single4 = itemAmounts.Min<ItemAmount>((ItemAmount x) => Mathf.Clamp01((float)player.inventory.GetAmount(x.itemid) / x.amount));
					if (single4 == 0f)
						return false;
					int num = 0;
					foreach (ItemAmount itemAmount in itemAmounts)
					{
						int num1 = Mathf.CeilToInt(single4 * itemAmount.amount);
						int num2 = num1;
						num = num + num2;
						if (charge.ContainsKey(itemAmount.itemid))
							charge[itemAmount.itemid] = charge[itemAmount.itemid] + num2;
						else
							charge.Add(itemAmount.itemid,num2);
					}
					float single5 = (float)num / (float)single3;
					hp = hp + single1 * single5;
					entity.OnRepair();
				}
			}
			foreach(KeyValuePair<int, int> item in charge)
			{
				ItemDefinition defs;
				defs = ItemManager.FindItemDefinition(item.Key);
				if (player.inventory.GetAmount(defs.itemid) < item.Value)
					return null;
			}
			foreach(KeyValuePair<int, int> item in charge)
			{
				player.inventory.Take(null, item.Key, item.Value);
				player.Command("note.inv", new object[] { item.Key, item.Value * -1 });
			}
			entity.health = entity.MaxHealth();
			entity.SendNetworkUpdateImmediate(true);
			return false;
		}
        private void OnServerInitialized()
        {
            if (Config["tMessageModeDisabled"] == null) { Puts("Resetting configuration file (out of date)..."); LoadDefaultConfig(); }
			Modes_Enabled = (int)Config["nModesEnabled(1=repair only, 2=destroy only, 3=both enabled)"];
        }
        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
			if (CanMagicHammer(player) && MagicHammerEnabled(player))
			{
				int mode = GetPlayerHammerMode(player);
				if ((mode == MODE_REPAIR && (Modes_Enabled == 1 || Modes_Enabled == 3)) || (mode == MODE_DESTROY && (Modes_Enabled == 2 || Modes_Enabled == 3)))
				{
					if (entity.health < entity.MaxHealth() || GetPlayerHammerMode(player) == MODE_DESTROY)
					{
						OnStructureRepairEx(entity, player);
						if (RETURNED == 1)
							return null; //can't afford or recently damaged
						else
							return false; //repaired - block default repair
					}
					else
						return null; //full health - ignore
				}
				//Using disabled mode... send msg and change their mode to avoid spam
				else
				{
					string str = "";
					if (mode == MODE_REPAIR)
						str = "repair";
					else if (mode == MODE_DESTROY)
						str = "destroy";
					string parsed_config = Config["tMessageModeDisabled"].ToString();
					parsed_config = parsed_config.Replace("{disabled_mode}", str);
					PrintToChatEx(player, parsed_config);
					
					parsed_config = Config["tHammerEnabled"].ToString();
                    parsed_config = parsed_config.Replace("{hammer_status}", "<color=#FF4000>disabled</color>");
                    PrintToChatEx(player, parsed_config);
                    SetPlayerHammerStatus(player, false);
					return null; //don't block normal repair even if plugin has repair/destroy disabled
				}
			}
			else 
				return null; //user not allowed to use MagicHammer -OR- they have it disabled (so regular repairing isn't blocked)
        }
    }
}