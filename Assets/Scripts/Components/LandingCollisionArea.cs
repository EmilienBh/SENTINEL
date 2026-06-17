using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Viable.VRNav {

    /// <summary>
    /// Component designed to detect if a landing zone is wrong, and if that's the case, change it
    /// </summary>
    public class LandingCollisionArea : MonoBehaviour {

        public static LandingCollisionArea Instance; // Singleton instance
        const float circleDistance = 10f; // The radius to apply to circular checks for a valid position
        const int maxNbCircles = 10; // The maximum number of circles applied to try to find a valid position

        bool posFound;

        private void Start() {
            if (Instance == null) { Instance = this; }
        }

        // There is no way to check actual collisions at any time, so we have to reference them manually
        List<Collider> collisions = new List<Collider>();
        void OnTriggerEnter(Collider other) => collisions.Add(other);
        void OnTriggerExit(Collider other) => collisions.Remove(other);


        /// <summary>
        /// Function called to check if a destination is valid, and to update it if not
        /// Since it relies on collision check, frame delays are required, so this function updates destination whenever it can
        /// </summary>
        public IEnumerator CheckDestination(Vector3 worldDestination) {
            posFound = false;
            yield return CheckPos(worldDestination);
            if (posFound) { yield break; } // If initial destination is valid, don't do anything

            for (int circleIt = 1 ; circleIt < maxNbCircles + 1 ; circleIt++) {
                int x = 0; int z = circleIt; // Make a "circle" from X/Y pos (actually a square, but will work the same way)
                while (z >= 0) {
                    yield return TryOffset(worldDestination, x, z); // Try true positive pos
                    if (posFound) { yield break; }

                    if (x != 0) {
                        yield return TryOffset(worldDestination, -x, z); // Try -X pos
                        if (posFound) { yield break; }
                    }
                    if (z != 0) {
                        yield return TryOffset(worldDestination, x, -z); // Try -Z pos
                        if (posFound) { yield break; }
                    }
                    if (x != 0 && z != 0) {
                        yield return TryOffset(worldDestination, -x, -z); // Try true negative pos
                        if (posFound) { yield break; }
                    }
                    x++; z--;
                }
            }

            Debug.LogError($"[{nameof(LandingCollisionArea)}] No valid landing area was found. Initial destination will be used");
        }

        /// <summary>
        /// Quick-bind function to avoid redundant code : Calculates an attempt destination pos and returns wether or not it's valid
        /// </summary>
        IEnumerator TryOffset(Vector3 worldDestination, float x, float z) {
            Vector3 attemptDestination = worldDestination + (new Vector3(x, 0f, z) * circleDistance);
            yield return CheckPos(attemptDestination);
        }

        /// <summary>
        /// Moves self-trigger to requested pos, and looks for collisions. Returns true if no collision is found
        /// </summary>
        IEnumerator CheckPos(Vector3 attemptDestination) {
            transform.position = attemptDestination + Vector3.up;
            yield return new WaitForEndOfFrame();
            if (collisions.Count == 0) {
                DroneMover.UpdateDestination(attemptDestination);
                posFound = true;
            }
        }

    }

}
