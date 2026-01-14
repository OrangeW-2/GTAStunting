using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using GTA.Math;

namespace GTAStunting
{
    /// <summary>
    /// Tracks player speed over time for graph visualization and ghost recording.
    /// Manages recording sessions, attempt history, and CSV import/export.
    /// </summary>
    public static class SpeedTracker
    {
        #region Constants

        private static float RECORD_INTERVAL = 0.033f; // ~30 samples per second

        #endregion

        #region State

        public static bool IsRecording { get; private set; }
        public static List<Attempt> SavedAttempts { get; private set; }
        public static List<Attempt> SessionAttempts { get; private set; } = new List<Attempt>();
        
        private static Attempt currentAttempt;
        private static float startTime;
        private static float lastRecordTime;

        #endregion

        #region Properties

        /// <summary>
        /// The currently recording attempt (null if not recording).
        /// </summary>
        public static Attempt CurrentAttempt => currentAttempt;

        /// <summary>
        /// Time elapsed in the current recording attempt.
        /// </summary>
        public static float CurrentAttemptTime
        {
            get
            {
                if (currentAttempt != null && currentAttempt.Records.Count > 0)
                    return currentAttempt.Records[currentAttempt.Records.Count - 1].Time;
                return 0f;
            }
        }

        #endregion

        #region Static Constructor

        static SpeedTracker()
        {
            SavedAttempts = new List<Attempt>();
            SessionAttempts = new List<Attempt>();
        }

        #endregion

        #region Recording

        /// <summary>
        /// Starts a new recording session. If already recording, finalizes the current attempt first.
        /// </summary>
        public static void StartRecording()
        {
            startTime = (float)Game.GameTime / 1000f;
            lastRecordTime = -RECORD_INTERVAL;

            if (IsRecording && currentAttempt != null)
            {
                try { FinalizeCurrentAttempt(); } catch { }
            }

            currentAttempt = new Attempt();
            IsRecording = true;
        }

        /// <summary>
        /// Stops the current recording session.
        /// </summary>
        public static void StopRecording()
        {
            if (!IsRecording || currentAttempt == null) return;

            try { FinalizeCurrentAttempt(); } catch { }
            currentAttempt = null;
            IsRecording = false;
        }

        /// <summary>
        /// Records current position, speed, and input state. Called every frame from GTAS.OnTick.
        /// </summary>
        public static void RecordTick()
        {
            if (!IsRecording || currentAttempt == null) return;

            float currentTime = (float)Game.GameTime / 1000f - startTime;
            if (currentTime - lastRecordTime < RECORD_INTERVAL) return;
            lastRecordTime = currentTime;

            Player player = Game.Player;
            if (player == null || !player.Character.Exists()) return;

            Vehicle vehicle = player.Character.CurrentVehicle;
            Vector3 pos;
            float speed;
            float height;

            if (vehicle != null && vehicle.Exists())
            {
                speed = vehicle.Speed;
                height = vehicle.Position.Z;
                pos = vehicle.Position;
            }
            else
            {
                speed = player.Character.Velocity.Length();
                height = player.Character.Position.Z;
                pos = player.Character.Position;
            }

            // Detect lean input (0 = neutral, 1 = forward, 2 = back)
            byte leanState = 0;
            if (vehicle != null && vehicle.Exists())
            {
                float leanInput = Game.GetControlValueNormalized(GTA.Control.VehicleMoveUpDown);
                if (leanInput > 0.3f) leanState = 1; // Forward
                else if (leanInput < -0.3f) leanState = 2; // Back
            }

            currentAttempt.Records.Add(new SpeedRecord(currentTime, speed, height, pos, leanState));
            if (speed > currentAttempt.MaxSpeed)
                currentAttempt.MaxSpeed = speed;
        }

        private static void FinalizeCurrentAttempt()
        {
            if (currentAttempt == null || currentAttempt.Records.Count == 0) return;

            currentAttempt.Finish();
            if (currentAttempt.Records.Count > 30)
                SessionAttempts.Add(currentAttempt);
        }

        #endregion

        #region Ghost Management

        /// <summary>
        /// Clears all saved ghosts and session attempts.
        /// </summary>
        public static void ClearAttempts()
        {
            SessionAttempts.Clear();
            SavedAttempts.Clear();
            GTAS.Notify("All attempts and ghosts cleared.");
        }

