using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Provides noclip-style movement for player/vehicle positioning.
    /// </summary>
    public static class Jetpack
    {
        #region State

        public static bool is_active;
        public static int speed;
        private static int speed_multiplier;
        private static int speed_min;
        private static int speed_max;

        #endregion

        #region Settings

        public static void LoadSettings(ScriptSettings settings)
        {
            string section = "Jetpack";
            speed = settings.GetValue(section, "Speed", 10);
            speed_multiplier = settings.GetValue(section, "Speed Multiplier", 100);
            speed_min = settings.GetValue(section, "Speed Min", 0);
            speed_max = settings.GetValue(section, "Speed Max", 5000);
        }

        #endregion

        #region Public Methods

        public static void Toggle()
        {
            if (!is_active)
                Enable();
            else
                Disable();
        }

        public static void Enable()
        {
            is_active = true;
            Entity entity = GTAS.GetControlledEntity();
            if (entity != null)
            {
                entity.IsCollisionEnabled = false;
                entity.IsPositionFrozen = true;
                GTAS.Notify("Jetpack on");
            }
        }

        public static void Disable()
        {
            is_active = false;
            Entity entity = GTAS.GetControlledEntity();
            if (entity != null)
            {
                entity.IsCollisionEnabled = true;
                entity.IsPositionFrozen = false;
                GTAS.Notify("Jetpack off");
            }
        }

        public static void Move(Vector3 direction)
        {
            Game.DisableAllControlsThisFrame();
            Game.EnableControlThisFrame(GTA.Control.LookLeftRight);
            Game.EnableControlThisFrame(GTA.Control.LookUpDown);

            Entity entity = GTAS.GetControlledEntity();
            if (entity == null) return;

            // Ensure collision stays disabled every frame (game may reset it)
            if (entity.IsCollisionEnabled)
                entity.IsCollisionEnabled = false;

            entity.Heading += GameplayCamera.RelativeHeading;

            if (entity.EntityType == EntityType.Ped)
                Function.Call(Hash.SET_PED_CONFIG_FLAG, (Ped)entity, 60, true);

            if (direction == Vector3.Zero)
            {
                if (!entity.IsPositionFrozen)
                    entity.IsPositionFrozen = true;
                return;
            }

            if (entity.IsPositionFrozen)
                entity.IsPositionFrozen = false;

            float calculatedSpeed = speed * speed_multiplier;

            if (Game.IsKeyPressed(Keys.ShiftKey))
                calculatedSpeed *= 0.01f;

            if (calculatedSpeed > speed_max)
                calculatedSpeed = speed_max;
            else if (!Game.IsKeyPressed(Keys.ShiftKey) && calculatedSpeed < speed_min)
                calculatedSpeed = speed_min;

            entity.Velocity = direction * calculatedSpeed;
        }

        #endregion
    }
}
