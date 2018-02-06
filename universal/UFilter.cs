/*
 * TODO:
 * Add command to list blocked words by partial match
 * Add command to search for blocked words
 * Add default colors to chat names
 * Add option to show uncensored message to admin
 * Add separate list for misc blocked words
 * Allow multiple actions if desired
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("UFilter", "Wulf/lukespragg", "4.0.0", ResourceId = 1987)]
    [Description("Filter and optionally punish players for advertising and/or profanity")]

    class UFilter : CovalencePlugin
    {
        #region Initialization

        [PluginReference]
        Plugin BetterChat, Slap;

        const string permAdmin = "ufilter.admin";
        const string permBypass = "ufilter.bypass";

        #region Blocked Words

        StoredData storedData;

        class StoredData
        {
            public readonly HashSet<string> Profanities = new HashSet<string>
            {
                "4r5e", "5h1t", "5hit", "a55", "a_s_s", "anal", "anus", "ar5e", "arrse", "arse", "ass", "ass-fucker", "asses", "assface", "assfaces", "assfucker",
                "assfukka", "asshole", "assholes", "asswhole", "b!tch", "b00bs", "b17ch", "b1tch", "ballbag", "balls", "ballsack", "bastard", "bastards", "beastial",
                "beastiality", "bellend", "bestial", "bestiality", "bi+ch", "biatch", "bitch", "bitcher", "bitchers", "bitches", "bitchin", "bitching", "bitchy",
                "bloody", "blow job", "blowjob", "blowjobs", "boiolas", "bollock", "bollok", "boner", "boob", "boobs", "booobs", "boooobs", "booooobs", "booooooobs",
                "breasts", "buceta", "bugger", "bullshit", "bum", "bunny fucker", "butt", "butthole", "buttmuch", "buttplug", "c0ck", "c0cksucker", "carpet muncher",
                "cawk", "chink", "cipa", "cl1t", "clit", "clitoris", "clits", "cnut", "cock", "cock-sucker", "cockface", "cockhead", "cockmunch", "cockmuncher", "cocks",
                "cocksuck", "cocksucked", "cocksucker", "cocksuckers", "cocksucking", "cocksucks", "cocksuka", "cocksukka", "cok", "cokmuncher", "coksucka", "coon",
                "cox", "crap", "cum", "cummer", "cumming", "cums", "cumshot", "cunilingus", "cunillingus", "cunnilingus", "cunt", "cuntlick", "cuntlicker", "cuntlicking",
                "cunts", "cyalis", "cyberfuc", "cyberfuck", "cyberfucked", "cyberfucker", "cyberfuckers", "cyberfucking", "d1ck", "damn", "dick", "dickhead", "dickheads",
                "dildo", "dildos", "dink", "dinks", "dirsa", "dlck", "dog-fucker", "doggin", "dogging", "donkeyribber", "doosh", "duche", "dyke", "ejaculate", "ejaculated",
                "ejaculates", "ejaculating", "ejaculatings", "ejaculation", "ejakulate", "f u c k", "f u c k e r", "f4nny", "f_u_c_k", "fag", "fagging", "faggitt", "faggot",
                "faggots", "faggs", "fagot", "fagots", "fags", "fanny", "fannyflaps", "fannyfucker", "fanyy", "fatass", "fcuk", "fcuker", "fcuking", "feck", "fecker",
                "felching", "fellate", "fellatio", "fingerfuck", "fingerfucked", "fingerfucker", "fingerfuckers", "fingerfucking", "fingerfucks", "fistfuck", "fistfucked",
                "fistfucker", "fistfuckers", "fistfucking", "fistfuckings", "fistfucks", "flange", "fook", "fooker", "fuc", "fuck", "fucka", "fucked", "fuckedup", "fucker",
                "fuckers", "fuckhead", "fuckheads", "fuckin", "fucking", "fuckings", "fuckingshitmotherfucker", "fuckme", "fuckoff", "fucks", "fuckup", "fuckwhit", "fuckwit",
                "fudge packer", "fudgepacker", "fuk", "fuker", "fukker", "fukkers", "fukkin", "fuks", "fukwhit", "fukwit", "fuq", "fux", "fux0r", "gangbang", "gangbanged",
                "gangbangs", "gaylord", "gaysex", "goatse", "god-dam", "god-damned", "goddamn", "goddamned", "goddamnit", "hardcoresex", "hell", "heshe", "hoar", "hoare",
                "hoer", "homo", "hore", "horniest", "horny", "hotsex", "jack-off", "jackass", "jackasses", "jackoff", "jap", "jerk", "jerk-off", "jism", "jiz", "jizm",
                "jizz", "kawk", "knob", "knob end", "knobead", "knobed", "knobend", "knobhead", "knobjocky", "knobjokey", "kock", "kondum", "kondums", "kum", "kummer",
                "kumming", "kums", "kunilingus", "l3i+ch", "l3itch", "labia", "lmao", "lmfao", "lust", "lusting", "m0f0", "m0fo", "m45terbate", "ma5terb8", "ma5terbate",
                "masochist", "master-bate", "masterb8", "masterbat*", "masterbat3", "masterbate", "masterbation", "masterbations", "masturbate", "mo-fo", "mof0", "mofo",
                "mothafuck", "mothafucka", "mothafuckas", "mothafuckaz", "mothafucked", "mothafucker", "mothafuckers", "mothafuckin", "mothafucking", "mothafuckings",
                "mothafucks", "mother fucker", "motherfuck", "motherfucked", "motherfucker", "motherfuckers", "motherfuckin", "motherfucking", "motherfuckings",
                "motherfuckka", "motherfucks", "muff", "mutha", "muthafecker", "muthafuckker", "muther", "mutherfucker", "n1gga", "n1gger", "nazi", "nigg3r", "nigg4h",
                "nigga", "niggah", "niggas", "niggaz", "nigger", "niggers", "nob", "nob jokey", "nobhead", "nobjocky", "nobjokey", "numbnuts", "nutsack", "omg", "orgasim",
                "orgasims", "orgasm", "orgasms", "p0rn", "pawn", "pecker", "penis", "penisfucker", "phonesex", "phuck", "phuk", "phuked", "phuking", "phukked", "phukking",
                "phuks", "phuq", "pigfucker", "pimpis", "piss", "pissed", "pisser", "pissers", "pisses", "pissflaps", "pissin", "pissing", "pissoff", "poop", "porn", "porno",
                "pornography", "pornos", "prick", "pricks", "pron", "pube", "pusse", "pussi", "pussies", "pussy", "pussys", "queer", "rectum", "retard", "rimjaw", "rimming",
                "s hit", "s.o.b.", "s_h_i_t", "sadist", "schlong", "screwing", "scroat", "scrote", "scrotum", "semen", "sex", "sh!+", "sh!t", "sh1t", "shag", "shagger",
                "shaggin", "shagging", "shemale", "shi+", "shit", "shitdick", "shite", "shited", "shitey", "shitfuck", "shitfull", "shithead", "shitheads", "shiting",
                "shitings", "shits", "shitted", "shitter", "shitters", "shittier", "shittiest", "shitting", "shittings", "shitty", "skank", "slut", "sluts", "smartass",
                "smartasses", "smegma", "smut", "snatch", "son-of-a-bitch", "spac", "spunk", "t1tt1e5", "t1tties", "teets", "teez", "testical", "testicle", "tit", "titfuck",
                "tities", "tits", "titt", "tittie5", "tittiefucker", "titties", "tittyfuck", "tittywank", "titwank", "tosser", "turd", "tw4t", "twat", "twathead", "twatty",
                "twunt", "twunter", "v14gra", "v1gra", "vagina", "viagra", "vulva", "w00se", "wang", "wank", "wanker", "wanky", "whoar", "whore", "willies", "willy",
                "wiseass", "wiseasses", "wtf", "xrated", "xxx"
            };
        }

        #endregion

        List<object> allowedAds;
        List<object> allowedProfanity;

        bool checkForAdvertising;
        bool checkForProfanity;
        bool checkChat;
        bool checkNames;
        bool warnInChat;

        string actionForAdvertising;
        string actionForProfanity;
        string censorText;

        protected override void LoadDefaultConfig()
        {
            // Options
            Config["Check for Advertising (true/false)"] = checkForAdvertising = GetConfig("Check for Advertising (true/false)", true);
            Config["Check for Profanity (true/false)"] = checkForProfanity = GetConfig("Check for Profanity (true/false)", true);
            Config["Check Chat (true/false)"] = checkChat = GetConfig("Check Chat (true/false)", true);
            Config["Check Names (true/false)"] = checkNames = GetConfig("Check Names (true/false)", true);
            Config["Warn in Chat (true/false)"] = warnInChat = GetConfig("Warn in Chat (true/false)", true);

            // Settings
            Config["Action for Advertising (ban, kick, etc.)"] = actionForAdvertising = GetConfig("Action for Advertising (ban, kick, etc.)", "");
            Config["Action for Profanity (ban, kick, etc.)"] = actionForProfanity = GetConfig("Action for Profanity (ban, kick, etc.)", "");
            Config["Allowed Advertisements"] = allowedAds = GetConfig("Allowed Advertisements", new List<object> { "8.8.8.8", "oxidemod.org" });
            Config["Allowed Profanity"] = allowedProfanity = GetConfig("Allowed Profanity", new List<object> { "butt", "fluffer", "monkey" });
            Config["Censor Text (****)"] = censorText = GetConfig("Censor Text (****)", "****");

            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
            LoadDefaultMessages();
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBypass, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Title);
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, storedData);

        #endregion

        #region Localization

        void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Usage: {0} <add | remove> <word or phrase>",
                ["NoAdvertising"] = "Advertising is not allowed on this server",
                ["NoProfanity"] = "Profanity is not allowed on this server",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["WordAdded"] = "Word '{0}' was added to the profanity list",
                ["WordListed"] = "Word '{0}' is already in the profanity list",
                ["WordNotListed"] = "Word '{0}' is not in the profanity list",
                ["WordRemoved"] = "Word '{0}' was removed from the profanity list"
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Utilisation : {0} <add | remove> <mot ou une phrase>",
                ["NoAdvertising"] = "La publicité n'est pas autorisée sur ce serveur",
                ["NoProfanity"] = "La vulgarité n'est pas autorisée sur ce serveur",
                ["NotAllowed"] = "Vous n'êtes pas autorisé à utiliser la commande « {0} »",
                ["WordAdded"] = "Le mot « {0} » a été ajouté à la liste de gros mots",
                ["WordListed"] = "Le mot « {0} » est déjà dans la liste de gros mots",
                ["WordNotListed"] = "Le mot « {0} » n'est pas dans la liste de gros mots",
                ["WordRemoved"] = "Le mot « {0} » a été retiré de la liste de gros mots"
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Nutzung: {0} <add | remove> <wort oder satz>",
                ["NoAdvertising"] = "Werbung ist auf diesem Server nicht erlaubt",
                ["NoProfanity"] = "Beleidigungen sind auf diesem Server nicht erlaubt",
                ["NotAllowed"] = "Sie sind nicht berechtigt, diesen Befehl zu verwenden '{0}'",
                ["WordAdded"] = "Wort '{0}' wurde der Beleidigungs-Liste hinzugefügt",
                ["WordListed"] = "Wort '{0}' ist bereits in der Beleidigungs Liste",
                ["WordNotListed"] = "Wort '{0}' ist nicht in der Beleidigungs Liste",
                ["WordRemoved"] = "Wort '{0}' wurde von der Beleidigungs Liste entfernt"
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Использование: {0} <add | remove> <слово или фразу>",
                ["NoAdvertising"] = "На этом сервере не допускается реклама",
                ["NoProfanity"] = "На этом сервере не допускается ненормативная лексика",
                ["NotAllowed"] = "Нельзя использовать команду «{0}»",
                ["WordAdded"] = "Слово «{0}» был добавлен к списку ненормативной лексики",
                ["WordListed"] = "Слово «{0}» уже находится в списке ненормативной лексики",
                ["WordNotListed"] = "Слово «{0}» не включен в список ненормативной лексики",
                ["WordRemoved"] = "Слово «{0}» был удален из списка ненормативной лексики"
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandUsage"] = "Uso: {0} <add | remove> <palabra o frase>",
                ["NoAdvertising"] = "No se permite la publicidad en este servidor",
                ["NoProfanity"] = "Las malas palabras no se permiten en este servidor",
                ["NotAllowed"] = "No se permite utilizar el comando '{0}'",
                ["WordAdded"] = "La palabra '{0}' se añadió a la lista de malas palabras",
                ["WordListed"] = "La palabra '{0}' ya está en la lista de malas palabras",
                ["WordNotListed"] = "La palabra '{0}' no está en la lista de malas palabras",
                ["WordRemoved"] = "La palabra '{0}' ha sido eliminado de la lista de malas palabras"
            }, this, "es");
        }

        #endregion

        #region Filter Matching

        string[] Advertisements(string text)
        {
            var ips = Regex.Matches(text, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d{2,5})?)");
            var domains = Regex.Matches(text, @"(\w{2,}\.\w{2,3}\.\w{2,3}|\w{2,}\.\w{2,3}(:\d{2,5})?)$");
            return ips.OfType<Match>().Concat(domains.OfType<Match>()).Select(m => m.Value.ToLower()).Where(a => !allowedAds.Contains(a)).ToArray();
        }

        string[] Profanities(string text)
        {
            return text.Split(' ').Where(word => storedData.Profanities.Contains(word.ToLower()) && !allowedProfanity.Contains(word.ToLower())).ToArray();
        }

        #endregion

        #region Chat Handling

        string ProcessText(string text, IPlayer player)
        {
            if (player.HasPermission(permBypass)) return text;

            var advertisements = Advertisements(text);
            var profanities = Profanities(text);

            if (checkForAdvertising && advertisements.Length > 0)
            {
                if (warnInChat) player.Reply(Lang("NoAdvertising", player.Id));
                if (string.IsNullOrEmpty(actionForAdvertising)) return null;

                var action = actionForAdvertising.ToLower().Trim();
                if (action == "censor") foreach (var advertisement in advertisements) text = text.Replace(advertisement, censorText);
                else
                {
                    TakeAction(player, action, Lang("NoAdvertising", player.Id));
                    return null;
                }
            }

            if (checkForProfanity && profanities.Length > 0)
            {
                if (warnInChat) player.Reply(Lang("NoProfanity", player.Id));
                if (string.IsNullOrEmpty(actionForProfanity)) return null;

                var action = actionForProfanity.ToLower().Trim();
                if (action == "censor") foreach (var profanity in profanities) text = text.Replace(profanity, censorText);
                else
                {
                    TakeAction(player, action, Lang("NoProfanity", player.Id));
                    return null;
                }
            }

            return text;
        }

        void TakeAction(IPlayer player, string action, string reason)
        {
            switch (action)
            {
                case "ban":
                    player.Ban(reason);
                    break;
                case "kick":
                    player.Kick(reason);
                    break;
                case "kill":
                    player.Kill();
                    break;
                case "slap":
                    if (Slap) Slap.Call("SlapPlayer", player);
                    else PrintWarning("Slap plugin is not installed; slap action will not work");
                    break;
            }
        }

        #endregion

        #region Commands

        [Command("ufilter")]
        void FilterCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

            var argList = string.Join(" ", args.Skip(1).Select(v => v.ToString()).ToArray());
            if (args.Length == 0 || args[0] != "list" && string.IsNullOrEmpty(argList))
            {
                player.Reply(Lang("CommandUsage", player.Id, command));
                return;
            }

            switch (args[0])
            {
                case "add":
                    if (storedData.Profanities.Contains(argList))
                    {
                        player.Reply(Lang("WordListed", player.Id, argList));
                        break;
                    }

                    storedData.Profanities.Add(argList);
                    SaveData();

                    player.Reply(Lang("WordAdded", player.Id, argList));
                    break;

                case "remove":
                    if (!storedData.Profanities.Contains(argList))
                    {
                        player.Reply(Lang("WordNotListed", player.Id, argList));
                        break;
                    }

                    storedData.Profanities.Remove(argList);
                    SaveData();

                    player.Reply(Lang("WordRemoved", player.Id, argList));
                    break;

                /*case "list":
                    player.Reply(string.Join(", ", storedData.Profanities.Cast<string>().ToArray()));
                    break;*/

                default:
                    player.Reply(Lang("CommandUsage", player.Id, command));
                    break;
            }
        }

        #endregion

        #region Plugin Hooks

        object OnUserChat(IPlayer player, string message)
        {
            if (!checkChat || string.IsNullOrEmpty(message)) return null;

            var processed = ProcessText(message, player);
            if (string.IsNullOrEmpty(processed)) return true;

            message = $"{player.Name}: {processed}";
            if (BetterChat) message = (string)BetterChat.Call("API_GetFormattedMessage", player, processed);
            foreach (var target in players.Connected)
            {
#if RUST
                var rust = Game.Rust.RustCore.FindPlayerByIdString(target.Id);
                rust?.SendConsoleCommand("chat.add", player.Id, message, 1.0);
#else
                target.Message(message);
#endif
            }
            Log($"[Chat] {player.Name}: {processed}");

            return true;
        }

        void OnUserConnected(IPlayer player)
        {
            if (!checkNames) return;

            var processed = ProcessText(player.Name, player);
            if (player.Name != processed) player.Rename(processed);
        }

        object OnBetterChat() => true;

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
