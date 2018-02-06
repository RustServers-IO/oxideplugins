using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Infamy", "Wulf/lukespragg", "0.1.3", ResourceId = 2488)]
    [Description("Allows players with permission to add, remove, reset, or set infamy")]
    public class Infamy : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandInfamy"] = "infamy",
                ["CommandUsage"] = "Usage: {0} <add|remove|reset|set> <name|id|all> [amount]",
                ["InfamyAdded"] = "{0} infamy has been added to {1}",
                ["InfamyAddedAll"] = "{0} infamy has been added to all players",
                ["InfamyRemoved"] = "{0} infamy has been removed from {1}",
                ["InfamyRemovedAll"] = "{0} infamy has been removed from all players",
                ["InfamyReset"] = "Infamy has been reset for {0}",
                ["InfamyResetAll"] = "Infamy has been reset for all players",
                ["InfamySet"] = "Infamy has been set to {0} for {1}",
                ["InfamySetAll"] = "Infamy has been set to {0} for all players",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permAdmin = "infamy.admin";

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);

            AddLocalizedCommand("CommandInfamy", "InfamyCommand");
        }

        #endregion Initialization

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

            var targets = new List<IPlayer>();
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
                    Message(player, "NoPlayersFound", args[1]);
                    return;
                }
            }

            var message = "";
            foreach (var target in targets.Where(t => t.IsConnected))
            {
                LogWarning(target.Name);
                var session = target.Object as PlayerSession;
#if HURTWORLDITEMV2
                var stats = session?.WorldPlayerEntity.Stats;
#else
                var stats = session?.WorldPlayerEntity.GetComponent<EntityStats>();
#endif
                if (stats == null) return;

                var infamy = stats.GetFluidEffect(EEntityFluidEffectType.Infamy).GetValue();
                var maxInfamy = stats.GetFluidEffect(EEntityFluidEffectType.Infamy).GetMaxValue();
                var amount = args.Length >= 3 ? Convert.ToSingle(args[2]) : 0f;
                var subCommand = args[0].ToLower();

                switch (subCommand)
                {
                    case "+":
                    case "add":
                        {
                            stats.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(amount);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamyAddedAll", player.Id, amount)
                                             : Lang("InfamyAdded", player.Id, amount, target.Name);
                            break;
                        }

                    case "-":
                    case "remove":
                        {
                            stats.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(amount);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamyRemovedAll", player.Id, amount)
                                             : Lang("InfamyRemoved", player.Id, amount, target.Name);
                            break;
                        }

                    case "r":
                    case "reset":
                        {
                            stats.GetFluidEffect(EEntityFluidEffectType.Infamy).Reset(true);
                            message = args[1].ToLower() == "all"
                                             ? Lang("InfamyResetAll", player.Id)
                                             : Lang("InfamyReset", player.Id, target.Name);
                            break;
                        }

                    case "s":
                    case "set":
                        {
                            stats.GetFluidEffect(EEntityFluidEffectType.Infamy).SetValue(amount);
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

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        #endregion Helpers
    }
}
