using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Viable.VRNav
{
    public class DroneMover_VRInputs : DroneMover
    {
        [SerializeField] Transform droneJoystick;
        [SerializeField] Transform droneThruster;
        [SerializeField, Tooltip("Acceleration slider is under the throttle 3D model")]
        Slider accelerationSlider;

        const float maxJoystickAngle = 20f;
        const float maxThrusterAngle = 40f;

        protected override void UpdateInputs()
        {
            Vector2 leftAxis  = ReadAxis(XRNode.LeftHand);   // throttle
            Vector2 rightAxis = ReadAxis(XRNode.RightHand);  // joystick

            if (droneThruster != null)
                droneThruster.localEulerAngles = new Vector3(0f, 0f, maxThrusterAngle * leftAxis.y);

            if (droneJoystick != null)
                droneJoystick.localEulerAngles = new Vector3(
                    maxJoystickAngle * rightAxis.y,
                    -90f,
                    -maxJoystickAngle * rightAxis.x
                );

            if (accelerationSlider != null)
                accelerationSlider.value = (leftAxis.y + 1f) * 0.5f;

            HandleInputs(leftAxis, rightAxis);
        }

        static Vector2 ReadAxis(XRNode node)
        {
            var d = InputDevices.GetDeviceAtXRNode(node);

            if (d.isValid && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 a))
                return a;

            if (d.isValid && d.TryGetFeatureValue(CommonUsages.secondary2DAxis, out a))
                return a;

            return Vector2.zero;
        }

        protected override void UpdateCamera() { }
    }
}