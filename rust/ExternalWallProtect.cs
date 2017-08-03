using System.Collections.Generic;
using UnityEngine;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("ExternalWallProtect", "redBDGR", "1.0.1", ResourceId = 2576)]
    [Description("Prevent ladders from being able to be placed on external walls")]

    class ExternalWallProtect : RustPlugin
    {
        private const string permissionName = "externalwallprotect.exempt";

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["Deny Crusher"] = "You are not allowed to place a ladder there",
            }, this);
        }

        private object CanBuild(Planner plan, Construction prefab)
        {
            if (prefab.prefabID != 2205372577) return null;
            RaycastHit hit;
            BasePlayer player = plan.GetOwnerPlayer();
            if (permission.UserHasPermission(player.UserIDString, permissionName)) return null;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f)) return null;
            BaseEntity ent = hit.GetEntity();
            if (!ent) return null;
            if (!ent.ShortPrefabName.Contains("external")) return null;
            player.ChatMessage(msg("Deny Crusher", player.UserIDString));
            return false;
        }

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
    }
}