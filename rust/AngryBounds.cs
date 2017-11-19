using System;
using System.Collections.Generic;

using Rust;

using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AngryBounds", "Tori1157", "1.0.1")]
    [Description("Prevents players from building outside map bounds.")]

    public class AngryBounds : RustPlugin
    {
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Out Of Bounds"] = "<color=red>You're out of bounds, can't build here!</color>",
            }, this);
        }

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
            var playerX = Convert.ToDecimal(player.transform.position.x);
            var playerZ = Convert.ToDecimal(player.transform.position.z);

            var positionX = (Math.Sign(playerX) == -1 ? Decimal.Negate(playerX) : playerX);
            var positionZ = (Math.Sign(playerZ) == -1 ? Decimal.Negate(playerZ) : playerZ);

            return positionX > worldHalf || positionZ > worldHalf;
        }
    }
}