namespace GTAStunting
{
    /// <summary>
    /// Represents a single data point on the speed/height graph.
    /// </summary>
    public class GraphDataPoint
    {
        public float Time { get; set; }
        public float Speed { get; set; }
        public float Height { get; set; }

        public GraphDataPoint(float time, float speed, float height)
        {
            Time = time;
            Speed = speed;
            Height = height;
        }
    }
}
