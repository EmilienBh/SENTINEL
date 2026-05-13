using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.XR.PXR;

public class RecordDataManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform origineXR;
    [SerializeField] private PerclosManager perclosManager;
    [SerializeField] private SaccadeManager saccadeManager;

    [Header("Sampling")]
    [SerializeField] private float frequenceEchantillonnageHz = 72f;

    [Header("Hit")]
    [SerializeField] private LayerMask masqueCollision = ~0;
    [SerializeField] private float distanceMaxHit = 50f;

    [Header("Output")]
    [SerializeField] private string prefixeFichier = "record_data";
    [SerializeField] private bool demarrerAutomatiquement = false;
    [SerializeField] private bool flushChaqueLigne = false;

    private StreamWriter writer;
    private string cheminFichierPrive;
    private string nomFichierExport;
    private bool enregistrementActif;

    private double tempsReference;
    private double prochainTempsPlanifie;

    private void Awake()
    {
        if (origineXR == null)
        {
            var xro = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            if (xro != null)
                origineXR = xro.transform;
        }

        if (perclosManager == null)
            perclosManager = FindObjectOfType<PerclosManager>();

        if (saccadeManager == null)
            saccadeManager = FindObjectOfType<SaccadeManager>();
    }

    private void OnEnable()
    {
        if (demarrerAutomatiquement)
            DemarrerEnregistrement();
    }

    private void OnDisable()
    {
        if (enregistrementActif)
            ArreterEnregistrement();
    }

    private void OnApplicationQuit()
    {
        if (enregistrementActif)
            ArreterEnregistrement();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            try { writer?.Flush(); } catch { }
        }
    }

    public void DemarrerEnregistrement()
    {
        if (enregistrementActif)
            return;

        tempsReference = Time.realtimeSinceStartupAsDouble;
        prochainTempsPlanifie = 0.0;

        string dossierPrive = Path.Combine(Application.persistentDataPath, "EyeTracking");
        Directory.CreateDirectory(dossierPrive);

        string horodatage = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string nomDossierSession = horodatage;
        string dossierSessionPrive = Path.Combine(dossierPrive, nomDossierSession);
        Directory.CreateDirectory(dossierSessionPrive);

        nomFichierExport = prefixeFichier + "_" + horodatage + ".csv";
        cheminFichierPrive = Path.Combine(dossierSessionPrive, nomFichierExport);

        writer = new StreamWriter(cheminFichierPrive, false, Encoding.UTF8);
        writer.AutoFlush = flushChaqueLigne;

        writer.WriteLine(string.Join(",",
            "t_sec",
            "t_sched_sec",
            "utc_iso",

            "ok_pose",
            "ok_vecteur",
            "ok_point",
            "ok_ouverture_gauche",
            "ok_ouverture_droite",
            "ok_statut",
            "valid",
            "statut_pose",

            "ouverture_gauche_brut",
            "ouverture_droite_brut",

            "vecteur_regard_local_x",
            "vecteur_regard_local_y",
            "vecteur_regard_local_z",

            "point_regard_local_x",
            "point_regard_local_y",
            "point_regard_local_z",

            "origine_regard_monde_x",
            "origine_regard_monde_y",
            "origine_regard_monde_z",

            "direction_regard_monde_x",
            "direction_regard_monde_y",
            "direction_regard_monde_z",

            "hit",
            "hit_object",
            "hit_x",
            "hit_y",
            "hit_z",

            "perclos_manager_present",
            "perclos_actuel",
            "yeux_fermes_manager",
            "temps_dernier_blink_sec",
            "ouverture_gauche_manager",
            "ouverture_droite_manager",
            "nombre_echantillons_fenetre",
            "nombre_echantillons_fermes",

            "saccade_manager_present",
            "vitesse_angulaire_deg_s",
            "amplitude_derniere_saccade_deg",
            "nombre_microsaccades",
            "nombre_saccades",
            "nombre_fixations",
            "en_saccade",
            "en_fixation",
            "duree_fixation_courante_ms",
            "duree_derniere_fixation_ms"
        ));

        writer.Flush();
        enregistrementActif = true;

        Debug.Log("[RecordDataManager] Enregistrement -> " + cheminFichierPrive);
    }

    public void ArreterEnregistrement()
    {
        if (!enregistrementActif)
            return;

        enregistrementActif = false;

        try
        {
            writer?.Flush();
            writer?.Close();
        }
        catch { }

        writer = null;

        Debug.Log("[RecordDataManager] Sauvegarde locale -> " + cheminFichierPrive);

#if UNITY_ANDROID && !UNITY_EDITOR
        string resultat = EyeTrackingExportToDownloads.ExportFile(cheminFichierPrive, nomFichierExport);
        Debug.Log("[RecordDataManager] Export -> " + resultat);
#endif
    }

    private void LateUpdate()
    {
        if (!enregistrementActif || writer == null)
            return;

        double tempsMaintenant = Time.realtimeSinceStartupAsDouble - tempsReference;
        double pas = 1.0 / Math.Max(1.0, frequenceEchantillonnageHz);

        if (tempsMaintenant < prochainTempsPlanifie)
            return;

        int garde = 0;
        while (tempsMaintenant >= prochainTempsPlanifie && garde++ < 3)
        {
            EcrireLigne(tempsMaintenant, prochainTempsPlanifie);
            prochainTempsPlanifie += pas;
        }
    }

    private void EcrireLigne(double tempsMaintenant, double tempsPlanifie)
    {
        string utcIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        bool okPose = PXR_EyeTracking.GetHeadPosMatrix(out Matrix4x4 poseTete);
        bool okVecteur = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 vecteurRegardLocal);
        bool okPoint = PXR_EyeTracking.GetCombineEyeGazePoint(out Vector3 pointRegardLocal);
        bool okOuvertureGauche = PXR_EyeTracking.GetLeftEyeGazeOpenness(out float ouvertureGaucheBrut);
        bool okOuvertureDroite = PXR_EyeTracking.GetRightEyeGazeOpenness(out float ouvertureDroiteBrut);
        bool okStatut = PXR_EyeTracking.GetCombinedEyePoseStatus(out uint statutPose);

        bool valid = okPose && okVecteur && okPoint && okOuvertureGauche && okOuvertureDroite && okStatut && statutPose == 1;

        Vector3 vecteurRegardLocalNorm = okVecteur ? vecteurRegardLocal.normalized : Vector3.zero;

        Matrix4x4 matriceOrigine = origineXR != null ? origineXR.localToWorldMatrix : Matrix4x4.identity;

        Vector3 origineRegardMonde = valid
            ? matriceOrigine.MultiplyPoint(poseTete.MultiplyPoint(pointRegardLocal))
            : Vector3.zero;

        Vector3 directionRegardMonde = valid
            ? matriceOrigine.MultiplyVector(poseTete.MultiplyVector(vecteurRegardLocal)).normalized
            : Vector3.zero;

        bool hit = false;
        string objetTouche = "";
        Vector3 pointHit = Vector3.zero;

        if (valid && Physics.Raycast(origineRegardMonde, directionRegardMonde, out RaycastHit hitInfo, distanceMaxHit, masqueCollision, QueryTriggerInteraction.Ignore))
        {
            hit = true;
            pointHit = hitInfo.point;
            objetTouche = hitInfo.collider != null ? hitInfo.collider.gameObject.name : "";
        }

        bool perclosPresent = perclosManager != null;
        float perclosActuel = perclosPresent ? perclosManager.PerclosActuel : 0f;
        bool yeuxFermesManager = perclosPresent && perclosManager.YeuxFermes;
        float tempsDernierBlink = perclosPresent ? perclosManager.TempsDernierBlink : 0f;
        float ouvertureGaucheManager = perclosPresent ? perclosManager.OuvertureGaucheActuelle : 0f;
        float ouvertureDroiteManager = perclosPresent ? perclosManager.OuvertureDroiteActuelle : 0f;
        int nombreEchantillonsFenetre = perclosPresent ? perclosManager.NombreEchantillons : 0;
        int nombreEchantillonsFermes = perclosPresent ? perclosManager.NombreEchantillonsFermes : 0;

        bool saccadePresent = saccadeManager != null;
        float vitesseAngulaire = saccadePresent ? saccadeManager.VitesseAngulaireBrute : 0f;
        float amplitudeDerniereSaccade = saccadePresent ? saccadeManager.AmplitudeDerniereSaccade : 0f;
        int nombreMicrosaccades = saccadePresent ? saccadeManager.NombreMicrosaccades : 0;
        int nombreSaccades = saccadePresent ? saccadeManager.NombreSaccades : 0;
        int nombreFixations = saccadePresent ? saccadeManager.NombreFixations : 0;
        bool enSaccade = saccadePresent && saccadeManager.EnSaccade;
        bool enFixation = saccadePresent && saccadeManager.EnFixation;
        float dureeFixationCouranteMs = saccadePresent ? saccadeManager.DureeFixationCouranteMs : 0f;
        float dureeDerniereFixationMs = saccadePresent ? saccadeManager.DureeDerniereFixationMs : 0f;

        var c = CultureInfo.InvariantCulture;

        string ligne = string.Join(",",
            tempsMaintenant.ToString("F6", c),
            tempsPlanifie.ToString("F6", c),
            utcIso,

            okPose ? "1" : "0",
            okVecteur ? "1" : "0",
            okPoint ? "1" : "0",
            okOuvertureGauche ? "1" : "0",
            okOuvertureDroite ? "1" : "0",
            okStatut ? "1" : "0",
            valid ? "1" : "0",
            statutPose.ToString(c),

            ouvertureGaucheBrut.ToString("F3", c),
            ouvertureDroiteBrut.ToString("F3", c),

            vecteurRegardLocalNorm.x.ToString("F6", c),
            vecteurRegardLocalNorm.y.ToString("F6", c),
            vecteurRegardLocalNorm.z.ToString("F6", c),

            pointRegardLocal.x.ToString("F6", c),
            pointRegardLocal.y.ToString("F6", c),
            pointRegardLocal.z.ToString("F6", c),

            origineRegardMonde.x.ToString("F6", c),
            origineRegardMonde.y.ToString("F6", c),
            origineRegardMonde.z.ToString("F6", c),

            directionRegardMonde.x.ToString("F6", c),
            directionRegardMonde.y.ToString("F6", c),
            directionRegardMonde.z.ToString("F6", c),

            hit ? "1" : "0",
            Nettoyer(objetTouche),
            pointHit.x.ToString("F6", c),
            pointHit.y.ToString("F6", c),
            pointHit.z.ToString("F6", c),

            perclosPresent ? "1" : "0",
            perclosActuel.ToString("F6", c),
            yeuxFermesManager ? "1" : "0",
            tempsDernierBlink.ToString("F6", c),
            ouvertureGaucheManager.ToString("F3", c),
            ouvertureDroiteManager.ToString("F3", c),
            nombreEchantillonsFenetre.ToString(c),
            nombreEchantillonsFermes.ToString(c),

            saccadePresent ? "1" : "0",
            vitesseAngulaire.ToString("F3", c),
            amplitudeDerniereSaccade.ToString("F3", c),
            nombreMicrosaccades.ToString(c),
            nombreSaccades.ToString(c),
            nombreFixations.ToString(c),
            enSaccade ? "1" : "0",
            enFixation ? "1" : "0",
            dureeFixationCouranteMs.ToString("F1", c),
            dureeDerniereFixationMs.ToString("F1", c)
        );

        writer.WriteLine(ligne);

        if (flushChaqueLigne)
            writer.Flush();
    }

    private static string Nettoyer(string texte)
    {
        if (string.IsNullOrEmpty(texte))
            return "";

        return texte.Replace(",", "_");
    }
}

