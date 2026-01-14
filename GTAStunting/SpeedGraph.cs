using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Display modes for ghost/graph visualization.
    /// </summary>
    public enum DisplayMode
    {
        Off = 0,
        GhostLinesOnly = 1,
        GhostAndGraph = 2,
        GraphOnly = 3
    }

    /// <summary>
    /// Renders speed graphs (2D UI) and 3D ghost trails for stunt runups.
    /// </summary>
    public static class SpeedGraph
    {
        #region Constants

        private const int MAX_UI_PRIMITIVES = 600;
        private const int MAX_WORLD_PRIMITIVES = 250;

        private static float GRAPH_X = 0.045f;
        private static float GRAPH_Y = 0.55f;
        private static float GRAPH_WIDTH = 0.18f;
        private static float GRAPH_HEIGHT = 0.125f;
        private static float TIME_WINDOW = 10f;

        #endregion

        #region State

        public static DisplayMode CurrentMode = DisplayMode.Off;
        public static bool IsVisible = false;

        private static float baseHeight = 0f;
        private static float currentSpeed = 0f;
        private static float currentHeight = 0f;
        private static int uiPrimitives = 0;
        private static int worldPrimitives = 0;

        #endregion

        #region Static Constructor

        static SpeedGraph()
        {
            IsVisible = false;
            baseHeight = 0f;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Cycles through display modes: Off -> GhostLinesOnly -> GhostAndGraph -> GraphOnly -> Off.
        /// </summary>
        public static void Toggle()
        {
            switch (CurrentMode)
            {
                case DisplayMode.Off:
                    CurrentMode = DisplayMode.GhostLinesOnly;
                    Reset();
                    GTAS.Notify("Ghost Lines ON");
                    break;
                case DisplayMode.GhostLinesOnly:
                    CurrentMode = DisplayMode.GhostAndGraph;
                    GTAS.Notify("Ghost Lines + Graph ON");
                    break;
                case DisplayMode.GhostAndGraph:
                    CurrentMode = DisplayMode.GraphOnly;
                    GTAS.Notify("Graph ON");
                    break;
                case DisplayMode.GraphOnly:
                    CurrentMode = DisplayMode.Off;
                    GTAS.Notify("Graph OFF");
                    break;
            }
            IsVisible = CurrentMode != DisplayMode.Off;
        }

        /// <summary>
        /// Updates current speed and height values from the player's vehicle/ped.
        /// </summary>
        public static void Update()
        {
            if (!IsVisible) return;

            currentSpeed = 0f;
            currentHeight = 0f;

            Player player = Game.Player;
            if (player != null && player.Character.Exists())
            {
                Vehicle vehicle = player.Character.CurrentVehicle;
                if (vehicle != null && vehicle.Exists())
                {
                    currentSpeed = vehicle.Speed * 3.6f;
                    currentHeight = vehicle.Position.Z - baseHeight;
                }
                else
                {
                    currentSpeed = player.Character.Velocity.Length() * 3.6f;
                    currentHeight = player.Character.Position.Z - baseHeight;
                }
            }
        }

        /// <summary>
        /// Main draw method - renders UI graph and/or 3D ghost trails based on CurrentMode.
        /// </summary>
        public static void Draw()
        {
            if (CurrentMode == DisplayMode.Off) return;

            uiPrimitives = 0;
            worldPrimitives = 0;

            try
            {
                List<Attempt> savedAttempts = SpeedTracker.SavedAttempts;
                Attempt currentAttempt = SpeedTracker.CurrentAttempt;

                // Draw graph UI if mode includes graph
                if (CurrentMode == DisplayMode.GhostAndGraph || CurrentMode == DisplayMode.GraphOnly)
                {
                    // Draw Text FIRST to ensure visibility (priority over lines)
                    DrawText($"{currentSpeed:F0} km/h", GRAPH_X, GRAPH_Y - 0.045f, 0.55f, 255, 255, 0);
                    DrawText($"Ghosts: {savedAttempts.Count}/3", GRAPH_X + GRAPH_WIDTH / 2f, GRAPH_Y - 0.025f, 0.25f, 200, 200, 200);

                    // Draw graph background
                    DrawRectUI(GRAPH_X + GRAPH_WIDTH / 2f, GRAPH_Y + GRAPH_HEIGHT / 2f, GRAPH_WIDTH, GRAPH_HEIGHT, 0, 0, 0, 150);

                    // Draw borders
                    float borderThickness = 0.001f;
                    DrawRectUI(GRAPH_X + GRAPH_WIDTH / 2f, GRAPH_Y, GRAPH_WIDTH, borderThickness, 255, 255, 255, 100);
                    DrawRectUI(GRAPH_X + GRAPH_WIDTH / 2f, GRAPH_Y + GRAPH_HEIGHT, GRAPH_WIDTH, borderThickness, 255, 255, 255, 100);
                    DrawRectUI(GRAPH_X, GRAPH_Y + GRAPH_HEIGHT / 2f, borderThickness, GRAPH_HEIGHT, 255, 255, 255, 100);
                    DrawRectUI(GRAPH_X + GRAPH_WIDTH, GRAPH_Y + GRAPH_HEIGHT / 2f, borderThickness, GRAPH_HEIGHT, 255, 255, 255, 100);

                    // Calculate time window
                    float currentTime = SpeedTracker.CurrentAttemptTime;
                    float endT = Math.Max(TIME_WINDOW, currentTime);
                    float startT = endT - TIME_WINDOW;

                    // Calculate max speed for scaling
                    float yMax = GetMaxVisibleSpeed(currentAttempt, savedAttempts, startT, endT) * 1.1f;
                    if (yMax < 50f) yMax = 50f;

                    // Draw current attempt
                    if (currentAttempt != null && currentAttempt.Records.Count > 0)
                        DrawAttemptLine2D(currentAttempt.Records, yMax, startT, endT, 255, 255, 0, 255, 0.003f, 2);

                    // Draw saved attempts
                    DrawSavedAttempts2D(savedAttempts, yMax, startT, endT);
                    DrawLegend(savedAttempts);
                }

                // Draw 3D ghost trails if mode includes ghost lines
                if (CurrentMode == DisplayMode.GhostLinesOnly || CurrentMode == DisplayMode.GhostAndGraph)
                {
                    Draw3DTrails(savedAttempts, 10);
                }
            }
            catch { }
        }

        /// <summary>
        /// Resets graph state and sets base height to current position.
        /// </summary>
        public static void Reset()
        {
            currentSpeed = 0f;
            currentHeight = 0f;
            Ped character = Game.Player.Character;
            if (character != null && character.Exists())
                baseHeight = character.Position.Z;
        }

        #endregion

        #region Private Drawing Methods

        private static void DrawText(string text, float x, float y, float scale, int r, int g, int b)
        {
            if (uiPrimitives > MAX_UI_PRIMITIVES) return;

            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, scale, scale);
            Function.Call(Hash.SET_TEXT_COLOUR, r, g, b, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
            uiPrimitives++;
        }

        private static void DrawRectUI(float x, float y, float w, float h, int r, int g, int b, int a)
        {
            if (uiPrimitives > MAX_UI_PRIMITIVES) return;
            Function.Call(Hash.DRAW_RECT, x, y, w, h, r, g, b, a);
            uiPrimitives++;
        }

        private static void DrawAttemptLine2D(List<SpeedRecord> records, float yMax, float startT, float endT, int r, int g, int b, int a, float thickness, int stride)
        {
            if (records == null || records.Count < 2) return;
            if (uiPrimitives > MAX_UI_PRIMITIVES) return;

            int startIdx = GetStartIndex(records, startT);
            if (startIdx < 1) startIdx = 1;

            float lastX = -999f;
            float minDist = 0.001f;

            for (int i = startIdx; i < records.Count && uiPrimitives <= MAX_UI_PRIMITIVES; i += stride)
            {
                SpeedRecord record = records[i];
                if (record.Time > endT) break;

                float x = GRAPH_X + (record.Time - startT) / TIME_WINDOW * GRAPH_WIDTH;
                if (Math.Abs(x - lastX) >= minDist && x >= GRAPH_X)
                {
                    float y = GRAPH_Y + GRAPH_HEIGHT - Math.Min(record.Speed * 3.6f / yMax, 1f) * GRAPH_HEIGHT;
                    DrawRectUI(x, y, thickness, thickness * 1.5f, r, g, b, a);
                    lastX = x;
                }
            }
        }

        private static void DrawSavedAttempts2D(List<Attempt> savedAttempts, float yMax, float startT, float endT)
        {
            for (int i = 0; i < savedAttempts.Count; i++)
            {
                GetColor(i, out int r, out int g, out int b);
                DrawAttemptLine2D(savedAttempts[i].Records, yMax, startT, endT, r, g, b, 150, 0.002f, 3);
            }
        }

        private static void DrawLegend(List<Attempt> savedAttempts)
        {
            if (savedAttempts.Count == 0) return;

            float legendX = GRAPH_X + GRAPH_WIDTH + 0.01f;
            float legendY = GRAPH_Y - 0.015f;

            for (int i = 0; i < savedAttempts.Count; i++)
            {
                GetColor(i, out int r, out int g, out int b);
                DrawRectUI(legendX, legendY + i * 0.018f, 0.008f, 0.008f, r, g, b, 200);
                float maxSpeed = savedAttempts[i].MaxSpeed * 3.6f;
                DrawText($"Ghost #{i + 1}: {maxSpeed:F0}", legendX + 0.01f, legendY + i * 0.018f - 0.006f, 0.2f, r, g, b);
            }
        }

        private static void Draw3DTrails(List<Attempt> attempts, int stride)
        {
            if (worldPrimitives > MAX_WORLD_PRIMITIVES) return;

            float maxDistSq = 22500f; // 150m squared
            Vector3 playerPos = Game.Player.Character.Position;

            for (int i = 0; i < attempts.Count; i++)
            {
                List<SpeedRecord> records = attempts[i].Records;
                if (records == null || records.Count < 2) continue;

                GetColor(i, out int r, out int g, out int b);

                // Draw trail lines
                for (int j = 0; j < records.Count - stride; j += stride)
                {
                    if (worldPrimitives > MAX_WORLD_PRIMITIVES) break;

                    Vector3 pos1 = records[j].Position;
                    if (pos1.DistanceToSquared(playerPos) <= maxDistSq)
                    {
                        Vector3 pos2 = records[j + stride].Position;
                        Function.Call(Hash.DRAW_LINE, pos1.X, pos1.Y, pos1.Z, pos2.X, pos2.Y, pos2.Z, r, g, b, 200);
                        worldPrimitives++;
                    }
                }

                // Draw lean markers (state transitions only)
                for (int j = 1; j < records.Count; j++)
                {
                    if (worldPrimitives > MAX_WORLD_PRIMITIVES) break;

                    byte prevLean = records[j - 1].LeanState;
                    byte currLean = records[j].LeanState;

                    // Draw marker only on input start (state change to non-neutral)
                    if (currLean != prevLean && currLean != 0)
                    {
                        Vector3 pos = records[j].Position;
                        if (pos.DistanceToSquared(playerPos) <= maxDistSq)
                        {
                            float size = 0.1f;
                            if (currLean == 1) // Forward = Cyan
                                Function.Call(Hash.DRAW_BOX, pos.X - size, pos.Y - size, pos.Z - size, pos.X + size, pos.Y + size, pos.Z + size, 0, 255, 255, 200);
                            else if (currLean == 2) // Back = Red
                                Function.Call(Hash.DRAW_BOX, pos.X - size, pos.Y - size, pos.Z - size, pos.X + size, pos.Y + size, pos.Z + size, 255, 0, 0, 200);
                            worldPrimitives++;
                        }
                    }
                }
            }
        }

        #endregion

        #region Utility Methods

        private static int GetStartIndex(List<SpeedRecord> list, float time)
        {
            int left = 0;
            int right = list.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (list[mid].Time < time)
                    left = mid + 1;
                else
                    right = mid - 1;
            }
            return Math.Max(0, left - 1);
        }

        private static void GetColor(int rank, out int r, out int g, out int b)
        {
            switch (rank)
            {
                case 0: r = 255; g = 100; b = 100; return;
                case 1: r = 100; g = 255; b = 100; return;
                case 2: r = 100; g = 100; b = 255; return;
                default: r = 200; g = 200; b = 200; return;
            }
        }

        private static float GetMaxVisibleSpeed(Attempt currentAttempt, List<Attempt> savedAttempts, float startT, float endT)
        {
            float maxSpeed = 50f;
            int sampleStride = 5;

            // Check current attempt
            if (currentAttempt?.Records != null)
            {
                List<SpeedRecord> records = currentAttempt.Records;
                int idx = GetStartIndex(records, startT);
                while (idx < records.Count && records[idx].Time <= endT)
                {
                    float speed = records[idx].Speed * 3.6f;
                    if (speed > maxSpeed) maxSpeed = speed;
                    idx += sampleStride;
                }
            }

            // Check saved attempts
            foreach (Attempt attempt in savedAttempts)
            {
                if (attempt.Records == null) continue;
                int idx = GetStartIndex(attempt.Records, startT);
                while (idx < attempt.Records.Count && attempt.Records[idx].Time <= endT)
                {
                    float speed = attempt.Records[idx].Speed * 3.6f;
                    if (speed > maxSpeed) maxSpeed = speed;
                    idx += sampleStride;
                }
            }

            return maxSpeed;
        }

        #endregion
    }
}
