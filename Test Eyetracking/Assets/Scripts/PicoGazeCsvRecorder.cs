using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.XR.PXR;

public class PicoGazeCsvRecorder : MonoBehaviour
{
    [Header("XR refs")]
    public Transform xrOrigin;          // XR Origin (root)
    public Camera xrCamera;            // Main Camera (facultatif ici)

    [Header("Sampling")]
    [Tooltip("Fréquence cible. Ne dépassera pas le framerate réel de l'app.")]
    public float sampleHz = 72f;

    [Header("Quality / blinks")]
    [Range(0f, 1f)] public float minEyeOpenness = 0.15f;
    public bool dropWhenEyeClosed = false;

    [Header("Saccades (I-VT)")]
    public float saccadeStartDegS = 120f;
    public float saccadeEndDegS   = 60f;
    public float saccadeMinDurationMs = 10f;
    public float saccadeMinInterEventMs = 20f;

    [Header("AOI / hit")]
    public LayerMask hitMask = ~0;
    public float maxHitDistance = 50f;

    [Header("Output")]
    public string filePrefix = "gaze";
    public bool exportToDownloadsOnStop = true;
    public bool flushEverySample = false;

    StreamWriter _writer;
    string _appPath;
    bool _recording;

    double _t0;
    double _nextSchedT;

    // saccade state
    bool _hasPrev;
    Vector3 _prevDirHead;
    double _prevT;

    bool _inSaccade;
    int _saccadeId;
    double _saccadeStartT;
    Vector3 _saccadeStartDir;
    float _saccadePeakVel;
    double _lastSaccadeEndT = -999;

    // last completed saccade summary
    int _countSaccades;
    float _lastAmpDeg, _lastDurMs, _lastPeakDegS;

    void OnEnable()
    {
        // Start automatically (tu peux aussi appeler StartRecording() à la main)
        StartRecording();
    }

    void OnDisable()
    {
        StopRecording();
    }

    void OnApplicationQuit()
    {
        // Best effort: ensure file is closed when the app exits normally.
        StopRecording();
    }

    void OnApplicationPause(bool pause)
    {
        // Best effort: if the OS backgrounds the app, flush pending lines.
        if (pause)
        {
            try { _writer?.Flush(); } catch { }
        }
    }

