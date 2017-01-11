
using System;
using System.Collections.Generic;

using CodeHatch.Networking.Events.Players;

namespace Oxide.Plugins
{
    [Info("Personal Welcome", "SweetLouHD", "1.0.0")]
    public class PersonalWelcome : ReignOfKingsPlugin
    {
		
		bool configChanged;
		
		//DO NOT MODIFY VALUES HERE. PLEASE MODIFY IN THE CONFIG FILE.
		private const bool DefaultEnableWelcome = true;
		public bool EnableWelcome { get; private set; }
		
		private const bool DefaultEnableReturn = true;
		public bool EnableReturn { get; private set; }
		
		private const string DefaultWelcomeMessage = "Welcome to our server. We've noticed this is your first time here and invite you to read the server rules at http://www.example.com";
		public string WelcomeMessage { get; private set; }
		
		private const string DefaultReturnMessage = "Welcome Back!";
		public string ReturnMessage { get; private set; }
		
		void Loaded()
		{
			LoadConfigData();
		}

		protected override void LoadDefaultConfig() => PrintWarning("New configuration file created.");

		void OnPlayerSpawn(PlayerFirstSpawnEvent e)
        {
            if (e.AtFirstSpawn)
			{
				if(EnableWelcome)
				{
					Puts(e.Player.DisplayName + " has joined for the first time. Sending them the Welcome message.");
					//Adding a 45 second delay to allow for user to fully get into the game before the message plays.
					timer.Once(45, () => SendReply(e.Player, WelcomeMessage));
				}
			} else {
				if(EnableReturn)
				{
					Puts(e.Player.DisplayName + " has joined again. Sending them the Return message.");
					//Adding a 45 second delay to allow for user to fully get into the game before the message plays.
					timer.Once(45, () => SendReply(e.Player, ReturnMessage));
				}
			}
        }
		
		private void LoadConfigData()
        {
			EnableWelcome = GetConfigValue("Settings", "EnableWelcome", DefaultEnableWelcome);
			EnableReturn = GetConfigValue("Settings", "EnableReturn", DefaultEnableReturn);
			WelcomeMessage = GetConfigValue("Messages", "WelcomeMessage", DefaultWelcomeMessage);
			ReturnMessage = GetConfigValue("Messages", "ReturnMessage", DefaultReturnMessage);
			
            if (!configChanged) return;
            PrintWarning("The configuration file was updated!");
            SaveConfig();
		}
		
		private T GetConfigValue<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }
	}
}