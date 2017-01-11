using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Game.HideHoldOut.Libraries;
using System.Globalization;


namespace Oxide.Plugins
{
    [Info("Give", "k1lly0u", "0.1.1", ResourceId = 1869)]
    public class Give : HideHoldOutPlugin
    {

        private readonly Command cmdlib = Interface.Oxide.GetLibrary<Command>();
        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        ServerManager sm = GameObject.Find("ServerManager").GetComponent<ServerManager>();
        NetworkController nc = UnityEngine.Object.FindObjectOfType<NetworkController>();
        ItemLibrary il = UnityEngine.Object.FindObjectOfType<ItemLibrary>();

        Dictionary<int, ITEM> Items = new Dictionary<int, ITEM>();
        Dictionary<vehicle_DBID, VEHICLE_BLUEPRINT> Vehicles = new Dictionary<vehicle_DBID, VEHICLE_BLUEPRINT>();

        void Loaded()
        {
            permission.RegisterPermission("give.use", this);
            cmdlib.AddChatCommand("give", this, "cmdGive");
            cmdlib.AddChatCommand("giveme", this, "cmdGiveMe");
            lang.RegisterMessages(new Dictionary<string, string>
        {
            { "giveItemSyn", "<color=#C4FF00>/give item <playername> <itemname/id> <amount></color> - Give <playername> a item, using ID# or partial name" },
            { "giveVehicleSyn", "<color=#C4FF00>/give vehicle <playername> <vehiclename/id></color> - Give <playername> a vehicle, using ID# or partial name" },
            { "noPlayers", "Could not find any players with the name <color=#C4FF00>{0}</color>" },
            { "multiPlayers", "Multiple players found with the name <color=#C4FF00>{0}</color>" },
            { "givenItem", "You have given <color=#C4FF00>{0} {1}x {2}</color>" },
            { "receivedItem", "You have received <color=#C4FF00>{0}x {1}</color>" },
            { "givenVehicle", "You have given <color=#C4FF00>{0} a {1}</color>" },
            { "receivedVehicle", "You have received a <color=#C4FF00>{0}</color>" },
            { "incSyn", "<color=#C4FF00>Incorrect Syntax!</color>" },
            { "title", "<color=#C4FF00>Give:</color> " },
            { "noPerms", "You do not have permission to use this command!" },
            { "givemeItem", "<color=#C4FF00>/giveme item <itemname/id> <amount></color> - Give youself a item, using ID# or partial name" },
            { "givemeVehicle", "<color=#C4FF00>/giveme vehicle <vehiclename/id></color> - Give youself a vehicle, using ID# or partial name" },
                {"noFind", "Unable to find the item/vehicle you were searching for" }

        }, this);
        }
        void OnServerInitialized()
        {
            Items = il.ITEM_Dictionary;
            Vehicles = il.VEHICLE_BLUEPRINT_Dictionary;
        }        
        private void SendReply(PlayerInfos player, string msg)
        {
            if (player != null && player.connected)
                ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });
        }
        private List<PlayerInfos> GetOnlinePlayers()
        {
            List<PlayerInfos> players = new List<PlayerInfos>();
            foreach (var entry in sm.Connected_Players)
                if (entry != null)
                    if (entry.connected)
                        players.Add(entry);
            return players;
        }
        private List<PlayerInfos> FindPlayers(string name)
        {
            var playerList = GetOnlinePlayers();
            List<PlayerInfos> foundPlayers = new List<PlayerInfos>();
            foreach (var player in playerList)
            {
                if (player.Nickname.ToLower().Contains(name.ToLower()))
                    foundPlayers.Add(player);
                else if (player.account_id == name)
                {
                    foundPlayers.Add(player);
                    break;
                }
            }
            return foundPlayers;
        }
        [ChatCommand("give")]
        private void cmdGive(PlayerInfos player, string command, string[] args)
        {
            if (HasPerm(player))
            {
                if (args == null || args.Length == 0)
                {
                    SendReply(player, lang.GetMessage("giveItemSyn", this, player.account_id));
                    SendReply(player, lang.GetMessage("giveVehicleSyn", this, player.account_id));
                    return;
                }
                if (args.Length >= 3)
                {
                    var receiver = FindPlayers(args[1]);
                    if (receiver.Count == 0)
                    {
                        SendReply(player, string.Format(lang.GetMessage("noPlayers", this, player.account_id), args[1]));
                        return;
                    }
                    if (receiver.Count > 1)
                    {
                        SendReply(player, string.Format(lang.GetMessage("multiPlayers", this, player.account_id), args[1]));
                        return;
                    }
                    switch (args[0].ToLower())
                    {

                        case "item":
                            if (args.Length == 4)
                            {
                                int itemID;
                                int itemAmount;
                                if (!int.TryParse(args[2], out itemID))
                                {
                                    foreach (var item in Items)
                                        if (item.Value.name.Contains(args[1]))
                                        {
                                            itemID = item.Key;
                                            break;
                                        }
                                }
                                if (itemID == 0)
                                {
                                    SendReply(player, lang.GetMessage("noFind", this, player.account_id));
                                    return;
                                }
                                if (int.TryParse(args[3], out itemAmount))
                                {
                                    nc.NetView.RPC("NET_ADMIN_ReceiveItem", receiver[0].NetPlayer, new object[] { itemID, itemAmount });
                                    SendReply(player, string.Format(lang.GetMessage("givenItem", this, player.account_id), receiver[0].Nickname, itemAmount, Items[itemID]));
                                    SendReply(receiver[0], string.Format(lang.GetMessage("receivedItem", this, player.account_id), itemAmount, Items[itemID]));
                                }
                            }
                            return;
                        case "vehicle":
                            int vehicleID;
                            if (!int.TryParse(args[2], out vehicleID))
                            {
                                foreach (var item in Vehicles)
                                    if (item.Value.name.Contains(args[2]))
                                    {
                                        vehicleID = (int)item.Key;
                                        break;
                                    }
                            }
                            if (vehicleID == 0)
                            {
                                SendReply(player, lang.GetMessage("noFind", this, player.account_id));
                                return;
                            }
                            GiveVehicle((vehicle_DBID)vehicleID, receiver[0]);
                            string vehiclename = Vehicles[(vehicle_DBID)vehicleID].name;
                            SendReply(player, string.Format(lang.GetMessage("givenVehicle", this, player.account_id), receiver[0].Nickname, vehiclename));
                            SendReply(player, string.Format(lang.GetMessage("receivedVehicle", this, player.account_id), vehiclename));
                            return;                        
                    }
                }
                SendReply(player, lang.GetMessage("incSyn", this, player.account_id));
            }
        }
        void GiveVehicle(vehicle_DBID num, PlayerInfos playerInfo)
        {
            {
                if (playerInfo.isDefined && playerInfo.connected)
                {
                    Vector3 vector3 = new Vector3(0f, 10f, 0f);
                    if (!playerInfo.connected)
                    {
                        vector3 = new Vector3(playerInfo.DB_pos.x, playerInfo.DB_pos.y + 10f, playerInfo.DB_pos.z);
                    }
                    else
                    {
                        float transfo = playerInfo.Transfo.position.x;
                        Vector3 transfo1 = playerInfo.Transfo.position;
                        Vector3 vector31 = playerInfo.Transfo.position;
                        vector3 = new Vector3(transfo, transfo1.y + 10f, vector31.z);
                    }
                    VEHICLE_BLUEPRINT item = Vehicles[num];

                    Transform transforms = uLink.Network.Instantiate<Transform>(item.Vehicle_prefab, vector3, Quaternion.identity, 0, new object[0]);
                    Vehicle component = transforms.GetComponent<Vehicle>();
                    if (component == null)
                    {
                        SHIP sHIP = transforms.GetComponent<SHIP>();
                        if (sHIP != null)
                        {
                            string str = DateTime.Now.ToString("yyyyMMddHHmmssf", CultureInfo.InvariantCulture);
                            sHIP.GetSet_DataBase_ID = str;
                            string empty = string.Empty;
                            if (sHIP.Linked_Turrets != null && (int)sHIP.Linked_Turrets.Length > 0)
                            {
                                for (int i = 0; i < (int)sHIP.Linked_Turrets.Length; i++)
                                {
                                    string str1 = string.Concat(str, i.ToString());
                                    nc.Add_Turret_To_DB(str1, sHIP.Linked_Turrets[i]);
                                    empty = (i != 0 ? string.Concat(empty, "_", str1) : str1);
                                }
                            }
                            sHIP.FinalizeProduction();
                            nc.Add_Vehicle_to_DB(str, sHIP.DBID, vector3, Quaternion.identity, sHIP.FUEL, sHIP.HEALTH, empty);
                            sHIP.Start_DB_Record();
                        }
                    }
                    else
                    {
                        string str2 = DateTime.Now.ToString("yyyyMMddHHmmssf", CultureInfo.InvariantCulture);
                        component.GetSet_DataBase_ID = str2;
                        string empty1 = string.Empty;
                        if (component.Linked_Turrets != null && (int)component.Linked_Turrets.Length > 0)
                        {
                            for (int j = 0; j < (int)component.Linked_Turrets.Length; j++)
                            {
                                string str3 = string.Concat(str2, j.ToString(CultureInfo.InvariantCulture));
                                nc.Add_Turret_To_DB(str3, component.Linked_Turrets[j]);
                                empty1 = (j != 0 ? string.Concat(empty1, "_", str3) : str3);
                            }
                        }
                        nc.Add_Vehicle_to_DB(str2, component.DBID, vector3, Quaternion.identity, component.FUEL, component.HEALTH, empty1);
                        component.Start_DB_Record();
                    }
                }
            }
        }
        [ChatCommand("giveme")]
        private void cmdGiveMe(PlayerInfos player, string command, string[] args)
        {
            if (HasPerm(player))
            {
                if (args == null || args.Length == 0)
                {
                    SendReply(player, lang.GetMessage("givemeItem",this, player.account_id));
                    SendReply(player, lang.GetMessage("givemeVehicle", this, player.account_id));
                    return;
                }
                if (args.Length >= 2)
                {
                    switch (args[0].ToLower())
                    {
                        case "item":
                            if (args.Length == 3)
                            {
                                int itemID;
                                int itemAmount;
                                if (!int.TryParse(args[1], out itemID))
                                {
                                    foreach (var item in Items)
                                        if (item.Value.name.Contains(args[1]))
                                        {
                                            itemID = item.Key;
                                            break;
                                        }
                                }
                                if (itemID == 0)
                                {
                                    SendReply(player, lang.GetMessage("noFind", this, player.account_id));
                                    return;
                                }
                                if (int.TryParse(args[2], out itemAmount))
                                {
                                    nc.NetView.RPC("NET_ADMIN_ReceiveItem", player.NetPlayer, new object[] { itemID, itemAmount });
                                }
                            }
                            return;
                        case "vehicle":
                            int vehicleID;
                            if (!int.TryParse(args[1], out vehicleID))
                            {
                                foreach (var item in Vehicles)
                                    if (item.Value.name.Contains(args[1]))
                                    {
                                        vehicleID = (int)item.Key;
                                        break;
                                    }
                            }
                            if (vehicleID == 0)
                            {
                                SendReply(player, lang.GetMessage("noFind", this, player.account_id));
                                return;
                            }
                            GiveVehicle((vehicle_DBID)vehicleID, player);
                            return;                                               
                    }
                }
                SendReply(player, lang.GetMessage("incSyn", this, player.account_id));
            }
        }
        private bool HasPerm(PlayerInfos player)
        {
            if (permission.UserHasPermission(player.account_id.ToString(), "give.use")) return true;
            SendReply(player, lang.GetMessage("title", this, player.account_id.ToString()) + lang.GetMessage("noPerms", this, player.account_id.ToString()));
            return false;
        }
    }
}