        /// <summary>
        /// Saves the most recent session attempt as a ghost (max 3 saved).
        /// </summary>
        public static void SaveLastAttempt()
        {
            if (SessionAttempts.Count == 0)
            {
                GTAS.Notify("No recent attempts to save.");
                return;
            }

            Attempt last = SessionAttempts[SessionAttempts.Count - 1];
            if (SavedAttempts.Count >= 3)
                SavedAttempts.RemoveAt(0);

            SavedAttempts.Add(last);
            GTAS.Notify($"Ghost #{SavedAttempts.Count} Saved (Max: {last.MaxSpeed * 3.6f:F0} km/h)");
        }

        /// <summary>
        /// Placeholder for stats display.
        /// </summary>
        public static void ShowStats() { }

        #endregion

        #region Import/Export

        /// <summary>
        /// Exports saved ghosts to a CSV file.
        /// </summary>
        public static void ExportToCSV()
        {
            if (SavedAttempts.Count == 0)
            {
                GTAS.Notify("No Saved Ghosts (F8) to export!");
                return;
            }

            string filename = $"stunt_ghosts_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

            try
            {
                int maxRecords = 0;
                foreach (Attempt attempt in SavedAttempts)
                {
                    if (attempt.Records.Count > maxRecords)
                        maxRecords = attempt.Records.Count;
                }

                using (StreamWriter writer = new StreamWriter(path))
                {
                    // Header
                    string header = "Time";
                    for (int i = 0; i < SavedAttempts.Count; i++)
                        header += $",Spd_{i + 1},X_{i + 1},Y_{i + 1},Z_{i + 1}";
                    writer.WriteLine(header);

                    // Data rows
                    for (int row = 0; row < maxRecords; row++)
                    {
                        float time = row * RECORD_INTERVAL;
                        string line = $"{time:F3}";

                        for (int col = 0; col < SavedAttempts.Count; col++)
                        {
                            if (row < SavedAttempts[col].Records.Count)
                            {
                                SpeedRecord rec = SavedAttempts[col].Records[row];
                                line += $",{rec.Speed:F2},{rec.Position.X:F2},{rec.Position.Y:F2},{rec.Position.Z:F2}";
                            }
                            else
                            {
                                line += ",,,,";
                            }
                        }
                        writer.WriteLine(line);
                    }
                }

                GTAS.Notify($"Exported {SavedAttempts.Count} ghosts to {filename}");
            }
            catch (Exception ex)
            {
                GTAS.Notify("Export failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Placeholder for summary export.
        /// </summary>
        public static void ExportSummary() { }

        /// <summary>
        /// Imports ghosts from a CSV file.
        /// </summary>
        public static void ImportFromCSV(string filename)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (!File.Exists(path))
            {
                path += ".csv";
                if (!File.Exists(path))
                {
                    GTAS.Notify("File not found: " + filename);
                    return;
                }
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 2) return;

                int columnCount = lines[0].Split(',').Length;
                int ghostCount = (columnCount - 1) / 4;
                if (ghostCount <= 0)
                {
                    GTAS.Notify("Invalid CSV format");
                    return;
                }
                if (ghostCount > 3) ghostCount = 3;

                List<Attempt> imported = new List<Attempt>();
                for (int i = 0; i < ghostCount; i++)
                    imported.Add(new Attempt());

                for (int row = 1; row < lines.Length; row++)
                {
                    string[] values = lines[row].Split(',');
                    if (values.Length < columnCount || !float.TryParse(values[0], out float time))
                        continue;

                    for (int g = 0; g < ghostCount; g++)
                    {
                        int baseIdx = 1 + g * 4;
                        string speedStr = values[baseIdx];
                        if (string.IsNullOrWhiteSpace(speedStr)) continue;

                        float speed = float.Parse(speedStr);
                        float x = float.Parse(values[baseIdx + 1]);
                        float y = float.Parse(values[baseIdx + 2]);
                        float z = float.Parse(values[baseIdx + 3]);
                        Vector3 pos = new Vector3(x, y, z);

                        imported[g].Records.Add(new SpeedRecord(time, speed, z, pos));
                        if (speed > imported[g].MaxSpeed)
                            imported[g].MaxSpeed = speed;
                    }
                }

                SavedAttempts.Clear();
                foreach (Attempt attempt in imported)
                {
                    if (attempt.Records.Count > 0)
                    {
                        attempt.Finish();
                        SavedAttempts.Add(attempt);
                    }
                }

                GTAS.Notify($"Imported {SavedAttempts.Count} ghosts.");
            }
            catch (Exception ex)
            {
                GTAS.Notify("Import failed: " + ex.Message);
            }
        }

        #endregion
    }
}
