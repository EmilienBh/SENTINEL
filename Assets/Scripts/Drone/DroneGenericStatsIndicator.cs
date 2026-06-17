using System.Collections;
using TMPro;
using UnityEngine;

namespace Viable.VRNav {
    public class DroneGenericStatsIndicator : MonoBehaviour {

        [SerializeField] TextMeshProUGUI speedField;
        [SerializeField] TextMeshProUGUI heightField;
        [SerializeField] TextMeshProUGUI elevationSpeedField;

        float delayBetweenTwoCloseValues = 0.5f;
        float lastCloseUpdate_visualSpeed;
        float lastCloseUpdate_elevationSpeed;

        float actualVisualSpeed;
        float actualVisualElevationSpeed;

        void Update() {
            Vector3 stats = DroneMover.GetGenericStats();
            TryUpdateSpeed(stats.x, ref actualVisualSpeed, speedField, ref lastCloseUpdate_visualSpeed, 5f);
            TryUpdateSpeed(stats.z, ref actualVisualElevationSpeed, elevationSpeedField, ref lastCloseUpdate_elevationSpeed, 1f);
            if (stats.y == 999f) {
                heightField.text = $"N/A";
            }
            else {
                heightField.text = $"{stats.y.ToString("F1")} m";
            }
        }

        /*
         * Function designed to update the speed indicator only if relevant : prevents close rounded values flickering
         */
        void TryUpdateSpeed(float newSpeed, ref float actualSpeed, TextMeshProUGUI tmp, ref float lastCloseUpdate, float closeCeil) {
            if (Mathf.Abs(newSpeed - actualSpeed) < closeCeil) {
                if (Time.time - lastCloseUpdate < delayBetweenTwoCloseValues) {
                    return; // If value is too close and was updated recently, don't update
                }
                else {
                    lastCloseUpdate = Time.time;
                }
            }

            actualSpeed = newSpeed;
            tmp.text = $"{actualSpeed.ToString("F0")} Km/h";
        }

    }

}
