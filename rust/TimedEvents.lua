PLUGIN.Title        = "Timed Events"
PLUGIN.Description  = "Allows automatic events to take place depending on server time."
PLUGIN.Author       = "InSaNe8472"
PLUGIN.Version      = V(1,1,4)
PLUGIN.ResourceId   = 1325

local ServerInitialized = false
local ESky, CurDay, LocalTime, ServerTime
local RepeatTimer = {}
local groupcnt = {}
local grouptmr = {}

function PLUGIN:Init()
	permission.RegisterPermission("timedevents.admin", self.Plugin)
	permission.RegisterPermission("timedevents.modify", self.Plugin)
	permission.RegisterPermission("timedevents.run", self.Plugin)
	permission.RegisterPermission("timedevents.group", self.Plugin)
	command.AddChatCommand("te", self.Plugin, "cmdTEvent")
	command.AddConsoleCommand("te.update", self.Plugin, "cmdUpdate")
	self:LoadDefaultConfig()
	self:LoadDefaultLang()
end

function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = self.Config.Settings or {}
	self.Config.Events = self.Config.Events or {}
	self.Config.Groups = self.Config.Groups or {}
	self.Config.Settings.Enabled = self.Config.Settings.Enabled or "true"
	self.Config.Settings.TimeFormat = self.Config.Settings.TimeFormat or "hh.mm.tt"
	self.Config.Settings.SayEnabled = self.Config.Settings.SayEnabled or "false"
	self.Config.Settings.ServerTime = self.Config.Settings.ServerTime or "true"
	self.Config.Settings.LocalTime = self.Config.Settings.LocalTime or "true"
	self.Config.Settings.Repeat = self.Config.Settings.Repeat or "true"
	self.Config.Settings.MessageSize = self.Config.Settings.MessageSize or "12"
	self.Config.Events.Local = self.Config.Events.Local or {
		"all:12.00.PM:say Time for an airdrop!",
		"all:12.00.PM:massdrop 1",
		"all:01.00.PM:say Welcome to my server!  Enjoy your stay!",
		"all:02.00.PM:say Play fair!  No Cheating!",
		"all:03.00.PM:say Time for another airdrop!",
		"all:03.00.PM:massdrop 1"
	}
	self.Config.Events.Server = self.Config.Events.Server or {
		"all:8:say Time for an airdrop!",
		"all:8:massdrop 1",
		"all:9:say Welcome to my server!  Enjoy your stay!",
		"all:10:say Play fair!  No Cheating!",
		"all:11:say Time for another airdrop!",
		"all:11:massdrop 1"
	}
	self.Config.Events.Repeat = self.Config.Events.Repeat or {
		"all:3600:say Saving server data.",
		"all:3600:server.save"
	}
	self.Config.Groups.restart = self.Config.Groups.restart or {
		"info:restart:Prepare server for restart with 60 second countdown",
		"0:say Server restarting in 60 seconds!",
		"30:say Server restarting in 30 seconds!",
		"55:say Server restarting in 5 seconds!",
		"60:server.save"
	}
	if not tonumber(self.Config.Settings.MessageSize) or tonumber(self.Config.Settings.MessageSize) < 1 then self.Config.Settings.MessageSize = "12" end
	self.Config.Settings.TimeFormat = self.Config.Settings.TimeFormat:gsub(":", ".")
	self:SaveConfig()
end

