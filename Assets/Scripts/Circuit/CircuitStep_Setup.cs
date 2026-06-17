using UnityEngine;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Component attached to windblow circuit step, requiring the player to a moving orientation for a given duration
     */
    public class CircuitStep_Setup : MonoBehaviour {

        static CircuitStep_Setup instance; // Pseudo-singleton

        [SerializeField, Tooltip("Object to target when destroying the prefab")] GameObject prefabRoot;

        void Start() {
            instance = this;
            Circuit.onCircuitStop += OnStepCompletion;
            MoveTransitionsManager.CircuitSetupFadeStart(SetupCompleted);
        }

        void OnDestroy() {
            Circuit.onCircuitStop -= OnStepCompletion;
        }

        void OnStepCompletion() => GameObject.Destroy(prefabRoot);

        void SetupCompleted() {
            CircuitManager.RequestCompleteStep(0f); // Complete step, no performance for setup
            OnStepCompletion();
        }

        public static void TriggerMoveDrone() => instance?.OnTriggerMoveDrone();
        void OnTriggerMoveDrone() {
            DroneMover.DroneRb.transform.position = transform.position;
            DroneMover.DroneRb.transform.rotation = transform.rotation;
        }

    }

}
