using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Cupboard Logging", "DylanSMR", "1.0.5", ResourceId = 1904)]
    [Description("Creates a log when a player places a cupboard.")]
    class CupboardLogs : RustPlugin
    {  
        void Loaded(){
            logData = Interface.GetMod().DataFileSystem.ReadObject<LogData>("Cupboard-Logs");   
            lang.RegisterMessages(messages, this);    
        }
        
        public Dictionary<string, string> messages = new Dictionary<string, string>(){
            {"Formatting", "[{0}:{1}] placed a tool cupboard with the following information: [Location:{3}] - [Time:{4}] - [OnGround:{5}] - [OnBuildingBlock:{6}]"},      
        };
        
        void WriteLogs() =>  Interface.Oxide.DataFileSystem.WriteObject("Cupboard-Logs", logData);    
        
        class LogData{
            public List<string> logs = new List<string>();
        }
        LogData logData;
        
        void OnEntitySpawned(BaseEntity entity, UnityEngine.GameObject gameObject){
            try {
                if(entity.ToString().Contains("cupboard.tool")){
                    if(entity.OwnerID == null) return; 
                    var player = BasePlayer.FindByID(entity.OwnerID);
                    var onground = false;
                    var onbuildingblock = false;
                        List<BaseEntity> nearby = new List<BaseEntity>();
                        Vis.Entities(entity.transform.position, 1, nearby);
                        foreach (var ent in nearby)  
                        {
                            if(ent.ShortPrefabName.Contains("cupboard"))
                            {
                                foreach(var ent1 in nearby)
                                {
                                    if(ent1 is BuildingBlock)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        onground = true;
                                        onbuildingblock = false;   
                                    }
                                }
                            }      
                            if (ent is BuildingBlock)
                            {
                                onground = false;
                                onbuildingblock = true;
                            }
                        }
                            
                    logData.logs.Add(string.Format(lang.GetMessage("Formatting", this), player.displayName, player.UserIDString, entity.transform.position, DateTime.Now, onground, onbuildingblock));
                    WriteLogs();
                }else{
                    return;
                }
            }
            catch(System.Exception)
            {
                return;       
            }
        }
    }
}