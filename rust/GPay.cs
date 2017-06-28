using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Oxide.Core;
using System.Text;

namespace Oxide.Plugins {

    [Info("GPay", "Soccerjunki", "1.0.1", ResourceId = 2386)]
    [Description("Auto donation system")]
    class GPay : RustPlugin {

        protected override void LoadDefaultConfig() {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            Config["Secret"] = "GPAY_SECRET_KEY";

            Dictionary<string, int> item = new Dictionary<string, int>();
            Dictionary<string, List<Dictionary<string, int>>> packages = new Dictionary<string, List<Dictionary<string, int>>>();
            List<Dictionary<string, int>> package1 = new List<Dictionary<string, int>>();
            item.Add("itemID", -1035059994);
            item.Add("qty", 70);
            package1.Add(item);
            item = new Dictionary<string, int>();

            item.Add("itemID", -191795897);
            item.Add("qty", 1);
            package1.Add(item);
            item = new Dictionary<string, int>();

            item.Add("itemID", -1578894260);
            item.Add("qty", 70);
            package1.Add(item);
            item = new Dictionary<string, int>();

            packages.Add("GPAY_PROD_ID", package1);

            Config["packages"] = packages;
            SaveConfig();
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
                {
                    {"NoDonationMessage", "No Donation was found"},
                    {"DonationFoundMessage", "Thanks for donating, your items have been added to your inventory"},
                    {"NoInventorySpace", "Please empty your inventory before claiming your donation!"},
                    {"CouldNotConnect", "Could not connect to the donation server, please try again later!"},
                    {"DonationLink", "You can Donate at https://app.gpay.io/SERVERNAME"},
                    {"PleaseWait", "Please Wait"},
            }, this);
        }

        string Lang(string key, string userId = null) => lang.GetMessage(key, this, userId);

        [ChatCommand("donate")]
        void DonateCommand(BasePlayer player, string command, string[] args) {
              SendReply(player, Lang("DonationLink", player.UserIDString));
              return;
        }

        [ChatCommand("claimdonation")]
        void ClaimDonatCommand(BasePlayer player, string command, string[] args) {
              if (player.inventory.AllItems().Length >= 36) {
              SendReply(player, Lang("NoInventorySpace", player.UserIDString));
              return;
            }
            SendReply(player,Lang("PleaseWait", player.UserIDString).ToString());
            string steamid = player.userID.ToString();
            string secret = Config["Secret"].ToString();
            webrequest.EnqueueGet("http://app.gpay.io/api/ruststeam/" + steamid + "/" + secret, (code, response) => GetCallback(code, response, player), this);
        }

        void GetCallback(int code, string response, BasePlayer player) {
            if (response == null || code != 200) {
                Puts($"Error: {code} - Could not contact GPAY server || {player.displayName}");
                 SendReply(player,Lang("CouldNotConnect", player.UserIDString));
                return;
            }
            if(response.IndexOf("!error") >= 0){
                Puts("Error: "+response);
                 SendReply(player,Lang("NoDonationMessage", player.UserIDString));
                return;
            }
            if (response == "0") {
                SendReply(player,Lang("NoDonationMessage", player.UserIDString));
                return;
            } else {
                if (response.IndexOf(",") >= 0) {
                  string[] seperator = new string[] {","};
                  string[] packs = response.Split(seperator, StringSplitOptions.None);
                  foreach (string pack in packs) {
                      List<object> packb = (List<object>) Config.Get("packages", pack);
                      foreach (Dictionary<string, object> item in packb) {
                        if(item.ContainsKey("command")){
                          StringBuilder buildertwo = new StringBuilder(item["command"].ToString());
                          buildertwo.Replace("<username>", player.displayName.ToString());
                          buildertwo.Replace("<steamid>", player.userID.ToString());

                          string commandToRunb = buildertwo.ToString();
                          rust.RunServerCommand(commandToRunb);
                        }else{
                          player.inventory.GiveItem(ItemManager.CreateByItemID((int)item["itemID"], (int)item["qty"]));
                        }
                      }
                  }
                  SendReply(player,Lang("DonationFoundMessage", player.UserIDString));
                  return;
                } else {
                    List<object> packg = (List<object>) Config.Get("packages", response.ToString());
                    foreach (Dictionary<string, object> item in packg) {
                        if(item.ContainsKey("command")){
                          StringBuilder buildertwo = new StringBuilder(item["command"].ToString());
                          buildertwo.Replace("<username>", player.displayName.ToString());
                          buildertwo.Replace("<steamid>", player.userID.ToString());

                          string commandToRunb = buildertwo.ToString();
                          rust.RunServerCommand(commandToRunb);
                        }else{
                          player.inventory.GiveItem(ItemManager.CreateByItemID((int)item["itemID"], (int)item["qty"]));
                        }

                    }
                    SendReply(player,Lang("DonationFoundMessage", player.UserIDString));
                    return;
                }
            }
        }
    }
}
