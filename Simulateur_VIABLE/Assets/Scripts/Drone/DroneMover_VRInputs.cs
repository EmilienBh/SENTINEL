using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the drone movement
    /// This is the VR version, implementing inputs for this config
    /// </summary>
    public class DroneMover_VRInputs : DroneMover {

        [SerializeField] Transform droneJoystick;
        [SerializeField] Transform droneThruster;
        [SerializeField, Tooltip("Acceleration slider is under the throttle 3D model")] Slider accelerationSlider;

        const float maxJoystickAngle = 20f;
        const float maxThrusterAngle = 40f;


        protected override void OnEnable() {
            base.OnEnable();
        }

        protected override void UpdateInputs() {
            /*
             * From controllers to commands rotation (hard-coded because out of time, should be replaced by grabbable commands)
             */
            droneThruster.localEulerAngles = new Vector3(0f, 0f, maxThrusterAngle * OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y);
            droneJoystick.localEulerAngles = new Vector3(maxJoystickAngle * OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).y, -90f, -maxJoystickAngle * OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x);
            accelerationSlider.value = (OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y + 1f) / 2f;

            HandleInputs(OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick), OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick));
        }

        protected override void UpdateCamera() { }

    }

}
