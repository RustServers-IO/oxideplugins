using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SkullCrusher", "redBDGR", "1.0.5", ResourceId = 2412)]
    [Description("Add some extra features to the crushing of human skulls")]
    class SkullCrusher : RustPlugin
    {
        private Dictionary<string, int> cacheDic = new Dictionary<string, int>();

        private bool Changed;
        [PluginReference] private Plugin Economics;

        private bool giveItemsOnCrush = true;
        private double moneyPerSkullCrush = 20.0;
        private bool normalCrusherMessage = true;
        private bool nullCrusherMessage = true;
        private bool ownCrusherMessage = true;
        private int RPPerSkullCrush = 20;
        private bool sendNotificaitionMessage = true;
        [PluginReference] private Plugin ServerRewards;

        private DynamicConfigFile SkullCrusherData;
        private StoredData storedData;
        private bool useEconomy;
        private bool useServerRewards;

        private void SaveData()
        {
            storedData.PlayerInformation = cacheDic;
            SkullCrusherData.WriteObject(storedData);
        }

        private void LoadData()
        {
            try
            {
                storedData = SkullCrusherData.ReadObject<StoredData>();
                cacheDic = storedData.PlayerInformation;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        private void OnServerInitialized()
        {
            LoadData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        private void Init()
        {
            LoadVariables();
        }

        private void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Null Crusher"] = "{0}'s skull was crushed",
                ["Crushed own skull"] = "{0} crushed their own skull!",
                ["Default Crush Message"] = "{0}'s skull was crushed by {1}",
                ["Skulls chat command reply"] = "You have crushed a total of {0} skulls",
                ["Economy Notice"] = "You received ${0} for crushing an enemies skull!",
                ["ServerRewards Notice"] = "You received {0} RP for crushing an enemies skull!"
            }, this);

            SkullCrusherData = Interface.Oxide.DataFileSystem.GetFile("SkullCrusher");

            if (useEconomy)
                if (!Economics)
                {
                    PrintError("Economics.cs was not found, auto-disabling economic features");
                    useEconomy = false;
                }
            if (useServerRewards)
                if (!ServerRewards)
                {
                    PrintError("ServerRewards.cs was not found, auto-disabling serverrewards features");
                    useServerRewards = false;
                }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            giveItemsOnCrush = Convert.ToBoolean(GetConfig("Settings", "Give items on crush", true));

            useEconomy = Convert.ToBoolean(GetConfig("Economy", "Use Economy", false));
            useServerRewards = Convert.ToBoolean(GetConfig("Economy", "Use ServerRewards", false));
            RPPerSkullCrush = Convert.ToInt32(GetConfig("Economy", "RP Per Skull Crush", 20));
            moneyPerSkullCrush = Convert.ToDouble(GetConfig("Economy", "Money Per Skull Crush", 20.0));
            sendNotificaitionMessage = Convert.ToBoolean(GetConfig("Economy", "Send Notification Message", true));

            nullCrusherMessage = Convert.ToBoolean(GetConfig("Settings", "Null Owner Crush Message", true));
            ownCrusherMessage = Convert.ToBoolean(GetConfig("Settings", "Own Skull Crush Message", true));
            normalCrusherMessage = Convert.ToBoolean(GetConfig("Settings", "Normal Crush Message", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private object OnItemAction(Item item, string action)
        {
            if (action != "crush") return null;
            if (item.info.shortname != "skull.human") return null;
            string skullName = null;
            if (item.name != null)
                skullName = item.name.Substring(10, item.name.Length - 11);
            if (string.IsNullOrEmpty(skullName)) return DecideReturn(item);

            if (item.GetOwnerPlayer() == null)
            {
                if (nullCrusherMessage)
                    rust.BroadcastChat(null, string.Format(msg("Null Crusher"), skullName));
                return DecideReturn(item);
            }
            var ownerPlayer = item.GetOwnerPlayer();
            if (ownerPlayer.displayName == skullName)
            {
                if (ownCrusherMessage)
                    rust.BroadcastChat(null, string.Format(msg("Crushed own skull"), ownerPlayer.displayName));
                return DecideReturn(item);
            }
            if (!cacheDic.ContainsKey(ownerPlayer.UserIDString))
                cacheDic.Add(ownerPlayer.UserIDString, 0);
            cacheDic[ownerPlayer.UserIDString]++;
            if (useEconomy)
                if (Economics)
                {
                    if (sendNotificaitionMessage)
                        ownerPlayer.ChatMessage(string.Format(msg("Economy Notice", ownerPlayer.UserIDString), moneyPerSkullCrush));
                    Economics.CallHook("Deposit", ownerPlayer.userID, moneyPerSkullCrush);
                }
            if (useServerRewards)
                if (ServerRewards)
                {
                    if (sendNotificaitionMessage)
                        ownerPlayer.ChatMessage(string.Format(msg("ServerRewards Notice", ownerPlayer.UserIDString), RPPerSkullCrush));
                    ServerRewards.Call("AddPoints", ownerPlayer.userID, RPPerSkullCrush);
                }
            if (normalCrusherMessage)
                rust.BroadcastChat(null, string.Format(msg("Default Crush Message"), skullName, ownerPlayer.displayName));
            return DecideReturn(item);
        }

        private object DecideReturn(Item item)
        {
            if (giveItemsOnCrush)
                return null;
            item.UseItem();
            return true;
        }

        [ChatCommand("skulls")]
        private void skullsCMD(BasePlayer player, string command, string[] args)
        {
            if (!cacheDic.ContainsKey(player.UserIDString))
                cacheDic.Add(player.UserIDString, 0);
            player.ChatMessage(string.Format(msg("Skulls chat command reply", player.UserIDString), cacheDic[player.UserIDString]));
        }

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

        private string msg(string key, string id = null)
        {
            return lang.GetMessage(key, this, id);
        }

        private class StoredData
        {
            public Dictionary<string, int> PlayerInformation = new Dictionary<string, int>();
        }
    }
}