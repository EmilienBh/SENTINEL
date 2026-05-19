# Sentinel Eye Tracking

## Structure

- Managers : gestion globale de l'enregistrement, du regard, des AOI, des métriques, du contrôle et du debug.
- AOI : composants liés aux zones d'intérêt.
- Materials : matériaux utilisés par les AOI.
- Audio : sons de retour utilisateur.
- Prefabs : prefabs à créer dans Unity après import.

## Prefab principal conseillé

Créer un GameObject `SentinelEyeTrackingRig` avec :

- RecordingSessionManager
- RecordingControlManager
- RecordDataManager
- GazeManager
- AOIHeatmapManager
- PerclosManager
- SaccadeManager
- DebugManager
- AOICaptureExporter

## Prefab AOI conseillé

Créer un GameObject `AOIQuad` avec :

- MeshFilter
- MeshRenderer
- MeshCollider
- AOI_QuadZone

## Commandes

- Grip droit + A : démarrer l'enregistrement
- Grip droit + B : arrêter l'enregistrement

## Export

Les fichiers sont écrits dans :

- Android : `/storage/emulated/0/Download/EyeTracking`
- Éditeur Unity : `Application.persistentDataPath/EyeTracking`
