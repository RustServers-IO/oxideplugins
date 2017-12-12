using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Plugins.CupboardMessagesExt;

namespace Oxide.Plugins
{
    [Info("Cupboard Messages", "Ryan", "1.0.0")]
    [Description("Sends a configured message to a user when they place a tool cupboard")]

    public class CupboardMessages : RustPlugin
    {
        #region Declaration

        public static CupboardMessages Instance;
        private const string Perm = "cupboardmessages.use";

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Notice.1"] = "Remember to put resources in your cupboard to prevent it decaying!",
                ["Notice.2"] = "This cupboard needs resources in its contents to prevent your base from being removed.",
                ["Notice.3"] = "Your base will removed if you don't put sufficient resources in it's storage!"
            }, this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(Perm, this);
            Instance = this;
        }

        private void OnEntitySpawned(BaseNetworkable networkable)
        {
            if (!(networkable is BuildingPrivlidge)) return;
            var cupboard = (BuildingPrivlidge) networkable;
            var player = BasePlayer.FindByID(cupboard.OwnerID);
            if (player != null && player.HasPermission(Perm))
                PrintToChat(player, $"Notice.{UnityEngine.Random.Range(1, 3)}".Lang(player.UserIDString));
        }

        #endregion
    }
}

namespace Oxide.Plugins.CupboardMessagesExt
{
    public static class Extensions
    {
        private static readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();
        private static readonly Lang lang = Interface.Oxide.GetLibrary<Lang>();

        public static bool HasPermission(this BasePlayer player, string perm) =>
            permission.UserHasPermission(player.UserIDString, perm);

        public static string Lang(this string key, string id = null, params object[] args) =>
            string.Format(lang.GetMessage(key, CupboardMessages.Instance, id), args);
    }
}