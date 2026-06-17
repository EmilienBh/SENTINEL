using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Viable.VRNav {

    public class AttitudeIndicator : MonoBehaviour {

        [SerializeField, Tooltip("The provided sensor will be considered as a gyroscope (its world rotation will be used)")] Transform droneOrientation;
        [SerializeField, Tooltip("The attitude component to rotate to represent bank")] RectTransform bankComponent;
        [SerializeField, Tooltip("The attitude component to move to represent pitch")] RectTransform pitchComponent;

        const float pitchMultiplier = 1.0375f; // The multiplier to convert pitch angle into distance on indicator
        const float pitchMax = pitchMultiplier * 40f; // The maximum pitch distance covered by indicator (too high pitch will be capped)

        void Start() {
            if (droneOrientation == null) { Destroy(this); } // Disable this component if required data is not set
        }
        
        void Update() {
            bankComponent.localEulerAngles = new Vector3(0f, 0f, droneOrientation.eulerAngles.z);
            float pitchAngle = TransformUtils.GetCenteredAngle(droneOrientation.eulerAngles.x); // Make sure the pitch angle is exploitable
            if (pitchAngle > 0) {
                pitchComponent.anchoredPosition = new Vector2(0f, Mathf.Min(pitchAngle * pitchMultiplier, pitchMax));
            }else {
                pitchComponent.anchoredPosition = new Vector2(0f, Mathf.Max(pitchAngle * pitchMultiplier, -pitchMax));
            }
        }

    }

}
