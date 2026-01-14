using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Vice City-style taxi boost (hop) functionality.
    /// Converted from VC physics to GTA V engine.
    /// </summary>
    internal static class TaxiBoost
    {
        #region State

        public static bool boosting;

        #endregion

        #region Public Methods

        /// <summary>
        /// Initiates a Vice City-style hop/jump if conditions are met.
        /// Physics based on VC: ApplyMoveForce(0,0,1)*mass*0.4 + ApplyTurnForce(Up*mass*0.035, Forward*1.0)
        /// </summary>
        public static void Boost()
        {
            if (boosting) return;

            // Block if using radio/character wheel
            if (Game.IsControlPressed(GTA.Control.VehicleRadioWheel) || Game.IsControlPressed(GTA.Control.CharacterWheel))
                return;

            Player player = Game.Player;
            Ped ped = player?.Character;
            if (ped == null) return;

            Vehicle vehicle = ped.CurrentVehicle;
            if (vehicle == null || !vehicle.Exists()) return;

            // VC Check: m_vecMoveSpeed.MagnitudeSqr() > sq(0.2f)
            // Converted to V: Speed > 5 m/s (~18 km/h) - VC used lower speeds
            if (vehicle.Speed < 5f) return;

            // VC Check: Must not already be airborne
            if (vehicle.IsInAir) return;

            // VC Check: m_aSuspensionSpringRatio[0] < 1.0f (at least one wheel touching ground)
            // In V, we raycast from wheel position to check ground contact
            Vector3 upVector = vehicle.UpVector;
            Vector3 wheelPos = vehicle.Position - upVector * 0.5f; // Approximate wheel level
            
            RaycastResult groundCheck = World.Raycast(wheelPos, wheelPos - upVector * 0.5f, IntersectFlags.Map, vehicle);
            if (!groundCheck.DidHit) return;

            // === APPLY VICE CITY PHYSICS ===
            float mass = vehicle.HandlingData.Mass;
            Vector3 forwardVector = vehicle.ForwardVector;
            
            // VC: ApplyMoveForce(CVector(0.0f, 0.0f, 1.0f) * m_fMass * 0.4f)
            // This is a pure upward impulse. In V, we use APPLY_FORCE_TO_ENTITY.
            // VC mass units differ from V - scale factor needed. 
            // V mass is typically 1000-2000kg for cars. VC was similar.
            // 0.4 * mass in VC gave a hop. In V, we need less due to different gravity model.
            float upForce = mass * 0.0025f; // Tuned for V (was 0.4 in VC, V needs ~0.25% of that)
            
            // VC: ApplyTurnForce(GetUp() * m_fMass * 0.035f, GetForward() * 1.0f)
            // This applies torque to pitch the car forward slightly during the hop.
            // In V: Apply a small forward rotation impulse
            float turnForce = mass * 0.00015f; // Tuned for V (was 0.035 in VC)

            // Apply upward force at center of mass
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY_CENTER_OF_MASS,
                vehicle,
                1,                    // Force type: 1 = impulse
                0f, 0f, upForce,      // Force vector (local Z = up)
                true,                 // Is local coords
                true,                 // Is direction relative to entity
                true                  // Is mass relative
            );

            // Apply forward pitch torque (nose down slightly for that VC feel)
            // Apply force at front of car pointing up, creating forward rotation
            Function.Call(Hash.APPLY_FORCE_TO_ENTITY,
                vehicle,
                1,                          // Force type: impulse
                0f, 0f, turnForce,          // Force direction (up)
                0f, 2f, 0f,                 // Offset (2m forward from center)
                0,                          // Bone index (0 = chassis)
                true,                       // Is direction relative
                true,                       // Ignore up vector
                true,                       // Is position relative
                false,                      // p13
                true                        // Is mass relative
            );

            boosting = true;
        }

        /// <summary>
        /// Stops the boost state.
        /// </summary>
        public static void StopBoost()
        {
            boosting = false;
        }

        /// <summary>
        /// Handles any per-frame boost logic (currently unused).
        /// </summary>
        public static void HandleWallStick(Vehicle vehicle)
        {
            // Reserved for future wall-stick functionality
        }

        #endregion
    }
}
