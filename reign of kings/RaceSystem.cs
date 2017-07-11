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
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Permissions;

namespace Oxide.Plugins
{
    [Info("RaceSystem", "D-Kay", "0.1.5", ResourceId = 2287)]
    public class RaceSystem : ReignOfKingsPlugin
    {
        #region Variables

        private bool ChangeAppearance { get; set; }
        private bool ChangeRace { get; set; }
        private bool RaceDamage { get; set; }

        private int ChangeTime { get; set; }

        private List<Race> Races { get; set; } = new List<Race>();
        private List<Race> DefaultRaces { get; } = new List<Race>
        {
            new Race("Dwarf", "3066c3"),
            new Race("Elf", "f0f029"),
            new Race("Human", "eee6c3"),
            new Race("Orc", "3a6336")
        };
        private Dictionary<ulong, PlayerData> Data { get; set; } = new Dictionary<ulong, PlayerData>();

        private class Race
        {
            public string Name { get; set; }
            public string Color { get; set; }
            public string Permission { get; set; }
            public string Format => $"[[{Color}]{Name}[ffffff]]";

            public Race() { }

            public Race(string name, string color, string permission = null)
            {
                Name = name;
                Color = color;
                Permission = permission;
            }

            public void ChangeColor(string color)
            {
                Color = color;
            }

            public void ChangePermission(string permission)
            {
                Permission = permission;
            }

            public override string ToString()
            {
                return Name;
            }
        }
        private class PlayerData
        {
            public Race Race { get; set; }
            public bool CanChange { get; set; }

            public PlayerData()
            {

            }

            public PlayerData(bool canChange, Race race = null)
            {
                CanChange = canChange;
                Race = race;
            }

            public void Reset(Player player = null)
            {
                if (player != null) player.DisplayNameFormat = player.DisplayNameFormat.ReplaceFirst($"{Race.Format} ", "");
                Race = null;
                CanChange = true;
            }

            public void ChangeReset()
            {
                CanChange = true;
            }

            public void ChangeExpired()
            {
                CanChange = false;
            }

            public void ChangeRace(Player player, Race race)
            {
                if (Race != null) player.DisplayNameFormat = player.DisplayNameFormat.ReplaceFirst($"{Race.Format} ", "");
                Race = race;
                CanChange = false;
                player.DisplayNameFormat = $"{Race.Format} {player.DisplayNameFormat}";
            }

            public void UpdateChatFormat(Player player)
            {
                if (Race == null) return;
                if (player.DisplayNameFormat.Contains(Race.Format)) return;
                player.DisplayNameFormat = $"{Race.Format} {player.DisplayNameFormat}";
            }

            public bool HasRace(Race race)
            {
                if (Race == null) return false;
                if (Race == race) return true;
                return false;
            }

            public bool HasRace(string race)
            {
                if (Race == null) return false;
                if (string.Equals(Race.Name, race, StringComparison.CurrentCultureIgnoreCase)) return true;
                return false;
            }
        }
        private class OldPlayerData
        {
            public string Race { get; set; }
            public string chatFormat { get; set; }
            public bool CanChange { get; set; }

            public OldPlayerData()
            {

            }
        }

        #endregion

        #region Save and Load Data

        private void Loaded()
        {
            LoadDefaultMessages();
            LoadRaceData();
            LoadConfigData();

            permission.RegisterPermission("RaceSystem.Modify", this);
            permission.RegisterPermission("RaceSystem.Show", this);
        }

        private void Unload()
        {
            SaveRaceData();
        }

        private void LoadRaceData()
        {
            Data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>("PlayerRaces");
            Races = Interface.Oxide.DataFileSystem.ReadObject<List<Race>>("Races");

            foreach (var player in Data)
            {
                if (player.Value.Race != null && !Races.Contains(player.Value.Race))
                {
                    player.Value.Race = Races.Find(r => string.Equals(r.Name, player.Value.Race.Name, StringComparison.CurrentCultureIgnoreCase));
                }
            }
        }

        private void SaveRaceData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("PlayerRaces", Data);
            Interface.Oxide.DataFileSystem.WriteObject("Races", Races);
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfigData();
            SaveConfigData();
        }

