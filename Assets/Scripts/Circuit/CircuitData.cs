using System;
using UnityEditor;
using UnityEngine;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Enum representing the type of a circuit step
     */
    [Serializable] public enum StepType { Setup, TakeOff, Destination, PreLanding, Landing, Wind, Detour }

    /*
     * Class representing a circuit step
     */
    [Serializable]
    public class CircuitStep {
        [SerializeField] StepType type;
        [SerializeField] Vector3 stepPosition; // The step position
        [SerializeField] Vector3 stepRotaEuler; // The step rotation in euler angles
        [SerializeField, DrawIf(nameof(type), StepType.Destination, ComparisonType.EnumEquals, DisablingType.ReadOnly), Tooltip("Only used for Destination steps. [0f ; 1f]\nIf set to 0, ignored. Represents the travel percentage after which step is canceled, used to initiate a crisis")] float cancelStepTime;
        float performance = 0f; // The performance score for this step after realization (generally the time to complete the action, excepted for destination which is the matching target speed percentage)

        public StepType Type => type;
        public Vector3 StepPosition { get => stepPosition; set => stepPosition = value; }
        public Vector3 StepRotaEuler { get => stepRotaEuler; set => stepRotaEuler = value; }
        public float CancelStepTime => cancelStepTime;
        public float Performance { get => performance; set => performance = value; }

        /// <summary>
        /// Function called to return a performance as a string, expliciting what the value represents
        /// </summary>
        public string PerformanceToString() {
            switch (type) {
                case StepType.Setup:
                case StepType.TakeOff:
                case StepType.Landing:
                    return "";
                case StepType.Destination:
                    return "\nDéplacement vers destination : " + performance.ToString("F0") + "%";
                case StepType.Detour:
                    return "\nDétour d'urgence : " + performance.ToString("F0") + " s.";
                case StepType.PreLanding:
                    return "\nAtterrissage : " + performance.ToString("F0") + " s.";
                case StepType.Wind:
                    return "\nCompensation de bourrasque : " + performance.ToString("F0") + " s.";
            }
            return "<Performance Inconnue>";
        }

        /// <summary>
        /// Function called to get the objective position (used for the orientation of the 3D cursor)
        /// </summary>
        public Vector3 GetObjectivePosition() {
            switch (type) {
                case StepType.TakeOff:
                    return DroneMover.DroneRb.transform.position + new Vector3(0f, 10000f, 0f);
                case StepType.Landing:
                    return DroneMover.DroneRb.transform.position + new Vector3(0f, -10000f, 0f);
                case StepType.Detour:
                    var nullablePos = CircuitStep_Door.GetDoorPosition();
                    return nullablePos.HasValue? nullablePos.Value: Vector3.zero;
                case StepType.Wind:
                    float angle = CircuitStep_Wind.GetActualAngle();
                    float normalSin = Mathf.Sin(Mathf.Deg2Rad * angle);
                    float normalCos = Mathf.Cos(Mathf.Deg2Rad * angle);
                    Vector3 posOffset = new Vector3(normalSin * 1000f, 0f, normalCos * 1000f);
                    return DroneMover.DroneRb.transform.position + posOffset;
            }
            return StepPosition;
        }

    }

}
