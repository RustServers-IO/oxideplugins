using System;
using System.Collections.Generic;
using System.Text;
using CodeHatch.Blocks.Networking.Events;
using CodeHatch.Common;
using CodeHatch.Engine.Networking;
using CodeHatch.Networking.Events;
using CodeHatch.Networking.Events.Entities;
using CodeHatch.Networking.Events.Players;
using CodeHatch.Permissions;

namespace Oxide.Plugins
{
    [Info("Hulk", "SweetLouHD", "1.0.2", ResourceId = 1463)]
    public class Hulk : ReignOfKingsPlugin
	{
		public float hulkAmt = 10000f;
		public static string prefix = "[FFFFFF][[64CEE1]Server[FFFFFF]]: ";
		public const string UsePermission = "oxide.hulk";
		Permission permissions;
		List<string> hulkList = new List<string>();

		void OnServerInitialized()
		{
			permissions = Server.Permissions;
		}

		bool HasPermission(Player player, string perm = null){return PlayerExtensions.HasPermission(player, perm);}

		private void SendHelpText(Player player)
		{
			if(HasPermission(player, UsePermission)){
				PrintToChat(player, "/hulk on - [666666]Turn on Hulk Mode.[-]");
				PrintToChat(player, "/hulk off - [666666]Turn off Hulk Mode.[-]");
				PrintToChat(player, "/hulk status - [666666]Displays status of your Hulk Mode.[-]");
				PrintToChat(player, "/hulk status {{name}} - [666666]Displays status of the specified names Hulk Mode.[-]");
				PrintToChat(player, "/hulk list - [666666]List of players that currently have Hulk turned on.[-]");
				PrintToChat(player, "/hulk amt - [666666]Displays the current Hulk Amount.[-]");
				PrintToChat(player, "/hulk amt {{number}}- [666666]Sets the amount of Damage and Repair Hulk does. This affects all users.[-]");
			}
		}

		[ChatCommand("hulk")]
		private void cmdHulk(Player player, string cmd, string[] args)
		{
			if (!HasPermission(player, UsePermission))
			{
				PrintToChat(player, "You are not permitted to use this command.[-]");
				return;
			}
			if (args.Length < 1) PrintToChat(player, $"{prefix}Incorrect usage. Type /help if you need.[-]");
			switch(args[0].ToLower())
			{
				case "on":
					hulkList.Add(player.Id.ToString());
					PrintToChat(player, $"{prefix}Hulk mode is now turned on. Amt: {hulkAmt}[-]");
					break;
				case "off":
					hulkList.Remove(player.Id.ToString());
					PrintToChat(player, $"{prefix}Hulk mode is now turned off.[-]");
					break;
				case "status":
					if(args.Length < 2)
					{
						if(hulkList.Contains(player.Id.ToString()))
						{
							PrintToChat(player, $"{prefix}You currently have Hulk turned on. Amt: {hulkAmt}[-]");
						}
						else
						{
							PrintToChat(player, $"{prefix}You currently have Hulk turned off.[-]");
						}
					}
					else if(args.Length < 3)
					{
						List<Player> onlineplayers = Server.ClientPlayers as List<Player>;
						foreach (Player toCheck in onlineplayers.ToArray())
						{
							if(toCheck.DisplayName.ToLower() == args[1].ToLower())
							{
								if(hulkList.Contains(toCheck.Id.ToString()))
									PrintToChat(player, $"{prefix}{args[1]} currently has Hulk turned on. Amt: {hulkAmt}[-]");
								else
									PrintToChat(player, $"{prefix}{args[1]} currently has Hulk turned off.[-]");
								return;
							}
						}
						PrintToChat(player, $"{prefix}{args[1]} is currently not online.[-]");
					}
					else
					{
						PrintToChat(player, "{prefix}Incorrect usage. Type /help if you need.[-]");
					}
					break;
				case "list":
					if(args.Length < 2)
					{
						PrintToChat(player, $"{prefix}List of Hulks:");
						List<Player> onlineplayers = Server.ClientPlayers as List<Player>;
						var i = 0;
						foreach (Player toCheck in onlineplayers.ToArray())
						{
							if(hulkList.Contains(toCheck.Id.ToString()))
							{
								i++;
								PrintToChat(player, $"     {toCheck.DisplayName}");
							}
								
						}
						if(i == 0)
							PrintToChat(player, "     No one has Hulk Turned on.[-]");
						else
							PrintToChat(player, "[-]");
						return;
					}
					else
						PrintToChat(player, "{prefix}Incorrect usage. Type /help if you need.[-]");
					break;
				case "amt":
					if(args.Length < 2)
					{
						PrintToChat(player, "{prefix}Hulk Amount is currently set to {hulkAmt}.[-]");
						return;
					}
					else if(args.Length < 3)
					{
						try
						{
							hulkAmt = float.Parse(args[1]);
							if(hulkAmt < 0)
							{
								PrintToChat(player, $"{prefix}{args[1]} is not a valid number. Please try again.[-]");
								return;
							}
							List<Player> onlineplayers = Server.ClientPlayers as List<Player>;
							foreach (Player toCheck in onlineplayers.ToArray())
							{
								if(hulkList.Contains(toCheck.Id.ToString()))
									PrintToChat(toCheck, $"{prefix}{player.DisplayName} has just adjusted the Hulk Amount to {args[1]}.[-]");
								else if(toCheck == player)
									PrintToChat(player, $"{prefix}You have changed Hulk Amount to {args[1]}.[-]");
							}
						}
						catch
						{
							PrintToChat(player, $"{prefix}{args[1]} is not a valid number. Please try again.[-]");
						}
					}
					break;
				default:
					PrintToChat(player, "{prefix}Incorrect usage. Type /help if you need.[-]");
					break;
			}
		}

