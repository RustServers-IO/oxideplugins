using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("BuildingBlockGUI", "wzp", "1.0.0")]
    [Description("Displays GUI to player when he enters or leaves building block without need of Planner")]
    public class BuildingBlockGUI : RustPlugin
    {

        #region Config
        List<ulong> activeUI = new List<ulong>();
        private bool configChanged = false;
        private float configTimerSeconds;
        private bool configUseTimer;

        Timer _timer;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("No configuration file found, generating...");
            Config.Clear();
            LoadVariables();
        }

        void LoadVariables()
        {
            configUseTimer = Convert.ToBoolean(GetConfig("useTimer", true));
            configTimerSeconds = Convert.ToSingle(GetConfig("timerSeconds", 0.5f));
            if (configChanged)
            {
                SaveConfig();
                configChanged = false;
            }
        }

        private object GetConfig(string dataValue, object defaultValue)
        {
            object value = Config[dataValue];
            if (value == null)
            {
                value = defaultValue;
                Config[dataValue] = value;
                configChanged = true;
            }
            return value;
        }

        #endregion

        #region Messages
        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"text", "BUILDING BLOCKED" }

            }, this);
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"text", "СТРОИТЕЛЬСТВО ЗАПРЕЩЕНО" }
            }, this, "ru");
        }
        private string msg(string key, BasePlayer player = null) => lang.GetMessage(key, this, player?.UserIDString);
        #endregion

        #region Oxide hooks

        void Init()
        {
            LoadVariables();
        }

        void OnServerInitialized()
        {
            if (configUseTimer)
            {
                _timer = timer.Repeat(configTimerSeconds, 0, () => PluginTimerTick());
            }
        }

        void Unload()
        {
            if (_timer != null) _timer.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            DestroyUI(player);
        }

        #endregion

        #region UI

        void DestroyUI(BasePlayer player)
        {
            if (!activeUI.Contains(player.userID)) return;
            CuiHelper.DestroyUi(player, "BuildingBlockGUI");
            activeUI.Remove(player.userID);
        }

        void CreateUI(BasePlayer player)
        {
            DestroyUI(player);
            CuiElementContainer container = new CuiElementContainer();
            var panel = container.Add(new CuiPanel()
            {
                Image = { Color = "1 0 0 0.15" },
                RectTransform = { AnchorMin = "0.35 0.11", AnchorMax = "0.63 0.14" }
            }, "Hud", "BuildingBlockGUI");

            CuiElement element = new CuiElement
            {
                Parent = panel,
                Components = {
                    new CuiTextComponent { Text = msg("text",player), FontSize = 15, Color = "1 1 1", Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0" }
                }
            };
            container.Add(element);
            CuiHelper.AddUi(player, container);
            activeUI.Add(player.userID);
        }
        #endregion

        #region Helpers
        void PluginTimerTick()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsBuildingBlocked())
                {
                    CreateUI(player);
                } else
                {
                    DestroyUI(player);
                }
            }
        }

        #endregion
    }
}