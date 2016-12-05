PLUGIN.Title        = "Npctp"
PLUGIN.Description  = "Allows npc to tp player."
PLUGIN.Author       = "razor"
PLUGIN.Version      = V(1,1,10)
PLUGIN.ResourceId   = 2229

local NPCPlugin = "HumanNPC"
local npc
local ProximityPlayer = {}

function PLUGIN:Init()
	permission.RegisterPermission("Npctp.admin", self.Plugin)
	permission.RegisterPermission("Npctp.use", self.Plugin)
	permission.RegisterPermission("Npctp.use2", self.Plugin)
	permission.RegisterPermission("Npctp.use3", self.Plugin)
	permission.RegisterPermission("Npctp.use4", self.Plugin)
	permission.RegisterPermission("Npctp.use5", self.Plugin)
        command.AddChatCommand( "npctp",       self.Plugin, "cmdNPCTP" )

	self:LoadDefaultConfig()
	self:LoadDefaultLang()
        self:LoadSavedData()
        self.NPCData = {}
        
end


function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = self.Config.Settings or {}
	self.Config.NPC = self.Config.NPC or {}
	self.Config.NPC2 = self.Config.NPC2 or {}
	self.Config.NPC3 = self.Config.NPC3 or {}
	self.Config.NPC4 = self.Config.NPC4 or {}
	self.Config.NPC5 = self.Config.NPC5 or {}
	self.Config.Messages = self.Config.Messages or {}

        self.Config.Messages.NPCTPLimitReached      = self.Config.Messages.NPCTPLimitReached or "No More limit Reached"
        self.Config.Messages.NPCTPCooldownReached   = self.Config.Messages.NPCTPCooldownReached or "Cooldown you must wate "
        self.Config.Messages.YouLackPerms           = self.Config.Messages.YouLackPerms or "You do not have the correct permissions"

	self.Config.NPC.Enabled         = self.Config.NPC.Enabled or "true"
        self.Config.NPC.UsePermissions  = self.Config.NPC.UsePermissions or "true"
	self.Config.NPC.Cooldown        = self.Config.NPC.Cooldown or 0
	self.Config.NPC.MustInteract    = self.Config.NPC.MustInteract or "true"
	self.Config.NPC.PlayerNPCTPName = self.Config.NPC.PlayerNPCTPName or "Land_TP"
        self.Config.NPC.DailyLimit      = self.Config.NPC.DailyLimit or 0
        self.Config.NPC.UseSpawnsDB     = self.Config.NPC.UseSpawnsDB or "false"
        self.Config.NPC.SpawnFileName   = self.Config.NPC.SpawnFileName or ""
        self.Config.NPC.x               = self.Config.NPC.x or "100"
        self.Config.NPC.y               = self.Config.NPC.y or "58"
        self.Config.NPC.z               = self.Config.NPC.z or "100"

	self.Config.NPC2.Cooldown        = self.Config.NPC2.Cooldown or 0
	self.Config.NPC2.MustInteract    = self.Config.NPC2.MustInteract or "true"
	self.Config.NPC2.Enabled         = self.Config.NPC2.Enabled or "false"
        self.Config.NPC2.UsePermissions  = self.Config.NPC2.UsePermissions or "true"
        self.Config.NPC2.PlayerNPCTPName = self.Config.NPC2.PlayerNPCTPName or "Land_TP2"
        self.Config.NPC2.DailyLimit      = self.Config.NPC2.DailyLimit or 0
        self.Config.NPC2.UseSpawnsDB     = self.Config.NPC2.UseSpawnsDB or "false"
        self.Config.NPC2.SpawnFileName   = self.Config.NPC2.SpawnFileName or ""
        self.Config.NPC2.x               = self.Config.NPC2.x or "100"
        self.Config.NPC2.y               = self.Config.NPC2.y or "58"
        self.Config.NPC2.z               = self.Config.NPC2.z or "100"

	self.Config.NPC3.Cooldown        = self.Config.NPC3.Cooldown or 0
	self.Config.NPC3.MustInteract    = self.Config.NPC3.MustInteract or "true"
	self.Config.NPC3.Enabled         = self.Config.NPC3.Enabled or "false"
        self.Config.NPC3.UsePermissions  = self.Config.NPC3.UsePermissions or "true"
        self.Config.NPC3.PlayerNPCTPName = self.Config.NPC3.PlayerNPCTPName or "Land_TP3"
        self.Config.NPC3.DailyLimit      = self.Config.NPC3.DailyLimit or 0
        self.Config.NPC3.UseSpawnsDB     = self.Config.NPC3.UseSpawnsDB or "false"
        self.Config.NPC3.SpawnFileName   = self.Config.NPC3.SpawnFileName or ""
        self.Config.NPC3.x               = self.Config.NPC3.x or "100"
        self.Config.NPC3.y               = self.Config.NPC3.y or "58"
        self.Config.NPC3.z               = self.Config.NPC3.z or "100"

	self.Config.NPC4.Cooldown        = self.Config.NPC4.Cooldown or 0
	self.Config.NPC4.MustInteract    = self.Config.NPC4.MustInteract or "true"
	self.Config.NPC4.Enabled         = self.Config.NPC4.Enabled or "false"
        self.Config.NPC4.UsePermissions  = self.Config.NPC4.UsePermissions or "true"
        self.Config.NPC4.PlayerNPCTPName = self.Config.NPC4.PlayerNPCTPName or "Land_TP4"
        self.Config.NPC4.DailyLimit      = self.Config.NPC4.DailyLimit or 0
        self.Config.NPC4.UseSpawnsDB     = self.Config.NPC4.UseSpawnsDB or "false"
        self.Config.NPC4.SpawnFileName   = self.Config.NPC4.SpawnFileName or ""
        self.Config.NPC4.x               = self.Config.NPC4.x or "100"
        self.Config.NPC4.y               = self.Config.NPC4.y or "58"
        self.Config.NPC4.z               = self.Config.NPC4.z or "100"

	self.Config.NPC5.Cooldown        = self.Config.NPC5.Cooldown or 86400
	self.Config.NPC5.MustInteract    = self.Config.NPC5.MustInteract or "true"
	self.Config.NPC5.Enabled         = self.Config.NPC5.Enabled or "false"
        self.Config.NPC5.UsePermissions  = self.Config.NPC5.UsePermissions or "true"
        self.Config.NPC5.PlayerNPCTPName = self.Config.NPC5.PlayerNPCTPName or "Blacksmith"
        self.Config.NPC5.DailyLimit      = self.Config.NPC5.DailyLimit or 0
        self.Config.NPC5.ServerCommand   = self.Config.NPC5.ServerCommand or "inv.giveplayer"
        self.Config.NPC5.CommandOnPlayer = self.Config.NPC5.CommandOnPlayer or "true"
        self.Config.NPC5.Option          = self.Config.NPC5.Option or "lmg.m249"

	self:SaveConfig()
	if self.Config.CustomPermissions then
		for current, data in pairs(self.Config.CustomPermissions) do
			permission.RegisterPermission(data.Permission, self.Plugin)
		end


