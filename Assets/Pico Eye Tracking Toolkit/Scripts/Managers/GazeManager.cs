using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

/// <summary>
/// Centralise la lecture du regard Pico.
/// Expose une origine et une direction monde pour les raycasts AOI et les exports CSV.
/// Peut afficher un point de regard UI optionnel, uniquement pour le debug visuel.
/// </summary>
public class GazeManager : MonoBehaviour
{
    #region Inspector

    [Header("Références")]
    [SerializeField] private Transform cameraCasque; // Transform de la caméra du casque XR. Si vide, Camera.main est utilisée.

    [Header("Tracking")]
    [SerializeField] private bool ignorerStatutPose = true; // Ignore le statut Pico si celui-ci est instable mais que le vecteur regard est disponible.
    [SerializeField] private float distanceMax = 30f; // Distance utilisée pour construire une cible monde théorique du regard.

    [Header("Point du regard debug")]
    [SerializeField] private bool afficherPointRegard = false; // Active ou désactive le point visuel de debug.
    [SerializeField] private float taillePoint = 12f; // Diamètre du point de regard en pixels UI.
    [SerializeField] private Color couleurPoint = Color.red; // Couleur du point de regard.
    [SerializeField] private bool afficherZoneMorte = true; // Affiche le cercle représentant la zone morte de stabilisation.
    [SerializeField] private Color couleurZoneMorte = new Color(1f, 0f, 0f, 0.15f); // Couleur du cercle de zone morte.
    [SerializeField] private float lissage = 10f; // Vitesse de lissage du point de regard.
    [SerializeField] private float zoneMortePixels = 12f; // Distance minimale en pixels avant de déplacer visuellement le point.

    [Header("Canvas du point")]
    [SerializeField] private float planeDistance = 0.4f; // Distance du Canvas devant la caméra. 0.3-0.5 évite souvent le passage derrière les AOI sans double vision.
    [SerializeField] private int sortingOrder = 9999; // Priorité d'affichage du Canvas de debug gaze point.

    #endregion

    #region Propriétés publiques

    public bool RegardValide { get; private set; }
    public Vector3 OrigineRegardMonde { get; private set; }
    public Vector3 DirectionRegardMonde { get; private set; } = Vector3.forward;
    public Vector3 DirectionRegardLocale { get; private set; } = Vector3.forward;
    public Vector3 CibleRegardMonde => OrigineRegardMonde + DirectionRegardMonde * distanceMax;

    #endregion

    #region Variables privées

