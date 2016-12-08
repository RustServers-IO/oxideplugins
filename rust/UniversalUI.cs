using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("UniversalUI", "Absolut", "2.0.0", ResourceId = 2226)]

    class UniversalUI : RustPlugin
    {
        [PluginReference]
        Plugin ImageLibrary;

        [PluginReference]
        Plugin BetterChat;

        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";
        bool localimages = true;
        private Dictionary<ulong, screen> UniversalUIInfo = new Dictionary<ulong, screen>();
        class screen
        {
            public int section;
            public int page;
            public bool open;
            public int showSection;
        }
        private bool Debugging;
        private List<ulong> NoInfo = new List<ulong>();
        private Dictionary<ulong, List<string>> OnDelay = new Dictionary<ulong, List<string>>();
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        #region Server Hooks

        void Loaded()
        {
            lang.RegisterMessages(messages, this);
            Debugging = false;
        }

        void Unload()
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                DestroyPlayer(p);
            }
            foreach (var entry in timers)
                entry.Value.Destroy();
            timers.Clear();
        }

        void OnServerInitialized()
        {
            try
            {
                ImageLibrary.Call("isLoaded", null);
            }
            catch (Exception)
            {
                PrintWarning($"ImageLibrary is missing. Unloading {Name} as it will not work without ImageLibrary.");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            LoadVariables();
            RegisterPermissions();
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerInit(p);
            GetAllImages();
            //foreach (var entry in configData.buttons)
            //    messages.Add(entry.Value.name, entry.Value.name);
            timers.Add("info", timer.Once(configData.InfoInterval, () => InfoLoop()));
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player != null)
            {
                //player.Command("bind tab \"UI_DestroyUniversalUI;inventory.toggle\"");
                //player.Command("bind q \"inventory.togglecrafting;UI_DestroyUniversalUI\"");
                //player.Command("bind escape \"UI_DestroyUniversalUI\"");
                if (configData.MenuKeyBinding != "")
                    player.Command($"bind {configData.MenuKeyBinding} \"UI_OpenUniversalUI\"");
                if (configData.InfoInterval != 0)
                    if (configData.MenuKeyBinding != "")
                        GetSendMSG(player, "UIInfo", configData.MenuKeyBinding.ToString());
                    else GetSendMSG(player, "UIInfo1");
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            DestroyUniversalUI(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyPlayer(player);
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChat) return null;
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return null;
            if (OnDelay.ContainsKey(player.userID))
            {
                if (arg.Args[0].ToString() == "quit")
                {
                    OnDelay.Remove(player.userID);
                    GetSendMSG(player, "ExitDelayed");
                    return false;
                }
                foreach (var e in arg.Args)
                    OnDelay[player.userID].Add(e);
                RunCommand(player, OnDelay[player.userID].ToArray());
                return false;
            }
            return null;
        }

        object OnBetterChat(IPlayer iplayer, string message)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return message;
            if (OnDelay.ContainsKey(player.userID))
            {
                if(message[0].ToString() == "quit")
                {
                    OnDelay.Remove(player.userID);
                    GetSendMSG(player, "ExitDelayed");
                    return true;
                }
                foreach (var e in message.Split(' '))
                    OnDelay[player.userID].Add(e);
                RunCommand(player, OnDelay[player.userID].ToArray());
                return true;
            }
            return message;
        }

        //object OnServerCommand(ConsoleSystem.Arg arg)
        //{
        //    var player = arg.connection?.player as BasePlayer;
        //    if (player == null)
        //        return null;
        //    if (OnDelay.ContainsKey(player.userID))
        //    {
        //        List<string> args = new List<string>();
        //        foreach (var e in arg.Args)
        //            OnDelay[player.userID].Add(e);
        //        RunCommand(player, OnDelay[player.userID].ToArray());
        //        return false;
        //    }
        //    return null;
        //}


        private void RunCommand(BasePlayer player, string[] command)
        {
            if (command[0] == "chat.say")
            {
                rust.RunClientCommand(player, $"chat.say", string.Join(" ", command.Skip(1).ToArray()));
                if(Debugging) Puts($"Chat say:");
            }
            else
            {
                rust.RunClientCommand(player, string.Join(" ", command.ToArray()));
                if (Debugging) Puts($"Console Command: {string.Join(" ", command.ToArray())}");
            }
            if (OnDelay.ContainsKey(player.userID))
                OnDelay.Remove(player.userID);
        }

    #endregion

        #region Functions
    [ConsoleCommand("UI_OpenUniversalUI")]
        private void cmdUI_OpenUniversalUI(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            if (UniversalUIInfo.ContainsKey(player.userID))
                if (!UniversalUIInfo[player.userID].open)
                {
                    UniversalUIInfo[player.userID].open = true;
                    OpenUniversalUI(player);
                }
                else
                    DestroyUniversalUI(player);
            else
                OpenUniversalUI(player);
        }

        private void OpenUniversalUI(BasePlayer player)
        {
            if (!UniversalUIInfo.ContainsKey(player.userID))
                UniversalUIInfo.Add(player.userID, new screen { page = 0, section = 0, showSection = 0 });
            UniversalUIPanel(player);
            if (!NoInfo.Contains(player.userID))
                UIInfoPanel(player);
            return;
        }

        [ConsoleCommand("UI_DestroyUniversalUI")]
        private void cmdUI_DestroyUniversalUI(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            DestroyUniversalUI(player);
        }

        [ConsoleCommand("UI_OpenInfoUI")]
        private void cmdUI_OpenInfoUI(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            if (NoInfo.Contains(player.userID))
                NoInfo.Remove(player.userID);
            OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_HideInfo")]
        private void cmdUI_HideInfo(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            if (!NoInfo.Contains(player.userID))
                NoInfo.Add(player.userID);
            CuiHelper.DestroyUi(player, PanelInfo);
            OpenUniversalUI(player);
        }

        [ConsoleCommand("UI_RunConsoleCommand")]
        private void cmdUI_RunConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            var cmd = "";
            if (arg.Args[0] == "delay")
            {
                if (OnDelay.ContainsKey(player.userID))
                    OnDelay.Remove(player.userID);
                List<string> args = new List<string>();
                foreach (var e in arg.Args.Where(e=> e != "delay"))
                    args.Add(e);
                OnDelay.Add(player.userID, args);
                var message = string.Join(" ", arg.Args.Skip(1).ToArray());
                if (arg.Args[1] == "chat.say")
                   message = string.Join(" ", arg.Args.Skip(2).ToArray());
                GetSendMSG(player, "DelayedCMD", message);
                DestroyUniversalUI(player);
            }
            if (arg.Args[0] == "chat.say")
            {
                cmd = string.Join(" ", arg.Args.Skip(1).ToArray());
                rust.RunClientCommand(player, $"chat.say", cmd);
            }
            else
            {
                cmd = string.Join(" ", arg.Args);
                rust.RunClientCommand(player, $"{cmd}");
            }
        }

        [ConsoleCommand("UI_PageTurn")]
        private void cmdUI_PageTurn(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            int page = Convert.ToInt32(arg.Args[0]);
            UniversalUIInfo[player.userID].page = page;
            UIInfoPanel(player);
        }

        [ConsoleCommand("UI_SwitchSection")]
        private void cmdUI_SwitchSection(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            var section = Convert.ToInt32(arg.Args[0]);
            if (section != 0)
            {
                var admin = arg.Args[1];
                if (admin == "false")
                {
                    if (arg.Args.Length > 2)
                        if (!isAllowed(player, false, arg.Args[2]))
                        {
                            GetSendMSG(player, "NotAuth");
                            return;
                        }
                }
                else
                {
                    if (!isAllowed(player, true))
                    {
                        GetSendMSG(player, "NotAuth");
                        return;
                    }
                }
            }
            UniversalUIInfo[player.userID].section = section;
            UniversalUIInfo[player.userID].page = 0;
            UIInfoPanel(player);
        }

        [ConsoleCommand("UI_InfoSectionButtonChange")]
        private void cmdUI_InfoSectionButtonChange(ConsoleSystem.Arg arg)
        {
            var player = arg.connection.player as BasePlayer;
            if (player == null)
                return;
            var section = Convert.ToInt32(arg.Args[0]);
            UniversalUIInfo[player.userID].showSection = section;
            UIInfoPanel(player);
        }



        private void DestroyUniversalUI(BasePlayer player)
        {
            if (UniversalUIInfo.ContainsKey(player.userID))
                if (UniversalUIInfo[player.userID].open)
                    UniversalUIInfo[player.userID].open = false;
            CuiHelper.DestroyUi(player, PanelUUI);
            CuiHelper.DestroyUi(player, PanelInfo);
        }

        private void DestroyPlayer(BasePlayer player)
        {
            if (UniversalUIInfo.ContainsKey(player.userID))
                UniversalUIInfo.Remove(player.userID);
            CuiHelper.DestroyUi(player, PanelUUI);
            CuiHelper.DestroyUi(player, PanelInfo);
            //player.Command("bind tab \"inventory.toggle\"");
            //player.Command("bind q \"inventory.togglecrafting\"");
            //player.Command("bind escape \"\"");
            if (configData.MenuKeyBinding != "")
                player.Command($"bind {configData.MenuKeyBinding} \"\"");
        }

        private string GetLang(string msg)
        {
            if (messages.ContainsKey(msg))
                return lang.GetMessage(msg, this);
            else return msg;
        }

        private void GetSendMSG(BasePlayer player, string message, string arg1 = "", string arg2 = "", string arg3 = "")
        {
            string msg = string.Format(lang.GetMessage(message, this), arg1, arg2, arg3);
            SendReply(player, TitleColor + lang.GetMessage("title", this, player.UserIDString) + "</color>" + MsgColor + msg + "</color>");
        }

        bool isAuth(BasePlayer player)
        {
            if (player.net.connection != null)
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, "UniversalUI.admin"))
                    return false;
            return true;
        }

        private void RegisterPermissions()
        {
            if (!permission.PermissionExists("UniversalUI.admin"))
                permission.RegisterPermission("UniversalUI.admin", this);
            foreach (var entry in configData.buttons)
                if (!string.IsNullOrEmpty(entry.Value.permission) && !permission.PermissionExists("UniversalUI." + entry.Value.permission))
                    permission.RegisterPermission("UniversalUI."+entry.Value.permission, this);
            foreach (var section in configData.sections)
            {
                if (!string.IsNullOrEmpty(section.Value.permission) && !permission.PermissionExists("UniversalUI." + section.Value.permission))
                    permission.RegisterPermission("UniversalUI." + section.Value.permission, this);
                foreach (var page in section.Value.pages)
                    foreach (var button in page.buttons)
                        if (!string.IsNullOrEmpty(button.permission) && !permission.PermissionExists("UniversalUI." + button.permission))
                            permission.RegisterPermission("UniversalUI." + button.permission, this);
            }

        }

        bool isAllowed(BasePlayer player, bool adminonly, string perm = "")
        {
            if (isAuth(player)) return true;
            if (adminonly) return false;
            if (!string.IsNullOrEmpty(perm))
                if (!permission.UserHasPermission(player.UserIDString, "UniversalUI." + perm)) return false;
            return true;
        }

        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (localimages)
                if (skin == 99)
                    return GetImage(shortname, (ulong)ResourceId);
                else return GetImage(shortname, skin);
            else if (skin == 99)
                return GetImageURL(shortname, (ulong)ResourceId);
            else return GetImageURL(shortname, skin);
        }

        private bool Valid(string name, ulong id = 99)
        {
            if (id == 99)
                return HasImage(name, (ulong)ResourceId);
            return HasImage(name, id);
        }

        public string GetImageURL(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImageURL", shortname, skin);
        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname, skin);


        #endregion

        #region UI Creation

        private string PanelUUI = "PanelUUI";
        private string PanelInfo = "PanelInfo";

        public class UI
        {
            static bool localimage = true;
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent,
                    panelName
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (UI.localimage)
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/generic textures/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/generic textures/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOverlay(ref CuiElementContainer element, string panel, string text, string color, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter, float fadein = 1.0f)
            {
                //if (configdata.DisableUI_FadeIn)
                //    fadein = 0;
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = fadein, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }

            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string text, string colorText, string colorOutline, string DistanceA, string DistanceB, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = DistanceA + " " + DistanceB, Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "1 1 1 0.3" },
            {"light", "0.7 0.7 0.7 0.3" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"brown", "0.3 0.16 0.0 1.0" },
            {"yellow", "0.9 0.9 0.0 1.0" },
            {"orange", "1.0 0.65 0.0 1.0" },
            {"limegreen", "0.42 1.0 0 1.0" },
            {"blue", "0.2 0.6 1.0 1.0" },
            {"red", "1.0 0.1 0.1 1.0" },
            {"white", "1 1 1 1" },
            {"green", "0.28 0.82 0.28 1.0" },
            {"grey", "0.85 0.85 0.85 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
            {"CSorange", "1.0 0.64 0.10 1.0" }
        };

        private Dictionary<string, string> TextColors = new Dictionary<string, string>
        {
            {"limegreen", "<color=#6fff00>" }
        };

        #endregion

        #region UI Panels

        void UniversalUIPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelUUI);
            if (configData.UseButtonPanel)
            {
                var element = UI.CreateElementContainer(PanelUUI, "0 0 0 0", "0.85 0.225", "1.0 0.725", true);
                //UI.CreatePanel(ref element, PanelUUI, UIColors["light"], "0 0", "1 1");
                foreach (var entry in configData.buttons)
                        if (!string.IsNullOrEmpty(entry.Value.command) && isAllowed(player, entry.Value.adminOnly, entry.Value.permission))
                            CreateButtonOnUI(ref element, PanelUUI, entry.Value, entry.Key);
                if (configData.UseInfoPanel)
                    if (NoInfo.Contains(player.userID))
                        UI.CreateButton(ref element, PanelUUI, UIColors["blue"], GetLang("InfoPanel"), 12, "0.2 -.1", "0.8 -.06", "UI_OpenInfoUI");
                CuiHelper.AddUi(player, element);
            }
        }

        private void CreateButtonOnUI(ref CuiElementContainer container, string panelName, MainButton button, int num)
        {
            var pos = CalcButtonPos(num);
            if (Valid($"UUIMainButton{num}"))
                {
                    UI.LoadImage(ref container, panelName, TryForImage($"UUIMainButton{num}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref container, panelName, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                }
            else
            {
                UI.CreateButton(ref container, panelName, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
            }
        }

        void UIInfoPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelInfo);
            if (!UniversalUIInfo.ContainsKey(player.userID))
                UniversalUIInfo.Add(player.userID, new screen { page = 0, section = 0, showSection = 0 });
            CuiElementContainer element = element = UI.CreateElementContainer(PanelInfo, "0 0 0 0", "0.2 0.2", "0.8 0.8", true);
            if (UniversalUIInfo[player.userID].showSection != 0)
                UI.CreateButton(ref element, PanelInfo, UIColors["black"], "<--", 12, "0.17 0.9", "0.2 0.975", $"UI_InfoSectionButtonChange {UniversalUIInfo[player.userID].showSection - 1}");
            if (UniversalUIInfo[player.userID].showSection + 4 < configData.sections.Max(kvp => kvp.Key))
                UI.CreateButton(ref element, PanelInfo, UIColors["black"], "-->", 12, "0.95 0.9", "0.98 0.975", $"UI_InfoSectionButtonChange {UniversalUIInfo[player.userID].showSection + 1}");
            if (UniversalUIInfo[player.userID].section != 0)
                foreach (var entry in configData.sections)
                {
                    if (Debugging) Puts($"No Home Page - Trying Section: {entry.Key.ToString()}");
                    if (entry.Key < UniversalUIInfo[player.userID].showSection) continue;
                    if (entry.Key > UniversalUIInfo[player.userID].showSection + 4) continue;
                    if (entry.Key == UniversalUIInfo[player.userID].section)
                        foreach (var page in entry.Value.pages.Where(kvp => kvp.page == UniversalUIInfo[player.userID].page))
                        {
                            if (Valid($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"))
                                UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPage{entry.Key}-{UniversalUIInfo[player.userID].page}"), "0 0", "1 0.88");
                            else
                            {
                                UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                                UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                            }
                            if (!string.IsNullOrEmpty(page.name))
                                UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.name.ToUpper(), 24, "0.3 0.8", "0.7 0.9");
                            if (!string.IsNullOrEmpty(page.text))
                                UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.text, 12, "0.03 0.2", "0.97 0.65", TextAnchor.UpperLeft);
                            foreach (var button in page.buttons.OrderBy(kvp => kvp.order))
                                if (!string.IsNullOrEmpty(button.command) && isAllowed(player, button.adminOnly, button.permission))
                                {
                                    var pos = CalcInfoButtonPos(button.order);
                                    if (Valid($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"))
                                    {
                                        UI.LoadImage(ref element, PanelInfo, TryForImage($"UUIPageButton{entry.Key}-{UniversalUIInfo[player.userID].page}-{button.order}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                        if (!string.IsNullOrEmpty(button.name))
                                        {
                                            UI.CreateLabel(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                            UI.CreateButton(ref element, PanelInfo, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                                        }
                                        else
                                            UI.CreateButton(ref element, PanelInfo, "0 0 0 0", "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrEmpty(button.name))
                                            UI.CreateButton(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                                        else
                                            UI.CreateButton(ref element, PanelInfo, UIColors["red"], "", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                                    }
                                }
                        }
                }
            //Create Section Buttons at the Top
            if (UniversalUIInfo[player.userID].section == 0)
            {
                if (Valid(configData.HomePage.name))
                    UI.LoadImage(ref element, PanelInfo, TryForImage(configData.HomePage.name), "0 0", "1 0.88");
                else
                {
                    UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                    UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                }
                if (!string.IsNullOrEmpty(configData.HomePage.text))
                    UI.CreateLabel(ref element, PanelInfo, UIColors["white"], configData.HomePage.text, 12, "0.02 0.2", "0.97 0.65");

                UI.CreatePanel(ref element, PanelInfo, UIColors["red"], "0.02 0.9", "0.16 0.975");
                UI.CreateLabel(ref element, PanelInfo, UIColors["white"], configData.HomePage.name.ToUpper(), 12, "0.02 0.9", "0.16 0.975");
            }
            else
            {
                UI.CreateButton(ref element, PanelInfo, UIColors["blue"], configData.HomePage.name.ToUpper(), 12, "0.02 0.9", "0.16 0.975", $"UI_SwitchSection {0} false ");
            }
            foreach (var entry in configData.sections)
            {
                var pos = CalcSectionButtonPos(entry.Key - UniversalUIInfo[player.userID].showSection);
                if (Debugging) Puts($"Trying Section: {entry.Key}");
                var admin = "false";
                if (entry.Value.adminOnly)
                    admin = "true";
                if (entry.Key == UniversalUIInfo[player.userID].section)
                {
                    if (Debugging) Puts($"Section Match: {entry.Key}");
                    var lastpage = configData.sections[UniversalUIInfo[player.userID].section].pages.Count() - 1;
                    var currentpage = UniversalUIInfo[player.userID].page;
                    if (currentpage < lastpage - 1)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Last"), 12, "0.8 0.03", "0.85 0.085", $"UI_PageTurn {lastpage}");
                    }
                    if (currentpage < lastpage)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Next"), 12, "0.74 0.03", "0.79 0.085", $"UI_PageTurn {currentpage + 1}");
                    }
                    if (currentpage > 0)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("Back"), 12, "0.68 0.03", "0.73 0.085", $"UI_PageTurn {currentpage - 1}");
                    }
                    if (currentpage > 1)
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["dark"], GetLang("First"), 12, "0.62 0.03", "0.67 0.085", $"UI_PageTurn {0}");
                    }
                    if (Debugging) Puts($"Past Page Turning Buttons");
                    UI.CreatePanel(ref element, PanelInfo, UIColors["red"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    if (!string.IsNullOrEmpty(entry.Value.name))
                        UI.CreateLabel(ref element, PanelInfo, UIColors["white"], entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    else UI.CreateLabel(ref element, PanelInfo, UIColors["white"], $"Section:{entry.Key.ToString().ToUpper()}", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    if (Debugging) Puts("Passed Select Section");
                }

                else
                {
                    if (Debugging) Puts($"Section Didn't Match: {entry.Key}");
                    if (Valid($"UUISectionButton{entry.Key}"))
                    {
                        UI.LoadImage(ref element, PanelInfo, TryForImage($"UUISectionButton{entry.Key}"), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        if (!string.IsNullOrEmpty(entry.Value.name))
                            UI.CreateButton(ref element, PanelInfo, "0 0 0 0", entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                        else
                            UI.CreateButton(ref element, PanelInfo, "0 0 0 0", $"Section:{entry.Key.ToString().ToUpper()}", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                        if (Debugging) Puts("Section Button Image Try - Non Selected");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(entry.Value.name))
                            UI.CreateButton(ref element, PanelInfo, UIColors["blue"], entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                        else
                            UI.CreateButton(ref element, PanelInfo, UIColors["blue"], $"Section:{entry.Key.ToString().ToUpper()}", 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin} {entry.Value.permission}");
                    }
                }
            }

            if (configData.UseButtonPanel)
                UI.CreateButton(ref element, PanelInfo, UIColors["red"], GetLang("HideInfoPanel"), 12, "0.03 0.03", "0.13 0.085", "UI_HideInfo");
            CuiHelper.AddUi(player, element);
        }


        private float[] CalcButtonPos(int number)
        {
            Vector2 position = new Vector2(0.03f, 0.95f);
            Vector2 dimensions = new Vector2(0.45f, 0.1f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            if (number > 9 && number < 20)
            {
                offsetY = (-0.01f - dimensions.y) * (number - 10);
                offsetX = (0.01f + dimensions.x) * 1;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcSectionButtonPos(int number)
        {
            number--;
            Vector2 position = new Vector2(0.23f, 0.9f);
            Vector2 dimensions = new Vector2(0.13f, 0.075f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 5)
            {
                offsetX = (0.01f + dimensions.x) * number;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private float[] CalcInfoButtonPos(int number)
        {
            Vector2 position = new Vector2(0.85f, 0.75f);
            Vector2 dimensions = new Vector2(0.125f, 0.05f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 10)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        #endregion

        #region Class

        class MainButton
        {
            public string name;
            public string command;
            public string ButtonImage;
            public bool adminOnly;
            public string permission;
        }

        class PageButton
        {
            public string name;
            public string command;
            public string PageButtonImage;
            public bool adminOnly;
            public int order;
            public string permission;
        }

        class Section
        {
            public string name;
            public string SectionButtonimage;
            public bool adminOnly;
            public List<Page> pages = new List<Page>();
            public string permission;
        }

        class Page
        {
            public int page;
            public string name;
            public string text;
            public string PageImage;
            public List<PageButton> buttons = new List<PageButton>();
        }

        #endregion

        #region Misc Commands

        [ChatCommand("ui")]
        private void cmdui(BasePlayer player, string command, string[] args)
        {
            if (UniversalUIInfo.ContainsKey(player.userID) && !UniversalUIInfo[player.userID].open)
            {
                UniversalUIInfo[player.userID].open = true;
                OpenUniversalUI(player);
            }
            else
                OpenUniversalUI(player);
        }


        [ChatCommand("uuidebug")]
        private void cmduuidebug(BasePlayer player, string command, string[] args)
        {
            if (Debugging)
                Debugging = false;
            else Debugging = true;
        }

        [ConsoleCommand("GetAllImages")]
        private void cmdGetAllImages(ConsoleSystem.Arg arg)
        {
            GetAllImages();
        }

        private void GetAllImages()
        {
            if (string.IsNullOrEmpty(configData.HomePage.PageImage)) configData.HomePage = DefaultHomePage;
            AddImage(configData.HomePage.PageImage, configData.HomePage.name, (ulong)ResourceId);
            foreach (var entry in configData.buttons)
                if (!string.IsNullOrEmpty(entry.Value.ButtonImage))
                    AddImage(entry.Value.ButtonImage, $"UUIMainButton{entry.Key}", (ulong)ResourceId);
            foreach (var entry in configData.sections)
            {
                if (!string.IsNullOrEmpty(entry.Value.SectionButtonimage))
                    AddImage(entry.Value.SectionButtonimage, $"UUISectionButton{entry.Key}", (ulong)ResourceId);
                foreach (var page in entry.Value.pages)
                {
                    if (!string.IsNullOrEmpty(page.PageImage))
                        AddImage(page.PageImage, $"UUIPage{entry.Key}-{page.page}", (ulong)ResourceId);
                    foreach (var button in page.buttons)
                        if (!string.IsNullOrEmpty(button.PageButtonImage))
                            AddImage(button.PageButtonImage, $"UUIPageButton{entry.Key}-{page.page}-{button.order}", (ulong)ResourceId);
                }
            }
        }

        private void CheckNewImages()
        {
            if (string.IsNullOrEmpty(configData.HomePage.PageImage)) configData.HomePage = DefaultHomePage;
            if (!Valid(configData.HomePage.name))
                AddImage(configData.HomePage.PageImage, configData.HomePage.name, (ulong)ResourceId);
            foreach (var entry in configData.buttons)
                if (!string.IsNullOrEmpty(entry.Value.ButtonImage))
                    if (!Valid($"UUIMainButton{entry.Key}"))
                        AddImage(entry.Value.ButtonImage, $"UUIMainButton{entry.Key}", (ulong)ResourceId);
            foreach (var entry in configData.sections)
            {
                if (!string.IsNullOrEmpty(entry.Value.SectionButtonimage))
                    if (!Valid($"UUISectionButton{entry.Key}"))
                        AddImage(entry.Value.SectionButtonimage, $"UUISectionButton{entry.Key}", (ulong)ResourceId);
                foreach (var page in entry.Value.pages)
                {
                    if (!string.IsNullOrEmpty(page.PageImage))
                        if (!Valid($"UUIPage{entry.Key}-{page.page}"))
                            AddImage(page.PageImage, $"UUIPage{entry.Key}-{page.page}", (ulong)ResourceId);
                    foreach (var button in page.buttons)
                        if (!string.IsNullOrEmpty(button.PageButtonImage))
                            if (!Valid($"UUIPageButton{entry.Key}-{page.page}-{button.order}"))
                                AddImage(button.PageButtonImage, $"UUIPageButton{entry.Key}-{page.page}-{button.order}", (ulong)ResourceId);
                }
            }
        }

        #endregion

        #region Timers
        private void InfoLoop()
        {
            if (timers.ContainsKey("info"))
            {
                timers["info"].Destroy();
                timers.Remove("info");
            }
            if (configData.InfoInterval == 0) return;
            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (configData.MenuKeyBinding != "")
                    GetSendMSG(p, "UIInfo", configData.MenuKeyBinding.ToString());
                else GetSendMSG(p, "UIInfo1");
            }
            timers.Add("info", timer.Once(configData.InfoInterval * 60, () => InfoLoop()));
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public string MenuKeyBinding { get; set; }
            public Page HomePage = new Page();
            public Dictionary<int, Section> sections = new Dictionary<int, Section>();
            public Dictionary<int, MainButton> buttons = new Dictionary<int, MainButton>();
            public bool UseInfoPanel { get; set; }
            public bool UseButtonPanel { get; set; }
            public int InfoInterval { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        private Page DefaultHomePage = new Page
        {
            PageImage = "http://i.imgur.com/ygJ6m7w.png",
            name = "HomePage",
        };

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MenuKeyBinding = "f5",
                UseInfoPanel = true,
                UseButtonPanel = true,
                InfoInterval = 15,
                HomePage = new Page
                {
                    PageImage = "http://i.imgur.com/ygJ6m7w.png",
                    name = "HomePage",
                    buttons = new List<PageButton> {
                        new PageButton {order = 0 },
                        new PageButton {order = 1 },
                        new PageButton {order = 2 },
                        new PageButton {order = 3 }
                    } },
                sections = new Dictionary<int, Section>
                    {
                    {1, new Section
                    {pages = new List<Page>
                    {
                    { new Page
                {page = 0, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 1, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 2, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } }
                    } } },
                    {2, new Section
                    {pages = new List<Page>
                    {
                    { new Page
                {page = 0, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 1, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 2, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } }
                    } } },
                    {3, new Section
                    {pages = new List<Page>
                    {
                    { new Page
                {page = 0, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 1, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } },
                    { new Page
                {page = 2, buttons = new List<PageButton> {new PageButton
                { order = 0 },new PageButton
                { order = 1 },new PageButton
                { order = 2 },new PageButton
                { order = 3 } } } }
                    } } },
                },

                buttons = new Dictionary<int, MainButton>
                {
                    {0, new MainButton() },
                    {1, new MainButton() },
                    {2, new MainButton() },
                    {3, new MainButton() },
                    {4, new MainButton() },
                    {5, new MainButton() },
                    {6, new MainButton() },
                    {7, new MainButton() },
                    {8, new MainButton() },
                    {9, new MainButton() },
                    {10, new MainButton() },
                    {11, new MainButton() },
                    {12, new MainButton() },
                    {13, new MainButton() },
                    {14, new MainButton() },
                    {15, new MainButton() },
                    {16, new MainButton() },
                    {17, new MainButton() },
                    {18, new MainButton() },
                    {19, new MainButton() },
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Messages
        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "UniversalUI: " },
            {"UIInfo", "This server is running Universal UI. Press <color=yellow>( {0} )</color> or Type <color=yellow>( /ui )</color> to access the Menu."},
            {"UIInfo1", "This server is running Universal UI. Type <color=yellow>( /ui )</color> to access the Menu."},
            {"InfoPanel","Show Info" },
            {"HideInfoPanel", "Hide Info" },
            {"NotAuth", "You are not authorized." },
            {"DelayedCMD", "You have selected a Delayed Command. To finish the command please type a 'parameter' for command: {0}. Or Type 'quit' to exit." },
            {"ExitDelayed", "You have exited the Delayed Command." }
        };
        #endregion

    }
}
