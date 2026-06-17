# Pico Eye Tracking Toolkit — Guide des scripts

Ce document explique le rôle des scripts du package et leur place dans l’architecture Unity.

---

# 1. Vue d’ensemble

Le package est organisé autour de quatre blocs :

```text
Tracking regard
→ GazeManager
→ PerclosManager
→ SaccadeManager

AOI / Heatmaps
→ AOI_QuadZone
→ AOIHeatmapManager
→ AOICaptureExporter
→ heatmap_maker.py

Enregistrement
→ RecordingSessionManager
→ RecordDataManager
→ ControllerManager

Debug
→ DebugManager
```

---

# 2. GazeManager

## Rôle

`GazeManager` récupère les données de regard fournies par le casque Pico Neo 2 Eye.

Il fournit :
- l’état de validité du regard ;
- l’origine du regard ;
- la direction locale du regard ;
- la direction monde du regard ;
- le point cible du regard ;
- le point visuel de debug dans le casque.

## Responsabilités

```text
Pico Eye Tracking
→ lecture du vecteur regard
→ conversion locale / monde
→ raycast dans la scène
→ affichage du point du regard
```

## Données principales exposées

```text
RegardValide
OrigineRegardMonde
DirectionRegardLocale
DirectionRegardMonde
CibleRegardMonde
```

## Utilisé par

```text
AOIHeatmapManager
RecordDataManager
SaccadeManager
DebugManager
```

---

# 3. PerclosManager

## Rôle

`PerclosManager` calcule le PERCLOS à partir de l’ouverture des yeux.

Le PERCLOS correspond au pourcentage de temps pendant lequel les yeux sont considérés comme fermés sur une fenêtre glissante.

## Responsabilités

```text
lecture ouverture yeux
→ détection yeux fermés
→ calcul PERCLOS
```

## Données principales exposées

```text
YeuxFermes
PerclosActuel
```

## Utilisé par

```text
RecordDataManager
AOIHeatmapManager
DebugManager
```

---

# 4. SaccadeManager

## Rôle

`SaccadeManager` analyse les mouvements du regard.

Il calcule :
- la vitesse angulaire du regard ;
- les saccades ;
- les microsaccades ;
- les fixations.

## Responsabilités

```text
direction regard
→ vitesse angulaire
→ détection saccade
→ détection microsaccade
→ détection fixation
```

## Données principales exposées

```text
NombreSaccades
NombreMicrosaccades
NombreFixations
EnSaccade
EnFixation
VitesseAngulaireBrute
AmplitudeDerniereSaccade
DureeFixationCouranteMs
DureeDerniereFixationMs
```

## Utilisé par

```text
RecordDataManager
DebugManager
```

---

# 5. AOI_QuadZone

## Rôle

`AOI_QuadZone` définit une zone d’intérêt de forme quadrilatère.

Il génère :
- le mesh de l’AOI ;
- le collider ;
- les coordonnées UV utilisées pour les heatmaps.

## Responsabilités

```text
coins AOI
→ mesh quad
→ collider
→ calcul UV
```

## Points importants

Chaque AOI doit avoir :

```text
AoiId unique
MeshCollider actif
Is Trigger = false
Convex = false
```

## Utilisé par

```text
AOIHeatmapManager
AOICaptureExporter
```

---

# 6. AOIHeatmapManager

## Rôle

`AOIHeatmapManager` détecte quelle AOI est regardée et exporte les données nécessaires aux heatmaps.

## Responsabilités

```text
raycast regard
→ détection AOI
→ calcul UV
→ export CSV AOI
→ export métadonnées AOI
```

## Fichiers générés

```text
aoi_heatmap_data_*.csv
aoi_metadata_*.csv
```

## Données principales exposées

```text
NomAoiCourante
DernierU
DernierV
DerniereDistance
AoiDetectee
DernierPointAoiMonde
```

## Utilisé par

```text
DebugManager
heatmap_maker.py
```

---

# 7. AOICaptureExporter

## Rôle

`AOICaptureExporter` génère les captures visuelles des AOI.

Ces captures servent de fond pour les heatmaps Python.

## Responsabilités

```text
recherche AOI
→ caméra temporaire
→ capture image AOI
→ masquage UI/debug/rayons
→ export PNG
```

## Fichiers générés

```text
aoi_capture_*.png
```

## Points importants

Le script masque automatiquement pendant la capture :
- le mesh visible de l’AOI ;
- les textes debug ;
- les Canvas ;
- les rayons XR.

Cela permet d’obtenir une capture propre sans éléments parasites.

---

# 8. RecordingSessionManager

## Rôle

`RecordingSessionManager` gère la session d’enregistrement.

