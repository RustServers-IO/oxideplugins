using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MagicCarpet", "redBDGR", "1.0.0")]
    [Description("Usable magic carpets!")]

    class MagicCarpet : RustPlugin
    {
        private const string permissionName = "magiccarpet.use";
        public static LayerMask collLayers = LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Water", "Default", "AI");
        private Dictionary<string, BaseEntity> users = new Dictionary<string, BaseEntity>();

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command!",
                ["MC start"] = "You have summoned a magic carpet!",
                ["MC End"] = "Your magic carpet has disappeared"
            }, this);
        }

        [ChatCommand("mc")]
        void attachCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionName))
            {
                player.ChatMessage(msg("No Permission", player.UserIDString));
                return;
            }
            if (users.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(msg("MC End", player.UserIDString));
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", player.transform.position);
                Carpet carpet = users[player.UserIDString].GetComponent<Carpet>();
                if (carpet)
                    carpet.Destroy();
                BaseEntity ent = users[player.UserIDString];
                users.Remove(player.UserIDString);
                ent.Kill();
            }
            else
            {
                player.ChatMessage(msg("MC start", player.UserIDString));
                Effect.server.Run("assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab", player.transform.position);
                BaseEntity ent = GameManager.server.CreateEntity("assets/prefabs/deployable/rug/rug.deployed.prefab", player.transform.position);
                ent.Spawn();
                ent.gameObject.AddComponent<Carpet>().player = player;
                users.Add(player.UserIDString, ent);
            }
        }

        private class Carpet : MonoBehaviour
        {
            public BasePlayer player;
            private BaseEntity ent;

            private void Awake()
            {
                ent = gameObject.GetComponent<BaseEntity>();
            }

            private void Update()
            {
                if (player == null) return;
                ent.transform.LookAt(ent.transform.position + player.eyes.HeadRay().direction * 1);
                ent.transform.position = player.transform.position - new Vector3(0f, 0.04f, 0f);
                ent.SendNetworkUpdateImmediate();
            }

            public void Destroy()
            {
                Destroy(this);
            }
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}