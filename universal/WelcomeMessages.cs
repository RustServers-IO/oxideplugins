using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("WelcomeMessages", "Ankawi", "1.0.0", ResourceId = 0)]
    [Description("Sends players welcome messages")]

    class WelcomeMessages : CovalencePlugin
    {
        void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome"] = "[#cyan]Welcome to the server {0}![/#]"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome"] = "[#cyan]Bienvenido al servidor {0}![/#]"
            }, this, "es");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Welcome"] = "[#cyan]Bienvenue sur le serveur {0}![/#]"
            }, this, "fr");
        }

        void OnUserConnected(IPlayer player)
        {
            player.Reply(covalence.FormatText(string.Format(lang.GetMessage("Welcome", this, player.Id), player.Name)));
        }
    }
}