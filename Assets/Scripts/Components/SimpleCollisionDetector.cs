using System.Collections.Generic;
using UnityEngine;
using Viable.VRNav;

namespace Assets.Scripts.Components {

    /*
     * Component designed to be placed on a collider GameObject, to count its actual collisions
     */
    public class SimpleCollisionDetector : MonoBehaviour {

        // There is no way to check actual collisions at any time, so we have to reference them manually
        public List<Collider> collisions = new List<Collider>();
        void OnTriggerEnter(Collider other) {
            if (other.gameObject.layer == GameLayers.Buildings) {
                collisions.Add(other);
            }
        }
        void OnTriggerExit(Collider other) {
            if (other.gameObject.layer == GameLayers.Buildings) {
                collisions.Remove(other);
            }
        }

    }
}
