using CodeHatch.Blocks;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Damaging;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Engine.Modules.SocialSystem;
using CodeHatch.Engine.Networking;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Thrones.AncientThrone;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.UserInterface.Dialogues;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using CodeHatch.Blocks.Inventory;
using UnityEngine;
using static CodeHatch.Blocks.Networking.Events.CubeEvent;

namespace Oxide.Plugins
{
    [Info("LevelSystem", "D-Kay", "1.0.0", ResourceId = 1822)]
    public class LevelSystem : ReignOfKingsPlugin
    {
        #region Variables

        [PluginReference("GrandExchange")]
        private Plugin GrandExchange;

        #region Fields
        private bool UsePvpXp { get; set; } = true; // Turns on/off gold for PVP.
        private bool UsePveXp { get; set; } = true; // Turns on/off gold for PVE.
        private bool UseDamageBonus { get; set; } = true; // Turns on/off the damage bonus.
        private bool UseDefenseBonus { get; set; } = true; // Turns on/off the defense bonus.
        private bool UseInventoryBonus { get; set; } = true; //Turns on/off the inventory slot bonus.
        private bool UseXpGain { get; set; } = true; // Turns on/off the xp gain per level.
        private bool UseThroneLevel { get; set; } = true; // Turns on/off the level requirement to take the throne.
        private bool UseCrestLevel { get; set; } = true; // Turns on/off the level requirement to do damage against a crest.
        private bool UseCubeLevel { get; set; } = true; // Turns on/off the level requirement to do damage against cubes.
        private bool UsePvpLevel { get; set; } = true; //Turns on/off the level requirement to do pvp damage.
        private bool UseRopingLevel { get; set; } = true; //Turns on/off the roping requirement to do pvp damage.
        private bool UseRemoveMyXpCommand { get; set; } = true;  //Turns on/off the ability to use the /removemyxp command.
        private bool UseLevelupStatChoice { get; set; } = true; //Turns on/off the level up stat choice popup.
        private bool RecalculateXpCurve { get; set; } = true; // If true will recalculate the xp curve.
        private bool UsePlaceLevel { get; set; } = true; // Turns on/off the level requirement for placing a specific block.
        private bool UseReturnCube { get; set; } = true; // Turns on/off the returning of the cube when placing one without the correct level.
        private bool UseCrestRoping { get; set; } = true; // Turns on/off the allowance of roping in your own crest territory.
        private bool UseStatReset { get; set; } = true; // Turns on/off the allowance for resetting ones skills.
        //private bool UseImprisonment { get; set; } = true; // Turns on/off the automated imprisonment of players.

        private static bool UseNametag { get; set; } = true; // Turns on/off the level nametag.

        // PvE settings: 
        private int MonsterKillMinXp => GetConfig("XpGain", "monsterKillMinXp", 15); // Minimum amount of xp a player can get for killing a monster.
        private int MonsterKillMaxXp => GetConfig("XpGain", "monsterKillMaxXp", 25); // Maximum amount of xp a player can get for killing a monster.
        private int AnimalKillMinXp => GetConfig("XpGain", "animalKillMinXp", 10); // Minimum amount of xp a player can get for killing an animal.
        private int AnimalKillMaxXp => GetConfig("XpGain", "animalKillMaxXp", 15); // Maximum amount of xp a player can get for killing an animal.
        // PvP settings:
        private int PvpGetMinXp => GetConfig("XpGain", "pvpGetMinXp", 10); // Minimum amount of xp a player can get for killing a player.
        private int PvpGetMaxXp => GetConfig("XpGain", "pvpGetMaxXp", 25); // Maximum amount of xp a player can get for killing a player.
        private int PvpLoseMinXp => GetConfig("XpGain", "pvpLoseMinXp", 10); // Minimum amount of xp a player can lose for getting killed by a player.
        private int PvpLoseMaxXp => GetConfig("XpGain", "pvpLoseMaxXp", 15); // Maximum amount of xp a player can lose for getting killed by a player.
        private double PvpXpLossPercentage => GetConfig("XpGain", "pvpXpLossPercentage", 20); // Amount of xp you get less for each level difference as percentage.
        private double XpGainPerLvPercentage => GetConfig("XpGain", "xpGainPerLvPercentage", 12); // Amount of xp you get more per level as percentage.
        // Damage bonus settings:
        private float PlayerDamageBonus => GetConfig("Bonusses", "playerDamageBonus", 0.2f); // Damagebonus when hitting a player for each level gained.
        private float BeastDamageBonus => GetConfig("Bonusses", "beastDamageBonus", 0.2f); // Damagebonus when hitting a monster for each level gained.
        private float BallistaDamageBonus => GetConfig("Bonusses", "ballistaDamageBonus", 5f); // Damagebonus when using siege weapons for each level gained.
        private float TrebuchetDamageBonus => GetConfig("Bonusses", "trebuchetDamageBonus", 50f); // Damagebonus when using siege weapons for each level gained.
        private float CubeDamageBonus => GetConfig("Bonusses", "cubeDamageBonus", 0.5f); // Damagebonus when hitting a block without siegeweapons for each level gained.
        // Defense bonus settings:
        private float PlayerDefenseBonus => GetConfig("Bonusses", "playerDefenseBonus", 0.2f); // Defensebonus when getting hit by a player for each level gained.
        private float BeastDefenseBonus => GetConfig("Bonusses", "beastDefenseBonus", 0.5f); // Defensebonus when getting hit by a monster for each level gained.
        // Inventory slot stat settings:
        private float InventorySlotBonus => GetConfig("Stats", "inventorySlotBonus", 0.5f); // Inventoryslot bonus per level gained.
        private int DefaultInventorySlots => GetConfig("Stats", "defaultInventorySlots", 32); // Default inventorySlots.
        // Top level settings:
        private int MaxTopPlayersList => GetConfig("CommandSettings", "maxTopPlayersList", 15); // Number of players in the top list.
        // Requirement settings:
        private int ThroneLevel => GetConfig("Requirements", "requiredLevelThrone", 3); // Needed level to claim the throne.
        private int CrestLevel => GetConfig("Requirements", "requiredLevelCrestDamage", 3); // Needed level to do damage against crests.
        private int CubeLevel => GetConfig("Requirements", "requiredLevelCubeDamage", 3); // Needed level to do damage against cubes.
        private int PvpLevel => GetConfig("Requirements", "requiredLevelPvp", 3); // Needed level to do pvp damage.
        private int RopingLevel => GetConfig("Requirements", "requiredLevelRoping", 3); // Needed level to capture players.
        private int StatResetGold => GetConfig("Requirements", "requiredStatResetGold", 50000); // Needed gold amount to reset ones skills.

        // Placement settings:
        private int SodLevel => GetConfig("Placement", "Sod", 1);
        private int ThatchLevel => GetConfig("Placement", "Thatch", 1);
        private int ClayLevel => GetConfig("Placement", "Clay", 1);
        private int SprucheLevel => GetConfig("Placement", "Spruche", 1);
        private int WoodLevel => GetConfig("Placement", "Wood", 1);
        private int LogLevel => GetConfig("Placement", "Log", 1);
        private int CobblestoneLevel => GetConfig("Placement", "Cobblestone", 3);
        private int ReinforcedLevel => GetConfig("Placement", "Reinforced Wood (Iron)", 3);
        private int StoneLevel => GetConfig("Placement", "Stone", 3);

        private int ReinforcedSteelDoorLevel => GetConfig("Placement - Other", "ReinforcedSteelDoor", 1);
        private int StoneWindowLevel => GetConfig("Placement - Other", "StoneWindow", 3);
        private int IronDoorLevel => GetConfig("Placement - Other", "IronDoor", 3);
        private int IronGateLevel => GetConfig("Placement - Other", "IronGate", 3);
        private int IronWindowLevel => GetConfig("Placement - Other", "IronWindow", 3);
        private int ReinforcedIronDoorLevel => GetConfig("Placement - Other", "ReinforcedIronDoor", 1);
        private int ReinforcedIronGateLevel => GetConfig("Placement - Other", "ReinforcedIronGate", 1);
        private int WoodDoorLevel => GetConfig("Placement - Other", "WoodDoor", 1);
        private int WoodGateLevel => GetConfig("Placement - Other", "WoodGate", 1);
        private int DrawbridgeLevel => GetConfig("Placement - Other", "Drawbridge", 3);
        private int WoodWindowLevel => GetConfig("Placement - Other", "WoodWindow", 1);
        private int LongDrawbridgeLevel => GetConfig("Placement - Other", "LongDrawbridge", 3);
        private int StoneArchLevel => GetConfig("Placement - Other", "StoneArch", 1);
        private int ReinforcedIronTrapDoorLevel => GetConfig("Placement - Other", "ReinforcedIronTrapDoor", 1);

        // Xp value settings:
        private int MaxLevel = 1000;
        private int XpCurveBasis => GetConfig("XpCurve", "xpCurveBasis", 10);
        private int XpCurveExtra => GetConfig("XpCurve", "xpCurveExtra", 40);
        private int XpCurveAccA => GetConfig("XpCurve", "xpCurveAcc_A", 10);
        private int XpCurveAccB => GetConfig("XpCurve", "xpCurveAcc_B", 10);
        #endregion

        private static HashSet<Level> Levels { get; } = new HashSet<Level>();
        private Dictionary<ulong, PlayerData> PlayerXpData { get; set; } = new Dictionary<ulong, PlayerData>();

        private System.Random Random { get; } = new System.Random();

        private static int MaxPossibleXp { get; set; }

        private enum Skill
        {
            PlayerDamage = 3,
            PlayerDefense = 1,
            BeastDamage = 4,
            BeastDefense = 2,
            CubeDamage = 5,
            BallistaDamage = 6,
            TrebuchetDamage = 7,
            InventorySlot = 8
        }

        private class Level
        {
            public int Id { get; set; } = 1;
            public int Xp { get; set; } = 0;
            public string Format => $"([40FF40]{Id}[ffffff])";
            public int MaxPoints => Id - 1;

            public Level() { }

            public Level(int id, int xp)
            {
                Id = id;
                Xp = xp;
            }

            public override string ToString()
            {
                return Id.ToString();
            }
        }

