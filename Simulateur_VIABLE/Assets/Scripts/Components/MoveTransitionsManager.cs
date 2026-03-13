using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the reactions to move-point teleportation resquests (anim and components update)
    /// </summary>
    public class MoveTransitionsManager : MonoBehaviour {

        [SerializeField, Tooltip("The container of move-points inside the drone.")] GameObject pointsInside;
        [SerializeField, Tooltip("The container of move-points outside the drone.")] GameObject pointsOutside;
        [SerializeField] Transform playerTransform;
        [Space]
        [SerializeField] Animator animator_canvas;
        [SerializeField] AnimationClip animFadeIn_canvas;
        [SerializeField] AnimationClip animFadeOut_canvas;
        [Space]
        [SerializeField] Animator animator_drone;
        [SerializeField] AnimationClip animFadeIn_drone_indoor;
        [SerializeField] AnimationClip animFadeOut_drone_indoor;

        GameObject actualPoint; // Last clicked move-point (disabled)
        bool _inside; // Wether last clicked move-point leads inside or not



        public void OnMoveClick_Inside(GameObject movePoint) {
            _inside = true;
            OnMoveClick(movePoint);
        }
        public void OnMoveClick_Outside(GameObject movePoint) {
            if (!DroneMover.IsLanded()) { return; } // Don't allow to move outside if drone is mid-air

            _inside = false;
            DroneMover.EnableMovement = false;
            OnMoveClick(movePoint);
        }

        /// <summary>
        /// Function called when a move-point is clicked.
        /// Disables all move points, updates last clicked point, and initiates fade-out anim.
        /// </summary>
        void OnMoveClick(GameObject movePoint) {
            pointsInside.SetActive(false);
            pointsOutside.SetActive(false);
            actualPoint?.SetActive(true);
            movePoint.SetActive(false);
            actualPoint = movePoint;
            animator_canvas.Play(animFadeOut_canvas.name);
            animator_drone.Play(animFadeOut_drone_indoor.name);
            //OnFadeOutCompleted(); // FIXME : This line should be straight up removed ; It's here because in VR, UI black fade dosen't work...
        }

        /// <summary>
        /// Function called when fade-out is complete.
        /// Enables appropriated move points, and initiates fade-in anim.
        /// </summary>
        public void OnFadeOutCompleted() {
            playerTransform.position = actualPoint.transform.position;
            if (_inside) {
                pointsInside.SetActive(true);
                DroneMover.EnableMovement = true;
            }
            else {
                pointsOutside.SetActive(true);
            }
            animator_canvas.Play(animFadeIn_canvas.name);
            animator_drone.Play(animFadeIn_drone_indoor.name);
        }

    }

}
