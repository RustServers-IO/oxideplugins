using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ChatClear", "Rick De Kikker", "1.0.0")]
    [Description("Clears the chat from one simple command")]

    class ChatClear : CovalencePlugin
    {
        [Command("clear")]
        void TestCommand(IPlayer player, string command, string[] args)
        {
			{
				player.Reply("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n<color=orange>------> Chat Cleared <------</color>");
				foreach(PlayerSession session in GameManager.Instance.GetSessions().Values)
				{
					if(session != null && session.IsLoaded)
					{
						ChatManagerClient cmc = session.WorldPlayerEntity.gameObject.GetComponent<ChatManagerClient>();
						cmc.ClearChat();
					}
				}
			}
		}
    }
}