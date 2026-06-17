using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Génère une capture PNG propre pour chaque AOI.
/// Les captures sont utilisées ensuite par le script Python pour superposer les heatmaps.
/// </summary>
public class AOICaptureExporter : MonoBehaviour
{
    #region Inspector

    [Header("Export")]
    [SerializeField] private int resolutionBase = 1024; // Taille maximale de la capture sur son plus grand côté.
    [SerializeField] private float distanceCamera = 1f; // Distance de recul de la caméra temporaire par rapport au centre de l'AOI.
    [SerializeField] private float marge = 1.05f; // Marge de cadrage autour de l'AOI pour éviter les bords coupés.

    [Header("Caméra")]
    [SerializeField] private Color couleurFond = Color.black; // Couleur de fond de la capture lorsque rien n'est rendu.
    [SerializeField, Range(0.01f, 0.99f)] private float nearClipRatio = 0.8f; // Ratio appliqué à distanceCamera pour couper les objets parasites proches.

    #endregion

    #region API publique

    public void ExporterToutesLesCaptures(string dossierExport)
    {
        Directory.CreateDirectory(dossierExport);

        foreach (AOI_QuadZone zone in FindObjectsOfType<AOI_QuadZone>())
            ExporterCapture(zone, dossierExport);
    }

    #endregion

    #region Capture

