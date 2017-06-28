using System;
using System.Text;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Bearrels","ichaleynbin","1.1.0", ResourceId = 2380)]
    [Description("Random chance of bears spawning when a barrel breaks.")]
    
    class Bearrels : RustPlugin
    {
        private System.Random random = new System.Random();
        private ConfigDats configData;
        private string Bear = "assets/rust.ai/agents/bear/bear.prefab";
        private Dictionary<string, string> messages = new Dictionary<string,string>();
        
        class ConfigDats
        {
            public int ChanceOfBearrel {get; set;}
        }
        
        void SpawnBear(Vector3 pos)
        {
            
            BaseEntity bear = GameManager.server.CreateEntity(Bear, pos, new Quaternion(), true);            
            bear.Spawn();
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity.name.Contains("barrel") && (random.Next(1,100)<= configData.ChanceOfBearrel))
            {
                SpawnBear(entity.GetEstimatedWorldPosition());
                BasePlayer player = info.Initiator?.ToPlayer();
                if (player != null)
                    PrintToChat(player,lang.GetMessage("Bearrel", this, player.UserIDString));
            }
        }
        
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigDats
            {
                ChanceOfBearrel = 2
            };
            Config.WriteObject(configData,true);
        }
        
        [ChatCommand("bearrels")]
        void SetBearrel(BasePlayer player, string command, string[] args)
        {
            if ((player.net.connection.authLevel >=1) && (args.Count() ==1) )
            {
                int chance;
                try
                {
                    chance = Convert.ToInt32(args[0]);
                }
                catch
                {
                    PrintToChat(player,lang.GetMessage("FailChange",this, player.UserIDString));
                    return;
                }
                if (chance >100)
                    chance = 100;
                else if (chance < 0)
                    chance = 0;
                configData.ChanceOfBearrel = chance;
                PrintToChat(string.Format(lang.GetMessage("Changing",this),chance));
            }
        }
        
        void Init()
        {
            try
            {
                configData = Config.ReadObject<ConfigDats>();
            }
            catch
            {
                LoadDefaultConfig();
            }
            messages["Changing"] = "Changing chance of Bearrels to {0}%";
            messages["FailChange"] = "Invalid integer chance format: chance unchanged.";
            messages["Bearrel"] = "That wasn't just a barrel- it was a Bearrel!";
            lang.RegisterMessages(messages,this);        }
        
        void OnServerSave()
        {
            Config.WriteObject(configData);
        }
        
    }
}
