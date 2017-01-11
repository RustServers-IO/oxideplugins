using System;
using System.Linq;
using System.Collections.Generic;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events.Entities;
using Oxide.Core;
using CodeHatch.Thrones.AncientThrone;
using CodeHatch.Blocks;
using CodeHatch.Thrones.Weapons.Salvage;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Engine.Entities.Definitions;
using CodeHatch.Thrones.Stamina;
using UnityEngine;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Engine.Behaviours;
using CodeHatch.Engine.Core.Cache;
using CodeHatch.Damaging;
using CodeHatch.Networking.Events;
using CodeHatch.Thrones.Capture;
using CodeHatch;
using CodeHatch.Networking.Events.Entities.Objects.Gadgets;
using static CodeHatch.Blocks.Networking.Events.CubeEvent;
using CodeHatch.Thrones.SocialSystem;
using CodeHatch.Engine.Modules.SocialSystem;

namespace Oxide.Plugins
{
    [Info("LevelSystem", "D-Kay", "0.5.4", ResourceId = 1822)]
    public class LevelSystem : ReignOfKingsPlugin
    {
        #region Variables

        private bool usePvpXp = true; // Turns on/off gold for PVP.
        private bool usePveXp = true; // Turns on/off gold for PVE.
        private bool useDamageBonus = true; // Turns on/off the damage bonus.
        private bool useDefenseBonus = true; // Turns on/off the defense bonus.
        private bool useInventoryBonus = true; //Turns on/off the inventory slot bonus.
        private bool useXpGain = true; // Turns on/off the xp gain per level.
        private bool useThroneLevel = true; // Turns on/off the level requirement to take the throne.
        private bool useCrestLevel = true; // Turns on/off the level requirement to do damage against a crest.
        private bool useCubeLevel = true; // Turns on/off the level requirement to do damage against cubes.
        private bool usePvpLevel = true; //Turns on/off the level requirement to do pvp damage.
        private bool useRopingLevel = true; //Turns on/off the roping requirement to do pvp damage.
        private bool useRemoveMyXpCommand = true;  //Turns on/off the ability to use the /removemyxp command.
        private bool useLevelupStatChoice = true; //Turns on/off the level up stat choice popup.
        private bool recalculateXpCurve = true; // If true will recalculate the xp curve.

        private bool usePlaceLevel = true; // Turns on/off the level requirement for placing a specific block.
        private bool useReturnCube = true; // Turns on/off the returning of the cube when placing one without the correct level.
        private bool useCrestRoping = true; // Turns on/off the allowance of roping in your own crest territory.

        // PvE settings: 
        private int monsterKillMinXp => GetConfig("XpGain", "monsterKillMinXp", 15); // Minimum amount of xp a player can get for killing a monster.
        private int monsterKillMaxXp => GetConfig("XpGain", "monsterKillMaxXp", 25); // Maximum amount of xp a player can get for killing a monster.
        private int animalKillMinXp => GetConfig("XpGain", "animalKillMinXp", 10); // Minimum amount of xp a player can get for killing an animal.
        private int animalKillMaxXp => GetConfig("XpGain", "animalKillMaxXp", 15); // Maximum amount of xp a player can get for killing an animal.
        // PvP settings:
        private int pvpGetMinXp => GetConfig("XpGain", "pvpGetMinXp", 10); // Minimum amount of xp a player can get for killing a player.
        private int pvpGetMaxXp => GetConfig("XpGain", "pvpGetMaxXp", 25); // Maximum amount of xp a player can get for killing a player.
        private int pvpLoseMinXp => GetConfig("XpGain", "pvpLoseMinXp", 10); // Minimum amount of xp a player can lose for getting killed by a player.
        private int pvpLoseMaxXp => GetConfig("XpGain", "pvpLoseMaxXp", 15); // Maximum amount of xp a player can lose for getting killed by a player.
        private double pvpXpLossPercentage => GetConfig("XpGain", "pvpXpLossPercentage", 20); // Amount of xp you get less for each level difference as percentage.
        private double xpGainPerLvPercentage => GetConfig("XpGain", "xpGainPerLvPercentage", 12); // Amount of xp you get more per level as percentage.
        // Damage bonus settings:
        private double playerDamageBonus => GetConfig("Bonusses", "playerDamageBonus", 0.2); // Damagebonus when hitting a player for each level gained.
        private double beastDamageBonus => GetConfig("Bonusses", "beastDamageBonus", 0.2); // Damagebonus when hitting a monster for each level gained.
        private double siegeDamageBonus => GetConfig("Bonusses", "siegeDamageBonus", 5); // Damagebonus when using siege weapons for each level gained.
        private double cubeDamageBonus => GetConfig("Bonusses", "cubeDamageBonus", 0.5); // Damagebonus when hitting a block without siegeweapons for each level gained.
        // Defense bonus settings:
        private double playerDefenseBonus => GetConfig("Bonusses", "playerDefenseBonus", 0.2); // Defensebonus when getting hit by a player for each level gained.
        private double beastDefenseBonus => GetConfig("Bonusses", "beastDefenseBonus", 0.5); // Defensebonus when getting hit by a monster for each level gained.
        // Inventory slot stat settings:
        private double inventorySlotBonus => GetConfig("Stats", "inventorySlotBonus", 0.5); // Inventoryslot bonus per level gained.
        private int defaultInventorySlots => GetConfig("Stats", "defaultInventorySlots", 32); // Default inventorySlots.
        // Top level settings:
        private int maxTopPlayersList => GetConfig("CommandSettings", "maxTopPlayersList", 15); // Number of players in the top list.
        // Requirement settings:
        private int throneLevel => GetConfig("Requirements", "requiredLevelThrone", 3); // Needed level to claim the throne.
        private int crestLevel => GetConfig("Requirements", "requiredLevelCrestDamage", 3); // Needed level to do damage against crests.
        private int cubeLevel => GetConfig("Requirements", "requiredLevelCubeDamage", 3); // Needed level to do damage against cubes.
        private int pvpLevel => GetConfig("Requirements", "requiredLevelPvp", 3); // Needed level to do pvp damage.
        private int ropingLevel => GetConfig("Requirements", "requiredLevelRoping", 3); // Needed level to capture players.

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

        // Xp value settings:
        private int maxLevel = 1000;
        private int xpCurveBasis => GetConfig("XpCurve", "xpCurveBasis", 10);
        private int xpCurveExtra => GetConfig("XpCurve", "xpCurveExtra", 40);
        private int xpCurveAcc_A => GetConfig("XpCurve", "xpCurveAcc_A", 10);
        private int xpCurveAcc_B => GetConfig("XpCurve", "xpCurveAcc_B", 10);
        private List<object> XpValues;
        #region DefaultXpValues
        private List<object> defaultXpValues = new List<object>()
        {
            0,
            50,
            118,
            213,
            343,
            514,
            735,
            1012,
            1351,
            1760, //lvl 10
            2242,
            2803,
            3449,
            4182,
            5006,
            5926,
            6942,
            8058,
            9275,
            10595, //lvl 20
            12017,
            13544,
            15173,
            16906,
            18742,
            20679,
            22716,
            24852,
            27084,
            29412, //30
            31832,
            34342,
            36940,
            39624,
            42390,
            45236,
            48159,
            51157,
            54226,
            57363, //40
            60566,
            63832,
            67158,
            70541,
            73979,
            77468,
            81008,
            84593,
            88224,
            91896, //50
            95607,
            99356,
            103140,
            106958,
            110806,
            114683,
            118588,
            122518,
            126472,
            130448, //60
            134444,
            138460,
            142493,
            146542,
            150607,
            154685,
            158775,
            162887,
            166990,
            171112, //70
            175242,
            179380,
            183524,
            187675,
            191830,
            195990,
            200154,
            204321,
            208490,
            212661, //80
            216834,
            221007,
            225181,
            229355,
            233529,
            237702,
            241873,
            246043,
            250221,
            254377, //90
            258541,
            262702,
            266860,
            271015,
            275166,
            279314,
            283459,
            287599,
            291735,
            295867 //100  // Maximum xp may not be above 2100000000. - 32-bit INTEGER FLOOD WARNING
        };
        #endregion

        private Dictionary<ulong, PlayerData> _playerXpData = new Dictionary<ulong, PlayerData>();

        private readonly System.Random _random = new System.Random();

        private int MaxPossibleXp;

        private class PlayerData
        {
            public ulong Id = 0;
            public string Name = "";
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

            public PlayerData() { }
        }

        #endregion

        #region Save and Load Data Methods

        private void LoadXpData()
        {
            _playerXpData = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("SavedPlayerXpData");
        }

        private void SaveXpData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SavedPlayerXpData", _playerXpData);
        }

        void Loaded()
        {
            LoadDefaultMessages();
            LoadXpData();
            LoadConfigData();

            if (recalculateXpCurve) CalculateXpCurve();

            permission.RegisterPermission("LevelSystem.Modify.Xp", this);
            permission.RegisterPermission("LevelSystem.Modify.Points", this);
            permission.RegisterPermission("LevelSystem.Toggle.Xp", this);
            permission.RegisterPermission("LevelSystem.Toggle.Bonus", this);
            permission.RegisterPermission("LevelSystem.Toggle.Requirement", this);
            permission.RegisterPermission("LevelSystem.Toggle.Command", this);
            permission.RegisterPermission("LevelSystem.Toggle.Stats", this);
            permission.RegisterPermission("LevelSystem.Toggle.Modify", this);

            MaxPossibleXp = Convert.ToInt32(XpValues[(XpValues.Count() - 1)]);
        }

