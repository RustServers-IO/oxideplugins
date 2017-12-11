using System;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Core.Interaction.Behaviours.Networking;
using CodeHatch.Engine.Modules.SocialSystem.Objects;
using CodeHatch.Networking.Events.Entities;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;

namespace Oxide.Plugins
{
    [Info("Crafting Ownership", "D-Kay", "1.0.0")]
    [Description("Stores crafting station interactions and craftings")]
    public class CraftOwnership : ReignOfKingsPlugin
    {
        #region Variables

        private readonly List<object> _defaultstations = new List<object>
        {
            "Work Bench",
            "Tannery",
            "Stone Cutter",
            "Tinker",
            "Anvil",
            "Smithy",
            "Bellows",
            "Fletcher",
            "Advanced Fletcher",
            "Siegeworks",
            "Woodworking",
            "Spinning Wheel"/*,
            "Campfire",
            "Firepit",
            "Smelter",
            "Granary",
            "Well",
            "Sawmill"*/
        };

        private HashSet<string> CraftingStations { get; set; } = new HashSet<string>();

        private StoredData Data { get; set; } = new StoredData();


        private class StoredData
        {
            public Dictionary<ulong, PlayerCraftingData> PlayerCraftingData { get; set; } = new Dictionary<ulong, PlayerCraftingData>();
            public Dictionary<uint, StationCraftingData> StationCraftingData { get; set; } = new Dictionary<uint, StationCraftingData>();

            public void Reset()
            {
                PlayerCraftingData.Clear();
                StationCraftingData.Clear();
            }

            public void Add(ulong playerId, PlayerCraftingData data)
            {
                PlayerCraftingData.Add(playerId, data);
            }

            public void Add(uint objectGUID, StationCraftingData data)
            {
                StationCraftingData.Add(objectGUID, data);
            }

            public void Remove(ulong playerId)
            {
                PlayerCraftingData.Remove(playerId);
            }

            public void Remove(uint objectGUID)
            {
                StationCraftingData.Remove(objectGUID);
            }
        }


        private abstract class CraftingData
        {
            public Craft Crafting { get; set; }

            public ulong? GetCrafter()
            {
                return Crafting?.PlayerId;
            }

            public int GetAmount()
            {
                return Crafting?.Amount ?? 0;
            }
        }

        private class PlayerCraftingData : CraftingData
        {
            public int SetCrafting(ulong player, int amount)
            {
                try
                {
                    var result = Crafting == null ? 1 : 2;
                    Crafting = new Craft(player, amount);
                    return result;
                }
                catch
                {
                    return 0;
                }
            }
        }

        private class StationCraftingData : CraftingData
        {
            public Interaction Interaction { get; set; }

            public int SetInteraction(ulong player)
            {
                try
                {
                    var result = Interaction == null ? 1 : 2;
                    Interaction = new Interaction(player);
                    return result;
                }
                catch
                {
                    return 0;
                }
            }

            public int SetCrafting(int amount)
            {
                try
                {
                    var result = Crafting == null ? 1 : 2;
                    if (Interaction == null) return -1;
                    Crafting = new Craft(Interaction.PlayerId, amount);
                    return result;
                }
                catch
                {
                    return 0;
                }
            }

            public ulong? GetInteractor()
            {
                return Interaction?.PlayerId;
            }

            public void Finish()
            {
                Crafting = null;
            }
        }


        private abstract class Action
        {
            public ulong PlayerId { get; set; }
        }

        private class Craft : Action
        {
            public int Amount { get; set; }

            public Craft() { }

            public Craft(ulong playerId, int amount)
                : this()
            {
                PlayerId = playerId;
                Amount = amount;
            }
        }

        private class Interaction : Action
        {
            public Interaction() { }

            public Interaction(ulong playerId)
                : this()
            {
                PlayerId = playerId;
            }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadConfigData();
            LoadData();

            permission.RegisterPermission("CraftOwnership.Modify.", this);
        }

        private void Unload()
        {
            SaveData();
        }

