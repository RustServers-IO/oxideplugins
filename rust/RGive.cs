using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RGive", "LaserHydra", "2.1.1", ResourceId = 929)]
    [Description("Random item giving")]
    internal class RGive : RustPlugin
    {
        private PluginConfiguration configuration = new PluginConfiguration();
        private readonly Dictionary<ItemCategory, List<ItemDefinition>> availableItems = new Dictionary<ItemCategory, List<ItemDefinition>>();

        #region Hooks
        
        private void Init()
        {
            RegisterPerm("use");
            
            LoadConfig(ref configuration);
            SaveConfig(configuration);
            
            foreach (var itemDefinition in ItemManager.itemList)
            {
                if (IsItemBlacklisted(itemDefinition))
                    continue;

                if (!IsCategoryEnabled(itemDefinition.category))
                    continue;

                if (availableItems.ContainsKey(itemDefinition.category))
                    availableItems[itemDefinition.category].Add(itemDefinition);
                else
                    availableItems.Add(itemDefinition.category, new List<ItemDefinition> { itemDefinition });
            }
        }

        #endregion

        #region Random Item Giving

        private BasePlayer GetRandomPlayer() => BasePlayer.activePlayerList[UnityEngine.Random.Range(0, BasePlayer.activePlayerList.Count - 1)];

        private void GiveRandomItem(BasePlayer player)
        {
            if (player == null || availableItems.Count == 0)
                return;
            
            ItemCategory category = availableItems.Keys.ToList().GetRandom((uint) DateTime.UtcNow.Millisecond);
            ItemDefinition itemDefinition = availableItems[category].GetRandom((uint) DateTime.UtcNow.Millisecond);

            int amount = GetRandomAmount(itemDefinition.category);

            GiveItem(ItemManager.CreateByItemID(itemDefinition.itemid), amount, player);

            string message = $"You have recieved random items: {amount}x {itemDefinition.displayName.english}s";

            if (amount == 1 || itemDefinition.displayName.english.EndsWith("s"))
                message = $"You have recieved a random item: {amount}x {itemDefinition.displayName.english}";

            SendMessage(player, "RGive", message);
        }

        private void GiveItem(Item item, int amount, BasePlayer player)
        {
            if (item == null)
                return;

            if (!player.inventory.GiveItem(ItemManager.CreateByItemID(item.info.itemid, amount)))
                item.Drop(player.transform.position, Vector3.zero);
        }

        #endregion

        #region Item & Category Helpers

        private bool IsItemBlacklisted(ItemDefinition itemDefinition) => configuration.Blacklist.Contains(itemDefinition.shortname);

        private bool IsCategoryEnabled(ItemCategory category)
        {
            if (!configuration.Categories.ContainsKey(category.ToString()))
                return false;

            return configuration.Categories[category.ToString()].Enabled;
        }

        private int GetRandomAmount(ItemCategory category)
        {
            if (!configuration.Categories.ContainsKey(category.ToString()))
                return 0;

            Category settings = configuration.Categories[category.ToString()];

            return UnityEngine.Random.Range(settings.MinimalAmount, settings.MaximalAmount + 1);
        }

        #endregion

        #region Commands

        [ChatCommand("rgive")]
        private void cmdRGive(BasePlayer player, string cmd, string[] args)
        {
            if (player != null)
            {
                if (!HasPerm(player.userID, "use"))
                {
                    SendMessage(player, "RGive", "You don't have permission to use this command!");
                    return;
                }
            }

            //	Give Random item to all
            if (args.Length == 1 && args[0].ToLower() == "all")
            {
                if (availableItems.Count == 0)
                {
                    SendMessage(player, "RGive", "Random items could not be given, because there are none enabled.");
                    return;
                }

                foreach (BasePlayer current in BasePlayer.activePlayerList)
                    GiveRandomItem(current);

                BroadcastChat("RGive", "Random items have been given to all online players!");

                return;
            }

            //	Show Syntax
            if (args.Length < 2)
            {
                SendMessage(player, "<size=20>RGive</size>", "\n" +
                            "<color=#00FF8D>/rgive player <playername></color> give random item to specific player\n" +
                            "<color=#00FF8D>/rgive item <itemname></color> give specific item to random player\n" +
                            "<color=#00FF8D>/rgive all</color> give random item to all players\n");

                return;
            }

            if (args.Length >= 2)
            {
                switch (args[0].ToLower())
                {
                    //	Give random item to specific player
                    case "player":
                        if (availableItems.Count == 0)
                        {
                            SendMessage(player, "RGive", "Random items could not be given, because there are none enabled.");
                            return;
                        }

                        BasePlayer specificTarget = FindPlayer(args[1], player);
                        if (specificTarget == null) return;

                        GiveRandomItem(specificTarget);
                        SendMessage(player, "RGive", "Random items given to " + specificTarget.displayName);

                        break;

                    //	Give specific item to random player
                    case "item":
                        BasePlayer randomTarget = GetRandomPlayer();
                        Item item = FindItem(args[1], player);
                        if (item == null)
                            return;

                        int amount = GetRandomAmount(item.info.category);

                        GiveItem(item, amount, randomTarget);

                        //	Send message to the sender

                        if (amount == 1 || item.info.displayName.english.EndsWith("s"))
                            SendMessage(player, "RGive", $"{amount} {item.info.displayName.english} given to {randomTarget.displayName}");
                        else
                            SendMessage(player, "RGive", $"{amount} {item.info.displayName.english}s given to {randomTarget.displayName}");

                        //	Send message to the lucky reciever

                        if (amount == 1 || item.info.displayName.english.EndsWith("s"))
                            SendMessage(randomTarget, "RGive", $"You have been randomly chosen to recieve {amount} {item.info.displayName.english}");
                        else
                            SendMessage(randomTarget, "RGive", "You have been randomly chosen to recieve {amount} {item.info.displayName.english}s");

                        break;

                    //	Wrong args, show Syntax
                    default:
                        SendMessage(player, "<size=20>RGive</size>", "\n" +
                            "<color=#00FF8D>/rgive player <playername></color> give random item to specific player\n" +
                            "<color=#00FF8D>/rgive item <itemname></color> give specific item to random player\n" +
                            "<color=#00FF8D>/rgive all</color> give random item to all players\n");
                        break;
                }
            }
        }

        [ConsoleCommand("rgive")]
        private void ccmdRGive(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg?.Connection?.player == null ? null : (BasePlayer) arg.Connection.player;
            string[] args = arg.HasArgs() ? arg.Args : new string[0];

            cmdRGive(player, arg.cmd.Name, args);
        }

        #endregion
        
        #region Finding

        private Item FindItem(string searchedItem, BasePlayer player)
        {
            if (ItemManager.CreateByName(searchedItem.ToLower()) != null)
                return ItemManager.CreateByName(searchedItem.ToLower());

            List<string> foundItemNames =
                (from info in ItemManager.itemList
                 where info.shortname.ToLower().Contains(searchedItem.ToLower())
                 select info.shortname).ToList();

            switch (foundItemNames.Count)
            {
                case 0:
                    SendMessage(player, "The item can not be found.");

                    break;

                case 1:
                    return ItemManager.CreateByName(foundItemNames[0]);

                default:
                    SendMessage(player, "Multiple matching items found: \n" + foundItemNames.ToSentence());

                    break;
            }

            return null;
        }

        private BasePlayer FindPlayer(string searchedPlayer, BasePlayer player)
        {
            foreach (BasePlayer current in BasePlayer.activePlayerList)
                if (current.displayName.ToLower() == searchedPlayer.ToLower())
                    return current;

            List<BasePlayer> foundPlayers =
                (from current in BasePlayer.activePlayerList
                 where current.displayName.ToLower().Contains(searchedPlayer.ToLower())
                 select current).ToList();

            switch (foundPlayers.Count)
            {
                case 0:
                    SendMessage(player, "The player can not be found.");
                    break;

                case 1:
                    return foundPlayers[0];

                default:
                    List<string> playerNames = (from current in foundPlayers select current.displayName).ToList();
                    SendMessage(player, "Multiple matching players found: \n" + playerNames.ToSentence());
                    break;
            }

            return null;
        }

        #endregion
        
        #region Config

        private class Category
        {
            public bool Enabled;
            [JsonProperty("Minimal Amount")] public int MinimalAmount;
            [JsonProperty("Maximal Amount")] public int MaximalAmount;
        }

        private class PluginConfiguration
        {
            public HashSet<string> Blacklist = new HashSet<string>
            {
                "autoturret",
                "mining.quarry",
                "mining.pumpjack",
                "cctv.camera",
                "targeting.computer"
            };

            public Dictionary<string, Category> Categories = new Dictionary<string, Category>
            {
                ["Weapon"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 2,
                    Enabled = true
                },
                ["Construction"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 5,
                    Enabled = true
                },
                ["Items"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 5,
                    Enabled = true
                },
                ["Resources"] = new Category
                {
                    MinimalAmount = 500,
                    MaximalAmount = 10000,
                    Enabled = false
                },
                ["Attire"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 2,
                    Enabled = true
                },
                ["Tool"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 2,
                    Enabled = true
                },
                ["Medical"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 5,
                    Enabled = true
                },
                ["Food"] = new Category
                {
                    MinimalAmount = 5,
                    MaximalAmount = 10,
                    Enabled = false
                },
                ["Ammunition"] = new Category
                {
                    MinimalAmount = 5,
                    MaximalAmount = 64,
                    Enabled = true
                },
                ["Traps"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 3,
                    Enabled = true
                },
                ["Misc"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 5,
                    Enabled = false
                },
                ["Component"] = new Category
                {
                    MinimalAmount = 1,
                    MaximalAmount = 5,
                    Enabled = false
                }
            };
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new config file...");

        private void LoadConfig<T>(ref T data) => data = Config.ReadObject<T>();

        private void SaveConfig<T>(T data)
        {
            Config.WriteObject(data, true);
            SaveConfig();
        } 

        #endregion

        #region Permissions

        private void RegisterPerm(params string[] permArray) => permission.RegisterPermission($"{PermissionPrefix}.{string.Join(".", permArray)}", this);

        private bool HasPerm(object uid, params string[] permArray) => permission.UserHasPermission(uid.ToString(), $"{PermissionPrefix}.{string.Join(".", permArray)}");

        private string PermissionPrefix => Title.Replace(" ", "").ToLower();

        #endregion

        #region Messages

        private string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID?.ToString());

        private void BroadcastChat(string prefix, string msg = null) => rust.BroadcastChat(msg == null ? prefix : "<color=#C4FF00>" + prefix + "</color>: " + msg);

        private void SendMessage(BasePlayer player, string prefix, string msg = null)
        {
            if (player == null)
                Puts(msg == null ? StripTags(prefix) : StripTags(msg));
            else
                rust.SendChatMessage(player, msg == null ? prefix : "<color=#C4FF00>" + prefix + "</color>: " + msg);
        }

        #endregion

        #region Formatting

        private string StripTags(string original)
        {
            List<string> regexTags = new List<string>
            {
                @"<color=.+?>",
                @"<size=.+?>"
            };

            List<string> tags = new List<string>
            {
                "</color>",
                "</size>",
                "<i>",
                "</i>",
                "<b>",
                "</b>"
            };

            foreach (string tag in tags)
                original = original.Replace(tag, "");

            foreach (string regexTag in regexTags)
                original = new Regex(regexTag).Replace(original, "");

            return original;
        }

        #endregion
    }
}