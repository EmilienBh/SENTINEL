using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Viable.VRNav {

    /*
     * Component designed to trigger an event when enabled/disabled
     */
    public class OnEnableComponent : MonoBehaviour {

        [SerializeField] UnityEvent onEnable;
        [SerializeField] UnityEvent onDisable;

        void OnEnable() {
            onEnable?.Invoke();
        }

        void OnDisable() {
            onDisable?.Invoke();
        }
    }

}