        private void LoadConfigData()
        {
            ChangeAppearance = GetConfig("Tunables", "ChangeAppearance", true);
            ChangeRace = GetConfig("Tunables", "ChangeRace", true);
            ChangeTime = GetConfig("Tunables", "ChangeTime", 2);
            RaceDamage = GetConfig("Tunables", "RaceDamage", true);
        }

        private void SaveConfigData()
        {
            Config["Tunables", "ChangeAppearance"] = ChangeAppearance;
            Config["Tunables", "ChangeRace"] = ChangeRace;
            Config["Tunables", "ChangeTime"] = ChangeTime;
            Config["Tunables", "RaceDamage"] = RaceDamage;
            SaveConfig();
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NoPermission", "You do not have permission to use this command." },
                { "ToggleChangeRace", "Race changing was turned {0}." },
                { "ToggleChangeAppearance", "Appearance changing was turned {0}." },
                { "ToggleRaceDamage", "Damage against player of the same race was turned {0}." },
                { "InvalidArgs", "Something went wrong. Please use /rshelp to see if you used the correct format." },
                { "InvalidAmount", "That is not a valid amount." },
                { "InvalidColor", "That is not a valid color." },
                { "InvalidRace", "That race does not excist." },
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
                { "RaceChangeConfirmRace", "Are you sure you want to join the race: '{0}[ffffff]'?" },
                { "CantChange", "You are not allowed to change your race now." },
                { "ResetChange", "You enabled the race change for {0}." },
                { "HelpTitle", "[0000FF]Race System Commands[FFFFFF]" },
                { "HelpRsChange", "[00ff00]/rschange[FFFFFF] - Change your race (only available for {0} minutes after you respawn)." },
                { "HelpRsList", "[00ff00]/rs.list[FFFFFF] - Show all races and their settings." },
                { "HelpRsAdd", "[00ff00]/rs.add (racename)[FFFFFF] - Add a new race." },
                { "HelpRsChangeColor", "[00ff00]/rs.changecolor (racename) (color)[FFFFFF] - Change the color of a race." },
                { "HelpRsChangePermission", "[00ff00]/rs.changepermission (racename) (permission)[FFFFFF] - Change the needed permission of a race." },
                { "HelpRsRemove", "[00ff00]/rs.remove (racename)[FFFFFF] - Remove a race." },
                { "HelpRsPlayer", "[00ff00]/rs.player (playername)[FFFFFF] - Show the race of a player. Use 'all' as playername to show a list of all players and their race." },
                { "HelpRsRace", "[00ff00]/rs.race[FFFFFF] - Toggle race change." },
                { "HelpRsAppearance", "[00ff00]/rs.appearance[FFFFFF] - Toggle appearance change after race change." },
                { "HelpRsTime", "[00ff00]/rs.time (time in minutes)[FFFFFF] - Change time a player can change his race after respawning." },
                { "HelpRsForce", "[00ff00]/rs.force (racename) (playername)[FFFFFF] - Forces a race to a player." },
                { "HelpRsReset", "[00ff00]/rs.reset (playername)[FFFFFF] - Forces a player to be able to change his race. Use 'all' as playername to do this for all players (both online and offline)." },
                { "HelpRsDamage", "[00ff00]/rs.damage[FFFFFF] - Toggle the damage against players of the same race." },
                { "HelpRsRestore", "[00ff00]/rs.restore[FFFFFF] - Restore the races to the default values." },
                { "HelpRsConvert", "[00ff00]/rs.convert[FFFFFF] - Takes all old config and race data and converts it to the new format." },
                { "RaceExists", "That race already exists." },
                { "RaceNonExisting", "That race does not exist." },
                { "RaceAdded", "You succesfully added the {0} race." },
                { "ColorChanged", "You succesfully changed the color for race {0} to [{1}]{1}[ffffff]." },
                { "PermissionChanged", "You succesfully changed the permission for race {0} to {1}." },
                { "RaceRemoved", "You succesfully removed the race {0}." },
                { "RaceInfoName", "{0}: " },
                { "RaceInfoColor", "    Color: [{0}]{0}[ffffff]" },
                { "RaceInfoPermission", "   Permission: {0}" },
                { "DefaultRestored", "The races have been reset to the default values." },
                { "RaceForced", "Player {0} is now part of race {1}." }
            }, this);
        }

        #endregion

        #region Commands

        [ChatCommand("rshelp")]
        private void CmdSendHelpText(Player player, string cmd)
        {
            SendHelpText(player);
        }

        [ChatCommand("rschange")]
        private void CmdChangePlayerRace(Player player, string cmd)
        {
            ChangeRacePopup(player);
        }

        [ChatCommand("rs.list")]
        private void CmdListRaces(Player player, string cmd)
        {
            ListRaces(player);
        }

        [ChatCommand("rs.add")]
        private void CmdAddRace(Player player, string cmd, string[] input)
        {
            AddRace(player, input);
        }

        [ChatCommand("rs.changecolor")]
        private void CmdChangeColor(Player player, string cmd, string[] input)
        {
            ChangeColor(player, input);
        }

        [ChatCommand("rs.changepermission")]
        private void CmdChangePermission(Player player, string cmd, string[] input)
        {
            ChangePermission(player, input);
        }

        [ChatCommand("rs.remove")]
        private void CmdRemoveRace(Player player, string cmd, string[] input)
        {
            RemoveRace(player, input);
        }

        [ChatCommand("rs.force")]
        private void CmdForcePlayerRace(Player player, string cmd, string[] input)
        {
            ForcePlayerRace(player, input);
        }

        [ChatCommand("rs.reset")]
        private void CmdResetPlayerRace(Player player, string cmd, string[] input)
        {
            ResetPlayerRace(player, input);
        }

        [ChatCommand("rs.player")]
        private void CmdGetPlayerRace(Player player, string cmd, string[] input)
        {
            GetPlayerRace(player, input);
        }

        [ChatCommand("rs.race")]
        private void CmdToggleCanChangeRace(Player player, string cmd)
        {
            ToggleChangeables(player, 1);
        }

        [ChatCommand("rs.appearance")]
        private void CmdToggleCanChangeAppearance(Player player, string cmd)
        {
            ToggleChangeables(player, 2);
        }

        [ChatCommand("rs.time")]
        private void CmdChangeChangeTime(Player player, string cmd, string[] input)
        {
            ToggleChangeables(player, input);
        }

        [ChatCommand("rs.damage")]
        private void CmdChangeRaceDamage(Player player, string cmd)
        {
            ToggleChangeables(player, 3);
        }

        [ChatCommand("rs.restore")]
        private void CmdRestoreDefaultRaces(Player player, string cmd)
        {
            RestoreDefaultRaces(player);
        }

        [ChatCommand("rs.convert")]
        private void CmdConvertOldConfigFile(Player player, string cmd)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            Races.Clear();

            var races = GetConfig("Races", "Names", new List<object> { "Dwarf", "Elf", "Human", "Orc" }).OfType<string>().ToList();
            foreach (var race in races)
            {
                var color = GetConfig("Colors", race, "[9f0000]");
                color = color.Replace("[", "");
                color = color.Replace("]", "");
                Races.Add(new Race(race, color));
            }

            try
            {
                var oldData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, OldPlayerData>>("RaceSystem");
                if (oldData != null)
                {
                    foreach (var data in oldData)
                    {
                        if (data.Value.Race.IsNullOrEmpty()) continue;
                        var race = Races.Find(r => string.Equals(r.Name, data.Value.Race, StringComparison.CurrentCultureIgnoreCase));
                        if (race != null)
                        {
                            Data.Add(data.Key, new PlayerData(data.Value.CanChange, race));
                        }
                    }
                }
            }
            catch
            {
                Puts("Could not load old data file.");
            }

            player.SendMessage(GetMessage("DefaultRestored", player), ChangeTime);

            SaveRaceData();
        }

        #endregion

        #region Functions

        private void CheckPlayerExcists(Player player)
        {
            if (!Data.ContainsKey(player.Id))
            {
                Data.Add(player.Id, new PlayerData(true));
                player.SendError(GetMessage("NoRace", player));
            }
            else
            {
                if (Data[player.Id].Race == null)
                {
                    player.SendError(GetMessage("NoRaceRepeat", player));
                    if (!Data[player.Id].CanChange) Data[player.Id].ChangeReset();
                }
                else if (Data[player.Id].CanChange) ResetRaceChange(player);
            }

            SaveRaceData();
        }

        private void ResetRaceChange(Player player)
        {
            if (!ChangeRace)
            {
                if (Data[player.Id].Race == null) return;
                if (Data[player.Id].CanChange) Data[player.Id].ChangeExpired();
                return;
            }

            player.SendError(GetMessage("DieChange", player), ChangeTime);

            Data[player.Id].ChangeReset();
            timer.In(ChangeTime * 60, Data[player.Id].ChangeExpired);

            SaveRaceData();
        }

        private string GetRace(Player player)
        {
            if (player == null) return null;
            if (!Data.ContainsKey(player.Id)) return null;
            if (Data[player.Id].Race == null) return null;
            return Data[player.Id].Race.ToString();
        }

        private bool HasPermission(Player player, string perm = null)
        {
            if (perm.IsNullEmptyOrWhite()) return true;

            var user = Server.Permissions.GetUser(player.Name);
            return user != null && user.HasGroup(perm);
        }

        private bool IsHex(IEnumerable<char> chars)
        {
            return chars.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        #region Race change

        private void ChangeRacePopup(Player player)
        {
            if (!Data.ContainsKey(player.Id)) Data.Add(player.Id, new PlayerData(true));

            if (Data[player.Id].Race != null && !ChangeRace) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (!Data[player.Id].CanChange) { player.SendError(GetMessage("CantChange", player)); return; }

            var message = "";

            message += GetMessage("RaceChangeRace", player);
            message += "\n\n";

            message = Races.Where(race => HasPermission(player, race.Permission)).Aggregate(message, (current, race) => current + $"[{race.Color}]{race}[ffffff]\r\n");

            if (ChangeAppearance) message += "\r\n" + GetMessage("RaceChangeFreeSlot", player);

            player.ShowInputPopup(GetMessage("RaceChangeTitle", player), message, "", GetMessage("RaceChangeConfirm", player), GetMessage("RaceChangeCancel", player), (options, dialogue1, data) => ChangeRaceConfirm(player, options, dialogue1));
        }

        private void ChangeRaceConfirm(Player player, Options selection, Dialogue dialogue)
        {
            if (selection == Options.Cancel) return;

            var race = Races.Find(r => string.Equals(r.Name, dialogue.ValueMessage, StringComparison.CurrentCultureIgnoreCase));

            if (race == null) { player.SendError(GetMessage("InvalidRace", player)); return; }
            if (!HasPermission(player, race.Permission)) { player.SendError(GetMessage("InvalidRace", player)); return; }
            var message = string.Format(GetMessage("RaceChangeConfirmRace", player), $"[{race.Color}]{race}");
            player.ShowConfirmPopup(GetMessage("RaceChangeTitle", player), message, GetMessage("RaceChangeYes", player), GetMessage("RaceChangeNo", player), (options, dialogue1, data) => ChangeRaceFinish(player, options, race));
        }

        private void ChangeRaceFinish(Player player, Options selection, Race race)
        {
            if (selection != Options.Yes) return;

            Data[player.Id].ChangeRace(player, race);
            SaveRaceData();

            if (!ChangeAppearance) return;
            var message = GetMessage("RaceChangeAppearance", player);
            player.ShowConfirmPopup(GetMessage("RaceChangeTitle", player), message, GetMessage("RaceChangeYes", player), GetMessage("RaceChangeNo", player), (options, dialogue1, data) => GivePoa(player, options));
        }

        private void GivePoa(Player player, Options selection)
        {
            if (selection != Options.Yes) return;

            var inventory = player.GetInventory().Contents;
            var blueprintForName = InvDefinitions.Instance.Blueprints.GetBlueprintForName("Potion of appearance", true, true);
            var invGameItemStack = new InvGameItemStack(blueprintForName, 1, null);
            ItemCollection.AutoMergeAdd(inventory, invGameItemStack);
        }

        #endregion

        #region Admin

        private void ListRaces(Player player)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            foreach (var race in Races)
            {
                player.SendMessage(GetMessage("RaceInfoName", player), race.Name);
                player.SendMessage(GetMessage("RaceInfoColor", player), race.Color);
                player.SendMessage(GetMessage("RaceInfoPermission", player), race.Permission);
            }
        }

        private void AddRace(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (args.Length != 1) { player.SendError(GetMessage("InvalidArgs", player)); return; }

            var name = args[0];

            if (Races.Find(r => string.Equals(r.Name, name, StringComparison.CurrentCultureIgnoreCase)) != null) { player.SendError(GetMessage("RaceExists", player)); return; }

            Races.Add(new Race(name, "9f0000"));
            player.SendMessage(GetMessage("RaceAdded", player), name);

            SaveRaceData();
        }

        private void ChangeColor(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (args.Length != 2) { player.SendError(GetMessage("InvalidArgs", player)); return; }
            
            var race = Races.Find(r => string.Equals(r.Name, args[0], StringComparison.CurrentCultureIgnoreCase));
            if (race == null) { player.SendError(GetMessage("RaceNonExisting", player)); return; }
            var color = args[1];
            if (!IsHex(color.ToCharArray())) { player.SendError(GetMessage("InvalidColor", player)); return; }

            race.ChangeColor(color);
            player.SendMessage(GetMessage("ColorChanged", player), race.Name, color);

            foreach (var pl in Server.ClientPlayers)
            {
                CheckPlayerExcists(pl);
                Data[pl.Id].UpdateChatFormat(pl);
            }

            SaveRaceData();
        }

        private void ChangePermission(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (args.Length != 2) { player.SendError(GetMessage("InvalidArgs", player)); return; }
            
            var race = Races.Find(r => string.Equals(r.Name, args[0], StringComparison.CurrentCultureIgnoreCase));
            if (race == null) { player.SendError(GetMessage("RaceNonExisting", player)); return; }
            var permission = args[1];

            race.ChangePermission(permission);
            foreach (var pl in Server.ClientPlayers)
            {
                CheckPlayerExcists(pl);
                if (!Data[pl.Id].HasRace(race) || HasPermission(pl, race.Permission)) continue;
                Data[pl.Id].Reset();
                CheckPlayerExcists(pl);
            }

            player.SendMessage(GetMessage("PermissionChanged", player), race.Name, permission);

            SaveRaceData();
        }

        private void RemoveRace(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (args.Length != 1) { player.SendError(GetMessage("InvalidArgs", player)); return; }
            
            var race = Races.Find(r => string.Equals(r.Name, args[0], StringComparison.CurrentCultureIgnoreCase));

            if (race == null) { player.SendError(GetMessage("RaceNonExisting", player)); return; }

            Races.Remove(race);
            player.SendMessage(GetMessage("RaceRemoved", player), race.Name);

            foreach (var pl in Server.ClientPlayers)
            {
                CheckPlayerExcists(pl);
                if (!Data[pl.Id].HasRace(race)) continue;
                Data[pl.Id].Reset(pl);
                CheckPlayerExcists(pl);
            }

            SaveRaceData();
        }

        private void ForcePlayerRace(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (args.Length < 2) { player.SendError(GetMessage("InvalidArgs", player)); return; }

            var race = Races.Find(r => string.Equals(r.Name, args[0], StringComparison.CurrentCultureIgnoreCase));
            if (race == null) { player.SendError(GetMessage("InvalidRace", player)); return; }

            var target = Server.GetPlayerByName(args.Skip(1).JoinToString(" "));
            if (target == null) { player.SendError(GetMessage("NoPlayer", player)); return; }

            CheckPlayerExcists(target);
            Data[player.Id].ChangeRace(target, race);

            player.SendMessage(GetMessage("RaceForced", player), target.Name, race.Name);

            SaveRaceData();
        }

        private void ResetPlayerRace(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (args[0].ToLower() == "all")
            {
                foreach (var data in Data) data.Value.Reset();
                return;
            }

            var target = Server.GetPlayerByName(args.JoinToString(" "));
            if (target == null) { player.SendError(GetMessage("NoPlayer", player)); return; }

            Data[target.Id].Reset(target);
            CheckPlayerExcists(target);

            player.SendMessage(GetMessage("ResetChange", player), target.Name);

            SaveRaceData();
        }

        private void GetPlayerRace(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Show")) { player.SendError(GetMessage("NoPermission", player)); return; }

            var players = new List<Player>();

            if (args[0].ToLower() == "all") players = Server.ClientPlayers;
            else players.Add(Server.GetPlayerByName(args.JoinToString(" ")));

            foreach (var target in players)
            {
                if (target == null) { player.SendError(GetMessage("NoPlayer", player)); return; }
                CheckPlayerExcists(target);

                player.SendMessage(GetMessage(Data[target.Id].Race == null ? "PlayerNoRace" : "PlayerRace", player), target.Name, Data[target.Id].Race);
            }
        }

        private void ToggleChangeables(Player player, int type)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

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
                case 3:
                    if (RaceDamage) { RaceDamage = false; PrintToChat(player, string.Format(GetMessage("ToggleRaceDamage", player), "off")); }
                    else { RaceDamage = true; PrintToChat(player, string.Format(GetMessage("ToggleRaceDamage", player), "on")); }
                    break;
            }

            SaveConfigData();
        }

        private void ToggleChangeables(Player player, string[] args)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            if (!args.Any()) { player.SendError(GetMessage("InvalidArgs", player)); return; }

            int newTime;
            if (!int.TryParse(args[0], out newTime)) { player.SendError(GetMessage("InvalidAmount", player)); return; }

            ChangeTime = newTime;
            player.SendMessage(GetMessage("ChangedTime", player), ChangeTime);

            SaveConfigData();
        }

        private void RestoreDefaultRaces(Player player)
        {
            if (!player.HasPermission("RaceSystem.Modify")) { player.SendError(GetMessage("NoPermission", player)); return; }

            Races.Clear();
            Races.AddRange(DefaultRaces);

            player.SendMessage(GetMessage("DefaultRestored", player), ChangeTime);

            SaveRaceData();
        }

        #endregion

        #endregion

        #region Hooks

        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            #region Null Checks
            if (e == null) return;
            if (e.Cancelled) return;
            if (e.Damage == null) return;
            if (e.Damage.DamageSource == null) return;
            if (!e.Damage.DamageSource.IsPlayer) return;
            if (e.Damage.DamageSource.Owner == null) return;
            if (e.Entity == null) return;
            if (!e.Entity.IsPlayer) return;
            if (e.Entity == e.Damage.DamageSource) return;
            #endregion

            if (RaceDamage) return;
            if (e.Damage.Amount < 0) return;

            if (Data[e.Damage.DamageSource.Owner.Id].Race != Data[e.Entity.Owner.Id].Race) return;

            e.Cancel();
            e.Damage.Amount = 0f;
        }

        private void OnPlayerConnected(Player player)
        {
            CheckPlayerExcists(player);
            Data[player.Id].UpdateChatFormat(player);
        }

        private void OnPlayerRespawn(PlayerRespawnEvent respawnEvent)
        {
            CheckPlayerExcists(respawnEvent.Player);
            ResetRaceChange(respawnEvent.Player);
        }

        private void OnPlayerChat(PlayerEvent e)
        {
            CheckPlayerExcists(e.Player);
            Data[e.Player.Id].UpdateChatFormat(e.Player);
        }

        private void SendHelpText(Player player)
        {
            if (!ChangeRace && !player.HasPermission("RaceSystem.Modify")) return;
            PrintToChat(player, GetMessage("HelpTitle", player));
            PrintToChat(player, string.Format(GetMessage("HelpRsChange", player), ChangeTime));
            if (player.HasPermission("RaceSystem.Show"))
            {
                player.SendMessage(GetMessage("HelpRsPlayer", player));
            }
            if (player.HasPermission("RaceSystem.Modify"))
            {
                player.SendMessage(GetMessage("HelpRsList", player));
                player.SendMessage(GetMessage("HelpRsAdd", player));
                player.SendMessage(GetMessage("HelpRsChangeColor", player));
                player.SendMessage(GetMessage("HelpRsChangePermission", player));
                player.SendMessage(GetMessage("HelpRsRemove", player));
                player.SendMessage(GetMessage("HelpRsRace", player));
                player.SendMessage(GetMessage("HelpRsAppearance", player));
                player.SendMessage(GetMessage("HelpRsTime", player));
                player.SendMessage(GetMessage("HelpRsForce", player));
                player.SendMessage(GetMessage("HelpRsReset", player));
                player.SendMessage(GetMessage("HelpRsDamage", player));
                player.SendMessage(GetMessage("HelpRsRestore", player));
                player.SendMessage(GetMessage("HelpRsConvert", player));
            }
        }

        #endregion

        #region Utility

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

        private string GetMessage(string key, Player player = null) => lang.GetMessage(key, this, player?.Id.ToString());

        #endregion
    }
}