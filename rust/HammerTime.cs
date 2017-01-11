using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hammer Time", "Shady", "1.0.13", ResourceId = 1711)]
    [Description("Tweak settings for building blocks like demolish time, and rotate time.")]
    class HammerTime : RustPlugin
    {
        #region Config/Init
        float DemolishTime;
        float RotateTime;
        float RepairCooldown;
        float pluginInitTime = 0f;

        bool DemolishAfterRestart;
        bool RotateAfterRestart;
        bool MustOwnDemolish;
        bool MustOwnRotate;
        bool AuthLevelOverride;


        /*--------------------------------------------------------------//
		//			Load up the default config on first use				//
		//--------------------------------------------------------------*/
        protected override void LoadDefaultConfig()
        {
            Config["DemolishTime"] = DemolishTime = GetConfig("DemolishTime", 600f);
            Config["RotateTime"] = RotateTime = GetConfig("RotateTime", 600f);
            Config["MustOwnToDemolish"] = MustOwnDemolish = GetConfig("MustOwnToDemolish", false);
            Config["MustOwnToRotate"] = MustOwnRotate = GetConfig("MustOwnToRotate", false);
            Config["AllowDemolishAfterServerRestart"] = DemolishAfterRestart = GetConfig("AllowDemolishAfterServerRestart", false);
            Config["AllowRotateAfterServerRestart"] = RotateAfterRestart = GetConfig("AllowRotateAfterServerRestart", false);
            Config["AuthLevelOverrideDemolish"] =  AuthLevelOverride = GetConfig("AuthLevelOverrideDemolish", true);
            Config["RepairDamageCooldown"] = RepairCooldown = GetConfig("RepairDamageCooldown", 8f);
            SaveConfig();
        }

        
        private void Init()
        {
            pluginInitTime = UnityEngine.Time.realtimeSinceStartup;
            LoadDefaultMessages();
            LoadDefaultConfig();
        }

        void OnServerInitialized()
        {
            var timeInit = UnityEngine.Time.realtimeSinceStartup - pluginInitTime;
            if (timeInit < 1) return; //server was probably already running, and not first start up
            if (!DemolishAfterRestart && !RotateAfterRestart) return;
            foreach(var entity in BaseEntity.saveList)
            {
                var isBlock = (entity?.GetType()?.ToString() ?? string.Empty) == "BuildingBlock";
                if (!isBlock) continue;
                var block = entity?.GetComponent<BuildingBlock>() ?? null;
                if (block != null)
                {
                    var isTwig = (block?.grade ?? BuildingGrade.Enum.Twigs) == BuildingGrade.Enum.Twigs;
                    if (isTwig) continue;
                    var doRotate = RotateAfterRestart;
                    if (doRotate) doRotate = block?.blockDefinition?.canRotate ?? RotateAfterRestart;
                    if (!doRotate && !DemolishAfterRestart) continue;
                    DoInvokes(block, DemolishAfterRestart, doRotate, false);
                }
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
                {"doesNotOwnDemo", "You can only demolish objects you own!"},
                {"doesNotOwnRotate", "You can only rotate objects you own!" }
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
                    NextTick(() =>
                    {
                        //next tick just in case?
                        block.SetFlag(BaseEntity.Flags.Reserved2, true, false); //reserved2 is demolishable
                    });
                  
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
                    NextTick(() =>
                    {
                        block.SetFlag(BaseEntity.Flags.Reserved1, true, false); //reserved1 is rotatable
                        //next tick just in case?
                    });
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
            if (block == null) return;
            var doRotate = block?.blockDefinition?.canRotate ?? true;
            NextTick(() => DoInvokes(block, true, doRotate, true));
        }
        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade) { NextTick(() => DoInvokes(block, false, block?.blockDefinition?.canRotate ?? true, false)); }

       object OnStructureRepair(BaseCombatEntity block, BasePlayer player)
        {
            if (block == null || player == null) return null;
            var cooldown = RepairCooldown;
            if (cooldown < 1f) cooldown = 0f;
            if (cooldown == 8f) return null;
            if (block.TimeSinceAttacked() < cooldown) return false;
            return null;
        }

        object OnHammerHit(BasePlayer player, HitInfo hitInfo)
        {
            var entity = hitInfo?.HitEntity?.GetComponent<BaseCombatEntity>() ?? null;
            if (entity == null) return null;
            var cooldown = RepairCooldown;
            if (cooldown < 1f) cooldown = 0f;
            if (cooldown == 8f) return null;
            if (entity.TimeSinceAttacked() < cooldown) return false;
            return null;
        }

        object OnStructureDemolish(BuildingBlock block, BasePlayer player)
        {
            if (!MustOwnDemolish) return null;
            if (AuthLevelOverride && player.IsAdmin()) return null;
            if (permission.UserHasPermission(player.userID.ToString(), "hammertime.allowdemo")) return null;
            if (block.OwnerID == 0) return null;
            if (block.OwnerID != player.userID)
            {
                SendReply(player, GetMessage("doesNotOwnDemo"));
                return true;
            }
            return null;
        }

        object OnStructureRotate(BuildingBlock block, BasePlayer player)
        {
            if (!MustOwnRotate) return null;
            if (block.OwnerID == 0) return null;
            if (block.OwnerID != player.userID)
            {
                SendReply(player, GetMessage("doesNotOwnRotate"));
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
        #endregion
    }
}