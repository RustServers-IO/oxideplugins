using Rust;
using UnityEngine;

using System;
using System.Collections.Generic;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("AngryAuthlevel", "Tori1157", "1.0.2")]
	[Description("Automatically gives people Authlevel")]
	
	class AngryAuthlevel : CovalencePlugin
	{
		#region Loaded

		private bool Changed;
		private string AuthLevel1List;
		private string AuthLevel2List;

		private void Init()
		{
			LoadVariables();
		}

		protected override void LoadDefaultConfig()
		{
			Puts("Creating a new configuration file!");
			Config.Clear();
			LoadVariables();
		}

		private void LoadVariables() 
		{
			AuthLevel1List = Convert.ToString(GetConfig("AuthLevel1", "STEAM_ID", new List<string>{
			"",
			}));

			AuthLevel2List = Convert.ToString(GetConfig("AuthLevel2", "STEAM_ID", new List<string>{
			"",
			}));

			if (Changed)
			{
				SaveConfig();
				Changed = false;		
			}	
		}

		void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GivenAuthlevel1"] = "<color=red>You have been given Auth Level 1 automatically, reconnect for it to activate.</color>",
                ["GivenAuthlevel2"] = "<color=red>You have been given Auth Level 2 automatically, reconnect for it to activate.</color>",

                ["ConsoleMessage1"] = "{0}({1}) has been given Auth Level 1 automatically.",
                ["ConsoleMessage2"] = "{0}({1}) has been given Auth Level 2 automatically."
            }, this);

            // French
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GivenAuthlevel1"] = "<color=red>Vous avez automatiquement reçu Auth Level 1, reconnectez-le pour l'activer.</color>",
                ["GivenAuthlevel2"] = "<color=red>Vous avez automatiquement reçu Auth Level 2, reconnectez-le pour l'activer.</color>",

                ["ConsoleMessage1"] = "{0}({1}) a reçu automatiquement Auth Level 1.",
                ["ConsoleMessage2"] = "{0}({1}) a reçu automatiquement Auth Level 2."
            }, this, "fr");

            // German
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GivenAuthlevel1"] = "<color=red>Du hast Auth Level 1 automatisch erhalten, um es wieder zu aktivieren.</color>",
                ["GivenAuthlevel2"] = "<color=red>Du hast Auth Level 2 automatisch erhalten, um es wieder zu aktivieren.</color>",

                ["ConsoleMessage1"] = "{0}({1}) wurde Auth Level 1 automatisch erhalten.",
                ["ConsoleMessage2"] = "{0}({1}) wurde Auth Level 2 automatisch erhalten."
            }, this, "de");

            // Russian
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GivenAuthlevel1"] = "<color=red>Вам автоматически предоставили Auth Level 1, чтобы снова активировать его.</color>",
                ["GivenAuthlevel2"] = "<color=red>Вам автоматически предоставили Auth Level 2, чтобы снова активировать его.</color>",

                ["ConsoleMessage1"] = "{0}({1}) был автоматически присвоен уровень 1-го уровня.",
                ["ConsoleMessage2"] = "{0}({1}) был автоматически присвоен уровень 2-го уровня."
            }, this, "ru");

            // Spanish
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GivenAuthlevel1"] = "<color=red>Se le ha otorgado Auth Level 1 automáticamente, vuelva a conectarlo para activarlo.</color>",
                ["GivenAuthlevel2"] = "<color=red>Se le ha otorgado Auth Level 2 automáticamente, vuelva a conectarlo para activarlo.</color>",

                ["ConsoleMessage1"] = "{0}({1}) se ha otorgado Auth Nivel 1 automáticamente.",
                ["ConsoleMessage2"] = "{0}({1}) se ha otorgado Auth Nivel 2 automáticamente."
            }, this, "es");
        }

		#endregion


		#region Functions

		private void OnUserConnected(IPlayer player)
		{
			if (player == null) return;

			BasePlayer BPlayer = player.Object as BasePlayer;
			
			//Checking config files for ID's, it's crude & dirty, but it works.

			// Auth level 1
			string Auth1 = "";
			foreach(var ID in Config["AuthLevel1", "STEAM_ID"] as List<object>)
			Auth1 = Auth1 + ID + " ";

			// Auth level 2
			string Auth2 = "";
			foreach(var ID in Config["AuthLevel2", "STEAM_ID"] as List<object>)
			Auth2 = Auth2 + ID + " ";

			if (Auth1.Contains(player.Id))
			{
				if (BPlayer.net.connection.authLevel == 1) return;

				server.Command("moderatorid " + player.Id + " " + player.Name);
				server.Command("server.writecfg");

				PrintWarning(Lang("ConsoleMessage1", null, player.Name, player.Id));

				timer.Once(5f, () =>
				{
					player.Reply(Lang("GivenAuthlevel1", null));
				});
			}

			if (Auth2.Contains(player.Id))
			{
				if (BPlayer.net.connection.authLevel == 2) return;

				server.Command("ownerid " + player.Id + " " + player.Name);
				server.Command("server.writecfg");

				PrintWarning(Lang("ConsoleMessage2", null, player.Name, player.Id));

				timer.Once(5f, () =>
				{
					player.Reply(Lang("GivenAuthlevel2", null));
				});
			}
		}

		#endregion
		

		#region Usefull/Needed Functions

		object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }

            object value;
            
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;  
        } 

        // Language converter
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
	}
}