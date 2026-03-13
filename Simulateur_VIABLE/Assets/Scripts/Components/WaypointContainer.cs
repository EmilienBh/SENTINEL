#if UNITY_EDITOR
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEditor;
using STRASS.Perception;

[ExecuteInEditMode]
public class WaypointContainer : MonoBehaviour {

    [SerializeField] GameObject container;
    [Space]
    public GameObject[] placeableObjects;
    [Tooltip("Only useful if connected barriers is enabled. \nPlease use normalized barriers (1x1x1 meters), with normalized pylons (also 1x1x1 meters)")] public GameObject[] barriers;
    public Vector3 objScale = Vector3.one;
    [Tooltip("If enabled, will randomize rotation on Y axis")] public bool randomRotation;
    [Tooltip("Scale will be randomized, in the extend of this factor (between it and its inverse), 1 for no randomization")] public float randomScaleFactor = 1f;
    [Space]
    [Tooltip("If enabled, will create barriers between objects")] public bool connectedBarriers;
    [DrawIf(nameof(connectedBarriers), true, ComparisonType.BoolEquals), Tooltip("If enabled, will place a last wall between first and last element, to close the area")] public bool isClosedArea;

    [NonSerialized] public GameObject selectedObject;
    [NonSerialized] public GameObject selectedBarrier;
    [NonSerialized] public GameObject lastObject;
    [NonSerialized] public GameObject firstObject = null;
    [NonSerialized] public List<GameObject> actualBarriers = new List<GameObject>();
    const float verticalScaleDiff = 0.01f; // Scale offset applied to barriers on validation, to avoid clipping


    /// <summary>
    /// Function called to place an object
    /// </summary>
    public void PlaceObject(Vector3 objPos, float quickScale = 1f) {
        GameObject waypointInstance = Instantiate(selectedObject) as GameObject;
        waypointInstance.transform.position = objPos;
        waypointInstance.transform.parent = container.transform;
        if (randomRotation) { waypointInstance.transform.eulerAngles = new Vector3(0f, UnityEngine.Random.Range(0f, 360f), 0f); }
        waypointInstance.transform.localScale = objScale * quickScale;
        if (randomScaleFactor != 1) { waypointInstance.transform.localScale *= UnityEngine.Random.Range(randomScaleFactor, 1f / randomScaleFactor); }
        Waypoint waypointScript = waypointInstance.GetComponent("Waypoint") as Waypoint;

        if (connectedBarriers) { PlaceBarrier(waypointInstance); } // Handle barrier placement

        EditorUtility.SetDirty(waypointInstance);
        if (firstObject == null) { firstObject = waypointInstance; }
        lastObject = waypointInstance;
    }

    /// <summary>
    /// Function called to place a barrier between last and new objects
    /// </summary>
    void PlaceBarrier(GameObject newObject) {
        if (lastObject == null || newObject == null) { return; }
        GameObject barrierInstance = Instantiate(selectedBarrier) as GameObject;
        barrierInstance.transform.parent = container.transform;
        EditorDynamicBarrier editorComp = barrierInstance.AddComponent<EditorDynamicBarrier>();
        editorComp.previousPylon = lastObject;
        editorComp.nextPylon = newObject;
        editorComp.TriggerUpdate(); // Trigger a manual update on creation (won't change anything, excepted for last barrier in closed areas, which won't have a frame to update)
        actualBarriers.Add(barrierInstance);
    }

    /// <summary>
    /// Function called to validate a barrier area : clears all objects and destroy all barrier temporary components
    /// </summary>
    public void ValidateBarriers() {
        if (actualBarriers == null) { actualBarriers = new List<GameObject>(); lastObject = null; return; } // This case should never happen ; If it does, reset relevant data
        int barrierVerticalOffsetDir = 1;
        if (actualBarriers.Count > 0 && actualBarriers[0] != null) { actualBarriers[0].transform.localScale += new Vector3(0f, verticalScaleDiff, 0f); } // Apply a second offset to first barrier, to make sure its offset is different than last one

        // Close area if enabled
        if (isClosedArea && firstObject != lastObject && lastObject != null) {
            PlaceBarrier(firstObject);
        }

        foreach (var barrier in actualBarriers) {
            if (barrier == null) { continue; };

            // Destroy edition component
            var edb = barrier.GetComponent<EditorDynamicBarrier>();
            if (edb != null) { GameObject.DestroyImmediate(edb); }

            // Slightly change vertical scale to avoid clipping
            barrier.transform.localScale += new Vector3(0f, verticalScaleDiff, 0f) * barrierVerticalOffsetDir;
            barrierVerticalOffsetDir *= -1;
        }
        firstObject = null;
        lastObject = null;
        actualBarriers.Clear();
    }

}
#endif