end
end
			

-- ----------------------------------------------------------------------------
-- PLUGIN:LoadSavedData()
-- ----------------------------------------------------------------------------
-- Load the DataTable file into a table or create a new table when the file
-- doesn't exist yet.
-- ----------------------------------------------------------------------------
function PLUGIN:LoadSavedData()
    -- Open the datafile if it exists, otherwise we'll create a new one.
    SaveData           = datafile.GetDataTable( "Npctp" )
    SaveData           = SaveData or {}
    SaveData.NPCData   = SaveData.NPCData or {}


end

-- ----------------------------------------------------------------------------
-- PLUGIN:SaveData()
-- ----------------------------------------------------------------------------
-- Saves the table with all the NPCdata to a DataTable file.
-- ----------------------------------------------------------------------------
function PLUGIN:SaveData()  
    -- Save the DataTable
    datafile.SaveDataTable( "Npctp" )
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

function comma(num)
	while true do  
		num, x = string.gsub(num, "^(-?%d+)(%d%d%d)", '%1,%2')
		if x == 0 then break end
	end
	return num
end



function PLUGIN:LoadDefaultLang()
	lang.RegisterMessages(util.TableToLangDict({
		["BuildingBlocked"] = "You cannot access me in building blocked areas.",
		["CheckGround"] = "You may only access me while standing on the ground.",
		["CheckRadius"] = "You cannot access me within <color=#cd422b>{range} meters</color> of another online player. Current nearest range is <color=#cd422b>{current} meters</color>.",
		["CoolDown"] = "You must wait <color=#cd422b>{cooldown} seconds</color> before trying that."
	}), self.Plugin)
