using UnityEngine;
using Pathfinding;

[RequireComponent(typeof(LineRenderer), typeof(Seeker))]
public class AStarPathVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Seeker seeker;
    private ABPath lastRenderedPath;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        seeker = GetComponent<Seeker>();
    }

    void Update()
    {
        ABPath currentPath = seeker.GetCurrentPath() as ABPath;

        // Evita ricalcoli inutili se il path è lo stesso o non valido
        if (currentPath == null || currentPath.vectorPath == null || currentPath.vectorPath.Count <= 1)
        {
            if (lineRenderer.positionCount != 0)
                lineRenderer.positionCount = 0;
            return;
        }

        // Aggiorna solo se il path è cambiato
        if (currentPath != lastRenderedPath)
        {
            lastRenderedPath = currentPath;

            int count = currentPath.vectorPath.Count;
            lineRenderer.positionCount = count;

            // Evita allocazione: usa array temporaneo se proprio necessario
            for (int i = 0; i < count; i++)
            {
                lineRenderer.SetPosition(i, currentPath.vectorPath[i]);
            }
        }
    }
}
