using System.Collections;
using UnityEngine;

namespace Viable.VRNav
{
    /// <summary>
    /// Component designed to handle the reactions to move-point teleportation requests (anim and components update)
    /// </summary>
    public class MoveTransitionsManager : MonoBehaviour
    {
        [SerializeField, Tooltip("The container of move-points inside the drone.")]
        GameObject pointsInside;

        [SerializeField, Tooltip("The container of move-points outside the drone.")]
        GameObject pointsOutside;

        [Header("XR (PICO/OpenXR)")]
        [SerializeField, Tooltip("Your XR Origin transform (NOT the camera).")]
        Transform xrOrigin;

        [SerializeField, Tooltip("The XR Main Camera under XR Origin.")]
        Transform xrCamera;

        [Space]
        [SerializeField] Animator animator_canvas;
        [SerializeField] AnimationClip animFadeIn_canvas;
        [SerializeField] AnimationClip animFadeOut_canvas;

        [Space]
        [SerializeField] Animator animator_drone;
        [SerializeField] AnimationClip animFadeIn_drone_indoor;
        [SerializeField] AnimationClip animFadeOut_drone_indoor;

        GameObject actualPoint; // Last clicked move-point (disabled)
        bool _inside; // Whether last clicked move-point leads inside or not

        Coroutine _transitionCo;

        public void OnMoveClick_Inside(GameObject movePoint)
        {
            _inside = true;
            OnMoveClick(movePoint);
        }

        public void OnMoveClick_Outside(GameObject movePoint)
        {
            if (!DroneMover.IsLanded()) { return; } // Don't allow to move outside if drone is mid-air

            _inside = false;
            DroneMover.EnableMovement = false;
            OnMoveClick(movePoint);
        }

        void OnMoveClick(GameObject movePoint)
        {
            pointsInside.SetActive(false);
            pointsOutside.SetActive(false);

            actualPoint?.SetActive(true);
            movePoint.SetActive(false);
            actualPoint = movePoint;

            // Play fade-out
            if (animator_canvas && animFadeOut_canvas)
                animator_canvas.Play(animFadeOut_canvas.name);

            if (animator_drone && animFadeOut_drone_indoor)
                animator_drone.Play(animFadeOut_drone_indoor.name);

            // Robust: don't rely on animation events
            if (_transitionCo != null) StopCoroutine(_transitionCo);
            _transitionCo = StartCoroutine(CoFinishAfterFadeOut());
        }

        IEnumerator CoFinishAfterFadeOut()
        {
            float tCanvas = animFadeOut_canvas ? animFadeOut_canvas.length : 0.15f;
            float tDrone  = animFadeOut_drone_indoor ? animFadeOut_drone_indoor.length : 0.15f;
            float wait = Mathf.Max(tCanvas, tDrone);

            yield return new WaitForSeconds(wait);

            OnFadeOutCompleted();
            _transitionCo = null;
        }

        public void OnFadeOutCompleted()
        {
            if (actualPoint == null)
                return;

            // Teleport XR Origin so that the XR camera ends up at the move point
            TeleportXROriginTo(actualPoint.transform);

            if (_inside)
            {
                pointsInside.SetActive(true);
                DroneMover.EnableMovement = true;
            }
            else
            {
                pointsOutside.SetActive(true);
            }

            // Play fade-in
            if (animator_canvas && animFadeIn_canvas)
                animator_canvas.Play(animFadeIn_canvas.name);

            if (animator_drone && animFadeIn_drone_indoor)
                animator_drone.Play(animFadeIn_drone_indoor.name);
        }

        void TeleportXROriginTo(Transform seat)
        {
            if (xrOrigin == null || xrCamera == null || seat == null)
                return;

            // 1) rotate yaw so view matches seat yaw
            float currentYaw = xrCamera.eulerAngles.y;
            float targetYaw = seat.eulerAngles.y;
            float deltaYaw = Mathf.DeltaAngle(currentYaw, targetYaw);
            xrOrigin.RotateAround(xrCamera.position, Vector3.up, deltaYaw);

            // 2) move origin so camera lands on seat position
            Vector3 camOffset = xrCamera.position - xrOrigin.position;
            xrOrigin.position = seat.position - camOffset;
        }
    }
}