end









    function PLUGIN:OnServerInitialized()
        spawns_plugin = plugins.Find("Spawns")
	npc = plugins.Find(NPCPlugin) or false  

        
    if(not spawns_plugin) then
   	 print("You must have the Spawns Database @ http://forum.rustoxide.com/plugins/spawns-database.720/")
   	 return false
   	end

         	if(self.Config.NPC.SpawnFileName  ~= "") then	
			self:LoadSpawnFile(self.Config.NPC.SpawnFileName)

end
         	if(self.Config.NPC2.SpawnFileName  ~= "") then	
                     self:LoadSpawnFiletwo(self.Config.NPC2.SpawnFileName)
end
         	if(self.Config.NPC3.SpawnFileName  ~= "") then	
                     self:LoadSpawnFilethree(self.Config.NPC3.SpawnFileName)
end
         	if(self.Config.NPC4.SpawnFileName  ~= "") then	
                     self:LoadSpawnFilefour(self.Config.NPC4.SpawnFileName)
end    
end  
    

    function PLUGIN:OnEnterNPC(npc, player)
	local npc = tostring(npc):match("([^%[]*)%[([^%]]*)")
	if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "true"
	end
	if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "true"
	end
	if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "true"
	end
	if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "true"
	end

end

function PLUGIN:OnLeaveNPC(npc, player)
	local npc = tostring(npc):match("([^%[]*)%[([^%]]*)")
	if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "false"
              end

	if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "false"
	end
	if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "false"
	end
	if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() then
		local playerSteamID = rust.UserIDFromPlayer(player)
		ProximityPlayer[playerSteamID] = "false"
	end
end








-- ----------------------------------------------------------------------------
-- PLUGIN:cmdNPCTP( player, cmd, string )                        Admin Command
-- ----------------------------------------------------------------------------
-- In-game '/npctp' command for server admins...
-- ----------------------------------------------------------------------------
function PLUGIN: cmdNPCTP( player, cmd, args )
        local command = cmd
	local playerSteamID = rust.UserIDFromPlayer(player)

	if args.Length > 0 and args[0] == "reload" then
		if not permission.UserHasPermission(playerSteamID, "npctp.admin") then
		rust.SendChatMessage(player, "Sorry you do not have Permission's.")
				return
			end


		rust.SendChatMessage(player, "Reloading Npctp plugin.")
                            rust.RunServerCommand("oxide.reload Npctp")


end


	if args.Length == 0 then
			rust.SendChatMessage(player, "<color=#ffd479>/npctp reload</color> - Reload this plugin")
		end
end




-- *******************************************
-- Teleport Player FUNCTIONS
-- *******************************************
function PLUGIN:TeleportPlayerToXYZ(player)
  if(not newVector3) then newVector3 = new( UnityEngine.Vector3._type , nil ) end
  local playerSteamID = rust.UserIDFromPlayer(player)

  local spawnPoint, err = spawns_plugin:CallHook("GetRandomSpawn", self.NPCData.SpawnsFile, self.NPCData.SpawnCount )
  if(not spawnPoint) then rust.BroadcastChat(self.Config.ChatName,err) end
  newVector3.x = spawnPoint.x
  newVector3.y = spawnPoint.y
  newVector3.z = spawnPoint.z
 -- self:TeleportPlayer(player, newVector3)
rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. newVector3.x .. " " .. newVector3.y .. " " .. newVector3.z .. "")
end

function PLUGIN:TeleportPlayerTotwoXYZ(player)
  if(not newVector3) then newVector3 = new( UnityEngine.Vector3._type , nil ) end
  local playerSteamID = rust.UserIDFromPlayer(player)

  local spawnPointtwo, err = spawns_plugin:CallHook("GetRandomSpawn", self.NPCData.SpawnsFile2, self.NPCData.SpawnCount2 )
  if(not spawnPoint) then rust.BroadcastChat(self.Config.ChatName,err) end
  newVector3.x = spawnPointtwo.x
  newVector3.y = spawnPointtwo.y
  newVector3.z = spawnPointtwo.z
 -- self:TeleportPlayer(player, newVector3)
rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. newVector3.x .. " " .. newVector3.y .. " " .. newVector3.z .. "")
end

function PLUGIN:TeleportPlayerTothreeXYZ(player)
  if(not newVector3) then newVector3 = new( UnityEngine.Vector3._type , nil ) end
  local playerSteamID = rust.UserIDFromPlayer(player)

  local spawnPointthree, err = spawns_plugin:CallHook("GetRandomSpawn", self.NPCData.SpawnsFile3, self.NPCData.SpawnCount3 )
  if(not spawnPoint) then rust.BroadcastChat(self.Config.ChatName,err) end
  newVector3.x = spawnPointthree.x
  newVector3.y = spawnPointthree.y
  newVector3.z = spawnPointthree.z
 -- self:TeleportPlayer(player, newVector3)
rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. newVector3.x .. " " .. newVector3.y .. " " .. newVector3.z .. "")
end

function PLUGIN:TeleportPlayerTofourXYZ(player)
  if(not newVector3) then newVector3 = new( UnityEngine.Vector3._type , nil ) end
  local playerSteamID = rust.UserIDFromPlayer(player)

  local spawnPointfour, err = spawns_plugin:CallHook("GetRandomSpawn", self.NPCData.SpawnsFile4, self.NPCData.SpawnCount4 )
  if(not spawnPoint) then rust.BroadcastChat(self.Config.ChatName,err) end
  newVector3.x = spawnPointfour.x
  newVector3.y = spawnPointforur.y
  newVector3.z = spawnPointfour.z
 -- self:TeleportPlayer(player, newVector3)
rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. newVector3.x .. " " .. newVector3.y .. " " .. newVector3.z .. "")
end


-- ----------------------------------------------------------------------------
-- PLUGIN:ParseRemainingTime( time )
-- ----------------------------------------------------------------------------
-- Returns an amount of seconds as a nice time string.
-- ----------------------------------------------------------------------------
function PLUGIN:ParseRemainingTime( time )
    local minutes  = nil
    local seconds  = nil
    local timeLeft = nil

    -- If the amount of seconds is higher than 60 we'll have minutes too, so
    -- start with grabbing the amount of minutes and then take the remainder as
    -- the seconds that are left on the timer.
    if time >= 60 then
        minutes = math.floor( time / 60 )
        seconds = time - ( minutes * 60 )
    else
        seconds = time
    end

    -- Build a nice string with the remaining time.
    if minutes and seconds > 0 then
        timeLeft = minutes .. " min " .. seconds .. " sec "
    elseif minutes and seconds == 0 then
        timeLeft = minutes .. " min "
    else    
        timeLeft = seconds .. " sec "
    end

    -- Return the time string.
    return timeLeft        
end

-- *******************************************
-- MAIN FUNCTIONS
-- *******************************************

function PLUGIN:LoadSpawnFile(filename)
  local spawnsCount, err = spawns_plugin:CallHook("GetSpawnsCount",filename)
  if (not spawnsCount) then
    return false, err
  end

  self.NPCData.SpawnsFile = filename
  self.NPCData.SpawnCount = spawncount
  return true
end



function PLUGIN:LoadSpawnFiletwo(filename)
  local spawnsCount, err = spawns_plugin:CallHook("GetSpawnsCount",filename)
  if (not spawnsCount) then
    return false, err
  end

  self.NPCData.SpawnsFile2 = filename
  self.NPCData.SpawnCount2 = spawncount
  return true
end

function PLUGIN:LoadSpawnFilethree(filename)
  local spawnsCount, err = spawns_plugin:CallHook("GetSpawnsCount",filename)
  if (not spawnsCount) then
    return false, err
  end

  self.NPCData.SpawnsFile3 = filename
  self.NPCData.SpawnCount3 = spawncount
  return true
end