function PLUGIN:LoadDefaultLang()
	lang.RegisterMessages(util.TableToLangDict({
		["Active"] = "<color=#2bcd42>[ACTIVE]</color>",
		["ChangedStatus"] = "Timed events <color=#cd422b>{status}</color>.",
		["Disabled"] = "disabled",
		["Enabled"] = "enabled",
		["EventAdded"] = "Event Add - ID: <color=#cd422b>{id}</color>, Day: <color=#cd422b>{eday}</color>, Time: <color=#cd422b>{etime}</color>, Group: <color=#cd422b>{group}</color>\n	> <color=#cd422b>{event}</color>",
		["EventDeleted"] = "Event Delete - ID: <color=#cd422b>{id}</color>, Day: <color=#cd422b>{eday}</color>, Time: <color=#cd422b>{etime}</color>, Group: <color=#cd422b>{group}</color>\n	> <color=#cd422b>{event}</color>",
		["EventRun"] = "Event Run - ID: <color=#cd422b>{id}</color>, Day: <color=#cd422b>{eday}</color>, Time: <color=#cd422b>{etime}</color>, Group: <color=#cd422b>{group}</color>\n	> <color=#cd422b>{event}</color>",
		["EventsCleared"] = "All <color=#cd422b>{group}</color> events successfully deleted.",
		["GroupComplete"] = "Group <color=#cd422b>{group}</color> successfully completed.",
		["GroupInvalid"] = "Group <color=#cd422b>{group}</color> is invalid. Please check your configuration.",
		["GroupName"] = "Group",
		["GroupNotActive"] = "Group <color=#cd422b>{group}</color> is not currently active.",
		["GroupRestart"] = "Group <color=#cd422b>{group}</color> is currently active and has been restarted.",
		["GroupStart"] = "Group <color=#cd422b>{group}</color> successfully started.",
		["GroupStop"] = "Group <color=#cd422b>{group}</color> successfully stopped.",
		["Help"] = "<color=#ffd479>/te</color> - View, add, delete and run server events",
		["Inactive"] = "<color=#cd422b>[INACTIVE]</color>",
		["InvalidEvent"] = "Invalid event format.  Please check your configuration.",
		["InvalidFormat"] = "Invalid event format, <color=#cd422b>{event}</color>, for <color=#cd422b>{group}</color> group.  Error detected with <color=#cd422b>{section}</color>. Use <color=#cd422b>/te</color> for help.",
		["InvalidID"] = "Invalid ID <color=#cd422b>{id}</color> for <color=#cd422b>{group}</color> group. Use <color=#cd422b>/te</color> for help.",
		["LangError"] = "Language Error: {lang}",
		["Limits"] = "\n	Main System Enabled: <color=#ffd479>{l1}</color>\n"..
		"	Local Enabled: <color=#ffd479>{l2}</color>\n"..
		"	Server Enabled: <color=#ffd479>{l3}</color>\n"..
		"	Repeat Enabled: <color=#ffd479>{l4}</color>\n"..
		"	Time Format: <color=#ffd479>{l5}</color>",
		["ListFormat"] = "<color=#cd422b>{num}.</color> <color=#ffd479>{eday}, {etime}</color> - {eaction}\n",
		["ListGroupFormat"] = "<color=#cd422b>{num}.</color> {status}  <color=#ffd479>{name}</color> - {desc} ({count} events)\n",
		["ListMessage"] = "\n{group} List\n\n{list}",
		["Local"] = "local",
		["Menu"] = "\n	<color=#ffd479>/te toggle <system | local | server | repeat></color> - Enable or disable system\n"..
		"	<color=#ffd479>/te limits</color> - View limits and configuration\n"..
		"	<color=#ffd479>/te time</color> - View current server time\n"..
		"	<color=#ffd479>/te list <l | s | r></color> - View current events\n"..
		"	<color=#ffd479>/te add <l | s | r> <day:00.00.PM:Event | day:0-24:Event |\n	day:1-86400:Event></color> - Add new event (day = all, mon, tue, etc.)\n"..
		"	<color=#ffd479>/te delete <l | s | r> <id></color> - Delete event (list for id's)\n"..
		"	<color=#ffd479>/te clear <l | s | r></color> - Delete all events (cannot be undone)\n"..
		"	<color=#ffd479>/te run <l | s | r> <id></color> - Manually run event ID (list for id's)\n"..
		"	<color=#ffd479>/te group <start | stop | list> [group]</color> - Start, stop or list group events",
		["NoEvents"] = "No events found for <color=#cd422b>{group}</color> group.",
		["NoGroup"] = "Group <color=#cd422b>{group}</color> was not found. Please check your configuration.",
		["NoPermission"] = "You do not have permission to use this command.",
		["Prefix"] = "[<color=#cd422b>Timed Events</color>] ",
		["PrintToConsole"] = "Event list exceeds chat limit and has been sent to console (F1).",
		["Repeat"] = "repeat",
		["SayPrefix"] = "[<color=#cd422b> SERVER </color>] ",
		["SectionColon"] = "colon section count",
		["SectionDay"] = "day",
		["SectionEmpty"] = "one or more empty sections",
		["SectionEvent"] = "event",
		["SectionTime"] = "time",
		["Server"] = "server",
		["ServerTime"] = "Time: <color=#cd422b>{ltime}</color> (local), <color=#cd422b>{stime}</color> (server)",
		["TimeChangedStatus"] = "Event group <color=#cd422b>{group}</color> now <color=#cd422b>{status}</color>.",
		["WrongArgs"] = "Syntax error. Use <color=#cd422b>/te</color> for help."
	}), self.Plugin)
end

function PLUGIN:OnServerInitialized()
	ServerInitialized = true
	self:SetRepeat()
end

