﻿using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RockBlock", "Nogrod", "1.0.4")]
    class RockBlock : RustPlugin
    {
        private readonly int worldLayer = LayerMask.GetMask("World", "Default");
        private const BaseNetworkable.DestroyMode DestroyMode = BaseNetworkable.DestroyMode.None;
        private ConfigData configData;

        class ConfigData
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

        private void Init()
        {
            configData = Config.ReadObject<ConfigData>();
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            var player = planner.GetOwnerPlayer();
            if (player == null || player.IsAdmin()) return;
            var entity = gameObject.GetComponent<BaseEntity>();
            RaycastHit hitInfo;
            if (configData.MaxHeight > 0 && Physics.Raycast(new Ray(entity.transform.position, Vector3.down), out hitInfo, float.PositiveInfinity, Rust.Layers.Terrain))
            {
                if (hitInfo.distance > configData.MaxHeight)
                {
                    SendReply(player, "Distance to ground too high: {0}", hitInfo.distance);
                    entity.Kill(DestroyMode);
                    return;
                }
            }
            CheckEntity(entity, player);
        }

        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            var player = deployer.GetOwnerPlayer();
            if (player == null || player.IsAdmin()) return;
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
                Puts($"{player.displayName} is suspected of building {entity.PrefabName} inside a rock at {entity.transform.position}!");
                entity.Kill(DestroyMode);
                break;
            }
        }

        private bool IsInCave(BaseEntity entity, BasePlayer player)
        {
            var targets = Physics.RaycastAll(new Ray(entity.transform.position, Vector3.up), 250, worldLayer);
            foreach (var hit in targets)
            {
                var collider = hit.collider.GetComponent<MeshCollider>();
                //if (collider != null) SendReply(player, $"Cave: {collider.sharedMesh.name}");
                if (collider == null || !collider.sharedMesh.name.StartsWith("rock_")) continue;
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
