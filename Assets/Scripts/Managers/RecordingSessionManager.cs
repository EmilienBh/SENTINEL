using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class RecordingSessionManager : MonoBehaviour
{
    public static RecordingSessionManager Instance { get; private set; }

    public int FrameCourante { get; private set; }
    public double TimestampFrameCourante { get; private set; }

    public bool SessionActive { get; private set; }
    public double TempsReference { get; private set; }
    public string DossierSession { get; private set; }
    public string Horodatage { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (!SessionActive)
            return;

        ActualiserTempsFrame();
    }

    private void ActualiserTempsFrame()
    {
        FrameCourante = Time.frameCount;
        TimestampFrameCourante = GetTimestamp();
    }

    public void DemarrerSession()
    {
        if (SessionActive)
            return;

        TempsReference = Time.realtimeSinceStartupAsDouble;
        Horodatage = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

#if UNITY_ANDROID && !UNITY_EDITOR
        string dossierRacine = Path.Combine("/storage/emulated/0/Download", "EyeTracking");
#else
        string dossierRacine = Path.Combine(Application.persistentDataPath, "EyeTracking");
#endif

        Directory.CreateDirectory(dossierRacine);

        DossierSession = Path.Combine(dossierRacine, Horodatage);
        Directory.CreateDirectory(DossierSession);

        SessionActive = true;
        ActualiserTempsFrame();

        Debug.Log("[RecordingSessionManager] Session démarrée : " + DossierSession);
    }

    public void ArreterSession()
    {
        SessionActive = false;
        Debug.Log("[RecordingSessionManager] Session arrêtée");
    }

    public double GetTimestamp()
    {
        return Time.realtimeSinceStartupAsDouble - TempsReference;
    }
}