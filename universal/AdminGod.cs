using System.Collections.Generic;
using Oxide.Core.Plugins;

/*

MADE BY
.----           ---,---  .----
| __    |    |     |     |___
|   |   |    |     |     |
`^--'   `----^     |     `^---
By: ~Gute

*/

namespace Oxide.Plugins
{
	[Info("AdminGod", "Gute", "3.0.0", ResourceId = 1786)]
    [Description("Have godmode with ease and remove.")]
	//[Url("http://oxidemod.org/plugins/admingod.1786/")]
	public class AdminGod : RustLegacyPlugin
	{
		private List<string> Good = new List<string>();

		static string permissionCanGod;

		static bool commandGood;
		static bool commandGod;
		static bool commandUngod;

		[PluginReference]
		Plugin AdminControl;

		void Init()
		{
			SetupConfig();
			SetupLang();
			SetupChatCommands();
			SetupPermissions();
			return;
		}

		string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
		void SetupLang()
		{
            // English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"ChatTag", "AdminGod"},

				{"NoPermission", "You are not allowed to use this command."},
				{"NoExistent", "No players found this name."},

				{"GoodOn", "You activate the godmode."},
				{"GoodOff", "You deactivate your godmode."},

				{"SyntaxGod", "Syntax: Use /god 'user' - For godmode for player."},
				{"God1", "You gave godmode to {0}"},
				{"God2", "{0} gave godmode to you."},

				{"SyntaxUngod", "Syntax: Use /ungod 'user' - To remove the godmode from the player."},
				{"Ungod1", "You removed godmode from {0}"},
				{"Ungod2", "{0} removed your godmode."}
			}, this);

            // Portugues Brasileiro
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"ChatTag", "AdminGod"},

				{"NoPermission", "Você não tem permissão para usa esse comando."},
				{"NoExistent", "Nenhum jogador encontrado esse nome."},

				{"GoodOn", "Você ativo o godmode."},
				{"GoodOff", "Você desativo seu godmode."},

				{"SyntaxGod", "Syntax: Use /god 'user' - para da godmode para o jogador."},
				{"God1", "Você deu godmode para {0}"},
				{"God2", "{0} deu godmode para você."},

				{"SyntaxUngod", "Syntax: Use /ungod 'user' - para remove o godmode do jogador."},
				{"Ungod1", "Você removeu godmode de {0}"},
				{"Ungod2", "{0} removeu seu godmode."}
			}, this, "pt-br");
			return;
		}

		void SetupPermissions()
		{
			permission.RegisterPermission(permissionCanGod, this);
			return;
		}

		void SetupChatCommands()
		{
			if (commandGood)
			cmd.AddChatCommand("good", this, "cmdGood");

			if (commandGod)
			cmd.AddChatCommand("god", this, "cmdGod");

			if (commandUngod)
			cmd.AddChatCommand("ungod", this, "cmdUngod");
		}

		protected override void LoadDefaultConfig()
		{
			Config["SettingsPermission"] = new Dictionary<string, object>
			{
				{"permissionCanGod", "admingod.cangod"}
			};
				Config["SettingsCommand"] = new Dictionary<string, object>
			{
				{"commandGood", true},
				{"commandGod", true},
				{"commandUngod", true}
			};
		}

		void SetupConfig()
		{
			permissionCanGod = Config.Get<string>("SettingsPermission", "permissionCanGod");

			commandGood = Config.Get<bool>("SettingsCommand", "commandGood");
			commandGod = Config.Get<bool>("SettingsCommand", "commandGod");
			commandUngod = Config.Get<bool>("SettingsCommand", "commandUngod");
		}

		void cmdGood(NetUser netUser, string command, string[] args)
		{
			var username = netUser.displayName.ToString();
			var Id = netUser.userID.ToString();
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			{
				if (!(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(Id, permissionCanGod)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this), lang.GetMessage("NoPermission", this));
				return;
			}
				if (Good.Contains(Id))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("GoodOff", this, Id));
				netUser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false);
				Good.Remove(Id);
				return;
			}
				else
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("GoodOn", this, Id));
				netUser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
				Good.Add(Id);
				return;
				}
			}
		}

		void cmdGod(NetUser netUser, string command, string[] args)
		{
			var Id = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			{
				if (!(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(Id, permissionCanGod)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
				return;
			}
				if (args.Length == 0 || args == null)
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("SyntaxGod", this, Id));
				return;
			}
				NetUser targetUser = rust.FindPlayer(args[0]);
				var name = targetUser.displayName;
				var steamId = targetUser.userID.ToString();
				if (targetUser == null)
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoExistent", this, Id));
				return;
			}
				targetUser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(true);
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("God1", this, Id), name));
				rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("God2", this, Id), username));
				return;
				}
			}
		}

		void cmdUngod(NetUser netUser, string command, string[] args)
		{
			var Id = netUser.userID.ToString();
			var username = netUser.displayName.ToString();
			bool IsAdmin = (bool)AdminControl?.Call("IsAdmin", netUser);
			{
				if (!(netUser.CanAdmin() || IsAdmin || permission.UserHasPermission(Id, permissionCanGod)))
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoPermission", this, Id));
				return;
			}
				if (args.Length == 0)
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("SyntaxUngod", this, Id));
				return;
			}
				NetUser targetUser = rust.FindPlayer(args[0]);
				var name = targetUser.displayName;
				var steamId = targetUser.userID.ToString();
				if (targetUser == null)
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), lang.GetMessage("NoExistent", this, Id));
				return;
			}
				targetUser.playerClient.rootControllable.rootCharacter.takeDamage.SetGodMode(false);
			{
				rust.SendChatMessage(netUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Ungod1", this, Id), name));
				rust.SendChatMessage(targetUser, lang.GetMessage("ChatTag", this, Id), string.Format(lang.GetMessage("Ungod2", this, Id), username));
				return;
				}
			}
		}
	}
}