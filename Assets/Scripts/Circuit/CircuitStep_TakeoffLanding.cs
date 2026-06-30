using UnityEngine;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Component attached to any circuit step requiring the player to enter in collision with a trigger zone
     */
    public class CircuitStep_TakeoffLanding : MonoBehaviour {

        [SerializeField, Tooltip("Object to target when destroying the prefab")] GameObject prefabRoot;
        [Space]
        [SerializeField, Tooltip("True if step is TakeOff, false if step is Landing")] bool isTakeoff;

        const float takeoffHeight = 20f; // The altitude in meters to validate takeoff
        float initialTime;


        void Start() {
            Circuit.onCircuitStop += OnStepCompletion;
            initialTime = Time.time;
        }

        void OnDestroy() {
            Circuit.onCircuitStop -= OnStepCompletion;
        }

        void Update() {
            float actualHeight = DroneMover.GetGenericStats().y;
            if (isTakeoff) { if (actualHeight < 20f) { return; } } // Don't complete if too low during takeoff
            else { if (actualHeight > 1f) { return; } } // Don't complete if too high during landing
            CircuitManager.RequestCompleteStep(0f); // Complete step, no performance for takeoff/landing
            OnStepCompletion();
        }

        void OnStepCompletion() => GameObject.Destroy(prefabRoot);

    }

}