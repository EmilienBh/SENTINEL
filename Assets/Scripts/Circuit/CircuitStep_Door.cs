using UnityEngine;
using UnityEngine.Events;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Component attached to any circuit step requiring the player to enter in collision with a trigger zone
     */
    public class CircuitStep_Door : MonoBehaviour {

        static CircuitStep_Door lastInstance; // Pseudo-Singleton

        [SerializeField, Tooltip("Object to target when destroying the prefab")] GameObject prefabRoot;
        [SerializeField, Tooltip("If false, step performance is time to reach door. If true, time out of target speed is doubled")] bool targetSpeed;
        [Space]
        [SerializeField] UnityEvent OnPrefabInitialized;
        const float speedToMatch = 70f;

        /*
         * Chrono related data
         */
        float initialTime; // Start time of the step
        bool targetSpeed_inRange = false; // Wether or not speed was in range at last frame
        float targetSpeed_inRangeInitialTime; // Start time when speed became in range
        float targetSpeed_totalTime; // Time at target speed
        const float targetSpeedRange = 7f; // The distance from target speed to consider as the match interval : [target - range ; target + range]

        /*
         * Step cancel related data
         */
        Vector3 startPos;
        float cancelDistance = 0f;


        void Start() {
            lastInstance = this;
            Circuit.onCircuitStop += OnStepCompletion;
            initialTime = Time.time;

            CircuitStep stepData = CircuitManager.GetCircuitStep();
            if (stepData.Type == StepType.Destination && stepData.CancelStepTime > 0f) {
                startPos = DroneMover.DroneRb.transform.position;
                cancelDistance = Vector3.Distance(startPos, gameObject.transform.position) * (1f - stepData.CancelStepTime);
            }

            SetStartPosition(stepData.StepPosition); // Depending of step type, it can need a specific update of its position
            OnPrefabInitialized?.Invoke(); // Only enable this step component once sure its position is set correctly
        }

        void Update() {
            prefabRoot.transform.LookAt(DroneMover.DroneRb.transform); // Rotate door to face drone
            if (targetSpeed) { // Destination step
                if (targetSpeed_inRange) {
                    if (!IsSpeedMatched()) {
                        targetSpeed_inRange = false;
                        targetSpeed_totalTime += Time.time - targetSpeed_inRangeInitialTime;
                    }
                }
                else {
                    if (IsSpeedMatched()) {
                        targetSpeed_inRange = true;
                        targetSpeed_inRangeInitialTime = Time.time;
                    }
                }
                if (cancelDistance != 0f) {
                    if (Vector3.Distance(DroneMover.DroneRb.transform.position, gameObject.transform.position) < cancelDistance) { CompleteStep(); }
                }
            }
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
            gameObject.SetActive(false); // Disable object to prevent further collision triggers
            if (targetSpeed) {
                if (targetSpeed_inRange) { targetSpeed_totalTime += Time.time - targetSpeed_inRangeInitialTime; } // Add actual time to target speed total time if relevant
                float targetPercentage = targetSpeed_totalTime / (Time.time - initialTime) * 100f;
                CircuitManager.RequestCompleteStep(targetPercentage); // Complete step, performance is time percentage at target speed
            }
            else {
                CircuitManager.RequestCompleteStep(Time.time - initialTime); // Complete step, performance is step duration
            }
            OnStepCompletion();
        }

        void OnStepCompletion() => GameObject.Destroy(prefabRoot);

        /*
         * Function designed to update the root object position if step is a detour (targetSpeed is a quick way to determine it), so it's relative to drone instead of world position
         */
        void SetStartPosition(Vector3 stepPosition) {
            if (targetSpeed) { return; } // Do nothing for destination steps
            var droneTransform = DroneMover.DroneRb.transform; // Store drone transform for quicker access                
            Vector3 newRootPosition = DroneMover.DroneRb.transform.position; // Door origin is drone
            newRootPosition += stepPosition.x * droneTransform.right; // Add X offset
            newRootPosition += stepPosition.y * droneTransform.up; // Add Y offset
            newRootPosition += stepPosition.z * droneTransform.forward; // Add Z offset
            prefabRoot.transform.position = newRootPosition; // Assign new position
        }

        public static bool IsSpeedMatched() {
            float actualSpeed = DroneMover.GetGenericStats().x;
            return actualSpeed > speedToMatch - targetSpeedRange && actualSpeed < speedToMatch + targetSpeedRange;
        }

        public static Vector3? GetDoorPosition() {
            return lastInstance?.prefabRoot.transform.position;
        }

    }

}
