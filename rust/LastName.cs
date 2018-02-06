using System;
using System.Collections.Generic;
using System.Linq;

using Oxide.Core;
using Oxide.Core.Configuration;

/* --- Do not edit anything here if you don't know what are you doing --- */

namespace Oxide.Plugins
{
	[Info("LastName", "deer_SWAG", "0.1.16", ResourceId = 1227)]
	[Description("Stores all usernames")]
	class LastName : RustPlugin
	{
		class StoredData
		{
			public HashSet<Player> Players = new HashSet<Player>();

			public StoredData() { }

			public void Add(Player player) => Players.Add(player);
		}

		class Player
		{
			public ulong userID;
			public HashSet<string> Names = new HashSet<string>();

			public Player() { }
			public Player(ulong userID) { this.userID = userID; }

			public void Add(string name) => Names.Add(name);
		}

		StoredData 		  data;
		DynamicConfigFile nameChangeData;

		protected override void LoadDefaultConfig()
		{
			CheckConfig();
			Puts("Default config was saved and loaded");
		}

		void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>()
			{
				{ "NoAccess", "You don't have access to this command" },
				{ "WrongQueryChat", "/names <name/steamID>" },
				{ "WrongQueryConsole", "player.names <name/steamID>" },
				{ "NoPlayerFound", "No players found with that name/steamID" },
				{ "PlayerWasFound", "{name}({id}) was also known as: " }
			}, this);
		}

		void Loaded()
		{
			LoadDefaultMessages();
			CheckConfig();

			data = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(Title);

			if (data == null)
			{
				RaiseError("Unable to load data file");
				rust.RunServerCommand("oxide.unload LastName");
			}

			if (IsPluginExists("NameChange"))
				nameChangeData = Interface.GetMod().DataFileSystem.GetDatafile("NameChange");
		}

		void OnPlayerConnected(Network.Message packet)
		{
			if ((bool)Config["ReplaceWithFirstName"] && data.Players.Count > 0)
			{
				if (nameChangeData != null)
				{
					foreach (KeyValuePair<string, object> item in nameChangeData)
					{
						if (Convert.ToUInt64(item.Key) != packet.connection.userid)
						{
							foreach (Player dataPlayer in data.Players)
							{
								if (packet.connection.userid == dataPlayer.userID)
								{
									packet.connection.username = dataPlayer.Names.First();
									goto end;
								}
							}
						}
					}
					end:;
				}
				else
				{
					foreach (Player dataPlayer in data.Players)
					{
						if (packet.connection.userid == dataPlayer.userID)
						{
							packet.connection.username = dataPlayer.Names.First();
							break;
						}
					}
				}
			}
		}

		void OnPlayerInit(BasePlayer player)
		{
			if (data.Players.Count > 0)
			{
				bool found = false;
				bool newName = false;

				foreach (Player dataPlayer in data.Players)
				{
					if (dataPlayer.userID == player.userID)
					{
						found = true;

						foreach (string name in dataPlayer.Names)
						{
							if (name == player.displayName)
								break;
							else
								newName = true;
						}

						if (newName)
							dataPlayer.Add(player.displayName);

						break;
					}
				}

				if (!found)
				{
					Player p = new Player(player.userID);
					p.Add(player.displayName);

					data.Add(p);
				}
			}
			else
			{
				Player p = new Player(player.userID);
				p.Add(player.displayName);

				data.Add(p);
			}

			SaveData();
		}

		[ChatCommand("lastname")]
		void cmdChat(BasePlayer player, string command, string[] args)
		{
			if (player.net.connection.authLevel >= (int)Config["CommandAuthLevel"])
				if (args.Length > 0)
					PrintToChat(player, GetNames(args));
				else
					PrintToChat(player, lang.GetMessage("WrongQueryChat", this));
			else
				PrintToChat(player, lang.GetMessage("NoAccess", this));
		}

		[ConsoleCommand("player.names")]
		void cmdConsole(ConsoleSystem.Arg arg)
		{
			if (arg.HasArgs())
				Puts(GetNames(arg.Args));
			else
				Puts(lang.GetMessage("WrongQueryConsole", this));
		}

		string GetNames(string[] args)
		{
			string message = lang.GetMessage("PlayerWasFound", this);
			string name = string.Empty;

			try
			{
				ulong id = Convert.ToUInt64(args[0]);

				foreach (Player dataPlayer in data.Players)
				{
					if (dataPlayer.userID == id)
					{
						name = dataPlayer.Names.First();

						foreach (string n in dataPlayer.Names)
							message += n + ", ";

						break;
					}
				}
			}
			catch { }
			finally
			{
				if (name.Length > 0)
				{
					message = message.Substring(0, message.Length - 2).Replace("{name}", name).Replace("{id}", args[0]);
				}
				else
				{
					Player found = null;

					for (int i = 0; i < args.Length; i++)
						name += args[i] + " ";

					name = name.TrimEnd();

					foreach (Player dataPlayer in data.Players)
					{
						foreach (string s in dataPlayer.Names)
						{
							if (s.Equals(name, StringComparison.CurrentCultureIgnoreCase))
							{
								found = dataPlayer;
								goto end;
							}
							else if (s.StartsWith(name, StringComparison.CurrentCultureIgnoreCase))
							{
								found = dataPlayer;
								goto end;
							}
							else if (StringContains(s, name, StringComparison.CurrentCultureIgnoreCase))
							{
								found = dataPlayer;
								goto end;
							}
						}
					} end:;

					if (found != null)
					{
						foreach (string s in found.Names)
							message += s + ", ";

						message = message.Substring(0, message.Length - 2).Replace("{name}", name).Replace("{id}", found.userID.ToString());
					}
					else
					{
						message = lang.GetMessage("NoPlayerFound", this);
					}
				}
			}

			return message;
		}

		void SendHelpText(BasePlayer player)
		{
			if (player.net.connection.authLevel >= (int)Config["CommandAuthLevel"])
				PrintToChat(player, lang.GetMessage("WrongQuery", this));
		}

		void CheckConfig()
		{
			ConfigItem("ReplaceWithFirstName", false);
			ConfigItem("CommandAuthLevel", 0);

			SaveConfig();
		}

		void SaveData()
		{
			Interface.GetMod().DataFileSystem.WriteObject(Title, data);
		}

		// ----------------------------- UTILS -----------------------------
		// -----------------------------------------------------------------

		void ConfigItem(string name, object defaultValue)
		{
			Config[name] = Config[name] ?? defaultValue;
		}

		void ConfigItem(string name, string name2, object defaultValue)
		{
			Config[name, name2] = Config[name, name2] ?? defaultValue;
		}

		private bool IsPluginExists(string name)
		{
			return Interface.GetMod().GetLibrary<Core.Libraries.Plugins>().Exists(name);
		}

		bool StringContains(string source, string value, StringComparison comparison)
		{
			return source.IndexOf(value, comparison) >= 0;
		}
	}
}