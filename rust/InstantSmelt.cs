
using System.Collections.Generic;
using Rust;
using System;

namespace Oxide.Plugins
{
    [Info("Instant Smelt", "Serenity 3", "1.1.5", ResourceId = 2352)]
    class InstantSmelt : RustPlugin
    {
        #region Constants
        // Constant values (Non Variable Values) This is to ensure that nothing changes in the process of making these items. 
        const int Osulfur = 889398893;
        const int Ohqm = 2133577942;
        const int Omf = -1059362949;
        const int Coil = 1983936587;
        const int ETuna = 1050986417;
        const int EBean = 2080339268;
        const int Oil = 28178745;
        const int Sulfur = -891243783;
        const int Hqm = 374890416;
        const int Mf = 688032252;
        const int Wood = 3655341;
        const int Charcoal = 1436001773;

        #endregion

        #region Permissions

        void Init()
        {
            permission.RegisterPermission("instantsmelt.use", this);
        }


        #endregion

        #region Config

        public bool SmeltCans => Config.Get<bool>("SmeltTunaCans");


        protected override void LoadDefaultConfig()
        {
            Config["SmeltTunaCans"] = false;
            SaveConfig();

        }
        #endregion

        #region Auto Smelt
        // Called When something enters a container
        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container.playerOwner == null) return;
            if (!permission.UserHasPermission(container.playerOwner.UserIDString, "instantsmelt.use")) return;




            // id is actuall integer value of the id instead of the name.
            int id = item.info.itemid;
            // gets the ammount of the item
            int ammount = item.amount;
            // Gets the position of the item in the inventory
            int pos = item.position;
            // Switch Statement for the Id of the Item
            switch (id)
            {
                // Checks for the object
                case Coil:
                    
                    item.RemoveFromContainer();
                    Item C = ItemManager.CreateByItemID(Oil, ammount * 3);
                    C.MoveToContainer(container);
                    break;

                case ETuna:
                    if (SmeltCans == false) break;
                    item.RemoveFromContainer();
                    Item ET = ItemManager.CreateByItemID(Mf, ammount * 10);
                    ET.MoveToContainer(container);
                    break;

                case EBean:
                    item.RemoveFromContainer();
                    Item EB = ItemManager.CreateByItemID(Mf, ammount * 15);
                    EB.MoveToContainer(container);
                    break;

                case Osulfur:
                    // Just removing stuff from the continer then creating the Sulfur to add back in.
                    item.RemoveFromContainer();
                    Item CS = ItemManager.CreateByItemID(Sulfur, ammount);
                    CS.MoveToContainer(container);
                    break;

                case Ohqm:
                    // replaces Hqm ore with HQM
                    item.RemoveFromContainer();
                    Item CH = ItemManager.CreateByItemID(Hqm, ammount);
                    CH.MoveToContainer(container);
                    break;

                case Omf:
                    //replaces Metal Ore with metal Fragments.
                    item.RemoveFromContainer();
                    Item CM = ItemManager.CreateByItemID(Mf, ammount);
                    CM.MoveToContainer(container);
                    break;
                // Just saying if it doesnt have anyone of these items it ignores it.
                default:
                    break;

            }
        }
        #endregion

    }


}
