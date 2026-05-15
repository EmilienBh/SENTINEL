using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

public class AOIHeatmapManager : MonoBehaviour
{
    [Header("References")]
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
    [SerializeField] private Text texteUI;

    private StreamWriter writer;
    private bool enregistrementActif;

    private string cheminFichierPrive;
    private string nomFichierExport;

    private double tempsReference;

    private string dossierSessionPrive;

    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

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
        if (origineXR == null)
        {
            AfficherDebug("origineXR NULL");
            return;
        }

        bool yeuxFermes =
            perclosManager != null &&
            perclosManager.YeuxFermes;

        if (ignorerSiYeuxFermes && yeuxFermes)
        {
            AfficherDebug("Yeux fermes");
            return;
        }

        bool okPose =
            PXR_EyeTracking.GetHeadPosMatrix(
                out Matrix4x4 poseTete
            );

        bool okVecteur =
            PXR_EyeTracking.GetCombineEyeGazeVector(
                out Vector3 vecteurRegardLocal
            );

        bool okPoint =
            PXR_EyeTracking.GetCombineEyeGazePoint(
                out Vector3 pointRegardLocal
            );

        bool okStatut =
            PXR_EyeTracking.GetCombinedEyePoseStatus(
                out uint statut
            );

        if (!okPose || !okVecteur || !okPoint)
        {
            AfficherDebug(
                "Tracking invalide\n" +
                "Pose : " + okPose + "\n" +
                "Vecteur : " + okVecteur + "\n" +
                "Point : " + okPoint
            );

            return;
        }

        if (ignorerSiTrackingInvalide &&
            (!okStatut || statut == 0))
        {
            AfficherDebug(
                "Statut tracking invalide\n" +
                "okStatut : " + okStatut + "\n" +
                "statut : " + statut
            );

            return;
        }

        Matrix4x4 matriceOrigine =
            origineXR.localToWorldMatrix;

        Vector3 origineRegardMonde =
            matriceOrigine.MultiplyPoint(
                poseTete.MultiplyPoint(pointRegardLocal)
            );

        Vector3 directionRegardMonde =
            matriceOrigine.MultiplyVector(
                poseTete.MultiplyVector(vecteurRegardLocal)
            ).normalized;

        if (afficherRayonDebug)
        {
            Debug.DrawRay(
                origineRegardMonde,
                directionRegardMonde * distanceMax,
                Color.red
            );
        }

        if (!Physics.Raycast(
                origineRegardMonde,
                directionRegardMonde,
                out RaycastHit hit,
                distanceMax,
                masqueCollision,
                QueryTriggerInteraction.Collide))
        {
            AfficherDebug("Raycast : rien");
            return;
        }

        AOI_QuadZone quadZone =
            hit.collider.GetComponent<AOI_QuadZone>();

        if (quadZone == null)
        {
            quadZone =
                hit.collider.GetComponentInParent<AOI_QuadZone>();
        }

        if (quadZone != null)
        {
            GererHitQuadZone(
                quadZone,
                hit,
                origineRegardMonde,
                directionRegardMonde,
                yeuxFermes
            );

            return;
        }

        AOI_Zone zone =
            hit.collider.GetComponent<AOI_Zone>();

        if (zone == null)
        {
            zone =
                hit.collider.GetComponentInParent<AOI_Zone>();
        }

        if (zone == null)
        {
            AfficherDebug(
                "Objet touche sans AOI\n" +
                hit.collider.gameObject.name
            );

            return;
        }

