#region using-directives
using System.Collections.Generic;
using CodeHatch.Common;
using Oxide.Core;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.UserInterface.Dialogues;
using System.Linq;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Networking.Events;
using CodeHatch.Thrones.Weapons;
using UnityEngine;
using System;
using System.Globalization;
using CodeHatch.Networking.Events.Characters;
using CodeHatch.Thrones.CharacterCreation;
using CodeHatch.Thrones.Events;
using CodeHatch.Engine.Serialization;
using CodeHatch.Engine.Characters;
using CodeHatch.Networking.Events.UI;
using CodeHatch.Thrones.CharacterCustomization;
using System.Diagnostics;
using UMA;
using CodeHatch.Thrones.Banner;
using CodeHatch.Thrones.Hair;
using CodeHatch.Thrones.Taunts;
using System.Collections;
using CodeHatch.Thrones;
using CodeHatch.UserInterface;
using CodeHatch.Networking.Events.Entities.Objects.Gadgets;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Networking.Events.Entities;
using PathologicalGames;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.ItemContainer;
using CodeHatch.TerrainAPI;
#endregion
#region Header
namespace Oxide.Plugins
{
    [Info("HellSpawnPlugin", "juk3b0x", "0.0.1")]
    public class HellSpawn : ReignOfKingsPlugin
    #endregion
    {
        bool Werewolves;
        bool Zombies;
        int CreatureAmount;
        #region Config
        void Init() => LoadDefaultConfig();
        protected override void LoadDefaultConfig()
        {
            // General Config
            Config["Do you want werewolves to spawn?"] = Werewolves = GetConfig("Do you want werewolves to spawn?", true);
            Config["Do you want zombies to spawn?"] = Zombies = GetConfig("Do you want zombies to spawn?", true);
            Config["AMOUNT of creatures to spawn"] = CreatureAmount = GetConfig("AMOUNT of creatures to spawn", 5);
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));
        #endregion
        void OnEntityHealthChange(EntityDamageEvent e)
        {
            if (!e.Entity.IsPlayer) Puts(e.Entity.name);
            
        }
       [ChatCommand("gatesofhell")]
        void OpenTheGatesOfHell(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin")&& !player.HasPermission("mod")) { PrintToChat(player, "You have no permission to open the gates of Hell!"); return;}
            string z = "villager";
            string w = "werewolf";
            if (input.IsNullOrEmpty()) { CreatureAmount = GetConfig("AMOUNT of creatures to spawn", 5); PrintToChat(player, "you have not set an argument for the number of spawned creatures, using default value of " + CreatureAmount.ToString()); }
            else CreatureAmount = int.Parse(string.Concat(input));

            if (CreatureAmount > 20) { CreatureAmount = 20; PrintToChat(player, "You cannot spawn more than 20 Villagers per Player with GatesOfHell (Server Lag)!"); }
            if (Werewolves&&Zombies)
            {
                timer.Repeat(3f, (CreatureAmount / 2), () =>
                {
                    foreach (Player connectedplayer in Server.AllPlayersExcept(Server.GetPlayerFromString("Server")))
                    {
                        Vector3 RandomPosition = Vector3Util.Randomize(connectedplayer.Entity.Position, -100, 100);
                        Vector3 RandomToSurfacePosition = new Vector3(RandomPosition.x, TerrainAPIBase.GetTerrainHeightAt(RandomPosition), RandomPosition.z);
                        SpawnSingleCreatureAroundEveryPlayer(RandomToSurfacePosition, connectedplayer, z);
                    }
                });
                timer.Repeat(3f, (CreatureAmount / 2), () =>
                {
                    foreach (Player connectedplayer in Server.AllPlayersExcept(Server.GetPlayerFromString("Server")))
                    {
                        Vector3 RandomPosition = Vector3Util.Randomize(connectedplayer.Entity.Position, -100, 100);
                        Vector3 RandomToSurfacePosition = new Vector3(RandomPosition.x, TerrainAPIBase.GetTerrainHeightAt(RandomPosition), RandomPosition.z);
                        SpawnSingleCreatureAroundEveryPlayer(RandomToSurfacePosition, connectedplayer, w);
                    }
                });
            }
            else if (Zombies)
            {
                timer.Repeat(3f, (CreatureAmount), () =>
                {
                    foreach (Player connectedplayer in Server.AllPlayersExcept(Server.GetPlayerFromString("Server")))
                    {
                        Vector3 RandomPosition = Vector3Util.Randomize(connectedplayer.Entity.Position, -100, 100);
                        Vector3 RandomToSurfacePosition = new Vector3(RandomPosition.x, TerrainAPIBase.GetTerrainHeightAt(RandomPosition), RandomPosition.z);
                        SpawnSingleCreatureAroundEveryPlayer(RandomToSurfacePosition, connectedplayer, z);
                    }
                });
            }
            else if (Werewolves)
            {
                timer.Repeat(3f, (CreatureAmount / 2), () =>
                {
                    foreach (Player connectedplayer in Server.AllPlayersExcept(Server.GetPlayerFromString("Server")))
                    {
                        Vector3 RandomPosition = Vector3Util.Randomize(connectedplayer.Entity.Position, -100, 100);
                        Vector3 RandomToSurfacePosition = new Vector3(RandomPosition.x, TerrainAPIBase.GetTerrainHeightAt(RandomPosition), RandomPosition.z);
                        SpawnSingleCreatureAroundEveryPlayer(RandomToSurfacePosition, connectedplayer, w);
                    }
                });
            }
            else if (!Werewolves && !Zombies) { PrintToChat(player, "You need to activate at least one creature in the configfile to spawn it! reload the plugin afterwards.");return; }

        }
        [ChatCommand("locate")]
        void Localize(Player player)
        {
            Puts(player.Entity.Position.x.ToString());
            Puts(player.Entity.Position.y.ToString());
            Puts(player.Entity.Position.z.ToString());
        }
        #region Utility
        void SpawnSingleCreatureAroundEveryPlayer(Vector3 RandomToSurfacePosition,Player player, string zw)
        {
            NetworkInstantiationArgs newNetInstArgs = new NetworkInstantiationArgs();
            newNetInstArgs.Information = InformationType.Prefab;
            newNetInstArgs.NetworkAware = true;
            newNetInstArgs.OwnerId = Server.GetPlayerByName("Server").Id;
            newNetInstArgs.SocialOwnerId = 0;
            InvItemBlueprint[] lala = new InvItemBlueprint[InvDefinitions.Instance.Blueprints.AllDefinedBlueprints.Count()];
            lala = InvDefinitions.Instance.Blueprints.AllDefinedBlueprints;
            InvItemBlueprint bptouse = new InvItemBlueprint();
            foreach (var blueprint in lala)
            {
                if (!blueprint.Name.ToLower().Contains(zw)) continue;
                bptouse = blueprint;
            }
            CustomNetworkInstantiate.Instantiate(bptouse, RandomToSurfacePosition, player.Entity.Rotation, newNetInstArgs);
        }
        #endregion

    }
}

