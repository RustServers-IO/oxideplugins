PLUGIN.Title        = "Admin Spawn"
PLUGIN.Description  = "Manage administrator spawns and messages."
PLUGIN.Author       = "InSaNe8472"
PLUGIN.Version      = V(1,1,5)
PLUGIN.ResourceId   = 1644

local kits, popupApi

function PLUGIN:Init()
	permission.RegisterPermission("adminspawn.hide", self.Plugin)
	permission.RegisterPermission("adminspawn.warn", self.Plugin)
	permission.RegisterPermission("adminspawn.blacklist", self.Plugin)
	permission.RegisterPermission("adminspawn.all", self.Plugin)
	permission.RegisterPermission("adminspawn.ammunition", self.Plugin)
	permission.RegisterPermission("adminspawn.attire", self.Plugin)
	permission.RegisterPermission("adminspawn.construction", self.Plugin)
	permission.RegisterPermission("adminspawn.food", self.Plugin)
	permission.RegisterPermission("adminspawn.items", self.Plugin)
	permission.RegisterPermission("adminspawn.medical", self.Plugin)
	permission.RegisterPermission("adminspawn.misc", self.Plugin)
	permission.RegisterPermission("adminspawn.resources", self.Plugin)
	permission.RegisterPermission("adminspawn.tool", self.Plugin)
	permission.RegisterPermission("adminspawn.traps", self.Plugin)
	permission.RegisterPermission("adminspawn.weapon", self.Plugin)
	permission.RegisterPermission("adminspawn.kit", self.Plugin)
	command.AddChatCommand("giveme", self.Plugin, "cmdAdminSpawn")
	command.AddChatCommand("give", self.Plugin, "cmdAdminSpawn")
	command.AddChatCommand("giveall", self.Plugin, "cmdAdminSpawn")
	command.AddChatCommand("drop", self.Plugin, "cmdAdminSpawn")
	command.AddChatCommand("givekit", self.Plugin, "cmdAdminSpawn")
	command.AddConsoleCommand("global.givekit", self.Plugin, "cmdGiveKit")
	self:LoadDefaultConfig()
	self:LoadDefaultLang()
end

function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = self.Config.Settings or {}
	self.Config.Blacklist = self.Config.Blacklist or {}
	self.Config.Settings.Console = self.Config.Settings.Console or "true"
	self.Config.Settings.Log = self.Config.Settings.Log or "true"
	self.Config.Settings.LogFile = self.Config.Settings.LogFile or "oxide/logs/AdminSpawn.txt"
	self.Config.Settings.Popup = self.Config.Settings.Popup or "true"
	self.Config.Settings.Blacklist = self.Config.Settings.Blacklist or "false"
	self.Config.Settings.WarnChat = self.Config.Settings.WarnChat or "false"
	self.Config.Settings.WarnUser = self.Config.Settings.WarnUser or "true"
	self.Config.Settings.WarnGiveTo = self.Config.Settings.WarnGiveTo or "true"
	self.Config.Settings.OneHundred = self.Config.Settings.OneHundred or "100"
	self.Config.Settings.OneThousand = self.Config.Settings.OneThousand or "1000"
	self.Config.Settings.GiveAllSleeping = self.Config.Settings.GiveAllSleeping or "true"
	self.Config.Settings.MessageSize = self.Config.Settings.MessageSize or "12"
	self.Config.Blacklist.Items = self.Config.Blacklist.Items or {
		"rifle.ak",
		"supply.signal"
	}
	if not tonumber(self.Config.Settings.OneHundred) or tonumber(self.Config.Settings.OneHundred) < 1 then self.Config.Settings.OneHundred = "100" end
	if not tonumber(self.Config.Settings.OneThousand) or tonumber(self.Config.Settings.OneThousand) < 1 then self.Config.Settings.OneThousand = "1000" end
	if not tonumber(self.Config.Settings.MessageSize) or tonumber(self.Config.Settings.MessageSize) < 1 then self.Config.Settings.MessageSize = "12" end
	self:SaveConfig()
end

