using System;
using System.Collections.Generic;

namespace GTAStunting
{
    /// <summary>
    /// Represents a single stunt attempt containing recorded speed/position data.
    /// </summary>
    public class Attempt
    {
        public List<SpeedRecord> Records { get; set; }
        public DateTime StartTime { get; set; }
        public float Duration { get; set; }
        public float MaxSpeed { get; set; }
        public float AverageSpeed { get; set; }
        public float MaxHeight { get; set; }
        public float MinHeight { get; set; }

        public Attempt()
        {
            Records = new List<SpeedRecord>();
            StartTime = DateTime.Now;
            Duration = 0f;
            MaxSpeed = 0f;
            AverageSpeed = 0f;
            MaxHeight = float.MinValue;
            MinHeight = float.MaxValue;
        }

        /// <summary>
        /// Calculates final statistics from recorded data.
        /// </summary>
        public void Finish()
        {
            if (Records.Count == 0) return;

            Duration = Records[Records.Count - 1].Time;
            float totalSpeed = 0f;

            foreach (SpeedRecord record in Records)
            {
                if (record.Speed > MaxSpeed) MaxSpeed = record.Speed;
                if (record.Height > MaxHeight) MaxHeight = record.Height;
                if (record.Height < MinHeight) MinHeight = record.Height;
                totalSpeed += record.Speed;
            }

            AverageSpeed = totalSpeed / Records.Count;
        }
    }
}
