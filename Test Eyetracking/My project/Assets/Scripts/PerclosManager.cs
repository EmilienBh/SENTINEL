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

        bool okGauche = PXR_EyeTracking.GetLeftEyeGazeOpenness(out float ouvertureGauche);
        bool okDroite = PXR_EyeTracking.GetRightEyeGazeOpenness(out float ouvertureDroite);

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

        if (texteUI != null)
        {
            texteUI.text =
                "PERCLOS : " + (perclos * 100f).ToString("0.0") + "%\n" +
                "Gauche : " + ouvertureGauche.ToString("0.00") + "\n" +
                "Droite : " + ouvertureDroite.ToString("0.00");
        }
    }
}