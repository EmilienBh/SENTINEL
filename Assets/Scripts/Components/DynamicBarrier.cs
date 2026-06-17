namespace Viable.VRNav {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Component designed to be placed on a plane, in order to adapt its UV to its scale
    /// </summary>
    public class DynamicBarrier : MonoBehaviour {

        [SerializeField] MeshFilter barrierMesh;

        private void OnEnable() {
            float heightFactor = transform.lossyScale.y;
            float widthFactor = transform.lossyScale.x;
            barrierMesh.mesh.uv = new Vector2[] { new Vector2(0, widthFactor), new Vector2(0, 0), new Vector2(heightFactor, widthFactor), new Vector2(heightFactor, 0) };

            barrierMesh.mesh.RecalculateNormals();
            barrierMesh.mesh.RecalculateTangents();
        }

    }

}
