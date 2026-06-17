using System.Collections.Generic;
using UnityEngine;
using Unity.XR.PXR;

/// <summary>
/// Calcule le PERCLOS : proportion du temps où les yeux sont fermés dans une fenêtre glissante.
/// Ici, un échantillon est considéré fermé si au moins un œil a une ouverture à 0.
/// </summary>
public class PerclosManager : MonoBehaviour
{
    #region Inspector

    [Header("PERCLOS")]
    [SerializeField] private float fenetreSecondes = 10f; // Durée de la fenêtre glissante utilisée pour calculer le PERCLOS.

    #endregion

    #region Propriétés publiques

    public bool YeuxFermes { get; private set; }
    public float TempsDernierBlink { get; private set; }
    public float PerclosActuel { get; private set; }
    public float OuvertureGaucheActuelle { get; private set; }
    public float OuvertureDroiteActuelle { get; private set; }
    public int NombreEchantillons => echantillons.Count;
    public int NombreEchantillonsFermes => nombreEchantillonsFermes;

    #endregion

    #region Variables privées

    private struct EchantillonPerclos
    {
        public float Temps; // Temps Unity de l'échantillon.
        public bool YeuxFermes; // État ouvert/fermé associé à l'échantillon.
    }

    private readonly Queue<EchantillonPerclos> echantillons = new Queue<EchantillonPerclos>();
    private int nombreEchantillonsFermes;

    #endregion

    #region Cycle Unity

    private void Update()
    {
        float tempsActuel = Time.time;

        PXR_EyeTracking.GetLeftEyeGazeOpenness(out float ouvertureGauche);
        PXR_EyeTracking.GetRightEyeGazeOpenness(out float ouvertureDroite);

        OuvertureGaucheActuelle = ouvertureGauche;
        OuvertureDroiteActuelle = ouvertureDroite;
        YeuxFermes = ouvertureGauche == 0f || ouvertureDroite == 0f;

        if (YeuxFermes)
            TempsDernierBlink = tempsActuel;

        AjouterEchantillon(tempsActuel, YeuxFermes);
        SupprimerEchantillonsExpires(tempsActuel);
        CalculerPerclos();
    }

    #endregion

    #region Calcul PERCLOS

    private void AjouterEchantillon(float temps, bool yeuxFermes)
    {
        echantillons.Enqueue(new EchantillonPerclos
        {
            Temps = temps,
            YeuxFermes = yeuxFermes
        });

        if (yeuxFermes)
            nombreEchantillonsFermes++;
    }

    private void SupprimerEchantillonsExpires(float tempsActuel)
    {
        float tempsMinimum = tempsActuel - fenetreSecondes;

        while (echantillons.Count > 0 && echantillons.Peek().Temps < tempsMinimum)
        {
            EchantillonPerclos ancien = echantillons.Dequeue();

            if (ancien.YeuxFermes)
                nombreEchantillonsFermes--;
        }
    }

    private void CalculerPerclos()
    {
        PerclosActuel = echantillons.Count > 0
            ? (float)nombreEchantillonsFermes / echantillons.Count
            : 0f;
    }

    #endregion
}
