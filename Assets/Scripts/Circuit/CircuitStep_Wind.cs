using UnityEngine;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Component attached to windblow circuit step, requiring the player to a moving orientation for a given duration
     */
    public class CircuitStep_Wind : MonoBehaviour {

        static CircuitStep_Wind lastInstance; // Pseudo-Singleton

        [SerializeField, Tooltip("Object to target when destroying the prefab")] GameObject prefabRoot;

        float initialTime; // Start time of the step

        /*
         * Alignment related data
         */
        float targetAngle; // The actual alignment target
        bool isAligned; // Wether or not the drone is considered aligned with the wind direction
        float alignedStartTime; // Start time since when drone is considered aligned with the wind direction
        const float alignedMaxAngle = 10f; // The max angle, between drone and wind transforms, to be considered aligned
        const float alignmentDuration = 4f; // Duration in seconds for which the drone must stay aligned

        /*
         * Wind direction related data
         */


        Transform droneTransform;

        static bool requestLandingCamera; // Request landing camera when disabled and zone is close enough (used for info box by other components)
        public static bool RequestLandingCamera => requestLandingCamera;


        void Start() {
            lastInstance = this;
            Circuit.onCircuitStop += OnStepCompletion;
            initialTime = Time.time;
            targetAngle = CircuitManager.GetCircuitStep().StepRotaEuler.y;
            droneTransform = DroneMover.DroneRb.transform;
        }

        private void Update() {
            CheckAlignment();
            MoveWind();
        }

        /// <summary>
        /// Check the validity of actual alignment
        /// </summary>
        void CheckAlignment() {
            float alignedAngle = Mathf.Abs(droneTransform.eulerAngles.y - targetAngle);

            /*
             * Cancel validation if angle is too high
             */
            if (alignedAngle > alignedMaxAngle && alignedAngle < 360f - alignedMaxAngle) {
                isAligned = false;
                return;
            }

            /*
             * If drone wasn't aligned yet the previous frame, refresh the start time
             */
            if (!isAligned) {
                alignedStartTime = Time.time;
                isAligned = true;
                return;
            }

            /*
             * Check if alignment is completed
             */
            if (Time.time - alignedStartTime > alignmentDuration) {
                CompleteStep();
            }
        }

        /// <summary>
        /// Update the wind direction
        /// </summary>
        void MoveWind() {

        }

        void OnDestroy() {
            Circuit.onCircuitStop -= OnStepCompletion;
        }

        void OnTriggerEnter(Collider other) {
            if (other.attachedRigidbody == DroneMover.DroneRb) {
                CompleteStep();
            }
        }

        void CompleteStep() {
            CircuitManager.RequestCompleteStep(Time.time - initialTime); // Complete step, performance is step duration
            OnStepCompletion();
        }

        void OnStepCompletion() => GameObject.Destroy(prefabRoot);


        public static bool IsAligned() {
            if (lastInstance == null) { return false; }
            return lastInstance.isAligned;
        }

        public static float GetActualAngle() {
            if (lastInstance == null) { return 0f; }
            return lastInstance.targetAngle;
        }

    }

}
