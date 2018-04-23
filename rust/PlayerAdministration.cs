using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PlayerAdministration", "ThibmoRozier", "1.2.0", ResourceId = 0)]
    [Description("Allows server admins to moderate users using a GUI from within the game.")]
    public class PlayerAdministration : RustPlugin
    {
        #region GUI
        #region Types
        /// <summary>
        /// UI Color object
        /// </summary>
        public class CuiColor
        {
            public byte R { get; set; } = 255;
            public byte G { get; set; } = 255;
            public byte B { get; set; } = 255;
            public float A { get; set; } = 1f;

            public CuiColor() { }

            public CuiColor(byte red, byte green, byte blue, float alpha = 1f)
            {
                R = red;
                G = green;
                B = blue;
                A = alpha;
            }

            public override string ToString() => $"{(double)R / 255} {(double)G / 255} {(double)B / 255} {A}";
        }

        /// <summary>
        /// Element position object
        /// </summary>
        public class CuiPoint
        {
            public float X { get; set; } = 0f;
            public float Y { get; set; } = 0f;

            public CuiPoint() { }

            public CuiPoint(float x, float y)
            {
                X = x;
                Y = y;
            }

            public override string ToString() => $"{X} {Y}";
        }

        /// <summary>
        /// UI pages to make the switching more humanly readable
        /// </summary>
        private enum UiPage
        {
            Main = 0,
            PlayersOnline,
            PlayersOffline,
            PlayersBanned,
            PlayerPage,
            PlayerPageBanned
        }
        #endregion Types

        #region Defaults
        /// <summary>
        /// Predefined default color set
        /// </summary>
        public class CuiDefaultColors
        {
            public static CuiColor Background { get; } = new CuiColor(240, 240, 240, 0.3f);
            public static CuiColor BackgroundMedium { get; } = new CuiColor(76, 74, 72, 0.83f);
            public static CuiColor BackgroundDark { get; } = new CuiColor(42, 42, 42, 0.93f);
            public static CuiColor Button { get; } = new CuiColor(42, 42, 42, 0.9f);
            public static CuiColor ButtonInactive { get; } = new CuiColor(168, 168, 168, 0.9f);
            public static CuiColor ButtonDecline { get; } = new CuiColor(192, 0, 0, 0.9f);
            public static CuiColor Text { get; } = new CuiColor(0, 0, 0, 1f);
            public static CuiColor TextAlt { get; } = new CuiColor(255, 255, 255, 1f);
            public static CuiColor TextTitle { get; } = new CuiColor(206, 66, 43, 1f);
            public static CuiColor None { get; } = new CuiColor(0, 0, 0, 0f);
        }
        #endregion Defaults

        #region UI object definitions
        /// <summary>
        /// Input field object
        /// </summary>
        public class CuiInputField
        {
            public CuiInputFieldComponent InputField { get; } = new CuiInputFieldComponent();
            public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
            public float FadeOut { get; set; }
        }
        #endregion UI object definitions

        #region Component container
        /// <summary>
        /// Custom version of the CuiElementContainer to add InputFields
        /// </summary>
        public class CustomCuiElementContainer : CuiElementContainer
        {
            public string Add(CuiInputField inputField, string parent = "Hud", string name = null)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                Add(new CuiElement {
                    Name = name,
                    Parent = parent,
                    FadeOut = inputField.FadeOut,
                    Components = {
                        inputField.InputField,
                        inputField.RectTransform
                    }
                });
                return name;
            }
        }
        #endregion Component container

        /// <summary>
        /// Rust UI object
        /// </summary>
        public class Cui
        {
            public const string PARENT_HUD = "Hud";
            public const string PARENT_OVERLAY = "Overlay";

            public string MainPanelName { get; set; }

            private BasePlayer player;
            private CustomCuiElementContainer container = new CustomCuiElementContainer();

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="player">The player this object is meant for</param>
            public Cui(BasePlayer player)
            {
                this.player = player;
            }

            /// <summary>
            /// Add a new panel
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="cursorEnabled">The panel requires the cursor</param>
            /// <param name="color">Image color</param>
            /// <param name="name">The object's name</param>
            /// <param name="png">Image PNG file path</param>
            /// <returns>New object name</returns>
            public string AddPanel(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, bool cursorEnabled, CuiColor color = null,
                                   string name = null, string png = null) =>
                AddPanel(parent, leftBottomAnchor, rightTopAnchor, new CuiPoint(), new CuiPoint(), cursorEnabled, color, name, png);

            /// <summary>
            /// Add a new panel
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="leftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="rightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="cursorEnabled">The panel requires the cursor</param>
            /// <param name="color">Image color</param>
            /// <param name="name">The object's name</param>
            /// <param name="png">Image PNG file path</param>
            /// <returns>New object name</returns>
            public string AddPanel(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiPoint leftBottomOffset, CuiPoint rightTopOffset,
                                   bool cursorEnabled, CuiColor color = null, string name = null, string png = null)
            {
                CuiPanel panel = new CuiPanel() {
                    RectTransform = {
                        AnchorMin = leftBottomAnchor.ToString(),
                        AnchorMax = rightTopAnchor.ToString(),
                        OffsetMin = leftBottomOffset.ToString(),
                        OffsetMax = rightTopOffset.ToString()
                    },
                    CursorEnabled = cursorEnabled
                };

                if (!string.IsNullOrEmpty(png) || (color != null)) {
                    panel.Image = new CuiImageComponent() {
                        Color = color.ToString(),
                        Png = png
                    };
                }

                return container.Add(panel, parent, name);
            }

            /// <summary>
            /// Add a new label
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="color">Text color</param>
            /// <param name="text">Text to show</param>
            /// <param name="name">The object's name</param>
            /// <param name="fontSize">Font size</param>
            /// <param name="align">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddLabel(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiColor color, string text, string name = null,
                                   int fontSize = 14, TextAnchor align = TextAnchor.UpperLeft) =>
                AddLabel(parent, leftBottomAnchor, rightTopAnchor, new CuiPoint(), new CuiPoint(), color, text, name, fontSize, align);

            /// <summary>
            /// Add a new label
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="leftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="rightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="color">Text color</param>
            /// <param name="text">Text to show</param>
            /// <param name="name">The object's name</param>
            /// <param name="fontSize">Font size</param>
            /// <param name="align">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddLabel(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiPoint leftBottomOffset, CuiPoint rightTopOffset,
                                   CuiColor color, string text, string name = null, int fontSize = 14, TextAnchor align = TextAnchor.UpperLeft)
            {
                return container.Add(new CuiLabel() {
                    Text = {
                        Text = text ?? "",
                        FontSize = fontSize,
                        Align = align,
                        Color = color.ToString()
                    },
                    RectTransform = {
                        AnchorMin = leftBottomAnchor.ToString(),
                        AnchorMax = rightTopAnchor.ToString(),
                        OffsetMin = leftBottomOffset.ToString(),
                        OffsetMax = rightTopOffset.ToString()
                    }
                }, parent, name);
            }

            /// <summary>
            /// Add a new button
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="buttonColor">Button background color</param>
            /// <param name="textColor">Text color</param>
            /// <param name="text">Text to show</param>
            /// <param name="command">OnClick event callback command</param>
            /// <param name="close">Panel to close</param>
            /// <param name="name">The object's name</param>
            /// <param name="fontSize">Font size</param>
            /// <param name="align">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddButton(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiColor buttonColor, CuiColor textColor, string text,
                                    string command = "", string close = "", string name = null, int fontSize = 14, TextAnchor align = TextAnchor.MiddleCenter) =>
                AddButton(parent, leftBottomAnchor, rightTopAnchor, new CuiPoint(), new CuiPoint(), buttonColor, textColor, text, command, close, name,
                          fontSize, align);

            /// <summary>
            /// Add a new button
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="leftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="rightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="buttonColor">Button background color</param>
            /// <param name="textColor">Text color</param>
            /// <param name="text">Text to show</param>
            /// <param name="command">OnClick event callback command</param>
            /// <param name="close">Panel to close</param>
            /// <param name="name">The object's name</param>
            /// <param name="fontSize">Font size</param>
            /// <param name="align">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddButton(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiPoint leftBottomOffset, CuiPoint rightTopOffset,
                                    CuiColor buttonColor, CuiColor textColor, string text, string command = "", string close = "", string name = null,
                                    int fontSize = 14, TextAnchor align = TextAnchor.MiddleCenter)
            {
                return container.Add(new CuiButton() {
                    Button = {
                        Command = command ?? "",
                        Close = close ?? "",
                        Color = buttonColor.ToString()
                    },
                    RectTransform = {
                        AnchorMin = leftBottomAnchor.ToString(),
                        AnchorMax = rightTopAnchor.ToString(),
                        OffsetMin = leftBottomOffset.ToString(),
                        OffsetMax = rightTopOffset.ToString()
                    },
                    Text = {
                        Text = text ?? "",
                        FontSize = fontSize,
                        Align = align,
                        Color = textColor.ToString()
                    }
                }, parent, name);
            }

            /// <summary>
            /// Add a new input field
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="color">Text color</param>
            /// <param name="text">Text to show</param>
            /// <param name="charsLimit">Max character count</param>
            /// <param name="command">OnChanged event callback command</param>
            /// <param name="isPassword">Indicates that this input should show password chars</param>
            /// <param name="name">The object's name</param>
            /// <param name="fontSize">Font size</param>
            /// <param name="align">Text alignment</param>
            /// <returns>New object name</returns>
            public string AddInputField(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiColor color, string text = "",
                                        int charsLimit = 100, string command = "", bool isPassword = false, string name = null, int fontSize = 14,
                                        TextAnchor align = TextAnchor.MiddleLeft) =>
                AddInputField(parent, leftBottomAnchor, rightTopAnchor, new CuiPoint(), new CuiPoint(), color, text, charsLimit, command, isPassword, name,
                              fontSize, align);

            /// <summary>
            /// Add a new input field
            /// </summary>
            /// <param name="parent">The parent object name</param>
            /// <param name="leftBottomAnchor">Left(x)-Bottom(y) relative position</param>
            /// <param name="rightTopAnchor">Right(x)-Top(y) relative position</param>
            /// <param name="leftBottomOffset">Left(x)-Bottom(y) relative offset</param>
            /// <param name="rightTopOffset">Right(x)-Top(y) relative offset</param>
            /// <param name="fadeOut">Fade-out time</param>
            /// <param name="color">Text color</param>
            /// <param name="text">Text to show</param>
            /// <param name="charsLimit">Max character count</param>
            /// <param name="command">OnChanged event callback command</param>
            /// <param name="isPassword">Indicates that this input should show password chars</param>
            /// <param name="name">The object's name</param>
            /// <param name="fontSize">Font size</param>
            /// <returns>New object name</returns>
            public string AddInputField(string parent, CuiPoint leftBottomAnchor, CuiPoint rightTopAnchor, CuiPoint leftBottomOffset, CuiPoint rightTopOffset,
                                        CuiColor color, string text = "", int charsLimit = 100, string command = "", bool isPassword = false,
                                        string name = null, int fontSize = 14, TextAnchor align = TextAnchor.MiddleLeft)
            {
                return container.Add(new CuiInputField() {
                    InputField = {
                        Text = text ?? "",
                        FontSize = fontSize,
                        Align = align,
                        Color = color.ToString(),
                        CharsLimit = charsLimit,
                        Command = command ?? "",
                        IsPassword = isPassword
                    },
                    RectTransform = {
                        AnchorMin = leftBottomAnchor.ToString(),
                        AnchorMax = rightTopAnchor.ToString(),
                        OffsetMin = leftBottomOffset.ToString(),
                        OffsetMax = rightTopOffset.ToString()
                    }
                }, parent, name);
            }

            /// <summary>
            /// Draw the UI to the player's client
            /// </summary>
            /// <returns></returns>
            public bool Draw()
            {
                if (!string.IsNullOrEmpty(MainPanelName))
                    return CuiHelper.AddUi(player, container);

                return false;
            }

            /// <summary>
            /// Retrieve the userId of the player this GUI is intended for
            /// </summary>
            /// <returns>Player ID</returns>
            public string GetPlayerId() => player.UserIDString;
        }
        #endregion GUI

        #region Utility methods
        /// <summary>
        /// Get a "page" of entities from a specified list
        /// </summary>
        /// <param name="aList">List of entities</param>
        /// <param name="aPage">Page number (Starting from 0)</param>
        /// <param name="aPageSize">Page size</param>
        /// <returns>List of entities</returns>
        List<T> GetPage<T>(IList<T> aList, int aPage, int aPageSize) => aList.Skip(aPage * aPageSize).Take(aPageSize).ToList();

        /// <summary>
        /// Add a button to the tab menu
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Name of the parent object</param>
        /// <param name="aCaption">Text to show</param>
        /// <param name="aCommand">Button to execute</param>
        /// <param name="aPos">Bounds of the button</param>
        /// <param name="aIndActive">To indicate whether or not the button is active</param>
        private void AddTabMenuBtn(ref Cui aUIObj, string aParent, string aCaption, string aCommand, int aPos, bool aIndActive)
        {
            Vector2 dimensions = new Vector2(0.096f, 0.75f);
            Vector2 offset = new Vector2(0.005f, 0.1f);
            CuiColor btnColor = (aIndActive ? CuiDefaultColors.ButtonInactive : CuiDefaultColors.Button);
            CuiPoint LBAnchor = new CuiPoint(((dimensions.x + offset.x) * aPos) + offset.x, offset.y);
            CuiPoint RTAnchor = new CuiPoint(LBAnchor.X + dimensions.x, offset.y + dimensions.y);
            aUIObj.AddButton(aParent, LBAnchor, RTAnchor, btnColor, CuiDefaultColors.TextAlt, aCaption, (aIndActive ? "" : aCommand));
        }

        /// <summary>
        /// Add a set of user buttons to the parent object
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">Name of the parent object</param>
        /// <param name="aUserList">List of entities</param>
        /// <param name="aCommandFmt">Base format of the command to execute (Will be completed with the user ID</param>
        /// <param name="aPage">User list page</param>
        private void AddPlayerButtons<T>(ref Cui aUIObj, string aParent, ref List<T> aUserList, string aCommandFmt, int aPage)
        {
            List<T> userRange = GetPage(aUserList, aPage, MAX_PLAYER_BUTTONS);
            Vector2 dimensions = new Vector2(0.194f, 0.09f);
            Vector2 offset = new Vector2(0.005f, 0.01f);
            int col = -1;
            int row = 0;
            float margin = 0.12f;

            foreach (T user in userRange) {
                if (++col >= MAX_PLAYER_COLS) {
                    row++;
                    col = 0;
                };

                float calcTop = (1f - margin) - (((dimensions.y + offset.y) * row) + offset.y);
                float calcLeft = ((dimensions.x + offset.x) * col) + offset.x;
                CuiPoint LBAnchor = new CuiPoint(calcLeft, calcTop - dimensions.y);
                CuiPoint RTAnchor = new CuiPoint(calcLeft + dimensions.x, calcTop);

                if (typeof(T) == typeof(BasePlayer)) {
                    string btnText = (user as BasePlayer).displayName;
                    string btnCommand = string.Format(aCommandFmt, (user as BasePlayer).UserIDString);
                    aUIObj.AddButton(aParent, LBAnchor, RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, btnText, btnCommand, "", "", 16);
                } else {
                    string btnText = (user as ServerUsers.User).username;
                    string btnCommand = string.Format(aCommandFmt, (user as ServerUsers.User).steamid);

                    if (string.IsNullOrEmpty(btnText) || UNKNOWN_NAME_LIST.Contains(btnText.ToLower()))
                        btnText = (user as ServerUsers.User).steamid.ToString();

                    aUIObj.AddButton(aParent, LBAnchor, RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, btnText, btnCommand, "", "", 16);
                }
            };
        }

        /// <summary>
        /// Get translated message for the specified key
        /// </summary>
        /// <param name="aKey">Message key</param>
        /// <param name="aPlayerId">Player ID</param>
        /// <param name="args">Optional args</param>
        /// <returns></returns>
        private string _(string aKey, string aPlayerId, params object[] args) => string.Format(lang.GetMessage(aKey, this, aPlayerId), args);

        /// <summary>
        /// Log an error message to the logfile
        /// </summary>
        /// <param name="aMessage"></param>
        private void LogError(string aMessage) => LogToFile("", $"[{DateTime.Now.ToString("hh:mm:ss")}] ERROR > {aMessage}", this);

        /// <summary>
        /// Log an informational message to the logfile
        /// </summary>
        /// <param name="aMessage"></param>
        private void LogInfo(string aMessage) => LogToFile("", $"[{DateTime.Now.ToString("hh:mm:ss")}] INFO > {aMessage}", this);

        /// <summary>
        /// Send a message to a specific player
        /// </summary>
        /// <param name="aPlayer">The player to send the message to</param>
        /// <param name="aMessage">The message to send</param>
        private void SendMessage(ref BasePlayer aPlayer, string aMessage) => rust.SendChatMessage(aPlayer, "", aMessage);

        /// <summary>
        /// Verify if a user has the specified permission
        /// </summary>
        /// <param name="aPlayer">The player</param>
        /// <param name="aPermission"></param>
        /// <returns></returns>
        private bool VerifyPermission(ref BasePlayer aPlayer, string aPermission)
        {
            if (permission.UserHasPermission(aPlayer.UserIDString, aPermission)) // User MUST have the required permission
                return true;

            SendMessage(ref aPlayer, _("Permission Error Text", aPlayer.UserIDString));
            LogError(_("Permission Error Log Text", aPlayer.UserIDString, aPlayer.displayName, aPermission));
            return false;
        }

        /// <summary>
        /// Retrieve server users
        /// </summary>
        /// <param name="aIndOffline">Retrieve the list of sleepers (offline players)</param>
        /// <returns></returns>
        private List<BasePlayer> GetServerUserList(bool aIndOffline = false)
        {
            List<BasePlayer> result = new List<BasePlayer>();

            if (!aIndOffline) {
                Player.Players.ForEach(user => {
                    ServerUsers.User servUser = ServerUsers.Get(user.userID);

                    if (servUser == null || servUser?.group != ServerUsers.UserGroup.Banned)
                        result.Add(user);
                });
            } else {
                Player.Sleepers.ForEach(user => {
                    ServerUsers.User servUser = ServerUsers.Get(user.userID);

                    if (servUser == null || servUser?.group != ServerUsers.UserGroup.Banned)
                        result.Add(user);
                });
            }

            return result;
        }

        /// <summary>
        /// Retrieve server users
        /// </summary>
        /// <returns></returns>
        private List<ServerUsers.User> GetBannedUserList() => ServerUsers.GetAll(ServerUsers.UserGroup.Banned).ToList();

        /// <summary>
        /// Retrieve the target player ID from the arguments and report success
        /// </summary>
        /// <param name="aArg">Argument object</param>
        /// <param name="aTarget">Player ID</param>
        /// <returns></returns>
        private bool GetTargetFromArg(ref ConsoleSystem.Arg aArg, out ulong aTarget)
        {
            aTarget = 0;

            if (!aArg.HasArgs() || !ulong.TryParse(aArg.Args[0], out aTarget))
                return false;

            return true;
        }

        /// <summary>
        /// Retrieve the target player ID and amount from the arguments and report success
        /// </summary>
        /// <param name="aArg">Argument object</param>
        /// <param name="aTarget">Player ID</param>
        /// <param name="aAmount">Amount</param>
        /// <returns></returns>
        private bool GetTargetAmountFromArg(ref ConsoleSystem.Arg aArg, out ulong aTarget, out float aAmount)
        {
            aTarget = 0;
            aAmount = 0;

            if (!aArg.HasArgs(2) || !ulong.TryParse(aArg.Args[0], out aTarget) || !float.TryParse(aArg.Args[1], out aAmount))
                return false;

            return true;
        }
        #endregion Utility methods

        #region GUI build methods
        /// <summary>
        /// Build the tab nav-bar
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aPageType">The active page type</param>
        private void BuildTabMenu(ref Cui aUIObj, UiPage aPageType)
        {
            string uiUserId = aUIObj.GetPlayerId();
            // Add the panels and title label
            string headerPanel = aUIObj.AddPanel(aUIObj.MainPanelName, TabMenuHeaderContainerLBAnchor, TabMenuHeaderContainerRTAnchor, false,
                                                 CuiDefaultColors.None);
            string tabBtnPanel = aUIObj.AddPanel(aUIObj.MainPanelName, TabMenuTabBtnContainerLBAnchor, TabMenuTabBtnContainerRTAnchor, false,
                                                 CuiDefaultColors.Background);
            aUIObj.AddLabel(headerPanel, TabMenuHeaderLblLBAnchor, TabMenuHeaderLblRTAnchor, CuiDefaultColors.TextTitle,
                            "Player Administration by ThibmoRozier", "", 22, TextAnchor.MiddleCenter);
            aUIObj.AddButton(headerPanel, TabMenuCloseBtnLBAnchor, TabMenuCloseBtnRTAnchor, CuiDefaultColors.ButtonDecline, CuiDefaultColors.TextAlt, "X",
                             "padm_closeui", "", null, 22);
            // Add the tab menu buttons
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, _("Main Tab Text", uiUserId), "padm_switchui Main", 0, (aPageType == UiPage.Main ? true : false));
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, _("Online Player Tab Text", uiUserId), "padm_switchui PlayersOnline 0", 1,
                          (aPageType == UiPage.PlayersOnline ? true : false));
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, _("Offline Player Tab Text", uiUserId), "padm_switchui PlayersOffline 0", 2,
                          (aPageType == UiPage.PlayersOffline ? true : false));
            AddTabMenuBtn(ref aUIObj, tabBtnPanel, _("Banned Player Tab Text", uiUserId), "padm_switchui PlayersBanned 0", 3,
                          (aPageType == UiPage.PlayersBanned ? true : false));
        }

        /// <summary>
        /// Build the main-menu
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        private void BuildMainPage(ref Cui aUIObj)
        {
            string uiUserId = aUIObj.GetPlayerId();
            // Add the panels and title
            string panel = aUIObj.AddPanel(aUIObj.MainPanelName, MainPagePanelLBAnchor, MainPagePanelRTAnchor, false, CuiDefaultColors.Background);
            aUIObj.AddLabel(panel, MainPageLblTitleLBAnchor, MainPageLblTitleRTAnchor, CuiDefaultColors.TextAlt, "Main", "", 18, TextAnchor.MiddleLeft);
            // Add the ban by ID group
            aUIObj.AddLabel(panel, MainPageLblBanByIdTitleLBAnchor, MainPageLblBanByIdTitleRTAnchor, CuiDefaultColors.TextTitle,
                            _("Ban By ID Title Text", uiUserId), null, 16, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(panel, MainPageLblBanByIdLBAnchor, MainPageLblBanByIdRTAnchor, CuiDefaultColors.TextAlt, _("Ban By ID Label Text", uiUserId), null,
                            14, TextAnchor.MiddleLeft);
            string panelBanByIdGroup = aUIObj.AddPanel(panel, MainPagePanelBanByIdLBAnchor, MainPagePanelBanByIdRTAnchor, false, CuiDefaultColors.BackgroundDark);
            aUIObj.AddInputField(panelBanByIdGroup, MainPageEdtBanByIdLBAnchor, MainPageEdtBanByIdRTAnchor, CuiDefaultColors.TextAlt, null, 24,
                                 "padm_mainpagebanidinputtext");

            if (configData.EnableBan) {
                aUIObj.AddButton(panel, MainPageBtnBanByIdLBAnchor, MainPageBtnBanByIdRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, "Ban",
                                 "padm_mainpagebanbyid");
            } else {
                aUIObj.AddButton(panel, MainPageBtnBanByIdLBAnchor, MainPageBtnBanByIdRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.TextAlt, "Ban");
            }
        }

        /// <summary>
        /// Build a page of user buttons
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aPageType">The active page type</param>
        /// <param name="aPage">User list page</param>
        private void BuildUserBtnPage(ref Cui aUIObj, UiPage aPageType, int aPage)
        {
            string pageLabel = _("User Button Page Title Text", aUIObj.GetPlayerId());
            string npBtnCommandFmt;
            int userCount;
            string panel = aUIObj.AddPanel(aUIObj.MainPanelName, UserBtnPanelLBAnchor, UserBtnPanelRTAnchor, false, CuiDefaultColors.Background);
            aUIObj.AddLabel(panel, UserBtnLblLBAnchor, UserBtnLblRTAnchor, CuiDefaultColors.TextAlt, pageLabel, "", 18, TextAnchor.MiddleLeft);

            if (aPageType == UiPage.PlayersOnline || aPageType == UiPage.PlayersOffline) {
                BuildUserButtons(ref aUIObj, panel, aPageType, ref aPage, out npBtnCommandFmt, out userCount);
            } else {
                BuildBannedUserButtons(ref aUIObj, panel, ref aPage, out npBtnCommandFmt, out userCount);
            }

            // Decide whether or not to activate the "previous" button
            if (aPage == 0) {
                aUIObj.AddButton(panel, UserBtnPreviousBtnLBAnchor, UserBtnPreviousBtnRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.TextAlt, "<<",
                                 "", "", "", 18);
            } else {
                aUIObj.AddButton(panel, UserBtnPreviousBtnLBAnchor, UserBtnPreviousBtnRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, "<<",
                                 string.Format(npBtnCommandFmt, aPage - 1), "", "", 18);
            };

            // Decide whether or not to activate the "next" button
            if (userCount > MAX_PLAYER_BUTTONS * (aPage + 1)) {
                aUIObj.AddButton(panel, UserBtnNextBtnLBAnchor, UserBtnNextBtnRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt, ">>",
                                 string.Format(npBtnCommandFmt, aPage + 1), "", "", 18);
            } else {
                aUIObj.AddButton(panel, UserBtnNextBtnLBAnchor, UserBtnNextBtnRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.TextAlt, ">>", "", "",
                                 "", 18);
            };
        }

        /// <summary>
        /// Build the current user buttons
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">The active page type</param>
        /// <param name="aPageType">The active page type</param>
        /// <param name="aPage">User list page</param>
        /// <param name="aBtnCommandFmt">Command format for the buttons</param>
        /// <param name="aUserCount">Total user count</param>
        private void BuildUserButtons(ref Cui aUIObj, string aParent, UiPage aPageType, ref int aPage, out string aBtnCommandFmt, out int aUserCount)
        {
            string commandFmt = "padm_switchui PlayerPage {0}";
            List<BasePlayer> userList;

            if (aPageType == UiPage.PlayersOnline) {
                userList = GetServerUserList();
                aBtnCommandFmt = "padm_switchui PlayersOnline {0}";
            } else {
                userList = GetServerUserList(true);
                aBtnCommandFmt = "padm_switchui PlayersOffline {0}";
            }

            aUserCount = userList.Count;

            if ((aPage != 0) && (userList.Count <= MAX_PLAYER_BUTTONS))
                aPage = 0; // Reset page to 0 if user count is lower or equal to max button count

            AddPlayerButtons(ref aUIObj, aParent, ref userList, commandFmt, aPage);
        }

        /// <summary>
        /// Build the banned user buttons
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aParent">The active page type</param>
        /// <param name="aPage">User list page</param>
        /// <param name="aBtnCommandFmt">Command format for the buttons</param>
        /// <param name="aUserCount">Total user count</param>
        private void BuildBannedUserButtons(ref Cui aUIObj, string aParent, ref int aPage, out string aBtnCommandFmt, out int aUserCount)
        {
            string commandFmt = "padm_switchui PlayerPageBanned {0}";
            List<ServerUsers.User> userList = GetBannedUserList();
            aBtnCommandFmt = "padm_switchui PlayersBanned {0}";
            aUserCount = userList.Count;

            if ((aPage != 0) && (userList.Count <= MAX_PLAYER_BUTTONS))
                aPage = 0; // Reset page to 0 if user count is lower or equal to max button count

            AddPlayerButtons(ref aUIObj, aParent, ref userList, commandFmt, aPage);
        }

        /// <summary>
        /// Build the user information and administration page
        /// </summary>
        /// <param name="aUIObj">Cui object</param>
        /// <param name="aPageType">The active page type</param>
        /// <param name="aPlayerId">Player ID (SteamId64)</param>
        private void BuildUserPage(ref Cui aUIObj, UiPage aPageType, ulong aPlayerId)
        {
            string uiUserId = aUIObj.GetPlayerId();
            // Add panels
            string panel = aUIObj.AddPanel(aUIObj.MainPanelName, UserPagePanelLBAnchor, UserPagePanelRTAnchor, false, CuiDefaultColors.Background);
            string infoPanel = aUIObj.AddPanel(panel, UserPageInfoPanelLBAnchor, UserPageInfoPanelRTAnchor, false, CuiDefaultColors.BackgroundMedium);
            string actionPanel = aUIObj.AddPanel(panel, UserPageActionPanelLBAnchor, UserPageActionPanelRTAnchor, false, CuiDefaultColors.BackgroundMedium);
            // Add title labels
            aUIObj.AddLabel(infoPanel, UserPageLblinfoTitleLBAnchor, UserPageLblinfoTitleRTAnchor, CuiDefaultColors.TextTitle,
                            _("Player Info Label Text", uiUserId), "", 14, TextAnchor.MiddleLeft);
            aUIObj.AddLabel(actionPanel, UserPageLblActionTitleLBAnchor, UserPageLblActionTitleRTAnchor, CuiDefaultColors.TextTitle,
                            _("Player Actions Label Text", uiUserId), "", 14, TextAnchor.MiddleLeft);

            if (aPageType == UiPage.PlayerPage) {
                BasePlayer player = BasePlayer.FindByID(aPlayerId) ?? BasePlayer.FindSleeping(aPlayerId);
                bool playerConnected = player.IsConnected;
                string lastCheatStr = _("Never Label Text", uiUserId);
                string authLevel = ServerUsers.Get(aPlayerId)?.group.ToString() ?? "None";

                // Pre-calc last admin cheat
                if (player.lastAdminCheatTime != 0f) {
                    TimeSpan lastCheatSinceStart = new TimeSpan(0, 0, (int)(Time.realtimeSinceStartup - player.lastAdminCheatTime));
                    DateTime lastCheat = DateTime.UtcNow.Subtract(lastCheatSinceStart);
                    lastCheatStr = $"{lastCheat.ToString(@"yyyy\/MM\/dd HH:mm:ss")} UTC";
                };

                aUIObj.AddLabel(panel, UserPageLblLBAnchor, UserPageLblRTAnchor, CuiDefaultColors.TextAlt,
                                _("User Page Title Format", uiUserId, player.displayName, ""), "", 18, TextAnchor.MiddleLeft);
                // Add user info labels
                aUIObj.AddLabel(infoPanel, UserPageLblIdLBAnchor, UserPageLblIdRTAnchor, CuiDefaultColors.TextAlt, _("Id Label Format", uiUserId, aPlayerId,
                                (player.IsDeveloper ? _("Dev Label Text", uiUserId) : "")), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblAuthLBAnchor, UserPageLblAuthRTAnchor, CuiDefaultColors.TextAlt,
                                _("Auth Level Label Format", uiUserId, authLevel), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblConnectLBAnchor, UserPageLblConnectRTAnchor, CuiDefaultColors.TextAlt,
                                _("Connection Label Format", uiUserId, (
                                    playerConnected ? _("Connected Label Text", uiUserId)
                                                    : _("Disconnected Label Text", uiUserId))
                                ), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblSleepLBAnchor, UserPageLblSleepRTAnchor, CuiDefaultColors.TextAlt, _("Status Label Format", uiUserId, (
                                    player.IsSleeping() ? _("Sleeping Label Text", uiUserId)
                                                        : _("Awake Label Text", uiUserId)
                                ), (
                                    player.IsAlive() ? _("Alive Label Text", uiUserId)
                                                     : _("Dead Label Text", uiUserId))
                                ), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblFlagLBAnchor, UserPageLblFlagRTAnchor, CuiDefaultColors.TextAlt, _("Flags Label Format", uiUserId,
                                    (player.IsFlying ? _("Flying Label Text", uiUserId) : ""),
                                    (player.isMounted ? _("Mounted Label Text", uiUserId) : "")
                                ), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblPosLBAnchor, UserPageLblPosRTAnchor, CuiDefaultColors.TextAlt,
                                _("Position Label Format", uiUserId, player.ServerPosition), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblRotLBAnchor, UserPageLblRotRTAnchor, CuiDefaultColors.TextAlt,
                                _("Rotation Label Format", uiUserId, player.GetNetworkRotation()), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblAdminCheatLBAnchor, UserPageLblAdminCheatRTAnchor, CuiDefaultColors.TextAlt,
                                _("Last Admin Cheat Label Format", uiUserId, lastCheatStr), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblIdleLBAnchor, UserPageLblIdleRTAnchor, CuiDefaultColors.TextAlt,
                                _("Idle Time Label Format", uiUserId, Convert.ToInt32(player.IdleTime)), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblHealthLBAnchor, UserPageLblHealthRTAnchor, CuiDefaultColors.TextAlt,
                                _("Health Label Format", uiUserId, player.health), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblCalLBAnchor, UserPageLblCalRTAnchor, CuiDefaultColors.TextAlt,
                                _("Calories Label Format", uiUserId, player.metabolism?.calories?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblHydraLBAnchor, UserPageLblHydraRTAnchor, CuiDefaultColors.TextAlt,
                                _("Hydration Label Format", uiUserId, player.metabolism?.hydration?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblTempLBAnchor, UserPageLblTempRTAnchor, CuiDefaultColors.TextAlt,
                                _("Temp Label Format", uiUserId, player.metabolism?.temperature?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblWetLBAnchor, UserPageLblWetRTAnchor, CuiDefaultColors.TextAlt,
                                _("Wetness Label Format", uiUserId, player.metabolism?.wetness?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblComfortLBAnchor, UserPageLblComfortRTAnchor, CuiDefaultColors.TextAlt,
                                _("Comfort Label Format", uiUserId, player.metabolism?.comfort?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblBleedLBAnchor, UserPageLblBleedRTAnchor, CuiDefaultColors.TextAlt,
                                _("Bleeding Label Format", uiUserId, player.metabolism?.bleeding?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblRads1LBAnchor, UserPageLblRads1RTAnchor, CuiDefaultColors.TextAlt,
                                _("Radiation Label Format", uiUserId, player.metabolism?.radiation_poison?.value), "", 14, TextAnchor.MiddleLeft);
                aUIObj.AddLabel(infoPanel, UserPageLblRads2LBAnchor, UserPageLblRads2RTAnchor, CuiDefaultColors.TextAlt,
                                _("Radiation Protection Label Format", uiUserId, player.RadiationProtection()), "", 14, TextAnchor.MiddleLeft);

                /* Build player action panel */
                if (configData.EnableBan) {
                    aUIObj.AddButton(actionPanel, UserPageBtnBanLBAnchor, UserPageBtnBanRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Ban Button Text", uiUserId), $"padm_banuser {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnBanLBAnchor, UserPageBtnBanRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Ban Button Text", uiUserId));
                }

                if (configData.EnableKill) {
                    aUIObj.AddButton(actionPanel, UserPageBtnKillLBAnchor, UserPageBtnKillRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Kill Button Text", uiUserId), $"padm_killuser {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnKillLBAnchor, UserPageBtnKillRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Kill Button Text", uiUserId));
                }

                if (configData.EnableKick && playerConnected) {
                    aUIObj.AddButton(actionPanel, UserPageBtnKickLBAnchor, UserPageBtnKickRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Kick Button Text", uiUserId), $"padm_kickuser {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnKickLBAnchor, UserPageBtnKickRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Kick Button Text", uiUserId));
                };

                // Add reset buttons
                if (configData.EnableClearInv) {
                    aUIObj.AddButton(actionPanel, UserPageBtnClearInventoryLBAnchor, UserPageBtnClearInventoryRTAnchor, CuiDefaultColors.Button,
                                     CuiDefaultColors.TextAlt, _("Clear Inventory Button Text", uiUserId), $"padm_clearuserinventory {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnClearInventoryLBAnchor, UserPageBtnClearInventoryRTAnchor, CuiDefaultColors.ButtonInactive,
                                     CuiDefaultColors.Text, _("Clear Inventory Button Text", uiUserId));
                }

                if (configData.EnableResetBP) {
                    aUIObj.AddButton(actionPanel, UserPageBtnResetBPLBAnchor, UserPageBtnResetBPRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Reset Blueprints Button Text", uiUserId), $"padm_resetuserblueprints {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnResetBPLBAnchor, UserPageBtnResetBPRTAnchor, CuiDefaultColors.ButtonInactive,
                                     CuiDefaultColors.Text, _("Reset Blueprints Button Text", uiUserId));
                }

                if (configData.EnableResetMetabolism) {
                    aUIObj.AddButton(actionPanel, UserPageBtnResetMetabolismLBAnchor, UserPageBtnResetMetabolismRTAnchor, CuiDefaultColors.Button,
                                     CuiDefaultColors.TextAlt, _("Reset Metabolism Button Text", uiUserId), $"padm_resetusermetabolism {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnResetMetabolismLBAnchor, UserPageBtnResetMetabolismRTAnchor, CuiDefaultColors.ButtonInactive,
                                     CuiDefaultColors.Text, _("Reset Metabolism Button Text", uiUserId));
                }

                // Add hurt buttons
                if (configData.EnableHurt) {
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt25LBAnchor, UserPageBtnHurt25RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Hurt 25 Button Text", uiUserId), $"padm_hurtuser {aPlayerId} 25");
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt50LBAnchor, UserPageBtnHurt50RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Hurt 50 Button Text", uiUserId), $"padm_hurtuser {aPlayerId} 50");
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt75LBAnchor, UserPageBtnHurt75RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Hurt 75 Button Text", uiUserId), $"padm_hurtuser {aPlayerId} 75");
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt100LBAnchor, UserPageBtnHurt100RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Hurt 100 Button Text", uiUserId), $"padm_hurtuser {aPlayerId} 100");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt25LBAnchor, UserPageBtnHurt25RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Hurt 25 Button Text", uiUserId));
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt50LBAnchor, UserPageBtnHurt50RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Hurt 50 Button Text", uiUserId));
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt75LBAnchor, UserPageBtnHurt75RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Hurt 75 Button Text", uiUserId));
                    aUIObj.AddButton(actionPanel, UserPageBtnHurt100LBAnchor, UserPageBtnHurt100RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Hurt 100 Button Text", uiUserId));
                }

                // Add heal buttons
                if (configData.EnableHeal) {
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal25LBAnchor, UserPageBtnHeal25RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Heal 25 Button Text", uiUserId), $"padm_healuser {aPlayerId} 25");
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal50LBAnchor, UserPageBtnHeal50RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Heal 50 Button Text", uiUserId), $"padm_healuser {aPlayerId} 50");
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal75LBAnchor, UserPageBtnHeal75RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Heal 75 Button Text", uiUserId), $"padm_healuser {aPlayerId} 75");
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal100LBAnchor, UserPageBtnHeal100RTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Heal 100 Button Text", uiUserId), $"padm_healuser {aPlayerId} 100");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal25LBAnchor, UserPageBtnHeal25RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Heal 25 Button Text", uiUserId));
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal50LBAnchor, UserPageBtnHeal50RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Heal 50 Button Text", uiUserId));
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal75LBAnchor, UserPageBtnHeal75RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Heal 75 Button Text", uiUserId));
                    aUIObj.AddButton(actionPanel, UserPageBtnHeal100LBAnchor, UserPageBtnHeal100RTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Heal 100 Button Text", uiUserId));
                }
            } else {
                ServerUsers.User serverUser = ServerUsers.Get(aPlayerId);
                aUIObj.AddLabel(panel, UserPageLblLBAnchor, UserPageLblRTAnchor, CuiDefaultColors.TextAlt,
                                _("User Page Title Format", uiUserId, serverUser.username, _("Banned Label Text", uiUserId)), "", 18, TextAnchor.MiddleLeft);
                // Add user info labels
                aUIObj.AddLabel(infoPanel, UserPageLblIdLBAnchor, UserPageLblIdRTAnchor, CuiDefaultColors.TextAlt, _("Id Label Format", uiUserId, aPlayerId, ""),
                                "", 14, TextAnchor.MiddleLeft);

                /* Build player action panel */
                if (configData.EnableUnban) {
                    aUIObj.AddButton(actionPanel, UserPageBtnBanLBAnchor, UserPageBtnBanRTAnchor, CuiDefaultColors.Button, CuiDefaultColors.TextAlt,
                                     _("Unban Button Text", uiUserId), $"padm_unbanuser {aPlayerId}");
                } else {
                    aUIObj.AddButton(actionPanel, UserPageBtnBanLBAnchor, UserPageBtnBanRTAnchor, CuiDefaultColors.ButtonInactive, CuiDefaultColors.Text,
                                     _("Unban Button Text", uiUserId));
                }
            }
        }

        /// <summary>
        /// Initiate the building of the UI page to show
        /// </summary>
        /// <param name="aPlayer"></param>
        /// <param name="aPageType"></param>
        /// <param name="aArg"></param>
        private void BuildUI(BasePlayer aPlayer, UiPage aPageType, string aArg = "")
        {
            // Initiate the new UI and panel
            Cui newUiLib = new Cui(aPlayer);
            newUiLib.MainPanelName = newUiLib.AddPanel(Cui.PARENT_OVERLAY, MainLBAnchor, MainRTAnchor, true, CuiDefaultColors.BackgroundDark, MAIN_PANEL_NAME);
            BuildTabMenu(ref newUiLib, aPageType);

            switch (aPageType) {
                case UiPage.Main: {
                    BuildMainPage(ref newUiLib);
                    break;
                }
                case UiPage.PlayersOnline:
                case UiPage.PlayersOffline:
                case UiPage.PlayersBanned: {
                    int page = 0;

                    if (aArg != "")
                        if (!int.TryParse(aArg, out page))
                            page = 0; // Just to be sure

                    BuildUserBtnPage(ref newUiLib, aPageType, page);
                    break;
                }
                case UiPage.PlayerPage:
                case UiPage.PlayerPageBanned: {
                    ulong playerId = aPlayer.userID;

                    if (aArg != "")
                        if (!ulong.TryParse(aArg, out playerId))
                            playerId = aPlayer.userID; // Just to be sure

                    BuildUserPage(ref newUiLib, aPageType, playerId);
                    break;
                }
            };

            // Cleanup any old/active UI and draw the new one
            CuiHelper.DestroyUi(aPlayer, MAIN_PANEL_NAME);
            newUiLib.Draw();
        }
        #endregion GUI build methods

        #region Config
        private class ConfigData
        {
            [DefaultValue("true")]
            [JsonProperty("Enable kick action")]
            public bool EnableKick { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable ban action")]
            public bool EnableBan { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable unban action")]
            public bool EnableUnban { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable kill action")]
            public bool EnableKill { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable inventory clear action")]
            public bool EnableClearInv { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable blueprint resetaction")]
            public bool EnableResetBP { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable metabolism reset action")]
            public bool EnableResetMetabolism { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable hurt action")]
            public bool EnableHurt { get; set; }
            [DefaultValue("true")]
            [JsonProperty("Enable heal action")]
            public bool EnableHeal { get; set; }
        }
        #endregion

        #region Constants
        private const int MAX_PLAYER_COLS = 5;
        private const int MAX_PLAYER_ROWS = 8;
        private const int MAX_PLAYER_BUTTONS = MAX_PLAYER_COLS * MAX_PLAYER_ROWS;
        private const string MAIN_PANEL_NAME = "PAdm_MainPanel";
        private readonly List<string> UNKNOWN_NAME_LIST = new List<string> { "unnamed", "unknown" };
        /* Define layout */
        // Main panel bounds
        private readonly CuiPoint MainLBAnchor = new CuiPoint(0.03f, 0.15f);
        private readonly CuiPoint MainRTAnchor = new CuiPoint(0.97f, 0.97f);
        // Tab menu bounds
        private readonly CuiPoint TabMenuHeaderContainerLBAnchor = new CuiPoint(0.005f, 0.9085f);
        private readonly CuiPoint TabMenuHeaderContainerRTAnchor = new CuiPoint(0.995f, 1f);
        private readonly CuiPoint TabMenuTabBtnContainerLBAnchor = new CuiPoint(0.005f, 0.83f);
        private readonly CuiPoint TabMenuTabBtnContainerRTAnchor = new CuiPoint(0.995f, 0.90849f);
        private readonly CuiPoint TabMenuHeaderLblLBAnchor = new CuiPoint(0f, 0f);
        private readonly CuiPoint TabMenuHeaderLblRTAnchor = new CuiPoint(1f, 1f);
        private readonly CuiPoint TabMenuCloseBtnLBAnchor = new CuiPoint(0.965f, 0.1f);
        private readonly CuiPoint TabMenuCloseBtnRTAnchor = new CuiPoint(0.997f, 0.9f);
        // Main page bounds
        private readonly CuiPoint MainPagePanelLBAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint MainPagePanelRTAnchor = new CuiPoint(0.995f, 0.817f);
        private readonly CuiPoint MainPageLblTitleLBAnchor = new CuiPoint(0.005f, 0.88f);
        private readonly CuiPoint MainPageLblTitleRTAnchor = new CuiPoint(0.995f, 0.99f);
        private readonly CuiPoint MainPageLblBanByIdTitleLBAnchor = new CuiPoint(0.005f, 0.82f);
        private readonly CuiPoint MainPageLblBanByIdTitleRTAnchor = new CuiPoint(0.995f, 0.87f);
        private readonly CuiPoint MainPageLblBanByIdLBAnchor = new CuiPoint(0.005f, 0.76f);
        private readonly CuiPoint MainPageLblBanByIdRTAnchor = new CuiPoint(0.05f, 0.81f);
        private readonly CuiPoint MainPagePanelBanByIdLBAnchor = new CuiPoint(0.055f, 0.76f);
        private readonly CuiPoint MainPagePanelBanByIdRTAnchor = new CuiPoint(0.305f, 0.81f);
        private readonly CuiPoint MainPageEdtBanByIdLBAnchor = new CuiPoint(0.005f, 0f);
        private readonly CuiPoint MainPageEdtBanByIdRTAnchor = new CuiPoint(0.995f, 1f);
        private readonly CuiPoint MainPageBtnBanByIdLBAnchor = new CuiPoint(0.315f, 0.76f);
        private readonly CuiPoint MainPageBtnBanByIdRTAnchor = new CuiPoint(0.365f, 0.81f);
        // User button page bounds 
        private readonly CuiPoint UserBtnPanelLBAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint UserBtnPanelRTAnchor = new CuiPoint(0.995f, 0.817f);
        private readonly CuiPoint UserBtnLblLBAnchor = new CuiPoint(0.005f, 0.88f);
        private readonly CuiPoint UserBtnLblRTAnchor = new CuiPoint(0.995f, 0.99f);
        private readonly CuiPoint UserBtnPreviousBtnLBAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint UserBtnPreviousBtnRTAnchor = new CuiPoint(0.035f, 0.061875f);
        private readonly CuiPoint UserBtnNextBtnLBAnchor = new CuiPoint(0.96f, 0.01f);
        private readonly CuiPoint UserBtnNextBtnRTAnchor = new CuiPoint(0.995f, 0.061875f);
        // User page panel bounds
        private readonly CuiPoint UserPagePanelLBAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint UserPagePanelRTAnchor = new CuiPoint(0.995f, 0.817f);
        private readonly CuiPoint UserPageInfoPanelLBAnchor = new CuiPoint(0.005f, 0.01f);
        private readonly CuiPoint UserPageInfoPanelRTAnchor = new CuiPoint(0.28f, 0.87f);
        private readonly CuiPoint UserPageActionPanelLBAnchor = new CuiPoint(0.285f, 0.01f);
        private readonly CuiPoint UserPageActionPanelRTAnchor = new CuiPoint(0.995f, 0.87f);
        // User page title label bounds
        private readonly CuiPoint UserPageLblLBAnchor = new CuiPoint(0.005f, 0.88f);
        private readonly CuiPoint UserPageLblRTAnchor = new CuiPoint(0.995f, 0.99f);
        private readonly CuiPoint UserPageLblinfoTitleLBAnchor = new CuiPoint(0.025f, 0.94f);
        private readonly CuiPoint UserPageLblinfoTitleRTAnchor = new CuiPoint(0.975f, 0.99f);
        private readonly CuiPoint UserPageLblActionTitleLBAnchor = new CuiPoint(0.01f, 0.94f);
        private readonly CuiPoint UserPageLblActionTitleRTAnchor = new CuiPoint(0.99f, 0.99f);
        // User page info label bounds
        private readonly CuiPoint UserPageLblIdLBAnchor = new CuiPoint(0.025f, 0.87f);
        private readonly CuiPoint UserPageLblIdRTAnchor = new CuiPoint(0.975f, 0.92f);
        private readonly CuiPoint UserPageLblAuthLBAnchor = new CuiPoint(0.025f, 0.81f);
        private readonly CuiPoint UserPageLblAuthRTAnchor = new CuiPoint(0.975f, 0.86f);
        private readonly CuiPoint UserPageLblConnectLBAnchor = new CuiPoint(0.025f, 0.75f);
        private readonly CuiPoint UserPageLblConnectRTAnchor = new CuiPoint(0.975f, 0.80f);
        private readonly CuiPoint UserPageLblSleepLBAnchor = new CuiPoint(0.025f, 0.69f);
        private readonly CuiPoint UserPageLblSleepRTAnchor = new CuiPoint(0.975f, 0.74f);
        private readonly CuiPoint UserPageLblFlagLBAnchor = new CuiPoint(0.025f, 0.63f);
        private readonly CuiPoint UserPageLblFlagRTAnchor = new CuiPoint(0.975f, 0.68f);
        private readonly CuiPoint UserPageLblPosLBAnchor = new CuiPoint(0.025f, 0.57f);
        private readonly CuiPoint UserPageLblPosRTAnchor = new CuiPoint(0.975f, 0.62f);
        private readonly CuiPoint UserPageLblRotLBAnchor = new CuiPoint(0.025f, 0.51f);
        private readonly CuiPoint UserPageLblRotRTAnchor = new CuiPoint(0.975f, 0.56f);
        private readonly CuiPoint UserPageLblAdminCheatLBAnchor = new CuiPoint(0.025f, 0.45f);
        private readonly CuiPoint UserPageLblAdminCheatRTAnchor = new CuiPoint(0.975f, 0.50f);
        private readonly CuiPoint UserPageLblIdleLBAnchor = new CuiPoint(0.025f, 0.39f);
        private readonly CuiPoint UserPageLblIdleRTAnchor = new CuiPoint(0.975f, 0.44f);
        private readonly CuiPoint UserPageLblHealthLBAnchor = new CuiPoint(0.025f, 0.25f);
        private readonly CuiPoint UserPageLblHealthRTAnchor = new CuiPoint(0.975f, 0.30f);
        private readonly CuiPoint UserPageLblCalLBAnchor = new CuiPoint(0.025f, 0.19f);
        private readonly CuiPoint UserPageLblCalRTAnchor = new CuiPoint(0.5f, 0.24f);
        private readonly CuiPoint UserPageLblHydraLBAnchor = new CuiPoint(0.5f, 0.19f);
        private readonly CuiPoint UserPageLblHydraRTAnchor = new CuiPoint(0.975f, 0.24f);
        private readonly CuiPoint UserPageLblTempLBAnchor = new CuiPoint(0.025f, 0.13f);
        private readonly CuiPoint UserPageLblTempRTAnchor = new CuiPoint(0.5f, 0.18f);
        private readonly CuiPoint UserPageLblWetLBAnchor = new CuiPoint(0.5f, 0.13f);
        private readonly CuiPoint UserPageLblWetRTAnchor = new CuiPoint(0.975f, 0.18f);
        private readonly CuiPoint UserPageLblComfortLBAnchor = new CuiPoint(0.025f, 0.07f);
        private readonly CuiPoint UserPageLblComfortRTAnchor = new CuiPoint(0.5f, 0.12f);
        private readonly CuiPoint UserPageLblBleedLBAnchor = new CuiPoint(0.5f, 0.07f);
        private readonly CuiPoint UserPageLblBleedRTAnchor = new CuiPoint(0.975f, 0.12f);
        private readonly CuiPoint UserPageLblRads1LBAnchor = new CuiPoint(0.025f, 0.01f);
        private readonly CuiPoint UserPageLblRads1RTAnchor = new CuiPoint(0.5f, 0.06f);
        private readonly CuiPoint UserPageLblRads2LBAnchor = new CuiPoint(0.5f, 0.01f);
        private readonly CuiPoint UserPageLblRads2RTAnchor = new CuiPoint(0.975f, 0.06f);
        // User page button bounds
        private readonly CuiPoint UserPageBtnBanLBAnchor = new CuiPoint(0.01f, 0.85f);
        private readonly CuiPoint UserPageBtnBanRTAnchor = new CuiPoint(0.16f, 0.92f);
        private readonly CuiPoint UserPageBtnKillLBAnchor = new CuiPoint(0.01f, 0.76f);
        private readonly CuiPoint UserPageBtnKillRTAnchor = new CuiPoint(0.16f, 0.83f);
        private readonly CuiPoint UserPageBtnKickLBAnchor = new CuiPoint(0.17f, 0.76f);
        private readonly CuiPoint UserPageBtnKickRTAnchor = new CuiPoint(0.32f, 0.83f);
        private readonly CuiPoint UserPageBtnClearInventoryLBAnchor = new CuiPoint(0.01f, 0.67f);
        private readonly CuiPoint UserPageBtnClearInventoryRTAnchor = new CuiPoint(0.16f, 0.74f);
        private readonly CuiPoint UserPageBtnResetBPLBAnchor = new CuiPoint(0.17f, 0.67f);
        private readonly CuiPoint UserPageBtnResetBPRTAnchor = new CuiPoint(0.32f, 0.74f);
        private readonly CuiPoint UserPageBtnResetMetabolismLBAnchor = new CuiPoint(0.33f, 0.67f);
        private readonly CuiPoint UserPageBtnResetMetabolismRTAnchor = new CuiPoint(0.48f, 0.74f);
        private readonly CuiPoint UserPageBtnHurt25LBAnchor = new CuiPoint(0.01f, 0.49f);
        private readonly CuiPoint UserPageBtnHurt25RTAnchor = new CuiPoint(0.16f, 0.56f);
        private readonly CuiPoint UserPageBtnHurt50LBAnchor = new CuiPoint(0.17f, 0.49f);
        private readonly CuiPoint UserPageBtnHurt50RTAnchor = new CuiPoint(0.32f, 0.56f);
        private readonly CuiPoint UserPageBtnHurt75LBAnchor = new CuiPoint(0.33f, 0.49f);
        private readonly CuiPoint UserPageBtnHurt75RTAnchor = new CuiPoint(0.48f, 0.56f);
        private readonly CuiPoint UserPageBtnHurt100LBAnchor = new CuiPoint(0.49f, 0.49f);
        private readonly CuiPoint UserPageBtnHurt100RTAnchor = new CuiPoint(0.64f, 0.56f);
        private readonly CuiPoint UserPageBtnHeal25LBAnchor = new CuiPoint(0.01f, 0.40f);
        private readonly CuiPoint UserPageBtnHeal25RTAnchor = new CuiPoint(0.16f, 0.47f);
        private readonly CuiPoint UserPageBtnHeal50LBAnchor = new CuiPoint(0.17f, 0.40f);
        private readonly CuiPoint UserPageBtnHeal50RTAnchor = new CuiPoint(0.32f, 0.47f);
        private readonly CuiPoint UserPageBtnHeal75LBAnchor = new CuiPoint(0.33f, 0.40f);
        private readonly CuiPoint UserPageBtnHeal75RTAnchor = new CuiPoint(0.48f, 0.47f);
        private readonly CuiPoint UserPageBtnHeal100LBAnchor = new CuiPoint(0.49f, 0.40f);
        private readonly CuiPoint UserPageBtnHeal100RTAnchor = new CuiPoint(0.64f, 0.47f);
        #endregion Constants

        #region Variables
        private ConfigData configData;
        // Format: <userId, text>
        private Dictionary<ulong, string> mainPageBanIdInputText = new Dictionary<ulong, string>();
        #endregion Variables

        #region Hooks
        void Loaded()
        {
            configData = Config.ReadObject<ConfigData>();
            permission.RegisterPermission("playeradministration.show", this);
        }

        void Unload()
        {
            foreach (BasePlayer player in Player.Players) {
                CuiHelper.DestroyUi(player, MAIN_PANEL_NAME);

                if (mainPageBanIdInputText.ContainsKey(player.userID))
                    mainPageBanIdInputText.Remove(player.userID);
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (mainPageBanIdInputText.ContainsKey(player.userID))
                mainPageBanIdInputText.Remove(player.userID);
        }

        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData {
                EnableKick = true,
                EnableBan = true,
                EnableUnban = true,
                EnableKill = true,
                EnableClearInv = true,
                EnableResetBP = true,
                EnableResetMetabolism = true,
                EnableHurt = true,
                EnableHeal = true
            };
            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                { "Permission Error Text", "You do not have the required permissions to use this command." },
                { "Permission Error Log Text", "{0}: Tried to execute a command requiring the '{1}' permission" },
                { "Kick Reason Message Text", "Administrative decision" },
                { "Ban Reason Message Text", "Administrative decision" },

                { "Never Label Text", "Never" },
                { "Banned Label Text", " (Banned)" },
                { "Dev Label Text", " (Developer)" },
                { "Connected Label Text", "Connected" },
                { "Disconnected Label Text", "Disconnected" },
                { "Sleeping Label Text", "Sleeping" },
                { "Awake Label Text", "Awake" },
                { "Alive Label Text", "Alive" },
                { "Dead Label Text", "Dead" },
                { "Flying Label Text", " Flying" },
                { "Mounted Label Text", " Mounted" },

                { "User Button Page Title Text", "Click a username to go to the player's control page" },
                { "User Page Title Format", "Control page for player '{0}'{1}" },

                { "Ban By ID Title Text", "Ban a user by ID" },
                { "Ban By ID Label Text", "User ID:" },
                { "Player Info Label Text", "Player information:" },
                { "Player Actions Label Text", "Player actions:" },

                { "Id Label Format", "ID: {0}{1}" },
                { "Auth Level Label Format", "Auth level: {0}" },
                { "Connection Label Format", "Connection: {0}" },
                { "Status Label Format", "Status: {0} and {1}" },
                { "Flags Label Format", "Flags:{0}{1}" },
                { "Position Label Format", "Position: {0}" },
                { "Rotation Label Format", "Rotation: {0}" },
                { "Last Admin Cheat Label Format", "Last admin cheat: {0}" },
                { "Idle Time Label Format", "Idle time: {0} seconds" },
                { "Health Label Format", "Health: {0}" },
                { "Calories Label Format", "Calories: {0}" },
                { "Hydration Label Format", "Hydration: {0}" },
                { "Temp Label Format", "Temperature: {0}" },
                { "Wetness Label Format", "Wetness: {0}" },
                { "Comfort Label Format", "Comfort: {0}" },
                { "Bleeding Label Format", "Bleeding: {0}" },
                { "Radiation Label Format", "Radiation: {0}" },
                { "Radiation Protection Label Format", "Protection: {0}" },

                { "Main Tab Text", "Main" },
                { "Online Player Tab Text", "Online Players" },
                { "Offline Player Tab Text", "Offline Players" },
                { "Banned Player Tab Text", "Banned Players" },

                { "Clear Inventory Button Text", "Clear Inventory" },
                { "Reset Blueprints Button Text", "Reset Blueprints" },
                { "Reset Metabolism Button Text", "Reset Metabolism" },

                { "Hurt 25 Button Text", "Hurt 25" },
                { "Hurt 50 Button Text", "Hurt 50" },
                { "Hurt 75 Button Text", "Hurt 75" },
                { "Hurt 100 Button Text", "Hurt 100" },

                { "Heal 25 Button Text", "Heal 25" },
                { "Heal 50 Button Text", "Heal 50" },
                { "Heal 75 Button Text", "Heal 75" },
                { "Heal 100 Button Text", "Heal 100" },

                { "Ban Button Text", "Ban" },
                { "Kick Button Text", "Kick" },
                { "Kill Button Text", "Kill" },
                { "Unban Button Text", "Unban" }
            }, this, "en");
        }
        #endregion Hooks

        #region Command Callbacks
        [ChatCommand("padmin")]
        void PlayerManagerUICallback(BasePlayer aPlayer, string aCommand, string[] aArgs)
        {
            if (!VerifyPermission(ref aPlayer, "playeradministration.show"))
                return;

            LogInfo($"{aPlayer.displayName}: Opened the menu");
            BuildUI(aPlayer, UiPage.Main);
        }

        [ConsoleCommand("padm_closeui")]
        void PlayerManagerCloseUICallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            CuiHelper.DestroyUi(arg.Player(), MAIN_PANEL_NAME);

            if (mainPageBanIdInputText.ContainsKey(player.userID))
                mainPageBanIdInputText.Remove(player.userID);
        }

        [ConsoleCommand("padm_switchui")]
        void PlayerManagerSwitchUICallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!VerifyPermission(ref player, "playeradministration.show") || !arg.HasArgs())
                return;

            switch (arg.Args[0].ToLower()) {
                case "playersonline": {
                    BuildUI(player, UiPage.PlayersOnline, (arg.HasArgs(2) ? arg.Args[1] : ""));
                    break;
                }
                case "playersoffline": {
                    BuildUI(player, UiPage.PlayersOffline, (arg.HasArgs(2) ? arg.Args[1] : ""));
                    break;
                }
                case "playersbanned": {
                    BuildUI(player, UiPage.PlayersBanned, (arg.HasArgs(2) ? arg.Args[1] : ""));
                    break;
                }
                case "playerpage": {
                    BuildUI(player, UiPage.PlayerPage, (arg.HasArgs(2) ? arg.Args[1] : ""));
                    break;
                }
                case "playerpagebanned": {
                    BuildUI(player, UiPage.PlayerPageBanned, (arg.HasArgs(2) ? arg.Args[1] : ""));
                    break;
                }
                default: { // Main is the default for everything
                    BuildUI(player, UiPage.Main);
                    break;
                }
            };
        }

        [ConsoleCommand("padm_kickuser")]
        void PlayerManagerKickUserCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;
            
            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableKick)
                return;

            BasePlayer.FindByID(targetId)?.Kick(_("Kick Reason Message Text", targetId.ToString()));
            LogInfo($"{player.displayName}: Kicked user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_banuser")]
        void PlayerManagerBanUserCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableBan)
                return;

            Player.Ban(targetId, _("Ban Reason Message Text", targetId.ToString()));
            LogInfo($"{player.displayName}: Banned user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_mainpagebanbyid")]
        void PlayerManagerMainPageBanByIdCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !mainPageBanIdInputText.ContainsKey(player.userID) ||
                !ulong.TryParse(mainPageBanIdInputText[player.userID], out targetId) || !configData.EnableBan)
                return;

            Player.Ban(targetId, _("Ban Reason Message Text", targetId.ToString()));
            LogInfo($"{player.displayName}: Banned user ID {targetId}");
            BuildUI(player, UiPage.Main);
        }

        [ConsoleCommand("padm_unbanuser")]
        void PlayerManagerUnbanUserCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableUnban)
                return;

            Player.Unban(targetId);
            LogInfo($"{player.displayName}: Unbanned user ID {targetId}");
            BuildUI(player, UiPage.Main);
        }

        [ConsoleCommand("padm_killuser")]
        void PlayerManagerKillUserCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableKill)
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.Die();
            LogInfo($"{player.displayName}: Killed user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_clearuserinventory")]
        void PlayerManagerClearUserInventoryCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableClearInv)
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.inventory.Strip();
            LogInfo($"{player.displayName}: Cleared the inventory of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_resetuserblueprints")]
        void PlayerManagerResetUserBlueprintsCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableResetBP)
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.blueprints.Reset();
            LogInfo($"{player.displayName}: Reset the blueprints of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_resetusermetabolism")]
        void PlayerManagerResetUserMetabolismCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetFromArg(ref arg, out targetId) || !configData.EnableResetMetabolism)
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.metabolism.Reset();
            LogInfo($"{player.displayName}: Reset the metabolism of user ID {targetId}");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_hurtuser")]
        void PlayerManagerHurtUserCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;
            float amount;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetAmountFromArg(ref arg, out targetId, out amount) ||
                !configData.EnableHurt)
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.Hurt(amount);
            LogInfo($"{player.displayName}: Hurt user ID {targetId} for {amount} points");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }

        [ConsoleCommand("padm_healuser")]
        void PlayerManagerHealUserCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            ulong targetId;
            float amount;

            if (!VerifyPermission(ref player, "playeradministration.show") || !GetTargetAmountFromArg(ref arg, out targetId, out amount) ||
                !configData.EnableHeal)
                return;

            (BasePlayer.FindByID(targetId) ?? BasePlayer.FindSleeping(targetId))?.Heal(amount);
            LogInfo($"{player.displayName}: Healed user ID {targetId} for {amount} points");
            BuildUI(player, UiPage.PlayerPage, targetId.ToString());
        }
        #endregion Command Callbacks

        #region Text Update Callbacks
        [ConsoleCommand("padm_mainpagebanidinputtext")]
        void PlayerManagerMainPageBanIdInputTextCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!VerifyPermission(ref player, "playeradministration.show") || !arg.HasArgs()) {
                if (mainPageBanIdInputText.ContainsKey(player.userID))
                    mainPageBanIdInputText.Remove(player.userID);

                return;
            };

            if (mainPageBanIdInputText.ContainsKey(player.userID)) {
                mainPageBanIdInputText[player.userID] = arg.Args[0];
            } else {
                mainPageBanIdInputText.Add(player.userID, arg.Args[0]);
            };
        }
        #endregion Text Update Callbacks
    }
}