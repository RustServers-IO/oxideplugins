using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("WelcomeMessages", "Ankawi", "1.0.4", ResourceId = 2219)]
    [Description("Sends players welcome messages")]

    class WelcomeMessages : CovalencePlugin
    {
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating new configuration file for " + this.Title + "--Version#: " + this.Version);
            Config["WaitIntervalInSeconds"] = 25f;
        }
        void Init()
        {
            LoadDefaultConfig();
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
            timer.Once((float)(Config["WaitIntervalInSeconds"]), () =>
            {
                player.Reply(covalence.FormatText(string.Format(lang.GetMessage("Welcome", this, player.Id), player.Name.Sanitize())));
            });        
        }
    }
}