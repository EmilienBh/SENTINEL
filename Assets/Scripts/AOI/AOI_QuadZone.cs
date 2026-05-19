using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class AOI_QuadZone : MonoBehaviour
{
    [Header("Identification")]
    [SerializeField] private string aoiId = "AOI_Quad";

    [Header("Coins locaux")]
    [FormerlySerializedAs("bottomLeft")]
    [SerializeField] private Vector3 coinBasGauche = new Vector3(-0.5f, -0.5f, 0f);
    [FormerlySerializedAs("bottomRight")]
    [SerializeField] private Vector3 coinBasDroit = new Vector3(0.5f, -0.5f, 0f);
    [FormerlySerializedAs("topRight")]
    [SerializeField] private Vector3 coinHautDroit = new Vector3(0.5f, 0.5f, 0f);
    [FormerlySerializedAs("topLeft")]
    [SerializeField] private Vector3 coinHautGauche = new Vector3(-0.5f, 0.5f, 0f);

    [Header("Gizmos")]
    [SerializeField] private bool afficherGizmos = true;

    public string AoiId => aoiId;
    public Vector3 BottomLeft => coinBasGauche;
    public Vector3 BottomRight => coinBasDroit;
    public Vector3 TopRight => coinHautDroit;
    public Vector3 TopLeft => coinHautGauche;

    private void Awake()
    {
        ConstruireMesh();
    }

    private void Reset()
    {
        ConstruireMesh();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += ReconstruireMeshEditeur;
    }

    private void ReconstruireMeshEditeur()
    {
        if (this == null)
            return;

        ConstruireMesh();
    }
#endif

    public bool TryGetUV(Vector3 pointMonde, out Vector2 uv, out Vector3 pointLocal)
    {
        pointLocal = transform.InverseTransformPoint(pointMonde);

        float u = 0.5f;
        float v = 0.5f;

        for (int i = 0; i < 12; i++)
        {
            Vector3 point = InterpolationBilineaire(coinBasGauche, coinBasDroit, coinHautDroit, coinHautGauche, u, v);
            Vector3 erreur = point - pointLocal;

            Vector3 deriveeU = (1f - v) * (coinBasDroit - coinBasGauche) + v * (coinHautDroit - coinHautGauche);
            Vector3 deriveeV = (1f - u) * (coinHautGauche - coinBasGauche) + u * (coinHautDroit - coinBasDroit);

            float a = Vector3.Dot(deriveeU, deriveeU);
            float b = Vector3.Dot(deriveeU, deriveeV);
            float c = Vector3.Dot(deriveeV, deriveeV);
            float d = Vector3.Dot(deriveeU, erreur);
            float e = Vector3.Dot(deriveeV, erreur);
            float determinant = a * c - b * b;

            if (Mathf.Abs(determinant) < 0.000001f)
                break;

            float deltaU = (c * d - b * e) / determinant;
            float deltaV = (a * e - b * d) / determinant;

            u = Mathf.Clamp01(u - deltaU);
            v = Mathf.Clamp01(v - deltaV);
        }

        uv = new Vector2(u, v);
        return true;
    }

    public Bounds GetWorldBounds()
    {
        Vector3[] points =
        {
            transform.TransformPoint(coinBasGauche),
            transform.TransformPoint(coinBasDroit),
            transform.TransformPoint(coinHautDroit),
            transform.TransformPoint(coinHautGauche)
        };

        Bounds bounds = new Bounds(points[0], Vector3.zero);

        for (int i = 1; i < points.Length; i++)
            bounds.Encapsulate(points[i]);

        return bounds;
    }

    private void ConstruireMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "AOI_Quad_Mesh",
            vertices = new[]
            {
                coinBasGauche,
                coinBasDroit,
                coinHautDroit,
                coinHautGauche
            },
            triangles = new[]
            {
                0, 2, 1,
                0, 3, 2
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            }
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshCollider colliderMesh = GetComponent<MeshCollider>();
        colliderMesh.sharedMesh = null;
        colliderMesh.sharedMesh = mesh;
        colliderMesh.convex = false;
    }

    private Vector3 InterpolationBilineaire(Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01, float u, float v)
    {
        Vector3 bas = Vector3.Lerp(p00, p10, u);
        Vector3 haut = Vector3.Lerp(p01, p11, u);
        return Vector3.Lerp(bas, haut, v);
    }

    private void OnDrawGizmos()
    {
        if (!afficherGizmos)
            return;

        Gizmos.color = Color.yellow;

        Vector3 p00 = transform.TransformPoint(coinBasGauche);
        Vector3 p10 = transform.TransformPoint(coinBasDroit);
        Vector3 p11 = transform.TransformPoint(coinHautDroit);
        Vector3 p01 = transform.TransformPoint(coinHautGauche);

        Gizmos.DrawLine(p00, p10);
        Gizmos.DrawLine(p10, p11);
        Gizmos.DrawLine(p11, p01);
        Gizmos.DrawLine(p01, p00);
    }
}
