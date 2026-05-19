using System.Globalization;
using System.IO;
using UnityEngine;

public class AOICaptureExporter : MonoBehaviour
{
    [Header("Export")]
    [SerializeField] private int resolutionBase = 1024;
    [SerializeField] private float distanceCamera = 1.0f;
    [SerializeField] private float marge = 1.05f;

    [Header("Caméra")]
    [SerializeField] private LayerMask masqueCapture = ~0;
    [SerializeField] private Color couleurFond = Color.black;

    public void ExporterToutesLesCaptures(string dossierExport)
    {
        Directory.CreateDirectory(dossierExport);

        AOI_QuadZone[] zones = FindObjectsOfType<AOI_QuadZone>();

        foreach (AOI_QuadZone zone in zones)
            ExporterCaptureQuad(zone, dossierExport);
    }

    private void ExporterCaptureQuad(AOI_QuadZone zone, string dossierExport)
    {
        Vector3 p00 = zone.transform.TransformPoint(zone.BottomLeft);
        Vector3 p10 = zone.transform.TransformPoint(zone.BottomRight);
        Vector3 p11 = zone.transform.TransformPoint(zone.TopRight);
        Vector3 p01 = zone.transform.TransformPoint(zone.TopLeft);

        Vector3 centre = (p00 + p10 + p11 + p01) / 4f;
        Vector3 droite = ((p10 - p00) + (p11 - p01)).normalized;
        Vector3 haut = ((p01 - p00) + (p11 - p10)).normalized;
        Vector3 normale = Vector3.Cross(droite, haut).normalized;

        if (normale == Vector3.zero)
            normale = zone.transform.forward;

        float largeur = Mathf.Max(Vector3.Distance(p00, p10), Vector3.Distance(p01, p11));
        float hauteur = Mathf.Max(Vector3.Distance(p00, p01), Vector3.Distance(p10, p11));

        ExporterCapture(zone.AoiId, zone.gameObject.name, centre, normale, haut, largeur, hauteur, dossierExport);
    }

    private void ExporterCapture(string aoiId, string nomObjet, Vector3 centre, Vector3 normale, Vector3 haut, float largeur, float hauteur, string dossierExport)
    {
        largeur *= marge;
        hauteur = Mathf.Max(hauteur * marge, 0.0001f);

        float ratio = largeur / hauteur;
        int largeurTexture;
        int hauteurTexture;

        if (ratio >= 1f)
        {
            largeurTexture = resolutionBase;
            hauteurTexture = Mathf.Max(1, Mathf.RoundToInt(resolutionBase / ratio));
        }
        else
        {
            hauteurTexture = resolutionBase;
            largeurTexture = Mathf.Max(1, Mathf.RoundToInt(resolutionBase * ratio));
        }

        GameObject objetCamera = new GameObject("Temp_AOI_Capture_Camera_" + aoiId);
        Camera cameraCapture = objetCamera.AddComponent<Camera>();

        cameraCapture.orthographic = true;
        cameraCapture.orthographicSize = hauteur / 2f;
        cameraCapture.clearFlags = CameraClearFlags.SolidColor;
        cameraCapture.backgroundColor = couleurFond;
        cameraCapture.cullingMask = masqueCapture;
        cameraCapture.nearClipPlane = 0.01f;
        cameraCapture.farClipPlane = distanceCamera + 10f;
        cameraCapture.enabled = false;

        cameraCapture.transform.position = centre - normale.normalized * distanceCamera;
        cameraCapture.transform.rotation = Quaternion.LookRotation(normale.normalized, haut.normalized);

        RenderTexture rendu = new RenderTexture(largeurTexture, hauteurTexture, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 8
        };

        Texture2D texture = new Texture2D(largeurTexture, hauteurTexture, TextureFormat.RGBA32, false);

        RenderTexture renduPrecedent = RenderTexture.active;
        RenderTexture ciblePrecedente = cameraCapture.targetTexture;

        cameraCapture.targetTexture = rendu;
        RenderTexture.active = rendu;
        cameraCapture.Render();

        texture.ReadPixels(new Rect(0, 0, largeurTexture, hauteurTexture), 0, 0);
        texture.Apply();

        cameraCapture.targetTexture = ciblePrecedente;
        RenderTexture.active = renduPrecedent;

        string nomFichier = "aoi_capture_" + NettoyerNomFichier(aoiId) + "_" + NettoyerNomFichier(nomObjet) + "_" + largeurTexture + "x" + hauteurTexture + ".png";
        string chemin = Path.Combine(dossierExport, nomFichier);

        File.WriteAllBytes(chemin, texture.EncodeToPNG());

        DebugManager.Instance?.Log("Capture AOI : " + aoiId + " | " + ratio.ToString("0.000", CultureInfo.InvariantCulture));

        rendu.Release();
        Destroy(texture);
        Destroy(rendu);
        Destroy(objetCamera);
    }

    private static string NettoyerNomFichier(string valeur)
    {
        if (string.IsNullOrEmpty(valeur))
            return "AOI";

        foreach (char caractere in Path.GetInvalidFileNameChars())
            valeur = valeur.Replace(caractere, '_');

        return valeur.Replace(" ", "_").Replace(",", "_");
    }
}
