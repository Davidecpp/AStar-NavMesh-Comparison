using UnityEngine;

public class NPCMovementTracker : MonoBehaviour
{
    public Heatmap heatmapManager;

    void Start()
    {
        if (heatmapManager == null)
        {
            heatmapManager = FindObjectOfType<Heatmap>();
            if (heatmapManager == null)
                Debug.LogError("HeatmapManager non trovato!");
        }
    }

    void Update()
    {
        if (heatmapManager != null)
            heatmapManager.RegisterPosition(transform.position);
    }

}
