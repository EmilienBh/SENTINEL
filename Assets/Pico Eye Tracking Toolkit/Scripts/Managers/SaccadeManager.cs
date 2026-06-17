using UnityEngine;
using Unity.XR.PXR;

/// <summary>
/// Détecte les saccades, microsaccades et fixations à partir de la vitesse angulaire du regard.
/// Les échantillons proches d'un clignement sont ignorés pour limiter les faux positifs.
/// </summary>
public class SaccadeManager : MonoBehaviour
{
    #region Inspector

    [Header("Références")]
    [SerializeField] private PerclosManager perclosManager; // Source de l'état yeux ouverts/fermés.

    [Header("Saccades")]
    [SerializeField] private float seuilDebutDegParSec = 120f; // Vitesse angulaire minimale pour démarrer une saccade.
    [SerializeField] private float seuilFinDegParSec = 60f; // Vitesse angulaire sous laquelle une saccade se termine.
    [SerializeField] private float dureeMinMs = 10f; // Durée minimale pour valider une saccade.
    [SerializeField] private float pauseMinMs = 20f; // Pause minimale entre deux saccades.

    [Header("Microsaccades")]
    [SerializeField] private float seuilMicrosaccadeDeg = 1f; // Amplitude sous laquelle une saccade est classée en microsaccade.

    [Header("Fixations")]
    [SerializeField] private float seuilFixationDegParSec = 30f; // Vitesse maximale pour considérer le regard comme stable.
    [SerializeField] private float dureeMinFixationMs = 100f; // Durée minimale pour compter une fixation.

    [Header("Clignements")]
    [SerializeField] private float blocageApresBlinkMs = 200f; // Durée ignorée après un blink pour éviter les artefacts.

    #endregion

    #region Propriétés publiques

    public float VitesseAngulaireBrute => vitesseAngulaireBrute;
    public float AmplitudeDerniereSaccade => amplitudeDerniereSaccade;
    public float DureeFixationCouranteMs => dureeFixationCouranteMs;
    public float DureeDerniereFixationMs => dureeDerniereFixationMs;
    public int NombreMicrosaccades => nombreMicrosaccades;
    public int NombreSaccades => nombreSaccades;
    public int NombreFixations => nombreFixations;
    public bool EnFixation => enFixation;
    public bool EnSaccade => enSaccade;

    #endregion

    #region Variables privées

    private Vector3 directionPrecedente;
    private Vector3 directionDebutSaccade;

    private double tempsPrecedent;
    private double tempsDebutSaccade;
    private double tempsDebutFixation;
    private double tempsFinDerniereSaccade = -999.0;

    private bool aDirectionPrecedente;
    private bool enSaccade;
    private bool enFixation;

    private float vitesseAngulaireBrute;
    private float amplitudeDerniereSaccade;
    private float dureeFixationCouranteMs;
    private float dureeDerniereFixationMs;

    private int nombreMicrosaccades;
    private int nombreSaccades;
    private int nombreFixations;

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();
    }

    private void Update()
    {
        double tempsActuel = Time.realtimeSinceStartupAsDouble;

        if (DoitIgnorerEchantillon())
        {
            TerminerFixation(tempsActuel);
            ReinitialiserSuiviTemporaire();
            return;
        }

        if (!PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 directionLocale))
        {
            TerminerFixation(tempsActuel);
            ReinitialiserSuiviTemporaire();
            return;
        }

        TraiterDirection(directionLocale.normalized, tempsActuel);
    }

    #endregion

    #region Détection

    private bool DoitIgnorerEchantillon()
    {
        if (perclosManager == null)
            return true;

        if (perclosManager.YeuxFermes)
            return true;

        float tempsDepuisBlinkMs = (Time.time - perclosManager.TempsDernierBlink) * 1000f;
        return tempsDepuisBlinkMs < blocageApresBlinkMs;
    }

    private void TraiterDirection(Vector3 directionActuelle, double tempsActuel)
    {
        if (!aDirectionPrecedente)
        {
            directionPrecedente = directionActuelle;
            tempsPrecedent = tempsActuel;
            aDirectionPrecedente = true;
            return;
        }

        double dt = tempsActuel - tempsPrecedent;
        if (dt <= 0.000001)
            return;

        vitesseAngulaireBrute = CalculerAngle(directionPrecedente, directionActuelle) / (float)dt;

        GererFixation(tempsActuel);
        GererSaccade(directionActuelle, tempsActuel);

        directionPrecedente = directionActuelle;
        tempsPrecedent = tempsActuel;
    }

    private void GererSaccade(Vector3 directionActuelle, double tempsActuel)
    {
        if (!enSaccade)
        {
            bool pauseRespectee = (tempsActuel - tempsFinDerniereSaccade) * 1000.0 >= pauseMinMs;

            if (pauseRespectee && vitesseAngulaireBrute >= seuilDebutDegParSec)
            {
                TerminerFixation(tempsActuel);
                enSaccade = true;
                tempsDebutSaccade = tempsActuel;
                directionDebutSaccade = directionActuelle;
            }

            return;
        }

        if (vitesseAngulaireBrute > seuilFinDegParSec)
            return;

        double dureeMs = (tempsActuel - tempsDebutSaccade) * 1000.0;
        amplitudeDerniereSaccade = CalculerAngle(directionDebutSaccade, directionActuelle);

        if (dureeMs >= dureeMinMs)
        {
            if (amplitudeDerniereSaccade < seuilMicrosaccadeDeg)
                nombreMicrosaccades++;
            else
                nombreSaccades++;
        }

        enSaccade = false;
        tempsFinDerniereSaccade = tempsActuel;
    }

    private void GererFixation(double tempsActuel)
    {
        if (vitesseAngulaireBrute > seuilFixationDegParSec)
        {
            TerminerFixation(tempsActuel);
            return;
        }

        if (!enFixation)
        {
            enFixation = true;
            tempsDebutFixation = tempsActuel;
        }

        dureeFixationCouranteMs = (float)((tempsActuel - tempsDebutFixation) * 1000.0);
    }

    private void TerminerFixation(double tempsActuel)
    {
        if (!enFixation)
        {
            dureeFixationCouranteMs = 0f;
            return;
        }

        float dureeMs = (float)((tempsActuel - tempsDebutFixation) * 1000.0);
        dureeDerniereFixationMs = dureeMs;

        if (dureeMs >= dureeMinFixationMs)
            nombreFixations++;

        enFixation = false;
        dureeFixationCouranteMs = 0f;
    }

    #endregion

    #region Utilitaires

    private static float CalculerAngle(Vector3 a, Vector3 b)
    {
        float dot = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
        return Mathf.Acos(dot) * Mathf.Rad2Deg;
    }

    private void ReinitialiserSuiviTemporaire()
    {
        aDirectionPrecedente = false;
        enSaccade = false;
        enFixation = false;
        vitesseAngulaireBrute = 0f;
        amplitudeDerniereSaccade = 0f;
        dureeFixationCouranteMs = 0f;
    }

    #endregion
}
