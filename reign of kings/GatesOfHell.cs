#region using-directives
using System.Collections.Generic;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using System.Linq;
using UnityEngine;
using CodeHatch.Engine.Events.Prefab;
using CodeHatch.Inventory.Blueprints;
using CodeHatch.TerrainAPI;
#endregion
#region Header
namespace Oxide.Plugins
{
    [Info("GatesOfHell", "juk3b0x", "1.2.0")]
    public class GatesOfHell : ReignOfKingsPlugin
    #endregion
    {
        #region LanguageAPI
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoAdmin", "You have no permission to open the gates of Hell!" },
                { "NoArgument", "You have not set an argument for the number of spawned creatures, using default value of {0}" },
                { "TooMany", "You cannot spawn more than 20 Villagers per Player with GatesOfHell (Server Lag)!" },
                { "WrongConfig", "You need to activate at least one creature in the configfile to spawn it! Reload the plugin afterwards." }
            }, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        #region Config
        bool Werewolves;
        bool Zombies;
        int CreatureAmount;
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
       [ChatCommand("gatesofhell")]
        void OpenTheGatesOfHell(Player player, string cmd, string[] input)
        {
            if (!player.HasPermission("admin")&& !player.HasPermission("mod")) { PrintToChat(player, string.Format(GetMessage("NoAdmin", player.Id.ToString()))); return;}
            string z = "villager";
            string w = "werewolf";
            if (input.IsNullOrEmpty()) { CreatureAmount = GetConfig("AMOUNT of creatures to spawn", 5); PrintToChat(player, string.Format(GetMessage("NoArgument", player.Id.ToString())), CreatureAmount.ToString()); }
            else CreatureAmount = int.Parse(string.Concat(input));

            if (CreatureAmount > 20) { CreatureAmount = 20; PrintToChat(player, string.Format(GetMessage("TooMany", player.Id.ToString()))); }
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
            else if (!Werewolves && !Zombies) { PrintToChat(player, string.Format(GetMessage("WrongConfig", player.Id.ToString())));return; }

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

