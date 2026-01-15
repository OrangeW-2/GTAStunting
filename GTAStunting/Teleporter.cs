using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Handles saving and loading player/vehicle positions with velocity.
    /// </summary>
    public static class Teleporter
    {
        /// <summary>
        /// Holds all teleport data for a single saved point.
        /// </summary>
        public struct TeleportPoint
        {
            public Vector3 Position;
            public Vector3 Rotation; // Euler
            public Quaternion Quaternion;
            public Vector3 RotationVelocity;
            public Vector3 Velocity; // True velocity vector
            public float Speed;
            public float Roll;
            public float Pitch;
            public float SteeringAngle;
            public bool IsSet;

            public TeleportPoint(Entity e)
            {
                if (e == null)
                {
                    Position = Vector3.Zero;
                    Rotation = Vector3.Zero;
                    Quaternion = Quaternion.Identity;
                    RotationVelocity = Vector3.Zero;
                    Velocity = Vector3.Zero;
                    Speed = 0f;
                    Roll = 0f;
                    Pitch = 0f;
                    SteeringAngle = 0f;
                    IsSet = false;
                    return;
                }

                Position = e.Position;
                Rotation = e.Rotation;
                Velocity = e.Velocity;
                Speed = e.Velocity.Length();
                SteeringAngle = 0f;

                if (e is Vehicle v)
                {
                    SteeringAngle = v.SteeringAngle;
                }
                
                // Capture Kinematics
                // GET_ENTITY_QUATERNION
                float x = 0, y = 0, z = 0, w = 0;
                unsafe
                {
                    Function.Call(Hash.GET_ENTITY_QUATERNION, e, &x, &y, &z, &w);
                }
                Quaternion = new Quaternion(x, y, z, w);
                
                // GET_ENTITY_ROTATION_VELOCITY
                RotationVelocity = Function.Call<Vector3>(Hash.GET_ENTITY_ROTATION_VELOCITY, e);
                
                // GET_ENTITY_ROLL / PITCH
                Roll = Function.Call<float>((Hash)0x831E0242595560DF, e);
                Pitch = Function.Call<float>((Hash)0xD45DC2893621E1FE, e);

                IsSet = true;
            }
        }

        // -- State --
        private static TeleportPoint mainPoint;

        private static Vehicle secondVehicle;
        public static TeleportPoint secondPoint = new TeleportPoint(null);
        private static bool hasSecondVehicle = false;

        // -- Fix State --
        public static int speedTeleportFixFrames = 0;
        public static Vehicle speedTeleportVehicle = null;
        // Multi-frame fixes
        public static int angleFixFrames = 0;
        public static Vehicle lastTeleportedVehicle = null;
        public static Vector3 savedRotationForFix;
        public static float savedSteeringAngleForFix;
        public static Vector3 savedPositionForFix;
        public static Vector3 savedVelocityForFix;
        public static bool fixIsStatic = true;

        // -- Stubs for GTAS compatibility --
        public static int idx = 0;

        // ========================================================================
        // 1. Default Save/Load
        // ========================================================================
        // (Save matches previous edit, skipped)

        public static void Save(Entity entity)
        {
            // Ensure Mission Entity to prevent aggressive culling/physics takeover
            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, entity, true, true);
            
            mainPoint = new TeleportPoint(entity);
            GTAS.Notify("Position Saved!");
        }

        public static void Load(Ped ped)
        {
            if (!mainPoint.IsSet)
            {
                GTAS.Notify("Position has not been set!");
                return;
            }
            // Load with 0 speed/velocity
            TeleportPoint pt = mainPoint;
            pt.Speed = 0;
            pt.Velocity = Vector3.Zero;
            pt.RotationVelocity = Vector3.Zero;

            LoadInternal(ped, pt);
            LoadSecondVehicle();
            GTAS.Notify("Pos Loaded");
        }

        // ========================================================================
        // 2. With-Speed Save/Load
        // ========================================================================

        public static void LoadWithSpeed(Ped ped)
        {
            if (!mainPoint.IsSet)
            {
                GTAS.Notify("Position has not been set!");
                return;
            }
            // Load with saved speed/velocity (using the saved point directly)
            LoadInternal(ped, mainPoint);
            LoadSecondVehicle();
            GTAS.Notify($"Loaded ({(mainPoint.Speed * 3.6f):F0} KPH)");
        }

        // ========================================================================
        // 3. Second Vehicle Save/Load
        // ========================================================================

        public static void SaveSecondVehicle()
        {
            Ped player = Game.Player.Character;
            Vehicle current = player.CurrentVehicle;
            Vehicle[] nearby = World.GetNearbyVehicles(player.Position, 20f);

            Vehicle best = null;
            float closestDist = float.MaxValue;

            foreach (Vehicle v in nearby)
            {
                if (v == null || !v.Exists() || v == current) continue;

                float d = Vector3.Distance(player.Position, v.Position);
                if (d < closestDist)
                {
                    closestDist = d;
                    best = v;
                }
            }

            if (best != null)
            {
                secondVehicle = best;
                hasSecondVehicle = true;
                Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, best, true, true);
                GTAS.Notify($"Second Vehicle Saved: {best.LocalizedName}");
            }
            else
            {
                GTAS.Notify("No nearby vehicle found!");
            }
        }

        public static void SaveSecondPosition()
        {
            if (!hasSecondVehicle || secondVehicle == null || !secondVehicle.Exists())
            {
                GTAS.Notify("No second vehicle set!");
                return;
            }
            secondPoint = new TeleportPoint(secondVehicle);
            GTAS.Notify("Second Vehicle Position Saved!");
        }

        // Returns status string
        private static void LoadSecondVehicle()
        {
            if (!secondPoint.IsSet) return;

            if (secondVehicle == null || !secondVehicle.Exists())
            {
                GTAS.Notify("2nd Vehicle Invalid");
                return;
            }

            // Use temp point with 0 speed for second vehicle
            TeleportPoint pt = secondPoint;
            pt.Velocity = Vector3.Zero;
            pt.RotationVelocity = Vector3.Zero;
            pt.Speed = 0;

            TeleportVehicle(secondVehicle, pt);

            if (secondVehicle.IsDead)
            {
                secondVehicle.Repair();
                secondVehicle.Wash();
            }
        }

        public static Vehicle GetVehicle(Ped ped)
        {
            if (ped.CurrentVehicle != null) return ped.CurrentVehicle;
            if (ped.LastVehicle != null) return ped.LastVehicle;
            return null;
        }

        // ========================================================================
        // Core Logic
        // ========================================================================

        private static string LoadInternal(Ped ped, TeleportPoint pt)
        {
            Vehicle v = null;
            string result = "Pos Loaded";

            if (ped.IsInVehicle())
            {
                v = ped.CurrentVehicle;
            }
            else if (ped.LastVehicle != null && ped.LastVehicle.Exists())
            {
                v = ped.LastVehicle;
                ped.SetIntoVehicle(v, VehicleSeat.Driver);
            }

            // Start tracking speed run
            SpeedTracker.StartRecording();

            // Start ghost vehicle playback
            GhostRecorder.StartPlayback();

            if (v != null)
            {
                if (v.IsDead) v.Repair();
                v.Repair();
                v.Wash();
                TeleportVehicle(v, pt);
            }
            else
            {
                ped.Position = pt.Position;
                ped.Rotation = pt.Rotation;
            }

            return result;
        }

        private static void TeleportVehicle(Vehicle v, TeleportPoint pt)
        {
            bool isStatic = (pt.Speed == 0);
            
            // -- PHASE 1: Stop all motion instantly --
            Function.Call(Hash.SET_ENTITY_VELOCITY, v, 0f, 0f, 0f);
            Function.Call((Hash)0x8339643499D1222E, v, 0f, 0f, 0f); // SET_ENTITY_ANGULAR_VELOCITY
            
            // Disable collision temporarily for clean placement
            v.IsCollisionEnabled = false;
            
            // For static teleports, freeze to prevent physics interference
            if (isStatic)
            {
                v.IsPositionFrozen = true;
            }
            
            // -- PHASE 2: Set Position --
            v.Position = pt.Position;
            
            // -- PHASE 3: Set Rotation (Euler, reliable) --
            Function.Call(Hash.SET_ENTITY_ROTATION, v, pt.Rotation.X, pt.Rotation.Y, pt.Rotation.Z, 2, true);
            v.Rotation = pt.Rotation;
            
            // -- PHASE 4: Set Velocity --
            if (!isStatic)
            {
                // Wake physics
                Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, v, pt.Speed);
                v.Velocity = pt.Velocity;
                // SET_ENTITY_ANGULAR_VELOCITY
                Function.Call((Hash)0x8339643499D1222E, v, pt.RotationVelocity.X, pt.RotationVelocity.Y, pt.RotationVelocity.Z);
            }
            else
            {
                v.Velocity = Vector3.Zero;
                Function.Call(Hash.SET_ENTITY_VELOCITY, v, 0f, 0f, 0f);
            }
            
            // -- PHASE 5: Re-enable collision --
            v.IsCollisionEnabled = true;
            
            // Multi-frame enforcement setup
            lastTeleportedVehicle = v;
            
            // Store target state for Hard Sync
            savedRotationForFix = pt.Rotation;
            savedSteeringAngleForFix = pt.SteeringAngle;
            savedPositionForFix = pt.Position;
            savedVelocityForFix = isStatic ? Vector3.Zero : pt.Velocity;
            
            fixIsStatic = isStatic;
            
            // Use 10 frames for ALL teleports to allow "Hard Sync" to override physics stabilizer
            angleFixFrames = 10;
        }

        public static void OnTick()
        {
            // Speed Teleport Fix (Acceleration Lockout)
            if (speedTeleportFixFrames > 0)
            {
                speedTeleportFixFrames--;
                Function.Call(Hash.DISABLE_CONTROL_ACTION, 0, (int)GTA.Control.VehicleAccelerate, true);
                Function.Call(Hash.SET_CONTROL_VALUE_NEXT_FRAME, 0, (int)GTA.Control.VehicleBrake, 1.0f);
            }

            // Hard Sync Physics Fix
            if (angleFixFrames > 0)
            {
                angleFixFrames--;
                Vehicle vFix = lastTeleportedVehicle;
                
                if (vFix != null && vFix.Exists())
                {
                    // "Hard Sync" Enforcement - Override physics engine completely
                    vFix.Position = savedPositionForFix;
                    vFix.Rotation = savedRotationForFix;
                    vFix.SteeringAngle = savedSteeringAngleForFix;

                    if (fixIsStatic)
                    {
                        // Static: Keep frozen and kill velocity
                        vFix.IsPositionFrozen = true;
                        vFix.Velocity = Vector3.Zero;
                        vFix.RotationVelocity = Vector3.Zero;
                        Function.Call(Hash.SET_ENTITY_VELOCITY, vFix, 0f, 0f, 0f);
                    }
                    else
                    {
                        // Moving: Force exact velocity vector
                        vFix.Velocity = savedVelocityForFix;
                        vFix.RotationVelocity = Vector3.Zero; // Kill induced spin
                    }
                }

                // Unfreeze on last frame
                if (angleFixFrames == 0)
                {
                    if (fixIsStatic && vFix != null && vFix.Exists())
                    {
                        vFix.IsPositionFrozen = false;
                    }
                    lastTeleportedVehicle = null;
                }
            }
        }
    }
}
