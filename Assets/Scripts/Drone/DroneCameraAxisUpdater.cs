using System.Collections;
using UnityEngine;

namespace Viable.VRNav {
    /*
     * Component designed to listen the actual camera position, and display its center
     */
    public class DroneCameraAxisUpdater : MonoBehaviour {

        [SerializeField] Camera screenCamera;
        [SerializeField] RectTransform axisX;
        [SerializeField] RectTransform axisY;

        const float axisXUnitPerAngle = 5.3f;
        const float axisYUnitPerAngle = -6.575f;

        void Update() {
            bool showCameraAxis = DroneScreenCameraManager.Instance.isCameraDown; // Show camera axis whenever camera is down
            axisX.gameObject.SetActive(showCameraAxis);
            axisY.gameObject.SetActive(showCameraAxis);
            if (showCameraAxis) {
                axisX.anchoredPosition = new Vector3(axisX.anchoredPosition.x, TransformUtils.GetCenteredAngle(screenCamera.transform.localEulerAngles.x) * axisXUnitPerAngle);
                axisY.anchoredPosition = new Vector3(TransformUtils.GetCenteredAngle(screenCamera.transform.localEulerAngles.y) * axisYUnitPerAngle, axisY.anchoredPosition.y);
            }
        }

    }

}
