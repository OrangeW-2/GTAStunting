using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Handles saving and loading vehicle/player positions with optional velocity preservation.
    /// Supports a primary teleport point and a secondary vehicle for stunting setups.
    /// </summary>
    public class Teleporter
    {
        // -- Data Structures --
        private struct TeleportPoint
        {
            public Vector3 Position;
            public Vector3 Rotation; // Euler
            public Vector3 Velocity; // True velocity vector
            public float Speed;
            public bool IsSet;

            public TeleportPoint(Entity e)
            {
                Position = e.Position;
                Rotation = e.Rotation;
                Velocity = e.Velocity;
                Speed = e.Velocity.Length();
                IsSet = true;
            }
        }

        // -- State --
        private static TeleportPoint mainPoint;

        private static Vehicle secondVehicle;
        private static TeleportPoint secondPoint;
        private static bool hasSecondVehicle = false;

        // -- Fix State --
        public static int speedTeleportFixFrames = 0;
        public static Vehicle speedTeleportVehicle = null;

        // -- Stubs for GTAS compatibility --
        public static int idx = 0;

        // ========================================================================
        // 1. Default Save/Load
        // ========================================================================

        public static void Save(Entity entity)
        {
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
            // 1. Capture Engine State
            float rpm = v.CurrentRPM;
            int gear = v.CurrentGear;
            float clutch = v.Clutch;
            float turbo = v.Turbo;
            float steering = v.SteeringAngle;
            Vector3 rotVel = (pt.Speed == 0) ? Vector3.Zero : Vector3.Zero;

            // 2. Set Position
            v.Position = pt.Position;

            // 3. Set Rotation
            v.Rotation = pt.Rotation;
            #pragma warning disable CS0618 // Type or member is obsolete
            v.RotationVelocity = rotVel; // Note: Teleporter logic might need review for Local vs World, but keeping assignment consistent with type
            #pragma warning restore CS0618 // Type or member is obsolete

            // 4. Set Speed / Velocity
            // Strategy: Use SET_VEHICLE_FORWARD_SPEED to wake up engine/physics preventing lockout.
            // Then immediately overwrite with specific Velocity vector to ensure correct direction (horizontal).
            if (pt.Speed > 0)
            {
                Function.Call(Hash.SET_VEHICLE_FORWARD_SPEED, v, pt.Speed); // Wake up
                v.Velocity = pt.Velocity; // Correct vector
            }
            else
            {
                v.Velocity = Vector3.Zero;
            }

            // 5. Restore Engine (only if moving)
            if (pt.Speed > 0)
            {
                v.CurrentRPM = rpm;
                v.CurrentGear = gear;
                v.Clutch = clutch;
                v.Turbo = turbo;
            }
            v.SteeringAngle = steering;

            // 6. Fix acceleration
            speedTeleportFixFrames = 10;
        }
    }
}