function PLUGIN:LoadDefaultLang()
	lang.RegisterMessages(util.TableToLangDict({
		["Prefix"] = "[<color=#cd422b> Admin Spawn </color>] ",
		["NoPermission"] = "You do not have permission to spawn <color=#cd422b>{item}</color>.",
		["NoPlayer"] = "Player not found or multiple players found.  Provide a more specific username.",
		["Invalid"] = "Invalid item <color=#ffd479>{item}</color>.",
		["AdminSpawn"] = "<color=#cd422b>{player}</color> gave <color=#ffd479>{amount} {item}</color> to <color=#cd422b>{target}</color>.",
		["AdminSpawnKit"] = "<color=#cd422b>{player}</color> gave kit <color=#ffd479>{kit}</color> to <color=#cd422b>{target}</color>.",
		["GiveTo"] = "Administrator gave you <color=#ffd479>{amount} {item}</color>.",
		["GiveToKit"] = "Administrator gave you kit <color=#ffd479>{kit}</color>.",
		["GivePlayer"] = "<color=#cd422b>{player}</color> received <color=#ffd479>{amount} {item}</color>.",
		["GivePlayerKit"] = "<color=#cd422b>{player}</color> received kit <color=#ffd479>{kit}</color>.",
		["LangError"] = "Language Error: {lang}",
		["UnknownCmd"] = "Unknown command: {cmd}",
		["InvalidCmd"] = "Invalid command: {cmd}",
		["AllPlayers"] = "All players",
		["ConsoleName"] = "Console",
		["ChatGiveMe"] = "Usage: /giveme <item> [quantity] [skin]",
		["ChatGive"] = "Usage: /give <player> <item> [quantity] [skin]",
		["ChatGiveAll"] = "Usage: /giveall <item> [quantity] [skin]",
		["ChatDrop"] = "Usage: /drop <item> [quantity] [skin]",
		["ChatGiveKit"] = "Usage: /givekit <player | *> <kit>",
		["NotAcc"] = "{cmd}: not accessible from server console",
		["F1GiveArm"] = "Usage: givearm <item> [quantity] [skin]",
		["F1GiveID"] = "Usage: giveid <item> [quantity] [skin]",
		["CSGive"] = "Usage: give <item> [quantity] [skin]",
		["CSGiveTo"] = "Usage: giveto <player> <item> [quantity] [skin]",
		["CSGiveAll"] = "Usage: giveall <item> [quantity] [skin]",
		["CSGiveKit"] = "Usage: givekit <player | *> <kit>",
		["NoKitPlugin"] = "Kit plugin was not found.",
		["NoKit"] = "Kit <color=#ffd479>{kit}</color> was not found."
	}), self.Plugin)
end

function PLUGIN:OnServerInitialized()
	kits = plugins.Find("Kits") or false
	popupApi = plugins.Find("PopupNotifications") or false
end

local function FormatMessage(message, values)
	for key, value in pairs(values) do message = message:gsub("{" .. key .. "}", value) end
	return message
end

function PLUGIN:Lang(player, lng)
	local playerSteamID
	if player and player ~= nil then playerSteamID = rust.UserIDFromPlayer(player) end
	local message = lang.GetMessage(lng, self.Plugin, playerSteamID)
	if message == lng then message = FormatMessage(self:Lang(player, "LangError"), { lang = lng }) end
	return message
end

local function StripMessage(message)
	local message = message:gsub("<color=%p*%w*>", "")
	message = message:gsub("</color>", "")
	return message
end

