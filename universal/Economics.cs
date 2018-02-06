using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

// TODO: Add SQLite and MySQL database support?
// TODO: Add individual permissions for each self command?
// TODO: Add config setting to set maximum balance for players, and reduce balances above max

namespace Oxide.Plugins
{
    [Info("Economics", "Wulf/lukespragg", "3.1.6", ResourceId = 717)]
    [Description("Basic economics system and economy API")]
    public class Economics : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Remove unused accounts (true/false)")]
            public bool RemoveUnused;

            [JsonProperty(PropertyName = "Start money amount (any number 1 or above)")]
            public int StartAmount;

            [JsonProperty(PropertyName = "Money transfer fee (any number 0.00 or above)")]
            public float TransferFee;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    RemoveUnused = true,
                    StartAmount = 1000,
                    TransferFee = 0.01f
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.StartAmount == null) LoadDefaultConfig();
            }
            catch
            {
                LogWarning($"Could not read oxide/config/{Name}.json, creating new config file");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Stored Data

        private DynamicConfigFile data;
        private StoredData storedData;
        private bool changed;

        private class StoredData
        {
            public Dictionary<string, double> Balances = new Dictionary<string, double>();
        }

        #endregion Stored Data

        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandBalance"] = "balance",
                ["CommandDeposit"] = "deposit",
                ["CommandSetMoney"] = "setmoney",
                ["CommandTransfer"] = "transfer",
                ["CommandWithdraw"] = "withdraw",
                ["CommandWipe"] = "ecowipe",
                ["DataSaved"] = "Economics data saved!",
                ["DataWiped"] = "Economics data wiped!",
                ["NegativeBalance"] = "Balance can not be negative!",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["PlayerBalance"] = "Balance for {0}: {1:C}",
                ["PlayerLacksMoney"] = "'{0}' does not have enough money!",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["ReceivedFrom"] = "You have received {0} from {1}",
                ["TransferredTo"] = "{0} transferred to {1}",
                ["TransferToSelf"] = "You can not transfer money yourself!",
                ["UsageBalance"] = "{0} - check your balance",
                ["UsageBalanceOthers"] = "{0} <name or id> - check balance of a player",
                ["UsageDeposit"] = "{0} <name or id> <amount> - deposits amount to player",
                ["UsageSetMoney"] = "Usage: {0} <name or id> <amount>",
                ["UsageTransfer"] = "Usage: {0} <name or id> <amount> - transfers amount to player (fee may apply)",
                ["UsageWithdraw"] = "Usage: {0} <name or id> <amount>",
                ["UsageWipe"] = "Usage: {0} - wipes all economics data",
                ["YouLackMoney"] = "You do not have enough money!",
                ["YouLostMoney"] = "You lost: {0:C}",
                ["YouReceivedMoney"] = "You received: {0:C}",
                ["YourBalance"] = "Your balance is: {0:C}",
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permAdmin = "economics.admin";
        private const string permUse = "economics.use";

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permUse, this);

            AddLocalizedCommand("CommandBalance", "BalanceCommand");
            AddLocalizedCommand("CommandDeposit", "DepositCommand");
            AddLocalizedCommand("CommandSetMoney", "SetMoneyCommand");
            AddLocalizedCommand("CommandTransfer", "TransferCommand");
            AddLocalizedCommand("CommandWithdraw", "WithdrawCommand");
            AddLocalizedCommand("CommandWipe", "WipeCommand");

            data = Interface.Oxide.DataFileSystem.GetFile(Name);
            try
            {
                var temp = data.ReadObject<Dictionary<ulong, double>>();
                try
                {
                    storedData = new StoredData();
                    foreach (var old in temp.ToArray())
                    {
                        if (!storedData.Balances.ContainsKey(old.Key.ToString()))
                            storedData.Balances.Add(old.Key.ToString(), old.Value);
                    }
                    changed = true;
                }
                catch { }
            }
            catch
            {
                storedData = data.ReadObject<StoredData>();
                changed = true;
            }

            if (config.RemoveUnused)
            {
                var playerData = storedData.Balances.Keys.ToArray();
                foreach (var p in playerData.Where(p => storedData.Balances[p].Equals(config.StartAmount))) storedData.Balances.Remove(p);
                if (playerData.Length != storedData.Balances.Count) changed = true;
            }

            SaveData();
        }

        #endregion Initialization

        #region Data Handling

        private void SaveData()
        {
            if (changed)
            {
                Puts("Saving balances for players...");
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void OnServerSave() => SaveData();

        private void Unload() => SaveData();

        #endregion Data Handling

        #region API Methods

        private double Balance(string playerId)
        {
            double playerData;
            return !storedData.Balances.TryGetValue(playerId, out playerData) ? config.StartAmount : playerData;
        }

        private double Balance(ulong playerId) => Balance(playerId.ToString());

        private void Deposit(string playerId, double amount)
        {
            if (amount < 0) return;

            amount += Balance(playerId);
            SetMoney(playerId, amount >= 0 ? amount : double.MaxValue);
        }

        private void Deposit(ulong playerId, double amount) => Deposit(playerId.ToString(), amount);

        private void SetMoney(string playerId, double amount)
        {
            storedData.Balances[playerId] = amount >= 0 ? amount : 0;
            changed = true;
        }

        private void SetMoney(ulong playerId, double amount) => SetMoney(playerId.ToString(), amount);

        private bool Transfer(string playerId, string targetId, double amount)
        {
            if (Withdraw(playerId, amount))
            {
                Deposit(targetId, amount);
                return true;
            }

            return false;
        }

        private bool Transfer(ulong playerId, ulong targetId, double amount) => Transfer(playerId.ToString(), targetId.ToString(), amount);

        private bool Withdraw(string playerId, double amount)
        {
            if (amount < 0) return false;

            var balance = Balance(playerId);
            if (balance >= amount)
            {
                SetMoney(playerId, balance - amount);
                return true;
            }

            return false;
        }

        private bool Withdraw(ulong playerId, double amount) => Withdraw(playerId.ToString(), amount);

        #endregion API Methods

        #region Commands

        #region Balance Command

        private void BalanceCommand(IPlayer player, string command, string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (!player.HasPermission(permAdmin))
                {
                    Message(player, "NotAllowed", command);
                    return;
                }

                var target = FindPlayer(args[0], player);
                if (target == null) return;

                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
                return;
            }

            if (player.IsServer)
                Message(player, "UsageBalanceOthers", command);
            else
                Message(player, "YourBalance", Balance(player.Id));
        }

        #endregion Balance Command

        #region Deposit Command

        private void DepositCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageDeposit", command);
                return;
            }

            var target = FindPlayer(args[0], player);
            if (target == null) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            Deposit(target.Id, amount);
            Message(player, "PlayerBalance", target.Name, Balance(target.Id));
        }

        #endregion Deposit Command

        #region Set Money Command

        private void SetMoneyCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageSetMoney", command);
                return;
            }

            var target = FindPlayer(args[0], player);
            if (target == null) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount < 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            SetMoney(target.Id, amount);
            Message(player, "PlayerBalance", target.Name, Balance(target.Id));
        }

        #endregion Set Money Command

        #region Transfer Command

        private void TransferCommand(IPlayer player, string command, string[] args)
        {
            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageTransfer", command);
                return;
            }

            var target = FindPlayer(args[0], player);
            if (target == null) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (target.Equals(player))
            {
                Message(player, "TransferToSelf");
                return;
            }

            if (!Withdraw(player.Id, amount))
            {
                Message(player, "YouLackMoney");
                return;
            }

            Deposit(target.Id, amount * (1 - config.TransferFee));
            Message(player, "TransferredTo", amount, target.Name);
            Message(target, "ReceivedFrom", amount, player.Name);
        }

        #endregion Transfer Command

        #region Withdraw Command

        private void WithdrawCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args == null || args.Length <= 1)
            {
                Message(player, "UsageWithdraw", command);
                return;
            }

            var target = FindPlayer(args[0], player);
            if (target == null) return;

            double amount;
            double.TryParse(args[1], out amount);
            if (amount <= 0)
            {
                Message(player, "NegativeBalance");
                return;
            }

            if (Withdraw(target.Id, amount))
                Message(player, "PlayerBalance", target.Name, Balance(target.Id));
            else
                Message(player, "YouLackMoney", target.Name);
        }

        #endregion Withdraw Command

        #region Wipe Command

        private void WipeCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            storedData = new StoredData();
            changed = true;
            SaveData();
            Message(player, "DataWiped");
        }

        #endregion Wipe Command

        #endregion Commands

        #region Helpers

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.Equals(key)))
                    if (!string.IsNullOrEmpty(message.Value)) AddCovalenceCommand(message.Value, command);
            }
        }

        private IPlayer FindPlayer(string nameOrId, IPlayer player)
        {
            var foundPlayers = players.FindPlayers(nameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return null;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null || !target.IsConnected)
            {
                Message(player, "NoPlayersFound", nameOrId);
                return null;
            }

            return target;
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
