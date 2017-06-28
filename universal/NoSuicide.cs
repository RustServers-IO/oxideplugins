/*
 * TODO:
 * Add option to only block suicide when downed, with optional timer
 */

using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoSuicide", "Wulf/lukespragg", "0.1.3", ResourceId = 2123)]
    [Description("Stops players from suiciding/killing themselves")]

    class NoSuicide : CovalencePlugin
    {
        #region Initialization

        const string permExclude = "nosuicide.exclude";

        void Init()
        {
#if !HURTWORLD && !RUST
            throw new NotSupportedException("This plugin does not support this game");
#endif
            permission.RegisterPermission(permExclude, this);

            lang.RegisterMessages(new Dictionary<string, string> { ["NotAllowed"] = "Sorry, suicide is not an option!" }, this);
            lang.RegisterMessages(new Dictionary<string, string> { ["NotAllowed"] = "Désolé, le suicide n’est pas un choix !" }, this, "fr");
            lang.RegisterMessages(new Dictionary<string, string> { ["NotAllowed"] = "Es tut uns leid, ist Selbstmord keine Wahl!" }, this, "de");
            lang.RegisterMessages(new Dictionary<string, string> { ["NotAllowed"] = "К сожалению, самоубийство-это не вариант!" }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string> { ["NotAllowed"] = "Lo sentimos, el suicidio no es una opción!" }, this, "es");
        }

        #endregion

        #region Suicide Handling

        bool CanSuicide(string id)
        {
            if (permission.UserHasPermission(id, permExclude)) return true;

            players.FindPlayer(id)?.Message(lang.GetMessage("NotAllowed", this, id));
            return false;
        }

#if HURTWORLD
        object OnPlayerSuicide(PlayerSession session) => CanSuicide(session.SteamId.ToString()) ? (object)null : true;
#endif
#if RUST
        object OnServerCommand(ConsoleSystem.Arg arg) => arg.cmd?.Name != "kill" || CanSuicide(arg.Connection?.userid.ToString()) ? (object)null : true;
#endif
        #endregion
    }
}
