using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Exporte les métriques globales eye tracking dans un CSV synchronisé avec la session.
/// Ce fichier complète le CSV AOI avec les directions regard, PERCLOS, saccades et fixations.
/// </summary>
public class RecordDataManager : MonoBehaviour
{
    #region Inspector

    [Header("Références")]
    [SerializeField] private GazeManager gazeManager; // Source du regard valide et de la direction monde.
    [SerializeField] private PerclosManager perclosManager; // Source des valeurs PERCLOS et yeux fermés.
    [SerializeField] private SaccadeManager saccadeManager; // Source des métriques saccades/fixations.

    [Header("Export CSV")]
    [SerializeField] private bool demarrerAutomatiquement = false; // Lance l'export dès OnEnable.
    [SerializeField] private string prefixeFichier = "record_data"; // Préfixe du CSV global.

    #endregion

    #region Variables privées

    private readonly CultureInfo culture = CultureInfo.InvariantCulture;
    private StreamWriter writer;
    private string cheminFichier;
    private bool enregistrementActif;

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        if (gazeManager == null)
            gazeManager = FindObjectOfType<GazeManager>();

        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();

        if (saccadeManager == null)
            saccadeManager = FindObjectOfType<SaccadeManager>();
    }

    private void OnEnable()
    {
        if (demarrerAutomatiquement)
            DemarrerEnregistrement();
    }

    private void LateUpdate()
    {
        if (enregistrementActif && writer != null)
            EcrireLigne();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            writer?.Flush();
    }

    private void OnDisable()
    {
        if (enregistrementActif)
            ArreterEnregistrement();
    }

    private void OnApplicationQuit()
    {
        if (enregistrementActif)
            ArreterEnregistrement();
    }

    #endregion

    #region API enregistrement

    public void DemarrerEnregistrement()
    {
        if (enregistrementActif)
            return;

        if (RecordingSessionManager.Instance == null)
        {
            DebugManager.Instance?.Erreur("[RecordDataManager] RecordingSessionManager introuvable.");
            return;
        }

        RecordingSessionManager.Instance.DemarrerSession();

        string dossierSession = RecordingSessionManager.Instance.DossierSession;
        Directory.CreateDirectory(dossierSession);

        string horodatage = DateTime.Now.ToString("yyyyMMdd_HHmmss", culture);
        cheminFichier = Path.Combine(dossierSession, prefixeFichier + "_" + horodatage + ".csv");

        writer = new StreamWriter(cheminFichier, false, Encoding.UTF8);
        EcrireEntete();

        enregistrementActif = true;

        DebugManager.Instance?.Log("[RecordDataManager] Enregistrement -> " + cheminFichier);
    }

    public void ArreterEnregistrement()
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

        DebugManager.Instance?.Log("[RecordDataManager] Sauvegarde -> " + cheminFichier);
    }

    #endregion

    #region CSV

    private void EcrireEntete()
    {
        writer.WriteLine(string.Join(",",
            "timestamp_sec",
            "frame",
            "gaze_valid",
            "gaze_direction_x",
            "gaze_direction_y",
            "gaze_direction_z",
            "eyes_closed",
            "perclos_percent",
            "left_eye_openness",
            "right_eye_openness",
            "saccade_count",
            "microsaccade_count",
            "saccade_velocity_deg_s",
            "last_saccade_amplitude_deg",
            "fixation_count",
            "current_fixation_duration_ms",
            "last_fixation_duration_ms"
        ));

        writer.Flush();
    }

    private void EcrireLigne()
    {
        bool regardValide = gazeManager != null && gazeManager.RegardValide;
        Vector3 direction = gazeManager != null ? gazeManager.DirectionRegardMonde : Vector3.zero;

        bool yeuxFermes = perclosManager != null && perclosManager.YeuxFermes;
        float perclosPourcentage = perclosManager != null ? perclosManager.PerclosActuel * 100f : 0f;
        float ouvertureGauche = perclosManager != null ? perclosManager.OuvertureGaucheActuelle : 0f;
        float ouvertureDroite = perclosManager != null ? perclosManager.OuvertureDroiteActuelle : 0f;

        int saccades = saccadeManager != null ? saccadeManager.NombreSaccades : 0;
        int microsaccades = saccadeManager != null ? saccadeManager.NombreMicrosaccades : 0;
        float vitesseSaccade = saccadeManager != null ? saccadeManager.VitesseAngulaireBrute : 0f;
        float amplitudeSaccade = saccadeManager != null ? saccadeManager.AmplitudeDerniereSaccade : 0f;

        int fixations = saccadeManager != null ? saccadeManager.NombreFixations : 0;
        float dureeFixationCourante = saccadeManager != null ? saccadeManager.DureeFixationCouranteMs : 0f;
        float dureeDerniereFixation = saccadeManager != null ? saccadeManager.DureeDerniereFixationMs : 0f;

        writer.WriteLine(string.Join(",",
            RecordingSessionManager.Instance.TimestampFrameCourante.ToString("F6", culture),
            RecordingSessionManager.Instance.FrameCourante.ToString(culture),
            regardValide ? "1" : "0",
            direction.x.ToString("F6", culture),
            direction.y.ToString("F6", culture),
            direction.z.ToString("F6", culture),
            yeuxFermes ? "1" : "0",
            perclosPourcentage.ToString("F3", culture),
            ouvertureGauche.ToString("F3", culture),
            ouvertureDroite.ToString("F3", culture),
            saccades.ToString(culture),
            microsaccades.ToString(culture),
            vitesseSaccade.ToString("F3", culture),
            amplitudeSaccade.ToString("F3", culture),
            fixations.ToString(culture),
            dureeFixationCourante.ToString("F1", culture),
            dureeDerniereFixation.ToString("F1", culture)
        ));
    }

    #endregion
}
