using UnityEditor;
using UnityEngine;

/*
 * Code used to add draw conditions to a serialized attribute
 * Code source : https://forum.unity.com/threads/draw-a-field-only-if-a-condition-is-met.448855/
 */

[CustomPropertyDrawer(typeof(DrawIfAttribute))]
public class DrawIfPropertyDrawer : PropertyDrawer {
    // Reference to the attribute on the property.
    DrawIfAttribute drawIf;

    // Field that is being compared.
    SerializedProperty comparedField;

    // Height of the property.
    private float propertyHeight;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return propertyHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        // Set the global variables.
        drawIf = attribute as DrawIfAttribute;
        comparedField = property.serializedObject.FindProperty(drawIf.comparedPropertyName);

        // Is the condition met? Should the field be drawn?
        bool conditionMet = false;

        // Compare the values to see if the condition is met.
        switch (drawIf.comparisonType) {
            case ComparisonType.StringEquals:
                if (comparedField.stringValue.Equals(drawIf.comparedValue))
                    conditionMet = true;
                break;
            case ComparisonType.EnumEquals:
                if (comparedField.enumNames[comparedField.enumValueIndex].Equals(drawIf.comparedValue.ToString()))
                    conditionMet = true;
                break;

            case ComparisonType.BoolEquals:
                if (comparedField.boolValue.Equals(drawIf.comparedValue))
                    conditionMet = true;
                break;

            case ComparisonType.FloatEquals:
                if (comparedField.floatValue.Equals((float) drawIf.comparedValue))
                    conditionMet = true;
                break;

            case ComparisonType.GreaterThan:
                if (comparedField.floatValue > (float) drawIf.comparedValue)
                    conditionMet = true;
                break;

            case ComparisonType.SmallerThan:
                if (comparedField.floatValue < (float) drawIf.comparedValue)
                    conditionMet = true;
                break;

            case ComparisonType.SmallerOrEqual:
                if (comparedField.floatValue <= (float) drawIf.comparedValue)
                    conditionMet = true;
                break;

            case ComparisonType.GreaterOrEqual:
                if (comparedField.floatValue >= (float) drawIf.comparedValue)
                    conditionMet = true;
                break;
        }

        // The height of the property should be defaulted to the default height.
        propertyHeight = base.GetPropertyHeight(property, label);

        // If the condition is met, simply draw the field. Else...
        if (conditionMet) {
            EditorGUI.PropertyField(position, property, label);
        }
        else {
            //...check if the disabling type is read only. If it is, draw it disabled, else, set the height to zero.
            if (drawIf.disablingType == DisablingType.ReadOnly) {
                GUI.enabled = false;
                EditorGUI.PropertyField(position, property);
                GUI.enabled = true;
            }
            else {
                propertyHeight = 0f;
            }
        }
    }
}
