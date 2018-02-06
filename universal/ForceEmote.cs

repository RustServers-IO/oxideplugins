using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ForceEmote", "Wulf/lukespragg", "0.2.2", ResourceId = 1684)]
    [Description("Force another player to do an emote on command")]
    public class ForceEmote : CovalencePlugin
    {
        #region Localization
         
        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandAlias"] = "emote",
                ["CommandUsage"] = "Usage: {0} <name or id> <emote>",
                ["Emotes"] = "Available emotes: airhump, bird, facepalm, handsup, point, salute, surrender",
                ["ForcedPlayer"] = "You forced {0} to use the {1} emote!",
                ["NoEmote"] = "You must provide an available emote",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerForced"] = "{0} forced you use the {1} emote!",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}"
            }, this);
        }

        #endregion

        #region Initailization

        private const string permUse = "forceemote.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            AddCommandAliases("CommandAlias", "EmoteCommand");
            AddCovalenceCommand("forceemote", "EmoteCommand");
        }

        #endregion

        #region Command

        private void EmoteCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length == 0)
            {
                Message(player, "CommandUsage", command);
                return;
            }

            if (args[0] == "emotes")
            {
                Message(player, "Emotes");
                return;
            }

            var foundPlayers = players.FindPlayers(args[1]).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).ToArray()));
                return;
            }

            var target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null)
            {
                Message(player, "PlayerNotFound", args[1]);
                return;
            }

            if (args.Length != 0)
            {
                var emote = args[1];

                if (emote.Length == 0)
                {
                    Message(player, "NoEmote");
                    return;
                }

                ForceEmotes(target, player, emote);
            }
        }

        #endregion

        #region Emotes

        private void ForceEmotes(IPlayer target, IPlayer player, string emote)
        {
            var targetSession = target.Object as PlayerSession;
            var emoteManager = targetSession?.WorldPlayerEntity.GetComponent<EmoteManagerServer>();
            if (emoteManager == null) return;

            switch (emote.ToLower())
            {
                case "airhump":
                    emoteManager.BeginEmoteServer(EEmoteType.Airhump);
                    break;
                case "bird":
                    emoteManager.BeginEmoteServer(EEmoteType.Bird);
                    break;
                case "facepalm":
                    emoteManager.BeginEmoteServer(EEmoteType.Facepalm);
                    break;
                case "handsup":
                    emoteManager.BeginEmoteServer(EEmoteType.HandsUp);
                    break;
                case "point":
                    emoteManager.BeginEmoteServer(EEmoteType.Point);
                    break;
                case "salute":
                    emoteManager.BeginEmoteServer(EEmoteType.Salute);
                    break;
                case "surrender":
                    emoteManager.BeginEmoteServer(EEmoteType.Surrender);
                    break;
            }

            target.Message(Lang("PlayerForced", target.Id, player.Name.Sanitize(), emote));
            player.Message(Lang("ForcedPlayer", player.Id, target.Name.Sanitize(), emote));
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
