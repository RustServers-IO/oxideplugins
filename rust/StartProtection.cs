// Requires: ImageLibrary
using System;
using System.IO;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("StartProtection", "Norn / wazzzup", "2.2.1", ResourceId = 1342)]
    [Description("Give people some leeway when they first join the game.")]
    public class StartProtection : RustPlugin
    {
        [PluginReference]
        Plugin Friends;
        [PluginReference]
        ImageLibrary ImageLibrary;

        class StoredData
        {
            public Dictionary<ulong, ProtectionInfo> Players = new Dictionary<ulong, ProtectionInfo>();
            public StoredData()
            {
            }
        }
        class StoredPlayersData
        {
            public List<ulong> Players = new List<ulong>();
            public StoredPlayersData()
            {
            }
        }

        class ProtectionInfo
        {
            public ulong UserId;
            public int TimeLeft;
            public bool Multiple;
            public int InitTimestamp;
            public ProtectionInfo()
            {
            }
        }

        Timer ProtectionTimer;
        StoredData storedData;
        StoredPlayersData storedPlayersData;
        StoredData storedDataEx;
        static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        static readonly double MaxUnixSeconds = (DateTime.MaxValue - UnixEpoch).TotalSeconds;
        static readonly Dictionary<ulong, string> guiInfo = new Dictionary<ulong, string>();
        string nofight_png;


        #region Config
        private bool Changed = false;
        private bool bProtectionEnabled;
        private bool bSleeperProtection;
        private bool bHelicopterProtection;
        private string UIIcon;
        private bool canLootHeli;
        private bool canLootDrop;
        private bool canLootFriends;
        private bool canLootFriendDeployables;
        private bool showUIIcon;
        private string UIIconAnchorMin;
        private string UIIconAnchorMax;
        private int iTime;
        private int iPunishment;
        private int iInactiveDays;
        private int iUpdateTimerInterval;
        
        private void LoadVariables()
        {
            canLootHeli = Convert.ToBoolean(GetConfig("Settings","canLootHeli", false));
            canLootDrop = Convert.ToBoolean(GetConfig("Settings","canLootDrop", false));
            canLootFriends = Convert.ToBoolean(GetConfig("Settings","canLootFriends", false));
            canLootFriendDeployables = Convert.ToBoolean(GetConfig("Settings","canLootFriendDeployables", true));
            bProtectionEnabled = Convert.ToBoolean(GetConfig("Settings","bProtectionEnabled", true));
            bSleeperProtection = Convert.ToBoolean(GetConfig("Settings","bSleeperProtection", true));
            bHelicopterProtection = Convert.ToBoolean(GetConfig("Settings","bHelicopterProtection", false));
            iUpdateTimerInterval = Convert.ToInt32(GetConfig("Settings","iUpdateTimerInterval", 10));
            iTime = Convert.ToInt32(GetConfig("Settings","iTime", 3600));
            iPunishment = Convert.ToInt32(GetConfig("Settings","iPunishment", 300));
            iInactiveDays = Convert.ToInt32(GetConfig("Settings", "iInactiveDays", 1));
            UIIcon = Convert.ToString(GetConfig("Settings", "UIIcon", "https://i.imgur.com/hom6JrH.png"));
            showUIIcon = Convert.ToBoolean(GetConfig("Settings", "showUIIcon", true));
            UIIconAnchorMin = Convert.ToString(GetConfig("Settings", "UIIconAnchorMin", "0.245 0.025"));
            UIIconAnchorMax = Convert.ToString(GetConfig("Settings", "UIIconAnchorMax", "0.290 0.095"));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
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

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);
            Interface.Oxide.DataFileSystem.WriteObject(this.Title+"Players", storedPlayersData);
        }

        #endregion

        #region Commands

        void Log(string msg)
        {
            LogToFile("log",$"[{DateTime.Now}] {msg}",this);
        }

        [ChatCommand("sp")]
        private void SPCommand(BasePlayer player, string command, string[] args)
        {
            if (bProtectionEnabled == false && player.net.connection.authLevel <2)
            {
                PrintToChatEx(player, GetMessage("tDisabled", player.UserIDString));
                return;
            }
            if (args.Length == 0 || args.Length > 2)
            {
                PrintToChatEx(player, "USAGE: /sp <time | end>");
                if (player.net.connection.authLevel >= 2)
                {
                    PrintToChatEx(player, "<color=yellow>ADMIN: /sp <toggle | togglesleep | cleardb | me></color>");
                }
            }
            else if (args[0] == "me")
            {
                if (player.net.connection.authLevel >= 2)
                {
                    if (storedPlayersData.Players.Contains(player.userID))
                    {
                        storedPlayersData.Players.Remove(player.userID);
                    }
                    OnPlayerFirstInit(player.userID);
                    ProtectionInfo p = null;
                    if (storedData.Players.TryGetValue(player.userID, out p))
                    {
                        int minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes);
                        Log("Start protection enabled for " + player.displayName + " [" + player.userID.ToString() + "] - Duration: " + minutes + " minutes.");
                        string parsed_config = GetMessage("tFirstSpawn", player.UserIDString);
                        parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                        PrintToChatEx(player, parsed_config);
                        if (showUIIcon) SPUiUser(BasePlayer.Find(player.userID.ToString()),minutes.ToString());
                    }
                    else { Log($"Failed for {player.userID}..."); }

                }
                else
                {
                    PrintToChatEx(player, GetMessage("tNoAuthLevel", player.UserIDString));
                }
            }
            else if (args[0] == "cleardb")
            {
                if (player.net.connection.authLevel >= 2)
                {
                    storedData.Players.Clear();
                    storedPlayersData.Players.Clear();
                    PrintToChatEx(player, GetMessage("tDBCleared", player.UserIDString));
                    SaveData();
                }
                else
                {
                    PrintToChatEx(player, GetMessage("tNoAuthLevel", player.UserIDString));
                }
            }
            else if (args[0] == "togglesleep")
            {
                if (player.net.connection.authLevel >= 2)
                {
                    if (bSleeperProtection == true)
                    {
                        PrintToChatEx(player, "Sleep Protection: <color=red>disabled</color>.");
                        Log("Start Protection sleeper protection has been disabled by " + player.displayName + " (type /sp togglesleep to enable).");
                        Config["bSleeperProtection"] = false;
                        SaveConfig();
                    }
                    else
                    {
                        PrintToChatEx(player, "Sleep Protection: <color=green>enabled</color>.");
                        Log("Start Protection sleeper protection has been enabled by " + player.displayName + " (type /sp togglesleep to disabled).");
                        Config["bSleeperProtection"] = true;
                        SaveConfig();
                    }
                }
                else
                {
                    PrintToChatEx(player, GetMessage("tNoAuthLevel", player.UserIDString));
                }
            }
            else if (args[0] == "toggle")
            {
                if (player.net.connection.authLevel >= 2)
                {
                    if (bProtectionEnabled == true)
                    {
                        if (ProtectionTimer != null)
                        {
                            ProtectionTimer.Destroy();
                        }
                        PrintToChatEx(player, GetMessage("tDisabled", player.UserIDString));
                        Puts("Start Protection has been disabled by " + player.displayName + " (type /sp toggle to enable).");
                        Config["bProtectionEnabled"] = false;
                        SaveConfig();
                    }
                    else
                    {
                        ProtectionTimer = timer.Repeat(iUpdateTimerInterval, 0, () => UpdateProtectedList(true));
                        PrintToChatEx(player, GetMessage("tEnabled", player.UserIDString));
                        int minutes = Convert.ToInt32(TimeSpan.FromSeconds(iTime).TotalMinutes);
                        Puts("Start Protection has been enabled by " + player.displayName + " [Minutes: " + minutes.ToString() + "] (type /sp toggle to disable).");
                        Config["bProtectionEnabled"] = true;
                        SaveConfig();
                    }
                }
                else
                {
                    PrintToChatEx(player, GetMessage("tNoAuthLevel", player.UserIDString));
                }
            }
            else if (args[0] == "end")
            {
                ProtectionInfo p = null;
                if (storedData.Players.TryGetValue(player.userID, out p))
                {
                    Log("Start protection disabled by user " + player.displayName + " [" + player.userID.ToString() + "]");
                    PunishPlayer(player, iTime + 1, false);
                }
                else
                {
                    PrintToChatEx(player, GetMessage("tNoProtection", player.UserIDString));
                }
            }
            else if (args[0] == "time")
            {
                ProtectionInfo p = null;
                if (storedData.Players.TryGetValue(player.userID, out p))
                {
                    string minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes).ToString();

                    string parsed_config = GetMessage("tSpawn", player.UserIDString);
                    parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                    PrintToChatEx(player, parsed_config);
                }
                else
                {

                    PrintToChatEx(player, GetMessage("tNoProtection", player.UserIDString));
                }
            }
        }
        #endregion

        #region Oxide Hooks

        protected override void LoadDefaultConfig()
        {
            Puts("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            if (bProtectionEnabled == true)
            {
                RemoveOldUsers();
                ProtectionTimer = timer.Repeat(iUpdateTimerInterval, 0, () => UpdateProtectedList(true));
                string minutes = Convert.ToInt32(TimeSpan.FromSeconds(iTime).TotalMinutes).ToString();
                Puts("Start Protection has been enabled [Minutes: " + minutes + "] (type /sp toggle to disable).");
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (storedData.Players.ContainsKey(player.userID))
                    {
                        DestroyUi(player);
                    }
                }
            }
            else
            {
                Puts("Start Protection is not enabled (type /sp toggle to enable).");
            }
            ImageLibrary.AddImage(UIIcon, "noak47", 0);
            LoadImage();
        }

        private void LoadImage()
        {
            if (!ImageLibrary.IsReady() || !ImageLibrary.HasImage("noak47",0))
            {
                PrintWarning("Waiting for ImageLibrary to finish image processing!");
                timer.In(10, LoadImage);
                return;
            }
            nofight_png = (string)ImageLibrary.GetImage("noak47",0);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!permission.UserExists(player.userID.ToString()) || !storedPlayersData.Players.Contains(player.userID))
                {
                    OnPlayerFirstInit(player.userID);
                }
            }
        }

        private void Loaded()
        {
            storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
            storedPlayersData = Interface.GetMod().DataFileSystem.ReadObject<StoredPlayersData>(this.Title+"Players");
            LoadVariables();
            LoadDefaultMessages();
            ulong u = 0;
        }

        void Unload()
        {
            Puts("Saving protection database...");
            if (ProtectionTimer != null)
            {
                ProtectionTimer.Destroy();
            }
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (storedData.Players.ContainsKey(player.userID))
                {
                    DestroyUi(player);
                }
            }
            SaveData();
        }

        void OnServerSave() => SaveData();
        void OnServerShutdown() => SaveData();

        void OnPlayerFirstInit(ulong steamid, int timeleft = -1)
        {
            ProtectionInfo p = null;
            if (storedData.Players.TryGetValue(steamid, out p))
            {
                if (p.Multiple == false && p.TimeLeft == iTime)
                {
                    Log("Removing " + steamid + " from protection list, cleaning up...");
                    storedData.Players.Remove(steamid);
                    OnPlayerFirstInit(steamid,timeleft);
                }
            }
            else
            {
                var info = new ProtectionInfo();
                if (timeleft == -1) timeleft = iTime;
                info.TimeLeft = timeleft;
                info.Multiple = false;
                info.InitTimestamp = UnixTimeStampUTC();// Timestamp
                info.UserId = steamid;
                storedData.Players.Add(steamid, info);
                Interface.GetMod().DataFileSystem.WriteObject(this.Title, storedData);
            }
        }
        private void OnNewSave(string filename)
        {
            storedData.Players.Clear();
            storedPlayersData.Players.Clear();
            SaveData();
            PrintWarning("Wipe detected, cleared data");
        }

        void OnUserApprove(Network.Connection connection)
        {
            string userid = connection.userid.ToString();
            ulong u = 0;
            if (!permission.UserExists(userid) || !storedPlayersData.Players.Contains(connection.userid))
            {
                OnPlayerFirstInit(connection.userid);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            ProtectionInfo p = null;
            if (storedData.Players.TryGetValue(player.userID, out p))
            {
                int minutes = 0;
                if (!p.Multiple)
                {
                    minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes);
                    Log("Start protection enabled for " + player.displayName + " [" + player.userID.ToString() + "] - Duration: " + minutes + " minutes.");
                    string parsed_config = GetMessage("tFirstSpawn", player.UserIDString);
                    parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                    SPUi(player, parsed_config);
                    p.Multiple = true;
                }
                else
                {
                    minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes);
                    string parsed_config = GetMessage("tSpawn", player.UserIDString);
                    parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                    PrintToChatEx(player, parsed_config);
                }
                if (showUIIcon) SPUiUser(player,minutes.ToString());
            }
        }

        private HitInfo OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (bProtectionEnabled == true)
            {
                if (entity is BasePlayer)
                {
                    var player = entity as BasePlayer;
                    if (player.userID<76560000000000000L || player is NPCPlayer) return null;

                    ProtectionInfo p = null;
                    ProtectionInfo z = null;
                    if (hitInfo.Initiator is BasePlayer)
                    {
                        var attacker = hitInfo.Initiator as BasePlayer;
                        if (attacker.userID<76560000000000000L || attacker is NPCPlayer) return null;
                        if (storedData.Players.TryGetValue(player.userID, out p))
                        {
                            if (storedData.Players.TryGetValue(attacker.userID, out z))
                            {
                                if (attacker.userID == player.userID)
                                {
                                    return null;
                                }
                                else
                                {
                                    PunishPlayer(attacker);
                                    Log("Punishing " + attacker.displayName.ToString() + " for attempting to pvp.");
                                }
                            }
                            if (attacker.userID != player.userID)
                            {
                                if (player.IsSleeping())
                                {
                                    //TODO possibly bug
                                    if (bSleeperProtection == false)
                                    {
                                        storedData.Players.Remove(player.userID);
                                        storedPlayersData.Players.Add(player.userID);
                                        Log("Removed " + player.displayName.ToString() + " (Sleeping) from the Start Protection list.");
                                        return null;
                                    }
                                }
                            }
                            PrintToChatEx(attacker, GetMessage("tAttackAttempt", attacker.UserIDString));
                            hitInfo.damageTypes.ScaleAll(0f);
                            return hitInfo;
                        }
                        else
                        {
                            if (storedData.Players.TryGetValue(attacker.userID, out p))
                            {
                                PunishPlayer(attacker);
                                Log("Punishing " + attacker.displayName.ToString() + " for attempting to pvp.");
                                hitInfo.damageTypes.ScaleAll(0f);
                                return hitInfo;
                            }
                        }
                    }
                    else if (hitInfo.Initiator is BaseHelicopter)
                    {
                        if (bHelicopterProtection == true)
                        {
                            if (player == null) { return null; }
                            if (storedData.Players.TryGetValue(player.userID, out z))
                            {
                                hitInfo.damageTypes.ScaleAll(0f);
                                return hitInfo;
                            }
                        }
                    }
                }
                else if(entity is BuildingBlock || entity is Door || (entity.PrefabName?.Contains("building") ?? false) || (entity.PrefabName?.Contains("deployable") ?? false))
                {
                    if (hitInfo.Initiator is BasePlayer && entity.OwnerID!=0 && entity.OwnerID!=(hitInfo.Initiator as BasePlayer).userID)
                    {
                        ProtectionInfo p = null;
                        var attacker = hitInfo.Initiator as BasePlayer;

                        if ((entity as BaseEntity).OwnerID!=attacker.userID) {
                            if (storedData.Players.TryGetValue(attacker.userID, out p))
                            {
                                PunishPlayer(attacker);
                                Log("Punishing " + attacker.displayName.ToString() + " for attempting to blow.");
                                hitInfo.damageTypes.ScaleAll(0f);
                                return hitInfo;
                            }
                        }
                    }
                }
                else if(entity is LootableCorpse && (entity as LootableCorpse).playerSteamID > 76560000000000000L)
                {
                    if (hitInfo.Initiator is BasePlayer)
                    {
                        ProtectionInfo p = null;
                        var attacker = hitInfo.Initiator as BasePlayer;

                        if ((entity as LootableCorpse).playerSteamID!=attacker.userID) {
                            if (storedData.Players.TryGetValue(attacker.userID, out p))
                            {
                                PunishPlayer(attacker);
                                Log("Punishing " + attacker.displayName.ToString() + " for attempting to corpse.");
                                hitInfo.damageTypes.ScaleAll(0f);
                                return hitInfo;
                            }
                        }
                    }

                }
            }
            return null;
        }

        object OnItemPickup(Item item, BasePlayer player)
        {
            if (bProtectionEnabled == true)
            {
                ProtectionInfo p = null;
                var hasProtection = storedData.Players.TryGetValue(player.userID, out p);
                if (!hasProtection) return null;

                if (item.info.category == ItemCategory.Weapon)
                {
                    string minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes).ToString();
                    string parsed_config = GetMessage("cantDo", player.UserIDString);
                    parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                    SPUi(player,parsed_config);
                    return false;
                }
            }
            return null;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (bProtectionEnabled == true)
            {
                ProtectionInfo p = null;
                var hasProtection = storedData.Players.TryGetValue(player.userID, out p);
                if (!hasProtection) return;

                var corpse = entity as LootableCorpse;
                var sleeper = entity as BasePlayer;
                string minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes).ToString();
                string parsed_config = GetMessage("cantDo", player.UserIDString);
                parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                
                //can loot corpses own and bots
                if (corpse != null && corpse.playerSteamID!=player.userID && corpse.playerSteamID> 76560000000000000L)
                {
                    SPUi(player,parsed_config);
                    timer.Once(0.01f, player.EndLooting);
                }
                //can loot friend sleeper
                else if (sleeper != null && canLootFriends && !(bool) (Friends?.CallHook("AreFriends", sleeper.userID,player.userID) ?? false))
                {
                    SPUi(player,parsed_config);
                    timer.Once(0.01f, player.EndLooting);
                }
                //can loot self or bot dropped rust_backpack
                else if (entity.PrefabName.Contains("item_drop"))
                {
                    if ((entity as DroppedItemContainer).playerSteamID == 0)
                    {
                        SPUi(player,parsed_config);
                        timer.Once(0.01f, player.EndLooting);
                    }
                    else if ((entity as DroppedItemContainer).playerSteamID!=player.userID && (entity as DroppedItemContainer).playerSteamID> 76560000000000000L && !(canLootFriends && (bool) (Friends?.CallHook("AreFriends", entity.OwnerID,player.userID) ?? false)))
                    {
                        SPUi(player,parsed_config);
                        timer.Once(0.01f, player.EndLooting);
                    }
                }
                //no loot heli or supply
                else if (!canLootHeli && entity.PrefabName.Contains("heli_crate"))
                {
                    SPUi(player,parsed_config);
                    timer.Once(0.01f, player.EndLooting);
                }
                else if (!canLootDrop && entity.PrefabName.Contains("supply_drop"))
                {
                    SPUi(player,parsed_config);
                    timer.Once(0.01f, player.EndLooting);
                }
                //can loot friends deployables or own
                else if (entity.PrefabName.Contains("deployable") && entity.OwnerID!=0 && entity.OwnerID!=player.userID)
                {
                    if (!(canLootFriendDeployables && (bool) (Friends?.CallHook("AreFriends", entity.OwnerID,player.userID) ?? false)))
                    {
                        SPUi(player,parsed_config);
                        timer.Once(0.01f, player.EndLooting);
                    }
                }
            }
        }

        #endregion

        #region UI

        void DestroyUi(BasePlayer player)
        {
            string gui;
            if(guiInfo.TryGetValue(player.userID, out gui)) CuiHelper.DestroyUi(player, gui);
            guiInfo.Remove(player.userID);
        }

        private void SPUiUser(BasePlayer player, string inputText = "")
        {
            DestroyUi(player);
            guiInfo[player.userID] = CuiHelper.GetGuid();

            if (inputText=="")
            {
                ProtectionInfo p = null;
                if (storedData.Players.TryGetValue(player.userID, out p))
                {
                    inputText = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes).ToString();
                }
            }

            var elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel()
            {
                Image =
                {
                    Color = "0.75 0.75 0.75 0.0"
                },
                RectTransform =
                {
                    AnchorMin = UIIconAnchorMin,
                    AnchorMax = UIIconAnchorMax
                }
            }, "Overlay", guiInfo[player.userID]);

            elements.Add(new CuiElement()
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = nofight_png, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });

            elements.Add(new CuiLabel()
            {
                Text =
                {
                    Text = "NO PVP\n"+inputText+" min",
                    FontSize = 18,
                    Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, panel);

            elements.Add(new CuiButton
            {
                Button =
                {
                    Command = "spinfo.show",
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
            }, panel);

            // Create the UI elements
            CuiHelper.AddUi(player, elements);
        }

        private void SPUi(BasePlayer player, string inputText)
        {
            CuiHelper.DestroyUi(player,"SPUi");
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
                    new CuiElement().Parent = "Overlay", "SPUi"
                }
            };
           
            elements.Add(new CuiElement
            {
                Parent = "SPUi",
                Components =
                    {
                        new CuiTextComponent { Color = "1 1 1 1.0", Text = inputText, FontSize = 30, Align = TextAnchor.MiddleCenter},
                        new CuiOutlineComponent { Distance = "1 1", Color = "0.0 0.0 0.0 1.0" },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        }
                    }
            });
            elements.Add(new CuiButton
            {
                Button =
                {
                    Close = "SPUi",
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
            }, "SPUi");            
            CuiHelper.AddUi(player, elements);
            timer.Once(5f, () =>
            {
                CuiHelper.DestroyUi(player,"SPUi");
            });
        }

        #endregion

        #region API
        private object HasProtection(BasePlayer player)
        {
            ProtectionInfo p = null;
            return storedData.Players.TryGetValue(player.userID, out p);
        }
        #endregion

        #region Helpers
        private void PrintToChatEx(BasePlayer player, string result, string tcolour = "orange")
        {
            PrintToChat(player, "<color=\"" + tcolour + "\">[" + GetMessage("title", player.UserIDString) + "]</color> " + result);
        }

        private void UpdateProtectedListEx(BasePlayer player,bool init=false)
        {
            if (player != null)
            {
                ProtectionInfo p = null;
                if (storedData.Players.TryGetValue(player.userID, out p))
                {
                    if (p.TimeLeft >= 1 && p.TimeLeft <= iTime)
                    {
                        p.TimeLeft = p.TimeLeft - iUpdateTimerInterval;
                        if (init) {
                            int minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes);
                            if (showUIIcon) SPUiUser(player,minutes.ToString());
                        }
                    }
                    else
                    {
                        storedData.Players.Remove(player.userID);
                        storedPlayersData.Players.Add(player.userID);
                        PrintToChatEx(player, GetMessage("tProtectionEnded", player.UserIDString));
                        DestroyUi(player);
                    }
                }
            }
        }

        private void UpdateProtectedList(bool init = false)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                UpdateProtectedListEx(player, init);
            }
        }

        public Int32 UnixTimeStampUTC()
        {
            Int32 unixTimeStamp;
            DateTime currentTime = DateTime.Now;
            DateTime zuluTime = currentTime.ToUniversalTime();
            DateTime unixEpoch = new DateTime(1970, 1, 1);
            unixTimeStamp = (Int32)(zuluTime.Subtract(unixEpoch)).TotalSeconds;
            return unixTimeStamp;
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            return unixTimeStamp > MaxUnixSeconds
               ? UnixEpoch.AddMilliseconds(unixTimeStamp)
               : UnixEpoch.AddSeconds(unixTimeStamp);
        }

        private void RemoveOldUsers()
        {
            int removed = 0;
            new List<ulong>(storedData.Players.Keys).ForEach(u =>
            {
                ulong steamid = u; ProtectionInfo item = null;
                if (storedData.Players.TryGetValue(steamid, out item))
                {
                    if (item.InitTimestamp == 0)
                    {
                        storedData.Players.Remove(steamid);
                        storedPlayersData.Players.Add(steamid);
                        removed++;
                    }
                    else
                    {
                        DateTime compareDate = UnixTimeStampToDateTime(item.InitTimestamp);
                        var days = (DateTime.Now - compareDate).Days;
                        if (days >= iInactiveDays)
                        {
                            storedData.Players.Remove(steamid);
                            storedPlayersData.Players.Add(steamid);
                            removed++;
                        }
                    }
                }
            });
            if (removed >= 1)
            {
                Puts("Removing " + removed.ToString() + " old entries from the protection list.");
                SaveData();
            }
            else
            {
                Puts("Entry list up to date.");
            }
        }


        private void PunishPlayer(BasePlayer player, int new_time = -1, bool message = true)
        {
            ProtectionInfo p = null;
            if (storedData.Players.TryGetValue(player.userID, out p))
            {
                int punish = 0;
                if (new_time != -1)
                {
                    punish = new_time;
                }
                else
                {
                    punish = iPunishment;
                }
                p.TimeLeft = Math.Max(p.TimeLeft - punish,0);
                if (p.TimeLeft <= 0) { UpdateProtectedListEx(player); }
                if (message)
                {
                    int minutes = Convert.ToInt32(TimeSpan.FromSeconds(p.TimeLeft).TotalMinutes);
                    string punishment = Convert.ToInt32(TimeSpan.FromSeconds(punish).TotalMinutes).ToString();
                    string parsed_config = GetMessage("tPunishment", player.UserIDString);
                    parsed_config = parsed_config.Replace("{minutes_revoked}", punishment.ToString());
                    parsed_config = parsed_config.Replace("{minutes_left}", minutes.ToString());
                    PrintToChatEx(player, parsed_config);
                    SPUi(player,parsed_config);
                    if (showUIIcon)
                    {
                        if (minutes > 0) SPUiUser(player, minutes.ToString());
                        else DestroyUi(player);
                    }
                }
            }
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"title", "StartProtection"},
                {"tPunishment", "<color=red>You have been punished for attempting to PVP with Start Protection Enabled!</color>\n{minutes_revoked} minutes revoked.\nYou now have <color=#FF3300>{minutes_left}</color> minutes left before your Start Protection is disabled."},
                {"tFirstSpawn", "Start protection enabled for {minutes_left} minutes, during this time you will not be able to pvp on any level.\nYou can check how much time you have left - /sp time\nTo turn protection off - /sp end" },
                {"tSpawn", "You have {minutes_left} minutes left before your Start Protection is disabled."},
                {"cantDo", "You have PVP Protection and can't loot/pickup that.\n{minutes_left} minutes left before your Start Protection is disabled.\nTo turn protection off - /sp end"},
                {"tProtectionEnded", "Start protection <color=#FF3300>disabled</color>, you are now on your own."},
                {"tNoProtection", "Start protection status is currently <color=#FF3300>disabled</color>."},
                {"tAttackAttempt","The player you are trying to attack has Start Protection enabled and <color=#FF3300>cannot</color> be damaged."},
                {"tDisabled", "Start Protection is currently <color=#FF3300>disabled</color> server-wide."},
                {"tEnabled", "Start Protection has been <color=#66FF66>enabled</color>, new players will now be protected upon spawning."},
                {"tNoAuthLevel", "You <color=#FF3300>do not</color> have access to this command."},
                {"tDBCleared", "You have <color=#FF3300>cleared</color> the Start Protection database."},
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"title", "Защита"},
                {"tPunishment", "<color=red>Вы наказаны за пвп с включенной защитой!</color>\n{minutes_revoked} минут отнято от защиты.\nОсталось {minutes_left} минут до конца защиты."},
                {"tFirstSpawn", "Защита от пвп включена на {minutes_left} минут, в это время нельзя пвп.\n\nСколько времени осталось, наберите - /sp time\n\nВыключить свою защиту - /sp end" },
                {"tSpawn", "Осталось {minutes_left} минут до конца защиты."},
                {"cantDo", "Ты находишься под защитой от пвп и не можешь открыть/взять это.\nОсталось {minutes_left} минут до конца защиты.\nВыключить свою защиту - /sp end "},
                {"tProtectionEnded", "Защита выключена."},
                {"tNoProtection", "Защита на данный момент <color=#FF3300>выключена</color>"},
                {"tAttackAttempt","Игрок находится под защитой, его <color=#FF3300>нельзя</color> убить"},
                {"tDisabled", "Защита <color=#FF3300>выключена</color> для сервера."},
                {"tEnabled", "Защита <color=#66FF66>включена</color>, новые игроки будут защищены."},
                {"tNoAuthLevel", "Нет доступа к этой команде"},
                {"tDBCleared", "База защиты очищена"},
            }, this,"ru");
        }
        string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);

        #endregion
    }
}