    private void ExporterCapture(AOI_QuadZone zone, string dossierExport)
    {
        CalculerGeometrieZone(zone, out Vector3 centre, out Vector3 normale, out Vector3 haut, out float largeur, out float hauteur);

        largeur *= marge;
        hauteur = Mathf.Max(hauteur * marge, 0.0001f);

        CalculerResolution(largeur, hauteur, out int largeurTexture, out int hauteurTexture);

        Camera cameraCapture = CreerCameraCapture(zone.AoiId, centre, normale, haut, hauteur);
        RenderTexture rendu = new RenderTexture(largeurTexture, hauteurTexture, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = new Texture2D(largeurTexture, hauteurTexture, TextureFormat.RGBA32, false);

        RenderTexture renduPrecedent = RenderTexture.active;
        RenderTexture ciblePrecedente = cameraCapture.targetTexture;

        List<Behaviour> composantsMasques = new List<Behaviour>();
        List<Renderer> renderersMasques = new List<Renderer>();
        List<LineRenderer> lignesMasquees = new List<LineRenderer>();

        try
        {
            cameraCapture.targetTexture = rendu;
            RenderTexture.active = rendu;

            MasquerElementsParasites(zone, composantsMasques, renderersMasques, lignesMasquees);

            cameraCapture.Render();

            texture.ReadPixels(new Rect(0, 0, largeurTexture, hauteurTexture), 0, 0);
            texture.Apply();

            string nomFichier =
                "aoi_capture_" +
                NettoyerNomFichier(zone.AoiId) + "_" +
                NettoyerNomFichier(zone.gameObject.name) + "_" +
                largeurTexture + "x" + hauteurTexture + ".png";

            string chemin = Path.Combine(dossierExport, nomFichier);
            File.WriteAllBytes(chemin, texture.EncodeToPNG());

            DebugManager.Instance?.Log("[AOICaptureExporter] Capture AOI : " + zone.AoiId);
        }
        finally
        {
            RestaurerElementsMasques(composantsMasques, renderersMasques, lignesMasquees);

            cameraCapture.targetTexture = ciblePrecedente;
            RenderTexture.active = renduPrecedent;

            rendu.Release();
            Destroy(texture);
            Destroy(rendu);
            Destroy(cameraCapture.gameObject);
        }
    }

    private Camera CreerCameraCapture(string aoiId, Vector3 centre, Vector3 normale, Vector3 haut, float hauteurCapture)
    {
        GameObject objetCamera = new GameObject("Temp_AOI_Capture_Camera_" + aoiId);
        Camera cameraCapture = objetCamera.AddComponent<Camera>();

        cameraCapture.orthographic = true;
        cameraCapture.orthographicSize = hauteurCapture / 2f;
        cameraCapture.clearFlags = CameraClearFlags.SolidColor;
        cameraCapture.backgroundColor = couleurFond;
        cameraCapture.nearClipPlane = Mathf.Max(0.01f, distanceCamera * nearClipRatio);
        cameraCapture.farClipPlane = distanceCamera + 20f;
        cameraCapture.enabled = false;
        cameraCapture.cullingMask = ~0;

        cameraCapture.transform.position = centre - normale.normalized * distanceCamera;
        cameraCapture.transform.rotation = Quaternion.LookRotation(normale.normalized, haut.normalized);

        return cameraCapture;
    }

    #endregion

    #region Géométrie AOI

    private static void CalculerGeometrieZone(AOI_QuadZone zone, out Vector3 centre, out Vector3 normale, out Vector3 haut, out float largeur, out float hauteur)
    {
        Vector3 p00 = zone.transform.TransformPoint(zone.BottomLeft);
        Vector3 p10 = zone.transform.TransformPoint(zone.BottomRight);
        Vector3 p11 = zone.transform.TransformPoint(zone.TopRight);
        Vector3 p01 = zone.transform.TransformPoint(zone.TopLeft);

        centre = (p00 + p10 + p11 + p01) / 4f;

        Vector3 droite = ((p10 - p00) + (p11 - p01)).normalized;
        haut = ((p01 - p00) + (p11 - p10)).normalized;
        normale = Vector3.Cross(droite, haut).normalized;

        if (normale == Vector3.zero)
            normale = zone.transform.forward;

        largeur = Mathf.Max(Vector3.Distance(p00, p10), Vector3.Distance(p01, p11));
        hauteur = Mathf.Max(Vector3.Distance(p00, p01), Vector3.Distance(p10, p11));
    }

    private void CalculerResolution(float largeur, float hauteur, out int largeurTexture, out int hauteurTexture)
    {
        float ratio = largeur / Mathf.Max(hauteur, 0.0001f);

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
    }

    #endregion

    #region Masquage temporaire

    private void MasquerElementsParasites(
        AOI_QuadZone zone,
        List<Behaviour> composantsMasques,
        List<Renderer> renderersMasques,
        List<LineRenderer> lignesMasquees)
    {
        // Cache le mesh visuel de l'AOI pour capturer uniquement le fond derrière.
        foreach (Renderer rendererAoi in zone.GetComponentsInChildren<Renderer>(true))
            MasquerRenderer(rendererAoi, renderersMasques);

        // Cache les rayons XR des manettes.
        foreach (LineRenderer ligne in FindObjectsOfType<LineRenderer>(true))
        {
            if (ligne == null || !ligne.enabled)
                continue;

            ligne.enabled = false;
            lignesMasquees.Add(ligne);
        }

        // Cache les textes UI de debug/enregistrement sans supprimer les objets.
        foreach (Text texte in FindObjectsOfType<Text>(true))
            MasquerComposant(texte, composantsMasques);

        // Cache seulement les Canvas de debug/gaze/recording, pas tous les Canvas du cockpit.
        foreach (Canvas canvas in FindObjectsOfType<Canvas>(true))
        {
            if (!EstCanvasParasite(canvas))
                continue;

            MasquerComposant(canvas, composantsMasques);
        }
    }

    private static bool EstCanvasParasite(Canvas canvas)
    {
        if (canvas == null)
            return false;

        string nom = canvas.gameObject.name.ToLowerInvariant();

        return
            nom.Contains("debug") ||
            nom.Contains("gaze") ||
            nom.Contains("record") ||
            nom.Contains("recording");
    }

    private static void MasquerComposant(Behaviour composant, List<Behaviour> composantsMasques)
    {
        if (composant == null || !composant.enabled)
            return;

        composant.enabled = false;
        composantsMasques.Add(composant);
    }

    private static void MasquerRenderer(Renderer renderer, List<Renderer> renderersMasques)
    {
        if (renderer == null || !renderer.enabled)
            return;

        renderer.enabled = false;
        renderersMasques.Add(renderer);
    }

    private static void RestaurerElementsMasques(
        List<Behaviour> composantsMasques,
        List<Renderer> renderersMasques,
        List<LineRenderer> lignesMasquees)
    {
        foreach (Behaviour composant in composantsMasques)
        {
            if (composant != null)
                composant.enabled = true;
        }

        foreach (Renderer renderer in renderersMasques)
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        foreach (LineRenderer ligne in lignesMasquees)
        {
            if (ligne != null)
                ligne.enabled = true;
        }
    }

    #endregion

    #region Utilitaires

    private static string NettoyerNomFichier(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom))
            return "sans_nom";

        foreach (char caractere in Path.GetInvalidFileNameChars())
            nom = nom.Replace(caractere.ToString(), "_");

        return nom.Replace(" ", "_");
    }

    #endregion
}
