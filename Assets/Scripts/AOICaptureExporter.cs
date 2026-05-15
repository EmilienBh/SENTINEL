using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class AOICaptureExporter : MonoBehaviour
{
    [Header("Export")]
    [SerializeField] private int baseResolution = 1024;
    [SerializeField] private float cameraDistance = 1.0f;
    [SerializeField] private float margin = 1.05f;

    [Header("Camera")]
    [SerializeField] private LayerMask captureMask = ~0;
    [SerializeField] private Color backgroundColor = Color.gray;

    [Header("Debug")]
    [SerializeField] private bool logDetails = true;

    public void ExporterToutesLesCaptures(string exportFolder)
    {
        Directory.CreateDirectory(exportFolder);

        AOI_Zone[] rectZones = FindObjectsOfType<AOI_Zone>();
        AOI_QuadZone[] quadZones = FindObjectsOfType<AOI_QuadZone>();

        foreach (AOI_Zone zone in rectZones)
            ExporterCaptureRect(zone, exportFolder);

        foreach (AOI_QuadZone zone in quadZones)
            ExporterCaptureQuad(zone, exportFolder);

        Debug.Log("[AOICaptureExporter] Captures exportées dans : " + exportFolder);
    }

    private void ExporterCaptureRect(AOI_Zone zone, string exportFolder)
    {
        Bounds bounds = CalculerBounds(zone.gameObject);

        Vector3 center = bounds.center;
        Vector3 normal = zone.transform.forward;
        Vector3 up = zone.transform.up;
        Vector3 right = zone.transform.right;

        float width = Mathf.Abs(Vector3.Dot(bounds.size, AbsVector(right)));
        float height = Mathf.Abs(Vector3.Dot(bounds.size, AbsVector(up)));

        if (width <= 0.0001f) width = Mathf.Abs(zone.transform.lossyScale.x);
        if (height <= 0.0001f) height = Mathf.Abs(zone.transform.lossyScale.y);

        ExporterCapture(zone.AoiId, zone.gameObject.name, center, normal, up, width, height, exportFolder);
    }

    private void ExporterCaptureQuad(AOI_QuadZone zone, string exportFolder)
    {
        Vector3 p00 = zone.transform.TransformPoint(zone.bottomLeft);
        Vector3 p10 = zone.transform.TransformPoint(zone.bottomRight);
        Vector3 p11 = zone.transform.TransformPoint(zone.topRight);
        Vector3 p01 = zone.transform.TransformPoint(zone.topLeft);

        Vector3 center = (p00 + p10 + p11 + p01) / 4f;

        Vector3 right = ((p10 - p00) + (p11 - p01)).normalized;
        Vector3 up = ((p01 - p00) + (p11 - p10)).normalized;
        Vector3 normal = Vector3.Cross(right, up).normalized;

        if (normal == Vector3.zero)
            normal = zone.transform.forward;

        float width = Mathf.Max(Vector3.Distance(p00, p10), Vector3.Distance(p01, p11));
        float height = Mathf.Max(Vector3.Distance(p00, p01), Vector3.Distance(p10, p11));

        ExporterCapture(zone.AoiId, zone.gameObject.name, center, normal, up, width, height, exportFolder);
    }

    private void ExporterCapture(
        string aoiId,
        string objectName,
        Vector3 center,
        Vector3 normal,
        Vector3 up,
        float width,
        float height,
        string exportFolder)
    {
        width *= margin;
        height *= margin;

        if (height <= 0.0001f)
            height = 0.0001f;

        float ratio = width / height;

        int textureWidth;
        int textureHeight;

        if (ratio >= 1f)
        {
            textureWidth = baseResolution;
            textureHeight = Mathf.Max(1, Mathf.RoundToInt(baseResolution / ratio));
        }
        else
        {
            textureHeight = baseResolution;
            textureWidth = Mathf.Max(1, Mathf.RoundToInt(baseResolution * ratio));
        }

        GameObject camObj = new GameObject("Temp_AOI_Capture_Camera_" + aoiId);
        Camera cam = camObj.AddComponent<Camera>();

        cam.orthographic = true;
        cam.orthographicSize = height / 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.cullingMask = captureMask;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = cameraDistance + 10f;
        cam.enabled = false;

        Vector3 cameraPosition = center - normal.normalized * cameraDistance;
        cam.transform.position = cameraPosition;
        cam.transform.rotation = Quaternion.LookRotation(normal.normalized, up.normalized);

        RenderTexture rt = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 8;

        Texture2D tex = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = cam.targetTexture;

        cam.targetTexture = rt;
        RenderTexture.active = rt;

        cam.Render();

        tex.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
        tex.Apply();

        cam.targetTexture = previousTarget;
        RenderTexture.active = previousActive;

        byte[] png = tex.EncodeToPNG();

        string safeId = NettoyerNomFichier(aoiId);
        string safeObj = NettoyerNomFichier(objectName);

        string fileName =
            "aoi_capture_" +
            safeId + "_" +
            safeObj + "_" +
            textureWidth + "x" + textureHeight +
            ".png";

        string path = Path.Combine(exportFolder, fileName);
        File.WriteAllBytes(path, png);

        rt.Release();

        DestroyImmediate(tex);
        DestroyImmediate(rt);
        DestroyImmediate(camObj);

        if (logDetails)
        {
            Debug.Log(
                "[AOICaptureExporter] " +
                aoiId +
                " | " +
                textureWidth + "x" + textureHeight +
                " | ratio=" +
                ratio.ToString("0.000", CultureInfo.InvariantCulture) +
                " | path=" +
                path
            );
        }
    }

    private Bounds CalculerBounds(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();

        if (col == null)
            col = obj.GetComponentInChildren<Collider>();

        if (col != null)
            return col.bounds;

        Renderer renderer = obj.GetComponent<Renderer>();

        if (renderer == null)
            renderer = obj.GetComponentInChildren<Renderer>();

        if (renderer != null)
            return renderer.bounds;

        return new Bounds(obj.transform.position, obj.transform.lossyScale);
    }

    private static Vector3 AbsVector(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static string NettoyerNomFichier(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "AOI";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value.Replace(" ", "_").Replace(",", "_");
    }
}