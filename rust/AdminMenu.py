try:
    import BasePlayer
    import Rust
    import Oxide.Game.Rust.Cui as Cui
    from System.Collections.Generic import List, Dictionary
    from System import Action, Array, Int32, String, Guid
    import UnityEngine.TextAnchor as TextAnchor
    import time, ItemManager
except ImportError, e:
    print 'IMPORT ERROR',e

class AdminMenu:
    def __init__(self):
        self.Title = "AdminMenu"
        self.Author = "FlamingMojo"
        self.Version = V(3,0,2)
        self.PlayerMenus = dict()
        self.displayedUIs = dict()
        self.HasConfig = True
        self.DefaultConfig = {
            'Version' : '3.0.0',
            'Broadcast' : False,
            'PlayersPerPage':10,
            'Reasons':{
                'Kick' : ("Spamming","Racism","Cheating","Disrespect"),
                'Ban' : ("Spamming","Racism","Cheating","Disrespect")
                },
            'GiveMenu':{
                    'Weapon' : (1),
                    'Ammunition' : (1,10,100),
                    'Medical' : (1,10,100),
                    'Food' : (1,10,100),
                    'Traps' : (1,10),
                    'Tool' : (1),
                    'Construction' : (1,10,100),
                    'Resources':(100,1000,10000),
                    'Items': (1,10,100),
                    'Component' : (1,10,100),
                    'Misc' : (1),
                    'Attire':(1)},      
            'ToolMenu':{
                'AdminMenu':{
                    'Dawn' :{
                        'pos'       : 1,
                        'cmd'       : 'env.time 6',
                        'bcolour'   : 'black_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'},
                    'Noon' :{
                        'pos'       : 2,
                        'cmd'       : 'env.time 12',
                        'bcolour'   : 'black_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'},
                    'Dusk' :{
                        'pos'       : 3,
                        'cmd'       : 'env.time 18',
                        'bcolour'   : 'black_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'},
                    'Midnight' :{
                        'pos'       : 4,
                        'cmd'       : 'env.time 0',
                        'bcolour'   : 'black_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'}},
                'HeliControl': {
                    'KillHelis' : {
                        'pos'       : 1,
                        'cmd'       : 'killheli',
                        'bcolour'   : 'black_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'helicontrol.killheli'}},
                'Vanish' : {
                        'Vanish' : {
                        'pos'       : 1,
                        'cmd'       : 'vanish',
                        'bcolour'   : 'green_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'vanish.allowed'}},
                'WeatherController':{
                        'Mild' : {
                            'pos'       : 1,
                            'cmd'       : 'adminmenu.handle weather mild',
                            'bcolour'   : 'white_tint',
                            'tcolour'   : 'black_text',
                            'perm'      : 'weathercontroller.canuseweather'},
                        'Average' : {
                            'pos'       : 2,
                            'cmd'       : 'adminmenu.handle weather average',
                            'bcolour'   : 'yellow_tint',
                            'tcolour'   : 'black_text',
                            'perm'      : 'weathercontroller.canuseweather'},
                        'Heavy' : {
                            'pos'       : 3,
                            'cmd'       : 'adminmenu.handle weather heavy',
                            'bcolour'   : 'orange_tint',
                            'tcolour'   : 'black_text',
                            'perm'      : 'weathercontroller.canuseweather'},
                        'Max' : {
                            'pos'       : 4,
                            'cmd'       : 'adminmenu.handle weather max',
                            'bcolour'   : 'red_tint',
                            'tcolour'   : 'white_text',
                            'perm'      : 'weathercontroller.canuseweather'},
                        'Auto' : {
                            'pos'       : 5,
                            'cmd'       : 'adminmenu.handle weather auto',
                            'bcolour'   : 'black_tint',
                            'tcolour'   : 'white_text',
                            'perm'      : 'weathercontroller.canuseweather'}}
                },
            'ActionMenu':{
                'AdminMenu':{ 
                    'GoTo':{
                        'pos'       : 1,
                        'cmd'       : 'adminmenu.handle goto {PLAYERID}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'green_text',
                        'perm'      : 'adminmenu.allow.goto'},
                    'Bring':{
                        'pos'       : 2,
                        'cmd'       : 'adminmenu.handle bring {PLAYERID}',
                        'bcolour'   : 'green_tint',
                        'tcolour'   : 'blue_text',
                        'perm'      : 'adminmenu.allow.goto'},
                    'Kill':{
                        'pos'       : 3,
                        'cmd'       : 'adminmenu.handle kill {PLAYERID}',
                        'bcolour'   : 'black_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'adminmenu.allow.kill'},
                    'Kick':{
                        'pos'       : 4,
                        'cmd'       : 'adminmenu.open confirm Kick {PLAYERID}',
                        'bcolour'   : 'yellow_tint',
                        'tcolour'   : 'red_text',
                        'perm'      : 'adminmenu.allow.kick'},
                    'Ban':{
                        'pos'       : 5,
                        'cmd'       : 'adminmenu.open confirm Ban {PLAYERID}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'adminmenu.allow.ban'},
                    'Perms':{
                        'pos'       : 6,
                        'cmd'       : 'adminmenu.open perms {PLAYERID}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'}
                    },
                'BetterChatMute':{
                    'Unmute':{
                        'pos'       : 1,
                        'cmd'       : 'unmute {PLAYERID}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'betterchatmute.use'},
                    'Mute5m':{
                        'pos'       : 2,
                        'cmd'       : 'mute {PLAYERID} 5m',
                        'bcolour'   : 'green_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'betterchatmute.use'},
                    'Mute15m':{
                        'pos'       : 3,
                        'cmd'       : 'mute {PLAYERID} 15m',
                        'bcolour'   : 'green_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'betterchatmute.use'},
                    'PermaMute':{
                        'pos'       : 4,
                        'cmd'       : 'mute {PLAYERID}',
                        'bcolour'   : 'green_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'betterchatmute.use'}
                    },
                'Godmode' :{
                    'God':{
                        'pos'       : 1,
                        'cmd'       : 'adminmenu.handle god {PLAYERID}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'godmode.allowed'},
                    'UnGod':{
                        'pos'       : 2,
                        'cmd'       : 'adminmenu.handle ungod {PLAYERID}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'godmode.allowed'}
                    },
                'ServerRewards' : {
                    'SrCheck':{
                        'pos'       : 1,
                        'cmd'       : 'adminmenu.handle srcheck {PLAYERID}',
                        'bcolour'   : 'yellow_tint',
                        'tcolour'   : 'blue_text',
                        'perm'      : 'IsAdmin'},
                    'SrClear':{
                        'pos'       : 2,
                        'cmd'       : 'adminmenu.handle srclear {PLAYERID}',
                        'bcolour'   : 'yellow_tint',
                        'tcolour'   : 'red_text',
                        'perm'      : 'IsAdmin'}
                    },
                'FancyDrop' : {
                    'DropTo':{
                        'pos'       : 1,
                        'cmd'       : 'ad.toplayer {PLAYERNAME}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'IsAdmin'},
                    'DropDirect':{
                        'pos'       : 2,
                        'cmd'       : 'ad.dropplayer {PLAYERNAME}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'red_text',
                        'perm'      : 'IsAdmin'}
                    },
                'Jail':{
                    'Jail 5m':{
                        'pos'       : 1,
                        'cmd'       : 'jail.send {PLAYERID} 300',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'jail.admin'},
                    'Jail 15m':{
                        'pos'       : 2,
                        'cmd'       : 'jail.send {PLAYERID} 900',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'jail.admin'},
                    'Jail 30m':{
                        'pos'       : 3,
                        'cmd'       : 'jail.send {PLAYERID} 1800',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'jail.admin'},
                    'Jail 1h':{
                        'pos'       : 4,
                        'cmd'       : 'jail.send {PLAYERID} 3600',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'jail.admin'},
                    'JailPerm':{
                        'pos'       : 5,
                        'cmd'       : 'jail.send {PLAYERID}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'jail.admin'},
                    'UnJail':{
                        'pos'       : 6,
                        'cmd'       : 'jail.free {PLAYERID}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'yellow_text',
                        'perm'      : 'jail.admin'}
                    },
                'Spectate':{
                    'Spectate':{
                        'pos'       : 1,
                        'cmd'       : 'spectate {PLAYERNAME}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'spectate.use'}
                    },
                'Airstrike':{
                    'Airstrike':{
                        'pos'       : 1,
                        'cmd'       : 'airstrike {PLAYERXYZ}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'airstrike.admin'},
                    'Squadstrike':{
                        'pos'       : 2,
                        'cmd'       : 'squadstrike {PLAYERXYZ}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'airstrike.mass'}
                    },
                'RainOfFire':{
                    'RainFire':{
                        'pos'       : 1,
                        'cmd'       : 'rof.onposition {PLAYERXZ}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'}
                    },
                'HeliControl':{
                    'CallHeliTo':{
                        'pos'       : 1,
                        'cmd'       : 'callheli {PLAYERNAME}',
                        'bcolour'   : 'red_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'helicontrol.callheli'}
                    },
                'Economics':{
                    'Balance':{
                        'pos'       : 1,
                        'cmd'       : 'adminmenu.handle economy balance {PLAYERID}',
                        'bcolour'   : 'blue_tint',
                        'tcolour'   : 'white_text',
                        'perm'      : 'IsAdmin'}
                    }
                }
            }
        self.Messages = {
            'menuAlreadyOpen'   : '[AdminMenu]<color="#ff0000ff">Your AdminMenu is already open</color>',
            'noPermissionToView': '[AdminMenu]<color="#ff0000ff">Sorry you have no permission to view the AdminMenu</color>',
            'kickPlayer'        : '[AdminMenu]<color="#00ff00ff">{ADMINNAME}</color> kicked <color="#ff0000ff">{PLAYERNAME}</color> for {REASON}',
            'banPlayer'         : '[AdminMenu]<color="#00ff00ff">{ADMINNAME}</color> banned <color="#ff0000ff">{PLAYERNAME}</color> for {REASON}',
            'gotoPlayer'        : '[AdminMenu]<color="#00ff00ff">{ADMINNAME}</color> teleported to <color="#ff0000ff">{PLAYERNAME}</color>',
            'bringPlayer'       : '[AdminMenu]<color="#00ff00ff">{ADMINNAME}</color> teleported <color="#ff0000ff">{PLAYERNAME}</color> to them',
            'killPlayer'        : '[AdminMenu]<color="#00ff00ff">{ADMINNAME}</color> killed <color="#ff0000ff">{PLAYERNAME}</color>',
            'godPlayer'         : '[AdminMenu]<color="#00ff00ff">You</color> godded <color="#ff0000ff">{PLAYERNAME}</color>',
            'ungodPlayer'       : '[AdminMenu]<color="#00ff00ff">You</color> ungodded <color="#ff0000ff">{PLAYERNAME}</color>',
            'srClear'           : '[AdminMenu]<color="#00ff00ff">You</color> removed all reward points from <color="#ff0000ff">{PLAYERNAME}</color>',
            'srCheck'           : '[AdminMenu]<color="#ff0000ff">{PLAYERNAME}</color> has {POINTS} reward points',
            'Balance'           : '[AdminMenu]<color="#ff0000ff">{PLAYERNAME}</color> has {BALANCE} coins'}
    def LoadDefaultConfig(self):
        self.Config.clear()
        self.Config = self.DefaultConfig
        self.SaveConfig()
        
    def OnServerInitialized(self):
        chatcommandlist = {
            'adminmenu':'openMenuFromChat',
            'adm' : 'openMenuFromChat'}
        commandlist = {
            'adminmenu.open' : 'openMenu',
            'adminmenu.close' : 'closePlayerMenu',
            'adminmenu.handle' : 'handleCommand'}
        permissions = [
            'adminmenu.allow.view',
            'adminmenu.allow.ban',
            'adminmenu.allow.kick',
            'adminmenu.allow.goto',
            'adminmenu.allow.bring',
            'adminmenu.allow.kill',
            'adminmenu.allow.all',
            'adminmenu.allow.groups']
        for perm in permissions:
            if not permission.PermissionExists(perm):
                permission.RegisterPermission(perm, self.Plugin)
        for comm in chatcommandlist:
            command.AddChatCommand(comm, self.Plugin, chatcommandlist[comm])
        for comm in commandlist:
            command.AddConsoleCommand(comm, self.Plugin, commandlist[comm])
        Messages = Dictionary[str,str]()
        for msgkey in self.Messages.keys():
            Messages.Add(msgkey,self.Messages[msgkey])
        lang.RegisterMessages(Messages,self.Plugin)
        if 'SETTINGS' in self.Config.keys():
            self.LoadDefaultConfig()
        self.itemCategories = list()
        self.GiveData = dict()
        for item in ItemManager.itemList:
            if str(item.category) not in self.itemCategories:
                self.itemCategories.append(str(item.category))
            self.GiveData[item.shortname] = dict()
            self.GiveData[item.shortname]['Name'] = item.name.replace('.item','').title()
            self.GiveData[item.shortname]['Category'] = str(item.category)
        if plugins.Exists('Kits'):
            KITS = plugins.Find('Kits')
            self.itemCategories.append('Kits')
            for k in KITS.Call('GetAllKits'):
                self.GiveData['kit '+k] = dict()
                self.GiveData['kit '+k]['Name'] = k
                self.GiveData['kit '+k]['Category'] = 'Kits'
            
        
    def handleCommand(self, args):
        admin = args.Connection.player
        arguments = list(args.Args)
        if arguments[0] == 'kick':
            if permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.kick') or permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.all') or admin.IsAdmin:
                rust.RunServerCommand('kick %s %s' % (arguments[1],arguments[2]))
                msg = lang.GetMessage('kickPlayer',self.Plugin,admin.UserIDString)
                msg = msg.replace('{ADMINNAME}',admin.displayName)
                msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                msg = msg.replace('{REASON}',arguments[2])
                rust.BroadcastChat(msg, None, '0')
        elif arguments[0] == 'ban':
            if permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.ban') or permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.all') or admin.IsAdmin:
                rust.RunServerCommand('ban %s %s' % (arguments[1],arguments[2]))
                msg = lang.GetMessage('banPlayer',self.Plugin,admin.UserIDString)
                msg = msg.replace('{ADMINNAME}',admin.displayName)
                msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                msg = msg.replace('{REASON}',arguments[2])
                rust.BroadcastChat(msg, None, '0')
        elif arguments[0] == 'goto':
            if permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.goto') or permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.all') or admin.IsAdmin:
                rust.RunServerCommand('teleport %s %s' % (admin.UserIDString,arguments[1]))
                if self.Config['Broadcast']:
                    msg = lang.GetMessage('gotoPlayer',self.Plugin,admin.UserIDString)
                    msg = msg.replace('{ADMINNAME}',admin.displayName)
                    msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                    rust.BroadcastChat(msg, None, '0')
        elif arguments[0] == 'bring':
            if permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.bring') or permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.all') or admin.IsAdmin:
                rust.RunServerCommand('teleport %s %s' % (arguments[1],admin.UserIDString))
                if self.Config['Broadcast']:
                    msg = lang.GetMessage('bringPlayer',self.Plugin,admin.UserIDString)
                    msg = msg.replace('{ADMINNAME}',admin.displayName)
                    msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                    rust.BroadcastChat(msg, None, '0')
        elif arguments[0] == 'kill':
            if permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.bring') or permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.all') or admin.IsAdmin:
                targetPlayer = self.getPlayerFromID(arguments[1])
                targetPlayer.Die()
                if self.Config['Broadcast']:
                    msg = lang.GetMessage('killPlayer',self.Plugin,admin.UserIDString)
                    msg = msg.replace('{ADMINNAME}',admin.displayName)
                    msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                    rust.BroadcastChat(msg, None, '0')
        
        elif arguments[0] == 'weather' and plugins.Exists('WeatherController'):
            if permission.UserHasPermission(admin.UserIDString, 'weathercontroller.canuseweather') or admin.IsAdmin:
                WC = plugins.Find('WeatherController')
                if arguments[1] == 'mild':
                    WC.mild(admin,1)
                elif arguments[1] == 'average':
                    WC.average(admin,1)
                elif arguments[1] == 'heavy':
                    WC.heavy(admin,1)
                elif arguments[1] == 'max':
                    WC.max(admin,1)
                elif arguments[1] == 'auto':
                    WC.weather(admin,'auto',-1)
                else:
                    print '[AdminMenu] Invalid weather: %s' % arguments[1]
        elif arguments[0] == 'srcheck' and plugins.Exists('ServerRewards'):
            if admin.IsAdmin:
                SR = plugins.Find('ServerRewards')
                worked = SR.Call("CheckPoints",arguments[1])
                if not worked or type(worked) != float:
                    worked = 0
                msg = lang.GetMessage('srCheck',self.Plugin,admin.UserIDString)
                msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                msg = msg.replace('{POINTS}','%d'%worked)
                rust.SendChatMessage(admin, msg, None, '0')
        elif arguments[0] == 'srclear' and plugins.Exists('ServerRewards'):
            if admin.IsAdmin:
                SR = plugins.Find('ServerRewards')
                worked = SR.Call("RemovePlayer",arguments[1])
                msg = lang.GetMessage('srClear',self.Plugin,admin.UserIDString)
                msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                rust.SendChatMessage(admin, msg, None, '0')
        elif arguments[0] == 'economy' and plugins.Exists('Economics'):
            if admin.IsAdmin:
                ECO = plugins.Find('Economics')
                if arguments[1] == 'balance':
                    balance = ECO.Call('GetPlayerMoney',arguments[2])
                    if not balance or type(balance) != float:
                        balance = 0
                    msg = lang.GetMessage('Balance',self.Plugin,admin.UserIDString)
                    msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[2]).displayName)
                    msg = msg.replace('{BALANCE}','%d' %balance)
                    rust.SendChatMessage(admin, msg, None, '0')
        elif arguments[0] == 'god' and plugins.Exists('Godmode'):
            if permission.UserHasPermission(admin.UserIDString, 'godmode.allowed') or admin.IsAdmin:
                GodMode = plugins.Find('Godmode')
                worked = GodMode.Call("EnableGodmode",self.getPlayerFromID(arguments[1]))
                msg = lang.GetMessage('godPlayer',self.Plugin,admin.UserIDString)
                msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                rust.SendChatMessage(admin, msg, None, '0')
        elif arguments[0] == 'ungod' and plugins.Exists('Godmode'):
            if permission.UserHasPermission(admin.UserIDString, 'godmode.allowed') or admin.IsAdmin:
                GodMode = plugins.Find('Godmode')
                worked = GodMode.Call("DisableGodmode",self.getPlayerFromID(arguments[1]))
                msg = lang.GetMessage('ungodPlayer',self.Plugin,admin.UserIDString)
                msg = msg.replace('{PLAYERNAME}',self.getPlayerFromID(arguments[1]).displayName)
                rust.SendChatMessage(admin, msg, None, '0')
        else:
            print '[AdminMenu] Cant handle command: %s' % str(arguments)
            
                
                    
            

            
                
        
    def openMenuFromChat(self, player, cmd, args):
        if permission.UserHasPermission(player.UserIDString, 'adminmenu.allow.view') or permission.UserHasPermission(player.UserIDString, 'adminmenu.allow.all') or player.IsAdmin:
            self.openMenu(args, player)
            
            
    def openMenu(self, args, admin = None):
        if admin:
            #From chat
            arguments = args
        else:
            #From console
            admin = args.Connection.player
            arguments = args.Args
        if not (permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.view') or permission.UserHasPermission(admin.UserIDString, 'adminmenu.allow.all') or admin.IsAdmin):
            rust.SendChatMessage(admin,lang.GetMessage('noPermissionToView',self.Plugin,admin.UserIDString) , None, '0')
            return
        if admin.UserIDString not in self.PlayerMenus:
            self.PlayerMenus[admin.UserIDString] = dict()
        if 'ToolbarMenu' not in self.PlayerMenus[admin.UserIDString]:
            ToolBar = ToolbarMenu(
                [0.25, 0.75, 0.875, 0.9],
                self.Config,
                admin)
            self.PlayerMenus[admin.UserIDString]['ToolbarMenu'] = ToolBar
        if len(arguments) == 1:
            if arguments[0] == "toolmenu":
                Tools = ToolMenu([0.25, 0.25, 0.75, 0.7], self.Config, admin)
                self.PlayerMenus[admin.UserIDString]['ToolMenu'] = Tools
                self.showMenuElement(admin, ['ToolMenu','ToolbarMenu'])
            elif arguments[0] == "groups":
                permissionList, maxPermPages = self.getPermissionsList()
                GrpMenu = GroupsMenu(
                    [0.25, 0.25, 0.75, 0.7],
                     permissionList, 0, maxPermPages)
                self.PlayerMenus[admin.UserIDString]['GroupMenu'] = GrpMenu
                self.showMenuElement(admin, ['GroupMenu','ToolbarMenu'])#open groups menu
            elif arguments[0] == "True":
                self.showMenuElement(admin, ['ToolbarMenu'])
            else:
                print "[AdminMenu]Invalid Argument count from %s" % admin.displayName
            return
        elif len(arguments) == 2:
            if arguments[0] == "playermenu":
                playerList, maxPlayerPages = self.getPlayersList()
                if arguments[1] == "action":
                    PlrMenu = PlayerMenu(
                        [0.25, 0.25, 0.5, 0.7],
                        playerList,
                        False,0, maxPlayerPages)
                    self.PlayerMenus[admin.UserIDString]['PlayerMenu'] = PlrMenu
                    self.showMenuElement(admin, ['PlayerMenu','ToolbarMenu'])
                elif arguments[1] == "give":
                    PlrMenu = PlayerMenu(
                        [0.25, 0.25, 0.5, 0.7],
                        playerList,
                        True,0, maxPlayerPages)
                    self.PlayerMenus[admin.UserIDString]['PlayerMenu'] = PlrMenu
                    self.showMenuElement(admin, ['PlayerMenu','ToolbarMenu'])
                else:
                    print "[AdminMenu]Invalid Argument count from %s" % admin.displayName
            elif arguments[0] == "action":
                targetPlayer = self.getPlayerFromID(arguments[1]) 
                ActMenu = ActionMenu(
                    [0.25, 0.25, 0.75, 0.7],
                    targetPlayer,
                    self.Config, admin)
                self.PlayerMenus[admin.UserIDString]['ActionMenu'] = ActMenu
                self.showMenuElement(admin, ['ActionMenu','ToolbarMenu'])
            elif arguments[0] == 'perms':
                player = self.getPlayerFromID(arguments[1])
                permissionList, maxPermPages = self.getPermissionsList()
                PermMenu = PermsMenu(
                    [0.25, 0.25, 0.75, 0.7],
                     permissionList, 0, maxPermPages, player)
                self.PlayerMenus[admin.UserIDString]['PermsMenu'] = PermMenu
                self.showMenuElement(admin, ['PermsMenu','ToolbarMenu'])
            else:
                print "[AdminMenu]Invalid Arguments from %s" % admin.displayName
        elif len(arguments) == 3:
            if arguments[0] == "confirm":
                if arguments[1] in ['Kick','Ban']:
                    targetPlayer = self.getPlayerFromID(arguments[2])
                    confMenu = ConfirmMenu(
                        [0.55, 0.25, 0.75, 0.7],
                        self.Config,
                        targetPlayer,
                        arguments[1])
                    self.PlayerMenus[admin.UserIDString]['ConfirmMenu'] = confMenu
                    self.showMenuElement(admin, ['ConfirmMenu','ToolbarMenu'])
            elif arguments[0] == "playermenu":
                playerList, maxPlayerPages = self.getPlayersList(int(arguments[2]))
                if arguments[1] == "action":
                    PlrMenu = PlayerMenu(
                        [0.25, 0.25, 0.5, 0.7],
                        playerList,
                        False, int(arguments[2]), maxPlayerPages)
                    self.PlayerMenus[admin.UserIDString]['PlayerMenu'] = PlrMenu
                    self.showMenuElement(admin, ['PlayerMenu','ToolbarMenu'])
                    return #open playermenu with action on player page arguments[2]
                elif arguments[1] == "give":
                    PlrMenu = PlayerMenu(
                        [0.25, 0.25, 0.5, 0.7],
                        playerList,
                        True,int(arguments[2]), maxPlayerPages)
                    self.PlayerMenus[admin.UserIDString]['PlayerMenu'] = PlrMenu
                    self.showMenuElement(admin,['PlayerMenu','ToolbarMenu'])
                    return #open playermenu with give on player page arguments[2]
            elif arguments[0] == "give" and arguments[1] == 'player':
                targetPlayer = self.getPlayerFromID(arguments[2])
                GivMenu = GiveMenu([0.25, 0.25, 0.75, 0.7],self.Config,self.GiveData,self.itemCategories,targetPlayer)
                self.PlayerMenus[admin.UserIDString]['GiveMenu'] = GivMenu
                self.showMenuElement(admin, ['GiveMenu','ToolbarMenu'])#open givemenu with player arguments[2]
            else:
                print "[AdminMenu]Invalid Argument count from %s" % admin.displayName
                return
        elif len(arguments) == 5:
            if arguments[0] == 'groups' and arguments[1] == 'group' and arguments[3] == 'page':
                permissionList, maxPermPages = self.getPermissionsList(int(arguments[4]))
                GrpMenu = GroupsMenu(
                    [0.25, 0.25, 0.75, 0.7],
                     permissionList, int(arguments[4]), maxPermPages, arguments[2])
                self.PlayerMenus[admin.UserIDString]['GroupMenu'] = GrpMenu
                self.showMenuElement(admin, ['GroupMenu','ToolbarMenu'])
            elif arguments[0] == 'perms' and arguments[1] == 'player' and arguments[3] == 'page':
                player = self.getPlayerFromID(arguments[2])
                permissionList, maxPermPages = self.getPermissionsList(int(arguments[4]))
                PermMenu = PermsMenu(
                    [0.25, 0.25, 0.75, 0.7],
                     permissionList, int(arguments[4]), maxPermPages,player)
                self.PlayerMenus[admin.UserIDString]['PermsMenu'] = PermMenu
                self.showMenuElement(admin, ['PermsMenu','ToolbarMenu'])
            elif arguments[0] == 'give'and arguments[1] == 'player' and arguments[3] == 'category':
                targetPlayer = self.getPlayerFromID(arguments[2])
                GivMenu = GiveMenu([0.25, 0.25, 0.75, 0.7],self.Config,self.GiveData,self.itemCategories,targetPlayer,0, arguments[4])
                self.PlayerMenus[admin.UserIDString]['GiveMenu'] = GivMenu
                self.showMenuElement(admin, ['GiveMenu','ToolbarMenu'])#open givemenu with player arguments[1]
            else:
                print "[AdminMenu]Invalid Argument count from %s" % admin.displayName
                return
        elif len(arguments) == 7:
            if arguments[0] == 'give'and arguments[1] == 'player' and arguments[3] == 'category' and arguments[5] == 'page':
                targetPlayer = self.getPlayerFromID(arguments[2])
                GivMenu = GiveMenu([0.25, 0.25, 0.75, 0.7],self.Config,self.GiveData,self.itemCategories,targetPlayer,int(arguments[6]), arguments[4])
                self.PlayerMenus[admin.UserIDString]['GiveMenu'] = GivMenu
                self.showMenuElement(admin, ['GiveMenu','ToolbarMenu'])
        else:
            self.showMenuElement(admin, ['ToolbarMenu'])#The adminmenu opened from chat / with no arguments
            
    def getPlayerFromID(self, playerID):
        for player in BasePlayer.activePlayerList:
            if player.UserIDString == playerID:
                return player
        for player in BasePlayer.sleepingPlayerList:
            if player.UserIDString == playerID:
                return player
        return None
            
    def getPlayersList(self, pagenum = 0):
        players = list()
        for player in BasePlayer.activePlayerList:
            players.append(player)
        for player in BasePlayer.sleepingPlayerList:
            players.append(player)
        playerPages = [players[x:x+self.Config['PlayersPerPage']] for x in xrange(0, len(players), self.Config['PlayersPerPage'])]
        return playerPages[pagenum], len(playerPages)
    
    def getPermissionsList(self, pagenum = 0):
        allpermissions = list(permission.GetPermissions())
        permPages = [allpermissions[x:x+14] for x in xrange(0, len(allpermissions), 14)]
        return permPages[pagenum], len(permPages)
    
    def showMenuElement(self,player, elemNames):
        if player.UserIDString not in self.displayedUIs:
            self.displayedUIs[player.UserIDString] = dict()
        elements = Cui.CuiElementContainer()
        for elemName in elemNames: 
            if elemName in self.PlayerMenus[player.UserIDString]:
                if elemName in self.displayedUIs[player.UserIDString]:
                    self.closeMenuElement(player, elemName)
                menu = self.PlayerMenus[player.UserIDString][elemName]
                elements.Add(menu.submenu)
                if menu.__class__ == PlayerMenu:
                    elements.Add(menu.TitleBox.textbox)
                    for button in menu.playerButtons.values():
                        elements.Add(button.playerbutton)
                    elements.Add(menu.NextBtn.actionbutton)
                    elements.Add(menu.PrevBtn.actionbutton)
                elif menu.__class__ == ActionMenu:
                    elements.Add(menu.TitleBox.textbox)
                    elements.Add(menu.IDBox.textbox)
                    elements.Add(menu.LocBox.textbox)
                    elements.Add(menu.IPBox.textbox)
                    for button in menu.ActionButtons.values():
                        elements.Add(button.actionbutton)
                    for txt in  menu.PluginTexts.values():
                        elements.Add(txt.textbox)
                elif menu.__class__ == ToolMenu:
                    elements.Add(menu.TitleBox.textbox)
                    for button in menu.ActionButtons.values():
                        elements.Add(button.actionbutton)
                    for txt in  menu.PluginTexts.values():
                        elements.Add(txt.textbox)
                elif menu.__class__ == ToolbarMenu:
                    elements.Add(menu.TitleBox.textbox)
                    elements.Add(menu.PlayerMgmntBtn.actionbutton)
                    elements.Add(menu.AdminToolBtn.actionbutton)
                    if hasattr(menu, "GiveBtn"):
                        elements.Add(menu.GiveBtn.actionbutton)
                    if hasattr(menu, "GroupsBtn"):
                        elements.Add(menu.GroupsBtn.actionbutton)
                    elements.Add(menu.CloseBtn.actionbutton)
                elif menu.__class__ == GroupsMenu:
                    elements.Add(menu.TitleBox.textbox)
                    for txt in menu.PermTexts:
                        elements.Add(menu.PermTexts[txt].textbox)
                    for btn in menu.PermButtons:
                        elements.Add(menu.PermButtons[btn].grouppermbutton)
                    for btn in menu.GroupButtons:
                        elements.Add(menu.GroupButtons[btn].actionbutton)
                    elements.Add(menu.PrevBtn.actionbutton)
                    elements.Add(menu.NextBtn.actionbutton)
                elif menu.__class__ == PermsMenu:
                    elements.Add(menu.TitleBox.textbox)
                    for txt in menu.PermTexts:
                        elements.Add(menu.PermTexts[txt].textbox)
                    for btn in menu.PermButtons:
                        elements.Add(menu.PermButtons[btn].playerpermbutton)
                    elements.Add(menu.PrevBtn.actionbutton)
                    elements.Add(menu.NextBtn.actionbutton)
                elif menu.__class__ == GiveMenu:
                    elements.Add(menu.TitleBox.textbox)
                    for txt in menu.itemTexts:
                        elements.Add(menu.itemTexts[txt].textbox)
                    for btn in menu.itemButtons:
                        elements.Add(menu.itemButtons[btn].actionbutton)
                    for btn in menu.categoryButtons:
                        elements.Add(menu.categoryButtons[btn].actionbutton)
                    elements.Add(menu.PrevBtn.actionbutton)
                    elements.Add(menu.NextBtn.actionbutton)
                elif menu.__class__ == ConfirmMenu:
                    elements.Add(menu.TitleBox.textbox)
                    for btn in menu.reasonButtons:
                        elements.Add(menu.reasonButtons[btn].actionbutton)
                    elements.Add(menu.CloseBtn.actionbutton)
                self.displayedUIs[player.UserIDString][elemName] = elements
                del self.PlayerMenus[player.UserIDString][elemName]
        Cui.CuiHelper.AddUi(player,elements)

    def Unload(self):
        for player in BasePlayer.activePlayerList:
            if player.UserIDString in self.displayedUIs:
                self.closeMenu(player)

    def closePlayerMenu(self, args):
        self.closeMenu(args.Connection.player)
                    
    def closeMenu(self, player):
        menus = self.displayedUIs[player.UserIDString].keys()
        for menu in menus:
            elements = self.displayedUIs[player.UserIDString][menu]
            for elem in elements:
                Cui.CuiHelper.DestroyUi(player, elem.Name)
        del self.PlayerMenus[player.UserIDString]
        del self.displayedUIs[player.UserIDString]

    def closeMenuElement(self, player, elemName):
        if player.UserIDString in self.displayedUIs:
            if elemName in self.displayedUIs[player.UserIDString]:
                elements = self.displayedUIs[player.UserIDString][elemName]
                for elem in elements:
                    Cui.CuiHelper.DestroyUi(player, elem.Name)
                del self.displayedUIs[player.UserIDString][elemName]

class TextBox:
    def __init__(self, position, text, textColour, fontSize, align):
        minPos = "%f %f" % (position[0], position[1])
        maxPos = "%f %f" % (position[2], position[3])
        self.txtcomp = Cui.CuiTextComponent(Text = text,
                        Color = textColour,
                        FadeIn = 0.5,
                        Align = align,
                        FontSize = fontSize)
        self.rectcomp = Cui.CuiRectTransformComponent(
            AnchorMin = minPos,
            AnchorMax = maxPos)
        self.textbox = Cui.CuiElement(FadeOut = 0.5)
        self.textbox.Components.Add(self.txtcomp)
        self.textbox.Components.Add(self.rectcomp)

class SubMenu:
    def __init__(self, position):
        self.pos = position
        self.Colours = {
            'grey_tint'  : "0.1 0.1 0.1 0.7",
            'red_tint'   : "0.5 0.1 0.1 0.7",
            'green_tint' : "0.1 0.4 0.1 0.5",
            'yellow_tint': "0.7 0.7 0.1 0.5",
            'orange_tint': "0.8 0.5 0.1 0.5",
            'blue_tint'  : "0.1 0.1 0.4 0.5",
            'black_tint' : "0.01 0.01 0.01 0.9",
            'white_tint' : "1 1 1 0.9",  
            'red_text'      : "0.8 0.1 0.1",
            'yellow_text'   : "0.7 0.7 0.1",
            'green_text'    : "0.1 0.8 0.1",
            'blue_text'     : "0.1 0.1 0.8",
            'white_text'    : "1 1 1",
            'orange_text'   : "0.8 0.5 0.1",
            'black_text'    : "0 0 0"}
        minPos = "%f %f" % (position[0], position[1])
        maxPos = "%f %f" % (position[2], position[3])
        self.submenu = Cui.CuiPanel()
        self.submenu.Image.Color = "0.1 0.1 0.1 0.7"
        self.submenu.Image.FadeIn = 0.5
        self.submenu.RectTransform.AnchorMin = minPos
        self.submenu.RectTransform.AnchorMax = maxPos
        self.submenu.CursorEnabled = True

class ToolbarMenu(SubMenu):
    def __init__(self, position, config, admin):
        SubMenu.__init__(self, position)
        self.Config = config
        self.TitleBox = TextBox(
            [self.pos[0],self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "AdminMenu V%s by FlamingMojo" % self.Config['Version'],
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        buttonHeight = 0.1
        buttonWidth = 0.125
        self.PlayerMgmntBtn = ActionButton(
            [self.pos[0],self.pos[1],self.pos[0]+buttonWidth,self.pos[1]+buttonHeight],
            "adminmenu.open playermenu action",
            "Player Management",
            self.Colours['white_text'],
            self.Colours['grey_tint'])
        self.AdminToolBtn = ActionButton(
            [self.pos[0]+buttonWidth,self.pos[1],self.pos[0]+(2*buttonWidth),self.pos[1]+buttonHeight],
            "adminmenu.open toolmenu",
            "Admin Tools",
            self.Colours['white_text'],
            self.Colours['grey_tint'])
        if plugins.Exists("Give") and admin.IsAdmin:
            self.GiveBtn = ActionButton(
                [self.pos[0]+(2*buttonWidth),self.pos[1],self.pos[0]+(3*buttonWidth),self.pos[1]+buttonHeight],
                "adminmenu.open playermenu give",
                "Give Menu",
                self.Colours['white_text'],
                self.Colours['grey_tint'])
        if admin.IsAdmin and permission.UserHasPermission(admin.UserIDString,'adminmenu.allow.groups'):
            self.GroupsBtn = ActionButton(
                [self.pos[0]+(3*buttonWidth),self.pos[1],self.pos[0]+(4*buttonWidth),self.pos[1]+buttonHeight],
                "adminmenu.open groups",
                "Group Permissions",
                self.Colours['white_text'],
                self.Colours['grey_tint'])
        self.CloseBtn = ActionButton(
            [self.pos[0]+(4*buttonWidth),self.pos[1],self.pos[0]+(5*buttonWidth),self.pos[1]+buttonHeight],
            "adminmenu.close",
            "Close",
            self.Colours['black_text'],
            self.Colours['red_tint'])
        
class ConfirmMenu(SubMenu):
    def __init__(self, position, config, player, action):
        SubMenu.__init__(self, position)
        self.reasonButtons = dict()
        buttonIndex = 0
        buttonHeight = 0.025
        self.TitleBox = TextBox(
            [self.pos[0],self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "%s %s for what?" % (action, player.displayName),
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        for reason in config['Reasons'][action]:
            reasonButton = ActionButton(
                [self.pos[0],self.pos[2]-(0.1+(buttonHeight+(buttonHeight*buttonIndex))), self.pos[3],self.pos[2]-(0.1+(buttonHeight*buttonIndex))],
                'adminmenu.handle %s %s %s' % (action.lower(),player.UserIDString, reason),
                reason,
                self.Colours['white_text'],
                self.Colours['black_tint'])
            self.reasonButtons[reasonButton.ID] = reasonButton
            buttonIndex += 1
        self.CloseBtn = ActionButton(
            [self.pos[0],self.pos[1],self.pos[2],self.pos[1]+buttonHeight],
            "adminmenu.open playermenu action",
            "Cancel",
            self.Colours['white_text'],
            self.Colours['red_tint'])
    
    
class PlayerMenu(SubMenu):
    def __init__(self, position, playersToDisplay, give, pagenum, totalpages):
        SubMenu.__init__(self, position)
        self.playerButtons = dict()
        if give:
            text = "Give to?"
            changecmd = "adminmenu.open playermenu give %s"
        else:
            text = "Manage who?"
            changecmd = "adminmenu.open playermenu action %s"
        self.TitleBox = TextBox(
            [self.pos[0],self.pos[3]-0.05,self.pos[2],self.pos[3]],
            text,
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        buttonIndex = 0
        buttonHeight = 0.025
        buttonWidth = 0.15
        border = 0.05
        topborder = 0.1
        for player in playersToDisplay:
            buttonMinX, buttonMinY = self.pos[0]+border , self.pos[3]-(topborder + buttonHeight + (buttonHeight*buttonIndex))
            buttonMaxX, buttonMaxY = buttonMinX+buttonWidth , buttonMinY+buttonHeight
            buttonPos = [buttonMinX, buttonMinY, buttonMaxX, buttonMaxY]
            plrButton = PlayerButton(buttonPos, player, give)
            self.playerButtons[plrButton.ID] = plrButton
            buttonIndex += 1
        if pagenum < totalpages -1:
            nextpage = pagenum+1
        else:
            nextpage = 0
        if pagenum > 0:
            prevpage = pagenum-1
        else:
            prevpage = totalpages -1
        btnWidth = (self.pos[2]-self.pos[0])/2.0
        btnHeight = 0.05
        self.PrevBtn = ActionButton(
            [self.pos[0],self.pos[1],self.pos[0]+btnWidth,self.pos[1]+btnHeight],
            changecmd % prevpage,
            "<<<",
            self.Colours["white_text"],
            self.Colours["grey_tint"])
        self.NextBtn = ActionButton(
            [self.pos[0]+btnWidth,self.pos[1],self.pos[2],self.pos[1]+btnHeight],
            changecmd % nextpage,
            ">>>",
            self.Colours["white_text"],
            self.Colours["grey_tint"])

class GroupsMenu(SubMenu):
    def __init__(self, position, permissions, page, maxPages, selectedGroup = 'Default'):
        SubMenu.__init__(self, position)
        self.PermTexts = dict()
        self.PermButtons = dict()
        self.GroupButtons = dict()
        changecmd = "adminmenu.open groups group %s page %d"
        if page < maxPages -1:
            nextpage = page+1
        else:
            nextpage = 0
        if page > 0:
            prevpage = page-1
        else:
            prevpage = maxPages -1
        self.TitleBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "%s Group Permissions" % selectedGroup,
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        permIndex = 0
        groupIndex = 0
        permwidth = (self.pos[2] - self.pos[0]) / 3.0
        permheight = 0.025
        for group in list(permission.GetGroups()):
            GrpBtn = ActionButton(
                [self.pos[0],
                 self.pos[3]-(0.05 + permheight + (permheight * groupIndex)),
                 self.pos[0]+permwidth,
                 self.pos[3] - (0.05 + (permheight * groupIndex))],
                "adminmenu.open groups group %s page %d " % (group,page),
                group,
                self.Colours["white_text"],
                self.Colours["grey_tint"])
            self.GroupButtons[GrpBtn.ID] = GrpBtn
            groupIndex += 1
        for perm in permissions:
            if permission.GroupHasPermission(selectedGroup,perm):
                textColour = self.Colours['white_text']
                text = perm+'     (Granted)'
            else:
                textColour = self.Colours['red_text']
                text = perm+'     (No Perm)'
            permText = TextBox(
                [self.pos[0]+permwidth,
                 self.pos[3]-(0.05 + permheight + (permheight * permIndex)),
                 self.pos[0]+(2*permwidth),
                 self.pos[3] - (0.05 + (permheight * permIndex))],
                text,
                textColour,
                12, TextAnchor.MiddleRight)
            self.PermTexts[perm] = permText
            PermBtn = GroupPermButton(
                [self.pos[0] + (2.33 * permwidth),
                 self.pos[3]-(0.05 + permheight + (permheight * permIndex)),
                 self.pos[2] - (permwidth/3.0),
                 self.pos[3] - (0.05 + (permheight * permIndex))],
                selectedGroup,
                perm)
            self.PermButtons[PermBtn.ID] = PermBtn
            permIndex += 1
        btnWidth = permwidth/2.0
        btnHeight = 0.05
        self.PrevBtn = ActionButton(
            [self.pos[0]+permwidth,self.pos[1],self.pos[0]+permwidth+btnWidth,self.pos[1]+btnHeight],
            changecmd % (selectedGroup,prevpage),
            "<<<",
            self.Colours["white_text"],
            self.Colours["grey_tint"])
        self.NextBtn = ActionButton(
            [self.pos[2]-(permwidth+btnWidth),self.pos[1],self.pos[2]-permwidth,self.pos[1]+btnHeight],
            changecmd % (selectedGroup,nextpage),
            ">>>",
            self.Colours["white_text"],
            self.Colours["grey_tint"])

class PermsMenu(SubMenu):
    def __init__(self, position, permissions, page, maxPages, player):
        SubMenu.__init__(self, position)
        self.PermTexts = dict()
        self.PermButtons = dict()
        changecmd = "adminmenu.open perms player %s page %d"
        if page < maxPages -1:
            nextpage = page+1
        else:
            nextpage = 0
        if page > 0:
            prevpage = page-1
        else:
            prevpage = maxPages -1
        self.TitleBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "%s Player Permissions" % player.displayName,
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        permIndex = 0
        permwidth = (self.pos[2] - self.pos[0]) / 3.0
        permheight = 0.025
        playerGroups = list(permission.GetUserGroups(player.UserIDString))
        for perm in permissions:
            Inherited = False
            if permission.UserHasPermission(player.UserIDString,perm):
                HasPerm = True
                for group in playerGroups:
                    if permission.GroupHasPermission(group,perm):
                        Inherited = True     
            else:
                HasPerm = False
            if HasPerm:
                if Inherited:
                    textColour = self.Colours['yellow_text']
                    text = perm+'      (Inherited)'
                else:
                    textColour = self.Colours['white_text']
                    text = perm+'      (Granted)'
            else:
                textColour = self.Colours['red_text']
                text = perm+'      (No Perm)'
            permText = TextBox(
                [self.pos[0]+permwidth,
                 self.pos[3]-(0.05 + permheight + (permheight * permIndex)),
                 self.pos[0]+(2*permwidth),
                 self.pos[3]- (0.05 + (permheight * permIndex))],
                text,
                textColour,
                12, TextAnchor.MiddleRight)
            self.PermTexts[perm] = permText
            PermBtn = PlayerPermButton(
                [self.pos[0] + (2.33 * permwidth),
                 self.pos[3] - (0.05 + permheight + (permheight * permIndex)),
                 self.pos[2] - (permwidth/3.0),
                 self.pos[3] - (0.05 + (permheight * permIndex))],
                player,
                perm)
            self.PermButtons[PermBtn.ID] = PermBtn
            permIndex += 1
        btnWidth = permwidth/2.0
        btnHeight = 0.05
        self.PrevBtn = ActionButton(
            [self.pos[0]+permwidth,self.pos[1],self.pos[0]+permwidth+btnWidth,self.pos[1]+btnHeight],
            changecmd % (player.UserIDString,prevpage),
            "<<<",
            self.Colours["white_text"],
            self.Colours["grey_tint"])
        self.NextBtn = ActionButton(
            [self.pos[2]-(permwidth+btnWidth),self.pos[1],self.pos[2]-permwidth,self.pos[1]+btnHeight],
            changecmd % (player.UserIDString,nextpage),
            ">>>",
            self.Colours["white_text"],
            self.Colours["grey_tint"])
    
class ActionMenu(SubMenu):
    def __init__(self, position, player, config, admin):
        SubMenu.__init__(self, position)
        self.ActionButtons = dict()
        self.PluginTexts = dict()
        self.Config = config
        playerPos = player.GetEstimatedWorldPosition()
        self.playerID = player.UserIDString
        self.TitleBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "What do you want to do with %s?" % player.displayName,
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        self.IDBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.1,self.pos[0]+0.15,self.pos[3]],
            "ID  : %s" % player.UserIDString,
            self.Colours["white_text"],
            12, TextAnchor.MiddleLeft)
        self.LocBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.15,self.pos[0]+0.15,self.pos[3]],
            "LOC : %s" % str(playerPos),
            self.Colours["white_text"],
            12, TextAnchor.MiddleLeft)
        if player in BasePlayer.activePlayerList:
            self.IPBox = TextBox(
                [self.pos[0]+0.01,self.pos[3]-0.2,self.pos[0]+0.15,self.pos[3]],
                "IP  : %s" % player.net.connection.ipaddress,
                self.Colours["white_text"],
                12, TextAnchor.MiddleLeft)
        else:
            self.IPBox = TextBox(
                [self.pos[0]+0.01,self.pos[3]-0.2,self.pos[0]+0.15,self.pos[3]],
                "Player is offline (sleeping)",
                self.Colours["yellow_text"],
                12, TextAnchor.MiddleLeft)
        pluginIndex = 0
        buttonWidth = 0.05
        buttonHeight = 0.025
        for plugin in self.Config['ActionMenu']:
            if plugins.Exists(plugin):     
                txtminX = self.pos[0] + 0.1
                txtminY = self.pos[3] - (0.1+buttonHeight + (pluginIndex * buttonHeight))
                txtmaxX = txtminX + buttonWidth*2
                txtmaxY = txtminY + buttonHeight
                plugintxt = TextBox(
                    [txtminX, txtminY, txtmaxX, txtmaxY],
                    plugin,
                    self.Colours['white_text'],
                    12, TextAnchor.MiddleLeft)
                self.PluginTexts[plugin] = plugintxt
                for act in self.Config['ActionMenu'][plugin]:
                    action = self.Config['ActionMenu'][plugin][act]
                    if (permission.UserHasPermission(admin.UserIDString,action['perm']) or
                        (action['perm'] == 'IsAdmin' and admin.IsAdmin) or
                        (plugin == 'AdminMenu' and permission.UserHasPermission(admin.UserIDString,'adminmenu.allow.all'))):
                        comm = action['cmd'].replace('{PLAYERID}',player.UserIDString)
                        comm = comm.replace('{PLAYERNAME}',player.displayName)
                        comm = comm.replace('{ADMINID}',admin.UserIDString)
                        comm = comm.replace('{ADMINNAME}',admin.displayName)
                        comm = comm.replace('{PLAYERXYZ}','%f %f %f' % (playerPos.x, playerPos.y, playerPos.z))
                        comm = comm.replace('{PLAYERXZ}','%f %f' % (playerPos.x, playerPos.z))
                        btnminX = self.pos[0] + 0.2 + ((action['pos']-1) * buttonWidth)
                        btnminY = self.pos[3] - (0.1+buttonHeight + (pluginIndex * buttonHeight))
                        btnmaxX = btnminX + buttonWidth
                        btnmaxY = btnminY + buttonHeight
                        actbtn = ActionButton(
                            [btnminX, btnminY, btnmaxX, btnmaxY],
                            comm,
                            act,
                            self.Colours[action['tcolour']],
                            self.Colours[action['bcolour']])
                        self.ActionButtons[actbtn.ID] = actbtn
                pluginIndex += 1

class ToolMenu(SubMenu):
    def __init__(self, position, config, admin):
        SubMenu.__init__(self, position)
        self.ActionButtons = dict()
        self.PluginTexts = dict()
        self.Config = config
        self.TitleBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "What do you want to do?",
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        pluginIndex = 0
        buttonWidth = 0.05
        buttonHeight = 0.025
        for plugin in self.Config['ToolMenu']:
            if plugins.Exists(plugin):     
                txtminX = self.pos[0] + 0.1
                txtminY = self.pos[3] - (0.1+ buttonHeight + (pluginIndex * buttonHeight))
                txtmaxX = txtminX + buttonWidth*2
                txtmaxY = txtminY + buttonHeight
                plugintxt = TextBox(
                    [txtminX, txtminY, txtmaxX, txtmaxY],
                    plugin,
                    self.Colours['white_text'],
                    12, TextAnchor.MiddleLeft)
                self.PluginTexts[plugin] = plugintxt
                for act in self.Config['ToolMenu'][plugin]:
                    action = self.Config['ToolMenu'][plugin][act]
                    if (permission.UserHasPermission(admin.UserIDString,action['perm']) or
                        (action['perm'] == 'IsAdmin' and admin.IsAdmin) or
                        (plugin == 'AdminMenu' and permission.UserHasPermission(admin.UserIDString,'adminmenu.allow.all'))):
                        comm = action['cmd'].replace('{ADMINID}',admin.UserIDString)
                        comm = comm.replace('{ADMINNAME}',admin.displayName)
                        btnminX = self.pos[0] + 0.2 + ((action['pos']-1) * buttonWidth)
                        btnminY = self.pos[3] - (0.1+ buttonHeight + (pluginIndex * buttonHeight))
                        btnmaxX = btnminX + buttonWidth
                        btnmaxY = btnminY + buttonHeight
                        actbtn = ActionButton(
                            [btnminX, btnminY, btnmaxX, btnmaxY],
                            comm,
                            act,
                            self.Colours[action['tcolour']],
                            self.Colours[action['bcolour']])
                        self.ActionButtons[actbtn.ID] = actbtn
                pluginIndex += 1

class GiveMenu(SubMenu):
    def __init__(self, position,config,data,categories,player,pagenum = 0,selectedCat = 'Weapon'):
        SubMenu.__init__(self, position)
        self.TitleBox = TextBox(
            [self.pos[0]+0.01,self.pos[3]-0.05,self.pos[2],self.pos[3]],
            "Give %s what?" % player.displayName,
            self.Colours["orange_text"],
            14, TextAnchor.MiddleCenter)
        changecmd = "adminmenu.open give player %s category %s page %d"
        itemsInCategory = list()
        for item in data:
            if data[item]['Category'] == selectedCat:
                itemsInCategory.append(item)
        dataToShow = [itemsInCategory[x:x+14] for x in xrange(0, len(itemsInCategory), 14)]
        totalpages = len(dataToShow)
        if pagenum < totalpages -1:
            nextpage = pagenum+1
        else:
            nextpage = 0
        if pagenum > 0:
            prevpage = pagenum-1
        else:
            prevpage = totalpages -1
        self.categoryButtons = dict()
        self.itemButtons = dict()
        self.itemTexts = dict()
        catBtnWidth = (self.pos[2] - self.pos[0]) / 3.0
        catBtnHeight = 0.025
        catBtnIndex = 0
        for cat in categories:
            minx, miny = self.pos[0], self.pos[3] - (0.06 + (catBtnHeight + (catBtnHeight*catBtnIndex)))
            maxx, maxy = minx + catBtnWidth, miny + catBtnHeight
            catBtn = ActionButton(
                [minx,miny,maxx,maxy],
                "adminmenu.open give player %s category %s" % (player.UserIDString, cat),
                cat,
                self.Colours['white_text'],
                self.Colours['grey_tint'])
            self.categoryButtons[catBtn.ID] = catBtn
            catBtnIndex += 1
        itemBtnWidth = catBtnWidth / 5.0
        itemBtnHeight = 0.025
        itemIndex = 0
        for item in dataToShow[pagenum]:
            minx, miny = self.pos[0]+catBtnWidth, self.pos[3] - (0.06 + (itemBtnHeight + (itemBtnHeight*itemIndex)))
            maxx, maxy = minx + catBtnWidth - 0.01, miny + itemBtnHeight
            itemTxt = TextBox(
                [minx,miny,maxx,maxy],
                data[item]['Name'],
                self.Colours["white_text"],
                12,TextAnchor.MiddleRight)
            self.itemTexts[item] = itemTxt
            if selectedCat in config['GiveMenu']:
                amounts = config['GiveMenu'][selectedCat]
                if type(amounts) == int:
                    amounts = [1]
            else:
                amounts = [1]
            amntIndex = 0
            for amount in amounts:
                minx, miny = self.pos[0]+(2*catBtnWidth + itemBtnWidth*amntIndex), self.pos[3] - (0.06 + (itemBtnHeight + (itemBtnHeight*itemIndex)))
                maxx, maxy = minx + itemBtnWidth, miny +itemBtnHeight
                amntBtn = ActionButton(
                    [minx,miny,maxx,maxy],
                    "inv.giveplayer %s %s %d" % (player.UserIDString, item, amount),
                    str(amount),
                    self.Colours['white_text'],
                    self.Colours['grey_tint'])
                amntIndex += 1
                self.itemButtons[amntBtn.ID] = amntBtn
            itemIndex += 1
        self.PrevBtn = ActionButton(
            [self.pos[0]+catBtnWidth,self.pos[1],self.pos[0]+catBtnWidth+(catBtnWidth/2.0),self.pos[1]+catBtnHeight],
            changecmd % (player.UserIDString, selectedCat,prevpage),
            "<<<",
            self.Colours["white_text"],
            self.Colours["grey_tint"])
        self.NextBtn = ActionButton(
            [self.pos[0]+(catBtnWidth+(catBtnWidth/2.0)),self.pos[1],self.pos[2]-catBtnWidth,self.pos[1]+catBtnHeight],
            changecmd % (player.UserIDString,selectedCat,nextpage),
            ">>>",
            self.Colours["white_text"],
            self.Colours["grey_tint"])            
            
class ActionButton:
    def __init__(self, position, command, name, textcolour, buttoncolour):
        self.ID = str(Guid.NewGuid()).replace('-','')
        self.pos = position
        minPos = "%f %f" % (position[0], position[1])
        maxPos = "%f %f" % (position[2], position[3])
        self.actionbutton = Cui.CuiButton()
        self.actionbutton.Button.Command = command
        self.actionbutton.Button.Color = buttoncolour
        self.actionbutton.Text.Text = name
        self.actionbutton.Text.Color = textcolour
        self.actionbutton.Text.FontSize = 12
        self.actionbutton.Text.Align = TextAnchor.MiddleCenter
        self.actionbutton.RectTransform.AnchorMin = minPos
        self.actionbutton.RectTransform.AnchorMax = maxPos

class GroupPermButton:
    def __init__(self, position, group, perm):
        self.ID = str(Guid.NewGuid()).replace('-','')
        self.pos = position
        minPos = "%f %f" % (position[0], position[1])
        maxPos = "%f %f" % (position[2], position[3])
        self.grouppermbutton = Cui.CuiButton()
        if permission.GroupHasPermission(group,perm):
            command = "revoke group %s %s" % (group, perm)
            text = 'Revoke'
            buttoncolour = "0.1 0.4 0.1 0.5"
        else:
            command = "grant group %s %s" % (group, perm)
            text = 'Grant'
            buttoncolour = "0.5 0.1 0.1 0.7"     
        self.grouppermbutton.Button.Command = command
        self.grouppermbutton.Button.Color = buttoncolour
        self.grouppermbutton.Text.Text = text
        self.grouppermbutton.Text.Color = "1 1 1"
        self.grouppermbutton.Text.FontSize = 12
        self.grouppermbutton.Text.Align = TextAnchor.MiddleCenter
        self.grouppermbutton.RectTransform.AnchorMin = minPos
        self.grouppermbutton.RectTransform.AnchorMax = maxPos

class PlayerPermButton:
    def __init__(self, position, player, perm):
        self.ID = str(Guid.NewGuid()).replace('-','')
        self.pos = position
        minPos = "%f %f" % (position[0], position[1])
        maxPos = "%f %f" % (position[2], position[3])
        self.playerpermbutton = Cui.CuiButton()
        if permission.UserHasPermission(player.UserIDString,perm):
            command = "revoke user %s %s" % (player.UserIDString, perm)
            text = 'Revoke'
            buttoncolour = "0.5 0.1 0.1 0.7"
        else:
            command = "grant user %s %s" % (player.UserIDString, perm)
            text = 'Grant'
            buttoncolour = "0.1 0.4 0.1 0.5"    
        self.playerpermbutton.Button.Command = command
        self.playerpermbutton.Button.Color = buttoncolour
        self.playerpermbutton.Text.Text = text
        self.playerpermbutton.Text.Color = "1 1 1"
        self.playerpermbutton.Text.FontSize = 12
        self.playerpermbutton.Text.Align = TextAnchor.MiddleCenter
        self.playerpermbutton.RectTransform.AnchorMin = minPos
        self.playerpermbutton.RectTransform.AnchorMax = maxPos

class PlayerButton:
    def __init__(self, position, player, give =  False, colourOn = "0.1 0.4 0.1 0.5", colourOff = "0.5 0.1 0.1 0.5"):
        self.ID = str(Guid.NewGuid()).replace('-','')
        self.pos = position
        self.Toggle = False
        minPos = "%f %f" % (position[0], position[1])
        maxPos = "%f %f" % (position[2], position[3])
        self.playerID = player.UserIDString
        self.playerName = player.displayName
        self.colourOn = colourOn
        self.colourOff = colourOff
        if give:
            self.command = "adminmenu.open give player %s" % self.playerID
        else:
            self.command = "adminmenu.open action %s" % self.playerID
        self.playerbutton = Cui.CuiButton()
        self.playerbutton.Button.Command = self.command
        if player in BasePlayer.activePlayerList:
            self.playerbutton.Button.Color = self.colourOn
        else:
            self.playerbutton.Button.Color = self.colourOff
        self.playerbutton.Text.Text = self.playerName
        self.playerbutton.Text.Color = "1 1 1"
        self.playerbutton.Text.FontSize = 12
        self.playerbutton.Text.Align = TextAnchor.MiddleCenter
        self.playerbutton.RectTransform.AnchorMin = minPos
        self.playerbutton.RectTransform.AnchorMax = maxPos

