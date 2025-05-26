using UnityEngine;
using Pathfinding;

[RequireComponent(typeof(LineRenderer), typeof(Seeker))]
public class AStarPathVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Seeker seeker;
    private ABPath currentPath;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        seeker = GetComponent<Seeker>();
    }

    void Update()
    {
        // Ottieni il path attuale dal seeker
        var path = seeker.GetCurrentPath() as ABPath;

        if (path != null && path.vectorPath != null && path.vectorPath.Count > 1)
        {
            currentPath = path;

            lineRenderer.positionCount = currentPath.vectorPath.Count;
            lineRenderer.SetPositions(currentPath.vectorPath.ToArray());
        }
        else
        {
            lineRenderer.positionCount = 0;
        }
    }
}
