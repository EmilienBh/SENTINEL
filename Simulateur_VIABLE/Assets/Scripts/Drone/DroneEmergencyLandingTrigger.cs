using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Viable.VRNav {
    /*
     * Component designed to catch emergency 
     */
    public class DroneEmergencyLandingTrigger : MonoBehaviour {

        [SerializeField] UnityEvent onEmergencyLandingStart;
        [SerializeField] UnityEvent onEmergencyLandingEnd;
        [SerializeField] UnityEvent onLowBatteryStart;
        [SerializeField] UnityEvent onLowBatteryEnd;

        void Start() {
            DroneMover.EmergencyLanding_Start += onEmergencyLandingStart.Invoke;
            DroneMover.EmergencyLanding_End += onEmergencyLandingEnd.Invoke;
            DroneMover.LowBattery_Start += onLowBatteryStart.Invoke;
            DroneMover.LowBattery_End += onLowBatteryEnd.Invoke;
        }

        void OnDestroy() {
            DroneMover.EmergencyLanding_Start -= onEmergencyLandingStart.Invoke;
            DroneMover.EmergencyLanding_End -= onEmergencyLandingEnd.Invoke;
            DroneMover.LowBattery_Start -= onLowBatteryStart.Invoke;
            DroneMover.LowBattery_End -= onLowBatteryEnd.Invoke;
        }

    }
}
