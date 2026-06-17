using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * Component designed to update the visual indicators of drive inputs
 */
public class UIInputStatusManager : MonoBehaviour {

    public static UIInputStatusManager Instance; // Singleton instance

    [SerializeField, Tooltip("Elevation indicator sprite")] RectTransform elevationIndicator;
    [SerializeField, Tooltip("Elevation indicator digit text field")] TextMeshProUGUI elevationTxt;
    [SerializeField, Tooltip("Frontal move indicator sprite")] RectTransform moveIndicator;
    [SerializeField, Tooltip("Frontal move indicator digit text field")] TextMeshProUGUI moveTxt;
    [SerializeField, Tooltip("Info button for Hover Mode")] Image hoverModeInfoBtn;
    [SerializeField, Tooltip("Info text for Hover Mode")] TextMeshProUGUI hoverModeInfoTxt;
    [SerializeField, Tooltip("Rotation indicator sprite")] RectTransform rotationIndicator;
    [SerializeField, Tooltip("Rotation indicator digit text field")] TextMeshProUGUI rotationTxt;
    [SerializeField, Tooltip("Info button for Flight Mode")] Image flightModeInfoBtn;
    [SerializeField, Tooltip("Info text for Flight Mode")] TextMeshProUGUI flightModeInfoTxt;
    [SerializeField, Tooltip("Enabled color for mode info")] Color modeInfoColor_Enabled;
    [SerializeField, Tooltip("Disabled color for mode info")] Color modeInfoColor_Disabled;

    [SerializeField, Tooltip("Max positive/negative offset to apply for max/min elevation value")] float elevationMaxPos;
    [SerializeField, Tooltip("Max positive/negative offset to apply for max/min move value")] float moveMaxPos;

    bool previous_isDroneStopped = true; // Remember if drone was considered stopped (hover mode) at previous call, to reduce data updates


    void Start() {
        Instance = this;
    }

    /*
     * Hard-coded, if required anytime update this to be flexible
     */
    public void Update_Elevation(float elevationInput) {
        if (elevationIndicator == null) { return; }
        elevationIndicator.anchoredPosition = new Vector2(0f, elevationInput * elevationMaxPos);
        elevationTxt.text = Mathf.RoundToInt(elevationInput * 10f).ToString();
    }

    public void Update_MoveIndicators(float moveInput) {
        if (moveIndicator == null) { return; }
        moveIndicator.anchoredPosition = new Vector2(0f, moveInput * moveMaxPos);
        moveTxt.text = Mathf.RoundToInt(moveInput * 10f).ToString();
    }

    public void Update_RotationIndicators(float rotationInput) {
        if (rotationIndicator == null) { return; }
        rotationIndicator.anchoredPosition = new Vector2(0f, -rotationInput * moveMaxPos);
        int rotaDigit = Mathf.RoundToInt(Mathf.Abs(rotationInput) * 10f);
        rotationTxt.text = rotaDigit + (rotaDigit == 0 ? "" : rotationInput < 0f ? " L" : " R");
    }

    public void Update_IsDroneStopped(bool droneStopped) {
        if (previous_isDroneStopped == droneStopped) { return; }
        previous_isDroneStopped = droneStopped;
        hoverModeInfoBtn.color = droneStopped ? modeInfoColor_Enabled : modeInfoColor_Disabled;
        hoverModeInfoTxt.color = droneStopped ? modeInfoColor_Enabled : modeInfoColor_Disabled;
        flightModeInfoBtn.color = droneStopped ? modeInfoColor_Disabled : modeInfoColor_Enabled;
        flightModeInfoTxt.color = droneStopped ? modeInfoColor_Disabled : modeInfoColor_Enabled;
    }

}
