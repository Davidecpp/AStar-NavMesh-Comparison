using UnityEngine;
using static Heatmap;

public class NPCMovementTracker : MonoBehaviour
{
    public Heatmap heatmapManager;
    public string npcName = "NPC";

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
            if (npcName.Equals("Navmesh"))
            {
                heatmapManager.RegisterPosition(transform.position, HeatmapType.NavMesh);
            }
            else if(npcName.Equals("AStar"))
            {
                heatmapManager.RegisterPosition(transform.position, HeatmapType.AStar);
            }
            else
            {
                heatmapManager.RegisterPosition(transform.position, HeatmapType.All);
            }
    }

}