        protected override void LoadDefaultConfig()
        {
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            usePvpXp = GetConfig("Toggles", "usePvpXp", true);
            usePveXp = GetConfig("Toggles", "usePveXp", true);
            useDamageBonus = GetConfig("Toggles", "useDamageBonus", true);
            useDefenseBonus = GetConfig("Toggles", "useDefenseBonus", true);
            useInventoryBonus = GetConfig("Toggles", "useInventorySlotBonus", true);
            useLevelupStatChoice = GetConfig("Toggles", "useLevelupStatChoice", true);
            useXpGain = GetConfig("Toggles", "useXpGainBonus", true);
            useThroneLevel = GetConfig("Toggles", "useThroneLevel", true);

            useCrestLevel = GetConfig("Toggles", "useCrestLevel", true);
            useCubeLevel = GetConfig("Toggles", "useCubeLevel", true);
            usePvpLevel = GetConfig("Toggles", "usePvpLevel", true);
            useRopingLevel = GetConfig("Toggles", "useRopingLevel", true);
            useRemoveMyXpCommand = GetConfig("Toggles", "useRemoveMyXpCommand", true);

            usePlaceLevel = GetConfig("Toggles", "usePlaceLevel", false);
            useReturnCube = GetConfig("Toggles", "useReturnCube", true);
            useCrestRoping = GetConfig("Toggles", "useCrestRoping", true);

            XpValues = GetConfig("XpCurve", "xpNeededPerLevel", defaultXpValues);
            recalculateXpCurve = GetConfig("XpCurve", "recalculateXpCurve", true);
            maxLevel = GetConfig("XpCurve", "maxLevel", 1000);
        }

