using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affiche les métriques utiles dans un Text UI.
/// Ne calcule rien : il lit uniquement l'état public des autres managers.
/// </summary>
public class DebugManager : MonoBehaviour
{
    #region Singleton

    public static DebugManager Instance { get; private set; }

    #endregion

    #region Inspector

    [Header("Activation")]
    [SerializeField] private bool debugActif = false; // Active ou masque le texte debug en VR.

    [Header("Affichage")]
    [SerializeField] private Text texteDebugMetrics; // Text UI utilisé pour afficher les métriques.
    [SerializeField] private float intervalleRafraichissement = 0.1f; // Temps en secondes entre deux mises à jour du texte.

    [Header("Références")]
    [SerializeField] private RecordingSessionManager recordingSessionManager; // Session d'enregistrement courante.
    [SerializeField] private PerclosManager perclosManager; // Source des métriques PERCLOS et yeux ouverts/fermés.
    [SerializeField] private SaccadeManager saccadeManager; // Source des métriques saccades/fixations.
    [SerializeField] private AOIHeatmapManager aoiHeatmapManager; // Source des informations AOI courantes.

    #endregion

    #region Variables privées

    private readonly StringBuilder contenu = new StringBuilder();
    private float tempsDernierRafraichissement;
    private float fps;

    #endregion

    #region Cycle Unity

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

        if (texteDebugMetrics != null)
            texteDebugMetrics.gameObject.SetActive(debugActif);
    }

    private void Update()
    {
        if (texteDebugMetrics == null)
            return;

        texteDebugMetrics.gameObject.SetActive(debugActif);

        if (!debugActif)
            return;

        if (Time.unscaledTime - tempsDernierRafraichissement < intervalleRafraichissement)
            return;

        tempsDernierRafraichissement = Time.unscaledTime;
        fps = 1f / Time.unscaledDeltaTime;
        ActualiserTexteDebug();
    }

    #endregion

    #region Logs simples

    public void Log(string message)
    {
        if (debugActif)
            Debug.Log(message);
    }

    public void Erreur(string message)
    {
        Debug.LogError(message);
    }

    #endregion

    #region Affichage texte

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
        contenu.AppendLine("AOI Debug : " + GetDebugAoi());

        texteDebugMetrics.text = contenu.ToString();
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
        return saccadeManager != null
            ? saccadeManager.NombreMicrosaccades.ToString()
            : "inconnu";
    }

    private string GetFixations()
    {
        if (saccadeManager == null)
            return "inconnu";

        return
            "nb=" + saccadeManager.NombreFixations +
            " | enCours=" + OuiNon(saccadeManager.EnFixation) +
            " | duree=" + saccadeManager.DureeFixationCouranteMs.ToString("0") + " ms" +
            " | derniere=" + saccadeManager.DureeDerniereFixationMs.ToString("0") + " ms";
    }

    private string GetAoiCourante()
    {
        if (aoiHeatmapManager == null || string.IsNullOrEmpty(aoiHeatmapManager.NomAoiCourante))
            return "aucune";

        return
            aoiHeatmapManager.NomAoiCourante +
            " | u=" + aoiHeatmapManager.DernierU.ToString("0.00") +
            " | v=" + aoiHeatmapManager.DernierV.ToString("0.00") +
            " | d=" + aoiHeatmapManager.DerniereDistance.ToString("0.00") + " m";
    }

    private string GetDebugAoi()
    {
        if (aoiHeatmapManager == null)
            return "inconnu";

        return
            "tracking=" + OuiNon(aoiHeatmapManager.RegardValideDebug) +
            " | hits=" + aoiHeatmapManager.NombreHitsRaycast +
            " | objet=" + TexteOuVide(aoiHeatmapManager.DernierObjetTouche) +
            " | etat=" + TexteOuVide(aoiHeatmapManager.DerniereErreurAoi);
    }

    private static string TexteOuVide(string valeur)
    {
        return string.IsNullOrEmpty(valeur) ? "aucun" : valeur;
    }

    private static string OuiNon(bool valeur)
    {
        return valeur ? "oui" : "non";
    }

    #endregion


    public void ToggleDebug()
    {
        debugActif = !debugActif;

        if (texteDebugMetrics != null)
            texteDebugMetrics.gameObject.SetActive(debugActif);

        Debug.Log("Debug : " + (debugActif ? "ON" : "OFF"));
    }

}
