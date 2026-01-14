using System;
using GTA;
using GTA.Math;
using GTA.Native;

namespace GTAStunting
{
    /// <summary>
    /// Handles spawning and upgrading vehicles for stunting.
    /// Maintains a rotation of up to 3 spawned vehicles.
    /// </summary>
    public static class VehicleSpawner
    {
        public static Vehicle[] vehicles = new Vehicle[3];

        /// <summary>
        /// Spawns a vehicle by name. If name is empty, prompts user input.
        /// </summary>
        public static void Spawner(string name = "")
        {
            if (name == "")
                name = Game.GetUserInput("");

            Model model = new Model(Function.Call<int>(Hash.GET_HASH_KEY, name));
            model.Request();

            if (model.IsVehicle)
            {
                // Rotate vehicle slots
                if (vehicles[0] != null)
                {
                    if (vehicles[1] != null)
                    {
                        if (vehicles[2] != null)
                            vehicles[2].Delete();
                        vehicles[2] = vehicles[1];
                    }
                    vehicles[1] = vehicles[0];
                }

                // Calculate spawn position
                float height = model.Dimensions.Item2.Z - model.Dimensions.Item1.Z;
                Vector3 position = Game.Player.Character.Position + Game.Player.Character.ForwardVector * (height + 1f);

                // Spawn and upgrade
                vehicles[0] = World.CreateVehicle(model, position, Game.Player.Character.Heading + 90f);
                UpgradeVehicle(vehicles[0]);

                // Set vehicle proofs
                Function.Call(Hash.SET_ENTITY_PROOFS, vehicles[0], true, true, true, false, true, false, 0, true);

                GTAS.Notify("Spawned " + vehicles[0].LocalizedName);
            }
            else
            {
                GTAS.Notify("Could not find vehicle");
            }
        }

        /// <summary>
        /// Fully upgrades a vehicle with all available mods.
        /// </summary>
        public static void UpgradeVehicle(Vehicle vehicle)
        {
            Function.Call(Hash.SET_VEHICLE_MOD_KIT, vehicle, 0);

            // Apply all standard mods
            foreach (object obj in Enum.GetValues(typeof(VehicleModType)))
            {
                VehicleModType modType = (VehicleModType)obj;
                int maxMod = Function.Call<int>(Hash.GET_NUM_VEHICLE_MODS, vehicle, (int)modType) - 1;
                if (maxMod >= 0)
                    Function.Call(Hash.SET_VEHICLE_MOD, vehicle, (int)modType, maxMod, false);
            }

            // Apply all toggle mods
            foreach (object obj in Enum.GetValues(typeof(VehicleToggleModType)))
            {
                VehicleToggleModType modType = (VehicleToggleModType)obj;
                Function.Call(Hash.TOGGLE_VEHICLE_MOD, vehicle, (int)modType, true);
            }

            // Make tires and wheels unbreakable
            Function.Call(Hash.SET_VEHICLE_TYRES_CAN_BURST, vehicle, false);
            Function.Call(Hash.SET_VEHICLE_WHEELS_CAN_BREAK, vehicle, false);

            vehicle.Mods.WindowTint = VehicleWindowTint.PureBlack;
        }
    }
}