function PLUGIN:cmdAdminSpawn(player, cmd, args)
	if cmd:lower() == "giveme" then
		local silent = self:CheckSilent(player)
		if silent then
			local message = FormatMessage(self:Lang(player, "UnknownCmd"), { cmd = cmd })
			rust.SendChatMessage(player, message)
			return
		end
		if args.Length < 1 then
			self:RustMessage(player, self:Lang(player, "ChatGiveMe"))
			return
		end
		local ItemName, SpawnAmt = args[0]:lower(), 1
		local ItemID = self:GetItemID(ItemName)
		local auth = self:CheckAuth(player, ItemID, ItemName, 1)
		if not auth then return false end
		if args.Length > 1 then
			SpawnAmt = args[1]
			if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
		end
		local Skin = 0
		if args.Length > 2 then
			Skin = args[2]
			if not tonumber(Skin) then Skin = 0 end
		end
		local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
		item:MoveToContainer(player.inventory.containerMain, -1)
		if self.Config.Settings.WarnChat ~= "true" then
			local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = player.displayName, amount = SpawnAmt, item = item.info.displayName.translated })
			self:RustMessage(player, message)
		end
		self:WarnPlayers(player, player.displayName, SpawnAmt, item, player.displayName, 0)
	end
	if cmd:lower() == "give" then
		local silent = self:CheckSilent(player)
		if silent then
			local message = FormatMessage(self:Lang(player, "UnknownCmd"), { cmd = cmd })
			rust.SendChatMessage(player, message)
			return
		end
		if args.Length < 2 then
			self:RustMessage(player, self:Lang(player, "ChatGive"))
			return
		end
		local target, ItemName, SpawnAmt = args[0], args[1]:lower(), 1
		local found, targetplayer, targetname = self:CheckPlayer(player, target, 1)
		if not found then return false end
		local ItemID = self:GetItemID(ItemName)
		local auth = self:CheckAuth(player, ItemID, ItemName, 1)
		if not auth then return false end
		if args.Length > 2 then
			SpawnAmt = args[2]
			if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
		end
		local Skin = 0
		if args.Length > 3 then
			Skin = args[3]
			if not tonumber(Skin) then Skin = 0 end
		end
		local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
		item:MoveToContainer(targetplayer.inventory.containerMain, -1)
		if self.Config.Settings.WarnChat ~= "true" then
			local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = targetname, amount = SpawnAmt, item = item.info.displayName.translated })
			self:RustMessage(player, message)
		end
		self:WarnPlayers(player, player.displayName, SpawnAmt, item, targetname, 0)
		if player ~= targetplayer and self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
			local message = FormatMessage(self:Lang(player, "GiveTo"), { amount = SpawnAmt, item = item.info.displayName.translated })
			self:RustMessage(targetplayer, message)
			self:ShowPopup(targetplayer, message)
		end
	end
	if cmd:lower() == "giveall" then
		local silent = self:CheckSilent(player)
		if silent then
			local message = FormatMessage(self:Lang(player, "UnknownCmd"), { cmd = cmd })
			rust.SendChatMessage(player, message)
			return
		end
		if args.Length < 1 then
			self:RustMessage(player, self:Lang(player, "ChatGiveAll"))
			return
		end
		local ItemName, SpawnAmt = args[0]:lower(), 1
		local ItemID = self:GetItemID(ItemName)
		local auth = self:CheckAuth(player, ItemID, ItemName, 1)
		if not auth then return false end
		if args.Length > 1 then
			SpawnAmt = args[1]
			if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
		end
		local Skin = 0
		if args.Length > 2 then
			Skin = args[2]
			if not tonumber(Skin) then Skin = 0 end
		end
		local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
		local message = FormatMessage(self:Lang(player, "GiveTo"), { amount = SpawnAmt, item = item.info.displayName.translated })
		local players = global.BasePlayer.activePlayerList:GetEnumerator()
		while players:MoveNext() do
			item = self:SpawnItem(ItemID, SpawnAmt, Skin)
			item:MoveToContainer(players.Current.inventory.containerMain, -1)
			if player ~= players.Current and self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
				self:RustMessage(players.Current, message)
				self:ShowPopup(players.Current, message)
			end
		end
		if self.Config.Settings.GiveAllSleeping == "true" then
			local players = global.BasePlayer.sleepingPlayerList:GetEnumerator()
			while players:MoveNext() do
				item = self:SpawnItem(ItemID, SpawnAmt, Skin)
				item:MoveToContainer(players.Current.inventory.containerMain, -1)
			end
		end
		if self.Config.Settings.WarnChat ~= "true" then
			local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = self:Lang(player, "AllPlayers"), amount = SpawnAmt, item = item.info.displayName.translated })
			self:RustMessage(player, message)
		end
		self:WarnPlayers(player, player.displayName, SpawnAmt, item, self:Lang(player, "AllPlayers"), 0)
	end
	if cmd:lower() == "drop" then
		local silent = self:CheckSilent(player)
		if silent then
			local message = FormatMessage(self:Lang(player, "UnknownCmd"), { cmd = cmd })
			rust.SendChatMessage(player, message)
			return
		end
		if args.Length < 1 then
			self:RustMessage(player, self:Lang(player, "ChatDrop"))
			return
		end
		local ItemName, SpawnAmt = args[0]:lower(), 1
		local ItemID = self:GetItemID(ItemName)
		local auth = self:CheckAuth(player, ItemID, ItemName, 1)
		if not auth then return false end
		if args.Length > 1 then
			SpawnAmt = args[1]
			if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
		end
		local Skin = 0
		if args.Length > 2 then
			Skin = args[2]
			if not tonumber(Skin) then Skin = 0 end
		end
		local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
		item:Drop(player:GetDropPosition(), player:GetDropVelocity(), player.transform.rotation)
		if self.Config.Settings.WarnChat ~= "true" then
			local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = player.displayName, amount = SpawnAmt, item = item.info.displayName.translated })
			self:RustMessage(player, message)
		end
		self:WarnPlayers(player, player.displayName, SpawnAmt, item, player.displayName, 0)
	end
	if cmd:lower() == "givekit" then
		local silent = self:CheckSilent(player)
		if silent then
			local message = FormatMessage(self:Lang(player, "UnknownCmd"), { cmd = cmd })
			rust.SendChatMessage(player, message)
			return
		end
		if args.Length < 2 then
			self:RustMessage(player, self:Lang(player, "ChatGiveKit"))
			return
		end
		local target, ItemName = args[0], args[1]
		local playerSteamID = rust.UserIDFromPlayer(player)
		if not permission.UserHasPermission(playerSteamID, "adminspawn.all") and not permission.UserHasPermission(playerSteamID, "adminspawn.kit") then
			local message = FormatMessage(self:Lang(player, "NoPermission"), { item = ItemName })
			self:RustMessage(player, message)
			return
		end
		self:SpawnKit(player, target, ItemName, 1)
	end
	return
