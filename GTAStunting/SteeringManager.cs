using System;
using System.Diagnostics;
using System.Windows.Forms;
using GTA;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Provides linear steering control with configurable rise time.
    /// Overrides default keyboard steering for more precise control.
    /// </summary>
    public static class SteeringManager
    {
        #region Constants

        private static float RISE_TIME = 0.1f;

        #endregion

        #region State

        public static bool IsActive = false;
        private static float currentSteer = 0f;
        private static Stopwatch _timer = new Stopwatch();
        private static double _lastTickTime = 0.0;

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggles linear steering mode on/off.
        /// </summary>
        public static void Toggle()
        {
            IsActive = !IsActive;

            if (IsActive)
            {
                currentSteer = 0f;
                _timer.Restart();
                _lastTickTime = 0.0;
            }
            else
            {
                _timer.Stop();
            }

            GTAS.Notify(IsActive ? "Linear Steering ON" : "Linear Steering OFF");
        }

        /// <summary>
        /// Called every frame to apply linear steering logic.
        /// </summary>
        public static void Update()
        {
            if (!IsActive) return;

            Player player = Game.Player;
            if (player == null || !player.Character.Exists()) return;

            Vehicle vehicle = player.Character.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) return;

            if (!_timer.IsRunning)
                _timer.Start();

            double currentTime = _timer.Elapsed.TotalSeconds;
            float deltaTime = (float)(currentTime - _lastTickTime);
            _lastTickTime = currentTime;

            // Cap delta time to prevent large jumps
            if (deltaTime > 0.2f) deltaTime = 0f;

            // Disable default steering controls
            Game.DisableControlThisFrame(GTA.Control.VehicleMoveLeft);
            Game.DisableControlThisFrame(GTA.Control.VehicleMoveRight);
            Game.DisableControlThisFrame(GTA.Control.VehicleMoveLeftRight);

            // Get input
            float targetSteer = 0f;
            if (Game.IsKeyPressed(Keys.A)) targetSteer += 1f;
            if (Game.IsKeyPressed(Keys.D)) targetSteer -= 1f;

            // Apply linear ramping
            if (Math.Abs(targetSteer) < 0.001f)
            {
                currentSteer = 0f;
            }
            else
            {
                float rampSpeed = 1f / RISE_TIME * deltaTime;

                if (currentSteer < targetSteer)
                {
                    currentSteer += rampSpeed;
                    if (currentSteer > targetSteer) currentSteer = targetSteer;
                }
                else if (currentSteer > targetSteer)
                {
                    currentSteer -= rampSpeed;
                    if (currentSteer < targetSteer) currentSteer = targetSteer;
                }
            }

            // Apply steering
            try
            {
                Function.Call(Hash.SET_VEHICLE_STEER_BIAS, vehicle, currentSteer);
            }
            catch
            {
                IsActive = false;
                GTAS.Notify("Steering Native Failed");
            }

            DrawDebugUI(currentSteer, targetSteer);
        }

        #endregion

        #region Private Methods

        private static void DrawDebugUI(float current, float target)
        {
            float centerX = 0.5f;
            float centerY = 0.9f;
            float barWidth = 0.2f;
            float barHeight = 0.015f;

            // Background bar
            Function.Call(Hash.DRAW_RECT, centerX, centerY, barWidth, barHeight, 50, 50, 50, 200);

            // Center line
            Function.Call(Hash.DRAW_RECT, centerX, centerY, 0.002f, barHeight + 0.005f, 255, 255, 255, 255);

            // Target indicator
            float targetPos = centerX + -target * (barWidth / 2f);
            Function.Call(Hash.DRAW_RECT, targetPos, centerY, 0.002f, barHeight, 255, 50, 50, 255);

            // Current steer indicator
            if (Math.Abs(current) > 0.001f)
            {
                float width = barWidth / 2f * -current;
                Function.Call(Hash.DRAW_RECT, centerX + width / 2f, centerY, Math.Abs(width), barHeight * 0.6f, 50, 255, 50, 255);
            }

            // Text display
            Function.Call(Hash.SET_TEXT_FONT, 4);
            Function.Call(Hash.SET_TEXT_SCALE, 0.3f, 0.3f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, 255);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, $"In: {target:F1} | Out: {current:F2}");
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, centerX - 0.05f, centerY - 0.03f);
        }

        #endregion
    }
}
