using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("RestoreUponDeath", "k1lly0u", "0.1.0", ResourceId = 0)]
    public class RestoreUponDeath : HideHoldOutPlugin
    {
        NetworkController nc = UnityEngine.Object.FindObjectOfType<NetworkController>();

        private static readonly FieldInfo ChatNetViewField = typeof(ChatManager).GetField("Chat_NetView", BindingFlags.NonPublic | BindingFlags.Instance);
        public static uLink.NetworkView ChatNetView = ChatNetViewField.GetValue(NetworkController.NetManager_.chatManager) as uLink.NetworkView;

        RODData rodData;
        private DynamicConfigFile PlayerInvData;

        private Dictionary<string, List<ItemDef>> playerInv;
        #region Oxide Hooks
        void Loaded() => PlayerInvData = Interface.Oxide.DataFileSystem.GetFile("restoreupondeath_data");
        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();

            lang.RegisterMessages(new Dictionary<string, string>
                    {
                        {"addSyn", "<color=#C4FF00>/rod add <permission> <percentage></color> - Adds a new permission and percentage" },
                        {"remSyn",  "<color=#C4FF00>/rod remove <permission></color> - Remove a permission"},
                        {"addSuccess", "You have successfully added the permission <color=#C4FF00>{0}</color> that has a loss percentage of <color=#C4FF00>{1}</color>" },
                        {"invNum", "You must enter a valid percentage number" },
                        {"exists", "That permission already exists" },
                        {"remSuccess", "You have successfully remove the permission <color=#C4FF00>{0}</color>" },
                        {"noExist", "The permission <color=#C4FF00>{0}</color> does not exist" },
                        {"currentPerms", "Current permissions;" },
                        {"noPerms", "<color=#C4FF00>There are currently no permissions set up</color>" },
                        {"listSyn", "<color=#C4FF00>/rod list</color> - Lists all permissions and assigned loss percentage" }

                    }, this);

            foreach (var perm in rodData.Permissions)
                permission.RegisterPermission(perm.Key, this);

            playerInv = new Dictionary<string, List<ItemDef>>();
            foreach (var entry in rodData.Inventorys)
                playerInv.Add(entry.Key, entry.Value);
            rodData.Inventorys.Clear();

            timer.Once(900, () => SaveLoop());
        }
        void OnPlayerRespawned(PlayerInfos player) => RestoreInventory(player);
        void OnPlayerDeath(PlayerInfos player)
        {
            if (player != null)
            {
                SaveInventory(player.account_id);
                ProcessInventory(player);
            }            
        }
        void Unload() => SaveData();
        private void SendReply(PlayerInfos player, string msg) => ChatNetView.RPC("NET_Receive_msg", player.NetPlayer, new object[] { "\r\n" + msg, chat_msg_type.standard, player.account_id });

        #endregion

        #region ChatCommands
        [ChatCommand("rod")]
        private void cmdRod(PlayerInfos player, string command, string[] args)
        {
            if (player.isADMIN)
            {
                if (args == null || args.Length == 0)
                {
                    SendReply(player, lang.GetMessage("addSyn", this, player.account_id));
                    SendReply(player, lang.GetMessage("remSyn", this, player.account_id));
                    SendReply(player, lang.GetMessage("listSyn", this, player.account_id));
                    return;
                }
                if (args.Length >= 1)
                {
                    switch (args[0].ToLower())
                    {
                        case "add":
                            if (args.Length == 3)
                            {
                                string perm = args[1].ToLower();
                                if (!perm.StartsWith(Title.ToLower() + "."))
                                    perm = Title.ToLower() + "." + perm;
                                if (!permission.PermissionExists(perm) && !rodData.Permissions.ContainsKey(perm))
                                {
                                    int percentage = 0;
                                    if (int.TryParse(args[2], out percentage))
                                    {
                                        rodData.Permissions.Add(perm, percentage);
                                        permission.RegisterPermission(perm, this);
                                        SaveData();
                                        SendReply(player, string.Format(lang.GetMessage("addSuccess", this, player.account_id), perm, percentage));
                                        return;
                                    }
                                    SendReply(player, lang.GetMessage("invNum", this, player.account_id));
                                    return;
                                }
                                SendReply(player, lang.GetMessage("exists", this, player.account_id));
                                return;
                            }
                            SendReply(player, lang.GetMessage("addSyn", this, player.account_id));
                            return;
                        case "remove":
                            if (args.Length >= 2)
                                if (rodData.Permissions.ContainsKey(args[1].ToLower()))
                                {
                                    rodData.Permissions.Remove(args[1].ToLower());
                                    SaveData();
                                    SendReply(player, string.Format(lang.GetMessage("remSuccess", this, player.account_id), args[1].ToLower()));
                                    return;
                                }
                            SendReply(player, string.Format(lang.GetMessage("noExist", this, player.account_id), args[1].ToLower()));
                            return;
                        case "list":
                            if (rodData.Permissions.Count > 0)
                            {
                                SendReply(player, lang.GetMessage("currentPerms", this, player.account_id));
                                foreach (var entry in rodData.Permissions)
                                    SendReply(player, $"{entry.Key} -- {entry.Value}%");
                                return;
                            }
                            SendReply(player, lang.GetMessage("noPerms", this, player.account_id));
                            return;
                    }
                }
            }
        }
        #endregion

        #region Functions
        private int GetPercentage(string playerid)
        {
            int percentage = configData.DefaultPercentageOfItemsKept;
            foreach (var entry in rodData.Permissions)
            {
                if (permission.UserHasPermission(playerid.ToString(), entry.Key))
                {
                    percentage = entry.Value;
                    break;
                }
            }
            return percentage;
        }  
        private List<ItemDef> GetItems(string inventory)
        {
            List<ItemDef> Items = new List<ItemDef>();
            string[] pieces = inventory.Replace("_", ":").Split(':');
            for (int i = 0; i < pieces.Length; i++)
            {
                ItemDef Item = new ItemDef();
                Item.ItemID = pieces[i];
                Item.Amount = pieces[i + 1];
                Item.Ammo = pieces[i + 2];
                if (Item.ItemID != "0")
                    Items.Add(Item);
                i = i + 2;
            }            
            return Items;
        }
        private void ProcessInventory(PlayerInfos player)
        {
            var items = GetItems(player.inventory);
            string ID = player.account_id;
            var percentage = GetPercentage(ID);
            if (percentage > 0)
            {
                double amount = (float)(items.Count * percentage) / 100;
                if (percentage == 100) amount = items.Count;
                var keepList = new List<ItemDef>();
                for (int i = 0; i < amount; i++)
                {
                    var num = UnityEngine.Random.Range(0, items.Count);
                    var item = items[num];
                    keepList.Add(item);
                    items.Remove(item);
                };
                playerInv[ID] = keepList;

                timer.Once(3, () => ReplaceCorpseInventory(player, items));
            }
        }
        private void ReplaceCorpseInventory(PlayerInfos player, List<ItemDef> Items)
        {    
            var corpses = UnityEngine.Object.FindObjectsOfType<Ragdoll_SYNC>();
            foreach (var c in corpses)
            if (c.LOOT_Container.CONTEXT_name.Contains(player.Nickname + "'s Body"))
                {
                    c.LOOT_Container.CONTEXT_name = player.Nickname + "'s Old Body";
                    c.LOOT_Container.CONTENT_str = CreateInventory(Items);
                }
        }
        private string CreateInventory(List<ItemDef> items)
        {
            string inventory = "";
            int count = 1;

            for (int i = 0; i < items.Count; i++)
            {
                inventory = inventory + $"{items[i].ItemID}_{items[i].Amount}_{items[i].Ammo}:";
                count++;
            }           
            return inventory;
        }
        private void SaveInventory(string playerid)
        {
            var Items = new List<ItemDef>();
            if (!playerInv.ContainsKey(playerid))
                playerInv.Add(playerid, Items);
            else playerInv[playerid] = Items;
        }
        private void RestoreInventory(PlayerInfos player)
        {
            List<ItemDef> items = new List<ItemDef>();
            if (playerInv.ContainsKey(player.account_id))
            {
                items = playerInv[player.account_id];
                GivePlayerInventory(player, items);
                playerInv.Remove(player.account_id);
            }
        }
        
        #endregion

        #region Give
        private void GivePlayerInventory(PlayerInfos player, List<ItemDef> items)
        {
            foreach (var item in items)
            {
                int ID = int.Parse(item.ItemID);
                int Amount = int.Parse(item.Amount);
                if (ID != 0)
                    nc.NetView.RPC("NET_ADMIN_ReceiveItem", player.NetPlayer, new object[] { ID, Amount });
            }
            playerInv.Remove(player.account_id);
        }       
        #endregion

        #region Classes
        class RODData
        {
            public Dictionary<string, List<ItemDef>> Inventorys = new Dictionary<string, List<ItemDef>>();
            public Dictionary<string, int> Permissions = new Dictionary<string, int>();
        }
        class ItemDef
        {
            public string ItemID;
            public string Amount;
            public string Ammo;
        }
        #endregion

        #region Data Management
        void SaveData()
        {
            rodData.Inventorys = playerInv;
            PlayerInvData.WriteObject(rodData);
        }
        private void SaveLoop()
        {
            SaveData();
            timer.Once(900, () => SaveLoop());
        }
        void LoadData()
        {
            try
            {
                rodData = PlayerInvData.ReadObject<RODData>();
            }
            catch
            {
                Puts("Couldn't load data, creating new datafile");
                rodData = new RODData();
            }
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            public int DefaultPercentageOfItemsKept { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                DefaultPercentageOfItemsKept = 25
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}