end

function PLUGIN:cmdGiveKit(arg)
	if arg and arg.cmd then
		if arg.connection then
			local player = arg.connection.player
			local silent = self:CheckSilent(player)
			if silent then
				local message = FormatMessage(self:Lang(player, "InvalidCmd"), { cmd = arg.cmd.namefull })
				player:SendConsoleCommand("echo "..message)
				return
			end
			if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 2 then
				player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "CSGiveKit"))
				return
			end
			local target, ItemName, SpawnAmt = arg.Args[0], arg.Args[1]
			local playerSteamID = rust.UserIDFromPlayer(player)
			if not permission.UserHasPermission(playerSteamID, "adminspawn.all") and not permission.UserHasPermission(playerSteamID, "adminspawn.kit") then
				local message = FormatMessage(self:Lang(player, "NoPermission"), { item = ItemName })
				player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
				return
			end
			self:SpawnKit(player, target, ItemName, 2)
			else
			if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 2 then
				self:Console(self:Lang(player, "CSGiveKit"))
				return
			end
			local target, ItemName = arg.Args[0], arg.Args[1]
			self:SpawnKit(nil, target, ItemName, 3)
		end
	end
end

function PLUGIN:OnServerCommand(arg)
	if arg and arg.cmd then
		local cmd = arg.cmd.name
		if cmd == "givebp" then return false end
		if cmd == "givearm" or cmd == "giveid" then
			if not arg.connection then
				local message = FormatMessage(self:Lang(player, "NotAcc"), { cmd = cmd })
				self:Console(StripMessage(message))
				return false
			end
			local player = arg.connection.player
			local silent = self:CheckSilent(player)
			if silent then return false end
			if arg.Args then
				local ItemID = arg.Args[0]:lower()
				if not tonumber(ItemID) then ItemID = self:GetItemID(ItemID) end
				local auth, ItemName = self:CheckAuth(player, ItemID, arg.Args[0], 3)
				if not auth then return false end
				local SpawnAmt, item = 1
				if arg.Args.Length > 1 then
					SpawnAmt = arg.Args[1]
					if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
				end
				local Skin = 0
				if arg.Args.Length > 2 then
					Skin = arg.Args[2]
					if not tonumber(Skin) then Skin = 0 end
				end
				if cmd == "givearm" then
					item = self:SpawnItem(ItemID, SpawnAmt, Skin)
					local Belt = player.inventory.containerBelt.itemList:GetEnumerator()
					local BeltItems = 0
					while Belt:MoveNext() do
						if Belt.Current.position then
							BeltItems = BeltItems + 1
						end
					end
					if BeltItems < 6 then
						item:MoveToContainer(player.inventory.containerBelt, -1)
						else
						item:MoveToContainer(player.inventory.containerMain, -1)
					end
				end
				if cmd == "giveid" then
					if arg.Args.Length > 1 then SpawnAmt = tonumber(arg.Args[1]) end
					if SpawnAmt == 100 then SpawnAmt = tonumber(self.Config.Settings.OneHundred) end
					if SpawnAmt == 1000 then SpawnAmt = tonumber(self.Config.Settings.OneThousand) end
					item = self:SpawnItem(ItemID, SpawnAmt, Skin)
					item:MoveToContainer(player.inventory.containerMain, -1)
				end
				local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = player.displayName, amount = SpawnAmt, item = item.info.displayName.translated })
				player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
				self:WarnPlayers(player, player.displayName, SpawnAmt, item, player.displayName, 0)
				return false
				else
				if cmd == "givearm" then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "F1GiveArm")) end
				if cmd == "giveid" then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "F1GiveID")) end
				return false
			end
		end
		if cmd == "give" then
			if arg.connection then
				local player = arg.connection.player
				local silent = self:CheckSilent(player)
				if silent then return false end
				if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 1 then
					player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "CSGive"))
					return false
				end
				local ItemName, SpawnAmt = arg.Args[0]:lower(), 1
				local ItemID = self:GetItemID(ItemName)
				local auth = self:CheckAuth(player, ItemID, arg.Args[0], 2)
				if not auth then return false end
				if arg.Args.Length > 1 then
					SpawnAmt = arg.Args[1]
					if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
				end
				local Skin = 0
				if arg.Args.Length > 2 then
					Skin = arg.Args[2]
					if not tonumber(Skin) then Skin = 0 end
				end
				local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
				item:MoveToContainer(player.inventory.containerMain, -1)
				local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = player.displayName, amount = SpawnAmt, item = item.info.displayName.translated })
				player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
				self:WarnPlayers(player, player.displayName, SpawnAmt, item, player.displayName, 0)
				return false
				else
				local message = FormatMessage(self:Lang(player, "NotAcc"), { cmd = cmd })
				self:Console(StripMessage(message))
				return false
			end
		end
		if cmd == "giveto" then
			if arg.connection then
				local player = arg.connection.player
				local silent = self:CheckSilent(player)
				if silent then return false end
				if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 2 then
					player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "CSGiveTo"))
					return false
				end
				local target, ItemName, SpawnAmt = arg.Args[0], arg.Args[1]:lower(), 1
				local found, targetplayer, targetname = self:CheckPlayer(player, target, 2)
				if not found then return false end
				local ItemID = self:GetItemID(ItemName)
				local auth = self:CheckAuth(player, ItemID, arg.Args[1], 2)
				if not auth then return false end
				if arg.Args.Length > 2 then
					SpawnAmt = arg.Args[2]
					if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
				end
				local Skin = 0
				if arg.Args.Length > 3 then
					Skin = arg.Args[3]
					if not tonumber(Skin) then Skin = 0 end
				end
				local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
				item:MoveToContainer(targetplayer.inventory.containerMain, -1)
				local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = targetname, amount = SpawnAmt, item = item.info.displayName.translated })
				player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
				self:WarnPlayers(player, player.displayName, SpawnAmt, item, targetname, 0)
				if player ~= targetplayer and self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
					local message = FormatMessage(self:Lang(player, "GiveTo"), { amount = SpawnAmt, item = item.info.displayName.translated })
					self:RustMessage(targetplayer, message)
					self:ShowPopup(targetplayer, message)
				end
				else
				if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 2 then
					self:Console(StripMessage(self:Lang(nil, "CSGiveTo")))
					return false
				end
				local target, ItemName, SpawnAmt = arg.Args[0], arg.Args[1]:lower(), 1
				local found, targetplayer, targetname = self:CheckPlayer(nil, target, 3)
				if not found then return false end
				local ItemID = self:GetItemID(ItemName)
				if not ItemID then
					local message = FormatMessage(self:Lang(nil, "Invalid"), { item = ItemName })
					self:Console(StripMessage(message))
					return false
				end
				if arg.Args.Length > 2 then
					SpawnAmt = arg.Args[2]
					if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
				end
				local Skin = 0
				if arg.Args.Length > 3 then
					Skin = arg.Args[3]
					if not tonumber(Skin) then Skin = 0 end
				end
				local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
				item:MoveToContainer(targetplayer.inventory.containerMain, -1)
				local message = FormatMessage(self:Lang(nil, "GivePlayer"), { player = targetname, amount = SpawnAmt, item = item.info.displayName.translated })
				self:Console(StripMessage(message))
				self:WarnPlayers(nil, self:Lang(nil, "ConsoleName"), SpawnAmt, item, targetname, 0)
				if self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
					local message = FormatMessage(self:Lang(nil, "GiveTo"), { amount = SpawnAmt, item = item.info.displayName.translated })
					self:RustMessage(targetplayer, message)
					self:ShowPopup(targetplayer, message)
				end
			end
			return false
		end
		if cmd == "giveall" then
			if arg.connection then
				local player = arg.connection.player
				local silent = self:CheckSilent(player)
				if silent then return false end
				if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 1 then
					player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "CSGiveAll"))
					return false
				end
				local ItemName, SpawnAmt = arg.Args[0]:lower(), 1
				local ItemID = self:GetItemID(ItemName)
				local auth = self:CheckAuth(player, ItemID, arg.Args[0], 2)
				if not auth then return false end
				if arg.Args.Length > 1 then
					SpawnAmt = arg.Args[1]
					if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
				end
				local Skin = 0
				if arg.Args.Length > 2 then
					Skin = arg.Args[2]
					if not tonumber(Skin) then Skin = 0 end
				end
				local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
				local message = FormatMessage(self:Lang(player, "GiveTo"), { amount = SpawnAmt, item = item.info.displayName.translated })
				local players = global.BasePlayer.activePlayerList:GetEnumerator()
				while players:MoveNext() do
					item = self:SpawnItem(ItemID, SpawnAmt, Skin)
					item:MoveToContainer(players.Current.inventory.containerMain, -1)
					if player ~= players.Current and self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
						self:RustMessage(players.Current, message)
						self:ShowPopup(players.Current, message)
					end
				end
				if self.Config.Settings.GiveAllSleeping == "true" then
					local players = global.BasePlayer.sleepingPlayerList:GetEnumerator()
					while players:MoveNext() do
						item = self:SpawnItem(ItemID, SpawnAmt, Skin)
						item:MoveToContainer(players.Current.inventory.containerMain, -1)
					end
				end
				local message = FormatMessage(self:Lang(player, "GivePlayer"), { player = self:Lang(player, "AllPlayers"), amount = SpawnAmt, item = item.info.displayName.translated })
				player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
				self:WarnPlayers(player, player.displayName, SpawnAmt, item, self:Lang(player, "AllPlayers"), 0)
				else
				if not arg.Args or arg.ArgsStr == nil or arg.Args.Length < 1 then
					self:Console(StripMessage(self:Lang(nil, "CSGiveAll")))
					return false
				end
				local ItemName, SpawnAmt = arg.Args[0]:lower(), 1
				local ItemID = self:GetItemID(ItemName)
				if not ItemID then
					local message = FormatMessage(self:Lang(nil, "Invalid"), { item = ItemName })
					self:Console(StripMessage(message))
					return false
				end
				if arg.Args.Length > 1 then
					SpawnAmt = arg.Args[1]
					if not tonumber(SpawnAmt) or tonumber(SpawnAmt) <= 0 then SpawnAmt = 1 end
				end
				local Skin = 0
				if arg.Args.Length > 2 then
					Skin = arg.Args[2]
					if not tonumber(Skin) then Skin = 0 end
				end
				local item = self:SpawnItem(ItemID, SpawnAmt, Skin)
				local message = FormatMessage(self:Lang(nil, "GiveTo"), { amount = SpawnAmt, item = item.info.displayName.translated })
				local players = global.BasePlayer.activePlayerList:GetEnumerator()
				while players:MoveNext() do
					item = self:SpawnItem(ItemID, SpawnAmt, Skin)
					item:MoveToContainer(players.Current.inventory.containerMain, -1)
					if self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
						self:RustMessage(players.Current, message)
						self:ShowPopup(players.Current, message)
					end
				end
				if self.Config.Settings.GiveAllSleeping == "true" then
					local players = global.BasePlayer.sleepingPlayerList:GetEnumerator()
					while players:MoveNext() do
						item = self:SpawnItem(ItemID, SpawnAmt, Skin)
						item:MoveToContainer(players.Current.inventory.containerMain, -1)
					end
				end
				local message = FormatMessage(self:Lang(nil, "GivePlayer"), { player = self:Lang(nil, "AllPlayers"), amount = SpawnAmt, item = item.info.displayName.translated })
				self:Console(StripMessage(message))
				self:WarnPlayers(nil, self:Lang(nil, "ConsoleName"), SpawnAmt, item, self:Lang(nil, "AllPlayers"), 0)
			end
			return false
		end
	end