Il centralise :
- le dossier de sortie ;
- le timestamp commun ;
- l’état de session active.

## Responsabilités

```text
start session
→ création dossier horodaté
→ timestamp commun
→ stop session
```

## Données principales exposées

```text
SessionActive
DossierSession
TimestampSession
TempsSession
FrameSession
```

## Utilisé par

```text
RecordDataManager
AOIHeatmapManager
ControllerManager
```

---

# 9. RecordDataManager

## Rôle

`RecordDataManager` écrit le CSV principal des données de regard et des métriques.

## Responsabilités

```text
données regard
+ PERCLOS
+ saccades
+ fixations
→ export CSV
```

## Fichier généré

```text
record_data_*.csv
```

## Colonnes principales

```text
timestamp_sec
gaze_valid
gaze_direction_x/y/z
eyes_closed
perclos_percent
saccade_count
microsaccade_count
saccade_velocity_deg_s
last_saccade_amplitude_deg
fixation_count
current_fixation_duration_ms
last_fixation_duration_ms
```

---

# 10. ControllerManager

## Rôle

`ControllerManager` lit les entrées des manettes VR.

Il déclenche :
- le démarrage de l’enregistrement ;
- l’arrêt de l’enregistrement ;
- l’affichage ou masquage du debug.

## Commandes

Les commandes fonctionnent avec la manette gauche ou droite.

```text
Grip + bouton principal → Start Recording
Grip + bouton secondaire → Stop Recording
Grip + clic joystick → Toggle Debug
```

## Responsabilités

```text
lecture manette gauche
+ lecture manette droite
→ commandes recording
→ toggle debug
→ feedback audio / texte
```

## Utilisé par

```text
RecordingSessionManager
RecordDataManager
AOIHeatmapManager
DebugManager
```

---

# 11. DebugManager

## Rôle

`DebugManager` affiche les informations de debug dans le casque.

Le debug est désactivé par défaut et peut être activé avec :

```text
Grip + clic joystick
```

## Informations affichées

```text
FPS
Enregistrement actif/inactif
PERCLOS
état yeux ouverts/fermés
saccades
microsaccades
fixations
AOI regardée
état tracking AOI
```

## Responsabilités

```text
lecture états managers
→ construction texte debug
→ affichage VR
```

---

# 12. heatmap_maker.py

## Rôle

`heatmap_maker.py` génère les overlays heatmap à partir des exports Unity.

## Entrées attendues

Dans un dossier de session :

```text
aoi_heatmap_data_*.csv
aoi_metadata_*.csv
aoi_capture_*.png
```

## Sortie

```text
Output/
└── overlay_heatmap_*.png
```

## Fonctionnement

```text
lecture CSV AOI
→ filtrage gaze_valid / eyes_closed
→ récupération UV
→ génération heatmap
→ superposition sur capture AOI
→ export PNG
```

## Paramètre à modifier

Dans le script :

```python
dossier_session = r"C:/chemin/vers/la/session"
```

---

# 13. Flux complet des données

```text
Pico Neo 2 Eye
→ GazeManager
→ PerclosManager / SaccadeManager
→ AOIHeatmapManager
→ RecordDataManager
→ CSV + captures AOI
→ heatmap_maker.py
→ Output/overlay_heatmap_*.png
```

---

# 14. Dépendances principales entre scripts

```text
GazeManager
├── utilisé par AOIHeatmapManager
├── utilisé par SaccadeManager
└── utilisé par RecordDataManager

PerclosManager
├── utilisé par RecordDataManager
├── utilisé par AOIHeatmapManager
└── utilisé par DebugManager

SaccadeManager
├── utilisé par RecordDataManager
└── utilisé par DebugManager

RecordingSessionManager
├── utilisé par RecordDataManager
├── utilisé par AOIHeatmapManager
└── utilisé par ControllerManager

ControllerManager
├── démarre/arrête RecordDataManager
├── démarre/arrête AOIHeatmapManager
└── toggle DebugManager
```

---

# 15. Résumé rapide

| Script | Rôle |
|---|---|
| `GazeManager` | lit le regard Pico et affiche le point de regard |
| `PerclosManager` | calcule yeux fermés + PERCLOS |
| `SaccadeManager` | calcule saccades, microsaccades, fixations |
| `AOI_QuadZone` | définit une AOI quadrilatère |
| `AOIHeatmapManager` | détecte les AOI et exporte les UV |
| `AOICaptureExporter` | capture les AOI en PNG |
| `RecordingSessionManager` | crée et synchronise la session |
| `RecordDataManager` | exporte le CSV principal |
| `ControllerManager` | gère les commandes manettes |
| `DebugManager` | affiche les métriques en VR |
| `heatmap_maker.py` | génère les heatmaps finales |
