using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;
using System.Collections.Generic;
using System;
using InventoryExtensions;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using RustProto;
namespace Oxide.Plugins
{
    [Info("AdvShop", "BDM", "1.0.0")]
    [Description("AdvShop")]
    public class AdvShop : RustLegacyPlugin
    {
        private Dictionary<string, Player> advshop;

        private readonly Dictionary<string, ItemDataBlock> datablocks = new Dictionary<string, ItemDataBlock>();
        private void OnServerInitialized()
        {
            foreach (var item in DatablockDictionary.All)
                datablocks.Add(item.name.ToLower(), item);
        }
        //player currency
        public int Startingdrabloons = 100;
        //item prices
        public int Ammo556 = 10;
        public int Ammo9mm = 5;
        public int Ammoarrow = 2;
        public int Ammohandmadeshell = 3;
        public int Ammoshotgunshell = 5;
        public int Armorclothhelmet = 25;
        public int Armorclothvest = 25;
        public int Armorclothpants = 25;
        public int Armorclothboots = 25;
        public int Armorleatherhelmet = 50;
        public int Armorleathervest = 50;
        public int Armorleatherpants = 50;
        public int Armorleatherboots = 50;
        public int Armorradsuithelmet = 75;
        public int Armorradsuitvest = 75;
        public int Armorradsuitpants = 75;
        public int Armorradsuitboots = 75;
        public int Armorkevlarhelmet = 150;
        public int Armorkevlarvest = 150;
        public int Armorkevlarpants = 150;
        public int Armorkevlarboots = 150;
        public int Foodcanofbeans = 5;
        public int Foodcanoftuna = 5;
        public int Foodchocolatebar = 5;
        public int Foodcookedchickenbreast = 3;
        public int Foodgranolabar = 5;
        public int Foodrawchickenbreast = 2;
        public int Foodsmallrations = 10;
        public int Medicalantiradpills = 10;
        public int Medicalbandage = 5;
        public int Medicallargemedkit = 20;
        public int Medicalsmallmedkit = 10;
        public int Modsflashlightmod = 25;
        public int Modsholosight = 30;
        public int Modslasersight = 20;
        public int Modssilencer = 25;
        public int Partsmetalceiling = 50;
        public int Partsmetaldoorway = 50;
        public int Partsmetalfoundation = 50;
        public int Partsmetalpillar = 25;
        public int Partsmetalramp = 50;
        public int Partsmetalstairs = 50;
        public int Partsmetalwall = 50;
        public int Partsmetalwindow = 50;
        public int Partsmetalwindowbars = 15;
        public int Partswoodceiling = 25;
        public int Partswooddoorway = 25;
        public int Partswoodfoundation = 25;
        public int Partswoodpillar = 12;
        public int Partswoodramp = 25;
        public int Partswoodstairs = 25;
        public int Partswoodwall = 25;
        public int Partswoodwindow = 25;
        public int Resourcesanimalfat = 5;
        public int Resourcesblood = 5;
        public int Resourcescloth = 5;
        public int Resourcesexplosives = 50;
        public int Resourcesgunpowder = 15;
        public int Resourcesleather = 10;
        public int Resourcesmetalfragments = 3;
        public int Resourcesmetalore = 10;
        public int Resourcespaper = 1;
        public int Resourcesstones = 1;
        public int Resourcessulfur = 3;
        public int Resourcessulfurore = 10;
        public int Resourceswood = 2;
        public int Resourceswoodplanks = 10;
        public int Survivalbed = 15;
        public int Survivalcampfire = 2;
        public int Survivalfurnace = 20;
        public int Survivallargespikewall = 15;
        public int Survivallargewoodstorage = 15;
        public int Survivallowgradefuel = 5;
        public int Survivallowqualitymetal = 25;
        public int Survivalmetaldoor = 50;
        public int Survivalrepairbench = 25;
        public int Survivalsleepingbag = 3;
        public int Survivalsmallstash = 1;
        public int Survivalspikewall = 7;
        public int Survivaltorch = 1;
        public int Survivalwoodbarricade = 10;
        public int Survivalwoodgate = 50;
        public int Survivalwoodgateway = 50;
        public int Survivalwoodshelter = 25;
        public int Survivalwoodstoragebox = 5;
        public int Survivalwoodendoor = 15;
        public int Survivalworkbench = 5;
        public int Toolsblooddrawkit = 5;
        public int Toolshandmadelockpick = 5;
        public int Toolsresearchkit = 50;
        public int Weapons9mmpistol = 40;
        public int Weaponsexplosivecharge = 100;
        public int Weaponsf1grenade = 50;
        public int Weaponshandcannon = 25;
        public int Weaponshatchet = 10;
        public int Weaponshuntingbow = 10;
        public int Weaponsm4 = 125;
        public int Weaponsmp5a4 = 100;
        public int Weaponsp250 = 80;
        public int Weaponspipeshotgun = 25;
        public int Weaponsrevolver = 25;
        public int Weaponsrock = 5;
        public int Weaponsshotgun = 60;
        public int Weaponsstonehatchet = 3;
        public int Weaponssupplysignal = 125;
        public int Weaponsboltactionrifle = 125;
        public int Weaponspickaxe = 50;

        /*
        public int SellAmmo556 = Ammo556 * 0.70;
        public int SellAmmo9mm = Ammo9mm * 0.70;
        public int SellAmmoarrow = Ammoarrow * 0.70;
        public int SellAmmohandmadeshell = Ammohandmadeshell * 0.70;
        public int SellAmmoshotgunshell = Ammoshotgunshell * 0.70;
        public int SellArmorclothhelmet = Armorclothhelmet * 0.70;
        public int SellArmorclothvest = Armorclothvest * 0.70;
        public int SellArmorclothpants = Armorclothpants * 0.70;
        public int SellArmorclothboots = Armorclothboots * 0.70;
        public int SellArmorleatherhelmet = Armorleatherhelmet * 0.70;
        public int SellArmorleathervest = Armorleathervest * 0.70;
        public int SellArmorleatherpants = Armorleatherpants * 0.70;
        public int SellArmorleatherboots = Armorleatherboots * 0.70;
        public int SellArmorradsuithelmet = Armorradsuithelmet * 0.70;
        public int SellArmorradsuitvest = Armorradsuitvest * 0.70;
        public int SellArmorradsuitpants = Armorradsuitpants * 0.70;
        public int SellArmorradsuitboots = Armorradsuitboots * 0.70;
        public int SellArmorkevlarhelmet = Armorkevlarhelmet * 0.70;
        public int SellArmorkevlarvest = Armorkevlarvest * 0.70;
        public int SellArmorkevlarpants = Armorkevlarpants * 0.70;
        public int SellArmorkevlarboots = Armorkevlarboots * 0.70;
        public int SellFoodcanofbeans = Foodcanofbeans * 0.70;
        public int SellFoodcanoftuna = Foodcanoftuna * 0.70;
        public int SellFoodchocolatebar = Foodchocolatebar * 0.70;
        public int SellFoodcookedchickenbreast = Foodcookedchickenbreast * 0.70;
        public int SellFoodgranolabar = Foodgranolabar * 0.70;
        public int SellFoodrawchickenbreast = Foodrawchickenbreast * 0.70;
        public int SellFoodsmallrations = Foodsmallrations * 0.70;
        public int SellMedicalantiradpills = Medicalantiradpills * 0.70;
        public int SellMedicalbandage = Medicalbandage * 0.70;
        public int SellMedicallargemedkit = Medicallargemedkit * 0.70;
        public int SellMedicalsmallmedkit = Medicalsmallmedkit * 0.70;
        public int SellModsflashlightmod = Modsflashlightmod * 0.70;
        public int SellModsholosight = Modsholosight * 0.70;
        public int SellModslasersight = Modslasersight * 0.70;
        public int SellModssilencer = Modssilencer * 0.70;
        public int SellPartsmetalceiling = Partsmetalceiling * 0.70;
        public int SellPartsmetaldoorway = Partsmetaldoorway * 0.70;
        public int SellPartsmetalfoundation = Partsmetalfoundation * 0.70;
        public int SellPartsmetalpillar = Partsmetalpillar * 0.70;
        public int SellPartsmetalramp = Partsmetalramp * 0.70;
        public int SellPartsmetalstairs = Partsmetalstairs * 0.70;
        public int SellPartsmetalwall = Partsmetalwall * 0.70;
        public int SellPartsmetalwindow = Partsmetalwindow * 0.70;
        public int SellPartsmetalwindowbars = Partsmetalwindowbars * 0.70;
        public int SellPartswoodceiling = Partswoodceiling * 0.70;
        public int SellPartswooddoorway = Partswooddoorway * 0.70;
        public int SellPartswoodfoundation = Partswoodfoundation * 0.70;
        public int SellPartswoodpillar = Partswoodpillar * 0.70;
        public int SellPartswoodramp = Partswoodramp * 0.70;
        public int SellPartswoodstairs = Partswoodstairs * 0.70;
        public int SellPartswoodwall = Partswoodwall * 0.70;
        public int SellPartswoodwindow = Partswoodwindow * 0.70;
        public int SellResourcesanimalfat = Resourcesanimalfat * 0.70;
        public int SellResourcesblood = Resourcesblood * 0.70;
        public int SellResourcescloth = Resourcescloth * 0.70;
        public int SellResourcesexplosives = Resourcesexplosives * 0.70;
        public int SellResourcesgunpowder = Resourcesgunpowder * 0.70;
        public int SellResourcesleather = Resourcesleather * 0.70;
        public int SellResourcesmetalfragments = Resourcesmetalfragments * 0.70;
        public int SellResourcesmetalore = Resourcesmetalore * 0.70;
        public int SellResourcespaper = Resourcespaper * 0.70;
        public int SellResourcesstones = Resourcesstones * 0.70;
        public int SellResourcessulfur = Resourcessulfur * 0.70;
        public int SellSellResourcessulfurore = Resourcessulfurore * 0.70;
        public int SellResourceswood = Resourceswood * 0.70;
        public int SellResourceswoodplanks = Resourceswoodplanks * 0.70;
        public int SellSurvivalbed = Survivalbed * 0.70;
        public int SellSurvivalcampfire = Survivalcampfire * 0.70;
        public int SellSurvivalfurnace = Survivalfurnace * 0.70;
        public int SellSurvivallargespikewall = Survivalspikewall * 0.70;
        public int SellSurvivallargewoodstorage = Survivallargewoodstorage * 0.70;
        public int SellSurvivallowgradefuel = Survivallowgradefuel * 0.70;
        public int SellSurvivallowqualitymetal = Survivallowqualitymetal * 0.70;
        public int SellSurvivalmetaldoor = Survivalmetaldoor * 0.70;
        public int SellSurvivalrepairbench = Survivalrepairbench * 0.70;
        public int SellSurvivalsleepingbag = Survivalsleepingbag * 0.70;
        public int SellSurvivalsmallstash = Survivalsmallstash * 0.70;
        public int SellSurvivalspikewall = Survivalspikewall * 0.70;
        public int SellSurvivaltorch = Survivaltorch * 0.70;
        public int SellSurvivalwoodbarricade = Survivalwoodbarricade * 0.70;
        public int SellSurvivalwoodgate = survivalwoodgate * 0.70;
        public int SellSurvivalwoodgateway = Survivalwoodgateway * 0.70;
        public int SellSurvivalwoodshelter = Survivalwoodshelter * 0.70;
        public int SellSurvivalwoodstoragebox = Survivalwoodstoragebox * 0.70;
        public int SellSurvivalwoodendoor = Survivalwoodendoor * 0.70;
        public int SellSurvivalworkbench = Survivalworkbench * 0.70;
        public int SellToolsblooddrawkit = Toolsblooddrawkit * 0.70;
        public int SellToolshandmadelockpick = Toolshandmadelockpick * 0.70;
        public int SellToolsresearchkit = Toolsresearchkit * 0.70;
        public int SellWeapons9mmpistol = Weapons9mmpistol * 0.70;
        public int SellWeaponsexplosivecharge = Weaponsexplosivecharge * 0.70;
        public int SellWeaponsf1grenade = Weaponsf1grenade * 0.70;
        public int SellWeaponshandcannon = Weaponshandcannon * 0.70;
        public int SellWeaponshatchet = Weaponshatchet * 0.70;
        public int SellWeaponshuntingbow = Weaponshuntingbow * 0.70;
        public int SellWeaponsm4 = Weaponsm4 * 0.70;
        public int SellWeaponsmp5a4 = Weaponsmp5a4 * 0.70;
        public int SellWeaponsp250 = Weaponsp250 * 0.70;
        public int SellWeaponspipeshotgun = Weaponspipeshotgun * 0.70;
        public int SellWeaponsrevolver = Weaponsrevolver * 0.70;
        public int SellWeaponsrock = Weaponsrock * 0.70;
        public int SellWeaponsshotgun = Weaponsshotgun * 0.70;
        public int SellWeaponsstonehatchet = Weaponsstonehatchet * 0.70;
        public int SellWeaponssupplysignal = Weaponssupplysignal * 0.70;
        public int SellWeaponsboltactionrifle = Weaponsboltactionrifle * 0.70;
        public double SellWeaponspickaxe = Weaponspickaxe * 0.70;
        */

