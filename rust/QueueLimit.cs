using System.Collections.Generic;
using ConVar;

namespace Oxide.Plugins
{
    [Info("QueueLimit", "Ryan", "1.0.0")]
    [Description("Limits the amount of people allowed to be in a queue")]

    class QueueLimit : RustPlugin
    {
        private int Limit;
        private string Perm;

        private void LoadDefaultConfig()
        {
            PrintWarning("Generating default configuration file");
            Config["Queue Limit"] = Limit = 100;
            Config["Bypass Permission"] = Perm = "queuelimit.bypass";
        }

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["KickMessage"] = "Server is full"
            }, this);
        }

        private void Init()
        {
            Limit = (int) Config["Queue Limit"];
            Perm = (string) Config["Bypass Permission"];
            permission.RegisterPermission(Perm, this);
        }

        private string CanClientLogin(Network.Connection connection)
        {
            if (permission.UserHasPermission(connection.userid.ToString(), Perm))
                return null;

            if (Admin.ServerInfo().Queued > Limit)
                return lang.GetMessage("KickMessage", this, connection.userid.ToString());

            return null;
        }
    }
}