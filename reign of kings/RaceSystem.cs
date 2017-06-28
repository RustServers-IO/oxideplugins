using System;
using System.Linq;
using System.Collections.Generic;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using Oxide.Core;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events.Players;
using CodeHatch.UserInterface.Dialogues;
using CodeHatch.Inventory.Blueprints;

namespace Oxide.Plugins
{
    [Info("RaceSystem", "D-Kay", "0.1.3", ResourceId = 2287)]
    public class RaceSystem : ReignOfKingsPlugin
    {
        #region Variables

        private bool ChangeAppearance = true;
        private bool ChangeRace = true;

        private int ChangeTime = 2;

        List<object> _DefaultRaces = new List<object>() { "Dwarf", "Elf", "Human", "Orc" };
        List<string> _Races = new List<string>();

        Dictionary<string, string> _ColorCodes = new Dictionary<string, string>();

        Dictionary<ulong, PlayerData> _Data = new Dictionary<ulong, PlayerData>();

        class PlayerData
        {
            public string Race { get; set; }
            public string chatFormat { get; set; }
            public bool CanChange { get; set; }

            public PlayerData()
            {

            }

            public void ChangeReset()
            {
                CanChange = true;
            }

            public void ChangeExpired()
            {
                CanChange = false;
            }

            public void ChangeRace(string race)
            {
                Race = race;
                CanChange = false;
            }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadConfigData();
            LoadRaceData();

            permission.RegisterPermission("RaceSystem.Modify", this);
            permission.RegisterPermission("RaceSystem.Show", this);
        }

        private void Unload()
        {
            SaveRaceData();
            SaveConfigData();
        }

        private void LoadRaceData()
        {
            _Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("RaceSystem");
        }

        private void SaveRaceData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("RaceSystem", _Data);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            _Races = GetConfig("Races", "Names", _DefaultRaces).Cast<string>().ToList();
            ChangeAppearance = GetConfig("Tunables", "ChangeAppearance", true);
            ChangeRace = GetConfig("Tunables", "ChangeRace", true);
            ChangeTime = GetConfig("Tunables", "ChangeTime", 2);

            foreach (string race in _Races)
            {
                _ColorCodes.Add(race, GetConfig("Colors", race, "[9f0000]"));
            }
        }

        private void SaveConfigData()
        {
            Config["Races", "Names"] = _Races;
            Config["Tunables", "ChangeAppearance"] = ChangeAppearance;
            Config["Tunables", "ChangeRace"] = ChangeRace;
            Config["Tunables", "ChangeTime"] = ChangeTime;

            foreach (KeyValuePair<string,string> color in _ColorCodes)
            {
                Config["Colors", color.Key] = color.Value;
            }

            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPermission", "You do not have permission to use this command." },
                { "ToggleChangeRace", "Race changing was turned {0}." },
                { "ToggleChangeAppearance", "Appearance changing was turned {0}." },
                { "InvalidArgs", "That is not a valid amount." },
                { "InvalidRace", "That race does not excist." },
                { "NoArgs", "Please type in the amount of minutes people are able to change their race after dying '/rstime (time in minutes)'." },
                { "ChangedTime", "Change time has been set to {0} minutes." },
                { "NoRace", "Please use the command /rschange to change your race." },
                { "NoRaceRepeat", "You have not selected a race yet. Please use the command /rschange to change your race." },
                { "NoPlayer", "There is no player with that name online right now." },
                { "PlayerRace", "'{0}' is part of the '{1}' race" },
                { "PlayerNoRace", "'{0}' has not chosen a race yet." },
                { "DieChange", "You can now change your race. Use the command /rschange within {0} minutes to change your race." },
                { "RaceChangeTitle", "Race change" },
                { "RaceChangeYes", "Yes" },
                { "RaceChangeNo", "No" },
                { "RaceChangeConfirm", "Change" },
                { "RaceChangeCancel", "Cancel" },
                { "RaceChangeRace", "Please type in the name of the race you want to be part of." },
                { "RaceChangeAppearance", "Do you want to change your apearance?" },
                { "RaceChangeFreeSlot", "Please make sure you have a free inventory slot for a potion of appearance if you want to change your appearance." },
                { "RaceChangeConfirmRace", "Are you sure you want to join the race: '{0}'?" },
                { "CantChange", "You are not allowed to change your race now." },
                { "ResetChange", "You enabled the race change for {0}." },
                { "HelpTitle", "[0000FF]Race System Commands[FFFFFF]" },
                { "HelpRsChange", "[00ff00]/rschange[FFFFFF] - Change your race (only available for {0} minutes after you respawn)." },
                { "HelpRsPlayer", "[00ff00]/rsplayer (playername)[FFFFFF] - Show the race of a player. Use 'all' as playername to show a list of all players and their race." },
                { "HelpRsRace", "[00ff00]/rsrace[FFFFFF] - Toggle race change." },
                { "HelpRsAppearance", "[00ff00]/rsappearance[FFFFFF] - Toggle appearance change after race change." },
                { "HelpRsTime", "[00ff00]/rstime (time in minutes)[FFFFFF] - Change time a player can change his race after respawning." },
                { "HelpRsReset", "[00ff00]/rsreset (playername)[FFFFFF] - Forces a player to be able to change his race. Use 'all' as playername to do this for all players (both online and offline)." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("rshelp")]
        private void SendPlayerHelpText(Player player, string cmd)
        {
            SendHelpText(player);
        }

        [ChatCommand("rsreset")]
        private void ResetplayerRaceData(Player player, string cmd, string[] input)
        {
            ResetPlayerRace(player, input);
        }

        [ChatCommand("rsplayer")]
        private void GetPlayerRaceData(Player player, string cmd, string[] input)
        {
            GetPlayerRace(player, input);
        }

        [ChatCommand("rschange")]
        private void ChangePlayerRace(Player player, string cmd)
        {
            ChangeRacePopup(player);
        }

        [ChatCommand("rsrace")]
        private void ToggleCanChangeRace(Player player, string cmd)
        {
            ToggleChangeables(player, 1);
        }

        [ChatCommand("rsappearance")]
        private void ToggleCanChangeAppearance(Player player, string cmd)
        {
            ToggleChangeables(player, 2);
        }

        [ChatCommand("rstime")]
        private void ChangeChangeTime(Player player, string cmd, string[] input)
        {
            ToggleChangeables(player, input);
        }

        #endregion

        #region Functions

        private void CheckPlayerExcists(Player player)
        {
            if (!_Data.ContainsKey(player.Id))
            {
                _Data.Add(player.Id, new PlayerData() { CanChange = true });
                PrintToChat(player, GetMessage("NoRace", player));
            }
            else
            {
                if (_Data[player.Id].Race.IsNullOrEmpty()) { PrintToChat(player, GetMessage("NoRaceRepeat", player)); _Data[player.Id].CanChange = true; }
                else if (_Data[player.Id].CanChange) ResetRaceChange(player);
            }

            SaveRaceData();
        }

        private void ResetPlayerRace(Player player, string[] input)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { PrintToChat(player, GetMessage("NoPermission", player)); return; }

            if (input[0].ToLower() == "all")
            {
                foreach (KeyValuePair<ulong, PlayerData> data in _Data)
                {
                    data.Value.CanChange = true;
                }
                return;
            }

            Player target = Server.GetPlayerByName(input[0]);
            ResetRaceChange(target);

            PrintToChat(player, string.Format(GetMessage("ResetChange", player), target.DisplayName));
        }