		private void OnPlayerConnected(Player player)
		{
			hulkList.Remove(player.Id.ToString());
		}

		private void OnPlayerDisconnected(Player player)
		{
			hulkList.Remove(player.Id.ToString());
		}

		private void OnEntityHealthChange(EntityDamageEvent e)
		{
			switch(e.Damage.DamageTypes.ToString())
			{
				case "Hunger":
				case "Thirst":
					return;
				break;
				default:
					try{
						if (e.Damage.DamageSource.Owner is Player)
						{
							Player damager = e.Damage.DamageSource.Owner;
							if(!hulkList.Contains(damager.Id.ToString())) return;
							if (!HasPermission(damager, UsePermission)) return;
							if(damager.DisplayName.ToLower() == "server") return;
							if(e.Damage.Amount < 0)
							{
								e.Damage.Amount = hulkAmt * -1f;
								PrintToChat(damager, $"{prefix}Hulk healing {hulkAmt.ToString()} damage.[-]");
								return;
							}
							e.Damage.Amount = hulkAmt;
							PrintToChat(damager, $"{prefix}Hulk dealing {hulkAmt.ToString()} damage.[-]");
							return;
						}
						else
						{
							return;
						}
					}
					catch
					{
						return;
					}
					return;
				break;
			}
		}

		void OnCubeTakeDamage(CubeDamageEvent e)
		{
			Player damager = e.Damage.DamageSource.Owner;
			if(!hulkList.Contains(damager.Id.ToString())) return;
			if (!HasPermission(damager, UsePermission)) return;
			if(e.Damage.Amount < 0)
			{
				e.Damage.Amount = hulkAmt * -1f;
				PrintToChat(damager, $"{prefix}Hulk healing {hulkAmt.ToString()} damage.[-]");
				return;
			}
			PrintToChat(damager, $"{prefix}Hulk dealing {hulkAmt.ToString()} damage.[-]");
			e.Damage.Amount = hulkAmt;
		}
	}
}
