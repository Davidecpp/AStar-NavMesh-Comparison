using UnityEngine;
using UnityEngine.AI;
using Pathfinding;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class PathVisualizer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private NavMeshAgent navMeshAgent;
    private Seeker seeker;
    private ABPath lastRenderedPath;
    private Vector3[] pathPositionsCache;

    public bool ShowPaths = true;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        seeker = GetComponent<Seeker>();
        StartCoroutine(UpdatePathCoroutine());

        UpdatePathVisibility();
    }

    // Coroutine to update the path at regular intervals
    IEnumerator UpdatePathCoroutine()
    {
        while (true)
        {
            if (ShowPaths) 
            {
                UpdatePath();
            }
            else
            {
                lineRenderer.positionCount = 0;
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    void UpdatePath()
    {
        // If paths are not visible, skip the update
        if (!ShowPaths) return;

        // Clear the line renderer if no path is available
        if (navMeshAgent != null && navMeshAgent.hasPath)
        {
            NavMeshPath path = navMeshAgent.path;
            int count = path.corners.Length;

            if (pathPositionsCache == null || pathPositionsCache.Length != count)
                pathPositionsCache = new Vector3[count];

            for (int i = 0; i < count; i++)
                pathPositionsCache[i] = path.corners[i];

            lineRenderer.positionCount = count;
            lineRenderer.SetPositions(pathPositionsCache);
        }
        else if (seeker != null)
        {
            ABPath currentPath = seeker.GetCurrentPath() as ABPath;

            if (currentPath == null || currentPath.vectorPath == null || currentPath.vectorPath.Count <= 1)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            // Check if the path has changed before updating the line renderer
            if (currentPath != lastRenderedPath)
            {
                lastRenderedPath = currentPath;
                int count = currentPath.vectorPath.Count;

                if (pathPositionsCache == null || pathPositionsCache.Length != count)
                    pathPositionsCache = new Vector3[count];

                for (int i = 0; i < count; i++)
                    pathPositionsCache[i] = currentPath.vectorPath[i];

                lineRenderer.positionCount = count;
                lineRenderer.SetPositions(pathPositionsCache);
            }
        }
        else
        {
            lineRenderer.positionCount = 0;
        }
    }

    private void UpdatePathVisibility()
    {
        if (!ShowPaths)
        {
            lineRenderer.positionCount = 0;
        }
    }

    public void SetPathsVisible(bool visible)
    {
        ShowPaths = visible;
        UpdatePathVisibility();
    }

    // Unity lifecycle methods to register and unregister the visualizer with the PathVisibilityController
    void OnEnable()
    {
        PathVisibilityController.Instance?.RegisterVisualizer(this);
    }

    void OnDisable()
    {
        PathVisibilityController.Instance?.UnregisterVisualizer(this);
    }
}