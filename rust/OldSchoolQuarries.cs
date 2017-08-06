using System;
using System.Collections.Generic;

using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("OldSchoolQuarries", "S0N_0F_BISCUIT", "1.0.0", ResourceId = 0)]
	[Description("Makes resource output from quarries better")]
	class OldSchoolQuarries : RustPlugin
	{
		#region Variables
		private enum DefaultOre { None, Sulfur, Metal, HighQuality };

		class StoredData
		{
			public List<Vector3> changedDeposits = new List<Vector3>();
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
				["AnalysisFooter"] = "----------------------------------"
			}, this);
		}
		#endregion

		#region Initialization
		//
		// Mod initialization
		//
		private void Init()
		{
			LoadData();
		}
		//
		// Register permissions
		//
		private void Loaded()
		{
			permission.RegisterPermission($"{Title.ToLower()}.probe", this);
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
				DefaultOre ore = DefaultOre.None;

				ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(entity.transform.position);

				if (data.changedDeposits.Contains(rd.origin))
					return;
				
				data.changedDeposits.Add(rd.origin);
				SaveData();

				ResourceDepositManager.ResourceDeposit.ResourceDepositEntry originalResource = null;
				
				int oreCount = 0;
				foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
				{
					switch (resource.type.shortname)
					{
						case "sulfur.ore":
							ore = DefaultOre.Sulfur;
							originalResource = resource;
							oreCount++;
							break;
						case "metal.ore":
							ore = DefaultOre.Metal;
							originalResource = resource;
							oreCount++;
							break;
						case "hq.metal.ore":
							originalResource = resource;
							oreCount++;
							break;
						default:
							break;
					}
				}

				if (oreCount > 1)
					return;

				if (originalResource == null && rd._resources.Count != 0)
					originalResource = rd._resources.ToArray()[0];

				ItemDefinition sulfur = ItemManager.itemList.Find(x => x.shortname == "sulfur.ore");
				ItemDefinition metal = ItemManager.itemList.Find(x => x.shortname == "metal.ore");
				ItemDefinition hq = ItemManager.itemList.Find(x => x.shortname == "hq.metal.ore");

				System.Random rng = new System.Random();
				int workNeeded = rng.Next(2, 4);
				int choice = rng.Next(1, 100);
				switch (ore)
				{
					case DefaultOre.Sulfur:  // Give a chance at some amount of metal ore
						if (workNeeded >= 3)
							rd.Add(metal, 1, rng.Next(10000, 100000), workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						break;
					case DefaultOre.Metal: // Give a chance at some amount of sulfur ore
						if (workNeeded >= 3)
							rd.Add(sulfur, 1, rng.Next(10000, 100000), 4, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						break;
					case DefaultOre.HighQuality: // Give a chance at some amount of either metal, sulfur, or both ores
						if (workNeeded < 3)
							return;
						if (choice < 40) // Just sulfur
							rd.Add(sulfur, 1, rng.Next(10000, 100000), 4, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						else if (choice < 80) // Just metal
							rd.Add(metal, 1, rng.Next(10000, 100000), workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						else // Both sulfur and metal
						{
							rd.Add(sulfur, 1, rng.Next(10000, 100000), 4, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							rd.Add(metal, 1, rng.Next(10000, 100000), workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						}
						break;
					default: // Give a chance at some amount of either metal, sulfur, or both ores
						if (workNeeded < 3)
							return;
						if (choice < 40) // Just sulfur
							rd.Add(sulfur, 1, rng.Next(10000, 100000), 4, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						else if (choice < 80) // Just metal
							rd.Add(metal, 1, rng.Next(10000, 100000), workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						else // Both sulfur and metal
						{
							rd.Add(sulfur, 1, rng.Next(10000, 100000), 4, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
							rd.Add(metal, 1, rng.Next(10000, 100000), workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
						}
						break;
				}
			}
		}
		#endregion

		#region Chat Commands
		[ChatCommand("getdeposit")]
		void getDeposit(BasePlayer player, string command, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, $"{Title.ToLower()}.probe"))
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
		#endregion

		#region Helpers
		//
		// Get string and format from lang file
		//
		private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
		#endregion
	}
}