        void LoadDefaultConfig() { }
        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        void Init()
        {
            CheckCfg<int>("Player Starting Balance", ref Startingdrabloons);
            CheckCfg<int>("Cost: 556 Ammo", ref Ammo556);
            CheckCfg<int>("Cost: 9mm Ammo", ref Ammo9mm);
            CheckCfg<int>("Cost: Arrow", ref Ammoarrow);
            CheckCfg<int>("Cost: Handmade Shell", ref Ammohandmadeshell);
            CheckCfg<int>("Cost: Shotgun Shell", ref Ammoshotgunshell);
            CheckCfg<int>("Cost: Cloth Helmet", ref Armorclothhelmet);
            CheckCfg<int>("Cost: Cloth Vest", ref Armorclothvest);
            CheckCfg<int>("Cost: Cloth Pants", ref Armorclothpants);
            CheckCfg<int>("Cost: Cloth Boots", ref Armorclothboots);
            CheckCfg<int>("Cost: Leather Helmet", ref Armorleatherhelmet);
            CheckCfg<int>("Cost: Leather Vest", ref Armorleathervest);
            CheckCfg<int>("Cost: Leather Pants", ref Armorleatherpants);
            CheckCfg<int>("Cost: Leather Boots", ref Armorleatherboots);
            CheckCfg<int>("Cost: Rad Suit Helmet", ref Armorradsuithelmet);
            CheckCfg<int>("Cost: Rad Suit Vest", ref Armorradsuitvest);
            CheckCfg<int>("Cost: Rad Suit Pants", ref Armorradsuitpants);
            CheckCfg<int>("Cost: Rad Suit Boots", ref Armorradsuitboots);
            CheckCfg<int>("Cost: Kevlar Helmet", ref Armorkevlarhelmet);
            CheckCfg<int>("Cost: Kevlar Vest", ref Armorkevlarvest);
            CheckCfg<int>("Cost: Kevlar Pants", ref Armorkevlarpants);
            CheckCfg<int>("Cost: Kevlar Boots", ref Armorkevlarboots);
            CheckCfg<int>("Cost: Can of Beans", ref Foodcanofbeans);
            CheckCfg<int>("Cost: Can of Tuna", ref Foodcanoftuna);
            CheckCfg<int>("Cost: Chocolate Bar", ref Foodchocolatebar);
            CheckCfg<int>("Cost: Cooked Chicken Breast", ref Foodcookedchickenbreast);
            CheckCfg<int>("Cost: Small Rations", ref Foodsmallrations);
            CheckCfg<int>("Cost: Anti-Radiation Pills", ref Medicalantiradpills);
            CheckCfg<int>("Cost: Bandage", ref Medicalbandage);
            CheckCfg<int>("Cost: Large Medkit", ref Medicallargemedkit);
            CheckCfg<int>("Cost: Small Medkit", ref Medicalsmallmedkit);
            CheckCfg<int>("Cost: Flashlight Mod", ref Modsflashlightmod);
            CheckCfg<int>("Cost: Holo Sight", ref Modsholosight);
            CheckCfg<int>("Cost: Laser Sight", ref Modslasersight);
            CheckCfg<int>("Cost: Metal Ceiling", ref Partsmetalceiling);
            CheckCfg<int>("Cost: Metal Doorway", ref Partsmetaldoorway);
            CheckCfg<int>("Cost: Metal Foundation", ref Partsmetalfoundation);
            CheckCfg<int>("Cost: Metal Pillar", ref Partsmetalpillar);
            CheckCfg<int>("Cost: Metal Ramp", ref Partsmetalramp);
            CheckCfg<int>("Cost: Metal Stairs", ref Partsmetalstairs);
            CheckCfg<int>("Cost: Metal Wall", ref Partsmetalwall);
            CheckCfg<int>("Cost: Metal Window", ref Partsmetalwindow);
            CheckCfg<int>("Cost: Metal Window Bars", ref Partsmetalwindowbars);
            CheckCfg<int>("Cost: Wood Ceiling", ref Partswoodceiling);
            CheckCfg<int>("Cost: Wood Doorway", ref Partswooddoorway);
            CheckCfg<int>("Cost: Wood Foundation", ref Partswoodfoundation);
            CheckCfg<int>("Cost: Wood Pillar", ref Partswoodpillar);
            CheckCfg<int>("Cost: Wood Ramp", ref Partswoodramp);
            CheckCfg<int>("Cost: Wood Stairs", ref Partswoodstairs);
            CheckCfg<int>("Cost: Wood Wall", ref Partswoodwall);
            CheckCfg<int>("Cost: Wood Window", ref Partswoodwindow);
            CheckCfg<int>("Cost: Animal Fat", ref Resourcesanimalfat);
            CheckCfg<int>("Cost: Blood", ref Resourcesblood);
            CheckCfg<int>("Cost: Cloth", ref Resourcescloth);
            CheckCfg<int>("Cost: Explosives", ref Resourcesexplosives);
            CheckCfg<int>("Cost: Gunpowder", ref Resourcesgunpowder);
            CheckCfg<int>("Cost: Leather", ref Resourcesleather);
            CheckCfg<int>("Cost: Metal Fragments", ref Resourcesmetalfragments);
            CheckCfg<int>("Cost: Metal Ore", ref Resourcesmetalore);
            CheckCfg<int>("Cost: Paper", ref Resourcespaper);
            CheckCfg<int>("Cost: Stones", ref Resourcesstones);
            CheckCfg<int>("Cost: Sulfur", ref Resourcessulfur);
            CheckCfg<int>("Cost: Sulfur Ore", ref Resourcessulfurore);
            CheckCfg<int>("Cost: Wood", ref Resourceswood);
            CheckCfg<int>("Cost: Wood Planks", ref Resourceswoodplanks);
            CheckCfg<int>("Cost: Bed", ref Survivalbed);
            CheckCfg<int>("Cost: Camp Fire", ref Survivalcampfire);
            CheckCfg<int>("Cost: Furnace", ref Survivalfurnace);
            CheckCfg<int>("Cost: Large Spike Wall", ref Survivallargespikewall);
            CheckCfg<int>("Cost: Large Wood Storage", ref Survivallargewoodstorage);
            CheckCfg<int>("Cost: Low Grade Fuel", ref Survivallowgradefuel);
            CheckCfg<int>("Cost: Low Quality Metal", ref Survivallowqualitymetal);
            CheckCfg<int>("Cost: Metal Door", ref Survivalmetaldoor);
            CheckCfg<int>("Cost: Repair Bench", ref Survivalrepairbench);
            CheckCfg<int>("Cost: Sleeping Bag", ref Survivalsleepingbag);
            CheckCfg<int>("Cost: Small Stash", ref Survivalsmallstash);
            CheckCfg<int>("Cost: Spike Wall", ref Survivalspikewall);
            CheckCfg<int>("Cost: Torch", ref Survivaltorch);
            CheckCfg<int>("Cost: Wood Barricade", ref Survivalwoodbarricade);
            CheckCfg<int>("Cost: Wood Gate", ref Survivalwoodgate);
            CheckCfg<int>("Cost: Wood Gateway", ref Survivalwoodgateway);
            CheckCfg<int>("Cost: Wood Shelter", ref Survivalwoodshelter);
            CheckCfg<int>("Cost: Wood Storage Box", ref Survivalwoodstoragebox);
            CheckCfg<int>("Cost: Wooden Door", ref Survivalwoodendoor);
            CheckCfg<int>("Cost: Workbench", ref Survivalworkbench);
            CheckCfg<int>("Cost: Blood Draw Kit", ref Toolsblooddrawkit);
            CheckCfg<int>("Cost: Handmade Lockpick", ref Toolshandmadelockpick);
            CheckCfg<int>("Cost: Research Kit", ref Toolsresearchkit);
            CheckCfg<int>("Cost: 9mm Pistol", ref Weapons9mmpistol);
            CheckCfg<int>("Cost: Explosive Charge", ref Weaponsexplosivecharge);
            CheckCfg<int>("Cost: F1 Grenade", ref Weaponsf1grenade);
            CheckCfg<int>("Cost: Hand Cannon", ref Weaponshandcannon);
            CheckCfg<int>("Cost: Hatchet", ref Weaponshatchet);
            CheckCfg<int>("Cost: Hunting Bow", ref Weaponshuntingbow);
            CheckCfg<int>("Cost: M4", ref Weaponsm4);
            CheckCfg<int>("Cost: MP5A4", ref Weaponsmp5a4);
            CheckCfg<int>("Cost: P250", ref Weaponsp250);
            CheckCfg<int>("Cost: Pipe Shotgun", ref Weaponspipeshotgun);
            CheckCfg<int>("Cost: Revolver", ref Weaponsrevolver);
            CheckCfg<int>("Cost: Rock", ref Weaponsrock);
            CheckCfg<int>("Cost: Shotgun", ref Weaponsshotgun);
            CheckCfg<int>("Cost: Stone Hatchet", ref Weaponsstonehatchet);
            CheckCfg<int>("Cost: Supply Signal", ref Weaponssupplysignal);
            CheckCfg<int>("Cost: Bolt Action Rifle", ref Weaponsboltactionrifle);
            CheckCfg<int>("Cost: Pickaxe", ref Weaponspickaxe);
            /*
            CheckCfg<int>("Profit: 556 Ammo", ref Ammo556);
            CheckCfg<int>("Profit: 9mm Ammo", ref Ammo9mm);
            CheckCfg<int>("Profit: Arrow", ref Ammoarrow);
            CheckCfg<int>("Profit: Handmade Shell", ref Ammohandmadeshell);
            CheckCfg<int>("Profit: Shotgun Shell", ref Ammoshotgunshell);
            CheckCfg<int>("Profit: Cloth Helmet", ref Armorclothhelmet);
            CheckCfg<int>("Profit: Cloth Vest", ref Armorclothvest);
            CheckCfg<int>("Profit: Cloth Pants", ref Armorclothpants);
            CheckCfg<int>("Profit: Cloth Boots", ref Armorclothboots);
            CheckCfg<int>("Profit: Leather Helmet", ref Armorleatherhelmet);
            CheckCfg<int>("Profit: Leather Vest", ref Armorleathervest);
            CheckCfg<int>("Profit: Leather Pants", ref Armorleatherpants);
            CheckCfg<int>("Profit: Leather Boots", ref Armorleatherboots);
            CheckCfg<int>("Profit: Rad Suit Helmet", ref Armorradsuithelmet);
            CheckCfg<int>("Profit: Rad Suit Vest", ref Armorradsuitvest);
            CheckCfg<int>("Profit: Rad Suit Pants", ref Armorradsuitpants);
            CheckCfg<int>("Profit: Rad Suit Boots", ref Armorradsuitboots);
            CheckCfg<int>("Profit: Kevlar Helmet", ref Armorkevlarhelmet);
            CheckCfg<int>("Profit: Kevlar Vest", ref Armorkevlarvest);
            CheckCfg<int>("Profit: Kevlar Pants", ref Armorkevlarpants);
            CheckCfg<int>("Profit: Kevlar Boots", ref Armorkevlarboots);
            CheckCfg<int>("Profit: Can of Beans", ref Foodcanofbeans);
            CheckCfg<int>("Profit: Can of Tuna", ref Foodcanoftuna);
            CheckCfg<int>("Profit: Chocolate Bar", ref Foodchocolatebar);
            CheckCfg<int>("Profit: Cooked Chicken Breast", ref Foodcookedchickenbreast);
            CheckCfg<int>("Profit: Small Rations", ref Foodsmallrations);
            CheckCfg<int>("Profit: Anti-Radiation Pills", ref Medicalantiradpills);
            CheckCfg<int>("Profit: Bandage", ref Medicalbandage);
            CheckCfg<int>("Profit: Large Medkit", ref Medicallargemedkit);
            CheckCfg<int>("Profit: Small Medkit", ref Medicalsmallmedkit);
            CheckCfg<int>("Profit: Flashlight Mod", ref Modsflashlightmod);
            CheckCfg<int>("Profit: Holo Sight", ref Modsholosight);
            CheckCfg<int>("Profit: Laser Sight", ref Modslasersight);
            CheckCfg<int>("Profit: Metal Ceiling", ref Partsmetalceiling);
            CheckCfg<int>("Profit: Metal Doorway", ref Partsmetaldoorway);
            CheckCfg<int>("Profit: Metal Foundation", ref Partsmetalfoundation);
            CheckCfg<int>("Profit: Metal Pillar", ref Partsmetalpillar);
            CheckCfg<int>("Profit: Metal Ramp", ref Partsmetalramp);
            CheckCfg<int>("Profit: Metal Stairs", ref Partsmetalstairs);
            CheckCfg<int>("Profit: Metal Wall", ref Partsmetalwall);
            CheckCfg<int>("Profit: Metal Window", ref Partsmetalwindow);
            CheckCfg<int>("Profit: Metal Window Bars", ref Partsmetalwindowbars);
            CheckCfg<int>("Profit: Wood Ceiling", ref Partswoodceiling);
            CheckCfg<int>("Profit: Wood Doorway", ref Partswooddoorway);
            CheckCfg<int>("Profit: Wood Foundation", ref Partswoodfoundation);
            CheckCfg<int>("Profit: Wood Pillar", ref Partswoodpillar);
            CheckCfg<int>("Profit: Wood Ramp", ref Partswoodramp);
            CheckCfg<int>("Profit: Wood Stairs", ref Partswoodstairs);
            CheckCfg<int>("Profit: Wood Wall", ref Partswoodwall);
            CheckCfg<int>("Profit: Wood Window", ref Partswoodwindow);
            CheckCfg<int>("Profit: Animal Fat", ref Resourcesanimalfat);
            CheckCfg<int>("Profit: Blood", ref Resourcesblood);
            CheckCfg<int>("Profit: Cloth", ref Resourcescloth);
            CheckCfg<int>("Profit: Explosives", ref Resourcesexplosives);
            CheckCfg<int>("Profit: Gunpowder", ref Resourcesgunpowder);
            CheckCfg<int>("Profit: Leather", ref Resourcesleather);
            CheckCfg<int>("Profit: Metal Fragments", ref Resourcesmetalfragments);
            CheckCfg<int>("Profit: Metal Ore", ref Resourcesmetalore);
            CheckCfg<int>("Profit: Paper", ref Resourcespaper);
            CheckCfg<int>("Profit: Stones", ref Resourcesstones);
            CheckCfg<int>("Profit: Sulfur", ref Resourcessulfur);
            CheckCfg<int>("Profit: Sulfur Ore", ref Resourcessulfurore);
            CheckCfg<int>("Profit: Wood", ref Resourceswood);
            CheckCfg<int>("Profit: Wood Planks", ref Resourceswoodplanks);
            CheckCfg<int>("Profit: Bed", ref Survivalbed);
            CheckCfg<int>("Profit: Camp Fire", ref Survivalcampfire);
            CheckCfg<int>("Profit: Furnace", ref Survivalfurnace);
            CheckCfg<int>("Profit: Large Spike Wall", ref Survivallargespikewall);
            CheckCfg<int>("Profit: Large Wood Storage", ref Survivallargewoodstorage);
            CheckCfg<int>("Profit: Low Grade Fuel", ref Survivallowgradefuel);
            CheckCfg<int>("Profit: Low Quality Metal", ref Survivallowqualitymetal);
            CheckCfg<int>("Profit: Metal Door", ref Survivalmetaldoor);
            CheckCfg<int>("Profit: Repair Bench", ref Survivalrepairbench);
            CheckCfg<int>("Profit: Sleeping Bag", ref Survivalsleepingbag);
            CheckCfg<int>("Profit: Small Stash", ref Survivalsmallstash);
            CheckCfg<int>("Profit: Spike Wall", ref Survivalspikewall);
            CheckCfg<int>("Profit: Torch", ref Survivaltorch);
            CheckCfg<int>("Profit: Wood Barricade", ref Survivalwoodbarricade);
            CheckCfg<int>("Profit: Wood Gate", ref Survivalwoodgate);
            CheckCfg<int>("Profit: Wood Gateway", ref Survivalwoodgateway);
            CheckCfg<int>("Profit: Wood Shelter", ref Survivalwoodshelter);
            CheckCfg<int>("Profit: Wood Storage Box", ref Survivalwoodstoragebox);
            CheckCfg<int>("Profit: Wooden Door", ref Survivalwoodendoor);
            CheckCfg<int>("Profit: Workbench", ref Survivalworkbench);
            CheckCfg<int>("Profit: Blood Draw Kit", ref Toolsblooddrawkit);
            CheckCfg<int>("Profit: Handmade Lockpick", ref Toolshandmadelockpick);
            CheckCfg<int>("Profit: Research Kit", ref Toolsresearchkit);
            CheckCfg<int>("Profit: 9mm Pistol", ref Weapons9mmpistol);
            CheckCfg<int>("Profit: Explosive Charge", ref Weaponsexplosivecharge);
            CheckCfg<int>("Profit: F1 Grenade", ref Weaponsf1grenade);
            CheckCfg<int>("Profit: Hand Cannon", ref Weaponshandcannon);
            CheckCfg<int>("Profit: Hatchet", ref Weaponshatchet);
            CheckCfg<int>("Profit: Hunting Bow", ref Weaponshuntingbow);
            CheckCfg<int>("Profit: M4", ref Weaponsm4);
            CheckCfg<int>("Profit: MP5A4", ref Weaponsmp5a4);
            CheckCfg<int>("Profit: P250", ref Weaponsp250);
            CheckCfg<int>("Profit: Pipe Shotgun", ref Weaponspipeshotgun);
            CheckCfg<int>("Profit: Revolver", ref Weaponsrevolver);
            CheckCfg<int>("Profit: Rock", ref Weaponsrock);
            CheckCfg<int>("Profit: Shotgun", ref Weaponsshotgun);
            CheckCfg<int>("Profit: Stone Hatchet", ref Weaponsstonehatchet);
            CheckCfg<int>("Profit: Supply Signal", ref Weaponssupplysignal);
            CheckCfg<int>("Profit: Bolt Action Rifle", ref Weaponsboltactionrifle);
            CheckCfg<int>("Profit: Pickaxe", ref Weaponspickaxe);     
            */      
            SaveConfig();
        }
        void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"Prefix", "AdvShop"},
                {"NoPerm", "You do not have the proper permission to use the command you tried."},
                //itemlist
                {"556Ammo", "556 Ammo"},
                //shop
                {"ShopNotice", "AdvShop - v1.0.0 - BDM"},
                {"ShopTitle", "[color green]-------------[color orange]AdvShop - Shop[color green]-------------"},
                {"ShopCmd", "[color cyan]The following commands are currently available:"},
                {"ShopCmd1", "[color yellow]/shop [color white]- The home page of this plugin."},
                {"ShopCmd2", "[color yellow]/buy [color white]- The menu to view what is available for purchase with the built in currency (Drabloons)."},
                {"ShopCmd3", "[color yellow]/sell [color white]- Sell what you've collected for built in currency (Drabloons)."},
                {"ShopCmd4", "[color yellow]/trade [color white]- Trade what you've collected for items."},
                {"ShopCmd5", "[color yellow]/currency [color white]- Check how much currency you currently have."},
                {"ShopCmd6", "[color yellow]/iap [color white]- View the available options to purchase ingame currency (costs real money)."},
                {"ShopCmd7", "[color yellow]/advshophelp [color white]- Help menu with more detailed help and technical assistance."},
                {"ShopCmd8", "[color yellow]/buyadmin [color white]- Purchase items as an admin without cost (test purposes, item will be removed)."},
                {"ShopCmd9", "[color yellow]/selladmin [color white]- Sell items as an admin without cost (test purposes, currency will be removed)."},
                {"ShopCmd10", "[color yellow]/tradeadmin [color white]- Trade items as an admin without cost (test purposes, trade will be reverted)."},
                {"ShopCmd11", "[color yellow]/currencyadmin [color white]- Modify your currency (test purposes, currency will be reverted)."},
                {"ShopCmd12", "[color yellow]/iapadmin [color white]- Recieve iap packages without cost (test purposes, currency will be removed)."},
                //buy
                {"BuyTitle", "[color green]-------------[color orange]AdvShop - Buy[color green]-------------"},
                {"BuyCmdIntro1", "[color red]Use [color yellow]/buy 'category' 'page' [color red]to view items in a specific category."},
                {"BuyCmdIntro2", "[color red]Use [color yellow]/buy 'item number/name in blue' 'quantity'[color red]to purchase a specific amount of an item."},
                {"BuyCmdIntro3", "[color cyan]The following categories are currently available:"},
                {"BuyCate1", "[color yellow]Ammo"},
                {"BuyCate2", "[color yellow]Armor"},
                {"BuyCate3", "[color yellow]Food"},
                {"BuyCate4", "[color yellow]Medical"},
                {"BuyCate5", "[color yellow]Mods"},
                {"BuyCate6", "[color yellow]Parts"},
                {"BuyCate7", "[color yellow]Resources"},
                {"BuyCate8", "[color yellow]Survival"},
                {"BuyCate9", "[color yellow]Tools"},
                {"BuyCate10", "[color yellow]Weapons"},
                {"BuyTitleAmmo", "[color green]-------------[color orange]AdvShop - Buy - Ammo[color green]-------------"},
                {"BuyAmmo1", "[color yellow]556 Ammo [color white]- [color cyan]1 [color white]- [color cyan]556ammo [color white]- [color red]"},
                {"BuyAmmo2", "[color yellow]9mm Ammo [color white]- [color cyan]2 [color white]- [color cyan]9mmammo [color white]- [color red]"},
                {"BuyAmmo3", "[color yellow]Arrow [color white]- [color cyan]3 [color white]- [color cyan]arrow [color white]- [color red]"},
                {"BuyAmmo4", "[color yellow]Handmade Shell [color white]- [color cyan]4 [color white]- [color cyan]handmadeshell [color white]- [color red]"},
                {"BuyAmmo5", "[color yellow]Shotgun Shells [color white]- [color cyan]5 [color white]- [color cyan]shotgunshell [color white]- [color red]"},
                {"AmmoPage", "[color green]-------------[color orange]Page 1/1[color green]-------------"},
                {"BuyTitleArmor", "[color green]-------------[color orange]AdvShop - Buy - Armor[color green]-------------"},
                {"BuyArmor1", "[color yellow]Cloth Helmet [color white]- [color cyan]6 [color white]- [color cyan]clothhelmet [color white]- [color red]"},
                {"BuyArmor2", "[color yellow]Cloth Vest [color white]- [color cyan]7 [color white]- [color cyan]clothvest [color white]- [color red]"},
                {"BuyArmor3", "[color yellow]Cloth Pants [color white]- [color cyan]8 [color white]- [color cyan]clothpants [color white]- [color red]"},
                {"BuyArmor4", "[color yellow]Cloth Boots [color white]- [color cyan]9 [color white]- [color cyan]clothboots [color white]- [color red]"},
                {"BuyArmor5", "[color yellow]Leather Helmet [color white]- [color cyan]10 [color white]- [color cyan]leatherhelmet [color white]- [color red]"},
                {"BuyArmor6", "[color yellow]Leather Vest [color white]- [color cyan]11 [color white]- [color cyan]leathervest [color white]- [color red]"},
                {"BuyArmor7", "[color yellow]Leather Pants [color white]- [color cyan]12 [color white]- [color cyan]leatherpants [color white]- [color red]"},
                {"BuyArmor8", "[color yellow]Leather Boots [color white]- [color cyan]13 [color white]- [color cyan]leatherboots [color white]- [color red]"},
                {"BuyArmor9", "[color yellow]Rad Suit Helmet [color white]- [color cyan]14 [color white]- [color cyan]radsuithelmet [color white]- [color red]"},
                {"BuyArmor10", "[color yellow]Rad Suit Vest [color white]- [color cyan]15 [color white]- [color cyan]radsuitvest [color white]- [color red]"},
                {"BuyArmor11", "[color yellow]Rad Suit Pants [color white]- [color cyan]16 [color white]- [color cyan]radsuitpants [color white]- [color red]"},
                {"BuyArmor12", "[color yellow]Rad Suit Boots [color white]- [color cyan]17 [color white]- [color cyan]radsuitboots [color white]- [color red]"},
                {"BuyArmor13", "[color yellow]Kevlar Helmet [color white]- [color cyan]18 [color white]- [color cyan]kevlarhelmet [color white]- [color red]"},
                {"BuyArmor14", "[color yellow]Kevlar Vest [color white]- [color cyan]19 [color white]- [color cyan]kevlarvest [color white]- [color red]"},
                {"BuyArmor15", "[color yellow]Kevlar Pants [color white]- [color cyan]20 [color white]- [color cyan]kevlarpants [color white]- [color red]"},
                {"BuyArmor16", "[color yellow]Kevlar Boots [color white]- [color cyan]21 [color white]- [color cyan]kevlarboots [color white]- [color red]"},
                {"ArmorPage1", "[color green]-------------[color orange]Page 1/2[color green]-------------"},
                {"ArmorPage2", "[color green]-------------[color orange]Page 2/2[color green]-------------"},
                {"BuyTitleFood", "[color green]-------------[color orange]AdvShop - Buy - Food[color green]-------------"},
                {"FoodPage1", "[color green]-------------[color orange]Page 1/1[color green]-------------"},
                {"BuyFood1", "[color yellow]Can of Beans [color white]- [color cyan]22 [color white]- [color cyan]canofbeans [color white]- [color red]"},
                {"BuyFood2", "[color yellow]Can of Tuna [color white]- [color cyan]23 [color white]- [color cyan]canoftuna [color white]- [color red]"},
                {"BuyFood3", "[color yellow]Chocolate Bar [color white]- [color cyan]24 [color white]- [color cyan]chocolatebar [color white]- [color red]"},
                {"BuyFood4", "[color yellow]Cooked Chicken Breast [color white]- [color cyan]25 [color white]- [color cyan]cookedchickenbreast [color white]- [color red]"},
                {"BuyFood5", "[color yellow]Granola Bar [color white]- [color cyan]26 [color white]- [color cyan]granolabar [color white]- [color red]"},
                {"BuyFood6", "[color yellow]Raw Chicken Breast [color white]- [color cyan]27 [color white]- [color cyan]rawchickenbreast [color white]- [color red]"},
                {"BuyFood7", "[color yellow]Small Rations [color white]- [color cyan]28 [color white]- [color cyan]smallrations [color white]- [color red]"},
                {"BuyTitleMedical", "[color green]-------------[color orange]AdvShop - Buy - Medical[color green]-------------"},
                {"MedicalPage1", "[color green]-------------[color orange]Page 1/1[color green]-------------"},
                {"BuyMedical1", "[color yellow]Anti-Radiation Pills [color white]- [color cyan]29 [color white]- [color cyan]antiradiationpills [color white]- [color red]"},
                {"BuyMedical2", "[color yellow]Bandage [color white]- [color cyan]30 [color white]- [color cyan]bandage [color white]- [color red]"},
                {"BuyMedical3", "[color yellow]Large Medkit [color white]- [color cyan]31 [color white]- [color cyan]largemedkit [color white]- [color red]"},
                {"BuyMedical4", "[color yellow]Small Medkit [color white]- [color cyan]32 [color white]- [color cyan]smallmedkit [color white]- [color red]"},
                {"BuyTitleMods", "[color green]-------------[color orange]AdvShop - Buy - Mods[color green]-------------"},
                {"ModsPage1", "[color green]-------------[color orange]Page 1/1[color green]-------------"},
                {"BuyMods1", "[color yellow]Flashlight Mod [color white]- [color cyan]33 [color white]- [color cyan]flashlightmod [color white]- [color red]"},
                {"BuyMods2", "[color yellow]Holo Sight [color white]- [color cyan]34 [color white]- [color cyan]holosight [color white]- [color red]"},
                {"BuyMods3", "[color yellow]Laser Sight [color white]- [color cyan]35 [color white]- [color cyan]lasersight [color white]- [color red]"},
                {"BuyMods4", "[color yellow]Silencer [color white]- [color cyan]36 [color white]- [color cyan]silencer [color white]- [color red]"},
                {"BuyTitleParts", "[color green]-------------[color orange]AdvShop - Buy - Parts[color green]-------------"},
                {"PartsPage1", "[color green]-------------[color orange]Page 1/2[color green]-------------"},
                {"PartsPage2", "[color green]-------------[color orange]Page 2/2[color green]-------------"},
                {"BuyParts1", "[color yellow]Metal Ceiling [color white]- [color cyan]37 [color white]- [color cyan]metalceiling [color white]- [color red]"},
                {"BuyParts2", "[color yellow]Metal Doorway [color white]- [color cyan]38 [color white]- [color cyan]metaldoorway [color white]- [color red]"},
                {"BuyParts3", "[color yellow]Metal Foundation [color white]- [color cyan]39 [color white]- [color cyan]metalfoundation [color white]- [color red]"},
                {"BuyParts4", "[color yellow]Metal Pillar [color white]- [color cyan]40 [color white]- [color cyan]metalpillar [color white]- [color red]"},
                {"BuyParts5", "[color yellow]Metal Ramp [color white]- [color cyan]41 [color white]- [color cyan]metalramp [color white]- [color red]"},
                {"BuyParts6", "[color yellow]Metal Stairs [color white]- [color cyan]42 [color white]- [color cyan]metalstairs [color white]- [color red]"},
                {"BuyParts7", "[color yellow]Metal Wall [color white]- [color cyan]43 [color white]- [color cyan]metalwall [color white]- [color red]"},
                {"BuyParts8", "[color yellow]Metal Window [color white]- [color cyan]44 [color white]- [color cyan]metalwindow [color white]- [color red]"},
                {"BuyParts9", "[color yellow]Metal Window Bars [color white]- [color cyan]45 [color white]- [color cyan]metalwindowbars [color white]- [color red]"},
                {"BuyParts10", "[color yellow]Wood Ceiling [color white]- [color cyan]46 [color white]- [color cyan]woodceiling [color white]- [color red]"},
                {"BuyParts11", "[color yellow]Wood Doorway [color white]- [color cyan]47 [color white]- [color cyan]wooddoorway [color white]- [color red]"},
                {"BuyParts12", "[color yellow]Wood Foundation [color white]- [color cyan]48 [color white]- [color cyan]woodfoundation [color white]- [color red]"},
                {"BuyParts13", "[color yellow]Wood Pillar [color white]- [color cyan]49 [color white]- [color cyan]woodpillar [color white]- [color red]"},
                {"BuyParts14", "[color yellow]Wood Ramp [color white]- [color cyan]50 [color white]- [color cyan]woodramp [color white]- [color red]"},
                {"BuyParts15", "[color yellow]Wood Stairs [color white]- [color cyan]51 [color white]- [color cyan]woodstairs [color white]- [color red]"},
                {"BuyParts16", "[color yellow]Wood Wall [color white]- [color cyan]52 [color white]- [color cyan]woodwall [color white]- [color red]"},
                {"BuyParts17", "[color yellow]Wood Window [color white]- [color cyan]53 [color white]- [color cyan]woodwindow [color white]- [color red]"},
                {"BuyTitleResources", "[color green]-------------[color orange]AdvShop - Buy - Resources[color green]-------------"},
                {"ResourcesPage1", "[color green]-------------[color orange]Page 1/2[color green]-------------"},
                {"ResourcesPage2", "[color green]-------------[color orange]Page 2/2[color green]-------------"},
                {"BuyResources1", "[color yellow]Animal Fat [color white]- [color cyan]54 [color white]- [color cyan]animalfat [color white]- [color red]"},
                {"BuyResources2", "[color yellow]Blood [color white]- [color cyan]55 [color white]- [color cyan]blood [color white]- [color red]"},
                {"BuyResources3", "[color yellow]Cloth [color white]- [color cyan]56 [color white]- [color cyan]cloth [color white]- [color red]"},
                {"BuyResources4", "[color yellow]Explosives [color white]- [color cyan]57 [color white]- [color cyan]explosives [color white]- [color red]"},
                {"BuyResources5", "[color yellow]Gunpowder [color white]- [color cyan]58 [color white]- [color cyan]gunpowder [color white]- [color red]"},
                {"BuyResources6", "[color yellow]Leather [color white]- [color cyan]59 [color white]- [color cyan]leather [color white]- [color red]"},
                {"BuyResources7", "[color yellow]Metal Fragments [color white]- [color cyan]60 [color white]- [color cyan]metalfragments [color white]- [color red]"},
                {"BuyResources8", "[color yellow]Metal Ore [color white]- [color cyan]61 [color white]- [color cyan]metalore [color white]- [color red]"},
                {"BuyResources9", "[color yellow]Paper [color white]- [color cyan]62 [color white]- [color cyan]paper [color white]- [color red]"},
                {"BuyResources10", "[color yellow]Stones [color white]- [color cyan]63 [color white]- [color cyan]stones [color white]- [color red]"},
                {"BuyResources11", "[color yellow]Sulfur [color white]- [color cyan]64 [color white]- [color cyan]sulfur [color white]- [color red]"},
                {"BuyResources12", "[color yellow]Sulfur Ore [color white]- [color cyan]65 [color white]- [color cyan]sulfurore [color white]- [color red]"},
                {"BuyResources13", "[color yellow]Wood [color white]- [color cyan]66 [color white]- [color cyan]wood [color white]- [color red]"},
                {"BuyResources14", "[color yellow]Wood Planks [color white]- [color cyan]67 [color white]- [color cyan]woodplanks [color white]- [color red]"},
                {"BuyTitleSurvival", "[color green]-------------[color orange]AdvShop - Buy - Survival[color green]-------------"},
                {"SurvivalPage1", "[color green]-------------[color orange]Page 1/2[color green]-------------"},
                {"SurvivalPage2", "[color green]-------------[color orange]Page 2/2[color green]-------------"},
                {"BuySurvival1", "[color yellow]Bed [color white]- [color cyan]68 [color white]- [color cyan]bed [color white]- [color red]"},
                {"BuySurvival2", "[color yellow]Camp Fire [color white]- [color cyan]69 [color white]- [color cyan]campfire [color white]- [color red]"},
                {"BuySurvival3", "[color yellow]Furnace [color white]- [color cyan]70 [color white]- [color cyan]furnace [color white]- [color red]"},
                {"BuySurvival4", "[color yellow]Large Spike Wall [color white]- [color cyan]71 [color white]- [color cyan]largespikewall [color white]- [color red]"},
                {"BuySurvival5", "[color yellow]Large Wood Storage [color white]- [color cyan]72 [color white]- [color cyan]largewoodstorage [color white]- [color red]"},
                {"BuySurvival6", "[color yellow]Low Grade Fuel [color white]- [color cyan]73 [color white]- [color cyan]lowgradefuel [color white]- [color red]"},
                {"BuySurvival7", "[color yellow]Low Quality Metal [color white]- [color cyan]74 [color white]- [color cyan]lowqualitymetal [color white]- [color red]"},
                {"BuySurvival8", "[color yellow]Metal Door [color white]- [color cyan]75 [color white]- [color cyan]metaldoor [color white]- [color red]"},
                {"BuySurvival9", "[color yellow]Repair Bench [color white]- [color cyan]76 [color white]- [color cyan]repairbench [color white]- [color red]"},
                {"BuySurvival10", "[color yellow]Sleeping Bag [color white]- [color cyan]77 [color white]- [color cyan]sleepingbag [color white]- [color red]"},
                {"BuySurvival11", "[color yellow]Small Stash [color white]- [color cyan]78 [color white]- [color cyan]smallstash [color white]- [color red]"},
                {"BuySurvival12", "[color yellow]Spike Wall [color white]- [color cyan]79 [color white]- [color cyan]spikewall [color white]- [color red]"},
                {"BuySurvival13", "[color yellow]Torch [color white]- [color cyan]80 [color white]- [color cyan]torch [color white]- [color red]"},
                {"BuySurvival14", "[color yellow]Wood Barricade [color white]- [color cyan]81 [color white]- [color cyan]woodbarricade [color white]- [color red]"},
                {"BuySurvival15", "[color yellow]Wood Gate [color white]- [color cyan]82 [color white]- [color cyan]woodgate [color white]- [color red]"},
                {"BuySurvival16", "[color yellow]Wood Gateway [color white]- [color cyan]83 [color white]- [color cyan]woodgateway [color white]- [color red]"},
                {"BuySurvival17", "[color yellow]Wood Shelter [color white]- [color cyan]84 [color white]- [color cyan]woodshelter [color white]- [color red]"},
                {"BuySurvival18", "[color yellow]Wood Storage Box [color white]- [color cyan]85 [color white]- [color cyan]woodstoragebox [color white]- [color red]"},
                {"BuySurvival19", "[color yellow]Wooden Door [color white]- [color cyan]86 [color white]- [color cyan]woodendoor [color white]- [color red]"},
                {"BuySurvival20", "[color yellow]Workbench [color white]- [color cyan]87 [color white]- [color cyan]workbench [color white]- [color red]"},
                {"BuyTitleTools", "[color green]-------------[color orange]AdvShop - Buy - Tools[color green]-------------"},
                {"ToolsPage1", "[color green]-------------[color orange]Page 1/1[color green]-------------"},
                {"BuyTools1", "[color yellow]Blood Draw Kit [color white]- [color cyan]88 [color white]- [color cyan]blooddrawkit [color white]- [color red]"},
                {"BuyTools2", "[color yellow]Homemade Lockpick [color white]- [color cyan]89 [color white]- [color cyan]homemadelockpick [color white]- [color red]"},
                {"BuyTools3", "[color yellow]Research Kit [color white]- [color cyan]90 [color white]- [color cyan]researchkit [color white]- [color red]"},
                {"BuyTitleWeapons", "[color green]-------------[color orange]AdvShop - Buy - Tools[color green]-------------"},
                {"WeaponsPage1", "[color green]-------------[color orange]Page 1/2[color green]-------------"},
                {"WeaponsPage2", "[color green]-------------[color orange]Page 2/2[color green]-------------"},
                {"BuyWeapons1", "[color yellow]9mm Pistol [color white]- [color cyan]91 [color white]- [color cyan]9mmpistol [color white]- [color red]"},
                {"BuyWeapons2", "[color yellow]Explosive Charge [color white]- [color cyan]92 [color white]- [color cyan]explosivecharge [color white]- [color red]"},
                {"BuyWeapons3", "[color yellow]F1 Grenade [color white]- [color cyan]93 [color white]- [color cyan]f1grenade [color white]- [color red]"},
                {"BuyWeapons4", "[color yellow]Hand Cannon [color white]- [color cyan]94 [color white]- [color cyan]handcannon [color white]- [color red]"},
                {"BuyWeapons5", "[color yellow]Hatchet [color white]- [color cyan]95 [color white]- [color cyan]hatchet [color white]- [color red]"},
                {"BuyWeapons6", "[color yellow]Hunting Bow [color white]- [color cyan]96 [color white]- [color cyan]huntingbow [color white]- [color red]"},
                {"BuyWeapons7", "[color yellow]M4 [color white]- [color cyan]97 [color white]- [color cyan]m4 [color white]- [color red]"},
                {"BuyWeapons8", "[color yellow]MP5A4 [color white]- [color cyan]98 [color white]- [color cyan]mp5a4 [color white]- [color red]"},
                {"BuyWeapons9", "[color yellow]P250 [color white]- [color cyan]99 [color white]- [color cyan]p250 [color white]- [color red]"},
                {"BuyWeapons10", "[color yellow]Pipe Shotgun [color white]- [color cyan]100 [color white]- [color cyan]pipeshotgun [color white]- [color red]"},
                {"BuyWeapons11", "[color yellow]Revolver [color white]- [color cyan]101 [color white]- [color cyan]revolver [color white]- [color red]"},
                {"BuyWeapons12", "[color yellow]Rock [color white]- [color cyan]102 [color white]- [color cyan]rock [color white]- [color red]"},
                {"BuyWeapons13", "[color yellow]Shotgun [color white]- [color cyan]103 [color white]- [color cyan]shotgun [color white]- [color red]"},
                {"BuyWeapons14", "[color yellow]Stone Hatchet [color white]- [color cyan]104 [color white]- [color cyan]stonehatchet [color white]- [color red]"},
                {"BuyWeapons15", "[color yellow]Supply Signal [color white]- [color cyan]105 [color white]- [color cyan]supplysignal [color white]- [color red]"},
                {"BuyWeapons16", "[color yellow]Bolt Action Rifle [color white]- [color cyan]106 [color white]- [color cyan]boltactionrifle [color white]- [color red]"},
                {"BuyWeapons17", "[color yellow]Pickaxe [color white]- [color cyan]107 [color white]- [color cyan]pickaxe [color white]- [color red]"},
                {"ItemBought1", "You have purchased [color yellow]"},
                {"ItemBought2", "[color white]x [color green]"},
                {"ItemBought3", " [color white]for [color red]"},
                {"ItemBought4", " [color white]Drabloons"},
                //sell
                {"SellTitle", "[color green]-------------[color orange]AdvShop - Sell[color green]-------------"},
                {"SellCmdIntro1", "[color red]Use [color yellow]/sell 'category' 'page' [color red]to view items in a specific category."},
                {"SellCmdIntro2", "[color red]Use [color yellow]/sell 'item number/name in blue' to sell a specific item."},
                {"SellCmdIntro3", "[color cyan]The following categories are currently available:"},
                {"SellCate1", "[color yellow]Ammo"},
                {"SellCate2", "[color yellow]Armor"},
                {"SellCate3", "[color yellow]Food"},
                {"SellCate4", "[color yellow]Medical"},
                {"SellCate5", "[color yellow]Mods"},
                {"SellCate6", "[color yellow]Parts"},
                {"SellCate7", "[color yellow]Resources"},
                {"SellCate8", "[color yellow]Survival"},
                {"SellCate9", "[color yellow]Tools"},
                {"SellCate10", "[color yellow]Weapons"},
                {"SellTitleAmmo", "[color green]-------------[color orange]AdvShop - Sell - Ammo[color green]-------------"},
                {"SellAmmo1", "[color yellow]556 Ammo [color white]- [color cyan]1 [color white]- [color cyan]556ammo [color white]- [color red]"},
                {"SellAmmo2", "[color yellow]9mm Ammo [color white]- [color cyan]2 [color white]- [color cyan]9mmammo [color white]- [color red]"},
                {"SellAmmo3", "[color yellow]Arrow [color white]- [color cyan]3 [color white]- [color cyan]arrow [color white]- [color red]"},
                {"SellAmmo4", "[color yellow]Handmade Shell [color white]- [color cyan]4 [color white]- [color cyan]handmadeshell [color white]- [color red]"},
                {"SellAmmo5", "[color yellow]Shotgun Shells [color white]- [color cyan]5 [color white]- [color cyan]shotgunshell [color white]- [color red]"},
                {"SellPage", "[color green]-------------[color orange]Page 1/1[color green]-------------"},
                {"SellTitleArmor", "[color green]-------------[color orange]AdvShop - Sell - Armor[color green]-------------"},
                {"SellArmor1", "[color yellow]Cloth Helmet [color white]- [color cyan]6 [color white]- [color cyan]clothhelmet [color white]- [color red]"},
                {"SellArmor2", "[color yellow]Cloth Vest [color white]- [color cyan]7 [color white]- [color cyan]clothvest [color white]- [color red]"},
                {"SellArmor3", "[color yellow]Cloth Pants [color white]- [color cyan]8 [color white]- [color cyan]clothpants [color white]- [color red]"},
                {"SellArmor4", "[color yellow]Cloth Boots [color white]- [color cyan]9 [color white]- [color cyan]clothboots [color white]- [color red]"},
                {"SellArmor5", "[color yellow]Leather Helmet [color white]- [color cyan]10 [color white]- [color cyan]leatherhelmet [color white]- [color red]"},
                {"SellArmor6", "[color yellow]Leather Vest [color white]- [color cyan]11 [color white]- [color cyan]leathervest [color white]- [color red]"},
                {"SellArmor7", "[color yellow]Leather Pants [color white]- [color cyan]12 [color white]- [color cyan]leatherpants [color white]- [color red]"},
                {"SellArmor8", "[color yellow]Leather Boots [color white]- [color cyan]13 [color white]- [color cyan]leatherboots [color white]- [color red]"},
                {"SellArmor9", "[color yellow]Rad Suit Helmet [color white]- [color cyan]14 [color white]- [color cyan]radsuithelmet [color white]- [color red]"},
                {"SellArmor10", "[color yellow]Rad Suit Vest [color white]- [color cyan]15 [color white]- [color cyan]radsuitvest [color white]- [color red]"},
                {"SellArmor11", "[color yellow]Rad Suit Pants [color white]- [color cyan]16 [color white]- [color cyan]radsuitpants [color white]- [color red]"},
                {"SellArmor12", "[color yellow]Rad Suit Boots [color white]- [color cyan]17 [color white]- [color cyan]radsuitboots [color white]- [color red]"},
                {"SellArmor13", "[color yellow]Kevlar Helmet [color white]- [color cyan]18 [color white]- [color cyan]kevlarhelmet [color white]- [color red]"},
                {"SellArmor14", "[color yellow]Kevlar Vest [color white]- [color cyan]19 [color white]- [color cyan]kevlarvest [color white]- [color red]"},
                {"SellArmor15", "[color yellow]Kevlar Pants [color white]- [color cyan]20 [color white]- [color cyan]kevlarpants [color white]- [color red]"},
                {"SellArmor16", "[color yellow]Kevlar Boots [color white]- [color cyan]21 [color white]- [color cyan]kevlarboots [color white]- [color red]"},
                {"SellTitleFood", "[color green]-------------[color orange]AdvShop - Sell - Food[color green]-------------"},
                {"SellFood1", "[color yellow]Can of Beans [color white]- [color cyan]22 [color white]- [color cyan]canofbeans [color white]- [color red]"},
                {"SellFood2", "[color yellow]Can of Tuna [color white]- [color cyan]23 [color white]- [color cyan]canoftuna [color white]- [color red]"},
                {"SellFood3", "[color yellow]Chocolate Bar [color white]- [color cyan]24 [color white]- [color cyan]chocolatebar [color white]- [color red]"},
                {"SellFood4", "[color yellow]Cooked Chicken Breast [color white]- [color cyan]25 [color white]- [color cyan]cookedchickenbreast [color white]- [color red]"},
                {"SellFood5", "[color yellow]Granola Bar [color white]- [color cyan]26 [color white]- [color cyan]granolabar [color white]- [color red]"},
                {"SellFood6", "[color yellow]Raw Chicken Breast [color white]- [color cyan]27 [color white]- [color cyan]rawchickenbreast [color white]- [color red]"},
                {"SellFood7", "[color yellow]Small Rations [color white]- [color cyan]28 [color white]- [color cyan]smallrations [color white]- [color red]"},
                {"SellTitleMedical", "[color green]-------------[color orange]AdvShop - Sell - Medical[color green]-------------"},
                {"SellMedical1", "[color yellow]Anti-Radiation Pills [color white]- [color cyan]29 [color white]- [color cyan]antiradiationpills [color white]- [color red]"},
                {"SellMedical2", "[color yellow]Bandage [color white]- [color cyan]30 [color white]- [color cyan]bandage [color white]- [color red]"},
                {"SellMedical3", "[color yellow]Large Medkit [color white]- [color cyan]31 [color white]- [color cyan]largemedkit [color white]- [color red]"},
                {"SellMedical4", "[color yellow]Small Medkit [color white]- [color cyan]32 [color white]- [color cyan]smallmedkit [color white]- [color red]"},
                {"SellTitleMods", "[color green]-------------[color orange]AdvShop - Sell - Mods[color green]-------------"},
                {"SellMods1", "[color yellow]Flashlight Mod [color white]- [color cyan]33 [color white]- [color cyan]flashlightmod [color white]- [color red]"},
                {"SellMods2", "[color yellow]Holo Sight [color white]- [color cyan]34 [color white]- [color cyan]holosight [color white]- [color red]"},
                {"SellMods3", "[color yellow]Laser Sight [color white]- [color cyan]35 [color white]- [color cyan]lasersight [color white]- [color red]"},
                {"SellMods4", "[color yellow]Silencer [color white]- [color cyan]36 [color white]- [color cyan]silencer [color white]- [color red]"},
                {"SellTitleParts", "[color green]-------------[color orange]AdvShop - Sell - Parts[color green]-------------"},
                {"SellParts1", "[color yellow]Metal Ceiling [color white]- [color cyan]37 [color white]- [color cyan]metalceiling [color white]- [color red]"},
                {"SellParts2", "[color yellow]Metal Doorway [color white]- [color cyan]38 [color white]- [color cyan]metaldoorway [color white]- [color red]"},
                {"SellParts3", "[color yellow]Metal Foundation [color white]- [color cyan]39 [color white]- [color cyan]metalfoundation [color white]- [color red]"},
                {"SellParts4", "[color yellow]Metal Pillar [color white]- [color cyan]40 [color white]- [color cyan]metalpillar [color white]- [color red]"},
                {"SellParts5", "[color yellow]Metal Ramp [color white]- [color cyan]41 [color white]- [color cyan]metalramp [color white]- [color red]"},
                {"SellParts6", "[color yellow]Metal Stairs [color white]- [color cyan]42 [color white]- [color cyan]metalstairs [color white]- [color red]"},
                {"SellParts7", "[color yellow]Metal Wall [color white]- [color cyan]43 [color white]- [color cyan]metalwall [color white]- [color red]"},
                {"SellParts8", "[color yellow]Metal Window [color white]- [color cyan]44 [color white]- [color cyan]metalwindow [color white]- [color red]"},
                {"SellParts9", "[color yellow]Metal Window Bars [color white]- [color cyan]45 [color white]- [color cyan]metalwindowbars [color white]- [color red]"},
                {"SellParts10", "[color yellow]Wood Ceiling [color white]- [color cyan]46 [color white]- [color cyan]woodceiling [color white]- [color red]"},
                {"SellParts11", "[color yellow]Wood Doorway [color white]- [color cyan]47 [color white]- [color cyan]wooddoorway [color white]- [color red]"},
                {"SellParts12", "[color yellow]Wood Foundation [color white]- [color cyan]48 [color white]- [color cyan]woodfoundation [color white]- [color red]"},
                {"SellParts13", "[color yellow]Wood Pillar [color white]- [color cyan]49 [color white]- [color cyan]woodpillar [color white]- [color red]"},
                {"SellParts14", "[color yellow]Wood Ramp [color white]- [color cyan]50 [color white]- [color cyan]woodramp [color white]- [color red]"},
                {"SellParts15", "[color yellow]Wood Stairs [color white]- [color cyan]51 [color white]- [color cyan]woodstairs [color white]- [color red]"},
                {"SellParts16", "[color yellow]Wood Wall [color white]- [color cyan]52 [color white]- [color cyan]woodwall [color white]- [color red]"},
                {"SellParts17", "[color yellow]Wood Window [color white]- [color cyan]53 [color white]- [color cyan]woodwindow [color white]- [color red]"},
                {"SellTitleResources", "[color green]-------------[color orange]AdvShop - Sell - Resources[color green]-------------"},
                {"SellResources1", "[color yellow]Animal Fat [color white]- [color cyan]54 [color white]- [color cyan]animalfat [color white]- [color red]"},
                {"SellResources2", "[color yellow]Blood [color white]- [color cyan]55 [color white]- [color cyan]blood [color white]- [color red]"},
                {"SellResources3", "[color yellow]Cloth [color white]- [color cyan]56 [color white]- [color cyan]cloth [color white]- [color red]"},
                {"SellResources4", "[color yellow]Explosives [color white]- [color cyan]57 [color white]- [color cyan]explosives [color white]- [color red]"},
                {"SellResources5", "[color yellow]Gunpowder [color white]- [color cyan]58 [color white]- [color cyan]gunpowder [color white]- [color red]"},
                {"SellResources6", "[color yellow]Leather [color white]- [color cyan]59 [color white]- [color cyan]leather [color white]- [color red]"},
                {"SellResources7", "[color yellow]Metal Fragments [color white]- [color cyan]60 [color white]- [color cyan]metalfragments [color white]- [color red]"},
                {"SellResources8", "[color yellow]Metal Ore [color white]- [color cyan]61 [color white]- [color cyan]metalore [color white]- [color red]"},
                {"SellResources9", "[color yellow]Paper [color white]- [color cyan]62 [color white]- [color cyan]paper [color white]- [color red]"},
                {"SellResources10", "[color yellow]Stones [color white]- [color cyan]63 [color white]- [color cyan]stones [color white]- [color red]"},
                {"SellResources11", "[color yellow]Sulfur [color white]- [color cyan]64 [color white]- [color cyan]sulfur [color white]- [color red]"},
                {"SellResources12", "[color yellow]Sulfur Ore [color white]- [color cyan]65 [color white]- [color cyan]sulfurore [color white]- [color red]"},
                {"SellResources13", "[color yellow]Wood [color white]- [color cyan]66 [color white]- [color cyan]wood [color white]- [color red]"},
                {"SellResources14", "[color yellow]Wood Planks [color white]- [color cyan]67 [color white]- [color cyan]woodplanks [color white]- [color red]"},
                {"SellTitleSurvival", "[color green]-------------[color orange]AdvShop - Sell - Survival[color green]-------------"},
                {"SellSurvival1", "[color yellow]Bed [color white]- [color cyan]68 [color white]- [color cyan]bed [color white]- [color red]"},
                {"SellSurvival2", "[color yellow]Camp Fire [color white]- [color cyan]69 [color white]- [color cyan]campfire [color white]- [color red]"},
                {"SellSurvival3", "[color yellow]Furnace [color white]- [color cyan]70 [color white]- [color cyan]furnace [color white]- [color red]"},
                {"SellSurvival4", "[color yellow]Large Spike Wall [color white]- [color cyan]71 [color white]- [color cyan]largespikewall [color white]- [color red]"},
                {"SellSurvival5", "[color yellow]Large Wood Storage [color white]- [color cyan]72 [color white]- [color cyan]largewoodstorage [color white]- [color red]"},
                {"SellSurvival6", "[color yellow]Low Grade Fuel [color white]- [color cyan]73 [color white]- [color cyan]lowgradefuel [color white]- [color red]"},
                {"SellSurvival7", "[color yellow]Low Quality Metal [color white]- [color cyan]74 [color white]- [color cyan]lowqualitymetal [color white]- [color red]"},
                {"SellSurvival8", "[color yellow]Metal Door [color white]- [color cyan]75 [color white]- [color cyan]metaldoor [color white]- [color red]"},
                {"SellSurvival9", "[color yellow]Repair Bench [color white]- [color cyan]76 [color white]- [color cyan]repairbench [color white]- [color red]"},
                {"SellSurvival10", "[color yellow]Sleeping Bag [color white]- [color cyan]77 [color white]- [color cyan]sleepingbag [color white]- [color red]"},
                {"SellSurvival11", "[color yellow]Small Stash [color white]- [color cyan]78 [color white]- [color cyan]smallstash [color white]- [color red]"},
                {"SellSurvival12", "[color yellow]Spike Wall [color white]- [color cyan]79 [color white]- [color cyan]spikewall [color white]- [color red]"},
                {"SellSurvival13", "[color yellow]Torch [color white]- [color cyan]80 [color white]- [color cyan]torch [color white]- [color red]"},
                {"SellSurvival14", "[color yellow]Wood Barricade [color white]- [color cyan]81 [color white]- [color cyan]woodbarricade [color white]- [color red]"},
                {"SellSurvival15", "[color yellow]Wood Gate [color white]- [color cyan]82 [color white]- [color cyan]woodgate [color white]- [color red]"},
                {"SellSurvival16", "[color yellow]Wood Gateway [color white]- [color cyan]83 [color white]- [color cyan]woodgateway [color white]- [color red]"},
                {"SellSurvival17", "[color yellow]Wood Shelter [color white]- [color cyan]84 [color white]- [color cyan]woodshelter [color white]- [color red]"},
                {"SellSurvival18", "[color yellow]Wood Storage Box [color white]- [color cyan]85 [color white]- [color cyan]woodstoragebox [color white]- [color red]"},
                {"SellSurvival19", "[color yellow]Wooden Door [color white]- [color cyan]86 [color white]- [color cyan]woodendoor [color white]- [color red]"},
                {"SellSurvival20", "[color yellow]Workbench [color white]- [color cyan]87 [color white]- [color cyan]workbench [color white]- [color red]"},
                {"SellTitleTools", "[color green]-------------[color orange]AdvShop - Sell - Tools[color green]-------------"},
                {"SellTools1", "[color yellow]Blood Draw Kit [color white]- [color cyan]88 [color white]- [color cyan]blooddrawkit [color white]- [color red]"},
                {"SellTools2", "[color yellow]Homemade Lockpick [color white]- [color cyan]89 [color white]- [color cyan]homemadelockpick [color white]- [color red]"},
                {"SellTools3", "[color yellow]Research Kit [color white]- [color cyan]90 [color white]- [color cyan]researchkit [color white]- [color red]"},
                {"SellTitleWeapons", "[color green]-------------[color orange]AdvShop - Sell - Tools[color green]-------------"},
                {"SellWeapons1", "[color yellow]9mm Pistol [color white]- [color cyan]91 [color white]- [color cyan]9mmpistol [color white]- [color red]"},
                {"SellWeapons2", "[color yellow]Explosive Charge [color white]- [color cyan]92 [color white]- [color cyan]explosivecharge [color white]- [color red]"},
                {"SellWeapons3", "[color yellow]F1 Grenade [color white]- [color cyan]93 [color white]- [color cyan]f1grenade [color white]- [color red]"},
                {"SellWeapons4", "[color yellow]Hand Cannon [color white]- [color cyan]94 [color white]- [color cyan]handcannon [color white]- [color red]"},
                {"SellWeapons5", "[color yellow]Hatchet [color white]- [color cyan]95 [color white]- [color cyan]hatchet [color white]- [color red]"},
                {"SellWeapons6", "[color yellow]Hunting Bow [color white]- [color cyan]96 [color white]- [color cyan]huntingbow [color white]- [color red]"},
                {"SellWeapons7", "[color yellow]M4 [color white]- [color cyan]97 [color white]- [color cyan]m4 [color white]- [color red]"},
                {"SellWeapons8", "[color yellow]MP5A4 [color white]- [color cyan]98 [color white]- [color cyan]mp5a4 [color white]- [color red]"},
                {"SellWeapons9", "[color yellow]P250 [color white]- [color cyan]99 [color white]- [color cyan]p250 [color white]- [color red]"},
                {"SellWeapons10", "[color yellow]Pipe Shotgun [color white]- [color cyan]100 [color white]- [color cyan]pipeshotgun [color white]- [color red]"},
                {"SellWeapons11", "[color yellow]Revolver [color white]- [color cyan]101 [color white]- [color cyan]revolver [color white]- [color red]"},
                {"SellWeapons12", "[color yellow]Rock [color white]- [color cyan]102 [color white]- [color cyan]rock [color white]- [color red]"},
                {"SellWeapons13", "[color yellow]Shotgun [color white]- [color cyan]103 [color white]- [color cyan]shotgun [color white]- [color red]"},
                {"SellWeapons14", "[color yellow]Stone Hatchet [color white]- [color cyan]104 [color white]- [color cyan]stonehatchet [color white]- [color red]"},
                {"SellWeapons15", "[color yellow]Supply Signal [color white]- [color cyan]105 [color white]- [color cyan]supplysignal [color white]- [color red]"},
                {"SellWeapons16", "[color yellow]Bolt Action Rifle [color white]- [color cyan]106 [color white]- [color cyan]boltactionrifle [color white]- [color red]"},
                {"SellWeapons17", "[color yellow]Pickaxe [color white]- [color cyan]107 [color white]- [color cyan]pickaxe [color white]- [color red]"},
                {"ItemSold1", "You have sold [color yellow]"},
                {"ItemSold2", "[color white]x [color green]"},
                {"ItemSold3", " [color white]for [color red]"},
                {"ItemSold4", " [color white]Drabloons"},                
                //currency
                {"CurrencyTitle", "[color green]-------------[color orange]AdvShop - Currency[color green]-------------"},
                {"Currencyintro", "The currency that is primarily used by this plugin is called: [color cyan]Drabloons"},
                {"Currencymaking1", "There are two ways to gain [color cyan]Drabloons[color white]. First you can sell items."},
                {"Currencymaking2", "Second you can purchase some through [color yellow]/iap"},
                {"Currencyavailable", "[color yellow]You currently have [color cyan]"},
                {"Currencyname", "[color yellow] Drabloons."},
                {"Insufficientcurrency", "You do not have enough Drabloons for "},
                //currencyadmin
                {"CurrencyAdminTitle", "[color green]-------------[color orange]AdvShop - Currency - Admin[color green]-------------"},
                {"Syntaxca", "Use [color orange] /currencyadmin 'command' 'username' 'amount'"},
                {"Option1ca", "[color yellow]add [color white]- Adds 'X' amount"},
                {"Option2ca", "[color yellow]sub [color white]- Subtracts 'X' amount"},
                {"CurrencyAdminTitleAdd", "[color green]-------------[color orange]AdvShop - Currency - Admin - Add[color green]-------------"},
                {"CurrencyAdminTitleSub", "[color green]-------------[color orange]AdvShop - Currency - Admin - Sub[color green]-------------"},
            };
            lang.RegisterMessages(messages, this);
        }
        void Loaded()
        {
            permission.RegisterPermission("advshop.allowed", this);
            LoadDefaultMessages();
            advshop = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Player>>("AdvShop");
            Puts(GetMessage("AdvShop created by BDM has loaded!"));
        }
        void Unload()
        {
            SaveData();
        }
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("AdvShop", advshop);
        }
        [ChatCommand("shop")]
        void cmdShop(NetUser netuser, string command, string[] args)
        {
            ShopControl(netuser);
        }
        [ChatCommand("buy")]
        void cmdBuy(NetUser netuser, string command, string[] args)
        {
            if (args.Length == 0)
            {
                BuyFrontControl(netuser);
            }
            else if (args[0].ToLower() == "ammo")
            {
                BuyAmmoControl(netuser);
            }
            else if (args[0].ToLower() == "armor")
            {
                if (args.Length > 2)
                {
                    BuyArmorControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    BuyArmorControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    BuyArmorControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    BuyArmorControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "food")
            {
                BuyFoodControl(netuser);
            }
            else if (args[0].ToLower() == "medical")
            {
                BuyMedicalControl(netuser);
            }
            else if (args[0].ToLower() == "mods")
            {
                BuyModsControl(netuser);
            }
            else if (args[0].ToLower() == "parts")
            {
                if (args.Length > 2)
                {
                    BuyPartsControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    BuyPartsControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    BuyPartsControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    BuyPartsControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "resources")
            {
                if (args.Length > 2)
                {
                    BuyResourcesControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    BuyResourcesControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    BuyResourcesControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    BuyResourcesControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "survival")
            {
                if (args.Length > 2)
                {
                    BuySurvivalControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    BuySurvivalControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    BuySurvivalControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    BuySurvivalControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "tools")
            {
                BuyToolsControl(netuser);
            }
            else if (args[0].ToLower() == "weapons")
            {
                if (args.Length > 2)
                {
                    BuyWeaponsControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    BuyWeaponsControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    BuyWeaponsControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    BuyWeaponsControl1(netuser);
                }
            }
            else
            {
                if (args.Length == 1)
                {
                    int quantity = 1;
                    BuyControllerQuantity(netuser, args[0], quantity);
                }
                else if (args.Length == 2)
                {
                    string quan = args[1];
                    int quantity = Int32.Parse(quan);
                    BuyControllerQuantity(netuser, args[0], quantity);
                }
            }
        }
        [ChatCommand("sell")]
        void cmdSell(NetUser netuser, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SellFrontControl(netuser);
            }
            else if (args[0].ToLower() == "ammo")
            {
                SellAmmoControl(netuser);
            }
            else if (args[0].ToLower() == "armor")
            {
                if (args.Length > 2)
                {
                    SellArmorControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    SellArmorControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    SellArmorControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    SellArmorControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "food")
            {
                SellFoodControl(netuser);
            }
            else if (args[0].ToLower() == "medical")
            {
                SellMedicalControl(netuser);
            }
            else if (args[0].ToLower() == "mods")
            {
                SellModsControl(netuser);
            }
            else if (args[0].ToLower() == "parts")
            {
                if (args.Length > 2)
                {
                    SellPartsControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    SellPartsControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    SellPartsControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    SellPartsControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "resources")
            {
                if (args.Length > 2)
                {
                    SellResourcesControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    SellResourcesControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    SellResourcesControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    SellResourcesControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "survival")
            {
                if (args.Length > 2)
                {
                    SellSurvivalControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    SellSurvivalControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    SellSurvivalControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    SellSurvivalControl1(netuser);
                }
            }
            else if (args[0].ToLower() == "tools")
            {
                SellToolsControl(netuser);
            }
            else if (args[0].ToLower() == "weapons")
            {
                if (args.Length > 2)
                {
                    SellWeaponsControl1(netuser);
                }
                else if (args.Length == 2 && args[1] == "2")
                {
                    SellWeaponsControl2(netuser);
                }
                else if (args.Length == 2 && args[1] == "1")
                {
                    SellWeaponsControl1(netuser);
                }
                else if (args.Length == 1)
                {
                    SellWeaponsControl1(netuser);
                }
            }
            else
            {
                if (args.Length == 1)
                {
                    SellController(netuser, args[0]);
                }
            }        
        }
        //[ChatCommand("trade")]
        //void cmdTrade(NetUser netuser, string command, string[] args)
        //{
        //    ShopControl(netuser);
       // }
        [ChatCommand("currency")]
        void cmdCurrency(NetUser netuser, string command, string[] args)
        {
            CurrencyControl(netuser);
        }
        [ChatCommand("currencyadmin")]
        void cmdCurrencyAdmin(NetUser netuser, string command, string[] args)
        {
            if (!permission.UserHasPermission(netuser.playerClient.userID.ToString(), "advshop.allowed"))
            {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("NoPerm", netuser.userID.ToString()));
            }
            else
            {
                if (args.Length == 0)
                {
                    CurrencyAdminControl1(netuser); 
                }
                else
                {
                string commands = args[0].ToString();
                if (args.Length == 1 && commands == "add")
                {
                    CurrencyAdminControl2(netuser);               
                }
                else if (args.Length == 1 && commands == "sub")
                {
                    CurrencyAdminControl3(netuser);               
                }       
                else if (args.Length == 2 && commands == "add")
                {
                    NetUser targetuser = rust.FindPlayer(args[1]);
                    if (targetuser != null)
                    {
                        CurrencyAdminControl4(netuser, targetuser.userID.ToString());               
                    }                
                }                
                else if (args.Length == 2 && commands == "sub")
                {
                    NetUser targetuser = rust.FindPlayer(args[1]);
                    if (targetuser != null)
                    {
                        CurrencyAdminControl5(netuser, targetuser.userID.ToString());                       
                    }                
                }
                else if (args.Length == 3 && commands == "add")
                {
                    NetUser targetuser = rust.FindPlayer(args[1]);
                    if (targetuser != null)
                    {
                        CurrencyAdminControl6(netuser, targetuser.userID.ToString(), Convert.ToDouble(args[2]));                       
                    }                
                }                
                else if (args.Length == 3 && commands == "sub")
                {
                    NetUser targetuser = rust.FindPlayer(args[1]);
                    if (targetuser != null)
                    {
                        CurrencyAdminControl7(netuser, targetuser.userID.ToString(), Convert.ToDouble(args[2]));                         
                    }                
                }
            }
            }
        }        [ChatCommand("iap")]
        void cmdIap(NetUser netuser, string command, string[] args)
        {
            ShopControl(netuser);
        }
        [ChatCommand("wipe")]
        void cmdWipe(NetUser netuser, string command, string[] args)
        {
            if (!permission.UserHasPermission(netuser.playerClient.userID.ToString(), "advshop.allowed"))
            {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("NoPerm", netuser.userID.ToString()));
            }
            else
            {
            advshop.Clear();
            SaveData();
            }
        }






        void ShopControl(NetUser netuser)
        {
            rust.Notice(netuser, GetMessage("ShopNotice", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopTitle", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd1", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd2", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd3", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd4", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd5", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd6", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd7", netuser.userID.ToString()));
            if (permission.UserHasPermission(netuser.playerClient.userID.ToString(), "advshop.allowed"))
            {
                rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd8", netuser.userID.ToString()));
                rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd9", netuser.userID.ToString()));
                rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd10", netuser.userID.ToString()));
                rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd11", netuser.userID.ToString()));
                rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ShopCmd12", netuser.userID.ToString()));
            }
        }
        void BuyFrontControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitle", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCmdIntro1", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCmdIntro2", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCmdIntro3", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate1", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate2", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate3", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate4", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate5", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate6", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate7", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate8", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate9", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyCate10", netuser.userID.ToString()));
        }
        void BuyAmmoControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleAmmo", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo1", netuser.userID.ToString()) + Ammo556.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo2", netuser.userID.ToString()) + Ammo9mm.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo3", netuser.userID.ToString()) + Ammoarrow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo4", netuser.userID.ToString()) + Ammohandmadeshell.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo5", netuser.userID.ToString()) + Ammoshotgunshell.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("AmmoPage", netuser.userID.ToString()));
        }
        void BuyArmorControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleArmor", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor1", netuser.userID.ToString()) + Armorclothhelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor2", netuser.userID.ToString()) + Armorclothvest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor3", netuser.userID.ToString()) + Armorclothpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor4", netuser.userID.ToString()) + Armorclothboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor5", netuser.userID.ToString()) + Armorleatherhelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor6", netuser.userID.ToString()) + Armorleathervest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor7", netuser.userID.ToString()) + Armorleatherpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor8", netuser.userID.ToString()) + Armorleatherboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor9", netuser.userID.ToString()) + Armorradsuithelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor10", netuser.userID.ToString()) + Armorradsuitvest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor11", netuser.userID.ToString()) + Armorradsuitpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor12", netuser.userID.ToString()) + Armorradsuitboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ArmorPage1", netuser.userID.ToString()));
        }
        void BuyArmorControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleArmor", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor13", netuser.userID.ToString()) + Armorkevlarhelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor14", netuser.userID.ToString()) + Armorkevlarvest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor15", netuser.userID.ToString()) + Armorkevlarpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor16", netuser.userID.ToString()) + Armorkevlarboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ArmorPage2", netuser.userID.ToString()));
        }
        void BuyFoodControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleFood", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood1", netuser.userID.ToString()) + Foodcanofbeans.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood2", netuser.userID.ToString()) + Foodcanoftuna.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood3", netuser.userID.ToString()) + Foodchocolatebar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood4", netuser.userID.ToString()) + Foodcookedchickenbreast.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood5", netuser.userID.ToString()) + Foodgranolabar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood6", netuser.userID.ToString()) + Foodrawchickenbreast.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood7", netuser.userID.ToString()) + Foodsmallrations.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("FoodPage1", netuser.userID.ToString()));
        }
        void BuyMedicalControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleMedical", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical1", netuser.userID.ToString()) + Medicalantiradpills.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical2", netuser.userID.ToString()) + Medicalbandage.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical3", netuser.userID.ToString()) + Medicallargemedkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical4", netuser.userID.ToString()) + Medicalsmallmedkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("MedicalPage1", netuser.userID.ToString()));
        }
        void BuyModsControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleMods", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods1", netuser.userID.ToString()) + Modsflashlightmod.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods2", netuser.userID.ToString()) + Modsholosight.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods3", netuser.userID.ToString()) + Modslasersight.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods4", netuser.userID.ToString()) + Modssilencer.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ModsPage1", netuser.userID.ToString()));
        }
        void BuyPartsControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleParts", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts1", netuser.userID.ToString()) + Partsmetalceiling.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts2", netuser.userID.ToString()) + Partsmetaldoorway.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts3", netuser.userID.ToString()) + Partsmetalfoundation.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts4", netuser.userID.ToString()) + Partsmetalpillar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts5", netuser.userID.ToString()) + Partsmetalramp.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts6", netuser.userID.ToString()) + Partsmetalstairs.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts7", netuser.userID.ToString()) + Partsmetalwall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts8", netuser.userID.ToString()) + Partsmetalwindow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts9", netuser.userID.ToString()) + Partsmetalwindowbars.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts10", netuser.userID.ToString()) + Partswoodceiling.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts11", netuser.userID.ToString()) + Partswooddoorway.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts12", netuser.userID.ToString()) + Partswoodfoundation.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("PartsPage1", netuser.userID.ToString()));
        }
        void BuyPartsControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleParts", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts13", netuser.userID.ToString()) + Partswoodpillar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts14", netuser.userID.ToString()) + Partswoodramp.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts15", netuser.userID.ToString()) + Partswoodstairs.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts16", netuser.userID.ToString()) + Partswoodwall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts17", netuser.userID.ToString()) + Partswoodwindow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("PartsPage2", netuser.userID.ToString()));
        }
        void BuyResourcesControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleResources", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources1", netuser.userID.ToString()) + Resourcesanimalfat.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources2", netuser.userID.ToString()) + Resourcesblood.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources3", netuser.userID.ToString()) + Resourcescloth.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources4", netuser.userID.ToString()) + Resourcesexplosives.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources5", netuser.userID.ToString()) + Resourcesgunpowder.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources6", netuser.userID.ToString()) + Resourcesleather.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources7", netuser.userID.ToString()) + Resourcesmetalfragments.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources8", netuser.userID.ToString()) + Resourcesmetalore.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources9", netuser.userID.ToString()) + Resourcespaper.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources10", netuser.userID.ToString()) + Resourcesstones.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources11", netuser.userID.ToString()) + Resourcessulfur.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources12", netuser.userID.ToString()) + Resourcessulfurore.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ResourcesPage1", netuser.userID.ToString()));
        }
        void BuyResourcesControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleResources", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources13", netuser.userID.ToString()) + Resourceswood.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources14", netuser.userID.ToString()) + Resourceswoodplanks.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ResourcesPage2", netuser.userID.ToString()));
        }
        void BuySurvivalControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleSurvival", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival1", netuser.userID.ToString()) + Survivalbed.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival2", netuser.userID.ToString()) + Survivalcampfire.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival3", netuser.userID.ToString()) + Survivalfurnace.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival4", netuser.userID.ToString()) + Survivallargespikewall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival5", netuser.userID.ToString()) + Survivallargewoodstorage.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival6", netuser.userID.ToString()) + Survivallowgradefuel.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival7", netuser.userID.ToString()) + Survivallowqualitymetal.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival8", netuser.userID.ToString()) + Survivalmetaldoor.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival9", netuser.userID.ToString()) + Survivalrepairbench.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival10", netuser.userID.ToString()) + Survivalsleepingbag.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival11", netuser.userID.ToString()) + Survivalsmallstash.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival12", netuser.userID.ToString()) + Survivalspikewall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SurvivalPage1", netuser.userID.ToString()));
        }
        void BuySurvivalControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleSurvival", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival13", netuser.userID.ToString()) + Survivaltorch.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival14", netuser.userID.ToString()) + Survivalwoodbarricade.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival15", netuser.userID.ToString()) + Survivalwoodendoor.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival16", netuser.userID.ToString()) + Survivalwoodgate.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival17", netuser.userID.ToString()) + Survivalwoodgateway.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival18", netuser.userID.ToString()) + Survivalwoodshelter.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival19", netuser.userID.ToString()) + Survivalwoodstoragebox.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival20", netuser.userID.ToString()) + Survivalworkbench.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SurvivalPage2", netuser.userID.ToString()));
        }
        void BuyToolsControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleTools", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTools1", netuser.userID.ToString()) + Toolsblooddrawkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTools2", netuser.userID.ToString()) + Toolshandmadelockpick.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTools3", netuser.userID.ToString()) + Toolsresearchkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ToolsPage1", netuser.userID.ToString()));
        }
        void BuyWeaponsControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleWeapons", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons1", netuser.userID.ToString()) + Weapons9mmpistol.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons2", netuser.userID.ToString()) + Weaponsexplosivecharge.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons3", netuser.userID.ToString()) + Weaponsf1grenade.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons4", netuser.userID.ToString()) + Weaponshandcannon.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons5", netuser.userID.ToString()) + Weaponshatchet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons6", netuser.userID.ToString()) + Weaponshuntingbow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons7", netuser.userID.ToString()) + Weaponsm4.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons8", netuser.userID.ToString()) + Weaponsmp5a4.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons9", netuser.userID.ToString()) + Weaponsp250.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons10", netuser.userID.ToString()) + Weaponspipeshotgun.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons11", netuser.userID.ToString()) + Weaponsrevolver.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons12", netuser.userID.ToString()) + Weaponsrock.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("WeaponsPage1", netuser.userID.ToString()));
        }
        void BuyWeaponsControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleWeapons", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons13", netuser.userID.ToString()) + Weaponsshotgun.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons14", netuser.userID.ToString()) + Weaponsstonehatchet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons15", netuser.userID.ToString()) + Weaponssupplysignal.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons16", netuser.userID.ToString()) + Weaponsboltactionrifle.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons17", netuser.userID.ToString()) + Weaponspickaxe.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("WeaponsPage2", netuser.userID.ToString()));
        }



        void SellFrontControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellTitle", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCmdIntro1", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCmdIntro2", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCmdIntro3", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate1", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate2", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate3", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate4", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate5", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate6", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate7", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate8", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate9", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SellCate10", netuser.userID.ToString()));
        }
        void SellAmmoControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleAmmo", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo1", netuser.userID.ToString()) + Ammo556.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo2", netuser.userID.ToString()) + Ammo9mm.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo3", netuser.userID.ToString()) + Ammoarrow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo4", netuser.userID.ToString()) + Ammohandmadeshell.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyAmmo5", netuser.userID.ToString()) + Ammoshotgunshell.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("AmmoPage", netuser.userID.ToString()));
        }
        void SellArmorControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleArmor", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor1", netuser.userID.ToString()) + Armorclothhelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor2", netuser.userID.ToString()) + Armorclothvest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor3", netuser.userID.ToString()) + Armorclothpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor4", netuser.userID.ToString()) + Armorclothboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor5", netuser.userID.ToString()) + Armorleatherhelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor6", netuser.userID.ToString()) + Armorleathervest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor7", netuser.userID.ToString()) + Armorleatherpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor8", netuser.userID.ToString()) + Armorleatherboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor9", netuser.userID.ToString()) + Armorradsuithelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor10", netuser.userID.ToString()) + Armorradsuitvest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor11", netuser.userID.ToString()) + Armorradsuitpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor12", netuser.userID.ToString()) + Armorradsuitboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ArmorPage1", netuser.userID.ToString()));
        }
        void SellArmorControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleArmor", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor13", netuser.userID.ToString()) + Armorkevlarhelmet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor14", netuser.userID.ToString()) + Armorkevlarvest.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor15", netuser.userID.ToString()) + Armorkevlarpants.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyArmor16", netuser.userID.ToString()) + Armorkevlarboots.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ArmorPage2", netuser.userID.ToString()));
        }
        void SellFoodControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleFood", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood1", netuser.userID.ToString()) + Foodcanofbeans.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood2", netuser.userID.ToString()) + Foodcanoftuna.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood3", netuser.userID.ToString()) + Foodchocolatebar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood4", netuser.userID.ToString()) + Foodcookedchickenbreast.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood5", netuser.userID.ToString()) + Foodgranolabar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood6", netuser.userID.ToString()) + Foodrawchickenbreast.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyFood7", netuser.userID.ToString()) + Foodsmallrations.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("FoodPage1", netuser.userID.ToString()));
        }
        void SellMedicalControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleMedical", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical1", netuser.userID.ToString()) + Medicalantiradpills.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical2", netuser.userID.ToString()) + Medicalbandage.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical3", netuser.userID.ToString()) + Medicallargemedkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMedical4", netuser.userID.ToString()) + Medicalsmallmedkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("MedicalPage1", netuser.userID.ToString()));
        }
        void SellModsControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleMods", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods1", netuser.userID.ToString()) + Modsflashlightmod.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods2", netuser.userID.ToString()) + Modsholosight.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods3", netuser.userID.ToString()) + Modslasersight.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyMods4", netuser.userID.ToString()) + Modssilencer.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ModsPage1", netuser.userID.ToString()));
        }
        void SellPartsControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleParts", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts1", netuser.userID.ToString()) + Partsmetalceiling.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts2", netuser.userID.ToString()) + Partsmetaldoorway.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts3", netuser.userID.ToString()) + Partsmetalfoundation.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts4", netuser.userID.ToString()) + Partsmetalpillar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts5", netuser.userID.ToString()) + Partsmetalramp.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts6", netuser.userID.ToString()) + Partsmetalstairs.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts7", netuser.userID.ToString()) + Partsmetalwall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts8", netuser.userID.ToString()) + Partsmetalwindow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts9", netuser.userID.ToString()) + Partsmetalwindowbars.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts10", netuser.userID.ToString()) + Partswoodceiling.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts11", netuser.userID.ToString()) + Partswooddoorway.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts12", netuser.userID.ToString()) + Partswoodfoundation.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("PartsPage1", netuser.userID.ToString()));
        }
        void SellPartsControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleParts", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts13", netuser.userID.ToString()) + Partswoodpillar.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts14", netuser.userID.ToString()) + Partswoodramp.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts15", netuser.userID.ToString()) + Partswoodstairs.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts16", netuser.userID.ToString()) + Partswoodwall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyParts17", netuser.userID.ToString()) + Partswoodwindow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("PartsPage2", netuser.userID.ToString()));
        }
        void SellResourcesControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleResources", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources1", netuser.userID.ToString()) + Resourcesanimalfat.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources2", netuser.userID.ToString()) + Resourcesblood.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources3", netuser.userID.ToString()) + Resourcescloth.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources4", netuser.userID.ToString()) + Resourcesexplosives.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources5", netuser.userID.ToString()) + Resourcesgunpowder.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources6", netuser.userID.ToString()) + Resourcesleather.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources7", netuser.userID.ToString()) + Resourcesmetalfragments.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources8", netuser.userID.ToString()) + Resourcesmetalore.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources9", netuser.userID.ToString()) + Resourcespaper.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources10", netuser.userID.ToString()) + Resourcesstones.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources11", netuser.userID.ToString()) + Resourcessulfur.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources12", netuser.userID.ToString()) + Resourcessulfurore.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ResourcesPage1", netuser.userID.ToString()));
        }
        void SellResourcesControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleResources", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources13", netuser.userID.ToString()) + Resourceswood.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyResources14", netuser.userID.ToString()) + Resourceswoodplanks.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ResourcesPage2", netuser.userID.ToString()));
        }
        void SellSurvivalControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleSurvival", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival1", netuser.userID.ToString()) + Survivalbed.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival2", netuser.userID.ToString()) + Survivalcampfire.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival3", netuser.userID.ToString()) + Survivalfurnace.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival4", netuser.userID.ToString()) + Survivallargespikewall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival5", netuser.userID.ToString()) + Survivallargewoodstorage.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival6", netuser.userID.ToString()) + Survivallowgradefuel.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival7", netuser.userID.ToString()) + Survivallowqualitymetal.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival8", netuser.userID.ToString()) + Survivalmetaldoor.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival9", netuser.userID.ToString()) + Survivalrepairbench.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival10", netuser.userID.ToString()) + Survivalsleepingbag.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival11", netuser.userID.ToString()) + Survivalsmallstash.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival12", netuser.userID.ToString()) + Survivalspikewall.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SurvivalPage1", netuser.userID.ToString()));
        }
        void SellSurvivalControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleSurvival", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival13", netuser.userID.ToString()) + Survivaltorch.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival14", netuser.userID.ToString()) + Survivalwoodbarricade.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival15", netuser.userID.ToString()) + Survivalwoodendoor.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival16", netuser.userID.ToString()) + Survivalwoodgate.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival17", netuser.userID.ToString()) + Survivalwoodgateway.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival18", netuser.userID.ToString()) + Survivalwoodshelter.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival19", netuser.userID.ToString()) + Survivalwoodstoragebox.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuySurvival20", netuser.userID.ToString()) + Survivalworkbench.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("SurvivalPage2", netuser.userID.ToString()));
        }
        void SellToolsControl(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleTools", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTools1", netuser.userID.ToString()) + Toolsblooddrawkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTools2", netuser.userID.ToString()) + Toolshandmadelockpick.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTools3", netuser.userID.ToString()) + Toolsresearchkit.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ToolsPage1", netuser.userID.ToString()));
        }
        void SellWeaponsControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleWeapons", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons1", netuser.userID.ToString()) + Weapons9mmpistol.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons2", netuser.userID.ToString()) + Weaponsexplosivecharge.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons3", netuser.userID.ToString()) + Weaponsf1grenade.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons4", netuser.userID.ToString()) + Weaponshandcannon.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons5", netuser.userID.ToString()) + Weaponshatchet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons6", netuser.userID.ToString()) + Weaponshuntingbow.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons7", netuser.userID.ToString()) + Weaponsm4.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons8", netuser.userID.ToString()) + Weaponsmp5a4.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons9", netuser.userID.ToString()) + Weaponsp250.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons10", netuser.userID.ToString()) + Weaponspipeshotgun.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons11", netuser.userID.ToString()) + Weaponsrevolver.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons12", netuser.userID.ToString()) + Weaponsrock.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("WeaponsPage1", netuser.userID.ToString()));
        }
        void SellWeaponsControl2(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyTitleWeapons", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons13", netuser.userID.ToString()) + Weaponsshotgun.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons14", netuser.userID.ToString()) + Weaponsstonehatchet.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons15", netuser.userID.ToString()) + Weaponssupplysignal.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons16", netuser.userID.ToString()) + Weaponsboltactionrifle.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("BuyWeapons17", netuser.userID.ToString()) + Weaponspickaxe.ToString());
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Page", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("WeaponsPage2", netuser.userID.ToString()));
        }



        void BuyControllerQuantity(NetUser netuser, string item, int quantity)
        {
            Player player = playerInformation(netuser.userID.ToString());
            double availabledrabloons = player.drabloons;
            if (item == "556ammo" || item == "1")
            {
                if (availabledrabloons >= Ammo556 * quantity)
                {
                    player.drabloons = availabledrabloons - (Ammo556 * quantity);
                    GiveItem(netuser, "556 Ammo", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "556 Ammo" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Ammo556 * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "556 Ammo");
                }
            }
            else if (item == "9mmammo" || item == "2")
            {
                if (availabledrabloons >= Ammo9mm * quantity)
                {
                    player.drabloons = availabledrabloons - (Ammo9mm * quantity);
                    GiveItem(netuser, "9mm Ammo", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "9mm Ammo" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Ammo9mm * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "9mm Ammo");
                }
            }
            else if (item == "Arrow" || item == "3")
            {
                if (availabledrabloons >= Ammoarrow * quantity)
                {
                    player.drabloons = availabledrabloons - (Ammoarrow * quantity);
                    GiveItem(netuser, "Arrow", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Arrow" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Ammoarrow * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Arrow");
                }
            }
            else if (item == "handmadeshell" || item == "4")
            {
                if (availabledrabloons >= Ammohandmadeshell * quantity)
                {
                    player.drabloons = availabledrabloons - (Ammohandmadeshell * quantity);
                    GiveItem(netuser, "Handmade Shell", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Handmade Shell" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Ammohandmadeshell * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Handmade Shell");
                }
            }
            else if (item == "shotgunshell" || item == "5")
            {
                if (availabledrabloons >= Ammoshotgunshell * quantity)
                {
                    player.drabloons = availabledrabloons - (Ammoshotgunshell * quantity);
                    GiveItem(netuser, "Shotgun Shell", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Shotgun Shell" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Ammoshotgunshell * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Shotgun Shell");
                }
            }
            else if (item == "clothhelmet" || item == "6")
            {
                if (availabledrabloons >= Armorclothhelmet * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorclothhelmet * quantity);
                    GiveItem(netuser, "Cloth Helmet", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Cloth Helmet" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorclothhelmet * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Cloth Helmet");
                }
            }
            else if (item == "clothvest" || item == "7")
            {
                if (availabledrabloons >= Armorclothvest * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorclothvest * quantity);
                    GiveItem(netuser, "Cloth Vest", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Cloth Vest" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorclothvest * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Cloth Vest");
                }
            }
            else if (item == "clothpants" || item == "8")
            {
                if (availabledrabloons >= Armorclothpants * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorclothpants * quantity);
                    GiveItem(netuser, "Cloth Pants", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Cloth Pants" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorclothpants * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Cloth Pants");
                }
            }
            else if (item == "clothboots" || item == "9")
            {
                if (availabledrabloons >= Armorclothboots * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorclothboots * quantity);
                    GiveItem(netuser, "Cloth Boots", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Cloth Boots" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorclothboots * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Cloth Boots");
                }
            }
            else if (item == "leatherhelmet" || item == "10")
            {
                if (availabledrabloons >= Armorleatherhelmet * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorleatherhelmet * quantity);
                    GiveItem(netuser, "Leather Helmet", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Leather Helmet" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorleatherhelmet * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Leather Helmet");
                }
            }
            else if (item == "leathervest" || item == "11")
            {
                if (availabledrabloons >= Armorleathervest * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorleathervest * quantity);
                    GiveItem(netuser, "Leather Vest", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + quantity + GetMessage("ItemBought2", netuser.userID.ToString()) + "Leather Vest" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorleathervest * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Leather Vest");
                }
            }
            else if (item == "leatherpants" || item == "12")
            {
                if (availabledrabloons >= Armorleatherpants * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorleatherpants * quantity);
                    GiveItem(netuser, "Leather Pants", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Leather Pants" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorleatherpants * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Leather Pants");
                }
            }
            else if (item == "leatherboots" || item == "13")
            {
                if (availabledrabloons >= Armorleatherboots * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorleatherboots * quantity);
                    GiveItem(netuser, "Leather Boots", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Leather Boots" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorleatherboots * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Leather Boots");
                }
            }
            else if (item == "radsuithelmet" || item == "14")
            {
                if (availabledrabloons >= Armorradsuithelmet * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorradsuithelmet * quantity);
                    GiveItem(netuser, "Rad Suit Helmet", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Rad Suit Helmet" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorradsuithelmet * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Rad Suit Helmet");
                }
            }
            else if (item == "radsuitvest" || item == "15")
            {
                if (availabledrabloons >= Armorradsuitvest * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorradsuitvest * quantity);
                    GiveItem(netuser, "Rad Suit Vest", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Rad Suit Vest" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorradsuitvest * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Rad Suit Vest");
                }
            }
            else if (item == "radsuitpants" || item == "16")
            {
                if (availabledrabloons >= Armorradsuitpants * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorradsuitpants * quantity);
                    GiveItem(netuser, "Rad Suit Pants", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Rad Suit Pants" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorradsuitpants * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Rad Suit Pants");
                }
            }
            else if (item == "radsuitboots" || item == "17")
            {
                if (availabledrabloons >= Armorradsuitboots * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorradsuitboots * quantity);
                    GiveItem(netuser, "Rad Suit Boots", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Rad Suit Boots" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorradsuitboots * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Rad Suit Boots");
                }
            }
            else if (item == "kevlarhelmet" || item == "18")
            {
                if (availabledrabloons >= Armorkevlarhelmet * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorkevlarhelmet * quantity);
                    GiveItem(netuser, "Kevlar Helmet", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Kevlar Helmet" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorkevlarhelmet * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Kevlar Helmet");
                }
            }
            else if (item == "kevlarvest" || item == "19")
            {
                if (availabledrabloons >= Armorkevlarvest * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorkevlarvest * quantity);
                    GiveItem(netuser, "Kevlar Vest", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Kevlar Vest" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorkevlarvest * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Kevlar Vest");
                }
            }
            else if (item == "kevlarpants" || item == "20")
            {
                if (availabledrabloons >= Armorkevlarpants * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorkevlarpants * quantity);
                    GiveItem(netuser, "Kevlar Pants", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Kevlar Pants" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorkevlarpants * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Kevlar Pants");
                }
            }
            else if (item == "kevlarboots" || item == "21")
            {
                if (availabledrabloons >= Armorkevlarboots * quantity)
                {
                    player.drabloons = availabledrabloons - (Armorkevlarboots * quantity);
                    GiveItem(netuser, "Kevlar Boots", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Kevlar Boots" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Armorkevlarboots * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Kevlar Boots");
                }
            }
            else if (item == "canofbeans" || item == "22")
            {
                if (availabledrabloons >= Foodcanofbeans * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodcanofbeans * quantity);
                    GiveItem(netuser, "Can of Beans", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Can of Beans" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodcanofbeans * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Can of Beans");
                }
            }
            else if (item == "canoftuna" || item == "23")
            {
                if (availabledrabloons >= Foodcanoftuna * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodcanoftuna * quantity);
                    GiveItem(netuser, "Can of Tuna", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Can of Tuna" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodcanoftuna * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Can of Tuna");
                }
            }
            else if (item == "chocolatebar" || item == "24")
            {
                if (availabledrabloons >= Foodchocolatebar * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodchocolatebar * quantity);
                    GiveItem(netuser, "Chocolate Bar", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Chocolate Bar" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodchocolatebar * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Chocolate Bar");
                }
            }
            else if (item == "cookedchickenbreast" || item == "25")
            {
                if (availabledrabloons >= Foodcookedchickenbreast * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodcookedchickenbreast * quantity);
                    GiveItem(netuser, "Cooked Chicken Breast", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Cooked Chicken Breast" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodcookedchickenbreast * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Cooked Chicken Breast");
                }
            }
            else if (item == "granolabar" || item == "26")
            {
                if (availabledrabloons >= Foodgranolabar * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodgranolabar * quantity);
                    GiveItem(netuser, "Granola Bar", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Granola Bar" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodgranolabar * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Granola Bar");
                }
            }
            else if (item == "rawchickenbreast" || item == "27")
            {
                if (availabledrabloons >= Foodrawchickenbreast * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodrawchickenbreast * quantity);
                    GiveItem(netuser, "Raw Chicken Breast", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Raw Chicken Breast" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodrawchickenbreast * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Raw Chicken Breast");
                }
            }
            else if (item == "smallrations" || item == "28")
            {
                if (availabledrabloons >= Foodsmallrations * quantity)
                {
                    player.drabloons = availabledrabloons - (Foodsmallrations * quantity);
                    GiveItem(netuser, "Small Rations", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Small Rations" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Foodsmallrations * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Small Rations");
                }
            }
            else if (item == "antiradpills" || item == "29")
            {
                if (availabledrabloons >= Medicalantiradpills * quantity)
                {
                    player.drabloons = availabledrabloons - (Medicalantiradpills * quantity);
                    GiveItem(netuser, "Anti-Radiation Pills", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Anti-Radiation Pills" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Medicalantiradpills * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Anti-Radiation Pills");
                }
            }
            else if (item == "bandage" || item == "30")
            {
                if (availabledrabloons >= Medicalbandage * quantity)
                {
                    player.drabloons = availabledrabloons - (Medicalbandage * quantity);
                    GiveItem(netuser, "Bandage", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Bandage" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Medicalbandage * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Bandage");
                }
            }
            else if (item == "largemedkit" || item == "31")
            {
                if (availabledrabloons >= Medicallargemedkit * quantity)
                {
                    player.drabloons = availabledrabloons - (Medicallargemedkit * quantity);
                    GiveItem(netuser, "Large Medkit", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Large Medkit" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Medicallargemedkit * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Large Medkit");
                }
            }
            else if (item == "smallmedkit" || item == "32")
            {
                if (availabledrabloons >= Medicalsmallmedkit)
                {
                    player.drabloons = availabledrabloons - (Medicalsmallmedkit * quantity);
                    GiveItem(netuser, "Small Medkit", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Small Medkit" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Medicalsmallmedkit * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Small Medkit");
                }
            }
            else if (item == "flashlightmod" || item == "33")
            {
                if (availabledrabloons >= Modsflashlightmod * quantity)
                {
                    player.drabloons = availabledrabloons - (Modsflashlightmod * quantity);
                    GiveItem(netuser, "Flashlight Mod", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Flashlight Mod" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Modsflashlightmod * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Flashlight Mod");
                }
            }
            else if (item == "holosight" || item == "34")
            {
                if (availabledrabloons >= Modsholosight * quantity)
                {
                    player.drabloons = availabledrabloons - (Modsholosight * quantity);
                    GiveItem(netuser, "Holo Sight", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Holo Sight" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Modsholosight * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Holo Sight");
                }
            }
            else if (item == "lasersight" || item == "35")
            {
                if (availabledrabloons >= Modslasersight * quantity)
                {
                    player.drabloons = availabledrabloons - (Modslasersight * quantity);
                    GiveItem(netuser, "Laser Sight", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Laser Sight" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Modslasersight) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Laser Sight");
                }
            }
            else if (item == "silencer" || item == "36")
            {
                if (availabledrabloons >= Modssilencer * quantity)
                {
                    player.drabloons = availabledrabloons - (Modssilencer * quantity);
                    GiveItem(netuser, "Silencer", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Silencer" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Modssilencer) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Silencer");
                }
            }
            else if (item == "metalceiling" || item == "37")
            {
                if (availabledrabloons >= Partsmetalceiling * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalceiling * quantity);
                    GiveItem(netuser, "Metal Ceiling", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Ceiling" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalceiling * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Ceiling");
                }
            }
            else if (item == "metaldoorway" || item == "38")
            {
                if (availabledrabloons >= Partsmetaldoorway * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetaldoorway * quantity);
                    GiveItem(netuser, "Metal Doorway", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Doorway" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetaldoorway * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Doorway");
                }
            }
            else if (item == "metalfoundation" || item == "39")
            {
                if (availabledrabloons >= Partsmetalfoundation * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalfoundation * quantity);
                    GiveItem(netuser, "Metal Foundation", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Foundation" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalfoundation * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Foundation");
                }
            }
            else if (item == "metalpillar" || item == "40")
            {
                if (availabledrabloons >= Partsmetalpillar * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalpillar * quantity);
                    GiveItem(netuser, "Metal Pillar", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Pillar" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalpillar * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Pillar");
                }
            }
            else if (item == "metalramp" || item == "41")
            {
                if (availabledrabloons >= Partsmetalramp * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalramp * quantity);
                    GiveItem(netuser, "Metal Ramp", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Ramp" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalramp * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Ramp");
                }
            }
            else if (item == "metalstairs" || item == "42")
            {
                if (availabledrabloons >= Partsmetalstairs * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalstairs * quantity);
                    GiveItem(netuser, "Metal Stairs", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Stairs" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalstairs * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Stairs");
                }
            }
            else if (item == "metalwall" || item == "43")
            {
                if (availabledrabloons >= Partsmetalwall * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalwall * quantity);
                    GiveItem(netuser, "Metal Wall", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Wall" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalwall * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Wall");
                }
            }
            else if (item == "metalwindow" || item == "44")
            {
                if (availabledrabloons >= Partsmetalwindow * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalwindow * quantity);
                    GiveItem(netuser, "Metal Window", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Window" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalwindow * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Window");
                }
            }
            else if (item == "metalwindowbars" || item == "45")
            {
                if (availabledrabloons >= Partsmetalwindowbars * quantity)
                {
                    player.drabloons = availabledrabloons - (Partsmetalwindowbars * quantity);
                    GiveItem(netuser, "Metal Window Bars", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Window Bars" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partsmetalwindowbars * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Window Bars");
                }
            }
            else if (item == "woodceiling" || item == "46")
            {
                if (availabledrabloons >= Partswoodceiling * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodceiling * quantity);
                    GiveItem(netuser, "Wood Ceiling", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Ceiling" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodceiling * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Ceiling");
                }
            }
            else if (item == "wooddoorway" || item == "47")
            {
                if (availabledrabloons >= Partswooddoorway * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswooddoorway * quantity);
                    GiveItem(netuser, "Wood Doorway", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Doorway" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswooddoorway * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Doorway");
                }
            }
            else if (item == "woodfoundation" || item == "48")
            {
                if (availabledrabloons >= Partswoodfoundation * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodfoundation * quantity);
                    GiveItem(netuser, "Wood Foundation", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Foundation" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodfoundation * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Foundation");
                }
            }
            else if (item == "woodpillar" || item == "49")
            {
                if (availabledrabloons >= Partswoodpillar * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodpillar * quantity);
                    GiveItem(netuser, "Wood Pillar", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Pillar" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodpillar * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Pillar");
                }
            }
            else if (item == "woodramp" || item == "50")
            {
                if (availabledrabloons >= Partswoodramp * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodramp * quantity);
                    GiveItem(netuser, "Wood Ramp", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Ramp" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodramp * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Ramp");
                }
            }
            else if (item == "woodstairs" || item == "51")
            {
                if (availabledrabloons >= Partswoodstairs * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodstairs * quantity);
                    GiveItem(netuser, "Wood Stairs", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Stairs" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodstairs * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Stairs");
                }
            }
            else if (item == "woodwall" || item == "52")
            {
                if (availabledrabloons >= Partswoodwall * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodwall * quantity);
                    GiveItem(netuser, "Wood Wall", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Wall" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodwall * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Wall");
                }
            }
            else if (item == "woodwindow" || item == "53")
            {
                if (availabledrabloons >= Partswoodwindow * quantity)
                {
                    player.drabloons = availabledrabloons - (Partswoodwindow * quantity);
                    GiveItem(netuser, "Wood Window", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Window" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Partswoodwindow * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Window");
                }
            }
            else if (item == "animalfat" || item == "54")
            {
                if (availabledrabloons >= Resourcesanimalfat * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesanimalfat * quantity);
                    GiveItem(netuser, "Animal Fat", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Animal Fat" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesanimalfat * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Animal Fat");
                }
            }
            else if (item == "blood" || item == "55")
            {
                if (availabledrabloons >= Resourcesblood * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesblood * quantity);
                    GiveItem(netuser, "Blood", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Blood" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesblood * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Blood");
                }
            }
            else if (item == "cloth" || item == "56")
            {
                if (availabledrabloons >= Resourcescloth * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcescloth * quantity);
                    GiveItem(netuser, "Cloth", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Cloth" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcescloth * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Cloth");
                }
            }
            else if (item == "explosives" || item == "57")
            {
                if (availabledrabloons >= Resourcesexplosives * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesexplosives * quantity);
                    GiveItem(netuser, "Explosives", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Explosives" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesexplosives * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Explosives");
                }
            }
            else if (item == "gunpowder" || item == "58")
            {
                if (availabledrabloons >= Resourcesgunpowder * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesgunpowder * quantity);
                    GiveItem(netuser, "Gunpowder", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Gunpowder" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesgunpowder * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Gunpowder");
                }
            }
            else if (item == "leather" || item == "59")
            {
                if (availabledrabloons >= Resourcesleather * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesleather * quantity);
                    GiveItem(netuser, "Leather", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Leather" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesleather * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Leather");
                }
            }
            else if (item == "metalfragments" || item == "60")
            {
                if (availabledrabloons >= Resourcesmetalfragments * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesmetalfragments * quantity);
                    GiveItem(netuser, "Metal Fragments", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Fragments" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesmetalfragments * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Fragments");
                }
            }
            else if (item == "metalore" || item == "61")
            {
                if (availabledrabloons >= Resourcesmetalore * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesmetalore * quantity);
                    GiveItem(netuser, "Metal Ore", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Ore" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesmetalore * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Ore");
                }
            }
            else if (item == "paper" || item == "62")
            {
                if (availabledrabloons >= Resourcespaper * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcespaper * quantity);
                    GiveItem(netuser, "Paper", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Paper" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcespaper * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Paper");
                }
            }
            else if (item == "stones" || item == "63")
            {
                if (availabledrabloons >= Resourcesstones * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcesstones * quantity);
                    GiveItem(netuser, "Stones", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Stones" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcesstones * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Stones");
                }
            }
            else if (item == "sulfur" || item == "64")
            {
                if (availabledrabloons >= Resourcessulfur * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcessulfur * quantity);
                    GiveItem(netuser, "Sulfur", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Sulfur" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcessulfur * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Sulfur");
                }
            }
            else if (item == "sulfurore" || item == "65")
            {
                if (availabledrabloons >= Resourcessulfurore * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourcessulfurore * quantity);
                    GiveItem(netuser, "Sulfur Ore", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Sulfur Ore" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourcessulfurore * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Sulfur Ore");
                }
            }
            else if (item == "wood" || item == "66")
            {
                if (availabledrabloons >= Resourceswood * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourceswood * quantity);
                    GiveItem(netuser, "Wood", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourceswood * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood");
                }
            }
            else if (item == "woodplanks" || item == "67")
            {
                if (availabledrabloons >= Resourceswoodplanks * quantity)
                {
                    player.drabloons = availabledrabloons - (Resourceswoodplanks * quantity);
                    GiveItem(netuser, "Wood Planks", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Planks" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Resourceswoodplanks * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Planks");
                }
            }
            else if (item == "bed" || item == "68")
            {
                if (availabledrabloons >= Survivalbed * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalbed * quantity);
                    GiveItem(netuser, "Bed", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Bed" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalbed * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Bed");
                }
            }
            else if (item == "campfire" || item == "69")
            {
                if (availabledrabloons >= Survivalcampfire * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalcampfire * quantity);
                    GiveItem(netuser, "Camp Fire", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Camp Fire" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalcampfire * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Camp Fire");
                }
            }
            else if (item == "furnace" || item == "70")
            {
                if (availabledrabloons >= Survivalfurnace * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalfurnace * quantity);
                    GiveItem(netuser, "Furnace", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Furnace" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalfurnace * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Furnace");
                }
            }
            else if (item == "largespikewall" || item == "71")
            {
                if (availabledrabloons >= Survivallargespikewall * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivallargespikewall * quantity);
                    GiveItem(netuser, "Large Spike Wall", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Large Spike Wall" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivallargespikewall * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Large Spike Wall");
                }
            }
            else if (item == "largewoodstorage" || item == "72")
            {
                if (availabledrabloons >= Survivallargewoodstorage * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivallargewoodstorage * quantity);
                    GiveItem(netuser, "Large Wood Storage", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Large Wood Storage" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivallargewoodstorage * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Large Wood Storage");
                }
            }
            else if (item == "lowgradefuel" || item == "73")
            {
                if (availabledrabloons >= Survivallowgradefuel * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivallowgradefuel * quantity);
                    GiveItem(netuser, "Low Grade Fuel", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Low Grade Fuel" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivallowgradefuel * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Low Grade Fuel");
                }
            }
            else if (item == "lowqualitymetal" || item == "74")
            {
                if (availabledrabloons >= Survivallowqualitymetal * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivallowqualitymetal * quantity);
                    GiveItem(netuser, "Low Quality Metal", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Low Quality Metal" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivallowqualitymetal * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Low Quality Metal");
                }
            }
            else if (item == "metaldoor" || item == "75")
            {
                if (availabledrabloons >= Survivalmetaldoor * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalmetaldoor * quantity);
                    GiveItem(netuser, "Metal Door", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Metal Door" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalmetaldoor * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Metal Door");
                }
            }
            else if (item == "repairbench" || item == "76")
            {
                if (availabledrabloons >= Survivalrepairbench * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalrepairbench * quantity);
                    GiveItem(netuser, "Repair Bench", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Repair Bench" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalrepairbench * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Repair Bench");
                }
            }
            else if (item == "sleepingbag" || item == "77")
            {
                if (availabledrabloons >= Survivalsleepingbag * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalsleepingbag * quantity);
                    GiveItem(netuser, "Sleeping Bag", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Sleeping Bag" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalsleepingbag * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Sleeping Bag");
                }
            }
            else if (item == "smallstash" || item == "78")
            {
                if (availabledrabloons >= Survivalsmallstash * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalsmallstash * quantity);
                    GiveItem(netuser, "Small Stash", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Small Stash" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalsmallstash * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Small Stash");
                }
            }
            else if (item == "spikewall" || item == "79")
            {
                if (availabledrabloons >= Survivalspikewall * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalspikewall * quantity);
                    GiveItem(netuser, "Spike Wall", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Spike Wall" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalspikewall * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Spike Wall");
                }
            }
            else if (item == "torch" || item == "80")
            {
                if (availabledrabloons >= Survivaltorch * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivaltorch * quantity);
                    GiveItem(netuser, "Torch", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Torch" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivaltorch * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Torch");
                }
            }
            else if (item == "woodbarricade" || item == "81")
            {
                if (availabledrabloons >= Survivalwoodbarricade * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalwoodbarricade * quantity);
                    GiveItem(netuser, "Wood Barricade", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Barricade" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalwoodbarricade * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Barricade");
                }
            }
            else if (item == "woodgate" || item == "82")
            {
                if (availabledrabloons >= Survivalwoodgate * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalwoodgate * quantity);
                    GiveItem(netuser, "Wood Gate", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Gate" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalwoodgate * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Gate");
                }
            }
            else if (item == "woodgateway" || item == "83")
            {
                if (availabledrabloons >= Survivalwoodgateway * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalwoodgateway * quantity);
                    GiveItem(netuser, "Wood Gateway", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Gateway" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalwoodgateway * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Gateway");
                }
            }
            else if (item == "woodshelter" || item == "84")
            {
                if (availabledrabloons >= Survivalwoodshelter * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalwoodshelter * quantity);
                    GiveItem(netuser, "Wood Shelter", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Shelter" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalwoodshelter * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Shelter");
                }
            }
                        else if (item == "woodstoragebox" || item == "85")
            {
                if (availabledrabloons >= Survivalwoodstoragebox * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalwoodstoragebox * quantity);
                    GiveItem(netuser, "Wood Storage Box", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wood Storage Box" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalwoodstoragebox * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wood Storage Box");
                }
            }
            else if (item == "woodendoor" || item == "86")
            {
                if (availabledrabloons >= Survivalwoodendoor * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalwoodendoor * quantity);
                    GiveItem(netuser, "Wooden Door", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Wooden Door" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalwoodendoor * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Wooden Door");
                }
            }
            else if (item == "workbench" || item == "87")
            {
                if (availabledrabloons >= Survivalworkbench * quantity)
                {
                    player.drabloons = availabledrabloons - (Survivalworkbench * quantity);
                    GiveItem(netuser, "Workbench", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Workbench" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Survivalworkbench * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Woodbench");
                }
            }
            else if (item == "blooddrawkit" || item == "88")
            {
                if (availabledrabloons >= Toolsblooddrawkit * quantity)
                {
                    player.drabloons = availabledrabloons - (Toolsblooddrawkit * quantity);
                    GiveItem(netuser, "Blood Draw Kit", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Blood Draw Kit" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Toolsblooddrawkit * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Blood Draw Kit");
                }
            }
            else if (item == "homemadelockpick" || item == "89")
            {
                if (availabledrabloons >= Toolshandmadelockpick * quantity)
                {
                    player.drabloons = availabledrabloons - (Toolshandmadelockpick * quantity);
                    GiveItem(netuser, "Homemade Lockpick", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Homemade Lockpick" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Toolshandmadelockpick * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Homemade Lockpick");
                }
            }
            else if (item == "researchkit" || item == "90")
            {
                if (availabledrabloons >= Toolsresearchkit * quantity)
                {
                    player.drabloons = availabledrabloons - (Toolsresearchkit * quantity);
                    GiveItem(netuser, "Research Kit", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Research Kit" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Toolsresearchkit * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Research Kit");
                }
            }
            else if (item == "9mmpistol" || item == "91")
            {
                if (availabledrabloons >= Weapons9mmpistol * quantity)
                {
                    player.drabloons = availabledrabloons - (Weapons9mmpistol * quantity);
                    GiveItem(netuser, "9mm Pistol", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "9mm Pistol" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weapons9mmpistol * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "9mm Pistol");
                }
            }
            else if (item == "explosivecharge" || item == "92")
            {
                if (availabledrabloons >= Weaponsexplosivecharge * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsexplosivecharge * quantity);
                    GiveItem(netuser, "Explosive Charge", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Explosive Charge" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsexplosivecharge * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Explosive Charge");
                }
            }
            else if (item == "f1grenade" || item == "93")
            {
                if (availabledrabloons >= Weaponsf1grenade * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsf1grenade * quantity);
                    GiveItem(netuser, "F1 Grenade", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "F1 Grenade" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsf1grenade * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "F1 Grenade");
                }
            }
            else if (item == "handcannon" || item == "94")
            {
                if (availabledrabloons >= Weaponshandcannon * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponshandcannon * quantity);
                    GiveItem(netuser, "Hand Cannon", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Hand Cannon" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponshandcannon * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Hand Cannon");
                }
            }
            else if (item == "hatchet" || item == "95")
            {
                if (availabledrabloons >= Weaponshatchet * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponshatchet * quantity);
                    GiveItem(netuser, "Hatchet", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Hand Cannon" + GetMessage("ItemBought3", netuser.userID.ToString()) + Armorkevlarboots + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Hand Cannon");
                }
            }
            else if (item == "huntingbow" || item == "96")
            {
                if (availabledrabloons >= Weaponshuntingbow * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponshuntingbow * quantity);
                    GiveItem(netuser, "Hunting Bow", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Hunting Bow" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponshuntingbow * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Hunting Bow");
                }
            }            
            else if (item == "m4" || item == "97")
            {
                if (availabledrabloons >= Weaponsm4 * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsm4 * quantity);
                    GiveWeapon(netuser, "M4", 30, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "M4" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsm4 * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "M4");
                }
            }
            else if (item == "mp5a4" || item == "98")
            {
                if (availabledrabloons >= Weaponsmp5a4 * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsmp5a4 * quantity);
                    GiveWeapon(netuser, "MP5A4", 30, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "MP5A4" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsmp5a4 * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "MP5A4");
                }
            }
            else if (item == "p250" || item == "99")
            {
                if (availabledrabloons >= Weaponsp250 * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsp250 * quantity);
                    GiveWeapon(netuser, "P250", 8, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "P250" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsp250 * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "P250");
                }
            }
            else if (item == "pipeshotgun" || item == "100")
            {
                if (availabledrabloons >= Weaponspipeshotgun * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponspipeshotgun * quantity);
                    GiveWeapon(netuser, "Pipe Shotgun", 1, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Pipe Shotgun" + GetMessage("ItemBought3", netuser.userID.ToString()) + Weaponspipeshotgun * quantity + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Pipe Shotgun");
                }
            }
            else if (item == "revolver" || item == "101")
            {
                if (availabledrabloons >= Weaponsrevolver * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsrevolver * quantity);
                    GiveWeapon(netuser, "Revolver", 8, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Revolver" + GetMessage("ItemBought3", netuser.userID.ToString()) + Weaponsrevolver * quantity + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Revolver");
                }
            }
            else if (item == "rock" || item == "102")
            {
                if (availabledrabloons >= Weaponsrock * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsrock * quantity);
                    GiveWeapon(netuser, "Rock", 1, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Rock" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsrock * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Rock");
                }
            }
            else if (item == "shotgun" || item == "103")
            {
                if (availabledrabloons >= Weaponsshotgun * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsshotgun * quantity);
                    GiveWeapon(netuser, "Shotgun", 8, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Weaponsshotgun" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsshotgun * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Weaponsshotgun");
                }
            }
            else if (item == "stonehatchet" || item == "104")
            {
                if (availabledrabloons >= Weaponsstonehatchet * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsstonehatchet * quantity);
                    GiveItem(netuser, "Stone Hatchet", 1);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Stone Hatchet" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsstonehatchet * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Stone Hatchet");
                }
            }
            else if (item == "supplysignal" || item == "105")
            {
                if (availabledrabloons >= Weaponssupplysignal * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponssupplysignal * quantity);
                    GiveItem(netuser, "supplysignal", 1);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Supply Signal" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponssupplysignal * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Supply Signal");
                }
            }

            else if (item == "boltactionrifle" || item == "106")
            {
                if (availabledrabloons >= Weaponsboltactionrifle * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponsboltactionrifle * quantity);
                    GiveWeapon(netuser, "Bolt Action Rifle", 1, new[] { "" });
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Bolt Action Rifle" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponsboltactionrifle * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Bolt Action Rifle");
                }
            }

            else if (item == "pickaxe" || item == "107")
            {
                if (availabledrabloons >= Weaponspickaxe * quantity)
                {
                    player.drabloons = availabledrabloons - (Weaponspickaxe * quantity);
                    GiveItem(netuser, "Pickaxe", quantity);
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("ItemBought1", netuser.userID.ToString()) + "1" + GetMessage("ItemBought2", netuser.userID.ToString()) + "Pickaxe" + GetMessage("ItemBought3", netuser.userID.ToString()) + (Weaponspickaxe * quantity) + GetMessage("ItemBought4", netuser.userID.ToString()));
                }
                else
                {
                    rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Insufficientcurrency", netuser.userID.ToString()) + "Pickaxe");
                }
            }
        }
        void SellController(NetUser netuser, string items)
        {
            Player player = playerInformation(netuser.userID.ToString());
            double availabledrabloons = player.drabloons;
            Inventory inventory = netuser.playerClient.controllable.GetComponent<PlayerInventory>();
            InventoryItem item;
            if (items == "556ammo" || items == "1")
            {
                player.drabloons = availabledrabloons + (Ammo556 * 250) * 0.70;
                var p = inventory.FindItem("556 Ammo");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                    string mes = p.ToString();
                    if (mes.Contains("250"))
                    {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                    }
                    else
                    {
                        SendReply(netuser, "Requires 250 556 Ammo!");
                    }
                }
            }
            else if (items == "9mmammo" || items == "2")
            {
                player.drabloons = availabledrabloons + (Ammo9mm * 250) * 0.70;
                var p = inventory.FindItem("9mm Ammo");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                    string mes = p.ToString();
                    if (mes.Contains("250"))
                    {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                    }
                    else
                    {
                        SendReply(netuser, "Requires 250 9mm Ammo!");
                    }
                }
            }
            else if (items == "Arrow" || items == "3")
            {
                player.drabloons = availabledrabloons + (Ammoarrow * 10) * 0.70;
                var p = inventory.FindItem("Arrow");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                    string mes = p.ToString();
                    if (mes.Contains("10"))
                    {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                    }
                    else
                    {
                        SendReply(netuser, "Requires 10 Arrows!");
                    }
                }
            }
            else if (items == "handmadeshell" || items == "4")
            {
                player.drabloons = availabledrabloons + (Ammohandmadeshell * 250) * 0.70;
                var p = inventory.FindItem("Handmade Shell");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                    string mes = p.ToString();
                    if (mes.Contains("250"))
                    {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                    }
                    else
                    {
                        SendReply(netuser, "Requires 250 Handmade Shells!");
                    }
                }
            }
            else if (items == "shotgunshell" || items == "5")
            {
                player.drabloons = availabledrabloons + (Ammoshotgunshell * 250) * 0.70;
                var p = inventory.FindItem("Shotgun Shell");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                    string mes = p.ToString();
                    if (mes.Contains("250"))
                    {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                    }
                    else
                    {
                        SendReply(netuser, "Requires 250 Shotgun Shells!");
                    }
                }
            }
            else if (items == "clothhelmet" || items == "6")
            {
                player.drabloons = availabledrabloons + (Armorclothhelmet * 0.70);
                var p = inventory.FindItem("Cloth Helmet");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "clothvest" || items == "7")
            {
                player.drabloons = availabledrabloons + (Armorclothvest * 0.70);
                var p = inventory.FindItem("Cloth Vest");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "clothpants" || items == "8")
            {
                player.drabloons = availabledrabloons + (Armorclothpants * 0.70);
                var p = inventory.FindItem("Cloth Pants");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "clothboots" || items == "9")
            {
                player.drabloons = availabledrabloons + (Armorclothboots * 0.70);
                var p = inventory.FindItem("Cloth Boots");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "leatherhelmet" || items == "10")
            {
                player.drabloons = availabledrabloons + (Armorleatherhelmet * 0.70);
                var p = inventory.FindItem("Leather Helmet");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "leathervest" || items == "11")
            {
                player.drabloons = availabledrabloons + (Armorleathervest * 0.70);
                var p = inventory.FindItem("Leather Vest");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "leatherpants" || items == "12")
            {
                player.drabloons = availabledrabloons + (Armorleatherpants * 0.70);
                var p = inventory.FindItem("Leather Pants");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "leatherboots" || items == "13")
            {
                player.drabloons = availabledrabloons + (Armorleatherboots * 0.70);
                var p = inventory.FindItem("Leather Boots");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "radsuithelmet" || items == "14")
            {
                player.drabloons = availabledrabloons + (Armorradsuithelmet * 0.70);
                var p = inventory.FindItem("Rad Suit Helmet");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "radsuitvest" || items == "15")
            {
                player.drabloons = availabledrabloons + (Armorradsuitvest * 0.70);
                var p = inventory.FindItem("Rad Suit Vest");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "radsuitpants" || items == "16")
            {
                player.drabloons = availabledrabloons + (Armorradsuitpants * 0.70);
                var p = inventory.FindItem("Rad Suit Pants");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "radsuitboots" || items == "17")
            {
                player.drabloons = availabledrabloons + (Armorradsuitboots * 0.70);
                var p = inventory.FindItem("Rad Suit Boots");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "kevlarhelmet" || items == "18")
            {
                player.drabloons = availabledrabloons + (Armorkevlarhelmet * 0.70);
                var p = inventory.FindItem("Kevlar Helmet");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "kevlarvest" || items == "19")
            {
                player.drabloons = availabledrabloons + (Armorkevlarvest * 0.70);
                var p = inventory.FindItem("Kevlar Vest");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "kevlarpants" || items == "20")
            {
                player.drabloons = availabledrabloons + (Armorkevlarpants * 0.70);
                var p = inventory.FindItem("Kevlar Pants");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "kevlarboots" || items == "21")
            {
                player.drabloons = availabledrabloons + (Armorkevlarboots * 0.70);
                var p = inventory.FindItem("Kevlar Boots");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "canofbeans" || items == "22")
            {
                player.drabloons = availabledrabloons + (Foodcanofbeans * 0.70);
                var p = inventory.FindItem("Can of Beans");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "canoftuna" || items == "23")
            {
                player.drabloons = availabledrabloons + (Foodcanoftuna * 0.70);
                var p = inventory.FindItem("Can of Tuna");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "chocolatebar" || items == "24")
            {
                player.drabloons = availabledrabloons + (Foodchocolatebar * 0.70);
                var p = inventory.FindItem("Chocolate Bar");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "cookedchickenbreast" || items == "25")
            {
                player.drabloons = availabledrabloons + (Foodcookedchickenbreast * 0.70);
                var p = inventory.FindItem("Cooked Chicken Breast");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "granolabar" || items == "26")
            {
                player.drabloons = availabledrabloons + (Foodgranolabar * 0.70);
                var p = inventory.FindItem("Food Granola Bar");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "rawchickenbreast" || items == "27")
            {
                player.drabloons = availabledrabloons + (Foodrawchickenbreast * 0.70);
                var p = inventory.FindItem("Raw Chicken Breast");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "smallrations" || items == "28")
            {
                player.drabloons = availabledrabloons + (Foodsmallrations * 0.70);
                var p = inventory.FindItem("Small Rations");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "antiradpills" || items == "29")
            {
                player.drabloons = availabledrabloons + (Medicalantiradpills * 0.70);
                var p = inventory.FindItem("Anti-Radiation Pills");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "bandage" || items == "30")
            {
                player.drabloons = availabledrabloons + (Medicalbandage * 0.70);
                var p = inventory.FindItem("Bandage");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "largemedkit" || items == "31")
            {
                player.drabloons = availabledrabloons + (Medicallargemedkit * 0.70);
                var p = inventory.FindItem("Large Medkit");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "smallmedkit" || items == "32")
            {
                player.drabloons = availabledrabloons + (Medicalsmallmedkit * 0.70);
                var p = inventory.FindItem("Small Medkit");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "flashlightmod" || items == "33")
            {
                player.drabloons = availabledrabloons + (Modsflashlightmod * 0.70);
                var p = inventory.FindItem("Flashlight Mod");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "holosight" || items == "34")
            {
                player.drabloons = availabledrabloons + (Modsholosight * 0.70);
                var p = inventory.FindItem("Holo sight");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "lasersight" || items == "35")
            {
                player.drabloons = availabledrabloons + (Modslasersight * 0.70);
                var p = inventory.FindItem("Laser Sight");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "silencer" || items == "36")
            {
                player.drabloons = availabledrabloons + (Modssilencer * 0.70);
                var p = inventory.FindItem("Silencer");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalceiling" || items == "37")
            {
                player.drabloons = availabledrabloons + (Partsmetalceiling * 0.70);
                var p = inventory.FindItem("Metal Ceiling");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metaldoorway" || items == "38")
            {
                player.drabloons = availabledrabloons + (Partsmetaldoorway * 0.70);
                var p = inventory.FindItem("Metal Doorway");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalfoundation" || items == "39")
            {
                player.drabloons = availabledrabloons + (Partsmetalfoundation * 0.70);
                var p = inventory.FindItem("Metal Foundation");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalpillar" || items == "40")
            {
                player.drabloons = availabledrabloons + (Partsmetalpillar * 0.70);
                var p = inventory.FindItem("Metal Pillar");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalramp" || items == "41")
            {
                player.drabloons = availabledrabloons + (Partsmetalramp * 0.70);
                var p = inventory.FindItem("Metal Ramp");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalstairs" || items == "42")
            {
                player.drabloons = availabledrabloons + (Partsmetalstairs * 0.70);
                var p = inventory.FindItem("Metal Stairs");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalwall" || items == "43")
            {
                player.drabloons = availabledrabloons + (Partsmetalwall * 0.70);
                var p = inventory.FindItem("Metal Wall");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalwindow" || items == "44")
            {
                player.drabloons = availabledrabloons + (Partsmetalwindow * 0.70);
                var p = inventory.FindItem("Metal Window");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalwindowbars" || items == "45")
            {
                player.drabloons = availabledrabloons + (Partsmetalwindowbars * 0.70);
                var p = inventory.FindItem("Metal Window Bars");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodceiling" || items == "46")
            {
                player.drabloons = availabledrabloons + (Partswoodceiling * 0.70);
                var p = inventory.FindItem("Wood Ceiling");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "wooddoorway" || items == "47")
            {
                player.drabloons = availabledrabloons + (Partswooddoorway * 0.70);
                var p = inventory.FindItem("Wood Doorway");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodfoundation" || items == "48")
            {
                player.drabloons = availabledrabloons + (Partswoodfoundation * 0.70);
                var p = inventory.FindItem("Wood Foundation");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodpillar" || items == "49")
            {
                player.drabloons = availabledrabloons + (Partswoodpillar * 0.70);
                var p = inventory.FindItem("Wood Pillar");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodramp" || items == "50")
            {
                player.drabloons = availabledrabloons + (Partswoodramp * 0.70);
                var p = inventory.FindItem("Wood Ramp");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodstairs" || items == "51")
            {
                player.drabloons = availabledrabloons + (Partswoodstairs * 0.70);
                var p = inventory.FindItem("Wood Stairs");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodwall" || items == "52")
            {
                player.drabloons = availabledrabloons + (Partswoodwall * 0.70);
                var p = inventory.FindItem("Wood Wall");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodwindow" || items == "53")
            {
                player.drabloons = availabledrabloons + (Partswoodwindow * 0.70);
                var p = inventory.FindItem("Wood Window");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "animalfat" || items == "54")
            {
                player.drabloons = availabledrabloons + (Resourcesanimalfat * 0.70);
                var p = inventory.FindItem("Animal Fat");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "blood" || items == "55")
            {
                player.drabloons = availabledrabloons + (Resourcesblood * 0.70);
                var p = inventory.FindItem("Blood");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "cloth" || items == "56")
            {
                player.drabloons = availabledrabloons + (Resourcescloth * 0.70);
                var p = inventory.FindItem("Cloth");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "explosives" || items == "57")
            {
                player.drabloons = availabledrabloons + (Resourcesexplosives * 0.70);
                var p = inventory.FindItem("Explosives");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "gunpowder" || items == "58")
            {
                player.drabloons = availabledrabloons + (Resourcesgunpowder * 0.70);
                var p = inventory.FindItem("Gunpowder");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "leather" || items == "59")
            {
                player.drabloons = availabledrabloons + (Resourcesleather * 0.70);
                var p = inventory.FindItem("Leather");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalfragments" || items == "60")
            {
                player.drabloons = availabledrabloons + (Resourcesmetalfragments * 0.70);
                var p = inventory.FindItem("Metal Fragments");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metalore" || items == "61")
            {
                player.drabloons = availabledrabloons + (Resourcesmetalore * 0.70);
                var p = inventory.FindItem("Metal Ore");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "paper" || items == "62")
            {
                player.drabloons = availabledrabloons + (Resourcespaper * 0.70);
                var p = inventory.FindItem("Paper");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "stones" || items == "63")
            {
                player.drabloons = availabledrabloons + (Resourcesstones * 0.70);
                var p = inventory.FindItem("Stones");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "sulfur" || items == "64")
            {
                player.drabloons = availabledrabloons + (Resourcessulfur * 0.70);
                var p = inventory.FindItem("Sulfur");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "sulfurore" || items == "65")
            {
                player.drabloons = availabledrabloons + (Resourcessulfurore * 0.70);
                var p = inventory.FindItem("Sulfur Ore");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "wood" || items == "66")
            {
                player.drabloons = availabledrabloons + (Resourceswood * 0.70);
                var p = inventory.FindItem("Wood");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodplanks" || items == "67")
            {
                player.drabloons = availabledrabloons + (Resourceswoodplanks * 0.70);
                var p = inventory.FindItem("Wood Planks");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "bed" || items == "68")
            {
                player.drabloons = availabledrabloons + (Survivalbed * 0.70);
                var p = inventory.FindItem("Bed");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "campfire" || items == "69")
            {
                player.drabloons = availabledrabloons + (Survivalcampfire * 0.70);
                var p = inventory.FindItem("Camp Fire");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "furnace" || items == "70")
            {
                player.drabloons = availabledrabloons + (Survivalfurnace * 0.70);
                var p = inventory.FindItem("Furnace");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "largespikewall" || items == "71")
            {
                player.drabloons = availabledrabloons + (Survivallargespikewall * 0.70);
                var p = inventory.FindItem("Laser Sight");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "largewoodstorage" || items == "72")
            {
                player.drabloons = availabledrabloons + (Survivallargewoodstorage * 0.70);
                var p = inventory.FindItem("Large Wood Storage");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "lowgradefuel" || items == "73")
            {
                player.drabloons = availabledrabloons + (Survivallowgradefuel * 0.70);
                var p = inventory.FindItem("Low Grade Fuel");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "lowqualitymetal" || items == "74")
            {
                player.drabloons = availabledrabloons + (Survivallowqualitymetal * 0.70);
                var p = inventory.FindItem("Low Quality Metal");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "metaldoor" || items == "75")
            {
                player.drabloons = availabledrabloons + (Survivalmetaldoor * 0.70);
                var p = inventory.FindItem("Metal Door");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "repairbench" || items == "76")
            {
                player.drabloons = availabledrabloons + (Survivalrepairbench * 0.70);
                var p = inventory.FindItem("Repair Bench");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "sleepingbag" || items == "77")
            {
                player.drabloons = availabledrabloons + (Survivalsleepingbag * 0.70);
                var p = inventory.FindItem("Sleeping Bag");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "smallstash" || items == "78")
            {
                player.drabloons = availabledrabloons + (Survivalsmallstash * 0.70);
                var p = inventory.FindItem("Small Stash");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "spikewall" || items == "79")
            {
                player.drabloons = availabledrabloons + (Survivalspikewall * 0.70);
                var p = inventory.FindItem("Spikewall");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "torch" || items == "80")
            {
                player.drabloons = availabledrabloons + (Survivaltorch * 0.70);
                var p = inventory.FindItem("Torch");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodbarricade" || items == "81")
            {
                player.drabloons = availabledrabloons + (Survivalwoodbarricade * 0.70);
                var p = inventory.FindItem("Wood Barricade");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodgate" || items == "82")
            {
                player.drabloons = availabledrabloons + (Survivalwoodgate * 0.70);
                var p = inventory.FindItem("Wood Gate");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodgateway" || items == "83")
            {
                player.drabloons = availabledrabloons + (Survivalwoodgateway * 0.70);
                var p = inventory.FindItem("Wood Gateway");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodshelter" || items == "84")
            {
                player.drabloons = availabledrabloons + (Survivalwoodshelter * 0.70);
                var p = inventory.FindItem("Wood Shelter");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
             else if (items == "woodstoragebox" || items == "85")
            {
                player.drabloons = availabledrabloons + (Survivalwoodstoragebox * 0.70);
                var p = inventory.FindItem("Wood Storage Box");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "woodendoor" || items == "86")
            {
                player.drabloons = availabledrabloons + (Survivalwoodendoor * 0.70);
                var p = inventory.FindItem("Wooden Door");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "workbench" || items == "87")
            {
                player.drabloons = availabledrabloons + (Survivalworkbench * 0.70);
                var p = inventory.FindItem("Workbench");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "blooddrawkit" || items == "88")
            {
                player.drabloons = availabledrabloons + (Toolsblooddrawkit * 0.70);
                var p = inventory.FindItem("Blood Draw Kit");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "homemadelockpick" || items == "89")
            {
                player.drabloons = availabledrabloons + (Toolshandmadelockpick * 0.70);
                var p = inventory.FindItem("Handmade Lockpick");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "researchkit" || items == "90")
            {
                player.drabloons = availabledrabloons + (Toolsresearchkit * 0.70);
                var p = inventory.FindItem("Research Kit");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "9mmpistol" || items == "91")
            {
                player.drabloons = availabledrabloons + (Weapons9mmpistol * 0.70);
                var p = inventory.FindItem("9mm Pistol");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "explosivecharge" || items == "92")
            {
                player.drabloons = availabledrabloons + (Weaponsexplosivecharge * 0.70);
                var p = inventory.FindItem("Explosive Charge");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "f1grenade" || items == "93")
            {
                player.drabloons = availabledrabloons + (Weaponsf1grenade * 0.70);
                var p = inventory.FindItem("F1 Grenade");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "handcannon" || items == "94")
            {
                player.drabloons = availabledrabloons + (Weaponshandcannon * 0.70);
                var p = inventory.FindItem("Hand Cannon");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "hatchet" || items == "95")
            {
                player.drabloons = availabledrabloons + (Weaponshatchet * 0.70);
                var p = inventory.FindItem("Hatchet");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "huntingbow" || items == "96")
            {
                player.drabloons = availabledrabloons + (Weaponshuntingbow * 0.70);
                var p = inventory.FindItem("Hunting Bow");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }            
            else if (items == "m4" || items == "97")
            {
                player.drabloons = availabledrabloons + (Weaponsm4 * 0.70);
                var p = inventory.FindItem("M4");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "mp5a4" || items == "98")
            {
                player.drabloons = availabledrabloons + (Weaponsmp5a4 * 0.70);
                var p = inventory.FindItem("MP5A4");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "p250" || items == "99")
            {
                player.drabloons = availabledrabloons + (Weaponsp250 * 0.70);
                var p = inventory.FindItem("P250");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "pipeshotgun" || items == "100")
            {
                player.drabloons = availabledrabloons + (Weaponspipeshotgun * 0.70);
                var p = inventory.FindItem("Pipe Shotgun");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "revolver" || items == "101")
            {
                player.drabloons = availabledrabloons + (Weaponsrevolver * 0.70);
                var p = inventory.FindItem("Revolver");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "rock" || items == "102")
            {
                player.drabloons = availabledrabloons + (Weaponsrock * 0.70);
                var p = inventory.FindItem("Rock");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "shotgun" || items == "103")
            {
                player.drabloons = availabledrabloons + (Weaponsshotgun * 0.70);
                var p = inventory.FindItem("Shotgun");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "stonehatchet" || items == "104")
            {
                player.drabloons = availabledrabloons + (Weaponsstonehatchet * 0.70);
                var p = inventory.FindItem("Stone Hatchet");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "supplysignal" || items == "105")
            {
                player.drabloons = availabledrabloons + (Weaponssupplysignal * 0.70);
                var p = inventory.FindItem("Supply Signal");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "boltactionrifle" || items == "106")
            {
                player.drabloons = availabledrabloons + (Weaponsboltactionrifle * 0.70);
                var p = inventory.FindItem("Bolt-Action Rifle");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
            else if (items == "pickaxe" || items == "107")
            {
                player.drabloons = availabledrabloons + (Weaponspickaxe * 0.70);
                var p = inventory.FindItem("Pickaxe");
                if (p == null)
                {
                    SendReply(netuser, "No item");
                    return;
                }
                else
                {
                        int savedweaponnumber = p.slot;
                        inventory.RemoveItem(savedweaponnumber);
                }
            }
        }
        void TradeController(NetUser netuser)
        {

        }
        void CurrencyControl(NetUser netuser)
        {
            Player player = playerInformation(netuser.userID.ToString());
            double drabloons = player.drabloons;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyTitle", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyintro", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencymaking1", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencymaking2", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }

        void CurrencyAdminControl1(NetUser netuser)
        {
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitle", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Syntaxca", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Option1ca", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Option2ca", netuser.userID.ToString()));     
        }
        void CurrencyAdminControl2(NetUser netuser)
        {
            Player player = playerInformation(netuser.userID.ToString());
            double drabloons = player.drabloons;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitleAdd", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()) + " + " + 500);
            player.drabloons = drabloons + 500;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }
        void CurrencyAdminControl3(NetUser netuser)
        {
            Player player = playerInformation(netuser.userID.ToString());
            double drabloons = player.drabloons;

            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitleSub", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()) + " - " + 500);
            player.drabloons = drabloons - 500;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }
        void CurrencyAdminControl4(NetUser netuser, string target)
        {
            Player player = playerInformation(target);
            double drabloons = player.drabloons;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitleAdd", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()) + " + " + 500);
            player.drabloons = drabloons + 500;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }
        void CurrencyAdminControl5(NetUser netuser, string target)
        {
            Player player = playerInformation(target);
            double drabloons = player.drabloons;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitleAdd", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()) + " - " + 500);
            player.drabloons = drabloons - 500;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }
        void CurrencyAdminControl6(NetUser netuser, string target, double amount)
        {
            Player player = playerInformation(target);
            double drabloons = player.drabloons;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitleAdd", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()) + " + " + amount);
            player.drabloons = drabloons + amount;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }
        void CurrencyAdminControl7(NetUser netuser, string target, double amount)
        {
            Player player = playerInformation(target);
            double drabloons = player.drabloons;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("CurrencyAdminTitleAdd", netuser.userID.ToString()));
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()) +  " - " + amount);
            player.drabloons = drabloons - amount;
            rust.SendChatMessage(netuser, GetMessage("Prefix", netuser.userID.ToString()), GetMessage("Currencyavailable", netuser.userID.ToString()) + player.drabloons + GetMessage("Currencyname", netuser.userID.ToString()));
        }

            void OnPlayerConnected(NetUser netuser)
        {
            Player player = playerInformation(netuser.userID.ToString());
            if (player.firsttime == true)
            {
                return;
            }
            else
            {
                player.drabloons = Startingdrabloons;
                player.firsttime = true;
                SaveData();
            }
        }









        private void GiveItembelt(NetUser player, string item, int amount)
        {
            var inventory = rust.GetInventory(player);
            if (!datablocks.ContainsKey(item.ToLower())) return;
            inventory.AddItemAmount(datablocks[item.ToLower()], amount, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.KindFlags.Belt));
        }
        private void GiveWeaponbelt(NetUser player, string weapon, int ammo, string[] attachments)
        {
            var inventory = rust.GetInventory(player);
            if (!datablocks.ContainsKey(weapon.ToLower())) return;
            var weapondata = datablocks[weapon.ToLower()];
            var item = inventory.AddItem(weapondata, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.Kind.Belt), 1) as IWeaponItem;
            if (item == null) return;
            item.SetUses(ammo);
            item.SetTotalModSlotCount(4);
            foreach (var attachment in attachments)
            {
                if (!datablocks.ContainsKey(attachment.ToLower())) continue;
                var attachmentdata = datablocks[attachment.ToLower()] as ItemModDataBlock;
                item.AddMod(attachmentdata);
            }
        }
        private void GiveItem(NetUser player, string item, int amount)
        {
            var inventory = rust.GetInventory(player);
            if (!datablocks.ContainsKey(item.ToLower())) return;
            inventory.AddItemAmount(datablocks[item.ToLower()], amount, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.KindFlags.Belt));
        }
        private void GiveWeapon(NetUser player, string weapon, int ammo, string[] attachments)
        {
            var inventory = rust.GetInventory(player);
            if (!datablocks.ContainsKey(weapon.ToLower())) return;
            var weapondata = datablocks[weapon.ToLower()];
            var item = inventory.AddItem(weapondata, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.Kind.Default), 1) as IWeaponItem;
            if (item == null) return;
            item.SetUses(ammo);
            item.SetTotalModSlotCount(4);
            foreach (var attachment in attachments)
            {
                if (!datablocks.ContainsKey(attachment.ToLower())) continue;
                var attachmentdata = datablocks[attachment.ToLower()] as ItemModDataBlock;
                item.AddMod(attachmentdata);
            }
        }
        private void GiveArmor(NetUser player, string item, int amount)
        {
            var inventory = rust.GetInventory(player);
            if (!datablocks.ContainsKey(item.ToLower())) return;
            inventory.AddItemAmount(datablocks[item.ToLower()], amount, Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor, false, Inventory.Slot.KindFlags.Armor));
        }
        private Player playerInformation(string v)
        {
            Player player;
            if (!advshop.TryGetValue(v, out player))
            {
                player = new Player();
                advshop.Add(v, player);
            }
            return player;
        }
        class Player
        {
            public string name { get; set; }
            public string steamId { get; set; }
            public bool firsttime { get; set; }
            public double drabloons { get; set; }
        }
        string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
    }
}