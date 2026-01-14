using GTA.Math;

namespace GTAStunting
{
    /// <summary>
    /// Represents a single recorded speed/position data point for ghost trails.
    /// </summary>
    public class SpeedRecord
    {
        public float Time { get; set; }
        public float Speed { get; set; }
        public float Height { get; set; }
        public Vector3 Position { get; set; }

        /// <summary>
        /// Lean input state: 0 = neutral, 1 = lean forward, 2 = lean back.
        /// </summary>
        public byte LeanState { get; set; }

        public SpeedRecord(float time, float speed, float height, Vector3 pos, byte lean = 0)
        {
            Time = time;
            Speed = speed;
            Height = height;
            Position = pos;
            LeanState = lean;
        }
    }
}
