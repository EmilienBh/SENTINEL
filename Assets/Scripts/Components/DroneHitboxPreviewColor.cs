using Assets.Scripts.Components;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * Component designed to update the drone hitbox preview mesh, 
 */
public class DroneHitboxPreviewColor : MonoBehaviour {

    [SerializeField] MeshRenderer previewMesh;
    [SerializeField] Material validMaterial;
    [SerializeField] Material invalidMaterial;
    [SerializeField] SimpleCollisionDetector[] collisionDetectors;

    void Update() {
        foreach(SimpleCollisionDetector detector in collisionDetectors) {
            if (detector.collisions.Count > 0) {
                previewMesh.material = invalidMaterial;
                return;
            }
        }
        previewMesh.material = validMaterial;
    }

}
