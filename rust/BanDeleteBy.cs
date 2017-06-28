using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("BanDeleteBy", "Ryan", "1.0.2")]
    [Description("Removes all entities placed by a player when they get banned.")]

    class BanDeleteBy : RustPlugin
    {
        void OnUserBanned(string name, string id)
        {
            var ID = Convert.ToUInt64(id);
            if (ID.IsSteamId())
            {
                ConVar.Entity.DeleteBy(ID);
                LogToFile("", $"Deleting all entities owned by {name} ({id}) because they got banned", this, false);
            }
        }
    }
}