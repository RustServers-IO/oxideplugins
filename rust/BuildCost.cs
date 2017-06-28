using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("BuildCost", "ignignokt84", "0.0.2", ResourceId = 2388)]
	[Description("Build cost utility")]
	class BuildCost : RustPlugin
	{
		#region Variables

		// usage permission
		const string PermCanUse = "buildcost.canuse";

		// layer mask
		int layerMasks = LayerMask.GetMask("Construction", "Construction Trigger", "Trigger");

		// deployable -> item lookup map
		Dictionary<uint, int> deployableLookup = new Dictionary<uint, int>();
		// aggregation helper
		CostAggregator aggregator = new CostAggregator();

		// groups
		enum Group { All, BuildingBlocks, Deployables }

		#endregion


		#region Lang

		// load default messages to Lang
		void LoadDefaultMessages()
		{
			var messages = new Dictionary<string, string>
			{
				{"Prefix", "<color=orange>[ BuildCost ]</color> "},
				{"BuildingBlockCost", "<color=cyan>Building Blocks ({0})</color>" + Environment.NewLine + "<size=12>{1}</size>" },
				{"DeployablesCost", "<color=cyan>Deployables ({0})</color>" + Environment.NewLine + "<size=12>{1}</size>" },
				{"NoBuildingFound", "No building found" }
			};
			lang.RegisterMessages(messages, this);
		}

		// get message from Lang
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

		#endregion

		#region Loading/Unloading

		// on load
		void Init()
		{
			permission.RegisterPermission(PermCanUse, this);
			cmd.AddChatCommand("cost", this, "CalculateCost");
		}

		// server initialized
		void OnServerInitialized()
		{
			BuildDeployableLookups();
		}

		#endregion

		#region Messaging

		// send reply to a player
		void SendMessage(BasePlayer player, string message, object[] options = null)
		{
			string msg = GetMessage(message, player.UserIDString);
			if (options != null && options.Length > 0)
				msg = string.Format(msg, options);
			SendReply(player, GetMessage("Prefix", player.UserIDString) + msg);
		}

		#endregion

		#region Helper Procedures

		// build reference map of prefab IDs to item IDs
		void BuildDeployableLookups()
		{
			foreach (ItemDefinition i in ItemManager.GetItemDefinitions())
			{
				if (i == null) continue;
				ItemModDeployable m = i.GetComponent<ItemModDeployable>();
				if (m != null)
					deployableLookup[m.entityPrefab.resourceID] = i.itemid;
			}
		}

		// handle command
		void CalculateCost(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, PermCanUse)) return;
			aggregator.Reset();
			object closestEntity;
			if (!GetRaycastTarget(player, out closestEntity))
			{
				SendMessage(player, "NoBuildingFound");
				return;
			}
			BaseEntity initialBlock = closestEntity as BaseEntity;
			if (initialBlock == null)
			{
				SendMessage(player, "NoBuildingFound");
				return;
			}

			HashSet<BuildingBlock> blocks;
			HashSet<BaseEntity> deployables;
			if (GetStructure(initialBlock, out blocks, out deployables))
			{
				CalculateBuildingBlockCost(blocks);
				CalculateDeployableCost(deployables);
			}
			if (aggregator.counter.Values.Sum() == 0)
			{
				SendMessage(player, "NoBuildingFound");
				return;
			}

			if (aggregator.costs.ContainsKey(Group.BuildingBlocks))
				SendMessage(player, "BuildingBlockCost", aggregator.GetCost(Group.BuildingBlocks));
			if (aggregator.costs.ContainsKey(Group.Deployables))
				SendMessage(player, "DeployablesCost", aggregator.GetCost(Group.Deployables));
		}

		// raycasting
		bool GetRaycastTarget(BasePlayer player, out object closestEntity)
		{
			closestEntity = false;
			RaycastHit hit;
			if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 100f))
				return false;
			closestEntity = hit.GetEntity();
			return true;
		}

		// builds a list of entities which make up a structure
		bool GetStructure(BaseEntity initialBlock, out HashSet<BuildingBlock> structure, out HashSet<BaseEntity> deployables)
		{
			structure = new HashSet<BuildingBlock>();
			deployables = new HashSet<BaseEntity>();
			List<Vector3> checkFrom = new List<Vector3>();
			BuildingBlock block;

			checkFrom.Add(initialBlock.transform.position);
			if (initialBlock is BuildingBlock)
				structure.Add(initialBlock as BuildingBlock);

			int current = 0;
			while (true)
			{
				current++;
				if (current > checkFrom.Count)
					break;
				List<BaseEntity> list = new List<BaseEntity>();
				Vis.Entities(checkFrom[current - 1], 3f, list);
				for (int i = 0; i < list.Count; i++)
				{
					BaseEntity hit = list[i];
					if (hit.GetComponentInParent<BuildingBlock>() != null)
					{
						block = hit.GetComponentInParent<BuildingBlock>();
						if (!(structure.Contains(block)))
						{
							checkFrom.Add(block.transform.position);
							structure.Add(block);
						}
					}
					else if (hit != null)
					{
						deployables.Add(hit);
						if (hit.HasSlot(BaseEntity.Slot.Lock))
						{
							BaseEntity l = hit.GetSlot(BaseEntity.Slot.Lock);
							if (l != null)
								deployables.Add(l);
						}
					}
				}
			}

			return true;
		}

		// aggregate all building block costs
		void CalculateBuildingBlockCost(HashSet<BuildingBlock> blocks)
		{
			foreach (BuildingBlock block in blocks)
			{
				if (block == null) continue;
				// only calculate twig and current grade
				aggregator.AddCosts(block.blockDefinition.grades[(int)block.grade].costToBuild, Group.BuildingBlocks);
				if (block.grade != BuildingGrade.Enum.Twigs)
					aggregator.AddCosts(block.blockDefinition.grades[(int)BuildingGrade.Enum.Twigs].costToBuild, Group.BuildingBlocks, false);
			}
		}

		// aggregate all deployable costs
		void CalculateDeployableCost(HashSet<BaseEntity> deployables)
		{
			foreach (BaseEntity deployable in deployables)
			{
				if (deployable == null) continue;
				if (!deployableLookup.ContainsKey(deployable.prefabID)) continue;
				int itemId = deployableLookup[deployable.prefabID];
				ItemBlueprint b = ItemManager.FindItemDefinition(itemId)?.Blueprint;
				if (b == null) continue;
				aggregator.AddCosts(b.ingredients, Group.Deployables);
			}
		}

		// permission check
		private bool hasPermission(BasePlayer player, string permname)
		{
			return player.IsAdmin || permission.UserHasPermission(player.UserIDString, permname);
		}

		#endregion

		#region Subclasses

		// entity cost aggregation helper class
		class CostAggregator
		{
			public Dictionary<Group, List<ItemAmount>> costs = new Dictionary<Group, List<ItemAmount>>();
			public Dictionary<Group, int> counter = new Dictionary<Group, int>();

			public void Reset()
			{
				costs = new Dictionary<Group, List<ItemAmount>>();
				counter = new Dictionary<Group, int>();
			}

			public void AddCosts(List<ItemAmount> amounts, Group group = Group.All, bool increment = true)
			{
				if (increment)
				{
					if (counter.ContainsKey(group))
						counter[group]++;
					else
						counter[group] = 1;
				}
				foreach (ItemAmount amount in amounts)
					AddCost(amount, group);
			}

			public void AddCost(ItemAmount amount, Group group = Group.All)
			{
				if (!costs.ContainsKey(group))
					costs[group] = new List<ItemAmount>();
				List<ItemAmount> groupCosts = costs[group];

				ItemAmount existing = groupCosts.SingleOrDefault(i => i.itemid == amount.itemid);
				if (existing == null)
					groupCosts.Add(new ItemAmount(amount.itemDef, amount.amount));
				else
					existing.amount += amount.amount;
			}

			public object[] GetCost(Group group = Group.All)
			{
				if (!costs.ContainsKey(group) || costs[group] == null || costs[group].Count == 0)
				{
					return null;
				}
				List<ItemAmount> itemList = costs[group];
				List<string> costStrings = new List<string>();
				foreach (ItemAmount amount in itemList)
					if (amount != null)
						costStrings.Add(amount.itemDef.displayName.translated + ": " + amount.amount);

				return new object[] { counter[group], string.Join(Environment.NewLine, costStrings.ToArray()) };
			}
		}

		#endregion
	}
}