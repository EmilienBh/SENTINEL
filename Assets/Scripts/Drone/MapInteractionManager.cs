using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the interactions with the "mission planner" map
    /// </summary>
    public class MapInteractionManager : MonoBehaviour, IPointerClickHandler {
        static List<MapInteractionManager> instances = new List<MapInteractionManager>(); // All instances are stored in order to notify them easily from an outer source

        [SerializeField, Tooltip("Use the rect transform of the object this component is attached to.")] RectTransform selfTransform;
        [SerializeField] DroneMover droneMover_Keyboard;
        [SerializeField] DroneMover droneMover_VR;
        [SerializeField] Transform mapCornerZero;
        [SerializeField] Transform mapCornerXMax;
        [SerializeField] Transform mapCornerYMax;
        [SerializeField] RectTransform evtolPin;
        [SerializeField] RectTransform autopilotPin;
        [SerializeField] GameObject buttonZoomMin;
        [SerializeField] GameObject buttonZoomMax;

        [SerializeField] UnityEvent onAutoPilotEnabled;
        [SerializeField] UnityEvent onAutoPilotDisabled;

        DroneMover droneMover => droneMover_VR.isActiveAndEnabled ? droneMover_VR : droneMover_Keyboard;

        // Zoom data
        float minZoom = 1f;
        float maxZoom = 2f;


        void Start() {
            instances.Add(this);
            this.GetComponent<AspectRatioFitter>().enabled = false; // Disable ratio fitter at start to be able to center map (we only need it for initial responsive purposes)
        }

        void OnDestroy() {
            instances.Remove(this);
        }

        void Update() {
            /*
             * Update evtol pin
             */
            Vector2 posRatio = DroneMover.GetWorlPositionToMapRatio();
            evtolPin.anchoredPosition = new Vector2(selfTransform.rect.width * posRatio.x - selfTransform.rect.width / 2f, selfTransform.rect.height * posRatio.y - selfTransform.rect.height / 2f);
            evtolPin.localEulerAngles = new Vector3(0f, 0f, DroneMover.GetWorlRotationToMap());

            /*
             * Center map position
             */
            if (selfTransform.localScale.x == minZoom) {
                selfTransform.localPosition = Vector3.zero;
            }
            else {
                selfTransform.localPosition = - evtolPin.localPosition * selfTransform.localScale.x;
            }
        }


        /// <summary>
        /// Function that turns the click event into a map position represented in percentage
        /// (Raycast base infos don't seem to cover this use case, this function is quite hard-coded and can probably be improved)
        /// </summary>
        public void OnPointerClick(PointerEventData pointerEventData) {
            Vector2 rangePos;
            Vector3 pointerWorldPos = pointerEventData.pointerCurrentRaycast.worldPosition;

            Vector3 xClosestPoint = ClosestPoint(mapCornerZero.position, mapCornerXMax.position, pointerWorldPos);
            Vector3 yClosestPoint = ClosestPoint(mapCornerZero.position, mapCornerYMax.position, pointerWorldPos);
            float xStartDistance = Vector3.Distance(mapCornerZero.position, xClosestPoint);
            float xMaxDistance = Vector3.Distance(mapCornerXMax.position, xClosestPoint);
            float yStartDistance = Vector3.Distance(mapCornerZero.position, yClosestPoint);
            float yMaxDistance = Vector3.Distance(mapCornerYMax.position, yClosestPoint);
            rangePos = new Vector2(xStartDistance / (xStartDistance + xMaxDistance), yStartDistance / (yStartDistance + yMaxDistance));
            droneMover.RequestDestination_MapPos(rangePos.x, rangePos.y);
        }

        /// <summary>
        /// Function that determines the closest point on a line between two points
        /// (Source : https://discussions.unity.com/t/get-closest-vector3-position-from-a-gameobject-and-two-transforms-and-the-line-inbetween-them/150904)
        /// </summary>
        private Vector3 ClosestPoint(Vector3 limit1, Vector3 limit2, Vector3 point) {
            Vector3 lineVector = limit2 - limit1;

            float lineVectorSqrMag = lineVector.sqrMagnitude;

            // Trivial case where limit1 == limit2
            if (lineVectorSqrMag < 1e-3f)
                return limit1;

            float dotProduct = Vector3.Dot(lineVector, limit1 - point);

            float t = -dotProduct / lineVectorSqrMag;

            return limit1 + Mathf.Clamp01(t) * lineVector;
        }

        /// <summary>
        /// Use this function on any autopilot update, so map pin gets updated too
        /// </summary>
        public static void OnAutopilotUpdate(Vector2? destination) {
            foreach (var instance in instances) {
                instance.UpdateAutopilotPin(destination);
            }
        }

        /// <summary>
        /// By-component function to update autopilot pin
        /// </summary>
        void UpdateAutopilotPin(Vector2? destination) {
            if (!destination.HasValue) {
                autopilotPin.gameObject.SetActive(false);
                onAutoPilotDisabled?.Invoke();
            }
            else {
                autopilotPin.anchoredPosition = new Vector2(Mathf.Lerp(-selfTransform.rect.width / 2f, selfTransform.rect.width / 2f, destination.Value.x), Mathf.Lerp(-selfTransform.rect.height / 2f, selfTransform.rect.height / 2f, destination.Value.y));
                autopilotPin.gameObject.SetActive(true);
                onAutoPilotEnabled?.Invoke();
            }
        }

        /// <summary>
        /// Function called to disable autopilot through map interaction
        /// </summary>
        public void RequestDisableAutopilot() => droneMover.CancelDestination();

        public void ZoomIn() => UpdateZoom(selfTransform.localScale.x + 0.2f);
        public void ZoomOut() => UpdateZoom(selfTransform.localScale.x - 0.2f);
        public void ZoomMax() => UpdateZoom(maxZoom);
        public void ZoomMin() => UpdateZoom(minZoom);

        void UpdateZoom(float newZoom) {
            if (newZoom > maxZoom) { newZoom = maxZoom; }
            if (newZoom < minZoom) { newZoom = minZoom; }
            selfTransform.localScale = new Vector3(newZoom, newZoom, newZoom);
            evtolPin.localScale = autopilotPin.localScale = new Vector3(1f / selfTransform.localScale.x, 1f / selfTransform.localScale.y, 1f / selfTransform.localScale.z);
            if (newZoom == minZoom) {
                buttonZoomMin.SetActive(false);
                buttonZoomMax.SetActive(true);
            }
            else {
                buttonZoomMin.SetActive(true);
                buttonZoomMax.SetActive(false);
            }
        }

    }

}
