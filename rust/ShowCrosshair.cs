using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;

namespace Oxide.Plugins
{
    [Info("ShowCrosshair", "Marat", "1.0.7", ResourceId = 2057)]
	[Description("Shows a crosshair on the screen.")]

    class ShowCrosshair : RustPlugin
    { 
	    List<ulong> Cross = new List<ulong>();
		List<ulong> Menu = new List<ulong>();
		bool EnableCross(BasePlayer player) => Cross.Contains(player.userID);
		bool EnableMenu(BasePlayer player) => Menu.Contains(player.userID);

		#region Initialization
		
		private bool configChanged;
	    private const string permShowCrosshair = "showcrosshair.allowed";
		private string background = "http://i.imgur.com/mD8K49U.png";
		private string background2 = "http://i.imgur.com/mYV1bFs.png";

        private void Loaded()
        {
			LoadConfiguration();
            LoadDefaultMessages();
            permission.RegisterPermission(permShowCrosshair, this);
            cmd.AddChatCommand(command, this, "cmdChatCrosshair");
			cmd.AddChatCommand(commandmenu, this, "cmdChatShowMenu");
        }
		
		#endregion
		
		#region Configuration
		
		protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created.");
            Config.Clear();
		}
		
		private bool usePermissions = false;
		private bool ShowOnLogin = false;
		private bool EnableSound = true;
		private bool ShowMessage = true;
		private bool KeyBindSet = true;
		private string SoundOpen = "assets/bundled/prefabs/fx/build/promote_metal.prefab";
		private string SoundDisable = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
		private string SoundSelect = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
		private string SoundToggle = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab";
		private string commandmenu = "showmenu";
		private string command = "crosshair";
		private string keybind = "f5";
		private string colorClose = "0 0 0 0.7";
		private string colorBackground = "0 0 0 0.7";
		private string colorToggle = "0 0 0 0.7";
		private string colorDisable = "0 0 0 0.7";
		private string image1 = "http://i.imgur.com/n1y3P5t.png";
	    private string image2 = "http://i.imgur.com/v6dqmPI.png";
	    private string image3 = "http://i.imgur.com/oTcb8fz.png";
		private string image4 = "http://i.imgur.com/FRpk2mJ.png";
		private string image5 = "http://i.imgur.com/8Jrca6t.png";
	    private string image6 = "http://i.imgur.com/K7yirTy.png";
	    private string image7 = "http://i.imgur.com/beHkRnR.png";
		private string image8 = "http://i.imgur.com/tB088dk.png";
		
