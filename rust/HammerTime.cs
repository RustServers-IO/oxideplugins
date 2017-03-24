using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hammer Time", "Shady", "1.0.15", ResourceId = 1711)]
    [Description("Tweak settings for building blocks like demolish time, and rotate time.")]
    class HammerTime : RustPlugin
    {
        [PluginReference]
        Plugin Friends;
        [PluginReference]
        Plugin Clans;
        #region Config/Init
        float DemolishTime;
        float RotateTime;
        float RepairCooldown;
        float pluginInitTime = 0f;

        bool DemolishAfterRestart;
        bool RotateAfterRestart;
        bool MustOwnDemolish;
        bool MustOwnRotate;

        bool FriendsCanDemolish;
        bool FriendsCanRotate;

        bool ClanCanDemolish;
        bool ClanCanRotate;


        /*--------------------------------------------------------------//
		//			Load up the default config on first use				//
		//--------------------------------------------------------------*/
        protected override void LoadDefaultConfig()
        {
            if (Config["AuthLevelOverrideDemolish"] != null) Config.Remove("AuthLevelOverrideDemolish");
            Config["DemolishTime"] = DemolishTime = GetConfig("DemolishTime", 600f);
            Config["RotateTime"] = RotateTime = GetConfig("RotateTime", 600f);
            Config["MustOwnToDemolish"] = MustOwnDemolish = GetConfig("MustOwnToDemolish", false);
            Config["MustOwnToRotate"] = MustOwnRotate = GetConfig("MustOwnToRotate", false);
            Config["AllowDemolishAfterServerRestart"] = DemolishAfterRestart = GetConfig("AllowDemolishAfterServerRestart", false);
            Config["AllowRotateAfterServerRestart"] = RotateAfterRestart = GetConfig("AllowRotateAfterServerRestart", false);
            Config["RepairDamageCooldown"] = RepairCooldown = GetConfig("RepairDamageCooldown", 8f);
            Config["FriendsCanDemolish"] = FriendsCanDemolish = GetConfig("FriendsCanDemolish", false);
            Config["FriendsCanRotate"] = FriendsCanRotate = GetConfig("FriendsCanRotate", false);
            Config["ClanCanDemolish"] = ClanCanDemolish = GetConfig("ClanCanDemolish", false);
            Config["ClanCanRotate"] = ClanCanRotate = GetConfig("ClanCanRotate", false);
            SaveConfig();
        }

        
        private void Init()
        {
            pluginInitTime = UnityEngine.Time.realtimeSinceStartup;
            LoadDefaultMessages();
            LoadDefaultConfig();
            permission.RegisterPermission("hammertime.allowed", this);
            permission.RegisterPermission("hammertime.repaircooldown", this);
            permission.RegisterPermission("hammertime.demolishoverride", this);
            permission.RegisterPermission("hammertime.rotateoverride", this);
        }


        void OnServerInitialized()
        {
            if ((UnityEngine.Time.realtimeSinceStartup - pluginInitTime) < 1) return; //server was probably already running, and not first start up
            if (!DemolishAfterRestart && !RotateAfterRestart) return;
            foreach(var entity in BaseEntity.saveList)
            {
                if (entity == null || (entity?.IsDestroyed ?? true)) continue;
                var block = entity?.GetComponent<BuildingBlock>() ?? null;
                if (block == null || !HasPerms((block?.OwnerID ?? 0), "hammertime.allowed")) continue;
                if ((block?.grade ?? BuildingGrade.Enum.Twigs) == BuildingGrade.Enum.Twigs) continue;
                var doRotate = false;
                if (RotateAfterRestart) doRotate = block?.blockDefinition?.canRotate ?? RotateAfterRestart;
                if (!doRotate && !DemolishAfterRestart) continue;
                DoInvokes(block, DemolishAfterRestart, doRotate, false);
            }
        }

        /*--------------------------------------------------------------//
        //			Localization Stuff			                        //
        //--------------------------------------------------------------*/

        private void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                //DO NOT EDIT LANGUAGE FILES HERE! Navigate to oxide\lang\HammerTime.en.json
                {"doesNotOwnDemo", "You do not have access to demolish this object!"},
                {"doesNotOwnRotate", "You do not have access to rotate this object!" }
            };
            lang.RegisterMessages(messages, this);
        }
        #endregion;
        #region InvokeBlocks
        void DoInvokes(BuildingBlock block, bool demo, bool rotate, bool justCreated)
        {
            if (block == null || (block?.IsDestroyed ?? true)) return;
            if (demo)
            {
                if (DemolishTime < 0)
                {
                    block.CancelInvoke("StopBeingDemolishable");
                    block.SetFlag(BaseEntity.Flags.Reserved2, true, false);
                }
                if (DemolishTime == 0) block.Invoke("StopBeingDemolishable", 0.01f);
                if (DemolishTime >= 1 && DemolishTime != 600) //if time is = to 600, then it's default, and there's no point in changing anything
                {
                    block.CancelInvoke("StopBeingDemolishable");
                    block.SetFlag(BaseEntity.Flags.Reserved2, true, false); //reserved2 is demolishable
                    block.Invoke("StopBeingDemolishable", DemolishTime);
                }
            }
            if (rotate)
            {
                if (RotateTime < 0)
                {
                    block.CancelInvoke("StopBeingRotatable");
                    block.SetFlag(BaseEntity.Flags.Reserved1, true, false); //reserved1 is rotatable
                }
                if (RotateTime == 0) block.Invoke("StopBeingRotatable", 0.01f);
                if (RotateTime >= 1 && RotateTime != 600) //if time is = to 600, then it's default, and there's no point in changing anything
                {
                    block.CancelInvoke("StopBeingRotatable");
                    block.SetFlag(BaseEntity.Flags.Reserved1, true, false); //reserved1 is rotatable
                    block.Invoke("StopBeingRotatable", RotateTime);
                }
            }
        }
        #endregion
        #region Hooks
      
        private void OnEntityBuilt(Planner plan, GameObject objectBlock)
        {
            var block = objectBlock?.ToBaseEntity()?.GetComponent<BuildingBlock>() ?? null;
            if (block == null || !HasPerms(plan?.GetOwnerPlayer()?.UserIDString ?? string.Empty, "hammertime.allowed")) return;
            var doRotate = block?.blockDefinition?.canRotate ?? true;
            NextTick(() => DoInvokes(block, true, doRotate, true));
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (!HasPerms(player.UserIDString, "hammertime.allowed")) return;
            NextTick(() => DoInvokes(block, false, block?.blockDefinition?.canRotate ?? true, false));
        }

       object OnStructureRepair(BaseCombatEntity block, BasePlayer player)
        {
            if (block == null || player == null || !HasPerms(player.UserIDString, "hammertime.repaircooldown") || RepairCooldown == 8f) return null;
            if (block.TimeSinceAttacked() < RepairCooldown) return false;
            return null;
        }

        object OnHammerHit(BasePlayer player, HitInfo hitInfo)
        {
            if (!HasPerms(player.UserIDString, "hammertime.repaircooldown")) return null;
            var entity = hitInfo?.HitEntity?.GetComponent<BaseCombatEntity>() ?? null;
            if (entity != null && entity.TimeSinceAttacked() < RepairCooldown) return false;
            return null;
        }

        object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (!MustOwnDemolish || HasPerms(player.UserIDString, "hammertime.demolishoverride") || block.OwnerID == 0) return null;
            if (block.OwnerID == player.userID) return null;
            if (FriendsCanDemolish)
            {
                var hasFriend = Friends?.Call<bool>("HasFriend", block.OwnerID, player.userID) ?? false;
                if (hasFriend) return null;
            }
            if (ClanCanDemolish)
            {
                var ownerClan = Clans?.Call<string>("GetClanOf", block.OwnerID.ToString()) ?? string.Empty;
                var targetClan = Clans?.Call<string>("GetClanOf", player.UserIDString) ?? string.Empty;
                if (!string.IsNullOrEmpty(ownerClan) && !string.IsNullOrEmpty(targetClan) && (targetClan == ownerClan)) return null;
            }
            if (block.OwnerID != player.userID)
            {
                SendReply(player, GetMessage("doesNotOwnDemo", player.UserIDString));
                return true;
            }
            return null;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (!MustOwnRotate || HasPerms(player.UserIDString, "hammertime.rotateoverride") || block.OwnerID == 0) return null;
            if (block.OwnerID == player.userID) return null;
            if (FriendsCanRotate)
            {
                var hasFriend = Friends?.Call<bool>("HasFriend", block.OwnerID, player.userID) ?? false;
                if (hasFriend) return null;
            }
            if (ClanCanRotate)
            {
                var ownerClan = Clans?.Call<string>("GetClanOf", block.OwnerID.ToString()) ?? string.Empty;
                var targetClan = Clans?.Call<string>("GetClanOf", player.UserIDString) ?? string.Empty;
                if (!string.IsNullOrEmpty(ownerClan) && !string.IsNullOrEmpty(targetClan) && (targetClan == ownerClan)) return null;
            }
            
            if (block.OwnerID != player.userID)
            {
                SendReply(player, GetMessage("doesNotOwnRotate", player.UserIDString));
                return true;
            }
            return null;
        }
        #endregion
        #region Util
        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        private string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
        private bool HasPerms(string userID, string perm)
        {
            if (string.IsNullOrEmpty(userID) || string.IsNullOrEmpty(perm)) return false;
            return permission.UserHasPermission(userID, perm);
        }
        bool HasPerms(ulong userID, string perm) { return HasPerms(userID.ToString(), perm); }
        #endregion
    }
}