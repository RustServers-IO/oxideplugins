using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ProfanityFilter", "Spicy", "1.0.1")]
    [Description("Filters profanity.")]

    class ProfanityFilter : CovalencePlugin
    {
        #region Config

        private List<string> bannedWords;

        protected override void LoadDefaultConfig()
        {
            Config["BannedWords"] = new List<string>
            {
                "fuck",
                "shit",
                "cunt"
            };
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            bannedWords = Config.Get<List<string>>("BannedWords");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BannedWord"] = "That's a banned word."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BannedWord"] = "Ce mot est interdit."
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BannedWord"] = "Esta palabra es prohibida."
            }, this, "es");
        }

        private object OnUserChat(IPlayer player, string message)
        {
            if (IsProfanity(message))
            {
                player.Reply(lang.GetMessage("BannedWord", this, player.Id));
                return true;
            }
            return null;
        }

        #endregion

        #region API

        bool IsProfanity(string message)
        {
            foreach (string bannedWord in bannedWords)
                if (message.ToLower().Contains(bannedWord.ToLower()))
                    return true;
            return false;
        }

        #endregion
    }
}
