using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to transform widget button interactions into calls to DroneScreenCameraManager (on press/release)
    /// </summary>
    public class ScreenCameraButtonHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {

        [SerializeField] Vector2 rotationAxis;

        public void OnPointerDown(PointerEventData eventData) {
            DroneScreenCameraManager.UpdateCameraRotation(rotationAxis);
        }

        public void OnPointerUp(PointerEventData eventData) {
            DroneScreenCameraManager.UpdateCameraRotation(Vector2.zero);
        }
    }

}
