using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("ToggleNoggin", "carny666", "1.0.0")]
    class ToggleNoggin : RustPlugin
    {
        private const string adminPermission = "ToggleNoggin.admin";
        static string lastHat = "";

        void Loaded()
        {
            try
            {
                permission.RegisterPermission(adminPermission, this);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in Loaded {ex.Message}");
            }
        }

        [ConsoleCommand("togglenoggin")]
        void ccToggleNoggin(ConsoleSystem.Arg arg)
        {
            try
            {
                if (arg.Player() == null) return;
                BasePlayer player = arg.Player();

                if (!permission.UserHasPermission(player.UserIDString, adminPermission)) return;

                var hat = (arg.Args.Length > 1) ? "hat.miner" : "hat.candle";

                if (player.inventory.containerWear.FindItemsByItemName("hat.miner") == null &&  player.inventory.containerWear.FindItemsByItemName("hat.candle") == null)
                {
                    var hatDef = ItemManager.FindItemDefinition(hat);
                    var fuelDef = ItemManager.FindItemDefinition("lowgradefuel");

                    lastHat = hat;

                    if (hatDef != null && fuelDef != null)
                    {
                        Item hatItem = ItemManager.CreateByItemID(hatDef.itemid, 1);
                        hatItem.contents.AddItem(fuelDef, 140);
                        player.inventory.GiveItem(hatItem, player.inventory.containerWear);
                        hatItem.SetFlag(global::Item.Flag.IsOn, true);                        
                    }
                }
                else
                {
                    var p = player.inventory.containerWear.FindItemsByItemName(lastHat);
                    p.RemoveFromContainer();

                }

            }
            catch (Exception ex)
            {
                throw new Exception($"Error in ccToggleNoggin {ex.Message}");

            }
        }

    }
}

