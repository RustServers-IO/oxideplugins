using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;
using Network;
using ProtoBuf;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("FlippableTurrets", "DylanSMR", "1.0.5", ResourceId = 2055)]
    class FlippableTurrets : RustPlugin
    {
        #region Oxide Hooks
            void Loaded()
            {
                permission.RegisterPermission("flippableturrets.canflip", this);
                lang.RegisterMessages(messages, this);
            }
            private bool HasPerm(string id) => permission.UserHasPermission(id, "flippableturrets.canflip");
        #endregion

        #region Langauge
            Dictionary<string, string> messages = new Dictionary<string, string>()
            {
                {"Flipped", "You have flipped that turret successfully."},
                {"UnFlipped", "You have unflipped that turret successfully."},
                {"NoPermission","You have no permission to flip this turret."},
                {"Error","An unexpected error has occured, please reported this to the owner with error: 2591(code)."},
            };
        #endregion

        #region BaseHooks
            private bool IsFlipped(BaseEntity entity){
                if(!entity.ShortPrefabName.Contains("")){
                    PrintWarning("Error Flipping Turret... Error Code: 2592... Please report this to the plugin developer.");
                    return false;
                }
                List<BaseEntity> nearby = new List<BaseEntity>();
                Vis.Entities(entity.transform.position, 0.5f, nearby);
                var turret = new BaseEntity();
                    
                foreach(var ent in nearby){
                    if(ent.ShortPrefabName.Contains("autoturret_deployed") && ent.transform.eulerAngles.z == 180)
                        turret = entity;
                }
                if(turret == null)
                    return false;
                else return true;
            }

            [ChatCommand("flipturret")]
            private void FlipTurret(BasePlayer player){
                if(!HasPerm(player.UserIDString)){
                    SendReply(player, lang.GetMessage("NoPermission", this, player.UserIDString));
                    return;
                }
                List<BaseEntity> nearby = new List<BaseEntity>();
                Vis.Entities(player.transform.position, 3.0f, nearby);
                var turret = new BaseEntity();
                var floor = new BaseEntity();
                var found = new BaseEntity();
                var tst = 0;
                foreach (BaseEntity entity in nearby){
                    if(entity.ShortPrefabName.Contains("autoturret_deployed")){
                        turret = entity as AutoTurret;
                        tst++;}
                    if(entity.ShortPrefabName.Contains("floor")){
                        floor = entity;
                    }
                    if(entity.ShortPrefabName.Contains("foundation")){
                        found = entity;
                    }
                }

                if(tst > 3){
                    Puts(tst.ToString());
                    PrintWarning("Error Code 2591... Error flipping turret... Please report this bug as quick as possible!!!");
                    SendReply(player, lang.GetMessage("Error", this, player.UserIDString));
                    return;
                }
 
                if(IsFlipped(floor)){
                    SendReply(player, lang.GetMessage("UnFlipped", this, player.UserIDString));
                    List<BaseEntity> close = new List<BaseEntity>();
                    Vis.Entities(new Vector3(floor.transform.position.x, floor.transform.position.y - 4, floor.transform.position.z), 1f, close);

                    foreach (BaseEntity entity in close){
                        if(entity.ShortPrefabName.Contains("floor"))
                            floor = entity;
                        if(entity.ShortPrefabName.Contains("foundation"))
                            found = entity;
                    }

                    if(found != null){
                        turret.transform.position = found.transform.position;
                    }else if(floor != null){
                        turret.transform.position = floor.transform.position;
                    }else{
                        Puts("Error Flipping Turret... Error Code 2593... Please report this to the plugin developer!");
                        return;}

                    turret.transform.eulerAngles = new Vector3(180, turret.transform.eulerAngles.y - 180, turret.transform.eulerAngles.z);
                    turret.SendNetworkUpdateImmediate();
                    return;
                }else{
                    SendReply(player, lang.GetMessage("Flipped", this, player.UserIDString));
                    turret.transform.position = floor.transform.position; 
                    turret.transform.eulerAngles = new Vector3(180, turret.transform.eulerAngles.y - 180, turret.transform.eulerAngles.z);
                    turret.SendNetworkUpdateImmediate();
                    return;
                }
            }

        #endregion
    }
}