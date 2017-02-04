using Oxide.Core;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("StashBlocker", "Ankawi", "1.0.0")]
    [Description("Disables players from building near foundation")]

    class StashBlocker : RustPlugin
    {
        private float radius;

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

            // Make sure gameobject is a BaseEntity
            // Make sure player and entity exists
            if (entity == null || player == null)
                return;

            // Make sure entity is a small stash
            if (!entity.PrefabName.ToLower().Contains("stash") || !entity.PrefabName.ToLower().Contains("small"))
                return;

            // Get all entities in a radius of 5
            List<BaseEntity> closeEntities = new List<BaseEntity>();
            Vis.Entities(gameObject.transform.position, radius, closeEntities);

            if (closeEntities.Any(ent => ent.ShortPrefabName.ToLower().Contains("foundation")))
            {
                PrintToChat(player, GetMsg("CannotPlaceStash", player.UserIDString));
                player.inventory.GiveItem(ItemManager.CreateByItemID(1051155022, 1));
                entity.Kill();
            }        
        }
        #endregion

        #region Localization
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CannotPlaceStash"] = "You cannot place stashes near foundations"
            }, this, "en");
        }
        #endregion

        #region Helpers
        private string GetMsg(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)System.Convert.ChangeType(Config[name], typeof(T));
        #endregion
    }
}
