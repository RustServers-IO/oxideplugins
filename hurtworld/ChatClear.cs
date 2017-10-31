using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ChatClear", "Rick De Kikker", "1.0.2 ")]
    [Description("Clear Chat")]

    class ChatClear : HurtworldPlugin
    {
		
		private const string perm = "ChatClear.use";

		private void Init()
        {
            permission.RegisterPermission(perm, this);
			
        }

        [Command("clear")]
        void TestCommand(IPlayer player, string command, string[] args)
        {
			if (!player.HasPermission(perm))
			{
			player.Reply("<color=#DCFF66>You need the permission ChatClear.use to get access to this command!</color>");
			}
			else{
            server.Broadcast("\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n<color=orange>----> Chat Cleared <----</color>");
			}
			
        }
    }
}