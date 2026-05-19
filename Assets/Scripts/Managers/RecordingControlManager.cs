using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class RecordingControlManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private RecordDataManager recordDataManager;
    [SerializeField] private AOIHeatmapManager aoiHeatmapManager;

    [Header("Retour utilisateur")]
    [SerializeField] private Text texteFeedback;
    [SerializeField] private AudioSource sourceAudio;
    [SerializeField] private AudioClip sonDemarrage;
    [SerializeField] private AudioClip sonArret;
    [SerializeField] private AudioClip sonErreur;

    [Header("Contrôle")]
    [SerializeField] private float dureeMessage = 3f;
    [SerializeField] private float delaiEntreCommandes = 1f;

    private InputDevice manetteDroite;
    private bool commandeDisponible = true;
    private Coroutine coroutineMessage;

    private void Awake()
    {
        if (recordDataManager == null)
            recordDataManager = FindObjectOfType<RecordDataManager>();

        if (aoiHeatmapManager == null)
            aoiHeatmapManager = FindObjectOfType<AOIHeatmapManager>();
    }

    private void Start()
    {
        InitialiserManette();

        if (texteFeedback != null)
            texteFeedback.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!manetteDroite.isValid)
            InitialiserManette();

        if (!manetteDroite.isValid || !commandeDisponible)
            return;

        manetteDroite.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
        manetteDroite.TryGetFeatureValue(CommonUsages.primaryButton, out bool boutonA);
        manetteDroite.TryGetFeatureValue(CommonUsages.secondaryButton, out bool boutonB);

        if (grip && boutonA)
            StartCoroutine(DemarrerEnregistrement());
        else if (grip && boutonB)
            StartCoroutine(ArreterEnregistrement());
    }

    private void InitialiserManette()
    {
        manetteDroite = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

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

    private void JouerSon(AudioClip clip)
    {
        if (sourceAudio != null && clip != null)
            sourceAudio.PlayOneShot(clip);
    }

    private void AfficherMessage(string message)
    {
        if (texteFeedback == null)
            return;

        texteFeedback.text = message;
        texteFeedback.gameObject.SetActive(true);

        if (coroutineMessage != null)
            StopCoroutine(coroutineMessage);

        coroutineMessage = StartCoroutine(MasquerMessage());
    }

    private IEnumerator MasquerMessage()
    {
        yield return new WaitForSeconds(dureeMessage);

        if (texteFeedback != null)
            texteFeedback.gameObject.SetActive(false);

        coroutineMessage = null;
    }
}
