using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Viable.VRNav {

    /// <summary>
    /// Component designed to handle the drone movement
    /// This is the generic, abstract version, that implements the drone behaviour but no specific input
    /// </summary>
    public abstract class DroneMover : MonoBehaviour {
        protected static DroneMover Instance; // Instance of this component (signleton)

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
        protected const float frontMaxSpeed = 40f; // Max force in front direction
        protected const float frontAcceleration = 1f; // Acceleration force in front direction
        protected const float verticalMaxSpeed = 4f; // Max force in vertical direction
        protected const float verticalAcceleration = 0.5f; // Acceleration force in vertical direction
        protected const float rotationMaxSpeed = 2.5f; // Rotation maximum velocity (won't apply more force over this velocity)
        protected const float rotationAcceleration = 15f; // Rotation acceleration (force applied to rigidbody)
        protected const float properllerMaxRotaSpeed = 1080f; // The maximum propeller rotation in degrees by second
        protected float frontPropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected float verticalPropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected float rotationPropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected float strafePropulsion = 0f; // Actual multiplier (value between 0 and 1, reaches the value of matching input after a given time)
        protected const float propulsionTime = 3f; // Time in seconds to raise propulsion from 0 to 1
        protected const float rotationPitch = 10f; // Drone anim X-rotation on pitch
        protected const float rotationBank = -5f; // Drone anim Z-rotation on bank
        float minAudioPitch = 0.5f; // Propulsors audio min pitch
        float maxAudioPitch = 1.4f; // Propulsors audio max pitch
        float minAudioVolume = 0f; // Propulsors audio min volume
        float maxAudioVolume = 0.5f; // Propulsors audio max volume
        static float height = 999f; // Drone actual height (if unknown, set to 999 since ground is far enough)
        float heightFactor = 0f; // Drone height factor, impacts the gravity to fake takeoff intertia (0 means on-ground, 1 means in-air)

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

        // Input vals
        protected float frontInput; // The acceleration input (0 = nothing, 1 = full front, 2 = full back)
        protected float verticalInput; // The vertical acceleration input (0 = nothing, 1 = full up, 2 = full down)
        protected float rotationInput; // The rotation input (0 = nothing, 1 = full right, 2 = full left)
        protected float strafeInput; // The strafe (lateral force) input (0 = nothing, 1 = full right, 2 = full left)
        float constant_frontInput = 0f; // Used by constant value conduct modes, to have a fix, incrementable input, decorrelated from controllers
        float constant_verticalInput = 0f; // Used by constant value conduct modes, to have a fix, incrementable input, decorrelated from controllers
        int numerical_previousFrontInput = 0; // Previous numerical value, used to determine numerical increment
        int numerical_previousVerticalInput = 0; // Previous numerical value, used to determine numerical increment
        const float numerical_increment = 0.25f; // Value to add/remove on numerical udpate
        const float linear_zeroCeil = 0.15f; // If the value of an input is lower than ceil, consider this input is released (see refs to this variable for concrete applications)

        // Properties
        float FrontSpeed => Vector3.Dot(droneRb.velocity, droneRb.transform.forward); // Frontal speed is velocity in front direction
        float VerticalSpeed => Vector3.Dot(droneRb.velocity, droneRb.transform.up); // Vertical speed is velocity in up direction
        bool IsInputCeiled(float testedInput) => testedInput < linear_zeroCeil && testedInput > -linear_zeroCeil; //Returns wether or not given input is under zero-ceil (meaning it can be considered as stopped, set to 0)
        bool IsHoverMode => FrontSpeed * 3.6f < 40f; // Allows to know if evtol is considered in hover mode (e.g. no or few front speed), which differenciate stationary and flight conduct modes (modifies the rotation behaviour)

        // Actual inputs based on mode
        float ActualFrontInput => IsConstantMode ? constant_frontInput : frontInput;
        float ActualVerticalInput => (IsConstantMode || actualMode == ConductMode.TiltRotor) ? constant_verticalInput : verticalInput; // Hard-coded exception for tilt-rotor mode (because one of the inputs is always constant)
        bool IsConstantMode => actualMode == ConductMode.TiltRotor && !IsHoverMode;


        #region Inputs

        /// <summary>
        /// Function designed to turn raw inputs into drone inputs. (example : "left stick vertical axis" => "vertical speed input")
        /// <br/>Raw input format is based on VR commands. Drone inputs are based on actual modes.
        /// </summary>
        protected void HandleInputs(Vector2 RawInput_Left, Vector2 RawInput_Right) {
            /*
             * Remove parasite inputs
             */
            if (!RemoveParasiteInput(RawInput_Left.x, RawInput_Left.y)) { RemoveParasiteInput(RawInput_Left.y, RawInput_Left.x); }
            if (!RemoveParasiteInput(RawInput_Right.x, RawInput_Right.y)) { RemoveParasiteInput(RawInput_Right.y, RawInput_Right.x); }

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
        bool RemoveParasiteInput(float input1, float input2) {
            if (Mathf.Abs(input1) < 0.5f) { return false; }
            if (Mathf.Abs(input2) > 0.1f) { return false; }
            input2 = 0f;
            return true;
        }

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
            UIInputStatusManager.Instance?.Update_MoveIndicators(ActualFrontInput);
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
            rotationPropulsion = UpdatePropulsion(rotationPropulsion, rotationInput, propulsionDelta);
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
                droneHitboxPreview.localEulerAngles = new Vector3(90f, 0f, -droneRb.transform.localEulerAngles.y); // Also update hitbox preview rotation based on actual drone rotation
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
            if (droneRb.useGravity) { return; } // Ignore non-vertical move if grounded
            float backFactor = ActualFrontInput > 0 ? 1f : -0.5f; // Divide acceleration and max speed when moving backward, and change direction
            bool resetAccelAnim = true; // If max speed reached or near ground, anim orientation should reset
            if (ActualFrontInput != 0 && (Mathf.Abs(FrontSpeed / backFactor) < frontMaxSpeed || FrontSpeed * ActualFrontInput < 0)) { // Don't add new acceleration force if actual speed is too high (opposed force is always valid)
                droneRb.AddForce(Mathf.Abs(frontPropulsion) * ActualFrontInput * frontAcceleration * droneRb.transform.forward, ForceMode.Force); // Set the force
                if (height > 1f && FrontSpeed < frontMaxSpeed * 0.9f) { // Update the drone anim X-rotation
                    droneAnim.localEulerAngles = new Vector3(Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.x), ActualFrontInput * rotationPitch, Time.fixedDeltaTime), droneAnim.localEulerAngles.y, droneAnim.localEulerAngles.z); // Reset rotation is slower
                    resetAccelAnim = false;
                }
            }
            if (resetAccelAnim) { // Update the drone anim Z-rotation to zero (affected by speed cap : once max speed is reached, pitch isn't required to move forward)
                droneAnim.localEulerAngles = new Vector3(Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.x), 0f, Time.fixedDeltaTime / 2f), droneAnim.localEulerAngles.y, droneAnim.localEulerAngles.z);
            }
        }


        /// <summary>
        /// Function called by UpdateForces to update the rotation forces
        /// </summary>
        public void UpdateRotation() => ApplyLateralForces(false, rotationInput, rotationPropulsion);

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
            if (height > 1f) { // Update the drone anim Z-rotation (not affected by speed cap : while drone turns, it banks)
                droneAnim.localEulerAngles = new Vector3(droneAnim.localEulerAngles.x, droneAnim.localEulerAngles.y, Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.z), lateralInput * rotationBank, Time.fixedDeltaTime));
            }
            else { // Reset anim Z-rotation if near ground
                droneAnim.localEulerAngles = new Vector3(droneAnim.localEulerAngles.x, droneAnim.localEulerAngles.y, Mathf.Lerp(TransformUtils.GetCenteredAngle(droneAnim.localEulerAngles.z), 0f, Time.fixedDeltaTime));
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
            if (IsConstantMode || actualMode == ConductMode.TiltRotor) { // Tilt-rotor opens this condition too (because of permanent constant vertical input)

                const float constant_maxSpeed = 1.1f; // Constant value change speed in units by seconds
                float deltaSpeed = constant_maxSpeed * Time.deltaTime; // Max value change for this frame, scaled by input

                if (verticalInput < -0.2f || verticalInput > 0.2f) {
                    constant_verticalInput += deltaSpeed * verticalInput;
                    if (constant_verticalInput > 1f) { constant_verticalInput = 1f; } // Clamp positive
                    else if (constant_verticalInput < -1f) { constant_verticalInput = -1f; } // Clamp negative
                }
                else {
                    if (IsInputCeiled(constant_verticalInput)) { constant_verticalInput = 0f; } // Zero-ceil (when input is released, set constant value to zero if under ceil)
                }

                if (IsConstantMode) { // Front input is only constant when true Constant Mode is on
                    if (frontInput < -0.2f || frontInput > 0.2f) {
                        constant_frontInput += deltaSpeed * frontInput;
                        if (constant_frontInput > 1f) { constant_frontInput = 1f; } // Clamp positive
                        else if (constant_frontInput < -1f) { constant_frontInput = -1f; } // Clamp negative
                    }
                    else {
                        if (IsInputCeiled(constant_frontInput)) { constant_frontInput = 0f; } // Zero-ceil (when input is released, set constant value to zero if under ceil)
                    }
                }
                else { // When not in true Constant Mode, reset front input
                    constant_frontInput = 1f;
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
            }
        }

        /// <summary>
        /// Change orientation with mouse
        /// </summary>
        protected abstract void UpdateCamera();

        /// <summary>
        /// Update anim related data
        /// </summary>
        void UpdateAnims() {
            /*
             * Inputs used by anim depend of the mode
             */
            float deltaRota = 0f;
            if (heightFactor == 1f) {
                rotorsUsageFactor = Mathf.Lerp(0.4f, 0.9f, (verticalPropulsion + 1f) / 2f); // Clamp V-propulsion to 1=>0 : 0.5 is stationary, 0 goes down, 1 goes up
            }
            else {
                rotorsUsageFactor = Mathf.Lerp(0f, 0.9f, verticalPropulsion); // If landed, only care for positive propulsion
            }
            deltaRota = rotorsUsageFactor * properllerMaxRotaSpeed * Time.deltaTime;

            foreach (var propeller in topPropellers) {
                propeller.localEulerAngles = new Vector3(0f, propeller.localEulerAngles.y + deltaRota, 0f);
            }

            propulsorsAudio.pitch = Mathf.Lerp(minAudioPitch, maxAudioPitch, Mathf.Abs(rotorsUsageFactor));
            propulsorsAudio.volume = Mathf.Lerp(minAudioVolume, maxAudioVolume, Mathf.Abs(rotorsUsageFactor));
        }

        /// <summary>
        /// Update battery autonomy based on actual propulsion, for the battery simulation feature 
        /// </summary>
        void UpdateBattery() {
            /*
             * Landed battery recharging
             */
            if (rotorsUsageFactor == 0f && actualBatteryPercentage < 1f) {
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
             * Battery depletion
             */
            float nbRotors; // Number of rotors used at the moment, acts as a battery usage coefficient
            if (actualMode == ConductMode.TiltRotor) { nbRotors = 5f; } // In tilt-rotor mode, always use all rotors at 50% (so consider it's half of the maximum rotor usage in other modes)
            else { nbRotors = Mathf.Abs(frontPropulsion) * 8f + (heightFactor == 1f ? 1f : Mathf.Abs(verticalPropulsion)) * 2f; } // In other modes, multiply propulsions by the number of rotors placed in this direction
            if (nbRotors == 0f) { return; } // In some cases, nbRotors value can be 0 without trigger the first return check, causing a bug

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
            if (rangeX < 0 || rangeX > 1 || rangeY < 0 || rangeY > 1 || (emergencyLanding && !emergencyRequest)) { return; } // Ignore invalid requests

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

        /// <summary>
        /// Function called to turn the drone world position into a map position, represented in X/Y percentage
        /// </summary>
        public static Vector2 GetWorlPositionToMapRatio() => Instance.WorlPositionToMapRatio();
        Vector2 WorlPositionToMapRatio(Vector3? manualPos = null) {
            Vector3 worldPos = manualPos.HasValue ? manualPos.Value : droneRb.transform.position;
            float actDistanceX = Mathf.Abs(southWestCorner.position.z - worldPos.z);
            float actDistanceY = Mathf.Abs(southWestCorner.position.x - worldPos.x);
            float maxDistanceX = Mathf.Abs(southWestCorner.position.z - northEastCorner.position.z);
            float maxDistanceY = Mathf.Abs(southWestCorner.position.x - northEastCorner.position.x);
            return new Vector2(actDistanceX / maxDistanceX, actDistanceY / maxDistanceY);
        }

        /// <summary>
        /// Function called to turn the drone orientation into a map orientation
        /// </summary>
        public static float GetWorlRotationToMap() => Instance.WorlRotationToMap();
        float WorlRotationToMap() => -droneRb.transform.eulerAngles.y;

        /// <summary>
        /// Function called to get generic stats about ship : speed (in km/s), and height (with -1 if unknown)
        /// </summary>
        public static Vector3 GetGenericStats() => Instance.GenericStats();
        Vector3 GenericStats() {
            return new Vector3(FrontSpeed / 1000f * 3600f, height, VerticalSpeed / 1000f * 3600f);
        }

        public static float GetBatteryPercentage() => Instance.actualBatteryPercentage;

        #endregion

    }

}