function PLUGIN:OnTick()
	if self.Config.Settings.Enabled == "true" then
		if self.Config.Settings.LocalTime == "true" and self.Config.Events.Local[1] then
			if LocalTime ~= time.GetCurrentTime():ToLocalTime():ToString(self.Config.Settings.TimeFormat) then
				LocalTime = time.GetCurrentTime():ToLocalTime():ToString(self.Config.Settings.TimeFormat)
				local i = 1
				while self.Config.Events.Local[i] do
					local EventDay, EventTime, EventAction = self.Config.Events.Local[i]:match("([^:]+):([^:]+):([^:]+)")
					if EventDay and EventTime and EventAction then
						if string.lower(EventDay) == "all" or string.lower(EventDay) == self:GetDay() then
							if string.lower(EventTime) == string.lower(LocalTime) then
								self:RunCommand(EventAction)
							end
						end
					end
					i = i + 1
				end
			end
		end
		if self.Config.Settings.ServerTime == "true" and self.Config.Events.Server[1] then
			if not ESky then ESky = global.TOD_Sky.get_Instance() end
			if ServerTime ~= tostring(ESky.Cycle.Hour):match"([^.]*).(.*)" then
				ServerTime = tostring(ESky.Cycle.Hour):match"([^.]*).(.*)"
				local i = 1
				while self.Config.Events.Server[i] do
					local EventDay, EventTime, EventAction = self.Config.Events.Server[i]:match("([^:]+):([^:]+):([^:]+)")
					if EventDay and EventTime and EventAction then
						if string.lower(EventDay) == "all" or string.lower(EventDay) == self:GetDay() then
							if tonumber(EventTime) == tonumber(ServerTime) then
								self:RunCommand(EventAction)
							end
						end
					end
					i = i + 1
				end
			end
		end
		if self.Config.Settings.Repeat == "true" and self.Config.Events.Repeat[1] then
			if CurDay ~= time.GetCurrentTime():ToLocalTime():ToString("d") then
				CurDay = time.GetCurrentTime():ToLocalTime():ToString("d")
				self:SetRepeat()
			end
		end
	end
end

function PLUGIN:SetRepeat()
	if self.Config.Settings.Repeat == "true" and self.Config.Events.Repeat[1] then
		local i = 1
		while self.Config.Events.Repeat[i] do
			if RepeatTimer[i] or RepeatTimer[i] ~= nil then
				RepeatTimer[i]:Destroy()
				RepeatTimer[i] = nil
			end
			local RepeatDay, RepeatTime, RepeatCommand = self.Config.Events.Repeat[i]:match("([^:]+):([^:]+):([^:]+)")
			if RepeatDay and RepeatTime and RepeatCommand then
				if string.lower(RepeatDay) == "all" or string.lower(RepeatDay) == self:GetDay() then
					RepeatTimer[i] = timer.Repeat(tonumber(RepeatTime), 0, function() self:RunCommand(RepeatCommand) end, self.Plugin)
				end
			end
			i = i + 1
		end
		local i = 1
		while RepeatTimer[i] do
			if not self.Config.Events.Repeat[i] then
				RepeatTimer[i]:Destroy()
				RepeatTimer[i] = nil
			end
			i = i + 1
		end
		else
		local i = 1
		while RepeatTimer[i] do
			RepeatTimer[i]:Destroy()
			RepeatTimer[i] = nil
			i = i + 1
		end
	end
end

function PLUGIN:TableMessage(args)
	local argsTbl = {}
	local length = args.Length
	for i = 0, length - 1, 1 do
		argsTbl[i + 1] = args[i]
	end
	return argsTbl
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

