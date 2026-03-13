using UnityEngine;
using System.Collections;
using System;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(WaypointContainer))]
public class WaypointContainerEditor : Editor {

    WaypointContainer _target;
    private static bool m_editMode = false;

    // Object selection popup variables
    string[] placeableObjectNames;
    string[] barrierNames;
    int selectedObjectIndex;
    int selectedBarrierIndex;
    float quickScale;


    private void Awake() {
        _target = (WaypointContainer) target;
    }

    void OnSceneGUI() {

        if (m_editMode) {
            if (Event.current.type == EventType.MouseUp) {
                Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                RaycastHit hitInfo;

                if (Physics.Raycast(worldRay, out hitInfo)) {
                    _target.PlaceObject(hitInfo.point, quickScale);
                }

            }

            Event.current.Use();

        }

    }

    public override void OnInspectorGUI() {
        if (m_editMode) {
            if (placeableObjectNames == null || barrierNames == null) { m_editMode = false; return; }

            // Object dropdown
            selectedObjectIndex = EditorGUILayout.Popup(selectedObjectIndex, placeableObjectNames);
            _target.selectedObject = _target.placeableObjects[selectedObjectIndex];

            // Barrier dropdown
            if (_target.connectedBarriers) { // Barrier mode interface
                selectedBarrierIndex = EditorGUILayout.Popup(selectedBarrierIndex, barrierNames);
                _target.selectedBarrier = _target.barriers[selectedBarrierIndex];
            }

            quickScale = EditorGUILayout.Slider(quickScale, 0.2f, 2f);

            GUILayout.Space(10);
            if (GUILayout.Button("Exit Instantiation Mode")) { Btn_ExitInstantiationMode(); }
        }
        else {
            DrawDefaultInspector();
            GUILayout.Space(10);
            if (GUILayout.Button("Enter Instantiation Mode")) { Btn_EnterInstantiationMode(); }
        }

        if (_target.connectedBarriers) { // Barrier mode interface
            GUILayout.Space(10);
            GUILayout.Label($"Barriers In Edition : {_target.actualBarriers?.Count} \nLast Object : {_target.lastObject}");
            if (GUILayout.Button("Stop Actual Barrier")) { _target.ValidateBarriers(); }
        }

    }

    // On-click button : Exit Instantiation Mode
    void Btn_ExitInstantiationMode() {
        m_editMode = false;
    }

    // On-click button : Enter Instantiation Mode
    void Btn_EnterInstantiationMode() {
        m_editMode = true;

        // Placeable objects data update
        if (_target.placeableObjects.Length != 0) {
            _target.selectedObject = _target.placeableObjects[0];
            placeableObjectNames = _target.placeableObjects.Select(f => f.name).ToArray(); // Objs array to string array (for dropdown)
            barrierNames = _target.barriers.Select(f => f.name).ToArray(); // Objs array to string array (for dropdown)
            quickScale = 1f;
        }
    }

}