        private void SaveConfigData()
        {
            Config["XpGain", "monsterKillMinXp"] = monsterKillMinXp;
            Config["XpGain", "monsterKillMaxXp"] = monsterKillMaxXp;
            Config["XpGain", "animalKillMinXp"] = animalKillMinXp;
            Config["XpGain", "animalKillMaxXp"] = animalKillMaxXp;

            Config["XpGain", "pvpGetMinXp"] = pvpGetMinXp;
            Config["XpGain", "pvpGetMaxXp"] = pvpGetMaxXp;
            Config["XpGain", "pvpLoseMinXp"] = pvpLoseMinXp;
            Config["XpGain", "pvpLoseMaxXp"] = pvpLoseMaxXp;

            Config["XpGain", "pvpXpLossPercentage"] = pvpXpLossPercentage;
            Config["XpGain", "xpGainPerLvPercentage"] = xpGainPerLvPercentage;

            Config["Bonusses", "playerDamageBonus"] = playerDamageBonus;
            Config["Bonusses", "beastDamageBonus"] = beastDamageBonus;
            Config["Bonusses", "siegeDamageBonus"] = siegeDamageBonus;
            Config["Bonusses", "cubeDamageBonus"] = cubeDamageBonus;

            Config["Bonusses", "playerDefenseBonus"] = playerDefenseBonus;
            Config["Bonusses", "beastDefenseBonus"] = beastDefenseBonus;

            Config["Stats", "defaultInventorySlots"] = defaultInventorySlots;
            Config["Stats", "inventorySlotBonus"] = inventorySlotBonus;

            Config["Toggles", "usePvpXp"] = usePvpXp;
            Config["Toggles", "usePveXp"] = usePveXp;
            Config["Toggles", "useDamageBonus"] = useDamageBonus;
            Config["Toggles", "useDefenseBonus"] = useDefenseBonus;
            Config["Toggles", "useInventorySlotBonus"] = useInventoryBonus;
            Config["Toggles", "useLevelupStatChoice"] = useLevelupStatChoice;
            Config["Toggles", "useXpGainBonus"] = useXpGain;
            Config["Toggles", "useThroneLevel"] = useThroneLevel;
            Config["Toggles", "useCrestLevel"] = useCrestLevel;
            Config["Toggles", "useCubeLevel"] = useCubeLevel;
            Config["Toggles", "usePvpLevel"] = usePvpLevel;
            Config["Toggles", "useRopingLevel"] = useRopingLevel;
            Config["Toggles", "useRemoveMyXpCommand"] = useRemoveMyXpCommand;
            Config["Toggles", "usePlaceLevel"] = usePlaceLevel;
            Config["Toggles", "useReturnCube"] = useReturnCube;
            Config["Toggles", "useCrestRoping"] = useCrestRoping;

            Config["XpCurve", "maxLevel"] = maxLevel;
            Config["XpCurve", "recalculateXpCurve"] = recalculateXpCurve;
            Config["XpCurve", "xpCurveBasis"] = xpCurveBasis;
            Config["XpCurve", "xpCurveExtra"] = xpCurveExtra;
            Config["XpCurve", "xpCurveAcc_A"] = xpCurveAcc_A;
            Config["XpCurve", "xpCurveAcc_B"] = xpCurveAcc_B;

            Config["CommandSettings", "maxTopPlayersList"] = maxTopPlayersList;

            Config["Requirements", "requiredLevelThrone"] = throneLevel;
            Config["Requirements", "requiredLevelCrestDamage"] = crestLevel;
            Config["Requirements", "requiredLevelCubeDamage"] = cubeLevel;
            Config["Requirements", "requiredLevelPvp"] = pvpLevel;
            Config["Requirements", "requiredLevelRoping"] = ropingLevel;

            Config["Placement", "Sod"] = SodLevel;
            Config["Placement", "Thatch"] = ThatchLevel;
            Config["Placement", "Clay"] = ClayLevel;
            Config["Placement", "Spruche"] = SprucheLevel;
            Config["Placement", "Wood"] = WoodLevel;
            Config["Placement", "Log"] = LogLevel;
            Config["Placement", "Cobblestone"] = CobblestoneLevel;
            Config["Placement", "Reinforced Wood (Iron)"] = ReinforcedLevel;
            Config["Placement", "Stone"] = StoneLevel;

            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "PlayerNotOnline", "That player does not appear to be online right now." },
                { "NoValidNumber", "That is not a valid number." },
                { "RemoveMyXp", "You removed all your xp." },
                { "XpDataDeleted", "All xp data was deleted." },
                { "TogglePvpXp", "PvP xp was turned {0}." },
                { "TogglePveXp", "PvE xp was turned {0}." },
                { "ToggleDamageBonus", "Bonus damage was turned {0}." },
                { "ToggleDefenseBonus", "Bonus defense was turned {0}." },
                { "ToggleXpGainBonus", "Xp gain bonus was turned {0}." },
                { "ToggleInventorySlotBonus", "Inventory slot bonus was turned {0}." },
                { "ToggleHealthBonus", "Health bonus was turned {0}." },
                { "ToggleStatChoicePopup", "The level up stat increase popup was turned {0}." },
                { "ToggleThroneRequirement", "Throne level requirement was turned {0}." },
                { "ToggleCrestRequirement", "Crest damage level requirement was turned {0}." },
                { "ToggleCubeRequirement", "Cube damage level requirement was turned {0}." },
                { "TogglePvpRequirement", "Pvp level requirement was turned {0}." },
                { "ToggleRopingRequirement", "Roping level requirement was turned {0}." },
                { "TogglePlaceRequirement", "Cube place level requirement was turned {0}." },
                { "ToggleCmdRemoveMyXp", "The command /removemyxp was turned {0}." },
                { "ToggleReturnCube", "Returning a cube when too low level was turned {0}." },
                { "ToggleCrestRoping", "Roping in own crest zone was turned {0}." },
                { "KilledGuildMember", "You won't gain any xp by killing a member of your own guild!" },
                { "XpGive", "{0} got [00FF00]{1}[FFFF00]xp[FFFFFF]." },
                { "XpRemove", "{0} lost [00FF00]{1}[FFFF00]xp[FFFFFF]." },
                { "PointsGive", "{0} got [00FF00]{1}[FFFF00]points[FFFFFF]." },
                { "PointsRemove", "{0} lost [00FF00]{1}[FFFF00]points[FFFFFF]." },
                { "ListPlayerLevel", "[00ff00]{0}[ffffff] is level [00ff00]{1}[ffffff]" },
                { "ListTopPlayers",   "{0}. {1} [FFFF00](level [00ff00]{2}[FFFF00])[ffffff]." },
                { "CurrentXp", "You currently have [00FF00]{0}[FFFF00]xp[FFFFFF]." },
                { "CurrentLevel", "Your current level is [00FF00]{0}[FFFFFF]." },
                { "NeededXp", "You need [00FF00]{0}[FFFF00]xp[FFFFFF] more to reach the next level." },
                { "HighestLevel", "You have reached the highest level possible." },
                { "GotMaxXp", "You cannot gain any more xp than you now have. Congratulations." },
                { "XpCollected", "[00FF00]{0}[FFFF00] xp[FFFFFF] collected." },
                { "XpLost", "[00FF00]{0}[FFFF00] xp[FFFFFF] lost." },
                { "LevelUp", "Concratulations! You reached level [00FF00]{0}[FFFFFF]!" },
                { "LevelDown", "Oh no! You went back to level [00FF00]{0}[FFFFFF]!" },
                { "NotHighEnoughThroneLevel", "Sorry. You need to be at least level [00FF00]{0}[FFFFFF] to be able to claim the throne." },
                { "NotHighEnoughCrestDamageLevel", "Sorry. You're too low level to do damage. Become level [00FF00]{0}[FFFFFF] first." },
                { "NotHighEnoughCubeDamageLevel", "Sorry. You're too low level to do damage. Become level [00FF00]{0}[FFFFFF] first." },
                { "NotHighEnoughPvpAttackLevel", "You're still under newborn protection. You can't do damage until you're level [00FF00]{0}[FFFFFF]." },
                { "NotHighEnoughPvpDefenseLevel", "That person is still under newborn protection! You can't damage him before he gets level [00FF00]{0}[FFFFFF]." },
                { "NotHighEnoughRopingOwnLevel" , "Your level is too low to rope others. Become level [00FF00]{0}[FFFFFF] first." },
                { "NotHighEnoughRopingOtherLevel" , "That person is still under newborn protection. You can't rope him before he gets level [00FF00]{0}[FFFFFF]." },
                { "NotHighEnoughCubePlacingLevel", "Sorry, you're too low level to place blocks of that material. Become level [00FF00]{0}[FFFFFF] first." },
                { "CurrentAttackBonus", "You currently have an attack bonus of [00FF00]{0}[FFFF00] damage[FFFFFF]." },
                { "CurrentDefenseBonus", "You currently have a defense bonus of [00FF00]{0}[FFFF00] damage[FFFFFF]." },
                { "PopupIncreaseStatQuestion", "Which of your stats do you want to increase?" },
                { "PopupIncreaseStatToDo", "Please type in the name of the stat." },
                { "PopupIncreaseStatNoPoints", "You don't have any points available to upgrade your stats." },
                { "PopupIncreaseStatNoStat", "Sorry but we're unable to do that." },
                { "PopupIncreasedInventory", "You have increased your inventory." },
                { "PopupIncreasedDamageSiege", "You have increased your damage done with siege weapons." },
                { "PopupIncreasedDamageCube", "You have increased your damage against blocks with a normal weapon." },
                { "PopupIncreasedDamagePlayer", "You have increased your damage against players." },
                { "PopupIncreasedDamageBeast", "You have increased your damage against beasts." },
                { "PopupIncreasedDefensePlayer", "You have increased your defense against players." },
                { "PopupIncreasedDefenseBeast", "You have increased your defense against beasts." },
                { "LevelupPointsAvailable", "You still have [00FF00]{0}[FFFFFF] levelup point(s) available." },
                { "LevelupPointsGained", "You got 1 levelup point. Use /levelup to increase one of your stats." },
                { "LevelupPointsLost", "You lost 1 levelup point." },
                { "StatShowDamageSiege", "You currently do [00FF00]{0}[FFFFFF] extra damage with siege weapons." },
                { "StatShowDamageCube", "You currently do [00FF00]{0}[FFFFFF] extra damage against block using a normal weapon." },
                { "StatShowDamagePlayer", "You currently do [00FF00]{0}[FFFFFF] extra damage against players." },
                { "StatShowDamageBeast", "You currently do [00FF00]{0}[FFFFFF] extra damage against beasts." },
                { "StatShowDefensePlayer", "You currently take [00FF00]{0}[FFFFFF] less damage from players," },
                { "StatShowDefenseBeast", "You currently take [00FF00]{0}[FFFFFF] less damage from beasts." },
                { "StatShowInventory", "You currently have [00FF00]{0}[FFFFFF] extra inventory space." }
            }, this);
        }

        #endregion

        #region User Commands

        [ChatCommand("xphelp")]
        private void SendPlayerHelpText(Player player, string cmd)
        {
            HelpText(player);
        }

        [ChatCommand("xp")]
        private void HowMuchXpAPlayerhas(Player player, string cmd)
        {
            HowMuchXpICurrentlyHave(player);
        }

        [ChatCommand("removemyxp")]
        private void ClearPlayerXp(Player player, string cmd)
        {
            if (!useRemoveMyXpCommand) return;
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

        [ChatCommand("xppvp")]
        private void togglePvpXp(Player player, string cmd)
        {
            ToggleXpGain(player, 1);
        }

        [ChatCommand("xppve")]
        private void togglePveXp(Player player, string cmd)
        {
            ToggleXpGain(player, 2);
        }

        [ChatCommand("xpdamage")]
        private void toggleDamageBonus(Player player, string cmd)
        {
            ToggleBonusses(player, 1);
        }

        [ChatCommand("xpdefense")]
        private void toggleDefenseBonus(Player player, string cmd)
        {
            ToggleBonusses(player, 2);
        }

        [ChatCommand("xpgain")]
        private void toggleXpGainBonus(Player player, string cmd)
        {
            ToggleBonusses(player, 3);
        }

        [ChatCommand("xpthronereq")]
        private void toggleThroneRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 1);
        }

        [ChatCommand("xpcrestreq")]
        private void toggleCrestRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 2);
        }

        [ChatCommand("xpcubereq")]
        private void toggleCubeRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 3);
        }

        [ChatCommand("xppvpreq")]
        private void togglePvpRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 4);
        }

        [ChatCommand("xpropingreq")]
        private void toggleRopingRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 5);
        }

        [ChatCommand("xpplacingreq")]
        private void togglePlacingRequirement(Player player, string cmd)
        {
            ToggleRequirements(player, 6);
        }

        [ChatCommand("xpcmdremovemyxp")]
        private void toggleCommandRemoveXp(Player player, string cmd)
        {
            ToggleCommand(player);
        }

        [ChatCommand("xpcubereturn")]
        private void toggleReturnCube(Player player, string cmd)
        {
            TogglePlaceCubeReturn(player);
        }

        [ChatCommand("xpcrestroping")]
        private void toggleCrestRoping(Player player, string cmd)
        {
            ToggleCrestRoping(player);
        }

        [ChatCommand("xpinventory")]
        private void toggleInventorySlotBonus(Player player, string cmd)
        {
            ToggleStatBonusses(player, 2);
        }

        [ChatCommand("xpstatpopup")]
        private void toggleLevelUpStatPopup(Player player, string cmd)
        {
            ToggleStatBonusses(player, 3);
        }

        [ChatCommand("levelup")]
        private void levelPlayerUp(Player player, string cmd)
        {
            LevelUp(player);
        }

        [ChatCommand("xpstats")]
        private void showPlayerStats(Player player, string cmd)
        {
            ShowStatChanges(player);
        }

        [ChatCommand("xpconvertdatafile")]
        private void convertOldXpData(Player player, string cmd)
        {
            if (!player.HasPermission("LevelSystem.Modify.Xp")) return;
            Dictionary<string, int> _playerXp = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, int>>("SavedplayerXp");
            _playerXpData.Clear();

            foreach (string playerId in _playerXp.Keys)
            {
                PlayerData data = new PlayerData();
                data.Name = playerId.Substring(0, playerId.IndexOf('(') - 1);
                data.Id = Convert.ToUInt64(playerId.Substring(playerId.IndexOf('(') + 1, 17));
                data.Xp = _playerXp[playerId];
                _playerXpData.Add(data.Id, data);
                _playerXpData[data.Id].Points = GetCurrentLevelById(data.Id) - 1;
            }
            SaveXpData();
            PrintToChat(player, "Data is converted.");
        }

        #endregion

        #region Command Functions

        private void HelpText(Player player)
        {
            PrintToChat(player, "[0000FF]Level Commands[FFFFFF]");
            PrintToChat(player, "[00FF00]/xp[FFFFFF] - Shows your current amount of xp, your current level and the amount of xp you need to reach the next level.");
            PrintToChat(player, "[00FF00]/levellist[FFFFFF] - Shows from all online players their current level.");
            PrintToChat(player, "[00FF00]/topplayers[FFFFFF] - Shows a numerical list of players ordered on their current level starting with the player with the highest level.");
            PrintToChat(player, "[00FF00]/levelup[FFFFFF] - Improve one of your stats at the cost of 1 levelup point.");
            PrintToChat(player, "[00FF00]/xpstats[FFFFFF] - Shows your current stat bonusses.");
            if (useRemoveMyXpCommand) PrintToChat(player, "[00FF00]/removemyxp[FFFFFF] - Kills you, removes all your xp and puts you back at level 1. USE WITH CAUTION!");
            if (player.HasPermission("LevelSystem.Modify.Xp"))
            {
                PrintToChat(player, "[00FF00]/givexp (amount) (optional: player)[FFFFFF] - Gives the amount of xp (optional: to the target player).");
                PrintToChat(player, "[00FF00]/removexp (amount) (optional: player)[FFFFFF] - Removes the amount of xp (optional: to the target player).");
                PrintToChat(player, "[00FF00]/clearxp[FFFFFF] - Removes all xp values from al players.");
                PrintToChat(player, "[00FF00]/xpconvertdatafile[FFFFFF] - Converts all data from version 0.4.6 and earlier to the new data file. Will overwrite anything already in the new data file.");
            }
            if (player.HasPermission("LevelSystem.Modify.Points"))
            {
                PrintToChat(player, "[00FF00]/givepoints (amount) (optional: player)[FFFFFF] - Gives the amount of levelup points (optional: to the target player).");
                PrintToChat(player, "[00FF00]/removepoints (amount) (optional: player)[FFFFFF] - Removes the amount of levelup points (optional: to the target player).");
            }
            if (player.HasPermission("LevelSystem.Toggle.Xp"))
            {
                PrintToChat(player, "[00FF00]/xppvp[FFFFFF] - Toggle if players can get xp from pvp.");
                PrintToChat(player, "[00FF00]/xppve[FFFFFF] - Toggle if players can get xp from pve.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Bonus"))
            {
                PrintToChat(player, "[00FF00]/xpdamage[FFFFFF] - Toggle the use of the damage bonus.");
                PrintToChat(player, "[00FF00]/xpdefense[FFFFFF] - Toggle the use of the defense bonus.");
                PrintToChat(player, "[00FF00]/xpgain[FFFFFF] - Toggle the use of the xp gain bonus.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Stats"))
            {
                PrintToChat(player, "[00FF00]/xpinventory[FFFFFF] - Toggle the inventory slot bonus.");
                PrintToChat(player, "[00FF00]/xpstatpopup[FFFFFF] - Toggle the use of the levelup stats popup.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Requirement"))
            {
                PrintToChat(player, "[00FF00]/xpthronereq[FFFFFF] - Toggle the throne level requirement.");
                PrintToChat(player, "[00FF00]/xpcrestreq[FFFFFF] - Toggle the crest damage level requirement.");
                PrintToChat(player, "[00FF00]/xpcubereq[FFFFFF] - Toggle the cube damage level requirement.");
                PrintToChat(player, "[00FF00]/xppvpreq[FFFFFF] - Toggle the pvp level requirement.");
                PrintToChat(player, "[00FF00]/xpropingreq[FFFFFF] - Toggle the roping level requirement.");
                PrintToChat(player, "[00FF00]/xpplacingreq[FFFFFF] - Toggle the cube placing level requirement.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Command"))
            {
                PrintToChat(player, "[00FF00]/xpcmdremovemyxp[FFFFFF] - Toggle the ability to use the /removemyxp command.");
            }
            if (player.HasPermission("LevelSystem.Toggle.Modify"))
            {
                PrintToChat(player, "[00FF00]/xpcubereturn[FFFFFF] - Toggle returning the placed cube when not meeting the level requirement.");
                PrintToChat(player, "[00FF00]/xpcrestroping[FFFFFF] - Toggle roping players that do not meet the roping level requirement in own crest area.");
            }
        }

        private void HowMuchXpICurrentlyHave(Player player)
        {
            CheckPlayerExcists(player);
      
            var XpAmount = _playerXpData[player.Id].Xp;
            PrintToChat(player, String.Format(GetMessage("CurrentXp", player.Id.ToString()), XpAmount));
            int level = GetCurrentLevel(player);
            PrintToChat(player, String.Format(GetMessage("CurrentLevel", player.Id.ToString()), level));

            if (XpValues.Count() != level)
            {
                int NextLevelXp = Convert.ToInt32(XpValues[level]);
                int NeededXp = NextLevelXp - XpAmount;
                PrintToChat(player, String.Format(GetMessage("NeededXp", player.Id.ToString()), NeededXp));
            }
            else
            {
                PrintToChat(player, GetMessage("HighestLevel", player.Id.ToString()));
            }
            /*if (useDamageBonus)
            {
                string damage = (Convert.ToDouble(level) * playerDamageBonus).ToString();
                PrintToChat(player, String.Format(GetMessage("CurrentAttackBonus", player.Id.ToString()), damage));
            }
            if (useDefenseBonus)
            {
                string defense = (Convert.ToDouble(level) * playerDefenseBonus * -1).ToString();
                PrintToChat(player, String.Format(GetMessage("CurrentDefenseBonus", player.Id.ToString()), defense));
            }*/
            if (useLevelupStatChoice && _playerXpData[player.Id].Points > 0)
            {
                string levelupPoints = _playerXpData[player.Id].Points.ToString();
                PrintToChat(player, String.Format(GetMessage("LevelupPointsAvailable", player.Id.ToString()), levelupPoints));
            }
        }

        private void RemoveTotalPlayerXp(Player player)
        {
            if (_playerXpData.ContainsKey(player.Id)) { _playerXpData.Remove(player.Id); }
            CheckPlayerExcists(player);
            PrintToChat(player, GetMessage("RemoveMyXp", player.Id.ToString()));
            SaveXpData();
            KillPlayer(player);
        }

        private void ChangePlayerXp(Player player, string[] args, int type)
        {
            Player target;
            if (!player.HasPermission("LevelSystem.Modify.Xp")) { PrintToChat(player, "error"); ; return; }
            if (args.Length < 2)
            {
                target = player;
            }
            else
            {
                target = Server.GetPlayerByName(args[1]);
                if (target == null)
                {
                    PrintToChat(player, GetMessage("PlayerNotOnline", player.Id.ToString()));
                    return;
                }
            }
            CheckPlayerExcists(target);
            int amountToGive;
            if (Int32.TryParse(args[0], out amountToGive))
            {
                int targetCurrentLevel = GetCurrentLevel(target);
                switch (type)
                {
                    case 1:
                        GiveXp(target, Convert.ToInt32(args[0]));
                        PrintToChat(player, String.Format(GetMessage("XpGive", player.Id.ToString()), target.Name, amountToGive));
                        int pointsToGet = GetCurrentLevel(target) - targetCurrentLevel;
                        if (pointsToGet < 0) pointsToGet = 0;
                        _playerXpData[target.Id].Points += pointsToGet;
                        break;
                    case 2:
                        RemoveXp(target, Convert.ToInt32(args[0]));
                        PrintToChat(player, String.Format(GetMessage("XpRemove", player.Id.ToString()), target.Name, amountToGive));
                        int pointsToLose = GetCurrentLevel(target) - targetCurrentLevel;
                        if ((_playerXpData[target.Id].Points + pointsToLose) < 0) _playerXpData[target.Id].Points = 0;
                        else _playerXpData[target.Id].Points += pointsToLose;
                        break;
                }
                SaveXpData();
            }
            else
            {
                PrintToChat(player, GetMessage("NoValidNumber", player.Id.ToString()));
            }
        }

        private void ChangePlayerPoints(Player player, string[] args, int type)
        {
            Player target;
            if (!player.HasPermission("LevelSystem.Modify.Points")) { PrintToChat(player, "error"); ; return; }
            if (args.Length < 2)
            {
                target = player;
            }
            else
            {
                target = Server.GetPlayerByName(args[1]);
                if (target == null)
                {
                    PrintToChat(player, GetMessage("PlayerNotOnline", player.Id.ToString()));
                    return;
                }
            }
            int amountToGive;
            if (Int32.TryParse(args[0], out amountToGive))
            {
                switch (type)
                {
                    case 1:
                        GivePoints(target, Convert.ToInt32(args[0]));
                        PrintToChat(player, String.Format(GetMessage("PointsGive", player.Id.ToString()), target.Name, amountToGive));
                        break;
                    case 2:
                        RemovePoints(target, Convert.ToInt32(args[0]));
                        PrintToChat(player, String.Format(GetMessage("PointsRemove", player.Id.ToString()), target.Name, amountToGive));
                        break;
                }
                SaveXpData();
            }
            else
            {
                PrintToChat(player, GetMessage("NoValidNumber", player.Id.ToString()));
            }
        }

        private void RemoveAllXp(Player player)
        {
            if (!player.HasPermission("LevelSystem.Modify.Xp")) return;
            _playerXpData = new Dictionary<ulong, PlayerData>();
            SaveXpData();
            PrintToChat(player, GetMessage("XpDataDeleted", player.Id.ToString()));
        }

        private void ShowAllOnlinePlayerLevels(Player player)
        {
            CheckPlayerExcists(player);

            List<Player> onlineplayers = Server.ClientPlayers as List<Player>;
            foreach (Player oPlayer in onlineplayers.ToArray())
            {
                CheckPlayerExcists(oPlayer);
                if (_playerXpData.ContainsKey(oPlayer.Id)) PrintToChat(player, String.Format(GetMessage("ListPlayerLevel", player.Id.ToString()), oPlayer.Name, GetCurrentLevel(oPlayer)));
            }
        }

        private void ShowBestPlayers(Player player)
        {
            CheckPlayerExcists(player);

            Dictionary<ulong, PlayerData> TopPlayers = new Dictionary<ulong, PlayerData>(_playerXpData);
            int topList = maxTopPlayersList;
            if (TopPlayers.Keys.Count() < maxTopPlayersList) topList = TopPlayers.Keys.Count();
            for (int i = 1; i <= topList; i++)
            {
                int TopXpAmount = 0;
                PlayerData target = new PlayerData();
                foreach (PlayerData data in TopPlayers.Values)
                {
                    if (data.Xp >= TopXpAmount)
                    {
                        target = data;
                        TopXpAmount = data.Xp;
                    }
                }
                int level = GetCurrentLevelById(target.Id);
                string TPlayer = target.Name;
                PrintToChat(player, String.Format(GetMessage("ListTopPlayers", player.Id.ToString()), i.ToString(), TPlayer, level));
                TopPlayers.Remove(target.Id);
            }
        }

        private void ToggleXpGain(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Xp")) return;
            switch (type)
            {
                case 1:
                    if (usePvpXp) { usePvpXp = false; PrintToChat(player, string.Format(GetMessage("TogglePvpXp", player.Id.ToString()), "off")); }
                    else { usePvpXp = true; PrintToChat(player, string.Format(GetMessage("TogglePvpXp", player.Id.ToString()), "on")); }
                    break;
                case 2:
                    if (usePveXp) { usePveXp = false; PrintToChat(player, string.Format(GetMessage("TogglePveXp", player.Id.ToString()), "off")); }
                    else { usePveXp = true; PrintToChat(player, string.Format(GetMessage("TogglePveXp", player.Id.ToString()), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void ToggleBonusses(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Bonus")) return;
            switch (type)
            {
                case 1:
                    if (useDamageBonus) { useDamageBonus = false; PrintToChat(player, string.Format(GetMessage("ToggleDamageBonus", player.Id.ToString()), "off")); }
                    else { useDamageBonus = true; PrintToChat(player, string.Format(GetMessage("ToggleDamageBonus", player.Id.ToString()), "on")); }
                    break;
                case 2:
                    if (useDefenseBonus) { useDefenseBonus = false; PrintToChat(player, string.Format(GetMessage("ToggleDefenseBonus", player.Id.ToString()), "off")); }
                    else { useDefenseBonus = true; PrintToChat(player, string.Format(GetMessage("ToggleDefenseBonus", player.Id.ToString()), "on")); }
                    break;
                case 3:
                    if (useXpGain) { useXpGain = false; PrintToChat(player, string.Format(GetMessage("ToggleXpGainBonus", player.Id.ToString()), "off")); }
                    else { useXpGain = true; PrintToChat(player, string.Format(GetMessage("ToggleXpGainBonus", player.Id.ToString()), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void ToggleRequirements(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Requirement")) return;
            switch (type)
            {
                case 1:
                    if (useThroneLevel) { useThroneLevel = false; PrintToChat(player, string.Format(GetMessage("ToggleThroneRequirement", player.Id.ToString()), "off")); }
                    else { useThroneLevel = true; PrintToChat(player, string.Format(GetMessage("ToggleThroneRequirement", player.Id.ToString()), "on")); }
                    break;
                case 2:
                    if (useCrestLevel) { useCrestLevel = false; PrintToChat(player, string.Format(GetMessage("ToggleCrestRequirement", player.Id.ToString()), "off")); }
                    else { useCrestLevel = true; PrintToChat(player, string.Format(GetMessage("ToggleCrestRequirement", player.Id.ToString()), "on")); }
                    break;
                case 3:
                    if (useCubeLevel) { useCubeLevel = false; PrintToChat(player, string.Format(GetMessage("ToggleCubeRequirement", player.Id.ToString()), "off")); }
                    else { useCubeLevel = true; PrintToChat(player, string.Format(GetMessage("ToggleThroneRequirement", player.Id.ToString()), "on")); }
                    break;
                case 4:
                    if (usePvpLevel) { usePvpLevel = false; PrintToChat(player, string.Format(GetMessage("TogglePvpRequirement", player.Id.ToString()), "off")); }
                    else { usePvpLevel = true; PrintToChat(player, string.Format(GetMessage("TogglePvpRequirement", player.Id.ToString()), "on")); }
                    break;
                case 5:
                    if (useRopingLevel) { useRopingLevel = false; PrintToChat(player, string.Format(GetMessage("ToggleRopingRequirement", player.Id.ToString()), "off")); }
                    else { useRopingLevel = true; PrintToChat(player, string.Format(GetMessage("ToggleRopingRequirement", player.Id.ToString()), "on")); }
                    break;
                case 6:
                    if (usePlaceLevel) { usePlaceLevel = false; PrintToChat(player, string.Format(GetMessage("TogglePlaceRequirement", player.Id.ToString()), "off")); }
                    else { usePlaceLevel = true; PrintToChat(player, string.Format(GetMessage("TogglePlaceRequirement", player.Id.ToString()), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void ToggleCommand(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Command")) return;
            if (useRemoveMyXpCommand) { useRemoveMyXpCommand = false; PrintToChat(player, string.Format(GetMessage("ToggleCmdRemoveMyXp", player.Id.ToString()), "off")); }
            else { useRemoveMyXpCommand = true; PrintToChat(player, string.Format(GetMessage("ToggleCmdRemoveMyXp", player.Id.ToString()), "on")); }
            SaveConfigData();
        }

        private void ToggleStatBonusses(Player player, int type)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Stats")) return;
            switch (type)
            {
                case 1:
                    //if (useHealthBonus) { useHealthBonus = false; PrintToChat(player, string.Format(GetMessage("ToggleHealthBonus", player.Id.ToString()), "off")); }
                    //else { useHealthBonus = true; PrintToChat(player, string.Format(GetMessage("ToggleHealthBonus", player.Id.ToString()), "on")); }
                    break;
                case 2:
                    if (useInventoryBonus) { useInventoryBonus = false; PrintToChat(player, string.Format(GetMessage("ToggleInventorySlotBonus", player.Id.ToString()), "off")); }
                    else { useInventoryBonus = true; PrintToChat(player, string.Format(GetMessage("ToggleInventorySlotBonus", player.Id.ToString()), "on")); }
                    break;
                case 3:
                    if (useLevelupStatChoice) { useLevelupStatChoice = false; PrintToChat(player, string.Format(GetMessage("ToggleStatChoicePopup", player.Id.ToString()), "off")); }
                    else { useLevelupStatChoice = true; PrintToChat(player, string.Format(GetMessage("ToggleStatChoicePopup", player.Id.ToString()), "on")); }
                    break;
            }
            SaveConfigData();
        }

        private void TogglePlaceCubeReturn(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Modify")) return;
            if (useReturnCube) { useReturnCube = false; PrintToChat(player, string.Format(GetMessage("ToggleReturnCube", player.Id.ToString()), "off")); }
            else { useReturnCube = true; PrintToChat(player, string.Format(GetMessage("ToggleReturnCube", player.Id.ToString()), "on")); }
            SaveConfigData();
        }

        private void ToggleCrestRoping(Player player)
        {
            if (!player.HasPermission("LevelSystem.Toggle.Modify")) return;
            if (useCrestRoping) { useCrestRoping = false; PrintToChat(player, string.Format(GetMessage("ToggleCrestRoping", player.Id.ToString()), "off")); }
            else { useCrestRoping = true; PrintToChat(player, string.Format(GetMessage("ToggleCrestRoping", player.Id.ToString()), "on")); }
            SaveConfigData();
        }

        private void LevelUp(Player player)
        {
            if (!useLevelupStatChoice) return;
            CheckPlayerExcists(player);
            if (_playerXpData[player.Id].Points == 0) { PrintToChat(player, GetMessage("PopupIncreaseStatNoPoints", player.Id.ToString())); return; }

            string message = "";
            message += GetMessage("PopupIncreaseStatQuestion", player.Id.ToString()) + "\n";
            message += "\n";
            //if (useHealthBonus) message += "[fff50c]Health[ffffff]\n";
            if (useInventoryBonus) message += "[fff50c]Inventory[ffffff]\n";
            if (useDamageBonus)
            {
                if (siegeDamageBonus > 0) message += "[fff50c]Siegedamage[ffffff]\n";
                if (cubeDamageBonus > 0) message += "[fff50c]Cubedamage[ffffff]\n";
                if (playerDamageBonus > 0) message += "[fff50c]Playerdamage[ffffff]\n";
                if (beastDamageBonus > 0) message += "[fff50c]Beastdamage[ffffff]\n";
            }
            if (useDefenseBonus)
            {
                if (playerDefenseBonus > 0) message += "[fff50c]Playerdefense[ffffff]\n";
                if (beastDefenseBonus > 0) message += "[fff50c]Beastdefense[ffffff]\n";
            }
            message += "\n";
            message += GetMessage("PopupIncreaseStatToDo", player.Id.ToString()) + "\n";

            player.ShowInputPopup("Level Up", message, "", "Upgrade", "Cancel", (options, dialogue1, data) => GiveStatIncrease(player, options, dialogue1, data));
        }

        private void ShowStatChanges(Player player)
        {
            CheckPlayerExcists(player);
            PlayerData Data = _playerXpData[player.Id];
            if (useDamageBonus)
            {
                if (siegeDamageBonus > 0)
                {
                    int points = GetCurrentLevel(player) - 1;
                    if (useLevelupStatChoice) points = Data.SiegeDamage;
                    string bonus = (points * siegeDamageBonus).ToString();
                    PrintToChat(player, string.Format(GetMessage("StatShowDamageSiege", player.Id.ToString()), bonus));
                }
                if (cubeDamageBonus > 0)
                {
                    int points = GetCurrentLevel(player) - 1;
                    if (useLevelupStatChoice) points = Data.CubeDamage;
                    string bonus = (points * cubeDamageBonus).ToString();
                    PrintToChat(player, string.Format(GetMessage("StatShowDamageCube", player.Id.ToString()), bonus));
                }
                if (playerDamageBonus > 0)
                {
                    int points = GetCurrentLevel(player) - 1;
                    if (useLevelupStatChoice) points = Data.PlayerDamage;
                    string bonus = (points * playerDamageBonus).ToString();
                    PrintToChat(player, string.Format(GetMessage("StatShowDamagePlayer", player.Id.ToString()), bonus));
                }
                if (beastDamageBonus > 0)
                {
                    int points = GetCurrentLevel(player) - 1;
                    if (useLevelupStatChoice) points = Data.BeastDamage;
                    string bonus = (points * beastDamageBonus).ToString();
                    PrintToChat(player, string.Format(GetMessage("StatShowDamageBeast", player.Id.ToString()), bonus));
                }
            }
            if (useDefenseBonus)
            {
                if (playerDefenseBonus > 0)
                {
                    int points = GetCurrentLevel(player) - 1;
                    if (useLevelupStatChoice) points = Data.PlayerDefense;
                    string bonus = (points * playerDefenseBonus).ToString();
                    PrintToChat(player, string.Format(GetMessage("StatShowDefensePlayer", player.Id.ToString()), bonus));
                }
                if (beastDefenseBonus > 0)
                {
                    int points = GetCurrentLevel(player) - 1;
                    if (useLevelupStatChoice) points = Data.BeastDefense;
                    string bonus = (points * beastDefenseBonus).ToString();
                    PrintToChat(player, string.Format(GetMessage("StatShowDefenseBeast", player.Id.ToString()), bonus));
                }
            }
            if (useInventoryBonus)
            {
                int points = GetCurrentLevel(player) - 1;
                if (useLevelupStatChoice) points = Data.InventorySlot;
                string bonus = (points * inventorySlotBonus).ToString();
                PrintToChat(player, string.Format(GetMessage("StatShowInventory", player.Id.ToString()), bonus));
            }
        }

        #endregion

        #region System Functions

        private void KillPlayer(Player player)
        {
            bool flag = player.HasGodMode();
            if (flag)
            {
                player.SetGodMode(false, true);
            }
            player.Kill(DamageType.Suicide);
            if (flag)
            {
                player.SetGodMode(true, true);
            }
        }

        private void CalculateXpCurve()
        {
            XpValues.Clear();
            for (int i = 1; i <= maxLevel; i++)
            {
                long n = Convert.ToInt64(Math.Round((xpCurveBasis * (Math.Pow(i - 1, 0.9 + xpCurveAcc_A / 250)) * i * (i + 1) / (6 + Math.Pow(i, 2) / 50 / xpCurveAcc_B) + (i - 1) * xpCurveExtra), 0));
                if (n > int.MaxValue) maxLevel = i - 1;
                else XpValues.Add(Convert.ToInt32(n));
            }
            recalculateXpCurve = false;
            SaveConfigData();
        }

        private int GetCurrentLevel(Player player)
        {
            var XpLNow = _playerXpData[player.Id].Xp;
            int level = 0;

            while ((level < XpValues.Count()) && (XpLNow >= Convert.ToInt32(XpValues[level]))) ++level;

            return level;
        }

        private int GetCurrentLevelById(ulong player)
        {
            var XpLNow = _playerXpData[player].Xp;
            int level = 0;

            while ((level < XpValues.Count()) && (XpLNow >= Convert.ToInt32(XpValues[level]))) ++level;

            return level;
        }

        private void GiveXp(Player player, int amount)
        {
            CheckPlayerExcists(player);

            var XpNow = _playerXpData[player.Id].Xp;
            if (XpNow + amount > MaxPossibleXp)
            {
                PrintToChat(player, GetMessage("GotMaxXp", player.Id.ToString()));
                XpNow = MaxPossibleXp;
            }
            else XpNow = XpNow + amount;

            _playerXpData[player.Id].Xp = XpNow;
            SaveXpData();
        }

        private void RemoveXp(Player player, int amount)
        {
            CheckPlayerExcists(player);

            var XpNow = _playerXpData[player.Id].Xp;
            XpNow = XpNow - amount;
            if (XpNow < 0) XpNow = 0;

            _playerXpData[player.Id].Xp = XpNow;
            SaveXpData();
        }

        private void GivePoints(Player player, int amount)
        {
            CheckPlayerExcists(player);

            int points = _playerXpData[player.Id].Points;
            if (amount <= 0) return;
            points += amount;

            _playerXpData[player.Id].Points = points;
            SaveXpData();
        }

        private void RemovePoints(Player player, int amount)
        {
            CheckPlayerExcists(player);

            int points = _playerXpData[player.Id].Points;
            if (amount <= 0) return;
            points -= amount;
            if (points < 0) points = 0;

            _playerXpData[player.Id].Points = points;
            SaveXpData();
        }

        private void CheckPlayerExcists(Player player)
        {
            if (_playerXpData.ContainsKey(player.Id))
            {
                if (_playerXpData[player.Id].Name == player.Name) return;
                _playerXpData[player.Id].Name = player.Name;
            }
            else
            {
                PlayerData data = new PlayerData();
                data.Id = player.Id;
                data.Name = player.Name;
                _playerXpData.Add(player.Id, data);
            }
            
            SaveXpData();
        }

        private long CalculateDamage(Player player, int PlayerLvl, double Damage, double Bonus, int type)
        {
            double bonus = (PlayerLvl-1) * Bonus;
            if (useLevelupStatChoice) switch (type)
            {
                case 1:
                    bonus = _playerXpData[player.Id].PlayerDamage * Bonus;
                    break;
                case 2:
                    bonus = _playerXpData[player.Id].BeastDamage * Bonus;
                    break;
                case 3:
                    bonus = _playerXpData[player.Id].PlayerDefense * Bonus;
                    break;
                case 4:
                    bonus = _playerXpData[player.Id].BeastDefense * Bonus;
                    break;
                case 5:
                    bonus = _playerXpData[player.Id].SiegeDamage * Bonus;
                    break;
                case 6:
                    bonus = _playerXpData[player.Id].CubeDamage * Bonus;
                    break;
            }

            if (type == 3 || type == 4) Damage = Damage - bonus;
            else Damage = Damage + bonus;
            
            if (Damage < 0) return 0;
            return Convert.ToInt64(Math.Round(Damage, 1, MidpointRounding.AwayFromZero));
        }

        private int XpAmountWithBonus(int playerLvl, int XpAmount)
        {
            double xpGainBonus = (Convert.ToDouble(playerLvl) - 1) * xpGainPerLvPercentage;
            XpAmount = Convert.ToInt32(Convert.ToDouble(XpAmount) * (xpGainBonus / 100 + 1));
            return XpAmount;
        }

        private void DoNothing() { /*Needed for popup messages*/ }

        private void LeveledUp(Player player)
        {
            PrintToChat(player, String.Format(GetMessage("LevelUp", player.Id.ToString()), GetCurrentLevel(player)));
            int totalPoints = 
                _playerXpData[player.Id].BeastDamage + 
                _playerXpData[player.Id].BeastDefense + 
                _playerXpData[player.Id].CubeDamage + 
                _playerXpData[player.Id].InventorySlot + 
                _playerXpData[player.Id].PlayerDamage + 
                _playerXpData[player.Id].PlayerDefense + 
                _playerXpData[player.Id].SiegeDamage + 
                _playerXpData[player.Id].Points;
            if (GetCurrentLevel(player) > totalPoints)
            {
                _playerXpData[player.Id].Points += 1;
                PrintToChat(player, GetMessage("LevelupPointsGained", player.Id.ToString()));
            }
            SaveXpData();
        }

        private void LeveledDown(Player player)
        {
            PrintToChat(player, String.Format(GetMessage("LevelDown", player.Id.ToString()), GetCurrentLevel(player)));
            if (_playerXpData[player.Id].Points > 0) { _playerXpData[player.Id].Points -= 1; PrintToChat(player, GetMessage("LevelupPointsLost", player.Id.ToString())); }
            SaveXpData();
        }

        private void GiveStatIncrease(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            if (selection == Options.Cancel) return;
            string statToIncrease = dialogue.ValueMessage;
            switch (statToIncrease.ToLower())
            {
                case "inventory":
                    if (useInventoryBonus) { _playerXpData[player.Id].InventorySlot += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedInventory", player.Id.ToString())); }
                    break;
                case "siegedamage":
                    if (useDamageBonus && siegeDamageBonus > 0) { _playerXpData[player.Id].SiegeDamage += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedDamageSiege", player.Id.ToString())); }
                    break;
                case "cubedamage":
                    if (useDamageBonus && cubeDamageBonus > 0) { _playerXpData[player.Id].CubeDamage += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedDamageCube", player.Id.ToString())); }
                    break;
                case "playerdamage":
                    if (useDamageBonus && playerDamageBonus > 0) { _playerXpData[player.Id].PlayerDamage += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedDamagePlayer", player.Id.ToString())); }
                    break;
                case "beastdamage":
                    if (useDamageBonus && beastDamageBonus > 0) { _playerXpData[player.Id].BeastDamage += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedDamageBeast", player.Id.ToString())); }
                    break;
                case "playerdefense":
                    if (useDefenseBonus && playerDefenseBonus > 0) { _playerXpData[player.Id].PlayerDefense += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedDefensePlayer", player.Id.ToString())); }
                    break;
                case "beastdefense":
                    if (useDefenseBonus && beastDefenseBonus > 0) { _playerXpData[player.Id].BeastDefense += 1; _playerXpData[player.Id].Points -= 1; player.ShowPopup("Stat increased", GetMessage("PopupIncreasedDefenseBeast", player.Id.ToString())); }
                    break;
                default:
                    PrintToChat(player, GetMessage("PopupIncreaseStatNoStat", player.Id.ToString()));
                    break;
            }
            SaveXpData();
            AddStatBonusses(player);
        }
        /*
        private void GiveHealthRegenBonus(Player player)
        {
            PlayerHealth health = player.GetHealth();
            double bonus = CalculateHealthRegenBonus(player);

            health.regen = (float)(defaultHealthHead + bonus[0]);
        }

        private double CalculateHealthRegenBonus(Player player)
        {
            PlayerHealth health = player.GetHealth();
            double playerPoints = GetCurrentLevel(player) - 1;
            if (useLevelupStatChoice) playerPoints = _playerXpData[player.Id].HealthRegen;
            double regenBonus = playerPoints * healthRegenBonus;
            return regenBonus;
        }
        */
        private void GiveInventorySpace(Player player)
        {
            Container inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);

            int oldSlotCount = inventory.MaximumSlots;
            int newSlotCount = CalculateNewInventorySlots(player);

            inventory.MaximumSlots = newSlotCount;

            if (oldSlotCount < newSlotCount) RefreshInventory(player);
        }

        private int CalculateNewInventorySlots(Player player)
        {
            int playerPoints = GetCurrentLevel(player);
            if (useLevelupStatChoice) playerPoints = _playerXpData[player.Id].InventorySlot;
            double bonus = playerPoints * inventorySlotBonus;
            int slotsToGain = 0;
            while (bonus >= 1) { slotsToGain++; bonus--; }
            return defaultInventorySlots + slotsToGain;
        }

        private void RefreshInventory(Player player)
        {
            if (player.Entity == null) return;
            Container[] containerArray = player.CurrentCharacter.Entity.TryGetArray<Container>();
            if (containerArray == null) return;
            for (int i = 0; i < containerArray.Length; i++)
            {
                if (containerArray[i].Contents.IsUnique)
                {
                    if (containerArray[i].Contents.IsType(CollectionTypes.Inventory))
                    {
                        containerArray[i].Contents.SetMaxSlotCount(containerArray[i].MaximumSlots, true);
                    }
                }
            }
        }
        
        private void AddStatBonusses(Player player)
        {
            //if (useHealthBonus) GiveHealthRegenBonus(player);
            if (useInventoryBonus) GiveInventorySpace(player);
        }

        private string GetCubeName(CubeData cube)
        {
            string cubeName = "";
            int amount = 1;
            switch (cube.Material)
            {
                case 0:
                    amount = 0;
                    break;
                case 1:
                    cubeName += "Cobblestone ";
                    break;
                case 2:
                    cubeName += "Stone ";
                    break;
                case 3:
                    cubeName += "Clay ";
                    break;
                case 4:
                    cubeName += "Sod ";
                    break;
                case 5:
                    cubeName += "Thatch ";
                    break;
                case 6:
                    cubeName += "Spruce Branches ";
                    break;
                case 7:
                    cubeName += "Wood ";
                    break;
                case 8:
                    cubeName += "Log ";
                    break;
                case 9:
                    cubeName += "Reinforced Wood (Iron) ";
                    break;
            }
            switch (cube.PrefabId)
            {
                case 0:
                    cubeName += "Block";
                    break;
                case 1:
                    cubeName += "Stairs";
                    break;
                case 2:
                    cubeName += "Ramp";
                    break;
                case 3:
                    cubeName += "Corner";
                    break;
                case 4:
                    cubeName += "Inverted Corner";
                    break;
            }
            if (amount > 0) return cubeName;
            return null;
        }

        private bool CanPlaceBlock(Player player, CubeData cube)
        {
            int levelNeeded = 1;
            switch (cube.Material)
            {
                case 0:
                    levelNeeded = 1;
                    break;
                case 1:
                    levelNeeded = CobblestoneLevel;
                    break;
                case 2:
                    levelNeeded = StoneLevel;
                    break;
                case 3:
                    levelNeeded = ClayLevel;
                    break;
                case 4:
                    levelNeeded = SodLevel;
                    break;
                case 5:
                    levelNeeded = ThatchLevel;
                    break;
                case 6:
                    levelNeeded = SprucheLevel;
                    break;
                case 7:
                    levelNeeded = WoodLevel;
                    break;
                case 8:
                    levelNeeded = LogLevel;
                    break;
                case 9:
                    levelNeeded = ReinforcedLevel;
                    break;
            }
            if (GetCurrentLevel(player) >= levelNeeded) return true;
            PrintToChat(player, string.Format(GetMessage("NotHighEnoughCubePlacingLevel", player.Id.ToString()), levelNeeded));
            return false;
        }

        private void CancelCubePlacement(CubePlaceEvent placeEvent)
        {
            Player player = placeEvent.Entity.Owner;
            CubeData cube = placeEvent.Cube;
            placeEvent.Cancel();

            if (!useReturnCube) return;

            string cubeName = GetCubeName(cube);
            if (cubeName == null) return;

            var inventory = player.GetInventory().Contents;
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName(cubeName, true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForName, 1, null);
            ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
        }

        private bool IsInOwnCrestArea(Player player)
        {
            CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
            Crest crest = crestScheme.GetCrestAt(player.Entity.Position);
            if (crest == null) return false;
            if (crest.GuildName == player.GetGuild().Name) return true;
            return false;
        }

        private bool IsInOwnCrestArea(Player player, Vector3 position)
        {
            CrestScheme crestScheme = SocialAPI.Get<CrestScheme>();
            Crest crest = crestScheme.GetCrestAt(position);
            if (crest == null) return false;
            if (crest.GuildName == player.GetGuild().Name) return true;
            return false;
        }
        
        #endregion

        #region Hooks

        private void OnPlayerRespawn(PlayerRespawnEvent respawnEvent)
        {
            AddStatBonusses(respawnEvent.Player);
        }

        private void OnPlayerConnected(Player player)
        {
            CheckPlayerExcists(player);
        }

        private void OnEntityDeath(EntityDeathEvent deathEvent)
        {
            if (deathEvent == null) return;
            if (deathEvent.KillingDamage == null) return;
            if (deathEvent.KillingDamage.DamageSource == null) return;
            if (deathEvent.Entity == null) return;
            if (deathEvent.Entity.Owner.Name == "server") return;

            if (deathEvent.KillingDamage.DamageSource.IsPlayer)
            {
                if (deathEvent.KillingDamage.DamageSource.Owner == null) return;

                var player = deathEvent.KillingDamage.DamageSource.Owner;
                if (player == null) return;

                var entity = deathEvent.Entity;

                if (!deathEvent.Entity.IsPlayer)
                {
                    if (usePveXp)
                    {
                        if (entity.name.Contains("Trebuchet")) return;
                        if (entity.name.Contains("Ballista")) return;

                        bool villager = entity.name.Contains("Plague Villager");
                        bool bear = entity.name.Contains("Grizzly Bear");
                        bool wolf = entity.name.Contains("Wolf");
                        bool werewolf = entity.name.Contains("Werewolf");

                        bool babyChicken = entity.name.Contains("Baby Chicken");
                        bool bat = entity.name.Contains("Bat");
                        bool chicken = entity.name.Contains("Chicken");
                        bool crab = entity.name.Contains("Crab");
                        bool crow = entity.name.Contains("Crow");
                        bool deer = entity.name.Contains("Deer");
                        bool duck = entity.name.Contains("Duck");
                        bool moose = entity.name.Contains("Moose");
                        bool pigeon = entity.name.Contains("Pigeon");
                        bool rabbit = entity.name.Contains("Rabbit");
                        bool rooster = entity.name.Contains("Rooster");
                        bool seagull = entity.name.Contains("Seagull");
                        bool sheep = entity.name.Contains("Sheep");
                        bool stag = entity.name.Contains("Stag");

                        int XpAmount = 0;
                        if (villager || bear || wolf || werewolf) XpAmount = _random.Next(monsterKillMinXp, (monsterKillMaxXp + 1));
                        else if (babyChicken || bat || chicken || crab || crow || deer || duck || moose || pigeon || rabbit || rooster || seagull || sheep || stag) XpAmount = _random.Next(animalKillMinXp, (animalKillMaxXp + 1));
                        else return;
                        CheckPlayerExcists(player);
                        int playerLvl = GetCurrentLevel(player);

                        if (useXpGain) XpAmount = XpAmountWithBonus(playerLvl, XpAmount);
                        GiveXp(player, XpAmount);

                        if (_playerXpData[player.Id].Xp < MaxPossibleXp) PrintToChat(player, String.Format(GetMessage("XpCollected", player.Id.ToString()), XpAmount));

                        if (GetCurrentLevel(player) > playerLvl) LeveledUp(player);
                    }
                }
                else
                {
                    if (usePvpXp)
                    {
                        if (deathEvent.Entity.Owner == null) return;
                        var victim = deathEvent.Entity.Owner;
                        if (victim == null) return;
                        if (victim.Id == 0 || player.Id == 0) return;
                        if (victim == player) return;
                        if (victim.GetGuild() == null || player.GetGuild() == null) return;

                        if (victim.GetGuild().Name == player.GetGuild().Name) { PrintToChat(player, GetMessage("KilledGuildMember", player.Id.ToString())); return; }

                        CheckPlayerExcists(victim);
                        CheckPlayerExcists(player);
                        int lvlVictim = GetCurrentLevel(victim);
                        int lvlPlayer = GetCurrentLevel(player);
                        int lvlDiff = lvlPlayer - lvlVictim;
                        int xpGain = _random.Next(pvpGetMinXp, (pvpGetMaxXp + 1));
                        int xpLoss = _random.Next(pvpLoseMinXp, (pvpLoseMaxXp + 1));
                        double xpLvlLoss = (100 - (pvpXpLossPercentage * lvlDiff));
                        if (xpLvlLoss < 0) xpLvlLoss = 0;
                        else if (xpLvlLoss > 100) xpLvlLoss = 100;
                        xpLvlLoss = xpLvlLoss / 100;

                        xpGain = Convert.ToInt32(Convert.ToDouble(xpGain) * xpLvlLoss);
                        xpLoss = Convert.ToInt32(Convert.ToDouble(xpLoss) * xpLvlLoss);

                        if (useXpGain) { xpGain = XpAmountWithBonus(lvlPlayer, xpGain); xpLoss = XpAmountWithBonus(lvlPlayer, xpLoss); }
                        GiveXp(player, xpGain);
                        RemoveXp(victim, xpLoss);

                        if (_playerXpData[player.Id].Xp < MaxPossibleXp) PrintToChat(player, String.Format(GetMessage("XpCollected", player.Id.ToString()), xpGain));
                        PrintToChat(victim, String.Format(GetMessage("XpLost", player.Id.ToString()), xpLoss));

                        if (GetCurrentLevel(player) > lvlPlayer) LeveledUp(player);
                        if (GetCurrentLevel(victim) < lvlVictim) LeveledDown(victim);
                    }
                }
            }
            SaveXpData();
        }

        private void OnCubeTakeDamage(CubeDamageEvent damageEvent)
        {
            #region Null Checks
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.Damager == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            #endregion

            string damageSource = damageEvent.Damage.Damager.name.ToString();
            if (damageEvent.Damage.DamageSource.IsPlayer)
            {
                if (damageEvent.Damage.DamageSource.Owner == null) return;
                Player player = damageEvent.Damage.DamageSource.Owner;
                CheckPlayerExcists(player);
                int CurrentLevel = GetCurrentLevel(player);
                if (useCubeLevel && CurrentLevel < cubeLevel)
                {
                    PrintToChat(player, string.Format(GetMessage("NotHighEnoughCubeDamageLevel", player.Id.ToString()), cubeLevel));
                    damageEvent.Damage.Amount = 0f;
                    TilesetColliderCube centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(damageEvent.Position);
                    var component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                    component.info.NotSalvageable = true;
                    damageEvent.Cancel();
                    return;
                }
                else
                {
                    TilesetColliderCube centralPrefabAtLocal = BlockManager.DefaultCubeGrid.GetCentralPrefabAtLocal(damageEvent.Position);
                    var component = centralPrefabAtLocal.GetComponent<SalvageModifier>();
                    component.info.NotSalvageable = false;
                }
                double damage = damageEvent.Damage.Amount;

                if (damage <= 0) return;
                
                if (damageSource.Contains("Trebuchet") || damageSource.Contains("Ballista")) damageEvent.Damage.Amount = CalculateDamage(player, CurrentLevel, damage, siegeDamageBonus, 5);
                else damageEvent.Damage.Amount = CalculateDamage(player, CurrentLevel, damage, cubeDamageBonus, 6);
            }
        }

        private void OnEntityHealthChange(EntityDamageEvent damageEvent)
        {
            #region Null Checks
            if (damageEvent == null) return;
            if (damageEvent.Damage == null) return;
            if (damageEvent.Damage.DamageSource == null) return;
            if (damageEvent.Entity == null) return;
            if (damageEvent.Entity == damageEvent.Damage.DamageSource) return;
            #endregion
            bool sourceIsPlayer = damageEvent.Damage.DamageSource.IsPlayer;
            bool entityIsPlayer = damageEvent.Entity.IsPlayer;
            if (damageEvent.Cancelled) return;

            if (sourceIsPlayer)
            {
                if (damageEvent.Damage.DamageSource.Owner == null) return;
                Player player = damageEvent.Damage.DamageSource.Owner;
                CheckPlayerExcists(player);
                int PlayerCurrentLevel = GetCurrentLevel(player);

                #region Crest Requirement
                if (damageEvent.Entity.name.Contains("Crest") && useCrestLevel)
                {
                    if (PlayerCurrentLevel < crestLevel)
                    {
                        PrintToChat(player, string.Format(GetMessage("NotHighEnoughCrestDamageLevel", player.Id.ToString()), crestLevel));
                        damageEvent.Damage.Amount = 0f;
                        damageEvent.Cancel();
                        return;
                    }
                }
                #endregion

                #region Damage Bonus
                if (useDamageBonus)
                {
                    if (usePvpLevel && PlayerCurrentLevel < pvpLevel && entityIsPlayer)
                    {
                        PrintToChat(player, string.Format(GetMessage("NotHighEnoughPvpAttackLevel", player.Id.ToString()), pvpLevel));
                        damageEvent.Damage.Amount = 0f;
                        damageEvent.Cancel();
                        return;
                    }

                    double damage = damageEvent.Damage.Amount;

                    #region Animal Check
                    string entity = damageEvent.Entity.name;
                    bool villager = entity.Contains("Plague Villager");
                    bool bear = entity.Contains("Grizzly Bear");
                    bool wolf = entity.Contains("Wolf");
                    bool werewolf = entity.Contains("Werewolf");
                    bool babyChicken = entity.Contains("Baby Chicken");
                    bool bat = entity.Contains("Bat");
                    bool chicken = entity.Contains("Chicken");
                    bool crab = entity.Contains("Crab");
                    bool crow = entity.Contains("Crow");
                    bool deer = entity.Contains("Deer");
                    bool duck = entity.Contains("Duck");
                    bool moose = entity.Contains("Moose");
                    bool pigeon = entity.Contains("Pigeon");
                    bool rabbit = entity.Contains("Rabbit");
                    bool rooster = entity.Contains("Rooster");
                    bool seagull = entity.Contains("Seagull");
                    bool sheep = entity.Contains("Sheep");
                    bool stag = entity.Contains("Stag");
                    #endregion

                    if (entityIsPlayer)
                    {
                        if (damageEvent.Entity.Owner == null) return;
                        Player victim = damageEvent.Entity.Owner;
                        CheckPlayerExcists(player);
                        int VictimCurrentLevel = GetCurrentLevel(victim);
                        if (usePvpLevel && VictimCurrentLevel < pvpLevel)
                        {
                            PrintToChat(player, string.Format(GetMessage("NotHighEnoughPvpDefenseLevel", player.Id.ToString()), pvpLevel));
                            damageEvent.Damage.Amount = 0f;
                            damageEvent.Cancel();
                            return;
                        }
                        damageEvent.Damage.Amount = CalculateDamage(player, PlayerCurrentLevel, damage, playerDamageBonus, 1);
                    }
                    else if (villager || bear || wolf || werewolf || babyChicken || bat || chicken || crab || crow || deer || duck || moose || pigeon || rabbit || rooster || seagull || sheep || stag)
                    {
                        damageEvent.Damage.Amount = CalculateDamage(player, PlayerCurrentLevel, damage, beastDamageBonus, 2);
                    }
                }
                #endregion
            }

            #region Defense Bonus
            if (useDefenseBonus && entityIsPlayer)
            {
                if (damageEvent.Entity.Owner == null) return;
                Player victim = damageEvent.Entity.Owner;
                CheckPlayerExcists(victim);
                int VictimCurrentLevel = GetCurrentLevel(victim);
                double damage = damageEvent.Damage.Amount;

                string damageSource = damageEvent.Damage.DamageSource.name;
                bool villager = damageSource.Contains("Plague Villager");
                bool bear = damageSource.Contains("Grizzly Bear");
                bool wolf = damageSource.Contains("Wolf");
                bool werewolf = damageSource.Contains("Werewolf");
                if (sourceIsPlayer)
                {
                    if (damageEvent.Damage.DamageSource.Owner == null) return;
                    damageEvent.Damage.Amount = CalculateDamage(victim, VictimCurrentLevel, damage, playerDefenseBonus, 3);
                }
                if (villager || bear || wolf || werewolf) damageEvent.Damage.Amount = CalculateDamage(victim, VictimCurrentLevel, damage, beastDefenseBonus, 4);
            }
            #endregion
        }

        private void OnThroneCapture(AncientThroneCaptureEvent captureEvent)
        {
            Player player = captureEvent.Player;
            CheckPlayerExcists(player);
            if (useThroneLevel && GetCurrentLevel(player) < throneLevel)
            {
                if (captureEvent.State == AncientThroneCaptureEvent.States.Cancelled) return;
                captureEvent.Cancel();
                player.ShowPopup("Error", string.Format(GetMessage("NotHighEnoughThroneLevel", player.Id.ToString()), throneLevel.ToString()), "Ok", (selection, dialogue, data) => DoNothing());
            }
        }

        private void OnPlayerCapture(PlayerCaptureEvent captureEvent)
        {
            if (!useRopingLevel) return;
            if (captureEvent == null) return;
            if (captureEvent.Captor == null) return;
            if (captureEvent.TargetEntity == null) return;
            if (captureEvent.Captor == captureEvent.TargetEntity) return;
            if (!captureEvent.Captor.IsPlayer) return;
            if (!captureEvent.TargetEntity.IsPlayer) return;

            Player captor = captureEvent.Captor.Owner;
            if (GetCurrentLevel(captor) < ropingLevel)
            {
                PrintToChat(captor, string.Format(GetMessage("NotHighEnoughRopingOwnLevel", captor.Id.ToString()), ropingLevel));
                captureEvent.Cancel();
                return;
            }

            if (useCrestRoping && IsInOwnCrestArea(captor, captureEvent.TargetEntity.Position)) return;

            Player target = captureEvent.TargetEntity.Owner;
            if (GetCurrentLevel(target) < ropingLevel)
            {
                PrintToChat(captor, string.Format(GetMessage("NotHighEnoughRopingOtherLevel", captor.Id.ToString()), ropingLevel));
                captureEvent.Cancel();
                return;
            }
        }

        private void OnCubePlacement(CubePlaceEvent placeEvent)
        {
            Player player = placeEvent.Entity.Owner;
            CubeData cube = placeEvent.Cube;
            if (!CanPlaceBlock(player, cube)) CancelCubePlacement(placeEvent);
        }

        private void SendHelpText(Player player)
        {
            HelpText(player);
        }

        #endregion

        #region Helpers

        private T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}