function PLUGIN:cmdTEvent(player, cmd, args)
	local playerSteamID = rust.UserIDFromPlayer(player)
	if not permission.UserHasPermission(playerSteamID, "timedevents.admin") and not permission.UserHasPermission(playerSteamID, "timedevents.modify") and
		not permission.UserHasPermission(playerSteamID, "timedevents.run") and not permission.UserHasPermission(playerSteamID, "timedevents.group") then
		self:RustMessage(player, self:Lang(player, "NoPermission"))
		return
	end
	if args.Length == 0 then
		self:RustMessage(player, self:Lang(player, "Menu"))
		return
		elseif args.Length > 0 then
		local func = args[0]
		if func ~= "toggle" and func ~= "limits" and func ~= "time" and func ~= "list" and func ~= "add" and func ~= "delete" and func ~= "clear" and func ~= "run" and func ~= "group" then
			self:RustMessage(player, self:Lang(player, "WrongArgs"))
			return
		end
		if func == "toggle" then
			if not permission.UserHasPermission(playerSteamID, "timedevents.admin") then
				self:RustMessage(player, self:Lang(player, "NoPermission"))
				return
			end
			if args.Length < 2 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "system" and sfunc ~= "local" and sfunc ~= "server" and sfunc ~= "repeat" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local message
			if sfunc == "system" then
				if self.Config.Settings.Enabled == "true" then
					self.Config.Settings.Enabled = "false"
					message = FormatMessage(self:Lang(player, "ChangedStatus"), { status = self:Lang(player, "Disabled") })
					else
					self.Config.Settings.Enabled = "true"
					message = FormatMessage(self:Lang(player, "ChangedStatus"), { status = self:Lang(player, "Enabled") })
				end
				self:SaveConfig()
				self:RustMessage(player, message)
				return
			end
			if sfunc == "local" then
				if self.Config.Settings.LocalTime == "true" then
					self.Config.Settings.LocalTime = "false"
					message = FormatMessage(self:Lang(player, "TimeChangedStatus"), { group = "local", status = self:Lang(player, "Disabled") })
					else
					self.Config.Settings.LocalTime = "true"
					message = FormatMessage(self:Lang(player, "TimeChangedStatus"), { group = "local", status = self:Lang(player, "Enabled") })
				end
				self:SaveConfig()
				self:RustMessage(player, message)
				return
			end
			if sfunc == "server" then
				if self.Config.Settings.ServerTime == "true" then
					self.Config.Settings.ServerTime = "false"
					message = FormatMessage(self:Lang(player, "TimeChangedStatus"), { group = "server", status = self:Lang(player, "Disabled") })
					else
					self.Config.Settings.ServerTime = "true"
					message = FormatMessage(self:Lang(player, "TimeChangedStatus"), { group = "server", status = self:Lang(player, "Enabled") })
				end
				self:SaveConfig()
				self:RustMessage(player, message)
				return
			end
			if sfunc == "repeat" then
				if self.Config.Settings.Repeat == "true" then
					self.Config.Settings.Repeat = "false"
					message = FormatMessage(self:Lang(player, "TimeChangedStatus"), { group = "repeat", status = self:Lang(player, "Disabled") })
					else
					self.Config.Settings.Repeat = "true"
					message = FormatMessage(self:Lang(player, "TimeChangedStatus"), { group = "repeat", status = self:Lang(player, "Enabled") })
				end
				self:SaveConfig()
				self:RustMessage(player, message)
				return
			end
		end
		if func == "limits" then
			local message = FormatMessage(self:Lang(player, "Limits"), { l1 = self.Config.Settings.Enabled, l2 = self.Config.Settings.LocalTime, l3 = self.Config.Settings.ServerTime, l4 = self.Config.Settings.Repeat, l5 = self.Config.Settings.TimeFormat })
			self:RustMessage(player, message)
			return
		end
		if func == "time" then
			local LTime = time.GetCurrentTime():ToLocalTime():ToString(self.Config.Settings.TimeFormat)
			local STime = global.TOD_Sky.get_Instance()
			STime = tostring(STime.Cycle.Hour):match"([^.]*).(.*)"
			local message = FormatMessage(self:Lang(player, "ServerTime"), { ltime = LTime, stime = STime })
			self:RustMessage(player, message)
			return
		end
		if func == "list" then
			if args.Length < 2 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "l" and sfunc ~= "s" and sfunc ~= "r" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local group, GroupList = "", ""
			if sfunc == "l" then
				group = self:Lang(player, "Local"):gsub("^%l", string.upper)
				GroupList = self.Config.Events.Local
			end
			if sfunc == "s" then
				group = self:Lang(player, "Server"):gsub("^%l", string.upper)
				GroupList = self.Config.Events.Server
			end
			if sfunc == "r" then
				group = self:Lang(player, "Repeat"):gsub("^%l", string.upper)
				GroupList = self.Config.Events.Repeat
			end
			if not GroupList[1] then
				local message = FormatMessage(self:Lang(player, "NoEvents"), { group = string.lower(Group) })
				self:RustMessage(player, message)
				return
			end
			local i, List = 1, ""
			while GroupList[i] do
				local EventDay, EventTime, EventAction = GroupList[i]:match("([^:]+):([^:]+):([^:]+)")
				List = List..FormatMessage(self:Lang(player, "ListFormat"), { num = i, eday = EventDay, etime = EventTime, eaction = EventAction })
				i = i + 1
			end
			local message = FormatMessage(self:Lang(player, "ListMessage"), { group = group, list = string.sub(List, 1, -2) })
			if i < 6 then
				self:RustMessage(player, message)
				else
				self:RustMessage(player, self:Lang(player, "PrintToConsole"))
				self:RustConsole(player, message)
			end
			return
		end
		if func == "add" then
			if not permission.UserHasPermission(playerSteamID, "timedevents.admin") and not permission.UserHasPermission(playerSteamID, "timedevents.modify") then
				self:RustMessage(player, self:Lang(player, "NoPermission"))
				return
			end
			if args.Length < 3 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "l" and sfunc ~= "s" and sfunc ~= "r" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local NewEvent = args[2]
			local EventArgs = ""
			if args.Length >= 4 then
				local args = self:TableMessage(args)
				local i = 4
				while args[i] do
					EventArgs = EventArgs..args[i].." "
					i = i + 1
				end
				EventArgs = string.sub(EventArgs, 1, -2)
			end
			local message
			if sfunc == "l" then
				if self:ErrorCheck(player, NewEvent, "l") then return end
				local EventDay, EventTime, EventAction = NewEvent:match("([^:]+):([^:]+):([^:]+)")
				table.insert(self.Config.Events.Local, string.lower(EventDay)..":"..string.lower(EventTime)..":"..EventAction.." "..EventArgs)
				local count = 0
				for current, data in pairs(self.Config.Events.Local) do
					count = count + 1
				end
				message = FormatMessage(self:Lang(player, "EventAdded"), { id = count, eday = EventDay, etime = EventTime, group = self:Lang(player, "Local"), event = EventAction.." "..EventArgs })
			end
			if sfunc == "s" then
				if self:ErrorCheck(player, NewEvent, "s") then return end
				local EventDay, EventTime, EventAction = NewEvent:match("([^:]+):([^:]+):([^:]+)")
				table.insert(self.Config.Events.Server, string.lower(EventDay)..":"..string.lower(EventTime)..":"..EventAction.." "..EventArgs)
				local count = 0
				for current, data in pairs(self.Config.Events.Server) do
					count = count + 1
				end
				message = FormatMessage(self:Lang(player, "EventAdded"), { id = count, eday = EventDay, etime = EventTime, group = self:Lang(player, "Server"), event = EventAction.." "..EventArgs })
			end
			if sfunc == "r" then
				if self:ErrorCheck(player, NewEvent, "r") then return end
				local EventDay, EventTime, EventAction = NewEvent:match("([^:]+):([^:]+):([^:]+)")
				table.insert(self.Config.Events.Repeat, string.lower(EventDay)..":"..string.lower(EventTime)..":"..EventAction.." "..EventArgs)
				local count = 0
				for current, data in pairs(self.Config.Events.Repeat) do
					count = count + 1
				end
				message = FormatMessage(self:Lang(player, "EventAdded"), { id = count, eday = EventDay, etime = EventTime, group = self:Lang(player, "Repeat"), event = EventAction.." "..EventArgs })
			end
			self:SaveConfig()
			if sfunc == "r" then self:SetRepeat() end
			self:RustMessage(player, message)
			return
		end
		if func == "delete" then
			if not permission.UserHasPermission(playerSteamID, "timedevents.admin") and not permission.UserHasPermission(playerSteamID, "timedevents.modify") then
				self:RustMessage(player, self:Lang(player, "NoPermission"))
				return
			end
			if args.Length < 3 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "l" and sfunc ~= "s" and sfunc ~= "r" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			if not tonumber(args[2]) then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local DelEvent = tonumber(args[2])
			local message
			if sfunc == "l" then
				if not self.Config.Events.Local[DelEvent] then
					message = FormatMessage(self:Lang(player, "InvalidID"), { id = DelEvent, group = self:Lang(player, "Local") })
					self:RustMessage(player, message)
					return
				end
				local DelDay, DelTime, DelTask = self.Config.Events.Local[DelEvent]:match("([^:]+):([^:]+):([^:]+)")
				table.remove(self.Config.Events.Local, DelEvent)
				message = FormatMessage(self:Lang(player, "EventDeleted"), { id = DelEvent, eday = DelDay, etime = DelTime, group = self:Lang(player, "Local"), event = DelTask })
			end
			if sfunc == "s" then
				if not self.Config.Events.Server[DelEvent] then
					message = FormatMessage(self:Lang(player, "InvalidID"), { id = DelEvent, group = self:Lang(player, "Server") })
					self:RustMessage(player, message)
					return
				end
				local DelDay, DelTime, DelTask = self.Config.Events.Server[DelEvent]:match("([^:]+):([^:]+):([^:]+)")
				table.remove(self.Config.Events.Server, DelEvent)
				message = FormatMessage(self:Lang(player, "EventDeleted"), { id = DelEvent, eday = DelDay, etime = DelTime, group = self:Lang(player, "Server"), event = DelTask })
			end
			if sfunc == "r" then
				if not self.Config.Events.Repeat[DelEvent] then
					message = FormatMessage(self:Lang(player, "InvalidID"), { id = DelEvent, group = self:Lang(player, "Repeat") })
					self:RustMessage(player, message)
					return
				end
				local DelDay, DelTime, DelTask = self.Config.Events.Repeat[DelEvent]:match("([^:]+):([^:]+):([^:]+)")
				table.remove(self.Config.Events.Repeat, DelEvent)
				message = FormatMessage(self:Lang(player, "EventDeleted"), { id = DelEvent, eday = DelDay, etime = DelTime, group = self:Lang(player, "Repeat"), event = DelTask })
			end
			self:SaveConfig()
			if sfunc == "r" then self:SetRepeat() end
			self:RustMessage(player, message)
			return
		end
		if func == "clear" then
			if not permission.UserHasPermission(playerSteamID, "timedevents.admin") and not permission.UserHasPermission(playerSteamID, "timedevents.modify") then
				self:RustMessage(player, self:Lang(player, "NoPermission"))
				return
			end
			if args.Length < 2 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "l" and sfunc ~= "s" and sfunc ~= "r" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local message
			if sfunc == "l" then
				self.Config.Events.Local = {}
				message = FormatMessage(self:Lang(player, "EventsCleared"), { group = self:Lang(player, "Local") })
			end
			if sfunc == "s" then
				self.Config.Events.Server = {}
				message = FormatMessage(self:Lang(player, "EventsCleared"), { group = self:Lang(player, "Server") })
			end
			if sfunc == "r" then
				self.Config.Events.Repeat = {}
				message = FormatMessage(self:Lang(player, "EventsCleared"), { group = self:Lang(player, "Repeat") })
			end
			self:SaveConfig()
			if sfunc == "r" then self:SetRepeat() end
			self:RustMessage(player, message)
			return
		end
		if func == "run" then
			if not permission.UserHasPermission(playerSteamID, "timedevents.admin") and not permission.UserHasPermission(playerSteamID, "timedevents.modify") and
				not permission.UserHasPermission(playerSteamID, "timedevents.run") then
				self:RustMessage(player, self:Lang(player, "NoPermission"))
				return
			end
			if args.Length < 3 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "l" and sfunc ~= "s" and sfunc ~= "r" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			if not tonumber(args[2]) then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local RunEvent = tonumber(args[2])
			local message
			if sfunc == "l" then
				if not self.Config.Events.Local[RunEvent] then
					local message = FormatMessage(self:Lang(player, "InvalidID"), { id = RunEvent, group = self:Lang(player, "Local") })
					self:RustMessage(player, message)
					return
				end
				local EventDay, EventTime, EventAction = self.Config.Events.Local[RunEvent]:match("([^:]+):([^:]+):([^:]+)")
				if not EventDay or not EventTime or not EventAction then
					self:RustMessage(player, self:Lang(player, "InvalidEvent"))
					return
				end
				self:RunCommand(EventAction)
				message = FormatMessage(self:Lang(player, "EventRun"), { id = RunEvent, eday = EventDay, etime = EventTime, group = self:Lang(player, "Local"), event = EventAction })
			end
			if sfunc == "s" then
				if not self.Config.Events.Server[RunEvent] then
					local message = FormatMessage(self:Lang(player, "InvalidID"), { id = RunEvent, group = self:Lang(player, "Server") })
					self:RustMessage(player, message)
					return
				end
				local EventDay, EventTime, EventAction = self.Config.Events.Server[RunEvent]:match("([^:]+):([^:]+):([^:]+)")
				if not EventDay or not EventTime or not EventAction then
					self:RustMessage(player, self:Lang(player, "InvalidEvent"))
					return
				end
				self:RunCommand(EventAction)
				message = FormatMessage(self:Lang(player, "EventRun"), { id = RunEvent, eday = EventDay, etime = EventTime, group = self:Lang(player, "Server"), event = EventAction })
			end
			if sfunc == "r" then
				if not self.Config.Events.Repeat[RunEvent] then
					local message = FormatMessage(self:Lang(player, "InvalidID"), { id = RunEvent, group = self:Lang(player, "Repeat") })
					self:RustMessage(player, message)
					return
				end
				local EventDay, EventTime, EventAction = self.Config.Events.Repeat[RunEvent]:match("([^:]+):([^:]+):([^:]+)")
				if not EventDay or not EventTime or not EventAction then
					self:RustMessage(player, self:Lang(player, "InvalidEvent"))
					return
				end
				self:RunCommand(EventAction)
				message = FormatMessage(self:Lang(player, "EventRun"), { id = RunEvent, eday = EventDay, etime = EventTime, group = self:Lang(player, "Repeat"), event = EventAction })
			end
			self:RustMessage(player, message)
			return
		end
		if func == "group" then
			if not permission.UserHasPermission(playerSteamID, "timedevents.admin") and not permission.UserHasPermission(playerSteamID, "timedevents.group") then
				self:RustMessage(player, self:Lang(player, "NoPermission"))
				return
			end
			if args.Length < 2 then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local sfunc = args[1]
			if sfunc ~= "start" and sfunc ~= "stop" and sfunc ~= "list" then
				self:RustMessage(player, self:Lang(player, "WrongArgs"))
				return
			end
			local group
			if sfunc ~= "list" then
				if args.Length < 3 then
					self:RustMessage(player, self:Lang(player, "WrongArgs"))
					return
					else
					group = args[2]
				end
			end
			if sfunc == "start" then
				if not self.Config.Groups[group] then
					local message = FormatMessage(self:Lang(player, "NoGroup"), { group = group })
					self:RustMessage(player, message)
					return
				end
				local id, idc, info = 1, 0, false
				while self.Config.Groups[group][id] do
					local idt = self.Config.Groups[group][id]:match("([^:]+)")
					if idt ~= "info" then
						if tonumber(idt) > tonumber(idc) then idc = idt end
						else
						if not info then info = true end
					end
					id = id + 1
				end
				if tonumber(idc) == 0 or info == false then
					local message = FormatMessage(self:Lang(player, "GroupInvalid"), { group = group })
					self:RustMessage(player, message)
					return
				end
				groupcnt[group] = 0
				if grouptmr[group] then
					grouptmr[group]:Destroy()
					grouptmr[group] = nil
					local message = FormatMessage(self:Lang(player, "GroupRestart"), { group = group })
					self:RustMessage(player, message)
					else
					local message = FormatMessage(self:Lang(player, "GroupStart"), { group = group })
					self:RustMessage(player, message)
				end
				grouptmr[group] = timer.Repeat(1, 0, function()
					local i = 1
					while self.Config.Groups[group][i] do
						local EventTime, EventAction = self.Config.Groups[group][i]:match("([^:]+):([^:]+)")
						if EventTime ~= "info" then
							if tonumber(EventTime) == tonumber(groupcnt[group]) then
								self:RunCommand(EventAction)
							end
						end
						i = i + 1
					end
					if tonumber(groupcnt[group]) >= tonumber(idc) then
						grouptmr[group]:Destroy()
						grouptmr[group] = nil
						local message = FormatMessage(self:Lang(player, "GroupComplete"), { group = group })
						self:RustMessage(player, message)
						return
					end
					groupcnt[group] = groupcnt[group] + 1
				end, self.Plugin)
			end
			if sfunc == "stop" then
				if not self.Config.Groups[group] then
					local message = FormatMessage(self:Lang(player, "NoGroup"), { group = group })
					self:RustMessage(player, message)
					return
				end
				if grouptmr[group] then
					grouptmr[group]:Destroy()
					grouptmr[group] = nil
					local message = FormatMessage(self:Lang(player, "GroupStop"), { group = group })
					self:RustMessage(player, message)
					return
				end
				local message = FormatMessage(self:Lang(player, "GroupNotActive"), { group = group })
				self:RustMessage(player, message)
			end
			if sfunc == "list" then
				local i, List = 1, ""
				for current, data in pairs(self.Config.Groups) do
					local id = 1
					while data[id] do
						if data[id]:match("([^:]+)") == "info" then
							local idt, idn, idd = data[id]:match("([^:]+):([^:]+):([^:]+)")
							local active = self:Lang(player, "Inactive")
							if grouptmr[idn] then active = self:Lang(player, "Active") end
							List = List..FormatMessage(self:Lang(player, "ListGroupFormat"), { num = i, status = active, name = idn, desc = idd, count = (tonumber(#data) - 1) })
							i = i + 1
						end
						id = id + 1
					end
				end
				local message = FormatMessage(self:Lang(player, "ListMessage"), { group = self:Lang(player, "GroupName"), list = string.sub(List, 1, -2) })
				if i < 6 then
					self:RustMessage(player, message)
					else
					self:RustMessage(player, self:Lang(player, "PrintToConsole"))
					self:RustConsole(player, message)
				end
			end
		end
		return
	end
end

function PLUGIN:ErrorCheck(player, NewEvent, Group)
	local eGroup
	if Group == "l" then eGroup = self:Lang(player, "Local") end
	if Group == "s" then eGroup = self:Lang(player, "Server") end
	if Group == "r" then eGroup = self:Lang(player, "Repeat") end
	local _, count = string.gsub(NewEvent, ":", "")
	if count ~= 2 then
		local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionColon") })
		self:RustMessage(player, message)
		return true
	end
	local NewDay, NewTime, NewTask = NewEvent:match("([^:]+):([^:]+):([^:]+)")
	if not NewDay or not NewTime or not NewTask then
		local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionEmpty") })
		self:RustMessage(player, message)
		return true
	end
	NewDay = string.lower(NewDay)
	if NewDay ~= "all" and NewDay ~= "sun" and NewDay ~= "mon" and NewDay ~= "tue" and NewDay ~= "wed" and NewDay ~= "thu" and NewDay ~= "fri" and NewDay ~= "sat" then
		local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionDay") })
		self:RustMessage(player, message)
		return true
	end
	if not string.match(NewTime, "%d+") then
		local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionTime") })
		self:RustMessage(player, message)
		return true
	end
	if not NewTask then
		local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionEvent") })
		self:RustMessage(player, message)
		return true
	end
	if Group == "s" then
		if tonumber(NewTime) < 0 or tonumber(NewTime) > 24 then
			local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionTime") })
			self:RustMessage(player, message)
			return true
		end
	end
	if Group == "r" then
		if tonumber(NewTime) < 0 or tonumber(NewTime) > 86400 then
			local message = FormatMessage(self:Lang(player, "InvalidFormat"), { event = NewEvent, group = eGroup, section = self:Lang(player, "SectionTime") })
			self:RustMessage(player, message)
			return true
		end
	end
	return false
end

function PLUGIN:RunCommand(EventAction)
	if ServerInitialized then
		local EventAction = EventAction:gsub("'", "\"")
		local prefix = self:Lang(player, "Prefix"):gsub(" ", "")
	local message = prefix.." "..EventAction
	if self.Config.Settings.SayEnabled == "true" then
		if string.sub(EventAction, 1, 4) == "say " then
			self:RustBroadcast(self:Lang(player, "SayPrefix").." "..string.sub(EventAction, 5))
			message = prefix.." "..string.sub(EventAction, 5)
			else
			rust.RunServerCommand(EventAction)
		end
		else
		rust.RunServerCommand(EventAction)
	end
	message = message:gsub("<color=%p*%w*>", "")
	message = message:gsub("</color>", "")
	print(message)
	end
end

function PLUGIN:GetDay()
	local Date = time.GetCurrentTime():ToLocalTime():ToString("d")
	local Month, Day, Year = Date:match("(%d+)/(%d+)/(%d+)")
	local Days = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" }
	local i = Month
	if Month == 1 then
		i = 13
		Year = Year - 1
	end
	if Month == 2 then
		i = 14
		Year = Year - 1
	end
	local ii = Day + (i * 2) + math.floor(((i + 1) * 3) / 5) + Year + math.floor(Year / 4) - math.floor(Year / 100) + math.floor(Year / 400) + 2
	local iii = math.floor(ii / 7)
	local iiii = ii - (iii * 7) 
	if iiii == 0 then iiii = 7 end
	return Days[iiii]
end

function PLUGIN:cmdUpdate(arg)
	local NewList = {}
	for current, data in pairs(self.Config.Events.Local) do
		table.insert(NewList, "all:"..data)
	end
	self.Config.Events.Local = NewList
	local NewList = {}
	for current, data in pairs(self.Config.Events.Server) do
		table.insert(NewList, "all:"..data)
	end
	self.Config.Events.Server = NewList
	local NewList = {}
	for current, data in pairs(self.Config.Events.Repeat) do
		table.insert(NewList, "all:"..data)
	end
	self.Config.Events.Repeat = NewList
	self:SaveConfig()
	local prefix = self:Lang(player, "Prefix"):gsub(" ", "")
	local message = prefix.." Configuration successfully updated."
	message = message:gsub("<color=%p*%w*>", "")
	message = message:gsub("</color>", "")
	print(message)
end

function PLUGIN:RustMessage(player, message)
	rust.SendChatMessage(player, "<size="..tonumber(self.Config.Settings.MessageSize)..">"..self:Lang(player, "Prefix")..message.."</size>")
end

function PLUGIN:RustConsole(player, message)
	player:SendConsoleCommand("echo ".."<size="..tonumber(self.Config.Settings.MessageSize)..">"..self:Lang(player, "Prefix")..message.."</size>")
end

function PLUGIN:RustBroadcast(message)
	rust.BroadcastChat("<size="..tonumber(self.Config.Settings.MessageSize)..">"..self:Lang(nil, "Prefix")..message.."</size>")
end

function PLUGIN:SendHelpText(player)
	local playerSteamID = rust.UserIDFromPlayer(player)
	if permission.UserHasPermission(playerSteamID, "timedevents.admin") or permission.UserHasPermission(playerSteamID, "timedevents.modify") or
		permission.UserHasPermission(playerSteamID, "timedevents.run") or permission.UserHasPermission(playerSteamID, "timedevents.group") then
		self:RustMessage(player, self:Lang(player, "Help"))
	end
end