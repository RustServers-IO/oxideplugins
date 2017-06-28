using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ItemsBlocker", "Vlad-00003", "2.1.3", ResourceId = 2407)]
    [Description("Prevents some items from being used for a limited period of time.")]

    class ItemsBlocker : RustPlugin
    {

        #region Config setup
        //private Dictionary<BasePlayer, string> Panels = new Dictionary<BasePlayer, string>();
        private string PanelName = "BlockerUI";

        private List<string> BlockedItems = new List<string>();
        private List<string> BlockedClothes = new List<string>();
        private DateTime BlockEnd;
        private int HoursOfBlock = 30;
        private bool UseChat = false;
        private bool Wipe = false;
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
            //DateTime BlockEnd;

            //if(!DateTime.TryParseExact(Config["Block end time"].ToString(), "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out BlockEnd))
            //{
            //    BlockEnd = DateTime.Now.AddHours(30);
            //    PrintWarning($"Unable to parse block end date format, block end set to {BlockEnd.ToString("dd.MM.yyyy HH:mm:ss")}");
            //}


            BlockEnd = DateTime.Now.AddHours(HoursOfBlock);
            string BlockEndStr = BlockEnd.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            GetConfig("Block end time", ref BlockEndStr);
            GetConfig("Hour of block after wipe", ref HoursOfBlock);
            GetConfig("Wipe?(If set to true all timers would be automaticly set to current time + Hours of block", ref Wipe);
            GetConfig("Chat prefix", ref Prefix);
            GetConfig("Chat prefix color", ref PrefixColor);
            GetConfig("Use chat insted of GUI", ref UseChat);
            GetConfig("List of blocked items", ref blockedItems);
            GetConfig("List of blocked clothes", ref blockedClothes);
            GetConfig("Bypass permission", ref BypassPermission);
            if (!DateTime.TryParseExact(BlockEndStr, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out BlockEnd))
            {
                BlockEnd = DateTime.Now.AddHours(HoursOfBlock);
                PrintWarning($"Unable to parse block end date format, block end set to {BlockEnd.ToString("dd.MM.yyyy HH:mm:ss")}");
            }
            foreach (var item in blockedItems)
            {
                BlockedItems.Add(item.ToString());
            }
            foreach(var item in blockedClothes)
            {
                BlockedClothes.Add(item.ToString());
            }

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
            if (Wipe)
            {
                BlockEnd = DateTime.Now.AddHours(HoursOfBlock);
                string BlockEndStr = BlockEnd.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                Config["Block end time"] = BlockEndStr;
                Config["Wipe?(If set to true all timers would be automaticly set to current time + Hours of block"] = false;
                SaveConfig();
            }
            permission.RegisterPermission(BypassPermission, this);
        }

        #endregion

        #region Equip controll
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
                    string reply = GetMsg("ItemBlocked", player.UserIDString) + "\n";
                    reply += string.Format(GetMsg("BlockTimeLeft", player.UserIDString), timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
                    reply += "\n" + GetMsg("Weapon line 2", player.UserIDString);

                    if (UseChat)
                    {
                        SendToChat(player, reply);
                    }
                    else
                    {
                        //if (Panels.ContainsKey(player))
                        //{
                        //    CuiHelper.DestroyUi(player, Panels[player]);
                        //    Panels.Remove(player);
                        //}
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
                    string reply = GetMsg("ItemBlocked", player.UserIDString) + "\n";
                    reply += string.Format(GetMsg("BlockTimeLeft", player.UserIDString), timeleft.Days, timeleft.Hours, timeleft.Minutes, timeleft.Seconds);
                    reply += "\n" + GetMsg("Cloth line 2", player.UserIDString);
                    if (UseChat)
                    {
                        SendToChat(player, reply);
                    }
                    else
                    {
                        //if (Panels.ContainsKey(player))
                        //{
                        //    CuiHelper.DestroyUi(player, Panels[player]);
                        //    Panels.Remove(player);
                        //}
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
            //timer.Once(7f, () =>
            //{
            //    CuiHelper.DestroyUi(player, PanelName);
            //});
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
        private void GetConfig<T>(string Key, ref T var)
        {
            if (Config[Key] != null)
            {
                var = (T)Convert.ChangeType(Config[Key], typeof(T));
            }
            Config[Key] = var;
        }
        #endregion
    }
}
