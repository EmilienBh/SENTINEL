using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Viable.VRNav;

public class DronePowerIndicator : MonoBehaviour {
    [SerializeField] Slider powerSlider;
    [SerializeField] TextMeshProUGUI powerTxt;

    void Update() {
        float batteryPercentage = DroneMover.GetBatteryPercentage();
        float displayedBatteryPercentage = Mathf.Max(0f, batteryPercentage - 0.2f) / 0.8f; // Peukert battery depletion makes last 20% burn in a VERY short time. Clamp the battery autonomy shown to the user in the range of 20-100%
        powerSlider.value = displayedBatteryPercentage;
        powerTxt.text = (displayedBatteryPercentage * 100f).ToString("F0") + '%';
    }
}
