using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Position", "Spicy", "1.0.1")]
    [Description("Shows players their positions.")]

    class Position : CovalencePlugin
    {
        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Position: {0}." }, this);
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Position: {0}." }, this, "fr");
            lang.RegisterMessages(new Dictionary<string, string> { ["Position"] = "Posición: {0}." }, this, "es");
        }

        [Command("position"), Permission("position.use")]
        private void cmdPosition(IPlayer player, string command, string[] args) =>
            player.Reply(string.Format(lang.GetMessage("Position", this, player.Id), player.Position().ToString()));
    }
}
