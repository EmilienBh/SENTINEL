using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Viable.Circuit {

    /*
     * Component designed to represent a circuit that can be played by the circuit manager
     * Can be initialized with scene transforms to create the circuit
     */
    [CreateAssetMenu(fileName = "New_Circuit", menuName = "Viable/Circuit", order = 1)]
    public class Circuit : ScriptableObject {

        [SerializeField] List<CircuitStep> steps;
        public List<CircuitStep> Steps => steps;

        public static Action onCircuitStop; // Give an event to which circuit components can subscribe to be notified of the interruption of the circuit
        int index;

        public CircuitStep actualStep { get; private set; }


        public void StartCircuit() {
            foreach (var step in steps) {
                step.Performance = 0f; // Reset all performance values
            }
            index = 0;
            UpdateStep();
        }

        /// <summary>
        /// Function called to mark a step as completed
        /// </summary>
        /// <param name="performance">The performance of the completed step</param>
        /// <returns>Wether or not there is at least one remaining step</returns>
        public bool CompleteStep(float performance) {
            actualStep.Performance = performance; // Set completed step performance
            index++;
            if (index < steps.Count) { // Update to new step
                UpdateStep();
                return true;
            }
            return false; // Last step reached
        }

        /// <summary>
        /// Function called to update the actual step after an index change, and instantiate its component
        /// </summary>
        void UpdateStep() {
            actualStep = steps[index];
            GameObject stepPrefab = null; // Get appropriated component prefab to instantiate for this step
            stepPrefab = CircuitManager.GetStepPrefab(actualStep.Type);
            if (stepPrefab != null) { // Instantiate component prefab if there is one
                GameObject.Instantiate(stepPrefab, actualStep.StepPosition, Quaternion.Euler(actualStep.StepRotaEuler.x, actualStep.StepRotaEuler.y, actualStep.StepRotaEuler.z));
            }
        }

    /// <summary>
    /// Function called to stop a circuit (at its end or when interrupted), to return performance results
    /// </summary>
    /// <returns>The circuit results as string</returns>
    public string CompleteCircuit() {
            string circuitResults = "";
            foreach (var step in steps) {
                circuitResults += step.PerformanceToString();
            }
            onCircuitStop?.Invoke();
            return circuitResults;
        }

    }

}
