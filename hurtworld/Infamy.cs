using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Infamy", "Wulf/lukespragg", "0.1.2", ResourceId = 2488)]
    [Description("Allows players with permission to add, remove, reset, or set infamy")]
    public class Infamy : CovalencePlugin
    {
        #region Initialization
 
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAlias"] = "inf",
                ["CommandUsage"] = "Usage: {0} <add|remove|reset|set> <name|id|all> [amount]",
                ["InfamyAdded"] = "{0} infamy has been added to {1}",
                ["InfamyAddedAll"] = "{0} infamy has been added to all players",
                ["InfamyRemoved"] = "{0} infamy has been removed from {1}",
                ["InfamyRemovedAll"] = "{0} infamy has been removed from all players",
                ["InfamyReset"] = "Infamy has been reset for {0}",
                ["InfamyResetAll"] = "Infamy has been reset for all players",
                ["InfamySet"] = "Infamy has been set to {0} for {1}",
                ["InfamySetAll"] = "Infamy has been set to {0} for all players",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayerNotFound"] = "Player '{0}' was not found",
            }, this);
        }

        private const string permAdmin = "infamy.admin";

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);

            AddCommandAliases("CommandAlias", "InfamyCommand");
            AddCovalenceCommand("infamy", "InfamyCommand");
        }

        #endregion

        #region Commands

        private void InfamyCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin) && player.Id != "server_console")
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 2)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            var amount = args.Length >= 3 ? Convert.ToSingle(args[2]) : 0f;
            var subCommand = args[0].ToLower();
            List<IPlayer> targets = null;

            if (args[1].ToLower() == "all")
                targets = players.Connected.ToList();
            else
            {
                var foundPlayers = players.FindPlayers(args[1]).Where(p => p.IsConnected).ToArray();
                if (foundPlayers.Length > 1)
                {
                    Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                    return;
                }

                if (foundPlayers.Length == 1)
                    targets.Add(foundPlayers[0]);
                else
                {
                    Message(player, "PlayerNotFound", args[1]);
                    return;
                }
            }

            var message = "";
            foreach (var target in targets.Where(t => t.IsConnected))
            {
                var session = target.Object as PlayerSession;
                var stats = session?.WorldPlayerEntity.GetComponent<EntityStats>();
                var infamy = stats?.GetFluidEffect(EEntityFluidEffectType.Infamy).GetValue();
                var maxInfamy = stats?.GetFluidEffect(EEntityFluidEffectType.Infamy).GetMaxValue();

                switch (subCommand)
                {
                    case "+":
                    case "add":
                        {
                            stats?.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(amount);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamyAddedAll", player.Id, amount)
                                             : Lang("InfamyAdded", player.Id, amount, target.Name);
                            break;
                        }

                    case "-":
                    case "remove":
                        {
                            stats?.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(amount);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamyRemovedAll", player.Id, amount)
                                             : Lang("InfamyRemoved", player.Id, amount, target.Name);
                            break;
                        }

                    case "r":
                    case "reset":
                        {
                            stats?.GetFluidEffect(EEntityFluidEffectType.Infamy).Reset(true);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamyResetAll", player.Id)
                                             : Lang("InfamyReset", player.Id, target.Name);
                            break;
                        }

                    case "s":
                    case "set":
                        {
                            stats?.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(amount);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamySetAll", player.Id, amount)
                                             : Lang("InfamySet", player.Id, amount, target.Name);
                            break;
                        }

                    default:
                        Message(player, "CommandUsage");
                        return;
                }
            }

            player.Reply(message);
        }

        #endregion

        #region Helpers

        private void AddCommandAliases(string key, string command)
        {
            foreach (var language in lang.GetLanguages(this))
            {
                var messages = lang.GetMessages(language, this);
                foreach (var message in messages.Where(m => m.Key.StartsWith(key))) AddCovalenceCommand(message.Value, command);
            }
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Message(Lang(key, player.Id, args));

        #endregion
    }
}
