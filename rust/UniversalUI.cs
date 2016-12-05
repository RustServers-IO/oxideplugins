using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.IO;
using System.Collections;
using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("UniversalUI", "Absolut", "1.1.0", ResourceId = 2226)]

    class UniversalUI : RustPlugin
    {
        string TitleColor = "<color=orange>";
        string MsgColor = "<color=#A9A9A9>";
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
        private Dictionary<string , Dictionary<int, uint>> CachedImages;
        private Dictionary<string, Dictionary<int, Dictionary<int, uint>>> CachedInfoButtonImages;
        static GameObject webObject;
        static SavedImages savedimages;
        static SavedInfoButtons savedinfobuttons;
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();
        private string ImageID;

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
            LoadVariables();
            if (!permission.PermissionExists("UniversalUI.admin"))
                permission.RegisterPermission("UniversalUI.admin", this);
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                OnPlayerInit(p);
            webObject = new GameObject("WebObject");
            savedinfobuttons = webObject.AddComponent<SavedInfoButtons>();
            savedinfobuttons.SetDataDir(this);
            savedimages = webObject.AddComponent<SavedImages>();
            savedimages.SetDataDir(this);
            CachedImages = new Dictionary<string, Dictionary<int, uint>>();
            CachedInfoButtonImages = new Dictionary<string, Dictionary<int, Dictionary<int, uint>>>();
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
                player.Command($"bind {configData.MenuKeyBinding} \"UI_OpenUniversalUI\"");
                if (configData.InfoInterval != 0)
                    GetSendMSG(player, "UIInfo", configData.MenuKeyBinding.ToString());
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
            var admin = arg.Args[1];
            if (admin == "false" || isAuth(player))
            {
                UniversalUIInfo[player.userID].section = section;
                UniversalUIInfo[player.userID].page = 0;
                UIInfoPanel(player);
            }
            else
            {
                GetSendMSG(player, "NotAuth");
            }
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
                if (player.net.connection.authLevel < 2 || !permission.UserHasPermission(player.UserIDString, "UniversalUI.admin"))
                    return false;
            return true;
        }

        #endregion

        #region UI Creation

        private string PanelUUI = "PanelUUI";
        private string PanelInfo = "PanelInfo";

        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panel, string color, string aMin, string aMax, bool cursor = false)
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
                    panel
                }
            };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer element, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                element.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer element, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer element, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public void LoadImage(ref CuiElementContainer element, string panel, string png, string aMin, string aMax)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                });
            }

            static public void LoadURLImage(ref CuiElementContainer element, string panel, string url, string aMin, string aMax)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Url = url },
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
                    if (!string.IsNullOrEmpty(entry.Value.command) && entry.Value.command != "NONE")
                    {
                        if (!entry.Value.adminOnly || isAuth(player))
                            CreateButtonOnUI(ref element, PanelUUI, entry.Value, entry.Key);
                    }
                if (configData.UseInfoPanel)
                    if (NoInfo.Contains(player.userID))
                        UI.CreateButton(ref element, PanelUUI, UIColors["blue"], GetLang("InfoPanel"), 12, "0.2 -.1", "0.8 -.06", "UI_OpenInfoUI");
                CuiHelper.AddUi(player, element);
            }
        }

        private void CreateButtonOnUI(ref CuiElementContainer container, string panelName, MainButton button, int num)
        {
            var pos = CalcButtonPos(num);
            if (CachedImages.ContainsKey("mainbuttons"))
            {
                if (CachedImages["mainbuttons"].ContainsKey(num))
                {
                    UI.LoadImage(ref container, panelName, CachedImages["mainbuttons"][num].ToString(), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateButton(ref container, panelName, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                }
                else
                {
                    UI.CreateButton(ref container, panelName, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                }
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
                    if (Debugging) Puts($"Trying Section: {entry.Key.ToString()} - Name: {entry.Value.name}");
                    if (entry.Key < UniversalUIInfo[player.userID].showSection) continue;
                    if (entry.Key > UniversalUIInfo[player.userID].showSection + 4) continue;
                    if (entry.Key == UniversalUIInfo[player.userID].section)
                        foreach (var page in entry.Value.pages.Where(kvp => kvp.page == UniversalUIInfo[player.userID].page))
                        {
                            if (CachedImages.ContainsKey(entry.Key.ToString()) && CachedImages[entry.Key.ToString()].ContainsKey(UniversalUIInfo[player.userID].page))
                                UI.LoadImage(ref element, PanelInfo, CachedImages[entry.Key.ToString()][UniversalUIInfo[player.userID].page].ToString(), "0 0", "1 0.88");
                            else
                            {
                                UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                                UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                            }
                            UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.name.ToUpper(), 24, "0.3 0.8", "0.7 0.9");
                            UI.CreateLabel(ref element, PanelInfo, UIColors["white"], page.text, 12, "0.03 0.2", "0.97 0.65", TextAnchor.UpperLeft);
                            foreach (var button in page.buttons.OrderBy(kvp => kvp.order))
                            {
                                if (!string.IsNullOrEmpty(button.command) && button.command != "NONE")
                                {
                                    if (!button.adminOnly || isAuth(player))
                                    {
                                        var pos = CalcInfoButtonPos(button.order);
                                        if (CachedInfoButtonImages.ContainsKey(entry.Key.ToString()) && CachedInfoButtonImages[entry.Key.ToString()].ContainsKey(page.page) && CachedInfoButtonImages[entry.Key.ToString()][page.page].ContainsKey(button.order))
                                                {
                                                    UI.LoadImage(ref element, PanelInfo, CachedInfoButtonImages[entry.Key.ToString()][page.page][button.order].ToString(), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                                                    UI.CreateButton(ref element, PanelInfo, "0 0 0 0", button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                                                }
                                                else
                                                {
                                                    UI.CreateButton(ref element, PanelInfo, UIColors["red"], button.name, 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_RunConsoleCommand {button.command}");
                                                }
                                    }
                                }
                            }
                        }
                }
            //Create Section Buttons at the Top
            if (UniversalUIInfo[player.userID].section == 0)
            {
                if (CachedImages.ContainsKey(configData.HomePage.name) && !string.IsNullOrEmpty(CachedImages[configData.HomePage.name][0].ToString()))
                    UI.LoadImage(ref element, PanelInfo, CachedImages[configData.HomePage.name][0].ToString(), "0 0", "1 0.88");
                else
                {
                    UI.CreatePanel(ref element, PanelInfo, UIColors["dark"], "0 0", "1 1");
                    UI.CreatePanel(ref element, PanelInfo, UIColors["light"], "0.01 0.02", "0.99 0.98");
                }
                UI.CreatePanel(ref element, PanelInfo, UIColors["red"], "0.02 0.9", "0.16 0.975");
                UI.CreateLabel(ref element, PanelInfo, UIColors["white"], configData.HomePage.name.ToUpper(), 12, "0.02 0.9", "0.16 0.975");
            }
            else
            {
                UI.CreateButton(ref element, PanelInfo, UIColors["blue"], configData.HomePage.name.ToUpper(), 12, "0.02 0.9", "0.16 0.975", $"UI_SwitchSection {0} false");
            }
            foreach (var entry in configData.sections)
            {
                var pos = CalcSectionButtonPos(entry.Key - UniversalUIInfo[player.userID].showSection);
                if (Debugging) Puts("Section Button");
                var admin = "false";
                if (entry.Value.adminOnly)
                    admin = "true";
                if (entry.Key == UniversalUIInfo[player.userID].section)
                {
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
                    UI.CreatePanel(ref element, PanelInfo, UIColors["red"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    UI.CreateLabel(ref element, PanelInfo, UIColors["white"], entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                    if (Debugging) Puts("Past Selected");
                }

                else
                {
                    if (CachedImages.ContainsKey(entry.Key.ToString()) && CachedImages[entry.Key.ToString()].ContainsKey(99))
                    {
                        UI.LoadImage(ref element, PanelInfo, CachedImages[entry.Key.ToString()][99].ToString(), $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
                        UI.CreateButton(ref element, PanelInfo, "0 0 0 0", entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin}");
                        if (Debugging) Puts("Section Button Image Try - Non Selected");
                    }
                    else
                    {
                        UI.CreateButton(ref element, PanelInfo, UIColors["blue"], entry.Value.name.ToUpper(), 12, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", $"UI_SwitchSection {entry.Key} {admin}");
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
        }

        class PageButton
        {
            public string name;
            public string command;
            public string PageButtonImage;
            public bool adminOnly;
            public int order;
        }

        class Section
        {
            public string name;
            public string SectionButtonimage;
            public bool adminOnly;
            public List<Page> pages = new List<Page>();
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

        #region Images

        class QueueImages
        {
            public string url;
            public string section;
            public int page;
            public QueueImages(string ur, string sec, int pg)
            {
                url = ur;
                section = sec;
                page = pg;
            }
        }

        class SavedImages : MonoBehaviour
        {
            UniversalUI filehandler;
            const int MaxActiveLoads = 3;
            static readonly List<QueueImages> QueueList = new List<QueueImages>();
            static byte activeLoads;
            private MemoryStream stream = new MemoryStream();

            public void SetDataDir(UniversalUI fc) => filehandler = fc;
            public void Add(string url, string section, int page)
            {
                QueueList.Add(new QueueImages(url, section, page));
                if (activeLoads < MaxActiveLoads) Next();
            }

            void Next()
            {
                activeLoads++;
                var qi = QueueList[0];
                QueueList.RemoveAt(0);
                var www = new WWW(qi.url);
                StartCoroutine(WaitForRequest(www, qi));
            }

            private void ClearStream()
            {
                stream.Position = 0;
                stream.SetLength(0);
            }

            IEnumerator WaitForRequest(WWW www, QueueImages info)
            {
                yield return www;

                if (www.error == null)
                {
                    if (!filehandler.CachedImages.ContainsKey(info.section))
                        filehandler.CachedImages.Add(info.section, new Dictionary<int, uint>());
                    if (!filehandler.CachedImages[info.section].ContainsKey(info.page))
                    {
                        ClearStream();
                        stream.Write(www.bytes, 0, www.bytes.Length);
                        uint textureID = FileStorage.server.Store(stream, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                        ClearStream();
                        filehandler.CachedImages[info.section].Add(info.page, textureID);
                    }
                }
                activeLoads--;
                if (QueueList.Count > 0) Next();
            }
        }

        class QueueInfoButtons
        {
            public string url;
            public string section;
            public int page;
            public int order;
            public QueueInfoButtons(string ur, string sec, int pg, int ord)
            {
                url = ur;
                section = sec;
                page = pg;
                order = ord;
            }
        }

        class SavedInfoButtons : MonoBehaviour
        {
            UniversalUI filehandler;
            const int MaxActiveLoads = 3;
            static readonly List<QueueInfoButtons> QueueList = new List<QueueInfoButtons>();
            static byte activeLoads;
            private MemoryStream stream = new MemoryStream();

            public void SetDataDir(UniversalUI fc) => filehandler = fc;
            public void Add(string url, string section, int page, int order)
            {
                QueueList.Add(new QueueInfoButtons(url, section, page, order));
                if (activeLoads < MaxActiveLoads) Next();
            }

            void Next()
            {
                activeLoads++;
                var qi = QueueList[0];
                QueueList.RemoveAt(0);
                var www = new WWW(qi.url);
                StartCoroutine(WaitForRequest(www, qi));
            }

            private void ClearStream()
            {
                stream.Position = 0;
                stream.SetLength(0);
            }

            IEnumerator WaitForRequest(WWW www, QueueInfoButtons info)
            {
                yield return www;

                if (www.error == null)
                {
                    if (!filehandler.CachedInfoButtonImages.ContainsKey(info.section))
                        filehandler.CachedInfoButtonImages.Add(info.section, new Dictionary<int, Dictionary<int, uint>>());
                    if (!filehandler.CachedInfoButtonImages[info.section].ContainsKey(info.page))
                        filehandler.CachedInfoButtonImages[info.section].Add(info.page, new Dictionary<int, uint>());
                    if (!filehandler.CachedInfoButtonImages[info.section][info.page].ContainsKey(info.order))
                    {
                        ClearStream();
                        stream.Write(www.bytes, 0, www.bytes.Length);
                        uint textureID = FileStorage.server.Store(stream, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
                        ClearStream();
                        filehandler.CachedInfoButtonImages[info.section][info.page].Add(info.order, textureID);
                    }
                }
                activeLoads--;
                if (QueueList.Count > 0) Next();
            }
        }


        [ConsoleCommand("GetAllImages")]
        private void cmdGetAllImages(ConsoleSystem.Arg arg)
        {
            GetAllImages();
        }

        private void GetAllImages()
        {
            CachedImages.Clear();
            CachedInfoButtonImages.Clear();
            foreach (var entry in configData.buttons)
                    savedimages.Add(entry.Value.ButtonImage, "mainbuttons", entry.Key);
            foreach (var entry in configData.sections)
            {
                if (entry.Value.SectionButtonimage != "www")
                    savedimages.Add(entry.Value.SectionButtonimage, entry.Key.ToString(), 99);
                foreach (var page in entry.Value.pages)
                {
                    if (page.PageImage != "www")
                        savedimages.Add(page.PageImage, entry.Key.ToString(), page.page);
                    foreach (var button in page.buttons)
                        if (button.PageButtonImage != "www")
                            savedinfobuttons.Add(button.PageButtonImage, entry.Key.ToString(), page.page, button.order);
                }
            }
            if (string.IsNullOrEmpty(configData.HomePage.PageImage)) configData.HomePage = DefaultHomePage;
                savedimages.Add(configData.HomePage.PageImage, configData.HomePage.name, 0);
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
                GetSendMSG(p, "UIInfo", configData.MenuKeyBinding.ToString());
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
                        new PageButton {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },
                        new PageButton {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },
                        new PageButton {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },
                        new PageButton {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 }
                    } },
                sections = new Dictionary<int, Section>
                    {
                    {1, new Section
                    {SectionButtonimage = "www", name = "First", adminOnly = false, pages = new List<Page>
                    {
                    { new Page
                {page = 0, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } },
                    { new Page
                {page = 1, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } },
                    {new Page
                {page = 2, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 }, new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } }
                    } } },
                    {2, new Section
                    {SectionButtonimage = "www", name = "Second", adminOnly = false, pages = new List<Page>
                    {
                    { new Page
                {page = 0, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } },
                    { new Page
                {page = 1, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } },
                    {new Page
                {page = 2, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 }, new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } }
                    } } },
                    {3, new Section
                    {SectionButtonimage = "www", name = "Third", adminOnly = false, pages = new List<Page>
                    {
                    { new Page
                {page = 0, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } },
                    { new Page
                {page = 1, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } },
                    {new Page
                {page = 2, name = "", text = "NONE", PageImage = "www", buttons = new List<PageButton> {new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 0 }, new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 1 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 2 },new PageButton
                {name = "", command = "NONE", PageButtonImage = "www", adminOnly = false, order = 3 } } } }
                    } } },
                },

                buttons = new Dictionary<int, MainButton>
                {
                    {0, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {1, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {2, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {3, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {4, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {5, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {6, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {7, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {8, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {9, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {10, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {12, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {13, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {14, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {15, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {16, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {17, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {18, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    {19, new MainButton
                {name = "", command = "NONE", ButtonImage = "www", adminOnly = false } },
                    },
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
            {"UIInfo", "This server is running Universal UI. Press <color=yellow>( {0} )</color> to access the Menu."},
            {"InfoPanel","Show Info" },
            {"HideInfoPanel", "Hide Info" },
            {"NotAuth", "You are not authorized." }
        };
        #endregion

    }
}