end

function PLUGIN:GetItemID(ItemName)
	local ItemDefinition = global.ItemManager.FindItemDefinition.methodarray[1]
	local TableArray = util.TableToArray({ItemName})
	Item = ItemDefinition:Invoke(nil, TableArray)
	if Item then return Item.itemid end
	return nil
end

function PLUGIN:CheckSilent(player)
	local playerSteamID = rust.UserIDFromPlayer(player)
	if permission.UserHasPermission(playerSteamID, "adminspawn.hide") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.warn") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.blacklist") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.all") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.ammunition") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.attire") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.construction") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.food") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.items") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.medical") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.misc") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.resources") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.tool") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.traps") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.weapon") then return false end
	if permission.UserHasPermission(playerSteamID, "adminspawn.kit") then return false end
	return true
end

function PLUGIN:CheckAuth(player, ItemID, ItemName, call)
	if not ItemID then
		local message = FormatMessage(self:Lang(player, "Invalid"), { item = ItemName })
		if call == 1 then self:RustMessage(player, message) end
		if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message) end
		if call == 3 then
			self:RustMessage(player, message)
			player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
		end
		return false
	end
	local ItemDefinition = global.ItemManager.FindItemDefinition.methodarray[0]
	local TableArray = util.TableToArray({ItemID})
	util.ConvertAndSetOnArray(TableArray, 0, ItemID, System.Int32._type)
	local Item = ItemDefinition:Invoke(nil, TableArray)
	if not Item then
		local message = FormatMessage(self:Lang(player, "Invalid"), { item = ItemName })
		if call == 1 then self:RustMessage(player, message) end
		if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message) end
		if call == 3 then
			self:RustMessage(player, message)
			player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
		end
		return false
	end
	local ItemName = Item.shortname
	local ItemCat = tostring(Item.category):match("([^:]+)"):lower()
	local playerSteamID = rust.UserIDFromPlayer(player)
	if not permission.UserHasPermission(playerSteamID, "adminspawn.all") and not permission.UserHasPermission(playerSteamID, "adminspawn."..ItemCat) then
		local message = FormatMessage(self:Lang(player, "NoPermission"), { item = ItemName })
		if call == 1 then self:RustMessage(player, message) end
		if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message) end
		if call == 3 then
			self:RustMessage(player, message)
			player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
		end
		return false
	end
	if self.Config.Settings.Blacklist == "true" then
		if not permission.UserHasPermission(playerSteamID, "adminspawn.blacklist") then
			if self.Config.Blacklist.Items[1] then
				local i = 1
				while self.Config.Blacklist.Items[i] do
					if ItemName:lower() == self.Config.Blacklist.Items[i]:lower() then
						local message = FormatMessage(self:Lang(player, "NoPermission"), { item = ItemName })
						if call == 1 then self:RustMessage(player, message) end
						if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message) end
						if call == 3 then
							self:RustMessage(player, message)
							player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message)
						end
						return false
					end
					i = i + 1
				end
			end
		end
	end
	return true, ItemName
