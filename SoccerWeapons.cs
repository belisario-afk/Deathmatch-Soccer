using System.Collections.Generic;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    /*
     * SoccerWeapons - Advanced weapon abilities for Deathmatch Soccer
     * 
     * WEAPON ABILITIES:
     * 
     * 1. MULTIPLE GRENADE LAUNCHER (MGL) - Medi-Launcher
     *    - Shoots healing projectiles that restore 50HP + 20 hydration
     *    - Projectile has reduced gravity and drag
     *    - Heals on direct hit to players
     * 
     * 2. SNOWBALL GUN - Magnet
     *    - Shoots vacuum projectiles that pull the ball
     *    - 25m radius vacuum effect on impact
     *    - Pulls ball with 50 force + 8 upward force
     *    - 3.0s cooldown between shots (prevents spam with only 1 snowball)
     * 
     * 3. NAILGUN PISTOL - Yellow Card
     *    - Tackles enemy players, wounding them for 3 seconds
     *    - 100m range, 0.5m beam thickness (SphereCast)
     *    - 2.0s cooldown between shots (prevents spam with only 6 nails)
     *    - Only hits players (ignores walls/ground via layer mask)
     * 
     * 4. PYTHON REVOLVER - Phase Shift
     *    - Teleports player to ball location
     *    - Swaps player and ball positions
     *    - 100m maximum range
     * 
     * 5. CROSSBOW - Whistle
     *    - Freezes ball mid-air for 2 seconds
     *    - Ball becomes kinematic (stops all physics)
     *    - 100m maximum range
     * 
     * 6. BASEBALL BAT - Home Run
     *    - Launches ball with 45 force on melee hit
     *    - Ball becomes "charged" for 2 seconds (can score on contact)
     *    - Adds upward arc to trajectory
     * 
     * 7. NIGHT VISION GOGGLES - ESP
     *    - Shows all players through walls with rainbow skeleton
     *    - 150m radius, updates every 0.1s
     *    - Displays player name and distance
     *    - NPCs shown in yellow, players in rainbow
     * 
     * CONFIGURATION:
     * All weapon mechanics are configured via constants at the top of the file.
     * Adjust these values to balance gameplay:
     * - Ranges, forces, cooldowns, durations, etc.
     */
    [Info("SoccerWeapons", "Jess", "11.4.0")]
    [Description("MGL=Heal, Snowball=Magnet, Nailgun=YellowCard (Fixed), Bat=HomeRun, NVG=ESP")]
    public class SoccerWeapons : RustPlugin
    {
        // ==========================================================================
        // CONFIGURATION
        // ==========================================================================
        
        // Cooldown tracking to prevent spam
        private Dictionary<ulong, float> tackleLastFired = new Dictionary<ulong, float>(); // Yellow Card (Nailgun)
        private Dictionary<ulong, float> magnetLastFired = new Dictionary<ulong, float>(); // Magnet (Snowball)
        
        // Track whether we've shown cooldown message to prevent spam
        private Dictionary<ulong, bool> tackleCooldownMessageShown = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> magnetCooldownMessageShown = new Dictionary<ulong, bool>();
        
        // WEAPONS
        private const string Medi_GunShortname = "multiplegrenadelauncher";
        private const string Medi_ItemToDrop = "largemedkit";
        private const float Medi_SpeedMultiplier = 0.5f;
        private const float Medi_HealAmount = 50f;
        private const float Medi_HydrationAmount = 20f;

        private const string Magnet_GunShortname = "snowballgun"; 
        private const string Magnet_ItemToDrop = "snowball"; 
        private const float Magnet_Speed = 60f; 
        private const float Magnet_Radius = 25f; // Vacuum radius
        private const float Magnet_Force = 50f; // Pull force
        private const float Magnet_Cooldown = 3.0f; // Fire rate cooldown (3.0s between shots)

        private const string Tackle_GunShortname = "pistol.nailgun"; // Updated to pistol.nailgun
        private const float Tackle_Duration = 3.0f; 
        private const float Tackle_Range = 100f; // Maximum range
        private const float Tackle_Radius = 0.5f; // Spherecast radius (beam thickness)
        private const float Tackle_Cooldown = 2.0f; // Fire rate cooldown (2.0s between shots)

        private const string Phase_GunShortname = "pistol.python";
        private const float Phase_Range = 100f; // Maximum teleport range
        
        private const string Whistle_GunShortname = "crossbow";
        private const float Whistle_FreezeTime = 2.0f;
        private const float Whistle_Range = 100f; // Maximum freeze range

        private const string Bat_Shortname = "mace.baseballbat";
        private const float Bat_HitForce = 45f; 
        private const float Bat_ChargeTime = 2.0f;

        // ESP
        private const string Esp_Shortname = "nightvisiongoggles";
        private const float Esp_Radius = 150f;
        private const float Esp_RefreshRate = 0.1f; 

        // ASSETS
        private const string FX_Explosion = "assets/prefabs/tools/c4/effects/c4_explosion.prefab";
        private const string FX_Magic = "assets/bundled/prefabs/fx/gestures/magic_glimmer_1.prefab";
        private const string FX_PhantomSmoke = "assets/bundled/prefabs/fx/smoke_rocket_explosion.prefab"; 

        // ==========================================================================
        // INITIALIZATION
        // ==========================================================================
        
        void OnServerInitialized()
        {
            timer.Every(Esp_RefreshRate, () => RunESPLoop());
        }

        // ==========================================================================
        // ADVANCED ESP LOGIC
        // ==========================================================================
        
        void RunESPLoop()
        {
            Color rainbow = Color.HSVToRGB((Time.time * 0.5f) % 1.0f, 1f, 1f);
            float duration = Esp_RefreshRate + 0.02f;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected || player.IsSleeping()) continue;
                if (player.inventory == null || player.inventory.containerWear == null) continue;

                bool hasGoggles = false;
                foreach (var item in player.inventory.containerWear.itemList)
                {
                    if (item.info.shortname == Esp_Shortname) { hasGoggles = true; break; }
                }

                if (hasGoggles) DrawUltimateESP(player, rainbow, duration);
            }
        }

        void DrawUltimateESP(BasePlayer observer, Color color, float duration)
        {
            List<BasePlayer> nearby = new List<BasePlayer>();
            Vis.Entities(observer.transform.position, Esp_Radius, nearby);

            foreach (var target in nearby)
            {
                if (target == observer || target.IsDead()) continue;

                float distance = Vector3.Distance(observer.transform.position, target.transform.position);
                Color c = target.IsNpc ? Color.yellow : color;
                
                // Distance-based intensity (brighter when closer)
                float intensity = Mathf.Clamp01(1.0f - (distance / Esp_Radius));
                c = Color.Lerp(c, c * 1.5f, intensity);

                // HEALTH BAR
                Vector3 headPos = target.eyes.position + new Vector3(0, 0.5f, 0);
                float healthPercent = target.health / target.MaxHealth();
                Color healthColor = healthPercent > 0.7f ? Color.green : (healthPercent > 0.3f ? Color.yellow : Color.red);
                Vector3 barStart = headPos + new Vector3(-0.3f, 0, 0);
                Vector3 barEnd = barStart + new Vector3(0.6f * healthPercent, 0, 0);
                observer.SendConsoleCommand("ddraw.line", duration, healthColor, barStart, barEnd);
                
                // WEAPON INFO
                Item activeItem = target.GetActiveItem();
                string weaponInfo = activeItem != null ? activeItem.info.displayName.english : "Unarmed";
                observer.SendConsoleCommand("ddraw.text", duration, Color.cyan, headPos + new Vector3(0, 0.15f, 0), weaponInfo);

                // STATUS INDICATORS
                string status = "";
                if (target.IsWounded()) status += "[WOUNDED] ";
                if (target.metabolism.bleeding.value > 0) status += "[BLEEDING] ";
                if (target.IsSleeping()) status += "[SLEEPING] ";
                if (!string.IsNullOrEmpty(status))
                    observer.SendConsoleCommand("ddraw.text", duration, Color.red, headPos + new Vector3(0, 0.3f, 0), status);

                // TALL BOX
                DrawPlayerBox(observer, target, c, duration);

                // ENHANCED SKELETON - Simplified to core bones that exist
                // Use player body positions directly where possible
                Vector3 head = target.eyes.position;
                Vector3 chest = target.CenterPoint();
                Vector3 pelvis = target.transform.position + new Vector3(0, 0.5f, 0);
                Vector3 feet = target.transform.position;
                
                // Core skeleton lines
                observer.SendConsoleCommand("ddraw.line", duration, c, head, chest);  // head to chest
                observer.SendConsoleCommand("ddraw.line", duration, c, chest, pelvis); // chest to pelvis
                observer.SendConsoleCommand("ddraw.line", duration, c, pelvis, feet);  // pelvis to feet
                
                // Try to draw limbs using bone positions if available
                // Arms
                DrawBoneLine(observer, target, "l_upperarm", "l_forearm", c, duration);
                DrawBoneLine(observer, target, "l_forearm", "l_hand", c, duration);
                DrawBoneLine(observer, target, "r_upperarm", "r_forearm", c, duration);
                DrawBoneLine(observer, target, "r_forearm", "r_hand", c, duration);
                
                // Legs
                DrawBoneLine(observer, target, "l_hip", "l_knee", c, duration);
                DrawBoneLine(observer, target, "l_knee", "l_ankle", c, duration);
                DrawBoneLine(observer, target, "r_hip", "r_knee", c, duration);
                DrawBoneLine(observer, target, "r_knee", "r_ankle", c, duration);

                // TARGETING AIDS
                // Head sphere for headshot targeting - use eyes position (most reliable)
                observer.SendConsoleCommand("ddraw.sphere", duration, Color.red, target.eyes.position, 0.15f);
                
                // Chest sphere for center mass - use CenterPoint() built-in method
                observer.SendConsoleCommand("ddraw.sphere", duration, Color.cyan, target.CenterPoint(), 0.2f);

                // MOVEMENT PREDICTION - Velocity vector
                if (target.estimatedVelocity.magnitude > 0.1f)
                {
                    Vector3 velocityEnd = target.transform.position + target.estimatedVelocity.normalized * 2f;
                    observer.SendConsoleCommand("ddraw.arrow", duration, Color.magenta, target.transform.position, velocityEnd, 0.1f);
                }

                // ENHANCED NAME TAG
                string dist = $"{(int)distance}m";
                string displayText = $"{target.displayName} [{dist}]\nHP: {(int)target.health}";
                observer.SendConsoleCommand("ddraw.text", duration, Color.white, headPos + new Vector3(0, 0.6f, 0), displayText);
            }
        }

        void DrawBoneLine(BasePlayer observer, BasePlayer target, string b1, string b2, Color c, float d)
        {
            if (target.model == null) return;
            
            var bone1 = target.model.FindBone(b1);
            var bone2 = target.model.FindBone(b2);
            
            if (bone1 != null && bone2 != null)
            {
                // Bone transforms are already in world space - just use their position directly
                // The transform.position property returns world position
                Vector3 worldPos1 = bone1.transform.position;
                Vector3 worldPos2 = bone2.transform.position;
                observer.SendConsoleCommand("ddraw.line", d, c, worldPos1, worldPos2);
            }
        }

        void DrawPlayerBox(BasePlayer observer, BasePlayer target, Color c, float d)
        {
            Vector3 pos = target.transform.position;
            float w = 0.4f; float h = 1.9f;
            Vector3 b1 = pos + new Vector3(w, 0, w); Vector3 b2 = pos + new Vector3(-w, 0, w);
            Vector3 b3 = pos + new Vector3(-w, 0, -w); Vector3 b4 = pos + new Vector3(w, 0, -w);
            Vector3 t1 = b1 + new Vector3(0, h, 0); Vector3 t2 = b2 + new Vector3(0, h, 0);
            Vector3 t3 = b3 + new Vector3(0, h, 0); Vector3 t4 = b4 + new Vector3(0, h, 0);

            observer.SendConsoleCommand("ddraw.line", d, c, b1, b2); observer.SendConsoleCommand("ddraw.line", d, c, b2, b3);
            observer.SendConsoleCommand("ddraw.line", d, c, b3, b4); observer.SendConsoleCommand("ddraw.line", d, c, b4, b1);
            observer.SendConsoleCommand("ddraw.line", d, c, t1, t2); observer.SendConsoleCommand("ddraw.line", d, c, t2, t3);
            observer.SendConsoleCommand("ddraw.line", d, c, t3, t4); observer.SendConsoleCommand("ddraw.line", d, c, t4, t1);
            observer.SendConsoleCommand("ddraw.line", d, c, b1, t1); observer.SendConsoleCommand("ddraw.line", d, c, b2, t2);
            observer.SendConsoleCommand("ddraw.line", d, c, b3, t3); observer.SendConsoleCommand("ddraw.line", d, c, b4, t4);
        }

        // ==========================================================================
        // DEBUG COMMAND: "/hitme"
        // ==========================================================================
        [ChatCommand("hitme")]
        void CmdHitMe(BasePlayer player, string command, string[] args)
        {
            string targetPath = "assets/content/vehicles/ball/ball.item.prefab";
            ItemDefinition ballDef = null;
            foreach (var def in ItemManager.itemList) {
                if (def.worldModelPrefab != null && def.worldModelPrefab.isValid && def.worldModelPrefab.resourcePath == targetPath) {
                    ballDef = def; break;
                }
            }
            if (ballDef == null) { player.ChatMessage("Error: Ball item definition not found."); return; }

            Item item = ItemManager.CreateByItemID(ballDef.itemid, 1);
            if (item == null) return;

            Vector3 spawnPos = player.transform.position + (player.eyes.BodyForward() * 10f);
            spawnPos.y += 2.0f; 

            BaseEntity ball = item.Drop(spawnPos, Vector3.zero);
            if (ball == null) return;

            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.isKinematic = false;
                rb.WakeUp();
                Vector3 direction = (player.eyes.position - spawnPos).normalized;
                rb.velocity = direction * 40f; 
            }

            ChargedBall script = ball.gameObject.AddComponent<ChargedBall>();
            script.Shooter = null; 
            script.LifeTime = 5.0f;
            script.Activate();
            player.ChatMessage("INCOMING!");
        }

        // ==========================================================================
        // HOOKS
        // ==========================================================================
        
        // Block base game projectile firing for snowballgun and nailgun
        // This prevents wasting ammo on projectiles that do nothing
        object OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            if (player == null) return null;
            Item heldItem = player.GetActiveItem();
            if (heldItem == null) return null;
            string weaponName = heldItem.info.shortname;
            
            // Block base projectile firing for ability weapons
            if (weaponName == Magnet_GunShortname || weaponName == Tackle_GunShortname)
            {
                // Return false to prevent the attack/ammo consumption
                return false;
            }
            
            return null;
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (projectile == null || player == null) return;
            Item heldItem = player.GetActiveItem();
            if (heldItem == null) return;
            string weaponName = heldItem.info.shortname;

            if (weaponName == Magnet_GunShortname)
            {
                // Check cooldown for Magnet FIRST
                float lastFired;
                bool onCooldown = false;
                if (magnetLastFired.TryGetValue(player.userID, out lastFired))
                {
                    float timeSince = Time.time - lastFired;
                    if (timeSince < Magnet_Cooldown)
                    {
                        onCooldown = true;
                        // Only show message once per cooldown period
                        bool messageShown;
                        if (!magnetCooldownMessageShown.TryGetValue(player.userID, out messageShown) || !messageShown)
                        {
                            float remaining = Magnet_Cooldown - timeSince;
                            player.ChatMessage($"<color=#00ffff>Cooldown!</color> Wait {remaining:F1}s before using Magnet again.");
                            magnetCooldownMessageShown[player.userID] = true;
                        }
                    }
                    else
                    {
                        // Cooldown expired, clear message flag
                        magnetCooldownMessageShown[player.userID] = false;
                    }
                }
                
                // ALWAYS clear base game projectiles - prevent projectile spawn
                if (projectiles != null && projectiles.projectiles != null)
                {
                    projectiles.projectiles.Clear();
                }
                
                // Only refund ammo if on cooldown (valid shots consume ammo normally)
                if (onCooldown)
                {
                    if (heldItem.GetHeldEntity() is BaseProjectile baseProj)
                    {
                        baseProj.primaryMagazine.contents++;
                        baseProj.SendNetworkUpdateImmediate();
                    }
                    return; // PREVENT the gun from firing during cooldown
                }
                
                // Valid shot - update cooldown and fire ability
                magnetLastFired[player.userID] = Time.time;
                magnetCooldownMessageShown[player.userID] = false; // Reset message flag
                
                Vector3 spawnPos = player.eyes.position + (player.eyes.BodyForward() * 1.5f);
                Vector3 velocity = player.eyes.BodyForward() * Magnet_Speed;
                SpawnProjectile(spawnPos, velocity, player, Magnet_ItemToDrop, false);
            }
            else if (weaponName == Tackle_GunShortname)
            {
                // ALWAYS clear base game projectiles - prevent projectile spawn
                if (projectiles != null && projectiles.projectiles != null)
                {
                    projectiles.projectiles.Clear();
                }
                
                ShootYellowCard(player, heldItem);
            }
            else if (weaponName == Phase_GunShortname) ShootPhaseShift(player);
            else if (weaponName == Whistle_GunShortname) ShootWhistle(player);
        }

        void OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.HitEntity == null) return;
            Item held = player.GetActiveItem();
            if (held == null || held.info.shortname != Bat_Shortname) return;

            if (info.HitEntity.ShortPrefabName.Contains("ball")) HandleBaseballHit_Ball(player, info.HitEntity);
            else if (info.HitEntity is BasePlayer) Effect.server.Run(FX_Explosion, info.HitEntity.transform.position);
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            BaseEntity baseEntity = entity as BaseEntity;
            if (baseEntity == null || baseEntity.ShortPrefabName == null) return;
            if (baseEntity is TimedExplosive && baseEntity.ShortPrefabName.Contains("40mm")) HandleMGL(baseEntity as TimedExplosive);
        }

        // ==========================================================================
        // LOGIC: GUNS & TOOLS
        // ==========================================================================
        
        void HandleBaseballHit_Ball(BasePlayer player, BaseEntity ball)
        {
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb == null) return;
            if (rb.isKinematic) rb.isKinematic = false;
            rb.WakeUp();

            Vector3 direction = player.eyes.BodyForward(); 
            direction += Vector3.up * 0.25f; 
            rb.velocity = Vector3.zero; 
            rb.AddForce(direction.normalized * Bat_HitForce, ForceMode.VelocityChange); 

            ChargedBall script = ball.GetComponent<ChargedBall>();
            if (script == null) script = ball.gameObject.AddComponent<ChargedBall>();
            script.Shooter = player;
            script.LifeTime = Bat_ChargeTime;
            script.Activate();

            Effect.server.Run("assets/bundled/prefabs/fx/impacts/blunt/metal_hit_metal.prefab", ball.transform.position);
            player.ChatMessage("<color=#ff0000>HOME RUN!</color>");
        }

        void HandleMGL(TimedExplosive explosive)
        {
            BasePlayer shooter = explosive.creatorEntity as BasePlayer;
            if (shooter == null) return;
            Item activeItem = shooter.GetActiveItem();
            if (activeItem?.info?.shortname != Medi_GunShortname) return;
            Vector3 startPos = explosive.transform.position;
            Vector3 velocity = Vector3.zero;
            Rigidbody rb = explosive.GetComponent<Rigidbody>();
            if (rb != null) velocity = rb.velocity;
            else if (shooter.eyes != null) velocity = shooter.eyes.BodyForward() * 50f;
            velocity = velocity * Medi_SpeedMultiplier;
            explosive.Kill();
            SpawnProjectile(startPos, velocity, shooter, Medi_ItemToDrop, true);
        }

        // --- FIXED: Yellow Card now uses a Layer Mask to hit ONLY players with cooldown prevention ---
        void ShootYellowCard(BasePlayer player, Item heldItem)
        {
            // Check cooldown
            float lastFired;
            bool onCooldown = false;
            if (tackleLastFired.TryGetValue(player.userID, out lastFired))
            {
                float timeSince = Time.time - lastFired;
                if (timeSince < Tackle_Cooldown)
                {
                    onCooldown = true;
                    // Only show message once per cooldown period
                    bool messageShown;
                    if (!tackleCooldownMessageShown.TryGetValue(player.userID, out messageShown) || !messageShown)
                    {
                        float remaining = Tackle_Cooldown - timeSince;
                        player.ChatMessage($"<color=#ff0000>Cooldown!</color> Wait {remaining:F1}s before tackling again.");
                        tackleCooldownMessageShown[player.userID] = true;
                    }
                    
                    // Refund ammo during cooldown
                    if (heldItem != null && heldItem.GetHeldEntity() is BaseProjectile baseProj)
                    {
                        baseProj.primaryMagazine.contents++;
                        baseProj.SendNetworkUpdateImmediate();
                    }
                    return;
                }
                else
                {
                    // Cooldown expired, clear message flag
                    tackleCooldownMessageShown[player.userID] = false;
                }
            }
            
            RaycastHit hit;
            // The mask "Player (Server)" ensures we ignore ground, walls, and invisible barriers
            int layerMask = LayerMask.GetMask("Player (Server)");

            // SphereCast for beam effect with configurable radius and range
            if (!Physics.SphereCast(player.eyes.position, Tackle_Radius, player.eyes.BodyForward(), out hit, Tackle_Range, layerMask)) 
            {
                return;
            }

            BaseEntity hitEntity = hit.GetEntity();
            if (hitEntity == null) return;

            BasePlayer target = hitEntity as BasePlayer;
            if (target != null && !target.IsWounded() && !target.IsSleeping())
            {
                // Update cooldown
                tackleLastFired[player.userID] = Time.time;
                tackleCooldownMessageShown[player.userID] = false; // Reset message flag
                
                HitInfo info = new HitInfo();
                info.Initiator = player;
                info.WeaponPrefab = player.GetHeldEntity();
                info.damageTypes = new DamageTypeList();
                info.damageTypes.Add(DamageType.Generic, 0f); 
                
                target.BecomeWounded(info);
                
                player.ChatMessage($"<color=#ffff00>YELLOW CARD!</color> You tackled {target.displayName}.");
                target.ChatMessage($"<color=#ffff00>YELLOW CARD!</color> You have been tackled for {Tackle_Duration}s!");

                timer.Once(Tackle_Duration, () => {
                    if (target != null && target.IsWounded()) { target.StopWounded(); target.Heal(10f); target.ChatMessage("<color=#00ff00>PLAY ON!</color>"); }
                });
            }
        }

        void ShootPhaseShift(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Phase_Range)) return;
            BaseEntity hitEntity = hit.GetEntity();
            if (hitEntity != null && hitEntity.ShortPrefabName.Contains("ball"))
            {
                // Safety check: ensure ball is still valid
                if (hitEntity.IsDestroyed) return;
                
                Vector3 playerPos = player.transform.position;
                Vector3 ballPos = hitEntity.transform.position;
                
                // Safety check: ensure positions are valid (not NaN or Infinity)
                if (!IsValidPosition(playerPos) || !IsValidPosition(ballPos)) return;
                
                // Calculate swap positions with safety offsets
                Vector3 newPlayerPos = ballPos + new Vector3(0, 0.5f, 0);
                Vector3 newBallPos = playerPos + new Vector3(0, 1.0f, 0);
                
                // Effects at original positions
                Effect.server.Run(FX_Magic, playerPos);
                Effect.server.Run(FX_Magic, ballPos);
                
                // Teleport player to ball location
                player.Teleport(newPlayerPos);
                
                // Move ball to player's original location with safety checks
                Rigidbody ballRb = hitEntity.GetComponent<Rigidbody>();
                if (ballRb != null) 
                { 
                    // Stop ball movement before teleport
                    ballRb.velocity = Vector3.zero;
                    ballRb.angularVelocity = Vector3.zero;
                    
                    // Set new position
                    hitEntity.transform.position = newBallPos;
                    
                    // Wake up physics
                    ballRb.WakeUp();
                }
                else
                {
                    // Fallback if no rigidbody (shouldn't happen but safety first)
                    hitEntity.transform.position = newBallPos;
                }
                
                // Ensure network updates
                hitEntity.SendNetworkUpdate();
                player.SendNetworkUpdateImmediate();
                
                // Wait a frame then send another update (helps with sync)
                timer.Once(0.1f, () => {
                    if (hitEntity != null && !hitEntity.IsDestroyed)
                    {
                        hitEntity.SendNetworkUpdateImmediate();
                    }
                });
                
                player.ChatMessage("<color=#00ffff>PHASE SHIFT!</color>");
            }
        }
        
        // Helper method to check if a position is valid
        bool IsValidPosition(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
                   !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
        }

        void ShootWhistle(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, Whistle_Range)) return;
            BaseEntity hitEntity = hit.GetEntity();
            if (hitEntity != null && hitEntity.ShortPrefabName.Contains("ball"))
            {
                Rigidbody ballRb = hitEntity.GetComponent<Rigidbody>();
                if (ballRb != null)
                {
                    ballRb.isKinematic = true;
                    Effect.server.Run(FX_Explosion, hitEntity.transform.position);
                    player.ChatMessage($"<color=#ff0000>WHISTLE!</color> Ball frozen for {Whistle_FreezeTime}s.");
                    timer.Once(Whistle_FreezeTime, () => {
                        if (ballRb != null) { ballRb.isKinematic = false; ballRb.WakeUp(); Effect.server.Run(FX_Explosion, hitEntity.transform.position); }
                    });
                }
            }
        }

        // ==========================================================================
        // CUSTOM SCRIPTS
        // ==========================================================================

        public class ChargedBall : MonoBehaviour
        {
            public BasePlayer Shooter;
            public float LifeTime;
            private float _stopTime;
            private float _nextEffectTime;
            private const string FX_Smoke = "assets/bundled/prefabs/fx/smoke/fog_1.prefab";
            private const string FX_Explosion = "assets/prefabs/tools/c4/effects/c4_explosion.prefab";
            public void Activate() { _stopTime = Time.time + LifeTime; }
            void FixedUpdate() {
                if (Time.time > _stopTime) { Destroy(this); return; }
                if (Time.time > _nextEffectTime) { Effect.server.Run(FX_Smoke, transform.position); _nextEffectTime = Time.time + 0.1f; }
            }
            void OnCollisionEnter(Collision col) {
                BasePlayer target = col.gameObject.GetComponentInParent<BasePlayer>();
                if (target != null) {
                    if (Shooter != null && target == Shooter) return;
                    Effect.server.Run(FX_Explosion, transform.position);
                    Destroy(this);
                }
            }
        }

        public class MediProjectile : MonoBehaviour
        {
            public BasePlayer Shooter;
            private BaseEntity _entity;
            private bool _hasHit = false;
            private float _spawnTime;
            private Vector3 _lastPosition;
            private const string HealEffect = "assets/bundled/prefabs/fx/build/promote_toptier.prefab"; 
            private const string HealSound = "assets/bundled/prefabs/fx/impacts/bloodreplacement/fleshbloodimpact_blunt_white.prefab";
            private int _playerMask;
            void Awake() { _entity = GetComponent<BaseEntity>(); _spawnTime = Time.time; _lastPosition = transform.position; _playerMask = LayerMask.GetMask("Player (Server)"); Invoke("DestroySelf", 10f); }
            void FixedUpdate() {
                if (_hasHit || _entity == null) return;
                Vector3 currentPos = transform.position;
                Vector3 direction = currentPos - _lastPosition;
                float distance = direction.magnitude;
                if (distance > 0) {
                    RaycastHit hit;
                    if (Physics.SphereCast(_lastPosition, 0.5f, direction.normalized, out hit, distance, _playerMask)) {
                         if (Time.time > _spawnTime + 0.2f) {
                             BaseEntity hitEntity = hit.GetEntity();
                             if (hitEntity == Shooter && Time.time < _spawnTime + 1.0f) {} 
                             else if (hitEntity is BasePlayer) { TriggerHit(hitEntity as BasePlayer); return; }
                         }
                    }
                }
                _lastPosition = currentPos;
            }
            void OnCollisionEnter(Collision collision) { if (Time.time < _spawnTime + 0.2f) return; if (_hasHit || _entity == null) return; TriggerHit(null); }
            void TriggerHit(BasePlayer target) {
                _hasHit = true;
                Effect.server.Run(HealEffect, transform.position, Vector3.up);
                Effect.server.Run(HealSound, transform.position, Vector3.up);
                if (target != null) { 
                    target.Heal(Medi_HealAmount); 
                    target.metabolism.hydration.value += Medi_HydrationAmount; 
                    if(Shooter != null && Shooter != target) 
                        Shooter.ChatMessage($"<color=#00ff00>Healed {target.displayName} (+{Medi_HealAmount}HP)!</color>"); 
                }
                DestroySelf();
            }
            void DestroySelf() { if (_entity != null && !_entity.IsDestroyed) _entity.Kill(); else Destroy(gameObject); }
        }

        public class MagnetProjectile : MonoBehaviour
        {
            public BasePlayer Shooter;
            private BaseEntity _entity;
            private bool _hasHit = false;
            private const string ImpactEffect = "assets/prefabs/tools/c4/effects/c4_explosion.prefab";
            void Awake() { _entity = GetComponent<BaseEntity>(); Invoke("DestroySelf", 5f); }
            void OnCollisionEnter(Collision collision) {
                if (_hasHit || _entity == null) return;
                BaseEntity hitEntity = collision.gameObject.GetComponentInParent<BaseEntity>();
                if (hitEntity != null && Shooter != null && hitEntity == Shooter) return;
                _hasHit = true;
                PerformVacuum(transform.position);
                Effect.server.Run(ImpactEffect, transform.position);
                DestroySelf();
            }
            void PerformVacuum(Vector3 centerPoint) {
                List<BaseEntity> nearbyEntities = new List<BaseEntity>();
                Vis.Entities(centerPoint, Magnet_Radius, nearbyEntities); 
                foreach (var entity in nearbyEntities) {
                    if (!entity.ShortPrefabName.Contains("ball")) continue;
                    if (entity is BasePlayer) continue;
                    Rigidbody rb = entity.GetComponent<Rigidbody>();
                    if (rb == null) continue;
                    if (rb.IsSleeping()) rb.WakeUp();
                    Vector3 directionToCenter = centerPoint - entity.transform.position;
                    Vector3 forceVector = directionToCenter.normalized * Magnet_Force; 
                    forceVector += Vector3.up * 8.0f; 
                    rb.velocity = Vector3.zero; 
                    rb.angularVelocity = Vector3.zero;
                    rb.AddForce(forceVector, ForceMode.VelocityChange);
                }
            }
            void DestroySelf() { if (_entity != null && !_entity.IsDestroyed) _entity.Kill(); else Destroy(gameObject); }
        }

        void SpawnProjectile(Vector3 pos, Vector3 vel, BasePlayer shooter, string itemName, bool isMedi)
        {
            Item item = ItemManager.CreateByName(itemName, 1);
            if (item == null) return;
            BaseEntity droppedEntity = item.Drop(pos, vel);
            if (droppedEntity == null) return;
            DroppedItem dropScript = droppedEntity.GetComponent<DroppedItem>();
            if (dropScript != null) dropScript.allowPickup = false;
            Rigidbody rb = droppedEntity.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (isMedi) { rb.drag = 0.5f; rb.useGravity = true; }
                else { rb.drag = 0.0f; rb.useGravity = false; }
            }
            if (isMedi) { MediProjectile script = droppedEntity.gameObject.AddComponent<MediProjectile>(); script.Shooter = shooter; } 
            else { MagnetProjectile script = droppedEntity.gameObject.AddComponent<MagnetProjectile>(); script.Shooter = shooter; }
        }
    }
}