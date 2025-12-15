using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System;

namespace Oxide.Plugins
{
    /*
     * DeathmatchSoccer - 3-Team Soccer Battle Plugin with Goal Swapping Rotation
     * 
     * FEATURES:
     * - 3-Team System: Blue (SHELL-SEA/GRUB), Red (Loot-pool/DOORCAMPER), Black (PZG/ROAMER)
     * - Goal Swapping Rotation: 2 teams play, losing team's goal is replaced by waiting team's goal
     * - Team Attire: Hazmat suits for field players (Red/Black/Blue), Heavy Armor for Goalies
     * - Modern UI: Team selection menu, dynamic scoreboard, 4-role selection
     * - 4 Roles with SoccerWeapons.cs Integration:
     *   • Striker (100HP): Bat (Home Run) + Python Revolver (Phase Shift) + 3 Barricades
     *   • Playmaker (125HP): Snowball Gun (Magnet) + Crossbow (Whistle) + 3 Barricades
     *   • Enforcer (150HP): Nailgun Pistol (Yellow Card) + Bat (Home Run) + 3 Barricades
     *   • Goalie (200HP): Heavy Armor + MGL (Medi-Launcher) + SPAS-12 + NVG (ESP) + 3 Barricades
     * - Active Goal System: Only active goals count for scoring
     * 
     * ROTATION SYSTEM:
     * - 2 black goals placed (one at red position, one at blue position)
     * - When a team loses, their goal is deactivated and black goal at that position activates
     * - Winner continues at their goal, black team takes over loser's goal position
     * - No teleportation needed - seamless goal swapping
     * 
     * ADMIN COMMANDS:
     * /set_red, /set_blue - Set red and blue goal positions
     * /set_black1, /set_black2 - Set black goals (at red and blue positions)
     * /set_center - Set ball spawn position
     * /set_lobby_spawn - Set lobby spawn point where players teleport during lobby
     * /save_goals, /load_goals - Persist arena data
     * /start_match - Begin the match
     * /rotation - Toggle rotation mode ON/OFF
     * /setskin <team> <item> <skinId> - Configure team skins
     * /showskins - Display all skin configurations
     * /goal_debug - Toggle goal zone visualization (shows active/inactive goals)
     * 
     * PLAYER COMMANDS:
     * /join [team] - Join a team (shows UI if no team specified)
     * /teams - Show team selection UI
     * /leave - Leave team and return to lobby
     * /help, /commands - Show help menu
     */
    [Info("DeathmatchSoccer", "KillaDome", "6.0.0")]
    [Description("3-Team Soccer with 4-Role System and SoccerWeapons Integration")]
    public class DeathmatchSoccer : RustPlugin
    {
        // ==========================================
        // 1. CONFIGURATION
        // ==========================================
        private string middlewareUrl = "http://165.22.174.250/chat"; 
        private string licenseKey = "2c573abe-3172-4fc9-b834-ba4c70fb1eb8";

        // SETTINGS
        private float KickForceMultiplier = 3500.0f; 
        private float MaxKickDistance = 15.0f; 
        private float LeashRadius = 15.0f;     
        private int ScoreToWin = 5;
        
        // GAME MODE: "soccer" = SoccerWeapons abilities, "normal" = custom skins + voted weapons only
        private string gameMode = "soccer";
        
        // GOAL BOX (Overwritten by LoadData)
        private float GoalWidth = 8.0f;
        private float GoalHeight = 4.0f;
        private float GoalDepth = 6.0f; 

        // IMAGES
        // 3 Scoreboard backgrounds for different matchups
        private string ImgScoreboardBgRedBlue = "https://i.imgur.com/rInAKIa.png"; 
        private string ImgScoreboardBgBlackRed = "https://i.imgur.com/T6MqVYH.png"; // TODO: Replace with actual URL
        private string ImgScoreboardBgBlueBlack = "https://i.imgur.com/ygeaW0I.png"; // TODO: Replace with actual URL
        
        // 3 Goal banner images for different matchups
        private string ImgGoalBannerRedBlue = "https://i.imgur.com/Jb9y1Xm.png";
        private string ImgGoalBannerBlackRed = "https://i.imgur.com/8KqZx4Y.png";
        private string ImgGoalBannerBlueBlack = "https://i.imgur.com/5LmNp2X.png";
        
        // HOST badge image (displayed in host UI panel)
        private string ImgHostBadge = "https://i.imgur.com/PeRORoA.png";

        [PluginReference] Plugin ImageLibrary;
        [PluginReference] Plugin Skins;

        // CUSTOM SKIN IDS (Configure these for each team)
        private Dictionary<string, TeamSkins> teamSkins = new Dictionary<string, TeamSkins>
        {
            { "blue", new TeamSkins { 
                TshirtSkin = 3619180626,     // Blue team tshirt
                PantsSkin = 3619368981,      // Blue team pants
                TorsoSkin = 3619178918,      // Blue team metal.plate.torso
                FacemaskSkin = 3619365504,   // Blue team metal.facemask
                ShoesSkin = 3619430819,      // Blue team burlap.shoes
                WeaponSkin = 0,              // Blue team thompson
                GoaliePantsSkin = 0,         // Blue goalie heavy.plate.pants
                GoalieJacketSkin = 0,        // Blue goalie heavy.plate.jacket
                GoalieWeaponSkin = 0         // Blue goalie spas12
            }},
            { "red", new TeamSkins { 
                TshirtSkin = 3619182996,     // Red team tshirt
                PantsSkin = 3619357729,      // Red team pants
                TorsoSkin = 3619182046,      // Red team metal.plate.torso
                FacemaskSkin = 3619366404,   // Red team metal.facemask
                ShoesSkin = 3619432687,      // Red team burlap.shoes
                WeaponSkin = 0,              // Red team thompson
                GoaliePantsSkin = 0,         // Red goalie heavy.plate.pants
                GoalieJacketSkin = 0,        // Red goalie heavy.plate.jacket
                GoalieWeaponSkin = 0         // Red goalie spas12
            }},
            { "black", new TeamSkins { 
                TshirtSkin = 3618727245,     // Black team tshirt
                PantsSkin = 3619358770,      // Black team pants
                TorsoSkin = 3619177448,      // Black team metal.plate.torso
                FacemaskSkin = 3619364394,   // Black team metal.facemask
                ShoesSkin = 3619429117,      // Black team burlap.shoes
                WeaponSkin = 0,              // Black team thompson
                GoaliePantsSkin = 0,         // Black goalie heavy.plate.pants
                GoalieJacketSkin = 0,        // Black goalie heavy.plate.jacket
                GoalieWeaponSkin = 0         // Black goalie spas12
            }}
        };
        
        private class TeamSkins
        {
            public ulong TshirtSkin { get; set; }
            public ulong PantsSkin { get; set; }
            public ulong TorsoSkin { get; set; }
            public ulong FacemaskSkin { get; set; }
            public ulong ShoesSkin { get; set; }
            public ulong WeaponSkin { get; set; }
            public ulong GoaliePantsSkin { get; set; }
            public ulong GoalieJacketSkin { get; set; }
            public ulong GoalieWeaponSkin { get; set; }
        }

        // STATE
        private BaseEntity activeBall;
        private BasePlayer lastKicker; 
        
        // DYNAMIC GOAL SYSTEM - Goal 1 and Goal 2
        private Vector3 goal1Pos, goal2Pos, centerPos;
        private Quaternion goal1Rot, goal2Rot;
        private string goal1Team = ""; // Which team is assigned to Goal 1
        private string goal2Team = ""; // Which team is assigned to Goal 2
        private Color goal1Color = Color.white; // Dynamic color for Goal 1
        private Color goal2Color = Color.white; // Dynamic color for Goal 2
        
        private int scoreRed = 0;
        private int scoreBlue = 0;
        private int scoreBlack = 0;
        private bool gameActive = false; 
        private bool matchStarted = false; 
        private bool matchActive = false; // Tracks if a match is currently active
        private bool debugActive = false;
        
        // Ball scaling (1.0 = normal, 0.5 = half size, 2.0 = double size)
        private float ballScale = 1.0f; // Configure ball size here (recommended range: 0.5 - 3.0)
        
        // ROTATION SYSTEM - Goal Swapping (2 play, 1 waits)
        private bool rotationMode = true; // Enable rotation by default
        private string waitingTeam = "black"; // Team waiting for next match
        private string team1Playing = "blue";
        private string team2Playing = "red";
        private int matchNumber = 1;
        private int maxMatchesPerTournament = 2; // Tournament ends after 2 matches
        
        // LOBBY SYSTEM
        private bool lobbyActive = true;
        private Vector3 lobbySpawnPos = Vector3.zero;
        private Vector3 loserSpawnPos = Vector3.zero;  // Spawn point for losing team after match
        private Timer lobbyTimer;
        private Timer lobbyReminderTimer;
        private int lobbyCountdown = 0;
        
        // CELEBRATION SYSTEM
        private List<string> celebrationMessages = new List<string>();
        private Timer celebrationTimer;
        
        // KILL FEED SYSTEM
        private List<KillFeedEntry> killFeed = new List<KillFeedEntry>();
        private Timer killFeedTimer;
        private const int MAX_KILL_FEED_ENTRIES = 5;
        
        // ACTIVE GOALS - Track which goals are currently in play
        private Dictionary<string, bool> activeGoals = new Dictionary<string, bool>
        {
            { "red", true },
            { "blue", true },
            { "black1", false },  // Black goal at red position (inactive initially)
            { "black2", false }   // Black goal at blue position (inactive initially)
        };
        
        // TEAM CONFIGURATIONS
        private Dictionary<string, TeamConfig> teamConfigs = new Dictionary<string, TeamConfig>
        {
            { "blue", new TeamConfig { Name = "SHELL-SEA FOOTBALL CLUB", Tag = "GRUB", Color = "0.2 0.4 1", HexColor = "#3366FF" } },
            { "red", new TeamConfig { Name = "Loot-pool F.C.", Tag = "DOORCAMPER", Color = "1 0.2 0.2", HexColor = "#FF3333" } },
            { "black", new TeamConfig { Name = "Project Zerg-Germain", Tag = "ROAMER", Color = "0.2 0.2 0.2", HexColor = "#333333" } }
        };
        
        private Timer gameTimer, tickerTimer, hudTimer, debugTimer;
        private Dictionary<ulong, bool> ballRangeState = new Dictionary<ulong, bool>();
        
        // TICKER
        private List<string> tickerMessages = new List<string> { "GOAL SWAPPING ROTATION", "LOSER'S GOAL REPLACED BY WAITING TEAM", "SHOOT BALL TO SCORE", "KILL ENEMIES", "FIRST TO 5 WINS" };
        private int tickerIndex = 0;
        
        //HOST SYSTEM
        private ulong hostPlayerId = 0; // Stores user ID of current host player
        
        // WEAPON VOTING SYSTEM
        private bool weaponVotingActive = false;
        private Dictionary<string, int> weaponVotes = new Dictionary<string, int>();
        private HashSet<ulong> playersWhoVoted = new HashSet<ulong>();
        private string votedWeapon = null;
        private Timer votingTimer;
        private int votingTimeRemaining = 30; // 30 seconds to vote
        private bool rerollUsedThisMatch = false; // Track if reroll has been used this match
        
        // MODE VOTING SYSTEM (Soccer vs Normal)
        private bool modeVotingActive = false;
        private int soccerModeVotes = 0;
        private int normalModeVotes = 0;
        private HashSet<ulong> playersWhoVotedMode = new HashSet<ulong>();
        private Timer modeVotingTimer;
        private int modeVotingTimeRemaining = 15; // 15 seconds to vote
        
        // Weapon voting options with item shortnames and display names
        private Dictionary<string, WeaponOption> weaponOptions = new Dictionary<string, WeaponOption>
        {
            { "ak47", new WeaponOption { ShortName = "rifle.ak", DisplayName = "AK-47", ImageUrl = "https://i.imgur.com/placeholder_ak.png" } },
            { "lr300", new WeaponOption { ShortName = "rifle.lr300", DisplayName = "LR-300", ImageUrl = "https://i.imgur.com/placeholder_lr300.png" } },
            { "m249", new WeaponOption { ShortName = "lmg.m249", DisplayName = "M249", ImageUrl = "https://i.imgur.com/placeholder_m249.png" } },
            { "thompson", new WeaponOption { ShortName = "smg.thompson", DisplayName = "Thompson", ImageUrl = "https://i.imgur.com/placeholder_thompson.png" } },
            { "mp5", new WeaponOption { ShortName = "smg.mp5", DisplayName = "MP5", ImageUrl = "https://i.imgur.com/placeholder_mp5.png" } },
            { "custom", new WeaponOption { ShortName = "smg.2", DisplayName = "Custom SMG", ImageUrl = "https://i.imgur.com/placeholder_custom.png" } },
            { "pump", new WeaponOption { ShortName = "shotgun.pump", DisplayName = "Pump Shotgun", ImageUrl = "https://i.imgur.com/placeholder_pump.png" } },
            { "double", new WeaponOption { ShortName = "shotgun.double", DisplayName = "Double Barrel", ImageUrl = "https://i.imgur.com/placeholder_double.png" } },
            { "compound", new WeaponOption { ShortName = "bow.compound", DisplayName = "Compound Bow", ImageUrl = "https://i.imgur.com/placeholder_bow.png" } },
            { "bolty", new WeaponOption { ShortName = "rifle.bolt", DisplayName = "Bolt Action", ImageUrl = "https://i.imgur.com/placeholder_bolt.png" } }
        };
        
        private class WeaponOption
        {
            public string ShortName { get; set; }
            public string DisplayName { get; set; }
            public string ImageUrl { get; set; }
        }

        // TEAMS
        private List<ulong> redTeam = new List<ulong>();
        private List<ulong> blueTeam = new List<ulong>();
        private List<ulong> blackTeam = new List<ulong>();
        private Dictionary<ulong, string> playerRoles = new Dictionary<ulong, string>();
        
        // TEAM CONFIG CLASS
        private class TeamConfig
        {
            public string Name { get; set; }
            public string Tag { get; set; }
            public string Color { get; set; }
            public string HexColor { get; set; }
        }
        
        // KILL FEED ENTRY CLASS
        private class KillFeedEntry
        {
            public string KillerName { get; set; }
            public string VictimName { get; set; }
            public string KillerTeam { get; set; }
            public string VictimTeam { get; set; }
            public string Message { get; set; }
            public float Timestamp { get; set; }
        }
        
        // CUSTOM TEAM/KIT SYSTEM
        private Dictionary<ulong, int> playerCurrency = new Dictionary<ulong, int>();
        private Dictionary<string, CustomTeam> customTeams = new Dictionary<string, CustomTeam>();
        private Dictionary<ulong, string> playerTeamAssignments = new Dictionary<ulong, string>();
        private HashSet<string> onlineCustomTeams = new HashSet<string>();
        
        // CUSTOM TEAM CLASS
        private class CustomTeam
        {
            public string TeamID { get; set; }
            public string TeamName { get; set; }
            public ulong OwnerID { get; set; }
            public List<ulong> Members { get; set; } = new List<ulong>();
            
            // Kit Skins (5-piece attire for regular players)
            public ulong TshirtSkin { get; set; }
            public ulong PantsSkin { get; set; }
            public ulong TorsoSkin { get; set; }
            public ulong FacemaskSkin { get; set; }
            public ulong ShoesSkin { get; set; }
            
            // Goalie Kit (heavy plate armor)
            public ulong GoalieJacketSkin { get; set; }
            public ulong GoaliePantsSkin { get; set; }
            
            public DateTime CreatedAt { get; set; }
        }
        
        // PLAYER CURRENCY CLASS
        private class PlayerCurrency
        {
            public ulong PlayerID { get; set; }
            public int Coins { get; set; }
        }

        // DATA FILE
        private const string DataFileName = "DeathmatchSoccer_Data";
        private const string CustomTeamsDataFile = "DeathmatchSoccer_CustomTeams";
        private const string PlayerCurrencyDataFile = "DeathmatchSoccer_Currency";

        // ==========================================
        // 2. DATA PERSISTENCE (SAVING/LOADING)
        // ==========================================
        private class ArenaData
        {
            // Dynamic Goal System
            public float G1x, G1y, G1z; // Goal 1 Position
            public float G2x, G2y, G2z; // Goal 2 Position
            public float G1qx, G1qy, G1qz, G1qw; // Goal 1 Rotation
            public float G2qx, G2qy, G2qz, G2qw; // Goal 2 Rotation
            public float Cx, Cy, Cz; // Center Pos
            public float Gw, Gh, Gd; // Goal Dimensions
            public float Lbx, Lby, Lbz; // Lobby spawn position
            public float Lsx, Lsy, Lsz; // Loser spawn position
        }

        private void SaveArenaData()
        {
            Puts($"[Data] Saving arena data to file");
            var data = new ArenaData
            {
                G1x = goal1Pos.x, G1y = goal1Pos.y, G1z = goal1Pos.z,
                G2x = goal2Pos.x, G2y = goal2Pos.y, G2z = goal2Pos.z,
                G1qx = goal1Rot.x, G1qy = goal1Rot.y, G1qz = goal1Rot.z, G1qw = goal1Rot.w,
                G2qx = goal2Rot.x, G2qy = goal2Rot.y, G2qz = goal2Rot.z, G2qw = goal2Rot.w,
                Cx = centerPos.x, Cy = centerPos.y, Cz = centerPos.z,
                Gw = GoalWidth, Gh = GoalHeight, Gd = GoalDepth,
                Lbx = lobbySpawnPos.x, Lby = lobbySpawnPos.y, Lbz = lobbySpawnPos.z,
                Lsx = loserSpawnPos.x, Lsy = loserSpawnPos.y, Lsz = loserSpawnPos.z
            };
            Puts($"[Data] Goal 1 saved: {goal1Pos}");
            Puts($"[Data] Goal 2 saved: {goal2Pos}");
            Puts($"[Data] Lobby spawn saved: {lobbySpawnPos}");
            Puts($"[Data] Loser spawn saved: {loserSpawnPos}");
            Interface.Oxide.DataFileSystem.WriteObject(DataFileName, data);
            Puts($"[Data] Arena data saved successfully to {DataFileName}");
        }

        private void LoadArenaData()
        {
            Puts($"[Data] Loading arena data from file");
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(DataFileName))
            {
                try
                {
                    var data = Interface.Oxide.DataFileSystem.ReadObject<ArenaData>(DataFileName);
                    if (data != null)
                    {
                        goal1Pos = new Vector3(data.G1x, data.G1y, data.G1z);
                        goal2Pos = new Vector3(data.G2x, data.G2y, data.G2z);
                        goal1Rot = new Quaternion(data.G1qx, data.G1qy, data.G1qz, data.G1qw);
                        goal2Rot = new Quaternion(data.G2qx, data.G2qy, data.G2qz, data.G2qw);
                        centerPos = new Vector3(data.Cx, data.Cy, data.Cz);
                        if (data.Gw > 0) { GoalWidth = data.Gw; GoalHeight = data.Gh; GoalDepth = data.Gd; }
                    
                        // Load spawn positions
                        lobbySpawnPos = new Vector3(data.Lbx, data.Lby, data.Lbz);
                        loserSpawnPos = new Vector3(data.Lsx, data.Lsy, data.Lsz);
                        
                        Puts($"[Data] Loaded Goal 1: {goal1Pos}");
                        Puts($"[Data] Loaded Goal 2: {goal2Pos}");
                        Puts($"[Data] Loaded lobby spawn: {lobbySpawnPos}");
                        Puts($"[Data] Loaded loser spawn: {loserSpawnPos}");
                        Puts("[Data] Arena data loaded successfully.");
                    }
                    else
                    {
                        Puts("[Data] ERROR: Arena data file exists but data is null");
                    }
                }
                catch (System.Exception ex)
                {
                    Puts($"[Data] ERROR: Failed to load arena data - {ex.Message}");
                }
            }
            else
            {
                Puts($"[Data] No arena data file found at {DataFileName}, using defaults");
            }
        }
        
        // CUSTOM TEAMS SAVE/LOAD
        private void SaveCustomTeams()
        {
            Interface.Oxide.DataFileSystem.WriteObject(CustomTeamsDataFile, customTeams);
            Puts($"[CustomTeams] Saved {customTeams.Count} custom teams");
        }
        