end

function PLUGIN:CheckPlayer(player, target, call)
	local target = rust.FindPlayer(target)
	if not target then
		if call == 1 then self:RustMessage(player, self:Lang(player, "NoPlayer")) end
		if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "NoPlayer")) end
		if call == 3 then self:Console(StripMessage(self:Lang(player, "NoPlayer"))) end
		return false
	end
	return true, target, target.displayName
end

function PLUGIN:SpawnItem(ItemID, SpawnAmt, Skin)
	return global.ItemManager.CreateByItemID(tonumber(ItemID), tonumber(SpawnAmt), tonumber(Skin))
end

function PLUGIN:SpawnKit(player, target, ItemName, call)
	if kits then
		local found, targetplayer, targetname
		if target ~= "*" then
			found, targetplayer, targetname = self:CheckPlayer(player, target, call)
			if not found then return end
			else
			targetname = self:Lang(nil, "AllPlayers")
		end
		if kits:CallHook("isKit", ItemName) then
			if target ~= "*" then
				kits:CallHook("GiveKit", targetplayer, ItemName)
				else
				local players = global.BasePlayer.activePlayerList:GetEnumerator()
				while players:MoveNext() do
					kits:CallHook("GiveKit", players.Current, ItemName)
					if players.Current ~= player and self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
						local message = FormatMessage(self:Lang(players.Current, "GiveToKit"), { kit = ItemName })
						self:RustMessage(players.Current, message)
						self:ShowPopup(players.Current, message)
					end
				end
				if self.Config.Settings.GiveAllSleeping == "true" then
					local players = global.BasePlayer.sleepingPlayerList:GetEnumerator()
					while players:MoveNext() do
						kits:CallHook("GiveKit", players.Current, ItemName)
					end
				end
			end
			if self.Config.Settings.WarnChat ~= "true" then
				local message = FormatMessage(self:Lang(player, "GivePlayerKit"), { player = targetname, kit = ItemName })
				if call == 1 then self:RustMessage(player, message) end
				if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message) end
				if call == 3 then self:Console(message) end
			end
			if call == 1 or call == 2 then self:WarnPlayers(player, player.displayName, 0, ItemName, targetname, 1) end
			if call == 3 then self:WarnPlayers(nil, self:Lang(nil, "ConsoleName"), 0, ItemName, targetname, 1) end
			if target ~= "*" then
				if player ~= targetplayer and self.Config.Settings.WarnChat ~= "true" and self.Config.Settings.WarnGiveTo == "true" then
					local message = FormatMessage(self:Lang(player, "GiveToKit"), { kit = ItemName })
					self:RustMessage(targetplayer, message)
					self:ShowPopup(targetplayer, message)
				end
			end
			return
		end
		local message = FormatMessage(self:Lang(player, "NoKit"), { kit = ItemName })
		if call == 1 then self:RustMessage(player, message) end
		if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..message) end
		if call == 3 then self:Console(message) end
		return
	end
	if call == 1 then self:RustMessage(player, self:Lang(player, "NoKitPlugin")) end
	if call == 2 then player:SendConsoleCommand("echo "..self:Lang(player, "Prefix")..self:Lang(player, "NoKitPlugin")) end
	if call == 3 then self:Console(self:Lang(player, "NoKitPlugin")) end
	return
