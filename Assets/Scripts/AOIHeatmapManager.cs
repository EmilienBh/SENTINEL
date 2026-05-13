using UnityEngine;
using Unity.XR.PXR;

public class AOIHeatmapManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform origineXR;
    [SerializeField] private PerclosManager perclosManager;

    [Header("Raycast regard")]
    [SerializeField] private LayerMask masqueCollision = ~0;
    [SerializeField] private float distanceMax = 30f;

    [Header("Filtres")]
    [SerializeField] private bool ignorerSiYeuxFermes = true;
    [SerializeField] private bool ignorerSiTrackingInvalide = true;

    [Header("Debug")]
    [SerializeField] private bool afficherRayonDebug = false;

    private void Awake()
    {
        if (origineXR == null)
        {
            var xro = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (xro != null)
                origineXR = xro.transform;
        }

        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();
    }

    private void LateUpdate()
    {
        if (origineXR == null)
            return;

        if (ignorerSiYeuxFermes && perclosManager != null && perclosManager.YeuxFermes)
            return;

        bool okPose = PXR_EyeTracking.GetHeadPosMatrix(out Matrix4x4 poseTete);
        bool okVecteur = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 vecteurRegardLocal);
        bool okPoint = PXR_EyeTracking.GetCombineEyeGazePoint(out Vector3 pointRegardLocal);
        bool okStatut = PXR_EyeTracking.GetCombinedEyePoseStatus(out uint statut);

        if (!okPose || !okVecteur || !okPoint)
            return;

        if (ignorerSiTrackingInvalide && (!okStatut || statut != 1))
            return;

        Matrix4x4 matriceOrigine = origineXR.localToWorldMatrix;

        Vector3 origineRegardMonde = matriceOrigine.MultiplyPoint(poseTete.MultiplyPoint(pointRegardLocal));
        Vector3 directionRegardMonde = matriceOrigine.MultiplyVector(poseTete.MultiplyVector(vecteurRegardLocal)).normalized;

        if (afficherRayonDebug)
            Debug.DrawRay(origineRegardMonde, directionRegardMonde * distanceMax, Color.red);

        if (!Physics.Raycast(
                origineRegardMonde,
                directionRegardMonde,
                out RaycastHit hit,
                distanceMax,
                masqueCollision,
                QueryTriggerInteraction.Ignore))
            return;

        AOI_Zone zone = hit.collider.GetComponent<AOI_Zone>();

        if (zone == null)
            zone = hit.collider.GetComponentInParent<AOI_Zone>();

        if (zone == null)
            return;

        zone.AjouterPointUV(hit.textureCoord);
    }

    public void ExporterToutesLesZones()
    {
        AOI_Zone[] zones = FindObjectsOfType<AOI_Zone>();

        for (int i = 0; i < zones.Length; i++)
            zones[i].ExporterImages();
    }

    public void ReinitialiserToutesLesZones()
    {
        AOI_Zone[] zones = FindObjectsOfType<AOI_Zone>();

        for (int i = 0; i < zones.Length; i++)
            zones[i].ReinitialiserHeatmap();
    }
}