function PLUGIN:LoadSpawnFilefour(filename)
  local spawnsCount, err = spawns_plugin:CallHook("GetSpawnsCount",filename)
  if (not spawnsCount) then
    return false, err
  end

  self.NPCData.SpawnsFile4 = filename
  self.NPCData.SpawnCount4 = spawncount
  return true
end

-- *******************************************
-- MAIN FUNCTIONS END
-- *******************************************


function PLUGIN:OnUseNPC(npc, player)
local npc = tostring(npc):match("([^%[]*)%[([^%]]*)")
	if npc and player and npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() or npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() or npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() or npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() or npc:lower() == self.Config.NPC5.PlayerNPCTPName:lower() then
                local playerSteamID = rust.UserIDFromPlayer(player)
                local timestamp   = time.GetUnixTimestamp()
                local currentDate = tostring( time.GetCurrentTime():ToString("d") )
                local DailyLimit  = self.Config.NPC.DailyLimit
                local DailyLimit2 = self.Config.NPC2.DailyLimit
                local DailyLimit3 = self.Config.NPC3.DailyLimit
                local DailyLimit4 = self.Config.NPC4.DailyLimit
                local DailyLimit5 = self.Config.NPC5.DailyLimit
                local Cooldown    = self.Config.NPC.Cooldown or 0
                local Cooldown2   = self.Config.NPC2.Cooldown or 0
                local Cooldown3   = self.Config.NPC3.Cooldown or 0
                local Cooldown4   = self.Config.NPC4.Cooldown or 0
                local Cooldown5   = self.Config.NPC5.Cooldown or 0


                -- Grab the user his/her Save data.
                SaveData.NPCData[playerSteamID] = SaveData.NPCData[playerSteamID] or {}
                SaveData.NPCData[playerSteamID].Saves = SaveData.NPCData[playerSteamID].Saves or {}
                SaveData.NPCData[playerSteamID].Saves.amount = SaveData.NPCData[playerSteamID].Saves.amount or 0
                SaveData.NPCData[playerSteamID].Saves.date = currentDate
                SaveData.NPCData[playerSteamID].Saves.timestamp = SaveData.NPCData[playerSteamID].Saves.timestamp or 0
             
                SaveData.NPCData[playerSteamID].Saves2 = SaveData.NPCData[playerSteamID].Saves2 or {}
                SaveData.NPCData[playerSteamID].Saves2.amount = SaveData.NPCData[playerSteamID].Saves2.amount or 0
                SaveData.NPCData[playerSteamID].Saves2.date = currentDate
                SaveData.NPCData[playerSteamID].Saves2.timestamp = SaveData.NPCData[playerSteamID].Saves2.timestamp or 0

                SaveData.NPCData[playerSteamID].Saves3 = SaveData.NPCData[playerSteamID].Saves3 or {}
                SaveData.NPCData[playerSteamID].Saves3.amount = SaveData.NPCData[playerSteamID].Saves3.amount or 0
                SaveData.NPCData[playerSteamID].Saves3.date = currentDate
                SaveData.NPCData[playerSteamID].Saves3.timestamp = SaveData.NPCData[playerSteamID].Saves3.timestamp or 0

                SaveData.NPCData[playerSteamID].Saves4 = SaveData.NPCData[playerSteamID].Saves4 or {}
                SaveData.NPCData[playerSteamID].Saves4.amount = SaveData.NPCData[playerSteamID].Saves4.amount or 0
                SaveData.NPCData[playerSteamID].Saves4.date = currentDate
                SaveData.NPCData[playerSteamID].Saves4.timestamp = SaveData.NPCData[playerSteamID].Saves4.timestamp or 0

                SaveData.NPCData[playerSteamID].Saves5 = SaveData.NPCData[playerSteamID].Saves5 or {}
                SaveData.NPCData[playerSteamID].Saves5.amount = SaveData.NPCData[playerSteamID].Saves5.amount or 0
                SaveData.NPCData[playerSteamID].Saves5.date = currentDate
                SaveData.NPCData[playerSteamID].Saves5.timestamp = SaveData.NPCData[playerSteamID].Saves5.timestamp or 0







     -- Check if there is saved Save data available for the
     -- player and reset data.
 
             if Cooldown == 0 and SaveData.NPCData[playerSteamID].Saves.timestamp then
                    if SaveData.NPCData[playerSteamID].Saves.timestamp + 86400 <= timestamp then
                        SaveData.NPCData[playerSteamID].Saves.amount = 0
                    end
                    end

              if Cooldown2 == 0 and SaveData.NPCData[playerSteamID].Saves2.timestamp then
                    if SaveData.NPCData[playerSteamID].Saves2.timestamp + 86400 <= timestamp then
                        SaveData.NPCData[playerSteamID].Saves2.amount = 0
                    end
                    end

              if Cooldown3 == 0 and SaveData.NPCData[playerSteamID].Saves3.timestamp then
                    if SaveData.NPCData[playerSteamID].Saves3.timestamp + 86400 <= timestamp then
                        SaveData.NPCData[playerSteamID].Saves3.amount = 0
                    end
                    end

              if Cooldown4 == 0 and SaveData.NPCData[playerSteamID].Saves4.timestamp then
                    if SaveData.NPCData[playerSteamID].Saves4.timestamp + 86400 <= timestamp then
                        SaveData.NPCData[playerSteamID].Saves4.amount = 0
         end
         end

              if Cooldown5 == 0 and SaveData.NPCData[playerSteamID].Saves5.timestamp then
                    if SaveData.NPCData[playerSteamID].Saves5.timestamp + 86400 <= timestamp then
                        SaveData.NPCData[playerSteamID].Saves5.amount = 0
         end
         end

               
