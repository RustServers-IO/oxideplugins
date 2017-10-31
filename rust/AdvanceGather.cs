using System.Collections.Generic;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdvanceGather", "Hougan", "0.1.1")]
    [Description("Custom gathering with some action's and extension drop")]
    class AdvanceGather : RustPlugin
    {
        #region Variable
        private int appleChance;
        private int rAppleChance;
        private int toolBreak;
        private int berryChance;
        private int berryAmount;
        private int berryStack;
        private string unbreakPerm = "AdvanceGather.Unbreakable";
        private bool enableBroadcast;
        #endregion

        #region Function
        object GetVariable(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
            }
            return value;
        }
        string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion

        #region Hooks
        protected override void LoadDefaultConfig()
        {
            toolBreak = Convert.ToInt32(GetVariable("Main", "Tool some-break chance (0 - to disable)", 2));
            enableBroadcast = Convert.ToBoolean(GetVariable("Main", "Enable broadcast", true));
            rAppleChance = Convert.ToInt32(GetVariable("Tree", "Chance to drop rotten apple (Chance depend on chance)", 5));
            appleChance = Convert.ToInt32(GetVariable("Tree", "Chance to drop any apples (0 - to disable)", 30));
            berryChance = Convert.ToInt32(GetVariable("Hemp", "Chance to get berry from hemp (0 - to disable)", 10));
            berryAmount = Convert.ToInt32(GetVariable("Hemp", "Max berry amount", 2));
            berryStack = Convert.ToInt32(GetVariable("Hemp", "Max berry stack", 5));
            SaveConfig();
        }
        void Init()
        {
            LoadDefaultConfig();
            ItemManager.FindItemDefinition(1611480185).stackable = berryStack;
            permission.RegisterPermission(unbreakPerm, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Apple"] = "<color=#DC143C>Congratulations</color>! You found <color=#DC143C>apple</color>!",
                ["Berry"] = "<color=#DC143C>Congratulations</color>! You found <color=#DC143C>berry</color>!",
                ["ToolBreak"] = "<color=#DC143C>Shame on you</color>! You broke your tool a third!"
            }, this);
        }

        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            if (dispenser.GetComponent<BaseEntity>() is TreeEntity)
            {
                if (Oxide.Core.Random.Range(0, 100) < appleChance)
                {
                    if (Oxide.Core.Random.Range(0, 100) < rAppleChance)
                        ItemManager.CreateByName("apple.spoiled", 1).Drop(new Vector3(entity.transform.position.x, entity.transform.position.y + 20f, entity.transform.position.z), Vector3.zero);
                    else
                        ItemManager.CreateByName("apple", 1).Drop(new Vector3(entity.transform.position.x, entity.transform.position.y + 20f, entity.transform.position.z), Vector3.zero);
                    if (enableBroadcast)
                        SendReply(entity as BasePlayer, String.Format(msg("Apple")));
                }
            }
            if (Oxide.Core.Random.Range(0, 100) < toolBreak && !permission.UserHasPermission((entity as BasePlayer).net.connection.userid.ToString(), unbreakPerm))
            {
                (entity as BasePlayer).GetActiveItem().condition = (entity as BasePlayer).GetActiveItem().condition / 3;
                SendReply(entity as BasePlayer, String.Format(msg("ToolBreak")));
            }
        }
        
        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            if (item.info.displayName.english.Contains("Cloth"))
            {
                if (Oxide.Core.Random.Range(0, 100) < berryChance)
                {
                    player.inventory.GiveItem(ItemManager.CreateByName("black.raspberries", Oxide.Core.Random.Range(1, berryAmount)));
                    if (enableBroadcast)
                        SendReply(player, String.Format(msg("Berry")));
                }
            }
        }
        #endregion
    }
}