using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.XR.PXR;

public class AOIHeatmapManager : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform origineXR;
    [SerializeField] private PerclosManager perclosManager;
    [SerializeField] private AOICaptureExporter captureExporter;

    [Header("Raycast regard")]
    [SerializeField] private LayerMask masqueCollision = ~0;
    [SerializeField] private float distanceMax = 30f;

    [Header("Filtres")]
    [SerializeField] private bool ignorerSiYeuxFermes = true;
    [SerializeField] private bool ignorerSiTrackingInvalide = true;

    [Header("Enregistrement CSV AOI")]
    [SerializeField] private bool enregistrerAutomatiquement = false;
    [SerializeField] private string prefixeFichier = "aoi_heatmap_data";
    [SerializeField] private bool flushChaqueLigne = false;

    [Header("Debug")]
    [SerializeField] private bool afficherRayonDebug = false;

    public string DossierSession => dossierSession;
    public string NomAoiCourante => nomAoiCourante;
    public float DernierU => dernierU;
    public float DernierV => dernierV;
    public float DerniereDistance => derniereDistance;

    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    private StreamWriter writer;

    private bool enregistrementActif;
    private string cheminFichier;
    private string dossierSession;

    private string nomAoiCourante = "";
    private float dernierU = -1f;
    private float dernierV = -1f;
    private float derniereDistance = -1f;

    private void Awake()
    {
        if (origineXR == null)
        {
            Unity.XR.CoreUtils.XROrigin origine = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();

            if (origine != null)
                origineXR = origine.transform;
        }

        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();

        if (captureExporter == null)
            captureExporter = FindObjectOfType<AOICaptureExporter>();
    }

    private void Start()
    {
        if (enregistrerAutomatiquement)
            DemarrerEnregistrementAOI();
    }

    private void LateUpdate()
    {
        ReinitialiserAoiCourante();

        if (!enregistrementActif || writer == null)
            return;

        bool yeuxFermes = perclosManager != null && perclosManager.YeuxFermes;

        if (origineXR == null)
        {
            EcrireLigneSansAOI(false, yeuxFermes, "NO_XR_ORIGIN", Vector3.zero);
            return;
        }

        bool okPose = PXR_EyeTracking.GetHeadPosMatrix(out Matrix4x4 poseTete);
        bool okVecteur = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 vecteurRegardLocal);
        bool okPoint = PXR_EyeTracking.GetCombineEyeGazePoint(out Vector3 pointRegardLocal);
        bool okStatut = PXR_EyeTracking.GetCombinedEyePoseStatus(out uint statut);

        bool regardValide =
            okPose &&
            okVecteur &&
            okPoint &&
            okStatut &&
            statut != 0;

        if (ignorerSiYeuxFermes && yeuxFermes)
        {
            EcrireLigneSansAOI(regardValide, true, "EYES_CLOSED", Vector3.zero);
            return;
        }

        if (ignorerSiTrackingInvalide && !regardValide)
        {
            EcrireLigneSansAOI(false, yeuxFermes, "TRACKING_INVALID", Vector3.zero);
            return;
        }

        Matrix4x4 matriceOrigine = origineXR.localToWorldMatrix;

        Vector3 origineRegardMonde =
            matriceOrigine.MultiplyPoint(
                poseTete.MultiplyPoint(pointRegardLocal)
            );

        Vector3 directionRegardMonde =
            matriceOrigine.MultiplyVector(
                poseTete.MultiplyVector(vecteurRegardLocal)
            ).normalized;

        if (afficherRayonDebug)
            Debug.DrawRay(origineRegardMonde, directionRegardMonde * distanceMax, Color.red);

        if (!Physics.Raycast(
                origineRegardMonde,
                directionRegardMonde,
                out RaycastHit hit,
                distanceMax,
                masqueCollision,
                QueryTriggerInteraction.Collide))
        {
            EcrireLigneSansAOI(regardValide, yeuxFermes, "NO_HIT", directionRegardMonde);
            return;
        }

        AOI_QuadZone zone = hit.collider.GetComponent<AOI_QuadZone>();

        if (zone == null)
            zone = hit.collider.GetComponentInParent<AOI_QuadZone>();

        if (zone == null)
        {
            EcrireLigneSansAOI(regardValide, yeuxFermes, "NO_AOI", directionRegardMonde);
            return;
        }

        GererHitAoi(
            zone,
            hit,
            regardValide,
            yeuxFermes,
            directionRegardMonde
        );
    }

    public void DemarrerEnregistrementAOI()
    {
        if (enregistrementActif)
            return;

        if (RecordingSessionManager.Instance == null)
        {
            DebugManager.Instance?.Erreur("[AOIHeatmapManager] RecordingSessionManager introuvable.");
            return;
        }

        RecordingSessionManager.Instance.DemarrerSession();

        dossierSession = RecordingSessionManager.Instance.DossierSession;
        Directory.CreateDirectory(dossierSession);

        string horodatage = DateTime.Now.ToString("yyyyMMdd_HHmmss", culture);

        if (captureExporter != null)
            captureExporter.ExporterToutesLesCaptures(dossierSession);

        ExporterMetadataAOI(dossierSession, horodatage);

        string nomFichier = prefixeFichier + "_" + horodatage + ".csv";
        cheminFichier = Path.Combine(dossierSession, nomFichier);

        writer = new StreamWriter(cheminFichier, false, Encoding.UTF8);
        writer.AutoFlush = flushChaqueLigne;

        EcrireEntete();

        enregistrementActif = true;

        DebugManager.Instance?.Log("[AOIHeatmapManager] Enregistrement AOI -> " + cheminFichier);
    }

    public void ArreterEnregistrementAOI()
    {
        if (!enregistrementActif)
            return;

        enregistrementActif = false;

        try
        {
            writer?.Flush();
            writer?.Close();
        }
        catch
        {
        }

        writer = null;

        DebugManager.Instance?.Log("[AOIHeatmapManager] Sauvegarde AOI -> " + cheminFichier);
    }

    private void GererHitAoi(
        AOI_QuadZone zone,
        RaycastHit hit,
        bool regardValide,
        bool yeuxFermes,
        Vector3 directionRegardMonde)
    {
        bool uvOk = zone.TryGetUV(
            hit.point,
            out Vector2 uv,
            out Vector3 pointLocal
        );

        if (!uvOk)
        {
            EcrireLigneSansAOI(regardValide, yeuxFermes, "UV_INVALID", directionRegardMonde);
            return;
        }

        nomAoiCourante = zone.gameObject.name;
        dernierU = uv.x;
        dernierV = uv.y;
        derniereDistance = hit.distance;

        EcrireLigneAOI(
            regardValide,
            yeuxFermes,
            zone.AoiId,
            zone.gameObject.name,
            uv,
            pointLocal,
            directionRegardMonde,
            hit.distance
        );
    }

    private void EcrireEntete()
    {
        writer.WriteLine(string.Join(",",
            "timestamp_sec",
            "gaze_valid",
            "eyes_closed",
            "aoi_id",
            "aoi_name",
            "aoi_uv_x",
            "aoi_uv_y",
            "aoi_local_x",
            "aoi_local_y",
            "aoi_local_z",
            "gaze_direction_x",
            "gaze_direction_y",
            "gaze_direction_z",
            "gaze_distance_m"
        ));

        writer.Flush();
    }

    private void EcrireLigneAOI(
        bool regardValide,
        bool yeuxFermes,
        string aoiId,
        string nomAoi,
        Vector2 uv,
        Vector3 pointLocal,
        Vector3 directionRegardMonde,
        float distance)
    {
        string ligne = string.Join(",",
            RecordingSessionManager.Instance.TimestampFrameCourante.ToString("F6", culture),

            regardValide ? "1" : "0",
            yeuxFermes ? "1" : "0",

            Nettoyer(aoiId),
            Nettoyer(nomAoi),

            uv.x.ToString("F6", culture),
            uv.y.ToString("F6", culture),

            pointLocal.x.ToString("F6", culture),
            pointLocal.y.ToString("F6", culture),
            pointLocal.z.ToString("F6", culture),

            directionRegardMonde.x.ToString("F6", culture),
            directionRegardMonde.y.ToString("F6", culture),
            directionRegardMonde.z.ToString("F6", culture),

            distance.ToString("F6", culture)
        );

        writer.WriteLine(ligne);

        if (flushChaqueLigne)
            writer.Flush();
    }

    private void EcrireLigneSansAOI(
        bool regardValide,
        bool yeuxFermes,
        string raison,
        Vector3 directionRegardMonde)
    {
        string ligne = string.Join(",",
            RecordingSessionManager.Instance.TimestampFrameCourante.ToString("F6", culture),

            regardValide ? "1" : "0",
            yeuxFermes ? "1" : "0",

            Nettoyer(raison),
            "NONE",

            "-1",
            "-1",

            "0",
            "0",
            "0",

            directionRegardMonde.x.ToString("F6", culture),
            directionRegardMonde.y.ToString("F6", culture),
            directionRegardMonde.z.ToString("F6", culture),

            "-1"
        );

        writer.WriteLine(ligne);

        if (flushChaqueLigne)
            writer.Flush();
    }

    private void ReinitialiserAoiCourante()
    {
        nomAoiCourante = "";
        dernierU = -1f;
        dernierV = -1f;
        derniereDistance = -1f;
    }

    private void ExporterMetadataAOI(string dossier, string horodatage)
    {
        string nomMetadata = "aoi_metadata_" + horodatage + ".csv";
        string cheminMetadata = Path.Combine(dossier, nomMetadata);

        using (StreamWriter metadataWriter = new StreamWriter(cheminMetadata, false, Encoding.UTF8))
        {
            metadataWriter.WriteLine(string.Join(",",
                "aoi_id",
                "aoi_name",
                "type"
            ));

            AOI_QuadZone[] zones = FindObjectsOfType<AOI_QuadZone>();

            foreach (AOI_QuadZone zone in zones)
            {
                metadataWriter.WriteLine(string.Join(",",
                    Nettoyer(zone.AoiId),
                    Nettoyer(zone.gameObject.name),
                    "quad"
                ));
            }
        }

        DebugManager.Instance?.Log("[AOIHeatmapManager] Metadata AOI -> " + cheminMetadata);
    }

    private static string Nettoyer(string texte)
    {
        if (string.IsNullOrEmpty(texte))
            return "";

        return texte
            .Replace(",", "_")
            .Replace("\n", "_")
            .Replace("\r", "_");
    }

    private void OnApplicationPause(bool pause)
    {
        if (!pause)
            return;

        try
        {
            writer?.Flush();
        }
        catch
        {
        }
    }

    private void OnDisable()
    {
        if (enregistrementActif)
            ArreterEnregistrementAOI();
    }

    private void OnApplicationQuit()
    {
        if (enregistrementActif)
            ArreterEnregistrementAOI();
    }
}
