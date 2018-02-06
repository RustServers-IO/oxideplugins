using System;
using System.Collections.Generic;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AngryBounds", "Tori1157", "1.1.0")]
    [Description("Prevents players from building outside of map bounds.")]

    public class AngryBounds : RustPlugin
    {
        #region Fields

        private bool Changed;

        private decimal boundChange;

        #endregion

        #region Loading
        
        private void Init()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            boundChange = Convert.ToDecimal(GetConfig("Options", "Boundary Adjust Size", 0));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new config file");
            Config.Clear();
            LoadVariables();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Out Of Bounds"] = "<color=red>You're out of bounds, can't build here!</color>",
            }, this);
        }

        #endregion

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            var entity = go.GetComponent<BuildingBlock>();

            if (player != null && CheckPlayerPosition(player))
            {
                NextTick(() =>
                {
                    entity.Kill();
                    player.ChatMessage(lang.GetMessage("Out Of Bounds", this, player.UserIDString));
                });
                foreach (var refundItem in entity.BuildCost()) // Credit to Ryan for this code.
                    player.GiveItem(ItemManager.CreateByItemID(refundItem.itemDef.itemid, (int)refundItem.amount));
            }
        }

        private bool CheckPlayerPosition(BasePlayer player)
        {
            var worldHalf = World.Size / 2;
            var worldAdded = (Math.Sign(boundChange) == -1 ? worldHalf - Decimal.Negate(boundChange) : worldHalf + boundChange);

            var playerX = Convert.ToDecimal(player.transform.position.x);
            var playerZ = Convert.ToDecimal(player.transform.position.z);

            var positionX = (Math.Sign(playerX) == -1 ? Decimal.Negate(playerX) : playerX);
            var positionZ = (Math.Sign(playerZ) == -1 ? Decimal.Negate(playerZ) : playerZ);

            if (Math.Sign(boundChange) == 0)
            {
                return positionX > worldHalf || positionZ > worldHalf;
            }
            else
            {
                return positionX > worldAdded || positionZ > worldAdded;
            }
        }

        #region Helpers

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;

            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }

            object value;

            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        #endregion
    }
}