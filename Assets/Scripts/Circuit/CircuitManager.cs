using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Viable.VRNav;

namespace Viable.Circuit {

    /*
     * Component designed to handle the circuit system
     */
    public class CircuitManager : MonoBehaviour {

        #region Editor

#if UNITY_EDITOR

        [Header("In-Scene Circuit Edition")]
        [SerializeField, DrawIf(nameof(isSceneEditionActivated), false, ComparisonType.BoolEquals)] Circuit circuitToEdit;
        [SerializeField, DrawIf(nameof(isSceneEditionActivated), false, ComparisonType.BoolEquals), Button(nameof(StartSceneEdition), ButtonWidth = 110)] bool EditCircuit;
        [SerializeField, DrawIf(nameof(isSceneEditionActivated), true, ComparisonType.BoolEquals), Button(nameof(SaveSceneEdition), ButtonWidth = 90)] bool SaveCircuit;
        [SerializeField, HideInInspector] bool isSceneEditionActivated;

        void StartSceneEdition() {
            if (circuitToEdit == null) { Debug.LogError("You must provide a circuit to edit."); return; }
            isSceneEditionActivated = true;
            EditorUtility.SetDirty(this); // Prepare this component so changes applied can get saved

            float prevCancelStepTime = 0f;
            StepType prevStepType = StepType.Destination;
            Vector3 prevEuler = Vector3.zero;
            Vector3 posPrev_1 = Vector3.zero;
            Vector3 posPrev_2 = Vector3.zero;
            foreach (CircuitStep step in circuitToEdit.Steps) {
                GameObject stepPrefab = Local_GetStepPrefab(step.Type); // Retrieve step prefab
                if (stepPrefab == null) { Debug.LogError("Invalid step prefab..."); continue; } // Having this case occuring is a bug
                GameObject stepInstance = PrefabUtility.InstantiatePrefab(stepPrefab) as GameObject;
                stepInstance.transform.parent = this.transform;

                /*
                 * Detour steps have a special offset system, making their real transform more complex to handle
                 */
                if (step.Type == StepType.Detour) {
                    /*
                     * Drone coords at step start may vary... For preview purposes, consider it went in a perfect straight line until reaching the cancel step time
                     */
                    Vector3 estimatedDronePos = prevCancelStepTime == 0f ? posPrev_1 : Vector3.Lerp(posPrev_2, posPrev_1, prevCancelStepTime);
                    Vector3 estimatedDroneEuler;
                    if (prevStepType == StepType.PreLanding || prevStepType == StepType.Wind) {
                        estimatedDroneEuler = prevEuler; // Some steps ask a specific rotation, then just use it
                    }
                    else {
                        stepInstance.transform.LookAt(stepInstance.transform.position + posPrev_1 - posPrev_2); // Look-at in the previous step direction, from the step before
                        estimatedDroneEuler = new Vector3(0f, stepInstance.transform.eulerAngles.y, 0f); // We can't apply a LookAt without a transform, so step transform was used as a proxy... Only keep Y rota
                    }

                    /*
                     * Once we have the drone estimated coords, add this step as an offset to get the estimated detour coords
                     */
                    Vector3 stepPosOffset = Quaternion.Euler(estimatedDroneEuler) * step.StepPosition; // Distance and normalized direction give us the real pos offset 
                    stepInstance.transform.position = estimatedDronePos + stepPosOffset;
                    stepInstance.transform.LookAt(estimatedDronePos); // Once Detour step pos is correct, make it look at estimated drone position, this time it's its "real" orientation
                }
                /*
                 * Non-Detour step cases
                 */
                else {
                    stepInstance.transform.position = step.StepPosition; // In most cases, step position is pretty straightforward

                    /*
                     * For Destination steps : rotation is handled automatically, don't trust the given rota and use a LookAt to update it
                     */
                    if (step.Type == StepType.Destination) {
                        stepInstance.transform.LookAt(posPrev_1); // Look-at rota assignment based on previous step
                    }
                    /*
                     * Regular step rota assignment
                     */
                    else {
                        stepInstance.transform.eulerAngles = step.StepRotaEuler;
                    }
                }

                /*
                 * Update "previous" step data, for next iteration
                 */
                prevCancelStepTime = step.CancelStepTime;
                prevStepType = step.Type;
                prevEuler = step.StepRotaEuler;
                posPrev_2 = posPrev_1;
                posPrev_1 = stepInstance.transform.position;
                if (step.Type == StepType.PreLanding) { posPrev_1 += Vector3.up * 25f; } // Pre-landing pos is grounded, consider evtol is 25m above
            }
        }

