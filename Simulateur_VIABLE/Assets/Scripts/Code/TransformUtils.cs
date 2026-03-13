using UnityEngine;

namespace Viable.VRNav {

    /// <summary>
    /// Class used to store global, re-usable functions related to transforms
    /// </summary>
    public static class TransformUtils {

        /// <summary>
        /// Converts an input angle to a "centered angle" : an angle between -180° and 180°
        /// </summary>
        public static float GetCenteredAngle(float angle) {
            angle = angle % 360f; // Ignore values over 360°
            if (angle > 180f) { return angle - 360f; } // Negative angle
            return angle; // Positive angle
        }

        /// <summary>
        /// Function that determines the closest point on a line between two points
        /// (Source : https://discussions.unity.com/t/get-closest-vector3-position-from-a-gameobject-and-two-transforms-and-the-line-inbetween-them/150904)
        /// </summary>
        public static Vector3 ClosestPoint(Vector3 limit1, Vector3 limit2, Vector3 point) {
            Vector3 lineVector = limit2 - limit1;

            float lineVectorSqrMag = lineVector.sqrMagnitude;

            // Trivial case where limit1 == limit2
            if (lineVectorSqrMag < 1e-3f)
                return limit1;

            float dotProduct = Vector3.Dot(lineVector, limit1 - point);

            float t = -dotProduct / lineVectorSqrMag;

            return limit1 + Mathf.Clamp01(t) * lineVector;
        }

    }

}
