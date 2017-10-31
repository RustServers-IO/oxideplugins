using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Smart Chat Bot", "i_love_code", 1.6, ResourceId = 2435)]
    [Description("I send chat messages based on some triggers or time.")]
    public class SmartChatBot : RustPlugin
    {
        private AutomatedMessageHandler MessageHelper;
        private ChatBotConfig PluginConfiguration;

        private bool _isDebug = false;
        private int _debugChatMessageTrimmedLength = 24;

        private Dictionary<string, DateTime> MessageLastTimeSend = new Dictionary<string, DateTime>();

        void Loaded()
        {
            MessageHelper = new AutomatedMessageHandler(this);

            MessageHelper.SetupAutomatedMessages(PluginConfiguration.TimedMessages);

            PrintPluginInfo();
        }

        [ConsoleCommand("smartchatbot.printinfo")]
        void DebugConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }

            PrintPluginInfo();
        }

        [ChatCommand("smartchatbot.testmessage")]
        void ChatTestChatMessage(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            var response = TestChatMessage(args);

            DisplayMessageToUser(player, response);
        }

        [ConsoleCommand("smartchatbot.testmessage")]
        void ConsoleTestChatMessage(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }

            var response = TestChatMessage(arg.Args);

            arg.ReplyWith(response);
        }

        private string TestChatMessage(string[] args)
        {
            var message = FindAutoresponseForMessage(string.Join(" ", args));

            if (message != null)
            {
                return $"Automated response found for message. Will print '{message.Message}' to {(message.SendToServer ? "server" : "player")}";
            }

            return $"No automated message found for [{message}]";
        }

        void PrintPluginInfo()
        {
            Puts($"----------------------------------------------");
            Puts($"          SmartChatBot - i_love_code          ");
            Puts($"----------------------------------------------");
            Puts($"--    {PluginConfiguration.AutomatedResponses.Count} Automated Responses ({PluginConfiguration.AutomatedResponses.Count(x => x.IsValid)} Valid)");
            Puts($"--    {PluginConfiguration.TimedMessages.Count} Timed Messages");
            Puts($"----------------------------------------------");

            if (PluginConfiguration.AutomatedResponses.Any())
            {
                Puts($"");
                Puts($"----------------------------------------------");
                Puts($"               Automated Messages             ");
                Puts($"----------------------------------------------");

                foreach (var automatedResponse in PluginConfiguration.AutomatedResponses)
                {
                    var messageTrimmed = automatedResponse.Message.Length > _debugChatMessageTrimmedLength ? automatedResponse.Message.Substring(0, _debugChatMessageTrimmedLength) : automatedResponse.Message;
                    var automatedResponseIndex = PluginConfiguration.AutomatedResponses.IndexOf(automatedResponse);

                    Puts($"-- {automatedResponseIndex}. {messageTrimmed}... | {(automatedResponse.IsValid ? "VALID" : "INVALID")}");

                    if (!automatedResponse.TriggerWordSets.Any())
                    {
                        Puts($"---- No trigger word sets defined | INVALID");
                    }
                    else
                    {
                        foreach (var triggerWordSet in automatedResponse.TriggerWordSets)
                        {
                            var triggerSetIndex = automatedResponse.TriggerWordSets.IndexOf(triggerWordSet);

                            if (!triggerWordSet.Any())
                            {
                                Puts($"------ {triggerSetIndex}. No trigger words defined | INVALID");
                            }
                            else
                            {
                                Puts($"------ {triggerSetIndex}. Chat Message must contain ALL [{string.Join(",", triggerWordSet.ToArray())}]");
                            }
                        }
                    }
                }

                if (PluginConfiguration.TimedMessages.Any())
                {
                    Puts($"----------------------------------------------");
                }
            }

            if (PluginConfiguration.TimedMessages.Any())
            {
                Puts($"");
                Puts($"----------------------------------------------");
                Puts($"                 Timed Messages               ");
                Puts($"----------------------------------------------");

                foreach (var timedMessageConfig in PluginConfiguration.TimedMessages)
                {
                    var messageTrimmed = timedMessageConfig.Message.Length > _debugChatMessageTrimmedLength ? timedMessageConfig.Message.Substring(0, _debugChatMessageTrimmedLength) : timedMessageConfig.Message;
                    var timedMessageIndex = PluginConfiguration.TimedMessages.IndexOf(timedMessageConfig);

                    Puts($"-- {timedMessageIndex}. {messageTrimmed}... | {(timedMessageConfig.IsValid ? "VALID" : "INVALID")}");

                    if (!timedMessageConfig.IsValid)
                    {
                        if (string.IsNullOrEmpty(timedMessageConfig.Message))
                        {
                            Puts($"---- Message is invalid due to message being empty");
                        }

                        if (timedMessageConfig.Cooldown == 0.0f)
                        {
                            Puts($"---- Message is invalid due to 0s cooldown");
                        }
                    }
                    else
                    {
                        Puts($"---- Message will be displayed every {timedMessageConfig.Cooldown}s");
                    }
                }
            }
        }

        void Unload()
        {
            MessageHelper.Destroy();
        }

        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;

            if (player == null)
                return null;

            var message = string.Join(" ", arg.Args);

            var messageConfig = FindAutoresponseForMessage(message);

            if (messageConfig == null)
                return null;

            Puts("Automated message found for chat message [" + message + "]. Will display [" + messageConfig.Message + "]");

            if (messageConfig.SendToServer)
            {
                var lastMessage = GetLastServerSend(messageConfig);

                if (lastMessage != null && lastMessage.AddSeconds(PluginConfiguration.BotCooldownInSeconds) >= DateTime.Now)
                {
                    Puts("Preventing global automated message due to timed cooldown");
                    return true;
                }

                NextTick(() =>
                {
                    Puts("Displaying [" + messageConfig.Message + "] to server");
                    DisplayMessageToServer(messageConfig.Message);
                    SetLastServerSend(messageConfig);
                });

                return null;
            }

            if (player.IsAdmin)
            {
                DisplayMessageToUser(player, $"{messageConfig.Message}");
                DisplayMessageToUser(player, $"The above message is a private autoresponse. Your chat message will be shown to the server, but other player's messages that trigger this command will not be.");
                return null;
            }

            Puts("Displaying [" + messageConfig.Message + "] directly to user");
            DisplayMessageToUser(player, $"{messageConfig.Message}");

            return true;
        }

        private AutomatedResponse FindAutoresponseForMessage(string message)
        {
            return PluginConfiguration.AutomatedResponses.FirstOrDefault(x => x.ShowOnChatMessage(message));
        }

        private void SetLastServerSend(AutomatedResponse messageConfig)
        {
            var message = messageConfig.Message;

            if (!MessageLastTimeSend.ContainsKey(message))
            {
                MessageLastTimeSend.Add(message, DateTime.Now);
            }
            else
            {
                MessageLastTimeSend[message] = DateTime.Now;
            }
        }

        private DateTime GetLastServerSend(AutomatedResponse messageConfig)
        {
            if (!MessageLastTimeSend.Any())
                return DateTime.MinValue;

            var message = messageConfig.Message;

            if (!MessageLastTimeSend.ContainsKey(message))
                return DateTime.MinValue;

            var existing = MessageLastTimeSend[message];

            return existing;
        }

        private void DisplayMessageToServer(string message, params object[] args)
        {
            rust.BroadcastChat(PluginConfiguration.ChatDisplayName, string.Format(message, args), PluginConfiguration.BotChatDisplaySteamId);
        }

        public void DisplayMessageToUser(BasePlayer player, string message, params object[] args)
        {
            if (player == null)
            {
                return;
            }
            if (!player.IsConnected)
            {
                return;
            }

            rust.SendChatMessage(player, PluginConfiguration.ChatDisplayName, string.Format(message, args), PluginConfiguration.BotChatDisplaySteamId);
        }

        #region Automated Messages

        public class AutomatedMessageHandler
        {
            private SmartChatBot _plugin;
            private List<AutomatedMessage> _messages;
            private List<Timer> _timers = new List<Timer>();

            public AutomatedMessageHandler(SmartChatBot plugin)
            {
                _plugin = plugin;
            }

            public void SetupAutomatedMessages(List<AutomatedMessage> messages)
            {
                ClearTimers();

                _messages = messages.Where(x => x.IsValid).ToList();

                _plugin.Puts($"Loaded {_messages.Count} valid timed messages");

                if (_messages.Count < messages.Count)
                {
                    _plugin.Puts("A configured automated message is not valid. Please check configuration.");
                }

                StartMessages();
            }

            private void StartMessages()
            {
                foreach (var message in _messages)
                {
                    if (message.Cooldown == 0.0f)
                        continue;

                    if (message.InitialCooldown > 0.0)
                    {
                        _plugin.timer.In(message.InitialCooldown, () =>
                        {
                            DisplayMessage(message);

                            var messageTimer = _plugin.timer.Every(message.Cooldown, () => DisplayMessage(message));

                            _timers.Add(messageTimer);
                        });
                    }
                    else
                    {
                        DisplayMessage(message);

                        var messageTimer = _plugin.timer.Every(message.Cooldown, () => DisplayMessage(message));

                        _timers.Add(messageTimer);
                    }
                }
            }

            private void DisplayMessage(AutomatedMessage message)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null)
                        continue;

                    // Could add exclusions for oxide groups

                    _plugin.DisplayMessageToUser(player, message.Message);
                }
            }

            private void ClearTimers()
            {
                foreach (var timer in _timers.ToList())
                {
                    timer?.Destroy();
                }

                _timers.Clear();
            }

            public void Destroy()
            {
                ClearTimers();
            }
        }

        public class AutomatedMessage
        {
            public string Message { get; set; }
            public float InitialCooldown { get; set; } = 0.0f;
            public float Cooldown { get; set; } = 0.0f;

            [JsonIgnore]
            public bool IsValid => !string.IsNullOrEmpty(Message) && Cooldown > 0.0f;
        }

        public class AutomatedResponse
        {
            public string Message { get; set; }
            public bool SendToServer { get; set; } = false;

            [JsonIgnore]
            public bool IsValid => !string.IsNullOrEmpty(Message) && TriggerWordSets.Where(x => x.Any()).Any();

            public List<List<string>> TriggerWordSets { get; set; } = new List<List<string>>();

            public bool ShowOnChatMessage(string chatMessage)
            {
                if (!TriggerWordSets.Any())
                    return false;

                return TriggerWordSets.Any(triggerMessageSettings => triggerMessageSettings.All(triggerMessageWord => chatMessage.IndexOf(triggerMessageWord, StringComparison.InvariantCultureIgnoreCase) != -1));
            }
        }

        #endregion

        public class ChatBotConfig : BaseConfigInstance
        {
            public string ChatDisplayName { get; set; } = "<color=#00FF00>[Bot]</color>";
            public float BotCooldownInSeconds { get; set; } = 15f;
            public string BotChatDisplaySteamId { get; set; } = "1";

            public List<AutomatedMessage> TimedMessages { get; set; } = new List<AutomatedMessage>();
            public List<AutomatedResponse> AutomatedResponses { get; set; } = new List<AutomatedResponse>();
        }

        public class BaseConfigInstance
        {

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            PluginConfiguration = Config.ReadObject<ChatBotConfig>();
        }
        protected override void LoadDefaultConfig() => PluginConfiguration = new ChatBotConfig();
        protected override void SaveConfig() => Config.WriteObject(PluginConfiguration);
    }
}
