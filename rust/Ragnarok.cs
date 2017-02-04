using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Ragnarok", "Drefetr et Shmitt", "0.7.9", ResourceId = 1985)]
    [Description("A constant barrage of meteors and crappy weather")]

    class Ragnarok : RustPlugin
    {
        /**
         * Minimum clockwise angular deviation from the normal vector;
         * Where 0.0f is 0 rad, and 1.0f is 1/2 rad.
         * (0.0f, ..., maxLaunchAngle).
         */
        float minLaunchAngle = 0.25f;

        /**
         * Maximum clockwise angular deviation from the normal vector;
         * Where 0.0f is 0 rad, and 1.0f is 1/2 rad.
         * (minLaunchAngle, ..., 1.0f).
         */
        float maxLaunchAngle = 0.5f;

        /**
         * Minimum launch height (m); suggested sensible bounds:
         * x >= 1 * maxLaunchVelocity.
         */
        float minLaunchHeight = 100.0f;

        /**
         * Maximum launch height (m); suggested sensible bounds:
         * x <= 10*minLaunchVelocity.
         */
        float maxLaunchHeight = 250.0f;

        /**
         * Minimum launch velocity (m/s^-1).
         */
        float minLaunchVelocity = 25.0f;

        /**
         * Maximum launch velocity (m/s^-1).
         * Suggested sensible maximum: 75.0f.
         */
        float maxLaunchVelocity = 75.0f;

        /**
         * ServerTicks between Meteor(s).
         */
        int meteorFrequency = 10;

        /**
         * Maximum number of Meteors per cluster.
         */
        int maxClusterSize = 5;

        /**
         * The minimum range (+/- x, & +/- z) of a Meteor cluster.
         */
        int minClusterRange = 1;

        /**
         * The maximum range (+/- x, & +/- z) of a Meteor clutser.
         */
        int maxClusterRange = 5;

        /**
         * Percent chance of the Meteor dropping loose resources at the point of impact.
         */
        float spawnResourcePercent = 0.05f;

        /**
         * Percent chance of the Meteor spawning a resource node at the point of impact.
         */
        float spawnResourceNodePercent = 1.0f;

        /**
         * ServerTicks since OnServerInit().
         */
        int tickCounter;

        /**
         * Server OnInit-bind; runs on server startup & mod. init.
         */
        void OnServerInitialized()
        {
            // Load configuration (& call LoadDefaultConfig if the file doesn't yet exist).
            minLaunchAngle = Convert.ToSingle(Config["MinLaunchAngle"]);
            maxLaunchAngle = Convert.ToSingle(Config["MaxLaunchAngle"]);
            minLaunchHeight = Convert.ToSingle(Config["MinLaunchHeight"]);
            maxLaunchHeight = Convert.ToSingle(Config["MaxLaunchHeight"]);
            minLaunchVelocity = Convert.ToSingle(Config["MinLaunchVelocity"]);
            maxLaunchVelocity = Convert.ToSingle(Config["MaxLaunchVelocity"]);
            meteorFrequency = (int)Config["MeteorFrequency"];
            maxClusterSize = (int)Config["MaxClusterSize"];
            minClusterRange = (int)Config["MinClusterRange"];
            maxClusterRange = (int)Config["MaxClusterRange"];
            spawnResourcePercent = Convert.ToSingle(Config["SpawnResourcePercent"]);
            spawnResourceNodePercent = Convert.ToSingle(Config["SpawnResourceNodePercent"]);

            // Ensure shitty weather; clouds & fog.
            ConsoleSystem.Run.Server.Normal("weather.clouds 1");
            ConsoleSystem.Run.Server.Normal("weather.fog 1");
        }

        /**
         * Loads & creates a default configuration file (using the properties and
         * values defined above).
         */
        protected override void LoadDefaultConfig()
        {
            Config.Set("MinLaunchAngle", minLaunchAngle);
            Config.Set("MaxLaunchAngle", maxLaunchAngle);
            Config.Set("MinLaunchHeight", minLaunchHeight);
            Config.Set("MaxLaunchHeight", maxLaunchHeight);
            Config.Set("MinLaunchVelocity", minLaunchVelocity);
            Config.Set("MaxLaunchVelocity", maxLaunchVelocity);
            Config.Set("MeteorFrequency", meteorFrequency);
            Config.Set("MaxClusterSize", maxClusterSize);
            Config.Set("MinClusterRange", minClusterRange);
            Config.Set("MaxClusterRange", maxClusterRange);
            Config.Set("SpawnResourcePercent", spawnResourcePercent);
            Config.Set("SpawnResourceNodePercent", spawnResourceNodePercent);

            SaveConfig();
        }

        /**
         * Server OnTick-bind; runs once per server tick --
         * (An externally configurable frequency).
         */
        void OnTick()
        {
            // Spawn Meteors(s) Y/N:
            if (tickCounter % meteorFrequency == 0)
            {
                // Fetch a random position, with an altitude of {0}.
                var location = GetRandomMapPosition();
                var clusterSize = UnityEngine.Random.Range(1, maxClusterSize);

                for (var i = 0; i < clusterSize; i++)
                {
                    var r = UnityEngine.Random.Range(0.0f, 100.0f);

                    // Add a (slight) degree of randomness to the launch position(s):
                    location.x += UnityEngine.Random.Range(minClusterRange, maxClusterRange);
                    location.z += UnityEngine.Random.Range(minClusterRange, maxClusterRange);

                    if (r < spawnResourcePercent)
                        // Spawn a loose resource:
                        SpawnResource(location);

                    if (r < spawnResourceNodePercent)
                        // Spawn a resource node:
                        SpawnResourceNode(location);

                    SpawnMeteor(location);
                }
            }

            tickCounter++;
        }

        /**
         * Spawns a Meteor in the location specified by Vector3(location).
         */
        void SpawnMeteor(Vector3 origin)
        {
            var launchAngle = UnityEngine.Random.Range(minLaunchAngle, maxLaunchAngle);
            var launchHeight = UnityEngine.Random.Range(minLaunchHeight, maxLaunchHeight);

            var launchDirection = (Vector3.up * -launchAngle + Vector3.right).normalized;
            var launchPosition = origin - launchDirection * launchHeight;

            var r = UnityEngine.Random.Range(0, 3);

            ItemDefinition projectileItem;

            // Fetch rocket of type <x>:
            switch (r)
            {
                case 0:
                    projectileItem = GetBasicRocket();
                    break;

                case 1:
                    projectileItem = GetHighVelocityRocket();
                    break;

                case 2:
                    projectileItem = GetSmokeRocket();
                    break;

                default:
                    projectileItem = GetFireRocket();
                    break;
            }

            // Create the in-game "Meteor" entity:
            var component = projectileItem.GetComponent<ItemModProjectile>();
            var entity = GameManager.server.CreateEntity(component.projectileObject.resourcePath, launchPosition, new Quaternion(), true);

            // Set Meteor speed:
            var serverProjectile = entity.GetComponent<ServerProjectile>();
            serverProjectile.speed = UnityEngine.Random.Range(minLaunchVelocity, maxLaunchVelocity);

            entity.SendMessage("InitializeVelocity", (object)(launchDirection * 1.0f));
            entity.Spawn();
        }

        /**
         * Spawns a ResourceItem of a random type at the location specified by
         * Vector3(location).
         */
        void SpawnResource(Vector3 location)
        {
            string resourceName;
            int resourceQuantity;

            var r = UnityEngine.Random.Range(0, 3);

            switch (r)
            {
                case 1:
                    resourceName = "hq.metal.ore";
                    resourceQuantity = 100;
                    break;

                case 2:
                    resourceName = "metal.ore";
                    resourceQuantity = 1000;
                    break;

                case 3:
                    resourceName = "stones";
                    resourceQuantity = 2500;
                    break;

                default:
                    resourceName = "sulfur.ore";
                    resourceQuantity = 1000;
                    break;
            }

            ItemManager.CreateByName(resourceName, resourceQuantity).Drop(location, Vector3.up);
        }

        /**
         * Spawns a ResourceNode of a random type at the location specified by
         * Vector3(location).
         */
        void SpawnResourceNode(Vector3 location)
        {
            var prefabName = "assets/bundled/prefabs/autospawn/resource/ores/";

            // Select a random ResourceNode type {Metal, Stone, Sulfur}.
            var r = UnityEngine.Random.Range(0, 2);

            switch (r)
            {
                case 1:
                    prefabName += "metal-ore";
                    break;

                case 2:
                    prefabName += "stone-ore";
                    break;

                default:
                    prefabName += "sulfur-ore";
                    break;
            }

            prefabName += ".prefab";

            // & spawn the ResourceNode at Vector3(location).
            var resourceNode = GameManager.server.CreateEntity(prefabName, location, new Quaternion(0, 0, 0, 0));
            resourceNode.Spawn();
        }

        /**
         * Returns an Item of type "ammo.rocket.basic":
         */
        ItemDefinition GetBasicRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.basic");
        }

        /**
         * Returns an Item of type "ammo.rocket.fire":
         */
        ItemDefinition GetFireRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.fire");
        }

        /**
         * Returns an Item of type "ammo.rocket.hv":
         */
        ItemDefinition GetHighVelocityRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.hv");
        }

        /**
         * Returns an Item of type "ammo.rocket.smoke":
         */
        ItemDefinition GetSmokeRocket()
        {
            return ItemManager.FindItemDefinition("ammo.rocket.smoke");
        }

        /**
         * Returns a random Map position (x, y).
         */
        Vector3 GetRandomMapPosition()
        {
            var mapsize = GetMapSize() - 500f;
            var randomX = UnityEngine.Random.Range(-mapsize, mapsize);
            var randomY = UnityEngine.Random.Range(-mapsize, mapsize);
            return new Vector3(randomX, 0f, randomY);
        }

        /**
         * Returns the current Map size, -assumed square- (x, y).
         */
        float GetMapSize()
        {
            return TerrainMeta.Size.x / 2;
        }
    }
}
