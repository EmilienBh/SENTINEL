using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to transmit a camera change to DroneScreenCameraManager (used with Unity Events)
    /// </summary>
    public class ScreenCameraChangeHandler : MonoBehaviour {

        bool isFront = true; // Actual state of the camera

        public void ChangeCamera() {
            isFront = !isFront;
            DroneScreenCameraManager.ChangeCamera(isFront);
        }

    }

}