        void SaveSceneEdition() {
            isSceneEditionActivated = false;
            EditorUtility.SetDirty(this);

            int stepIt = 0;
            float prevCancelStepTime = 0f;
            StepType prevStepType = StepType.Destination;
            Vector3 prevEuler = Vector3.zero;
            Vector3 posPrev_1 = Vector3.zero;
            Vector3 posPrev_2 = Vector3.zero;
            foreach (Transform prevObj in transform) {
                CircuitStep step = circuitToEdit.Steps[stepIt]; // Get the step data to edit

                if (step.Type == StepType.Detour) {
                    step.StepRotaEuler = prevObj.eulerAngles; // Rotation isn't even useful actually, just handle it quickly
                    /*
                     * Drone coords at step start may vary... For preview purposes, consider it went in a perfect straight line until reaching the cancel step time
                     */
                    Vector3 estimatedDronePos = prevCancelStepTime == 0f ? posPrev_1 : Vector3.Lerp(posPrev_2, posPrev_1, prevCancelStepTime);
                    Vector3 estimatedDroneEuler;
                    if (prevStepType == StepType.PreLanding || prevStepType == StepType.Wind) {
                        estimatedDroneEuler = prevEuler; // Some steps ask a specific rotation, then just use it
                    }
                    else {
                        prevObj.transform.LookAt(prevObj.transform.position + posPrev_1 - posPrev_2); // Look-at in the previous step direction, from the step before
                        estimatedDroneEuler = new Vector3(0f, prevObj.transform.eulerAngles.y, 0f); // We can't apply a LookAt without a transform, so prevObj transform was used as a proxy... Only keep Y rota
                    }

                    /*
                     * Once we have the drone estimated coords, add this step as an offset to get the estimated detour coords
                     */
                    float stepDistance = Vector3.Distance(Vector3.zero, step.StepPosition); // Transform the un-rotated position offset into a distance
                    Vector3 stepDirection = Quaternion.Euler(estimatedDroneEuler) * Vector3.forward; // Turn Euler Angles into a normalized vector (e.g. direction)
                    Vector3 stepPosOffset = Quaternion.Euler(estimatedDroneEuler) * step.StepPosition; // Distance and normalized direction give us the real pos offset 
                    Vector3 absoluteOffset = estimatedDronePos - prevObj.position; // Calculate the "absolute" offset, e.g. based on world referential and not evtol referential
                    step.StepPosition = Quaternion.Inverse(Quaternion.Euler(estimatedDroneEuler)) * -absoluteOffset; // Orientate the offset to be evtol-relative, and assign it to step position
                }
                else { // General step behaviour, pretty straightforward
                    step.StepPosition = prevObj.position;
                    step.StepRotaEuler = prevObj.eulerAngles;
                }

                /*
                 * Updated loop-related data
                 */
                stepIt++;
                prevCancelStepTime = step.CancelStepTime;
                prevStepType = step.Type;
                prevEuler = step.StepRotaEuler;
                posPrev_2 = posPrev_1;
                posPrev_1 = prevObj.position; // Use the position of the previous object, not the step position ! (because of Detour steps)
                if (step.Type == StepType.PreLanding) { posPrev_1 += Vector3.up * 25f; } // Pre-landing pos is grounded, consider evtol is 25m above
            }

            transform.Cast<Transform>().ToList().ForEach(prevObj => DestroyImmediate(prevObj.gameObject)); // This cast will allow to DestroyImmediate children consistently, unlike a foreach
        }

#endif

        #endregion

        #region Main Component

        protected static CircuitManager Instance; // Instance of this component (signleton)

