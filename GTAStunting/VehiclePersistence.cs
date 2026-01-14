using System;
using System.IO;
using System.Xml.Serialization;
using GTA;

namespace GTAStunting
{
    /// <summary>
    /// Handles saving and loading vehicle configurations (colors, mods, extras) to/from XML files.
    /// </summary>
    public static class VehiclePersistence
    {
        private static string ConfigFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VehicleConfigs");

        static VehiclePersistence()
        {
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);
        }

        /// <summary>
        /// Saves the vehicle's configuration to an XML file.
        /// </summary>
        public static void SaveVehicle(Vehicle veh, string filename)
        {
            if (veh == null || !veh.Exists())
            {
                GTAS.Notify("No vehicle to save!");
                return;
            }

            VehicleData data = new VehicleData
            {
                Name = veh.LocalizedName,
                ModelHash = veh.Model.Hash,
                PrimaryColor = (int)veh.Mods.PrimaryColor,
                SecondaryColor = (int)veh.Mods.SecondaryColor,
                PearlescentColor = (int)veh.Mods.PearlescentColor,
                RimColor = (int)veh.Mods.RimColor,
                DashboardColor = (int)veh.Mods.DashboardColor,
                TrimColor = (int)veh.Mods.TrimColor,
                WindowTint = (int)veh.Mods.WindowTint,
                WheelType = (int)veh.Mods.WheelType,
                Livery = veh.Mods.Livery,
                PlateText = veh.Mods.LicensePlate,
                PlateStyle = (int)veh.Mods.LicensePlateStyle
            };

            // Collect mods
            foreach (object obj in Enum.GetValues(typeof(VehicleModType)))
            {
                VehicleModType modType = (VehicleModType)obj;
                int index = veh.Mods[modType].Index;
                if (index > -1)
                {
                    data.Mods.Add(new ModItem { ModType = (int)modType, Index = index });
                }
            }

            // Collect toggle mods
            foreach (object obj in Enum.GetValues(typeof(VehicleToggleModType)))
            {
                VehicleToggleModType modType = (VehicleToggleModType)obj;
                if (veh.Mods[modType].IsInstalled)
                {
                    data.Toggles.Add(new ToggleItem { ModType = (int)modType, IsActive = true });
                }
            }

            // Collect extras
            for (int i = 0; i < 15; i++)
            {
                if (veh.IsExtraOn(i))
                    data.Extras.Add(i);
            }

            try
            {
                string path = Path.Combine(ConfigFolder, filename + ".xml");

                // Refuse to overwrite existing file
                if (File.Exists(path))
                {
                    GTAS.Notify("Config already exists: " + filename + " - use different name!");
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(VehicleData));
                using (StreamWriter writer = new StreamWriter(path))
                {
                    serializer.Serialize(writer, data);
                }
                GTAS.Notify("Vehicle config saved: " + filename);
            }
            catch (Exception ex)
            {
                GTAS.Notify("Save failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads a vehicle configuration from an XML file and applies it.
        /// </summary>
        public static void LoadVehicle(Vehicle veh, string filename)
        {
            if (veh == null || !veh.Exists()) return;

            string path = Path.Combine(ConfigFolder, filename + ".xml");
            if (!File.Exists(path))
            {
                GTAS.Notify("Config not found: " + filename);
                return;
            }

            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(VehicleData));
                VehicleData data;
                using (StreamReader reader = new StreamReader(path))
                {
                    data = (VehicleData)serializer.Deserialize(reader);
                }

                if (veh.Model.Hash != data.ModelHash)
                    GTAS.Notify($"Warning: Applying {data.Name} config to {veh.LocalizedName}");

                // Apply configuration
                veh.Mods.InstallModKit();
                veh.Mods.WheelType = (VehicleWheelType)data.WheelType;
                veh.Mods.PrimaryColor = (VehicleColor)data.PrimaryColor;
                veh.Mods.SecondaryColor = (VehicleColor)data.SecondaryColor;
                veh.Mods.PearlescentColor = (VehicleColor)data.PearlescentColor;
                veh.Mods.RimColor = (VehicleColor)data.RimColor;
                veh.Mods.DashboardColor = (VehicleColor)data.DashboardColor;
                veh.Mods.TrimColor = (VehicleColor)data.TrimColor;
                veh.Mods.WindowTint = (VehicleWindowTint)data.WindowTint;
                veh.Mods.Livery = data.Livery;
                veh.Mods.LicensePlate = data.PlateText;
                veh.Mods.LicensePlateStyle = (LicensePlateStyle)data.PlateStyle;

                // Apply mods
                foreach (ModItem mod in data.Mods)
                {
                    VehicleModType modType = (VehicleModType)mod.ModType;
                    if (veh.Mods[modType].Count > 0)
                    {
                        int index = Math.Min(mod.Index, veh.Mods[modType].Count - 1);
                        veh.Mods[modType].Index = index;
                    }
                }

                // Apply toggles
                foreach (ToggleItem toggle in data.Toggles)
                {
                    VehicleToggleModType modType = (VehicleToggleModType)toggle.ModType;
                    veh.Mods[modType].IsInstalled = toggle.IsActive;
                }

                // Reset and apply extras
                for (int i = 0; i < 15; i++)
                {
                    if (veh.ExtraExists(i))
                        veh.ToggleExtra(i, false);
                }
                foreach (int extra in data.Extras)
                {
                    if (veh.ExtraExists(extra))
                        veh.ToggleExtra(extra, true);
                }

                GTAS.Notify("Vehicle loaded!");
            }
            catch (Exception ex)
            {
                GTAS.Notify("Load failed: " + ex.Message);
            }
        }
    }
}
