using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Détecte l'AOI regardée par raycast, calcule les coordonnées UV dans l'AOI,
/// puis exporte les données nécessaires à la génération des heatmaps.
/// </summary>
public class AOIHeatmapManager : MonoBehaviour
{
    #region Inspector

    [Header("Références")]
    [SerializeField] private GazeManager gazeManager; // Source de l'origine et de la direction du regard.
    [SerializeField] private PerclosManager perclosManager; // Source de l'état yeux ouverts/fermés.
    [SerializeField] private AOICaptureExporter captureExporter; // Génère les captures de fond des AOI au démarrage d'une session.

    [Header("Raycast AOI")]
    [SerializeField] private LayerMask masqueCollision = ~0; // Layers testés par le raycast AOI.
    [SerializeField] private float distanceMax = 30f; // Distance maximale du raycast AOI.

    [Header("Export CSV")]
    [SerializeField] private bool enregistrerAutomatiquement = false; // Lance l'enregistrement AOI dès le Start.
    [SerializeField] private string prefixeFichier = "aoi_heatmap_data"; // Préfixe du CSV contenant les points regard/AOI.

    #endregion

    #region Propriétés publiques

    public string DossierSession => dossierSession;
    public string NomAoiCourante => nomAoiCourante;
    public float DernierU => dernierU;
    public float DernierV => dernierV;
    public float DerniereDistance => derniereDistance;
    public bool AoiDetectee => aoiDetectee;
    public Vector3 DernierPointAoiMonde => dernierPointAoiMonde;

    public bool RegardValideDebug => regardValideDebug;
    public int NombreHitsRaycast => nombreHitsRaycast;
    public string DernierObjetTouche => dernierObjetTouche;
    public string DerniereErreurAoi => derniereErreurAoi;

    #endregion

    #region Variables privées

    private readonly CultureInfo culture = CultureInfo.InvariantCulture;
    private StreamWriter writer;
    private bool enregistrementActif;
    private string cheminFichier;
    private string dossierSession;

    private string nomAoiCourante = "";
    private float dernierU = -1f;
    private float dernierV = -1f;
    private float derniereDistance = -1f;
    private bool aoiDetectee;
    private Vector3 dernierPointAoiMonde;

    private bool regardValideDebug;
    private int nombreHitsRaycast;
    private string dernierObjetTouche = "";
    private string derniereErreurAoi = "";

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        if (gazeManager == null)
            gazeManager = FindObjectOfType<GazeManager>();

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
        ReinitialiserEtatFrame();

        if (gazeManager == null)
        {
            TraiterAbsenceAoi(false, false, "NO_GAZE_MANAGER", Vector3.zero);
            return;
        }

        bool yeuxFermes = perclosManager != null && perclosManager.YeuxFermes;
        bool regardValide = gazeManager.RegardValide;
        Vector3 origine = gazeManager.OrigineRegardMonde;
        Vector3 direction = gazeManager.DirectionRegardMonde;

        regardValideDebug = regardValide;

        RaycastHit[] hits = Physics.RaycastAll(origine, direction, distanceMax, masqueCollision, QueryTriggerInteraction.Collide);
        nombreHitsRaycast = hits.Length;

