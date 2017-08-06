using System;
using System.Collections.Generic;

using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("OldSchoolQuarries", "S0N_0F_BISCUIT", "1.0.0", ResourceId = 2585)]
	[Description("Makes resource output from quarries better")]
	class OldSchoolQuarries : RustPlugin
	{
		#region Variables
		private enum OreType { None, Sulfur, Metal, HighQuality };

		private ItemDefinition sulfur = ItemManager.itemList.Find(x => x.shortname == "sulfur.ore");
		private ItemDefinition metal = ItemManager.itemList.Find(x => x.shortname == "metal.ore");
		private ItemDefinition hq = ItemManager.itemList.Find(x => x.shortname == "hq.metal.ore");

		class DepositEntry
		{
			public OreType type = OreType.None;
			public int amount;
			public float workNeeded;
		}

		class Deposit
		{
			public Vector3 origin = Vector3.zero;
			public List<DepositEntry> entries = new List<DepositEntry>();
		}

		class StoredData
		{
			public List<Deposit> changedDeposits = new List<Deposit>();
		}

		private StoredData data;
		#endregion

		#region Localization
		private new void LoadDefaultMessages()
		{
			// English
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoCommandPermission"] = "You do not have permission to use this command!",
				["NoResources"] = "No resources found.",
				["AnalysisHeader"] = "Mineral Analysis:",
				["Analysis"] = "Item: {0}, Amount: {1} pM",
				["AnalysisFooter"] = "----------------------------------",
				["ClearData"] = "Plugin data cleared."
			}, this);
		}
		#endregion

		#region Initialization
		//
		// Mod initialization
		//
		private void Init()
		{
			permission.RegisterPermission($"oldschoolquarries.probe", this);
			LoadData();
		}
		//
		// Edit the stored resource deposits
		//
		void OnTerrainInitialized()
		{
			foreach (Deposit deposit in data.changedDeposits)
			{
				ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(deposit.origin);

				int oreCount = 0;
				foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
				{
					switch (resource.type.shortname)
					{
						case "sulfur.ore":
							oreCount++;
							break;
						case "metal.ore":
							oreCount++;
							break;
						case "hq.metal.ore":
							oreCount++;
							break;
						default:
							break;
					}
				}

				if (oreCount > 1)
					return;

				foreach (DepositEntry entry in deposit.entries)
				{
					switch (entry.type)
					{
						case OreType.Metal:
							rd.Add(metal, 1, entry.amount, entry.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							break;
						case OreType.Sulfur:
							rd.Add(sulfur, 1, entry.amount, entry.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							break;
						default:
							break;
					}
				}
			}
		}
		#endregion

		#region Data Handling
		//
		// Load plugin data
		//
		private void LoadData()
		{
			try
			{
				data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Title);
			}
			catch
			{
				data = new StoredData();
				SaveData();
			}
		}
		//
		// Save PlayerData
		//
		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject(Title, data);
		}
		//
		// Clear StoredData
		//
		private void ClearData()
		{
			data = new StoredData();
			SaveData();
		}
		#endregion

		#region Functionality
		//
		// Update salt map when resource deposit is tapped for the first time
		void OnEntitySpawned(BaseNetworkable entity)
		{
			if (entity is SurveyCharge)
			{
				OreType ore = OreType.None;

				ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(entity.transform.position);

				if (data.changedDeposits.Exists(d => d.origin == rd.origin))
					return;

				ResourceDepositManager.ResourceDeposit.ResourceDepositEntry originalResource = null;
				
				int oreCount = 0;
				foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
				{
					switch (resource.type.shortname)
					{
						case "sulfur.ore":
							ore = OreType.Sulfur;
							originalResource = resource;
							oreCount++;
							break;
						case "metal.ore":
							ore = OreType.Metal;
							originalResource = resource;
							oreCount++;
							break;
						case "hq.metal.ore":
							ore = OreType.HighQuality;
							originalResource = resource;
							oreCount++;
							break;
						default:
							break;
					}
				}

				if (oreCount > 1)
					return;

				Deposit deposit = new Deposit { origin = rd.origin };

				if (originalResource == null && rd._resources.Count != 0)
					originalResource = rd._resources.ToArray()[0];

				System.Random rng = new System.Random();
				float workNeeded = (float)(rng.Next(0, 2) + rng.NextDouble());
				int choice = rng.Next(1, 100);
				int amount = 0;
				switch (ore)
				{
					case OreType.Sulfur:  // Give a chance at some amount of metal ore
						if (workNeeded > 1f)
						{
							amount = rng.Next(10000, 100000);
							rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
						}
						break;
					case OreType.Metal: // Give a chance at some amount of sulfur ore
						if (workNeeded > 1.75f)
						{
							amount = rng.Next(10000, 100000);
							rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
						}
						break;
					case OreType.HighQuality: // Give a chance at some amount of either metal, sulfur, or both ores
						if (choice < 40) // Just sulfur
						{
							amount = rng.Next(10000, 100000);
							workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
							rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
						}
						else if (choice < 80) // Just metal
						{
							if (workNeeded < 1.75f)
								workNeeded += (1.75f - workNeeded);
							amount = rng.Next(10000, 100000);
							rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
						}
						else // Both sulfur and metal
						{
							if (workNeeded < 1.75f)
								workNeeded += (1.75f - workNeeded);
							amount = rng.Next(10000, 100000);
							rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
							amount = rng.Next(10000, 100000);
							workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
							rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
						}
						break;
					default: // Give a chance at some amount of either metal, sulfur, or both ores
						if (oreCount == 1)
							return;
						if (choice < 40) // Just sulfur
						{
							if (workNeeded > 1.75f)
								return;
							amount = rng.Next(10000, 100000);
							workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
							rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
						}
						else if (choice < 80) // Just metal
						{
							if (workNeeded < 1.75f)
								workNeeded += (1.75f - workNeeded);
							amount = rng.Next(10000, 100000);
							rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
						}
						else // Both sulfur and metal
						{
							if (workNeeded < 1.75f)
								workNeeded += (1.75f - workNeeded);
							amount = rng.Next(10000, 100000);
							rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
							amount = rng.Next(10000, 100000);
							workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
							rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
						}
						break;
				}
				data.changedDeposits.Add(deposit);
				SaveData();
			}
		}
		#endregion

		#region Commands
		//
		// Perform a mineral analysis at players position
		//
		[ChatCommand("getdeposit")]
		void getDeposit(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, $"oldschoolquarries.probe"))
			{
				PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
				return;
			}
			
			ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(player.transform.position);
			if (rd == null)
			{
				PrintToChat(player, Lang("NoResources", player.UserIDString));
				return;
			}

			PrintToChat(player, Lang("AnalysisHeader", player.UserIDString));
			float num1 = 10f;
			float num2 = 7.5f;
			List<int> fixIndex = new List<int>();
			int index = 0;
			foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
			{
				float num3 = (float)(60.0 / num1 * (num2 / (double)resource.workNeeded));
				if (float.IsInfinity(num3))
					fixIndex.Add(index);
				PrintToChat(player, Lang("Analysis", player.UserIDString, resource.type.displayName.translated, Math.Round(num3, 1)));
				index++;
			}

			if (fixIndex.Count != 0)
			{
				foreach (int pos in fixIndex)
					rd._resources.RemoveAt(pos);
			}

			PrintToChat(player, Lang("AnalysisFooter", player.UserIDString));
		}

		[ConsoleCommand("oldschoolquarries.cleardata")]
		void softWipe(ConsoleSystem.Arg arg)
		{
			if (!arg.IsAdmin)
				return;
			ClearData();
			Puts(Lang("ClearData"));
		}
		#endregion

		#region Helpers
		//
		// Get string and format from lang file
		//
		private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
		#endregion
	}
}