using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Spiritwalk", "Wulf/lukespragg", "2.0.0", ResourceId = 1406)]
    [Description("Leave your body behind and enter the spirit realm")]

    class Spiritwalk : CovalencePlugin
    {
        #region Initialization

        const string permAllow = "spiritwalk.allow";

        void Init()
        {
            LoadDefaultMessages();
            permission.RegisterPermission(permAllow, this);
        }

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "You aren't able to leave your body",
                ["SpiritFree"] = "Your spirit has been set free",
                ["SpiritReturned"] = "Your spirit has returned to your body"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Vous n’êtes pas en mesure de quitter votre corps",
                ["SpiritFree"] = "Votre esprit a été mis en liberté",
                ["SpiritReturned"] = "Votre esprit est revenue à votre corps"
            }, this);

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Sie sind nicht in der Lage, den Körper zu verlassen",
                ["SpiritFree"] = "Ihr Geist ist frei eingestellt",
                ["SpiritReturned"] = "Dein Geist zurückgekehrt ist, um Ihren Körper"
            }, this);

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "Вы не можете оставить ваше тело",
                ["SpiritFree"] = "Ваш дух был установлен бесплатный",
                ["SpiritReturned"] = "Dein Geist zurückgekehrt ist, um Ihren Körper"
            }, this);

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NotAllowed"] = "No eres capaz de dejar su cuerpo",
                ["SpiritFree"] = "Tu espíritu ha sido liberado",
                ["SpiritReturned"] = "Tu espíritu ha vuelto a su cuerpo"
            }, this);
        }

        #endregion

        #region Chat Command

        [Command("spirit")]
        void SpiritCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAllow))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            if (player.MaxHealth < 0 || player.Health < 0)
            {
                player.Health = 100;
                player.Reply(Lang("SpiritReturned", player.Id));
                return;
            }

            player.Health = -100;
            player.Reply(Lang("SpiritFree", player.Id));
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)System.Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
