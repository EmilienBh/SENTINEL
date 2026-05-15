using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class AOI_QuadZone : MonoBehaviour
{
    [Header("Identification")]
    [SerializeField] private string aoiId = "AOI_Quad";
    public string AoiId => aoiId;

    [Header("Coins locaux de l'AOI")]
    public Vector3 bottomLeft = new Vector3(-0.5f, -0.5f, 0f);
    public Vector3 bottomRight = new Vector3(0.5f, -0.5f, 0f);
    public Vector3 topRight = new Vector3(0.5f, 0.5f, 0f);
    public Vector3 topLeft = new Vector3(-0.5f, 0.5f, 0f);

    [Header("Debug")]
    public bool afficherGizmos = true;

    private MeshCollider meshCollider;

    private void Awake()
    {
        ConstruireMesh();
    }

    private void ConstruireMesh()
    {
        meshCollider = GetComponent<MeshCollider>();

        Mesh mesh = new Mesh();
        mesh.name = "AOI_Quad_Mesh";

        mesh.vertices = new Vector3[]
        {
            bottomLeft,
            bottomRight,
            topRight,
            topLeft
        };

        // Face orientée dans le bon sens
        mesh.triangles = new int[]
        {
            0, 2, 1,
            0, 3, 2
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false;

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
    }

    public bool TryGetUV(Vector3 worldPoint, out Vector2 uv, out Vector3 localPoint)
    {
        localPoint = transform.InverseTransformPoint(worldPoint);

        Vector3 p00 = bottomLeft;
        Vector3 p10 = bottomRight;
        Vector3 p11 = topRight;
        Vector3 p01 = topLeft;

        float u = 0.5f;
        float v = 0.5f;

        for (int i = 0; i < 12; i++)
        {
            Vector3 p = Bilinear(p00, p10, p11, p01, u, v);
            Vector3 error = p - localPoint;

            Vector3 du = (1f - v) * (p10 - p00) + v * (p11 - p01);
            Vector3 dv = (1f - u) * (p01 - p00) + u * (p11 - p10);

            float a = Vector3.Dot(du, du);
            float b = Vector3.Dot(du, dv);
            float c = Vector3.Dot(dv, dv);
            float d = Vector3.Dot(du, error);
            float e = Vector3.Dot(dv, error);

            float det = a * c - b * b;

            if (Mathf.Abs(det) < 0.000001f)
                break;

            float deltaU = (c * d - b * e) / det;
            float deltaV = (a * e - b * d) / det;

            u -= deltaU;
            v -= deltaV;

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }

        uv = new Vector2(u, v);
        return true;
    }

    private Vector3 Bilinear(Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01, float u, float v)
    {
        Vector3 bas = Vector3.Lerp(p00, p10, u);
        Vector3 haut = Vector3.Lerp(p01, p11, u);
        return Vector3.Lerp(bas, haut, v);
    }

    public Bounds GetWorldBounds()
    {
        Vector3[] points =
        {
            transform.TransformPoint(bottomLeft),
            transform.TransformPoint(bottomRight),
            transform.TransformPoint(topRight),
            transform.TransformPoint(topLeft)
        };

        Bounds bounds = new Bounds(points[0], Vector3.zero);

        for (int i = 1; i < points.Length; i++)
            bounds.Encapsulate(points[i]);

        return bounds;
    }

    private void OnDrawGizmos()
    {
        if (!afficherGizmos) return;

        Gizmos.color = Color.yellow;

        Vector3 p00 = transform.TransformPoint(bottomLeft);
        Vector3 p10 = transform.TransformPoint(bottomRight);
        Vector3 p11 = transform.TransformPoint(topRight);
        Vector3 p01 = transform.TransformPoint(topLeft);

        Gizmos.DrawLine(p00, p10);
        Gizmos.DrawLine(p10, p11);
        Gizmos.DrawLine(p11, p01);
        Gizmos.DrawLine(p01, p00);
    }
}