    public void StartRecording()
    {
        if (_recording) return;

        _t0 = Time.realtimeSinceStartupAsDouble;
        _nextSchedT = 0.0;

        Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, "EyeTracking"));
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        _appPath = Path.Combine(Application.persistentDataPath, "EyeTracking", $"{filePrefix}_{stamp}.csv");

        _writer = new StreamWriter(_appPath, false, Encoding.UTF8);
        _writer.AutoFlush = flushEverySample;

        _writer.WriteLine(string.Join(",",
            "t_sec",                 // timestamp monotone
            "t_sched_sec",           // timestamp régulier demandé
            "utc_iso",               // timestamp UTC (pratique pour synchro EEG)
            "valid",
            "left_open","right_open",

            "gaze_dir_head_x","gaze_dir_head_y","gaze_dir_head_z",   // direction eye-in-head (pour saccades)
            "gaze_origin_wx","gaze_origin_wy","gaze_origin_wz",      // origin world
            "gaze_dir_wx","gaze_dir_wy","gaze_dir_wz",               // direction world (pour hit)

            "ang_vel_deg_s",
            "in_saccade",
            "saccade_id",
            "saccade_count",
            "last_amp_deg","last_dur_ms","last_peak_deg_s",

            "hit",
            "hit_object",
            "hit_aoi",
            "hit_wx","hit_wy","hit_wz"
        ));
        _writer.Flush();

        _recording = true;
        ResetSaccadeState();

        Debug.Log($"[PicoGazeCsvRecorder] Recording => {_appPath}");
    }

    public void StopRecording()
    {
        if (!_recording) return;
        _recording = false;

        try { _writer?.Flush(); _writer?.Close(); } catch { }
        _writer = null;

        Debug.Log($"[PicoGazeCsvRecorder] Saved (app-private) => {_appPath}");

#if UNITY_ANDROID && !UNITY_EDITOR
        if (exportToDownloadsOnStop)
        {
            string fileName = Path.GetFileName(_appPath);
            string result = EyeTrackingExportToDownloads.ExportFile(_appPath, fileName);
            Debug.Log($"[PicoGazeCsvRecorder] Export => {result}  (Download/EyeTracking/{fileName})");
        }
#endif
    }

    // Handy for a UI button: stops recording and triggers the export to Download/EyeTracking.
    public void StopAndExport()
    {
        StopRecording();
    }

    void ResetSaccadeState()
    {
        _hasPrev = false;
        _inSaccade = false;
        _saccadeId = 0;
        _countSaccades = 0;
        _lastAmpDeg = _lastDurMs = _lastPeakDegS = 0f;
        _lastSaccadeEndT = -999;
    }

    void Update()
    {
        if (!_recording || _writer == null) return;

        double nowT = Time.realtimeSinceStartupAsDouble - _t0;
        double dtTarget = 1.0 / Math.Max(1.0, sampleHz);

        // Scheduler: déclenche à pas régulier, mais échantillonne au moment "maintenant"
        if (nowT < _nextSchedT) return;

        // On rattrape si on a du retard (sans boucler à l'infini)
        int guard = 0;
        while (nowT >= _nextSchedT && guard++ < 3)
        {
            SampleAndWrite(nowT, _nextSchedT);
            _nextSchedT += dtTarget;
        }
    }

    void SampleAndWrite(double tNow, double tSched)
    {
        string utcIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // --- Eye tracking API ---
        bool okHead = PXR_EyeTracking.GetHeadPosMatrix(out Matrix4x4 headPose);
        bool okVec  = PXR_EyeTracking.GetCombineEyeGazeVector(out Vector3 gazeVecLocal);
        bool okPt   = PXR_EyeTracking.GetCombineEyeGazePoint(out Vector3 gazeOriginLocal);

        PXR_EyeTracking.GetLeftEyeGazeOpenness(out float leftOpen);
        PXR_EyeTracking.GetRightEyeGazeOpenness(out float rightOpen);

        bool valid = okHead && okVec && okPt;

        if (dropWhenEyeClosed && (leftOpen < minEyeOpenness || rightOpen < minEyeOpenness))
            valid = false;

        // direction eye-in-head (pour saccades)
        Vector3 dirHead = gazeVecLocal.normalized;

        // conversion world (même logique que les samples PICO)
        Matrix4x4 originM = xrOrigin != null ? xrOrigin.localToWorldMatrix : Matrix4x4.identity;
        Vector3 gazeOriginW = valid ? originM.MultiplyPoint(headPose.MultiplyPoint(gazeOriginLocal)) : Vector3.zero;
        Vector3 gazeDirW    = valid ? originM.MultiplyVector(headPose.MultiplyVector(gazeVecLocal)).normalized : Vector3.forward;

        // --- Angular velocity + saccades (I-VT) sur dirHead ---
        float angVel = 0f;
        bool inSaccade = false;
        int saccId = 0;

        if (valid)
        {
            if (!_hasPrev)
            {
                _prevDirHead = dirHead;
                _prevT = tNow;
                _hasPrev = true;
            }
            else
            {
                double dt = Math.Max(1e-6, tNow - _prevT);
                float dot = Mathf.Clamp(Vector3.Dot(_prevDirHead, dirHead), -1f, 1f);
                float angRad = Mathf.Acos(dot);
                angVel = (angRad * Mathf.Rad2Deg) / (float)dt;

                // detect
                if (!_inSaccade)
                {
                    bool canStart = (tNow - _lastSaccadeEndT) * 1000.0 >= saccadeMinInterEventMs;
                    if (canStart && angVel >= saccadeStartDegS)
                    {
                        _inSaccade = true;
                        _saccadeId++;
                        _saccadeStartT = tNow;
                        _saccadeStartDir = _prevDirHead;
                        _saccadePeakVel = angVel;
                    }
                }
                else
                {
                    _saccadePeakVel = Mathf.Max(_saccadePeakVel, angVel);

                    if (angVel <= saccadeEndDegS)
                    {
                        double durMs = (tNow - _saccadeStartT) * 1000.0;
                        float ampDeg = Mathf.Acos(Mathf.Clamp(Vector3.Dot(_saccadeStartDir, dirHead), -1f, 1f)) * Mathf.Rad2Deg;

                        if (durMs >= saccadeMinDurationMs)
                        {
                            _countSaccades++;
                            _lastAmpDeg = ampDeg;
                            _lastDurMs = (float)durMs;
                            _lastPeakDegS = _saccadePeakVel;
                        }

                        _inSaccade = false;
                        _lastSaccadeEndT = tNow;
                    }
                }

                _prevDirHead = dirHead;
                _prevT = tNow;
            }

            inSaccade = _inSaccade;
            saccId = _saccadeId;
        }
        else
        {
            // si invalid, on reset pour éviter faux pics
            _hasPrev = false;
            if (_inSaccade)
            {
                _inSaccade = false;
                _lastSaccadeEndT = tNow;
            }
        }

        // --- hit / AOI ---
        bool hit = false;
        string hitObj = "";
        string hitAoi = "";
        Vector3 hitPos = Vector3.zero;

        if (valid && Physics.Raycast(gazeOriginW, gazeDirW, out RaycastHit hitInfo, maxHitDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            hit = true;
            hitPos = hitInfo.point;
            hitObj = hitInfo.collider ? hitInfo.collider.gameObject.name : "";
            var aoi = hitInfo.collider ? hitInfo.collider.GetComponentInParent<AOI>() : null;
            hitAoi = aoi != null ? aoi.aoiId : "";
        }

        // --- CSV line ---
        var c = CultureInfo.InvariantCulture;
        string line = string.Join(",",
            tNow.ToString("F6", c),
            tSched.ToString("F6", c),
            utcIso,
            valid ? "1" : "0",
            leftOpen.ToString("F3", c), rightOpen.ToString("F3", c),

            dirHead.x.ToString("F6", c), dirHead.y.ToString("F6", c), dirHead.z.ToString("F6", c),
            gazeOriginW.x.ToString("F6", c), gazeOriginW.y.ToString("F6", c), gazeOriginW.z.ToString("F6", c),
            gazeDirW.x.ToString("F6", c), gazeDirW.y.ToString("F6", c), gazeDirW.z.ToString("F6", c),

            angVel.ToString("F3", c),
            inSaccade ? "1" : "0",
            saccId.ToString(c),
            _countSaccades.ToString(c),
            _lastAmpDeg.ToString("F2", c),
            _lastDurMs.ToString("F1", c),
            _lastPeakDegS.ToString("F1", c),

            hit ? "1" : "0",
            Sanitize(hitObj),
            Sanitize(hitAoi),
            hitPos.x.ToString("F6", c), hitPos.y.ToString("F6", c), hitPos.z.ToString("F6", c)
        );

        _writer.WriteLine(line);
        if (flushEverySample) _writer.Flush();
    }

    static string Sanitize(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(",", "_");

    public void StopExportAndQuit()
    {
        StopRecording();                 // ferme le fichier + export vers Download/EyeTracking
        Application.Quit();              // quitte l'app (sur casque)
    }
}

#if UNITY_ANDROID && !UNITY_EDITOR
public static class EyeTrackingExportToDownloads
{
    // Exporte un fichier vers "Internal Shared Storage/Download/EyeTracking" (visible en USB MTP)
    public static string ExportFile(string sourcePath, string destFileName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return null;

        // safety: ensure filename has the expected extension
        if (!destFileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            destFileName += ".csv";

        byte[] bytes = File.ReadAllBytes(sourcePath);
        // IMPORTANT: MediaStore "relative_path" is a folder like "Download/EyeTracking" (no "Device memory" prefix).
        return SaveBytesToDownloads(destFileName, "text/csv", bytes, "Download/EyeTracking");
    }

    static string SaveBytesToDownloads(string displayName, string mimeType, byte[] bytes, string relativePath)
    {
        int sdkInt = GetAndroidSdkInt();

        // Android < 10 (API < 29) : écriture directe (peut nécessiter permission selon OS)
        if (sdkInt < 29)
        {
            string legacyDir = $"/sdcard/{relativePath}";
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
        return uri.Call<string>("toString"); // content://...
    }

    static int GetAndroidSdkInt()
    {
        using var version = new AndroidJavaClass("android.os.Build$VERSION");
        return version.GetStatic<int>("SDK_INT");
    }
}
#endif