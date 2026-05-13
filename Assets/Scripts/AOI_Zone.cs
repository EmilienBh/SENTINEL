using System;
using System.IO;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshCollider))]
public class AOI_Zone : MonoBehaviour
{
    [Header("Identite")]
    [SerializeField] private string aoiId = "AOI";

    [Header("Camera ortho")]
    [SerializeField] private Camera cameraOrtho;
    [SerializeField] private bool creerCameraSiAbsente = true;
    [SerializeField] private float distanceCamera = 1f;
    [SerializeField] private int pixelsParUnite = 512;
    [SerializeField] private LayerMask masqueCamera = ~0;
    [SerializeField] private Color couleurFond = new Color(0f, 0f, 0f, 0f);

    [Header("Heatmap")]
    [SerializeField] private int largeurGrille = 128;
    [SerializeField] private int hauteurGrille = 128;
    [SerializeField, Range(0f, 1f)] private float opaciteHeatmap = 0.55f;
    [SerializeField] private Gradient degradeChaleur;

    private int[,] grille;
    private int maxCase;

    private RenderTexture renderTexture;
    private Texture2D textureFond;
    private Texture2D textureHeatmap;
    private Texture2D textureOverlay;

    private Vector3 derniereScale;
    private int largeurCapture;
    private int hauteurCapture;

    public string AoiId => aoiId;

    private void Reset()
    {
        if (degradeChaleur == null || degradeChaleur.colorKeys.Length == 0)
            degradeChaleur = CreerDegradeParDefaut();
    }

