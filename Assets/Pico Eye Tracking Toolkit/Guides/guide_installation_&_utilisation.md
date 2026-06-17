# Pico Eye Tracking Toolkit — Guide installation & utilisation

Package Unity pour le suivi du regard sur Pico Neo 2 Eye.

Fonctions :
- suivi du regard ;
- point du regard VR ;
- AOI ;
- captures AOI ;
- heatmaps ;
- PERCLOS ;
- saccades, microsaccades, fixations ;
- exports CSV synchronisés.

---

## Structure du package

Assets/
├── Prefabs/
│   ├── AOI
│   ├── EventSystem
│   ├── Managers
│   └── XR Origin EyeTracking
│
├── Scripts/
│   ├── AOI/
│   │   ├── AOI_QuadZone.cs
│   │   └── AOICaptureExporter.cs
│   │
│   ├── Managers/
│   │   ├── AOIHeatmapManager.cs
│   │   ├── DebugManager.cs
│   │   ├── GazeManager.cs
│   │   ├── PerclosManager.cs
│   │   ├── RecordDataManager.cs
│   │   ├── RecordingControlManager.cs
│   │   ├── RecordingSessionManager.cs
│   │   └── SaccadeManager.cs
│   │
│   └── Python/
│       └── Python_heatmap_maker.ipynb
│
├── Sounds/
│   ├── beep_error
│   ├── beep_start_recording
│   └── beep_stop_recording
│
├── Materials/
└── package.json

# 1. Installation du package

Dans votre projet Unity :

Assets
→ Import Package
→ Custom Package

Sélectionner PicoEyeTrackingToolkit.unitypackage
Puis Import All

Après import

les dossiers suivants sont importés dans les assets :

Assets/Prefabs
Assets/Scripts
Assets/Materials
Assets/Sounds

---

# 2. Prérequis Unity
Unity testé 2021.3.11f1

Packages à installer/importer :

- PicoXR_Platform_SDK-1.2.3_B55
- XR Interaction Toolkit
- Input System

- remplacer DroneMover_VRInputs


---

# 3. Configuration Pico XR

Dans Unity :
Project Settings → XR Plug-in Management
Activer Pico

---

# 4. Configuration Android

Dans File → Build Settings
Sélectionner Android → Switch Platform

Puis dans Project Settings → Player
Configurer:
Scripting Backend = IL2CPP
Target Architectures = ARM64
Texture Compression = ASTC
Color Space = Linear
Minimum API Level = Android 10 / API 29
Target API Level = Automatic / Highest installed

---

# 5. Désactiver Vulkan

Dans Project Settings → Player → Other Settings
Désactiver Auto Graphics API

Supprimer Vulkan

Garder uniquement OpenGLES3

---

# 6. Mise en place dans la scène

Glisser dans la scène :

XR Origin EyeTracking (doit comporter PXR Manager, Input Action Manager, XR Origin, XR Interaction Manager)
EventSystem
Managers
AOI

---

# 7. EventSystem

L’`EventSystem` doit utiliser le script XR UI Input Module

---

# 8. Managers

Le prefab `Managers` doit être présent une seule fois dans la scène.

Il contient :

```text
GazeManager
AOIHeatmapManager
AOICaptureExporter
RecordDataManager
RecordingSessionManager
ControllerManager
PerclosManager
SaccadeManager
DebugManager
```

---

# 9. AOI

Chaque AOI doit contenir :
MeshFilter
MeshRenderer
MeshCollider
AOI_QuadZone

Configuration du `Layer` :
Créer un layer AOI et l’assigner à toutes les AOI.

Les AOI doivent être détectées uniquement par le raycast regard.

Dans AOIHeatmapManager :
Masque Collision = AOI

Important :
Le layer AOI ne doit PAS être utilisé dans le Raycast Mask des XR Ray Interactor des manettes.

Sinon :
- les rayons traversent l’UI ;
- les interactions boutons deviennent incorrectes ;
- les rayons semblent cliquer derrière l’écran.


Configuration du `MeshCollider` :
Convex = True
Is Trigger = True

Configuration du `MeshRenderer`:
Soit vous le desactivez, soit vous supprimer le Material, sinon, la zone est visible et cache l'AOI visée



Chaque AOI doit avoir un identifiant unique par exemple :
AoiId = AOI_1
AoiId = AOI_2
AoiId = AOI_3

---

# 10. Point du regard

Le point du regard est géré par `GazeManager`.

Réglages utiles :
Afficher Point Regard
Taille Point
Couleur Point
Afficher Zone Morte
Zone Morte Pixels
Lissage
Plane Distance

Valeur conseillée :
Plane Distance ≈ 0.3 à 0.5

---

# 11. Contrôles manettes

Fonctionne avec la manette gauche ou droite:

Grip + bouton principal (A ou X) → Start Recording
Grip + bouton secondaire (B ou Y) → Stop Recording
Grip + clic joystick → afficher/masquer le debug

---

# 12. Debug

Le debug est désactivé par défaut.

Le debug affiche :
FPS
PERCLOS
état des yeux
saccades
microsaccades
fixations
AOI regardée
état du tracking

---

# 13. Enregistrement

Au démarrage :
- création du dossier session ;
- export CSV ;
- captures AOI ;
- synchronisation timestamps.

---

# 14. Dossier de sortie casque

/storage/emulated/0/Download/EyeTracking/

---

# 15. Fichiers générés

record_data_*.csv
aoi_heatmap_data_*.csv
aoi_metadata_*.csv
aoi_capture_*.png

---

# 16. Génération des heatmaps

Ouvrir heatmap_maker.py

Modifier dossier_session = r"C:/chemin/vers/la/session"

Puis lancer le script.

Résultats dans C:/chemin/vers/la/session/Output/

---

# 17. Utilisation rapide

1. Mettre le casque.
2. Lancer la scène.
3. `Grip + clic joystick` → debug ON/OFF.
4. `Grip + bouton principal` → démarrer.
5. Regarder les AOI.
6. `Grip + bouton secondaire` → arrêter.
7. Récupérer le dossier session.
8. Lancer `heatmap_maker.py`.
9. Lire les résultats dans `Output/`.