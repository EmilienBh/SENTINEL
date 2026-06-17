using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// Gère les commandes VR liées à l'enregistrement et au debug.
/// Commandes utilisables avec la manette gauche ou droite :
/// - Grip + bouton principal : démarrer l'enregistrement.
/// - Grip + bouton secondaire : arrêter l'enregistrement.
/// - Grip + clic joystick : activer/désactiver l'affichage debug.
/// </summary>
public class ControllerManager : MonoBehaviour
{
    #region Inspector

    [Header("Managers")]
    [SerializeField] private RecordDataManager recordDataManager; // Export CSV principal des données eye tracking.
    [SerializeField] private AOIHeatmapManager aoiHeatmapManager; // Export CSV AOI et captures utilisées pour les heatmaps.
    [SerializeField] private DebugManager debugManager; // Affichage debug VR activable/désactivable à la manette.

    [Header("Retour utilisateur")]
    [SerializeField] private Text texteDebugRecording; // Texte temporaire affiché lors du start/stop recording.
    [SerializeField] private AudioSource sourceAudio; // Source audio utilisée pour les sons de confirmation.
    [SerializeField] private AudioClip sonDemarrage; // Son joué au démarrage de l'enregistrement.
    [SerializeField] private AudioClip sonArret; // Son joué à l'arrêt de l'enregistrement.
    [SerializeField] private AudioClip sonErreur; // Son joué quand la commande demandée est invalide.

    [Header("Contrôle")]
    [SerializeField] private float dureeMessage = 3f; // Durée d'affichage du message utilisateur.
    [SerializeField] private float delaiEntreCommandes = 1f; // Anti-rebond entre deux commandes d'enregistrement.

    #endregion

    #region Variables privées

    private InputDevice manetteDroite; // Référence à la manette droite XR.
    private InputDevice manetteGauche; // Référence à la manette gauche XR.

    private bool commandeDisponible = true; // Évite de lancer plusieurs start/stop à la suite.
    private float dernierToggleDebug = -999f; // Dernier moment où le debug a été togglé.
    [SerializeField] private float cooldownToggleDebug = 0.5f; // Délai minimum entre deux toggles debug.

    private Coroutine coroutineMessage; // Coroutine en cours pour masquer le message utilisateur.

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        if (recordDataManager == null)
            recordDataManager = FindObjectOfType<RecordDataManager>();

        if (aoiHeatmapManager == null)
            aoiHeatmapManager = FindObjectOfType<AOIHeatmapManager>();

        if (debugManager == null)
            debugManager = FindObjectOfType<DebugManager>();
    }

    private void Start()
    {
        InitialiserManettes();

        if (texteDebugRecording != null)
            texteDebugRecording.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!manetteDroite.isValid || !manetteGauche.isValid)
            InitialiserManettes();

        LireCommandesManette(manetteDroite);
        LireCommandesManette(manetteGauche);
    }

    #endregion

    #region Initialisation manettes

    private void InitialiserManettes()
    {
        List<InputDevice> devices = new List<InputDevice>();

        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count > 0)
            manetteDroite = devices[0];

        devices.Clear();

        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, devices);
        if (devices.Count > 0)
            manetteGauche = devices[0];
    }

    #endregion

    #region Lecture commandes

    private void LireCommandesManette(InputDevice manette)
    {
        if (!manette.isValid)
            return;

        manette.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
        manette.TryGetFeatureValue(CommonUsages.primaryButton, out bool boutonPrincipal);
        manette.TryGetFeatureValue(CommonUsages.secondaryButton, out bool boutonSecondaire);
        manette.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool clicJoystick);

        GererToggleDebug(grip, clicJoystick);
        GererCommandeEnregistrement(grip, boutonPrincipal, boutonSecondaire);
    }

    private void GererToggleDebug(bool grip, bool clicJoystick)
    {
        if (!grip || !clicJoystick)
            return;

        if (Time.time - dernierToggleDebug < cooldownToggleDebug)
            return;

        dernierToggleDebug = Time.time;
        debugManager?.ToggleDebug();
    }

    private void GererCommandeEnregistrement(bool grip, bool boutonPrincipal, bool boutonSecondaire)
    {
        if (!commandeDisponible)
            return;

        if (grip && boutonPrincipal)
            StartCoroutine(DemarrerEnregistrement());
        else if (grip && boutonSecondaire)
            StartCoroutine(ArreterEnregistrement());
    }

    #endregion

    #region Enregistrement

    private IEnumerator DemarrerEnregistrement()
    {
        commandeDisponible = false;

        if (RecordingSessionManager.Instance != null && RecordingSessionManager.Instance.SessionActive)
        {
            JouerSon(sonErreur);
            AfficherMessage("ENREGISTREMENT DÉJÀ EN COURS");
        }
        else
        {
            RecordingSessionManager.Instance?.DemarrerSession();

            recordDataManager?.DemarrerEnregistrement();
            aoiHeatmapManager?.DemarrerEnregistrementAOI();

            JouerSon(sonDemarrage);
            AfficherMessage("ENREGISTREMENT LANCÉ");
        }

        yield return new WaitForSeconds(delaiEntreCommandes);
        commandeDisponible = true;
    }

    private IEnumerator ArreterEnregistrement()
    {
        commandeDisponible = false;

        if (RecordingSessionManager.Instance == null || !RecordingSessionManager.Instance.SessionActive)
        {
            JouerSon(sonErreur);
            AfficherMessage("AUCUN ENREGISTREMENT EN COURS");
        }
        else
        {
            recordDataManager?.ArreterEnregistrement();
            aoiHeatmapManager?.ArreterEnregistrementAOI();
            RecordingSessionManager.Instance.ArreterSession();

            JouerSon(sonArret);
            AfficherMessage("ENREGISTREMENT ARRÊTÉ");
        }

        yield return new WaitForSeconds(delaiEntreCommandes);
        commandeDisponible = true;
    }

    #endregion

    #region Feedback utilisateur

    private void JouerSon(AudioClip clip)
    {
        if (sourceAudio != null && clip != null)
            sourceAudio.PlayOneShot(clip);
    }

    private void AfficherMessage(string message)
    {
        if (texteDebugRecording == null)
            return;

        texteDebugRecording.text = message;
        texteDebugRecording.gameObject.SetActive(true);

        if (coroutineMessage != null)
            StopCoroutine(coroutineMessage);

        coroutineMessage = StartCoroutine(MasquerMessage());
    }

    private IEnumerator MasquerMessage()
    {
        yield return new WaitForSeconds(dureeMessage);

        if (texteDebugRecording != null)
            texteDebugRecording.gameObject.SetActive(false);

        coroutineMessage = null;
    }

    #endregion
}
