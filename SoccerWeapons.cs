using System.Collections.Generic;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("SoccerWeapons", "Jess", "11.4.0")]
    [Description("MGL=Heal, Snowball=Magnet, Nailgun=YellowCard (Fixed), Bat=HomeRun, NVG=ESP")]
    public class SoccerWeapons : RustPlugin
    {
        // ==========================================================================
        // CONFIGURATION
        // ==========================================================================
        
        // WEAPONS
        private const string Medi_GunShortname = "multiplegrenadelauncher";
        private const string Medi_ItemToDrop = "largemedkit";
        private const float Medi_SpeedMultiplier = 0.5f;

        private const string Magnet_GunShortname = "snowballgun"; 
        private const string Magnet_ItemToDrop = "snowball"; 
        private const float Magnet_Speed = 60f; 

        private const string Tackle_GunShortname = "nailgun";
        private const float Tackle_Duration = 3.0f; 

        private const string Phase_GunShortname = "pistol.python";
        private const string Whistle_GunShortname = "crossbow";
        private const float Whistle_FreezeTime = 2.0f;

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
                if (target == observer || target.IsDead() || target.IsSleeping()) continue;

                Color c = target.IsNpc ? Color.yellow : color;

                // TALL BOX
                DrawPlayerBox(observer, target, c, duration);

                // SKELETON
                DrawBoneLine(observer, target, "head", "neck", c, duration);
                DrawBoneLine(observer, target, "neck", "spine3", c, duration);
                DrawBoneLine(observer, target, "spine3", "spine1", c, duration);
                DrawBoneLine(observer, target, "spine1", "pelvis", c, duration);
                // Arms
                DrawBoneLine(observer, target, "neck", "l_upperarm", c, duration);
                DrawBoneLine(observer, target, "l_upperarm", "l_forearm", c, duration);
                DrawBoneLine(observer, target, "l_forearm", "l_hand", c, duration);
                DrawBoneLine(observer, target, "neck", "r_upperarm", c, duration);
                DrawBoneLine(observer, target, "r_upperarm", "r_forearm", c, duration);
                DrawBoneLine(observer, target, "r_forearm", "r_hand", c, duration);
                // Legs
                DrawBoneLine(observer, target, "pelvis", "l_hip", c, duration);
                DrawBoneLine(observer, target, "l_hip", "l_knee", c, duration);
                DrawBoneLine(observer, target, "l_knee", "l_foot", c, duration);
                DrawBoneLine(observer, target, "pelvis", "r_hip", c, duration);
                DrawBoneLine(observer, target, "r_hip", "r_knee", c, duration);
                DrawBoneLine(observer, target, "r_knee", "r_foot", c, duration);

                // NAME TAG
                Vector3 headPos = target.eyes.position;
                string dist = $"{(int)Vector3.Distance(observer.transform.position, target.transform.position)}m";
                observer.SendConsoleCommand("ddraw.text", duration, Color.white, headPos + new Vector3(0, 0.4f, 0), $"{target.displayName} [{dist}]");
            }
        }

        void DrawBoneLine(BasePlayer observer, BasePlayer target, string b1, string b2, Color c, float d)
        {
            var bone1 = target.FindBone(b1);
            var bone2 = target.FindBone(b2);
            if (bone1 != null && bone2 != null)
                observer.SendConsoleCommand("ddraw.line", d, c, bone1.position, bone2.position);
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

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            if (projectile == null || player == null) return;
            Item heldItem = player.GetActiveItem();
            if (heldItem == null) return;
            string weaponName = heldItem.info.shortname;

            if (weaponName == Magnet_GunShortname)
            {
                Vector3 spawnPos = player.eyes.position + (player.eyes.BodyForward() * 1.5f);
                Vector3 velocity = player.eyes.BodyForward() * Magnet_Speed;
                SpawnProjectile(spawnPos, velocity, player, Magnet_ItemToDrop, false);
            }
            else if (weaponName == Tackle_GunShortname) ShootYellowCard(player);
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

        // --- FIXED: Yellow Card now uses a Layer Mask to hit ONLY players ---
        void ShootYellowCard(BasePlayer player)
        {
            RaycastHit hit;
            // The mask "Player (Server)" ensures we ignore ground, walls, and invisible barriers
            int layerMask = LayerMask.GetMask("Player (Server)");

            // 0.5m thick beam, 100m range
            if (!Physics.SphereCast(player.eyes.position, 0.5f, player.eyes.BodyForward(), out hit, 100f, layerMask)) 
            {
                return;
            }

            BaseEntity hitEntity = hit.GetEntity();
            if (hitEntity == null) return;

            BasePlayer target = hitEntity as BasePlayer;
            if (target != null && !target.IsWounded() && !target.IsSleeping())
            {
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
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 100f)) return;
            BaseEntity hitEntity = hit.GetEntity();
            if (hitEntity != null && hitEntity.ShortPrefabName.Contains("ball"))
            {
                Vector3 playerPos = player.transform.position;
                Vector3 ballPos = hitEntity.transform.position;
                Effect.server.Run(FX_Magic, playerPos);
                Effect.server.Run(FX_Magic, ballPos);
                player.Teleport(ballPos + new Vector3(0, 0.5f, 0));
                hitEntity.transform.position = playerPos + new Vector3(0, 1.0f, 0);
                Rigidbody ballRb = hitEntity.GetComponent<Rigidbody>();
                if (ballRb != null) { ballRb.velocity = Vector3.zero; ballRb.WakeUp(); }
                hitEntity.SendNetworkUpdateImmediate();
                player.ChatMessage("<color=#00ffff>PHASE SHIFT!</color>");
            }
        }

        void ShootWhistle(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 100f)) return;
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
                if (target != null) { target.Heal(50f); target.metabolism.hydration.value += 20; if(Shooter != null && Shooter != target) Shooter.ChatMessage($"<color=#00ff00>Healed {target.displayName}!</color>"); }
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
                Vis.Entities(centerPoint, 25f, nearbyEntities); 
                foreach (var entity in nearbyEntities) {
                    if (!entity.ShortPrefabName.Contains("ball")) continue;
                    if (entity is BasePlayer) continue;
                    Rigidbody rb = entity.GetComponent<Rigidbody>();
                    if (rb == null) continue;
                    if (rb.IsSleeping()) rb.WakeUp();
                    Vector3 directionToCenter = centerPoint - entity.transform.position;
                    Vector3 forceVector = directionToCenter.normalized * 50f; 
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