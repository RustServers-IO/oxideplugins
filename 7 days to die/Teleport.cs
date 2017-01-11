using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Teleport", "OpenFunRus", 1.1)]
    [Description("Teleportation system by OpenFun")]
	
    class Teleport : SevenDaysPlugin
    {
		public int DTHome => Config.Get<int>("Delay teleport to Home");
		public int DTPoint => Config.Get<int>("Delay teleport to Point");
		public int DTPlayer => Config.Get<int>("Delay teleport to Player");
		public int DDHome => Config.Get<int>("Delay delete Home");
		
		protected override void LoadDefaultConfig()
		{ 
			PrintWarning("Creating a new configuration file.");
			Config.Clear();
			Config["Delay teleport to Home"] = 300;
			Config["Delay teleport to Point"] = 600;
			Config["Delay teleport to Player"] = 600;
			Config["Delay delete Home"] = 0;
			SaveConfig();
		}
		
		void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"HelpInfo", "/helptp - shows help info"},
                {"Home", "/home - teleport to home"},
                {"HomeAdded", "Home has been added"},
                {"HomeExists", "You already have a home"},
				{"HomeRemoved", "You removed your home"},
                {"NoHomes", "You do not have any homes"},
                {"RemoveHome", "/delhome - delete a home"},
				{"Wait", "Wait"},
				{"Seconds", "seconds"},
                {"SetHome", "/sethome - add a home"},
                {"Teleported", "You teleported to"},
                {"AddTeleport", "You added teleport"},
				{"NotAdmin", "You are not Admin..."},
				{"DelTeleport", "You removed teleport"},
				{"NoTeleport", "You do not have teleport"},
				{"AddTpInfo", "/addtp <name> - add teleport point"},
				{"DelTpInfo", "/deltp <name> - del teleport point"},
				{"TpInfo", "/tp <name> - teleport to point"},
				{"TpList", "/listtp - show teleport list"},
				{"ListTp", "Teleport - /tp"},
				{"LandWarning", "You are on private territory"},
				{"LandWarningFriend", "Your Friend are on private territory"},
				{"PlayerNotFound", "Player not found"},
				{"NotFriend", "Player is not your friend"},
				{"TeleFriend", "/tp <nickname> - teleport to friend."},				
				
				
            }, this);
        }
		
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);
		
		class StoredData
		{
			public Dictionary<string, PlayerHomes> Homes  = new Dictionary<string, PlayerHomes>();
			public Dictionary<string, PlayerDelay> DelayTeleport  = new Dictionary<string, PlayerDelay>();
			public Dictionary<string, AddTeleport> AddTeleport  = new Dictionary<string, AddTeleport>();
			
			
			public StoredData()
			{
			}
		}

		class PlayerHomes
		{
			  public string Name;
			  public string HomeX;
			  public string HomeY;
			  public string HomeZ;
			  public PlayerHomes(){}
		}
		class PlayerDelay
		{
			  public string Delay;
			  public PlayerDelay(){}
		}
		class AddTeleport
		{
			  public string Name;
			  public string TpX;
			  public string TpY;
			  public string TpZ;
			  public string LocX;
			  public string LocZ;
			  public AddTeleport(){}
		}

		StoredData storedData;

		void SaveData()
		{    
			Interface.Oxide.DataFileSystem.WriteObject(this.Title, storedData);
		}

		void Loaded()
		{
			storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(this.Title);
			LoadDefaultMessages();
		}
		
		void OnPlayerChat(ClientInfo _cInfo, string message)
		{
			if (!string.IsNullOrEmpty(message) && message.StartsWith("/") && !string.IsNullOrEmpty(_cInfo.playerName) )
			{
				EntityPlayer _player = GameManager.Instance.World.Players.dict[_cInfo.entityId];
				string pp = (int)_player.position.x + "," +(int)_player.position.y + "," + (int)_player.position.z;
				Vector3i posit = Vector3i.Parse(pp);
				bool LandProtectionPlayer = GameManager.Instance.World.CanPlaceBlockAt (posit,GameManager.Instance.GetPersistentPlayerList ().GetPlayerData (_cInfo.playerId));
				string _filter = "[ffffffff][/url][/b][/i][/u][/s][/sub][/sup][ff]";
				if (message.EndsWith(_filter))
				{
					message = message.Remove(message.Length - _filter.Length);
				}
				if (!string.IsNullOrEmpty(_cInfo.playerName))
				{
					if ( message.StartsWith("/") )
					{
						DateTime nowtime = DateTime.Now;
						message = message.Replace("/", "");
						string mesg = message.ToLower();
						if ( mesg == "home" )
						{
							if(LandProtectionPlayer)
							{
								if (storedData.Homes.ContainsKey(_cInfo.playerId))
								{
									PlayerDelay pDelay = storedData.DelayTeleport[_cInfo.playerId];
									var playertime = Convert.ToDateTime(pDelay.Delay);
									TimeSpan rez = nowtime - playertime;
									int showtime = DTHome - Convert.ToInt32(rez.TotalSeconds);
									if (rez.TotalSeconds >= DTHome)
									{
										PlayerHomes home = storedData.Homes[_cInfo.playerId];
										_player.position.x = float.Parse(home.HomeX);
										_player.position.y = float.Parse(home.HomeY);
										_player.position.z = float.Parse(home.HomeZ);
										storedData.DelayTeleport.Remove(_cInfo.playerId);
										storedData.DelayTeleport.Add(_cInfo.playerId, new PlayerDelay());
										storedData.DelayTeleport[_cInfo.playerId].Delay = nowtime.ToString();
										SaveData();
										NetPackageTeleportPlayer pkg = new NetPackageTeleportPlayer(new Vector3(_player.position.x, _player.position.y, _player.position.z));
										_cInfo.SendPackage(pkg);
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("Teleported", _cInfo.playerId)), "Server", false, "", false));
									}
									else
									{
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} {2} [FFFFFF]", GetMessage("Wait", _cInfo.playerId), showtime, GetMessage("Seconds", _cInfo.playerId)), "Server", false, "", false));
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NoHomes", _cInfo.playerId)), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("LandWarning", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg == "sethome" )
						{
							if(LandProtectionPlayer)
							{
								if(!storedData.Homes.ContainsKey(_cInfo.playerId))
								{
									storedData.Homes.Add(_cInfo.playerId, new PlayerHomes());
									storedData.Homes[_cInfo.playerId].Name = _player.EntityName;
									storedData.Homes[_cInfo.playerId].HomeX = _player.position.x.ToString();
									storedData.Homes[_cInfo.playerId].HomeY = _player.position.y.ToString();
									storedData.Homes[_cInfo.playerId].HomeZ = _player.position.z.ToString();
									SaveData();
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("HomeAdded", _cInfo.playerId)), "Server", false, "", false));
									if(!storedData.DelayTeleport.ContainsKey(_cInfo.playerId))
									{
										storedData.DelayTeleport.Add(_cInfo.playerId, new PlayerDelay());
										storedData.DelayTeleport[_cInfo.playerId].Delay = nowtime.ToString();
										SaveData();
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("HomeExists", _cInfo.playerId)), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("LandWarning", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg == "delhome" )
						{
							if(storedData.Homes.ContainsKey(_cInfo.playerId))
							{
								PlayerDelay pDelay = storedData.DelayTeleport[_cInfo.playerId];
								var playertime = Convert.ToDateTime(pDelay.Delay);
								TimeSpan rez = nowtime - playertime;
								int showtime = DDHome - Convert.ToInt32(rez.TotalSeconds);
								if (rez.TotalSeconds >= DDHome)
								{
									storedData.Homes.Remove(_cInfo.playerId);
									storedData.DelayTeleport.Remove(_cInfo.playerId);
									SaveData();
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("HomeRemoved", _cInfo.playerId)), "Server", false, "", false));
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} {2} [FFFFFF]", GetMessage("Wait", _cInfo.playerId), showtime, GetMessage("Seconds", _cInfo.playerId)), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NoHomes", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg == "helptp" )
						{
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("SetHome", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("RemoveHome", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("Home", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("TpInfo", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("TpList", _cInfo.playerId)), "Server", false, "", false));
							_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("TeleFriend", _cInfo.playerId)), "Server", false, "", false));
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("AddTpInfo", _cInfo.playerId)), "Server", false, "", false));
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("DelTpInfo", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg == "listtp" )
						{
							foreach (var TPR in storedData.AddTeleport.Values)
								{
									float _x = float.Parse(TPR.TpX);
									float _z = float.Parse(TPR.TpZ);
									if (_x < 0){_x = _x * -1;}
									if (_z < 0){_z = _z * -1;}
									int x = (int) Math.Round (_x);
									int z = (int) Math.Round (_z);
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {1} {0}   ({3} {4},{2} {5})[FFFFFF]", TPR.Name, GetMessage("ListTp", _cInfo.playerId), x.ToString(), z.ToString(), TPR.LocZ, TPR.LocX), "Server", false, "", false));
								}
						}
						if ( mesg.StartsWith("settp") || mesg.StartsWith("addtp") )
						{
							message = message.Replace("addtp ", "");
							
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								storedData.AddTeleport.Add(message, new AddTeleport());
								storedData.AddTeleport[message].Name = message.ToString();
								storedData.AddTeleport[message].TpX = _player.position.x.ToString();
								storedData.AddTeleport[message].TpY = _player.position.y.ToString();
								storedData.AddTeleport[message].TpZ = _player.position.z.ToString();
								SaveData();
								if (_player.position.x > 0)
								{
									string Loc = "E";
									storedData.AddTeleport[message].LocX = Loc;
									SaveData();
								}
								else
								{
									string Loc = "W";
									storedData.AddTeleport[message].LocX = Loc;
									SaveData();
								}
								if (_player.position.z > 0)
								{
									string Loc = "N";
									storedData.AddTeleport[message].LocZ = Loc;
									SaveData();
								}
								else
								{
									string Loc = "S";
									storedData.AddTeleport[message].LocZ = Loc;
									SaveData();
								}
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} {1} [FFFFFF]", GetMessage("AddTeleport", _cInfo.playerId), message), "Server", false, "", false));
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotAdmin", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg.StartsWith("tp") )
						{
							if(LandProtectionPlayer)
							{
								message = message.Replace("tp ", "");
								if(storedData.AddTeleport.ContainsKey(message))
								{
									if(!storedData.DelayTeleport.ContainsKey(_cInfo.playerId))
									{
										storedData.DelayTeleport.Add(_cInfo.playerId, new PlayerDelay());
										storedData.DelayTeleport[_cInfo.playerId].Delay = nowtime.ToString();
										SaveData();
									}
									PlayerDelay pDelay = storedData.DelayTeleport[_cInfo.playerId];
									var playertime = Convert.ToDateTime(pDelay.Delay);
									TimeSpan rez = nowtime - playertime;
									int showtime = DTPoint - Convert.ToInt32(rez.TotalSeconds);
									if (rez.TotalSeconds >= DTPoint)
									{
										AddTeleport Tp = storedData.AddTeleport[message];
										_player.position.x = float.Parse(Tp.TpX);
										_player.position.y = float.Parse(Tp.TpY);
										_player.position.z = float.Parse(Tp.TpZ);
										storedData.DelayTeleport.Remove(_cInfo.playerId);
										storedData.DelayTeleport.Add(_cInfo.playerId, new PlayerDelay());
										storedData.DelayTeleport[_cInfo.playerId].Delay = nowtime.ToString();
										SaveData();
										NetPackageTeleportPlayer pkg = new NetPackageTeleportPlayer(new Vector3(_player.position.x, _player.position.y, _player.position.z));
										_cInfo.SendPackage(pkg);
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} {1} [FFFFFF]", GetMessage("Teleported", _cInfo.playerId), message), "Server", false, "", false));
									}
									else
									{
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} {2} [FFFFFF]", GetMessage("Wait", _cInfo.playerId), showtime, GetMessage("Seconds", _cInfo.playerId)), "Server", false, "", false));
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} [FFFFFF]", GetMessage("NoTeleport", _cInfo.playerId), message), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("LandWarning", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg.StartsWith("deltp") || mesg.StartsWith("removetp") )
						{
							message = message.Replace("deltp ", "");
							if (GameManager.Instance.adminTools.IsAdmin(_cInfo.playerId))
							{
								storedData.AddTeleport.Remove(message);
								SaveData();
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} {1} [FFFFFF]", GetMessage("DelTeleport", _cInfo.playerId), message), "Server", false, "", false));
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotAdmin", _cInfo.playerId)), "Server", false, "", false));
							}
						}
						if ( mesg.StartsWith("tf") )
						{
							message = message.Replace("tf ", "");
							ClientInfo _targetInfo = ConsoleHelper.ParseParamIdOrName(message);
							PlayerDelay pDelay = storedData.DelayTeleport[_cInfo.playerId];
							var playertime = Convert.ToDateTime(pDelay.Delay);
							TimeSpan rez = nowtime - playertime;
							int showtime = DTPlayer - Convert.ToInt32(rez.TotalSeconds);
							if(_targetInfo != null)
							{
								EntityPlayer _target = GameManager.Instance.World.Players.dict[_targetInfo.entityId];
								string _pp = (int)_target.position.x + "," +(int)_target.position.y + "," + (int)_target.position.z;
								Vector3i _posit = Vector3i.Parse(_pp);
								bool _LandProtectionPlayer = GameManager.Instance.World.CanPlaceBlockAt (_posit,GameManager.Instance.GetPersistentPlayerList ().GetPlayerData (_targetInfo.playerId));
								bool friend = _player.IsFriendsWith(_target);
								if(friend)
								{
									if(LandProtectionPlayer)
									{
										if(_LandProtectionPlayer)
										{
											if(rez.TotalSeconds >= DTPlayer)
											{
												storedData.DelayTeleport.Remove(_cInfo.playerId);
												storedData.DelayTeleport.Add(_cInfo.playerId, new PlayerDelay());
												storedData.DelayTeleport[_cInfo.playerId].Delay = nowtime.ToString();
												SaveData();
												NetPackageTeleportPlayer pkg = new NetPackageTeleportPlayer(new Vector3(_target.position.x, _target.position.y, _target.position.z));
												_cInfo.SendPackage(pkg);
												_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("Teleported", _cInfo.playerId)), "Server", false, "", false));
											}
											else
											{
												_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} {1} {2} [FFFFFF]", GetMessage("Wait", _cInfo.playerId), showtime, GetMessage("Seconds", _cInfo.playerId)), "Server", false, "", false));
											}
										}
										else
										{
											_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("LandWarningFriend", _cInfo.playerId)), "Server", false, "", false));
										}
									}
									else
									{
										_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("LandWarning", _cInfo.playerId)), "Server", false, "", false));
									}
								}
								else
								{
									_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("NotFriend", _cInfo.playerId)), "Server", false, "", false));
								}
							}
							else
							{
								_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF0000] {0} [FFFFFF]", GetMessage("PlayerNotFound", _cInfo.playerId)), "Server", false, "", false));
							}
						}
					}
				}
			}
		}
		
		void OnPlayerConnected(ClientInfo _cInfo)
		{
			DateTime nowtime = DateTime.Now;
			if(!storedData.DelayTeleport.ContainsKey(_cInfo.playerId))
			{
				storedData.DelayTeleport.Add(_cInfo.playerId, new PlayerDelay());
				storedData.DelayTeleport[_cInfo.playerId].Delay = nowtime.ToString();
				SaveData();
			}
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("HelpInfo", _cInfo.playerId)), "Server", false, "", false));
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("SetHome", _cInfo.playerId)), "Server", false, "", false));
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("RemoveHome", _cInfo.playerId)), "Server", false, "", false));
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("Home", _cInfo.playerId)), "Server", false, "", false));
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("TpList", _cInfo.playerId)), "Server", false, "", false));
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("TpInfo", _cInfo.playerId)), "Server", false, "", false));
			_cInfo.SendPackage(new NetPackageGameMessage(EnumGameMessages.Chat, string.Format("[FF8000] {0} [FFFFFF]", GetMessage("TeleFriend", _cInfo.playerId)), "Server", false, "", false));
		}
		
        void OnServerSave() => SaveData();
		void Unload() => SaveData();
    }
}