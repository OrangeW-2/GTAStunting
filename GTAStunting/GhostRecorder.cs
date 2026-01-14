using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Records and plays back vehicle state for "ghost driving" the second vehicle.
    /// Uses Hard Sync (Position/Rotation/Velocity replay) for maximum stunting precision.
    /// Supports persistent save/load to file.
    /// </summary>
    public static class GhostRecorder
    {
        #region Data Structures

        private struct GhostFrame
        {
            public float Time;
            public Vector3 Position;
            public Vector3 Rotation;
            public Vector3 Velocity;
            public float SteeringAngle;
            public float Speed;

            public GhostFrame(Vehicle v, float time)
            {
                Time = time;
                Position = v.Position;
                Rotation = v.Rotation;
                Velocity = v.Velocity;
                SteeringAngle = v.SteeringAngle;
                Speed = v.Speed;
            }
        }

        #endregion

        #region State

        private static List<GhostFrame> frames = new List<GhostFrame>();
        private static Vehicle targetVehicle;
        private static int targetVehicleHash; // Store model hash for respawn
        private static bool isRecording = false;
        private static bool isPlaying = false;
        private static float recordStartTime;
        private static float playbackStartTime;

        private static string SaveFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GhostRecordings");
        private const string DEFAULT_FILENAME = "ghost_recording.csv";

        #endregion

        #region Properties

        public static bool IsRecording => isRecording;
        public static bool IsPlaying => isPlaying;
        public static bool HasData => frames.Count > 0;

        #endregion

        #region Recording

        public static void ToggleRecording()
        {
            if (isRecording)
                StopRecording();
            else
                StartRecording();
        }

        public static void StartRecording()
        {
            Ped player = Game.Player.Character;
            if (!player.IsInVehicle())
            {
                GTAS.Notify("Ghost Record: Must be in a vehicle!");
                return;
            }

            targetVehicle = player.CurrentVehicle;
            targetVehicleHash = targetVehicle.Model.Hash;
            frames.Clear();
            recordStartTime = (float)Game.GameTime / 1000f;
            isRecording = true;
            isPlaying = false;

            Function.Call(Hash.SET_ENTITY_AS_MISSION_ENTITY, targetVehicle, true, true);
            GTAS.Notify("Ghost Record: Started");
        }

        public static void StopRecording()
        {
            if (!isRecording) return;

            isRecording = false;
            
            // Auto-save to file
            SaveToFile(DEFAULT_FILENAME);
            
            GTAS.Notify($"Ghost Record: Saved ({frames.Count} frames, {frames.Count * 0.033f:F1}s)");
        }

        #endregion

        #region Playback

        public static void StartPlayback()
        {
            // Try to load from file if no data in memory
            if (frames.Count == 0)
            {
                LoadFromFile(DEFAULT_FILENAME);
            }

            if (frames.Count == 0)
            {
                return; // No data
            }

            // If vehicle is missing, try to find/spawn it
            if (targetVehicle == null || !targetVehicle.Exists())
            {
                // Try to find a nearby vehicle of the same model
                if (targetVehicleHash != 0)
                {
                    Vehicle[] nearby = World.GetNearbyVehicles(Game.Player.Character.Position, 50f);
                    foreach (Vehicle v in nearby)
                    {
                        if (v != null && v.Exists() && v.Model.Hash == targetVehicleHash && v != Game.Player.Character.CurrentVehicle)
                        {
                            targetVehicle = v;
                            break;
                        }
                    }
                }

                if (targetVehicle == null || !targetVehicle.Exists())
                {
                    GTAS.Notify("Ghost Playback: Vehicle missing!");
                    return;
                }
            }

            // Teleport to start position
            GhostFrame first = frames[0];
            targetVehicle.Position = first.Position;
            targetVehicle.Rotation = first.Rotation;
            targetVehicle.Velocity = first.Velocity;
            targetVehicle.SteeringAngle = first.SteeringAngle;

            if (targetVehicle.IsDead) targetVehicle.Repair();

            playbackStartTime = (float)Game.GameTime / 1000f;
            isPlaying = true;
            isRecording = false;
        }

        public static void StopPlayback()
        {
            isPlaying = false;
        }

        #endregion

        #region Update (Called every frame)

        public static void Update()
        {
            if (isRecording)
                RecordFrame();
            else if (isPlaying)
                PlaybackFrame();
        }

        private static void RecordFrame()
        {
            if (targetVehicle == null || !targetVehicle.Exists())
            {
                StopRecording();
                GTAS.Notify("Ghost Record: Vehicle lost!");
                return;
            }

            float time = (float)Game.GameTime / 1000f - recordStartTime;
            frames.Add(new GhostFrame(targetVehicle, time));
        }

        private static void PlaybackFrame()
        {
            if (targetVehicle == null || !targetVehicle.Exists())
            {
                StopPlayback();
                return;
            }

            float currentTime = (float)Game.GameTime / 1000f - playbackStartTime;

            int idx = 0;
            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (frames[i + 1].Time > currentTime)
                {
                    idx = i;
                    break;
                }
                idx = i;
            }

            if (currentTime >= frames[frames.Count - 1].Time)
            {
                GhostFrame last = frames[frames.Count - 1];
                targetVehicle.Position = last.Position;
                targetVehicle.Rotation = last.Rotation;
                targetVehicle.Velocity = last.Velocity;
                StopPlayback();
                return;
            }

            GhostFrame a = frames[idx];
            GhostFrame b = frames[Math.Min(idx + 1, frames.Count - 1)];

            float t = 0f;
            float dt = b.Time - a.Time;
            if (dt > 0.001f)
                t = (currentTime - a.Time) / dt;

            // Velocity-based correction for physics-friendly playback
            Vector3 targetPos = Vector3.Lerp(a.Position, b.Position, t);
            Vector3 targetVel = Vector3.Lerp(a.Velocity, b.Velocity, t);
            Vector3 currentPos = targetVehicle.Position;

            Vector3 error = targetPos - currentPos;
            targetVehicle.Velocity = targetVel + (error * 10.0f);
            targetVehicle.Rotation = Vector3.Lerp(a.Rotation, b.Rotation, t);
            targetVehicle.SteeringAngle = a.SteeringAngle + (b.SteeringAngle - a.SteeringAngle) * t;
        }

        #endregion

        #region File Save/Load

        /// <summary>
        /// Saves the current recording to a CSV file.
        /// </summary>
        public static void SaveToFile(string filename)
        {
            if (frames.Count == 0)
            {
                GTAS.Notify("Ghost: Nothing to save!");
                return;
            }

            try
            {
                if (!Directory.Exists(SaveFolder))
                    Directory.CreateDirectory(SaveFolder);

                string path = Path.Combine(SaveFolder, filename);

                using (StreamWriter writer = new StreamWriter(path))
                {
                    // Header with vehicle info
                    writer.WriteLine($"VehicleHash,{targetVehicleHash}");
                    writer.WriteLine("Time,PosX,PosY,PosZ,RotX,RotY,RotZ,VelX,VelY,VelZ,Steer,Speed");

                    foreach (GhostFrame f in frames)
                    {
                        writer.WriteLine($"{f.Time:F4},{f.Position.X:F3},{f.Position.Y:F3},{f.Position.Z:F3},{f.Rotation.X:F3},{f.Rotation.Y:F3},{f.Rotation.Z:F3},{f.Velocity.X:F3},{f.Velocity.Y:F3},{f.Velocity.Z:F3},{f.SteeringAngle:F3},{f.Speed:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                GTAS.Notify("Ghost Save Failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads a recording from a CSV file.
        /// </summary>
        public static void LoadFromFile(string filename)
        {
            string path = Path.Combine(SaveFolder, filename);

            if (!File.Exists(path))
                return; // Silent fail - no file exists yet

            try
            {
                frames.Clear();
                string[] lines = File.ReadAllLines(path);

                if (lines.Length < 3) return;

                // Parse vehicle hash from first line
                string[] hashParts = lines[0].Split(',');
                if (hashParts.Length >= 2)
                    int.TryParse(hashParts[1], out targetVehicleHash);

                // Parse frames (skip header lines)
                for (int i = 2; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(',');
                    if (parts.Length < 12) continue;

                    GhostFrame f = new GhostFrame
                    {
                        Time = float.Parse(parts[0]),
                        Position = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])),
                        Rotation = new Vector3(float.Parse(parts[4]), float.Parse(parts[5]), float.Parse(parts[6])),
                        Velocity = new Vector3(float.Parse(parts[7]), float.Parse(parts[8]), float.Parse(parts[9])),
                        SteeringAngle = float.Parse(parts[10]),
                        Speed = float.Parse(parts[11])
                    };
                    frames.Add(f);
                }

                if (frames.Count > 0)
                    GTAS.Notify($"Ghost: Loaded {frames.Count} frames");
            }
            catch (Exception ex)
            {
                GTAS.Notify("Ghost Load Failed: " + ex.Message);
            }
        }

        #endregion

        #region Utility

        public static void Clear()
        {
            frames.Clear();
            isRecording = false;
            isPlaying = false;
            targetVehicle = null;
            targetVehicleHash = 0;
        }

        #endregion
    }
}
