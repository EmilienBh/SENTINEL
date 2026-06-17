using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Viable.VRNav {

    /*
     * Component designed to update the color of the throttle indicator when updated, based on front/back acceleration
     */
    public class ThrottlePowerColor : MonoBehaviour {
        [SerializeField] Color positiveColor;
        [SerializeField] Color negativeColor;
        [SerializeField] Image throttleGauge;
        bool actualColorPositive = true;

        public void OnThrottleValueUpdate(float newValue) {
            if (actualColorPositive && newValue < 0.5f) {
                actualColorPositive = false;
                throttleGauge.color = negativeColor;
            }
            else if (!actualColorPositive && newValue >= 0.5f) {
                actualColorPositive = true;
                throttleGauge.color = positiveColor;
            }
        }

    }

}