#if UNITY_ANDROID && !UNITY_EDITOR
public static class EyeTrackingExportToDownloads
{
    public static string ExportFile(string sourcePath, string destFileName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return null;

        if (!destFileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            destFileName += ".csv";

        byte[] bytes = File.ReadAllBytes(sourcePath);
        return SaveBytesToDownloads(destFileName, "text/csv", bytes, "Download/EyeTracking");
    }

    private static string SaveBytesToDownloads(string displayName, string mimeType, byte[] bytes, string relativePath)
    {
        int sdkInt = GetAndroidSdkInt();

        if (sdkInt < 29)
        {
            string legacyDir = "/sdcard/" + relativePath;
            Directory.CreateDirectory(legacyDir);
            string legacyPath = Path.Combine(legacyDir, displayName);
            File.WriteAllBytes(legacyPath, bytes);
            return legacyPath;
        }

        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver");

        using var contentValues = new AndroidJavaObject("android.content.ContentValues");
        contentValues.Call("put", "_display_name", displayName);
        contentValues.Call("put", "mime_type", mimeType);
        contentValues.Call("put", "relative_path", relativePath);

        using (var int1 = new AndroidJavaObject("java.lang.Integer", 1))
            contentValues.Call("put", "is_pending", int1);

        using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
        using AndroidJavaObject externalUri = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

        using AndroidJavaObject uri = resolver.Call<AndroidJavaObject>("insert", externalUri, contentValues);
        if (uri == null) return null;

        using AndroidJavaObject os = resolver.Call<AndroidJavaObject>("openOutputStream", uri);
        if (os == null) return null;

        os.Call("write", bytes);
        os.Call("flush");
        os.Call("close");

        using var contentValues2 = new AndroidJavaObject("android.content.ContentValues");
        using (var int0 = new AndroidJavaObject("java.lang.Integer", 0))
            contentValues2.Call("put", "is_pending", int0);

        resolver.Call<int>("update", uri, contentValues2, null, null);
        return uri.Call<string>("toString");
    }

    private static int GetAndroidSdkInt()
    {
        using var version = new AndroidJavaClass("android.os.Build$VERSION");
        return version.GetStatic<int>("SDK_INT");
    }
}
#endif