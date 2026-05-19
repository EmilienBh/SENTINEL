using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.XR.PXR;

public class RecordDataManager : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform origineXR;
    [SerializeField] private PerclosManager perclosManager;
    [SerializeField] private SaccadeManager saccadeManager;

    [Header("Export")]
    [SerializeField] private string prefixeFichier = "record_data";
    [SerializeField] private bool demarrerAutomatiquement = false;
    [SerializeField] private bool flushChaqueLigne = false;

    private readonly CultureInfo culture = CultureInfo.InvariantCulture;

    private StreamWriter writer;
    private string cheminFichier;
    private bool enregistrementActif;

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
        if (!enregistrementActif || writer == null)
            return;

        EcrireLigne();
    }

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
        string nomFichier = prefixeFichier + "_" + horodatage + ".csv";

        cheminFichier = Path.Combine(dossierSession, nomFichier);

        writer = new StreamWriter(cheminFichier, false, Encoding.UTF8);
        writer.AutoFlush = flushChaqueLigne;

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

    private void EcrireEntete()
    {
        writer.WriteLine(string.Join(",",
            "timestamp_sec",
            "gaze_valid",
            "gaze_direction_x",
            "gaze_direction_y",
            "gaze_direction_z",
            "eyes_closed",
            "perclos_percent",
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
        bool okPose = PXR_EyeTracking.GetHeadPosMatrix(out Matrix4x4 poseTete);
        bool okVecteur = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 vecteurRegardLocal);
        bool okPoint = PXR_EyeTracking.GetCombineEyeGazePoint(out Vector3 pointRegardLocal);
        bool okStatut = PXR_EyeTracking.GetCombinedEyePoseStatus(out uint statutPose);

        bool regardValide =
            okPose &&
            okVecteur &&
            okPoint &&
            okStatut &&
            statutPose != 0;

        Matrix4x4 matriceOrigine = origineXR != null ? origineXR.localToWorldMatrix : Matrix4x4.identity;

        Vector3 directionRegardMonde = Vector3.zero;

        if (regardValide)
            directionRegardMonde = matriceOrigine.MultiplyVector(poseTete.MultiplyVector(vecteurRegardLocal)).normalized;

        bool yeuxFermes = perclosManager != null && perclosManager.YeuxFermes;
        float perclosPourcentage = perclosManager != null ? perclosManager.PerclosActuel * 100f : 0f;

        int nombreSaccades = saccadeManager != null ? saccadeManager.NombreSaccades : 0;
        int nombreMicrosaccades = saccadeManager != null ? saccadeManager.NombreMicrosaccades : 0;
        float vitesseSaccade = saccadeManager != null ? saccadeManager.VitesseAngulaireBrute : 0f;
        float amplitudeDerniereSaccade = saccadeManager != null ? saccadeManager.AmplitudeDerniereSaccade : 0f;

        int nombreFixations = saccadeManager != null ? saccadeManager.NombreFixations : 0;
        float dureeFixationCourante = saccadeManager != null ? saccadeManager.DureeFixationCouranteMs : 0f;
        float dureeDerniereFixation = saccadeManager != null ? saccadeManager.DureeDerniereFixationMs : 0f;

        string ligne = string.Join(",",
            RecordingSessionManager.Instance.TimestampFrameCourante.ToString("F6", culture),
            regardValide ? "1" : "0",
            directionRegardMonde.x.ToString("F6", culture),
            directionRegardMonde.y.ToString("F6", culture),
            directionRegardMonde.z.ToString("F6", culture),
            yeuxFermes ? "1" : "0",
            perclosPourcentage.ToString("F3", culture),
            nombreSaccades.ToString(culture),
            nombreMicrosaccades.ToString(culture),
            vitesseSaccade.ToString("F3", culture),
            amplitudeDerniereSaccade.ToString("F3", culture),
            nombreFixations.ToString(culture),
            dureeFixationCourante.ToString("F1", culture),
            dureeDerniereFixation.ToString("F1", culture)
        );

        writer.WriteLine(ligne);

        if (flushChaqueLigne)
            writer.Flush();
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
            ArreterEnregistrement();
    }

    private void OnApplicationQuit()
    {
        if (enregistrementActif)
            ArreterEnregistrement();
    }
}
