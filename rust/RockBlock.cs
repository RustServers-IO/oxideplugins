using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rock Block", "Nogrod", "1.1.0", ResourceId = 1831)]
    [Description("Blocks players from building in rocks")]
    public class RockBlock : RustPlugin
    {
        private ConfigData configData;
        private const BaseNetworkable.DestroyMode DestroyMode = BaseNetworkable.DestroyMode.None;
        private const string permBypass = "rockblock.bypass";
        private readonly int worldLayer = LayerMask.GetMask("World", "Default");

        private class ConfigData
        {
            public int MaxHeight { get; set; }
            public bool AllowCave { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                MaxHeight = -1,
                AllowCave = false
            };
            Config.WriteObject(config, true);
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DistanceTooHigh"] = "Distance to ground too high: {0}",
                ["PlayerSuspected"] = "{0} is suspected of building {1} inside a rock at {2}!"
            }, this);
        }

        private void Init()
        {
            configData = Config.ReadObject<ConfigData>();
            permission.RegisterPermission(permBypass, this);
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            var player = planner.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, permBypass)) return;

            RaycastHit hitInfo;
            var entity = gameObject.GetComponent<BaseEntity>();
            if (configData.MaxHeight > 0 && Physics.Raycast(new Ray(entity.transform.position, Vector3.down), out hitInfo, float.PositiveInfinity, Rust.Layers.Terrain))
            {
                if (hitInfo.distance > configData.MaxHeight)
                {
                    SendReply(player, string.Format(lang.GetMessage("DistanceTooHigh", this, player.UserIDString), hitInfo.distance));
                    entity.Kill(DestroyMode);
                    return;
                }
            }
            CheckEntity(entity, player);
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            var player = deployer.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, permBypass)) return;

            CheckEntity(entity, player);
        }

        private void CheckEntity(BaseEntity entity, BasePlayer player)
        {
            if (entity == null) return;

            var targets = Physics.RaycastAll(new Ray(entity.transform.position + Vector3.up * 200f, Vector3.down), 250, worldLayer);
            foreach (var hit in targets)
            {
                var collider = hit.collider.GetComponent<MeshCollider>();
                //if (collider != null) SendReply(player, $"Rock: {collider.sharedMesh.name}");
                if (collider == null || !collider.sharedMesh.name.StartsWith("rock_") || !IsInside(hit.collider, entity) && (configData.AllowCave || !IsInCave(entity, player))) continue;

                Puts(lang.GetMessage("PlayerSuspected", this), player.displayName, entity.PrefabName, entity.transform.position); // TODO: Optional logging
                entity.Kill(DestroyMode);
                break;
            }
        }

        private bool IsInCave(BaseEntity entity, BasePlayer player)
        {
            var targets = Physics.RaycastAll(new Ray(entity.transform.position, Vector3.up), 250, worldLayer);
            foreach (var hit in targets)
            {
                //var collider = hit.collider.GetComponent<MeshCollider>();
                //if (collider != null) SendReply(player, $"Cave: {collider.sharedMesh.name}");
                //if (collider == null || !collider.sharedMesh.name.StartsWith("rock_")) continue;
                if (!hit.collider.name.StartsWith("rock_")) continue;
                return true;
            }
            return false;
        }

        private static bool IsInside(Collider collider, BaseEntity entity)
        {
            var center = collider.bounds.center;
            //var point = entity.ClosestPoint(center);
            var point = entity.WorldSpaceBounds().ToBounds().max;
            var direction = center - point;
            var ray = new Ray(point, direction);
            RaycastHit hitInfo;
            return !collider.Raycast(ray, out hitInfo, direction.magnitude + 1);
        }
    }
}
