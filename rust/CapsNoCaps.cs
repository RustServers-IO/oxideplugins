using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("CapsNoCaps", "PsychoTea", "1")]
	[Description("Turns all uppercase chat into lowercase")]
	
    public sealed class CapsNoCaps : RustPlugin
	{
        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = (BasePlayer)arg.connection.player;
            string message = arg.GetString(0, "text");

            if (!(message[0].Equals("/")))
            {
                string lowerMessage = message.ToLower();
                foreach (BasePlayer bp in BasePlayer.activePlayerList)
                    rust.SendChatMessage(bp, lowerMessage, null, player.userID.ToString());
                Interface.Oxide.ServerConsole.AddMessage("[CHAT] " + player.displayName + ": " + message);
                return true;
            }

            return null;
        }
	}
}