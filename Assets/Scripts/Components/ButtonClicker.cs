using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component designed to trigger the OnClick event of a button on the same object.
/// Allows to call a button click from a serialized event.
/// </summary>
public class ButtonClicker : MonoBehaviour {

    public void TriggerOnClick() {
        gameObject.GetComponent<Button>()?.onClick?.Invoke();
    }

}
