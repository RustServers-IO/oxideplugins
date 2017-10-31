using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using RustNative;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdminPanel", "austinv900", "1.2.8", ResourceId = 2034)]
    internal class AdminPanel : RustPlugin
    {
        [PluginReference]
        private Plugin AdminRadar, EnhancedBanSystem, Godmode, NTeleportation, Vanish;

        private const string permAP = "adminpanel.allowed";

        #region Integrations

        #region GodMode

        private bool IsGod(string UserID)
        {
            if (Godmode == null)
                return false;
            return Godmode.Call<bool>("IsGod", UserID);
        }

        private void ToggleGodmode(BasePlayer player)
        {
            if (Godmode == null)
                return;
            if (IsGod(player.UserIDString))
                Godmode.Call("DisableGodmode", player.IPlayer);
            else
                Godmode.Call("EnableGodmode", player.IPlayer);
            AdminGui(player);
        }

        #endregion GodMode

        #region Vanish

        private bool IsInvisable(BasePlayer player)
        {
            if (Vanish == null)
                return false;
            return Vanish.Call<bool>("IsInvisible", player);
        }

        private void ToggleVanish(BasePlayer player)
        {
            if (Vanish == null)
                return;
            if (!IsInvisable(player))
                Vanish.Call("Disappear", player);
            else
                Vanish.Call("Reappear", player);
            AdminGui(player);
        }

        #endregion Vanish

        #region AdminRadar

        private bool IsRadar(string id)
        {
            if (AdminRadar == null)
                return false;
            return AdminRadar.Call<bool>("IsRadar", id);
        }

        private void ToggleRadar(BasePlayer player)
        {
            if (AdminRadar == null)
                return;
            AdminRadar.Call("ToggleRadar", player);
            AdminGui(player);
        }

        #endregion AdminRadar

        #endregion Integrations

        private void Init()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permAP, this);
            cacheImage();
        }

        #region Configuration

        private string serverBanner;
        private string adminZoneCords;
        private string PanelPosMin;
        private string PanelPosMax;
        private string btnInactColor;
        private string btnActColor;
        private bool ToggleMode;

        protected override void LoadDefaultConfig()
        {
            Config["ServerBannerImage"] = serverBanner = GetConfig("ServerBannerImage", "banner.png");
            Config["AdminZoneCoordinates"] = adminZoneCords = GetConfig("AdminZoneCoordinates", "0;0;0;");
            Config["AdminPanelPosMin"] = PanelPosMin = GetConfig("AdminPanelPosMin", "0.838 0.14");
            Config["AdminPanelPosMax"] = PanelPosMax = GetConfig("AdminPanelPosMax", "0.986 0.36");
            Config["PanelButtonInactiveColor"] = btnInactColor = GetConfig("PanelButtonInactiveColor", "2.55 0 0 0.3");
            Config["PanelButtonActiveColor"] = btnActColor = GetConfig("PanelButtonActiveColor", "0 2.55 0 0.3");
            Config["AdminPanelToggleMode"] = ToggleMode = GetConfig("AdminPanelToggleMode", false);

            SaveConfig();
        }

        #endregion Configuration

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Title"] = "Admin Controller",
                ["Vanish"] = "Vanish",
                ["GodMode"] = "God",
                ["Radar"] = "Radar",
                ["AdminTP"] = "AdminTP"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Title"] = "Controlador",
                ["Vanish"] = "Desaparecer",
                ["GodMode"] = "Dios",
                ["Radar"] = "Radar",
                ["AdminTP"] = "AdminTP"
            }, this, "es");
        }

        #endregion Localization

        #region Hooks

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (ToggleMode == false)
            {
                if (IsAllowed(player.UserIDString, permAP))
                {
                    CuiHelper.DestroyUi(player, "GUIBackground");
                    AdminGui(player);
                }
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (ToggleMode == true)
            {
                if (IsAllowed(player.UserIDString, permAP))
                {
                    player.SendConsoleCommand("bind", "backquote", "+adminpanel", "toggle");
                }
            }
        }

        #endregion Hooks

        #region Command Structure

        [ConsoleCommand("adminpanel")]
        private void ccmdAdminPanel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var args = arg?.Args ?? null;
            if (IsAllowed(player.UserIDString, permAP))
            {
                switch (args[0])
                {
                    case "action":
                        if (args[1] == "vanish")
                        {
                            if (Vanish) ToggleVanish(player);
                        }
                        else if (args[1] == "admintp")
                        {
                            var pos = adminZoneCords.Split(';');
                            var loc = new Vector3(float.Parse(pos[0]), float.Parse(pos[1]), float.Parse(pos[2]));
                            covalence.Players.FindPlayer(player.UserIDString).Teleport(loc.x, loc.y, loc.z);
                        }
                        else if (args[1] == "radar")
                        {
                            if (AdminRadar) ToggleRadar(player);
                        }
                        else if (args[1] == "god")
                        {
                            if (Godmode) ToggleGodmode(player);
                        }
                        break;

                    case "toggle":
                        if (IsAllowed(player.UserIDString, permAP))
                        {
                            if (args[1] == "True" && (ToggleMode == true))
                            {
                                AdminGui(player);
                            }
                            else if (args[1] == "False" && (ToggleMode == true))
                            {
                                CuiHelper.DestroyUi(player, "GUIBackground");
                            }
                        }
                        break;

                    default:
                        SendReply(player, $"[<color=#6275a4>{Name}</color>]: Invalid Syntax");
                        return;
                }
            }
            else { Reply(player, null); }
        }

        [ChatCommand("adminpanel")]
        private void cmdAdminPanel(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player.UserIDString, permAP))
            {
                SendReply(player, $"Unknown command: {command}");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, $"/{command} Show/Hide");
                return;
            }

            switch (args[0])
            {
                case "hide":
                    CuiHelper.DestroyUi(player, "GUIBackground");
                    Reply(player, "Admin Panel Hidden");
                    break;

                case "show":
                    AdminGui(player);
                    Reply(player, "Admin Panel Refreshed/Shown");
                    break;

                case "settp":
                    Vector3 coord = (player.transform.position + new Vector3(0, 1, 0));
                    Config["AdminZoneCoordinates"] = adminZoneCords = $"{coord.x};{coord.y};{coord.z}";
                    Config.Save();
                    Reply(player, string.Format("Admin Zone Coordinents set to current position {0}", (player.transform.position + new Vector3(0, 1, 0)).ToString()));
                    break;

                default:
                    SendReply(player, $"[<color=#6275a4>{Name}</color>]: Invalid Syntax /{command} {args[0]}");
                    return;
            }
        }

        #endregion Command Structure

        #region ImageSaving

        private ImageCache ImageAssets;
        private GameObject AdminPanelObject;

        private void cacheImage()
        {
            AdminPanelObject = new GameObject();
            ImageAssets = AdminPanelObject.AddComponent<ImageCache>();
            ImageAssets.imageFiles.Clear();
            string dataDirectory = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "AdminPanel" + Path.DirectorySeparatorChar + "img" + Path.DirectorySeparatorChar;

            ImageAssets.getImage("AdminPanalImage", dataDirectory + serverBanner);
            download();
        }

        public class ImageCache : MonoBehaviour
        {
            public Dictionary<string, string> imageFiles = new Dictionary<string, string>();

            public List<Queue> queued = new List<Queue>();

            public class Queue
            {
                public string url { get; set; }
                public string name { get; set; }
            }

            private void OnDestroy()
            {
                foreach (var value in imageFiles.Values)
                {
                    FileStorage.server.RemoveEntityNum(uint.MaxValue, Convert.ToUInt32(value));
                }
            }

            public void getImage(string name, string url)
            {
                queued.Add(new Queue
                {
                    url = url,
                    name = name
                });
            }

            private IEnumerator WaitForRequest(Queue queue)
            {
                using (var www = new WWW(queue.url))
                {
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                    {
                        imageFiles.Add(queue.name, FileStorage.server.Store(www.bytes, FileStorage.Type.png, uint.MaxValue).ToString());
                    }
                    else
                    {
                        Debug.Log("Error downloading banner.png . It must be in your oxide/data/AdminPanel/img/banner.png");
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, "oxide.unload AdminPanel");
                    }
                }
            }

            public void process()
            {
                StartCoroutine(WaitForRequest(queued[0]));
            }
        }

        private void download()
        {
            ImageAssets.process();
        }

        public string fetchImage(string name)
        {
            string result;
            if (ImageAssets.imageFiles.TryGetValue(name, out result))
                return result;
            return string.Empty;
        }

        #endregion ImageSaving

        #region GUI Panel

        private void AdminGui(BasePlayer player)
        {
            // Destroy existing UI
            CuiHelper.DestroyUi(player, "GUIBackground");

            var BTNColorVanish = btnInactColor;
            var BTNColorGod = btnInactColor;
            var BTNColorRadar = btnInactColor;

            if (Godmode) { if (IsGod(player.UserIDString)) { BTNColorGod = btnActColor; }; };
            if (Vanish) { if (IsInvisable(player)) { BTNColorVanish = btnActColor; }; };
            if (AdminRadar) { if (IsRadar(player.UserIDString)) { BTNColorRadar = btnActColor; }; };

            var GUIElement = new CuiElementContainer();

            var GUIBackground = GUIElement.Add(new CuiPanel
            {
                Image =
                {
                    Color = "1 1 1 0.02"
                },
                RectTransform =
                {
                    AnchorMin = PanelPosMin,
                    AnchorMax = PanelPosMax
                },
                CursorEnabled = ToggleMode
            }, "Hud", "GUIBackground");
            GUIElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = Lang("Title", player.UserIDString),
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.00 0.36",
                    AnchorMax = "1.00 0.51"
                }
            }, GUIBackground);
            if (AdminRadar && permission.UserHasPermission(player.UserIDString, "adminradar.allowed"))
            {
                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action radar",
                        Color = BTNColorRadar
                    },
                    Text =
                {
                    Text = Lang("Radar", player.UserIDString),
                    FontSize = 8,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 0.21",
                    AnchorMax = "0.48 0.37"
                }
                }, GUIBackground);
            }
            GUIElement.Add(new CuiButton
            {
                Button =
                    {
                        Command = "adminpanel action admintp",
                        Color = "1.28 0 1.28 0.3"
                    },
                Text =
                {
                    Text = Lang("AdminTP", player.UserIDString),
                    FontSize = 8,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                RectTransform =
                {
                    AnchorMin = "0.52 0.21",
                    AnchorMax = "0.95 0.37"
                }
            }, GUIBackground);
            if (Vanish && permission.UserHasPermission(player.UserIDString, "vanish.allowed"))
            {
                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action vanish",
                        Color = BTNColorVanish
                    },
                    Text =
                {
                    Text = Lang("Vanish", player.UserIDString),
                    FontSize = 8,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.05 0.02",
                    AnchorMax = "0.48 0.19"
                }
                }, GUIBackground);
            }
            if (Godmode && permission.UserHasPermission(player.UserIDString, "godmode.allowed"))
            {
                GUIElement.Add(new CuiButton
                {
                    Button =
                    {
                        Command = "adminpanel action god",
                        Color = BTNColorGod
                    },
                    Text =
                {
                    Text = Lang("GodMode", player.UserIDString),
                    FontSize = 8,
                    Align = TextAnchor.MiddleCenter,
                    Color = "1.0 1.0 1.0 1.0"
                },
                    RectTransform =
                {
                    AnchorMin = "0.52 0.02",
                    AnchorMax = "0.95 0.19"
                }
                }, GUIBackground);
            }

            GUIElement.Add(new CuiElement
            {
                Name = "Logo",
                Parent = "GUIBackground",
                Components =
                    {
                        new CuiRawImageComponent { Color = "1.00 1.00 1.00 1", Png = fetchImage("AdminPanalImage"), FadeIn = 0 },
                        new CuiRectTransformComponent { AnchorMin = "0.03 0.49",  AnchorMax = "0.97 0.99" }
                    }
            });

            CuiHelper.AddUi(player, GUIElement);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "GUIBackground");
            }
        }

        #endregion GUI Panel

        #region Helpers

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private bool IsAdmin(string id) => permission.UserHasGroup(id, "admin");

        private bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm) || IsAdmin(id);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Reply(BasePlayer player, string Getmessage)
        { rust.SendChatMessage(player, "[<color=red>AdminPanel</color>]", Getmessage); }

        #endregion Helpers
    }
}
