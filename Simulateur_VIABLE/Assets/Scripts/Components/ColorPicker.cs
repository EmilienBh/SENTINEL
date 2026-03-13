using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Component designed to throw a color in a UnityEvent, from a predefined panel of colors
/// </summary>
public class ColorPicker : MonoBehaviour {

    [SerializeField] Color[] newColor;
    [SerializeField] UnityEvent<Color> colorPicked;

    public void ChangeColor(int index) {
        if (newColor.Length > index) { colorPicked?.Invoke(newColor[index]); }
    }

}
