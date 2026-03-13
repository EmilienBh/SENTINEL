using UnityEngine;
using UnityEngine.UI;

/*
 * Component designed to improve buttons with events triggered on transitions (used for multiple sprite updates)
 */
class EventButton : Button {

    [SerializeField] ButtonEvent buttonEvent;

#if UNITY_EDITOR
    protected override void OnValidate() {
        base.OnValidate();
        if (buttonEvent == null) { buttonEvent = GetComponent<ButtonEvent>(); }
    }
#endif

    protected override void DoStateTransition(SelectionState state, bool instant) {
        base.DoStateTransition(state, instant);
        switch (state) {
            case SelectionState.Normal:
                buttonEvent?.OnNormal?.Invoke();
                break;
            case SelectionState.Disabled:
                buttonEvent?.OnDisabled?.Invoke();
                break;
            case SelectionState.Highlighted:
                buttonEvent?.OnHighlighted?.Invoke();
                break;
            case SelectionState.Pressed:
                buttonEvent?.OnPressed?.Invoke();
                break;
            case SelectionState.Selected:
                buttonEvent?.OnSelected?.Invoke();
                break;
        }
    }

}