end

function PLUGIN:WarnPlayers(player, playerName, SpawnAmt, item, target, kit)
	local message
	if kit == 0 then
		message = FormatMessage(self:Lang(player, "AdminSpawn"), { player = playerName, amount = SpawnAmt, item = item.info.displayName.translated, target = target })
		else
		message = FormatMessage(self:Lang(player, "AdminSpawnKit"), { player = playerName, kit = item, target = target })
	end
	if self.Config.Settings.Log == "true" then
		self:LogEvent(StripMessage(message))
	end
	if player ~= nil and player then
		local playerSteamID = rust.UserIDFromPlayer(player)
		if permission.UserHasPermission(playerSteamID, "adminspawn.hide") then return end
	end
	self:Console(message)
	if self.Config.Settings.WarnChat == "true" then
		self:RustBroadcast(message)
		else
		if self.Config.Settings.WarnUser == "true" then
			local players = global.BasePlayer.activePlayerList:GetEnumerator()
			while players:MoveNext() do
				if players.Current ~= player then
					local targetSteamID = rust.UserIDFromPlayer(players.Current)
					if permission.UserHasPermission(targetSteamID, "adminspawn.warn") then
						self:RustMessage(players.Current, message)
					end
				end
			end
		end
	end
end

function PLUGIN:Console(message)
	if self.Config.Settings.Console == "true" then
		UnityEngine.Debug.LogWarning.methodarray[0]:Invoke(nil, util.TableToArray({ StripMessage(self:Lang(player, "Prefix")..message) }))
	end
end

function PLUGIN:ShowPopup(player, message)
	if self.Config.Settings.Popup == "true" and popupApi then
		popupApi:CallHook("CreatePopupNotification", message, player)
	end
end

function PLUGIN:RustMessage(player, message)
	rust.SendChatMessage(player, "<size="..tonumber(self.Config.Settings.MessageSize)..">"..self:Lang(player, "Prefix")..message.."</size>")
end

function PLUGIN:RustBroadcast(message)
	rust.BroadcastChat("<size="..tonumber(self.Config.Settings.MessageSize)..">"..self:Lang(nil, "Prefix")..message.."</size>")
end

function PLUGIN:LogEvent(logdata)
	ConVar.Server.Log(self.Config.Settings.LogFile, logdata)
end