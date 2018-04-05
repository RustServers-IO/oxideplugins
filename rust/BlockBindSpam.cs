using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Block Bind Spam", "Slut", "1.0.1")]
    class BlockBindSpam : RustPlugin
    {
        private void OnServerInitialized()
        {
            if (plugins.Find("BetterChat") != null)
            {
                Unsubscribe(nameof(OnUserChat));
            }
        }
        private object OnBetterChat(Dictionary<string, object> data)
        {
            var newMessage = processMessage(data["Text"].ToString());
            data["Text"] = newMessage;
            return data;
        }
        private object OnUserChat(IPlayer player, string message)
        {
            return processMessage(message);
        }
        private string processMessage(string message)
        {
            char[] charmessage = message.ToCharArray();
            if (message.ToList().Where(x => x.ToString() == @"\").Count() > 2)
            {
                return new string(message.Where(x => x.ToString() != @"\").ToArray());
            }
            return message;
        }
    }
}
