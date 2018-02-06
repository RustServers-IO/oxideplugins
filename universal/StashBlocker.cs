using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("StashBlocker", "Ankawi", "1.0.2")]
    [Description("Disables players from building near foundation")]

    class StashBlocker : RustPlugin
    {
        private float radius;
        bool heldItemIsFoundation = false;
        bool heldItemIsStash = false;

        #region Configuration
        protected override void LoadDefaultConfig()
        {
            Config["FoundationRadius"] = radius = GetConfig("FoundationRadius", 5.0f);
            SaveConfig();
        }
        #endregion

        #region Functions
        private void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            BaseEntity entity = gameObject.ToBaseEntity();

            if (entity == null || player == null)
                return;

            List<BaseEntity> closeEntities = new List<BaseEntity>();
            Vis.Entities(gameObject.transform.position, radius, closeEntities);

            if (entity.PrefabName.ToLower().Contains("foundation"))
            {
                heldItemIsFoundation = true;
                if (heldItemIsFoundation)
                {
                    if (closeEntities.Any(ent => ent.PrefabName.ToLower().Contains("stash")))
                    {
                        PrintToChat(player, GetMsg("CannotPlaceStash", player.UserIDString));
                        entity.Kill();
                    }
                }
                return;
            }

            if (entity.PrefabName.ToLower().Contains("stash"))
            {
                heldItemIsStash = true;
                if (heldItemIsStash)
                {
                    if (closeEntities.Any(ent => ent.ShortPrefabName.ToLower().Contains("foundation")))
                    {
                        PrintToChat(player, GetMsg("CannotPlaceStash", player.UserIDString));
                        player.inventory.GiveItem(ItemManager.CreateByItemID(1051155022, 1));
                        entity.Kill();
                    }
                }
                return;
            }
            else return;
        }
        #endregion

        #region Localization
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CannotPlaceStash"] = "You cannot place your stash here."
            }, this, "en");
        }
        #endregion

        #region Helpers
        private string GetMsg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)System.Convert.ChangeType(Config[name], typeof(T));
        #endregion
    }
}