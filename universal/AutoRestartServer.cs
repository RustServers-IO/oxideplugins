using System;
using System.Collections.Generic;


namespace Oxide.Plugins
{
    [Info("AutoRestartServer", "Feramor", 0.3)]
    [Description("AutoRestartServer by Feramor")]

    class AutoRestartServer : SevenDaysPlugin
    {
        #region Veriables
        public bool AnnounceRestart => Config.Get<bool>("Chat Announcement");
        public int WarnLastXSec => Config.Get<int>("Start Chat Announcements Just Before x Seconds");
        public int WarnEveryXSec => Config.Get<int>("Chat Announcements Will Occur Every x Seconds");
        public int RestartEveryXSec => Config.Get<int>("Server Will Restart Every x Seconds");
        #endregion

        #region Localization
        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"RestartMessage","Server restart in {0} second(s)." },
                {"RestartNowMessage","Server restarting...You can reconnect in a few minutes." }
            };
            SortedDictionary<string, string> sortedMessages = new SortedDictionary<string, string>(messages);
            messages.Clear();
            foreach (KeyValuePair<string, string> Current in sortedMessages) messages.Add(Current.Key, Current.Value);
            lang.RegisterMessages(messages, this);
            SaveConfig();
        }
        #endregion
        #region Connfigration
        protected override void LoadDefaultConfig()
        {
            if ((Config["Version"] == null) || (Config.Get<string>("Version") != "0.3"))
            {
				LoadDefaultMessages()
                PrintWarning("Creating a new configuration file.");
                Config.Clear();
                Config["Version"] = "0.3";
                Config["Chat Announcement"] = true;
                Config["Start Chat Announcements Just Before x Seconds"] = 60;
                Config["Chat Announcements Will Occur Every x Seconds"] = 15;
                Config["Server Will Restart Every x Seconds"] = 14400;
                SaveConfig();
            }
        }
        #endregion
        #region Helper Methods
        string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
        #endregion
        void Init()
        {
            Puts(String.Format(GetMessage("RestartMessage"), RestartEveryXSec));
            timer.Repeat(RestartEveryXSec - WarnLastXSec, 0, () =>
            {
                int RemTime = WarnLastXSec;
                Puts(String.Format(GetMessage("RestartMessage"), RemTime));
                if (AnnounceRestart)
                {
                    PrintToChat(String.Format(GetMessage("RestartMessage"), RemTime));
                }

                timer.Repeat(WarnEveryXSec, 0, () =>
                {
                    RemTime -= WarnEveryXSec;
                    if (RemTime >= WarnEveryXSec)
                    {
                        Puts(String.Format(GetMessage("RestartMessage"), RemTime));
                        if (AnnounceRestart)
                        {
                            PrintToChat(String.Format(GetMessage("RestartMessage"), RemTime));
                        }
                    }
                    else
                    {
                        Puts(String.Format(GetMessage("RestartNowMessage")));
                        if (AnnounceRestart)
                        {
                            PrintToChat(String.Format(GetMessage("RestartNowMessage")));
                        }
                        SdtdConsole.Instance.ExecuteSync("saveworld", null);
                        SdtdConsole.Instance.ExecuteSync("shutdown", null);
                    }
                });
            });
        }
    }
}