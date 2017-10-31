using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("CapsNoCaps", "PsychoTea", "1.3.3")]
    [Description("Turns all uppercase chat into lowercase")]

    class CapsNoCaps : CovalencePlugin
    {
        private const string IgnorePerm = "capsnocaps.ignore";

        [PluginReference] Plugin BetterChat;

        void Init()
        {
            permission.RegisterPermission(IgnorePerm, this);
        }

        object OnUserChat(IPlayer player, string message)
        {
            if (BetterChat != null) return null;
            if (HasPerm(player.Id)) return null;

            foreach (var target in players.Connected)
            {
                #if RUST
                
                var rPlayer = player.Object as BasePlayer;
                
                string colour = "#5af";
                if (rPlayer.IsAdmin) colour = "#af5";
                if (rPlayer.IsDeveloper) colour = "#fa5";
                
                var rTarget = target.Object as BasePlayer;
                
                rTarget?.SendConsoleCommand("chat.add2", rPlayer.userID, message, rPlayer.displayName, colour);
                
                #else

                target.Message($"{player.Name}: {message}");
                
                #endif
            }

            Log($"[CHAT] {player.Name}: {message}");

            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            IPlayer player = dict["Player"] as IPlayer;
            if (HasPerm(player.Id)) return null;

            dict["Text"] = (dict["Text"] as string).SentenceCase();
            return dict;
        }

        bool HasPerm(string userID, string perm = IgnorePerm) => permission.UserHasPermission(userID, perm);
    }
}