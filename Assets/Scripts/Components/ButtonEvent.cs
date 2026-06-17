using UnityEngine;
using UnityEngine.Events;

/*
 * Component designed to throw events for a EventButton (because more events can't just be serialized on button class)
 */
class ButtonEvent : MonoBehaviour {

    public UnityEvent OnNormal;
    public UnityEvent OnDisabled;
    public UnityEvent OnHighlighted;
    public UnityEvent OnPressed;
    public UnityEvent OnSelected;

}
