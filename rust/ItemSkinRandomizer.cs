// Reference: Rust.Workshop

using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Facepunch.Steamworks;
using Rust;

namespace Oxide.Plugins
{
    [Info("Item Skin Randomizer", "Mughisi", "1.2.1", ResourceId = 1328)]
    [Description("Simple plugin that will select a random skin for an item when crafting.")]
    class ItemSkinRandomizer : RustPlugin
    {
        private readonly Dictionary<string, List<int>> skinsCache = new Dictionary<string, List<int>>();
        private readonly List<int> randomizedTasks = new List<int>();

        private void OnServerInitialized()
        {
            webrequest.EnqueueGet("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", (code, response) =>
            {
                if (!(response == null && code == 200))
                {
                    var schema = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                    var defs = new List<Inventory.Definition>();
                    foreach (var item in schema.items)
                    {
                        if (item.itemshortname == string.Empty) continue;
                        var steamItem = Global.SteamServer.Inventory.CreateDefinition((int) item.itemdefid);
                        steamItem.Name = item.name;
                        steamItem.SetProperty("itemshortname", item.itemshortname);
                        steamItem.SetProperty("workshopid", item.workshopid.ToString());
                        steamItem.SetProperty("workshopdownload", item.workshopdownload);
                        defs.Add(steamItem);
                    }

                    Global.SteamServer.Inventory.Definitions = defs.ToArray();

                    foreach (var item in ItemManager.itemList)
                        item.skins2 =
                            Global.SteamServer.Inventory.Definitions.Where(
                                x =>
                                    (x.GetStringProperty("itemshortname") == item.shortname) &&
                                    !string.IsNullOrEmpty(x.GetStringProperty("workshopdownload"))).ToArray();

                    Puts($"Loaded {Global.SteamServer.Inventory.Definitions.Length} approved workshop skins.");
                }
                else
                {
                    PrintWarning($"Failed to load approved workshop skins... Error {code}");
                }
            }, this);
        }

        private void OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            var skins = GetSkins(task.blueprint.targetItem);
            if (skins.Count < 1 || task.skinID != 0) return;
            randomizedTasks.Add(task.taskUID);
            task.skinID = skins.GetRandom();
        }

        private void OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            if (!randomizedTasks.Contains(task.taskUID)) return;
            if (task.amount == 0)
            {
                randomizedTasks.Remove(task.taskUID);
                return;
            }
            var skins = GetSkins(task.blueprint.targetItem);
            task.skinID = skins.GetRandom();
        }

        private List<int> GetSkins(ItemDefinition def)
        {
            List<int> skins;
            if (skinsCache.TryGetValue(def.shortname, out skins)) return skins;
            skins = new List<int> { 0 };
            skins.AddRange(def.skins.Select(skin => skin.id));
            skins.AddRange(def.skins2.Select(skin => skin.Id));
            skinsCache.Add(def.shortname, skins);
            return skins;
        }
    }
}
