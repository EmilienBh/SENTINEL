using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.PXR;

public class PerclosManager : MonoBehaviour
{
    public Text texteUI;
    public float fenetreSecondes = 10f;

    public bool YeuxFermes { get; private set; }
    public float TempsDernierBlink { get; private set; }
    public float PerclosActuel { get; private set; }
    public float OuvertureGaucheActuelle { get; private set; }
    public float OuvertureDroiteActuelle { get; private set; }
    public int NombreEchantillons => echantillons.Count;
    public int NombreEchantillonsFermes => nombreEchantillonsFermes;

    struct Echantillon
    {
        public float temps;
        public bool ferme;
    }

    Queue<Echantillon> echantillons = new Queue<Echantillon>();
    int nombreEchantillonsFermes = 0;

    void Update()
    {
        float tempsActuel = Time.time;

        PXR_EyeTracking.GetLeftEyeGazeOpenness(out float ouvertureGauche);
        PXR_EyeTracking.GetRightEyeGazeOpenness(out float ouvertureDroite);

        OuvertureGaucheActuelle = ouvertureGauche;
        OuvertureDroiteActuelle = ouvertureDroite;

        YeuxFermes = (ouvertureGauche == 0f) || (ouvertureDroite == 0f);

        if (YeuxFermes)
            TempsDernierBlink = tempsActuel;

        echantillons.Enqueue(new Echantillon
        {
            temps = tempsActuel,
            ferme = YeuxFermes
        });

        if (YeuxFermes)
            nombreEchantillonsFermes++;

        float tempsMinimum = tempsActuel - fenetreSecondes;

        while (echantillons.Count > 0 && echantillons.Peek().temps < tempsMinimum)
        {
            Echantillon ancien = echantillons.Dequeue();

            if (ancien.ferme)
                nombreEchantillonsFermes--;
        }

        float perclos = 0f;

        if (echantillons.Count > 0)
            perclos = (float)nombreEchantillonsFermes / echantillons.Count;

        PerclosActuel = perclos;

        if (texteUI != null)
        {
            texteUI.text =
                "PERCLOS : " + (perclos * 100f).ToString("0.0") + "%\n" +
                "Gauche : " + ouvertureGauche.ToString("0.00") + "\n" +
                "Droite : " + ouvertureDroite.ToString("0.00");
        }
    }
}