        private void ResetRaceChange(Player player)
        {
            if (!ChangeRace)
            {
                if (!_Data[player.Id].Race.IsNullOrEmpty()) if (_Data[player.Id].CanChange) _Data[player.Id].ChangeExpired();
                return;
            }
            
            PrintToChat(player, string.Format(GetMessage("DieChange", player), ChangeTime));

            _Data[player.Id].CanChange = true;
            timer.In((ChangeTime * 60), _Data[player.Id].ChangeExpired);

            SaveRaceData();
        }

        private void GetPlayerRace(Player player, string[] input)
        {
            if (!player.HasPermission("RaceSystem.Show")) { PrintToChat(player, GetMessage("NoPermission", player)); return; }

            List<Player> players = new List<Player>();

            if (input[0].ToLower() == "all") players = Server.ClientPlayers;
            else players.Add(Server.GetPlayerByName(input.JoinToString(" ")));
            
            foreach (Player target in players)
            {
                if (target == null) { PrintToChat(player, GetMessage("NoPlayer", player)); return; }
                CheckPlayerExcists(target);

                string race = _Data[target.Id].Race;

                if (race == null) PrintToChat(player, string.Format(GetMessage("PlayerNoRace", player), target.DisplayName));
                else PrintToChat(player, string.Format(GetMessage("PlayerRace", player), target.DisplayName, race));
            }
        }
        
        #region Race change

        private void ChangeRacePopup(Player player)
        {
            if (!_Data[player.Id].Race.IsNullOrEmpty() && !ChangeRace) { PrintToChat(player, GetMessage("NoPermission", player)); return; }

            if (!_Data.ContainsKey(player.Id)) _Data.Add(player.Id, new PlayerData() { CanChange = true });

            if (!_Data[player.Id].CanChange) { PrintToChat(player, GetMessage("CantChange", player)); return; }

            string message = "";

            message += GetMessage("RaceChangeRace", player);
            message += "\n\n";

            foreach (string race in _Races) message += $"{race}\n";

            if (ChangeAppearance) message += "\n " + GetMessage("RaceChangeFreeSlot", player);

            player.ShowInputPopup(GetMessage("RaceChangeTitle", player), message, "", GetMessage("RaceChangeConfirm", player), GetMessage("RaceChangeCancel", player), (options, dialogue1, data) => ChangeRaceConfirm(player, options, dialogue1));
        }

