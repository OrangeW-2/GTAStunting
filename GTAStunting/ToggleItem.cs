using System;

namespace GTAStunting
{
    /// <summary>
    /// Represents a toggle modification for XML serialization.
    /// </summary>
    [Serializable]
    public class ToggleItem
    {
        public int ModType;
        public bool IsActive;
    }
}
