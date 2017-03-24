
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("VendingManager", "ignignokt84", "0.1.7", ResourceId = 2331)]
	[Description("Improved vending machine control")]
	class VendingManager : RustPlugin
	{
		#region Variables
		
		[PluginReference]
		Plugin Economics;

		// usage permission
		private const string PermCanUse = "vendingmanager.canuse";
		private const string PermCanEject = "vendingmanager.caneject";

		// valid commands
		private enum Command {
			add, clear, eject, info, list, load, reset, save, set, unset
		};

		// configuration options
		private enum Option {
			destroyOnUnload,	// destroy locks on unload
			ejectLocks,         // eject locks on unload/reload
			health,             // health
			lockable,           // allow attaching locks
			lockFailureMessage, // display message on lock attach failure
			saveLocks,          // save locks on unload
			setHealth,			// enable setting health
			noBroadcast,		// blocks broadcasting
			restricted,			// restrict panel access to owners
			useEconomics,		// use economics
			transactionTimeout,	// timeout to end transactions
			logTransSuccess,	// enable logging transaction success
			logTransFailure,	// enable logging transaction failures
			transMessages		// enable transaction success messages
		}

		// default configuration values
		object[] defaults = new object[] { false, false, defaultHealth, true, false, true, true, false, false, false, 300f, false, false, true };

		// container for config/data
		VendingData data = new VendingData();
		Dictionary<uint, VendingMachineInfo> vms = new Dictionary<uint, VendingMachineInfo>();
		Dictionary<string, LockInfo> oldlocks = new Dictionary<string, LockInfo>();
		Dictionary<uint, LockInfo> locks = new Dictionary<uint, LockInfo>();
		const float defaultHealth = 500f;
		ProtectionProperties defaultProtection;
		ProtectionProperties customProtection;

		const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
		const string KeyLockPrefab = "assets/prefabs/locks/keylock/lock.key.prefab";

		FieldInfo sellOrderIdField = typeof(VendingMachine).GetField("vend_sellOrderID", BindingFlags.NonPublic | BindingFlags.Instance);
		FieldInfo numTransactionsField = typeof(VendingMachine).GetField("vend_numberOfTransactions", BindingFlags.NonPublic | BindingFlags.Instance);
		FieldInfo transactionActiveField = typeof(VendingMachine).GetField("transactionActive", BindingFlags.NonPublic | BindingFlags.Instance);

		Dictionary<ulong, Timer> econTransactionTimers = new Dictionary<ulong, Timer>();
		Dictionary<ulong, Timer> timeoutTimers = new Dictionary<ulong, Timer>();

		bool isShuttingDown = false;
		bool useEconomics = false;

		int currencyIndex = 24;

		#endregion

		#region Lang

		// load default messages to Lang
		void LoadDefaultMessages()
		{
			var messages = new Dictionary<string, string>
			{
				{"Prefix", "<color=orange>[ VendingManager ]</color> "},
				{"ClearSuccess", "Successfully cleared Vending Machine sell orders"},
				{"SaveSuccess", "Saved Vending Machine sell orders to \"{0}\""},
				{"LoadSuccess", "Loaded Vending Machine sell orders from \"{0}\""},
				{"ResetSuccess", "Successfully cleared and reset VendingManager configuration to defaults"},
				{"ConfirmReset", "To reset the VendingManager configuration and remove all saved templates, type: /vm reset confirm"},
				{"VMNotFound", "No Vending Machine found"},
				{"EmptyTemplate", "Template \"{0}\" is empty"},
				{"EmptyVM", "Vending Machine has no sell orders defined"},
				{"TemplateNotFound", "No sell order template found with name \"{0}\""},
				{"TemplateExists", "Template with name \"{0}\" already exists, add \"overwrite\" parameter to command to overwrite template"},
				{"InvalidCommand", "Invalid command: {0}"},
				{"InvalidParameter", "Invalid parameter"},
				{"NotAuthorized", "You cannot add a lock to that Vending Machine"},
				{"CommandList", "<color=cyan>Valid Commands:</color>" + Environment.NewLine + "{0}"},
				{"TemplateList", "<color=cyan>Templates:</color>" + Environment.NewLine + "{0}"},
				{"Ejected", "Ejected {0} locks from Vending Machines"},
				{"NoBroadcast", "Broadcasting is not allowed" },
				{"Restricted", "You do not have access to administrate that VendingMachine" },
				{"Information", "Vending Machine ID: <color=cyan>{0}</color>" + Environment.NewLine + "Has configuration? <color=cyan>{1}</color>" + Environment.NewLine + "Flags: <color=cyan>{2}</color>" },
				{"EconNotEnoughMoney", "Transaction Cancelled (Economics): Not enough money" },
				{"EconNotEnoughMoneyOwner", "Transaction Cancelled (Economics): Buyer doesn't have enough money" },
				{"EconTransferFailed", "Transaction Cancelled (Economics): Money transfer failed" },
				{"EconPurchaseSuccess", "Successfully purchased {0} {1} for {2:C}; Remaining balance: {3:C}" },
				{"EconSellSuccess", "Successfully sold {0} {1} for {2:C}; New balance: {3:C}" },
				{"SetSuccess", "Successfully set flag <color=cyan>{0}</color>" },
				{"UnsetSuccess", "Successfully removed flag <color=cyan>{0}</color>" },

				{"CmdBase", "vm"}
			};
			lang.RegisterMessages(messages, this);
		}

		// get message from Lang
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

		#endregion

		#region Loading/Unloading

		// on load
		void Loaded()
		{
			LoadDefaultMessages();
			cmd.AddChatCommand(GetMessage("CmdBase"), this, "CommandDelegator");
			permission.RegisterPermission(PermCanUse, this);
			permission.RegisterPermission(PermCanEject, this);
			LoadData();
		}

		// on unload, reset all vending machines
		void Unload()
		{
			if (ConfigValue<bool>(Option.saveLocks) || isShuttingDown)
				SaveVMsAndLocks();
			SetAll(false);
			if (ConfigValue<bool>(Option.destroyOnUnload) || isShuttingDown)
				DestroyLocks();

			foreach (Timer t in econTransactionTimers.Values)
				t?.Destroy();
			foreach (Timer t in timeoutTimers.Values)
				t?.Destroy();
		}

		// server initialized
		void OnServerInitialized()
		{
			SetAll(ConfigValue<bool>(Option.lockable), ConfigValue<float>(Option.health));
			if (ConfigValue<bool>(Option.saveLocks))
				LoadLocks();
			CheckEconomics();
		}

		void OnPluginLoaded(Plugin plugin)
		{
			if (plugin.Name == "Economics")
				Economics = plugin;
			CheckEconomics();
		}

		void OnPluginUnloaded(Plugin plugin)
		{
			if (plugin.Name == "Economics")
				Economics = null;
			CheckEconomics();
		}
		
		void CheckEconomics()
		{
			bool prev = useEconomics;
			useEconomics = ConfigValue<bool>(Option.useEconomics) && Economics != null;
			if (prev != useEconomics)
				Puts("Economics " + (useEconomics ? "detected - money purchases enabled" : "not detected - money purchases disabled"));
		}

		// save/destroy locks on server shutdown to avoid NULL in saveList
		void OnServerShutdown()
		{
			isShuttingDown = true;
		}

		// save locks when server saves
		void OnServerSave()
		{
			// delayed save to fight the lag monster
			timer.In(5f, () => SaveVMsAndLocks());
		}

		#endregion

		#region Configuration

		// load default config
		bool LoadDefaultConfig()
		{
			data = new VendingData();
			CheckConfig();
			data.templates = new Dictionary<string, SellOrderTemplate>();
			return true;
		}

		void LoadData()
		{
			bool dirty = false;
			try {
				data = Config.ReadObject<VendingData>();
			} catch (Exception) { }
			dirty = CheckConfig();
			if (data.templates == null)
				dirty |= LoadDefaultConfig();
			if (dirty)
				SaveData();
			vms = Interface.GetMod()?.DataFileSystem?.ReadObject<Dictionary<uint, VendingMachineInfo>>("VendingManagerVMs");
			try {
				oldlocks = Interface.GetMod()?.DataFileSystem?.ReadObject<Dictionary<string, LockInfo>>("VendingManagerLocks");
			} catch (Exception) { }
			if (oldlocks != null && oldlocks.Count > 0)
				ConvertOldLocks();
			else
				locks = Interface.GetMod()?.DataFileSystem?.ReadObject<Dictionary<uint, LockInfo>>("VendingManagerLocks");
		}

		// write data container to config
		void SaveData()
		{
			Config.WriteObject(data);
		}

		void ConvertOldLocks()
		{
			foreach(VendingMachine vm in GameObject.FindObjectsOfType<VendingMachine>())
			{
				LockInfo li;
				if (oldlocks.TryGetValue(GetIDFromPosition(vm.transform.position), out li))
				{
					li.vmId = vm.net.ID;
					locks[vm.net.ID] = li;
				}
			}
			SaveVMsAndLocks();
		}

		void SaveVendingMachineData()
		{
			Interface.GetMod().DataFileSystem.WriteObject("VendingManagerVMs", vms);
		}

		// save locks data to file
		void SaveLocksData()
		{
			Interface.GetMod().DataFileSystem.WriteObject("VendingManagerLocks", locks);
		}

		// get value from config (handles type conversion)
		T GetConfig<T>(string group, string name, T value)
		{
			if (Config[group, name] == null)
			{
				Config[group, name] = value;
				SaveConfig();
			}
			return (T)Convert.ChangeType(Config[group, name], typeof(T));
		}

		// validate configuration
		bool CheckConfig()
		{
			bool dirty = false;
			foreach (Option option in Enum.GetValues(typeof(Option)))
				if (!data.config.ContainsKey(option))
				{
					data.config[option] = defaults[(int)option];
					dirty = true;
				}
			return dirty;
		}

		#endregion

		#region Hooks

		// set newly spawned vending machines to the value of lockable
		void OnEntitySpawned(BaseNetworkable entity)
		{
			if (entity.GetType() == typeof(VendingMachine))
				Set(entity as VendingMachine, ConfigValue<bool>(Option.lockable), ConfigValue<float>(Option.health));
		}

		// block unauthorized lock deployment onto vending machines
		// only allow attachment from the rear, except if player is
		// the owner of the vending machine
		void OnItemDeployed(Deployer deployer, BaseEntity entity)
		{
			if (!ConfigValue<bool>(Option.lockable)) return;
			if (deployer == null || entity == null) return;
			BasePlayer player = deployer.GetOwnerPlayer();
			VendingMachine vm = entity as VendingMachine;
			if (vm == null || player == null) return;
			if (deployer.GetDeployable().slot == BaseEntity.Slot.Lock && !(vm.CanPlayerAdmin(player) || player.userID == vm.OwnerID))
			{
				BaseLock lockEntity = vm.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
				if (lockEntity == null) return;
				deployer.GetItem().amount++;
				lockEntity.Kill();
				if (ConfigValue<bool>(Option.lockFailureMessage))
					SendMessage(player, "NotAuthorized");
			}
		}

		// handle blocking broadcasting
		void OnToggleVendingBroadcast(VendingMachine vm, BasePlayer player)
		{
			if(ConfigValue<bool>(Option.noBroadcast))
			{
				vm.SetFlag(BaseEntity.Flags.Reserved4, false, false);
				SendMessage(player, "NoBroadcast");
			}
		}

		object OnVendingTransaction(VendingMachine vm, BasePlayer player)
		{
			VendingMachineInfo i;
			vms.TryGetValue(vm.net.ID, out i);
			bool bottomless = i == null ? false : (i.flags & VendingMachineInfo.VMFlags.Bottomless) == VendingMachineInfo.VMFlags.Bottomless;
			
			bool log = ConfigValue<bool>(Option.logTransSuccess) || ConfigValue<bool>(Option.logTransFailure) || (i != null && (i.flags & VendingMachineInfo.VMFlags.LogTransactions) == VendingMachineInfo.VMFlags.LogTransactions);
			bool force = i != null && (i.flags & VendingMachineInfo.VMFlags.LogTransactions) == VendingMachineInfo.VMFlags.LogTransactions;
			int sellOrderId = (int)sellOrderIdField.GetValue(vm);
			int numTransactions = (int)numTransactionsField.GetValue(vm);
			ProtoBuf.VendingMachine.SellOrder sellOrder = vm.sellOrders.sellOrders[sellOrderId];
			
			bool isEconomicsSellOrder = useEconomics && sellOrder.currencyID == 93832698; // blood bag
			bool isEconomicsBuyOrder = useEconomics && sellOrder.itemToSellID == 93832698; // blood bag

			LogEntry logEntry = new LogEntry();
			if (log)
			{
				logEntry.id = vm.net.ID;
				logEntry.playerID = player.userID;
				logEntry.playerName = player.displayName;
			}
			
			List<Item> items = vm.inventory.FindItemsByItemID(sellOrder.itemToSellID);
			if (items == null || items.Count == 0)
			{
				return false;
			}
			int numberOfTransactions = Mathf.Clamp(numTransactions, 1, (!items[0].hasCondition ? 1000000 : 1));
			int sellCount = sellOrder.itemToSellAmount * numberOfTransactions;
			int buyCount = sellOrder.currencyAmountPerItem * numberOfTransactions;

			if (sellCount > items.Sum(x => x.amount))
				return false;
			
			int cost = 0;
			if (!isEconomicsSellOrder)
			{
				int num2 = sellOrder.currencyAmountPerItem * numberOfTransactions;

				if (log) logEntry.cost = num2 + " " + ItemManager.FindItemDefinition(sellOrder.currencyID).displayName.translated;

				List<Item> items1 = player.inventory.FindItemIDs(sellOrder.currencyID);
				if (items1.Count == 0)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.NoItems;
						LogTransaction(logEntry, force);
					}
					return false;
				}

				int num1 = items1.Sum(x => x.amount);
				if (num1 < num2)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.NoItems;
						LogTransaction(logEntry, force);
					}
					return false;
				}

				transactionActiveField.SetValue(vm, true);
				int num3 = 0;
				Item item;
				foreach (Item item2 in items1)
				{
					int num4 = Mathf.Min(num2 - num3, item2.amount);
					item = (item2.amount > num4 ? item2.SplitItem(num4) : item2);
					if(bottomless)
						item.Remove();
					else
						if (!item.MoveToContainer(vm.inventory, -1, true))
						{
							item.Drop(vm.inventory.dropPosition, Vector3.zero, new Quaternion());
						}
					num3 = num3 + num4;
					if (num3 < num2)
						continue;
					break;
				}
			}
			else
			{
				cost = sellOrder.currencyAmountPerItem * numberOfTransactions;
				if (log)
				{
					logEntry.isBuyOrder = true;
					logEntry.cost = string.Format("{0:C}", cost);
				}
				double money = (double) Economics.CallHook("GetPlayerMoney", player.userID);
				if (money < 1.0)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.NoMoney;
						LogTransaction(logEntry, force);
					}
					return false;
				}

				if(Mathf.FloorToInt((float)money) < cost)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.NoMoney;
						LogTransaction(logEntry, force);
					}
					SendMessage(player, "EconNotEnoughMoney");
					return false;
				}
				
				transactionActiveField.SetValue(vm, true);
				bool success = false;
				if (bottomless)
					success = (bool)Economics.CallHook("Withdraw", player.userID, (double)cost);
				else
					success = (bool)Economics.CallHook("Transfer", player.userID, vm.OwnerID, (double)cost);

				if(!success)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.Unknown;
						LogTransaction(logEntry, force);
					}
					SendMessage(player, "EconTransferFailed");
					transactionActiveField.SetValue(vm, false);
					return false;
				}
			}
			int amount = 0;
			if (isEconomicsBuyOrder)
			{
				amount = sellOrder.itemToSellAmount * numberOfTransactions;
				if (log)
				{
					logEntry.isBuyOrder = false;
					logEntry.cost = string.Format("{0:C}", amount);
					logEntry.bought = sellOrder.currencyAmountPerItem + " " + ItemManager.FindItemDefinition(sellOrder.currencyID).displayName.translated;
				}
				double money = (double)Economics.CallHook("GetPlayerMoney", vm.OwnerID);
				if (money < 1.0)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.NoMoney;
						LogTransaction(logEntry, force);
					}
					return false;
				}

				if (Mathf.FloorToInt((float)money) < amount)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.NoMoney;
						LogTransaction(logEntry, force);
					}
					SendMessage(player, "EconNotEnoughMoneyOwner");
					return false;
				}
				
				transactionActiveField.SetValue(vm, true);
				bool success = false;
				if (bottomless)
				{
					Economics.CallHook("Deposit", player.userID, (double)amount);
					success = true;
				}
				else
					success = (bool)Economics.CallHook("Transfer", vm.OwnerID, player.userID, (double)amount);
				
				if (!success)
				{
					if (log)
					{
						logEntry.success = false;
						logEntry.reason = LogEntry.FailureReason.Unknown;
						LogTransaction(logEntry, force);
					}
					SendMessage(player, "EconTransferFailed");
					transactionActiveField.SetValue(vm, false);
					return false;
				}
			}
			else
			{
				if(log) logEntry.bought = sellOrder.itemToSellAmount + " " + ItemManager.FindItemDefinition(sellOrder.itemToSellID).displayName.translated;
				if (!bottomless)
				{
					int num5 = 0;
					Item item1 = null;
					foreach (Item item3 in items)
					{
						item1 = (item3.amount > sellCount ? item3.SplitItem(sellCount) : item3);
						num5 = num5 + item1.amount;
						player.GiveItem(item1, BaseEntity.GiveItemReason.PickedUp);
						if (num5 < sellCount)
							continue;
						break;
					}
				}
				else
				{
					Item item = ItemManager.CreateByItemID(sellOrder.itemToSellID, sellCount);
					player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
				}
			}

			vm.UpdateEmptyFlag();
			transactionActiveField.SetValue(vm, false);

			if (ConfigValue<bool>(Option.transMessages) && isEconomicsSellOrder && cost > 0 )
			{
				double remaining = (double)Economics.CallHook("GetPlayerMoney", player.userID);
				SendMessage(player, "EconPurchaseSuccess", new object[] { sellCount, ItemManager.FindItemDefinition(sellOrder.itemToSellID).displayName.translated, cost, remaining });
			}
			else if(ConfigValue<bool>(Option.transMessages) && isEconomicsBuyOrder && amount > 0)
			{
				double balance = (double)Economics.CallHook("GetPlayerMoney", player.userID);
				SendMessage(player, "EconSellSuccess", new object[] { buyCount, ItemManager.FindItemDefinition(sellOrder.currencyID).displayName.translated, amount, balance });
			}
			if(log)
			{
				logEntry.success = true;
				LogTransaction(logEntry, force);
			}

			return true;
		}

		// override administration if restricted access on
		object CanAdministerVending(VendingMachine vm, BasePlayer player)
		{
			bool restricted = ConfigValue<bool>(Option.restricted);
			if(!restricted)
			{
				VendingMachineInfo i;
				if (vms.TryGetValue(vm.net.ID, out i))
					restricted = (i.flags & VendingMachineInfo.VMFlags.Restricted) == VendingMachineInfo.VMFlags.Restricted;
			}
			if (restricted && vm.OwnerID != player.userID && !player.IsAdmin)
			{
				SendMessage(player, "Restricted");
				return false;
			}
			return null;
		}

		// block damage for Immortal vending machines
		void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
		{
			if (entity == null || hitinfo == null) return;
			if(entity is VendingMachine)
			{
				VendingMachineInfo i;
				if(vms.TryGetValue(entity.net.ID, out i))
				{
					if ((i.flags & VendingMachineInfo.VMFlags.Immortal) == VendingMachineInfo.VMFlags.Immortal)
						hitinfo.damageTypes = new DamageTypeList();
				}
			}
		}

		// hack to show vending buttons
		void OnOpenVendingShop(VendingMachine vm, BasePlayer player)
		{
			if (!useEconomics) return;
			if (vm.sellOrders.sellOrders.Count == 0) return;
			
			bool hasEconomicsSellOrder = false;
			bool hasEconomicsBuyOrder = true;
			// create and add items to player inventory to prevent "Can't Afford" button
			foreach (ProtoBuf.VendingMachine.SellOrder so in vm.sellOrders.sellOrders)
			{
				if (so.currencyID == 93832698)
					hasEconomicsSellOrder = true;
				else if (so.itemToSellID == 93832698)
					hasEconomicsBuyOrder = true;
			}
			if (!hasEconomicsSellOrder && !hasEconomicsBuyOrder) return;

			int playerMoney = 0;
			if (hasEconomicsBuyOrder)
			{
				vm.inventory.capacity = currencyIndex + 1;
				playerMoney = Mathf.FloorToInt((float)(double)Economics.CallHook("GetPlayerMoney", vm.OwnerID));
				Item money = ItemManager.CreateByItemID(93832698, playerMoney, 0);
				money.MoveToContainer(vm.inventory, currencyIndex, true);

				int lastMoney = playerMoney;
				econTransactionTimers[player.userID] = timer.Every(0.5f, () => {
					int m = Mathf.FloorToInt((float)(double)Economics.CallHook("GetPlayerMoney", vm.OwnerID));
					if (lastMoney != m)
					{
						lastMoney = m;
						Item item = vm.inventory.GetSlot(currencyIndex);
						if (item != null)
						{
							if (lastMoney == 0)
								item.Remove();
							else
								item.amount = lastMoney;
							vm.RefreshSellOrderStockLevel();
						}
					}
				});
			}
			if(hasEconomicsSellOrder)
			{
				player.inventory.containerMain.capacity = currencyIndex + 1;
				playerMoney = Mathf.FloorToInt((float)(double)Economics.CallHook("GetPlayerMoney", player.userID));
				Item money = ItemManager.CreateByItemID(93832698, playerMoney, 0);
				money.MoveToContainer(player.inventory.containerMain, currencyIndex, true);

				int lastMoney = playerMoney;
				econTransactionTimers[player.userID] = timer.Every(0.5f, () => {
					int m = Mathf.FloorToInt((float)(double)Economics.CallHook("GetPlayerMoney", player.userID));
					if (lastMoney != m)
					{
						lastMoney = m;
						Item item = player.inventory.containerMain.GetSlot(currencyIndex);
						if (item != null)
						{
							if (lastMoney == 0)
								item.Remove();
							else
								item.amount = lastMoney;
							player.inventory.SendSnapshot();
						}
					}
				});
			}
			if (ConfigValue<float>(Option.transactionTimeout) > 0f)
				timeoutTimers[player.userID] = timer.Once(ConfigValue<float>(Option.transactionTimeout), () => player.EndLooting());
		}

		void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
		{
			if (!useEconomics || entity == null || !(entity is VendingMachine)) return;
			if(econTransactionTimers.ContainsKey(player.userID))
				econTransactionTimers[player.userID]?.Destroy();
			if (timeoutTimers.ContainsKey(player.userID))
				timeoutTimers[player.userID]?.Destroy();

			int i = player.inventory.containerMain.capacity;
			while (i >= currencyIndex)
				player.inventory.containerMain.GetSlot(i--)?.Remove();
			Item b = player.inventory.containerMain.FindItemByItemID(93832698);
			if (b != null) b.Remove();
			player.inventory.containerMain.capacity = currencyIndex;

			VendingMachine vm = entity as VendingMachine;
			int j = vm.inventory.capacity;
			while (j >= currencyIndex)
				vm.inventory.GetSlot(j--)?.Remove();
			Item c = vm.inventory.FindItemByItemID(93832698);
			if (c != null) c.Remove();
			vm.inventory.capacity = currencyIndex;
		}

		#endregion

		#region Command Handling

		// command delegator
		void CommandDelegator(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, PermCanUse)) return;
			string message = "InvalidCommand";
			// assume args[0] is the command (beyond /vm)
			if (args != null && args.Length > 0)
				command = args[0];
			// shift arguments
			if (args != null)
			{
				if (args.Length > 1)
					args = args.Skip(1).ToArray();
				else
					args = new string[] { };
			}
			object[] opts = new object[] { command };
			if (Enum.IsDefined(typeof(Command), command))
			{
				switch ((Command)Enum.Parse(typeof(Command), command))
				{
					case Command.add:
						HandleLoad(player, args, false, out message, out opts);
						break;
					case Command.clear:
						HandleClear(player, out message);
						break;
					case Command.eject:
						if (hasPermission(player, PermCanEject))
							EjectAll(out message, out opts);
						break;
					case Command.info:
						HandleInfo(player, out message, out opts);
						break;
					case Command.list:
						HandleList(out message, out opts);
						break;
					case Command.load:
						HandleLoad(player, args, true, out message, out opts);
						break;
					case Command.reset:
						HandleReset(args, out message);
						break;
					case Command.save:
						HandleSave(player, args, out message, out opts);
						break;
					case Command.set:
						HandleSet(player, args, out message, out opts);
						break;
					case Command.unset:
						HandleSet(player, args, out message, out opts, true);
						break;
					default:
						break;
				}
			}
			else
				ShowCommands(out message, out opts);
			if (message != null && message != "")
				SendMessage(player, message, opts);
		}

		// handle reset command
		void HandleReset(string[] args, out string message)
		{
			bool confirm = (args.Length > 0 && args[0] != null && args[0].ToLower() == "confirm");
			if (confirm)
			{
				Config.Clear();
				data = new VendingData();
				SaveData();
				message = "ResetSuccess";
			}
			else
				message = "ConfirmReset";
		}

		// handle clear command
		void HandleClear(BasePlayer player, out string message)
		{
			message = "VMNotFound";
			object entity;
			if (GetRaycastTarget(player, out entity))
				if (entity != null && entity is VendingMachine)
				{
					(entity as VendingMachine).sellOrders.sellOrders.Clear();
					message = "ClearSuccess";
				}
		}

		void HandleInfo(BasePlayer player, out string message, out object[] opts)
		{
			message = "VMNotFound";
			opts = new object[] { };
			object entity;
			if (GetRaycastTarget(player, out entity))
				if (entity != null && entity is VendingMachine)
				{
					uint id = (entity as VendingMachine).net.ID;
					bool isConfigured = vms.ContainsKey(id);
					string flags = "None";
					if (isConfigured)
						flags = vms[id].flags.ToString();
					message = "Information";
					opts = new object[] { id, isConfigured, flags };
				}
		}

		// handle load command
		void HandleLoad(BasePlayer player, string[] args, bool replaceAll, out string message, out object[] opts)
		{
			message = "";
			opts = new object[] { };
			if (args == null || args.Length == 0 || args[0] == null || args[0] == "")
			{
				message = "InvalidParameter";
				return;
			}
			object entity;
			if (!GetRaycastTarget(player, out entity))
			{
				message = "VMNotFound";
				return;
			}

			opts = new object[] { args[0] };
			if (entity != null && entity is VendingMachine)
				LoadSellOrders(entity as VendingMachine, args[0], replaceAll, out message);
		}

		// handle loading the sell orders into a vending machine
		void LoadSellOrders(VendingMachine vm, string templateName, bool replace, out string message)
		{
			message = "LoadSuccess";
			if (!data.templates.ContainsKey(templateName))
			{
				message = "TemplateNotFound";
				return;
			}
			if (data.templates[templateName].Empty())
			{
				message = "EmptyTemplate";
				return;
			}
			if (replace)
				vm.sellOrders.sellOrders.Clear();
			foreach (SellOrderEntry e in data.templates[templateName].entries)
			{
				ProtoBuf.VendingMachine.SellOrder o = new ProtoBuf.VendingMachine.SellOrder();
				o.itemToSellID = ItemManager.FindItemDefinition(e.itemToSellName).itemid;
				o.itemToSellAmount = e.itemToSellAmount;
				o.currencyID = ItemManager.FindItemDefinition(e.currencyName).itemid;
				o.currencyAmountPerItem = e.currencyAmountPerItem;
				vm.sellOrders.sellOrders.Add(o);
			}
			vm.RefreshSellOrderStockLevel();
			return;
		}

		// handle save command
		void HandleSave(BasePlayer player, string[] args, out string message, out object[] opts)
		{
			message = "";
			opts = new object[] { };
			if (args == null || args.Length == 0 || args[0] == null || args[0] == "")
			{
				message = "InvalidParameter";
				return;
			}
			bool overwrite = (args.Length > 1 && args[1] != null && args[1].ToLower() == "overwrite");
			object entity;
			if (!GetRaycastTarget(player, out entity))
			{
				message = "VMNotFound";
				return;
			}
			opts = new object[] { args[0] };
			if (entity != null && entity is VendingMachine)
				SaveSellOrders(entity as VendingMachine, args[0], out message, overwrite);
		}

		// handle saving the sell orders from a vending machine
		void SaveSellOrders(VendingMachine vm, string templateName, out string message, bool overwrite = false)
		{
			message = "SaveSuccess";
			if (templateName == null || templateName == "")
			{
				message = "InvalidParameter";
				return;
			}
			if (data.templates.ContainsKey(templateName) && !overwrite)
			{
				message = "TemplateExists";
				return;
			}

			ProtoBuf.VendingMachine.SellOrderContainer sellOrderContainer = vm.sellOrders;
			if (sellOrderContainer == null || sellOrderContainer.sellOrders == null || sellOrderContainer.sellOrders.Count == 0)
			{
				message = "EmptyVM";
				return;
			}
			SellOrderTemplate template = new SellOrderTemplate();
			template.PopulateTemplate(sellOrderContainer.sellOrders);
			if (!template.Empty())
			{
				data.templates[templateName] = template;
				SaveData();
			}
			return;
		}

		void HandleSet(BasePlayer player, string[] args, out string message, out object[] opts, bool unset = false)
		{
			message = unset ? "UnsetSuccess" : "SetSuccess";
			opts = new object[] { };
			if (args == null || args.Length == 0 || args[0] == null || args[0] == "" || !Enum.IsDefined(typeof(VendingMachineInfo.VMFlags), args[0]))
			{
				message = "InvalidParameter";
				return;
			}
			object entity;
			if (!GetRaycastTarget(player, out entity))
			{
				message = "VMNotFound";
				return;
			}
			opts = new object[] { args[0] };
			if (entity != null && entity is VendingMachine)
			{
				VendingMachineInfo.VMFlags flags = (VendingMachineInfo.VMFlags)Enum.Parse(typeof(VendingMachineInfo.VMFlags), args[0]);
				VendingMachineInfo i;
				if(!vms.TryGetValue((entity as VendingMachine).net.ID, out i))
				{
					i = new VendingMachineInfo();
					i.id = (entity as VendingMachine).net.ID;
					vms[i.id] = i;
				}
				if (unset)
					i.flags &= ~flags;
				else
					i.flags |= flags;
				if (i.flags == VendingMachineInfo.VMFlags.None)
					vms.Remove(i.id);
				SaveVendingMachineData();
			}
		}

		#endregion

		#region Messaging

		// send reply to a player
		void SendMessage(BasePlayer player, string message, object[] options = null)
		{
			string msg = GetMessage(message, player.UserIDString);
			if (options != null && options.Length > 0)
				msg = String.Format(msg, options);
			SendReply(player, GetMessage("Prefix", player.UserIDString) + msg);
		}

		// handle list command
		void HandleList(out string message, out object[] opts)
		{
			message = "TemplateList";
			opts = new object[] { data.GetTemplateList() };
		}

		// show list of valid commands
		void ShowCommands(out string message, out object[] opts)
		{
			message = "CommandList";
			opts = new object[] { string.Join(", ", Enum.GetValues(typeof(Command)).Cast<Command>().Select(x => x.ToString()).ToArray()) };
		}

		void LogTransaction(LogEntry logEntry, bool force = false)
		{
			if ((ConfigValue<bool>(Option.logTransSuccess) && logEntry.success) || (ConfigValue<bool>(Option.logTransFailure) && !logEntry.success) || force)
			{
				string logString = logEntry.ToString();
				ConVar.Server.Log("oxide/logs/VendingManager.log", logString);
			}
		}

		#endregion

		#region Helper Procedures

		// set all vending machines
		void SetAll(bool lockable, float health = defaultHealth)
		{
			foreach (VendingMachine vm in GameObject.FindObjectsOfType(typeof(VendingMachine)))
				Set(vm, lockable, health);
		}

		// setup a specific vending machine
		void Set(VendingMachine vm, bool lockable, float health = defaultHealth, bool restoreProtection = false)
		{
			if (ConfigValue<bool>(Option.noBroadcast))
				vm.SetFlag(BaseEntity.Flags.Reserved4, false, false);
			if (defaultProtection == null)
			{
				defaultProtection = vm.baseProtection;
				if (data.resistances == null)
				{
					data.SetResistances(defaultProtection.amounts);
					SaveData();
				}
			}
			else
			{
				if (customProtection == null)
					customProtection = UnityEngine.Object.Instantiate(vm.baseProtection) as ProtectionProperties;
				if (data.resistances != null && !restoreProtection)
				{
					customProtection.amounts = data.GetResistances();
					vm.baseProtection = customProtection;
					//vm.baseProtection.amounts = data.GetResistances();
				}
				if (restoreProtection)
					vm.baseProtection = defaultProtection;
			}
			if (!lockable && ConfigValue<bool>(Option.ejectLocks)) Eject(vm);
			vm.isLockable = lockable;
			if (ConfigValue<bool>(Option.setHealth))
			{
				float h = health * vm.healthFraction;
				vm.InitializeHealth(h, health);
			}
		}

		// eject lock from vending machine
		bool Eject(VendingMachine m)
		{
			BaseEntity lockEntity = m.GetSlot(BaseEntity.Slot.Lock);
			if (lockEntity != null && lockEntity is BaseLock)
			{
				Item lockItem = ItemManager.Create((lockEntity as BaseLock).itemType, 1, lockEntity.skinID);
				lockEntity.Kill();
				lockItem.Drop(m.GetDropPosition(), m.GetDropVelocity(), m.GetEstimatedWorldRotation());
				m.isLockable = ConfigValue<bool>(Option.lockable);
				return true;
			}
			return false;
		}

		// eject locks from all vending machines
		void EjectAll(out string message, out object[] opts)
		{
			int counter = 0;
			foreach (VendingMachine m in GameObject.FindObjectsOfType(typeof(VendingMachine)))
				if (Eject(m)) counter++;

			message = "Ejected";
			opts = new object[] { counter };
		}

		// raycast to find entity being looked at
		bool GetRaycastTarget(BasePlayer player, out object closestEntity)
		{
			closestEntity = null;
			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
				return false;
			closestEntity = hit.GetEntity();
			return true;
		}

		// check if player is an admin
		private static bool isAdmin(BasePlayer player)
		{
			if (player?.net?.connection == null) return true;
			return player.net.connection.authLevel > 0;
		}

		// check if player has permission or is an admin
		private bool hasPermission(BasePlayer player, string permname)
		{
			return isAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
		}

		// get config value and convert type
		T ConfigValue<T>(Option option)
		{
			return (T)Convert.ChangeType(data.config[option], typeof(T));
		}

		// save all locks
		void SaveVMsAndLocks()
		{
			locks.Clear();
			foreach (VendingMachine vm in GameObject.FindObjectsOfType(typeof(VendingMachine)))
			{
				BaseLock l = (BaseLock)vm.GetSlot(BaseEntity.Slot.Lock);
				if (l == null) continue;

				LockInfo li = new LockInfo(vm.net.ID, l);
				locks[vm.net.ID] = li;
			}
			SaveVendingMachineData();
			SaveLocksData();
		}

		// load all locks
		void LoadLocks()
		{
			foreach(LockInfo li in locks.Values)
			{
				VendingMachine vm = (VendingMachine) BaseNetworkable.serverEntities.Find(li.vmId);
				if (vm == null) continue;
				if (vm.GetSlot(BaseEntity.Slot.Lock) != null) continue;

				BaseLock l;
				if (li.isCodeLock)
					l = (CodeLock)GameManager.server.CreateEntity(CodeLockPrefab);
				else
					l = (KeyLock)GameManager.server.CreateEntity(KeyLockPrefab);

				if (l == null) continue;

				l.gameObject.Identity();
				li.ToLock(ref l);
				l.SetParent(vm, vm.GetSlotAnchorName(BaseEntity.Slot.Lock));
				l.OnDeployed(vm);
				l.Spawn();
				vm.SetSlot(BaseEntity.Slot.Lock, l);
			}
		}

		// Destroy all attached locks on shutdown
		void DestroyLocks()
		{
			foreach (VendingMachine vm in GameObject.FindObjectsOfType(typeof(VendingMachine)))
			{
				BaseEntity l;
				if ((l = vm.GetSlot(BaseEntity.Slot.Lock)) != null)
					l.Kill();
			}
		}

		// generate a string identifier from a Vector3
		string GetIDFromPosition(Vector3 position)
		{
			return "[" + position.x + "" + position.y + "" + position.z + "]";
		}

		#endregion

		#region Subclasses

		// config/data container
		class VendingData
		{
			public Dictionary<Option, object> config = new Dictionary<Option, object>();
			public Dictionary<DamageType, float> resistances;
			public Dictionary<string, SellOrderTemplate> templates;

			public string GetTemplateList() {
				string list = string.Join(", ", templates.Keys.ToArray());
				if (list == null || list == "")
					list = "(empty)";
				return list;
			}

			public void SetResistances(float[] amounts)
			{
				resistances = new Dictionary<DamageType, float>();
				for (int i = 0; i < amounts.Length; i++)
					resistances[(DamageType)i] = amounts[i];
			}

			public float[] GetResistances()
			{
				float[] values = new float[22];
				if (resistances != null)
					foreach (KeyValuePair<DamageType, float> entry in resistances)
						values[(int)entry.Key] = entry.Value;
				return values;
			}
		}

		// helper class for building sell order entries
		class SellOrderTemplate
		{
			public List<SellOrderEntry> entries = new List<SellOrderEntry>();

			public void PopulateTemplate(List<ProtoBuf.VendingMachine.SellOrder> sellOrders)
			{
				if (sellOrders == null) return;
				foreach (ProtoBuf.VendingMachine.SellOrder o in sellOrders)
					AddSellOrder(o);
			}

			public void AddSellOrder(ProtoBuf.VendingMachine.SellOrder o)
			{
				if (o == null) return;
				SellOrderEntry e = new SellOrderEntry();
				e.itemToSellName = ItemManager.FindItemDefinition(o.itemToSellID).shortname;
				e.itemToSellAmount = o.itemToSellAmount;
				e.currencyName = ItemManager.FindItemDefinition(o.currencyID).shortname;
				e.currencyAmountPerItem = o.currencyAmountPerItem;
				entries.Add(e);
			}

			public bool Empty()
			{
				return (entries == null || entries.Count == 0);
			}
		}

		// simple sell order entry container
		struct SellOrderEntry
		{
			public string itemToSellName;
			public int itemToSellAmount;
			public string currencyName;
			public int currencyAmountPerItem;
		}

		struct LogEntry
		{
			public enum FailureReason { NoMoney, NoItems, Unknown }
			public uint id;
			public ulong playerID;
			public string playerName;
			public string bought;
			public string cost;
			public bool success;
			public bool isBuyOrder;
			public FailureReason reason;

			public override string ToString()
			{
				if(isBuyOrder)
					return "VM " + id + ": " + playerName + " [" + playerID + "] " + (success ? "bought " : "failed to buy ") + bought + " for " + cost + (success ? "" : " - Reason: " + GetReason());
				else
					return "VM " + id + ": " + playerName + " [" + playerID + "] " + (success ? "sold " : "failed to sell ") + bought + " for " + cost + (success ? "" : " - Reason: " + GetReason());
			}
			string GetReason()
			{
				if (reason == FailureReason.NoItems)
					return "Not enough currency items";
				if (reason == FailureReason.NoMoney)
					return "Not enough money";
				return "Unknown reason";
			}
		}

		class VendingMachineInfo
		{
			[Flags]
			public enum VMFlags {
				None			= 0,
				Bottomless		= 1,
				Immortal		= 1 << 1,
				Restricted		= 1 << 2,
				LogTransactions	= 1 << 3
			}
			public uint id;
			[JsonConverter(typeof(StringEnumConverter))]
			public VMFlags flags;
		}
		
		// Lock details container
		class LockInfo
		{
			static readonly byte[] entropy = new byte[] { 11, 7, 5, 3 };
			public uint vmId;
			public bool isCodeLock = false;
			public string codeEncrypted;
			[JsonIgnore]
			public string code
			{
				get {
					return Shift(codeEncrypted, -4);
				}
				set {
					codeEncrypted = Shift(value, 4);
				}
			}
			public string guestCodeEncrypted;
			[JsonIgnore]
			public string guestCode
			{
				get {
					return Shift(guestCodeEncrypted, -4);
				}
				set {
					guestCodeEncrypted = Shift(value, 4);
				}
			}
			public List<ulong> whitelist;
			public List<ulong> guests;
			public int keyCode;
			public bool firstKey;
			public bool isLocked;

			FieldInfo codeField = typeof(CodeLock).GetField("code", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo guestCodeField = typeof(CodeLock).GetField("guestCode", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo whitelistField = typeof(CodeLock).GetField("whitelistPlayers", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo guestField = typeof(CodeLock).GetField("guestPlayers", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo keyCodeField = typeof(KeyLock).GetField("keyCode", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo firstKeyField = typeof(KeyLock).GetField("firstKeyCreated", BindingFlags.Instance | BindingFlags.NonPublic);

			public LockInfo() { }
			public LockInfo(uint vmId, BaseLock l)
			{
				this.vmId = vmId;
				FromLock(l);
			}

			public void FromLock(BaseLock l)
			{
				if (l.GetType() == typeof(CodeLock))
				{
					isCodeLock = true;
					code = codeField.GetValue(l).ToString();
					guestCode = guestCodeField.GetValue(l).ToString();
					whitelist = (List<ulong>)whitelistField.GetValue(l);
					guests = (List<ulong>)guestField.GetValue(l);
				}
				else if (l.GetType() == typeof(KeyLock))
				{
					keyCode = (int)keyCodeField.GetValue(l);
					firstKey = (bool)firstKeyField.GetValue(l);
				}
				isLocked = l.IsLocked();
			}

			public void ToLock(ref BaseLock l)
			{
				if (l.GetType() == typeof(CodeLock))
				{
					codeField.SetValue(l, code);
					guestCodeField.SetValue(l, guestCode);
					whitelistField.SetValue(l, whitelist);
					guestField.SetValue(l, guests);
				}
				else if (l.GetType() == typeof(KeyLock))
				{
					keyCodeField.SetValue(l, keyCode);
					firstKeyField.SetValue(l, firstKey);
				}
				l.SetFlag(BaseEntity.Flags.Locked, isLocked);
			}

			// simple obfuscation for codes
			static string Shift(string source, int shift)
			{
				int maxChar = Convert.ToInt32(char.MaxValue);
				int minChar = Convert.ToInt32(char.MinValue);

				char[] buffer = source.ToCharArray();

				for (int i = 0; i < buffer.Length; i++)
				{
					int shifted = Convert.ToInt32(buffer[i]) + (shift * entropy[i]);

					if (shifted > maxChar)
					{
						shifted -= maxChar;
					}
					else if (shifted < minChar)
					{
						shifted += maxChar;
					}

					buffer[i] = Convert.ToChar(shifted);
				}

				return new string(buffer);
			}
		}

		#endregion
	}
}