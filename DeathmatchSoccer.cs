using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
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
     * - Custom Skins: Each team can have unique skins via Skins.cs plugin
     * - Modern UI: Team selection menu, dynamic scoreboard, role selection
     * - 2 Roles: Striker (100HP, Thompson) and Goalie (200HP, SPAS-12)
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
     * Use /join command - Shows team selection UI (reminders appear during lobby)
     */
    [Info("DeathmatchSoccer", "KillaDome", "5.4.0")]
    [Description("3-Team Soccer with Lobby System and Celebrations")]
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
        private Vector3 redGoalPos, blueGoalPos, blackGoalPos1, blackGoalPos2, centerPos;
        private Quaternion redGoalRot, blueGoalRot, blackGoalRot1, blackGoalRot2;
        
        private int scoreRed = 0;
        private int scoreBlue = 0;
        private int scoreBlack = 0;
        private bool gameActive = false; 
        private bool matchStarted = false; 
        private bool debugActive = false;
        
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

        // DATA FILE
        private const string DataFileName = "DeathmatchSoccer_Data";

        // ==========================================
        // 2. DATA PERSISTENCE (SAVING/LOADING)
        // ==========================================
        private class ArenaData
        {
            public float Rx, Ry, Rz; // Red Pos
            public float Bx, By, Bz; // Blue Pos
            public float Bl1x, Bl1y, Bl1z; // Black1 Pos (at red position)
            public float Bl2x, Bl2y, Bl2z; // Black2 Pos (at blue position)
            public float Cx, Cy, Cz; // Center Pos
            public float Rqx, Rqy, Rqz, Rqw; // Red Rot
            public float Bqx, Bqy, Bqz, Bqw; // Blue Rot
            public float Bl1qx, Bl1qy, Bl1qz, Bl1qw; // Black1 Rot
            public float Bl2qx, Bl2qy, Bl2qz, Bl2qw; // Black2 Rot
            public float Gw, Gh, Gd; // Dimensions
            public float Lbx, Lby, Lbz; // Lobby spawn position
            public float Lsx, Lsy, Lsz; // Loser spawn position
        }

        private void SaveArenaData()
        {
            Puts($"[Data] Saving arena data to file");
            var data = new ArenaData
            {
                Rx = redGoalPos.x, Ry = redGoalPos.y, Rz = redGoalPos.z,
                Bx = blueGoalPos.x, By = blueGoalPos.y, Bz = blueGoalPos.z,
                Bl1x = blackGoalPos1.x, Bl1y = blackGoalPos1.y, Bl1z = blackGoalPos1.z,
                Bl2x = blackGoalPos2.x, Bl2y = blackGoalPos2.y, Bl2z = blackGoalPos2.z,
                Cx = centerPos.x, Cy = centerPos.y, Cz = centerPos.z,
                Rqx = redGoalRot.x, Rqy = redGoalRot.y, Rqz = redGoalRot.z, Rqw = redGoalRot.w,
                Bqx = blueGoalRot.x, Bqy = blueGoalRot.y, Bqz = blueGoalRot.z, Bqw = blueGoalRot.w,
                Bl1qx = blackGoalRot1.x, Bl1qy = blackGoalRot1.y, Bl1qz = blackGoalRot1.z, Bl1qw = blackGoalRot1.w,
                Bl2qx = blackGoalRot2.x, Bl2qy = blackGoalRot2.y, Bl2qz = blackGoalRot2.z, Bl2qw = blackGoalRot2.w,
                Gw = GoalWidth, Gh = GoalHeight, Gd = GoalDepth,
                Lbx = lobbySpawnPos.x, Lby = lobbySpawnPos.y, Lbz = lobbySpawnPos.z,
                Lsx = loserSpawnPos.x, Lsy = loserSpawnPos.y, Lsz = loserSpawnPos.z
            };
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
                        redGoalPos = new Vector3(data.Rx, data.Ry, data.Rz);
                        blueGoalPos = new Vector3(data.Bx, data.By, data.Bz);
                        blackGoalPos1 = new Vector3(data.Bl1x, data.Bl1y, data.Bl1z);
                        blackGoalPos2 = new Vector3(data.Bl2x, data.Bl2y, data.Bl2z);
                        centerPos = new Vector3(data.Cx, data.Cy, data.Cz);
                        redGoalRot = new Quaternion(data.Rqx, data.Rqy, data.Rqz, data.Rqw);
                        blueGoalRot = new Quaternion(data.Bqx, data.Bqy, data.Bqz, data.Bqw);
                        blackGoalRot1 = new Quaternion(data.Bl1qx, data.Bl1qy, data.Bl1qz, data.Bl1qw);
                        blackGoalRot2 = new Quaternion(data.Bl2qx, data.Bl2qy, data.Bl2qz, data.Bl2qw);
                        if (data.Gw > 0) { GoalWidth = data.Gw; GoalHeight = data.Gh; GoalDepth = data.Gd; }
                    
                        // Load spawn positions
                        lobbySpawnPos = new Vector3(data.Lbx, data.Lby, data.Lbz);
                        loserSpawnPos = new Vector3(data.Lsx, data.Lsy, data.Lsz);
                        
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

        // ==========================================
        // 3. LIFECYCLE
        // ==========================================
        void OnServerInitialized()
        {
            LoadArenaData(); // Load saved goals
            
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
            }
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
            SendReply(player, "Arena Data Reloaded.");
            if (redGoalPos != Vector3.zero) DrawGoal(player, redGoalPos, redGoalRot, Color.red, 5f);
        }

        [ChatCommand("start_match")]
        private void CmdStartMatch(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            if (centerPos == Vector3.zero) { SendReply(player, "Error: Set Center first!"); return; }
            
            scoreRed = 0; scoreBlue = 0; scoreBlack = 0;
            matchNumber = 1;
            
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
            
            gameActive = true; matchStarted = true;
            
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

        [ChatCommand("set_red")] private void CmdSetRed(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ redGoalPos=p.transform.position; redGoalRot=p.transform.rotation; SendReply(p, "Red Goal Set."); DrawGoal(p, redGoalPos, redGoalRot, Color.red, 5f); }}
        [ChatCommand("set_blue")] private void CmdSetBlue(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ blueGoalPos=p.transform.position; blueGoalRot=p.transform.rotation; SendReply(p, "Blue Goal Set."); DrawGoal(p, blueGoalPos, blueGoalRot, Color.blue, 5f); }}
        [ChatCommand("set_black1")] private void CmdSetBlack1(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ blackGoalPos1=p.transform.position; blackGoalRot1=p.transform.rotation; SendReply(p, "Black Goal 1 Set (at Red position)."); DrawGoal(p, blackGoalPos1, blackGoalRot1, Color.black, 5f); }}
        [ChatCommand("set_black2")] private void CmdSetBlack2(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ blackGoalPos2=p.transform.position; blackGoalRot2=p.transform.rotation; SendReply(p, "Black Goal 2 Set (at Blue position)."); DrawGoal(p, blackGoalPos2, blackGoalRot2, Color.black, 5f); }}
        [ChatCommand("set_center")] private void CmdSetCenter(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ centerPos=p.transform.position; SendReply(p, "Center Set."); }}
        [ChatCommand("set_lobby_spawn")] 
        private void CmdSetLobbySpawn(BasePlayer p, string c, string[] a) 
        { 
            if(!p.IsAdmin) return;
            
            Puts($"[LobbySpawn] Admin {p.displayName} running /set_lobby_spawn at position {p.transform.position}");
            
            // Get position slightly above ground to prevent spawning in air
            RaycastHit hit;
            Puts($"[LobbySpawn] Performing raycast from {p.transform.position + Vector3.up} going down");
            if (Physics.Raycast(p.transform.position + Vector3.up, Vector3.down, out hit, 10f, LayerMask.GetMask("Terrain", "World", "Construction")))
            {
                lobbySpawnPos = hit.point + new Vector3(0, 0.5f, 0); // Slightly above ground
                Puts($"[LobbySpawn] Ground found at {hit.point}, setting spawn 0.5m above at: {lobbySpawnPos}");
            }
            else
            {
                lobbySpawnPos = p.transform.position;
                Puts($"[LobbySpawn] No ground found via raycast, using player position: {lobbySpawnPos}");
            }
            
            SaveArenaData();
            Puts($"[LobbySpawn] Lobby spawn saved to data file");
            SendReply(p, $"✓ Lobby spawn point set at {lobbySpawnPos}! Players will teleport here during lobby.");
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
        
        [ChatCommand("reset_ball")] private void CmdResetBall(BasePlayer p, string c, string[] a) { if(p.IsAdmin){ SpawnBall(); SendReply(p, "Ball Reset."); }}
        
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
            debugActive = !debugActive;
            if (debugTimer != null) debugTimer.Destroy();
            
            if (debugActive)
            {
                SendReply(player, "Debug ON - Showing all goals.");
                debugTimer = timer.Repeat(1.0f, 0, () => {
                    if (redGoalPos != Vector3.zero) 
                    {
                        Color col = activeGoals["red"] ? Color.red : new Color(0.5f, 0, 0, 0.3f);
                        DrawGoal(player, redGoalPos, redGoalRot, col, 1.0f);
                    }
                    if (blueGoalPos != Vector3.zero) 
                    {
                        Color col = activeGoals["blue"] ? Color.blue : new Color(0, 0, 0.5f, 0.3f);
                        DrawGoal(player, blueGoalPos, blueGoalRot, col, 1.0f);
                    }
                    if (blackGoalPos1 != Vector3.zero) 
                    {
                        Color col = activeGoals["black1"] ? Color.black : new Color(0.2f, 0.2f, 0.2f, 0.3f);
                        DrawGoal(player, blackGoalPos1, blackGoalRot1, col, 1.0f);
                    }
                    if (blackGoalPos2 != Vector3.zero) 
                    {
                        Color col = activeGoals["black2"] ? Color.black : new Color(0.2f, 0.2f, 0.2f, 0.3f);
                        DrawGoal(player, blackGoalPos2, blackGoalRot2, col, 1.0f);
                    }
                });
            }
            else SendReply(player, "Debug OFF.");
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

        [ConsoleCommand("select_team")]
        private void CmdSelectTeam(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || arg.Args == null || arg.Args.Length < 1) return;
            CuiHelper.DestroyUi(player, "TeamSelectUI");
            
            string team = arg.Args[0].ToLower();
            redTeam.Remove(player.userID);
            blueTeam.Remove(player.userID);
            blackTeam.Remove(player.userID);
            playerRoles.Remove(player.userID);
            ballRangeState.Remove(player.userID);
            
            CuiHelper.DestroyUi(player, "BallRangeHUD"); 
            CuiHelper.DestroyUi(player, "LeashHUD");

            if (team == "red") { redTeam.Add(player.userID); CheckRole(player, "red"); }
            else if (team == "blue") { blueTeam.Add(player.userID); CheckRole(player, "blue"); }
            else if (team == "black") { blackTeam.Add(player.userID); CheckRole(player, "black"); }
        }

        private void CheckRole(BasePlayer player, string team)
        {
            int goalies = 0;
            List<ulong> list = (team == "red") ? redTeam : (team == "blue") ? blueTeam : blackTeam;
            foreach(ulong id in list) if(playerRoles.ContainsKey(id) && playerRoles[id] == "Goalie") goalies++;

            if(goalies == 0) ShowRoleUI(player, team);
            else AssignRole(player, "Striker");
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
                // Player kill messages
                killerName = killer.displayName;
                killerTeam = redTeam.Contains(killer.userID) ? "red" :
                            blueTeam.Contains(killer.userID) ? "blue" : "black";
                
                var messages = GetFunnyKillMessages();
                message = messages[UnityEngine.Random.Range(0, messages.Count)];
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
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID))
                {
                    ShowKillFeed(player);
                }
            }
        }
        
        // Show kill feed UI
        private void ShowKillFeed(BasePlayer player)
        {
            var container = new CuiElementContainer();
            
            float yPos = 0.85f; // Start from top
            int index = 0;
            
            foreach (var entry in killFeed)
            {
                if (index >= MAX_KILL_FEED_ENTRIES) break;
                
                // Fade effect based on age
                float age = UnityEngine.Time.time - entry.Timestamp;
                float alpha = Mathf.Clamp(1f - (age / 10f), 0.3f, 1f);
                
                // Get team colors
                string killerColor = teamConfigs[entry.KillerTeam].HexColor;
                string victimColor = teamConfigs[entry.VictimTeam].HexColor;
                
                // Background panel
                container.Add(new CuiPanel
                {
                    Image = { Color = $"0.1 0.1 0.1 {0.8f * alpha}" },
                    RectTransform = { AnchorMin = "0.01 " + (yPos - index * 0.05f - 0.045f), AnchorMax = "0.35 " + (yPos - index * 0.05f) }
                }, "Hud", $"KillFeed_{index}");
                
                // Killer name (team colored)
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = entry.KillerName, 
                        FontSize = 14, 
                        Align = TextAnchor.MiddleLeft,
                        Color = $"{GetColorFromHex(killerColor)} {alpha}",
                        Font = "robotocondensed-bold.ttf"
                    },
                    RectTransform = { AnchorMin = "0.02 0.2", AnchorMax = "0.4 0.8" }
                }, $"KillFeed_{index}");
                
                // Kill icon/message
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = "☠", 
                        FontSize = 18, 
                        Align = TextAnchor.MiddleCenter,
                        Color = $"1 0 0 {alpha}"
                    },
                    RectTransform = { AnchorMin = "0.38 0.2", AnchorMax = "0.48 0.8" }
                }, $"KillFeed_{index}");
                
                // Victim name (team colored)
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = entry.VictimName, 
                        FontSize = 14, 
                        Align = TextAnchor.MiddleLeft,
                        Color = $"{GetColorFromHex(victimColor)} {alpha}",
                        Font = "robotocondensed-bold.ttf"
                    },
                    RectTransform = { AnchorMin = "0.5 0.2", AnchorMax = "0.98 0.8" }
                }, $"KillFeed_{index}");
                
                // Funny message subtitle
                container.Add(new CuiLabel
                {
                    Text = { 
                        Text = entry.Message, 
                        FontSize = 10, 
                        Align = TextAnchor.MiddleCenter,
                        Color = $"0.8 0.8 0.8 {alpha * 0.7f}",
                        Font = "robotocondensed-regular.ttf"
                    },
                    RectTransform = { AnchorMin = "0.02 0.0", AnchorMax = "0.98 0.25" }
                }, $"KillFeed_{index}");
                
                index++;
            }
            
            CuiHelper.DestroyUi(player, "KillFeedContainer");
            if (container.Count > 0)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, "Hud", "KillFeedContainer");
                
                CuiHelper.AddUi(player, container);
            }
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
        private void GiveKit(BasePlayer player, string role)
        {
            player.inventory.Strip();
            
            // Determine which team the player is on
            string team = redTeam.Contains(player.userID) ? "red" : 
                         blueTeam.Contains(player.userID) ? "blue" : "black";
            TeamSkins skins = teamSkins[team];
            
            if (role == "Striker") 
            {
                // Striker Kit (All positions except Goalie)
                GiveItemWithSkin(player, "tshirt", 1, skins.TshirtSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "pants", 1, skins.PantsSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "metal.plate.torso", 1, skins.TorsoSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "metal.facemask", 1, skins.FacemaskSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "burlap.shoes", 1, skins.ShoesSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "smg.thompson", 1, skins.WeaponSkin, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 5), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("barricade.wood.cover", 3), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.pistol", 200), player.inventory.containerMain);
                player.SetMaxHealth(100); 
                player.health = 100;
            } 
            else // Goalie
            {
                GiveItemWithSkin(player, "tshirt", 1, skins.TshirtSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "heavy.plate.pants", 1, skins.GoaliePantsSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "heavy.plate.jacket", 1, skins.GoalieJacketSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "metal.facemask", 1, skins.FacemaskSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "burlap.shoes", 1, skins.ShoesSkin, player.inventory.containerWear);
                GiveItemWithSkin(player, "shotgun.spas12", 1, skins.GoalieWeaponSkin, player.inventory.containerBelt);
                player.inventory.GiveItem(ItemManager.CreateByName("syringe.medical", 10), player.inventory.containerMain);
                player.inventory.GiveItem(ItemManager.CreateByName("ammo.shotgun", 64), player.inventory.containerMain);
                player.SetMaxHealth(200); 
                player.health = 200;
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
            
            // Additional delayed update to ensure visibility
            NextTick(() => {
                if (player != null && player.IsConnected)
                {
                    player.SendNetworkUpdate();
                }
            });
        }
        
        private void GiveItemWithSkin(BasePlayer player, string itemName, int amount, ulong skinId, ItemContainer container)
        {
            Item item = ItemManager.CreateByName(itemName, amount, skinId);
            if (item != null)
            {
                // Explicitly set skin ID
                item.skin = skinId;
                
                // Add item to inventory
                if (player.inventory.GiveItem(item, container))
                {
                    // Mark item as dirty to force network update
                    item.MarkDirty();
                    
                    // Force container update
                    container.MarkDirty();
                }
            }
            else
            {
                Puts($"ERROR: Failed to create item '{itemName}' for player {player.displayName}");
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
                if (role == "Goalie")
                {
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
            var config = teamConfigs[team];
            string p = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.9" }, RectTransform = { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" }, CursorEnabled = true }, "Overlay", "RoleSelectUI");
            c.Add(new CuiLabel { Text = { Text = $"CHOOSE ROLE - {config.Name}", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = config.Color + " 1" }, RectTransform = { AnchorMin = "0 0.8", AnchorMax = "1 1" } }, p);
            c.Add(new CuiLabel { Text = { Text = $"({config.Tag})", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0.7", AnchorMax = "1 0.8" } }, p);
            c.Add(new CuiButton { Button = { Command = "select_role Striker", Color = "0.2 0.6 0.2 1" }, Text = { Text = "STRIKER", FontSize = 16, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.1 0.2", AnchorMax = "0.45 0.6" } }, p);
            c.Add(new CuiButton { Button = { Command = "select_role Goalie", Color = "0.8 0.4 0.1 1" }, Text = { Text = "GOALIE", FontSize = 16, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.55 0.2", AnchorMax = "0.9 0.6" } }, p);
            CuiHelper.AddUi(player, c);
        }

        private void ShowTeamSelectUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "TeamSelectUI");
            var c = new CuiElementContainer();
            string panel = c.Add(new CuiPanel { Image = { Color = "0 0 0 0.95" }, RectTransform = { AnchorMin = "0.25 0.25", AnchorMax = "0.75 0.75" }, CursorEnabled = true }, "Overlay", "TeamSelectUI");
            
            // Title
            c.Add(new CuiLabel { Text = { Text = "SELECT YOUR TEAM", FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.98" } }, panel);
            
            // Blue Team Button
            var blueConfig = teamConfigs["blue"];
            string blueBtn = c.Add(new CuiButton { Button = { Command = "select_team blue", Color = blueConfig.Color + " 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = "0.05 0.55", AnchorMax = "0.32 0.78" } }, panel);
            c.Add(new CuiLabel { Text = { Text = blueConfig.Name, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.35" } }, blueBtn);
            c.Add(new CuiLabel { Text = { Text = $"[{blueConfig.Tag}]", FontSize = 18, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.4", AnchorMax = "0.95 0.7" } }, blueBtn);
            c.Add(new CuiLabel { Text = { Text = $"{blueTeam.Count} Players", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.95 0.95" } }, blueBtn);
            
            // Red Team Button
            var redConfig = teamConfigs["red"];
            string redBtn = c.Add(new CuiButton { Button = { Command = "select_team red", Color = redConfig.Color + " 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = "0.36 0.55", AnchorMax = "0.64 0.78" } }, panel);
            c.Add(new CuiLabel { Text = { Text = redConfig.Name, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.35" } }, redBtn);
            c.Add(new CuiLabel { Text = { Text = $"[{redConfig.Tag}]", FontSize = 18, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.4", AnchorMax = "0.95 0.7" } }, redBtn);
            c.Add(new CuiLabel { Text = { Text = $"{redTeam.Count} Players", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.95 0.95" } }, redBtn);
            
            // Black Team Button
            var blackConfig = teamConfigs["black"];
            string blackBtn = c.Add(new CuiButton { Button = { Command = "select_team black", Color = "0.3 0.3 0.3 0.8" }, Text = { Text = "", FontSize = 1 }, RectTransform = { AnchorMin = "0.68 0.55", AnchorMax = "0.95 0.78" } }, panel);
            c.Add(new CuiLabel { Text = { Text = blackConfig.Name, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.35" } }, blackBtn);
            c.Add(new CuiLabel { Text = { Text = $"[{blackConfig.Tag}]", FontSize = 18, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "1 1 1 1" }, RectTransform = { AnchorMin = "0.05 0.4", AnchorMax = "0.95 0.7" } }, blackBtn);
            c.Add(new CuiLabel { Text = { Text = $"{blackTeam.Count} Players", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7" }, RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.95 0.95" } }, blackBtn);
            
            // Team descriptions
            c.Add(new CuiLabel { Text = { Text = "Fast & Agile", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }, RectTransform = { AnchorMin = "0.05 0.45", AnchorMax = "0.32 0.53" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "Tactical & Strong", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }, RectTransform = { AnchorMin = "0.36 0.45", AnchorMax = "0.64 0.53" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "Coordinated & Deadly", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" }, RectTransform = { AnchorMin = "0.68 0.45", AnchorMax = "0.95 0.53" } }, panel);
            
            // Instructions
            c.Add(new CuiLabel { Text = { Text = "Click a team to join the battle!", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.8" }, RectTransform = { AnchorMin = "0 0.15", AnchorMax = "1 0.25" } }, panel);
            c.Add(new CuiLabel { Text = { Text = "You can also use: /join blue, /join red, /join black", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5" }, RectTransform = { AnchorMin = "0 0.08", AnchorMax = "1 0.15" } }, panel);
            
            CuiHelper.AddUi(player, c);
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
        void OnPlayerRespawn(BasePlayer player)
        {
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
        }
        
        // Skip respawn screen - instant respawn
        object OnPlayerRespawnOnMap(BasePlayer player, Vector3 position)
        {
            if (matchStarted && (redTeam.Contains(player.userID) || blueTeam.Contains(player.userID) || blackTeam.Contains(player.userID)))
            {
                // Skip respawn screen by respawning immediately
                return null;
            }
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
        
        void OnEntityBuilt(Planner plan, GameObject go, BasePlayer player)
        {
            BaseEntity entity = go.ToBaseEntity();
            if (entity == null || player == null) return;
            
            // Auto-destroy ALL player-placed entities after 7 seconds (ALWAYS active, not just during matches)
            string shortName = entity.ShortPrefabName ?? "barricade.wood.cover";
            string fullName = entity.PrefabName ?? "";
            
            Puts($"[EntityBuilt] Player {player.displayName} placed {shortName} at {entity.transform.position}");
            
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
                                 entity is BuildingBlock ||
                                 entity is Deployable;
            
            if (shouldDestroy)
            {
                Puts($"[EntityBuilt] Entity {shortName} WILL be destroyed in 7 seconds (ID: {entity.net.ID})");
                SendReply(player, $"⚠ {shortName} will auto-destroy in 7 seconds!");
                
                // Store timer reference
                var destroyTimer = timer.Once(7f, () =>
                {
                    Puts($"[EntityDestroy] Timer fired for {shortName} ID: {entity.net.ID}");
                    
                    if (entity != null && !entity.IsDestroyed)
                    {
                        Puts($"[EntityDestroy] Entity still exists, proceeding with destruction");
                        Puts($"[EntityDestroy] Destroying {shortName} ID: {entity.net.ID}");
                        entity.Kill(BaseNetworkable.DestroyMode.None);
                        Puts($"[EntityDestroy] Entity destroyed successfully");
                    }
                    else
                    {
                        Puts($"[EntityDestroy] Entity already destroyed or null ID: {entity?.net.ID}");
                    }
                    
                    // Clean up timer reference
                    if (entity != null)
                    {
                        entityTimers.Remove(entity.net.ID);
                        Puts($"[EntityDestroy] Removed timer from tracking dictionary");
                    }
                });
                
                entityTimers[entity.net.ID] = destroyTimer;
                Puts($"[EntityBuilt] Timer stored with ID: {entity.net.ID}");
            }
            else
            {
                Puts($"[EntityBuilt] Entity {shortName} will NOT be destroyed (not in destruction list)");
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

        private void SpawnBall()
        {
            if (activeBall != null && !activeBall.IsDestroyed) activeBall.Kill();
            string prefab = "assets/content/vehicles/ball/ball.entity.prefab";
            activeBall = GameManager.server.CreateEntity(prefab, centerPos + new Vector3(0, 2, 0));
            if (activeBall == null) { prefab = "assets/prefabs/misc/soccerball/soccerball.prefab"; activeBall = GameManager.server.CreateEntity(prefab, centerPos + new Vector3(0, 1, 0)); }
            if (activeBall == null) return;
            activeBall.Spawn();
            
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
                timer.Once(5f, () => { foreach(var p in BasePlayer.activePlayerList) { CuiHelper.DestroyUi(p, "SoccerScoreboard"); CuiHelper.DestroyUi(p, "SoccerTicker"); CuiHelper.DestroyUi(p, "BallRangeHUD"); CuiHelper.DestroyUi(p, "LeashHUD"); } });
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
            
            // Start lobby countdown
            timer.Once(15f, () => StartLobbyCountdown(30));
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
            Vector3[] p = new Vector3[8];
            p[0]=c+r*new Vector3(-hw,-hh,-hd); p[1]=c+r*new Vector3(hw,-hh,-hd); p[2]=c+r*new Vector3(hw,-hh,hd); p[3]=c+r*new Vector3(-hw,-hh,hd);
            p[4]=c+r*new Vector3(-hw,hh,-hd); p[5]=c+r*new Vector3(hw,hh,-hd); p[6]=c+r*new Vector3(hw,hh,hd); p[7]=c+r*new Vector3(-hw,hh,hd);
            player.SendConsoleCommand("ddraw.line", dur, col, p[0], p[1]); player.SendConsoleCommand("ddraw.line", dur, col, p[1], p[2]); player.SendConsoleCommand("ddraw.line", dur, col, p[2], p[3]); player.SendConsoleCommand("ddraw.line", dur, col, p[3], p[0]);
            player.SendConsoleCommand("ddraw.line", dur, col, p[4], p[5]); player.SendConsoleCommand("ddraw.line", dur, col, p[5], p[6]); player.SendConsoleCommand("ddraw.line", dur, col, p[6], p[7]); player.SendConsoleCommand("ddraw.line", dur, col, p[7], p[4]);
            player.SendConsoleCommand("ddraw.line", dur, col, p[0], p[4]); player.SendConsoleCommand("ddraw.line", dur, col, p[1], p[5]); player.SendConsoleCommand("ddraw.line", dur, col, p[2], p[6]); player.SendConsoleCommand("ddraw.line", dur, col, p[3], p[7]);
            player.SendConsoleCommand("ddraw.text", dur, col, c + new Vector3(0, hh + 2f, 0), "GOAL ZONE");
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