using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the drone camera behaviours (camera change and rotation)
    /// </summary>
    public class DroneScreenCameraManager : MonoBehaviour {

        [SerializeField] Camera screenCamera;
        [SerializeField] Transform cameraPosFront;
        [SerializeField] Transform cameraPosDown;
        [SerializeField, Tooltip("The hitbox preview component, that should only be active for landing camera (hide it otherwise)")] GameObject hitboxPreviewComponent;

        Vector2 acutalAxisInput = Vector2.zero;
        public bool isCameraDown = false; // Wether or not the camera looks down

        const float camMaxSpeed = 7f; // Maximum speed in angle by second
        const float camMaxAngle = 20f; // Maximum camera angle that can be reached

        public static DroneScreenCameraManager Instance; // Singleton

        void Start() { if (Instance == null) { Instance = this; } }

        /// <summary>
        /// Function called to change the screen camera placement (front or down).
        /// </summary>
        public static void ChangeCamera(bool isFront) => Instance?.OnChangeCamera(isFront);
        void OnChangeCamera(bool isFront) {
            screenCamera.transform.parent = isFront ? cameraPosFront : cameraPosDown;
            isCameraDown = !isFront;
            hitboxPreviewComponent.SetActive(!isFront);
            screenCamera.transform.localEulerAngles = Vector3.zero;
            screenCamera.transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Function called to update the rotation axis for screen camera.
        /// (Axis must be set to 0 when rotation should stop.)
        /// </summary>
        public static void UpdateCameraRotation(Vector2 axis) => Instance?.OnUpdateCameraRotation(axis);
        void OnUpdateCameraRotation(Vector2 axis) {
            acutalAxisInput = axis;
        }

        void FixedUpdate() {
            // Add rotation angle to actual rotation
            float angleX = screenCamera.transform.localEulerAngles.x + acutalAxisInput.x * camMaxSpeed * Time.fixedDeltaTime;
            float angleY = screenCamera.transform.localEulerAngles.y + acutalAxisInput.y * camMaxSpeed * Time.fixedDeltaTime;
            float angleZ = screenCamera.transform.localEulerAngles.z;

            // Clamp angles to max
            angleX = TransformUtils.GetCenteredAngle(angleX);
            angleY = TransformUtils.GetCenteredAngle(angleY);
            if (angleX > 0) { angleX = Mathf.Min(angleX, camMaxAngle); } else { angleX = Mathf.Max(angleX, -camMaxAngle); }
            if (angleY > 0) { angleY = Mathf.Min(angleY, camMaxAngle); } else { angleY = Mathf.Max(angleY, -camMaxAngle); }

            screenCamera.transform.localEulerAngles = new Vector3(angleX, angleY, angleZ);
        }

    }

}