        [Space]
        [SerializeField] bool enableSendLogs;
        [Space]
        [Header("Step Prefabs")]
        [SerializeField] GameObject stepPrefab_Setup;
        [SerializeField] GameObject stepPrefab_Takeoff;
        [SerializeField] GameObject stepPrefab_Destination;
        [SerializeField] GameObject stepPrefab_PreLanding;
        [SerializeField] GameObject stepPrefab_Landing;
        [SerializeField] GameObject stepPrefab_Detour;
        [SerializeField] GameObject stepPrefab_Wind;
        [Space, Header("Results-Related Components")]
        [SerializeField, Tooltip("The Circuit Cursor root object, to enable/disable on circuit start/end")] GameObject circuitCursor;
        [SerializeField, Tooltip("Container to enable to display results")] GameObject resultsContainer;
        [SerializeField, Tooltip("Text field in which results will be displayed")] TextMeshProUGUI resultsText;
        [SerializeField, Tooltip("Container to enable to display infos")] GameObject circuitInfosContainer;
        [SerializeField, Tooltip("Text field in which infos on actual step are given")] TextMeshProUGUI infosText;
        [Space]
        [SerializeField, Tooltip("Step infos container, to change color as a step feedback")] Image infosContainer;
        [SerializeField, Tooltip("Green color, for infos container")] Color GreenColor;
        [SerializeField, Tooltip("Orange color, for infos container")] Color OrangeColor;
        [SerializeField, Tooltip("Red color, for infos container")] Color RedColor;
        [Space, Header("Circuits")]
        [SerializeField] List<Circuit> availableCircuits;

        Circuit actualCircuit = null; // When null, it means no circuit is actually playing

        public static GameObject StepPrefab_Setup => Instance.stepPrefab_Setup;
        public static GameObject StepPrefab_Takeoff => Instance.stepPrefab_Takeoff;
        public static GameObject StepPrefab_Destination => Instance.stepPrefab_Destination;
        public static GameObject StepPrefab_PreLanding => Instance.stepPrefab_PreLanding;
        public static GameObject StepPrefab_Landing => Instance.stepPrefab_Landing;
        public static GameObject StepPrefab_Detour => Instance.stepPrefab_Detour;
        public static GameObject StepPrefab_Wind => Instance.stepPrefab_Wind;


        void Start() {
            Instance = this; // Assign to singleton
        }

        void Update() {
            if (DroneMover.IsCircuitInProgress) { UpdateCircuitInfos(); }
        }

        /// <summary>
        /// Requests the start of the given circuit
        /// </summary>
        /// <param name="circuitIndex">The index of the circuit to play, in the CircuitManager list</param>
        public static void RequestCircuitStart(int circuitIndex) => Instance?.StartCircuit(circuitIndex);
        void StartCircuit(int circuitIndex) {
            if (actualCircuit != null || circuitIndex >= availableCircuits.Count) { return; } // Check request validity
            actualCircuit = availableCircuits[circuitIndex];
            DroneMover.IsCircuitInProgress = true; // Notify circuit start to DroneMover
            actualCircuit.StartCircuit();
            circuitInfosContainer.SetActive(true);
            circuitCursor.SetActive(true);
        }

        /// <summary>
        /// Requests the completion of the actual step
        /// </summary>
        /// <param name="performance">The performance score for the completed step</param>
        public static void RequestCompleteStep(float performance) => Instance?.CompleteStep(performance);
        void CompleteStep(float performance) {
            if (!actualCircuit.CompleteStep(performance)) { // Check if completed step was the last one of the circuit
                StopCircuit(true);
            }
        }

        /// <summary>
        /// Requests the interruption of the actual circuit before its completion
        /// </summary>
        public static void RequestCircuitStop() => Instance?.StopCircuit(false);
        void StopCircuit(bool isCompleted) {
            if (actualCircuit == null) { return; } // Check request validity
            string circuitResults = actualCircuit.CompleteCircuit(); // Request the circuit completion (raw infos - lacks of an introduction message, added below)
            string circuitMessageToScreen = "Circuit complété!\nRésumé du parcours:" + circuitResults;
            string circuitMessageToDrive = $"[{actualCircuit.name}] - Statut : {(isCompleted ? "Complété" : "Non complété")} - Mode : {DroneMover.GetActualMode()}" + circuitResults;

            if (enableSendLogs) { GoogleDriveCircuitDataUploader.SubmitCircuitData(circuitMessageToDrive); }
            resultsText.text = circuitMessageToScreen;
            circuitInfosContainer.SetActive(false);
            circuitCursor.SetActive(false);
            resultsContainer.SetActive(true);
            actualCircuit = null; // Clear actual circuit
            DroneMover.IsCircuitInProgress = false; // Notify circuit end to DroneMover
        }