        private void LoadData()
        {
            Data = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("CraftOwnership");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("CraftOwnership", Data);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            var list = GetConfig("Crafting stations", _defaultstations);
            list.Where(i => i is string).Foreach(i => CraftingStations.Add((string)i));
        }

        private void SaveConfigData()
        {
            Config["Crafting stations"] = CraftingStations;
            SaveConfig();
        }

        #endregion

        #region System Functions

        private bool IsCraftingStation(Entity entity)
        {
            return CraftingStations.Any(entity.name.ContainsIgnoreCase);
        }

        #endregion

        #region Hook Calls

        private ulong? GetLastUser(Entity entity)
        {
            if (entity.IsPlayer) return entity.OwnerId;

            var objectGUID = entity.Get<ISecurable>()?.ObjectGUID;
            return objectGUID == null ? null : GetLastUserByObjectGUID((uint)objectGUID);
        }

        private ulong? GetLastUserByObjectGUID(uint objectGUID)
        {
            return Data.StationCraftingData[objectGUID]?.GetInteractor();
        }

        private ulong? GetLastCrafter(Entity entity)
        {
            if (entity.IsPlayer) return entity.OwnerId;

            var objectGUID = entity.Get<ISecurable>()?.ObjectGUID;
            return objectGUID == null ? null : GetLastCrafterByObjectGUID((uint)objectGUID);
        }

        private ulong? GetLastCrafterByObjectGUID(uint objectGUID)
        {
            return Data.StationCraftingData[objectGUID]?.GetCrafter();
        }

        private int? GetCraftedAmount(Entity entity)
        {
            if (entity.IsPlayer) return Data.PlayerCraftingData[entity.OwnerId]?.GetAmount();

            var objectGUID = entity.Get<ISecurable>()?.ObjectGUID;
            return objectGUID == null ? null : GetCraftedAmountByObjectGUID((uint)objectGUID);
        }

        private int? GetCraftedAmountByObjectGUID(uint objectGUID)
        {
            return Data.StationCraftingData[objectGUID]?.GetAmount();
        }

        #endregion

        #region Hooks

        private void OnPlayerInteract(InteractEvent interactEvent)
        {
            #region Checks
            if (interactEvent == null) return;
            if (interactEvent.Cancelled) return;
            if (interactEvent.Entity == null) return;
            if (interactEvent.ControllerEntity == null) return;
            if (!interactEvent.ControllerEntity.IsPlayer) return;
            #endregion

            var player = interactEvent.ControllerEntity.Owner;
            var security = interactEvent.Entity.TryGet<ISecurable>();
            if (security == null) return;
            var objectGUID = security.ObjectGUID;

            StationCraftingData data;
            if (!Data.StationCraftingData.TryGetValue(objectGUID, out data))
            {
                data = new StationCraftingData();
                Data.Add(objectGUID, data);
            }

            data.SetInteraction(player.Id);

            SaveData();
        }

        private void OnItemCraft(ItemCrafterStartEvent craftEvent)
        {
            #region Checks
            if (craftEvent == null) return;
            if (craftEvent.Cancelled) return;
            if (craftEvent.Entity == null) return;
            #endregion

            var amount = craftEvent.Crafter.QuantityEstimated;
            var security = craftEvent.Entity.TryGet<ISecurable>();
            if (security == null)
            {
                if (!craftEvent.Entity.IsPlayer) return;
                var player = craftEvent.Entity.Owner;
                PlayerCraftingData data;
                if (!Data.PlayerCraftingData.TryGetValue(player.Id, out data))
                {
                    data = new PlayerCraftingData();
                    Data.Add(craftEvent.Entity.OwnerId, data);
                }

                data.SetCrafting(player.Id, amount);
            }
            else
            {
                var objectGUID = security.ObjectGUID;
                StationCraftingData data;
                if (!Data.StationCraftingData.TryGetValue(objectGUID, out data))
                {
                    data = new StationCraftingData();
                    Data.Add(objectGUID, data);
                }

                data.SetCrafting(amount);
            }

            SaveData();
        }

        #endregion

        #region Utility

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        #endregion
    }
}