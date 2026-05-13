using UnityEngine;

public class AOIHeatmapUI : MonoBehaviour
{
    [SerializeField] private AOIHeatmapManager aoiHeatmapManager;

    private void Awake()
    {
        if (aoiHeatmapManager == null)
            aoiHeatmapManager = FindObjectOfType<AOIHeatmapManager>();
    }

    public void ExporterHeatmaps()
    {
        if (aoiHeatmapManager == null)
            return;

        aoiHeatmapManager.ExporterToutesLesZones();
    }

    public void ResetHeatmaps()
    {
        if (aoiHeatmapManager == null)
            return;

        aoiHeatmapManager.ReinitialiserToutesLesZones();
    }
}