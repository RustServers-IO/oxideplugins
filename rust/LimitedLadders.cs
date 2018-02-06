using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LimitedLadders", "VVoid", "1.0.3", ResourceId = 1051)]
    [Description("Controls the placement of ladders where building is blocked")]

    class LimitedLadders : RustPlugin
    {
        const string ladderPrefabs = "assets/bundled/prefabs/items/ladders/";
        readonly int layerMasks = LayerMask.GetMask("Construction");

        bool disableOnlyOnConstructions;
        //bool allowOnExternalWalls; // TODO: Implement

        void Cfg<T>(string key, ref T var)
        {
            if (Config[key] is T)
                var = (T)Config[key];
            else
            {
                Config[key] = var;
                SaveConfig();
            }
        }

        void Init()
        {
            Cfg("Disable Only On Constructions (true/false)", ref disableOnlyOnConstructions);
            //Cfg("Allow On External Walls (true/false)", ref allowOnExternalWalls);

            if (!disableOnlyOnConstructions) Unsubscribe("OnEntityBuilt");

            // English
            lang.RegisterMessages(new Dictionary<string, string> { ["BuildingBlocked"] = "Building is blocked!" }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config["Disable Only On Constructions (true/false)"] = false;
            //Config["Allow On External Walls (true/false)"] = false;

            Config.Remove("BuildingBlockedMsg");
            Config.Remove("DisableOnlyOnConstructions");
        }

        void OnServerInitialized()
        {
            PrefabAttribute.server.GetAll<Construction>().Where(pref => pref.fullName.StartsWith(ladderPrefabs))
                .ToList().ForEach(ladder => ladder.canBypassBuildingPermission = disableOnlyOnConstructions);
        }

        void OnEntityBuilt(HeldEntity heldentity, GameObject obj)
        {
            var player = heldentity.GetOwnerPlayer();
            if (player.CanBuild()) return;

            var entity = obj.GetComponent<BaseCombatEntity>();
            if (!entity || !entity.ShortPrefabName.StartsWith(ladderPrefabs)) return;

            if (Physics.CheckSphere(entity.transform.position, 1.2f, layerMasks))
            {
                entity.Kill(BaseNetworkable.DestroyMode.Gib);
                player.ChatMessage(lang.GetMessage("BuildingBlocked", this, player.UserIDString));
                TryReturnLadder(player, entity);
            }
        }

        void TryReturnLadder(BasePlayer player, BaseCombatEntity entity)
        {
            var item = ItemManager.CreateByName(entity.ShortPrefabName.Replace(".prefab", string.Empty));
            if (item != null) player.GiveItem(item);
        }
    }
}
