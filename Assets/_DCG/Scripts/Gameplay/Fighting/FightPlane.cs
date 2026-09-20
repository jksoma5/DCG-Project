using UnityEngine;

namespace DCG.Gameplay.Fighting
{
    // The 2D plane a fight happens in.
    //
    // A fighting game is 2D because the camera and the two characters share one axis. Up to now that
    // axis was the map's east-west line: the stage was built along X and the camera stood to the south
    // of it (doc 12, 1-1). That works for a fight on a dedicated stage and breaks the moment the
    // opponent is a class that moves freely in three dimensions.
    //
    // So the axis is taken from the participants instead of from the map. It runs from the near side to
    // the far side, and it is what the camera calls "screen right". The plane is rebuilt every frame, so
    // a fighter chasing a strafing sniper keeps turning and keeps playing a 2D game: forward is always
    // toward the opponent, back is always away, and a sidestep is always across the screen.
    public readonly struct FightPlane
    {
        // Midpoint of the two participants.
        public readonly Vector3 Origin;
        // Unit vector from the near participant toward the far one. Screen right.
        public readonly Vector3 Axis;
        // Horizontal perpendicular, cross(up, axis). The camera stands on this side, which is what
        // makes Axis point right on screen rather than left.
        public readonly Vector3 Normal;

        public FightPlane(Vector3 origin, Vector3 axis)
        {
            Origin = origin;
            Axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.right;
            Normal = Vector3.Cross(Vector3.up, Axis).normalized;
        }

        // Builds the plane between two positions. Height is ignored: the plane is always vertical, so a
        // launched opponent stays in it. When the two are closer than the dead zone their direction is
        // meaningless, so the previous axis is kept and the plane stops spinning on the spot.
        public static FightPlane Between(Vector3 near, Vector3 far, Vector3 previousAxis, float deadZone)
        {
            Vector3 origin = (near + far) * .5f;
            Vector3 axis = far - near;
            axis.y = 0;
            if (axis.magnitude <= Mathf.Max(1e-4f, deadZone))
                axis = previousAxis.sqrMagnitude > 1e-6f ? previousAxis : Vector3.right;
            return new FightPlane(origin, axis);
        }

        // Where a point sits along the axis. This is the coordinate that decides left and right, the
        // job the raw world X used to do.
        public float Along(Vector3 point) => Vector3.Dot(point - Origin, Axis);

        // Horizontal distance between two points measured in the plane, which is the distance a move's
        // range is checked against.
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0; b.y = 0;
            return Vector3.Distance(a, b);
        }
    }
}
