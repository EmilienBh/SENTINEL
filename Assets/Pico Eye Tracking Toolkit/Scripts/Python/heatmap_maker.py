#%%

"""
Génère des heatmaps AOI à partir d'un dossier de session Unity.

Entrée attendue :
- aoi_heatmap_data_*.csv
- aoi_metadata_*.csv
- aoi_capture_*.png

Usage :
    python heatmap_maker.py "C:/chemin/vers/session"
"""

import glob
import os

import matplotlib.cm as cm
import numpy as np
import pandas as pd
from PIL import Image
from scipy.ndimage import gaussian_filter


def trouver_premier(pattern: str) -> str:
    fichiers = glob.glob(pattern)
    if not fichiers:
        raise FileNotFoundError(f"Aucun fichier trouvé pour : {pattern}")
    return fichiers[0]


def trouver_capture(captures, aoi_id: str):
    prefixe = f"aoi_capture_{aoi_id}_"
    for chemin in captures:
        if os.path.basename(chemin).startswith(prefixe):
            return chemin
    return None


def creer_heatmap_rectangulaire(u, v, largeur=1024, hauteur=1024, sigma=18, alpha_max=0.70, colormap="jet"):
    heatmap, _, _ = np.histogram2d(u, v, bins=[largeur, hauteur], range=[[0, 1], [0, 1]])
    heatmap = gaussian_filter(heatmap, sigma=sigma)

    if heatmap.max() > 0:
        heatmap = heatmap / heatmap.max()

    heatmap_img = np.flipud(heatmap.T)
    rgba = cm.get_cmap(colormap)(heatmap_img)
    rgba[:, :, 3] = heatmap_img * alpha_max
    return (rgba * 255).astype(np.uint8)


def generer_heatmaps(dossier_session: str, sigma=18, alpha_max=0.70, colormap="jet"):
    dossier_sortie = os.path.join(dossier_session, "Output")
    os.makedirs(dossier_sortie, exist_ok=True)

    csv_donnees = trouver_premier(os.path.join(dossier_session, "aoi_heatmap_data*.csv"))
    csv_metadata = trouver_premier(os.path.join(dossier_session, "aoi_metadata*.csv"))
    captures = glob.glob(os.path.join(dossier_session, "aoi_capture_*.png"))

    donnees = pd.read_csv(csv_donnees)
    metadata = pd.read_csv(csv_metadata)

    donnees = donnees[(donnees["gaze_valid"] == 1) & (donnees["eyes_closed"] == 0)]
    donnees = donnees[(donnees["aoi_uv_x"].between(0, 1)) & (donnees["aoi_uv_y"].between(0, 1))]

    for _, ligne in metadata.iterrows():
        aoi_id = str(ligne["aoi_id"])
        donnees_aoi = donnees[donnees["aoi_id"] == aoi_id]

        if donnees_aoi.empty:
            print(f"Aucune donnée pour {aoi_id}")
            continue

        capture = trouver_capture(captures, aoi_id)
        if capture is None:
            print(f"Capture introuvable pour {aoi_id}")
            continue

        fond = Image.open(capture).convert("RGBA")
        largeur, hauteur = fond.size

        heatmap = creer_heatmap_rectangulaire(
            donnees_aoi["aoi_uv_x"].values,
            donnees_aoi["aoi_uv_y"].values,
            largeur=largeur,
            hauteur=hauteur,
            sigma=sigma,
            alpha_max=alpha_max,
            colormap=colormap,
        )

        overlay = Image.alpha_composite(fond, Image.fromarray(heatmap, mode="RGBA"))
        chemin_sortie = os.path.join(dossier_sortie, f"overlay_heatmap_{aoi_id}.png")
        overlay.save(chemin_sortie)
        print(f"{aoi_id} -> {chemin_sortie}")


def main():

    dossier_session = r"C:\Users\ebonhomm\Desktop\20260602_114455"
    

    generer_heatmaps(
        dossier_session,
        sigma=18,
        alpha_max=0.70,
        colormap="jet"
    )

if __name__ == "__main__":
    main()
# %%
