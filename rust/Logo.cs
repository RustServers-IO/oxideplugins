using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.IO;
namespace Oxide.Plugins
{
    [Info("Logo", "Pattrik", "1.0.0")]
    class Logo : RustPlugin
    {
        private string PanelName = "GsAdX1wazasdsHs";

        #region Config Setup
        private string Amax = "0.34 0.105";
        private string Amin = "0.29 0.025";
        private string ImageAddress = "Logo";
        #endregion

        #region Main
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Default configuration file created.");
        }
        void Loaded()
        {
            GetConfig("Picture. Image Link (data or url)", ref ImageAddress);
            GetConfig("Minimal indentation", ref Amin);
            GetConfig("Maximum indentation", ref Amax);
            SaveConfig();
            if (!ImageAddress.ToLower().Contains("http"))
            {
                ImageAddress = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + ImageAddress;
            }
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                CreateButton(player);
            }
        }
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, PanelName);
            }
        }
        void OnPlayerSleepEnded(BasePlayer player) => CreateButton(player);
        #endregion

        #region UI
        private void CreateButton(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelName);
            CuiElementContainer elements = new CuiElementContainer();
            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = "0.00 0.00 0.00 0.00"
                },
                RectTransform = {
                    AnchorMin = Amin,
                    AnchorMax = Amax
                },
                CursorEnabled = false
            }, "Overlay", PanelName);
            elements.Add(new CuiElement
            {
                Parent = PanelName,
                Components =
                {
                    new CuiRawImageComponent {Url = ImageAddress, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                    new CuiRectTransformComponent {AnchorMin = "0 0", AnchorMax = "1 1" }
                }
            });
            CuiHelper.AddUi(player, elements);
        }
        #endregion

        #region Helpers
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
