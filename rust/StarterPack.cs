using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StarterPack", "mangiang", 1.10, ResourceId = 2461)]
    [Description("Gives some basic items on connection")]
    class StarterPack : RustPlugin
    {
        /// <summary>
        ///  List of Player ids
        /// </summary>
        List<string> ids = new List<string>();

        /// <summary>
        /// Items given on connection
        /// </summary>
        Dictionary<string, Dictionary<string, object>> items = new Dictionary<string, Dictionary<string, object>>();

        /// <summary>
        /// Gives items only on first connection 
        /// </summary>
        bool OnlyFirstConnection;

        /// <summary>
        /// Message on connection
        /// </summary>
        string ConnectionMessage;

        /// <summary>
        /// Default message
        /// </summary>
        string DefaultConnectionMessage = "A cup of coffee has been poured";

        /// <summary>
        /// Chat parameters
        /// </summary>
        Dictionary<string, string> ChatParameters;
        /// <summary>
        /// Plugin Name parameters
        /// </summary>
        Dictionary<string, string> PluginNameParameters;

        /// <summary>
        /// If the item to EVERYONE when a new player connects ?
        /// </summary>
        bool ItemToEveryOne;

        /// <summary>
        /// Is the message broadcasted ?
        /// </summary>
        bool BroadcastedMessage;

        /// <summary>
        /// Save ids across reboots
        /// </summary>
        List<string> save_ids;

        /// <summary>
        /// Chat name : how is the plugin identified
        /// </summary>
        string ChatName;

        /// <summary>
        /// Log verbose
        /// </summary>
        bool LogVerbose;

        /// <summary>
        /// Verbose mode
        /// </summary>
        bool VerboseMode;

        protected override void LoadDefaultConfig() => Puts("New configuration file created.");
        private void Init() => LoadConfigValues();

        void LoadConfigValues()
        {
            string str;

            #region Loging and verbose
            str = Config["Use logs"] as string;
            if (str == null)
            {
                LogVerbose = false;
                Config["Use logs"] = LogVerbose.ToString();
            }
            else
            {
                LogVerbose = (str.Equals("True") ? true : false);
                if (!LogVerbose)
                {
                    LogVerbose = (str.Equals("true") ? true : false);
                }
            }
            if (LogVerbose)
            {
                Puts("Logs enabled");
                Log("Initialization");
                Log("Logs enabled");
            }

            str = Config["Verbose mode"] as string;
            if (str == null)
            {
                VerboseMode = false;
                Config["Verbose mode"] = VerboseMode.ToString();
            }
            else
            {
                VerboseMode = (str.Equals("True") ? true : false);
                if (!VerboseMode)
                {
                    VerboseMode = (str.Equals("true") ? true : false);
                }
            }
            if (VerboseMode)
            {
                Puts("Verbose mode enabled");
            }
            if (LogVerbose)
            {
                Log("Verbose mode enabled");
            }
            #endregion

            save_ids = Interface.Oxide.DataFileSystem.ReadObject<List<string>>("StarterPack");
            if (LogVerbose)
            {
                string id = "save_ids :\r\n";
                foreach (string s in save_ids)
                {
                    id += "- " + s + "\r\n";
                }
                Log(id);
            }

            #region Items Initialisation
            items.Add("Main Inventory", GetConfigValue("Items", "Main Inventory", new Dictionary<string, object> { { "991728250", "1" } }));
            items.Add("Wear Inventory", GetConfigValue("Items", "Wear Inventory", new Dictionary<string, object>()));
            items.Add("Belt Inventory", GetConfigValue("Items", "Belt Inventory", new Dictionary<string, object>()));
            if (LogVerbose)
            {
                string log = "";
                Dictionary<string, object> dic = items["Main Inventory"];
                var enu = dic.GetEnumerator();
                log += "Main Inventory :\r\n";
                while (enu.MoveNext())
                {
                    log += "- " + enu.Current.Key + " : " + enu.Current.Value + "\r\n";
                }

                dic = items["Wear Inventory"];
                enu = dic.GetEnumerator();
                log += "Wear Inventory :\r\n";
                while (enu.MoveNext())
                {
                    log += "- " + enu.Current.Key + " : " + enu.Current.Value + "\r\n";
                }

                dic = items["Belt Inventory"];
                enu = dic.GetEnumerator();
                log += "Belt Inventory :\r\n";
                while (enu.MoveNext())
                {
                    log += "- " + enu.Current.Key + " : " + enu.Current.Value + "\r\n";
                }

                Log(log);
            }
            #endregion

            #region Chat name
            ChatName = Config["Chat name"] as string;
            if (ChatName == null)
            {
                ChatName = this.Name;
                Config["Chat name"] = ChatName;
            }
            if (LogVerbose)
            {
                Log("Chat name : " + ChatName);
            }
            #endregion

            #region Message Initialisation
            ConnectionMessage = Config["Connection Message"] as string;
            if (ConnectionMessage == null)
            {
                ConnectionMessage = DefaultConnectionMessage;
                Config["Connection Message"] = ConnectionMessage;
            }
            if (LogVerbose)
            {
                Log("Connection Message : " + ConnectionMessage);
            }
            #endregion

            #region If the item to EVERYONE when a new player connects ?
            str = Config["Give item to everyone"] as string;
            if (str == null)
            {
                ItemToEveryOne = false;
                Config["Give item to everyone"] = ItemToEveryOne.ToString();
            }
            else
            {
                ItemToEveryOne = (str.Equals("True") ? true : false);
                if (!ItemToEveryOne)
                {
                    ItemToEveryOne = (str.Equals("true") ? true : false);
                }
            }
            if (LogVerbose)
            {
                Log("Give item to everyone : " + ItemToEveryOne);
            }
            #endregion

            #region Is the message broadcasted ?
            str = Config["Is the message broadcasted ?"] as string;
            if (str == null)
            {
                BroadcastedMessage = false;
                Config["Is the message broadcasted ?"] = BroadcastedMessage.ToString();
            }
            else
            {
                BroadcastedMessage = (str.Equals("True") ? true : false);
                if (!ItemToEveryOne)
                {
                    BroadcastedMessage = (str.Equals("true") ? true : false);
                }
            }

            if (LogVerbose)
            {
                Log("Is the message broadcasted ? : " + BroadcastedMessage);
            }
            #endregion

            #region Only on first connection Initialisation
            str = Config["Only on first connection"] as string;
            if (str == null)
            {
                OnlyFirstConnection = false;
                Config["Only on first connection"] = OnlyFirstConnection.ToString();
            }
            else
            {
                OnlyFirstConnection = (str.Equals("True") ? true : false);
                if (!OnlyFirstConnection)
                {
                    OnlyFirstConnection = (str.Equals("true") ? true : false);
                }
            }
            if (LogVerbose)
            {
                Log("Only on first connection : " + OnlyFirstConnection);
            }
            #endregion

            #region ChatParameters Initialisation 
            Dictionary<string, object> TmpChatParameters = Config["Chat Parameters"] as Dictionary<string, object>;
            if (TmpChatParameters == null)
            {
                ChatParameters = new Dictionary<string, string>();
                ChatParameters.Add("size", "0");
                ChatParameters.Add("color", "white");
                Config["Chat Parameters"] = ChatParameters;
            }
            else
            {
                var enu = TmpChatParameters.GetEnumerator();
                ChatParameters = new Dictionary<string, string>();
                while (enu.MoveNext())
                {
                    ChatParameters.Add(enu.Current.Key, (string)enu.Current.Value);
                }
            }
            if (LogVerbose)
            {
                string log = "";
                var enu = ChatParameters.GetEnumerator();
                log += "Chat parameters :\r\n";
                while (enu.MoveNext())
                {
                    log += enu.Current.Key + " : " + enu.Current.Value + "\r\n";
                }
                Log(log);
            }
            #endregion

            #region PluginNameParameters Initialisation
            Dictionary<string, object> TmpPluginNameParameters = Config["Plugin Name Parameters"] as Dictionary<string, object>;
            if (TmpPluginNameParameters == null)
            {
                PluginNameParameters = new Dictionary<string, string>();
                PluginNameParameters.Add("size", "0");
                PluginNameParameters.Add("color", "orange");
                Config["Plugin Name Parameters"] = PluginNameParameters;
            }
            else
            {
                var enu = TmpPluginNameParameters.GetEnumerator();
                PluginNameParameters = new Dictionary<string, string>();
                while (enu.MoveNext())
                {
                    PluginNameParameters.Add(enu.Current.Key, (string)enu.Current.Value);
                }
            }
            if (LogVerbose)
            {
                string log = "";
                var enu = PluginNameParameters.GetEnumerator();
                log += "Plugin Name Parameters :\r\n";
                while (enu.MoveNext())
                {
                    log += enu.Current.Key + " : " + enu.Current.Value + "\r\n";
                }
                Log(log);
            }
            #endregion


            Log("Initialization finished");

            SaveConfig();
        }

        /// <summary>
        /// Called when a player connects
        /// </summary>
        /// <param name="packet"></param>
        void OnPlayerConnected(Network.Message packet)
        {
            // Add the player to the list
            if (!ids.Exists(x => x.Equals(packet.connection.userid.ToString())))
            {
                ids.Add(packet.connection.userid.ToString());

                if (VerboseMode)
                    Puts("OnPlayerConnected => " + packet.connection.userid.ToString() + " added to ids list");

                if (LogVerbose)
                    Log("OnPlayerConnected => " + packet.connection.userid.ToString() + " added to ids list");
            }
        }

        /// <summary>
        /// Called when a player wakes up
        /// </summary>
        /// <param name="player"></param>
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (ids.Exists(x => x.Equals(player.UserIDString)))
            {
                // If items should be given at every connection
                if (!OnlyFirstConnection)
                {
                    GiveToPlayer(player);
                    TellPlayer(player);

                    // If 1rst connection
                    if (!save_ids.Exists(x => x.Equals(player.UserIDString)))
                    {
                        save_ids.Add(player.UserIDString);
                        Interface.Oxide.DataFileSystem.WriteObject("StarterPack", save_ids);
                        if (VerboseMode)
                            Puts("OnPlayerSleepEnded => Save first connection " + player.UserIDString);

                        if (LogVerbose)
                            Log("OnPlayerSleepEnded : !OnlyFirstConnection => Save first connection " + player.UserIDString);
                    }
                }
                // If items should be given ONLY at the first connection
                else if (!save_ids.Exists(x => x.Equals(player.UserIDString)))
                {
                    GiveToPlayer(player);
                    TellPlayer(player);

                    // Save the 1rst connection
                    save_ids.Add(player.UserIDString);
                    Interface.Oxide.DataFileSystem.WriteObject("StarterPack", save_ids);
                    if (VerboseMode)
                        Puts("OnPlayerSleepEnded => Save first connection " + player.UserIDString);

                    if (LogVerbose)
                        Log("OnPlayerSleepEnded : !OnlyFirstConnection  && !save_ids.Exists(x => x.Equals(player.UserIDString)) => Save first connection " + player.UserIDString);
                }

                // Remove from connection list
                ids.Remove(player.UserIDString);

                if (VerboseMode)
                    Puts("OnPlayerSleepEnded => " + player.UserIDString + " removed from ids list");

                if (LogVerbose)
                    Log("OnPlayerSleepEnded => " + player.UserIDString + " removed from ids list");
            }
        }

        /// <summary>
        /// Gives the items to the player(s)
        /// </summary>
        /// <param name="player">The new player</param>
        void GiveToPlayer(BasePlayer player)
        {
            if (LogVerbose)
            {
                Log("Enter GiveToPlayer");
            }
            List<BasePlayer> tmpPlayer = new List<BasePlayer>(ItemToEveryOne ? Player.Players : new List<BasePlayer>(new BasePlayer[] { player }));
            for (int i = 0; i < tmpPlayer.Count; ++i)
            {
                GivePlayer("Main Inventory", items["Main Inventory"], tmpPlayer[i]);
                GivePlayer("Wear Inventory", items["Wear Inventory"], tmpPlayer[i]);
                GivePlayer("Belt Inventory", items["Belt Inventory"], tmpPlayer[i]);
            }
            if (LogVerbose)
            {
                Log("Exit GiveToPlayer");
            }
        }

        /// <summary>
        /// Tells the message to the player(s)
        /// </summary>
        /// <param name="player">The new player</param>
        void TellPlayer(BasePlayer player)
        {
            if (LogVerbose)
            {
                Log("Enter TellPlayer");
            }
            List<BasePlayer> tmpPlayer = new List<BasePlayer>(BroadcastedMessage ? Player.Players : new List<BasePlayer>(new BasePlayer[] { player }));
            foreach (BasePlayer pl in tmpPlayer)
            {
                Say(pl, ConnectionMessage, player);
            }
            if (LogVerbose)
            {
                Log("Exit TellPlayer");
            }
        }

        #region helper functions
        /// <summary>
        /// Gives items to the player
        /// </summary>
        /// <param name="inventoryName">The name of the inventory the items are added to</param>
        /// <param name="inventory">The Items</param>
        /// <param name="player">The player</param>
        /// <returns></returns>
        ItemContainer GivePlayer(string inventoryName, Dictionary<string, object> inventory, BasePlayer player)
        {
            if (LogVerbose)
            {
                Log("Enter GivePlayer");
            }
            // For all items in dictionary
            if (inventoryName.Equals("Main Inventory"))
            {
                Log("Main Inventory");
                var enu = inventory.GetEnumerator();
                while (enu.MoveNext())
                {
                    Log("Begin item : " + enu.Current.Key + " => " + enu.Current.Value);

                    Log("Itemid Dictionary : " + (enu.Current.Key));
                    int itemid = int.Parse(enu.Current.Key);
                    Log("Itemid : " + itemid);

                    int itemAmount = 0;

                    if (enu.Current.Value is string)
                    {
                        Log("ItemAmount Dictionary : " + (enu.Current.Value as string));
                        itemAmount = int.Parse(enu.Current.Value as string);
                        Log("ItemAmount : " + itemAmount);
                    }
                    else if (enu.Current.Value is int)
                    {
                        Log("ItemAmount Dictionary : " + (int)enu.Current.Value);
                        itemAmount = (int)enu.Current.Value;
                        Log("ItemAmount : " + itemAmount);
                    }
                    else
                    {
                        Log("Wrong value for item " + enu.Current.Key);
                    }

                    ItemDefinition def = ItemManager.CreateByItemID(itemid, 1).info;
                    player.inventory.containerMain.AddItem(def, itemAmount);


                    if (LogVerbose)
                    {
                        Log("Gave Main inventory : " + player.displayName + " (" + player.UserIDString + ") : " + player.inventory.containerMain.FindItemByItemID(itemid).amount + " " + def.displayName.english);
                    }
                    if (VerboseMode)
                    {
                        Puts("Gave Main inventory : " + player.displayName + " (" + player.UserIDString + ") : " + player.inventory.containerMain.FindItemByItemID(itemid).amount + " " + def.displayName.english);
                    }
                }

                if (LogVerbose)
                {
                    Log("Exit GivePlayer");
                }
                return player.inventory.containerMain;
            }
            else if (inventoryName.Equals("Wear Inventory"))
            {
                Log("Wear Inventory");
                var enu = inventory.GetEnumerator();
                while (enu.MoveNext())
                {
                    Log("Begin item : " + enu.Current.Key + " => " + enu.Current.Value);

                    Log("Itemid Dictionary : " + (enu.Current.Key));
                    int itemid = int.Parse(enu.Current.Key);
                    Log("Itemid : " + itemid);

                    int itemAmount = 0;

                    if (enu.Current.Value is string)
                    {
                        Log("ItemAmount Dictionary : " + (enu.Current.Value as string));
                        itemAmount = int.Parse(enu.Current.Value as string);
                        Log("ItemAmount : " + itemAmount);
                    }
                    else if (enu.Current.Value is int)
                    {
                        Log("ItemAmount Dictionary : " + (int)enu.Current.Value);
                        itemAmount = (int)enu.Current.Value;
                        Log("ItemAmount : " + itemAmount);
                    }
                    else
                    {
                        Log("Wrong value for item " + enu.Current.Key);
                    }

                    ItemDefinition def = ItemManager.CreateByItemID(itemid, 1).info;
                    player.inventory.containerWear.AddItem(def, itemAmount);

                    if (LogVerbose)
                    {
                        Log("Give Wear inventory : " + player.displayName + " (" + player.UserIDString + ") : " + player.inventory.containerWear.FindItemByItemID(itemid).amount + " " + def.displayName.english);
                    }
                    if (VerboseMode)
                    {
                        Puts("Give Wear inventory : " + player.displayName + " (" + player.UserIDString + ") : " + player.inventory.containerWear.FindItemByItemID(itemid).amount + " " + def.displayName.english);
                    }

                }

                if (LogVerbose)
                {
                    Log("Exit GivePlayer");
                }
                return player.inventory.containerWear;
            }
            else
            {
                Log("Belt Inventory");
                var enu = inventory.GetEnumerator();
                while (enu.MoveNext())
                {
                    Log("Begin item : " + enu.Current.Key + " => " + enu.Current.Value);

                    Log("Itemid Dictionary : " + (enu.Current.Key));
                    int itemid = int.Parse(enu.Current.Key);
                    Log("Itemid : " + itemid);

                    int itemAmount = 0;

                    if (enu.Current.Value is string)
                    {
                        Log("ItemAmount Dictionary : " + (enu.Current.Value as string));
                        itemAmount = int.Parse(enu.Current.Value as string);
                        Log("ItemAmount : " + itemAmount);
                    }
                    else if (enu.Current.Value is int)
                    {
                        Log("ItemAmount Dictionary : " + (int)enu.Current.Value);
                        itemAmount = (int)enu.Current.Value;
                        Log("ItemAmount : " + itemAmount);
                    }
                    else
                    {
                        Log("Wrong value for item " + enu.Current.Key);
                    }

                    ItemDefinition def = ItemManager.CreateByItemID(itemid, 1).info;
                    player.inventory.containerBelt.AddItem(def, itemAmount);

                    if (LogVerbose)
                    {
                        Log("Give Belt inventory : " + player.displayName + " (" + player.UserIDString + ") : " + player.inventory.containerBelt.FindItemByItemID(itemid).amount + " " + def.displayName.english);
                    }
                    if (VerboseMode)
                    {
                        Puts("Give Belt inventory : " + player.displayName + " (" + player.UserIDString + ") : " + player.inventory.containerBelt.FindItemByItemID(itemid).amount + " " + def.displayName.english);
                    }

                }

                if (LogVerbose)
                {
                    Log("Exit GivePlayer");
                }
                return player.inventory.containerBelt;
            }

        }

        /// <summary>
        /// Display message
        /// </summary>
        /// <param name="player">The player the message is displayed to</param>
        /// <param name="str">The message</param>
        void Say(BasePlayer player, string str, BasePlayer newComer)
        {
            if (LogVerbose)
                Log("Enter Say");

            StringBuilder sb = new StringBuilder();

            #region Plugin name
            if (!PluginNameParameters["size"].Equals("0"))
            {
                sb.Append("<size=" + PluginNameParameters["size"] + ">");
            }
            if (LogVerbose)
                Log("Open size ok");

            if (!PluginNameParameters["color"].Equals("white"))
            {
                sb.Append("<color=" + PluginNameParameters["color"] + ">");
            }
            if (LogVerbose)
                Log("Open color ok");

            sb.Append("[").Append(ChatName).Append("] ");
            if (LogVerbose)
                Log("ChatName ok ");

            if (!PluginNameParameters["color"].Equals("white"))
            {
                sb.Append("</color>");
            }
            if (LogVerbose)
                Log("Close color ok");

            if (!PluginNameParameters["size"].Equals("0"))
            {
                sb.Append("</size>");
            }
            if (LogVerbose)
                Log("Close size ok");
            #endregion

            #region Message
            if (!ChatParameters["size"].Equals("0"))
            {
                sb.Append("<size=" + ChatParameters["size"] + ">");
            }
            if (LogVerbose)
                Log("Open size Say");

            if (!ChatParameters["color"].Equals("white"))
            {
                sb.Append("<color=" + ChatParameters["color"] + ">");
            }
            if (LogVerbose)
                Log("Open color ok");

            ReplaceOptions(str, player, newComer);
            if (LogVerbose)
                Log("Replace Options ok");
            sb.Append(str);
            if (LogVerbose)
                Log("Append message ok");

            if (!ChatParameters["color"].Equals("white"))
            {
                sb.Append("</color>");
            }
            if (LogVerbose)
                Log("Close color ok");

            if (!ChatParameters["size"].Equals("0"))
            {
                sb.Append("</size>");
            }
            if (LogVerbose)
                Log("Close color ok");
            #endregion

            player.ChatMessage(sb.ToString());

            if (VerboseMode)
                Puts("Say to " + player.displayName + " (" + player.UserIDString + ") : " + sb.ToString());

            if (LogVerbose)
            {
                Log("Say to " + player.displayName + " (" + player.UserIDString + ") : " + sb.ToString());
                Log("Exit Say");
            }
        }

        void ReplaceOptions(string str, BasePlayer player, BasePlayer newComer)
        {
            if (LogVerbose)
            {
                Log("Enter Say");
                Log("Begin : " + str);
            }

            // Replace {player_name} by the actual player name
            str = Regex.Replace(str, "{player_name}", player.displayName);

            if (LogVerbose)
            {
                Log("After replace {player_name} : " + str);
            }

            // Replace {player_userID} by the actual player name
            str = Regex.Replace(str, "{player_userID}", player.UserIDString);

            if (LogVerbose)
            {
                Log("After replace {player_userID} : " + str);
            }

            // Replace {newcomer_name} by the actual player name
            str = Regex.Replace(str, "{newcomer_name}", player.displayName);

            if (LogVerbose)
            {
                Log("After replace {newcomer_name} : " + str);
            }

            // Replace {newcomer_userID} by the actual player name
            str = Regex.Replace(str, "{newcomer_userID}", player.UserIDString);

            if (LogVerbose)
            {
                Log("After replace {newcomer_userID} : " + str);
                Log("Exit Say");
            }
        }

        T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            object value;
            var data = Config[category] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
            }

            if (data.TryGetValue(setting, out value))
                return (T)Convert.ChangeType(value, typeof(T));

            value = defaultValue;
            data[setting] = value;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        void SetConfigValue<T>(string category, string setting, T newValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data != null && data.TryGetValue(setting, out value))
            {
                value = newValue;
                data[setting] = value;
            }
            SaveConfig();
        }

        void Log(string text)
        {
            LogToFile(this.Name, $"[{DateTime.Now}] {text}", this);
        }
        #endregion
    }
}