        void UpdateCircuitInfos() {
            switch (actualCircuit?.actualStep?.Type) {
                case StepType.Setup:
                    infosText.text = "Démarrage du circuit...";
                    infosContainer.color = GreenColor;
                    break;
                case StepType.TakeOff:
                    infosText.text = "Décollez à une altitude de 20m\npour commencer.";
                    infosContainer.color = GreenColor;
                    break;
                case StepType.Destination:
                    infosText.text = "Atteignez la destination.\nVitesse à maintenir :\n70km/h.";
                    infosContainer.color = CircuitStep_Door.IsSpeedMatched() ? GreenColor : OrangeColor;
                    break;
                case StepType.PreLanding:
                    if (DroneMover.GetGenericStats().y < 10f) {
                        infosText.text = "Préparation à l'atterrissage\nRemontez une altitude de 10m...";
                        infosContainer.color = RedColor;
                    }
                    else if (CircuitStep_PreLandingZone.RequestLandingCamera) {
                        infosText.text = "Préparation à l'atterrissage\nActivez la caméra d'atterrissage...";
                        infosContainer.color = OrangeColor;
                    }
                    else {
                        infosText.text = "Préparation à l'atterrissage\nPlacez-vous comme demandé le plus vite possible.";
                        infosContainer.color = OrangeColor;
                    }
                    break;
                case StepType.Landing:
                    infosText.text = "Circuit complété, atterrissez.";
                    infosContainer.color = GreenColor;
                    break;
                case StepType.Detour:
                    infosText.text = "Détour : Atteignez\nla destination\nsans limitation\nde vitesse.";
                    infosContainer.color = RedColor;
                    break;
                case StepType.Wind:
                    if (CircuitStep_Wind.IsAligned()) {
                        infosText.text = "Maintenez le cap indiqué...";
                        infosContainer.color = OrangeColor;
                    }
                    else {
                        infosText.text = "Fortes rafales de vent !\nCalcul d'un cap de compensation, suivez le cap indiqué.";
                        infosContainer.color = RedColor;
                    }
                    break;
            }
        }

        public static Vector3 RequestObjectivePosition() { if (Instance == null) { return Vector3.zero; } return Instance.GetObjectivePosition(); }
        public Vector3 GetObjectivePosition() {
            Vector3? nullablePos = Instance?.actualCircuit?.actualStep?.GetObjectivePosition();
            return nullablePos.HasValue ? nullablePos.Value : Vector3.zero;
        }

        public static CircuitStep GetCircuitStep() => Instance?.actualCircuit?.actualStep;

        public static Circuit GetCurrentCircuit() => Instance?.actualCircuit;

        public static bool TryGetCurrentStep(out CircuitStep step) {
            step = Instance?.actualCircuit?.actualStep;
            return step != null;
        }

        /// <summary>
        /// Function called to convert a prefab type into its prefab, attached to this component
        /// </summary>
        public static GameObject GetStepPrefab(StepType stepType) => Instance?.Local_GetStepPrefab(stepType);
        // Don't make this base function static : it's used for editor functions, and Instance is not set out of runtime
        GameObject Local_GetStepPrefab(StepType stepType) {
            switch (stepType) {
                case StepType.Setup:
                    return stepPrefab_Setup;
                case StepType.TakeOff:
                    return stepPrefab_Takeoff;
                case StepType.Destination:
                    return stepPrefab_Destination;
                case StepType.PreLanding:
                    return stepPrefab_PreLanding;
                case StepType.Landing:
                    return stepPrefab_Landing;
                case StepType.Detour:
                    return stepPrefab_Detour;
                case StepType.Wind:
                    return stepPrefab_Wind;
            }
            return null;
        }

        #endregion

    }

}