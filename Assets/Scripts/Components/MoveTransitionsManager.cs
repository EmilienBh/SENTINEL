using System;
using System.Collections;
using UnityEngine;
using Viable.Circuit;

namespace Viable.VRNav
{
    /// <summary>
    /// Handle move-point teleport transitions (fade + reposition) and circuit setup fade.
    /// </summary>
    public class MoveTransitionsManager : MonoBehaviour
    {
        public static MoveTransitionsManager Instance { get; private set; }

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

        // Optionnel : ancien clip utilisé par les circuits (sans event). Si tu n’en as pas, laisse null.
        [SerializeField] AnimationClip animFadeOut_canvas_NoEvent;

        [Space]
        [SerializeField] Animator animator_drone;
        [SerializeField] AnimationClip animFadeIn_drone_indoor;
        [SerializeField] AnimationClip animFadeOut_drone_indoor;

        GameObject actualPoint; // Last clicked move-point (disabled)
        bool _inside; // Whether last clicked move-point leads inside or not

        Coroutine _transitionCo;
        Coroutine _circuitCo;

        void Awake()
        {
            Instance = this;
        }

        public void OnMoveClick_Inside(GameObject movePoint)
        {
            _inside = true;
            OnMoveClick(movePoint);
        }

        public void OnMoveClick_Outside(GameObject movePoint)
        {
            if (!DroneMover.IsLanded()) return; // Don't allow to move outside if drone is mid-air

            _inside = false;
            DroneMover.EnableMovement = false;
            OnMoveClick(movePoint);
        }

        void OnMoveClick(GameObject movePoint)
        {
            if (pointsInside) pointsInside.SetActive(false);
            if (pointsOutside) pointsOutside.SetActive(false);

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
                if (pointsInside) pointsInside.SetActive(true);
                DroneMover.EnableMovement = true;
            }
            else
            {
                if (pointsOutside) pointsOutside.SetActive(true);
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

        // =========================================================
        // CIRCUITS API (compatibilité avec l’ancien code)
        // =========================================================

        /// <summary>
        /// Called by circuit setup step. Fade out, move drone, fade in, then callback.
        /// </summary>
        public static void CircuitSetupFadeStart(Action onCompletion)
        {
            if (Instance == null)
            {
                Debug.LogError("[MoveTransitionsManager] CircuitSetupFadeStart: no Instance in scene.");
                onCompletion?.Invoke();
                return;
            }

            if (Instance._circuitCo != null)
                Instance.StopCoroutine(Instance._circuitCo);

            Instance._circuitCo = Instance.StartCoroutine(Instance.CoCircuitSetupFadeStart(onCompletion));
        }

        IEnumerator CoCircuitSetupFadeStart(Action onCompletion)
        {
            yield return null;

            // Fade out canvas (utilise le clip "NoEvent" si fourni, sinon le fadeOut classique)
            var fadeOutClip = animFadeOut_canvas_NoEvent != null ? animFadeOut_canvas_NoEvent : animFadeOut_canvas;
            if (animator_canvas && fadeOutClip)
                animator_canvas.Play(fadeOutClip.name);

            float waitOut = fadeOutClip != null ? fadeOutClip.length : 1.5f;
            yield return new WaitForSeconds(waitOut);

            // Setup / move drone (appel attendu par le circuit)
            CircuitStep_Setup.TriggerMoveDrone();

            // Petite pause
            yield return new WaitForSeconds(0.5f);

            // Fade in
            if (animator_canvas && animFadeIn_canvas)
                animator_canvas.Play(animFadeIn_canvas.name);

            float waitIn = animFadeIn_canvas != null ? animFadeIn_canvas.length : 1.5f;
            yield return new WaitForSeconds(waitIn);

            onCompletion?.Invoke();
            _circuitCo = null;
        }
    }
}