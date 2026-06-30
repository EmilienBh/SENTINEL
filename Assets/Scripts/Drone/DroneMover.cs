using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Viable.Circuit;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the drone movement
    /// This is the generic, abstract version, that implements the drone behaviour but no specific input
    /// </summary>
    public abstract class DroneMover : MonoBehaviour {

        #region Data
        protected static DroneMover Instance; // Instance of this component (singleton)

        public enum ConductMode { Classical, Drone, TiltRotor };
        static ConductMode actualMode = ConductMode.Classical;
        public void SetMode_Classical() => StaticSetMode(ConductMode.Classical); // Allows serialized call
        public void SetMode_Drone() => StaticSetMode(ConductMode.Drone); // Allows serialized call
        public void SetMode_TiltRotor() => StaticSetMode(ConductMode.TiltRotor); // Allows serialized call
        public static void StaticSetMode(ConductMode newMode) { // On mode update, also reset some data
            Instance.CancelDestination(); // Stop autopilot if enabled
            actualMode = newMode;
            Instance.constant_frontInput = 0f;
            Instance.constant_verticalInput = 0f;
            Instance.constant_rotationInput = 0f;
        }

        [Space]
        [SerializeField] protected Transform mainCamera;
        [SerializeField, Tooltip("The rigidbody used for drone position (locked in orientation)")] protected Rigidbody droneRb;
        [SerializeField, Tooltip("The audio source that will play propulsor sounds")] AudioSource propulsorsAudio;
        [SerializeField, Tooltip("The transform that should be moved for drone anims")] protected Transform droneAnim;
        [SerializeField, Tooltip("The transform considered as the drone back motors placement \n(Distance to center should be the same as front motors !)")] protected Transform droneBackMotors;
        [SerializeField, Tooltip("The transform considered as the drone front motors placement \n(Distance to center should be the same as back motors !)")] protected Transform droneFrontMotors;
        [SerializeField, Tooltip("The transform considered as the drone height sensor \n(place it at the bottom of drone collider)")] protected Transform droneHeightSensor;
        [SerializeField, Tooltip("The transform to place in the South-West corner of the map \n(Y-axis will be ignored)")] protected Transform southWestCorner;
        [SerializeField, Tooltip("The transform to place in the North-East corner of the map \n(Y-axis will be ignored)")] protected Transform northEastCorner;
        [SerializeField, Tooltip("The collider used to check destination if landing area is valid")] MeshCollider landingZoneCollider;
        [SerializeField, Tooltip("The drone hitbox preview mesh, to place on raycast point")] Transform droneHitboxPreview;
        [Space, SerializeField, Tooltip("The transforms of all top propellers to rotate")] Transform[] topPropellers;
        [Space, SerializeField, Tooltip("Event called on autopilot (where assistance mode is triggered")] UnityEvent onAutopilot;

        // Drone vals
        public static bool EnableMovement = true; // If disabled, all drone inputs will be interrupted (only camera update will be done)
        protected const float frontMaxSpeed = 26f; // Max force in front direction
        protected const float frontAcceleration = 0.5f; // Acceleration force in front direction
        protected const float verticalMaxSpeed = 4f; // Max force in vertical direction
        protected const float verticalAcceleration = 0.5f; // Acceleration force in vertical direction
        protected const float rotationMaxSpeed = 5f; // Rotation maximum velocity (won't apply more force over this velocity)
        protected const float rotationAcceleration = 40f; // Rotation acceleration (force applied to rigidbody)
        protected const float properllerMaxRotaSpeed = 1080f; // The maximum propeller rotation in degrees by second
        protected float frontPropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected float verticalPropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected float rotationPropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected float strafePropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected const float propulsionTime = 3f; // Time in seconds to raise propulsion from 0 to 1
        static float height = 999f; // Drone actual height (if unknown, set to 999 since ground is far enough)
        float heightFactor = 0f; // Drone height factor, impacts the gravity to fake takeoff intertia (0 means on-ground, 1 means in-air)

        // Anim vals
        protected const float rotationPitch = 10f; // Drone anim X-rotation on pitch
        protected const float rotationBank = -5f; // Drone anim Z-rotation on bank
        float actualRotorAnimFactor = 0f; // Rotors anim factor, stored for lerp
        float minAudioPitch = 0.5f; // Propulsors audio min pitch
        float maxAudioPitch = 1.4f; // Propulsors audio max pitch
        float minAudioVolume = 0f; // Propulsors audio min volume
        float maxAudioVolume = 0.5f; // Propulsors audio max volume
        bool resetPitchAnim = false; // Set to true during an update to reset the X-rotation (pitch) of the drone, based on actual acceleration

        // Battery related data
        float rotorsUsageFactor = 0f; // Represents the usage percentage (0f-1f) of the rotors. Used for animation and battery usage
        float actualBatteryPercentage = 1f; // The actual battery remaining autonomy, between 0 and 1
        const float batteryRechargeDuration = 10f; // The time needed to fully recharge battery, from 0% to 100%
        const float maxBatteryDepletionRate = 0.005f; // The battery depletion rate, when battery usage is at 100% (represented in percentage per second)
        const float emergencyLandingCeil = 0.4f; // Battery percentage under which emergency landing is triggered
        const float lowBatteryCeil = 0.6f; // Battery percentage under which low battery notification is triggered
        bool emergencyLanding = false; // If true, battery is low and emergency landing has been triggered. Disables the manual commands
        bool lowBattery = false; // If true, battery is low and emergency landing has been triggered. Disables the manual commands
        public static Action EmergencyLanding_Start;
        public static Action EmergencyLanding_End;
        public static Action LowBattery_Start;
        public static Action LowBattery_End;
        protected bool isCircuitInProgress; // Updated when circuit is started or stopped, will cancel the battery functions for the duration of the circuit

        // Input vals
        protected float frontInput; // The acceleration input (0 = nothing, 1 = full front, 2 = full back)
        protected float verticalInput; // The vertical acceleration input (0 = nothing, 1 = full up, 2 = full down)
        protected float rotationInput; // The rotation input (0 = nothing, 1 = full right, 2 = full left)
        protected float strafeInput; // The strafe (lateral force) input (0 = nothing, 1 = full right, 2 = full left)
        float constant_frontInput = 0f; // Used by constant value conduct modes, to have a fix, incrementable input, decorrelated from controllers
        float constant_verticalInput = 0f; // Used by constant value conduct modes, to have a fix, incrementable input, decorrelated from controllers
        float constant_rotationInput = 0f; // Used by constant value conduct modes, to have a fix, incrementable input, decorrelated from controllers
        int numerical_previousFrontInput = 0; // Previous numerical value, used to determine numerical increment
        int numerical_previousVerticalInput = 0; // Previous numerical value, used to determine numerical increment
        const float numerical_increment = 0.25f; // Value to add/remove on numerical udpate
        const float linear_zeroCeil = 0.15f; // If the value of an input is lower than ceil, consider this input is released (see refs to this variable for concrete applications)


        // Experimental assistance modes for SAGAS protocol
        public enum AutomationMode { FullManual, SemiAssisted, Assisted }

        [Space, Header("SAGAS automation")]
        [SerializeField] AutomationMode automationMode = AutomationMode.FullManual;
        [SerializeField, Range(0f, 1f)] float semiAssistanceStrength = 0.45f;
        [SerializeField] float targetAngleMax = 30f;
        [SerializeField] float waypointTolerance = 2.5f;
        [SerializeField] float preLandingHeightAboveCheckpoint = 10f;
        [SerializeField, Tooltip("Mandatory safety height above PreLanding/Landing point before any landing descent starts. This is measured from the drone height sensor, not from the Rigidbody center.")]
        float landingSafetyHeightAboveCheckpoint = 10.5f;
        [SerializeField, Tooltip("Direct vertical speed used during the final assisted landing descent, in meters per second.")]
        float landingDescentSpeed = 2.0f;
        [SerializeField] float landingHeightAboveCheckpoint = 0.5f;
        [SerializeField, Tooltip("Distance before a checkpoint at which the assisted pilot starts matching the checkpoint altitude.")]
        float checkpointAltitudeAlignmentDistance = 15f;
        [SerializeField, Tooltip("Target cruise speed on Destination checkpoints, in km/h.")]
        float destinationCruiseSpeedKmh = 70f;
        [SerializeField, Tooltip("Horizontal distance under which the landing step is considered centered above the landing point.")]
        float landingHorizontalTolerance = 1.25f;
        [SerializeField, Tooltip("Height above the landing point under which the landing assistance stops pushing down.")]
        float landedWorldAltitudeTolerance = 0.35f;

        [Header("Semi-assisted corridor")]
        [SerializeField, Tooltip("Horizontal distance from the ideal segment where no trajectory assistance is applied.")]
        float semiCorridorFreeRadius = 3f;
        [SerializeField, Tooltip("Horizontal distance from the ideal segment where the trajectory assistance reaches the Semi Assistance Strength value.")]
        float semiCorridorMaxRadius = 12f;
        [SerializeField, Tooltip("Point ahead on the ideal segment used as the correction target when the drone leaves the corridor.")]
        float semiCorridorLookAhead = 12f;
        [SerializeField, Tooltip("Maximum lateral correction injected by the semi-assisted corridor.")]
        float semiMaxStrafeCorrection = 0.75f;
        [SerializeField, Tooltip("Minimum player command magnitude required before speed assistance can push the drone forward.")]
        float semiPlayerMoveDeadzone = 0.15f;

        CircuitStep lastAssistedStep = null;
        float stepEntryWorldAltitude = 0f;
        float stepEntryHorizontalDistance = 0f;
        Vector3 stepEntryTargetPosition = Vector3.zero;
        bool hasStepEntryReference = false;
        Vector3 lastPreLandingWorldPosition = Vector3.zero;

        // Properties
        float FrontSpeed => Vector3.Dot(droneRb.velocity, droneRb.transform.forward); // Frontal speed is velocity in front direction
        float VerticalSpeed => Vector3.Dot(droneRb.velocity, droneRb.transform.up); // Vertical speed is velocity in up direction
        bool IsInputCeiled(float testedInput) => testedInput < linear_zeroCeil && testedInput > -linear_zeroCeil; //Returns wether or not given input is under zero-ceil (meaning it can be considered as stopped, set to 0)
        bool IsHoverMode => FrontSpeed * 3.6f < 60f; // Allows to know if evtol is considered in hover mode (e.g. no or few front speed), which differenciate stationary and flight conduct modes (modifies the rotation behaviour)

        // Actual inputs based on mode
        float ActualFrontInput => IsConstantMode ? constant_frontInput / 4f + 0.75f : frontInput; // In constant mode (which is always positive), lerp the input to be able to decelerate
        float ActualVerticalInput => (actualMode == ConductMode.TiltRotor) ? constant_verticalInput : verticalInput; // Hard-pass condition for tilt-rotor mode (because one of the inputs is always constant)
        float ActualRotationInput => IsConstantMode ? constant_rotationInput : rotationInput;
        bool IsConstantMode => actualMode == ConductMode.TiltRotor && !IsHoverMode;

        #endregion


        #region Inputs

        /// <summary>
        /// Function designed to turn raw inputs into drone inputs. (example : "left stick vertical axis" => "vertical speed input")
        /// <br/>Raw input format is based on VR commands. Drone inputs are based on actual modes.
        /// </summary>
        protected void HandleInputs(Vector2 RawInput_Left, Vector2 RawInput_Right) {
            /*
             * Remove parasite inputs
             */
            if (!RemoveParasiteInput(RawInput_Left.x, ref RawInput_Left.y)) { RemoveParasiteInput(RawInput_Left.y, ref RawInput_Left.x); }
            if (!RemoveParasiteInput(RawInput_Right.x, ref RawInput_Right.y)) { RemoveParasiteInput(RawInput_Right.y, ref RawInput_Right.x); }

            /*
             * Ceil high value to magnitude
             */
            CeilInputToMagnitude(ref RawInput_Left);
            CeilInputToMagnitude(ref RawInput_Right);

            /*
             * Map inputs depending on mode
             */
            switch (actualMode) {
                case ConductMode.Classical:
                    strafeInput = 0f;
                    frontInput = RawInput_Left.y;
                    rotationInput = RawInput_Right.x;
                    verticalInput = -RawInput_Right.y;
                    break;
                case ConductMode.Drone:
                    strafeInput = RawInput_Left.x;
                    frontInput = RawInput_Left.y;
                    rotationInput = RawInput_Right.x;
                    verticalInput = RawInput_Right.y;
                    break;
                case ConductMode.TiltRotor:
                    strafeInput = RawInput_Left.x;
                    frontInput = RawInput_Left.y;
                    rotationInput = RawInput_Right.x;
                    verticalInput = -RawInput_Right.y;
                    break;
            }
        }

        /// <summary>
        /// Function called to remove input where axis value is low, and other axis of the same stick is high, to avoid "parasite" inputs
        /// </summary>
        /// <returns>Wether or not parasite input was removed</returns>
        bool RemoveParasiteInput(float input1, ref float input2) {
            if (Mathf.Abs(input1) < 0.5f) { return false; }
            if (Mathf.Abs(input2) > 0.25f) { return false; }
            input2 = 0f;
            return true;
        }

        /// <summary>
        /// Function called to ceil high value to magnitude : if stick is fully pushed, then higher axis (horizontal/vertial) should be considered at a value of 1, etc.
        /// </summary>
        void CeilInputToMagnitude(ref Vector2 input) {
            float mag = Mathf.Clamp01(input.magnitude); // Get input magnitude
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y)) { // Get higher axis and assign magnitude to it
                input.x = input.x > 0f ? mag : -mag;
            }
            else {
                input.y = input.y > 0f ? mag : -mag;
            }
        }

        /// <summary>
        /// Function called to update a constant input mode value
        /// </summary>
        /// <param name="numerical_input">Numerical input data to update with this call</param>
        /// <param name="numerical_previousInput">Previous value for this numerical input</param>
        /// <param name="actualInput">Player input to use</param>
        void UpdateConstantInput_Numerical(ref float numerical_input, ref int numerical_previousInput, float actualInput) {
            switch (numerical_previousInput) {
                case 0: // Detect if a numerical input is triggered
                    if (actualInput > 0.5f) {
                        numerical_previousInput = 1;
                        if (numerical_input < 1f) { numerical_input += numerical_increment; }
                    }
                    else if (actualInput < -0.5f) {
                        numerical_previousInput = -1;
                        if (numerical_input > -1f) { numerical_input -= numerical_increment; }
                    }
                    break;
                case 1: // Detect if positive input is released
                    if (actualInput < 0.25f) {
                        numerical_previousInput = 0;
                    }
                    break;

                case -1: // Detect if negative input is released
                    if (actualInput > -0.25f) {
                        numerical_previousInput = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// Function called to update the constant inputs in linear mode
        /// </summary>
        void UpdateConstantInput_Linear() {
            if (actualMode == ConductMode.TiltRotor) { // Tilt-rotor opens this condition too (because of permanent constant vertical input)

                const float constant_maxSpeed = 1.1f; // Constant value change speed in units by seconds
                float deltaSpeed = constant_maxSpeed * Time.deltaTime; // Max value change for this frame, scaled by input

                UpdateConstantInput(verticalInput, ref constant_verticalInput, deltaSpeed);

                if (IsConstantMode) { // Front input is only constant when true Constant Mode is on
                    UpdateConstantInput(frontInput, ref constant_frontInput, deltaSpeed);
                    UpdateConstantInput(rotationInput, ref constant_rotationInput, deltaSpeed);
                }
                else { // When not in true Constant Mode, reset front input (used as start value for future mode switch)
                    constant_frontInput = 1f;
                    constant_rotationInput = 0f;
                }

                /*
                 * When eVTOL is grounded, remove every constant input excepted positive vertical
                 */
                if (droneRb.useGravity) {
                    if (constant_frontInput != 0f) { constant_frontInput = 0f; }
                    if (constant_verticalInput < 0f) { constant_verticalInput = 0f; }
                }
            }
            else { // Reset constant inputs when not in constant mode
                constant_frontInput = 1f;
                constant_verticalInput = 0f;
                constant_rotationInput = 0f;
            }
        }

        void UpdateConstantInput(float actualinput, ref float constInput, float deltaSpeed) {
            if (actualinput < -0.2f || actualinput > 0.2f) {
                constInput += deltaSpeed * actualinput;
                if (constInput > 1f) { constInput = 1f; } // Clamp positive
                else if (constInput < -1f) { constInput = -1f; } // Clamp negative
            }
            else {
                if (IsInputCeiled(constInput)) { constInput = 0f; } // Zero-ceil (when input is released, set constant value to zero if under ceil)
            }
        }

        /// <summary>
        /// Change orientation with mouse
        /// </summary>
        protected abstract void UpdateCamera();


        #endregion


        #region Common Move

        protected virtual void OnEnable() {
            /*
             * Orientation rigidbody freeze
             * (to understand why : https://discussions.unity.com/t/rigibody-constraints-do-not-work-still-moves-a-little/205580)
             */
            droneRb.centerOfMass = Vector3.zero;
            droneRb.inertiaTensorRotation = Quaternion.identity;
            Instance = this; // Assign to singleton (replace previous one on enable, for input-type switch purposes)
        }

        void FixedUpdate() {

            UpdateCamera(); // Update camera orientation

            if (!EnableMovement) return; // Skip all movement if disabled

            /*
             * Update drone inputs before any movement
             */
            if (autopilotActive) {
                AutopilotInputs();
            }
            else {
                UpdateInputs();
                ApplyExperimentalAutomation();
                if (actualMode == ConductMode.TiltRotor && !IsHoverMode) { strafeInput = 0f; } // Disable strafe input for Tilt-Rotor mode, if not in hover mode
                UpdateConstantInput_Linear();
            }

            /*
             * Main logic of drone forces application, based on modes and inputs
             */
            UpdatePropulsion(); // Update propulsion values, based on inputs, used for move
            UpdateHeight(); // Update height-related data

            UpdateElevation();
            UpdateAcceleration();
            UpdateRotation();
            UpdateStrafe();

            UpdateBattery();
            UpdateAnims();

            /*
             * UI data calls
             */
            UIInputStatusManager.Instance?.Update_Elevation(ActualVerticalInput);
            UIInputStatusManager.Instance?.Update_MoveIndicators(IsConstantMode ? constant_frontInput : frontInput); // For display, don't use the lerped constant input of the ActualFrontInput property
            UIInputStatusManager.Instance?.Update_RotationIndicators(ActualRotationInput);
            UIInputStatusManager.Instance?.Update_IsDroneStopped(IsHoverMode);
        }

        /// <summary>
        /// Update player inputs status
        /// </summary>
        protected abstract void UpdateInputs();

        /// <summary>
        /// Update propulsion values : propulsion is based on how long an input stays pressed, and mitigates the movement acceleration
        /// </summary>
        void UpdatePropulsion() {
            float propulsionDelta = Time.fixedDeltaTime / propulsionTime; // The maximum propulsion value change for this frame

            frontPropulsion = UpdatePropulsion(frontPropulsion, ActualFrontInput, propulsionDelta);
            verticalPropulsion = UpdatePropulsion(verticalPropulsion, ActualVerticalInput, propulsionDelta);
            rotationPropulsion = UpdatePropulsion(rotationPropulsion, ActualRotationInput, propulsionDelta);
            strafePropulsion = UpdatePropulsion(strafePropulsion, strafeInput, propulsionDelta);
        }

        /// <summary>
        /// Function used to update one of the propulsion values, using bounds to not exceed target
        /// </summary>
        /// <param name="actualPropulsion">The actual propulsion value to update</param>
        /// <param name="targetPropulsion">The target value for this propulsion (should be the value of input)</param>
        /// <param name="propulsionDelta">The maximum value change for this frame</param>
        /// <returns></returns>
        float UpdatePropulsion(float actualPropulsion, float targetPropulsion, float propulsionDelta) {
            if (actualPropulsion * targetPropulsion < 0f) { return 0f; } // If input direction is changed (e.g. from front to back), clear previous velocity
            if (targetPropulsion > actualPropulsion) {
                return Mathf.Min(actualPropulsion + propulsionDelta, targetPropulsion);
            }
            else {
                return Mathf.Max(actualPropulsion - propulsionDelta, targetPropulsion);
            }
        }

        /// <summary>
        /// Function called by UpdateForces to handle the height-related data (actual height, gravity, anims)
        /// </summary>
        public void UpdateHeight() {
            RaycastHit hit;
            Ray downRay = new Ray(droneHeightSensor.transform.position, -Vector3.up);
            if (Physics.Raycast(downRay, out hit, 1000f, GameLayers.Combined_DefaultAndBuildings)) {
                droneHitboxPreview.position = hit.point; // Update hitbox preview position to match ray point
                droneHitboxPreview.localEulerAngles = new Vector3(0f, droneRb.transform.localEulerAngles.y, 0f); // Also update hitbox preview rotation based on actual drone rotation
                height = hit.distance;
            }
            else {
                height = 999f;
            }

            heightFactor = Mathf.Min(height / 3f, 1f); // height factor is 0-1 (1 is max values related to height)

            /*
             * Update mass and gravity : Gravity is only enabled up to 3m high, and mass is lowered by distance and propulsion to feel smooth
             */
            float propulsionFactor = Mathf.Max(verticalPropulsion * 1.3f, 1f); // Add a margin to max propulsion (something like 0.7 is "max" value)
            droneRb.mass = Mathf.Lerp(4f, 0.01f, Mathf.Max(heightFactor, propulsionFactor));
            droneRb.useGravity = heightFactor < 0.9f;
        }

        /// <summary>
        /// Function called by UpdateForces to update the vertical force (elevation)
        /// </summary>
        public void UpdateElevation() {
            if (emergencyLanding && !autopilotActive) { return; } // Small fix to prevent user to take-off immedialty after an emergency landing, until batteries are recharged to "low" state
            float elevationSpeed = Vector3.Dot(droneRb.velocity, droneRb.transform.up); // Elevation speed is velocity in up direction
            if (ActualVerticalInput != 0 && (Mathf.Abs(elevationSpeed) < verticalMaxSpeed || elevationSpeed * ActualVerticalInput < 0)) { // Don't add new acceleration force if actual speed is too high (opposed force is always valid)
                droneRb.AddForce(Mathf.Abs(verticalPropulsion) * ActualVerticalInput * verticalAcceleration * droneRb.transform.up, ForceMode.Force); // Set the force
            }
        }


        /// <summary>
        /// Function called by UpdateForces to update the front force (acceleration)
        /// </summary>
        public void UpdateAcceleration() {
            resetPitchAnim = true; // If max speed reached or near ground, anim orientation should reset
            if (droneRb.useGravity) { return; } // Ignore non-vertical move if grounded
            if (IsConstantMode) { resetPitchAnim = false; } // If drone is in constant mode, and not grounded, always update pitch anim
            float backFactor = ActualFrontInput > 0 ? 1f : -0.5f; // Divide acceleration and max speed when moving backward, and change direction
            if (ActualFrontInput != 0 && (Mathf.Abs(FrontSpeed / backFactor) < frontMaxSpeed || FrontSpeed * ActualFrontInput < 0)) { // Don't add new acceleration force if actual speed is too high (opposed force is always valid)
                droneRb.AddForce(Mathf.Abs(frontPropulsion) * ActualFrontInput * frontAcceleration * droneRb.transform.forward, ForceMode.Force); // Set the force
                if (height > 1f && FrontSpeed < frontMaxSpeed * 0.9f) { // Update the drone pitch anim, since frontal acceleration uses some pitch
                    resetPitchAnim = false;
                }
            }
        }


        /// <summary>
        /// Function called by UpdateForces to update the rotation forces
        /// </summary>
        public void UpdateRotation() => ApplyLateralForces(false, ActualRotationInput, rotationPropulsion);

        /// <summary>
        /// Function called by UpdateForces to update the strafe forces (lateral movement)
        /// </summary>
        public void UpdateStrafe() => ApplyLateralForces(true, strafeInput, strafePropulsion);

        /// <summary>
        /// Function called both by UpdateRotation and UpdateStrafe.
        /// <br/>Depending on which one calls it, it will apply lateral forces in identical or opposed direction, to rotate or strafe.
        /// </summary>
        public void ApplyLateralForces(bool isStrafe, float lateralInput, float lateralPropulsion) {
            if (lateralInput != 0f && !droneRb.useGravity) { // Ignore lateral forces if no input or grounded (Still needs to update anim)
                bool rotationSpeedUnderCap = Mathf.Abs(droneRb.angularVelocity.y) < rotationMaxSpeed || lateralInput * droneRb.angularVelocity.y < 0; // Rotation is considered under cap based on its actual velocity, but turning in the opposite direction of the actual rotation is always valid
                if (isStrafe || rotationSpeedUnderCap) {
                    int forceDir = isStrafe ? 1 : -1; // One of the two forces will be applied this factor : if strafing, both forces go the same direction to push lateraly ; else, forces are opposed and create a rotation
                    float forceFactor = isStrafe ? 0.05f : 1f; // If both forces go the same direction (lateral move is applied), drasticlally reduce them
                    droneRb.AddForceAtPosition(Mathf.Abs(lateralPropulsion) * lateralInput * rotationAcceleration * droneRb.transform.right * forceFactor * forceDir / 2f, droneBackMotors.position, ForceMode.Force); // Applies lateral force from the back motors (motors must be BACK-side, that's why we use left vector)
                    droneRb.AddForceAtPosition(Mathf.Abs(lateralPropulsion) * lateralInput * rotationAcceleration * droneRb.transform.right * forceFactor / 2f, droneFrontMotors.position, ForceMode.Force); // Applies lateral force from the back motors (placed front, opposed to back motors, so use right vector)
                }
            }
        }


        /// <summary>
        /// Update anim related data
        /// </summary>
        void UpdateAnims() {
            /*
             * Rotors anim, depending of the mode
             */
            float deltaRota = 0f;
            if (IsConstantMode) {
                rotorsUsageFactor = 0f; // Disable vertical rotors in constant mode (since ship doesn't need them to fly)
            }
            else if (heightFactor == 1f) {
                rotorsUsageFactor = Mathf.Lerp(0.4f, 0.9f, (verticalPropulsion + 1f) / 2f); // Clamp V-propulsion to 1=>0 : 0.5 is stationary, 0 goes down, 1 goes up
            }
            else {
                rotorsUsageFactor = Mathf.Lerp(0f, 0.9f, verticalPropulsion); // If landed, only care for positive propulsion
            }
            actualRotorAnimFactor = Mathf.Lerp(actualRotorAnimFactor, rotorsUsageFactor, Time.deltaTime);
            deltaRota = actualRotorAnimFactor * properllerMaxRotaSpeed * Time.deltaTime;

            foreach (var propeller in topPropellers) {
                if (propeller != null) { propeller.localEulerAngles = new Vector3(0f, propeller.localEulerAngles.y + deltaRota, 0f); }
            }

            /*
             * Sounds based on rotors
             */
            float audioFactor = IsConstantMode ? Mathf.Max(0.3f, actualRotorAnimFactor) : actualRotorAnimFactor; // In constant mode, still apply some sound because of wind
            propulsorsAudio.pitch = Mathf.Lerp(minAudioPitch, maxAudioPitch, Mathf.Abs(audioFactor));
            propulsorsAudio.volume = Mathf.Lerp(minAudioVolume, maxAudioVolume, Mathf.Abs(audioFactor));


            float droneAnimFactor = IsConstantMode ? 3f : 1f; // Tilt-rotor aviation mode is a fake "airplane" conduct mode, increase anim max angles

            /*
             * Drone Pitch (X) rotation
             */
            if (resetPitchAnim) { // Update the drone anim Z-rotation to zero (affected by speed cap : once max speed is reached, pitch isn't required to move forward)
                droneAnim.localEulerAngles = new Vector3(Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.x), 0f, Time.fixedDeltaTime / 2f), droneAnim.localEulerAngles.y, droneAnim.localEulerAngles.z);
            }
            else {
                float pitchInput = IsConstantMode ? -ActualVerticalInput * 0.5f : ActualFrontInput;
                droneAnim.localEulerAngles = new Vector3(Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.x), pitchInput * rotationPitch * droneAnimFactor, Time.fixedDeltaTime), droneAnim.localEulerAngles.y, droneAnim.localEulerAngles.z); // Reset rotation is slower
            }

            /*
             * Drone Roll (Y) axis
             */
            if (height > 1f) { // Update the drone anim Z-rotation (not affected by speed cap : while drone turns, it banks)
                droneAnim.localEulerAngles = new Vector3(droneAnim.localEulerAngles.x, droneAnim.localEulerAngles.y, Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.z), ActualRotationInput * rotationBank * droneAnimFactor, Time.fixedDeltaTime));
            }
            else { // Reset anim Z-rotation if near ground
                droneAnim.localEulerAngles = new Vector3(droneAnim.localEulerAngles.x, droneAnim.localEulerAngles.y, Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.z), 0f, Time.fixedDeltaTime));
            }
        }

        /// <summary>
        /// Update battery autonomy based on actual propulsion, for the battery simulation feature 
        /// </summary>
        void UpdateBattery() {
            if (isCircuitInProgress) { return; } // Battery update is disabled during a circuit

            /*
             * Landed battery recharging
             */
            if (!IsConstantMode && rotorsUsageFactor == 0f && ActualFrontInput == 0f && actualBatteryPercentage < 1f) { // Determine if drone is stopped on ground
                actualBatteryPercentage += Time.deltaTime / batteryRechargeDuration;
                if (actualBatteryPercentage > 1f) { actualBatteryPercentage = 1f; } // Clamp to 100%
                if (lowBattery && actualBatteryPercentage > lowBatteryCeil) {
                    lowBattery = false;
                    LowBattery_End?.Invoke();
                }
                else if (emergencyLanding && actualBatteryPercentage > emergencyLandingCeil) { // Disable emergency landing if battery gets over ceil
                    emergencyLanding = false;
                    EmergencyLanding_End?.Invoke();
                }
                return; // Since we are landed and not using rotors, nothing else is relevant to do after this
            }

            if (actualBatteryPercentage == 0f) { return; } // Ignore the battery update if evtol is landed, or if battery is out of power

            /*
             * Get the "number" of rotors fully used (depending of total rotors and actual propulsion)
             */
            float maxVerticalRotors = 8f;
            float nbVerticalRotors = 0f;
            if (IsConstantMode) { nbVerticalRotors = 0f; }
            else if (heightFactor == 1f) { nbVerticalRotors = Mathf.Lerp(0.33f, 1f, (verticalPropulsion + 1f) / 2f) * maxVerticalRotors; } // Clamp vertical propulsion (-1;1) to mid-air propulsor usage (1/3;1)
            else if (verticalPropulsion > 0f) { nbVerticalRotors = verticalPropulsion * maxVerticalRotors; } // During take-off, just take the vertical propulsion as a direct propulsor usage
            float nbFrontRotors = Mathf.Abs(frontPropulsion) * 3f; // Though there are only 2 back rotors, consider pitch causes a power increase (also because otherwise difference on depletion is invisible)
            float nbRotors = nbVerticalRotors + nbFrontRotors;
            if (nbRotors == 0f) { return; } // In some cases, nbRotors value can be 0 without trigger the first return check, causing a bug

            /*
             * Battery depletion
             */
            float batteryDepletionRate = maxBatteryDepletionRate * nbRotors / 10f; // The actual depletion rate of the battery, in percentage per second
            float batteryUsage = (batteryDepletionRate) / (Mathf.Pow(actualBatteryPercentage, 1.1f)); // Simplified Peukert formula based on EdC 1.067 (see EDC for original). The pow exponent is a Peukert exposant, generally between 1.1 and 1.3
            actualBatteryPercentage -= Mathf.Min(actualBatteryPercentage, batteryUsage * Time.deltaTime);

            /*
             * Out-of-battery verification
             */
            if (!lowBattery && actualBatteryPercentage < lowBatteryCeil) {
                lowBattery = true;
                LowBattery_Start?.Invoke();
            }
            else if (!emergencyLanding && actualBatteryPercentage < emergencyLandingCeil) { // Enable emergency landing if battery gets under ceil
                emergencyLanding = true;
                EmergencyLanding_Start?.Invoke();
                var actualCoords = WorlPositionToMapRatio(droneRb.transform.position + droneRb.transform.forward * 50f); // Get get the position in front of the drone, as map coordinates
                RequestDestination_MapPos(actualCoords.x, actualCoords.y, true); // On emergency landing start, trigger an autopilot request at actual position (land the closest possible)
            }
        }


        #endregion


        #region SAGAS Experimental Automation

        public static void SetAutomationMode(AutomationMode mode) {
            if (Instance == null) { return; }
            Instance.automationMode = mode;

            // If the old map autopilot was active, stop it when selecting an experimental mode manually.
            // The SAGAS circuit automation below is independent from this legacy autopilot.
            if (mode != AutomationMode.Assisted && Instance.autopilotActive) {
                Instance.CancelDestination();
            }
        }

        // ---------------------------------------------------------------------
        // Unity UI / VR button calls
        // ---------------------------------------------------------------------
        // These parameterless methods are meant to be assigned directly in
        // Button > OnClick() from a World Space Canvas in the headset.

        public void SetPilotMode_Manual() {
            automationMode = AutomationMode.FullManual;
            if (autopilotActive) { CancelDestination(); }
        }

        public void SetPilotMode_SemiAssisted() {
            automationMode = AutomationMode.SemiAssisted;
            if (autopilotActive) { CancelDestination(); }
        }

        public void SetPilotMode_Assisted() {
            automationMode = AutomationMode.Assisted;
        }

        public void SetControlMode_Classical() {
            StaticSetMode(ConductMode.Classical);
        }

        public void SetControlMode_Drone() {
            StaticSetMode(ConductMode.Drone);
        }

        public void SetControlMode_TiltRotor() {
            StaticSetMode(ConductMode.TiltRotor);
        }

        public AutomationMode CurrentAutomationMode => automationMode;
        public ConductMode CurrentConductMode => actualMode;


        void ApplyExperimentalAutomation() {
            if (!isCircuitInProgress || autopilotActive) { return; }

            if (!CircuitManager.TryGetCurrentStep(out CircuitStep step)) { return; }

            if (step != lastAssistedStep) {
                lastAssistedStep = step;
                stepEntryWorldAltitude = droneRb.transform.position.y;
                stepEntryHorizontalDistance = 0f;
                stepEntryTargetPosition = Vector3.zero;
                hasStepEntryReference = false;

                // The Landing step can be serialized as (0,0,0) because it inherits the previous PreLanding point.
                // Keep the last pre-landing world position so the drone does not fly away from the landing zone.
                if (step.Type == StepType.PreLanding) {
                    lastPreLandingWorldPosition = step.StepPosition;
                }
            }

            switch (automationMode) {
                case AutomationMode.FullManual:
                    return;

                case AutomationMode.SemiAssisted:
                    ApplySemiAssisted(step);
                    return;

                case AutomationMode.Assisted:
                    ApplyAssisted(step);
                    return;
            }
        }

        void ApplySemiAssisted(CircuitStep step) {
            // The semi-assisted mode is intentionally NOT a permanent blend with the autopilot.
            // The player keeps full control inside a corridor around the ideal segment.
            // The farther the drone leaves that corridor, the stronger the corrective help becomes.

            switch (step.Type) {
                case StepType.Setup:
                    return;

                case StepType.TakeOff:
                    // Do not auto take-off in semi-assisted mode: the player must initiate the climb.
                    return;

                case StepType.PreLanding:
                    // Semi-assisted mode must NOT fly the pre-landing step alone.
                    // The player keeps control; the system only prevents unsafe low altitude
                    // and gently helps to stay around the final approach point.
                    ApplySemiPreLandingAssist(step);
                    return;

                case StepType.Landing:
                    // Semi-assisted mode must NOT perform the automatic final descent.
                    // The player must descend/land manually. We only keep a light horizontal help
                    // and avoid dragging the drone down by ourselves.
                    ApplySemiLandingAssist(step);
                    return;
            }

            Vector3 target = ResolveStepTarget(step);
            if (target == Vector3.zero) { return; }

            Vector3 dronePos = droneRb.position;
            float horizontalDistanceToTarget = HorizontalDistance(dronePos, target);
            EnsureStepEntryReference(target, horizontalDistanceToTarget);

            Vector3 segmentStart = new Vector3(stepEntryTargetPosition.x, 0f, stepEntryTargetPosition.z);
            if (segmentStart == Vector3.zero) {
                segmentStart = new Vector3(dronePos.x, 0f, dronePos.z);
            }

            // stepEntryTargetPosition stores the current target. The segment start is the drone position captured
            // when the step began. This makes the corridor independent from the previous circuit internals.
            Vector3 start = new Vector3(stepEntryStartPosition.x, 0f, stepEntryStartPosition.z);
            Vector3 end = new Vector3(target.x, 0f, target.z);
            Vector3 pos = new Vector3(dronePos.x, 0f, dronePos.z);
            Vector3 segment = end - start;

            if (segment.sqrMagnitude < 0.01f) {
                return;
            }

            Vector3 segmentDir = segment.normalized;
            float segmentLength = segment.magnitude;
            float progress = Mathf.Clamp01(Vector3.Dot(pos - start, segmentDir) / segmentLength);
            Vector3 closestPoint = start + segmentDir * (progress * segmentLength);
            float lateralError = Vector3.Distance(pos, closestPoint);

            float corridorAssist = ComputeSmoothAssist(lateralError, semiCorridorFreeRadius, semiCorridorMaxRadius) * semiAssistanceStrength;

            if (corridorAssist > 0.001f) {
                // Aim at a point slightly ahead on the ideal segment, not directly at the checkpoint.
                // This creates a visible "rail/corridor" behaviour instead of a weak target-facing correction.
                Vector3 aheadPoint = start + segmentDir * Mathf.Clamp(progress * segmentLength + semiCorridorLookAhead, 0f, segmentLength);
                Vector3 correctionDirection = aheadPoint - pos;

                if (correctionDirection.sqrMagnitude > 0.1f) {
                    float angle = Vector3.SignedAngle(droneRb.transform.forward, correctionDirection.normalized, Vector3.up);
                    float rotationCorrection = Mathf.Clamp(angle / targetAngleMax, -1f, 1f);
                    rotationInput = Mathf.Lerp(rotationInput, rotationCorrection, corridorAssist);
                }

                // Add a true lateral correction. Without this, the drone may only rotate a bit and the player
                // does not feel any corridor assistance.
                Vector3 toCorridor = closestPoint - pos;
                if (toCorridor.sqrMagnitude > 0.01f) {
                    float signedRightCorrection = Vector3.Dot(toCorridor.normalized, droneRb.transform.right);
                    float strafeCorrection = Mathf.Clamp(signedRightCorrection * semiMaxStrafeCorrection, -semiMaxStrafeCorrection, semiMaxStrafeCorrection);
                    strafeInput = Mathf.Lerp(strafeInput, strafeCorrection, corridorAssist);
                }
            }

            ApplySemiAltitudeAssist(step, target, horizontalDistanceToTarget);
            ApplySemiSpeedGuard(horizontalDistanceToTarget, corridorAssist);
        }

        Vector3 stepEntryStartPosition = Vector3.zero;

        Vector3 ResolveStepTarget(CircuitStep step) {
            if (step.Type == StepType.Landing) {
                return ResolveLandingTarget(step);
            }

            Vector3 objective = step.GetObjectivePosition();
            if (objective != Vector3.zero) { return objective; }
            return step.StepPosition;
        }

        float ComputeSmoothAssist(float value, float freeValue, float maxValue) {
            if (maxValue <= freeValue) { return value > freeValue ? 1f : 0f; }
            float t = Mathf.InverseLerp(freeValue, maxValue, value);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        void ApplySemiAltitudeAssist(CircuitStep step, Vector3 target, float horizontalDistanceToTarget) {
            if (step.Type != StepType.Destination && step.Type != StepType.Detour && step.Type != StepType.Wind) {
                return;
            }

            // Altitude help must be local to the checkpoint. It must not drag the drone down early,
            // otherwise a low checkpoint behind buildings can make the drone land on rooftops.
            float proximityAssist = ComputeSmoothAssist(30f - horizontalDistanceToTarget, 0f, 25f);
            float altitudeAssist = proximityAssist * semiAssistanceStrength;

            float altitudeError = target.y - droneRb.position.y;

            // If the checkpoint is much lower, do not force a descent while still far from it.
            if (altitudeError < 0f && horizontalDistanceToTarget > checkpointAltitudeAlignmentDistance) {
                altitudeAssist *= 0.15f;
            }

            if (altitudeAssist <= 0.001f) { return; }

            float altitudeCorrection = Mathf.Clamp(altitudeError / 6f, -1f, 1f);
            verticalInput = Mathf.Lerp(verticalInput, altitudeCorrection, altitudeAssist);
        }

        void ApplySemiSpeedGuard(float horizontalDistanceToTarget, float corridorAssist) {
            // Semi-assisted must not fly away by itself when the player releases the stick.
            // It only prevents absurdly low speed when the player is actually trying to move,
            // or when the drone is far outside the corridor.
            bool playerWantsToMove = Mathf.Abs(frontInput) > semiPlayerMoveDeadzone;
            bool outsideCorridor = corridorAssist > 0.25f;
            if (!playerWantsToMove && !outsideCorridor) { return; }

            float targetSpeed = destinationCruiseSpeedKmh / 3.6f;
            float autoFront = ComputeCruiseFrontInput(targetSpeed, horizontalDistanceToTarget, 1f);
            float speedAssist = Mathf.Clamp01(Mathf.Max(corridorAssist, semiAssistanceStrength * 0.35f));
            frontInput = Mathf.Lerp(frontInput, autoFront, speedAssist);
        }

        void ApplySemiPreLandingAssist(CircuitStep step) {
            Vector3 preLandingPoint = step.StepPosition;
            if (preLandingPoint == Vector3.zero) { return; }

            // Store it for the following Landing step, because Landing may be serialized as (0,0,0).
            lastPreLandingWorldPosition = preLandingPoint;

            Vector3 dronePos = droneRb.position;
            float horizontalDistance = HorizontalDistance(dronePos, preLandingPoint);
            ApplySemiPointHorizontalAssist(preLandingPoint, horizontalDistance);

            // Safety only: if the player arrives too low, the assist lifts the drone back to 10.5 m.
            // If the player is already above this height, we do NOT pull the drone down.
            float safeSensorY = preLandingPoint.y + landingSafetyHeightAboveCheckpoint;
            ForceMinimumSensorAltitude(safeSensorY);
        }

        void ApplySemiLandingAssist(CircuitStep step) {
            Vector3 landingPoint = ResolveLandingTarget(step);
            if (landingPoint == Vector3.zero) { return; }

            Vector3 dronePos = droneRb.position;
            float horizontalDistance = HorizontalDistance(dronePos, landingPoint);
            ApplySemiPointHorizontalAssist(landingPoint, horizontalDistance);

            // Important: no automatic descent in SemiAssisted mode.
            // Once the player is around/above the landing point, vertical control is manual.
            // We only keep the 10.5 m safety while the drone is still not centered, to avoid
            // arriving below the validated landing corridor.
            if (horizontalDistance > landingHorizontalTolerance) {
                float safeSensorY = landingPoint.y + landingSafetyHeightAboveCheckpoint;
                ForceMinimumSensorAltitude(safeSensorY);
            }
        }

        void ApplySemiPointHorizontalAssist(Vector3 targetPoint, float horizontalDistance) {
            if (targetPoint == Vector3.zero) { return; }

            // Assistance progressive autour du point final : libre proche du point, correction forte si on s'en écarte.
            float pointAssist = ComputeSmoothAssist(horizontalDistance, semiCorridorFreeRadius, semiCorridorMaxRadius) * semiAssistanceStrength;
            if (pointAssist <= 0.001f) { return; }

            Vector3 dronePos = droneRb.position;
            Vector3 flatDirection = new Vector3(targetPoint.x - dronePos.x, 0f, targetPoint.z - dronePos.z);
            if (flatDirection.sqrMagnitude > 0.1f) {
                float angle = Vector3.SignedAngle(droneRb.transform.forward, flatDirection.normalized, Vector3.up);
                float rotationCorrection = Mathf.Clamp(angle / targetAngleMax, -1f, 1f);
                rotationInput = Mathf.Lerp(rotationInput, rotationCorrection, pointAssist);

                float signedRightCorrection = Vector3.Dot(flatDirection.normalized, droneRb.transform.right);
                float strafeCorrection = Mathf.Clamp(signedRightCorrection * semiMaxStrafeCorrection, -semiMaxStrafeCorrection, semiMaxStrafeCorrection);
                strafeInput = Mathf.Lerp(strafeInput, strafeCorrection, pointAssist);
            }
        }

        void ApplyAssisted(CircuitStep step) {
            switch (step.Type) {
                case StepType.Setup:
                    StopAssistedInputs();
                    break;

                case StepType.TakeOff:
                    // Original circuit instruction: take off until 20 m relative height.
                    StopHorizontalAssistedInputs();
                    verticalInput = height < 20f ? 1f : 0f;
                    break;

                case StepType.Destination:
                    MoveToTarget(step.StepPosition, 1f, 1f, false, destinationCruiseSpeedKmh / 3.6f);
                    break;

                case StepType.Detour:
                    MoveToTarget(step.GetObjectivePosition(), 1f, 1f, false, destinationCruiseSpeedKmh / 3.6f);
                    break;

                case StepType.Wind:
                    MoveToTarget(step.GetObjectivePosition(), 0.6f, 0.8f, true);
                    break;

                case StepType.PreLanding:
                    MoveToPreLanding(step);
                    break;

                case StepType.Landing:
                    MoveToLanding(step);
                    break;
            }
        }

        void MoveToPreLanding(CircuitStep step) {
            Vector3 dronePos = droneRb.position;
            Vector3 preLandingPoint = step.StepPosition;
            float safeSensorY = preLandingPoint.y + landingSafetyHeightAboveCheckpoint;

            Vector3 horizontalTarget = new Vector3(preLandingPoint.x, dronePos.y, preLandingPoint.z);
            float horizontalDistance = HorizontalDistance(dronePos, horizontalTarget);

            if (horizontalDistance > waypointTolerance) {
                // Go to the PreLanding point horizontally, but never descend during this phase.
                MoveToTarget(horizontalTarget, 0.7f, 1f, true);

                // Safety is measured with the height sensor, because this is the bottom of the drone.
                ForceMinimumSensorAltitude(safeSensorY);
                return;
            }

            // Once above the PreLanding point, hold horizontally and force 10.5 m minimum.
            StopHorizontalAssistedInputs();
            ForceMinimumSensorAltitude(safeSensorY);
        }

        void MoveToLanding(CircuitStep step) {
            Vector3 dronePos = droneRb.position;
            Vector3 landingPoint = ResolveLandingTarget(step);
            float safeSensorY = landingPoint.y + landingSafetyHeightAboveCheckpoint;
            Vector3 horizontalTarget = new Vector3(landingPoint.x, dronePos.y, landingPoint.z);
            float horizontalDistance = HorizontalDistance(dronePos, horizontalTarget);

            if (horizontalDistance > landingHorizontalTolerance) {
                // Stay horizontally locked on the real landing point, not on the serialized (0,0,0) Landing step.
                // While centering, never descend. If the sensor is below the safety height, climb first.
                MoveToTarget(horizontalTarget, 0.22f, 0.5f, true, 3.5f);
                ForceMinimumSensorAltitude(safeSensorY);
                return;
            }

            StopHorizontalAssistedInputs();

            // Absolute rule: once centered above the landing point, the drone MUST first reach
            // 10.5 m above the landing point, measured from the bottom height sensor.
            // No descent command is allowed before that.
            if (ForceMinimumSensorAltitude(safeSensorY)) {
                return;
            }

            float targetSensorY = landingPoint.y + landingHeightAboveCheckpoint;

            if (GetSensorWorldY() <= targetSensorY + landedWorldAltitudeTolerance || height <= 0.7f) {
                verticalInput = 0f;
                verticalPropulsion = 0f;
                KillVerticalVelocity();
                return;
            }

            // Final descent is direct and deterministic. It does not rely on verticalInput/propulsion,
            // because those are intentionally damped by the original drone physics and made the landing
            // crawl at roughly 0.1 m every couple of seconds.
            MoveSensorTowardsWorldY(targetSensorY, Mathf.Max(0.1f, landingDescentSpeed));
            verticalInput = 0f;
            verticalPropulsion = 0f;
        }

        bool ForceMinimumSensorAltitude(float minimumSensorWorldY) {
            const float climbSpeed = 3.5f;
            const float altitudeEpsilon = 0.03f;

            if (GetSensorWorldY() >= minimumSensorWorldY - altitudeEpsilon) {
                verticalInput = 0f;
                return false;
            }

            MoveSensorTowardsWorldY(minimumSensorWorldY, climbSpeed);
            verticalInput = 0f;
            verticalPropulsion = 0f;
            return true;
        }

        void MoveSensorTowardsWorldY(float targetSensorWorldY, float speedMetersPerSecond) {
            float currentSensorY = GetSensorWorldY();
            float nextSensorY = Mathf.MoveTowards(currentSensorY, targetSensorWorldY, speedMetersPerSecond * Time.fixedDeltaTime);
            float deltaY = nextSensorY - currentSensorY;

            if (Mathf.Abs(deltaY) < 0.0001f) { return; }

            Vector3 pos = droneRb.position;
            droneRb.MovePosition(new Vector3(pos.x, pos.y + deltaY, pos.z));
            KillVerticalVelocity();
        }

        float GetSensorWorldY() {
            return droneHeightSensor != null ? droneHeightSensor.position.y : droneRb.position.y;
        }

        void KillVerticalVelocity() {
            Vector3 velocity = droneRb.velocity;
            velocity.y = 0f;
            droneRb.velocity = velocity;
        }

        Vector3 ResolveLandingTarget(CircuitStep step) {
            Vector3 objective = step.GetObjectivePosition();
            if (objective != Vector3.zero) { return objective; }

            if (step.StepPosition != Vector3.zero) { return step.StepPosition; }

            if (lastPreLandingWorldPosition != Vector3.zero) { return lastPreLandingWorldPosition; }

            return droneRb.transform.position;
        }

        void MoveToTarget(Vector3 target, float maxForwardInput, float maxVerticalInput, bool keepCurrentAltitude = false, float targetForwardSpeed = -1f) {
            if (target == Vector3.zero) {
                StopAssistedInputs();
                return;
            }

            Vector3 dronePos = droneRb.transform.position;
            if (keepCurrentAltitude) { target.y = dronePos.y; }

            Vector3 flatDirection = new Vector3(target.x - dronePos.x, 0f, target.z - dronePos.z);
            float horizontalDistance = flatDirection.magnitude;

            if (horizontalDistance > waypointTolerance) {
                float angle = Vector3.SignedAngle(droneRb.transform.forward, flatDirection.normalized, Vector3.up);
                rotationInput = Mathf.Clamp(angle / targetAngleMax, -1f, 1f);

                // Avoid flying forward when the drone is not aligned enough.
                if (Mathf.Abs(angle) > 50f) {
                    frontInput = 0f;
                }
                else if (targetForwardSpeed > 0f) {
                    frontInput = ComputeCruiseFrontInput(targetForwardSpeed, horizontalDistance, maxForwardInput);
                }
                else {
                    frontInput = maxForwardInput;
                }
            }
            else {
                frontInput = 0f;
                rotationInput = 0f;
            }

            if (keepCurrentAltitude) {
                verticalInput = 0f;
            }
            else {
                EnsureStepEntryReference(target, horizontalDistance);
                float wantedY = ComputeAlignedAltitude(stepEntryWorldAltitude, target.y, horizontalDistance, stepEntryHorizontalDistance);
                float altitudeError = wantedY - dronePos.y;
                verticalInput = Mathf.Clamp(altitudeError / 4f, -maxVerticalInput, maxVerticalInput);
            }

            strafeInput = 0f;
        }

        void EnsureStepEntryReference(Vector3 target, float horizontalDistance) {
            Vector3 flatTarget = new Vector3(target.x, 0f, target.z);
            Vector3 flatReference = new Vector3(stepEntryTargetPosition.x, 0f, stepEntryTargetPosition.z);

            if (!hasStepEntryReference || Vector3.Distance(flatTarget, flatReference) > 0.1f) {
                stepEntryWorldAltitude = droneRb.transform.position.y;
                stepEntryHorizontalDistance = Mathf.Max(horizontalDistance, 0.01f);
                stepEntryTargetPosition = target;
                stepEntryStartPosition = droneRb.position;
                hasStepEntryReference = true;
            }
        }

        float ComputeAlignedAltitude(float startY, float targetY, float horizontalDistance, float entryHorizontalDistance) {
            float alignmentFactor = 1f - Mathf.Clamp01(horizontalDistance / Mathf.Max(entryHorizontalDistance, 0.01f));
            return Mathf.Lerp(startY, targetY, alignmentFactor);
        }

        float ComputeCruiseFrontInput(float targetSpeed, float horizontalDistance, float maxForwardInput) {
            // Keep a real cruise phase around 70 km/h.
            // The previous proportional command stabilized too low (~51 km/h), because it reduced input too early.
            // Here we keep full forward command until the measured speed is close to the requested speed.
            // Then we only ease down inside the final 15 m, with a minimum approach target of 30 km/h.
            float minApproachSpeed = 30f / 3.6f;
            float slowDownDistance = 15f;

            float desiredSpeed = targetSpeed;
            if (horizontalDistance < slowDownDistance) {
                float t = Mathf.Clamp01(horizontalDistance / slowDownDistance);
                desiredSpeed = Mathf.Lerp(minApproachSpeed, targetSpeed, t);
            }

            float speedError = desiredSpeed - FrontSpeed;

            // Below the desired speed, push fully instead of using a soft proportional value.
            // This avoids the cruise plateau around 50 km/h.
            if (speedError > 0.5f) {
                return maxForwardInput;
            }

            // Near or above target speed, regulate gently. A small negative command is allowed so the drone can
            // actually slow down during the last 15 m, but it is capped to avoid near-stops at checkpoints.
            return Mathf.Clamp(speedError / (targetSpeed * 0.20f), -0.25f, maxForwardInput);
        }

        float HorizontalDistance(Vector3 a, Vector3 b) {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        void StopHorizontalAssistedInputs() {
            frontInput = 0f;
            rotationInput = 0f;
            strafeInput = 0f;
        }

        void StopAssistedInputs() {
            frontInput = 0f;
            verticalInput = 0f;
            rotationInput = 0f;
            strafeInput = 0f;
        }

        #endregion


        #region Autopilot

        // Autopilot data
        bool autopilotActive = false;
        Vector3 destination;
        enum AutopilotStep { Takeoff, Orientation, Transit, TransitStop, Landing }
        AutopilotStep autopilotStep = AutopilotStep.Takeoff;


        protected void AutopilotInputs() {
            if (!autopilotActive) { return; } // Don't do anything is autopilot isn't active

            switch (autopilotStep) {
                case AutopilotStep.Takeoff: // Takeoff mode : Raise until desired height is reached, or until no ground is detected
                    if (height < 30f) {
                        frontInput = 0f;
                        rotationInput = 0f;
                        verticalInput = 1f;
                    }
                    else {
                        frontInput = 0f;
                        rotationInput = 0f;
                        verticalInput = 0f;
                        autopilotStep = AutopilotStep.Orientation;
                    }
                    break;
                case AutopilotStep.Orientation:
                    float targetRotaInput = GetTargetAngle();
                    frontInput = 0f;
                    rotationInput = targetRotaInput;
                    verticalInput = 0f;
                    if (Mathf.Abs(targetRotaInput) < 0.25f) { // If orientation is close enough from target, move to next step (where rota will still be adjusted)
                        autopilotStep = AutopilotStep.Transit;
                    }
                    break;
                case AutopilotStep.Transit:
                    frontInput = 1f;
                    rotationInput = GetTargetAngle();
                    verticalInput = 0f;
                    if (GetSpeedToReachDestination() < FrontSpeed) {
                        autopilotStep = AutopilotStep.TransitStop;
                    }
                    break;
                case AutopilotStep.TransitStop:
                    if (GetSpeedToReachDestination() < FrontSpeed) { // Completely release input if enough speed is applied
                        frontInput = 0f;
                    }
                    else {
                        frontInput = Mathf.Min(GetSpeedToReachDestination() / frontMaxSpeed, frontMaxSpeed); // Complete speed if drone slows down too much
                    }
                    rotationInput = 0f;
                    verticalInput = 0f;
                    if (FrontSpeed < frontMaxSpeed * 0.02f) { // Move to next step once speed is lower than 2%
                        autopilotStep = AutopilotStep.Landing;
                    }
                    break;
                case AutopilotStep.Landing:
                    frontInput = 0f;
                    rotationInput = 0f;
                    verticalInput = -0.5f;
                    if (IsLanded()) { CancelDestination(); } // Autopilot reached its destination
                    break;
            }
        }

        /// <summary>
        /// Returns the angle to rotate to be aligned with destination
        /// </summary>
        float GetTargetAngle() {
            float targetAngle = Vector3.SignedAngle(droneRb.transform.forward, new Vector3(destination.x - droneRb.transform.position.x, droneRb.transform.forward.y, destination.z - droneRb.transform.position.z), Vector3.up);
            return (targetAngle > 0 ? 1 : -1) * Mathf.Min(Mathf.Abs(targetAngle), 15f) / 15f; // Max rotation input is reached at an angle of 15�, reduce input strength for closer angle
        }

        /// <summary>
        /// Returns the frontal speed required to reach destination if acceleration input is completely released now
        /// </summary>
        float GetSpeedToReachDestination() {
            float pureDistance = Vector3.Distance(droneRb.transform.position, new Vector3(destination.x, droneRb.transform.position.y, destination.z)); // The distance on X/Y axis
            float frontalFactor = Vector3.Dot((new Vector3(destination.x, droneRb.transform.position.y, destination.z) - droneRb.transform.position).normalized, droneRb.transform.forward); // The part of pure distance that is forward (ignore lateral distance, frontal speed won't help it)
            return pureDistance * frontalFactor * 2f;
        }

        /// <summary>
        /// Function called to enable autopilot with a desired destination (given as 2D map position)
        /// </summary>
        /// <param name="rangeX">The percentage of map on X axis (0 is West, 1 is East)</param>
        /// <param name="rangeY">The percentage of map on Y axis (0 is South, 1 is North)</param>
        /// <param name="emergencyRequest">If true, the request is triggered by the emergency landing and will ignore its verification</param>
        public void RequestDestination_MapPos(float rangeX, float rangeY, bool emergencyRequest = false) {
            if (emergencyLanding && !emergencyRequest) { return; } // Ignore invalid requests
            if (rangeX < 0f) { rangeX = 0f; } else if (rangeX > 1f) { rangeX = 1f; }
            if (rangeY < 0f) { rangeY = 0f; } else if (rangeY > 1f) { rangeY = 1f; }

            autopilotActive = true;
            actualMode = ConductMode.Classical; // Autopilot only works in assistance mode
            onAutopilot.Invoke();
            destination = new Vector3(Mathf.Lerp(southWestCorner.position.x, northEastCorner.position.x, rangeY), 0f, Mathf.Lerp(southWestCorner.position.z, northEastCorner.position.z, rangeX));
            autopilotStep = AutopilotStep.Takeoff;

            MapInteractionManager.OnAutopilotUpdate(new Vector2(rangeX, rangeY));

            StartCoroutine(LandingCollisionArea.Instance.CheckDestination(destination)); // Check destination validity (might take some frames)
        }

        /// <summary>
        /// Function that should be ONLY called by LandingCollisionArea, to update an incorrect destination with closest found destination.
        /// (Won't pass any validity test or data update, so don't use it for unsafe cases)
        /// </summary>
        public static void UpdateDestination(Vector3 newDestination) => Instance.OnUpdateDestination(newDestination);
        void OnUpdateDestination(Vector3 newDestination) {
            destination = newDestination;

            /*
             * Reconvert destination in range coordinates and pass it to UI
             */
            float rangeX = (southWestCorner.position.z - destination.z) / (southWestCorner.position.z - northEastCorner.position.z);
            float rangeY = (southWestCorner.position.x - destination.x) / (southWestCorner.position.x - northEastCorner.position.x);
            MapInteractionManager.OnAutopilotUpdate(new Vector2(rangeX, rangeY));
        }

        /// <summary>
        /// Function called to disable autopilot before reaching destination
        /// </summary>
        public void CancelDestination() {
            autopilotActive = false;
            MapInteractionManager.OnAutopilotUpdate(null);
        }

        #endregion


        #region Singleton Calls

        public static bool IsLanded() => height < 0.7f;
        public static Rigidbody DroneRb => Instance?.droneRb; // Give a public link to droneRb so anyone can access it easily through this singleton
        public static Transform DroneHitboxPreview => Instance?.droneHitboxPreview; // Give a public link to droneHitboxPreview so anyone can access it easily through this singleton

        /// <summary>
        /// Function called to turn the drone world position into a map position, represented in X/Y percentage
        /// </summary>
        public static Vector2 GetWorlPositionToMapRatio() => Instance.WorlPositionToMapRatio();
        Vector2 WorlPositionToMapRatio(Vector3? manualPos = null) {
            Vector3 worldPos = manualPos.HasValue ? manualPos.Value : droneRb.transform.position;
            float actDistanceX = southWestCorner.position.z - worldPos.z;
            float actDistanceY = southWestCorner.position.x - worldPos.x;
            float maxDistanceX = southWestCorner.position.z - northEastCorner.position.z;
            float maxDistanceY = southWestCorner.position.x - northEastCorner.position.x;
            return new Vector2(maxDistanceX == 0f ? 0f : actDistanceX / maxDistanceX, maxDistanceY == 0f ? 0f : actDistanceY / maxDistanceY);
        }

        /// <summary>
        /// Function called to turn the drone orientation into a map orientation
        /// </summary>
        public static float GetWorlRotationToMap() => Instance.WorlRotationToMap();
        float WorlRotationToMap() => -droneRb.transform.eulerAngles.y;

        /// <summary>
        /// Function called to get generic stats about ship : speed, height (with -1 if unknown), and vertical speed
        /// </summary>
        public static Vector3 GetGenericStats() => Instance.GenericStats();
        Vector3 GenericStats() {
            return new Vector3(FrontSpeed / 1000f * 3600f, height, VerticalSpeed / 1000f * 3600f);
        }

        public static float GetBatteryPercentage() => Instance.actualBatteryPercentage;

        public static ConductMode GetActualMode() => actualMode;

        public static bool IsCircuitInProgress {
            get => Instance.isCircuitInProgress;
            set {
                if (value) { // If circuit starts, refill battery
                    if (Instance.actualBatteryPercentage < emergencyLandingCeil) { Instance.emergencyLanding = false; EmergencyLanding_End?.Invoke(); }
                    if (Instance.actualBatteryPercentage < lowBatteryCeil) { Instance.lowBattery = false; LowBattery_End?.Invoke(); }
                    Instance.CancelDestination();
                    Instance.actualBatteryPercentage = 1f;
                }
                Instance.isCircuitInProgress = value; // Update bool value
            }
        }

        #endregion

    }

}