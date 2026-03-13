using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Component designed to trigger 1 of 2 events, alternated every call
/// </summary>
public class SwitchEvent : MonoBehaviour {

    [Tooltip("If ticked, newt event called will be \"true\" event.")] public bool nextEventTrue = true;
    [SerializeField] UnityEvent trueEvent;
    [SerializeField] UnityEvent falseEvent;

    public void Trigger() {
        if (nextEventTrue) {
            trueEvent.Invoke();
        }
        else {
            falseEvent.Invoke();
        }
        nextEventTrue = !nextEventTrue;
    }

}