        private class PlayerData
        {
            public ulong Id { get; set; }
            public string Name { get; set; }
            public string UsedFormat { get; set; }
            public int Xp { get; set; }
            public int Points { get; set; }
            public Level Level { get; set; }
            public Dictionary<Skill, Bonus> Bonuses { get; set; }

            public PlayerData() { }

            public PlayerData(ulong id, string name, int xp = 0, int points = 0)
            {
                Id = id;
                Name = name;
                Xp = xp;
                Points = points;
                Level = Levels.Last(l => l.Xp <= Xp);
                Bonuses = new Dictionary<Skill, Bonus>
                {
                    {Skill.PlayerDamage, new Bonus()},
                    {Skill.PlayerDefense, new Bonus()},
                    {Skill.BeastDamage, new Bonus()},
                    {Skill.BeastDefense, new Bonus()},
                    {Skill.CubeDamage, new Bonus()},
                    {Skill.BallistaDamage, new Bonus()},
                    {Skill.TrebuchetDamage, new Bonus()},
                    {Skill.InventorySlot, new Bonus()}
                };
            }

            public void Reset()
            {
                Xp = 0;
                Points = 0;
                Level = Levels.Last(l => l.Xp <= Xp);
                Bonuses = new Dictionary<Skill, Bonus>
                {
                    {Skill.PlayerDamage, new Bonus()},
                    {Skill.PlayerDefense, new Bonus()},
                    {Skill.BeastDamage, new Bonus()},
                    {Skill.BeastDefense, new Bonus()},
                    {Skill.CubeDamage, new Bonus()},
                    {Skill.BallistaDamage, new Bonus()},
                    {Skill.TrebuchetDamage, new Bonus()},
                    {Skill.InventorySlot, new Bonus()}
                };
            }

            public int GiveXp(int amount)
            {
                if (Xp + amount >= MaxPossibleXp)
                {
                    Xp = MaxPossibleXp;
                    var newLevel = Levels.Last(l => l.Xp <= Xp);
                    if (newLevel == Level) return 3;
                    Level = newLevel;
                    return 2;
                }
                else
                {
                    Xp += amount;
                    var newLevel = Levels.Last(l => l.Xp <= Xp);
                    if (newLevel == Level) return 0;
                    Level = newLevel;
                    return 1;
                }
            }

            public int RemoveXp(int amount)
            {
                if (Xp - amount < 0)
                {
                    Xp = 0;
                    var newLevel = Levels.Last(l => l.Xp <= Xp);
                    if (newLevel == Level) return 3;
                    Level = newLevel;
                    return 2;
                }
                else
                {
                    Xp -= amount;
                    var newLevel = Levels.Last(l => l.Xp <= Xp);
                    if (newLevel == Level) return 0;
                    Level = newLevel;
                    return 1;
                }
            }

            public int TotalPoints()
            {
                var total = Points;
                Bonuses.Foreach(b => total += b.Value.Points);
                return total;
            }

            public void ResetSkills()
            {
                Bonuses.Foreach(b => b.Value.Points = 0);
                Points = Level.MaxPoints;
            }

            public void UpgradeSkill(Skill skill, int points)
            {
                Bonuses[skill].Upgrate(points);
                Points -= points;
            }

            public void Update(Player player)
            {
                Name = player.Name;
                Level = Levels.Last(l => l.Xp <= Xp);
                if (UseNametag) UpdateChatFormat(player);
            }

            public void UpdateChatFormat(Player player)
            {
                if (Level == null)
                {
                    if (UsedFormat.IsNullOrEmpty()) return;
                    player.DisplayNameFormat.Replace($" {UsedFormat}", "");
                    UsedFormat = null;
                    return;
                }
                if (player.DisplayNameFormat.Contains(Level.Format)) return;
                player.DisplayNameFormat = UsedFormat.IsNullOrEmpty() ? $"{player.DisplayNameFormat} {Level.Format}" : $"{player.DisplayNameFormat.Replace($" {UsedFormat}", "")} {Level.Format}";
                UsedFormat = Level.Format;
            }

            public void UpdateBonus(Skill skill, float value)
            {
                Bonuses[skill].Update(value);
            }
        }

        private class Bonus
        {
            public int Points { get; set; } = 0;
            public float Value { get; set; } = 0.0f;

            public Bonus() { }

            public Bonus(float value, int points = 0)
            {
                Points = points;
                Value = value;
            }

            public float Get(int points = -1)
            {
                return (points < 0 ? Points : points) * Value;
            }

            public void Upgrate(int points)
            {
                Points += points;
            }

            public void Update(float value)
            {
                Value = value;
            }

            public void Update(int points)
            {
                Points = points;
            }
        }

        private class OldPlayerData
        {
            public ulong Id = 0;
            public string Name = "";
            public string NameFormat = "";
            public int Xp = 0;
            public int Points = 0;
            public int HealthRegen = 0;
            public int InventorySlot = 0;
            public int SiegeDamage = 0;
            public int CubeDamage = 0;
            public int PlayerDamage = 0;
            public int BeastDamage = 0;
            public int PlayerDefense = 0;
            public int BeastDefense = 0;

            public OldPlayerData() { }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadXpData();
            LoadConfigData();

            if (RecalculateXpCurve) CalculateXpCurve();

            permission.RegisterPermission("LevelSystem.Info.Player", this);
            permission.RegisterPermission("LevelSystem.Modify.Xp", this);
            permission.RegisterPermission("LevelSystem.Modify.Points", this);
            permission.RegisterPermission("LevelSystem.Toggle.Xp", this);
            permission.RegisterPermission("LevelSystem.Toggle.Bonus", this);
            permission.RegisterPermission("LevelSystem.Toggle.Requirement", this);
            permission.RegisterPermission("LevelSystem.Toggle.Command", this);
            permission.RegisterPermission("LevelSystem.Toggle.Stats", this);
            permission.RegisterPermission("LevelSystem.Toggle.Modify", this);

            MaxPossibleXp = Levels.FirstOrDefault(l => l.Id == MaxLevel).Xp;
            PlayerXpData.Foreach(d => UpdateSkills(d.Value));
        }

        private void Unload()
        {
            SaveXpData();
        }

        private void LoadXpData()
        {
            PlayerXpData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("LevelSystem");
        }

        private void SaveXpData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("LevelSystem", PlayerXpData);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            UsePvpXp = GetConfig("Toggles", "usePvpXp", true);
            UsePveXp = GetConfig("Toggles", "usePveXp", true);
            UseDamageBonus = GetConfig("Toggles", "useDamageBonus", true);
            UseDefenseBonus = GetConfig("Toggles", "useDefenseBonus", true);
            UseInventoryBonus = GetConfig("Toggles", "useInventorySlotBonus", true);
            UseLevelupStatChoice = GetConfig("Toggles", "useLevelupStatChoice", true);
            UseXpGain = GetConfig("Toggles", "useXpGainBonus", true);
            UseThroneLevel = GetConfig("Toggles", "useThroneLevel", true);

            UseCrestLevel = GetConfig("Toggles", "useCrestLevel", true);
            UseCubeLevel = GetConfig("Toggles", "useCubeLevel", true);
            UsePvpLevel = GetConfig("Toggles", "usePvpLevel", true);
            UseRopingLevel = GetConfig("Toggles", "useRopingLevel", true);
            UseRemoveMyXpCommand = GetConfig("Toggles", "useRemoveMyXpCommand", true);

            UsePlaceLevel = GetConfig("Toggles", "usePlaceLevel", false);
            UseReturnCube = GetConfig("Toggles", "useReturnCube", true);
            UseCrestRoping = GetConfig("Toggles", "useCrestRoping", true);

            UseNametag = GetConfig("Toggles", "useNametag", true);

            RecalculateXpCurve = GetConfig("XpCurve", "recalculateXpCurve", true);
            MaxLevel = GetConfig("XpCurve", "maxLevel", 1000);

            Levels.Clear();
            var xpValues = GetConfig("XpCurve", "xpNeededPerLevel", new List<object>()).Cast<int>().ToList();
            for (var i = 0; i < xpValues.Count; i++)
            {
                Levels.Add(new Level(i + 1, xpValues[i]));
            }
        }

