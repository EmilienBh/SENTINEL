using UnityEngine;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Component attached to pre-landing circuit step, requiring the player to match position and orientation of the zone
     */
    public class CircuitStep_PreLandingZone : MonoBehaviour {

        [SerializeField, Tooltip("Object to target when destroying the prefab")] GameObject prefabRoot;

        Transform DroneHitboxPreview; // Drone original hitbox preview, to compare with the step landing zone
        float initialTime; // Start time of the step
        const float posBoundary = 3f; // Max offset for X/2 position to consider pre-landing valid
        //const float rotaBoundary = 5f; // Max offset for Y rotation to consider pre-landing valid

        static bool requestLandingCamera; // Request landing camera when disabled and zone is close enough (used for info box by other components)
        public static bool RequestLandingCamera => requestLandingCamera;


        void Start() {
            Circuit.onCircuitStop += OnStepCompletion;
            initialTime = Time.time;
            DroneHitboxPreview = DroneMover.DroneHitboxPreview;
        }

        private void Update() {
            if (DroneMover.GetGenericStats().y < 10f) { return; } // Altitude too low for maneuver

            float offsetX = Mathf.Abs(prefabRoot.transform.position.x - DroneHitboxPreview.position.x);
            float offsetZ = Mathf.Abs(prefabRoot.transform.position.z - DroneHitboxPreview.position.z);

            requestLandingCamera = (!DroneHitboxPreview.gameObject.activeInHierarchy && offsetX < 30f && offsetZ < 30f);
            if (offsetX > posBoundary || offsetZ > posBoundary) { return; } // Pos ouf of bounds

            /* OLD : Handle rotation (for landing in a restricted orientation)
            float stepRota = prefabRoot.transform.eulerAngles.y % 360f;
            float droneRota_Front = DroneHitboxPreview.eulerAngles.y % 360f;
            if (Mathf.Abs(TransformUtils.GetCenteredAngle(Mathf.Abs(stepRota - droneRota_Front))) > rotaBoundary) { return; } // Rota ouf of bounds*/
            /* OLD : Accept both directions (if landing zone is symetrical on Z-axis)
              float droneRota_Back = (DroneHitboxPreview.eulerAngles.y + 180f) % 360f; // Drone can be either be oriented in front or back direction
              if (Mathf.Abs(TransformUtils.GetCenteredAngle(Mathf.Abs(stepRota - droneRota_Front))) > rotaBoundary 
                && Mathf.Abs(TransformUtils.GetCenteredAngle(Mathf.Abs(stepRota - droneRota_Back))) > rotaBoundary) { return; } // Rota ouf of bounds*/

            CompleteStep(); // Drone in landing zone
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

    }

}
