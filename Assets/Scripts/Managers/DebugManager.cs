using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DebugManager : MonoBehaviour
{
    public static DebugManager Instance { get; private set; }

    [Header("Activation")]
    [SerializeField] private bool debugActif = false;
    [SerializeField] private bool logsConsoleActifs = false;

    [Header("Affichage")]
    [SerializeField] private Text texteDebug;
    [SerializeField] private float intervalleRafraichissement = 0.5f;

    [Header("Références")]
    [SerializeField] private RecordingSessionManager recordingSessionManager;
    [SerializeField] private PerclosManager perclosManager;
    [SerializeField] private SaccadeManager saccadeManager;
    [SerializeField] private AOIHeatmapManager aoiHeatmapManager;

    private readonly StringBuilder contenu = new StringBuilder();

    private float tempsDernierRafraichissement;
    private float fps;

    private void Awake()
    {
        Instance = this;

        if (recordingSessionManager == null)
            recordingSessionManager = FindObjectOfType<RecordingSessionManager>();

        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();

        if (saccadeManager == null)
            saccadeManager = FindObjectOfType<SaccadeManager>();

        if (aoiHeatmapManager == null)
            aoiHeatmapManager = FindObjectOfType<AOIHeatmapManager>();

        if (texteDebug != null)
            texteDebug.gameObject.SetActive(debugActif);
    }

    private void Update()
    {
        if (texteDebug == null)
            return;

        texteDebug.gameObject.SetActive(debugActif);

        if (!debugActif)
            return;

        if (Time.unscaledTime - tempsDernierRafraichissement < intervalleRafraichissement)
            return;

        tempsDernierRafraichissement = Time.unscaledTime;
        fps = 1f / Time.unscaledDeltaTime;

        ActualiserTexteDebug();
    }

    public void Log(string message)
    {
        if (debugActif && logsConsoleActifs)
            Debug.Log(message);
    }

    public void Erreur(string message)
    {
        if (logsConsoleActifs)
            Debug.LogError(message);
    }

    private void ActualiserTexteDebug()
    {
        contenu.Length = 0;

        contenu.AppendLine("FPS : " + fps.ToString("0"));
        contenu.AppendLine("Enregistrement : " + GetEtatEnregistrement());
        contenu.AppendLine("PERCLOS : " + GetPerclos());
        contenu.AppendLine("Yeux : " + GetEtatYeux());
        contenu.AppendLine("Saccades : " + GetSaccades());
        contenu.AppendLine("Microsaccades : " + GetMicrosaccades());
        contenu.AppendLine("Fixations : " + GetFixations());
        contenu.AppendLine("AOI : " + GetAoiCourante());

        texteDebug.text = contenu.ToString();
    }

    private string GetEtatEnregistrement()
    {
        if (recordingSessionManager == null)
            return "inconnu";

        return recordingSessionManager.SessionActive ? "actif" : "inactif";
    }

    private string GetPerclos()
    {
        if (perclosManager == null)
            return "inconnu";

        return (perclosManager.PerclosActuel * 100f).ToString("0.0") + " %";
    }

    private string GetEtatYeux()
    {
        if (perclosManager == null)
            return "inconnu";

        return perclosManager.YeuxFermes ? "fermés" : "ouverts";
    }

    private string GetSaccades()
    {
        if (saccadeManager == null)
            return "inconnu";

        return
            "nb=" + saccadeManager.NombreSaccades +
            " | enCours=" + OuiNon(saccadeManager.EnSaccade) +
            " | vitesse=" + saccadeManager.VitesseAngulaireBrute.ToString("0.0") + " deg/s" +
            " | amplitude=" + saccadeManager.AmplitudeDerniereSaccade.ToString("0.00") + " deg";
    }

    private string GetMicrosaccades()
    {
        if (saccadeManager == null)
            return "inconnu";

        return saccadeManager.NombreMicrosaccades.ToString();
    }

    private string GetFixations()
    {
        if (saccadeManager == null)
            return "inconnu";

        return
            "nb=" + saccadeManager.NombreFixations +
            " | enCours=" + OuiNon(saccadeManager.EnFixation) +
            " | durée=" + saccadeManager.DureeFixationCouranteMs.ToString("0") + " ms" +
            " | dernière=" + saccadeManager.DureeDerniereFixationMs.ToString("0") + " ms";
    }

    private string GetAoiCourante()
    {
        if (aoiHeatmapManager == null)
            return "aucune";

        if (string.IsNullOrEmpty(aoiHeatmapManager.NomAoiCourante))
            return "aucune";

        return aoiHeatmapManager.NomAoiCourante;
    }

    private string OuiNon(bool valeur)
    {
        return valeur ? "oui" : "non";
    }
}