        private void SaveConfigData()
        {
            Config["XpGain", "monsterKillMinXp"] = MonsterKillMinXp;
            Config["XpGain", "monsterKillMaxXp"] = MonsterKillMaxXp;
            Config["XpGain", "animalKillMinXp"] = AnimalKillMinXp;
            Config["XpGain", "animalKillMaxXp"] = AnimalKillMaxXp;

            Config["XpGain", "pvpGetMinXp"] = PvpGetMinXp;
            Config["XpGain", "pvpGetMaxXp"] = PvpGetMaxXp;
            Config["XpGain", "pvpLoseMinXp"] = PvpLoseMinXp;
            Config["XpGain", "pvpLoseMaxXp"] = PvpLoseMaxXp;

            Config["XpGain", "pvpXpLossPercentage"] = PvpXpLossPercentage;
            Config["XpGain", "xpGainPerLvPercentage"] = XpGainPerLvPercentage;

            Config["Bonusses", "playerDamageBonus"] = PlayerDamageBonus;
            Config["Bonusses", "beastDamageBonus"] = BeastDamageBonus;
            Config["Bonusses", "ballistaDamageBonus"] = BallistaDamageBonus;
            Config["Bonusses", "trebuchetDamageBonus"] = TrebuchetDamageBonus;
            Config["Bonusses", "cubeDamageBonus"] = CubeDamageBonus;

            Config["Bonusses", "playerDefenseBonus"] = PlayerDefenseBonus;
            Config["Bonusses", "beastDefenseBonus"] = BeastDefenseBonus;

            Config["Stats", "defaultInventorySlots"] = DefaultInventorySlots;
            Config["Stats", "inventorySlotBonus"] = InventorySlotBonus;

            Config["Toggles", "usePvpXp"] = UsePvpXp;
            Config["Toggles", "usePveXp"] = UsePveXp;
            Config["Toggles", "useDamageBonus"] = UseDamageBonus;
            Config["Toggles", "useDefenseBonus"] = UseDefenseBonus;
            Config["Toggles", "useInventorySlotBonus"] = UseInventoryBonus;
            Config["Toggles", "useLevelupStatChoice"] = UseLevelupStatChoice;
            Config["Toggles", "useXpGainBonus"] = UseXpGain;
            Config["Toggles", "useThroneLevel"] = UseThroneLevel;
            Config["Toggles", "useCrestLevel"] = UseCrestLevel;
            Config["Toggles", "useCubeLevel"] = UseCubeLevel;
            Config["Toggles", "usePvpLevel"] = UsePvpLevel;
            Config["Toggles", "useRopingLevel"] = UseRopingLevel;
            Config["Toggles", "useRemoveMyXpCommand"] = UseRemoveMyXpCommand;
            Config["Toggles", "usePlaceLevel"] = UsePlaceLevel;
            Config["Toggles", "useReturnCube"] = UseReturnCube;
            Config["Toggles", "useCrestRoping"] = UseCrestRoping;
            Config["Toggles", "useNametag"] = UseNametag;

            Config["XpCurve", "xpNeededPerLevel"] = Levels.Select(l => l.Xp).Cast<object>();
            Config["XpCurve", "maxLevel"] = MaxLevel;
            Config["XpCurve", "recalculateXpCurve"] = RecalculateXpCurve;
            Config["XpCurve", "xpCurveBasis"] = XpCurveBasis;
            Config["XpCurve", "xpCurveExtra"] = XpCurveExtra;
            Config["XpCurve", "xpCurveAcc_A"] = XpCurveAccA;
            Config["XpCurve", "xpCurveAcc_B"] = XpCurveAccB;

            Config["CommandSettings", "maxTopPlayersList"] = MaxTopPlayersList;

            Config["Requirements", "requiredLevelThrone"] = ThroneLevel;
            Config["Requirements", "requiredLevelCrestDamage"] = CrestLevel;
            Config["Requirements", "requiredLevelCubeDamage"] = CubeLevel;
            Config["Requirements", "requiredLevelPvp"] = PvpLevel;
            Config["Requirements", "requiredLevelRoping"] = RopingLevel;

            Config["Placement", "Sod"] = SodLevel;
            Config["Placement", "Thatch"] = ThatchLevel;
            Config["Placement", "Clay"] = ClayLevel;
            Config["Placement", "Spruche"] = SprucheLevel;
            Config["Placement", "Wood"] = WoodLevel;
            Config["Placement", "Log"] = LogLevel;
            Config["Placement", "Cobblestone"] = CobblestoneLevel;
            Config["Placement", "Reinforced Wood (Iron)"] = ReinforcedLevel;
            Config["Placement", "Stone"] = StoneLevel;

            Config["Placement - Other", "ReinforcedSteelDoor"] = ReinforcedSteelDoorLevel;
            Config["Placement - Other", "StoneWindow"] = StoneWindowLevel;
            Config["Placement - Other", "IronDoor"] = IronDoorLevel;
            Config["Placement - Other", "IronGate"] = IronGateLevel;
            Config["Placement - Other", "IronWindow"] = IronWindowLevel;
            Config["Placement - Other", "ReinforcedIronDoor"] = ReinforcedIronDoorLevel;
            Config["Placement - Other", "ReinforcedIronGate"] = ReinforcedIronGateLevel;
            Config["Placement - Other", "WoodDoor"] = WoodDoorLevel;
            Config["Placement - Other", "WoodGate"] = WoodGateLevel;
            Config["Placement - Other", "Drawbridge"] = DrawbridgeLevel;
            Config["Placement - Other", "WoodWindow"] = WoodWindowLevel;
            Config["Placement - Other", "LongDrawbridge"] = LongDrawbridgeLevel;
            Config["Placement - Other", "StoneArch"] = StoneArchLevel;
            Config["Placement - Other", "ReinforcedIronTrapDoor"] = ReinforcedIronTrapDoorLevel;

            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "You do not have permission to use this command." },
                { "Player Not Online", "That player does not appear to be online right now." },
                { "No Valid Number", "That is not a valid number." },
                { "Remove My Xp", "You removed all your xp." },
                { "Xp Data Deleted", "All xp data was deleted." },
                { "Toggle Pvp Xp", "PvP xp was turned {0}." },
                { "Toggle Pve Xp", "PvE xp was turned {0}." },
                { "Toggle Bonus Damage", "Bonus damage was turned {0}." },
                { "Toggle Bonus Defense", "Bonus defense was turned {0}." },
                { "Toggle Bonus Xp Gain", "Xp gain bonus was turned {0}." },
                { "Toggle Bonus Inventory Slot", "Inventory slot bonus was turned {0}." },
                { "Toggle Bonus Health", "Health bonus was turned {0}." },
                { "Toggle Stat Choice Popup", "The level up stat increase popup was turned {0}." },
                { "Toggle Requirement Throne", "Throne level requirement was turned {0}." },
                { "Toggle Requirement Crest", "Crest damage level requirement was turned {0}." },
                { "Toggle Requirement Cube", "Cube damage level requirement was turned {0}." },
                { "Toggle Requirement Pvp", "Pvp level requirement was turned {0}." },
                { "Toggle Requirement Roping", "Roping level requirement was turned {0}." },
                { "Toggle Requirement Place", "Cube place level requirement was turned {0}." },
                { "Toggle Cmd Remove My Xp", "The command /removemyxp was turned {0}." },
                { "Toggle Return Cube", "Returning a cube when too low level was turned {0}." },
                { "Toggle Crest Roping", "Roping in own crest zone was turned {0}." },
                { "Toggle Stat Reset", "Allowance for players to reset their skills was turned {0}." },
                { "Killed Guild Member", "You won't gain any xp by killing a member of your own guild!" },
                { "Xp Give", "{0} got [00FF00]{1}[FFFF00]xp[FFFFFF]." },
                { "Xp Remove", "{0} lost [00FF00]{1}[FFFF00]xp[FFFFFF]." },
                { "Points Give", "{0} got [00FF00]{1}[FFFF00]points[FFFFFF]." },
                { "Points Remove", "{0} lost [00FF00]{1}[FFFF00]points[FFFFFF]." },
                { "List Player Level", "[00ff00]{0}[ffffff] is level [00ff00]{1}[ffffff]" },
                { "List Top Players",   "{0}. {1} [FFFF00](level [00ff00]{2}[FFFF00])[ffffff]." },
                { "Current Xp", "You currently have [00FF00]{0}[FFFF00]xp[FFFFFF]." },
                { "Current Level", "Your current level is [00FF00]{0}[FFFFFF]." },
                { "Needed Xp", "You need [00FF00]{0}[FFFF00]xp[FFFFFF] more to reach the next level." },
                { "Highest Level", "You have reached the highest level possible." },
                { "Got Max Xp", "You cannot gain any more xp than you now have. Congratulations." },
                { "Xp Collected", "[00FF00]{0}[FFFF00] xp[FFFFFF] collected." },
                { "Xp Lost", "[00FF00]{0}[FFFF00] xp[FFFFFF] lost." },
                { "Level Up", "Concratulations! You reached level [00FF00]{0}[FFFFFF]!" },
                { "Level Down", "Oh no! You went back to level [00FF00]{0}[FFFFFF]!" },
                { "Not High Enough Throne Level", "Sorry. You need to be at least level [00FF00]{0}[FFFFFF] to be able to claim the throne." },
                { "Not High Enough Crest Damage Level", "Sorry. You're too low level to do damage. Become level [00FF00]{0}[FFFFFF] first." },
                { "Not High Enough Cube Damage Level", "Sorry. You're too low level to do damage. Become level [00FF00]{0}[FFFFFF] first." },
                { "Not High Enough Pvp Attack Level", "You're still under newborn protection. You can't do damage until you're level [00FF00]{0}[FFFFFF]." },
                { "Not High Enough Pvp Defense Level", "That person is still under newborn protection! You can't damage him before he gets level [00FF00]{0}[FFFFFF]." },
                { "Not High Enough Roping Own Level" , "Your level is too low to rope others. Become level [00FF00]{0}[FFFFFF] first." },
                { "Not High Enough Roping Other Level" , "That person is still under newborn protection. You can't rope him before he gets level [00FF00]{0}[FFFFFF]." },
                { "Not High Enough Cube Placing Level", "Sorry, you're too low level to place blocks of that material. Become level [00FF00]{0}[FFFFFF] first." },
                { "Current Attack Bonus", "You currently have an attack bonus of [00FF00]{0}[FFFF00] damage[FFFFFF]." },
                { "Current Defense Bonus", "You currently have a defense bonus of [00FF00]{0}[FFFF00] damage[FFFFFF]." },
                { "Popup Increase Stat Question", "Which of your skills do you want to increase?" },
                { "Popup Increase Amount Question", "With how many points do you want to increase this stat?" },
                { "Popup Increase Stat To Do", "Please type in the name of the stat." },
                { "Popup Increase Stat No Points", "You don't have any points available to upgrade your skills." },
                { "Popup Increase Stat Not Enough Points", "You don't have enough points available to upgrade your stat with that amount." },
                { "Popup Increase Stat No Stat", "Sorry but we're unable to do that." },
                { "Popup Increased Inventory", "You have increased your inventory." },
                { "Popup Increased Damage Siege", "You have increased your damage done with siege weapons." },
                { "Popup Increased Damage Cube", "You have increased your damage against blocks with a normal weapon." },
                { "Popup Increased Damage Player", "You have increased your damage against players." },
                { "Popup Increased Damage Beast", "You have increased your damage against beasts." },
                { "Popup Increased Defense Player", "You have increased your defense against players." },
                { "Popup Increased Defense Beast", "You have increased your defense against beasts." },
                { "Skill Points Available", "You still have [00FF00]{0}[FFFFFF] skill point(s) available." },
                { "Skill Points Gained", "You got {0} skill point(s). Use /levelup to increase your skills." },
                { "Skill Points Lost", "You lost {0} skill point(s)." },
                { "Stat Show Damage Siege Ballista", "You currently do [00FF00]{0}[FFFFFF] extra damage with ballista's." },
                { "Stat Show Damage Siege Trebuchet", "You currently do [00FF00]{0}[FFFFFF] extra damage with trebuchets." },
                { "Stat Show Damage Cube", "You currently do [00FF00]{0}[FFFFFF] extra damage against block using a normal weapon." },
                { "Stat Show Damage Player", "You currently do [00FF00]{0}[FFFFFF] extra damage against players." },
                { "Stat Show Damage Beast", "You currently do [00FF00]{0}[FFFFFF] extra damage against beasts." },
                { "Stat Show Defense Player", "You currently take [00FF00]{0}[FFFFFF] less damage from players," },
                { "Stat Show Defense Beast", "You currently take [00FF00]{0}[FFFFFF] less damage from beasts." },
                { "Stat Show Inventory", "You currently have [00FF00]{0}[FFFFFF] extra inventory space." },
                { "Stat Reset", "Are you sure you want to reset your skills now? \r\nYou will be killed in order to apply some changes." },
                { "Stat Reset NeededGold", " \r\n\r\nResetting your skills will cost you {0} gold." },
                { "Stat Reset Title", "Stat reset" },
                { "Stat Reset Confirm", "Confirm" },
                { "Stat Reset Cancel", "Cancel" },
                { "Stat Reset No Gold", "You do not have enough gold." },
                { "Stat Reset Finish", "You have succesfully reset your skills." },
                { "Info Current Xp", "{0} currently has [00FF00]{1}[FFFF00]xp[FFFFFF]." },
                { "Info Current Level", "{0} current level is [00FF00]{1}[FFFFFF]." },
                { "Info Skill Points Available", "{0} still has [00FF00]{1}[FFFFFF] skill point(s) available." },
                { "Info Skill Points Total", "{0} has a total of [00FF00]{1}[FFFFFF] skill point(s)." }
            }, this);
        }

        #endregion

        #region User Commands

        [ChatCommand("xphelp")]
        private void SendPlayerHelpText(Player player, string cmd)
        {
            SendHelpText(player);
        }

        [ChatCommand("xp")]
        private void HowMuchXpAPlayerhas(Player player, string cmd)
        {
            HowMuchXpICurrentlyHave(player);
        }

        [ChatCommand("removemyxp")]
        private void ClearPlayerXp(Player player, string cmd)
        {
            if (!UseRemoveMyXpCommand) return;
            RemoveTotalPlayerXp(player);
        }

        [ChatCommand("givexp")]
        private void GivePlayerXp(Player player, string cmd, string[] input)
        {
            ChangePlayerXp(player, input, 1);
        }

        [ChatCommand("removexp")]
        private void RemovePlayerXp(Player player, string cmd, string[] input)
        {
            ChangePlayerXp(player, input, 2);
        }

        [ChatCommand("givepoints")]
        private void GivePlayerPoints(Player player, string cmd, string[] input)
        {
            ChangePlayerPoints(player, input, 1);
        }

        [ChatCommand("removepoints")]
        private void RemovePlayerPoints(Player player, string cmd, string[] input)
        {
            ChangePlayerPoints(player, input, 2);
        }

        [ChatCommand("clearxp")]
        private void RemoveAllPlayerXp(Player player, string cmd)
        {
            RemoveAllXp(player);
        }

        [ChatCommand("levellist")]
        private void ShowOnlinePlayersLevel(Player player, string cmd)
        {
            ShowAllOnlinePlayerLevels(player);
        }

        [ChatCommand("topplayers")]
        private void ShowTopPlayers(Player player, string cmd)
        {
            ShowBestPlayers(player);
        }

        [ChatCommand("xpplayer")]
        private void ShowPlayerXpInfo(Player player, string cmd, string[] input)
        {
            ShowPlayerInfo(player, input);
        }

        [ChatCommand("xppvp")]
        private void TogglePvpXp(Player player, string cmd)
        {
            ToggleXpGain(player, 1);
        }

        [ChatCommand("xppve")]
        private void TogglePveXp(Player player, string cmd)
        {
            ToggleXpGain(player, 2);
        }

        [ChatCommand("xpdamage")]
        private void ToggleDamageBonus(Player player, string cmd)
        {
            ToggleBonusses(player, 1);
        }

        [ChatCommand("xpdefense")]
        private void ToggleDefenseBonus(Player player, string cmd)
        {
            ToggleBonusses(player, 2);
        }

        [ChatCommand("xpgain")]
        private void ToggleXpGainBonus(Player player, string cmd)
        {
            ToggleBonusses(player, 3);
        }

        [ChatCommand("xpthronereq")]
        private void ToggleThroneRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 1);
        }

        [ChatCommand("xpcrestreq")]
        private void ToggleCrestRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 2);
        }

        [ChatCommand("xpcubereq")]
        private void ToggleCubeRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 3);
        }

        [ChatCommand("xppvpreq")]
        private void TogglePvpRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 4);
        }

        [ChatCommand("xpropingreq")]
        private void ToggleRopingRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 5);
        }

        [ChatCommand("xpplacingreq")]
        private void TogglePlacingRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 6);
        }

        [ChatCommand("xpcmdremovemyxp")]
        private void ToggleCommandRemoveXp(Player player, string cmd)
        {
            ToggleCommand(player);
        }

        [ChatCommand("xpcubereturn")]
        private void ToggleReturnCube(Player player, string cmd)
        {
            TogglePlaceCubeReturn(player);
        }

        [ChatCommand("xpcrestroping")]
        private void toggleCrestRoping(Player player, string cmd)
        {
            ToggleCrestRoping(player);
        }

        [ChatCommand("xpinventory")]
        private void ToggleInventorySlotBonus(Player player, string cmd)
        {
            ToggleStatBonusses(player, 1);
        }

        [ChatCommand("xpstatpopup")]
        private void ToggleLevelUpStatPopup(Player player, string cmd)
        {
            ToggleStatBonusses(player, 2);
        }

        [ChatCommand("levelup")]
        private void LevelPlayerUp(Player player, string cmd)
        {
            LevelUp(player);
        }

        [ChatCommand("xpstats")]
        private void ShowPlayerStats(Player player, string cmd)
        {
            ShowStatChanges(player);
        }

        [ChatCommand("xpstatsreset")]
        private void toggleStatReset(Player player, string cmd)
        {
            ToggleStatReset(player);
        }

        [ChatCommand("xpresetstats")]
        private void ResetPlayerStats(Player player, string cmd)
        {
            ResetStatChanges(player);
        }

        [ChatCommand("xpconvertdatafile")]
        private void ConvertOldXpData(Player player, string cmd)
        {
            if (!player.HasPermission("LevelSystem.Modify.Xp")) return;

            var playerXpData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, OldPlayerData>>("SavedPlayerXpData");
            PlayerXpData.Clear();

            foreach (var oldData in playerXpData)
            {
                PlayerXpData.Add(oldData.Key, new PlayerData(oldData.Value.Id, oldData.Value.Name, oldData.Value.Xp, oldData.Value.Points));
                var data = PlayerXpData[oldData.Key];
                data.Bonuses[Skill.PlayerDamage].Update(oldData.Value.PlayerDamage);
                data.Bonuses[Skill.PlayerDefense].Update(oldData.Value.PlayerDefense);
                data.Bonuses[Skill.BeastDamage].Update(oldData.Value.BeastDamage);
                data.Bonuses[Skill.BeastDefense].Update(oldData.Value.BeastDefense);
                data.Bonuses[Skill.CubeDamage].Update(oldData.Value.CubeDamage);
                data.Bonuses[Skill.BallistaDamage].Update(oldData.Value.SiegeDamage);
                data.Bonuses[Skill.TrebuchetDamage].Update(oldData.Value.SiegeDamage);
                data.Bonuses[Skill.InventorySlot].Update(oldData.Value.InventorySlot);
            }
            SaveXpData();
            PrintToChat(player, "Data is converted.");
        }

        #endregion

        #region Command Functions

        private void HowMuchXpICurrentlyHave(Player player)
        {
            CheckPlayerExists(player);

            var data = PlayerXpData[player.Id];

            var xpAmount = data.Xp;
            player.SendMessage(GetMessage("Current Xp", player), xpAmount);
            var level = data.Level.Id;
            player.SendMessage(GetMessage("Current Level", player), level);

            if (Levels.Count != level)
            {
                var nextLevel = Levels.FirstOrDefault(l => l.Id == level + 1);
                var neededXp = nextLevel.Xp - xpAmount;
                player.SendMessage(GetMessage("Needed Xp", player), neededXp);
            }
            else player.SendMessage(GetMessage("Highest Level", player));

            if (!UseLevelupStatChoice || data.Points <= 0) return;
            player.SendMessage(GetMessage("Skill Points Available", player), data.Points);
        }

        private void RemoveTotalPlayerXp(Player player)
        {
            CheckPlayerExists(player);
            var data = PlayerXpData[player.Id];
            data.Reset();
            data.Update(player);
            KillPlayer(player);
            player.SendMessage(GetMessage("Remove My Xp", player));
            SaveXpData();
        }

        private void ChangePlayerXp(Player player, string[] args, int type)
        {
            if (!player.HasPermission("LevelSystem.Modify.Xp")) { player.SendError(GetMessage("No Permission", player)); return; }

            Player target;
            if (args.Length < 2) target = player;
            else
            {
                target = Server.GetPlayerByName(args.Skip(1).JoinToString(" "));
                if (target == null) { player.SendError(GetMessage("Player Not Online", player)); return; }
            }

            int amount;
            if (!int.TryParse(args[0], out amount)) { player.SendError(GetMessage("No Valid Number", player)); return; }

            switch (type)
            {
                case 1:
                    GiveXp(target, amount);
                    player.SendMessage(GetMessage("Xp Give", player), target.Name, amount);
                    break;
                case 2:
                    RemoveXp(target, amount);
                    player.SendMessage(GetMessage("Xp Remove", player), target.Name, amount);
                    break;
            }
            SaveXpData();
        }

        private void ChangePlayerPoints(Player player, string[] args, int type)
        {
            if (!player.HasPermission("LevelSystem.Modify.Points")) { player.SendError(GetMessage("No Permission", player)); return; }

            Player target;
            if (args.Length < 2) target = player;
            else
            {
                target = Server.GetPlayerByName(args[1]);
                if (target == null) { player.SendError(GetMessage("Player Not Online", player)); return; }
            }

            int amount;
            if (!int.TryParse(args[0], out amount)) { player.SendError(GetMessage("No Valid Number", player)); return; }

            switch (type)
            {
                case 1:
                    GivePoints(target, amount);
                    player.SendError(GetMessage("Points Give", player), target.Name, amount);
                    break;
                case 2:
                    RemovePoints(target, amount);
                    player.SendError(GetMessage("Points Remove", player), target.Name, amount);
                    break;
            }
            SaveXpData();
        }

        private void RemoveAllXp(Player player)
        {
            if (!player.HasPermission("LevelSystem.Modify.Xp")) { player.SendError(GetMessage("No Permission", player)); return; }
            PlayerXpData.Values.Foreach(d => d.Reset());
            Server.ClientPlayers.ForEach(p => PlayerXpData[p.Id].Update(p));
            player.SendMessage(GetMessage("Xp Data Deleted", player));
            SaveXpData();
        }

        private void ShowAllOnlinePlayerLevels(Player player)
        {
            CheckPlayerExists(player);

            var onlineplayers = Server.ClientPlayers;
            foreach (var oPlayer in onlineplayers)
            {
                CheckPlayerExists(oPlayer);
                player.SendMessage(GetMessage("List Player Level", player), oPlayer.Name, PlayerXpData[oPlayer.Id].Level.Id);
            }

            SaveXpData();
        }

        private void ShowBestPlayers(Player player)
        {
            CheckPlayerExists(player);

            var topPlayers = new Dictionary<ulong, PlayerData>(PlayerXpData);
            var topList = MaxTopPlayersList;
            if (topPlayers.Keys.Count < MaxTopPlayersList) topList = topPlayers.Keys.Count;
            for (var i = 1; i <= topList; i++)
            {
                var topXpAmount = 0;
                PlayerData target = null;
                foreach (var data in topPlayers.Values)
                {
                    if (data.Xp < topXpAmount) continue;
                    target = data;
                    topXpAmount = data.Xp;
                }
                player.SendMessage(GetMessage("List Top Players", player), i, target.Name, target.Level.Id);
                topPlayers.Remove(target.Id);
            }
        }

        private void ShowPlayerInfo(Player player, string[] args)
        {
            if (!player.HasPermission("LevelSystem.Info.Player")) { player.SendError(GetMessage("No Permission", player)); return; }

            var target = Server.GetPlayerByName(args.JoinToString(" "));
            if (target == null) { player.SendError(GetMessage("Player Not Online", player)); return; }
            CheckPlayerExists(target);
            var data = PlayerXpData[target.Id];

            player.SendMessage(GetMessage("Info Current Xp", player), target.Name, data.Xp);
            player.SendMessage(GetMessage("Info Current Level", player), target.Name, data.Level.Id);
            player.SendMessage(GetMessage("Info Skill Points Available", player), target.Name, data.Points);
            player.SendMessage(GetMessage("Info Skill Points Total", player), target.Name, data.TotalPoints());
        }

        private void ToggleXpGain(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Xp")) { player.SendError(GetMessage("No Permission", player)); return; }
            switch (type)
            {
                case 1:
                    if (UsePvpXp) { UsePvpXp = false; PrintToChat(player, string.Format(GetMessage("Toggle Pvp Xp", player), "off")); }
                    else { UsePvpXp = true; PrintToChat(player, string.Format(GetMessage("Toggle Pvp Xp", player), "on")); }
                    break;
                case 2:
                    if (UsePveXp) { UsePveXp = false; PrintToChat(player, string.Format(GetMessage("Toggle Pve Xp", player), "off")); }
                    else { UsePveXp = true; PrintToChat(player, string.Format(GetMessage("Toggle Pve Xp", player), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void ToggleBonusses(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Bonus")) { player.SendError(GetMessage("No Permission", player)); return; }
            switch (type)
            {
                case 1:
                    if (UseDamageBonus) { UseDamageBonus = false; PrintToChat(player, string.Format(GetMessage("Toggle Bonus Damage", player), "off")); }
                    else { UseDamageBonus = true; PrintToChat(player, string.Format(GetMessage("Toggle Bonus Damage", player), "on")); }
                    break;
                case 2:
                    if (UseDefenseBonus) { UseDefenseBonus = false; PrintToChat(player, string.Format(GetMessage("Toggle Bonus Defense", player), "off")); }
                    else { UseDefenseBonus = true; PrintToChat(player, string.Format(GetMessage("Toggle Bonus Defense", player), "on")); }
                    break;
                case 3:
                    if (UseXpGain) { UseXpGain = false; PrintToChat(player, string.Format(GetMessage("Toggle Bonus Xp Gain", player), "off")); }
                    else { UseXpGain = true; PrintToChat(player, string.Format(GetMessage("Toggle Bonus Xp Gain", player), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void ToggleRequirements(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Requirement")) { player.SendError(GetMessage("No Permission", player)); return; }
            switch (type)
            {
                case 1:
                    if (UseThroneLevel) { UseThroneLevel = false; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Throne", player), "off")); }
                    else { UseThroneLevel = true; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Throne", player), "on")); }
                    break;
                case 2:
                    if (UseCrestLevel) { UseCrestLevel = false; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Crest", player), "off")); }
                    else { UseCrestLevel = true; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Crest", player), "on")); }
                    break;
                case 3:
                    if (UseCubeLevel) { UseCubeLevel = false; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Cube", player), "off")); }
                    else { UseCubeLevel = true; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Throne", player), "on")); }
                    break;
                case 4:
                    if (UsePvpLevel) { UsePvpLevel = false; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Pvp", player), "off")); }
                    else { UsePvpLevel = true; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Pvp", player), "on")); }
                    break;
                case 5:
                    if (UseRopingLevel) { UseRopingLevel = false; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Roping", player), "off")); }
                    else { UseRopingLevel = true; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Roping", player), "on")); }
                    break;
                case 6:
                    if (UsePlaceLevel) { UsePlaceLevel = false; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Place", player), "off")); }
                    else { UsePlaceLevel = true; PrintToChat(player, string.Format(GetMessage("Toggle Requirement Place", player), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void ToggleCommand(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Command")) { player.SendError(GetMessage("No Permission", player)); return; }
            if (UseRemoveMyXpCommand) { UseRemoveMyXpCommand = false; PrintToChat(player, string.Format(GetMessage("Toggle Cmd Remove My Xp", player), "off")); }
            else { UseRemoveMyXpCommand = true; PrintToChat(player, string.Format(GetMessage("Toggle Cmd Remove My Xp", player), "on")); }
            SaveConfigData();
        }

        private void ToggleStatBonusses(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Stats")) { player.SendError(GetMessage("No Permission", player)); return; }
            switch (type)
            {
                case 1:
                    if (UseInventoryBonus) { UseInventoryBonus = false; PrintToChat(player, string.Format(GetMessage("Toggle Inventory Slot Bonus", player), "off")); }
                    else { UseInventoryBonus = true; PrintToChat(player, string.Format(GetMessage("Toggle Inventory Slot Bonus", player), "on")); }
                    break;
                case 2:
                    if (UseLevelupStatChoice) { UseLevelupStatChoice = false; PrintToChat(player, string.Format(GetMessage("Toggle Stat Choice Popup", player), "off")); }
                    else { UseLevelupStatChoice = true; PrintToChat(player, string.Format(GetMessage("Toggle Stat Choice Popup", player), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void TogglePlaceCubeReturn(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }
            if (UseReturnCube) { UseReturnCube = false; PrintToChat(player, string.Format(GetMessage("Toggle Return Cube", player), "off")); }
            else { UseReturnCube = true; PrintToChat(player, string.Format(GetMessage("Toggle Return Cube", player), "on")); }
            SaveConfigData();
        }

        private void ToggleCrestRoping(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }
            if (UseCrestRoping) { UseCrestRoping = false; PrintToChat(player, string.Format(GetMessage("Toggle Crest Roping", player), "off")); }
            else { UseCrestRoping = true; PrintToChat(player, string.Format(GetMessage("Toggle Crest Roping", player), "on")); }
            SaveConfigData();
        }

        private void ToggleStatReset(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Modify")) { player.SendError(GetMessage("No Permission", player)); return; }
            if (UseStatReset) { UseStatReset = false; PrintToChat(player, string.Format(GetMessage("Toggle Stat Reset", player), "off")); }
            else { UseStatReset = true; PrintToChat(player, string.Format(GetMessage("Toggle Stat Reset", player), "on")); }
            SaveConfigData();
        }

        private void LevelUp(Player player)
        {
            if (!UseLevelupStatChoice) return;
            CheckPlayerExists(player);
            if (PlayerXpData[player.Id].Points == 0) { PrintToChat(player, GetMessage("Popup Increase Stat No Points", player)); return; }

            string message = "";
            message += GetMessage("Popup Increase Stat Question", player) + "\n";
            message += "\n";
            if (UseInventoryBonus) message += "[fff50c]Inventory[ffffff]\n";
            if (UseDamageBonus)
            {
                if (BallistaDamageBonus > 0 || TrebuchetDamageBonus > 0) message += "[fff50c]Siegedamage[ffffff]\n";
                if (CubeDamageBonus > 0) message += "[fff50c]Cubedamage[ffffff]\n";
                if (PlayerDamageBonus > 0) message += "[fff50c]Playerdamage[ffffff]\n";
                if (BeastDamageBonus > 0) message += "[fff50c]Beastdamage[ffffff]\n";
            }
            if (UseDefenseBonus)
            {
                if (PlayerDefenseBonus > 0) message += "[fff50c]Playerdefense[ffffff]\n";
                if (BeastDefenseBonus > 0) message += "[fff50c]Beastdefense[ffffff]\n";
            }
            message += "\n";
            message += GetMessage("Popup Increase Stat To Do", player) + "\n";

            player.ShowInputPopup("Level Up", message, "", "Upgrade", "Cancel", (options, dialogue, data) => GiveStatIncrease(player, options, dialogue));
        }

        private void ShowStatChanges(Player player)
        {
            CheckPlayerExists(player);
            var data = PlayerXpData[player.Id];
            var points = data.Level.MaxPoints;
            if (UseDamageBonus)
            {
                if (BallistaDamageBonus > 0) player.SendMessage(GetMessage("Stat Show Damage Siege Ballista", player), data.Bonuses[Skill.BallistaDamage].Get(UseLevelupStatChoice ? -1 : points));
                if (TrebuchetDamageBonus > 0) player.SendMessage(GetMessage("Stat Show Damage Siege Trebuchet", player), data.Bonuses[Skill.TrebuchetDamage].Get(UseLevelupStatChoice ? -1 : points));
                if (CubeDamageBonus > 0) player.SendMessage(GetMessage("Stat Show Damage Cube", player), data.Bonuses[Skill.CubeDamage].Get(UseLevelupStatChoice ? -1 : points));
                if (PlayerDamageBonus > 0) player.SendMessage(GetMessage("Stat Show Damage Player", player), data.Bonuses[Skill.PlayerDamage].Get(UseLevelupStatChoice ? -1 : points));
                if (BeastDamageBonus > 0) player.SendMessage(GetMessage("Stat Show Damage Beast", player), data.Bonuses[Skill.BeastDamage].Get(UseLevelupStatChoice ? -1 : points));
            }
            if (UseDefenseBonus)
            {
                if (PlayerDefenseBonus > 0) player.SendMessage(GetMessage("Stat Show Defense Player", player), data.Bonuses[Skill.PlayerDefense].Get(UseLevelupStatChoice ? -1 : points));
                if (BeastDefenseBonus > 0) player.SendMessage(GetMessage("Stat Show Defense Beast", player), data.Bonuses[Skill.BeastDefense].Get(UseLevelupStatChoice ? -1 : points));
            }
            if (UseInventoryBonus) player.SendMessage(GetMessage("Stat Show Inventory", player), data.Bonuses[Skill.InventorySlot].Get(UseLevelupStatChoice ? -1 : points));
        }

        private void ResetStatChanges(Player player)
        {
            if (!UseStatReset) return;
            var msg = GetMessage("Stat Reset", player);
            if (plugins.Exists("GrandExchange") && StatResetGold > 0)
            {
                msg += string.Format(GetMessage("Stat Reset Needed Gold", player), StatResetGold);
            }
            player.ShowConfirmPopup(GetMessage("Stat Reset Title", player), msg, GetMessage("Stat Reset Confirm", player), GetMessage("Stat Reset Cancel", player), (options, dialogue, data) => ApplyStatReset(player, options));
        }

        #endregion

        #region System Functions

        private void CheckPlayerExists(Player player)
        {
            if (PlayerXpData.ContainsKey(player.Id)) PlayerXpData[player.Id].Update(player);
            else
            {
                var data = new PlayerData(player.Id, player.Name);
                PlayerXpData.Add(player.Id, data);
                data.Update(player);
                UpdateSkills(data);
            }
        }

        private void UpdateSkills(PlayerData data)
        {
            data.UpdateBonus(Skill.PlayerDamage, PlayerDamageBonus);
            data.UpdateBonus(Skill.PlayerDefense, PlayerDefenseBonus);
            data.UpdateBonus(Skill.BeastDamage, BeastDamageBonus);
            data.UpdateBonus(Skill.BeastDefense, BeastDefenseBonus);
            data.UpdateBonus(Skill.CubeDamage, CubeDamageBonus);
            data.UpdateBonus(Skill.BallistaDamage, BallistaDamageBonus);
            data.UpdateBonus(Skill.TrebuchetDamage, TrebuchetDamageBonus);
            data.UpdateBonus(Skill.InventorySlot, InventorySlotBonus);
        }

        private void KillPlayer(Player player)
        {
            var flag = player.HasGodMode();
            if (flag) player.SetGodMode(false);
            player.Kill(DamageType.Suicide);
            if (flag) player.SetGodMode(true);
        }

        private void CalculateXpCurve()
        {
            Levels.Clear();
            for (var i = 1; i <= MaxLevel; i++)
            {
                var n = Convert.ToInt64(Math.Round(XpCurveBasis * Math.Pow(i - 1, 0.9 + XpCurveAccA / 250) * i * (i + 1) / (6 + Math.Pow(i, 2) / 50 / XpCurveAccB) + (i - 1) * XpCurveExtra, 0));
                if (n > int.MaxValue) MaxLevel = i - 1;
                else Levels.Add(new Level(i, Convert.ToInt32(n)));
            }
            RecalculateXpCurve = false;
            SaveConfigData();
        }

        private void GiveXp(Player player, int amount)
        {
            CheckPlayerExists(player);

            var data = PlayerXpData[player.Id];
            if (data.Xp == MaxPossibleXp) return;

            switch (data.GiveXp(amount))
            {
                case 3:
                    player.SendMessage(GetMessage("Got Max Xp", player));
                    break;
                case 2:
                    player.SendMessage(GetMessage("Xp Collected", player), amount);
                    player.SendMessage(GetMessage("Got Max Xp", player));
                    LeveledUp(player, data);
                    break;
                case 1:
                    player.SendMessage(GetMessage("Xp Collected", player), amount);
                    LeveledUp(player, data);
                    break;
                case 0:
                    player.SendMessage(GetMessage("Xp Collected", player), amount);
                    break;
            }

            SaveXpData();
        }

        private void RemoveXp(Player player, int amount)
        {
            CheckPlayerExists(player);

            var data = PlayerXpData[player.Id];
            if (data.Xp == 0) return;

            switch (data.RemoveXp(amount))
            {
                case 3:
                    break;
                case 2:
                    player.SendMessage(GetMessage("Xp Lost", player), amount);
                    LeveledDown(player, data);
                    break;
                case 1:
                    player.SendMessage(GetMessage("Xp Lost", player), amount);
                    LeveledDown(player, data);
                    break;
                case 0:
                    player.SendMessage(GetMessage("Xp Lost", player), amount);
                    break;
            }

            SaveXpData();
        }

        private void LeveledUp(Player player, PlayerData data)
        {
            player.SendMessage(GetMessage("Level Up", player), data.Level);

            var points = data.Level.MaxPoints - data.TotalPoints();

            if (points > 0)
            {
                data.Points += points;
                player.SendMessage(GetMessage("Skill Points Gained", player), points);
            }
            if (UseNametag) data.UpdateChatFormat(player);

            SaveXpData();
        }

        private void LeveledDown(Player player, PlayerData data)
        {
            player.SendMessage(GetMessage("Level Down", player), data.Level);

            var points = data.TotalPoints() - data.Level.MaxPoints;
            if (points > 0)
            {
                data.Points -= points;
                if (data.Points < 0) data.Points = 0;
                player.SendMessage(GetMessage("Skill Points Lost", player), points);
            }
            if (UseNametag) data.UpdateChatFormat(player);
            SaveXpData();
        }

        private void GivePoints(Player player, int amount)
        {
            if (amount <= 0) return;
            CheckPlayerExists(player);

            PlayerXpData[player.Id].Points += amount;

            SaveXpData();
        }

        private void RemovePoints(Player player, int amount)
        {
            if (amount <= 0) return;
            CheckPlayerExists(player);

            var data = PlayerXpData[player.Id];
            data.Points -= amount;
            if (data.Points < 0) data.Points = 0;

            SaveXpData();
        }

        private float CalculateDamage(PlayerData data, Skill skill, float damage)
        {
            var bonus = data.Bonuses[skill].Get(UseLevelupStatChoice ? -1 : data.Level.MaxPoints);
            if ((int)skill < 3) damage -= bonus;
            else damage += bonus;
            return damage < 0f ? 0f : (float)Math.Round(damage, 1, MidpointRounding.AwayFromZero);
        }

        private int XpAmountWithBonus(int playerLvl, int xpAmount)
        {
            var xpGainBonus = (Convert.ToDouble(playerLvl) - 1) * XpGainPerLvPercentage;
            return Convert.ToInt32(Convert.ToDouble(xpAmount) * (xpGainBonus / 100 + 1));
        }

        private void GiveStatIncrease(Player player, Options selection, Dialogue dialogue)
        {
            if (selection == Options.Cancel) return;
            var statToIncrease = dialogue.ValueMessage;
            switch (statToIncrease.ToLower())
            {
                case "inventory":
                    if (UseInventoryBonus) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                case "siegedamage":
                    if (UseDamageBonus && (BallistaDamageBonus > 0 || TrebuchetDamageBonus > 0)) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                case "cubedamage":
                    if (UseDamageBonus && CubeDamageBonus > 0) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                case "playerdamage":
                    if (UseDamageBonus && PlayerDamageBonus > 0) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                case "beastdamage":
                    if (UseDamageBonus && BeastDamageBonus > 0) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                case "playerdefense":
                    if (UseDefenseBonus && PlayerDefenseBonus > 0) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                case "beastdefense":
                    if (UseDefenseBonus && BeastDefenseBonus > 0) player.ShowInputPopup("Level Up", GetMessage("Popup Increase Amount Question", player), "", "Upgrade", "Cancel", (options, dialogue1, data) => SelectPointAmount(player, options, dialogue1, statToIncrease));
                    break;
                default:
                    player.SendMessage(GetMessage("PopupIncreaseStatNoStat", player));
                    break;
            }
        }

        private void SelectPointAmount(Player player, Options selection, Dialogue dialogue, string stat)
        {
            if (selection == Options.Cancel) return;
            int increaseAmount;
            if (!int.TryParse(dialogue.ValueMessage, out increaseAmount)) { PrintToChat(player, GetMessage("No Valid Number", player)); return; }

            var data = PlayerXpData[player.Id];
            if (data.Points < increaseAmount) { PrintToChat(player, GetMessage("Popup Increase Stat Not Enough Points", player)); return; }

            switch (stat.ToLower())
            {
                case "inventory":
                    data.UpgradeSkill(Skill.InventorySlot, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased Inventory", player));
                    break;
                case "siegedamage":
                    data.UpgradeSkill(Skill.BallistaDamage, increaseAmount);
                    data.UpgradeSkill(Skill.TrebuchetDamage, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased DamageSiege", player));
                    break;
                case "cubedamage":
                    data.UpgradeSkill(Skill.CubeDamage, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased Damage Cube", player));
                    break;
                case "playerdamage":
                    data.UpgradeSkill(Skill.PlayerDamage, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased Damage Player", player));
                    break;
                case "beastdamage":
                    data.UpgradeSkill(Skill.BeastDamage, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased Damage Beast", player));
                    break;
                case "playerdefense":
                    data.UpgradeSkill(Skill.PlayerDefense, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased Defense Player", player));
                    break;
                case "beastdefense":
                    data.UpgradeSkill(Skill.BeastDefense, increaseAmount);
                    player.ShowPopup("Stat increased", GetMessage("Popup Increased Defense Beast", player));
                    break;
            }
            AddStatBonusses(player);

            SaveXpData();
        }

        private void AddStatBonusses(Player player)
        {
            if (UseInventoryBonus) GiveInventorySpace(player);
        }

        private void GiveInventorySpace(Player player)
        {
            var inventory = player.GetInventory();
            var newSlotCount = CalculateNewInventorySlots(player);

            inventory.MaximumSlots = newSlotCount;
            inventory.Contents.SetMaxSlotCount(inventory.MaximumSlots);
        }

        private int CalculateNewInventorySlots(Player player)
        {
            var data = PlayerXpData[player.Id];
            var bonus = data.Bonuses[Skill.InventorySlot].Get(UseLevelupStatChoice ? -1 : data.Level.MaxPoints);
            var slotsToGain = Convert.ToInt32(Math.Truncate(bonus));
            return DefaultInventorySlots + slotsToGain;
        }

        private void ApplyStatReset(Player player, Options selection)
        {
            if (selection != Options.Yes) return;

            if (plugins.Exists("GrandExchange") && StatResetGold > 0)
            {
                if (!(bool)GrandExchange.Call("CanRemoveGold", player, StatResetGold)) { PrintToChat(player, GetMessage("Stat Reset No Gold")); return; }
                GrandExchange.Call("Remove Gold", player, StatResetGold);
            }

            ResetStats(player);
            player.SendMessage(GetMessage("Stat Reset Finish", player));
        }

        private void ResetStats(Player player)
        {
            PlayerXpData[player.Id].ResetSkills();
            KillPlayer(player);
        }

        private int GetCubePlaceLevel(CubeData cube)
        {
            if (cube.PrefabId < 10)
                switch (cube.Material)
                {
                    case 1:
                        return CobblestoneLevel;
                    case 2:
                        return StoneLevel;
                    case 3:
                        return ClayLevel;
                    case 4:
                        return SodLevel;
                    case 5:
                        return ThatchLevel;
                    case 6:
                        return SprucheLevel;
                    case 7:
                        return WoodLevel;
                    case 8:
                        return LogLevel;
                    case 9:
                        return ReinforcedLevel;
                    default:
                        return 1;
                }
            switch (cube.Material)
            {
                case 1:
                    if (cube.PrefabId == 10) return ReinforcedSteelDoorLevel;
                    return StoneWindowLevel;
                case 2:
                    if (cube.PrefabId == 10) return IronDoorLevel;
                    else if (cube.PrefabId == 11) return IronGateLevel;
                    return IronWindowLevel;
                case 3:
                    if (cube.PrefabId == 10) return ReinforcedIronDoorLevel;
                    return ReinforcedIronGateLevel;
                case 4:
                    if (cube.PrefabId == 10) return WoodDoorLevel;
                    return WoodGateLevel;
                case 7:
                    if (cube.PrefabId == 11) return DrawbridgeLevel;
                    return WoodWindowLevel;
                case 8:
                    if (cube.PrefabId == 11) return LongDrawbridgeLevel;
                    return StoneArchLevel;
                case 9:
                    return ReinforcedIronTrapDoorLevel;
                default:
                    return 1;
            }
        }

        private bool IsInOwnCrestArea(Player player, Vector3 position)
        {
            var crestScheme = SocialAPI.Get<CrestScheme>();
            var crest = crestScheme.GetCrestAt(position);
            return crest?.SocialId == player.GetGuild().BaseID;
        }

        private bool IsAnimal(Entity e)
        {
            return e.Has<MonsterEntity>() || e.Has<CritterEntity>();
        }

        #endregion

        #region Hooks

        private void OnPlayerRespawn(PlayerRespawnEvent respawnEvent)
        {
            CheckPlayerExists(respawnEvent.Player);
            AddStatBonusses(respawnEvent.Player);

            if (UseNametag) PlayerXpData[respawnEvent.Player.Id].UpdateChatFormat(respawnEvent.Player);
        }

        private void OnPlayerConnected(Player player)
        {
            #region Checks
            if (player == null) return;
            #endregion
            CheckPlayerExists(player);
            if (UseNametag) PlayerXpData[player.Id].UpdateChatFormat(player);
        }

        private void OnPlayerChat(PlayerEvent e)
        {
            CheckPlayerExists(e.Player);
        }

        private void OnEntityDeath(EntityDeathEvent deathEvent)
        {
            #region Null Checks
            if (deathEvent == null) return;
            if (deathEvent.Cancelled) return;
            if (deathEvent.KillingDamage == null) return;
            if (deathEvent.KillingDamage.DamageSource == null) return;
            if (deathEvent.Entity == null) return;
            if (deathEvent.KillingDamage.DamageSource == deathEvent.Entity) return;
            if (!deathEvent.KillingDamage.DamageSource.IsPlayer) return;
            #endregion

            var player = deathEvent.KillingDamage.DamageSource.Owner;
            if (player == null) return;
            CheckPlayerExists(player);
            var pData = PlayerXpData[player.Id];
            var playerLevel = pData.Level.Id;

            var entity = deathEvent.Entity;

            if (!deathEvent.Entity.IsPlayer)
            {
                if (!UsePveXp) return;
                if (!IsAnimal(entity)) return;

                var villager = entity.name.Contains("Plague Villager");
                var bear = entity.name.Contains("Grizzly Bear");
                var wolf = entity.name.Contains("Wolf");
                var werewolf = entity.name.Contains("Werewolf");

                int xpAmount;
                if (villager || bear || wolf || werewolf)
                    xpAmount = UseXpGain ? 
                        Random.Next(XpAmountWithBonus(playerLevel, MonsterKillMinXp), XpAmountWithBonus(playerLevel, MonsterKillMaxXp + 1)) :
                        Random.Next(MonsterKillMinXp, MonsterKillMaxXp + 1);
                else xpAmount = Random.Next(AnimalKillMinXp, AnimalKillMaxXp + 1);
                
                GiveXp(player, xpAmount);
            }
            else
            {
                if (!UsePvpXp) return;

                var victim = deathEvent.Entity.Owner;
                if (victim == null) return;
                if (player.Name.ToLower().Contains("server") || victim.Name.ToLower().Contains("server")) return;
                CheckPlayerExists(victim);
                var vData = PlayerXpData[victim.Id];

                if (victim.GetGuild() == null || player.GetGuild() == null) return;
                if (victim.GetGuild().BaseID == player.GetGuild().BaseID)
                {
                    PrintToChat(player, GetMessage("Killed Guild Member", player));
                    return;
                }

                var victimLevel = vData.Level.Id;
                var lvlDiff = playerLevel - victimLevel;
                var xpGain = UseXpGain ? 
                    Random.Next(XpAmountWithBonus(playerLevel, PvpGetMinXp), XpAmountWithBonus(playerLevel, PvpGetMaxXp) + 1) :
                    Random.Next(PvpGetMinXp, PvpGetMaxXp + 1);
                var xpLoss = UseXpGain ? 
                    Random.Next(XpAmountWithBonus(victimLevel, PvpLoseMinXp), XpAmountWithBonus(victimLevel, PvpLoseMaxXp) + 1) :
                    Random.Next(PvpLoseMinXp, PvpLoseMaxXp + 1);
                var xpLossLvlDiff = 100 - PvpXpLossPercentage * lvlDiff;
                if (xpLossLvlDiff < 0) xpLossLvlDiff = 0;
                else if (xpLossLvlDiff > 100) xpLossLvlDiff = 100;
                xpLossLvlDiff = xpLossLvlDiff / 100;

                xpGain = (int)(xpGain * xpLossLvlDiff);
                xpLoss = (int)(xpLoss * xpLossLvlDiff);
                
                GiveXp(player, xpGain);
                RemoveXp(victim, xpLoss);
            }

            SaveXpData();
        }

        private void OnCubeTakeDamage(CubeDamageEvent damageEvent)
        {
            #region Checks
            if (damageEvent == null) return;
            if (damageEvent.Cancelled) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.Damager == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (!damageEvent.Damage.DamageSource.IsPlayer) return;
            if (damageEvent.Damage.Amount <= 0) return;
            #endregion

            var damageSource = damageEvent.Damage.Damager.name;
            var player = damageEvent.Damage.DamageSource.Owner;
            if (player == null) return;
            CheckPlayerExists(player);
            var data = PlayerXpData[player.Id];
            var currentLevel = data.Level.Id;

            if (UseCubeLevel && currentLevel < CubeLevel)
            {
                player.SendError(GetMessage("Not High Enough Cube Damage Level", player), CubeLevel);
                var centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(damageEvent.Position);
                if (centralPrefabAtLocal != null)
                {
                    var component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                    if (component != null) component.info.NotSalvageable = true;
                }
                damageEvent.Damage.Amount = 0f;
                damageEvent.Cancel();
                return;
            }
            else
            {
                var centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(damageEvent.Position);
                if (centralPrefabAtLocal != null)
                {
                    var component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                    if (component != null) component.info.NotSalvageable = false;
                }
            }

            var damage = damageEvent.Damage.Amount;
            if (damageSource.Contains("Ballista")) damageEvent.Damage.Amount = CalculateDamage(data, Skill.BallistaDamage, damage);
            else if (damageSource.Contains("Trebuchet")) damageEvent.Damage.Amount = CalculateDamage(data, Skill.TrebuchetDamage, damage);
            else damageEvent.Damage.Amount = CalculateDamage(data, Skill.CubeDamage, damage);
        }

        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            #region Checks
            if (damageEvent == null) return;
            if (damageEvent.Cancelled) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (damageEvent.Entity == null) return;
            if (damageEvent.Entity == damageEvent.Damage.DamageSource) return;
            #endregion
            var sourceIsPlayer = damageEvent.Damage.DamageSource.IsPlayer;
            var entityIsPlayer = damageEvent.Entity.IsPlayer;
            var player = damageEvent.Damage.DamageSource.Owner;
            if (player == null) return;
            PlayerData pData = null;
            if (sourceIsPlayer)
            {
                CheckPlayerExists(player);
                pData = PlayerXpData[player.Id];
            }
            var victim = damageEvent.Entity.Owner;
            if (victim == null) return;
            PlayerData vData = null;
            if (entityIsPlayer)
            {
                CheckPlayerExists(victim);
                vData = PlayerXpData[victim.Id];
            }
            var damage = damageEvent.Damage.Amount;

            if (sourceIsPlayer)
            {
                var playerCurrentLevel = pData.Level.Id;

                #region Crest Requirement
                if (damageEvent.Entity.name.Contains("Crest") && UseCrestLevel)
                {
                    if (playerCurrentLevel < CrestLevel)
                    {
                        player.SendError(GetMessage("Not High Enough Crest Damage Level", player), CrestLevel);
                        damageEvent.Damage.Amount = 0f;
                        damageEvent.Cancel();
                        return;
                    }
                }
                #endregion

                if (entityIsPlayer)
                {
                    var victimCurrentLevel = vData.Level.Id;

                    #region PvP Requirement
                    if (UsePvpLevel && playerCurrentLevel < PvpLevel)
                    {
                        player.SendError(GetMessage("Not High Enough Pvp Attack Level", player), PvpLevel);
                        damageEvent.Damage.Amount = 0f;
                        damageEvent.Cancel();
                        return;
                    }
                    if (UsePvpLevel && victimCurrentLevel < PvpLevel)
                    {
                        player.SendError(GetMessage("Not High Enough Pvp Defense Level", player), PvpLevel);
                        damageEvent.Damage.Amount = 0f;
                        damageEvent.Cancel();
                        return;
                    }
                    #endregion
                    
                    if (UseDamageBonus) damage = CalculateDamage(pData, Skill.PlayerDamage, damage);
                }
                else if (UseDamageBonus && IsAnimal(damageEvent.Entity)) damage = CalculateDamage(pData, Skill.BeastDamage, damage);
            }

            #region Defense Bonus
            if (!UseDefenseBonus || !entityIsPlayer) return;
            {
                if (sourceIsPlayer) damage = CalculateDamage(vData, Skill.PlayerDefense, damage);
                if (IsAnimal(damageEvent.Entity)) damage = CalculateDamage(vData, Skill.BeastDefense, damage);
            }

            damageEvent.Damage.Amount = damage;
            #endregion
        }

        private void OnThroneCapture(AncientThroneCaptureEvent captureEvent)
        {
            #region Checks
            if (!UseThroneLevel) return;
            if (captureEvent == null) return;
            if (captureEvent.Cancelled) return;
            if (captureEvent.Player == null) return;
            #endregion

            var player = captureEvent.Player;
            CheckPlayerExists(player);
            if (PlayerXpData[player.Id].Level.Id >= ThroneLevel) return;
            if (captureEvent.State == AncientThroneCaptureEvent.States.Cancelled) return;
            captureEvent.Cancel();
            player.ShowPopup("Error", string.Format(GetMessage("Not High Enough Throne Level", player), ThroneLevel));
        }

        private void OnPlayerCapture(PlayerCaptureEvent captureEvent)
        {
            #region Checks
            if (!UseRopingLevel) return;
            if (captureEvent == null) return;
            if (captureEvent.Cancelled) return;
            if (captureEvent.Captor == null) return;
            if (captureEvent.TargetEntity == null) return;
            if (captureEvent.Captor == captureEvent.TargetEntity) return;
            if (!captureEvent.Captor.IsPlayer) return;
            if (!captureEvent.TargetEntity.IsPlayer) return;
            #endregion

            var captor = captureEvent.Captor.Owner;
            if (UseCrestRoping && IsInOwnCrestArea(captor, captureEvent.TargetEntity.Position)) return;
            CheckPlayerExists(captor);
            if (PlayerXpData[captor.Id].Level.Id < RopingLevel)
            {
                captor.SendError(GetMessage("Not High Enough Roping Own Level", captor), RopingLevel);
                captureEvent.Cancel();
                return;
            }

            var target = captureEvent.TargetEntity.Owner;
            CheckPlayerExists(target);
            if (PlayerXpData[target.Id].Level.Id >= RopingLevel) return;
            captor.SendError(GetMessage("Not High Enough Roping Other Level", captor), RopingLevel);
            captureEvent.Cancel();
        }

        private void OnCubePlacement(CubePlaceEvent placeEvent)
        {
            #region Check
            if (placeEvent == null) return;
            if (placeEvent.Cancelled) return;
            if (placeEvent.Material == 0) return;
            if (placeEvent.Entity == null) return;
            if (placeEvent.Entity.Owner == null) return;
            #endregion

            var player = placeEvent.Entity.Owner;
            CheckPlayerExists(player);
            var cube = placeEvent.Cube;

            var neededLevel = GetCubePlaceLevel(cube);
            if (PlayerXpData[player.Id].Level.Id >= neededLevel) return;

            player.SendError(GetMessage("Not High Enough Cube Placing Level", player), neededLevel);
            placeEvent.Cancel();

            if (!UseReturnCube) return;

            var blueprint = InventoryUtil.GetTilesetBlueprint(cube.Material, cube.PrefabId);
            var inventory = player.GetInventory().Contents;
            var invGameItemStack = new InvGameItemStack(blueprint, 1, null);
            inventory.AddItem(invGameItemStack);
        }

        private void SendHelpText(Player player)
        {
            player.SendMessage("[0000FF]Level Commands[FFFFFF]");
            player.SendMessage("[00FF00]/xp[FFFFFF] - Shows your current amount of xp, your current level and the amount of xp you need to reach the next level.");
            player.SendMessage("[00FF00]/levellist[FFFFFF] - Shows from all online players their current level.");
            player.SendMessage("[00FF00]/topplayers[FFFFFF] - Shows a numerical list of players ordered on their current level starting with the player with the highest level.");
            player.SendMessage("[00FF00]/levelup[FFFFFF] - Improve one of your skills at the cost of skill points.");
            player.SendMessage("[00FF00]/xpstats[FFFFFF] - Shows your current stat bonusses.");
            if (UseRemoveMyXpCommand) player.SendMessage("[00FF00]/removemyxp[FFFFFF] - Kills you, removes all your xp and puts you back at level 1. USE WITH CAUTION!");
            if (UseStatReset) player.SendMessage("[00FF00]/xpresetstats[FFFFFF] - Will reset your skills to default and refund all your skill points.");
            if (player.HasPermission("LevelSystem.Info.Player")) player.SendMessage("[00FF00]/xpplayer (player)[FFFFFF] - Gives info about a player.");
            if (player.HasPermission("LevelSystem.Modify.Xp"))
            {
                player.SendMessage("[00FF00]/givexp (amount) (optional: player)[FFFFFF] - Gives the amount of xp (optional: to the target player).");
                player.SendMessage("[00FF00]/removexp (amount) (optional: player)[FFFFFF] - Removes the amount of xp (optional: to the target player).");
                player.SendMessage("[00FF00]/clearxp[FFFFFF] - Removes all xp values from al players.");
                player.SendMessage("[00FF00]/xpconvertdatafile[FFFFFF] - Converts all data from version 0.4.6 and earlier to the new data file. Will overwrite anything already in the new data file.");
            }
            if (player.HasPermission("LevelSystem.Modify.Points"))
            {
                player.SendMessage("[00FF00]/givepoints (amount) (optional: player)[FFFFFF] - Gives the amount of skill points (optional: to the target player).");
                player.SendMessage("[00FF00]/removepoints (amount) (optional: player)[FFFFFF] - Removes the amount of skill points (optional: to the target player).");
            }
            if (player.HasPermission("LevelSystem.Toggle.Xp"))
            {
                player.SendMessage("[00FF00]/xppvp[FFFFFF] - Toggle if players can get xp from pvp.");
                player.SendMessage("[00FF00]/xppve[FFFFFF] - Toggle if players can get xp from pve.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Bonus"))
            {
                player.SendMessage("[00FF00]/xpdamage[FFFFFF] - Toggle the use of the damage bonus.");
                player.SendMessage("[00FF00]/xpdefense[FFFFFF] - Toggle the use of the defense bonus.");
                player.SendMessage("[00FF00]/xpgain[FFFFFF] - Toggle the use of the xp gain bonus.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Stats"))
            {
                player.SendMessage("[00FF00]/xpinventory[FFFFFF] - Toggle the inventory slot bonus.");
                player.SendMessage("[00FF00]/xpstatpopup[FFFFFF] - Toggle the use of the levelup skill popup.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Requirement"))
            {
                player.SendMessage("[00FF00]/xpthronereq[FFFFFF] - Toggle the throne level requirement.");
                player.SendMessage("[00FF00]/xpcrestreq[FFFFFF] - Toggle the crest damage level requirement.");
                player.SendMessage("[00FF00]/xpcubereq[FFFFFF] - Toggle the cube damage level requirement.");
                player.SendMessage("[00FF00]/xppvpreq[FFFFFF] - Toggle the pvp level requirement.");
                player.SendMessage("[00FF00]/xpropingreq[FFFFFF] - Toggle the roping level requirement.");
                player.SendMessage("[00FF00]/xpplacingreq[FFFFFF] - Toggle the cube placing level requirement.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Command"))
            {
                player.SendMessage("[00FF00]/xpcmdremovemyxp[FFFFFF] - Toggle the ability to use the /removemyxp command.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Modify"))
            {
                player.SendMessage("[00FF00]/xpcubereturn[FFFFFF] - Toggle returning the placed cube when not meeting the level requirement.");
                player.SendMessage("[00FF00]/xpcrestroping[FFFFFF] - Toggle roping players that do not meet the roping level requirement in own crest area.");
                player.SendMessage("[00FF00]/xpstatreset[FFFFFF] - Toggle the allowance for players to reset their skill points.");
            }
        }

        #endregion

        #region Utility

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
            }
            object value;
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        private string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player?.Id.ToString());

        private int GetCurrentLevel(Player player)
        {
            CheckPlayerExists(player);
            return PlayerXpData[player.Id].Level.Id;
        }

        #endregion
    }
}