        private void LoadCustomTeams()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(CustomTeamsDataFile))
            {
                try
                {
                    customTeams = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, CustomTeam>>(CustomTeamsDataFile);
                    
                    // Rebuild player assignments
                    playerTeamAssignments.Clear();
                    foreach (var team in customTeams.Values)
                    {
                        foreach (var memberID in team.Members)
                        {
                            playerTeamAssignments[memberID] = team.TeamID;
                        }
                    }
                    
                    Puts($"[CustomTeams] Loaded {customTeams.Count} custom teams");
                }
                catch (System.Exception ex)
                {
                    Puts($"[CustomTeams] ERROR loading data: {ex.Message}");
                    customTeams = new Dictionary<string, CustomTeam>();
                }
            }
            else
            {
                Puts("[CustomTeams] No data file found, starting fresh");
                customTeams = new Dictionary<string, CustomTeam>();
            }
        }
        
        // PLAYER CURRENCY SAVE/LOAD
        private void SavePlayerCurrency()
        {
            Interface.Oxide.DataFileSystem.WriteObject(PlayerCurrencyDataFile, playerCurrency);
            Puts($"[Currency] Saved currency for {playerCurrency.Count} players");
        }
        
        private void LoadPlayerCurrency()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(PlayerCurrencyDataFile))
            {
                try
                {
                    playerCurrency = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>(PlayerCurrencyDataFile);
                    Puts($"[Currency] Loaded currency for {playerCurrency.Count} players");
                }
                catch (System.Exception ex)
                {
                    Puts($"[Currency] ERROR loading data: {ex.Message}");
                    playerCurrency = new Dictionary<ulong, int>();
                }
            }
            else
            {
                Puts("[Currency] No data file found, starting fresh");
                playerCurrency = new Dictionary<ulong, int>();
            }
        }
        
        // HELPER METHODS FOR CUSTOM TEAMS
        private BasePlayer FindPlayer(string nameOrID)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrID.ToLower()))
                    return player;
                if (player.UserIDString == nameOrID)
                    return player;
            }
            return null;
        }
        
        private string GetPlayerName(ulong playerID)
        {
            var player = BasePlayer.FindByID(playerID);
            if (player != null)
                return player.displayName;
            
            // Try to get from connected players list
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.userID == playerID)
                    return p.displayName;
            }
            
            return $"Player_{playerID}";
        }
        
        private void UpdateOnlineCustomTeams()
        {
            onlineCustomTeams.Clear();
            
            // Scan all custom teams and check if ANY member is online
            foreach (var kvp in customTeams)
            {
                var team = kvp.Value;
                bool hasOnlineMembers = false;
                
                foreach (var memberID in team.Members)
                {
                    var member = BasePlayer.FindByID(memberID);
                    if (member != null && member.IsConnected)
                    {
                        hasOnlineMembers = true;
                        break;
                    }
                }
                
                if (hasOnlineMembers)
                {
                    onlineCustomTeams.Add(team.TeamID);
                }
            }
        }

        // ==========================================
        // 3. LIFECYCLE
        // ==========================================
        void OnServerInitialized()
        {
            Puts("═══════════════════════════════════");
            Puts("DeathmatchSoccer Plugin Loaded!");
            Puts("Version: 6.0.0 - Custom Teams Update");
            Puts("═══════════════════════════════════");
            
            LoadArenaData(); // Load saved goals
            LoadCustomTeams(); // Load custom teams
            LoadPlayerCurrency(); // Load player currency
            UpdateOnlineCustomTeams(); // Track online custom teams from start
            
            if (ImageLibrary != null)
            {
                // Register 3 scoreboard backgrounds for different matchups
                ImageLibrary.Call("AddImage", ImgScoreboardBgRedBlue, "Soccer_Bar_BG_RedBlue");
                ImageLibrary.Call("AddImage", ImgScoreboardBgBlackRed, "Soccer_Bar_BG_BlackRed");
                ImageLibrary.Call("AddImage", ImgScoreboardBgBlueBlack, "Soccer_Bar_BG_BlueBlack");
                
                // Register 3 goal banner images for different matchups
                ImageLibrary.Call("AddImage", ImgGoalBannerRedBlue, "Soccer_Goal_Banner_RedBlue");
                ImageLibrary.Call("AddImage", ImgGoalBannerBlackRed, "Soccer_Goal_Banner_BlackRed");
                ImageLibrary.Call("AddImage", ImgGoalBannerBlueBlack, "Soccer_Goal_Banner_BlueBlack");
                
                // Register HOST badge image
                ImageLibrary.Call("AddImage", ImgHostBadge, "Host_Badge");
                
                // Register weapon vote images
                foreach (var weaponKey in weaponOptions.Keys)
                {
                    var weapon = weaponOptions[weaponKey];
                    ImageLibrary.Call("AddImage", weapon.ImageUrl, $"Weapon_{weaponKey}");
                }
            }
            
            Puts("DeathmatchSoccer: Hooks registered successfully");
            Puts("DeathmatchSoccer: OnEntityBuilt hook should now be active");
            
            // Start always-on goal visualization for all players
            timer.Repeat(1.5f, 0, () => {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null || !player.IsConnected) continue;
                    
                    if (redGoalPos != Vector3.zero) 
                    {
                        Color col = activeGoals["red"] ? Color.red : new Color(0.5f, 0, 0, 0.5f);
                        DrawGoal(player, redGoalPos, redGoalRot, col, 1.7f);
                    }
                    if (blueGoalPos != Vector3.zero) 
                    {
                        Color col = activeGoals["blue"] ? Color.blue : new Color(0, 0, 0.5f, 0.5f);
                        DrawGoal(player, blueGoalPos, blueGoalRot, col, 1.7f);
                    }
                    if (blackGoalPos1 != Vector3.zero) 
                    {
                        Color col = activeGoals["black1"] ? new Color(0.3f, 0.3f, 0.3f, 1f) : new Color(0.2f, 0.2f, 0.2f, 0.4f);
                        DrawGoal(player, blackGoalPos1, blackGoalRot1, col, 1.7f);
                    }
                    if (blackGoalPos2 != Vector3.zero) 
                    {
                        Color col = activeGoals["black2"] ? new Color(0.3f, 0.3f, 0.3f, 1f) : new Color(0.2f, 0.2f, 0.2f, 0.4f);
                        DrawGoal(player, blackGoalPos2, blackGoalRot2, col, 1.7f);
                    }
                }
            });
            
            Puts("Goal visualization started - always on for all players");
        }

        void Unload()
        {
            if (activeBall != null && !activeBall.IsDestroyed) activeBall.Kill();
            if (gameTimer != null) gameTimer.Destroy();
            if (tickerTimer != null) tickerTimer.Destroy();
            if (hudTimer != null) hudTimer.Destroy();
            if (debugTimer != null) debugTimer.Destroy();
            
            // Cleanup entity timers
            foreach (var timer in entityTimers.Values)
            {
                timer?.Destroy();
            }
            entityTimers.Clear();
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "SoccerScoreboard");
                CuiHelper.DestroyUi(player, "SoccerTicker");
                CuiHelper.DestroyUi(player, "GoalBanner");
                CuiHelper.DestroyUi(player, "BallRangeHUD");
                CuiHelper.DestroyUi(player, "RoleSelectUI");
                CuiHelper.DestroyUi(player, "TeamSelectUI");
                CuiHelper.DestroyUi(player, "LeashHUD");
                CuiHelper.DestroyUi(player, "HostUI");
            }
        }
        
        // Handle host disconnect - transfer host to another player
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            
            // Update online custom teams
            UpdateOnlineCustomTeams();
            
            // If host disconnected, select new host
            if (player.userID == hostPlayerId)
            {
                Puts($"[Host] Host {player.displayName} disconnected, selecting new host");
                hostPlayerId = 0; // Clear host
                SelectHost(); // Select new host
            }
        }

        // ==========================================
        // 4. ADMIN COMMANDS
        // ==========================================
        [ChatCommand("save_goals")]
        private void CmdSaveGoals(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            SaveArenaData();
            SendReply(player, "Arena Data Saved! Positions will persist.");
        }

        [ChatCommand("load_goals")]
        private void CmdLoadGoals(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            LoadArenaData();
            SendReply(player, "Arena Data Reloaded!");
            if (goal1Pos != Vector3.zero) DrawGoal(player, goal1Pos, goal1Rot, Color.cyan, 5f);
            if (goal2Pos != Vector3.zero) DrawGoal(player, goal2Pos, goal2Rot, Color.magenta, 5f);
        }

        [ChatCommand("start_match")]
        private void CmdStartMatch(BasePlayer player, string command, string[] args)
        {
            // Allow admins OR host to start match
            if (!player.IsAdmin && player.userID != hostPlayerId) 
            {
                SendReply(player, "Only admins or the host can start the match!");
                return;
            }
            if (centerPos == Vector3.zero) { SendReply(player, "Error: Set Center first!"); return; }
            
            // Start weapon voting first (match will start after both voting phases complete)
            PrintToChat("<color=#FFD700>========================================</color>");
            PrintToChat("<color=#FFD700>🎮 MATCH STARTING SOON!</color>");
            PrintToChat("<color=#FFD700>⚔️ WEAPON VOTE BEGINS NOW!</color>");
            PrintToChat("<color=#FFD700>========================================</color>");
            
            StartWeaponVoting();
            
            // Match will start after: Weapon Vote (30s) → Mode Vote (15s) → BeginActualMatch()
            // Do NOT call BeginActualMatch() here - it's called in EndModeVoting()
        }
        
        // Actually start the match after voting completes
        private void BeginActualMatch()
        {
            // Do NOT reset gameMode here - it's set by mode voting
            // gameMode is determined by EndModeVoting() before this is called
            
            scoreRed = 0; scoreBlue = 0; scoreBlack = 0;
            matchNumber = 1;
            rerollUsedThisMatch = false; // Reset reroll flag for new match
            
            if (rotationMode)
            {
                // Set initial rotation: blue vs red, black waits
                team1Playing = "blue";
                team2Playing = "red";
                waitingTeam = "black";
                
                // Activate red and blue goals, deactivate black goals
                activeGoals["red"] = true;
                activeGoals["blue"] = true;
                activeGoals["black1"] = false;
                activeGoals["black2"] = false;
                
                PrintToChat($"ROTATION MATCH #{matchNumber}: {teamConfigs[team1Playing].Tag} vs {teamConfigs[team2Playing].Tag}");
                PrintToChat($"Next Team: {teamConfigs[waitingTeam].Tag}");
            }
            else
            {
                // In 3-way mode, activate all original goals
                activeGoals["red"] = true;
                activeGoals["blue"] = true;
                activeGoals["black1"] = false;
                activeGoals["black2"] = false;
                PrintToChat("MATCH STARTED! 3 Teams Battle!");
            }
            
            gameActive = true; matchStarted = true; matchActive = true;
            
            SpawnBall();
            RefreshScoreboardAll();
            StartTicker();
            
            if (gameTimer != null) gameTimer.Destroy();
            gameTimer = timer.Repeat(0.05f, 0, CheckGoals);
            
            if (hudTimer != null) hudTimer.Destroy();
            hudTimer = timer.Repeat(0.5f, 0, HudLoop);

            PrintToChat("MATCH STARTED! 3 Teams Battle!");
            CallMiddleware("EVENT: MATCH_START. Score 0-0-0. 3-Team Battle.");
        }

        [ChatCommand("goal_size")]
        private void CmdSetSize(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args.Length < 3) { SendReply(player, $"Current: {GoalWidth}x{GoalHeight}x{GoalDepth}"); return; }
            if (float.TryParse(args[0], out float w) && float.TryParse(args[1], out float h) && float.TryParse(args[2], out float d))
            {
                GoalWidth = w; GoalHeight = h; GoalDepth = d;
                SendReply(player, $"Goal Updated: {w}x{h}x{d}. Use /save_goals to keep.");
            }
        }

        // DYNAMIC GOAL COMMANDS - Set Goal 1 and Goal 2
        [ChatCommand("set_goal1")] 
        private void CmdSetGoal1(BasePlayer p, string c, string[] a) 
        { 
            if(!p.IsAdmin) return;
            goal1Pos = p.transform.position; 
            goal1Rot = p.transform.rotation; 
            SendReply(p, "Goal 1 Set! Position saved. Use /save_goals to persist.");
            DrawGoal(p, goal1Pos, goal1Rot, Color.cyan, 5f);
        }
        
        [ChatCommand("set_goal2")] 
        private void CmdSetGoal2(BasePlayer p, string c, string[] a) 
        { 
            if(!p.IsAdmin) return;
            goal2Pos = p.transform.position; 
            goal2Rot = p.transform.rotation; 
            SendReply(p, "Goal 2 Set! Position saved. Use /save_goals to persist.");
            DrawGoal(p, goal2Pos, goal2Rot, Color.magenta, 5f);
        }
        [ChatCommand("set_center")] private void CmdSetCenter(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ centerPos=p.transform.position; SendReply(p, "Center Set."); }}
        [ChatCommand("set_lobby_spawn")] 
        private void CmdSetLobbySpawn(BasePlayer p, string c, string[] a) 
        { 
            if(!p.IsAdmin) return;
            
            Puts($"[LobbySpawn] Admin {p.displayName} running /set_lobby_spawn at position {p.transform.position}");
            
            // Try to find ground below player for better spawn position
            RaycastHit hit;
            Vector3 rayStart = p.transform.position + Vector3.up;
            Puts($"[LobbySpawn] Performing raycast from {rayStart} going down");
            
            // Try with default layers first
            if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                lobbySpawnPos = hit.point + new Vector3(0, 0.5f, 0); // Slightly above ground
                Puts($"[LobbySpawn] Ground found at {hit.point}, setting spawn 0.5m above at: {lobbySpawnPos}");
            }
            // Try with all layers as fallback
            else if (Physics.Raycast(rayStart, Vector3.down, out hit, 10f))
            {
                lobbySpawnPos = hit.point + new Vector3(0, 0.5f, 0);
                Puts($"[LobbySpawn] Ground found (all layers) at {hit.point}, setting spawn at: {lobbySpawnPos}");
            }
            // Use player position as final fallback
            else
            {
                lobbySpawnPos = p.transform.position;
                Puts($"[LobbySpawn] No ground found via raycast, using player position: {lobbySpawnPos}");
            }
            
            SaveArenaData();
            Puts($"[LobbySpawn] Lobby spawn saved to data file");
            SendReply(p, $"✓ Lobby spawn point set at {lobbySpawnPos}! Players will teleport here on join.");
            SendReply(p, $"⚠ Test with /test_lobby_spawn to verify!");
            Puts($"[LobbySpawn] Confirmation sent to admin");
        }
        
        [ChatCommand("set_loser_spawn")]
        private void CmdSetLoserSpawn(BasePlayer p, string c, string[] a)
        {
            if(!p.IsAdmin) return;
            
            // Get position slightly above ground to prevent spawning in air
            RaycastHit hit;
            if (Physics.Raycast(p.transform.position + Vector3.up, Vector3.down, out hit, 10f, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                loserSpawnPos = hit.point + new Vector3(0, 0.5f, 0); // Slightly above ground
            }
            else
            {
                loserSpawnPos = p.transform.position;
            }
            
            SaveArenaData();
            SendReply(p, $"✓ Loser spawn point set at {loserSpawnPos}! Losing team will teleport here after match.");
            Puts($"Loser spawn set to: {loserSpawnPos}");
        }
        
        [ChatCommand("reset_ball")]
        private void CmdResetBall(BasePlayer p, string c, string[] a) 
        { 
            // Allow admins OR host to reset ball
            if(!p.IsAdmin && p.userID != hostPlayerId)
            {
                SendReply(p, "Only admins or the host can reset the ball!");
                return;
            }
            SpawnBall(); 
            SendReply(p, "Ball Reset."); 
        }
        
        [ChatCommand("reroll_vote")]
        private void CmdRerollVote(BasePlayer p, string c, string[] a)
        {
            // Allow admins OR host to reroll vote
            if (!p.IsAdmin && p.userID != hostPlayerId)
            {
                SendReply(p, "<color=#FF0000>Only admins or the host can reroll the vote!</color>");
                return;
            }
            
            // Check if match is active
            if (!matchActive)
            {
                SendReply(p, "<color=#FF0000>Match is not active! Start a match first.</color>");
                return;
            }
            
            // Check if reroll has already been used this match
            if (rerollUsedThisMatch)
            {
                SendReply(p, "<color=#FF0000>ReRoll has already been used this match! Only one reroll per match.</color>");
                return;
            }
            
            // Check if voting is already active
            if (weaponVotingActive)
            {
                SendReply(p, "<color=#FF0000>Weapon voting is already active!</color>");
                return;
            }
            
            // Mark reroll as used
            rerollUsedThisMatch = true;
            
            // Clear previous voted weapon
            votedWeapon = null;
            
            // Announce reroll
            PrintToChat($"<color=#FFD700>════════════════════════════════════════</color>");
            PrintToChat($"<color=#FFD700>🔄 HOST REROLL!</color>");
            PrintToChat($"<color=#FFD700>⚔️ WEAPON VOTE BEGINS NOW!</color>");
            PrintToChat($"<color=#FFD700>════════════════════════════════════════</color>");
            
            // Start new weapon voting
            StartWeaponVoting();
            
            SendReply(p, "<color=#00FF00>✓ Weapon vote rerolled successfully!</color>");
        }
        
        [ChatCommand("test_lobby_spawn")]
        private void CmdTestLobbySpawn(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            
            if (lobbySpawnPos == Vector3.zero)
            {
                SendReply(player, "❌ Lobby spawn not set! Use /set_lobby_spawn first.");
                return;
            }
            
            SendReply(player, $"Testing lobby spawn teleport to {lobbySpawnPos}...");
            Puts($"[TestLobby] Admin {player.displayName} testing lobby spawn teleport");
            
            // Wake player if sleeping
            if (player.IsSleeping())
            {
                player.EndSleeping();
                Puts($"[TestLobby] Woke up sleeping player");
            }
            
            // Teleport
            player.Teleport(lobbySpawnPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", lobbySpawnPos);
            player.SendNetworkUpdateImmediate();
            
            SendReply(player, $"✓ Teleported to lobby spawn!");
            Puts($"[TestLobby] Teleport completed");
        }
        
        [ChatCommand("debug_entities")]
        private void CmdDebugEntities(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            
            SendReply(player, $"=== ENTITY TIMER DEBUG ===");
            SendReply(player, $"Total tracked timers: {entityTimers.Count}");
            
            int index = 0;
            foreach (var kvp in entityTimers)
            {
                SendReply(player, $"#{index++}: ID={kvp.Key}, Timer={kvp.Value != null && !kvp.Value.Destroyed}");
                if (index >= 10) break; // Limit to 10 entries
            }
            
            Puts($"[DebugEntities] Admin requested entity timer debug - {entityTimers.Count} tracked");
        }
        
        [ChatCommand("rotation")]
        private void CmdRotation(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            rotationMode = !rotationMode;
            SendReply(player, $"Rotation Mode: {(rotationMode ? "ON (2 play, 1 waits)" : "OFF (3-way battle)")}");
        }
        
        [ChatCommand("goal_debug")]
        private void CmdToggleDebug(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            SendReply(player, "Goal debug is now ALWAYS ON for all players!");
            SendReply(player, "Goals are automatically displayed with thick visualization 24/7.");
        }

        [ChatCommand("setskin")]
        private void CmdSetSkin(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (args.Length < 3)
            {
                SendReply(player, "Usage: /setskin <team> <item> <skinId>");
                SendReply(player, "Teams: blue, red, black");
                SendReply(player, "Items: tshirt, pants, torso, facemask, weapon, goaliepants, goaliejacket, goalieweapon");
                SendReply(player, "Example: /setskin blue tshirt 123456789");
                return;
            }
            
            string team = args[0].ToLower();
            string item = args[1].ToLower();
            if (!ulong.TryParse(args[2], out ulong skinId))
            {
                SendReply(player, "Invalid skin ID. Must be a number.");
                return;
            }
            
            if (!teamSkins.ContainsKey(team))
            {
                SendReply(player, "Invalid team. Use: blue, red, or black");
                return;
            }
            
            var skins = teamSkins[team];
            switch (item)
            {
                case "tshirt": skins.TshirtSkin = skinId; break;
                case "pants": skins.PantsSkin = skinId; break;
                case "torso": skins.TorsoSkin = skinId; break;
                case "facemask": skins.FacemaskSkin = skinId; break;
                case "weapon": skins.WeaponSkin = skinId; break;
                case "goaliepants": skins.GoaliePantsSkin = skinId; break;
                case "goaliejacket": skins.GoalieJacketSkin = skinId; break;
                case "goalieweapon": skins.GoalieWeaponSkin = skinId; break;
                default:
                    SendReply(player, "Invalid item name.");
                    return;
            }
            
            SendReply(player, $"Set {team} {item} skin to {skinId}");
        }

        [ChatCommand("showskins")]
        private void CmdShowSkins(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            SendReply(player, "=== TEAM SKIN IDs ===");
            foreach (var kvp in teamSkins)
            {
                var team = kvp.Key;
                var skins = kvp.Value;
                SendReply(player, $"--- {team.ToUpper()} ---");
                SendReply(player, $"Tshirt: {skins.TshirtSkin}");
                SendReply(player, $"Pants: {skins.PantsSkin}");
                SendReply(player, $"Torso: {skins.TorsoSkin}");
                SendReply(player, $"Facemask: {skins.FacemaskSkin}");
                SendReply(player, $"Thompson: {skins.WeaponSkin}");
                SendReply(player, $"Goalie Pants: {skins.GoaliePantsSkin}");
                SendReply(player, $"Goalie Jacket: {skins.GoalieJacketSkin}");
                SendReply(player, $"Goalie SPAS-12: {skins.GoalieWeaponSkin}");
            }
        }

        // ==========================================
        // 5. JOINING & TEAMS
        // ==========================================
        [ChatCommand("help")]
        private void CmdHelp(BasePlayer player, string command, string[] args)
        {
            ShowHelpMenu(player);
        }
        
        [ChatCommand("commands")]
        private void CmdCommands(BasePlayer player, string command, string[] args)
        {
            ShowHelpMenu(player);
        }
        
        private void ShowHelpMenu(BasePlayer player)
        {
            SendReply(player, "═══════════════════════════════════");
            SendReply(player, "DEATHMATCH SOCCER - COMMANDS");
            SendReply(player, "═══════════════════════════════════");
            
            SendReply(player, "--- PLAYER COMMANDS ---");
            SendReply(player, "/join [team] - Join a team (blue/red/black) or show team select UI");
            SendReply(player, "/leave - Leave your current team and return to lobby");
            SendReply(player, "/teams - Show team selection UI");
            SendReply(player, "/help or /commands - Show this help menu");
            
            if (player.IsAdmin)
            {
                SendReply(player, "\n--- ADMIN COMMANDS - Setup ---");
                SendReply(player, "/set_red - Set red team goal position");
                SendReply(player, "/set_blue - Set blue team goal position");
                SendReply(player, "/set_black1 - Set black goal 1 (at red position)");
                SendReply(player, "/set_black2 - Set black goal 2 (at blue position)");
                SendReply(player, "/set_center - Set ball spawn position");
                SendReply(player, "/set_lobby_spawn - Set lobby spawn point");
                SendReply(player, "/set_loser_spawn - Set spawn for losing team");
                SendReply(player, "/goal_size <width> <height> <depth> - Set goal dimensions");
                
                SendReply(player, "\n--- ADMIN COMMANDS - Match Control ---");
                SendReply(player, "/start_match - Start the match");
                SendReply(player, "/reset_ball - Reset ball to center");
                SendReply(player, "/rotation - Toggle rotation mode ON/OFF");
                
                SendReply(player, "\n--- ADMIN COMMANDS - Data ---");
                SendReply(player, "/save_goals - Save arena configuration");
                SendReply(player, "/load_goals - Reload arena configuration");
                
                SendReply(player, "\n--- ADMIN COMMANDS - Skins ---");
                SendReply(player, "/setskin <team> <item> <skinId> - Set team skin");
                SendReply(player, "/showskins - Display all team skins");
                
                SendReply(player, "\n--- ADMIN COMMANDS - Debug ---");
                SendReply(player, "/test_lobby_spawn - Test lobby spawn teleport");
                SendReply(player, "/debug_entities - Show tracked entity timers");
                SendReply(player, "/goal_debug - Toggle goal zone visualization");
            }
            
            SendReply(player, "═══════════════════════════════════");
        }
        
        [ChatCommand("teams")]
        private void CmdTeams(BasePlayer player, string command, string[] args)
        {
            ShowTeamSelectUI(player);
        }

        [ChatCommand("join")]
        private void CmdJoin(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) { 
                ShowTeamSelectUI(player);
                return; 
            }
            string team = args[0].ToLower();

            redTeam.Remove(player.userID);
            blueTeam.Remove(player.userID);
            blackTeam.Remove(player.userID);
            playerRoles.Remove(player.userID);
            ballRangeState.Remove(player.userID);
            
            CuiHelper.DestroyUi(player, "BallRangeHUD"); 
            CuiHelper.DestroyUi(player, "LeashHUD");
            CuiHelper.DestroyUi(player, "TeamSelectUI");

            if (team == "red") { redTeam.Add(player.userID); CheckRole(player, "red"); }
            else if (team == "blue") { blueTeam.Add(player.userID); CheckRole(player, "blue"); }
            else if (team == "black") { blackTeam.Add(player.userID); CheckRole(player, "black"); }
            else SendReply(player, "Invalid team. Use: blue, red, or black");
        }
        
        [ChatCommand("leave")]
        private void CmdLeave(BasePlayer player, string command, string[] args)
        {
            // Check if this player is the host before removing
            if (player.userID == hostPlayerId)
            {
                Puts($"[Host] Host {player.displayName} is leaving team, transferring host");
                hostPlayerId = 0; // Clear host
                SelectHost(); // Transfer host to another player
            }
            
            // Remove player from all teams
            bool wasOnTeam = redTeam.Remove(player.userID) || 
                            blueTeam.Remove(player.userID) || 
                            blackTeam.Remove(player.userID);
            
            if (!wasOnTeam)
            {
                SendReply(player, "You are not on any team.");
                return;
            }
            
            // Clean up player data
            playerRoles.Remove(player.userID);
            ballRangeState.Remove(player.userID);
            
            // Clean up UI
            CuiHelper.DestroyUi(player, "BallRangeHUD");
            CuiHelper.DestroyUi(player, "LeashHUD");
            CuiHelper.DestroyUi(player, "TeamSelectUI");
            CuiHelper.DestroyUi(player, "RoleSelectUI");
            CuiHelper.DestroyUi(player, "SoccerScoreboard");
            CuiHelper.DestroyUi(player, "SoccerTicker");
            
            // Strip inventory
            player.inventory.Strip();
            
            // Teleport to lobby if set
            if (lobbySpawnPos != Vector3.zero)
            {
                Puts($"[Leave] Player {player.displayName} left team, teleporting to lobby");
                
                // Wake player if sleeping
                if (player.IsSleeping())
                {
                    player.EndSleeping();
                }
                
                // Teleport to lobby
                player.Teleport(lobbySpawnPos);
                player.ClientRPCPlayer(null, player, "ForcePositionTo", lobbySpawnPos);
                player.SendNetworkUpdateImmediate();
                
                SendReply(player, "✓ Left team and returned to lobby!");
                
                // Show team select UI after a moment
                timer.Once(1f, () =>
                {
                    if (player != null && player.IsConnected)
                    {
                        ShowTeamSelectUI(player);
                    }
                });
            }
            else
            {
                SendReply(player, "✓ Left team! Use /join to select a new team.");
                Puts($"[Leave] Player {player.displayName} left team (no lobby spawn set)");
            }
        }
        
        // ==========================================
        // CUSTOM TEAM/KIT SYSTEM COMMANDS
        // ==========================================
        
        [ChatCommand("createteam")]
        private void CmdCreateTeam(BasePlayer player, string command, string[] args)
        {
            // Show UI instead of direct command
            ShowCreateTeamUI(player);
        }
        
        [ConsoleCommand("createteam_submit")]
        private void CmdCreateTeamSubmit(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            
            string teamName = arg.Args[0].Trim();
            
            if (string.IsNullOrEmpty(teamName))
            {
                SendReply(player, "❌ Please enter a team name!");
                return;
            }
            
            // Check if player already owns a team
            foreach (var team in customTeams.Values)
            {
                if (team.OwnerID == player.userID)
                {
                    SendReply(player, $"❌ You already own a team: {team.TeamName}");
                    CuiHelper.DestroyUi(player, "CreateTeamUI");
                    return;
                }
            }
            
            if (teamName.Length > 30)
            {
                SendReply(player, "❌ Team name must be 30 characters or less.");
                return;
            }
            
            // Check if name already exists
            foreach (var team in customTeams.Values)
            {
                if (team.TeamName.ToLower() == teamName.ToLower())
                {
                    SendReply(player, "❌ A team with that name already exists!");
                    return;
                }
            }
            
            // Create unique team ID
            string teamID = $"custom_{player.userID}_{DateTime.Now.Ticks}";
            
            var newTeam = new CustomTeam
            {
                TeamID = teamID,
                TeamName = teamName,
                OwnerID = player.userID,
                Members = new List<ulong> { player.userID },
                CreatedAt = DateTime.Now
            };
            
            customTeams[teamID] = newTeam;
            playerTeamAssignments[player.userID] = teamID;
            
            SaveCustomTeams();
            UpdateOnlineCustomTeams();
            
            CuiHelper.DestroyUi(player, "CreateTeamUI");
            
            SendReply(player, $"✓ Created team: {teamName}");
            SendReply(player, "Use /teamkit to configure your team's kit");
            SendReply(player, "Use /addplayer <name> to add members");
            
            Puts($"[CustomTeam] {player.displayName} created team: {teamName} (ID: {teamID})");
        }
        
        [ConsoleCommand("createteam_close")]
        private void CmdCreateTeamClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "CreateTeamUI");
        }
        
        [ChatCommand("addplayer")]
        private void CmdAddPlayer(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, "Usage: /addplayer <player name>");
                return;
            }
            
            // Find player's team
            if (!playerTeamAssignments.ContainsKey(player.userID))
            {
                SendReply(player, "You don't own a team. Use /createteam first.");
                return;
            }
            
            string teamID = playerTeamAssignments[player.userID];
            var team = customTeams[teamID];
            
            // Check if they are the owner
            if (team.OwnerID != player.userID)
            {
                SendReply(player, "Only the team owner can add players.");
                return;
            }
            
            // Check team size
            if (team.Members.Count >= 6)
            {
                SendReply(player, "Your team is full (max 6 players).");
                return;
            }
            
            // Find target player
            string targetName = string.Join(" ", args);
            BasePlayer target = FindPlayer(targetName);
            
            if (target == null)
            {
                SendReply(player, $"Player not found: {targetName}");
                return;
            }
            
            if (target.userID == player.userID)
            {
                SendReply(player, "You're already on your team!");
                return;
            }
            
            // Check if already on this team
            if (team.Members.Contains(target.userID))
            {
                SendReply(player, $"{target.displayName} is already on your team.");
                return;
            }
            
            // Check if on another custom team
            if (playerTeamAssignments.ContainsKey(target.userID))
            {
                SendReply(player, $"{target.displayName} is already on another custom team.");
                return;
            }
            
            // Add to team
            team.Members.Add(target.userID);
            playerTeamAssignments[target.userID] = teamID;
            
            SaveCustomTeams();
            
            SendReply(player, $"✓ Added {target.displayName} to {team.TeamName}");
            SendReply(target, $"✓ You've been added to {team.TeamName}!");
            
            Puts($"[CustomTeam] {player.displayName} added {target.displayName} to team {team.TeamName}");
        }
        
        [ChatCommand("kickplayer")]
        private void CmdKickPlayer(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, "Usage: /kickplayer <player name>");
                return;
            }
            
            // Find player's team
            if (!playerTeamAssignments.ContainsKey(player.userID))
            {
                SendReply(player, "You don't own a team.");
                return;
            }
            
            string teamID = playerTeamAssignments[player.userID];
            var team = customTeams[teamID];
            
            // Check if they are the owner
            if (team.OwnerID != player.userID)
            {
                SendReply(player, "Only the team owner can kick players.");
                return;
            }
            
            // Find target player
            string targetName = string.Join(" ", args);
            BasePlayer target = FindPlayer(targetName);
            
            if (target == null)
            {
                SendReply(player, $"Player not found: {targetName}");
                return;
            }
            
            if (target.userID == player.userID)
            {
                SendReply(player, "You can't kick yourself! Use /deleteteam to delete your team.");
                return;
            }
            
            // Check if on team
            if (!team.Members.Contains(target.userID))
            {
                SendReply(player, $"{target.displayName} is not on your team.");
                return;
            }
            
            // Remove from team
            team.Members.Remove(target.userID);
            playerTeamAssignments.Remove(target.userID);
            
            // Strip their kit if they're online and wearing it
            if (target.IsConnected)
            {
                target.inventory.Strip();
                SendReply(target, $"You've been kicked from {team.TeamName}");
            }
            
            SaveCustomTeams();
            
            SendReply(player, $"✓ Kicked {target.displayName} from {team.TeamName}");
            
            Puts($"[CustomTeam] {player.displayName} kicked {target.displayName} from team {team.TeamName}");
        }
        
        [ChatCommand("myteam")]
        private void CmdMyTeam(BasePlayer player, string command, string[] args)
        {
            if (!playerTeamAssignments.ContainsKey(player.userID))
            {
                SendReply(player, "You're not on a custom team.");
                SendReply(player, "Use /createteam <name> to create one!");
                return;
            }
            
            string teamID = playerTeamAssignments[player.userID];
            var team = customTeams[teamID];
            
            SendReply(player, $"═══ {team.TeamName} ═══");
            SendReply(player, $"Owner: {GetPlayerName(team.OwnerID)}");
            SendReply(player, $"Members ({team.Members.Count}/6):");
            
            foreach (var memberID in team.Members)
            {
                string memberName = GetPlayerName(memberID);
                bool isOnline = BasePlayer.FindByID(memberID) != null;
                SendReply(player, $"  • {memberName} {(isOnline ? "[ONLINE]" : "[OFFLINE]")}");
            }
            
            if (team.OwnerID == player.userID)
            {
                SendReply(player, "");
                SendReply(player, "Commands:");
                SendReply(player, "/addplayer <name> - Add a player");
                SendReply(player, "/kickplayer <name> - Kick a player");
                SendReply(player, "/teamkit - Configure team kit");
                SendReply(player, "/deleteteam - Delete your team");
            }
        }
        
        [ChatCommand("givecurrency")]
        private void CmdGiveCurrency(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "Only admins can give currency.");
                return;
            }
            
            if (args.Length < 2)
            {
                SendReply(player, "Usage: /givecurrency <player name> <amount>");
                return;
            }
            
            string targetName = args[0];
            BasePlayer target = FindPlayer(targetName);
            
            if (target == null)
            {
                SendReply(player, $"Player not found: {targetName}");
                return;
            }
            
            int amount;
            if (!int.TryParse(args[1], out amount))
            {
                SendReply(player, "Invalid amount.");
                return;
            }
            
            if (!playerCurrency.ContainsKey(target.userID))
            {
                playerCurrency[target.userID] = 0;
            }
            
            playerCurrency[target.userID] += amount;
            
            SavePlayerCurrency();
            
            SendReply(player, $"✓ Gave {amount} coins to {target.displayName}");
            SendReply(player, $"New balance: {playerCurrency[target.userID]} coins");
            
            SendReply(target, $"✓ You received {amount} coins from admin!");
            SendReply(target, $"Balance: {playerCurrency[target.userID]} coins");
            
            Puts($"[Currency] Admin {player.displayName} gave {amount} coins to {target.displayName}");
        }
        
        [ChatCommand("teamkit")]
        private void CmdTeamKit(BasePlayer player, string command, string[] args)
        {
            // Find player's team
            if (!playerTeamAssignments.ContainsKey(player.userID))
            {
                SendReply(player, "You're not on a custom team. Use /createteam first.");
                return;
            }
            
            string teamID = playerTeamAssignments[player.userID];
            var team = customTeams[teamID];
            
            // Check if they are the owner
            if (team.OwnerID != player.userID)
            {
                SendReply(player, "Only the team owner can configure the kit.");
                return;
            }
            
            if (args.Length < 2)
            {
                SendReply(player, "═══ Team Kit Configuration ═══");
                SendReply(player, "Usage: /teamkit <item> <skin ID>");
                SendReply(player, "");
                SendReply(player, "Items:");
                SendReply(player, "  tshirt - Long T-Shirt");
                SendReply(player, "  pants - Pants");
                SendReply(player, "  torso - Metal Plate Torso");
                SendReply(player, "  facemask - Metal Facemask");
                SendReply(player, "  shoes - Burlap Shoes");
                SendReply(player, "  goalie_jacket - Heavy Plate Jacket (Goalie)");
                SendReply(player, "  goalie_pants - Heavy Plate Pants (Goalie)");
                SendReply(player, "");
                SendReply(player, "Example: /teamkit tshirt 3619180626");
                SendReply(player, "");
                SendReply(player, "Current Kit:");
                SendReply(player, $"  Tshirt: {team.TshirtSkin}");
                SendReply(player, $"  Pants: {team.PantsSkin}");
                SendReply(player, $"  Torso: {team.TorsoSkin}");
                SendReply(player, $"  Facemask: {team.FacemaskSkin}");
                SendReply(player, $"  Shoes: {team.ShoesSkin}");
                SendReply(player, $"  Goalie Jacket: {team.GoalieJacketSkin}");
                SendReply(player, $"  Goalie Pants: {team.GoaliePantsSkin}");
                return;
            }
            
            string item = args[0].ToLower();
            ulong skinID;
            
            if (!ulong.TryParse(args[1], out skinID))
            {
                SendReply(player, "Invalid skin ID. Must be a number.");
                return;
            }
            
            bool updated = false;
            switch (item)
            {
                case "tshirt":
                    team.TshirtSkin = skinID;
                    updated = true;
                    break;
                case "pants":
                    team.PantsSkin = skinID;
                    updated = true;
                    break;
                case "torso":
                    team.TorsoSkin = skinID;
                    updated = true;
                    break;
                case "facemask":
                    team.FacemaskSkin = skinID;
                    updated = true;
                    break;
                case "shoes":
                    team.ShoesSkin = skinID;
                    updated = true;
                    break;
                case "goalie_jacket":
                    team.GoalieJacketSkin = skinID;
                    updated = true;
                    break;
                case "goalie_pants":
                    team.GoaliePantsSkin = skinID;
                    updated = true;
                    break;
                default:
                    SendReply(player, $"Unknown item: {item}");
                    SendReply(player, "Valid items: tshirt, pants, torso, facemask, shoes, goalie_jacket, goalie_pants");
                    return;
            }
            
            if (updated)
            {
                SaveCustomTeams();
                SendReply(player, $"✓ Updated {item} skin to {skinID}");
                SendReply(player, "Players will see the new kit on next spawn.");
                Puts($"[CustomTeam] {player.displayName} updated {item} skin for team {team.TeamName}");
            }
        }
        
        [ChatCommand("deleteteam")]
        private void CmdDeleteTeam(BasePlayer player, string command, string[] args)
        {
            // Find player's team
            if (!playerTeamAssignments.ContainsKey(player.userID))
            {
                SendReply(player, "You don't own a team.");
                return;
            }
            
            string teamID = playerTeamAssignments[player.userID];
            var team = customTeams[teamID];
            
            // Check if they are the owner
            if (team.OwnerID != player.userID)
            {
                SendReply(player, "Only the team owner can delete the team.");
                return;
            }
            
            // Remove all member assignments
            foreach (var memberID in team.Members)
            {
                playerTeamAssignments.Remove(memberID);
                
                // Strip kit if online
                var member = BasePlayer.FindByID(memberID);
                if (member != null && member.IsConnected)
                {
                    member.inventory.Strip();
                    SendReply(member, $"Team {team.TeamName} has been deleted by the owner.");
                }
            }
            
            // Remove team
            customTeams.Remove(teamID);
            SaveCustomTeams();
            
            SendReply(player, $"✓ Deleted team: {team.TeamName}");
            Puts($"[CustomTeam] {player.displayName} deleted team {team.TeamName}");
        }
        
        [ChatCommand("mycurrency")]
        private void CmdMyCurrency(BasePlayer player, string command, string[] args)
        {
            if (!playerCurrency.ContainsKey(player.userID))
            {
                SendReply(player, "Balance: 0 coins");
                SendReply(player, "Ask an admin to give you currency with /givecurrency");
                return;
            }
            
            int balance = playerCurrency[player.userID];
            SendReply(player, $"═══ YOUR CURRENCY ═══");
            SendReply(player, $"Balance: {balance} coins");
        }
        
        [ChatCommand("teams")]
        private void CmdTeamsList(BasePlayer player, string command, string[] args)
        {
            // If no args or "list", show team list UI
            if (args.Length == 0 || args[0].ToLower() == "list")
            {
                ShowTeamListUI(player);
                return;
            }
            
            // Otherwise show team selection
            ShowTeamSelectUI(player);
        }
        
        [ConsoleCommand("teamlist_close")]
        private void CmdTeamListClose(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "TeamListUI");
        }
        
        [ConsoleCommand("view_teamstats")]
        private void CmdViewTeamStats(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            
            string teamID = arg.Args[0];
            if (!customTeams.ContainsKey(teamID))
            {
                SendReply(player, "❌ Team not found!");
                return;
            }
            
            CuiHelper.DestroyUi(player, "TeamListUI");
            
            // Show detailed stats (use existing /teamstats logic)
            var team = customTeams[teamID];
            ShowTeamStatsDetailed(player, team);
        }
        
        [ChatCommand("teamstats")]
        private void CmdTeamStats(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, "Usage: /teamstats <teamname>");
                SendReply(player, "Or use /teams list to see all teams");
                return;
            }
            
            string searchName = string.Join(" ", args).ToLower();
            CustomTeam targetTeam = null;
            
            // Find team by name
            foreach (var team in customTeams.Values)
            {
                if (team.TeamName.ToLower().Contains(searchName))
                {
                    targetTeam = team;
                    break;
                }
            }
            
            if (targetTeam == null)
            {
                SendReply(player, $"Team not found: {searchName}");
                SendReply(player, "Use /teams list to see all teams");
                return;
            }
            
            // Display detailed stats
            SendReply(player, $"═══ {targetTeam.TeamName.ToUpper()} STATISTICS ═══");
            SendReply(player, $"Owner: {GetPlayerName(targetTeam.OwnerID)}");
            
            // Count online members
            int onlineCount = 0;
            foreach (var memberID in targetTeam.Members)
            {
                if (BasePlayer.FindByID(memberID) != null) onlineCount++;
            }
            
            SendReply(player, $"Members: {targetTeam.Members.Count}/6");
            SendReply(player, $"Online: {onlineCount}/{targetTeam.Members.Count}");
            SendReply(player, $"Status: {(onlineCount > 0 ? "ONLINE" : "offline")}");
            SendReply(player, "");
            
            SendReply(player, "MEMBERS:");
            foreach (var memberID in targetTeam.Members)
            {
                string memberName = GetPlayerName(memberID);
                bool isOnline = BasePlayer.FindByID(memberID) != null;
                string status = isOnline ? "✓ [ONLINE]" : "✗ [offline]";
                SendReply(player, $"  {status} {memberName}");
            }
            
            SendReply(player, "");
            SendReply(player, "KIT CONFIGURATION:");
            SendReply(player, $"  Tshirt: {(targetTeam.TshirtSkin > 0 ? targetTeam.TshirtSkin.ToString() : "default")}");
            SendReply(player, $"  Pants: {(targetTeam.PantsSkin > 0 ? targetTeam.PantsSkin.ToString() : "default")}");
            SendReply(player, $"  Torso: {(targetTeam.TorsoSkin > 0 ? targetTeam.TorsoSkin.ToString() : "default")}");
            SendReply(player, $"  Facemask: {(targetTeam.FacemaskSkin > 0 ? targetTeam.FacemaskSkin.ToString() : "default")}");
            SendReply(player, $"  Shoes: {(targetTeam.ShoesSkin > 0 ? targetTeam.ShoesSkin.ToString() : "default")}");
            SendReply(player, $"  Goalie Jacket: {(targetTeam.GoalieJacketSkin > 0 ? targetTeam.GoalieJacketSkin.ToString() : "default")}");
            SendReply(player, $"  Goalie Pants: {(targetTeam.GoaliePantsSkin > 0 ? targetTeam.GoaliePantsSkin.ToString() : "default")}");
            
            SendReply(player, "");
            SendReply(player, $"Created: {targetTeam.CreatedAt:MM/dd/yyyy HH:mm}");
        }
        
        private void ShowTeamStatsDetailed(BasePlayer player, CustomTeam team)
        {
            // Display detailed stats (same as above but called from UI)
            SendReply(player, $"═══ {team.TeamName.ToUpper()} STATISTICS ═══");
            SendReply(player, $"Owner: {GetPlayerName(team.OwnerID)}");
            
            // Count online members
            int onlineCount = 0;
            foreach (var memberID in team.Members)
            {
                if (BasePlayer.FindByID(memberID) != null) onlineCount++;
            }
            
            SendReply(player, $"Members: {team.Members.Count}/6");
            SendReply(player, $"Online: {onlineCount}/{team.Members.Count}");
            SendReply(player, $"Status: {(onlineCount > 0 ? "ONLINE" : "offline")}");
            SendReply(player, "");
            
            SendReply(player, "MEMBERS:");
            foreach (var memberID in team.Members)
            {
                string memberName = GetPlayerName(memberID);
                bool isOnline = BasePlayer.FindByID(memberID) != null;
                string status = isOnline ? "✓ [ONLINE]" : "✗ [offline]";
                SendReply(player, $"  {status} {memberName}");
            }
            
            SendReply(player, "");
            SendReply(player, "KIT CONFIGURATION:");
            SendReply(player, $"  Tshirt: {(team.TshirtSkin > 0 ? team.TshirtSkin.ToString() : "default")}");
            SendReply(player, $"  Pants: {(team.PantsSkin > 0 ? team.PantsSkin.ToString() : "default")}");
            SendReply(player, $"  Torso: {(team.TorsoSkin > 0 ? team.TorsoSkin.ToString() : "default")}");
            SendReply(player, $"  Facemask: {(team.FacemaskSkin > 0 ? team.FacemaskSkin.ToString() : "default")}");
            SendReply(player, $"  Shoes: {(team.ShoesSkin > 0 ? team.ShoesSkin.ToString() : "default")}");
            SendReply(player, $"  Goalie Jacket: {(team.GoalieJacketSkin > 0 ? team.GoalieJacketSkin.ToString() : "default")}");
            SendReply(player, $"  Goalie Pants: {(team.GoaliePantsSkin > 0 ? team.GoaliePantsSkin.ToString() : "default")}");
            
            SendReply(player, "");
            SendReply(player, $"Created: {team.CreatedAt:MM/dd/yyyy HH:mm}");
        }
        
        [ChatCommand("listteams")]
        private void CmdListTeams(BasePlayer player, string command, string[] args)
        {
            ShowTeamListUI(player);
        }

        [ConsoleCommand("select_team")]
        private void CmdSelectTeam(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            CuiHelper.DestroyUi(player, "TeamSelectUI");
            
            string teamInput = arg.Args[0];
            string team = teamInput.ToLower();
            
            // Remove from all default teams
            redTeam.Remove(player.userID);
            blueTeam.Remove(player.userID);
            blackTeam.Remove(player.userID);
            playerRoles.Remove(player.userID);
            ballRangeState.Remove(player.userID);
            
            CuiHelper.DestroyUi(player, "BallRangeHUD"); 
            CuiHelper.DestroyUi(player, "LeashHUD");

            // Handle default teams
            if (team == "red") { redTeam.Add(player.userID); CheckRole(player, "red"); }
            else if (team == "blue") { blueTeam.Add(player.userID); CheckRole(player, "blue"); }
            else if (team == "black") { blackTeam.Add(player.userID); CheckRole(player, "black"); }
            else
            {
                // Check if it's a custom team ID
                if (customTeams.ContainsKey(teamInput))
                {
                    var customTeam = customTeams[teamInput];
                    
                    // Check if team has online members
                    if (!onlineCustomTeams.Contains(teamInput))
                    {
                        SendReply(player, "❌ That custom team has no members online!");
                        timer.Once(0.5f, () => ShowTeamSelectUI(player));
                        return;
                    }
                    
                    // Add player to custom team assignment
                    if (playerTeamAssignments.ContainsKey(player.userID))
                    {
                        playerTeamAssignments[player.userID] = teamInput;
                    }
                    else
                    {
                        playerTeamAssignments.Add(player.userID, teamInput);
                    }
                    
                    SendReply(player, $"✓ Joined custom team: {customTeam.TeamName}");
                    CheckRole(player, "custom");
                }
            }
            
            // Check if we need to select a host
            SelectHost();
        }

        private void CheckRole(BasePlayer player, string team)
        {
            // Handle custom teams differently (they don't use default team lists)
            if (team == "custom")
            {
                ShowRoleUI(player, team);
                return;
            }
            
            // Count existing roles in the team
            int goalies = 0, strikers = 0, playmakers = 0, enforcers = 0;
            List<ulong> list = (team == "red") ? redTeam : (team == "blue") ? blueTeam : blackTeam;
            
            foreach(ulong id in list)
            {
                if(!playerRoles.ContainsKey(id)) continue;
                string role = playerRoles[id];
                if(role == "Goalie") goalies++;
                else if(role == "Striker") strikers++;
                else if(role == "Playmaker") playmakers++;
                else if(role == "Enforcer") enforcers++;
            }

            // Always show UI to let player choose their preferred role
            // This gives full flexibility for team composition
            ShowRoleUI(player, team);
        }

        [ConsoleCommand("select_role")]
        private void CmdSelectRole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            CuiHelper.DestroyUi(player, "RoleSelectUI");
            AssignRole(player, arg.Args[0]);
        }
        
        private void AssignRole(BasePlayer player, string role)
        {
            playerRoles[player.userID] = role;
            if (centerPos != Vector3.zero) {
                Vector3 goalPos;
                Quaternion goalRot;
                
                // Check if player is on a custom team
                if (playerTeamAssignments.ContainsKey(player.userID))
                {
                    // Custom team players spawn at center for now
                    // Will be handled by match system when teams are assigned to goals
                    goalPos = centerPos;
                    goalRot = Quaternion.identity;
                }
                else if (redTeam.Contains(player.userID))
                {
                    goalPos = redGoalPos;
                    goalRot = redGoalRot;
                }
                else if (blueTeam.Contains(player.userID))
                {
                    goalPos = blueGoalPos;
                    goalRot = blueGoalRot;
                }
                else // Black team
                {
                    // Determine which black goal position to use
                    if (activeGoals["black1"])
                    {
                        goalPos = blackGoalPos1;
                        goalRot = blackGoalRot1;
                    }
                    else if (activeGoals["black2"])
                    {
                        goalPos = blackGoalPos2;
                        goalRot = blackGoalRot2;
                    }
                    else
                    {
                        // Default to black1 if neither is active (shouldn't happen in normal gameplay)
                        goalPos = blackGoalPos1 != Vector3.zero ? blackGoalPos1 : blackGoalPos2;
                        goalRot = blackGoalPos1 != Vector3.zero ? blackGoalRot1 : blackGoalRot2;
                    }
                }
                
                Vector3 spawn = goalPos + (goalRot * Vector3.forward * 5f);
                player.Teleport(spawn);
            }
            GiveKit(player, role);
            UpdateScoreUI(player);
            
            // Show host UI if player is host
            if (player.userID == hostPlayerId)
            {
                ShowHostUI(player);
            }
        }

        // ==========================================
        // 6. KILL FEED SYSTEM
        // ==========================================
        
        // Funny R-rated kill messages
        private List<string> GetFunnyKillMessages()
        {
            return new List<string>
            {
                "just got absolutely demolished",
                "ate shit hard",
                "got their ass handed to them",
                "was fucking obliterated",
                "got sent to the shadow realm",
                "was turned into Swiss cheese",
                "got their skull cracked open",
                "got completely wrecked",
                "was murdered in cold blood",
                "got absolutely destroyed",
                "got their face rearranged",
                "was brutally executed",
                "got dumpstered",
                "was sent back to spawn",
                "got clapped",
                "got absolutely violated",
                "was sent to the afterlife",
                "got fucking annihilated",
                "got their head taken off",
                "was erased from existence"
            };
        }
        
        // Generate AI-powered funny/vulgar kill message
        private void GenerateAIKillMessage(string killerName, string victimName, string killerTeam, string victimTeam)
        {
            string prompt = $"KILLER: {killerName} murdered VICTIM: {victimName}. Write a short, hilarious roast about what {killerName} did to {victimName}. Focus on HOW {killerName} destroyed {victimName}. Be out-of-pocket funny. MAX 10 words. DO NOT repeat their names in the roast - we already know who killed who. Just describe the kill creatively.";
            
            var msg = new List<object> { 
                new { role = "system", content = "You create SHORT, hilarious kill descriptions without using player names. Player names are already shown separately. Describe HOW the kill happened in a funny way. Examples: 'absolutely deleted them', 'sent them to the shadow realm', 'turned them into ground beef'. Be creative and funny, not cringe. MAX 10 WORDS." }, 
                new { role = "user", content = prompt } 
            };
            
            var data = new { license = licenseKey, server_ip = ConVar.Server.ip, messages = msg, mode = "killfeed", user_input = prompt };
            
            webrequest.Enqueue(middlewareUrl, JsonConvert.SerializeObject(data), (c, r) => {
                if (c == 200) {
                    try {
                        var res = JsonConvert.DeserializeObject<OpenAIResponse>(r);
                        string aiMessage = res.choices[0].message.content.Trim();
                        
                        // Clean up any quotes, JSON formatting, and newlines
                        aiMessage = aiMessage.Replace("\"", "").Replace("```", "").Replace("\n", " ").Replace("\r", "").Trim();
                        
                        // Remove any JSON-like structures (curly braces, colons, etc)
                        if (aiMessage.Contains("{") || aiMessage.Contains("}") || aiMessage.Contains(":"))
                        {
                            // Extract only the message content if it's wrapped in JSON
                            var match = System.Text.RegularExpressions.Regex.Match(aiMessage, @"message:\s*([^,}]+)");
                            if (match.Success)
                            {
                                aiMessage = match.Groups[1].Value.Trim();
                            }
                            else
                            {
                                // If regex fails, just remove JSON characters
                                aiMessage = aiMessage.Replace("{", "").Replace("}", "").Replace("message:", "").Replace("offer_accepted:", "").Replace("items_to_take:", "").Replace("items_to_give:", "").Trim();
                                // Take only the first meaningful part before any remaining JSON
                                var parts = aiMessage.Split(new[] { "false", "true", "(", "[" }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 0)
                                {
                                    aiMessage = parts[0].Trim();
                                }
                            }
                        }
                        
                        // Remove any "message_to_player:" prefix
                        if (aiMessage.Contains("message_to_player:"))
                        {
                            aiMessage = aiMessage.Substring(aiMessage.IndexOf("message_to_player:") + 18).Trim();
                        }
                        
                        // Aggressively remove ALL instances of both player names from the message
                        // This prevents the AI from including names we'll add separately
                        aiMessage = System.Text.RegularExpressions.Regex.Replace(aiMessage, System.Text.RegularExpressions.Regex.Escape(killerName), "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        aiMessage = System.Text.RegularExpressions.Regex.Replace(aiMessage, System.Text.RegularExpressions.Regex.Escape(victimName), "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        
                        // Clean up extra spaces left by name removal
                        aiMessage = System.Text.RegularExpressions.Regex.Replace(aiMessage, @"\s+", " ").Trim();
                        
                        // Final cleanup - remove any trailing commas, numbers, or special characters
                        aiMessage = System.Text.RegularExpressions.Regex.Replace(aiMessage, @"\s+\d+\s*$", ""); // Remove trailing numbers
                        aiMessage = aiMessage.TrimEnd(',', '!', '.', ' ').Trim();
                        
                        // Make sure message starts cleanly (no leftover punctuation)
                        aiMessage = aiMessage.TrimStart(':', ',', ' ', '-').Trim();
                        
                        if (!aiMessage.EndsWith("!") && !aiMessage.EndsWith("."))
                        {
                            aiMessage += "!"; // Add exclamation for impact
                        }
                        
                        Puts($"[AI Kill Feed] Cleaned message: {aiMessage}");
                        
                        // Update the kill feed entry with the AI message instead of broadcasting to chat
                        // Increased window to 10 seconds since AI response can take 3-5 seconds
                        var recentEntry = killFeed.FirstOrDefault(e => 
                            e.KillerName == killerName && 
                            e.VictimName == victimName && 
                            (UnityEngine.Time.time - e.Timestamp) < 10f // Within last 10 seconds
                        );
                        
                        if (recentEntry != null)
                        {
                            recentEntry.Message = aiMessage;
                            Puts($"[AI Kill Feed] Updated kill feed entry with AI message");
                            // Refresh the UI for all players to show the new message
                            UpdateKillFeedForAll();
                        }
                        else
                        {
                            Puts($"[AI Kill Feed] Could not find recent kill feed entry to update");
                            Puts($"[AI Kill Feed] Current killFeed count: {killFeed.Count}");
                            Puts($"[AI Kill Feed] Looking for: Killer={killerName}, Victim={victimName}");
                            if (killFeed.Count > 0)
                            {
                                Puts($"[AI Kill Feed] Most recent entry: Killer={killFeed[0].KillerName}, Victim={killFeed[0].VictimName}, Age={(UnityEngine.Time.time - killFeed[0].Timestamp)}s");
                            }
                        }
                    } catch (Exception ex) { 
                        Puts($"[AI Kill Feed ERROR] Parse failed: {ex.Message}"); 
                        Puts($"[AI Kill Feed ERROR] Raw response: {r}");
                    }
                } else { 
                    Puts($"[AI Kill Feed ERROR] Code: {c} | {r}"); 
                }
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }
        
        // Add kill to feed
        private void AddKillToFeed(BasePlayer killer, BasePlayer victim, string deathType = "Player")
        {
            if (victim == null) return;
            
            Puts($"[KillFeed] Adding kill feed entry for {victim.displayName}, death type: {deathType}");
            
            string killerName = "Unknown";
            string killerTeam = "";
            string victimTeam = redTeam.Contains(victim.userID) ? "red" :
                               blueTeam.Contains(victim.userID) ? "blue" : 
                               blackTeam.Contains(victim.userID) ? "black" : "";
            
            string message = "";
            
            // Determine message based on death type
            if (deathType == "Fall")
            {
                // Fall damage messages
                var fallMessages = new List<string>
                {
                    "fell to their death",
                    "forgot how to land",
                    "faceplanted from a great height",
                    "learned about gravity the hard way",
                    "took the express route down",
                    "forgot their parachute",
                    "went splat"
                };
                message = fallMessages[UnityEngine.Random.Range(0, fallMessages.Count)];
                killerName = "Fall Damage";
            }
            else if (deathType == "Suicide")
            {
                // Suicide messages
                var suicideMessages = new List<string>
                {
                    "took the easy way out",
                    "said fuck this shit",
                    "quit the game",
                    "removed themselves from the match",
                    "rage quit IRL"
                };
                message = suicideMessages[UnityEngine.Random.Range(0, suicideMessages.Count)];
                killerName = "Suicide";
            }
            else if (deathType == "Unknown")
            {
                // Environmental/unknown death messages
                var unknownMessages = new List<string>
                {
                    "died somehow",
                    "got rekt by the environment",
                    "ceased to exist",
                    "had a mysterious accident",
                    "got deleted from reality"
                };
                message = unknownMessages[UnityEngine.Random.Range(0, unknownMessages.Count)];
                killerName = "The Environment";
            }
            else if (killer != null)
            {
                // Player kill - simple format: "killed"
                killerName = killer.displayName;
                killerTeam = redTeam.Contains(killer.userID) ? "red" :
                            blueTeam.Contains(killer.userID) ? "blue" : "black";
                
                // Simple kill message
                message = "killed";
            }
            else
            {
                // Fallback
                message = "died";
                killerName = "Unknown";
            }
            
            Puts($"[KillFeed] Killer: {killerName}, Victim: {victim.displayName}, Message: {message}");
            
            var entry = new KillFeedEntry
            {
                KillerName = killerName,
                VictimName = victim.displayName,
                KillerTeam = killerTeam,
                VictimTeam = victimTeam,
                Message = message,
                Timestamp = UnityEngine.Time.time
            };
            
            killFeed.Insert(0, entry);
            Puts($"[KillFeed] Kill feed now has {killFeed.Count} entries");
            
            // Keep only last 5 entries
            if (killFeed.Count > MAX_KILL_FEED_ENTRIES)
            {
                killFeed.RemoveRange(MAX_KILL_FEED_ENTRIES, killFeed.Count - MAX_KILL_FEED_ENTRIES);
            }
            
            // Update kill feed UI for all players
            Puts($"[KillFeed] Showing kill feed to all players");
            UpdateKillFeedForAll();
            
            // Auto-remove after 10 seconds
            timer.Once(10f, () => {
                if (killFeed.Contains(entry))
                {
                    killFeed.Remove(entry);
                    UpdateKillFeedForAll();
                }
            });
        }
        
        // Update kill feed UI for all players
        private void UpdateKillFeedForAll()
        {
            Puts($"[KillFeed] UpdateKillFeedForAll called - current feed has {killFeed.Count} entries");
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected)
                {
                    Puts($"[KillFeed] Skipping null or disconnected player");
                    continue;
                }
                
                // Show kill feed to ALL players (not just team members during match)
                // This ensures kill feed is visible even in lobby
                Puts($"[KillFeed] Showing kill feed to player: {player.displayName}");
                ShowKillFeed(player);
            }
        }
        
        // Show kill feed UI
        private void ShowKillFeed(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            Puts($"[KillFeed] ShowKillFeed called for {player.displayName}, feed entries: {killFeed.Count}");
            
            // Always destroy old UI first
            CuiHelper.DestroyUi(player, "KillFeedContainer");
            
            if (killFeed.Count == 0)
            {
                Puts($"[KillFeed] No kill feed entries to display");
                return;
            }
            
            var container = new CuiElementContainer();
            
            // Create main container first
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Hud", "KillFeedContainer");
            
            float yPos = 0.85f; // Start from top
            int index = 0;
            
            Puts($"[KillFeed] Creating UI elements for {killFeed.Count} entries");
            
            foreach (var entry in killFeed)
            {
                if (index >= MAX_KILL_FEED_ENTRIES) break;
                
                Puts($"[KillFeed] Entry {index}: {entry.KillerName} -> {entry.VictimName}");
                
                // Fade effect based on age
                float age = UnityEngine.Time.time - entry.Timestamp;
                float alpha = Mathf.Clamp(1f - (age / 15f), 0.85f, 1f); // Increased min alpha from 0.3 to 0.85, increased fade time
                
                // Get team colors - handle empty team strings
                string killerColor = "#FFFFFF"; // Default white
                string victimColor = "#FFFFFF"; // Default white
                
                if (!string.IsNullOrEmpty(entry.KillerTeam) && teamConfigs.ContainsKey(entry.KillerTeam))
                {
                    killerColor = teamConfigs[entry.KillerTeam].HexColor;
                }
                
                if (!string.IsNullOrEmpty(entry.VictimTeam) && teamConfigs.ContainsKey(entry.VictimTeam))
                {
                    victimColor = teamConfigs[entry.VictimTeam].HexColor;
                }
                
                // Replace dark colors (black team #333333) with bright visible colors
                if (killerColor == "#333333") killerColor = "#FFD700"; // Gold for black team
                if (victimColor == "#333333") victimColor = "#FFD700"; // Gold for black team
                
                Puts($"[KillFeed] Colors - Killer: {killerColor}, Victim: {victimColor}");
                
                // Single flat panel per entry - dark background for visibility
                string panelName = $"KillFeed_{index}";
                container.Add(new CuiPanel
                {
                    Image = { Color = $"0 0 0 {0.8f * alpha}" }, // Dark semi-transparent background
                    RectTransform = { AnchorMin = "0.02 " + (yPos - index * 0.065f - 0.06f), AnchorMax = "0.5 " + (yPos - index * 0.065f) } // Single line height: 0.06, spacing: 0.065
                }, "KillFeedContainer", panelName);
                
                // Simple format: "Killer killed Victim"
                string combinedText = $"{entry.KillerName} {entry.Message} {entry.VictimName}";
                
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = combinedText, 
                        FontSize = 16,
                        Align = TextAnchor.MiddleLeft,
                        Color = $"1 1 1 {alpha}", // White text for all
                        Font = "robotocondensed-bold.ttf"
                    },
                    RectTransform = { AnchorMin = "0.02 0.1", AnchorMax = "0.98 0.9" } // Full panel with margins
                }, panelName);
                
                index++;
            }
            
            Puts($"[KillFeed] Adding UI with {container.Count} elements to player {player.displayName}");
            CuiHelper.AddUi(player, container);
            Puts($"[KillFeed] UI added successfully");
        }
        
        // Helper to convert hex color to RGB
        private string GetColorFromHex(string hex)
        {
            hex = hex.Replace("#", "");
            int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return $"{r / 255f} {g / 255f} {b / 255f}";
        }

        // ==========================================
        // 7. KITS & HUD LOOPS
        // ==========================================
        
        // Helper method to give role-specific weapons (Soccer Mode only)
        private void GiveRoleWeapons(BasePlayer player, string role)
        {
            if (role == "Striker")
            {
                GiveItemWithSkin(player, "bat", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "pistol.python", 1, 0, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 128), player.inventory.containerMain);
            }
            else if (role == "Playmaker")
            {
                GiveItemWithSkin(player, "snowballgun", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "crossbow", 1, 0, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("arrow.wooden", 64), player.inventory.containerMain);
            }
            else if (role == "Enforcer")
            {
                GiveItemWithSkin(player, "pistol.nailgun", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "bat", 1, 0, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.nailgun.nails", 128), player.inventory.containerMain);
            }
            else if (role == "Goalie")
            {
                GiveItemWithSkin(player, "multiplegrenadelauncher", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "shotgun.spas12", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "weapon.mod.flashlight", 1, 0, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.grenadelauncher.he", 24), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.shotgun", 64), player.inventory.containerMain);
            }
        }
        
        // Helper method to give bonus weapon
        private void GiveBonusWeapon(BasePlayer player)
        {
            if (!matchActive || string.IsNullOrEmpty(votedWeapon))
                return;
            
            // Check if player already has this weapon (prevent duplicates)
            bool hasWeapon = false;
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item.info.shortname == votedWeapon)
                {
                    hasWeapon = true;
                    break;
                }
            }
            
            if (hasWeapon)
                return;
            
            // Give weapon to belt
            GiveItemWithSkin(player, votedWeapon, 1, 0, player.inventory.containerBelt);
            
            // Add appropriate ammo based on weapon type
            if (votedWeapon.Contains("rifle.ak") || votedWeapon.Contains("rifle.lr300") || votedWeapon.Contains("rifle.m249"))
            {
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.rifle", 128), player.inventory.containerMain);
            }
            else if (votedWeapon.Contains("smg."))
            {
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 128), player.inventory.containerMain);
            }
            else if (votedWeapon.Contains("shotgun."))
            {
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.shotgun", 64), player.inventory.containerMain);
            }
            else if (votedWeapon.Contains("pistol."))
            {
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 200), player.inventory.containerMain);
            }
            else if (votedWeapon.Contains("rifle.bolt"))
            {
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.rifle", 64), player.inventory.containerMain);
            }
            else if (votedWeapon.Contains("bow.compound"))
            {
                player.inventory.GiveItem(ItemManager.CreateByName("arrow.wooden", 64), player.inventory.containerMain);
            }
        }
        
        private void GiveKit(BasePlayer player, string role)
        {
            player.inventory.Strip();
            
            // If match hasn't started yet (still in voting phase), just strip inventory and return
            // Players will get their kits when match actually starts (after voting)
            if (!matchActive)
            {
                Puts($"[GiveKit] Match not active yet - player {player.displayName} will get kit when match starts");
                return;
            }
            
            // Check if player is on a custom team
            bool hasCustomTeam = playerTeamAssignments.ContainsKey(player.userID);
            CustomTeam customTeam = null;
            
            if (hasCustomTeam)
            {
                string teamID = playerTeamAssignments[player.userID];
                customTeam = customTeams[teamID];
                
                // Apply custom team kit (works in both Soccer and Normal mode)
                if (role != "Goalie")
                {
                    // Give custom team's 5-piece attire
                    GiveItemWithSkin(player, "tshirt.long", 1, customTeam.TshirtSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "pants", 1, customTeam.PantsSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "metal.plate.torso", 1, customTeam.TorsoSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "metal.facemask", 1, customTeam.FacemaskSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "burlap.shoes", 1, customTeam.ShoesSkin, player.inventory.containerWear);
                    
                    // Medical supplies
                    player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 5), player.inventory.containerMain);
                    player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                }
                else // Goalie gets custom goalie kit
                {
                    GiveItemWithSkin(player, "metal.facemask.hockey", 1, 0, player.inventory.containerWear);
                    GiveItemWithSkin(player, "heavy.plate.jacket", 1, customTeam.GoalieJacketSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "heavy.plate.pants", 1, customTeam.GoaliePantsSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "shoes.boots", 1, 0, player.inventory.containerWear);
                    
                    // Goalie supplies
                    player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 10), player.inventory.containerMain);
                    player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                }
                
                // Set health based on role
                if (role == "Striker")
                {
                    player.SetMaxHealth(100); 
                    player.health = 100;
                }
                else if (role == "Playmaker")
                {
                    player.SetMaxHealth(125); 
                    player.health = 125;
                }
                else if (role == "Enforcer")
                {
                    player.SetMaxHealth(150); 
                    player.health = 150;
                }
                else // Goalie
                {
                    player.SetMaxHealth(200); 
                    player.health = 200;
                }
                
                // Give Soccer Mode weapons if in Soccer Mode (custom teams keep abilities)
                if (gameMode == "soccer")
                {
                    GiveRoleWeapons(player, role);
                }
                
                // Give bonus weapon (always given regardless of mode)
                GiveBonusWeapon(player);
                
                Puts($"[GiveKit] Applied custom team kit for {player.displayName} ({customTeam.TeamName})");
                return; // Skip default team kits
            }
            
            // Determine which team the player is on (default teams: red, blue, black)
            string team = redTeam.Contains(player.userID) ? "red" : 
                         blueTeam.Contains(player.userID) ? "blue" : "black";
            TeamSkins skins = teamSkins[team];
            
            // ==========================================
            // NORMAL MODE: Custom skins + voted weapons only
            // ==========================================
            if (gameMode == "normal")
            {
                // Non-goalie roles get custom skinned attire
                if (role != "Goalie")
                {
                    // Give custom skinned attire
                    GiveItemWithSkin(player, "tshirt.long", 1, skins.TshirtSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "pants", 1, skins.PantsSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "metal.plate.torso", 1, skins.TorsoSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "metal.facemask", 1, skins.FacemaskSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "burlap.shoes", 1, skins.ShoesSkin, player.inventory.containerWear);
                    
                    // Give medical supplies and barricades (no weapons - only voted weapon)
                    player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 5), player.inventory.containerMain);
                    player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                }
                else // Goalie gets custom skinned heavy plate armor
                {
                    GiveItemWithSkin(player, "metal.facemask.hockey", 1, 0, player.inventory.containerWear);
                    GiveItemWithSkin(player, "heavy.plate.jacket", 1, skins.GoalieJacketSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "heavy.plate.pants", 1, skins.GoaliePantsSkin, player.inventory.containerWear);
                    GiveItemWithSkin(player, "shoes.boots", 1, 0, player.inventory.containerWear);
                    
                    // Goalie supplies (no weapons - only voted weapon)
                    player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 10), player.inventory.containerMain);
                    player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                }
                
                // Set health based on role
                if (role == "Striker")
                {
                    player.SetMaxHealth(100); 
                    player.health = 100;
                }
                else if (role == "Playmaker")
                {
                    player.SetMaxHealth(125); 
                    player.health = 125;
                }
                else if (role == "Enforcer")
                {
                    player.SetMaxHealth(150); 
                    player.health = 150;
                }
                else // Goalie
                {
                    player.SetMaxHealth(200); 
                    player.health = 200;
                }
            }
            // ==========================================
            // SOCCER MODE: SoccerWeapons.cs abilities
            // ==========================================
            else
            {
                // Team-specific hazmat suit (NOT for goalies - they get armor)
                string hazmatSuit = team == "red" ? "oubreak_scientist" :  // outbreak_scientist (red hazmat)
                                   team == "black" ? "hazmatsuit_scientist_nvgm" : // hazmatsuit_scientist_nvgm (black hazmat with NVG)
                                   "hazmat.krieg"; // blue team (krieg hazmat)
                
                // Give team hazmat suit to non-goalie roles
                if (role != "Goalie")
                {
                    GiveItemWithSkin(player, hazmatSuit, 1, 0, player.inventory.containerWear);
                }
                
                // Role-specific loadouts with SoccerWeapons.cs integration
                if (role == "Striker")
            {
                // Striker (Scorer): Speed, Scoring, Juking
                // Primary: Baseball Bat (Home Run - hits ball)
                // Secondary: Python Revolver (Phase Shift - teleport to ball)
                GiveItemWithSkin(player, "mace.baseballbat", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "pistol.python", 1, 0, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 200), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 5), player.inventory.containerMain);
                // Add 3 wooden barricades
                player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                player.SetMaxHealth(100); 
                player.health = 100;
            }
            else if (role == "Playmaker")
            {
                // Playmaker (Midfield): Ball Control, Passing, Setups
                // Primary: Snowball Gun (Magnet - pulls ball)
                // Secondary: Crossbow (Whistle - freezes ball)
                GiveItemWithSkin(player, "snowballgun", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "crossbow", 1, 0, player.inventory.containerBelt);
                // 1 snowball = 50 shots, so only give 1 snowball per respawn
                player.inventory.GiveItem(ItemManager.CreateByName("snowball", 1), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("arrow.wooden", 64), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 5), player.inventory.containerMain);
                // Add 3 wooden barricades
                player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                player.SetMaxHealth(125); 
                player.health = 125;
            }
            else if (role == "Enforcer")
            {
                // Enforcer (Defender): Tackling, Blocking, Clearing
                // Primary: Nailgun Pistol (Yellow Card - tackles players)
                // Secondary: Baseball Bat (Home Run - clears ball)
                GiveItemWithSkin(player, "pistol.nailgun", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "mace.baseballbat", 1, 0, player.inventory.containerBelt);
                // Limited to 6 nails to prevent spam
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.nailgun.nails", 6), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 7), player.inventory.containerMain);
                // Add 3 wooden barricades
                player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                player.SetMaxHealth(150); 
                player.health = 150;
            }
            else // Goalie
            {
                // Goalie (Support): Saving Goals, Healing Team
                // Primary: MGL (Medi-Launcher - heals teammates)
                // Secondary: SPAS-12 (standard shooting)
                // Wear: Heavy Plate Armor + Hockey Mask + Boots (no hazmat suit)
                
                // HEAD: Hockey Facemask
                GiveItemWithSkin(player, "metal.facemask.hockey", 1, 0, player.inventory.containerWear);
                // CHEST: Heavy Plate Jacket
                GiveItemWithSkin(player, "heavy.plate.jacket", 1, 0, player.inventory.containerWear);
                // PANTS: Heavy Plate Pants
                GiveItemWithSkin(player, "heavy.plate.pants", 1, 0, player.inventory.containerWear);
                // SHOES: Boots
                GiveItemWithSkin(player, "shoes.boots", 1, 0, player.inventory.containerWear);
                
                // Weapons
                GiveItemWithSkin(player, "multiplegrenadelauncher", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "shotgun.spas12", 1, 0, player.inventory.containerBelt);
                GiveItemWithSkin(player, "nightvisiongoggles", 1, 0, player.inventory.containerWear);
                
                // Ammo and supplies
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.grenadelauncher.he", 12), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.shotgun", 64), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 10), player.inventory.containerMain);
                // Add 3 wooden barricades
                player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                
                player.SetMaxHealth(200); 
                player.health = 200;
            }
            } // End of Soccer Mode
            
            // Try using Skins plugin if available for better skin loading
            if (Skins != null)
            {
                Puts($"[Skins] Using Skins plugin to refresh skins for {player.displayName}");
                Skins.Call("RefreshPlayer", player);
            }
            
            // Give bonus weapon if match is active and weapon was voted
            if (matchActive && !string.IsNullOrEmpty(votedWeapon))
            {
                // Check if player already has this weapon (prevent duplicates on respawn)
                bool hasWeapon = false;
                foreach (Item item in player.inventory.containerBelt.itemList)
                {
                    if (item.info.shortname == votedWeapon)
                    {
                        hasWeapon = true;
                        break;
                    }
                }
                
                if (!hasWeapon)
                {
                    // Give weapon to belt
                    GiveItemWithSkin(player, votedWeapon, 1, 0, player.inventory.containerBelt);
                    
                    // Add appropriate ammo based on weapon type
                    if (votedWeapon.Contains("rifle.ak") || votedWeapon.Contains("rifle.lr300") || votedWeapon.Contains("rifle.m249"))
                    {
                        player.inventory.GiveItem(ItemManager.CreateByName("ammo.rifle", 128), player.inventory.containerMain);
                    }
                    else if (votedWeapon.Contains("smg."))
                    {
                        player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 128), player.inventory.containerMain);
                    }
                    else if (votedWeapon.Contains("shotgun."))
                    {
                        player.inventory.GiveItem(ItemManager.CreateByName("ammo.shotgun", 64), player.inventory.containerMain);
                    }
                    else if (votedWeapon.Contains("pistol."))
                    {
                        player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 200), player.inventory.containerMain);
                    }
                    else if (votedWeapon.Contains("rifle.bolt"))
                    {
                        player.inventory.GiveItem(ItemManager.CreateByName("ammo.rifle", 64), player.inventory.containerMain);
                    }
                    else if (votedWeapon.Contains("bow.compound"))
                    {
                        player.inventory.GiveItem(ItemManager.CreateByName("arrow.wooden", 64), player.inventory.containerMain);
                    }
                }
            }
            
            // Force multiple network updates to ensure skins load properly
            // Update each container individually
            player.inventory.containerWear.MarkDirty();
            player.inventory.containerBelt.MarkDirty();
            player.inventory.containerMain.MarkDirty();
            
            // Force server update on inventory
            player.inventory.ServerUpdate(0f);
            
            // Send immediate network update
            player.SendNetworkUpdateImmediate();
            
            // Force client to re-render items with staggered updates
            timer.Once(0.1f, () => {
                if (player != null && player.IsConnected)
                {
                    player.SendNetworkUpdate();
                    
                    // Force each worn item to update
                    foreach (var item in player.inventory.containerWear.itemList)
                    {
                        item.MarkDirty();
                    }
                    foreach (var item in player.inventory.containerBelt.itemList)
                    {
                        item.MarkDirty();
                    }
                }
            });
            
            // Additional delayed update to ensure visibility
            timer.Once(0.5f, () => {
                if (player != null && player.IsConnected)
                {
                    player.SendNetworkUpdate();
                    player.SendNetworkUpdateImmediate();
                    
                    Puts($"[Skins] Final network update sent for {player.displayName}");
                }
            });
        }
        
        private void GiveItemWithSkin(BasePlayer player, string itemName, int amount, ulong skinId, ItemContainer container)
        {
            // Log the attempt
            Puts($"[GiveItemWithSkin] Creating {itemName} with skin {skinId} for {player.displayName}");
            
            Item item = ItemManager.CreateByName(itemName, amount, skinId);
            if (item != null)
            {
                // Explicitly set skin ID multiple times to ensure it sticks
                item.skin = skinId;
                
                // Log success
                Puts($"[GiveItemWithSkin] Item created successfully, skin ID: {item.skin}");
                
                // Add item to inventory
                if (player.inventory.GiveItem(item, container))
                {
                    // Set skin again after adding to inventory
                    item.skin = skinId;
                    
                    // Mark item as dirty to force network update
                    item.MarkDirty();
                    
                    // Force container update
                    container.MarkDirty();
                    
                    Puts($"[GiveItemWithSkin] Item added to inventory, final skin ID: {item.skin}");
                }
                else
                {
                    Puts($"[GiveItemWithSkin] WARNING: Failed to add item to inventory for {player.displayName}");
                    item.Remove();
                }
            }
            else
            {
                Puts($"[GiveItemWithSkin] ERROR: Failed to create item '{itemName}' for player {player.displayName}");
            }
        }

        private void HudLoop()
        {
            if (!matchStarted) return;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!playerRoles.ContainsKey(player.userID)) continue;
                string role = playerRoles[player.userID];
                bool isRed = redTeam.Contains(player.userID);
                bool isBlue = blueTeam.Contains(player.userID);
                bool isBlack = blackTeam.Contains(player.userID);

                // BALL RANGE HUD
                if (activeBall != null)
                {
                    float distBall = Vector3.Distance(player.transform.position, activeBall.transform.position);
                    bool inRange = distBall <= MaxKickDistance;

                    if (!ballRangeState.ContainsKey(player.userID) || ballRangeState[player.userID] != inRange)
                    {
                        ballRangeState[player.userID] = inRange;
                        DrawBallRangeUI(player, inRange);
                    }
                }

                // GOALIE LEASH
                // Skip leash check for waiting team goalies (they're not currently playing)
                if (role == "Goalie")
                {
                    // Determine which team the player is on
                    string playerTeam = isRed ? "red" : isBlue ? "blue" : isBlack ? "black" : "";
                    
                    // Skip leash check if this goalie is on the waiting team
                    // Waiting team players are "active" in match but not their turn to play
                    if (playerTeam == waitingTeam)
                    {
                        // No leash enforcement for waiting team - they can't return to goals that aren't in play
                        continue;
                    }
                    
                    Vector3 home;
                    if (isRed)
                    {
                        home = redGoalPos;
                    }
                    else if (isBlue)
                    {
                        home = blueGoalPos;
                    }
                    else // Black team
                    {
                        // Determine which black goal position to use
                        home = activeGoals["black1"] ? blackGoalPos1 : blackGoalPos2;
                    }
                    
                    if (home != Vector3.zero && Vector3.Distance(player.transform.position, home) > LeashRadius)
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, "RETURN TO GOAL!");
                        if (Vector3.Distance(player.transform.position, home) > LeashRadius + 5f)
                        {
                            HitInfo h = new HitInfo(); h.damageTypes.Add(global::Rust.DamageType.Radiation, 5f);
                            player.Hurt(h);
                        }
                    }
                }
            }
        }

        // ==========================================
        // 7. UI DRAWING
        // ==========================================
        private string GetImg(string name) { return (ImageLibrary != null) ? (string)ImageLibrary.Call("GetImage", name) : ""; }

        private void RefreshScoreboardAll() { foreach (var player in BasePlayer.activePlayerList) UpdateScoreUI(player); }

        private void UpdateScoreUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SoccerScoreboard");
            if (!matchStarted) return;

            var container = new CuiElementContainer();
            
            // Select scoreboard background based on current matchup
            string scoreboardKey = "Soccer_Bar_BG_RedBlue"; // Default
            
            if ((team1Playing == "black" && team2Playing == "red") || 
                (team1Playing == "red" && team2Playing == "black"))
            {
                scoreboardKey = "Soccer_Bar_BG_BlackRed";
            }
            else if ((team1Playing == "blue" && team2Playing == "black") || 
                     (team1Playing == "black" && team2Playing == "blue"))
            {
                scoreboardKey = "Soccer_Bar_BG_BlueBlack";
            }
            
            string imgId = GetImg(scoreboardKey);
            
            var panel = new CuiPanel { Image = { Color = "0 0 0 0.8" }, RectTransform = { AnchorMin = "0.25 0.88", AnchorMax = "0.75 0.98" }, CursorEnabled = false };
            if (!string.IsNullOrEmpty(imgId))
                container.Add(new CuiElement { Name = "SoccerScoreboard", Parent = "Overlay", Components = { new CuiRawImageComponent { Png = imgId }, new CuiRectTransformComponent { AnchorMin = "0.25 0.88", AnchorMax = "0.75 0.98" } } });
            else container.Add(panel, "Overlay", "SoccerScoreboard");

            if (rotationMode)
            {
                // Rotation Mode: Show only playing teams + waiting indicator
                container.Add(new CuiLabel { Text = { Text = $"MATCH #{matchNumber}", FontSize = 10, Align = TextAnchor.UpperCenter, Color = "1 1 0 0.8" }, RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" } }, "SoccerScoreboard");
                
                // Determine left and right teams based on consistent positioning
                string leftTeam, rightTeam;
                
                // Red vs Blue: Blue left, Red right
                if ((team1Playing == "blue" && team2Playing == "red") || (team1Playing == "red" && team2Playing == "blue"))
                {
                    leftTeam = "blue";
                    rightTeam = "red";
                }
                // Black vs Red: Black left, Red right
                else if ((team1Playing == "black" && team2Playing == "red") || (team1Playing == "red" && team2Playing == "black"))
                {
                    leftTeam = "black";
                    rightTeam = "red";
                }
                // Blue vs Black: Blue left, Black right
                else
                {
                    leftTeam = "blue";
                    rightTeam = "black";
                }
                
                var leftConfig = teamConfigs[leftTeam];
                var rightConfig = teamConfigs[rightTeam];
                int leftScore = GetTeamScore(leftTeam);
                int rightScore = GetTeamScore(rightTeam);
                
                // Left Team
                container.Add(new CuiLabel { Text = { Text = leftConfig.Tag, FontSize = 10, Align = TextAnchor.UpperCenter, Color = leftConfig.Color + " 0.8" }, RectTransform = { AnchorMin = "0.1 0.5", AnchorMax = "0.4 0.8" } }, "SoccerScoreboard");
                container.Add(new CuiLabel { Text = { Text = leftScore.ToString(), FontSize = 28, Align = TextAnchor.MiddleCenter, Color = leftConfig.Color + " 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.1 0.0", AnchorMax = "0.4 0.5" } }, "SoccerScoreboard");
                
                // VS
                container.Add(new CuiLabel { Text = { Text = "VS", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0.45 0.2", AnchorMax = "0.55 0.5" } }, "SoccerScoreboard");
                
                // Right Team
                container.Add(new CuiLabel { Text = { Text = rightConfig.Tag, FontSize = 10, Align = TextAnchor.UpperCenter, Color = rightConfig.Color + " 0.8" }, RectTransform = { AnchorMin = "0.6 0.5", AnchorMax = "0.9 0.8" } }, "SoccerScoreboard");
                container.Add(new CuiLabel { Text = { Text = rightScore.ToString(), FontSize = 28, Align = TextAnchor.MiddleCenter, Color = rightConfig.Color + " 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.6 0.0", AnchorMax = "0.9 0.5" } }, "SoccerScoreboard");
                
                // Waiting team indicator
                var waitingConfig = teamConfigs[waitingTeam];
                container.Add(new CuiLabel { Text = { Text = $"Waiting: {waitingConfig.Tag}", FontSize = 9, Align = TextAnchor.LowerCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.1" } }, "SoccerScoreboard");
            }
            else
            {
                // Normal 3-way mode
                var blueConfig = teamConfigs["blue"];
                var redConfig = teamConfigs["red"];
                var blackConfig = teamConfigs["black"];
                
                // Blue Team (Left)
                container.Add(new CuiLabel { Text = { Text = blueConfig.Tag, FontSize = 10, Align = TextAnchor.UpperCenter, Color = blueConfig.Color + " 0.8" }, RectTransform = { AnchorMin = "0.05 0.6", AnchorMax = "0.28 0.95" } }, "SoccerScoreboard");
                container.Add(new CuiLabel { Text = { Text = scoreBlue.ToString(), FontSize = 28, Align = TextAnchor.MiddleCenter, Color = blueConfig.Color + " 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.1", AnchorMax = "0.28 0.7" } }, "SoccerScoreboard");
                
                // Red Team (Middle)
                container.Add(new CuiLabel { Text = { Text = redConfig.Tag, FontSize = 10, Align = TextAnchor.UpperCenter, Color = redConfig.Color + " 0.8" }, RectTransform = { AnchorMin = "0.36 0.6", AnchorMax = "0.64 0.95" } }, "SoccerScoreboard");
                container.Add(new CuiLabel { Text = { Text = scoreRed.ToString(), FontSize = 28, Align = TextAnchor.MiddleCenter, Color = redConfig.Color + " 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.36 0.1", AnchorMax = "0.64 0.7" } }, "SoccerScoreboard");
                
                // Black Team (Right)
                container.Add(new CuiLabel { Text = { Text = blackConfig.Tag, FontSize = 10, Align = TextAnchor.UpperCenter, Color = "0.8 0.8 0.8 0.8" }, RectTransform = { AnchorMin = "0.72 0.6", AnchorMax = "0.95 0.95" } }, "SoccerScoreboard");
                container.Add(new CuiLabel { Text = { Text = scoreBlack.ToString(), FontSize = 28, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.72 0.1", AnchorMax = "0.95 0.7" } }, "SoccerScoreboard");
            }

            CuiHelper.AddUi(player, container);
        }

        private void DrawBallRangeUI(BasePlayer player, bool inRange)
        {
            CuiHelper.DestroyUi(player, "BallRangeHUD");
            var container = new CuiElementContainer();
            string color = inRange ? "0.2 0.8 0.2 0.8" : "0.8 0.2 0.2 0.8"; 
            string text = inRange ? "IN KICK RANGE" : "TOO FAR FROM BALL";
            container.Add(new CuiPanel { Image = { Color = color }, RectTransform = { AnchorMin = "0.4 0.12", AnchorMax = "0.6 0.15" }, CursorEnabled = false }, "Overlay", "BallRangeHUD");
            container.Add(new CuiLabel { Text = { Text = text, FontSize = 12, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "BallRangeHUD");
            CuiHelper.AddUi(player, container);
        }

        private void ShowGoalBanner(string team)
        {
            // Determine which banner image to use based on current matchup
            string bannerImage = "Soccer_Goal_Banner_RedBlue"; // Default: Red vs Blue
            
            if ((team1Playing == "black" && team2Playing == "red") || (team1Playing == "red" && team2Playing == "black"))
            {
                bannerImage = "Soccer_Goal_Banner_BlackRed";
            }
            else if ((team1Playing == "blue" && team2Playing == "black") || (team1Playing == "black" && team2Playing == "blue"))
            {
                bannerImage = "Soccer_Goal_Banner_BlueBlack";
            }
            
            string col = (team == "RED") ? "1 0.2 0.2" : (team == "BLUE") ? "0.2 0.4 1" : "0.8 0.8 0.8";
            string teamTag = (team == "RED") ? teamConfigs["red"].Tag : (team == "BLUE") ? teamConfigs["blue"].Tag : teamConfigs["black"].Tag;
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, "GoalBanner");
                var c = new CuiElementContainer();
                c.Add(new CuiPanel { Image = { Color = $"{col} 0.3", FadeIn = 0.1f }, RectTransform = { AnchorMin = "0 0.4", AnchorMax = "1 0.6" } }, "Overlay", "GoalBanner");
                c.Add(new CuiLabel { Text = { Text = $"{teamTag} SCORES!", FontSize = 50, Align = TextAnchor.MiddleCenter, Font = "permanentmarker.ttf", FadeIn=0.2f }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "GoalBanner");
                CuiHelper.AddUi(p, c);
                timer.Once(3f, () => CuiHelper.DestroyUi(p, "GoalBanner"));
            }
        }

        private void ShowRoleUI(BasePlayer player, string team)
        {
            CuiHelper.DestroyUi(player, "RoleSelectUI");
            var c = new CuiElementContainer();
            string p; // Declare variables once at method scope
            string strikerBtn;
            string playmakerBtn;
            string enforcerBtn;
            string goalieBtn;
            
            // Handle custom teams (don't use teamConfigs)
            if (team == "custom")
            {
                p = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.9" }, RectTransform = { AnchorMin = "0.25 0.2", AnchorMax = "0.75 0.8" }, CursorEnabled = true }, "Overlay", "RoleSelectUI");
                
                // Title - Generic styling for custom teams
                c.Add(new CuiLabel { Text = { Text = "CHOOSE ROLE - CUSTOM TEAM", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.98" } }, p);
                c.Add(new CuiLabel { Text = { Text = "(Your Custom Team)", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.80", AnchorMax = "1 0.88" } }, p);
                
                // Add role buttons (same as below)
                // Row 1: Striker and Playmaker
                strikerBtn = c.Add(new CuiButton { Button = { Command = "select_role Striker", Color = "0.2 0.6 0.2 0.9" }, Text = { Text = "STRIKER", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.52", AnchorMax = "0.48 0.75" } }, p);
                c.Add(new CuiLabel { Text = { Text = "⚡ Speed & Scoring", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, strikerBtn);
                c.Add(new CuiLabel { Text = { Text = "Bat • Python", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, strikerBtn);
                
                playmakerBtn = c.Add(new CuiButton { Button = { Command = "select_role Playmaker", Color = "0.2 0.4 0.8 0.9" }, Text = { Text = "PLAYMAKER", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.52 0.52", AnchorMax = "0.95 0.75" } }, p);
                c.Add(new CuiLabel { Text = { Text = "🎯 Ball Control", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, playmakerBtn);
                c.Add(new CuiLabel { Text = { Text = "Snowball • Crossbow", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, playmakerBtn);
                
                // Row 2: Enforcer and Goalie
                enforcerBtn = c.Add(new CuiButton { Button = { Command = "select_role Enforcer", Color = "0.6 0.2 0.6 0.9" }, Text = { Text = "ENFORCER", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.25", AnchorMax = "0.48 0.48" } }, p);
                c.Add(new CuiLabel { Text = { Text = "🛡️ Tackling & Defense", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, enforcerBtn);
                c.Add(new CuiLabel { Text = { Text = "Nailgun • Bat", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, enforcerBtn);
                
                goalieBtn = c.Add(new CuiButton { Button = { Command = "select_role Goalie", Color = "0.8 0.4 0.1 0.9" }, Text = { Text = "GOALIE", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.52 0.25", AnchorMax = "0.95 0.48" } }, p);
                c.Add(new CuiLabel { Text = { Text = "💊 Support & Healing", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, goalieBtn);
                c.Add(new CuiLabel { Text = { Text = "MGL • SPAS-12 • Flashlight", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, goalieBtn);
                
                // Instructions
                c.Add(new CuiLabel { Text = { Text = "Select your role for the match", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.10", AnchorMax = "0.95 0.22" } }, p);
                c.Add(new CuiLabel { Text = { Text = "⚽ Each role has unique weapons and abilities", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = "0.05 0.02", AnchorMax = "0.95 0.10" } }, p);
                
                CuiHelper.AddUi(player, c);
                return;
            }
            
            // Default teams use teamConfigs
            var config = teamConfigs[team];
            p = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.9" }, RectTransform = { AnchorMin = "0.25 0.2", AnchorMax = "0.75 0.8" }, CursorEnabled = true }, "Overlay", "RoleSelectUI");
            
            // Title
            c.Add(new CuiLabel { Text = { Text = $"CHOOSE ROLE - {config.Name}", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = config.Color + " 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.98" } }, p);
            c.Add(new CuiLabel { Text = { Text = $"({config.Tag})", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.80", AnchorMax = "1 0.88" } }, p);
            
            // Row 1: Striker and Playmaker
            // Striker - Green (Speed & Scoring)
            strikerBtn = c.Add(new CuiButton { Button = { Command = "select_role Striker", Color = "0.2 0.6 0.2 0.9" }, Text = { Text = "STRIKER", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.52", AnchorMax = "0.48 0.75" } }, p);
            c.Add(new CuiLabel { Text = { Text = "⚡ Speed & Scoring", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, strikerBtn);
            c.Add(new CuiLabel { Text = { Text = "Bat • Python", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, strikerBtn);
            
            // Playmaker - Blue (Ball Control)
            playmakerBtn = c.Add(new CuiButton { Button = { Command = "select_role Playmaker", Color = "0.2 0.4 0.8 0.9" }, Text = { Text = "PLAYMAKER", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.52 0.52", AnchorMax = "0.95 0.75" } }, p);
            c.Add(new CuiLabel { Text = { Text = "🎯 Ball Control", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, playmakerBtn);
            c.Add(new CuiLabel { Text = { Text = "Snowball • Crossbow", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, playmakerBtn);
            
            // Row 2: Enforcer and Goalie
            // Enforcer - Purple (Defending)
            enforcerBtn = c.Add(new CuiButton { Button = { Command = "select_role Enforcer", Color = "0.6 0.2 0.6 0.9" }, Text = { Text = "ENFORCER", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.25", AnchorMax = "0.48 0.48" } }, p);
            c.Add(new CuiLabel { Text = { Text = "🛡️ Tackling & Defense", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, enforcerBtn);
            c.Add(new CuiLabel { Text = { Text = "Nailgun • Bat", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, enforcerBtn);
            
            // Goalie - Orange (Support)
            goalieBtn = c.Add(new CuiButton { Button = { Command = "select_role Goalie", Color = "0.8 0.4 0.1 0.9" }, Text = { Text = "GOALIE", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.52 0.25", AnchorMax = "0.95 0.48" } }, p);
            c.Add(new CuiLabel { Text = { Text = "💊 Support & Healing", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } }, goalieBtn);
            c.Add(new CuiLabel { Text = { Text = "MGL • SPAS-12 • NVG", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.95" } }, goalieBtn);
            
            // Instructions
            c.Add(new CuiLabel { Text = { Text = "Select your role to begin!", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0 0.08", AnchorMax = "1 0.20" } }, p);
            
            CuiHelper.AddUi(player, c);
        }

        private void ShowTeamSelectUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "TeamSelectUI");
            var c = new CuiElementContainer();
            string panel = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.95" }, RectTransform = { AnchorMin = "0.20 0.20", AnchorMax = "0.80 0.80" }, CursorEnabled = true }, "Overlay", "TeamSelectUI");
            
            // Title
            c.Add(new CuiLabel { Text = { Text = "SELECT YOUR TEAM", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0 0.90", AnchorMax = "1 0.98" } }, panel);
            
            // Section titles
            c.Add(new CuiLabel { Text = { Text = "DEFAULT TEAMS", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0.05 0.83", AnchorMax = "0.48 0.89" } }, panel);
            
            // Check if there are online custom teams
            List<CustomTeam> onlineTeams = new List<CustomTeam>();
            foreach (var teamID in onlineCustomTeams)
            {
                if (customTeams.ContainsKey(teamID))
                {
                    onlineTeams.Add(customTeams[teamID]);
                }
            }
            
            if (onlineTeams.Count > 0)
            {
                c.Add(new CuiLabel { Text = { Text = "CUSTOM TEAMS", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0.52 0.83", AnchorMax = "0.95 0.89" } }, panel);
            }
            
            // LEFT COLUMN: Default teams
            // Blue Team Button
            var blueConfig = teamConfigs["blue"];
            string blueBtn = c.Add(new CuiButton { Button = { Command = "select_team blue", Color = blueConfig.Color + " 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = "0.05 0.60", AnchorMax = "0.48 0.80" } }, panel);
            c.Add(new CuiLabel { Text = { Text = blueConfig.Name, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.55", AnchorMax = "0.95 0.75" } }, blueBtn);
            c.Add(new CuiLabel { Text = { Text = $"[{blueConfig.Tag}]", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.95 0.55" } }, blueBtn);
            c.Add(new CuiLabel { Text = { Text = $"{blueTeam.Count} Players", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.95 0.35" } }, blueBtn);
            
            // Red Team Button
            var redConfig = teamConfigs["red"];
            string redBtn = c.Add(new CuiButton { Button = { Command = "select_team red", Color = redConfig.Color + " 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = "0.05 0.38", AnchorMax = "0.48 0.58" } }, panel);
            c.Add(new CuiLabel { Text = { Text = redConfig.Name, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.55", AnchorMax = "0.95 0.75" } }, redBtn);
            c.Add(new CuiLabel { Text = { Text = $"[{redConfig.Tag}]", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.95 0.55" } }, redBtn);
            c.Add(new CuiLabel { Text = { Text = $"{redTeam.Count} Players", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.95 0.35" } }, redBtn);
            
            // Black Team Button
            var blackConfig = teamConfigs["black"];
            string blackBtn = c.Add(new CuiButton { Button = { Command = "select_team black", Color = "0.3 0.3 0.3 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = "0.05 0.16", AnchorMax = "0.48 0.36" } }, panel);
            c.Add(new CuiLabel { Text = { Text = blackConfig.Name, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.55", AnchorMax = "0.95 0.75" } }, blackBtn);
            c.Add(new CuiLabel { Text = { Text = $"[{blackConfig.Tag}]", FontSize = 16, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.95 0.55" } }, blackBtn);
            c.Add(new CuiLabel { Text = { Text = $"{blackTeam.Count} Players", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.95 0.35" } }, blackBtn);
            
            // RIGHT COLUMN: Custom teams (dynamic)
            if (onlineTeams.Count > 0)
            {
                float startY = 0.80f;
                float height = 0.18f;
                float spacing = 0.02f;
                int index = 0;
                string[] teamColors = { "1 0.55 0", "0.58 0.44 0.86", "0 0.81 0.82", "1 0.84 0", "1 0.41 0.71", "0.20 0.80 0.20", "1 0.50 0.31", "0 0.50 0.50", "1 0 1", "1 1 0" };
                
                foreach (var team in onlineTeams)
                {
                    if (index >= 10) break; // Max 10 custom teams shown
                    
                    float minY = startY - (height + spacing) * (index + 1);
                    float maxY = startY - (height + spacing) * index - spacing;
                    
                    // Get online member count
                    int onlineCount = 0;
                    foreach (var memberID in team.Members)
                    {
                        var member = BasePlayer.FindByID(memberID);
                        if (member != null && member.IsConnected) onlineCount++;
                    }
                    
                    // Get owner name
                    string ownerName = "Unknown";
                    var owner = BasePlayer.FindByID(team.OwnerID);
                    if (owner != null && owner.IsConnected)
                    {
                        ownerName = owner.displayName;
                    }
                    
                    // Team button
                    string teamColor = teamColors[index % teamColors.Length];
                    string customBtn = c.Add(new CuiButton { Button = { Command = $"select_team {team.TeamID}", Color = teamColor + " 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = $"0.52 {minY}", AnchorMax = $"0.95 {maxY}" } }, panel);
                    c.Add(new CuiLabel { Text = { Text = team.TeamName, FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.05 0.55", AnchorMax = "0.95 0.80" } }, customBtn);
                    c.Add(new CuiLabel { Text = { Text = $"{onlineCount}/{team.Members.Count} online", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0.05 0.35", AnchorMax = "0.95 0.55" } }, customBtn);
                    c.Add(new CuiLabel { Text = { Text = $"Owner: {ownerName}", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0.05 0.15", AnchorMax = "0.95 0.35" } }, customBtn);
                    
                    index++;
                }
            }
            
            // Instructions
            c.Add(new CuiLabel { Text = { Text = "Click a team to join the battle!", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.06", AnchorMax = "1 0.12" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "Custom teams appear when members are online", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = "0 0.02", AnchorMax = "1 0.06" } }, panel);
            
            CuiHelper.AddUi(player, c);
        }
        
        // Show team creation UI
        private void ShowCreateTeamUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "CreateTeamUI");
            var c = new CuiElementContainer();
            string panel = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.95" }, RectTransform = { AnchorMin = "0.30 0.30", AnchorMax = "0.70 0.70" }, CursorEnabled = true }, "Overlay", "CreateTeamUI");
            
            // Title
            c.Add(new CuiLabel { Text = { Text = "CREATE CUSTOM TEAM", FontSize = 22, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.95" } }, panel);
            
            // Instructions
            c.Add(new CuiLabel { Text = { Text = "Enter a unique name for your team", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.75", AnchorMax = "1 0.82" } }, panel);
            
            // Input field
            c.Add(new CuiPanel { Image = { Color = "0.2 0.2 0.2 0.9" }, RectTransform = { AnchorMin = "0.1 0.60", AnchorMax = "0.9 0.72" } }, panel, "InputBg");
            c.Add(new CuiElement
            {
                Parent = "InputBg",
                Components =
                {
                    new CuiInputFieldComponent { FontSize = 16, Align = TextAnchor.MiddleLeft, Command = "createteam_submit", Text = "", CharsLimit = 30 },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0", AnchorMax = "0.95 1" }
                }
            });
            
            // Guidelines
            c.Add(new CuiLabel { Text = { Text = "Guidelines:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.1 0.50", AnchorMax = "0.9 0.57" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "• Maximum 30 characters", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.1 0.44", AnchorMax = "0.9 0.50" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "• Unique name required", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.1 0.38", AnchorMax = "0.9 0.44" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "• One team per owner", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.1 0.32", AnchorMax = "0.9 0.38" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "• You become the team owner", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.1 0.26", AnchorMax = "0.9 0.32" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "• Team size: 4-6 players", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.1 0.20", AnchorMax = "0.9 0.26" } }, panel);
            
            // Close button
            c.Add(new CuiButton { Button = { Command = "createteam_close", Color = "0.8 0.2 0.2 0.9" }, Text = { Text = "CLOSE", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.13" } }, panel);
            
            CuiHelper.AddUi(player, c);
        }
        
        // Show team list UI
        private void ShowTeamListUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "TeamListUI");
            var c = new CuiElementContainer();
            string panel = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.95" }, RectTransform = { AnchorMin = "0.25 0.15", AnchorMax = "0.75 0.85" }, CursorEnabled = true }, "Overlay", "TeamListUI");
            
            // Title
            c.Add(new CuiLabel { Text = { Text = "CUSTOM TEAMS", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 0.98" } }, panel);
            
            // Get all custom teams sorted by online status
            List<CustomTeam> allTeams = new List<CustomTeam>();
            foreach (var kvp in customTeams)
            {
                allTeams.Add(kvp.Value);
            }
            
            // Sort: online teams first
            allTeams.Sort((a, b) =>
            {
                bool aOnline = onlineCustomTeams.Contains(a.TeamID);
                bool bOnline = onlineCustomTeams.Contains(b.TeamID);
                if (aOnline && !bOnline) return -1;
                if (!aOnline && bOnline) return 1;
                return 0;
            });
            
            if (allTeams.Count == 0)
            {
                c.Add(new CuiLabel { Text = { Text = "No custom teams exist yet", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.40", AnchorMax = "1 0.50" } }, panel);
                c.Add(new CuiLabel { Text = { Text = "Use /createteam to create your own team!", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = "0 0.35", AnchorMax = "1 0.40" } }, panel);
            }
            else
            {
                float startY = 0.88f;
                float height = 0.14f;
                float spacing = 0.01f;
                int index = 0;
                
                foreach (var team in allTeams)
                {
                    if (index >= 5) break; // Max 5 teams shown (scrolling could be added later)
                    
                    float minY = startY - (height + spacing) * (index + 1);
                    float maxY = startY - (height + spacing) * index - spacing;
                    
                    bool isOnline = onlineCustomTeams.Contains(team.TeamID);
                    string statusIndicator = isOnline ? "🟢" : "🔴";
                    string statusText = isOnline ? "ONLINE" : "offline";
                    string bgColor = isOnline ? "0.1 0.3 0.1 0.7" : "0.2 0.2 0.2 0.5";
                    
                    // Get online member count
                    int onlineCount = 0;
                    foreach (var memberID in team.Members)
                    {
                        var member = BasePlayer.FindByID(memberID);
                        if (member != null && member.IsConnected) onlineCount++;
                    }
                    
                    // Get owner name
                    string ownerName = "Unknown";
                    var owner = BasePlayer.FindByID(team.OwnerID);
                    if (owner != null)
                    {
                        ownerName = owner.displayName;
                    }
                    
                    // Team panel
                    string teamPanel = c.Add(new CuiPanel { Image = { Color = bgColor }, RectTransform = { AnchorMin = $"0.02 {minY}", AnchorMax = $"0.98 {maxY}" } }, panel);
                    
                    // Team name and status
                    c.Add(new CuiLabel { Text = { Text = $"{statusIndicator} {team.TeamName} ({statusText})", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.02 0.65", AnchorMax = "0.70 0.95" } }, teamPanel);
                    
                    // Owner info
                    c.Add(new CuiLabel { Text = { Text = $"Owner: {ownerName}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0.02 0.40", AnchorMax = "0.50 0.65" } }, teamPanel);
                    
                    // Member count
                    c.Add(new CuiLabel { Text = { Text = $"Members: {onlineCount}/{team.Members.Count} online", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0.02 0.15", AnchorMax = "0.50 0.40" } }, teamPanel);
                    
                    // Created date
                    string createdDate = team.CreatedAt.ToString("MM/dd/yyyy");
                    c.Add(new CuiLabel { Text = { Text = $"Created: {createdDate}", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0.52 0.40", AnchorMax = "0.98 0.65" } }, teamPanel);
                    
                    // View stats button
                    c.Add(new CuiButton { Button = { Command = $"view_teamstats {team.TeamID}", Color = "0.2 0.6 0.8 0.9" }, Text = { Text = "View Stats", FontSize = 11, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.75 0.15", AnchorMax = "0.97 0.35" } }, teamPanel);
                    
                    index++;
                }
                
                // Show count
                c.Add(new CuiLabel { Text = { Text = $"Total Teams: {allTeams.Count}", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0 0.06", AnchorMax = "1 0.10" } }, panel);
            }
            
            // Close button
            c.Add(new CuiButton { Button = { Command = "teamlist_close", Color = "0.8 0.2 0.2 0.9" }, Text = { Text = "CLOSE", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0.35 0.01", AnchorMax = "0.65 0.05" } }, panel);
            
            CuiHelper.AddUi(player, c);
        }
        
        // Show host UI panel with privileges
        private void ShowHostUI(BasePlayer player)
        {
            if (player.userID != hostPlayerId) return; // Only show to host
            
            CuiHelper.DestroyUi(player, "HostUI");
            var c = new CuiElementContainer();
            
            // Main HOST panel - larger to accommodate buttons
            // Positioned in top-right corner
            c.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { AnchorMin = "0.80 0.82", AnchorMax = "0.99 0.98" },
                CursorEnabled = false
            }, "Overlay", "HostUI");
            
            // HOST badge image at top
            string hostBadgeImg = GetImg("Host_Badge");
            c.Add(new CuiElement
            {
                Parent = "HostUI",
                Name = "HostBadge",
                Components =
                {
                    new CuiRawImageComponent { Png = hostBadgeImg },
                    new CuiRectTransformComponent { AnchorMin = "0.05 0.70", AnchorMax = "0.95 0.95" }
                }
            });
            
            // Button 1: Start Match
            c.Add(new CuiButton
            {
                Button = { Command = "ds.start_match", Color = "0.2 0.8 0.2 0.9" },
                RectTransform = { AnchorMin = "0.05 0.48", AnchorMax = "0.95 0.64" },
                Text = { Text = "START MATCH", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "HostUI", "HostStartBtn");
            
            // Button 2: Reset Ball
            c.Add(new CuiButton
            {
                Button = { Command = "ds.reset_ball", Color = "0.8 0.6 0.2 0.9" },
                RectTransform = { AnchorMin = "0.05 0.30", AnchorMax = "0.95 0.46" },
                Text = { Text = "RESET BALL", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "HostUI", "HostResetBtn");
            
            // Button 3: ReRoll Vote
            c.Add(new CuiButton
            {
                Button = { Command = "ds.reroll_vote", Color = "0.6 0.2 0.8 0.9" },
                RectTransform = { AnchorMin = "0.05 0.12", AnchorMax = "0.95 0.28" },
                Text = { Text = "REROLL VOTE", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "HostUI", "HostRerollBtn");
            
            // Info text at bottom
            c.Add(new CuiLabel
            {
                Text = { Text = "HOST CONTROLS", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.05 0.02", AnchorMax = "0.95 0.10" }
            }, "HostUI");
            
            CuiHelper.AddUi(player, c);
        }
        
        // Select host - called when first player joins
        private void SelectHost()
        {
            // If host already exists and is online, keep them
            if (hostPlayerId != 0)
            {
                var existingHost = BasePlayer.FindByID(hostPlayerId);
                if (existingHost != null && existingHost.IsConnected)
                {
                    return; // Keep existing host
                }
            }
            
            // Find new host from team players
            List<ulong> allPlayers = new List<ulong>();
            allPlayers.AddRange(redTeam);
            allPlayers.AddRange(blueTeam);
            allPlayers.AddRange(blackTeam);
            
            if (allPlayers.Count == 0) 
            {
                hostPlayerId = 0;
                return;
            }
            
            // Select random online player as host
            var onlinePlayers = new List<BasePlayer>();
            foreach (var playerId in allPlayers)
            {
                var player = BasePlayer.FindByID(playerId);
                if (player != null && player.IsConnected)
                {
                    onlinePlayers.Add(player);
                }
            }
            
            if (onlinePlayers.Count > 0)
            {
                var newHost = onlinePlayers[UnityEngine.Random.Range(0, onlinePlayers.Count)];
                hostPlayerId = newHost.userID;
                PrintToChat($"<color=#FFD700>🎮 {newHost.displayName} is now the HOST!</color>");
                SendReply(newHost, "<color=#FFD700>You are now the HOST! You can use /start_match and /reset_ball</color>");
                ShowHostUI(newHost);
            }
        }
        
        // Console command handlers for Host UI buttons
        [ConsoleCommand("ds.start_match")]
        private void CmdStartMatchConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            // Check if player is admin or host
            if (!player.IsAdmin && player.userID != hostPlayerId)
            {
                SendReply(player, "Only admins or the host can start the match!");
                return;
            }
            
            // Call existing start match logic
            CmdStartMatch(player, "start_match", new string[0]);
        }
        
        [ConsoleCommand("ds.reset_ball")]
        private void CmdResetBallConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            // Check if player is admin or host
            if (!player.IsAdmin && player.userID != hostPlayerId)
            {
                SendReply(player, "Only admins or the host can reset the ball!");
                return;
            }
            
            // Call existing reset ball logic
            CmdResetBall(player, "reset_ball", new string[0]);
        }
        
        [ConsoleCommand("ds.reroll_vote")]
        private void CmdRerollVoteConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            // Check if player is admin or host
            if (!player.IsAdmin && player.userID != hostPlayerId)
            {
                SendReply(player, "<color=#FF0000>Only admins or the host can reroll the vote!</color>");
                return;
            }
            
            // Call existing reroll vote logic
            CmdRerollVote(player, "reroll_vote", new string[0]);
        }
        
        // ==========================================
        // WEAPON VOTING SYSTEM
        // ==========================================
        
        // Start weapon voting before match begins
        private void StartWeaponVoting()
        {
            weaponVotingActive = true;
            weaponVotes.Clear();
            playersWhoVoted.Clear();
            votedWeapon = null;
            votingTimeRemaining = 30;
            
            // Initialize vote counts
            foreach (var weapon in weaponOptions.Keys)
            {
                weaponVotes[weapon] = 0;
            }
            
            // Show voting UI to all team players
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (redTeam.Contains(p.userID) || blueTeam.Contains(p.userID) || blackTeam.Contains(p.userID))
                {
                    ShowWeaponVotingUI(p);
                }
            }
            
            PrintToChat("<color=#FFD700>⚔️ WEAPON VOTE STARTED! Click your choice - 30 seconds!</color>");
            
            // Start countdown timer
            if (votingTimer != null) votingTimer.Destroy();
            votingTimer = timer.Repeat(1.0f, 30, () =>
            {
                votingTimeRemaining--;
                
                // Update UI with remaining time
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (redTeam.Contains(p.userID) || blueTeam.Contains(p.userID) || blackTeam.Contains(p.userID))
                    {
                        UpdateWeaponVotingUI(p);
                    }
                }
                
                if (votingTimeRemaining <= 0)
                {
                    EndWeaponVoting();
                }
            });
        }
        
        // Show weapon voting UI with 10 weapon options in 2 rows of 5
        private void ShowWeaponVotingUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "WeaponVotingUI");
            var c = new CuiElementContainer();
            
            // Main panel
            string panel = c.Add(new CuiPanel 
            { 
                Image = { Color = "0 0 0 0.95" }, 
                RectTransform = { AnchorMin = "0.2 0.3", AnchorMax = "0.8 0.7" }, 
                CursorEnabled = true 
            }, "Overlay", "WeaponVotingUI");
            
            // Title
            c.Add(new CuiLabel 
            { 
                Text = { Text = $"⚔️ VOTE FOR BONUS WEAPON - {votingTimeRemaining}s", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 0.84 0 1" }, 
                RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.98" } 
            }, panel);
            
            // Instructions
            c.Add(new CuiLabel 
            { 
                Text = { Text = "Click a weapon to vote! Winner will be given to all players in addition to role kits.", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, 
                RectTransform = { AnchorMin = "0 0.78", AnchorMax = "1 0.86" } 
            }, panel);
            
            // Weapon buttons - 2 rows of 5
            int index = 0;
            float rowHeight = 0.32f;
            float buttonWidth = 0.18f;
            float buttonHeight = 0.28f;
            float spacing = 0.02f;
            
            foreach (var kvp in weaponOptions)
            {
                string weaponKey = kvp.Key;
                var weapon = kvp.Value;
                int row = index / 5;
                int col = index % 5;
                
                float minX = 0.05f + (col * (buttonWidth + spacing));
                float maxX = minX + buttonWidth;
                float minY = 0.42f - (row * (rowHeight + spacing));
                float maxY = minY + buttonHeight;
                
                // Button
                string btnPanel = c.Add(new CuiButton 
                { 
                    Button = { Command = $"vote_weapon {weaponKey}", Color = "0.2 0.2 0.2 0.9" }, 
                    Text = { Text = "", FontSize = 1 }, 
                    RectTransform = { AnchorMin = $"{minX} {minY}", AnchorMax = $"{maxX} {maxY}" } 
                }, panel);
                
                // Weapon icon (using ImageLibrary URL-based images)
                string weaponImgId = GetImg($"Weapon_{weaponKey}");
                if (!string.IsNullOrEmpty(weaponImgId))
                {
                    c.Add(new CuiElement
                    {
                        Parent = btnPanel,
                        Components =
                        {
                            new CuiRawImageComponent { Png = weaponImgId },
                            new CuiRectTransformComponent { AnchorMin = "0.1 0.35", AnchorMax = "0.9 0.85" }
                        }
                    });
                }
                
                // Weapon name
                c.Add(new CuiLabel 
                { 
                    Text = { Text = weapon.DisplayName, FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, 
                    RectTransform = { AnchorMin = "0 0.05", AnchorMax = "1 0.25" } 
                }, btnPanel);
                
                // Vote count
                int voteCount = weaponVotes.ContainsKey(weaponKey) ? weaponVotes[weaponKey] : 0;
                c.Add(new CuiLabel 
                { 
                    Text = { Text = $"{voteCount} votes", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "0 1 0 1", Font = "robotocondensed-bold.ttf" }, 
                    RectTransform = { AnchorMin = "0 0.82", AnchorMax = "1 0.95" } 
                }, btnPanel);
                
                index++;
            }
            
            // Show vote status
            if (playersWhoVoted.Contains(player.userID))
            {
                c.Add(new CuiLabel 
                { 
                    Text = { Text = "✓ YOU VOTED!", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 1 0 1", Font = "robotocondensed-bold.ttf" }, 
                    RectTransform = { AnchorMin = "0 0.02", AnchorMax = "1 0.10" } 
                }, panel);
            }
            
            CuiHelper.AddUi(player, c);
        }
        
        // Update voting UI (for countdown timer)
        private void UpdateWeaponVotingUI(BasePlayer player)
        {
            ShowWeaponVotingUI(player); // Just refresh the entire UI
        }
        
        // End weapon voting and determine winner
        private void EndWeaponVoting()
        {
            weaponVotingActive = false;
            
            if (votingTimer != null)
            {
                votingTimer.Destroy();
                votingTimer = null;
            }
            
            // Close voting UI for all players
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, "WeaponVotingUI");
            }
            
            // Determine winner
            string winnerKey = null;
            int maxVotes = 0;
            
            foreach (var kvp in weaponVotes)
            {
                if (kvp.Value > maxVotes)
                {
                    maxVotes = kvp.Value;
                    winnerKey = kvp.Key;
                }
            }
            
            if (winnerKey != null && weaponOptions.ContainsKey(winnerKey))
            {
                votedWeapon = weaponOptions[winnerKey].ShortName;
                string displayName = weaponOptions[winnerKey].DisplayName;
                PrintToChat($"<color=#FFD700>🏆 {displayName} WINS with {maxVotes} votes!</color>");
                PrintToChat($"<color=#FFD700>All players will receive {displayName} + their role kit!</color>");
            }
            else
            {
                // No votes or tie - pick random
                var keys = new List<string>(weaponOptions.Keys);
                string randomKey = keys[UnityEngine.Random.Range(0, keys.Count)];
                votedWeapon = weaponOptions[randomKey].ShortName;
                string displayName = weaponOptions[randomKey].DisplayName;
                PrintToChat($"<color=#FFD700>🎲 No clear winner! Random selection: {displayName}</color>");
            }
            
            // Start Mode Voting (Soccer vs Normal)
            StartModeVoting();
        }
        
        private void DistributeBonusWeapon()
        {
            Puts($"[DistributeBonusWeapon] Starting distribution - votedWeapon: {votedWeapon}, matchActive: {matchActive}");
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                // Determine player's team
                string team = "none";
                if (redTeam.Contains(player.userID)) team = "red";
                else if (blueTeam.Contains(player.userID)) team = "blue";
                else if (blackTeam.Contains(player.userID)) team = "black";
                
                if (team == "none") continue; // Only give to team players
                
                // Check if player has a role assigned
                if (!playerRoles.ContainsKey(player.userID)) continue;
                string role = playerRoles[player.userID];
                
                // Give the player their kit (now that match is active)
                // GiveKit() will also handle bonus weapon distribution
                Puts($"[DistributeBonusWeapon] Giving kit to {player.displayName} (Role: {role})");
                GiveKit(player, role);
            }
        }
        
        // Console command for weapon voting
        [ConsoleCommand("vote_weapon")]
        private void CmdVoteWeapon(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (!weaponVotingActive)
            {
                SendReply(player, "<color=#FF0000>Weapon voting is not active!</color>");
                return;
            }
            
            if (playersWhoVoted.Contains(player.userID))
            {
                SendReply(player, "<color=#FF0000>You already voted!</color>");
                return;
            }
            
            string weaponKey = arg.GetString(0);
            if (!weaponOptions.ContainsKey(weaponKey))
            {
                SendReply(player, "<color=#FF0000>Invalid weapon choice!</color>");
                return;
            }
            
            // Register vote
            weaponVotes[weaponKey]++;
            playersWhoVoted.Add(player.userID);
            
            string weaponName = weaponOptions[weaponKey].DisplayName;
            SendReply(player, $"<color=#00FF00>✓ Voted for {weaponName}!</color>");
            
            // Update UI for this player
            ShowWeaponVotingUI(player);
            
            // Update UI for all other players (to show updated vote counts)
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.userID != player.userID && (redTeam.Contains(p.userID) || blueTeam.Contains(p.userID) || blackTeam.Contains(p.userID)))
                {
                    ShowWeaponVotingUI(p);
                }
            }
        }

        // ==========================================
        // MODE VOTING SYSTEM
        // ==========================================
        private void StartModeVoting()
        {
            modeVotingActive = true;
            soccerModeVotes = 0;
            normalModeVotes = 0;
            playersWhoVotedMode.Clear();
            modeVotingTimeRemaining = 15;
            
            PrintToChat("<color=#FFD700>⚽ MODE VOTE: Soccer Mode (abilities) or Normal Mode (skins)?</color>");
            PrintToChat("<color=#00FF00>🎮 Soccer Mode:</color> Role weapons + SoccerWeapons abilities");
            PrintToChat("<color=#FF9933>👕 Normal Mode:</color> Custom team skins + voted weapon only");
            
            // Show mode voting UI to all players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID))
                {
                    ShowModeVotingUI(player);
                }
            }
            
            // Start countdown timer
            modeVotingTimer = timer.Repeat(1f, modeVotingTimeRemaining, () =>
            {
                modeVotingTimeRemaining--;
                
                if (modeVotingTimeRemaining <= 0)
                {
                    EndModeVoting();
                }
                else
                {
                    // Update UI for all players with new time
                    foreach (var p in BasePlayer.activePlayerList)
                    {
                        if (redTeam.Contains(p.userID) || blueTeam.Contains(p.userID) || blackTeam.Contains(p.userID))
                        {
                            ShowModeVotingUI(p);
                        }
                    }
                }
            });
        }
        
        private void ShowModeVotingUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            
            // Main panel
            container.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.95" },
                RectTransform = { AnchorMin = "0.25 0.3", AnchorMax = "0.75 0.7" },
                CursorEnabled = true
            }, "Overlay", "ModeVotingUI");
            
            // Title
            container.Add(new CuiLabel
            {
                Text = { Text = $"MODE VOTE - {modeVotingTimeRemaining}s", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 0.84 0 1" },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, "ModeVotingUI");
            
            // Soccer Mode Button (Left)
            container.Add(new CuiButton
            {
                Button = { Command = "vote_mode soccer", Color = "0 0.8 0 0.8" },
                RectTransform = { AnchorMin = "0.05 0.4", AnchorMax = "0.45 0.75" },
                Text = { Text = $"⚽ SOCCER MODE\n\nRole Weapons + Abilities\nHazmat Suits\n\n{soccerModeVotes} Votes", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "ModeVotingUI");
            
            // Normal Mode Button (Right)
            container.Add(new CuiButton
            {
                Button = { Command = "vote_mode normal", Color = "1 0.6 0 0.8" },
                RectTransform = { AnchorMin = "0.55 0.4", AnchorMax = "0.95 0.75" },
                Text = { Text = $"👕 NORMAL MODE\n\nCustom Team Skins\nVoted Weapon Only\n\n{normalModeVotes} Votes", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, "ModeVotingUI");
            
            // Info text
            container.Add(new CuiLabel
            {
                Text = { Text = "Click to vote for your preferred mode!", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0 0.15", AnchorMax = "1 0.3" }
            }, "ModeVotingUI");
            
            CuiHelper.DestroyUi(player, "ModeVotingUI");
            CuiHelper.AddUi(player, container);
        }
        
        private void EndModeVoting()
        {
            modeVotingActive = false;
            
            if (modeVotingTimer != null)
            {
                modeVotingTimer.Destroy();
                modeVotingTimer = null;
            }
            
            // Close voting UI for all players
            foreach (var p in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(p, "ModeVotingUI");
            }
            
            // Determine winner
            if (soccerModeVotes > normalModeVotes)
            {
                gameMode = "soccer";
                PrintToChat($"<color=#00FF00>⚽ SOCCER MODE WINS with {soccerModeVotes} votes!</color>");
                PrintToChat("<color=#00FF00>Players will receive role weapons + SoccerWeapons abilities!</color>");
            }
            else if (normalModeVotes > soccerModeVotes)
            {
                gameMode = "normal";
                PrintToChat($"<color=#FF9933>👕 NORMAL MODE WINS with {normalModeVotes} votes!</color>");
                PrintToChat("<color=#FF9933>Players will receive custom team skins + voted weapon only!</color>");
            }
            else
            {
                // Tie - default to soccer mode
                gameMode = "soccer";
                PrintToChat("<color=#FFD700>🎲 TIE! Defaulting to Soccer Mode.</color>");
            }
            
            // NOW start the actual match (after both voting phases complete)
            PrintToChat("<color=#FFD700>========================================</color>");
            PrintToChat("<color=#FFD700>🎮 MATCH STARTING NOW!</color>");
            PrintToChat("<color=#FFD700>========================================</color>");
            
            BeginActualMatch();
            
            // Distribute bonus weapon and kits (players will get kits on respawn)
            DistributeBonusWeapon();
        }
        
        [ConsoleCommand("vote_mode")]
        private void CmdVoteMode(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            
            if (!modeVotingActive)
            {
                SendReply(player, "<color=#FF0000>Mode voting is not active!</color>");
                return;
            }
            
            if (playersWhoVotedMode.Contains(player.userID))
            {
                SendReply(player, "<color=#FF0000>You already voted!</color>");
                return;
            }
            
            string mode = arg.GetString(0);
            if (mode != "soccer" && mode != "normal")
            {
                SendReply(player, "<color=#FF0000>Invalid mode choice!</color>");
                return;
            }
            
            // Register vote
            if (mode == "soccer")
            {
                soccerModeVotes++;
                SendReply(player, "<color=#00FF00>✓ Voted for Soccer Mode!</color>");
            }
            else
            {
                normalModeVotes++;
                SendReply(player, "<color=#FF9933>✓ Voted for Normal Mode!</color>");
            }
            
            playersWhoVotedMode.Add(player.userID);
            
            // Update UI for all players
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (redTeam.Contains(p.userID) || blueTeam.Contains(p.userID) || blackTeam.Contains(p.userID))
                {
                    ShowModeVotingUI(p);
                }
            }
        }

        private void StartTicker()
        {
            if (tickerTimer != null) tickerTimer.Destroy();
            tickerTimer = timer.Repeat(4.0f, 0, () => {
                tickerIndex++; if (tickerIndex >= tickerMessages.Count) tickerIndex = 0;
                string msg = tickerMessages[tickerIndex];
                foreach(var p in BasePlayer.activePlayerList) UpdateTickerUI(p, msg);
            });
        }

        private void UpdateTickerUI(BasePlayer player, string msg)
        {
            CuiHelper.DestroyUi(player, "SoccerTicker");
            if (!matchStarted) return;
            var c = new CuiElementContainer();
            c.Add(new CuiPanel { Image = { Color = "0 0 0 0.6" }, RectTransform = { AnchorMin = "0.35 0.87", AnchorMax = "0.65 0.90" } }, "Overlay", "SoccerTicker");
            c.Add(new CuiLabel { Text = { Text = msg, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 0 1" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "SoccerTicker");
            CuiHelper.AddUi(player, c);
        }

        // ==========================================
        // 8. PHYSICS & GAME LOGIC
        // ==========================================
        
        // Handle player connection - teleport to lobby spawn
        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            
            Puts($"[OnPlayerConnected] Player {player.displayName} connected");
            
            // Update online custom teams
            UpdateOnlineCustomTeams();
            
            // Check if player is already on a team and match is active
            bool isOnTeam = redTeam.Contains(player.userID) || 
                           blueTeam.Contains(player.userID) || 
                           blackTeam.Contains(player.userID);
            
            Puts($"[OnPlayerConnected] Player is on team: {isOnTeam}, Match started: {matchStarted}");
            
            // If match is active and player is on a team, let OnPlayerRespawn handle spawning
            if (matchStarted && isOnTeam)
            {
                Puts($"[OnPlayerConnected] Match active and player on team - letting OnPlayerRespawn handle spawn");
                return;
            }
            
            // Teleport to lobby spawn if it's set
            if (lobbySpawnPos != Vector3.zero)
            {
                Puts($"[OnPlayerConnected] Lobby spawn is set at {lobbySpawnPos}, scheduling teleport");
                
                // Wait a bit for player to fully load before teleporting
                timer.Once(2f, () =>
                {
                    if (player != null && player.IsConnected)
                    {
                        Puts($"[OnPlayerConnected] Executing teleport for {player.displayName}");
                        
                        // Wake player if sleeping
                        if (player.IsSleeping())
                        {
                            Puts($"[OnPlayerConnected] Player is sleeping, waking them up");
                            player.EndSleeping();
                        }
                        
                        // Teleport to lobby
                        player.Teleport(lobbySpawnPos);
                        player.ClientRPCPlayer(null, player, "ForcePositionTo", lobbySpawnPos);
                        player.SendNetworkUpdateImmediate();
                        
                        Puts($"[OnPlayerConnected] Teleported {player.displayName} to lobby spawn at {lobbySpawnPos}");
                        
                        // Show join UI after teleport
                        timer.Once(1f, () =>
                        {
                            if (player != null && player.IsConnected)
                            {
                                ShowTeamSelectUI(player);
                                SendReply(player, "⚽ Welcome! Select your team to join the match!");
                            }
                        });
                    }
                });
            }
            else
            {
                Puts($"[OnPlayerConnected] Lobby spawn not set (Vector3.zero), player will spawn at default location");
                
                // Still show team select UI even if lobby spawn not set
                timer.Once(3f, () =>
                {
                    if (player != null && player.IsConnected)
                    {
                        ShowTeamSelectUI(player);
                        SendReply(player, "⚽ Welcome! Select your team to join the match!");
                    }
                });
            }
        }
        
        void OnPlayerRespawn(BasePlayer player)
        {
            Puts($"[OnPlayerRespawn] Called for {player.displayName}");
            Puts($"[OnPlayerRespawn] Match started: {matchStarted}");
            Puts($"[OnPlayerRespawn] On team: {redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID)}");
            
            if (matchStarted && (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID)))
            {
                NextTick(() => {
                    if (!playerRoles.ContainsKey(player.userID)) playerRoles[player.userID] = "Striker";
                    string role = playerRoles[player.userID];
                    
                    Vector3 goalPos;
                    Quaternion goalRot;
                    
                    if (redTeam.Contains(player.userID))
                    {
                        goalPos = redGoalPos;
                        goalRot = redGoalRot;
                    }
                    else if (blueTeam.Contains(player.userID))
                    {
                        goalPos = blueGoalPos;
                        goalRot = blueGoalRot;
                    }
                    else // Black team
                    {
                        // Determine which black goal position to use
                        if (activeGoals["black1"])
                        {
                            goalPos = blackGoalPos1;
                            goalRot = blackGoalRot1;
                        }
                        else
                        {
                            goalPos = blackGoalPos2;
                            goalRot = blackGoalRot2;
                        }
                    }
                    
                    // Force instant respawn
                    if (goalPos != Vector3.zero) 
                    {
                        player.MovePosition(goalPos + (goalRot * Vector3.forward * 5f));
                        player.ClientRPCPlayer(null, player, "ForcePositionTo", goalPos + (goalRot * Vector3.forward * 5f));
                    }
                    
                    player.metabolism.radiation_poison.value = 0;
                    player.health = player.MaxHealth();
                    if (player.IsSleeping()) player.EndSleeping();
                    player.SendNetworkUpdateImmediate();
                    
                    GiveKit(player, role);
                    
                    // Force another network update after giving kit
                    player.SendNetworkUpdateImmediate();
                });
            }
            else
            {
                // Player not on team or match not started - teleport to lobby
                Puts($"[OnPlayerRespawn] Player not on team or match not started");
                
                if (lobbySpawnPos != Vector3.zero)
                {
                    Puts($"[OnPlayerRespawn] Teleporting to lobby spawn at {lobbySpawnPos}");
                    
                    NextTick(() => {
                        if (player != null && player.IsConnected)
                        {
                            if (player.IsSleeping()) player.EndSleeping();
                            
                            player.Teleport(lobbySpawnPos);
                            player.ClientRPCPlayer(null, player, "ForcePositionTo", lobbySpawnPos);
                            player.SendNetworkUpdateImmediate();
                            
                            Puts($"[OnPlayerRespawn] Teleported {player.displayName} to lobby");
                        }
                    });
                }
                else
                {
                    Puts($"[OnPlayerRespawn] Lobby spawn not set, allowing default spawn");
                }
            }
        }
        
        // Override spawn point selection to prevent sky spawns
        object OnPlayerRespawnOnMap(BasePlayer player, Vector3 position)
        {
            Puts($"[OnPlayerRespawnOnMap] Called for {player.displayName} at position {position}");
            
            // If player is on a team during match, let OnPlayerRespawn handle it
            if (matchStarted && (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID)))
            {
                Puts($"[OnPlayerRespawnOnMap] Player on team during match - allowing default");
                return null; // Let OnPlayerRespawn handle team spawning
            }
            
            // If lobby spawn is set, override spawn position
            if (lobbySpawnPos != Vector3.zero)
            {
                Puts($"[OnPlayerRespawnOnMap] Overriding spawn position to lobby: {lobbySpawnPos}");
                
                // Return the lobby spawn position to override Rust's spawn selection
                BasePlayer.SpawnPoint spawnPoint = new BasePlayer.SpawnPoint
                {
                    pos = lobbySpawnPos,
                    rot = Quaternion.identity
                };
                
                return spawnPoint;
            }
            
            Puts($"[OnPlayerRespawnOnMap] No override - allowing default spawn");
            return null; // Allow default spawning
        }
        
        // Alternative hook for spawn point determination
        object OnFindSpawnPoint()
        {
            Puts($"[OnFindSpawnPoint] Called - checking if lobby spawn should be used");
            
            if (lobbySpawnPos != Vector3.zero)
            {
                Puts($"[OnFindSpawnPoint] Returning lobby spawn: {lobbySpawnPos}");
                return lobbySpawnPos;
            }
            
            Puts($"[OnFindSpawnPoint] No override");
            return null;
        }


        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (activeBall != null && entity == activeBall)
            {
                info.damageTypes.ScaleAll(0); 
                if (info.Initiator is BasePlayer p)
                {
                    if (Vector3.Distance(p.transform.position, entity.transform.position) > MaxKickDistance) return;
                    lastKicker = p; 
                    Vector3 dir = (entity.transform.position - p.transform.position).normalized; dir.y += 0.2f; 
                    entity.GetComponent<Rigidbody>()?.AddForce(dir * KickForceMultiplier, ForceMode.Impulse);
                    Effect.server.Run("assets/bundled/prefabs/fx/impacts/physics/phys-impact-metal-thin-hollow-soft.prefab", entity.transform.position);
                }
            }
        }

        // Track player-placed entities for auto-destroy
        private Dictionary<NetworkableId, Timer> entityTimers = new Dictionary<NetworkableId, Timer>();
        
        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            Puts("════════════════════════════════════════════════");
            Puts($"[EntityBuilt] HOOK FIRED! Planner: {planner != null}, GameObject: {gameObject != null}");
            Puts("════════════════════════════════════════════════");
            
            if (planner == null || gameObject == null)
            {
                Puts($"[EntityBuilt] Early return - planner null: {planner == null}, gameObject null: {gameObject == null}");
                return;
            }
            
            BaseEntity entity = gameObject.ToBaseEntity();
            if (entity == null)
            {
                Puts($"[EntityBuilt] Early return - entity is null after ToBaseEntity()");
                return;
            }
            
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null)
            {
                Puts($"[EntityBuilt] Early return - player is null from GetOwnerPlayer()");
                return;
            }
            
            // Auto-destroy ALL player-placed entities after 7 seconds (ALWAYS active, not just during matches)
            string shortName = entity.ShortPrefabName ?? "unknown";
            string fullName = entity.PrefabName ?? "";
            
            Puts($"[EntityBuilt] Player {player.displayName} placed {shortName} (full: {fullName}) at {entity.transform.position}");
            Puts($"[EntityBuilt] Entity Net ID: {entity.net.ID}, IsDestroyed: {entity.IsDestroyed}");
            
            // Check if it's a deployable/buildable (barricades, walls, boxes, etc.)
            bool shouldDestroy = shortName.Contains("barricade") || 
                                 fullName.Contains("barricade") ||
                                 shortName.Contains("wall") ||
                                 fullName.Contains("wall") ||
                                 shortName.Contains("gate") ||
                                 shortName.Contains("deploy") ||
                                 shortName.Contains("box") ||
                                 shortName.Contains("shutter") ||
                                 shortName.Contains("door") ||
                                 shortName.Contains("wood") ||
                                 shortName.Contains("cover") ||
                                 entity is BuildingBlock ||
                                 entity is Deployable;
            
            Puts($"[EntityBuilt] Should destroy: {shouldDestroy}");
            
            if (shouldDestroy)
            {
                Puts($"[EntityBuilt] Entity {shortName} WILL be destroyed in 7 seconds (ID: {entity.net.ID})");
                
                // Show immediate notifications to player
                SendReply(player, $"⚠ Your {shortName} will auto-destroy in 7 seconds!");
                player.ShowToast(GameTip.Styles.Red_Normal, $"⚠ {shortName} auto-destroys in 7s!");
                
                // Capture entity reference for timer closure
                var entityId = entity.net.ID;
                var entityRef = entity;
                
                // Store timer reference
                var destroyTimer = timer.Once(7f, () =>
                {
                    Puts($"[EntityDestroy] Timer FIRED for {shortName} ID: {entityId}");
                    Puts($"[EntityDestroy] Entity reference null: {entityRef == null}, IsDestroyed: {entityRef?.IsDestroyed}");
                    
                    if (entityRef != null && !entityRef.IsDestroyed)
                    {
                        Puts($"[EntityDestroy] Entity still exists, proceeding with destruction");
                        Puts($"[EntityDestroy] Calling Kill() on {shortName} ID: {entityId}");
                        
                        try
                        {
                            entityRef.Kill(BaseNetworkable.DestroyMode.None);
                            Puts($"[EntityDestroy] Kill() executed successfully for {shortName}");
                            
                            // Verify destruction
                            NextTick(() =>
                            {
                                if (entityRef != null)
                                {
                                    Puts($"[EntityDestroy] Post-kill check - IsDestroyed: {entityRef.IsDestroyed}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Puts($"[EntityDestroy] ERROR killing entity: {ex.Message}");
                            Puts($"[EntityDestroy] Stack trace: {ex.StackTrace}");
                        }
                    }
                    else
                    {
                        Puts($"[EntityDestroy] Entity already destroyed or null - ID: {entityId}");
                    }
                    
                    // Clean up timer reference
                    if (entityTimers.ContainsKey(entityId))
                    {
                        entityTimers.Remove(entityId);
                        Puts($"[EntityDestroy] Removed timer from tracking dictionary for ID: {entityId}");
                    }
                });
                
                entityTimers[entityId] = destroyTimer;
                Puts($"[EntityBuilt] Timer stored with ID: {entityId}, total tracked timers: {entityTimers.Count}");
                
                // Additional countdown notifications
                timer.Once(4f, () => {
                    if (player != null && player.IsConnected && entityRef != null && !entityRef.IsDestroyed)
                    {
                        SendReply(player, $"⚠ {shortName} destroying in 3 seconds...");
                    }
                });
            }
            else
            {
                Puts($"[EntityBuilt] Entity {shortName} will NOT be destroyed (not in destruction list)");
            }
        }
        
        // Alternative hook that might catch items being deployed
        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            Puts("════════════════════════════════════════════════");
            Puts($"[OnItemDeployed] HOOK FIRED! Deployer: {deployer != null}, Entity: {entity != null}");
            
            if (deployer == null || entity == null)
            {
                Puts($"[OnItemDeployed] Early return - deployer null: {deployer == null}, entity null: {entity == null}");
                return;
            }
            
            BasePlayer player = deployer.GetOwnerPlayer();
            if (player == null)
            {
                Puts($"[OnItemDeployed] Early return - player is null");
                return;
            }
            
            string shortName = entity.ShortPrefabName ?? "unknown";
            string fullName = entity.PrefabName ?? "";
            
            Puts($"[OnItemDeployed] Player {player.displayName} deployed {shortName} (full: {fullName}) at {entity.transform.position}");
            Puts($"[OnItemDeployed] Entity Net ID: {entity.net.ID}");
            
            // Check if it's a deployable that should be destroyed
            bool shouldDestroy = shortName.Contains("barricade") || 
                                 fullName.Contains("barricade") ||
                                 shortName.Contains("wall") ||
                                 fullName.Contains("wall") ||
                                 shortName.Contains("wood") ||
                                 shortName.Contains("cover") ||
                                 entity is Deployable;
            
            Puts($"[OnItemDeployed] Should destroy: {shouldDestroy}");
            
            if (shouldDestroy)
            {
                Puts($"[OnItemDeployed] Setting up 7-second destruction timer");
                
                SendReply(player, $"⚠ Your {shortName} will auto-destroy in 7 seconds!");
                player.ShowToast(GameTip.Styles.Red_Normal, $"⚠ {shortName} auto-destroys in 7s!");
                
                var entityId = entity.net.ID;
                var entityRef = entity;
                
                var destroyTimer = timer.Once(7f, () =>
                {
                    Puts($"[OnItemDeployed-Destroy] Timer FIRED for {shortName} ID: {entityId}");
                    
                    if (entityRef != null && !entityRef.IsDestroyed)
                    {
                        Puts($"[OnItemDeployed-Destroy] Destroying entity");
                        try
                        {
                            entityRef.Kill(BaseNetworkable.DestroyMode.None);
                            Puts($"[OnItemDeployed-Destroy] Entity destroyed successfully");
                        }
                        catch (Exception ex)
                        {
                            Puts($"[OnItemDeployed-Destroy] ERROR: {ex.Message}");
                        }
                    }
                    
                    entityTimers.Remove(entityId);
                });
                
                entityTimers[entityId] = destroyTimer;
                Puts($"[OnItemDeployed] Timer stored, total timers: {entityTimers.Count}");
            }
        }
        
        // Clean up entity timer if destroyed early
        void OnEntityKill(BaseEntity entity)
        {
            if (entity == null) return;
            
            if (entityTimers.ContainsKey(entity.net.ID))
            {
                entityTimers[entity.net.ID]?.Destroy();
                entityTimers.Remove(entity.net.ID);
            }
        }
        
        // Handle player death - delete corpse, loot bags, and dropped items
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;
            
            // Check if it's a player
            if (entity is BasePlayer)
            {
                BasePlayer player = entity as BasePlayer;
                if (player != null)
                {
                    Puts($"[Death] Player {player.displayName} died, cleaning up corpse and items");
                    
                    // Determine death cause and add to kill feed (ALWAYS active, not just during matches)
                    string deathCause = "Unknown";
                    BasePlayer killer = null;
                    
                    if (info != null)
                    {
                        // Check death type
                        var majorDamageType = info.damageTypes.GetMajorityDamageType();
                        Puts($"[KillFeed] Player {player.displayName} died");
                        Puts($"[KillFeed] Death cause: {majorDamageType}");
                        
                        // Fall damage
                        if (majorDamageType == Rust.DamageType.Fall)
                        {
                            Puts($"[KillFeed] This is a fall damage death");
                            AddKillToFeed(null, player, "Fall");
                        }
                        // Player killed by another player
                        else if (info.InitiatorPlayer != null && info.InitiatorPlayer != player)
                        {
                            killer = info.InitiatorPlayer;
                            Puts($"[KillFeed] Killer: {killer.displayName}");
                            AddKillToFeed(killer, player, "Player");
                        }
                        // Suicide or self-damage
                        else if (info.InitiatorPlayer == player)
                        {
                            Puts($"[KillFeed] This is a suicide");
                            AddKillToFeed(null, player, "Suicide");
                        }
                        // Other environmental/unknown death
                        else
                        {
                            Puts($"[KillFeed] Unknown/environmental death");
                            AddKillToFeed(null, player, "Unknown");
                        }
                    }
                    else
                    {
                        Puts($"[KillFeed] No HitInfo available, treating as unknown death");
                        AddKillToFeed(null, player, "Unknown");
                    }
                    
                    // Store player position for item cleanup
                    Vector3 deathPos = player.transform.position;
                    Puts($"[DeathCleanup] Player {player.displayName} died at position {deathPos}");
                    
                    // Find and delete corpse on next frame
                    NextTick(() =>
                    {
                        Puts($"[DeathCleanup] Finding corpses for player ID: {player.userID}");
                        // Delete corpse
                        var corpses = UnityEngine.Object.FindObjectsOfType<PlayerCorpse>();
                        foreach (var corpse in corpses)
                        {
                            if (corpse.playerSteamID == player.userID)
                            {
                                Puts($"[DeathCleanup] Found corpse for {player.displayName}, deleting");
                                corpse.Kill(BaseNetworkable.DestroyMode.None);
                                Puts($"[DeathCleanup] Deleted corpse for {player.displayName}");
                            }
                        }
                        
                        // Delete dropped items and loot bags near death position
                        Puts($"[DeathCleanup] Scanning 5m radius for dropped items at {deathPos}");
                        var nearbyEntities = new List<BaseEntity>();
                        Vis.Entities(deathPos, 5f, nearbyEntities);
                        Puts($"[DeathCleanup] Found {nearbyEntities.Count} entities near death position");
                        
                        foreach (var ent in nearbyEntities)
                        {
                            if (ent == null || ent.IsDestroyed) continue;
                            
                            // Delete dropped weapons
                            if (ent is DroppedItem || ent is DroppedItemContainer)
                            {
                                Puts($"[DeathCleanup] Deleting dropped item: {ent.ShortPrefabName}");
                                ent.Kill(BaseNetworkable.DestroyMode.None);
                            }
                            // Delete loot containers/bags
                            else if (ent is LootContainer || ent is DroppedItemContainer)
                            {
                                Puts($"[DeathCleanup] Deleting loot container: {ent.ShortPrefabName}");
                                ent.Kill(BaseNetworkable.DestroyMode.None);
                            }
                            // Delete any item entity
                            else if (ent.ShortPrefabName != null && 
                                    (ent.ShortPrefabName.Contains("item_drop") || 
                                     ent.ShortPrefabName.Contains("loot")))
                            {
                                Puts($"[DeathCleanup] Deleting item: {ent.ShortPrefabName}");
                                ent.Kill(BaseNetworkable.DestroyMode.None);
                            }
                        }
                    });
                }
            }
        }
        
        // Backup corpse deletion on populate - also clean up loot
        void OnCorpsePopulate(PlayerCorpse corpse, BasePlayer player)
        {
            if (corpse == null || player == null) return;
            
            Puts($"[Death] Corpse created for {player.displayName}, deleting immediately with all loot");
            
            // Clear corpse containers before deletion
            if (corpse.containers != null)
            {
                foreach (var container in corpse.containers)
                {
                    if (container != null)
                    {
                        container.Clear();
                    }
                }
            }
            
            // Delete corpse immediately
            timer.Once(0.1f, () =>
            {
                if (corpse != null && !corpse.IsDestroyed)
                {
                    corpse.Kill(BaseNetworkable.DestroyMode.None);
                    Puts($"[Death] Deleted corpse for {player.displayName}");
                }
            });
        }

        // Protect ALL waiting team players from radiation damage
        // Waiting team is the 3rd team not currently playing in the match
        // They are still "active" in the match but not their team's turn to play
        void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player, float delta)
        {
            if (player == null || metabolism == null) return;
            
            // Check which team the player is on
            string team = redTeam.Contains(player.userID) ? "red" :
                         blueTeam.Contains(player.userID) ? "blue" :
                         blackTeam.Contains(player.userID) ? "black" : "";
            
            // If player is on the waiting team (any role), protect from radiation
            // This includes all players since they cannot return to goal during waiting
            if (!string.IsNullOrEmpty(team) && team == waitingTeam)
            {
                // Clear all radiation - waiting team players are immune
                metabolism.radiation_poison.value = 0f;
                metabolism.radiation_level.value = 0f;
            }
        }
        
        // Prevent players from dropping items (but allow moving them in inventory)
        object CanDropActiveItem(BasePlayer player)
        {
            // Check if player is in a team
            if (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID))
            {
                // Block item dropping - return true to prevent action
                return true;
            }
            return null;
        }
        
        // Block ALL item dropping methods (drag outside, right-click, etc.)
        object OnItemDropped(Item item, BasePlayer player)
        {
            if (player == null || item == null) return null;
            
            // Check if player is in a team
            if (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID))
            {
                // Item has already been dropped, we need to remove it from world and return to player
                NextTick(() => {
                    if (item != null && player != null && player.IsConnected)
                    {
                        // Find the dropped entity and kill it
                        var droppedItem = item.GetWorldEntity();
                        if (droppedItem != null && !droppedItem.IsDestroyed)
                        {
                            droppedItem.Kill();
                        }
                        
                        // Create new item and give to player
                        Item newItem = ItemManager.CreateByItemID(item.info.itemid, item.amount, item.skin);
                        if (newItem != null)
                        {
                            player.GiveItem(newItem);
                        }
                    }
                });
                return true; // Return true to indicate we handled it
            }
            return null;
        }
        
        // Additional hook to catch item actions (drops via right-click menu, etc.)
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (player == null || item == null) return null;
            
            // Block drop and drop_all actions for team players
            if ((action == "drop" || action == "drop_all") && 
                (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID)))
            {
                SendReply(player, "You cannot drop items while on a team!");
                return true; // Block the action
            }
            return null;
        }

        private void SpawnBall()
        {
            if (activeBall != null && !activeBall.IsDestroyed) activeBall.Kill();
            string prefab = "assets/content/vehicles/ball/ball.entity.prefab";
            activeBall = GameManager.server.CreateEntity(prefab, centerPos + new Vector3(0, 2, 0));
            if (activeBall == null) { prefab = "assets/prefabs/misc/soccerball/soccerball.prefab"; activeBall = GameManager.server.CreateEntity(prefab, centerPos + new Vector3(0, 1, 0)); }
            if (activeBall == null) return;
            activeBall.Spawn();
            
            // Apply ball scaling (network synchronized to all players)
            activeBall.transform.localScale = new Vector3(ballScale, ballScale, ballScale);
            activeBall.SendNetworkUpdate();
            
            Rigidbody rb = activeBall.GetComponent<Rigidbody>();
            if (rb != null) 
            { 
                rb.mass = 200.0f; 
                rb.drag = 1.5f; 
                rb.angularDrag = 1.0f; 
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; 
                rb.WakeUp(); 
            }
        }

        private void CheckGoals()
        {
            if (!gameActive || activeBall == null) return;
            
            // Determine which goal was scored in and award point to the kicking team
            string scoringTeam = null;
            string goalType = null;
            
            // Check blue goal (if active)
            if (activeGoals["blue"] && IsInside(activeBall.transform.position, blueGoalPos, blueGoalRot))
            {
                goalType = "blue";
                // Ball went into blue's goal - determine who kicked it
                if (lastKicker != null)
                {
                    if (redTeam.Contains(lastKicker.userID)) scoringTeam = "RED";
                    else if (blackTeam.Contains(lastKicker.userID)) scoringTeam = "BLACK";
                }
            }
            // Check red goal (if active)
            else if (activeGoals["red"] && IsInside(activeBall.transform.position, redGoalPos, redGoalRot))
            {
                goalType = "red";
                // Ball went into red's goal - determine who kicked it
                if (lastKicker != null)
                {
                    if (blueTeam.Contains(lastKicker.userID)) scoringTeam = "BLUE";
                    else if (blackTeam.Contains(lastKicker.userID)) scoringTeam = "BLACK";
                }
            }
            // Check black goal 1 (if active)
            else if (activeGoals["black1"] && IsInside(activeBall.transform.position, blackGoalPos1, blackGoalRot1))
            {
                goalType = "black1";
                // Ball went into black1's goal - determine who kicked it
                if (lastKicker != null)
                {
                    if (blueTeam.Contains(lastKicker.userID)) scoringTeam = "BLUE";
                    else if (redTeam.Contains(lastKicker.userID)) scoringTeam = "RED";
                }
            }
            // Check black goal 2 (if active)
            else if (activeGoals["black2"] && IsInside(activeBall.transform.position, blackGoalPos2, blackGoalRot2))
            {
                goalType = "black2";
                // Ball went into black2's goal - determine who kicked it
                if (lastKicker != null)
                {
                    if (blueTeam.Contains(lastKicker.userID)) scoringTeam = "BLUE";
                    else if (redTeam.Contains(lastKicker.userID)) scoringTeam = "RED";
                }
            }
            
            // In rotation mode, only count goals if scored by playing teams
            if (scoringTeam != null)
            {
                if (rotationMode)
                {
                    string teamLower = scoringTeam.ToLower();
                    if (teamLower == team1Playing || teamLower == team2Playing)
                    {
                        HandleGoal(scoringTeam);
                    }
                }
                else
                {
                    HandleGoal(scoringTeam);
                }
            }
        }

        private bool IsInside(Vector3 b, Vector3 g, Quaternion r)
        {
            Vector3 l = Quaternion.Inverse(r) * (b - g);
            return Mathf.Abs(l.x) < GoalWidth/2 && Mathf.Abs(l.y) < GoalHeight/2 && Mathf.Abs(l.z) < GoalDepth/2;
        }

        private void HandleGoal(string team)
        {
            gameActive = false;
            
            // Only count goals for teams that are playing (in rotation mode)
            if (rotationMode)
            {
                if (team.ToLower() == team1Playing) 
                {
                    if (team == "RED") scoreRed++; 
                    else if (team == "BLUE") scoreBlue++; 
                    else if (team == "BLACK") scoreBlack++;
                }
                else if (team.ToLower() == team2Playing)
                {
                    if (team == "RED") scoreRed++; 
                    else if (team == "BLUE") scoreBlue++; 
                    else if (team == "BLACK") scoreBlack++;
                }
            }
            else
            {
                // Normal 3-way mode
                if (team == "RED") scoreRed++; 
                else if (team == "BLUE") scoreBlue++; 
                else if (team == "BLACK") scoreBlack++;
            }
            
            // Goal scoring effects
            Vector3 goalPos = activeBall.transform.position;
            Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", goalPos);
            
            // Spawn goal effects using entity spawning
            SpawnGoalEffects(goalPos);
            
            RefreshScoreboardAll(); ShowGoalBanner(team);
            string mvp = (lastKicker != null) ? lastKicker.displayName : "None";
            tickerMessages.Add($"GOAL: {team} ({mvp})");
            
            if (rotationMode)
            {
                CallMiddleware($"EVENT: GOAL. {team} Scores. MVP: {mvp}. Match #{matchNumber}");
                int score1 = GetTeamScore(team1Playing);
                int score2 = GetTeamScore(team2Playing);
                if (score1 >= ScoreToWin || score2 >= ScoreToWin) EndMatch(team);
                else timer.Once(5f, () => { SpawnBall(); gameActive = true; });
            }
            else
            {
                CallMiddleware($"EVENT: GOAL. {team} Scores. MVP: {mvp}. Score: R{scoreRed}-B{scoreBlue}-Bl{scoreBlack}");
                if (scoreRed >= ScoreToWin || scoreBlue >= ScoreToWin || scoreBlack >= ScoreToWin) EndMatch(team);
                else timer.Once(5f, () => { SpawnBall(); gameActive = true; });
            }
        }

        private void EndMatch(string winner)
        {
            string winnerTag = teamConfigs[winner.ToLower()].Tag;
            PrintToChat($"MATCH #{matchNumber} OVER! {winnerTag} WINS!");
            CallMiddleware($"EVENT: MATCH_END. Winner: {winnerTag}");
            if (activeBall != null) activeBall.Kill();
            gameActive = false;
            
            // Clear kill feed
            killFeed.Clear();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "KillFeedContainer");
            }
            
            // Trigger celebrations
            TriggerCelebrations(winner.ToLower());
            
            if (rotationMode)
            {
                // Check if tournament should end (after 2 matches)
                if (matchNumber >= maxMatchesPerTournament)
                {
                    timer.Once(8f, () => EndTournament(winner.ToLower()));
                }
                else
                {
                    // Continue rotation
                    string loser = (winner.ToLower() == team1Playing) ? team2Playing : team1Playing;
                    timer.Once(8f, () => RotateTeams(winner.ToLower(), loser));
                }
            }
            else
            {
                matchStarted = false;
                matchActive = false; // Disable match to prevent auto-start
                timer.Once(5f, () => { 
                    foreach(var p in BasePlayer.activePlayerList) { 
                        CuiHelper.DestroyUi(p, "SoccerScoreboard"); 
                        CuiHelper.DestroyUi(p, "SoccerTicker"); 
                        CuiHelper.DestroyUi(p, "BallRangeHUD"); 
                        CuiHelper.DestroyUi(p, "LeashHUD"); 
                    }
                });
                
                // Notify that match is over and manual start required
                timer.Once(6f, () => {
                    PrintToChat("═══════════════════════════════════");
                    PrintToChat("<color=#FFD700>🏆 MATCH ENDED!</color>");
                    PrintToChat("<color=#FFD700>Host can start new match with /start_match</color>");
                    PrintToChat("═══════════════════════════════════");
                });
            }
        }
        
        private int GetTeamScore(string team)
        {
            if (team == "red") return scoreRed;
            if (team == "blue") return scoreBlue;
            if (team == "black") return scoreBlack;
            return 0;
        }
        
        private void RotateTeams(string winner, string loser)
        {
            matchNumber++;
            
            // Winner stays, waiting team comes in, loser goes to waiting
            string newTeam1 = winner;
            string newTeam2 = waitingTeam;
            string newWaiting = loser;
            
            team1Playing = newTeam1;
            team2Playing = newTeam2;
            waitingTeam = newWaiting;
            
            // Reset scores for new match
            scoreRed = 0; scoreBlue = 0; scoreBlack = 0;
            
            // GOAL SWAPPING LOGIC
            // Determine current goal states before making changes
            bool winnerIsBlack = (winner == "black");
            bool loserIsBlack = (loser == "black");
            bool waitingIsBlack = (waitingTeam == "black");
            
            if (loserIsBlack)
            {
                // Black is leaving, need to determine which black goal to deactivate
                // and activate the original team goal for the waiting team
                if (activeGoals["black1"]) // Black was using black1 (at red position)
                {
                    activeGoals["black1"] = false;
                    // Waiting team gets red goal position
                    if (waitingTeam == "red")
                    {
                        activeGoals["red"] = true;
                        PrintToChat($"Red team reclaiming their goal!");
                    }
                    else if (waitingTeam == "blue")
                    {
                        // Black1 is at red position, so we need black2 for blue
                        activeGoals["black2"] = true;
                        PrintToChat($"Black team moving to BLUE goal position!");
                    }
                }
                else if (activeGoals["black2"]) // Black was using black2 (at blue position)
                {
                    activeGoals["black2"] = false;
                    // Waiting team gets blue goal position
                    if (waitingTeam == "blue")
                    {
                        activeGoals["blue"] = true;
                        PrintToChat($"Blue team reclaiming their goal!");
                    }
                    else if (waitingTeam == "red")
                    {
                        // Black2 is at blue position, so we need black1 for red
                        activeGoals["black1"] = true;
                        PrintToChat($"Black team moving to RED goal position!");
                    }
                }
            }
            else if (winnerIsBlack)
            {
                // Black won, loser is red or blue
                // Black stays at current position, loser's goal gets deactivated
                // Waiting team takes over loser's position
                if (loser == "red")
                {
                    activeGoals["red"] = false;
                    // Waiting team enters at red position
                    if (waitingTeam == "blue")
                    {
                        activeGoals["blue"] = true;
                        PrintToChat($"Blue team entering at their goal!");
                    }
                    else // Waiting is red (shouldn't happen but handle it)
                    {
                        activeGoals["black1"] = true;
                        PrintToChat($"Setup at RED goal position!");
                    }
                }
                else if (loser == "blue")
                {
                    activeGoals["blue"] = false;
                    // Waiting team enters at blue position
                    if (waitingTeam == "red")
                    {
                        activeGoals["red"] = true;
                        PrintToChat($"Red team entering at their goal!");
                    }
                    else // Waiting is blue (shouldn't happen but handle it)
                    {
                        activeGoals["black2"] = true;
                        PrintToChat($"Setup at BLUE goal position!");
                    }
                }
            }
            else
            {
                // Winner is red or blue, loser is red or blue, waiting is black
                // Deactivate loser's goal and activate black goal at that position
                if (loser == "red")
                {
                    activeGoals["red"] = false;
                    activeGoals["black1"] = true;  // Black goal at red position
                    PrintToChat($"Black team taking over RED goal position!");
                }
                else if (loser == "blue")
                {
                    activeGoals["blue"] = false;
                    activeGoals["black2"] = true;  // Black goal at blue position
                    PrintToChat($"Black team taking over BLUE goal position!");
                }
            }
            
            PrintToChat("═══════════════════════════════════");
            PrintToChat($"ROTATION MATCH #{matchNumber}");
            PrintToChat($"{teamConfigs[team1Playing].Tag} vs {teamConfigs[team2Playing].Tag}");
            PrintToChat($"Next Team: {teamConfigs[waitingTeam].Tag}");
            PrintToChat("═══════════════════════════════════");
            
            // Start new match after delay
            timer.Once(5f, () => {
                gameActive = true;
                SpawnBall();
                RefreshScoreboardAll();
            });
        }
        
        private void EndTournament(string tournamentWinner)
        {
            string winnerTag = teamConfigs[tournamentWinner].Tag;
            PrintToChat("═══════════════════════════════════");
            PrintToChat($"TOURNAMENT COMPLETE!");
            PrintToChat($"CHAMPION: {winnerTag}");
            PrintToChat("═══════════════════════════════════");
            
            // Final celebrations
            TriggerTournamentCelebrations(tournamentWinner);
            
            // Reset match state
            matchStarted = false;
            matchActive = false; // Disable match to prevent auto-start
            matchNumber = 1;
            
            // Clear all UIs
            timer.Once(10f, () => {
                foreach(var p in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(p, "SoccerScoreboard");
                    CuiHelper.DestroyUi(p, "SoccerTicker");
                    CuiHelper.DestroyUi(p, "BallRangeHUD");
                    CuiHelper.DestroyUi(p, "LeashHUD");
                }
            });
            
            // DISABLED AUTO-START: Players now need host to manually start next tournament
            // timer.Once(15f, () => StartLobbyCountdown(30));
            
            PrintToChat("═══════════════════════════════════");
            PrintToChat("<color=#FFD700>🏆 TOURNAMENT ENDED!</color>");
            PrintToChat("<color=#FFD700>Host can start new match with /start_match</color>");
            PrintToChat("═══════════════════════════════════");
        }
        
        private void StartLobbyCountdown(int seconds)
        {
            lobbyActive = true;
            lobbyCountdown = seconds;
            
            PrintToChat("═══════════════════════════════════");
            PrintToChat($"LOBBY ACTIVE - Next match in {seconds} seconds");
            PrintToChat("⚽ Use /join to select your team! ⚽");
            PrintToChat("═══════════════════════════════════");
            
            if (lobbyTimer != null) lobbyTimer.Destroy();
            lobbyTimer = timer.Repeat(1f, seconds, () => {
                lobbyCountdown--;
                
                if (lobbyCountdown == 10)
                {
                    PrintToChat($"Match starting in {lobbyCountdown} seconds!");
                }
                else if (lobbyCountdown == 5)
                {
                    PrintToChat($"Match starting in {lobbyCountdown}...");
                }
                else if (lobbyCountdown <= 3 && lobbyCountdown > 0)
                {
                    PrintToChat($"{lobbyCountdown}...");
                }
                else if (lobbyCountdown == 0)
                {
                    AutoStartMatch();
                }
            });
            
            // Start lobby reminders and teleport players
            TeleportAllToLobby();
            StartLobbyReminders();
            PrintToChat("⚽ Use /join to select your team! ⚽");
        }
        
        private void AutoStartMatch()
        {
            lobbyActive = false;
            
            // Stop lobby countdown timer
            if (lobbyTimer != null && !lobbyTimer.Destroyed)
            {
                lobbyTimer.Destroy();
            }
            
            // Stop lobby reminder timer
            if (lobbyReminderTimer != null && !lobbyReminderTimer.Destroyed)
            {
                lobbyReminderTimer.Destroy();
            }
            
            Puts("Match starting - lobby ended");
            
            // Reset for new tournament
            scoreRed = 0; scoreBlue = 0; scoreBlack = 0;
            matchNumber = 1;
            
            if (rotationMode)
            {
                team1Playing = "blue";
                team2Playing = "red";
                waitingTeam = "black";
                
                activeGoals["red"] = true;
                activeGoals["blue"] = true;
                activeGoals["black1"] = false;
                activeGoals["black2"] = false;
                
                PrintToChat($"ROTATION MATCH #{matchNumber}: {teamConfigs[team1Playing].Tag} vs {teamConfigs[team2Playing].Tag}");
                PrintToChat($"Next Team: {teamConfigs[waitingTeam].Tag}");
            }
            else
            {
                PrintToChat("MATCH STARTED! 3 Teams Battle!");
            }
            
            gameActive = true;
            matchStarted = true;
            
            SpawnBall();
            RefreshScoreboardAll();
            StartTicker();
            
            if (gameTimer != null) gameTimer.Destroy();
            gameTimer = timer.Repeat(0.05f, 0, CheckGoals);
            
            if (hudTimer != null) hudTimer.Destroy();
            hudTimer = timer.Repeat(0.5f, 0, HudLoop);
        }
        
        // ==========================================
        // LOBBY SYSTEM - JOIN REMINDERS
        // ==========================================
        private void StartLobbyReminders()
        {
            // Stop existing reminder timer
            if (lobbyReminderTimer != null && !lobbyReminderTimer.Destroyed)
            {
                lobbyReminderTimer.Destroy();
            }
            
            // Start new reminder timer - every 10 seconds
            lobbyReminderTimer = timer.Repeat(10f, 0, () => {
                if (!lobbyActive) return;
                
                PrintToChat("⚽ Use /join to select your team! ⚽");
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player != null && player.IsConnected)
                    {
                        player.ShowToast(GameTip.Styles.Blue_Normal, "⚽ Use /join to select your team! ⚽");
                    }
                }
            });
            
            Puts("Lobby join reminders started");
        }
        
        private void TeleportToLobby(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            if (lobbySpawnPos == Vector3.zero)
            {
                Puts($"Cannot teleport {player.displayName} to lobby - lobby spawn not set!");
                SendReply(player, "⚠ Lobby spawn not set! Admin needs to run /set_lobby_spawn");
                return;
            }
            
            // Ensure player is fully spawned before teleporting
            NextTick(() =>
            {
                if (player != null && player.IsConnected)
                {
                    // Wake player if sleeping
                    if (player.IsSleeping()) 
                        player.EndSleeping();
                    
                    // Force position update
                    player.MovePosition(lobbySpawnPos);
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", lobbySpawnPos);
                    player.SendNetworkUpdateImmediate();
                    
                    Puts($"Teleported {player.displayName} to lobby at {lobbySpawnPos}");
                }
            });
        }
        
        private void TeleportLoserTeam(string losingTeam)
        {
            if (loserSpawnPos == Vector3.zero)
            {
                Puts("Loser spawn not set - skipping loser team teleport");
                return;
            }
            
            List<ulong> loserPlayers = null;
            if (losingTeam == "red") loserPlayers = redTeam;
            else if (losingTeam == "blue") loserPlayers = blueTeam;
            else if (losingTeam == "black") loserPlayers = blackTeam;
            
            if (loserPlayers == null) return;
            
            foreach (var playerId in loserPlayers)
            {
                BasePlayer player = BasePlayer.FindByID(playerId);
                if (player != null && player.IsConnected)
                {
                    NextTick(() =>
                    {
                        if (player != null && player.IsConnected)
                        {
                            if (player.IsSleeping()) player.EndSleeping();
                            player.MovePosition(loserSpawnPos);
                            player.ClientRPCPlayer(null, player, "ForcePositionTo", loserSpawnPos);
                            player.SendNetworkUpdateImmediate();
                            Puts($"Teleported losing player {player.displayName} to loser spawn");
                        }
                    });
                }
            }
        }
        
        private void TeleportAllToLobby()
        {
            if (lobbySpawnPos == Vector3.zero)
            {
                Puts("Lobby spawn not set - players not teleported");
                return;
            }
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected)
                {
                    TeleportToLobby(player);
                }
            }
            
            Puts($"Teleported all players to lobby spawn");
        }
        
        
        // ==========================================
        // CELEBRATION SYSTEM
        // ==========================================
        private void TriggerCelebrations(string winner)
        {
            string winnerTag = teamConfigs[winner].Tag;
            var winnerColor = teamConfigs[winner].Color;
            
            // Team-colored fireworks effect
            timer.Repeat(0.5f, 10, () => {
                LaunchFirework(centerPos + new Vector3(UnityEngine.Random.Range(-20f, 20f), 0, UnityEngine.Random.Range(-20f, 20f)), winner);
            });
            
            // Dancing laser lines from corners with team colors
            StartDancingLasers(5f, winner);
            
            // Sky text celebration
            ShowSkyText(winnerTag + " WINS!", winnerColor, 5f);
        }
        
        private void TriggerTournamentCelebrations(string winner)
        {
            string winnerTag = teamConfigs[winner].Tag;
            var winnerColor = teamConfigs[winner].Color;
            
            // Big team-colored fireworks
            timer.Repeat(0.3f, 20, () => {
                LaunchFirework(centerPos + new Vector3(UnityEngine.Random.Range(-30f, 30f), 0, UnityEngine.Random.Range(-30f, 30f)), winner);
            });
            
            // Epic dancing lasers - longer duration for tournament with team colors
            StartDancingLasers(10f, winner);
            
            // Tournament champion text
            ShowSkyText("TOURNAMENT", "1 1 1", 3f, 40f);
            timer.Once(3f, () => ShowSkyText("CHAMPION", "1 1 0", 3f, 35f));
            timer.Once(6f, () => ShowSkyText(winnerTag, winnerColor, 5f, 45f));
        }
        
        private void LaunchFirework(Vector3 position, string team)
        {
            // Use C4 explosion as "firework" - reliable and visible
            Vector3 spawnPos = position + new Vector3(0, 30f, 0);
            Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", spawnPos);
            
            // Add sparkles for extra effect
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-5f, 5f));
                Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", spawnPos + offset);
            }
        }
        
        private void SpawnGoalEffects(Vector3 position)
        {
            // Main C4 explosion at ball position
            Effect.server.Run("assets/prefabs/tools/c4/effects/c4_explosion.prefab", position);
            
            // Sparkle particles spread around for extra effect
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = new Vector3(
                    UnityEngine.Random.Range(-3f, 3f), 
                    UnityEngine.Random.Range(0f, 2f), 
                    UnityEngine.Random.Range(-3f, 3f)
                );
                Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", position + offset);
            }
        }
        
        private void ShowSkyText(string text, string colorStr, float duration, float height = 30f)
        {
            // Parse color
            string[] rgb = colorStr.Split(' ');
            Color color = new Color(
                float.Parse(rgb[0]),
                float.Parse(rgb[1]),
                float.Parse(rgb[2])
            );
            
            // Show text in sky for all players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                
                Vector3 textPos = centerPos + new Vector3(0, height, 0);
                
                // Main text
                player.SendConsoleCommand("ddraw.text", duration, color, textPos, $"<size=50>{text}</size>");
                
                // Outer glow effect (multiple layers)
                Color glowColor = new Color(color.r, color.g, color.b, 0.3f);
                player.SendConsoleCommand("ddraw.text", duration, glowColor, textPos + new Vector3(0.2f, 0.2f, 0), $"<size=50>{text}</size>");
                player.SendConsoleCommand("ddraw.text", duration, glowColor, textPos + new Vector3(-0.2f, 0.2f, 0), $"<size=50>{text}</size>");
                player.SendConsoleCommand("ddraw.text", duration, glowColor, textPos + new Vector3(0.2f, -0.2f, 0), $"<size=50>{text}</size>");
                player.SendConsoleCommand("ddraw.text", duration, glowColor, textPos + new Vector3(-0.2f, -0.2f, 0), $"<size=50>{text}</size>");
            }
        }
        
        private void StartDancingLasers(float duration, string team)
        {
            // Define 4 corner positions around the arena (50m radius from center)
            Vector3[] corners = new Vector3[4];
            corners[0] = centerPos + new Vector3(-50f, 0, -50f);  // Bottom-left
            corners[1] = centerPos + new Vector3(50f, 0, -50f);   // Bottom-right
            corners[2] = centerPos + new Vector3(50f, 0, 50f);    // Top-right
            corners[3] = centerPos + new Vector3(-50f, 0, 50f);   // Top-left
            
            // Get team color
            string teamColorStr = teamConfigs[team].Color;
            string[] rgb = teamColorStr.Split(' ');
            Color teamColor = new Color(
                float.Parse(rgb[0]),
                float.Parse(rgb[1]),
                float.Parse(rgb[2])
            );
            
            // Create variations of team color for variety
            Color[] colors = new Color[] {
                teamColor,                                          // Main team color
                new Color(teamColor.r * 1.2f, teamColor.g * 1.2f, teamColor.b * 1.2f),  // Brighter
                new Color(teamColor.r * 0.8f, teamColor.g * 0.8f, teamColor.b * 0.8f),  // Darker
                new Color(teamColor.r, teamColor.g * 1.3f, teamColor.b),                // Green tint
                new Color(teamColor.r * 1.3f, teamColor.g, teamColor.b),                // Red tint
                new Color(teamColor.r, teamColor.g, teamColor.b * 1.3f),                // Blue tint
                new Color(1f, 1f, 1f),                             // White flash
                new Color(teamColor.r * 1.5f, teamColor.g * 1.5f, teamColor.b * 1.5f)   // Super bright
            };
            
            // Clamp all colors to valid range
            for (int c = 0; c < colors.Length; c++)
            {
                colors[c].r = Mathf.Clamp01(colors[c].r);
                colors[c].g = Mathf.Clamp01(colors[c].g);
                colors[c].b = Mathf.Clamp01(colors[c].b);
            }
            
            // Animate lasers over duration
            float interval = 0.1f;
            int totalSteps = (int)(duration / interval);
            
            timer.Repeat(interval, totalSteps, () => {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null || !player.IsConnected) continue;
                    
                    // Each corner shoots lasers to center and other corners
                    for (int i = 0; i < corners.Length; i++)
                    {
                        Vector3 cornerStart = corners[i] + new Vector3(0, 10f, 0); // Elevate start point
                        
                        // Random height variation for dancing effect
                        float heightOffset = UnityEngine.Random.Range(-5f, 15f);
                        Vector3 centerTarget = centerPos + new Vector3(0, 20f + heightOffset, 0);
                        
                        // Laser to center with team color variation
                        Color laserColor = colors[UnityEngine.Random.Range(0, colors.Length)];
                        player.SendConsoleCommand("ddraw.line", interval + 0.05f, laserColor, cornerStart, centerTarget);
                        
                        // Cross lasers to opposite corners
                        int oppositeCorner = (i + 2) % 4;
                        Vector3 oppositeStart = corners[oppositeCorner] + new Vector3(0, 10f, 0);
                        Color crossColor = colors[UnityEngine.Random.Range(0, colors.Length)];
                        player.SendConsoleCommand("ddraw.line", interval + 0.05f, crossColor, cornerStart, oppositeStart);
                        
                        // Rotating lasers to adjacent corners
                        int nextCorner = (i + 1) % 4;
                        Vector3 nextStart = corners[nextCorner] + new Vector3(0, 10f + UnityEngine.Random.Range(-3f, 3f), 0);
                        Color adjacentColor = colors[UnityEngine.Random.Range(0, colors.Length)];
                        player.SendConsoleCommand("ddraw.line", interval + 0.05f, adjacentColor, cornerStart, nextStart);
                    }
                }
            });
        }
        
        private void DrawGoal(BasePlayer player, Vector3 c, Quaternion r, Color col, float dur)
        {
            float hw=GoalWidth/2, hh=GoalHeight/2, hd=GoalDepth/2;
            float thick = 0.15f; // Thickness offset for double lines
            Vector3[] p = new Vector3[8];
            p[0]=c+r*new Vector3(-hw,-hh,-hd); p[1]=c+r*new Vector3(hw,-hh,-hd); p[2]=c+r*new Vector3(hw,-hh,hd); p[3]=c+r*new Vector3(-hw,-hh,hd);
            p[4]=c+r*new Vector3(-hw,hh,-hd); p[5]=c+r*new Vector3(hw,hh,-hd); p[6]=c+r*new Vector3(hw,hh,hd); p[7]=c+r*new Vector3(-hw,hh,hd);
            
            // Draw main lines
            player.SendConsoleCommand("ddraw.line", dur, col, p[0], p[1]); player.SendConsoleCommand("ddraw.line", dur, col, p[1], p[2]); player.SendConsoleCommand("ddraw.line", dur, col, p[2], p[3]); player.SendConsoleCommand("ddraw.line", dur, col, p[3], p[0]);
            player.SendConsoleCommand("ddraw.line", dur, col, p[4], p[5]); player.SendConsoleCommand("ddraw.line", dur, col, p[5], p[6]); player.SendConsoleCommand("ddraw.line", dur, col, p[6], p[7]); player.SendConsoleCommand("ddraw.line", dur, col, p[7], p[4]);
            player.SendConsoleCommand("ddraw.line", dur, col, p[0], p[4]); player.SendConsoleCommand("ddraw.line", dur, col, p[1], p[5]); player.SendConsoleCommand("ddraw.line", dur, col, p[2], p[6]); player.SendConsoleCommand("ddraw.line", dur, col, p[3], p[7]);
            
            // Draw thick parallel lines for better visibility (offset inward slightly)
            Vector3 offset = r * new Vector3(thick, 0, 0);
            player.SendConsoleCommand("ddraw.line", dur, col, p[0]+offset, p[1]-offset); player.SendConsoleCommand("ddraw.line", dur, col, p[2]-offset, p[3]+offset);
            player.SendConsoleCommand("ddraw.line", dur, col, p[4]+offset, p[5]-offset); player.SendConsoleCommand("ddraw.line", dur, col, p[6]-offset, p[7]+offset);
            offset = r * new Vector3(0, thick, 0);
            player.SendConsoleCommand("ddraw.line", dur, col, p[0]+offset, p[4]+offset); player.SendConsoleCommand("ddraw.line", dur, col, p[1]+offset, p[5]+offset);
            player.SendConsoleCommand("ddraw.line", dur, col, p[2]+offset, p[6]+offset); player.SendConsoleCommand("ddraw.line", dur, col, p[3]+offset, p[7]+offset);
            
            player.SendConsoleCommand("ddraw.text", dur, col, c + new Vector3(0, hh + 2f, 0), "<size=20>GOAL ZONE</size>");
        }
        
        private void CallMiddleware(string text)
        {
            var msg = new List<object> { new { role = "system", content = "Sports Caster AI" }, new { role = "user", content = text } };
            // ADDED: Mode = soccer to trigger correct prompt on server
            var data = new { license = licenseKey, server_ip = ConVar.Server.ip, messages = msg, mode = "soccer", user_input = text };
            
            Puts($"[AI DEBUG] Sending to: {middlewareUrl}");

            webrequest.Enqueue(middlewareUrl, JsonConvert.SerializeObject(data), (c, r) => {
                if (c == 200) {
                    try {
                        var res = JsonConvert.DeserializeObject<OpenAIResponse>(r);
                        var clean = res.choices[0].message.content.Replace("```json","").Replace("```","").Trim();
                        int s=clean.IndexOf('{'), e=clean.LastIndexOf('}');
                        if(s>=0 && e>s) PrintToChat($"<color=#00ffff>[COMMENTATOR]</color>: {JsonConvert.DeserializeObject<AnnouncerResponse>(clean.Substring(s,e-s+1)).message_to_player}");
                    } catch (Exception ex) { Puts($"[AI ERROR] Parse failed: {ex.Message}"); }
                } else { Puts($"[AI ERROR] Code: {c} | {r}"); }
            }, this, RequestMethod.POST, new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        public class OpenAIResponse { public Choice[] choices { get; set; } }
        public class Choice { public Message message { get; set; } }
        public class Message { public string content { get; set; } }
        public class AnnouncerResponse { public string message_to_player { get; set; } }
    }
}