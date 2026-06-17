using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Viable.Circuit {
    /*
     * Component designed to be attached to the circuit cursor, and to make it point to the actual step target
     */
    public class Circuit_Cursor : MonoBehaviour {

        void Update() {
            transform.LookAt(CircuitManager.RequestObjectivePosition());
        }
        
    }
}