        GererHitZoneRectangulaire(
            zone,
            hit,
            origineRegardMonde,
            directionRegardMonde,
            yeuxFermes
        );
    }

    private void GererHitZoneRectangulaire(
        AOI_Zone zone,
        RaycastHit hit,
        Vector3 origineRegardMonde,
        Vector3 directionRegardMonde,
        bool yeuxFermes)
    {
        Vector2 uv = RecupererUV(zone, hit);

        Vector3 localPoint =
            zone.transform.InverseTransformPoint(hit.point);

        zone.AjouterPointUV(uv);

        AfficherDebug(
            "AOI : " + zone.AoiId + "\n" +
            "U : " + uv.x.ToString("0.00") + "\n" +
            "V : " + uv.y.ToString("0.00")
        );

        if (enregistrementActif && writer != null)
        {
            EcrireLigneAOI(
                zone.AoiId,
                zone.gameObject.name,
                hit,
                uv,
                localPoint,
                origineRegardMonde,
                directionRegardMonde,
                yeuxFermes
            );
        }
    }

    private void GererHitQuadZone(
        AOI_QuadZone zone,
        RaycastHit hit,
        Vector3 origineRegardMonde,
        Vector3 directionRegardMonde,
        bool yeuxFermes)
    {
        bool uvOk =
            zone.TryGetUV(
                hit.point,
                out Vector2 uv,
                out Vector3 localPoint
            );

        if (!uvOk)
        {
            AfficherDebug("UV invalides");
            return;
        }

        AfficherDebug(
            "AOI Quad : " + zone.AoiId + "\n" +
            "U : " + uv.x.ToString("0.00") + "\n" +
            "V : " + uv.y.ToString("0.00")
        );

        if (enregistrementActif && writer != null)
        {
            EcrireLigneAOI(
                zone.AoiId,
                zone.gameObject.name,
                hit,
                uv,
                localPoint,
                origineRegardMonde,
                directionRegardMonde,
                yeuxFermes
            );
        }
    }

    private Vector2 RecupererUV(
        AOI_Zone zone,
        RaycastHit hit)
    {
        Vector2 uv = hit.textureCoord;

        if (uv.x > 0f || uv.y > 0f)
            return uv;

        Vector3 local =
            zone.transform.InverseTransformPoint(hit.point);

        float u = local.x + 0.5f;
        float v = local.y + 0.5f;

        return new Vector2(
            Mathf.Clamp01(u),
            Mathf.Clamp01(v)
        );
    }

    public void DemarrerEnregistrementAOI()
    {
        if (enregistrementActif)
            return;

        tempsReference =
            Time.realtimeSinceStartupAsDouble;

#if UNITY_ANDROID && !UNITY_EDITOR
        string dossierPrive =
            Path.Combine(
                "/storage/emulated/0/Download",
                "EyeTracking"
            );
#else
        string dossierPrive =
            Path.Combine(
                Application.persistentDataPath,
                "EyeTracking"
            );
#endif

        Directory.CreateDirectory(dossierPrive);

        string horodatage =
            DateTime.Now.ToString(
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture
            );

        dossierSessionPrive =
            Path.Combine(
                dossierPrive,
                horodatage
            );

        Directory.CreateDirectory(dossierSessionPrive);

        if (captureExporter != null)
        {
            captureExporter.ExporterToutesLesCaptures(
                dossierSessionPrive
            );
        }

        nomFichierExport =
            prefixeFichier +
            "_" +
            horodatage +
            ".csv";

        cheminFichierPrive =
            Path.Combine(
                dossierSessionPrive,
                nomFichierExport
            );

        writer =
            new StreamWriter(
                cheminFichierPrive,
                false,
                Encoding.UTF8
            );

        writer.AutoFlush = flushChaqueLigne;

        writer.WriteLine(string.Join(",",
            "t_sec",
            "utc_iso",
            "frame",
            "valid",
            "aoi_id",
            "aoi_object",
            "u",
            "v",
            "hit_x",
            "hit_y",
            "hit_z",
            "local_x",
            "local_y",
            "local_z",
            "origine_x",
            "origine_y",
            "origine_z",
            "direction_x",
            "direction_y",
            "direction_z",
            "distance",
            "yeux_fermes"
        ));

        writer.Flush();

        ExporterMetadataAOI(
            dossierSessionPrive,
            horodatage
        );

        enregistrementActif = true;

        Debug.Log(
            "[AOIHeatmapManager] Enregistrement AOI -> " +
            cheminFichierPrive
        );
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
        catch { }

        writer = null;

        Debug.Log(
            "[AOIHeatmapManager] Sauvegarde AOI -> " +
            cheminFichierPrive
        );
    }

    private void EcrireLigneAOI(
        string aoiId,
        string objectName,
        RaycastHit hit,
        Vector2 uv,
        Vector3 localPoint,
        Vector3 origineRegardMonde,
        Vector3 directionRegardMonde,
        bool yeuxFermes)
    {
        double tSec =
            Time.realtimeSinceStartupAsDouble -
            tempsReference;

        string utcIso =
            DateTime.UtcNow.ToString("o", culture);

        string ligne = string.Join(",",
            tSec.ToString("F6", culture),
            utcIso,
            Time.frameCount.ToString(culture),
            "1",
            Nettoyer(aoiId),
            Nettoyer(objectName),
            uv.x.ToString("F6", culture),
            uv.y.ToString("F6", culture),
            hit.point.x.ToString("F6", culture),
            hit.point.y.ToString("F6", culture),
            hit.point.z.ToString("F6", culture),
            localPoint.x.ToString("F6", culture),
            localPoint.y.ToString("F6", culture),
            localPoint.z.ToString("F6", culture),
            origineRegardMonde.x.ToString("F6", culture),
            origineRegardMonde.y.ToString("F6", culture),
            origineRegardMonde.z.ToString("F6", culture),
            directionRegardMonde.x.ToString("F6", culture),
            directionRegardMonde.y.ToString("F6", culture),
            directionRegardMonde.z.ToString("F6", culture),
            hit.distance.ToString("F6", culture),
            yeuxFermes ? "1" : "0"
        );

        writer.WriteLine(ligne);

        if (flushChaqueLigne)
            writer.Flush();
    }

    private void ExporterMetadataAOI(
        string dossierSessionPrive,
        string horodatage)
    {
        string nomMetadata =
            "aoi_metadata_" +
            horodatage +
            ".csv";

        string cheminMetadata =
            Path.Combine(
                dossierSessionPrive,
                nomMetadata
            );

        using (StreamWriter metadataWriter =
            new StreamWriter(
                cheminMetadata,
                false,
                Encoding.UTF8))
        {
            metadataWriter.WriteLine(string.Join(",",
                "aoi_id",
                "aoi_object",
                "type"
            ));

            AOI_Zone[] zonesRect =
                FindObjectsOfType<AOI_Zone>();

            foreach (AOI_Zone zone in zonesRect)
            {
                metadataWriter.WriteLine(string.Join(",",
                    Nettoyer(zone.AoiId),
                    Nettoyer(zone.gameObject.name),
                    "rect"
                ));
            }

            AOI_QuadZone[] zonesQuad =
                FindObjectsOfType<AOI_QuadZone>();

            foreach (AOI_QuadZone zone in zonesQuad)
            {
                metadataWriter.WriteLine(string.Join(",",
                    Nettoyer(zone.AoiId),
                    Nettoyer(zone.gameObject.name),
                    "quad"
                ));
            }
        }
    }

    public void ExporterToutesLesZones()
    {
        AOI_Zone[] zones = FindObjectsOfType<AOI_Zone>();

        for (int i = 0; i < zones.Length; i++)
        {               
            zones[i].ExporterImages();
        }
    }

    public void ReinitialiserToutesLesZones()
    {
        AOI_Zone[] zones = FindObjectsOfType<AOI_Zone>();

        for (int i = 0; i < zones.Length; i++)
        {
            zones[i].ReinitialiserHeatmap();
        }
    }

    private void AfficherDebug(string message)
    {
        if (texteUI != null)
            texteUI.text = message;
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
        if (pause)
        {
            try
            {
                writer?.Flush();
            }
            catch { }
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