-- CoolDown reset Player Cooldown


                if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() and self.Config.NPC.Cooldown > 0 and ( timestamp - SaveData.NPCData[playerSteamID].Saves.timestamp ) < self.Config.NPC.Cooldown then
                    local remainingTime = self:ParseRemainingTime( self.Config.NPC.Cooldown - ( timestamp - SaveData.NPCData[playerSteamID].Saves.timestamp ) )
          	rust.SendChatMessage(player, self.Config.Messages.NPCTPCooldownReached, "[ " .. remainingTime .. "]" )

                  return
                end
				

                if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() and self.Config.NPC2.Cooldown > 0 and ( timestamp - SaveData.NPCData[playerSteamID].Saves2.timestamp ) < self.Config.NPC2.Cooldown then
                    local remainingTime2 = self:ParseRemainingTime( self.Config.NPC2.Cooldown - ( timestamp - SaveData.NPCData[playerSteamID].Saves2.timestamp ) )
          	rust.SendChatMessage(player, self.Config.Messages.NPCTPCooldownReached, "[ " .. remainingTime2 .. "]" )

                  return
                end

                if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() and self.Config.NPC3.Cooldown > 0 and ( timestamp - SaveData.NPCData[playerSteamID].Saves3.timestamp ) < self.Config.NPC3.Cooldown then
                    local remainingTime3 = self:ParseRemainingTime( self.Config.NPC3.Cooldown - ( timestamp - SaveData.NPCData[playerSteamID].Saves3.timestamp ) )
          	rust.SendChatMessage(player, self.Config.Messages.NPCTPCooldownReached, "[ " .. remainingTime3 .. "]" )

                  return
                end

                if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() and self.Config.NPC4.Cooldown > 0 and ( timestamp - SaveData.NPCData[playerSteamID].Saves4.timestamp ) < self.Config.NPC4.Cooldown then
                    local remainingTime4 = self:ParseRemainingTime( self.Config.NPC4.Cooldown - ( timestamp - SaveData.NPCData[playerSteamID].Saves4.timestamp ) )
          	rust.SendChatMessage(player, self.Config.Messages.NPCTPCooldownReached, "[ " .. remainingTime4 .. "]" )

                  return
                end

                if npc:lower() == self.Config.NPC5.PlayerNPCTPName:lower() and self.Config.NPC5.Cooldown > 0 and ( timestamp - SaveData.NPCData[playerSteamID].Saves5.timestamp ) < self.Config.NPC5.Cooldown then
                    local remainingTime5 = self:ParseRemainingTime( self.Config.NPC5.Cooldown - ( timestamp - SaveData.NPCData[playerSteamID].Saves5.timestamp ) )
          	rust.SendChatMessage(player, self.Config.Messages.NPCTPCooldownReached, "[ " .. remainingTime5 .. "]" )

                  return
                end
            
