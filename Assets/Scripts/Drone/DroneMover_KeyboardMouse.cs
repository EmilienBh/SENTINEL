using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the drone movement
    /// This is the keyboard-mouse version, implementing inputs for this config
    /// </summary>
    public class DroneMover_KeyboardMouse : DroneMover {

        [SerializeField] GameObject[] vrComponentsToDisable;
        [SerializeField] Transform droneJoystick;
        [SerializeField] Transform droneThruster;
        [SerializeField, Tooltip("Acceleration slider is under the throttle 3D model")] Slider accelerationSlider;

        const float maxJoystickAngle = 20f;
        const float maxThrusterAngle = 40f;

        // Mouse vals
        const float arrowMouseSpeed = 7.0f;
        const float translateSpeed = 300.0f;
        float rotY = 0.0f; // rotation around the up/y axis
        float rotX = 0.0f; // rotation around the right/x axis
        Quaternion newRotation;
        bool modeFree; // Wether or not cursor is "free" (not locked in front of screen - FPS mode)


        protected override void OnEnable() {
            base.OnEnable();
            SetFreeCameraMode(true);
            foreach (var comp in vrComponentsToDisable) { comp.SetActive(false); }
        }

        protected override void UpdateInputs() {
            Vector2 rawInput_Left;
            Vector2 rawInput_Right;
            if (Input.GetKey(KeyCode.Z)) { rawInput_Left.y = 1f; } else if (Input.GetKey(KeyCode.S)) { rawInput_Left.y = -1f; } else { rawInput_Left.y = 0f; }
            if (Input.GetKey(KeyCode.D)) { rawInput_Left.x = 1; } else if (Input.GetKey(KeyCode.Q)) { rawInput_Left.x = -1; } else { rawInput_Left.x = 0f; }
            if (Input.GetKey(KeyCode.UpArrow)) { rawInput_Right.y = 1; } else if (Input.GetKey(KeyCode.DownArrow)) { rawInput_Right.y = -1; } else { rawInput_Right.y = 0f; }
            if (Input.GetKey(KeyCode.RightArrow)) { rawInput_Right.x = 1; } else if (Input.GetKey(KeyCode.LeftArrow)) { rawInput_Right.x = -1; } else { rawInput_Right.x = 0f; }

            if (droneThruster != null) { droneThruster.localEulerAngles = new Vector3(0f, 0f, maxThrusterAngle * rawInput_Left.y); }
            if (droneJoystick != null) { droneJoystick.localEulerAngles = new Vector3(maxJoystickAngle * rawInput_Right.y, -90f, -maxJoystickAngle * rawInput_Right.x); }
            accelerationSlider.value = (rawInput_Left.y + 1f) / 2f;
            HandleInputs(rawInput_Left, rawInput_Right);
        }

        protected override void UpdateCamera() {
            /*
             * Camera/Mouse mode
             */
            if (Input.GetKeyDown(KeyCode.Escape))
                SetFreeCameraMode(!modeFree);

            /*
             * Camera orientation
             */
            rotY += Input.GetAxis("Mouse X") * arrowMouseSpeed;
            rotX += -Input.GetAxis("Mouse Y") * arrowMouseSpeed;

            newRotation = Quaternion.Euler(rotX, rotY, 0.0f);
            mainCamera.localRotation = newRotation;
        }

        /// <summary>
        /// Change between camera mode and cursor mode
        /// If value is true camera mode
        /// If value is false cursor mode
        /// </summary>
        /// <param name="value">true for cam mode, false for cursor mode</param>
        public void SetFreeCameraMode(bool value) {
            modeFree = value;
            Cursor.visible = !value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
        }

    }

}
