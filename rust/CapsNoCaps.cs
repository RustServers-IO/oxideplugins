using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CapsNoCaps", "PsychoTea", "1.3.0")]
    [Description("Turns all uppercase chat into lowercase")]

    public sealed class CapsNoCaps : RustPlugin
    {
        [PluginReference]
        Plugin BetterChat;

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (BetterChat != null) return null;
            BasePlayer player = (BasePlayer)arg.Connection.player;
            string message = arg.GetString(0, "text").SentenceCase();
            string colour = "#5af";
            if (player.IsAdmin) colour = "#af5";
            if (player.IsDeveloper) colour = "#fa5";
            ConsoleNetwork.BroadcastToAllClients("chat.add2", new object[] { player.userID, message, player.displayName, colour });
            Debug.Log($"[CHAT] {player.displayName}[{player.net.ID}/{player.userID}] : {message}");
            return true;
        }

        Dictionary<string, object> OnBetterChat(Dictionary<string, object> dict)
        {
            dict["Text"] = (dict["Text"] as string).SentenceCase();
            return dict;
        }
    }
}