-- Permissions can or cant use

                if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() and self.Config.NPC.UsePermissions == "true" and not permission.UserHasPermission(playerSteamID, "npctp.use") then
          	rust.SendChatMessage(player, self.Config.Messages.YouLackPerms)

                  return
                end
              

                if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() and self.Config.NPC2.UsePermissions == "true" and not permission.UserHasPermission(playerSteamID, "npctp.use2") then
          	rust.SendChatMessage(player, self.Config.Messages.YouLackPerms)

                  return
                end

                if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() and self.Config.NPC3.UsePermissions == "true" and not permission.UserHasPermission(playerSteamID, "npctp.use3") then
          	rust.SendChatMessage(player, self.Config.Messages.YouLackPerms)

                  return
                end

                if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() and self.Config.NPC4.UsePermissions == "true" and not permission.UserHasPermission(playerSteamID, "npctp.use4") then
          	rust.SendChatMessage(player, self.Config.Messages.YouLackPerms)

                  return
                end

                if npc:lower() == self.Config.NPC5.PlayerNPCTPName:lower() and self.Config.NPC5.UsePermissions == "true" and not permission.UserHasPermission(playerSteamID, "npctp.use5") then
          	rust.SendChatMessage(player, self.Config.Messages.YouLackPerms)

                  return
                end		



               if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() and DailyLimit > 0 and SaveData.NPCData[playerSteamID].Saves.amount >= DailyLimit and currentDate >= SaveData.NPCData[playerSteamID].Saves.date then
                    -- The player has reached the limit, show a message to the
                    -- player.
                    player:ChatMessage(self.Config.Messages.NPCTPLimitReached)

                    return
                end

              if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() and DailyLimit2 > 0 and SaveData.NPCData[playerSteamID].Saves2.amount >= DailyLimit2 and currentDate >= SaveData.NPCData[playerSteamID].Saves2.date then
                    -- The player has reached the limit, show a message to the
                    -- player.
                    player:ChatMessage(self.Config.Messages.NPCTPLimitReached)

                    return
                end

              if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() and DailyLimit3 > 0 and SaveData.NPCData[playerSteamID].Saves3.amount >= DailyLimit2 and currentDate >= SaveData.NPCData[playerSteamID].Saves3.date then
                    -- The player has reached the limit, show a message to the
                    -- player.
                    player:ChatMessage(self.Config.Messages.NPCTPLimitReached)

                    return
                end

              if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() and DailyLimit4 > 0 and SaveData.NPCData[playerSteamID].Saves4.amount >= DailyLimit4 and currentDate >= SaveData.NPCData[playerSteamID].Saves4.date then
                    -- The player has reached the limit, show a message to the
                    -- player.
                    player:ChatMessage(self.Config.Messages.NPCTPLimitReached)

                    return
                end

              if npc:lower() == self.Config.NPC5.PlayerNPCTPName:lower() and DailyLimit5 > 0 and SaveData.NPCData[playerSteamID].Saves5.amount >= DailyLimit5 and currentDate >= SaveData.NPCData[playerSteamID].Saves5.date then
                    -- The player has reached the limit, show a message to the
                    -- player.
                    player:ChatMessage(self.Config.Messages.NPCTPLimitReached)

                    return
                end







		if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() and self.Config.NPC.UseSpawnsDB ~= "false" then
			if self.Config.NPC.Enabled ~= "true" then return end

                           self:TeleportPlayerToXYZ(player)
                            SaveData.NPCData[playerSteamID].Saves.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves.amount = SaveData.NPCData[playerSteamID].Saves.amount + 1
                            self:SaveData()
			return
		end


		if npc:lower() == self.Config.NPC.PlayerNPCTPName:lower() and self.Config.NPC.UseSpawnsDB ~= "true" then

        	if self.Config.NPC.Enabled ~= "true" then return end

                            rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. self.Config.NPC.x .. " " .. self.Config.NPC.y .. " " .. self.Config.NPC.z .. "")
                            SaveData.NPCData[playerSteamID].Saves.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves.amount = SaveData.NPCData[playerSteamID].Saves.amount + 1
                            self:SaveData()
			return
		end
 

		if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() and self.Config.NPC2.UseSpawnsDB ~= "false" then
			if self.Config.NPC2.Enabled ~= "true" then return end

                           self:TeleportPlayerTotwoXYZ(player)
                            SaveData.NPCData[playerSteamID].Saves2.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves2.amount = SaveData.NPCData[playerSteamID].Saves2.amount + 1
                            self:SaveData()
			return
		end


		if npc:lower() == self.Config.NPC2.PlayerNPCTPName:lower() and self.Config.NPC2.UseSpawnsDB ~= "true" then

        	if self.Config.NPC2.Enabled ~= "true" then return end

                            rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. self.Config.NPC2.x .. " " .. self.Config.NPC2.y .. " " .. self.Config.NPC2.z .. "")
                            SaveData.NPCData[playerSteamID].Saves2.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves2.amount = SaveData.NPCData[playerSteamID].Saves2.amount + 1
                            self:SaveData()
			return
		end


		if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() and self.Config.NPC4.UseSpawnsDB ~= "false" then
			if self.Config.NPC4.Enabled ~= "true" then return end

                           self:TeleportPlayerTofourXYZ(player)
                            SaveData.NPCData[playerSteamID].Saves4.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves4.amount = SaveData.NPCData[playerSteamID].Saves4.amount + 1
                            self:SaveData()
			return
		end


		if npc:lower() == self.Config.NPC4.PlayerNPCTPName:lower() and self.Config.NPC4.UseSpawnsDB ~= "true" then

        	if self.Config.NPC4.Enabled ~= "true" then return end

                            rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. self.Config.NPC4.x .. " " .. self.Config.NPC4.y .. " " .. self.Config.NPC4.z .. "")
                            SaveData.NPCData[playerSteamID].Saves4.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves4.amount = SaveData.NPCData[playerSteamID].Saves4.amount + 1
                            self:SaveData()
			return
		end



		if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() and self.Config.NPC3.UseSpawnsDB ~= "false" then
			if self.Config.NPC3.Enabled ~= "true" then return end

                           self:TeleportPlayerTothreeXYZ(player)
                            SaveData.NPCData[playerSteamID].Saves3.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves3.amount = SaveData.NPCData[playerSteamID].Saves3.amount + 1
                            self:SaveData()
			return
		end


		if npc:lower() == self.Config.NPC3.PlayerNPCTPName:lower() and self.Config.NPC3.UseSpawnsDB ~= "true" then

        	if self.Config.NPC3.Enabled ~= "true" then return end

                            rust.RunServerCommand("teleport.topos " .. playerSteamID .. " " .. self.Config.NPC3.x .. " " .. self.Config.NPC3.y .. " " .. self.Config.NPC3.z .. "")
                            SaveData.NPCData[playerSteamID].Saves3.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves3.amount = SaveData.NPCData[playerSteamID].Saves3.amount + 1
                            self:SaveData()
			return
		end


		    if npc:lower() == self.Config.NPC5.PlayerNPCTPName:lower() and self.Config.NPC5.CommandOnPlayer ~= "true" then
			if self.Config.NPC5.Enabled ~= "true" then return end
		        	    rust.RunServerCommand("" .. self.Config.NPC5.ServerCommand .. " " .. self.Config.NPC5.Option .. " ")
                            SaveData.NPCData[playerSteamID].Saves5.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves5.amount = SaveData.NPCData[playerSteamID].Saves5.amount + 1
                            self:SaveData()
			        return
                end

	   
                    if npc:lower() == self.Config.NPC5.PlayerNPCTPName:lower() and self.Config.NPC5.CommandOnPlayer ~= "false" then
			if self.Config.NPC5.Enabled ~= "true" then return end
		        	    rust.RunServerCommand("" .. self.Config.NPC5.ServerCommand .. " " .. playerSteamID .. " " .. self.Config.NPC5.Option .. " ")
                            SaveData.NPCData[playerSteamID].Saves5.timestamp = timestamp
                            SaveData.NPCData[playerSteamID].Saves5.amount = SaveData.NPCData[playerSteamID].Saves5.amount + 1
                            self:SaveData()
			        return
		        end                  
            end
  end

