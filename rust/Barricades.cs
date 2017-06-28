using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("Barricades", "Xianith", "0.0.2", ResourceId = 2460)]
    [Description("Legacy wooden barricade made out of double stacked sign posts. Can be picked up.")]
    class Barricades : RustPlugin
    {
        static Barricades burrS = null;
        Dictionary<BasePlayer, BaseEntity> barricaders = new Dictionary<BasePlayer, BaseEntity>();
        Dictionary<string, BarricadeGui> BarricadeGUIinfo = new Dictionary<string, BarricadeGui>();

        class BarricadeGui
        {
            public string panel;
            public BaseEntity entity;
        }

        #region Config
        private bool Changed = false;

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

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        // public string pickup = "true";

        void LoadVariables()
        {
            // string pickup = Convert.ToString(GetConfig("Barricades", "Allow Pickup", "true"));

            if (!Changed) return;
            PrintWarning("Configuration file updated.");
            SaveConfig();
            Changed = false;
        }
        #endregion

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DestroyBarrUi(player);
            }
        }

        void Init()
        {
            burrS = this;
            permission.RegisterPermission("barricades.use", this);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            // if (pickup != "true") return;
            if (!input.IsDown(BUTTON.USE)) return;
            if (!barricaders.ContainsKey(player)) return;

            BaseEntity entity = barricaders[player];
            if (!entity || entity == null || !entity.IsValid())
            {
                barricaders.Remove(player);
                return;
            }

            timer.Once(0.001f, () =>
            {
                entity.Kill();
                DestroyBarrUi(player);
                createBarricade(player);
                return;
            });
        }

        void OnEntitySpawned(BaseNetworkable entityn)
        {
            BaseEntity parent = entityn as BaseEntity;

            if (parent.ShortPrefabName != "sign.post.town") return;
            if (parent.skinID != 865089368) return;

            parent.gameObject.AddComponent<BarricadeRadius>();

            var ownerID = parent.OwnerID;

            BasePlayer player = BasePlayer.FindByID(ownerID);

            var ent = GameManager.server.CreateEntity("assets/prefabs/deployable/signs/sign.post.town.prefab", new Vector3(0, -1.05f, 0), Quaternion.Euler(0, 0, 0));

            ent.SetParent(parent);

            parent.SetFlag(BaseEntity.Flags.Busy, true);
            ent.SetFlag(BaseEntity.Flags.Busy, true);

            parent.GetComponent<BaseCombatEntity>().health = 50; //Will be configureable eventually...
            ent.GetComponent<BaseCombatEntity>().startHealth = 50;

            ent.UpdateNetworkGroup();
            ent.SendNetworkUpdateImmediate();
            ent.Spawn();
        }

        class BarricadeRadius : MonoBehaviour
        {
            BaseEntity entity;
            public bool isEnabled;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
                var collider = entity.gameObject.AddComponent<SphereCollider>();
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.radius = 1.5f;
                collider.isTrigger = true;
                isEnabled = true;
            }

            private void OnTriggerEnter(Collider col)
            {
                var player = col.GetComponent<BasePlayer>();
                if (player == null) return;
                if (!player.IsValid()) return;

                if (burrS.barricaders.ContainsKey(player))
                    burrS.barricaders.Remove(player);
                burrS.CreateBarrUi(player, entity);
                burrS.barricaders.Add(player, entity);
            }

            private void OnTriggerExit(Collider col)
            {
                var player = col.GetComponent<BasePlayer>();
                if (player == null) return;
                if (!player.IsValid()) return;

                if (burrS.barricaders.ContainsKey(player))
                    burrS.barricaders.Remove(player);
                burrS.DestroyBarrUi(player);

            }
        }

        void CreateBarrUi(BasePlayer player, BaseEntity entity)
        {
            // if (pickup != "true") return;
            if (BarricadeGUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, BarricadeGUIinfo[player.UserIDString].panel);
                BarricadeGUIinfo.Remove(player.UserIDString);
            }

            var elements = new CuiElementContainer();
            var rpanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0", FadeIn = 1.0f },
                RectTransform = { AnchorMin = "0.4 0.06", AnchorMax = "0.59 0.2" },
            }, "Hud");
            elements.Add(new CuiLabel
            {
                Text = { Text = lang.GetMessage("pickup", this, player.UserIDString), Color = "0.8 0.8 0.8 1", FontSize = 14, Align = TextAnchor.MiddleCenter, FadeIn = 1f },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, rpanel);
            CuiHelper.AddUi(player, elements);
            BarricadeGUIinfo.Add(player.UserIDString, new BarricadeGui() { panel = rpanel, entity = entity });
        }

        void DestroyBarrUi(BasePlayer player)
        {
            if (BarricadeGUIinfo.ContainsKey(player.UserIDString))
            {
                CuiHelper.DestroyUi(player, BarricadeGUIinfo[player.UserIDString].panel);
                BarricadeGUIinfo.Remove(player.UserIDString);
            }
        }

        void createBarricade(BasePlayer player)
        {
            Item newItem = ItemManager.Create(ItemManager.FindItemDefinition("sign.post.town"), 1, 0uL);
            newItem.name = lang.GetMessage("name", this, player.UserIDString);
            newItem.skin = 865089368;
            newItem.blueprintTarget = -1792066367;

            if (!newItem.MoveToContainer(player.inventory.containerMain))
                newItem.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity);
        }

        #region Commands
        [ConsoleCommand("barricade.add")]
        void cmdBarricadeAdd(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (permission.UserHasPermission(player.UserIDString, "barricades.use"))
            {
                createBarricade(player);
                SendReply(player, string.Format(lang.GetMessage("title", this, player.UserIDString) + lang.GetMessage("added", this, player.UserIDString)));
            }
            else
                SendReply(player, string.Format(lang.GetMessage("title", this, player.UserIDString) + lang.GetMessage("noperms", this, player.UserIDString)));
            return;
        }
        #endregion

        #region LangAPI

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"title", "<color=orange>Barricades</color> : "},
                {"name", "Personal Barricade"},
                {"noperms", "You do not have permission to add a barricade to your inventory!"},
                {"added", "1x Barricade added to inventory!"},
                {"pickup", "Press <color=orange>USE</color> to pickup this barricade"}
            }, this);
        }
        #endregion

    }
}