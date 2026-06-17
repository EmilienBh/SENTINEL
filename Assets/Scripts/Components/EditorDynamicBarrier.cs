namespace STRASS.Perception {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Component designed to handle the behaviour of a dynamic barrier, generating its own panel between two pylons (editor script, should not exist anymore after object setup)
    /// Barrier object (on which this component is placed) should measure 1x1x1 meters to get expected results
    /// </summary>
    [ExecuteInEditMode]
    public class EditorDynamicBarrier : MonoBehaviour {

        public GameObject previousPylon;
        public GameObject nextPylon;

        Quaternion quarterRota = Quaternion.Euler(0f, 90f, 0f); // Quarter-rotation to apply to fence (X-axis should be distance, not Z-axis)


        /// <summary>
        /// Function calculating and creating the barrier panel, based on pylons
        /// </summary>
        void Update() => TriggerUpdate();
        public void TriggerUpdate() {
            if (previousPylon == null || nextPylon == null) { return; }
            transform.position = (previousPylon.transform.position + nextPylon.transform.position) / 2f;
            transform.rotation = Quaternion.LookRotation((nextPylon.transform.position - previousPylon.transform.position).normalized, Vector3.up) * quarterRota;
            float barrierDistance = Vector3.Distance(previousPylon.transform.position, nextPylon.transform.position);
            transform.localScale = new Vector3(barrierDistance, previousPylon.transform.localScale.y, previousPylon.transform.localScale.z);
        }

    }

}
