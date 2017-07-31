using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ItemsBlocker", "Vlad-00003", "2.2.0", ResourceId = 2407)]
    [Description("Prevents some items from being used for a limited period of time.")]

    class ItemsBlocker : RustPlugin
    {
        #region Vars
        private string PanelName = "BlockerUI";
        #endregion
        #region Config setup

        private List<string> BlockedItems = new List<string>();
        private List<string> BlockedClothes = new List<string>();
        private DateTime BlockEnd;
        private int HoursOfBlock = 30;
        private bool UseChat = false;
        private string Prefix = "[Items Blocker]";
        private string PrefixColor = "#f44253";
        private string BypassPermission = "itemsblocker.bypass";

        #endregion

        #region Init

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created, Block Start now and will remain for 30 hours. You can change it into the config.");
        }

        private void LoadConfigValues()
        {
            List<object> blockedItems = new List<object>()
            {
                "Satchel Charge",
                "Timed Explosive Charge",
                "Eoka Pistol",
                "Custom SMG",
                "Assault Rifle",
                "Bolt Action Rifle",
                "Waterpipe Shotgun",
                "Revolver",
                "Thompson",
                "Semi-Automatic Rifle",
                "Semi-Automatic Pistol",
                "Pump Shotgun",
                "M249",
                "Rocket Launcher",
                "Flame Thrower",
                "Double Barrel Shotgun",
                "Beancan Grenade",
                "F1 Grenade",
                "MP5A4",
                "LR-300 Assault Rifle",
                "M92 Pistol",
                "Python Revolver"
            };
            List<object> blockedClothes = new List<object>
            {
                "Metal Facemask",
                "Metal Chest Plate",
                "Road Sign Kilt",
                "Road Sign Jacket",
                "Heavy Plate Pants",
                "Heavy Plate Jacket",
                "Heavy Plate Helmet",
                "Riot Helmet",
                "Bucket Helmet",
                "Coffee Can Helmet"

            };

            BlockEnd = DateTime.Now.AddHours(HoursOfBlock);
            string BlockEndStr = BlockEnd.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            if (GetConfig("Block end time", ref BlockEndStr))
                PrintWarning("Option \"Block end time\" was added to the config");
            if(GetConfig("Hour of block after wipe", ref HoursOfBlock))
                PrintWarning("Option \"Hour of block after wipe\" was added to the config");
            if(GetConfig("Chat prefix", ref Prefix))
                PrintWarning("Option \"Chat prefix\" was added to the config");
            if(GetConfig("Chat prefix color", ref PrefixColor))
                PrintWarning("Option \"Chat prefix color\" was added to the config");
            if(GetConfig("Use chat insted of GUI", ref UseChat))
                PrintWarning("Option \"Use chat insted of GUI\" was added to the config");
            if(GetConfig("List of blocked items", ref blockedItems))
                PrintWarning("Option \"List of blocked items\" was added to the config");
            if(GetConfig("List of blocked clothes", ref blockedClothes))
                PrintWarning("Option \"List of blocked clothes\" was added to the config");
            if(GetConfig("Bypass permission", ref BypassPermission))
                PrintWarning("Option \"Bypass permission\" was added to the config");
            if (!DateTime.TryParseExact(BlockEndStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out BlockEnd))
            {
                BlockEnd = DateTime.Now.AddHours(HoursOfBlock);
                PrintWarning($"Unable to parse block end date format, block end set to {BlockEnd.ToString("dd.MM.yyyy HH:mm:ss")}");
            }
            BlockedItems = blockedItems.Select(i => i.ToString()).ToList();
            BlockedClothes = blockedClothes.Select(i => i.ToString()).ToList();

            SaveConfig();
        }
        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ItemBlocked", "Using this item is blocked!"},
                {"BlockTimeLeft","{0}d {1:00}:{2:00}:{3:00} until unblock." },
                {"Weapon line 2", "You can only use Hunting bow and Crossbow" },
                {"Cloth line 2","You can only use wood and bone armor!" }
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ItemBlocked", "Использование данного предмета заблокировано!"},
                {"BlockTimeLeft", "До окончания блокировки осталось {0}д. {1:00}:{2:00}:{3:00}" },
                {"Weapon line 2", "Вы можете использовать только Лук и Арбалет" },
                {"Cloth line 2","Используйте только деревянную и костяную броню!" }
            }, this, "ru");
        }

        void Loaded()
        {
            LoadConfigValues();
            LoadMessages();
            permission.RegisterPermission(BypassPermission, this);
        }

        #endregion

        #region Oxide hooks
        void OnNewSave(string filename)
        {
            BlockEnd = DateTime.Now.AddHours(HoursOfBlock);
            string BlockEndStr = BlockEnd.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            Config["Block end time"] = BlockEndStr;
            SaveConfig();
            PrintWarning($"Wipe detected. Block end set to {BlockEndStr}");
        }
        object CanEquipItem(PlayerInventory inventory, Item item)
        {
            if (InBlock())
            {
                var player = inventory.GetComponent<BasePlayer>();
                if (permission.UserHasPermission(player.UserIDString, BypassPermission))
                    return null;
                if(BlockedItems.Contains(item.info.displayName.english) || BlockedItems.Contains(item.info.shortname))
                {
                    var timeleft = TimeLeft();
                    string reply = GetMsg("ItemBlocked", player) + "\n";
                    reply += string.Format(GetMsg("BlockTimeLeft", player), timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
                    reply += "\n" + GetMsg("Weapon line 2", player);

                    if (UseChat)
                    {
                        SendToChat(player, reply);
                    }
                    else
                    {
                        BlockerUi(player, reply);
                    }
                    return false;
                }
            }
            return null;
        }
        object CanWearItem(PlayerInventory inventory, Item item)
        {
            if (InBlock())
            {
                var player = inventory.GetComponent<BasePlayer>();
                if (permission.UserHasPermission(player.UserIDString, BypassPermission))
                    return null;
                if (BlockedClothes.Contains(item.info.displayName.english) || BlockedClothes.Contains(item.info.shortname))
                {
                    var timeleft = TimeLeft();
                    string reply = GetMsg("ItemBlocked", player) + "\n";
                    reply += string.Format(GetMsg("BlockTimeLeft", player), timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
                    reply += "\n" + GetMsg("Cloth line 2", player);
                    if (UseChat)
                    {
                        SendToChat(player, reply);
                    }
                    else
                    {
                        BlockerUi(player, reply);
                    }                    
                    return false;
                }
            }
            return null;
        }

        #endregion

        #region Blocker UI

        private void BlockerUi(BasePlayer player, string inputText)
        {
            CuiHelper.DestroyUi(player,PanelName);
            var elements = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image =
                        {
                            Color = "0.1 0.1 0.1 0.5"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                        CursorEnabled = true
                    },
                    new CuiElement().Parent = "Overlay", PanelName
                }
            };
            elements.Add(new CuiButton
            {
                Button =
                {
                    Close = PanelName,
                    Color = "0.8 0.8 0.8 0"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
                Text =
                {
                    Text = "",
                    FontSize = 22,
                    Align = TextAnchor.MiddleCenter
                }
            }, PanelName);
            elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = inputText,
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0.443 0.867 0.941 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.35 0.47",
                    AnchorMax = "0.65 0.57"
                }
            }, PanelName);
            CuiHelper.AddUi(player, elements);
        }

        #endregion

        #region Helpers
        private bool InBlock()
        {
            if (TimeLeft().TotalSeconds >= 0)
            {
                return true;
            }
            return false;
        }
        string GetMsg(string key, BasePlayer player = null) => GetMsg(key, player.UserIDString);
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        TimeSpan TimeLeft() => BlockEnd.Subtract(DateTime.Now);
        private void SendToChat(BasePlayer Player, string Message)
        {
            PrintToChat(Player, "<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }
        private void SendToChat(string Message)
        {
            PrintToChat("<color=" + PrefixColor + ">" + Prefix + "</color> " + Message);
        }
        private bool GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
                return false;
            }
            Config[Key] = var;
            return true;
        }
        #endregion
    }
}
