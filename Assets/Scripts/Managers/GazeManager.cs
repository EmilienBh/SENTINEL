using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

public class GazeManager : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform origineXR;
    [SerializeField] private Camera cameraCasque;

    [Header("Raycast")]
    [SerializeField] private LayerMask masqueCollision = ~0;
    [SerializeField] private float distanceMax = 30f;

    [Header("Affichage")]
    [SerializeField] private bool afficherPointRegard = true;
    [SerializeField] private float lissage = 10f;
    [SerializeField] private float zoneMortePixels = 12f;

    public bool RegardValide { get; private set; }
    public Vector3 OrigineRegardMonde { get; private set; }
    public Vector3 DirectionRegardMonde { get; private set; }
    public Vector3 CibleRegardMonde { get; private set; }

    private RectTransform pointRegard;
    private RectTransform zoneMorte;
    private RectTransform rectCanvas;
    private Vector2 positionPoint;
    private bool premierPoint = true;

    private void Awake()
    {
        InitialiserReferences();

        if (afficherPointRegard)
            CreerAffichageRegard();
    }

    private void LateUpdate()
    {
        if (!ActualiserRegard())
            return;

        if (afficherPointRegard)
            ActualiserAffichageRegard();
    }

    private void InitialiserReferences()
    {
        if (cameraCasque == null)
            cameraCasque = Camera.main;

        if (origineXR == null)
        {
            Unity.XR.CoreUtils.XROrigin origine = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();

            if (origine != null)
                origineXR = origine.transform;
        }
    }

    private bool ActualiserRegard()
    {
        RegardValide = false;

        if (origineXR == null || cameraCasque == null)
            return false;

        bool okPose = PXR_EyeTracking.GetHeadPosMatrix(out Matrix4x4 poseTete);
        bool okVecteur = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 vecteurRegardLocal);
        bool okPoint = PXR_EyeTracking.GetCombineEyeGazePoint(out Vector3 pointRegardLocal);

        if (!okPose || !okVecteur || !okPoint)
            return false;

        Matrix4x4 matriceOrigine = origineXR.localToWorldMatrix;

        OrigineRegardMonde = matriceOrigine.MultiplyPoint(poseTete.MultiplyPoint(pointRegardLocal));
        DirectionRegardMonde = matriceOrigine.MultiplyVector(poseTete.MultiplyVector(vecteurRegardLocal)).normalized;

        if (Physics.Raycast(OrigineRegardMonde, DirectionRegardMonde, out RaycastHit hit, distanceMax, masqueCollision))
            CibleRegardMonde = hit.point;
        else
            CibleRegardMonde = OrigineRegardMonde + DirectionRegardMonde * distanceMax;

        RegardValide = true;
        return true;
    }

    private void CreerAffichageRegard()
    {
        if (cameraCasque == null)
            return;

        GameObject objetCanvas = new GameObject("GazeCanvas", typeof(Canvas));
        Canvas canvas = objetCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cameraCasque;
        canvas.planeDistance = 0.5f;
        rectCanvas = canvas.GetComponent<RectTransform>();

        GameObject objetZoneMorte = new GameObject("ZoneMorte", typeof(Image));
        objetZoneMorte.transform.SetParent(objetCanvas.transform, false);
        zoneMorte = objetZoneMorte.GetComponent<RectTransform>();
        zoneMorte.sizeDelta = new Vector2(zoneMortePixels * 2f, zoneMortePixels * 2f);

        Image imageZone = objetZoneMorte.GetComponent<Image>();
        imageZone.sprite = CreerSpriteCirculaire(64, new Color(1f, 0f, 0f, 0.12f));
        imageZone.raycastTarget = false;

        GameObject objetPoint = new GameObject("PointRegard", typeof(Image));
        objetPoint.transform.SetParent(objetCanvas.transform, false);
        pointRegard = objetPoint.GetComponent<RectTransform>();
        pointRegard.sizeDelta = new Vector2(12f, 12f);

        Image imagePoint = objetPoint.GetComponent<Image>();
        imagePoint.sprite = CreerSpriteCirculaire(32, Color.red);
        imagePoint.raycastTarget = false;
    }

    private void ActualiserAffichageRegard()
    {
        if (cameraCasque == null || pointRegard == null || zoneMorte == null || rectCanvas == null)
            return;

        Vector3 positionEcran = cameraCasque.WorldToScreenPoint(CibleRegardMonde);

        if (positionEcran.z <= 0f)
            return;

        Vector2 positionVoulue = new Vector2(positionEcran.x, positionEcran.y);

        if (premierPoint)
        {
            positionPoint = positionVoulue;
            premierPoint = false;
        }
        else if (Vector2.Distance(positionPoint, positionVoulue) > zoneMortePixels)
        {
            positionPoint = Vector2.Lerp(positionPoint, positionVoulue, lissage * Time.unscaledDeltaTime);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectCanvas, positionPoint, cameraCasque, out Vector2 positionLocale);

        pointRegard.anchoredPosition = positionLocale;
        zoneMorte.anchoredPosition = positionLocale;
        zoneMorte.sizeDelta = new Vector2(zoneMortePixels * 2f, zoneMortePixels * 2f);
    }

    private Sprite CreerSpriteCirculaire(int taille, Color couleur)
    {
        Texture2D texture = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
        float rayon = taille * 0.5f;
        Vector2 centre = new Vector2(rayon, rayon);

        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                float distancePixel = Vector2.Distance(new Vector2(x, y), centre);
                texture.SetPixel(x, y, distancePixel <= rayon - 1f ? couleur : Color.clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, taille, taille), new Vector2(0.5f, 0.5f));
    }
}
