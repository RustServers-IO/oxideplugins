using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("CustomChatCommands", "PsychoTea", "2.0.0")]
    [Description("Allows you to set up custom commands.")]

    class CustomChatCommands : CovalencePlugin
    {
        class ChatCommand
        {
            public string Command;
            public List<string> Messages;
            public string Permission;
            public List<string> ConsoleCmd;
            public ulong UserID;
            public bool Broadcast;

            public ChatCommand(string Command, List<string> Messages, string Permission = "", List<string> ConsoleCmd = null, ulong UserID = 0, bool Broadcast = false)
            {
                this.Command = Command;
                this.Messages = Messages;
                this.Permission = Permission;
                this.ConsoleCmd = ConsoleCmd;
                this.UserID = UserID;
                this.Broadcast = Broadcast;
            }
        }

        #region Config

        ConfigFile config;

        class ConfigFile
        {
            public List<ChatCommand> Commands;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Commands = new List<ChatCommand>()
                    {
                        new ChatCommand("sinfo", new List<string>() { "<color=lime>Insert your server info here!</color>" }),
                        new ChatCommand("website", new List<string>() { "Insert your server website here! This is broadcasted to all users!" }, "customchatcommands.admin", null, 0, true),
                        new ChatCommand("adminhelp", new List<string>() { "Password for TeamSpeak channel: xyz", "Discord invite: website.com/discord" }, "customchatcommands.admin"),
                        new ChatCommand("noclip", new List<string>() { "NoClip toggled." }, "customchatcommands.admin", new List<string> { "noclip" }, 0, false)
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigFile>();
        }

        protected override void LoadDefaultConfig() => config = ConfigFile.DefaultConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        void Init()
        {
            foreach (var command in config.Commands)
                AddCovalenceCommand(command.Command, "customCommand", command.Permission);
        }

        void customCommand(IPlayer player, string command, string[] args)
        {
            var search = config.Commands.Where(x => x.Command.ToLower() == command.ToLower());
            if (!search.Any()) return;
            var cmd = search.First();

            if (cmd.Broadcast)
            {
                foreach (var target in players.Connected)
                    SendMessage(target, cmd.Messages, cmd.UserID);
            }
            else
            {
                SendMessage(player, cmd.Messages, cmd.UserID);
            }

            if (cmd.ConsoleCmd != null && cmd.ConsoleCmd.Count > 0)
                foreach (var consoleCmd in cmd.ConsoleCmd)
                    player.Command(consoleCmd);
        }

        void SendMessage(IPlayer player, List<string> messages, ulong userID = 0)
        {
            foreach (var message in messages)
            {
                #if RUST
                var basePlayer = player.Object as BasePlayer;
                basePlayer?.SendConsoleCommand("chat.add", userID, message);
                #else
                player.Message(message);
                #endif
            }
        }
    }
}