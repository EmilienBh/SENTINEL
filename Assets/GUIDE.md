# Sentinel Eye Tracking

## Structure

```text
SentinelEyeTracking/
├── Managers/
│   ├── RecordingSessionManager.cs
│   ├── RecordingControlManager.cs
│   ├── RecordDataManager.cs
│   ├── GazeManager.cs
│   ├── AOIHeatmapManager.cs
│   ├── PerclosManager.cs
│   ├── SaccadeManager.cs
│   └── DebugManager.cs

│
├── AOI/
│   ├── AOI_QuadZone.cs
│   └── AOICaptureExporter.cs
│
├── Materials/
├── Audio/
└── Prefabs/
```

## Scripts

### RecordingSessionManager
Gère la session d'enregistrement, le dossier de sortie, l'horodatage et le timestamp commun.

### RecordingControlManager
Gère les raccourcis manettes pour démarrer et arrêter l'enregistrement.

### RecordDataManager
Écrit le fichier CSV principal : regard, tracking, hit, PERCLOS et saccades.

### GazeManager
Affiche le point de regard dans l'interface VR.

### AOIHeatmapManager
Gère le raycast sur les AOI et écrit le fichier CSV dédié aux heatmaps.

### PerclosManager
Calcule le PERCLOS et l'état yeux fermés.

### SaccadeManager
Détecte les saccades, microsaccades et fixations.

### AOI_QuadZone
Crée une AOI quadrilatère avec mesh, collider et coordonnées UV.

### AOICaptureExporter
Exporte les captures des AOI au démarrage d'une session.

## Mise en scène Unity

Créer un GameObject `SentinelEyeTrackingRig` et ajouter :

- `RecordingSessionManager`
- `RecordingControlManager`
- `RecordDataManager`
- `GazeManager`
- `AOIHeatmapManager`
- `PerclosManager`
- `SaccadeManager`
- `AOICaptureExporter`

Créer ensuite les AOI avec un GameObject contenant :

- `MeshFilter`
- `MeshRenderer`
- `MeshCollider`
- `AOI_QuadZone`

## Commandes

- Grip droit + A : démarrer l'enregistrement.
- Grip droit + B : arrêter l'enregistrement.

## Export

Sur Android :

```text
/storage/emulated/0/Download/EyeTracking/<session>/
```

Dans l'éditeur :

```text
Application.persistentDataPath/EyeTracking/<session>/
```