		private void LoadConfiguration()
        {
			command = GetConfigValue("Options", "Command", command);
			commandmenu = GetConfigValue("Options", "CommandMenu", commandmenu);
			keybind = GetConfigValue("Options", "KeyBindMenu", keybind);
            ShowMessage = GetConfigValue("Options", "ShowMessage", ShowMessage);
			KeyBindSet = GetConfigValue("Options", "KeyBindSet", KeyBindSet);
			ShowOnLogin = GetConfigValue("Options", "ShowOnLogin", ShowOnLogin);
			EnableSound = GetConfigValue("Options", "EnableSound", EnableSound);
			usePermissions = GetConfigValue("Options", "UsePermissions", usePermissions);
			
			SoundOpen = GetConfigValue("Sound", "SoundOpen", SoundOpen);
			SoundDisable = GetConfigValue("Sound", "SoundDisable", SoundDisable);
			SoundSelect = GetConfigValue("Sound", "SoundSelect", SoundSelect);
			SoundToggle = GetConfigValue("Sound", "SoundToggle", SoundToggle);
			
			colorClose = GetConfigValue("Color", "ColorButtonClose", colorClose);
			colorToggle = GetConfigValue("Color", "ColorButtonToggle", colorToggle);
			colorDisable = GetConfigValue("Color", "ColorButtonDisable", colorDisable);
			colorBackground = GetConfigValue("Color", "ColorBackground", colorBackground);
			
			image1 = GetConfigValue("Image", "Crosshair1", image1);
			image2 = GetConfigValue("Image", "Crosshair2", image2);
			image3 = GetConfigValue("Image", "Crosshair3", image3);
			image4 = GetConfigValue("Image", "Crosshair4", image4);
			image5 = GetConfigValue("Image", "Crosshair5", image5);
			image6 = GetConfigValue("Image", "Crosshair6", image6);
			image7 = GetConfigValue("Image", "Crosshair7", image7);
			image8 = GetConfigValue("Image", "Crosshair8", image8);
			
			if (!configChanged) return;
            PrintWarning("Configuration file updated.");
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
		
		#endregion
		
		#region Localization
		
		private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				["NoPermission"] = "You don't have permission to use this command.",
				["Enabled"] = "You have enabled the crosshair.",
                ["Disabled"] = "You have disabled the crosshair.",
				["crosshair1"] = "You set the crosshair №1.",
				["crosshair2"] = "You set the crosshair №2.",
				["crosshair3"] = "You set the crosshair №3.",
				["crosshair4"] = "You set the crosshair №4.",
				["crosshair5"] = "You set the crosshair №5.",
				["crosshair6"] = "You set the crosshair №6.",
				["crosshair7"] = "You set the crosshair №7.",
				["crosshair8"] = "You set the crosshair №8."
            }, this, "en");
			lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У вас нет разрешения на использование этой команды.",
				["Enabled"] = "Вы включили перекрестие.",
                ["Disabled"] = "Вы отключили перекрестие.",
				["crosshair1"] = "Вы установили перекрестие №1.",
				["crosshair2"] = "Вы установили перекрестие №2.",
				["crosshair3"] = "Вы установили перекрестие №3.",
				["crosshair4"] = "Вы установили перекрестие №4.",
				["crosshair5"] = "Вы установили перекрестие №5.",
				["crosshair6"] = "Вы установили перекрестие №6.",
				["crosshair7"] = "Вы установили перекрестие №7.",
				["crosshair8"] = "Вы установили перекрестие №8."
            }, this, "ru");
        }

        #endregion
		
		#region Commands
		
		/////Crosshair/////
		private void cmdChatCrosshair(BasePlayer player)
		{
			if (usePermissions && !IsAllowed(player.UserIDString, permShowCrosshair))
            {
                Reply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
			if (EnableCross(player))
			{
                DisabledCrosshair(player);
			}
            else
			{
                EnabledCrosshair(player);
			}
        }
		////ShowMenu////
		private void cmdChatShowMenu(BasePlayer player)
		{
			if (usePermissions && !IsAllowed(player.UserIDString, permShowCrosshair))
            {
                Reply(player, Lang("NoPermission", player.UserIDString));
                return;
            }
			if (EnableMenu(player))
			{
                DisabledMenu(player);
			}
            else
			{
                EnabledMenu(player);
				if(EnableSound)Effect.server.Run(SoundOpen, player.transform.position, Vector3.zero, null, false);
			}
        }
		[ConsoleCommand("ShowMenu")]
        private void cmdConsoleShowMenu(ConsoleSystem.Arg arg)
	    {
			var player = arg.Player();
			cmdChatShowMenu(player);
	    }
		////CloseMenu////
		[ConsoleCommand("CloseMenu")]
        void cmdConsoleCloseMenu(ConsoleSystem.Arg arg)
	    {
		    var player = arg.Player();
		    DisabledMenu(player);
	    }
		////Commands////
		[ConsoleCommand("command1")]
        void cmdConsoleCommand1(ConsoleSystem.Arg arg)
        {
			var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair1(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair1", player.UserIDString));
        }
        [ConsoleCommand("command2")]
        void cmdConsoleCommand2(ConsoleSystem.Arg arg)
        {
			var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair2(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair2", player.UserIDString));
        }
        [ConsoleCommand("command3")]
        void cmdConsoleCommand3(ConsoleSystem.Arg arg)
        {
		    var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair3(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair3", player.UserIDString));
        }
        [ConsoleCommand("command4")]
        void cmdConsoleCommand4(ConsoleSystem.Arg arg)
		{
		    var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair4(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair4", player.UserIDString));
	    }
		[ConsoleCommand("command5")]
        void cmdConsoleCommand5(ConsoleSystem.Arg arg)
        {
			var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair5(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair5", player.UserIDString));
        }
        [ConsoleCommand("command6")]
        void cmdConsoleCommand6(ConsoleSystem.Arg arg)
        {
			var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair6(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair6", player.UserIDString));
        }
        [ConsoleCommand("command7")]
        void cmdConsoleCommand7(ConsoleSystem.Arg arg)
        {
		    var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair7(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair7", player.UserIDString));
        }
        [ConsoleCommand("command8")]
        void cmdConsoleCommand8(ConsoleSystem.Arg arg)
		{
		    var player = arg.Player();
			DestroyCrosshair(player);
		    Crosshair8(player);
			if(EnableSound)Effect.server.Run(SoundSelect, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("crosshair8", player.UserIDString));
	    }
		[ConsoleCommand("commandNext")]
        void cmdConsoleCommandNext(ConsoleSystem.Arg arg)
		{
		    var player = arg.Player();
			DestroyGUImenu(player);
			NextMenu(player, null);
			if(EnableSound)Effect.server.Run(SoundToggle, player.transform.position, Vector3.zero, null, false);
	    }
		[ConsoleCommand("commandBack")]
        void cmdConsoleCommandBack(ConsoleSystem.Arg arg)
		{
		    var player = arg.Player();
			DestroyGUImenu(player);
			ShowMenu(player, null);
			if(EnableSound)Effect.server.Run(SoundToggle, player.transform.position, Vector3.zero, null, false);
	    }
		[ConsoleCommand("commandDisable")]
        void cmdConsoleCommandDisable(ConsoleSystem.Arg arg)
		{
		    var player = arg.Player();
			DestroyCrosshair(player);
			if(EnableSound)Effect.server.Run(SoundDisable, player.transform.position, Vector3.zero, null, false);
			if(ShowMessage)Reply(player, Lang("Disabled", player.UserIDString));
	    }
		
		#endregion
		
		#region Hooks
		
		private void OnPlayerInit(BasePlayer player)
        {
			if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
			{
				timer.Once(2, () => OnPlayerInit(player));
				return;
			}
            if (usePermissions && !IsAllowed(player.UserIDString, permShowCrosshair))
            {
                return;
            }
			if (ShowOnLogin)
		    {
				EnabledCrosshair(player);
		    }
			if (KeyBindSet)
            {
                player.Command("bind " + keybind + " \"ShowMenu\"");
            }
	    }
		private void OnPlayerDisconnected(BasePlayer player)
	    {
			if (Menu.Contains(player.userID))
            {
			    if (KeyBindSet)
                {
			        player.SendConsoleCommand("bind " + keybind + " \"\"");
			    }
                Menu.Remove(player.userID);
			    DestroyAll(player);
			    return;
			}
	    }
		private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                Menu.Remove(player.userID);
				DestroyAll(player);
				return;
            }
        }
		private void DestroyAll(BasePlayer player)
	    {
			DestroyGUImenu(player);
		    DestroyCrosshair(player);
	    }
		private void DestroyCrosshair(BasePlayer player)
	    {
		    CuiHelper.DestroyUi(player, "image1");
			CuiHelper.DestroyUi(player, "image2");
			CuiHelper.DestroyUi(player, "image3");
			CuiHelper.DestroyUi(player, "image4");
			CuiHelper.DestroyUi(player, "image5");
			CuiHelper.DestroyUi(player, "image6");
			CuiHelper.DestroyUi(player, "image7");
			CuiHelper.DestroyUi(player, "image8");
	    }
		private void DestroyGUImenu(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, "GUImenu");
			CuiHelper.DestroyUi(player, "GUImenu2");
		}
		private void EnabledCrosshair(BasePlayer player)
        {
            if (!Cross.Contains(player.userID))
            {
                Cross.Add(player.userID);
				DestroyCrosshair(player);
				player.SendConsoleCommand("command1");
            }
        }
        private void DisabledCrosshair(BasePlayer player)
        {
            if (Cross.Contains(player.userID))
            {
                Cross.Remove(player.userID);
			    player.SendConsoleCommand("commandDisable");
            }
        }
		private void EnabledMenu(BasePlayer player)
        {
            if (!Menu.Contains(player.userID))
            {
                Menu.Add(player.userID);
				DestroyGUImenu(player);
		        ShowMenu(player, null);
            }
        }
        private void DisabledMenu(BasePlayer player)
        {
            if (Menu.Contains(player.userID))
            {
                Menu.Remove(player.userID);
			    DestroyGUImenu(player);
            }
        }
		
		#endregion
		
		#region Crosshair
		
	    private void Crosshair1(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
            elements.Add(new CuiElement
            {
                Name = "image1",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image1,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair2(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
			elements.Add(new CuiElement
            {
                Name = "image2",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image2,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair3(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
			elements.Add(new CuiElement
            {
                Name = "image3",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image3,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair4(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
			elements.Add(new CuiElement
            {
                Name = "image4",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image4,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair5(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
            elements.Add(new CuiElement
            {
                Name = "image5",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image5,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair6(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
			elements.Add(new CuiElement
            {
                Name = "image6",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image6,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair7(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
			elements.Add(new CuiElement
            {
                Name = "image7",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image7,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		private void Crosshair8(BasePlayer player)
        {
		    var elements = new CuiElementContainer();
			elements.Add(new CuiElement
            {
                Name = "image8",
				Parent = "Hud.Under",
                Components =
                {
                    new CuiRawImageComponent 
					{ 
						Color = "1 1 1 1", 
						Url = image8,
						Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					},
                    new CuiRectTransformComponent 
					{ 
						AnchorMin = "0.490 0.4812",
                        AnchorMax = "0.509 0.517"
					}
                }
            });
			CuiHelper.AddUi(player, elements);
		}
		
		#endregion
		
		#region GuiMenu
		
		/////////////////Menu1/////////////////////
		
		private void ShowMenu(BasePlayer player, string text)
        {
			var elements = new CuiElementContainer();
            var menu = elements.Add(new CuiPanel
            {
                Image =
                {
					FadeIn = 0.6f,
                    Color = colorBackground
                },
                RectTransform =
                {
                    AnchorMin = "0.2395 0.18",
                    AnchorMax = "0.761 0.4525"
                },
                CursorEnabled = true
            }, "Hud", "GUImenu"); 
			var buttonClose = new CuiButton
            {
                Button =
                {
                    Command = "CloseMenu",
					FadeIn = 0.6f,
                    Color = colorClose
                },
                RectTransform =
                {
                    AnchorMin = "0.402 -0.225",
                    AnchorMax = "0.596 -0.058"
                },
                Text =
                {
                    Text = "<color=#ff0000>C</color><color=#ff1a1a>l</color><color=#ff3333>o</color><color=#ff1a1a>s</color><color=#ff0000>e</color>",
	   /////rus/////Text = "<color=#ff0000>З</color><color=#ff1a1a>а</color><color=#ff3333>к</color><color=#ff4d4d>р</color><color=#ff3333>ы</color><color=#ff1a1a>т</color><color=#ff0000>ь</color>",
                    FontSize = 18,
					FadeIn = 0.6f,
                    Align = TextAnchor.MiddleCenter
                }
            };
			
			/////////////button///////////////////
			
            elements.Add(buttonClose, menu);
            {
				//button1
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command1",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.0445 0.11",
                        AnchorMax = $"0.236 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",						
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//button2
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command2",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.282 0.11",
                        AnchorMax = $"0.476 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",						
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//button3
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command3",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.523 0.11",
                        AnchorMax = $"0.715 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//button4
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command4",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.762 0.11",
                        AnchorMax = $"0.954 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//buttonDisable
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"commandDisable",
						FadeIn = 0.6f,
                        Color = colorDisable
                    },
                    RectTransform =
                    {
                        AnchorMin = "-0.003 -0.226",
                        AnchorMax = "0.192 -0.060"
                    },
                    Text =
                    {
                        Text = "<color=#fbff00>D</color><color=#fbff1a>i</color><color=#fcff33>s</color><color=#fcff4d>a</color><color=#fcff33>b</color><color=#fbff1a>l</color><color=#fbff00>e</color>",
		   /////rus/////Text = "<color=#e2e600>О</color><color=#fbff00>т</color><color=#fbff1a>к</color><color=#fcff33>л</color><color=#fcff4d>ю</color><color=#fcff33>ч</color><color=#fbff1a>и</color><color=#fbff00>т</color><color=#e2e600>ь</color>",
                        FontSize = 18,
						FadeIn = 0.6f,
                        Align = TextAnchor.MiddleCenter
                    }
                }, menu);
				//buttonNext
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"commandNext",
						FadeIn = 0.6f,
                        Color = colorToggle
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.805 -0.226",
                        AnchorMax = "1 -0.060"
                    },
                    Text =
                    {
                        Text = "<color=#0055ff>N</color><color=#1a66ff>e</color><color=#1a66ff>x</color><color=#0055ff>t</color>",
		   /////rus/////Text = "<color=#0055ff>Д</color><color=#1a66ff>а</color><color=#3377ff>л</color><color=#1a66ff>е</color><color=#0055ff>е</color>",
                        FontSize = 18,
						FadeIn = 0.6f,
                        Align = TextAnchor.MiddleCenter
                    }
                }, menu);
				
				////////////////background///////////////
				
				//background1
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.2555 0.195",
                            AnchorMax = $"0.3705 0.44"
				        }
                    }
                });
				//background2
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.3805 0.195",
                            AnchorMax = $"0.4955 0.44"
				        }
                    }
                });
				//background3
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.5055 0.195",
                            AnchorMax = $"0.6205 0.44"
				        }
                    }
                });
				//background4
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.6305 0.195",
                            AnchorMax = $"0.7455 0.44"
				        }
                    }
                });
				
				////////////////image////////////////
				
				//image1
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image1,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.294 0.295",
                            AnchorMax = $"0.335 0.365"
				        }
                    }
                });
				//image2
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image2,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.42 0.295",
                            AnchorMax = $"0.46 0.365"
				        }
                    }
                });
				//image3
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image3,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.545 0.295",
                            AnchorMax = $"0.585 0.365"
				        }
                    }
                });
				//image4
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image4,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.666 0.295",
                            AnchorMax = $"0.710 0.365"
				        }
                    }
                });
				
				////////////MainBackground////////////////
				
				elements.Add(new CuiElement
                {
                    Name = menu,
					FadeOut = 0.3f,
				    Parent = "Hud.Under",
                    Components =
                    {
                        new CuiRawImageComponent 
					    { 
						    Color = "1 1 1 1", 
							FadeIn = 0.3f,
						    Url = background2,
						    Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					    },
                        new CuiRectTransformComponent 
					    { 
						    AnchorMin = "0.2365 0.110",
                            AnchorMax = "0.7635 0.468"
					    }
                    }
                });
            }
            CuiHelper.AddUi(player, elements);
        }
		
		/////////////////Menu2/////////////////////
		
		private void NextMenu(BasePlayer player, string text)
        {
			var elements = new CuiElementContainer();
            var menu = elements.Add(new CuiPanel
            {
                Image =
                {
					FadeIn = 0.6f,
                    Color = colorBackground
                },
                RectTransform =
                {
                    AnchorMin = "0.2395 0.18",
                    AnchorMax = "0.761 0.4525"
                },
                CursorEnabled = true
            }, "Hud", "GUImenu2"); 
			var buttonClose = new CuiButton
            {
                Button =
                {
                    Command = "CloseMenu",
					FadeIn = 0.6f,
                    Color = colorClose
                },
                RectTransform =
                {
                    AnchorMin = "0.402 -0.225",
                    AnchorMax = "0.596 -0.058"
                },
                Text =
                {
                    Text = "<color=#ff0000>C</color><color=#ff1a1a>l</color><color=#ff3333>o</color><color=#ff1a1a>s</color><color=#ff0000>e</color>",
	   /////rus/////Text = "<color=#ff0000>З</color><color=#ff1a1a>а</color><color=#ff3333>к</color><color=#ff4d4d>р</color><color=#ff3333>ы</color><color=#ff1a1a>т</color><color=#ff0000>ь</color>",
                    FontSize = 18,
					FadeIn = 0.6f,
                    Align = TextAnchor.MiddleCenter
                }
            };
			
			/////////////button///////////////////
			
            elements.Add(buttonClose, menu);
            {
				//button5
                elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command5",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.0445 0.11",
                        AnchorMax = $"0.236 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",						
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//button6
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command6",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.282 0.11",
                        AnchorMax = $"0.476 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",						
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//button7
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command7",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.523 0.11",
                        AnchorMax = $"0.715 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//button8
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"command8",
						FadeIn = 0.6f,
                        Color = "0 0 0 0"
                    },
                    RectTransform =
                    {
                        AnchorMin = $"0.762 0.11",
                        AnchorMax = $"0.954 0.85"
                    },
                    Text =
                    {
                        Text = "<color=#46d100>S</color><color=#52f500>e</color><color=#66ff1a>l</color><color=#52f500>e</color><color=#46d100>c</color><color=#3aad00>t</color>",
		   /////rus/////Text = "<color=#3aad00>В</color><color=#46d100>ы</color><color=#52f500>б</color><color=#66ff1a>р</color><color=#52f500>а</color><color=#46d100>т</color><color=#3aad00>ь</color>",
                        FontSize = 20,
						FadeIn = 0.6f,
                        Align = TextAnchor.LowerCenter
                    }
                }, menu);
				//buttonDisable
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"commandDisable",
						FadeIn = 0.6f,
                        Color = colorDisable
                    },
                    RectTransform =
                    {
                        AnchorMin = "-0.003 -0.226",
                        AnchorMax = "0.192 -0.060"
                    },
                    Text =
                    {
                        Text = "<color=#fbff00>D</color><color=#fbff1a>i</color><color=#fcff33>s</color><color=#fcff4d>a</color><color=#fcff33>b</color><color=#fbff1a>l</color><color=#fbff00>e</color>",
		   /////rus/////Text = "<color=#e2e600>О</color><color=#fbff00>т</color><color=#fbff1a>к</color><color=#fcff33>л</color><color=#fcff4d>ю</color><color=#fcff33>ч</color><color=#fbff1a>и</color><color=#fbff00>т</color><color=#e2e600>ь</color>",
                        FontSize = 18,
						FadeIn = 0.6f,
                        Align = TextAnchor.MiddleCenter
                    }
                }, menu);
				//buttonBack
				elements.Add(new CuiButton
                {
                    Button =
                    {
                        Command = $"commandBack",
						FadeIn = 0.6f,
                        Color = colorToggle
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.805 -0.226",
                        AnchorMax = "1 -0.060"
                    },
                    Text =
                    {
                        Text = "<color=#0055ff>B</color><color=#1a66ff>a</color><color=#1a66ff>c</color><color=#0055ff>k</color>",
		   /////rus/////Text = "<color=#0055ff>Н</color><color=#1a66ff>а</color><color=#3377ff>з</color><color=#1a66ff>а</color><color=#0055ff>д</color>",
                        FontSize = 18,
						FadeIn = 0.6f,
                        Align = TextAnchor.MiddleCenter
                    }
                }, menu);
				
				////////////////background///////////////
				
				//background1
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.2555 0.195",
                            AnchorMax = $"0.3705 0.44"
				        }
                    }
                });
				//background2
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.3805 0.195",
                            AnchorMax = $"0.4955 0.44"
				        }
                    }
                });
				//background3
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.5055 0.195",
                            AnchorMax = $"0.6205 0.44"
				        }
                    }
                });
				//background4
				elements.Add(new CuiElement
                {
                    Name = menu,
					Parent = "Hud.Under",
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1",
							FadeIn = 0.3f,
							Url = background,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.6305 0.195",
                            AnchorMax = $"0.7455 0.44"
				        }
                    }
                });
				
				////////////////image////////////////
				
				//image5
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image5,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.294 0.295",
                            AnchorMax = $"0.335 0.365"
				        }
                    }
                });
				//image6
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image6,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.42 0.295",
                            AnchorMax = $"0.46 0.365"
				        }
                    }
                });
				//image7
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image7,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.545 0.295",
                            AnchorMax = $"0.585 0.365"
				        }
                    }
                });
				//image8
				elements.Add(new CuiElement
                {
                    Name = menu,
			        Components =
                    {
                        new CuiRawImageComponent
				        { 
					        Color = "1 1 1 1", 
					        Url = image8,
					        Sprite = "assets/content/textures/generic/fulltransparent.tga" 
				        },
                        new CuiRectTransformComponent 
				        { 
					        AnchorMin = $"0.666 0.295",
                            AnchorMax = $"0.710 0.365"
				        }
                    }
                });
				
				////////////MainBackground////////////////
				
				elements.Add(new CuiElement
                {
                    Name = menu,
					FadeOut = 0.3f,
				    Parent = "Hud.Under",
                    Components =
                    {
                        new CuiRawImageComponent 
					    { 
						    Color = "1 1 1 1", 
							FadeIn = 0.3f,
						    Url = background2,
						    Sprite = "assets/content/textures/generic/fulltransparent.tga" 
					    },
                        new CuiRectTransformComponent 
					    { 
						    AnchorMin = "0.2365 0.110",
                            AnchorMax = "0.7635 0.468"
					    }
                    }
                });
            }
            CuiHelper.AddUi(player, elements);
        }
		
		#endregion
		 
		#region Helpers
		
        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        void Reply(BasePlayer player, string message, string args = null) => PrintToChat(player, $"{message}", args);
		
		bool IsAllowed(string id, string perm) => permission.UserHasPermission(id, perm);
		
        #endregion
    }
}