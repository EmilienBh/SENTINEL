using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Définit une zone d'intérêt (AOI) quadrilatère.
/// Le mesh sert à visualiser la zone dans Unity et le MeshCollider sert aux raycasts du regard.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class AOI_QuadZone : MonoBehaviour
{
    #region Inspector

    [Header("Identification")]
    [SerializeField] private string aoiId = "AOI_Quad"; // Identifiant unique exporté dans les CSV et utilisé pour retrouver la capture AOI.

    [Header("Coins locaux")]
    [FormerlySerializedAs("bottomLeft")]
    [SerializeField] private Vector3 coinBasGauche = new Vector3(-0.5f, -0.5f, 0f); // Coin bas gauche de l'AOI dans le repère local.
    [FormerlySerializedAs("bottomRight")]
    [SerializeField] private Vector3 coinBasDroit = new Vector3(0.5f, -0.5f, 0f); // Coin bas droit de l'AOI dans le repère local.
    [FormerlySerializedAs("topRight")]
    [SerializeField] private Vector3 coinHautDroit = new Vector3(0.5f, 0.5f, 0f); // Coin haut droit de l'AOI dans le repère local.
    [FormerlySerializedAs("topLeft")]
    [SerializeField] private Vector3 coinHautGauche = new Vector3(-0.5f, 0.5f, 0f); // Coin haut gauche de l'AOI dans le repère local.

    [Header("Affichage éditeur")]
    [SerializeField] private bool afficherGizmos = true; // Affiche le contour de l'AOI dans la Scene View.

    #endregion

    #region Propriétés publiques

    public string AoiId => aoiId;
    public Vector3 BottomLeft => coinBasGauche;
    public Vector3 BottomRight => coinBasDroit;
    public Vector3 TopRight => coinHautDroit;
    public Vector3 TopLeft => coinHautGauche;

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        ReconstruireMesh();
    }

    private void OnValidate()
    {
        // DelayCall évite de modifier les composants Unity pendant la validation de l'inspecteur.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ReconstruireMesh();
        };
#else
        ReconstruireMesh();
#endif
    }

    #endregion

    #region API AOI

    /// <summary>
    /// Convertit un point monde touché par raycast en coordonnées UV normalisées dans l'AOI.
    /// u et v sont compris entre 0 et 1 lorsque le point est dans la zone.
    /// </summary>
    public bool TryGetUV(Vector3 pointMonde, out float u, out float v)
    {
        Vector3 pointLocal = transform.InverseTransformPoint(pointMonde);

        Vector3 bas = coinBasDroit - coinBasGauche;
        Vector3 gauche = coinHautGauche - coinBasGauche;

        float largeurCarree = Vector3.Dot(bas, bas);
        float hauteurCarree = Vector3.Dot(gauche, gauche);

        if (largeurCarree <= 0.000001f || hauteurCarree <= 0.000001f)
        {
            u = 0f;
            v = 0f;
            return false;
        }

        Vector3 depuisOrigine = pointLocal - coinBasGauche;

        u = Mathf.Clamp01(Vector3.Dot(depuisOrigine, bas) / largeurCarree);
        v = Mathf.Clamp01(Vector3.Dot(depuisOrigine, gauche) / hauteurCarree);

        return true;
    }

    #endregion

    #region Mesh et collider

    private void ReconstruireMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "AOI_QuadZone_Mesh"
        };

        mesh.vertices = new[]
        {
            coinBasGauche,
            coinBasDroit,
            coinHautDroit,
            coinHautGauche
        };

        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        mesh.triangles = new[]
        {
            0, 1, 2,
            0, 2, 3
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        meshFilter.sharedMesh = mesh;

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true; 
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!afficherGizmos)
            return;

        Vector3 p00 = transform.TransformPoint(coinBasGauche);
        Vector3 p10 = transform.TransformPoint(coinBasDroit);
        Vector3 p11 = transform.TransformPoint(coinHautDroit);
        Vector3 p01 = transform.TransformPoint(coinHautGauche);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p00, p10);
        Gizmos.DrawLine(p10, p11);
        Gizmos.DrawLine(p11, p01);
        Gizmos.DrawLine(p01, p00);
    }

    #endregion
}
