using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.ItemContainer;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.UserInterface.Dialogues;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("ProfessionSystem", "juk3b0x", "1.0.2")] // Known/Future Issues: 1:Might want to specify the Resource/Item by its Unique ID, 2:Add German Language support 3: Might add a few more professions like smith and carpenter
    public class ProfessionSystem : ReignOfKingsPlugin
    {
        #region Language API
        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Title", "Choose Your Profession" },
                { "NewOnServer", "You are new on this server and thus you have to choose a profession" },
                { "WorkerDescription", "If you choose to become WORKER you will have {0} inventory slots, but do only {1} percent damage." },
                { "WarriorDescription", "If you choose to become a WARRIOR you will do normal damage but only {0} inventory slots." },
                { "Choose", "Choose wisely, changing your profession later will cost {0} {1} !" },
                { "Fighter", "WARRIOR" },
                { "Builder", "WORKER" },
                { "Change", "You decided to change your profession." },
                { "Careful", "[FF0000]CAREFUL [FFFFFF], it will cost  {0} {1} to change AND you will lose all other items on your inventory!" },
                { "FailedToChoose", "You have failed to choose a profession, type /changeprofession to do that!" },
                { "PrefixWorker", "[Worker]" },
                { "PrefixWarrior", "[Warrior]" },
                { "ChosenWorker", "Congratulations! You have chosen to become a {0}{1}[FFFFFF]!" },
                { "ChosenWarrior", "Congratulations! You have chosen to become a {0}{1}[FFFFFF]!" },
                { "NotEnough", "You don't have enough {0} to change your Profession!" }
            }, this);
        }
        string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
        #endregion
        #region Lists
        private Dictionary<ulong,string> _ProfessionList = new Dictionary<ulong,string>();
        // _ProfessionList.Key = SteamId
        // _ProfessionList.Value = Playername including prefix (worker or Warrior)
        #endregion
        #region List loading and saving
        private void LoadLists()
        {
            _ProfessionList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong,string>>("ProfessionList");
        }
        private void SaveProfessionList()
        {
            Interface.GetMod().DataFileSystem.WriteObject("ProfessionList", _ProfessionList);
        }
        void Loaded()
        {
            LoadLists();
        }
        #endregion
        #region Checks
        bool HasProfession (Player player)
        {
            for (var players =0; players < _ProfessionList.Count; players++ )
            {
                if (_ProfessionList.Keys.Contains(player.Id))
                {
                    return true;
                }
                return false;
            }
            return false;
        }
        bool PlayerHasResources(Player player)
        {
            var inventory = player.CurrentCharacter.Entity.GetContainerOfType(CollectionTypes.Inventory);
            int foundResource = 0;
            foreach (var item in inventory.Contents.Where(item => item != null))
            {
                if (item.Name.ToLower() == ResourceToChangeProfession.ToLower())
                {
                    foundResource = foundResource + item.StackAmount;
                }
            }
            if (foundResource >= ResourceAmountToChange) return true;
            return false;
        }
        bool IsWorker(Player player)
        {
        foreach (var players in _ProfessionList)
            {
                if ((players.Key == player.Id) && players.Value.ToLower() == "worker")
                {
                    return true;
                }
                continue;
            }
            return false;
        }
        #endregion
        #region Config
        bool KillViolator;
        bool AddPrefix;
        string WarriorPrefixColor;
        string WorkerPrefixColor;
        string ResourceToChangeProfession;
        int ResourceAmountToChange;
        int InventorySpaceWarrior;
        int InventorySpaceWorker;
        float DecreasedDamageForWorkers;

        void Init() => LoadDefaultConfig();

        protected override void LoadDefaultConfig()
        {
            Config["Add a Prefix"] = AddPrefix = GetConfig("Add a Prefix", true);
            Config["Kill Violators"] = KillViolator = GetConfig("Kill Violators", true);
            Config["Color for the WARRIOR-Prefix"] = WarriorPrefixColor = GetConfig("Color for the WARRIOR-Prefix", "[FF0000]");
            Config["Color for the WORKER-Prefix"] = WorkerPrefixColor = GetConfig("Color for the WORKER-Prefix", "[008000]");
            Config["Resource required to change Profession"] = ResourceToChangeProfession = GetConfig("Resource required to change Profession", "Diamond");
            Config["AMOUNT of the Resource required to change Profession"] = ResourceAmountToChange = GetConfig("AMOUNT of the Resource required to change Profession", 10);
            Config["Inventory Space for Warriors"] = InventorySpaceWarrior = GetConfig("Inventory Space for Warriors", 2);
            Config["Inventory Space for Workers"] = InventorySpaceWorker = GetConfig("Inventory Space for Workers", 100);
            Config["Percent of Damage a Worker does"] = DecreasedDamageForWorkers = GetConfig("Percent of Damage a Worker does", 10f);
            SaveConfig();
        }

        T GetConfig<T>(string name, T defaultValue) => Config[name] == null ? defaultValue : (T)System.Convert.ChangeType(Config[name], typeof(T));

        #endregion
        #region The Magic
        private void OnPlayerSpawn (PlayerFirstSpawnEvent e)
        {
            if (!e.Player.CurrentCharacter.HasCompletedCreation)
            {
                e.Player.ShowConfirmPopup(string.Format(GetMessage("Title", e.Player.Id.ToString())), string.Format(GetMessage("NewOnServer", e.Player.Id.ToString())) + "\n\n" + string.Format(GetMessage("WorkerDescription", e.Player.Id.ToString()), InventorySpaceWorker.ToString(), DecreasedDamageForWorkers.ToString()) + "\n\n" + string.Format(GetMessage("WarriorDescription", e.Player.Id.ToString()), InventorySpaceWarrior.ToString()) + "\n" + string.Format(GetMessage("Choose", e.Player.Id.ToString()), ResourceAmountToChange.ToString(), ResourceToChangeProfession.ToString()), string.Format(GetMessage("Fighter", e.Player.Id.ToString())), string.Format(GetMessage("Builder", e.Player.Id.ToString())), (selection, dialogue, data) => WorkerOrWarrior(e.Player, selection, dialogue, data));
                return;
            }
            foreach (var player in _ProfessionList)
                if((e.Player.Id == player.Key)&& AddPrefix == true)
                {
                    if (IsWorker(e.Player))
                    {
                        e.Player.DisplayNameFormat = WorkerPrefixColor + string.Format(GetMessage("PrefixWorker", e.Player.Id.ToString())) + "%name%";
                    }
                    e.Player.DisplayNameFormat = WarriorPrefixColor + string.Format(GetMessage("PrefixWarrior", e.Player.Id.ToString())) + "%name%";
                }
                    
            return;
        }
        private void WorkerOrWarrior(Player player, Options selection, Dialogue dialogue, object contextData)
        {
            var PlayerId = player.Id;
            if (selection == Options.No)
            {
                
               
                PrintToChat(player, string.Format(GetMessage("ChosenWorker", player.Id.ToString()), WorkerPrefixColor, string.Format(GetMessage("PrefixWorker", player.Id.ToString()))));
                _ProfessionList.Add(PlayerId,"worker");
                SaveProfessionList();
                player.ClearInventory();
                var inventory = player.GetInventory();
                inventory.MaximumSlots = InventorySpaceWorker;
                inventory.Contents.SetMaxSlotCount(inventory.MaximumSlots);
                if(AddPrefix)
                {
                    player.DisplayNameFormat = WorkerPrefixColor + string.Format(GetMessage("PrefixWorker", player.Id.ToString())) + "[FFFFFF]" + "%name%";
                }
                
            }
            if (selection == Options.Yes)
            {
                
                PrintToChat(player, string.Format(GetMessage("ChosenWarrior", player.Id.ToString()), WarriorPrefixColor, string.Format(GetMessage("PrefixWarrior", player.Id.ToString()))));
                _ProfessionList.Add(PlayerId, "warrior");
                SaveProfessionList();
                player.ClearInventory();
                var inventory = player.GetInventory();
                inventory.MaximumSlots = InventorySpaceWarrior;
                inventory.Contents.SetMaxSlotCount(inventory.MaximumSlots);
                if (AddPrefix)
                {
                    player.DisplayNameFormat = WarriorPrefixColor + string.Format(GetMessage("PrefixWarrior", player.Id.ToString())) + "[FFFFFF]" + "%name%";
                }
            }
        }
        [ChatCommand("changeprofession")]
        private void ProfessionChange (Player player)
        {
            LoadLists();
            if (!HasProfession(player))
            {
                player.ShowConfirmPopup(string.Format(GetMessage("Title", player.Id.ToString())), string.Format(GetMessage("NewOnServer", player.Id.ToString())) + "\n\n" + string.Format(GetMessage("WorkerDescription", player.Id.ToString()), InventorySpaceWorker.ToString(), DecreasedDamageForWorkers.ToString()) + "\n\n" + string.Format(GetMessage("WarriorDescription", player.Id.ToString()), InventorySpaceWarrior.ToString()) + "\n" + string.Format(GetMessage("Choose", player.Id.ToString()), ResourceAmountToChange.ToString(), ResourceToChangeProfession.ToString()), string.Format(GetMessage("Fighter", player.Id.ToString())), string.Format(GetMessage("Builder", player.Id.ToString())), (selection, dialogue, data) => WorkerOrWarrior(player, selection, dialogue, data));
                return;
            }
            if (!PlayerHasResources(player))
            {
                PrintToChat(player, string.Format(GetMessage("NotEnough", player.Id.ToString()), ResourceToChangeProfession));
                return;
            }
            _ProfessionList.Remove(player.Id);
            SaveProfessionList();
            player.ShowConfirmPopup(string.Format(GetMessage("Title", player.Id.ToString())), string.Format(GetMessage("Change", player.Id.ToString())) + "\n\n" + string.Format(GetMessage("WorkerDescription", player.Id.ToString()), InventorySpaceWorker.ToString(), DecreasedDamageForWorkers.ToString()) + "\n\n" + string.Format(GetMessage("WarriorDescription", player.Id.ToString()), InventorySpaceWarrior.ToString()) + "\n" + string.Format(GetMessage("Careful", player.Id.ToString()), ResourceAmountToChange.ToString(), ResourceToChangeProfession.ToString()), string.Format(GetMessage("Fighter", player.Id.ToString())), string.Format(GetMessage("Builder", player.Id.ToString())), (selection, dialogue, data) => WorkerOrWarrior(player, selection, dialogue, data));
            return;
        }
        private void OnEntityHealthChange(EntityDamageEvent e)
        {
            var attacker = e.Damage.DamageSource.Owner;
            if (e.Damage.DamageSource.IsPlayer &&(!HasProfession(e.Damage.DamageSource.Owner)))
            {
                if (!KillViolator)
                {
                    PrintToChat(attacker, string.Format(GetMessage("FailedToChoose", attacker.Id.ToString())));
                    return;
                }
                attacker.Kill();
                PrintToChat(attacker, string.Format(GetMessage("FailedToChoose", attacker.Id.ToString())));
            }
            if (e.Damage.DamageSource.IsPlayer && e.Entity != e.Damage.DamageSource)  // entity taking damage is not taking damage from self
            {
                if (attacker.DisplayNameFormat.ToLower().Contains("worker"))
                {
                    e.Damage.Amount = (e.Damage.Amount / 100 * DecreasedDamageForWorkers); 
                }
                return;
            }
        }
        #endregion

    }
}