    private void OnEnable()
    {
        if (degradeChaleur == null || degradeChaleur.colorKeys.Length == 0)
            degradeChaleur = CreerDegradeParDefaut();

        AssurerCamera();
        AssurerGrille();
        SynchroniserCameraEtCapture();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            AssurerCamera();
            AssurerGrille();

            if (transform.lossyScale != derniereScale)
                SynchroniserCameraEtCapture();
        }
    }

    private void AssurerCamera()
    {
        if (cameraOrtho != null)
            return;

        Transform enfant = transform.Find("AOI_OrthoCamera");
        if (enfant != null)
            cameraOrtho = enfant.GetComponent<Camera>();

        if (cameraOrtho == null && creerCameraSiAbsente)
        {
            GameObject go = new GameObject("AOI_OrthoCamera");
            go.transform.SetParent(transform, false);
            cameraOrtho = go.AddComponent<Camera>();
        }
    }

    private void AssurerGrille()
    {
        if (grille == null || grille.GetLength(0) != largeurGrille || grille.GetLength(1) != hauteurGrille)
        {
            grille = new int[Mathf.Max(1, largeurGrille), Mathf.Max(1, hauteurGrille)];
            maxCase = 0;
        }
    }

    private void SynchroniserCameraEtCapture()
    {
        derniereScale = transform.lossyScale;

        float largeurMonde = Mathf.Abs(transform.lossyScale.x);
        float hauteurMonde = Mathf.Abs(transform.lossyScale.y);

        largeurMonde = Mathf.Max(0.001f, largeurMonde);
        hauteurMonde = Mathf.Max(0.001f, hauteurMonde);

        largeurCapture = Mathf.Max(64, Mathf.RoundToInt(largeurMonde * pixelsParUnite));
        hauteurCapture = Mathf.Max(64, Mathf.RoundToInt(hauteurMonde * pixelsParUnite));

        if (cameraOrtho != null)
        {
            cameraOrtho.orthographic = true;
            cameraOrtho.orthographicSize = hauteurMonde * 0.5f;
            cameraOrtho.nearClipPlane = 0.01f;
            cameraOrtho.farClipPlane = Mathf.Max(2f, distanceCamera * 4f);
            cameraOrtho.clearFlags = CameraClearFlags.SolidColor;
            cameraOrtho.backgroundColor = couleurFond;
            cameraOrtho.cullingMask = masqueCamera;
            cameraOrtho.enabled = false;

            cameraOrtho.transform.localPosition = new Vector3(0f, 0f, -distanceCamera);
            cameraOrtho.transform.localRotation = Quaternion.identity;
        }

        if (renderTexture != null)
        {
            if (cameraOrtho != null)
                cameraOrtho.targetTexture = null;

            renderTexture.Release();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(renderTexture);
            else
                Destroy(renderTexture);
#else
            Destroy(renderTexture);
#endif
        }

        renderTexture = new RenderTexture(largeurCapture, hauteurCapture, 24);
        renderTexture.Create();

        if (cameraOrtho != null)
            cameraOrtho.targetTexture = renderTexture;
    }

    public void AjouterPointUV(Vector2 uv)
    {
        if (grille == null)
            return;

        int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(uv.x) * largeurGrille), 0, largeurGrille - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(uv.y) * hauteurGrille), 0, hauteurGrille - 1);

        grille[x, y]++;

        if (grille[x, y] > maxCase)
            maxCase = grille[x, y];
    }

    public void ReinitialiserHeatmap()
    {
        grille = new int[Mathf.Max(1, largeurGrille), Mathf.Max(1, hauteurGrille)];
        maxCase = 0;
    }

    public void ExporterImages()
    {
        CapturerFond();
        GenererHeatmap();
        GenererOverlay();
        Sauvegarder();
    }

    private void CapturerFond()
    {
        if (cameraOrtho == null || renderTexture == null)
            return;

        RenderTexture activeAvant = RenderTexture.active;
        RenderTexture.active = renderTexture;

        cameraOrtho.Render();

        if (textureFond == null || textureFond.width != largeurCapture || textureFond.height != hauteurCapture)
            textureFond = new Texture2D(largeurCapture, hauteurCapture, TextureFormat.RGBA32, false);

        textureFond.ReadPixels(new Rect(0, 0, largeurCapture, hauteurCapture), 0, 0);
        textureFond.Apply();

        RenderTexture.active = activeAvant;
    }

    private void GenererHeatmap()
    {
        if (textureHeatmap == null || textureHeatmap.width != largeurCapture || textureHeatmap.height != hauteurCapture)
            textureHeatmap = new Texture2D(largeurCapture, hauteurCapture, TextureFormat.RGBA32, false);

        for (int y = 0; y < hauteurCapture; y++)
        {
            for (int x = 0; x < largeurCapture; x++)
            {
                int gx = Mathf.Clamp(Mathf.FloorToInt((float)x / largeurCapture * largeurGrille), 0, largeurGrille - 1);
                int gy = Mathf.Clamp(Mathf.FloorToInt((float)y / hauteurCapture * hauteurGrille), 0, hauteurGrille - 1);

                float t = maxCase > 0 ? (float)grille[gx, gy] / maxCase : 0f;

                Color c = degradeChaleur.Evaluate(t);
                c.a = t * opaciteHeatmap;

                textureHeatmap.SetPixel(x, y, c);
            }
        }

        textureHeatmap.Apply();
    }

    private void GenererOverlay()
    {
        if (textureFond == null)
            CapturerFond();

        if (textureFond == null || textureHeatmap == null)
            return;

        if (textureOverlay == null || textureOverlay.width != largeurCapture || textureOverlay.height != hauteurCapture)
            textureOverlay = new Texture2D(largeurCapture, hauteurCapture, TextureFormat.RGBA32, false);

        for (int y = 0; y < hauteurCapture; y++)
        {
            for (int x = 0; x < largeurCapture; x++)
            {
                Color fond = textureFond.GetPixel(x, y);
                Color chaud = textureHeatmap.GetPixel(x, y);

                Color final = Color.Lerp(fond, new Color(chaud.r, chaud.g, chaud.b, 1f), chaud.a);
                final.a = 1f;

                textureOverlay.SetPixel(x, y, final);
            }
        }

        textureOverlay.Apply();
    }

    private void Sauvegarder()
    {
        string dossier = Path.Combine(Application.persistentDataPath, "AOI_Heatmaps");
        Directory.CreateDirectory(dossier);

        string baseNom = aoiId + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (textureFond != null)
            File.WriteAllBytes(Path.Combine(dossier, baseNom + "_fond.png"), textureFond.EncodeToPNG());

        if (textureHeatmap != null)
            File.WriteAllBytes(Path.Combine(dossier, baseNom + "_heatmap.png"), textureHeatmap.EncodeToPNG());

        if (textureOverlay != null)
            File.WriteAllBytes(Path.Combine(dossier, baseNom + "_overlay.png"), textureOverlay.EncodeToPNG());

        Debug.Log("[AOI_Zone] Export termine : " + dossier);
    }

    private Gradient CreerDegradeParDefaut()
    {
        Gradient g = new Gradient();

        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0f, 0f, 0f), 0f),
                new GradientColorKey(Color.blue, 0.25f),
                new GradientColorKey(Color.cyan, 0.5f),
                new GradientColorKey(Color.yellow, 0.75f),
                new GradientColorKey(Color.red, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return g;
    }
}