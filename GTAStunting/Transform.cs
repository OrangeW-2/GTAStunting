using GTA;
using GTA.Math;

namespace GTAStunting
{
    /// <summary>
    /// Represents a spatial transform with position, rotation, and velocity.
    /// </summary>
    public class Transform : ISpatial
    {
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Quaternion Orientation { get; set; }
        public Vector3 Velocity { get; set; }
        public Vector3 RotationVelocity { get; set; }

        public Transform()
        {
            Position = Vector3.Zero;
            Rotation = Vector3.Zero;
            Orientation = Quaternion.Identity;
            Velocity = Vector3.Zero;
            RotationVelocity = Vector3.Zero;
        }

        public Transform(Entity entity)
        {
            Position = entity.Position;
            Rotation = entity.Rotation;
            Orientation = entity.Quaternion;
            Velocity = entity.Velocity;
            #pragma warning disable CS0618 // Type or member is obsolete
            RotationVelocity = entity.RotationVelocity;
            #pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