        private void ChangeRaceConfirm(Player player, Options selection, Dialogue dialogue)
        {
            if (selection == Options.Cancel) return;

            bool contained = false;
            foreach (string race in _Races)
            {
                if (race.ToLower() == dialogue.ValueMessage.ToLower())
                {
                    contained = true;
                    string message = string.Format(GetMessage("RaceChangeConfirmRace", player), race);
                    player.ShowConfirmPopup(GetMessage("RaceChangeTitle", player), message, GetMessage("RaceChangeYes", player), GetMessage("RaceChangeNo", player), (options, dialogue1, data) => ChangeRaceFinish(player, options, dialogue1, race));
                    continue;
                }
            }
            if (!contained) PrintToChat(player, GetMessage("InvalidRace", player));
        }

        private void ChangeRaceFinish(Player player, Options selection, Dialogue dialogue, string race)
        {
            if (selection != Options.Yes) return;

            _Data[player.Id].ChangeRace(race);
            ChangeChatFormat(player);
            SaveRaceData();

            if (ChangeAppearance)
            {
                string message = GetMessage("RaceChangeAppearance", player);
                player.ShowConfirmPopup(GetMessage("RaceChangeTitle", player), message, GetMessage("RaceChangeYes", player), GetMessage("RaceChangeNo", player), (options, dialogue1, data) => GivePOA(player, options, dialogue1));
            }
        }

        private void GivePOA(Player player, Options selection, Dialogue dialogue)
        {
            if (selection != Options.Yes) return;

            ItemCollection inventory = player.GetInventory().Contents;
            InvItemBlueprint blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Potion of appearance", true, true);
            InvGameItemStack invGameItemStack = new InvGameItemStack(blueprintForName, 1, null);
            ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
        }

        #endregion

        private void ToggleChangeables(Player player, int type)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { PrintToChat(player, GetMessage("NoPermission", player)); return; }

            switch (type)
            {
                case 1:
                    if (ChangeRace) { ChangeRace = false; PrintToChat(player, string.Format(GetMessage("ToggleChangeRace", player), "off")); }
                    else { ChangeRace = true; PrintToChat(player, string.Format(GetMessage("ToggleChangeRace", player), "on")); }
                    break;
                case 2:
                    if (ChangeAppearance) { ChangeAppearance = false; PrintToChat(player, string.Format(GetMessage("ToggleChangeAppearance", player), "off")); }
                    else { ChangeAppearance = true; PrintToChat(player, string.Format(GetMessage("ToggleChangeAppearance", player), "on")); }
                    break;
            }

            SaveConfigData();
        }

        private void ToggleChangeables(Player player, string[] input)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { PrintToChat(player, GetMessage("NoPermission", player)); return; }

            if (input.Count() < 1) { PrintToChat(player, GetMessage("NoArgs", player)); return; }

            int newTime = 0;
            if (!int.TryParse(input[0], out newTime)) { PrintToChat(player, GetMessage("InvalidArgs", player)); return; }

            ChangeTime = newTime;
            PrintToChat(player, string.Format(GetMessage("ChangedTime", player), ChangeTime));

            SaveConfigData();
        }

        private void ChangeChatFormat(Player player)
        {
            string nameFormat = player.DisplayNameFormat;

            if (!_Data[player.Id].chatFormat.IsNullOrEmpty())
            {
                string format = _Data[player.Id].chatFormat;
                if (nameFormat.Contains($"{format} ")) nameFormat = nameFormat.ReplaceFirst($"{format} ", "");
            }

            if (!_Data[player.Id].Race.IsNullOrEmpty())
            {
                string color = _ColorCodes[_Data[player.Id].Race];
                string white = "[ffffff]";
                string race = _Data[player.Id].Race;
                string format = $"[{color}{race}{white}]";

                _Data[player.Id].chatFormat = format;
                
                nameFormat = $"{format} {nameFormat}";
            }
            
            player.DisplayNameFormat = nameFormat;

            SaveRaceData();
        }

        #endregion

        #region Hooks

        private void OnPlayerConnected(Player player)
        {
            CheckPlayerExcists(player);
            ChangeChatFormat(player);
        }

        private void OnPlayerRespawn(PlayerRespawnEvent respawnEvent)
        {
            CheckPlayerExcists(respawnEvent.Player);
            ResetRaceChange(respawnEvent.Player);
        }

        private void OnPlayerChat(PlayerEvent e)
        {
            CheckPlayerExcists(e.Player);
            ChangeChatFormat(e.Player);
        }

        private void SendHelpText(Player player)
        {
            if (!ChangeRace && !player.HasPermission("RaceSystem.Modify")) return;
            PrintToChat(player, GetMessage("HelpTitle", player));
            PrintToChat(player, string.Format(GetMessage("HelpRsChange", player), ChangeTime));
            if (player.HasPermission("RaceSystem.Show"))
            {
                PrintToChat(player, GetMessage("HelpRsPlayer", player));
            }
            if (player.HasPermission("RaceSystem.Modify"))
            {
                PrintToChat(player, GetMessage("HelpRsRace", player));
                PrintToChat(player, GetMessage("HelpRsAppearance", player));
                PrintToChat(player, GetMessage("HelpRsTime", player));
                PrintToChat(player, GetMessage("HelpRsReset", player));
            }
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

        string GetMessage(string key, Player player = null)
        {
            if (player == null) lang.GetMessage(key, this, null);
            return lang.GetMessage(key, this, player.Id.ToString());
        }

        #endregion
    }
}