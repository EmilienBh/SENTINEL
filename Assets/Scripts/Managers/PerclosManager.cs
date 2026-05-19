using System.Collections.Generic;
using UnityEngine;
using Unity.XR.PXR;

public class PerclosManager : MonoBehaviour
{
    [Header("PERCLOS")]
    [SerializeField] private float fenetreSecondes = 10f;

    public bool YeuxFermes { get; private set; }
    public float TempsDernierBlink { get; private set; }
    public float PerclosActuel { get; private set; }
    public float OuvertureGaucheActuelle { get; private set; }
    public float OuvertureDroiteActuelle { get; private set; }
    public int NombreEchantillons => echantillons.Count;
    public int NombreEchantillonsFermes => nombreEchantillonsFermes;

    private struct EchantillonPerclos
    {
        public float Temps;
        public bool YeuxFermes;
    }

    private readonly Queue<EchantillonPerclos> echantillons = new Queue<EchantillonPerclos>();
    private int nombreEchantillonsFermes;

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
        if (echantillons.Count == 0)
        {
            PerclosActuel = 0f;
            return;
        }

        PerclosActuel = (float)nombreEchantillonsFermes / echantillons.Count;
    }
}
