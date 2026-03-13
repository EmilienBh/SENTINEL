using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

public class GazeManager : MonoBehaviour
{
    public Transform origineXR;
    public Camera cameraCasque;
    public LayerMask masqueCollision = ~0;
    public float distanceMax = 30f;
    public float lissage = 10f;
    public float zoneMortePixels = 12f;

    RectTransform pointRegard;
    RectTransform zoneMorte;
    RectTransform rectCanvas;

    Vector2 positionPoint;
    Vector2 positionVoulue;
    Vector2 positionLocale;

    Matrix4x4 poseTete;
    Vector3 vecteurRegardLocal;
    Vector3 pointRegardLocal;
    Matrix4x4 matriceOrigine;
    Vector3 origineRegardMonde;
    Vector3 directionRegardMonde;
    Vector3 cible;
    Vector3 positionEcran;

    Texture2D texturePoint;
    float rayon;
    Vector2 centre;
    float distancePixel;

    bool premierPoint = true;

    void Awake()
    {
        if (!cameraCasque)
            cameraCasque = Camera.main;

        if (!origineXR)
        {
            var xro = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (xro) origineXR = xro.transform;
        }

        GameObject canvasGO = new GameObject("GazeCanvas", typeof(Canvas));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cameraCasque;
        canvas.planeDistance = 0.5f;
        rectCanvas = canvas.GetComponent<RectTransform>();

        GameObject zoneGO = new GameObject("ZoneMorte", typeof(Image));
        zoneGO.transform.SetParent(canvasGO.transform, false);
        zoneMorte = zoneGO.GetComponent<RectTransform>();
        zoneMorte.sizeDelta = new Vector2(zoneMortePixels * 2f, zoneMortePixels * 2f);

        Image imageZone = zoneGO.GetComponent<Image>();
        imageZone.sprite = CreerSpritePoint(64, new Color(1f, 0f, 0f, 0.12f));
        imageZone.raycastTarget = false;

        GameObject pointGO = new GameObject("PointRegard", typeof(Image));
        pointGO.transform.SetParent(canvasGO.transform, false);
        pointRegard = pointGO.GetComponent<RectTransform>();
        pointRegard.sizeDelta = new Vector2(12f, 12f);

        Image imagePoint = pointGO.GetComponent<Image>();
        imagePoint.sprite = CreerSpritePoint(32, Color.red);
        imagePoint.raycastTarget = false;
    }

    void LateUpdate()
    {
        if (!origineXR || !cameraCasque || !pointRegard)
            return;

        if (!PXR_EyeTracking.GetHeadPosMatrix(out poseTete))
            return;

        if (!PXR_EyeTracking.GetCombineEyeGazeVector(out vecteurRegardLocal))
            return;

        if (!PXR_EyeTracking.GetCombineEyeGazePoint(out pointRegardLocal))
            return;

        matriceOrigine = origineXR.localToWorldMatrix;

        origineRegardMonde = matriceOrigine.MultiplyPoint(poseTete.MultiplyPoint(pointRegardLocal));
        directionRegardMonde = matriceOrigine.MultiplyVector(poseTete.MultiplyVector(vecteurRegardLocal)).normalized;

        if (Physics.Raycast(origineRegardMonde, directionRegardMonde, out RaycastHit hit, distanceMax, masqueCollision))
            cible = hit.point;
        else
            cible = origineRegardMonde + directionRegardMonde * distanceMax;

        positionEcran = cameraCasque.WorldToScreenPoint(cible);

        if (positionEcran.z <= 0f)
            return;

        positionVoulue = new Vector2(positionEcran.x, positionEcran.y);

        if (premierPoint)
        {
            positionPoint = positionVoulue;
            premierPoint = false;
        }
        else
        {
            if (Vector2.Distance(positionPoint, positionVoulue) > zoneMortePixels)
                positionPoint = Vector2.Lerp(positionPoint, positionVoulue, lissage * Time.unscaledDeltaTime);
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectCanvas, positionPoint, cameraCasque, out positionLocale);

        pointRegard.anchoredPosition = positionLocale;
        zoneMorte.anchoredPosition = positionLocale;
        zoneMorte.sizeDelta = new Vector2(zoneMortePixels * 2f, zoneMortePixels * 2f);
    }

    Sprite CreerSpritePoint(int taille, Color couleur)
    {
        texturePoint = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
        rayon = taille * 0.5f;
        centre = new Vector2(rayon, rayon);

        for (int y = 0; y < taille; y++)
        {
            for (int x = 0; x < taille; x++)
            {
                distancePixel = Vector2.Distance(new Vector2(x, y), centre);
                texturePoint.SetPixel(x, y, distancePixel <= rayon - 1 ? couleur : Color.clear);
            }
        }

        texturePoint.Apply();
        return Sprite.Create(texturePoint, new Rect(0, 0, taille, taille), new Vector2(0.5f, 0.5f));
    }
}