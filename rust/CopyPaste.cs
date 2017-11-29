﻿using Facepunch;
using Oxide.Core;
using Oxide.Core.Libraries;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Copy Paste", "Reneb", "3.4.7", ResourceId = 716)]
	[Description("Copy and paste your buildings to save them or move them")]

	class CopyPaste : RustPlugin
	{
		private int copyLayer 		= LayerMask.GetMask("Construction", "Construction Trigger", "Trigger", "Deployed", "Default");
		private int groundLayer 	= LayerMask.GetMask("Terrain", "Default");
		private int rayCopy 		= LayerMask.GetMask("Construction", "Deployed", "Tree", "Resource", "Prevent Building");
		private int rayPaste 		= LayerMask.GetMask("Construction", "Deployed", "Tree", "Terrain", "World", "Water", "Prevent Building");

		private string copyPermission	= "copypaste.copy";
		private string pastePermission	= "copypaste.paste";
		private string undoPermission	= "copypaste.undo";
		private string serverID			= "Server";
		private string subDirectory		= "copypaste/";

		private Dictionary<string, Stack<List<BaseEntity>>> lastPastes = new Dictionary<string, Stack<List<BaseEntity>>>();

		private List<BaseEntity.Slot> checkSlots = new List<BaseEntity.Slot>()
		{
			BaseEntity.Slot.Lock,
			BaseEntity.Slot.UpperModifier,
			BaseEntity.Slot.MiddleModifier,
			BaseEntity.Slot.LowerModifier
		};

		private DataFileSystem dataSystem = Interface.Oxide.DataFileSystem;

		private enum CopyMechanics { Building, Proximity }

		private FieldInfo _hasCode = typeof(CodeLock).GetField("hasCode", (BindingFlags.Instance | BindingFlags.NonPublic));
		private FieldInfo _hasGuestCode = typeof(CodeLock).GetField("hasGuestCode", (BindingFlags.Instance | BindingFlags.NonPublic));
		private FieldInfo _equippingActive = typeof(Locker).GetField("equippingActive", (BindingFlags.Instance | BindingFlags.NonPublic));
		
		//Config

		private ConfigData config;

		private class ConfigData
		{
			[JsonProperty(PropertyName = "Copy Options")]
			public CopyOptions Copy { get; set; }

			[JsonProperty(PropertyName = "Paste Options")]
			public PasteOptions Paste { get; set; }
			
			public class CopyOptions
			{
				[JsonProperty(PropertyName = "Buildings (true/false)")]
				[DefaultValue(true)]
				public bool Buildings { get; set; } = true;

				[JsonProperty(PropertyName = "Deployables (true/false)")]
				[DefaultValue(true)]
				public bool Deployables { get; set; } = true;

				[JsonProperty(PropertyName = "Inventories (true/false)")]
				[DefaultValue(true)]
				public bool Inventories { get; set; } = true;
				
				[JsonProperty(PropertyName = "Share (true/false)")]
				[DefaultValue(false)]
				public bool Share { get; set; } = false;

				[JsonProperty(PropertyName = "Tree (true/false)")]
				[DefaultValue(false)]
				public bool Tree { get; set; } = false;
			}

			public class PasteOptions
			{
				[JsonProperty(PropertyName = "Auth (true/false)")]
				[DefaultValue(false)]
				public bool Auth { get; set; } = false;

				[JsonProperty(PropertyName = "Deployables (true/false)")]
				[DefaultValue(true)]
				public bool Deployables { get; set; } = true;

				[JsonProperty(PropertyName = "Inventories (true/false)")]
				[DefaultValue(true)]
				public bool Inventories { get; set; } = true;
				
				[JsonProperty(PropertyName = "Vending Machines (true/false)")]
				[DefaultValue(true)]
				public bool VendingMachines { get; set; } = true;				
			}
		}

		private void LoadVariables()
		{
			Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
			
			config = Config.ReadObject<ConfigData>();

			Config.WriteObject(config, true);
		}

		protected override void LoadDefaultConfig()
		{
			var configData = new ConfigData
			{
				Copy = new ConfigData.CopyOptions(),
				Paste = new ConfigData.PasteOptions()
			};
			
			Config.WriteObject(configData, true);
		}
		
		//Hooks

		private void Init()
		{
			permission.RegisterPermission(copyPermission, this);
			permission.RegisterPermission(pastePermission, this);
			permission.RegisterPermission(undoPermission, this);

			Dictionary<string, Dictionary<string, string>> compiledLangs = new Dictionary<string, Dictionary<string, string>>();

			foreach(var line in messages)
			{
				foreach(var translate in line.Value)
				{
					if(!compiledLangs.ContainsKey(translate.Key))
						compiledLangs[translate.Key] = new Dictionary<string, string>();

					compiledLangs[translate.Key][line.Key] = translate.Value;
				}
			}

			foreach(var cLangs in compiledLangs)
			{
				lang.RegisterMessages(cLangs.Value, this, cLangs.Key);
			}
		}

		private void OnServerInitialized() => LoadVariables();

		//API

		object TryCopyFromSteamID(ulong userID, string filename, string[] args)
		{
			var player = BasePlayer.FindByID(userID);

			if(player == null)
				return Lang("NOT_FOUND_PLAYER", player.UserIDString);

			var ViewAngles = Quaternion.Euler(player.GetNetworkRotation());
			BaseEntity sourceEntity;
			Vector3 sourcePoint;

			if(!FindRayEntity(player.eyes.position, ViewAngles * Vector3.forward, out sourcePoint, out sourceEntity, rayCopy))
				return Lang("NO_ENTITY_RAY", player.UserIDString);

			return TryCopy(sourcePoint, sourceEntity.transform.rotation.ToEulerAngles(), filename, ViewAngles.ToEulerAngles().y, args);
		}

		object TryPasteFromSteamID(ulong userID, string filename, string[] args)
		{
			var player = BasePlayer.FindByID(userID);

			if(player == null)
				return Lang("NOT_FOUND_PLAYER", player.UserIDString);

			var ViewAngles = Quaternion.Euler(player.GetNetworkRotation());
			BaseEntity sourceEntity;
			Vector3 sourcePoint;

			if(!FindRayEntity(player.eyes.position, ViewAngles * Vector3.forward, out sourcePoint, out sourceEntity, rayPaste))
				return Lang("NO_ENTITY_RAY", player.UserIDString);

			return TryPaste(sourcePoint, filename, player, ViewAngles.ToEulerAngles().y, args);
		}

		object TryPasteFromVector3(Vector3 pos, float rotationCorrection, string filename, string[] args)
		{
			return TryPaste(pos, filename, null, rotationCorrection, args);
		}

		//Other methods

		private object CheckCollision(List<Dictionary<string,object>> entities, Vector3 startPos, float radius)
		{
			foreach(var entityobj in entities)
			{
				if(Physics.CheckSphere((Vector3)entityobj["position"], radius, copyLayer))
					return Lang("BLOCKING_PASTE", null);
			}

			return true;
		}

		private object cmdPasteBack(BasePlayer player, string[] args)
		{
			string userIDString = (player == null) ? serverID : player.UserIDString;

			if(args.Length < 1)
				return Lang("SYNTAX_PASTEBACK", userIDString);

			var success = TryPasteBack(args[0], player, args.Skip(1).ToArray());

			if(success is string)
				return (string)success;
			
			if(!lastPastes.ContainsKey(userIDString))
				lastPastes[userIDString] = new Stack<List<BaseEntity>>();
			
			lastPastes[userIDString].Push((List<BaseEntity>)success);

			return true;
		}

		private object cmdUndo(string userIDString, string[] args)
		{
			if(!lastPastes.ContainsKey(userIDString))
				return Lang("NO_PASTED_STRUCTURE", userIDString);

			foreach(var entity in lastPastes[userIDString].Pop())
			{
				if(entity == null || entity.IsDestroyed)
					continue;

				entity.Kill();
			}

			if(lastPastes[userIDString].Count == 0)
				lastPastes.Remove(userIDString);

			return true;
		}

		private object Copy(Vector3 sourcePos, Vector3 sourceRot, string filename, float RotationCorrection, CopyMechanics copyMechanics, float range, bool saveBuildings, bool saveDeployables, bool saveInventories, bool saveTree, bool saveShare)
		{
			var rawData = new List<object>();
			var copy = CopyProcess(sourcePos, sourceRot, RotationCorrection, range, saveBuildings, saveDeployables, saveInventories, saveTree, saveShare, copyMechanics);

			if(copy is string)
				return copy;

			rawData = copy as List<object>;

			var defaultData = new Dictionary<string, object>
			{
				{"position", new Dictionary<string, object>
					{
						{"x", sourcePos.x.ToString()  },
						{"y", sourcePos.y.ToString() },
						{"z", sourcePos.z.ToString() }
					}
				},
				{"rotationy", sourceRot.y.ToString() },
				{"rotationdiff", RotationCorrection.ToString() }
			};

			string path = subDirectory + filename;
			var CopyData = dataSystem.GetDatafile(path);

			CopyData.Clear();
			CopyData["default"] = defaultData;
			CopyData["entities"] = rawData;

			dataSystem.SaveDatafile(path);

			return true;
		}

		private object CopyProcess(Vector3 sourcePos, Vector3 sourceRot, float RotationCorrection, float range, bool saveBuildings, bool saveDeployables, bool saveInventories, bool saveTree, bool saveShare, CopyMechanics copyMechanics)
		{
			var rawData = new List<object>();
			var houseList = new List<BaseEntity>();
			var checkFrom = new List<Vector3> { sourcePos };
			uint buildingid = 0;
			int currentLayer = copyLayer;
			int current = 0;

			if(saveTree)
				currentLayer |= LayerMask.GetMask("Tree");

			while(current < checkFrom.Count)
			{
				List<BaseEntity> list = Pool.GetList<BaseEntity>();
				Vis.Entities<BaseEntity>(checkFrom[current], range, list, currentLayer);

				foreach(var entity in list)
				{
					if(!isValid(entity) || houseList.Contains(entity))
						continue;
				
					houseList.Add(entity);

					if(copyMechanics == CopyMechanics.Building)
					{
						BuildingBlock buildingblock = entity.GetComponentInParent<BuildingBlock>();

						if(buildingblock)
						{
							if(buildingid == 0)
								buildingid = buildingblock.buildingID;
							else if(buildingid != buildingblock.buildingID)
								continue;
						}
					}

					if(!checkFrom.Contains(entity.transform.position))
						checkFrom.Add(entity.transform.position);

					if(!saveBuildings && entity.GetComponentInParent<BuildingBlock>() != null)
						continue;

					if(!saveDeployables && (entity.GetComponentInParent<BuildingBlock>() == null && entity.GetComponent<BaseCombatEntity>() != null))
						continue;

					rawData.Add(EntityData(entity, sourcePos, sourceRot, entity.transform.position, entity.transform.rotation.ToEulerAngles(), RotationCorrection, saveInventories, saveShare));
				}

				current++;
			}

			return rawData;
		}
		
		private Dictionary<string, object> EntityData(BaseEntity entity, Vector3 sourcePos, Vector3 sourceRot, Vector3 entPos, Vector3 entRot, float diffRot, bool saveInventories, bool saveShare)
		{
			var normalizedPos = NormalizePosition(sourcePos, entPos, diffRot);
			var normalizedRot = entRot.y - diffRot;

			var data = new Dictionary<string, object>
			{
				{"prefabname", entity.PrefabName},
				{"skinid", entity.skinID},
				{"flags", TryCopyFlags(entity)},
				{"pos", new Dictionary<string,object>
					{
						{"x", normalizedPos.x.ToString()},
						{"y", normalizedPos.y.ToString()},
						{"z", normalizedPos.z.ToString()}
					}
				},
				{"rot", new Dictionary<string,object>
					{
						{"x", entRot.x.ToString()},
						{"y", normalizedRot.ToString()},
						{"z", entRot.z.ToString()},
					}
				}
			};
			
			TryCopySlots(entity, data, saveShare);

			var buildingblock = entity.GetComponentInParent<BuildingBlock>();

			if(buildingblock != null )
			{
				data.Add("grade", buildingblock.grade);
			}

			var box = entity.GetComponentInParent<StorageContainer>();

			if(box != null)
			{
				var itemlist = new List<object>();

				if(saveInventories)
				{
					foreach(Item item in box.inventory.itemList)
					{
						var itemdata = new Dictionary<string, object>
						{
							{"condition", item.condition.ToString() },
							{"id", item.info.itemid },
							{"amount", item.amount },
							{"skinid", item.skin },
							{"position", item.position },
						};

						var heldEnt = item.GetHeldEntity();

						if(heldEnt != null)
						{
							var projectiles = heldEnt.GetComponent<BaseProjectile>();

							if(projectiles != null)
							{
								var magazine = projectiles.primaryMagazine;

								if(magazine != null)
								{
									itemdata.Add("magazine", new Dictionary<string, object>
									{
										{ magazine.ammoType.itemid.ToString(), magazine.contents }
									});
								}
							}
						}

						if(item?.contents?.itemList != null)
						{
							var contents = new List<object>();

							foreach(Item itemContains in item.contents.itemList)
							{
								contents.Add(new Dictionary<string, object>
								{
									{"id", itemContains.info.itemid },
									{"amount", itemContains.amount },
								});
							}

							itemdata["items"] = contents;
						}

						itemlist.Add(itemdata);
					}
				}

				data.Add("items", itemlist);
			}

			var sign = entity.GetComponentInParent<Signage>();

			if(sign != null)
			{
				var imageByte = FileStorage.server.Get(sign.textureID, FileStorage.Type.png, sign.net.ID);

				data.Add("sign", new Dictionary<string, object>
				{
					{"locked", sign.IsLocked() }
				});

				if(sign.textureID > 0 && imageByte != null)
					((Dictionary<string, object>)data["sign"]).Add("texture", Convert.ToBase64String(imageByte));
			}

			if(saveShare)
			{
				var sleepingBag = entity.GetComponentInParent<SleepingBag>();

				if(sleepingBag != null)
				{
					data.Add("sleepingbag", new Dictionary<string, object>
					{
						{"niceName", sleepingBag.niceName },
						{"deployerUserID", sleepingBag.deployerUserID },
						{"isPublic", sleepingBag.IsPublic() },
					});
				}

				var cupboard = entity.GetComponentInParent<BuildingPrivlidge>();

				if(cupboard != null)
				{
					data.Add("cupboard", new Dictionary<string, object>
					{
						{"authorizedPlayers", cupboard.authorizedPlayers.Select(y => y.userid).ToList() }
					});
				}
			}

			var vendingMachine = entity.GetComponentInParent<VendingMachine>();
			
			if(vendingMachine != null)
			{				
				var sellOrders = new List<object>();
				
				foreach(var vendItem in vendingMachine.sellOrders.sellOrders)
				{
					sellOrders.Add(new Dictionary<string, object>
					{
						{"itemToSellID", vendItem.itemToSellID },
						{"itemToSellAmount", vendItem.itemToSellAmount },	
						{"currencyID", vendItem.currencyID },	
						{"currencyAmountPerItem", vendItem.currencyAmountPerItem },							
					});					
				}
				
				data.Add("vendingmachine", new Dictionary<string, object>
				{
					{"shopName", vendingMachine.shopName },
					{"isBroadcasting", vendingMachine.IsBroadcasting() },
					{"sellOrders", sellOrders}
				});
			}
			
			return data;
		}

		private object FindBestHeight(List<Dictionary<string,object>> entities, Vector3 startPos)
		{
			float maxHeight = 0f;

			foreach(var entity in entities)
			{
				if(((string)entity["prefabname"]).Contains("/foundation/"))
				{
					var foundHeight = GetGround((Vector3)entity["position"]);

					if(foundHeight != null)
					{
						var height = (Vector3)foundHeight;

						if(height.y > maxHeight)
							maxHeight = height.y;
					}
				}
			}

			maxHeight += 1f;

			return maxHeight;
		}

		private bool FindRayEntity(Vector3 sourcePos, Vector3 sourceDir, out Vector3 point, out BaseEntity entity, int rayLayer)
		{
			RaycastHit hitinfo;
			entity = null;
			point = Vector3.zero;

			if(!Physics.Raycast(sourcePos, sourceDir, out hitinfo, 1000f, rayLayer))
				return false;

			entity = hitinfo.GetEntity();
			point = hitinfo.point;

			return true;
		}

		private object GetGround(Vector3 pos)
		{
			RaycastHit hitInfo;

			if(Physics.Raycast(pos, Vector3.up, out hitInfo, groundLayer))
				return hitInfo.point;

			if(Physics.Raycast(pos, Vector3.down, out hitInfo, groundLayer))
				return hitInfo.point;

			return null;
		}

		private bool HasAccess(BasePlayer player, string permName)
		{
			return player.net.connection.authLevel > 1 || permission.UserHasPermission(player.UserIDString, permName);
		}

		private bool isValid(BaseEntity entity)
		{
			return (entity.GetComponentInParent<BuildingBlock>() != null || entity.GetComponentInParent<BaseCombatEntity>() != null || entity.GetComponentInParent<Spawnable>() != null);
		}

		private string Lang(string key, string userID = null, params object[] args) => string.Format(lang.GetMessage(key, this, userID), args);

		private Vector3 NormalizePosition(Vector3 InitialPos, Vector3 CurrentPos, float diffRot)
		{
			var transformedPos = CurrentPos - InitialPos;
			var newX = (transformedPos.x * (float)System.Math.Cos(-diffRot)) + (transformedPos.z * (float)System.Math.Sin(-diffRot));
			var newZ = (transformedPos.z * (float)System.Math.Cos(-diffRot)) - (transformedPos.x * (float)System.Math.Sin(-diffRot));

			transformedPos.x = newX;
			transformedPos.z = newZ;

			return transformedPos;
		}

		private List<BaseEntity> Paste(List<Dictionary<string,object>> entities, Vector3 startPos, BasePlayer player)
		{
			bool unassignid = true;
			uint buildingid = 0;
			var pastedEntities = new List<BaseEntity>();

			foreach(var data in entities)
			{
				var prefabname = (string)data["prefabname"];
				var skinid = ulong.Parse(data["skinid"].ToString());
				var pos = (Vector3)data["position"];
				var rot = (Quaternion)data["rotation"];

				bool isPlaced = false;

				List<BaseEntity> ents = new List<BaseEntity>();
				Vis.Entities<BaseEntity>(pos, 2f, ents);

				foreach(BaseEntity ent in ents)
				{
					if(ent.PrefabName == prefabname && ent.transform.position == pos && ent.transform.rotation == rot)
					{
						isPlaced = true;
						break;
					}
				}

				if(isPlaced)
					continue;

				var entity = GameManager.server.CreateEntity(prefabname, pos, rot, true);

				if(entity != null)
				{
					entity.transform.position = pos;
					entity.transform.rotation = rot;

					if(player != null)
					{
						entity.SendMessage("SetDeployedBy", player, SendMessageOptions.DontRequireReceiver);
						entity.OwnerID = player.userID;
					}

					var buildingblock = entity.GetComponentInParent<BuildingBlock>();

					if(buildingblock != null)
					{
						buildingblock.blockDefinition = PrefabAttribute.server.Find<Construction>(buildingblock.prefabID);
						buildingblock.SetGrade((BuildingGrade.Enum)data["grade"]);

						if(unassignid)
						{
							buildingid = BuildingBlock.NewBuildingID();
							unassignid = false;
						}

						buildingblock.buildingID = buildingid;
					}

					entity.skinID = skinid;
					entity.Spawn();

					var basecombat = entity.GetComponentInParent<BaseCombatEntity>();

					if(basecombat != null)
						basecombat.ChangeHealth(basecombat.MaxHealth());

					pastedEntities.AddRange(TryPasteSlots(entity, data));

					var box = entity.GetComponentInParent<StorageContainer>();

					if(box != null)
					{				
						Locker locker = box as Locker;
						
						if(locker != null)
							_equippingActive.SetValue(locker, true);
						
						box.inventory.Clear();
						
						var items = new List<object>();

						if(data.ContainsKey("items"))					
							items = data["items"] as List<object>;
						
						foreach(var itemDef in items)
						{
							var item = itemDef as Dictionary<string, object>;
							var itemid = Convert.ToInt32(item["id"]);
							var itemamount = Convert.ToInt32(item["amount"]);
							var itemskin = ulong.Parse(item["skinid"].ToString());
							var itemcondition = Convert.ToSingle(item["condition"]);

							var i = ItemManager.CreateByItemID(itemid, itemamount, itemskin);

							if(i != null)
							{
								i.condition = itemcondition;

								if(item.ContainsKey("magazine"))
								{
									var heldent = i.GetHeldEntity();

									if(heldent != null)
									{
										var projectiles = heldent.GetComponent<BaseProjectile>();

										if(projectiles != null)
										{
											var magazine = item["magazine"] as Dictionary<string, object>;
											var ammotype = int.Parse(magazine.Keys.ToArray()[0]);
											var ammoamount = int.Parse(magazine[ammotype.ToString()].ToString());

											projectiles.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
											projectiles.primaryMagazine.contents = ammoamount;
										}

										//TODO Не добавляет капли воды в некоторые контейнеры

										if(item.ContainsKey("items"))
										{
											var itemContainsList = item["items"] as List<object>;

											foreach(var itemContains in itemContainsList)
											{
												var contents = itemContains as Dictionary<string, object>;

												i.contents.AddItem(ItemManager.FindItemDefinition(Convert.ToInt32(contents["id"])), Convert.ToInt32(contents["amount"]));
											}
										}
									}
								}

								int targetPos = -1;
								
								if(item.ContainsKey("position"))
									targetPos = Convert.ToInt32(item["position"]);
								
								i.MoveToContainer(box.inventory, targetPos);
							}
						}
						
						if(locker != null)
							_equippingActive.SetValue(locker, false);				
					}
					 
					var sign = entity.GetComponentInParent<Signage>();

					if(sign != null && data.ContainsKey("sign"))
					{
						var signData = data["sign"] as Dictionary<string, object>;

						if(signData.ContainsKey("texture"))
						{
							var stringSign = Convert.FromBase64String(signData["texture"].ToString());
							sign.textureID = FileStorage.server.Store(stringSign, FileStorage.Type.png, sign.net.ID);
						}

						if(Convert.ToBoolean(signData["locked"]))
							sign.SetFlag(BaseEntity.Flags.Locked, true);

						sign.SendNetworkUpdate();
					}

					var sleepingBag = entity.GetComponentInParent<SleepingBag>();

					if(sleepingBag != null && data.ContainsKey("sleepingbag"))
					{
						var bagData = data["sleepingbag"] as Dictionary<string, object>;

						sleepingBag.niceName = bagData["niceName"].ToString();
						sleepingBag.deployerUserID = ulong.Parse(bagData["deployerUserID"].ToString());
						sleepingBag.SetPublic(Convert.ToBoolean(bagData["isPublic"]));
					}

					var cupboard = entity.GetComponentInParent<BuildingPrivlidge>();

					if(cupboard != null)
					{
						List<ulong> authorizedPlayers = new List<ulong>();

						if(data.ContainsKey("cupboard"))
						{
							var cupboardData = data["cupboard"] as Dictionary<string, object>;
							authorizedPlayers = (cupboardData["authorizedPlayers"] as List<object>).Select(y => Convert.ToUInt64(y)).ToList();
						}

						if(data.ContainsKey("auth") && player != null && !authorizedPlayers.Contains(player.userID))
							authorizedPlayers.Add(player.userID);

						foreach(var userID in authorizedPlayers)
						{
							cupboard.authorizedPlayers.Add(new PlayerNameID()
							{
								userid = Convert.ToUInt64(userID),
								username = "Player"
							});
						}

						//cupboard.UpdateAllPlayers();
						cupboard.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
					}
					
					var vendingMachine = entity.GetComponentInParent<VendingMachine>();

					if(vendingMachine != null && data.ContainsKey("vendingmachine"))
					{						
						var vendingData = data["vendingmachine"] as Dictionary<string, object>;
						
						vendingMachine.shopName = vendingData["shopName"].ToString();
						vendingMachine.SetFlag(BaseEntity.Flags.Reserved4, Convert.ToBoolean(vendingData["isBroadcasting"]));
						
						var sellOrders = vendingData["sellOrders"] as List<object>;

						foreach(var orderPreInfo in sellOrders)
						{				
							var orderInfo = orderPreInfo as Dictionary<string, object>;
							
							vendingMachine.sellOrders.sellOrders.Add(new ProtoBuf.VendingMachine.SellOrder()
							{
								ShouldPool = false,
								itemToSellID = Convert.ToInt32(orderInfo["itemToSellID"]),
								itemToSellAmount = Convert.ToInt32(orderInfo["itemToSellAmount"]),
								currencyID = Convert.ToInt32(orderInfo["currencyID"]),
								currencyAmountPerItem = Convert.ToInt32(orderInfo["currencyAmountPerItem"])
							});  
						}	
											
						vendingMachine.FullUpdate();
					}
					
					var flags = data.ContainsKey("flags") ? data["flags"] as Dictionary<string, object> : new Dictionary<string, object>();
					
					//TODO Проверить необходимость выполнения дополнительных действий
					
					var baseOven = entity.GetComponentInParent<BaseOven>();
					
					if(baseOven != null)
					{
						if(flags.ContainsKey("On") && Convert.ToBoolean(flags["On"]))
							baseOven.StartCooking();
					}
					
					pastedEntities.Add(entity);
				}
			}

			return pastedEntities;
		}
		
		//TODO
		
		private void PastebinSend(BasePlayer player, string name, string text, string api_dev_key = "a30aa0c4ac9c29b8b2a016114023a687")
		{
            webrequest.Enqueue("https://pastebin.com/api/api_post.php", $"api_option=paste&api_paste_private=1&api_paste_name={name}&api_paste_expire_date=N&api_paste_format=json&api_dev_key={api_dev_key}&api_paste_code={text}", (code, response) =>
            {
                if(!(response == null && code == 200))			
                    SendReply(player, Lang("LINK_ON_BUILDING", player.UserIDString) + response);						
            }, this, RequestMethod.POST);
		}
		
		private List<Dictionary<string, object>> PreLoadData(List<object> entities, Vector3 startPos, float RotationCorrection, bool deployables, bool inventories, bool auth, bool vending)
		{
			var eulerRotation = new Vector3(0f, RotationCorrection, 0f);
			var quaternionRotation = Quaternion.EulerRotation(eulerRotation);
			var preloaddata = new List<Dictionary<string, object>>();

			foreach(var entity in entities)
			{
				var data = entity as Dictionary<string, object>;

				if(!deployables && !data.ContainsKey("grade"))
					continue;

				var pos = (Dictionary<string, object>)data["pos"];
				var rot = (Dictionary<string, object>)data["rot"];
				var fixedRotation = Quaternion.EulerRotation(eulerRotation + new Vector3(Convert.ToSingle(rot["x"]), Convert.ToSingle(rot["y"]), Convert.ToSingle(rot["z"])));
				var tempPos = quaternionRotation * (new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]), Convert.ToSingle(pos["z"])));
				Vector3 newPos = tempPos + startPos;

				data.Add("position", newPos);
				data.Add("rotation", fixedRotation);

				if(!inventories && data.ContainsKey("items"))
					data["items"] = new List<object>();

				if(auth && data["prefabname"].ToString().Contains("cupboard.tool"))
					data["auth"] = true;

				if(!vending && data["prefabname"].ToString().Contains("vendingmachine"))
					data.Remove("vendingmachine");
				
				preloaddata.Add(data);
			}

			return preloaddata;
		}

		private object TryCopy(Vector3 sourcePos, Vector3 sourceRot, string filename, float RotationCorrection, string[] args)
		{
			bool saveBuildings = config.Copy.Buildings, saveDeployables = config.Copy.Deployables, saveInventories = config.Copy.Inventories, saveShare = config.Copy.Share, saveTree = config.Copy.Tree;
			CopyMechanics copyMechanics = CopyMechanics.Proximity;
			float radius = 3f;

			for(int i = 0; ; i = i + 2)
			{
				if(i >= args.Length)
					break;

				int valueIndex = i + 1;

				if(valueIndex >= args.Length)
					return Lang("SYNTAX_COPY", null);

				string param = args[i].ToLower();

				switch(param)
				{
					case "b":
					case "buildings":
						if(!bool.TryParse(args[valueIndex], out saveBuildings))
							return Lang("SYNTAX_BOOL", null, param);

						break;
					case "d":
					case "deployables":
						if(!bool.TryParse(args[valueIndex], out saveDeployables))
							return Lang("SYNTAX_BOOL", null, param);

						break;
					case "i":
					case "inventories":
						if(!bool.TryParse(args[valueIndex], out saveInventories))
							return Lang("SYNTAX_BOOL", null, param);

						break;
					case "m":
					case "method":
						switch(args[valueIndex].ToLower())
						{
							case "b":
							case "building":
								copyMechanics = CopyMechanics.Building;
								break;
							case "p":
							case "proximity":
								copyMechanics = CopyMechanics.Proximity;
								break;
						}

						break;
					case "r":
					case "radius":
						if(!float.TryParse(args[valueIndex], out radius))
							return Lang("SYNTAX_RADIUS", null);

						break;
					case "s":
					case "share":
						if(!bool.TryParse(args[valueIndex], out saveShare))
							return Lang("SYNTAX_BOOL", null, param);

						break;
					case "t":
					case "tree":
						if(!bool.TryParse(args[valueIndex], out saveTree))
							return Lang("SYNTAX_BOOL", null, param);

						break;
					default:
						return Lang("SYNTAX_COPY", null);
				}
			}

			return Copy(sourcePos, sourceRot, filename, RotationCorrection, copyMechanics, radius, saveBuildings, saveDeployables, saveInventories, saveTree, saveShare);
		}

		private void TryCopySlots(BaseEntity ent, IDictionary<string, object> housedata, bool saveShare)
		{
			foreach(BaseEntity.Slot slot in checkSlots)
			{
				if(!ent.HasSlot(slot))
					continue;

				var slotEntity = ent.GetSlot(slot);

				if(slotEntity == null)
					continue;

				var codedata = new Dictionary<string, object>
				{
					{"prefabname", slotEntity.PrefabName},
					{"flags", TryCopyFlags(ent)}
				};

				if(slotEntity.GetComponent<CodeLock>())
				{
					CodeLock codeLock = slotEntity.GetComponent<CodeLock>();

					codedata.Add("code", codeLock.code);
					
					if(saveShare)			
						codedata.Add("whitelistPlayers", codeLock.whitelistPlayers);
					
					if(codeLock.guestCode != null && codeLock.guestCode.Length == 4)
					{
						codedata.Add("guestCode", codeLock.guestCode);
					
						if(saveShare)
							codedata.Add("guestPlayers", codeLock.guestPlayers);
					}											
				} else if(slotEntity.GetComponent<KeyLock>()) {
					KeyLock keyLock = slotEntity.GetComponent<KeyLock>();
					var code = keyLock.keyCode;

					if(keyLock.firstKeyCreated)
						code |= 0x80;

					codedata.Add("code", code.ToString());
				}

				string slotName = slot.ToString().ToLower();

				housedata.Add(slotName, codedata);
			}
		}
		
		private Dictionary<string, object> TryCopyFlags(BaseEntity entity)
		{
			var flags = new Dictionary<string, object>();
			
			foreach(BaseEntity.Flags flag in Enum.GetValues(typeof(BaseEntity.Flags)))
			{
				flags.Add(flag.ToString(), entity.HasFlag(flag));
			}

			return flags;
		}
		
		private object TryPaste(Vector3 startPos, string filename, BasePlayer player, float RotationCorrection, string[] args, bool autoHeight = true)
		{
			var userID = player?.UserIDString;

			string path = subDirectory + filename;

			if(!dataSystem.ExistsDatafile(path))
				return Lang("FILE_NOT_EXISTS", userID);

			var data = dataSystem.GetDatafile(path);

			if(data["default"] == null || data["entities"] == null)
				return Lang("FILE_BROKEN", userID);

			float heightAdj = 0f, blockCollision = 0f;
			bool  auth = config.Paste.Auth, inventories = config.Paste.Inventories, deployables = config.Paste.Deployables, vending = config.Paste.VendingMachines;

			for(int i = 0; ; i = i + 2)
			{
				if(i >= args.Length)
					break;

				int valueIndex = i + 1;

				if(valueIndex >= args.Length)
					return Lang("SYNTAX_PASTE_OR_PASTEBACK", userID);

				string param = args[i].ToLower();

				switch(param)
				{
					case "a":
					case "auth":
						if(!bool.TryParse(args[valueIndex], out auth))
							return Lang("SYNTAX_BOOL", userID, param);

						break;
					case "b":
					case "blockcollision":
						if(!float.TryParse(args[valueIndex], out blockCollision))
							return Lang("SYNTAX_BLOCKCOLLISION", userID);

						break;
					case "d":
					case "deployables":
						if(!bool.TryParse(args[valueIndex], out deployables))
							return Lang("SYNTAX_BOOL", userID, param);

						break;
					case "height":
						if(!float.TryParse(args[valueIndex], out heightAdj))
							return Lang("SYNTAX_HEIGHT", userID);

						autoHeight = false;

						break;
					case "i":
					case "inventories":
						if(!bool.TryParse(args[valueIndex], out inventories))
							return Lang("SYNTAX_BOOL", userID, param);

						break;
					case "v":
					case "vending":
						if(!bool.TryParse(args[valueIndex], out vending))
							return Lang("SYNTAX_BOOL", userID, param);

						break;						
					default:
						return Lang("SYNTAX_PASTE_OR_PASTEBACK", userID);
				}
			}

			startPos.y += heightAdj;

			var preloadData = PreLoadData(data["entities"] as List<object>, startPos, RotationCorrection, deployables, inventories, auth, vending);

			if(autoHeight)
			{
				var bestHeight = FindBestHeight(preloadData, startPos);

				if(bestHeight is string)
					return bestHeight;

				heightAdj = (float)bestHeight - startPos.y;

				foreach(var entity in preloadData)
				{
					var pos = ((Vector3)entity["position"]);
					pos.y += heightAdj;
					entity["position"] = pos;
				}
			}

			if(blockCollision > 0f)
			{
				var collision = CheckCollision(preloadData, startPos, blockCollision);

				if(collision is string)
					return collision;
			}

			return Paste(preloadData, startPos, player);
		}

		private List<BaseEntity> TryPasteSlots(BaseEntity ent, Dictionary<string, object> structure)
		{
			List<BaseEntity> entitySlots = new List<BaseEntity>();
			
			foreach(BaseEntity.Slot slot in checkSlots)
			{
				string slotName = slot.ToString().ToLower();

				if(!ent.HasSlot(slot) || !structure.ContainsKey(slotName))
					continue;

				var slotData = structure[slotName] as Dictionary<string, object>;
				BaseEntity slotEntity = GameManager.server.CreateEntity((string)slotData["prefabname"], Vector3.zero, new Quaternion(), true);

				if(slotEntity == null)
					continue;

				slotEntity.gameObject.Identity();
				slotEntity.SetParent(ent, slotName);
				slotEntity.OnDeployed(ent);
				slotEntity.Spawn();

				ent.SetSlot(slot, slotEntity);

				entitySlots.Add(slotEntity);
				
				if(slotName == "lock" && slotData.ContainsKey("code"))
				{
					if(slotEntity.GetComponent<CodeLock>())
					{
						string code = (string)slotData["code"];

						if(!string.IsNullOrEmpty(code))
						{
							CodeLock codeLock = slotEntity.GetComponent<CodeLock>();
							codeLock.code = code;					
							_hasCode.SetValue(codeLock, true);
							
							if(slotData.ContainsKey("whitelistPlayers"))
							{
								foreach(var userID in slotData["whitelistPlayers"] as List<object>)
								{
									codeLock.whitelistPlayers.Add(Convert.ToUInt64(userID));
								}
							}
							
							if(slotData.ContainsKey("guestCode"))
							{
								string guestCode = (string)slotData["guestCode"];
								
								codeLock.guestCode = guestCode;
								_hasGuestCode.SetValue(codeLock, true);
								
								if(slotData.ContainsKey("guestPlayers"))
								{
									foreach(var userID in slotData["guestPlayers"] as List<object>)
									{
										codeLock.guestPlayers.Add(Convert.ToUInt64(userID));
									}
								}
							}
							
							codeLock.SetFlag(BaseEntity.Flags.Locked, true);
						}
					} else if(slotEntity.GetComponent<KeyLock>()) {
						int code = Convert.ToInt32(slotData["code"]);
						KeyLock keyLock = slotEntity.GetComponent<KeyLock>();

						if((code & 0x80) != 0)
						{
							keyLock.keyCode = (code & 0x7F);
							keyLock.firstKeyCreated = true;
							keyLock.SetFlag(BaseEntity.Flags.Locked, true);
						}
					}
				}
			}
			
			return entitySlots;
		}

		private object TryPasteBack(string filename, BasePlayer player, string[] args)
		{
			string path = subDirectory + filename;

			if(!dataSystem.ExistsDatafile(path))
				return Lang("FILE_NOT_EXISTS", player?.UserIDString);

			var data = dataSystem.GetDatafile(path);

			if(data["default"] == null || data["entities"] == null)
				return Lang("FILE_BROKEN", player?.UserIDString);

			var defaultdata = data["default"] as Dictionary<string, object>;
			var pos = defaultdata["position"] as Dictionary<string, object>;
			var rotationCorrection = Convert.ToSingle(defaultdata["rotationdiff"]);
			var startPos = new Vector3(Convert.ToSingle(pos["x"]), Convert.ToSingle(pos["y"]), Convert.ToSingle(pos["z"]));

			return TryPaste(startPos, filename, player, rotationCorrection, args, autoHeight: false);
		}

		//Сhat commands
		
		[ChatCommand("copy")]
		private void cmdChatCopy(BasePlayer player, string command, string[] args)
		{
			if(!HasAccess(player, copyPermission))
			{
				SendReply(player, Lang("NO_ACCESS", player.UserIDString));
				return;
			}

			if(args.Length < 1)
			{
				SendReply(player, Lang("SYNTAX_COPY", player.UserIDString));
				return;
			}

			var savename = args[0];
			var success = TryCopyFromSteamID(player.userID, savename, args.Skip(1).ToArray());

			if(success is string)
			{
				SendReply(player, (string)success);
				return;
			}

			SendReply(player, Lang("COPY_SUCCESS", player.UserIDString, savename));
		}

		[ChatCommand("paste")]
		private void cmdChatPaste(BasePlayer player, string command, string[] args)
		{
			if(!HasAccess(player, pastePermission))
			{
				SendReply(player, Lang("NO_ACCESS", player.UserIDString));
				return;
			}

			if(args.Length < 1)
			{
				SendReply(player, Lang("SYNTAX_PASTE_OR_PASTEBACK", player.UserIDString));
				return;
			}

			var success = TryPasteFromSteamID(player.userID, args[0], args.Skip(1).ToArray());

			if(success is string)
			{
				SendReply(player, (string)success);
				return;
			}
			
			if(!lastPastes.ContainsKey(player.UserIDString))
				lastPastes[player.UserIDString] = new Stack<List<BaseEntity>>();
			
			lastPastes[player.UserIDString].Push((List<BaseEntity>)success);

			SendReply(player, Lang("PASTE_SUCCESS", player.UserIDString));
		}

		[ChatCommand("pasteback")]
		private void cmdChatPasteBack(BasePlayer player, string command, string[] args)
		{
			if(!HasAccess(player, pastePermission))
			{
				SendReply(player, Lang("NO_ACCESS", player.UserIDString));
				return;
			}

			var result = cmdPasteBack(player, args);

			if(result is string)
				SendReply(player, (string)result);
			else
				SendReply(player, Lang("PASTEBACK_SUCCESS", player.UserIDString));
		}

		[ChatCommand("undo")]
		private void cmdChatUndo(BasePlayer player, string command, string[] args)
		{
			if(!HasAccess(player, undoPermission))
			{
				SendReply(player, Lang("NO_ACCESS", player.UserIDString));
				return;
			}

			var result = cmdUndo(player.UserIDString, args);

			if(result is string)
				SendReply(player, (string)result);
			else
				SendReply(player, Lang("UNDO_SUCCESS", player.UserIDString));
		}
		
		//Console commands [From Server]

		[ConsoleCommand("pasteback")]
		private void cmdConsolePasteBack(ConsoleSystem.Arg arg)
		{
			if(!arg.IsRcon)
				return;

			var result = cmdPasteBack(null, arg.Args);

			if(result is string)
				SendReply(arg, (string)result);
			else
				SendReply(arg, Lang("PASTEBACK_SUCCESS", null));
		}

		[ConsoleCommand("undo")]
		private void cmdConsoleUndo(ConsoleSystem.Arg arg)
		{
			if(!arg.IsRcon)
				return;

			var result = cmdUndo(serverID, arg.Args);

			if(result is string)
				SendReply(arg, (string)result);
			else
				SendReply(arg, Lang("UNDO_SUCCESS", null));
		}

		//Languages phrases

		private readonly Dictionary<string, Dictionary<string, string>> messages = new Dictionary<string, Dictionary<string, string>>
		{
			{"FILE_NOT_EXISTS", new Dictionary<string, string>() {
				{"en", "File does not exist"},
				{"ru", "Файл не существует"},
			}},
			{"FILE_BROKEN", new Dictionary<string, string>() {
				{"en", "File is broken, can not be paste"},
				{"ru", "Файл поврежден, вставка невозможна"},
			}},
			{"NO_ACCESS", new Dictionary<string, string>() {
				{"en", "You don't have the permissions to use this command"},
				{"ru", "У вас нет прав доступа к данной команде"},
			}},
			{"SYNTAX_PASTEBACK", new Dictionary<string, string>() {
				{"en", "Syntax: /pasteback <Target Filename> <options values>\nheight XX - Adjust the height\nvending - Information and sellings in vending machine"},
				{"ru", "Синтаксис: /pasteback <Название Объекта> <опция значение>\nheight XX - Высота от земли\nvending - Информация и товары в торговом автомате"},
			}},
			{"SYNTAX_PASTE_OR_PASTEBACK", new Dictionary<string, string>() {
				{"en", "Syntax: /paste or /pasteback <Target Filename> <options values>\nheight XX - Adjust the height\nautoheight true/false - sets best height, carefull of the steep\nblockcollision XX - blocks the entire paste if something the new building collides with something\ndeployables true/false - false to remove deployables\ninventories true/false - false to ignore inventories\nvending - Information and sellings in vending machine"},
				{"ru", "Синтаксис: /paste or /pasteback <Название Объекта> <опция значение>\nheight XX - Высота от земли\nautoheight true/false - автоматически подобрать высоту от земли\nblockcollision XX - блокировать вставку, если что-то этому мешает\ndeployables true/false - false для удаления предметов\ninventories true/false - false для игнорирования копирования инвентаря\nvending - Информация и товары в торговом автомате"},
			}},
			{"PASTEBACK_SUCCESS", new Dictionary<string, string>() {
				{"en", "You've successfully placed back the structure"},
				{"ru", "Постройка успешно вставлена на старое место"},
			}},
			{"PASTE_SUCCESS", new Dictionary<string, string>() {
				{"en", "You've successfully pasted the structure"},
				{"ru", "Постройка успешно вставлена"},
			}},
			{"SYNTAX_COPY", new Dictionary<string, string>() {
				{"en", "Syntax: /copy <Target Filename> <options values>\n radius XX (default 3)\n method proximity/building (default proximity)\nbuilding true/false (saves structures or not)\ndeployables true/false (saves deployables or not)\ninventories true/false (saves inventories or not)"},
				{"ru", "Синтаксис: /copy <Название Объекта> <опция значение>\n radius XX (default 3)\n method proximity/building (по умолчанию proximity)\nbuilding true/false (сохранять постройку или нет)\ndeployables true/false (сохранять предметы или нет)\ninventories true/false (сохранять инвентарь или нет)"},
			}},
			{"NO_ENTITY_RAY", new Dictionary<string, string>() {
				{"en", "Couldn't ray something valid in front of you"},
				{"ru", "Не удалось найти какой-либо объект перед вами"},
			}},
			{"COPY_SUCCESS", new Dictionary<string, string>() {
				{"en", "The structure was successfully copied as {0}"},
				{"ru", "Постройка успешно скопирована под названием: {0}"},
			}},
			{"NO_PASTED_STRUCTURE", new Dictionary<string, string>() {
				{"en", "You must paste structure before undoing it"},
				{"ru", "Вы должны вставить постройку перед тем, как отменить действие"},
			}},
			{"UNDO_SUCCESS", new Dictionary<string, string>() {
				{"en", "You've successfully undid what you pasted"},
				{"ru", "Вы успешно снесли вставленную постройку"},
			}},
			{"NOT_FOUND_PLAYER", new Dictionary<string, string>() {
				{"en", "Couldn't find the player"},
				{"ru", "Не удалось найти игрока"},
			}},
			{"SYNTAX_BOOL", new Dictionary<string, string>() {
				{"en", "Option {0} must be true/false"},
				{"ru", "Опция {0} принимает значения true/false"},
			}},
			{"SYNTAX_HEIGHT", new Dictionary<string, string>() {
				{"en", "Option height must be a number"},
				{"ru", "Опция height принимает только числовые значения"},
			}},
			{"SYNTAX_BLOCKCOLLISION", new Dictionary<string, string>() {
				{"en", "Option blockcollision must be a number, 0 will deactivate the option"},
				{"ru", "Опция blockcollision принимает только числовые значения, 0 позволяет отключить проверку"},
			}},
			{"SYNTAX_RADIUS", new Dictionary<string, string>() {
				{"en", "Option radius must be a number"},
				{"ru", "Опция radius принимает только числовые значения"},
			}},
			{"BLOCKING_PASTE", new Dictionary<string, string>() {
				{"en", "Something is blocking the paste"},
				{"ru", "Что-то препятствует вставке"},
			}},
			{"LINK_ON_BUILDING", new Dictionary<string, string>() {
				{"en", "Link on building: "},
				{"ru", "Ссылка на постройку: "},
			}},			
		};
	}
}