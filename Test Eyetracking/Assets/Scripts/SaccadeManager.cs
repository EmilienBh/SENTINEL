using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

public class SaccadeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Text texteUI;
    [SerializeField] private PerclosManager perclosManager;

    [Header("Saccades")]
    [SerializeField] private float seuilDebutDegParSec = 120f;
    [SerializeField] private float seuilFinDegParSec = 60f;
    [SerializeField] private float dureeMinMs = 10f;
    [SerializeField] private float pauseMinMs = 20f;

    [Header("Microsaccades")]
    [SerializeField] private float seuilMicrosaccadeDeg = 1f;

    [Header("Fixations")]
    [SerializeField] private float seuilFixationDegParSec = 30f;
    [SerializeField] private float dureeMinFixationMs = 100f;

    [Header("Blinks")]
    [SerializeField] private float blocageApresBlinkMs = 200f;

    private Vector3 directionPrecedente;
    private Vector3 directionDebutSaccade;

    private double tempsPrecedent;
    private double tempsDebutSaccade;
    private double tempsDebutFixation;
    private double tempsFinDerniereSaccade = -999.0;

    private bool aPrecedent = false;
    private bool enSaccade = false;
    private bool enFixation = false;

    private float vitesseAngulaireBrute = 0f;
    private float amplitudeDerniereSaccade = 0f;
    private float dureeFixationCouranteMs = 0f;
    private float dureeDerniereFixationMs = 0f;

    private int nombreMicrosaccades = 0;
    private int nombreSaccades = 0;
    private int nombreFixations = 0;

    private void Awake()
    {
        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();
    }

    private void Update()
    {
        if (perclosManager == null)
        {
            AfficherMessage("PerclosManager introuvable");
            return;
        }

        double tempsActuel = Time.realtimeSinceStartupAsDouble;

        if (perclosManager.YeuxFermes)
        {
            TerminerFixation(tempsActuel);
            ReinitialiserSuivi();
            AfficherTexte();
            return;
        }

        float tempsDepuisDernierBlinkMs = (Time.time - perclosManager.TempsDernierBlink) * 1000f;

        if (tempsDepuisDernierBlinkMs < blocageApresBlinkMs)
        {
            TerminerFixation(tempsActuel);
            ReinitialiserSuivi();
            AfficherTexte();
            return;
        }

        bool okDirection = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 directionRegardLocale);
        if (!okDirection)
        {
            TerminerFixation(tempsActuel);
            ReinitialiserSuivi();
            AfficherTexte();
            return;
        }

        Vector3 directionActuelle = directionRegardLocale.normalized;

        if (!aPrecedent)
        {
            directionPrecedente = directionActuelle;
            tempsPrecedent = tempsActuel;
            aPrecedent = true;
            AfficherTexte();
            return;
        }

        double dt = tempsActuel - tempsPrecedent;
        if (dt <= 0.000001)
        {
            AfficherTexte();
            return;
        }

        float produitScalaire = Mathf.Clamp(Vector3.Dot(directionPrecedente, directionActuelle), -1f, 1f);
        float angleDeg = Mathf.Acos(produitScalaire) * Mathf.Rad2Deg;
        vitesseAngulaireBrute = angleDeg / (float)dt;

        GererFixation(tempsActuel);

        if (!enSaccade)
        {
            bool pauseRespectee = (tempsActuel - tempsFinDerniereSaccade) * 1000.0 >= pauseMinMs;

            if (pauseRespectee && vitesseAngulaireBrute >= seuilDebutDegParSec)
            {
                enSaccade = true;
                tempsDebutSaccade = tempsActuel;
                directionDebutSaccade = directionActuelle;
            }
        }
        else
        {
            if (vitesseAngulaireBrute <= seuilFinDegParSec)
            {
                double dureeMsActuelle = (tempsActuel - tempsDebutSaccade) * 1000.0;

                float produitScalaireAmplitude = Mathf.Clamp(Vector3.Dot(directionDebutSaccade, directionActuelle), -1f, 1f);
                float amplitudeTotaleDeg = Mathf.Acos(produitScalaireAmplitude) * Mathf.Rad2Deg;
                amplitudeDerniereSaccade = amplitudeTotaleDeg;

                if (dureeMsActuelle >= dureeMinMs)
                {
                    if (amplitudeTotaleDeg < seuilMicrosaccadeDeg)
                        nombreMicrosaccades++;
                    else
                        nombreSaccades++;
                }

                enSaccade = false;
                tempsFinDerniereSaccade = tempsActuel;
            }
        }

        directionPrecedente = directionActuelle;
        tempsPrecedent = tempsActuel;

        AfficherTexte();
    }

    private void GererFixation(double tempsActuel)
    {
        if (vitesseAngulaireBrute <= seuilFixationDegParSec)
        {
            if (!enFixation)
            {
                enFixation = true;
                tempsDebutFixation = tempsActuel;
            }

            dureeFixationCouranteMs = (float)((tempsActuel - tempsDebutFixation) * 1000.0);
        }
        else
        {
            TerminerFixation(tempsActuel);
        }
    }

    private void TerminerFixation(double tempsActuel)
    {
        if (!enFixation)
        {
            dureeFixationCouranteMs = 0f;
            return;
        }

        float dureeFixationMs = (float)((tempsActuel - tempsDebutFixation) * 1000.0);
        dureeDerniereFixationMs = dureeFixationMs;

        if (dureeFixationMs >= dureeMinFixationMs)
            nombreFixations++;

        enFixation = false;
        dureeFixationCouranteMs = 0f;
    }

    private void ReinitialiserSuivi()
    {
        aPrecedent = false;
        enSaccade = false;
        enFixation = false;
        vitesseAngulaireBrute = 0f;
        amplitudeDerniereSaccade = 0f;
        dureeFixationCouranteMs = 0f;
    }

    private void AfficherTexte()
    {
        if (texteUI == null) return;

        texteUI.text =
            "Vitesse angulaire : " + vitesseAngulaireBrute.ToString("0.0") + " deg/s\n" +
            "Amplitude : " + amplitudeDerniereSaccade.ToString("0.00") + " deg\n" +
            "Fixations : " + nombreFixations + "\n" +
            "Fixation courante : " + dureeFixationCouranteMs.ToString("0") + " ms\n" +
            "Derniere fixation : " + dureeDerniereFixationMs.ToString("0") + " ms\n" +
            "Microsaccades : " + nombreMicrosaccades + "\n" +
            "Saccades : " + nombreSaccades;
    }

    private void AfficherMessage(string message)
    {
        if (texteUI == null) return;
        texteUI.text = message;
    }
}