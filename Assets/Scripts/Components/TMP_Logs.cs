using System;
using System.Collections;
using TMPro;
using UnityEngine;
/*
 * Component designed to display log messages on a TMP text
 */
public class TMP_Logs : MonoBehaviour {

    protected static TMP_Logs Instance; // Pseudo-singleton

    [SerializeField] TextMeshProUGUI tmp;

    private void Start() {
        Instance = this;
    }

    public static void PrintLog(string message) => Instance?.OnPrintLog(message);
    void OnPrintLog(string message) {
        tmp.text = message;
    }

}