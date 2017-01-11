using System.Linq;
using CodeHatch.Engine.Networking;
using CodeHatch.Common;

namespace Oxide.Plugins
{
	//Much credit to Mughisi for optimizing, cleaning, and allowing multiple words in the title
	
    [Info("Pop", "DumbleDora", "1")]
    public class Pop : ReignOfKingsPlugin
    {
        [ChatCommand("pop")]
        private void ShowPopup(Player player, string command, string[] args)
        {
            if (!player.HasPermission("admin")) return;

            if (args.Length < 3)
            {
                PrintToChat(player, "Usage: /pop playername \"Title\" \"Message\"");
                return;
            } 

            var target = Server.GetPlayerByName(args[0]);
            if (target == null)
            {
                PrintToChat(player, $"Player '{args[0]}' wasn't found!");
                return;
            }
            target.ShowPopup(args[1], args.Skip(2).JoinToString(" "));
        }
    }
}