    private Camera cameraXR;
    private Canvas canvasPoint;
    private RectTransform rectCanvas;
    private RectTransform pointRegard;
    private RectTransform zoneMorte;
    private Vector2 positionLisse;
    private bool pointInitialise;

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        InitialiserCamera();
        CreerAffichagePoint();
    }

    private void Update()
    {
        ActualiserRegard();
    }

    private void LateUpdate()
    {
        ActualiserPointRegard();
    }

    #endregion

    #region Tracking regard

    private void InitialiserCamera()
    {
        if (cameraCasque == null && Camera.main != null)
            cameraCasque = Camera.main.transform;

        if (cameraCasque != null)
            cameraXR = cameraCasque.GetComponent<Camera>();

        if (cameraXR == null)
            cameraXR = Camera.main;
    }

    private void ActualiserRegard()
    {
        if (cameraCasque == null)
            InitialiserCamera();

        if (cameraCasque == null)
        {
            RegardValide = false;
            OrigineRegardMonde = Vector3.zero;
            DirectionRegardLocale = Vector3.forward;
            DirectionRegardMonde = Vector3.forward;
            return;
        }

        bool vecteurOk = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 directionLocale);
        bool statutOk = PXR_EyeTracking.GetCombinedEyePoseStatus(out uint statutPose);
        bool statutValide = ignorerStatutPose || (statutOk && statutPose != 0);

        RegardValide = vecteurOk && statutValide && directionLocale.sqrMagnitude > 0.000001f;
        OrigineRegardMonde = cameraCasque.position;

        if (!RegardValide)
        {
            DirectionRegardLocale = Vector3.forward;
            DirectionRegardMonde = cameraCasque.forward;
            return;
        }

        DirectionRegardLocale = directionLocale.normalized;
        DirectionRegardMonde = cameraCasque.TransformDirection(DirectionRegardLocale).normalized;
    }

    #endregion

    #region Point du regard debug

    private void CreerAffichagePoint()
    {
        if (!afficherPointRegard || cameraXR == null)
            return;

        GameObject objetCanvas = new GameObject("Canvas_GazePoint");
        canvasPoint = objetCanvas.AddComponent<Canvas>();
        canvasPoint.renderMode = RenderMode.ScreenSpaceCamera;
        canvasPoint.worldCamera = cameraXR;
        canvasPoint.planeDistance = planeDistance;
        canvasPoint.overrideSorting = true;
        canvasPoint.sortingOrder = sortingOrder;

        objetCanvas.AddComponent<CanvasScaler>();
        objetCanvas.AddComponent<GraphicRaycaster>();

        rectCanvas = canvasPoint.GetComponent<RectTransform>();

        zoneMorte = CreerCercleUI("Zone_Morte_Regard", zoneMortePixels * 2f, couleurZoneMorte);
        pointRegard = CreerCercleUI("Point_Regard", taillePoint, couleurPoint);

        zoneMorte.gameObject.SetActive(false);
        pointRegard.gameObject.SetActive(false);
    }

    private RectTransform CreerCercleUI(string nom, float taille, Color couleur)
    {
        GameObject objet = new GameObject(nom);
        objet.transform.SetParent(canvasPoint.transform, false);

        RectTransform rect = objet.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(taille, taille);

        GazeCircleGraphic cercle = objet.AddComponent<GazeCircleGraphic>();
        cercle.color = couleur;
        cercle.raycastTarget = false;

        return rect;
    }

    private void ActualiserPointRegard()
    {
        if (!afficherPointRegard || pointRegard == null || rectCanvas == null || cameraXR == null)
        {
            DesactiverPoint();
            return;
        }

        if (!RegardValide)
        {
            DesactiverPoint();
            return;
        }

        Vector3 directionLocale = DirectionRegardLocale.normalized;

        if (directionLocale.z <= 0.0001f)
        {
            DesactiverPoint();
            return;
        }

        float distancePlan = Mathf.Max(canvasPoint.planeDistance, 0.0001f);
        float x = (directionLocale.x / directionLocale.z) * distancePlan;
        float y = (directionLocale.y / directionLocale.z) * distancePlan;

        float facteurPixels =
            rectCanvas.rect.height /
            (2f * distancePlan * Mathf.Tan(cameraXR.fieldOfView * 0.5f * Mathf.Deg2Rad));

        Vector2 ciblePixels = new Vector2(x, y) * facteurPixels;

        if (!pointInitialise)
        {
            positionLisse = ciblePixels;
            pointInitialise = true;
        }

        if (Vector2.Distance(positionLisse, ciblePixels) > zoneMortePixels)
        {
            positionLisse = Vector2.Lerp(positionLisse, ciblePixels, Time.deltaTime * lissage);
        }

        pointRegard.anchoredPosition = positionLisse;
        pointRegard.gameObject.SetActive(true);

        if (zoneMorte != null)
        {
            zoneMorte.anchoredPosition = positionLisse;
            zoneMorte.gameObject.SetActive(afficherZoneMorte);
        }
    }

    private void DesactiverPoint()
    {
        pointInitialise = false;

        if (pointRegard != null)
            pointRegard.gameObject.SetActive(false);

        if (zoneMorte != null)
            zoneMorte.gameObject.SetActive(false);
    }

    #endregion
}

/// <summary>
/// Dessine un cercle UI sans image ni sprite externe.
/// Utilisé uniquement par le point de regard et la zone morte visuelle.
/// </summary>
public class GazeCircleGraphic : Graphic
{
    [SerializeField] private int segments = 32; // Nombre de segments du cercle. 32 suffit pour un rond propre en VR.

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float rayon = rectTransform.rect.width * 0.5f;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = Vector2.zero;
        vh.AddVert(vertex);

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rayon;
            vh.AddVert(vertex);
        }

        for (int i = 1; i <= segments; i++)
            vh.AddTriangle(0, i, i + 1);
    }
}
