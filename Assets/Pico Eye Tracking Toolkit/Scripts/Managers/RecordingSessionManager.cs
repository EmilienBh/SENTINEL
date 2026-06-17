using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Crée une session commune pour tous les exports.
/// Tous les CSV et captures d'une session partagent le même dossier et le même timestamp de référence.
/// </summary>
public class RecordingSessionManager : MonoBehaviour
{
    #region Singleton

    public static RecordingSessionManager Instance { get; private set; }

    #endregion

    #region Propriétés publiques

    public bool SessionActive { get; private set; }
    public int FrameCourante { get; private set; }
    public double TimestampFrameCourante { get; private set; }
    public double TempsReference { get; private set; }
    public string DossierSession { get; private set; }
    public string Horodatage { get; private set; }

    #endregion

    #region Cycle Unity

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (SessionActive)
            ActualiserTempsFrame();
    }

    #endregion

    #region API session

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

        DebugManager.Instance?.Log("[RecordingSessionManager] Session démarrée : " + DossierSession);
    }

    public void ArreterSession()
    {
        if (!SessionActive)
            return;

        SessionActive = false;
        DebugManager.Instance?.Log("[RecordingSessionManager] Session arrêtée");
    }

    public double GetTimestamp()
    {
        return Time.realtimeSinceStartupAsDouble - TempsReference;
    }

    #endregion

    #region Temps synchronisé

    private void ActualiserTempsFrame()
    {
        FrameCourante = Time.frameCount;
        TimestampFrameCourante = GetTimestamp();
    }

    #endregion
}
