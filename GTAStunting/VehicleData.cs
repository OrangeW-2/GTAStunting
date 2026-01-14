using System;
using System.Collections.Generic;

namespace GTAStunting
{
    /// <summary>
    /// Serializable vehicle configuration data for save/load operations.
    /// </summary>
    [Serializable]
    public class VehicleData
    {
        public string Name;
        public int ModelHash;
        public int PrimaryColor;
        public int SecondaryColor;
        public int PearlescentColor;
        public int RimColor;
        public int DashboardColor;
        public int TrimColor;
        public int WindowTint;
        public int Livery;
        public int WheelType;
        public string PlateText;
        public int PlateStyle;
        public List<ModItem> Mods = new List<ModItem>();
        public List<ToggleItem> Toggles = new List<ToggleItem>();
        public List<int> Extras = new List<int>();
    }
}