        if (hits.Length == 0)
        {
            TraiterAbsenceAoi(regardValide, yeuxFermes, "NO_HIT", direction);
            return;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        dernierObjetTouche = hits[0].collider != null ? hits[0].collider.gameObject.name : "";

        foreach (RaycastHit hit in hits)
        {
            AOI_QuadZone zone = hit.collider.GetComponent<AOI_QuadZone>() ?? hit.collider.GetComponentInParent<AOI_QuadZone>();

            if (zone == null)
                continue;

            GererHitAoi(zone, hit, regardValide, yeuxFermes, direction);
            return;
        }

        TraiterAbsenceAoi(regardValide, yeuxFermes, "NO_AOI", direction);
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            writer?.Flush();
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

    #endregion

    #region API enregistrement

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
        cheminFichier = Path.Combine(dossierSession, prefixeFichier + "_" + horodatage + ".csv");

        writer = new StreamWriter(cheminFichier, false, Encoding.UTF8);
        EcrireEnteteDonnees();

        enregistrementActif = true;

        captureExporter?.ExporterToutesLesCaptures(dossierSession);
        EcrireMetadataAoi();

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

    #endregion

    #region Détection AOI

    private void GererHitAoi(AOI_QuadZone zone, RaycastHit hit, bool regardValide, bool yeuxFermes, Vector3 directionRegard)
    {
        nomAoiCourante = zone.AoiId;
        derniereDistance = hit.distance;
        aoiDetectee = true;
        dernierPointAoiMonde = hit.point;
        derniereErreurAoi = "OK";

        if (!zone.TryGetUV(hit.point, out dernierU, out dernierV))
        {
            derniereErreurAoi = "UV_ERROR";
            return;
        }

        if (enregistrementActif && regardValide && !yeuxFermes)
            EcrireLigneDonnees(zone, hit, directionRegard);
    }

    private void TraiterAbsenceAoi(bool regardValide, bool yeuxFermes, string etat, Vector3 directionRegard)
    {
        derniereErreurAoi = etat;

        if (enregistrementActif && regardValide && !yeuxFermes)
            EcrireLigneVide(directionRegard);
    }

    private void ReinitialiserEtatFrame()
    {
        nomAoiCourante = "";
        dernierU = -1f;
        dernierV = -1f;
        derniereDistance = -1f;
        aoiDetectee = false;
        dernierPointAoiMonde = Vector3.zero;

        regardValideDebug = false;
        nombreHitsRaycast = 0;
        dernierObjetTouche = "";
        derniereErreurAoi = "";
    }

    #endregion

    #region CSV données

    private void EcrireEnteteDonnees()
    {
        writer.WriteLine(string.Join(",",
            "timestamp_sec",
            "frame",
            "gaze_valid",
            "eyes_closed",
            "aoi_id",
            "aoi_object",
            "aoi_uv_x",
            "aoi_uv_y",
            "hit_distance_m",
            "hit_world_x",
            "hit_world_y",
            "hit_world_z",
            "gaze_dir_x",
            "gaze_dir_y",
            "gaze_dir_z"
        ));

        writer.Flush();
    }

    private void EcrireLigneDonnees(AOI_QuadZone zone, RaycastHit hit, Vector3 directionRegard)
    {
        writer.WriteLine(string.Join(",",
            RecordingSessionManager.Instance.TimestampFrameCourante.ToString("F6", culture),
            RecordingSessionManager.Instance.FrameCourante.ToString(culture),
            "1",
            "0",
            NettoyerCsv(zone.AoiId),
            NettoyerCsv(zone.gameObject.name),
            dernierU.ToString("F6", culture),
            dernierV.ToString("F6", culture),
            hit.distance.ToString("F6", culture),
            hit.point.x.ToString("F6", culture),
            hit.point.y.ToString("F6", culture),
            hit.point.z.ToString("F6", culture),
            directionRegard.x.ToString("F6", culture),
            directionRegard.y.ToString("F6", culture),
            directionRegard.z.ToString("F6", culture)
        ));
    }

    private void EcrireLigneVide(Vector3 directionRegard)
    {
        writer.WriteLine(string.Join(",",
            RecordingSessionManager.Instance.TimestampFrameCourante.ToString("F6", culture),
            RecordingSessionManager.Instance.FrameCourante.ToString(culture),
            "1",
            "0",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            directionRegard.x.ToString("F6", culture),
            directionRegard.y.ToString("F6", culture),
            directionRegard.z.ToString("F6", culture)
        ));
    }

    #endregion

    #region CSV metadata

    private void EcrireMetadataAoi()
    {
        if (string.IsNullOrEmpty(dossierSession))
            return;

        string cheminMetadata = Path.Combine(dossierSession, "aoi_metadata_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", culture) + ".csv");

        using (StreamWriter metadataWriter = new StreamWriter(cheminMetadata, false, Encoding.UTF8))
        {
            metadataWriter.WriteLine("aoi_id,aoi_object");

            foreach (AOI_QuadZone zone in FindObjectsOfType<AOI_QuadZone>())
                metadataWriter.WriteLine(NettoyerCsv(zone.AoiId) + "," + NettoyerCsv(zone.gameObject.name));
        }

        DebugManager.Instance?.Log("[AOIHeatmapManager] Metadata AOI -> " + cheminMetadata);
    }

    #endregion

    #region Utilitaires

    private static string NettoyerCsv(string valeur)
    {
        if (string.IsNullOrEmpty(valeur))
            return "";

        return valeur.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");
    }

    #endregion
}
