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

    // Aggiungi questa variabile per lo stato della checkbox
    public bool ShowPaths = true;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        seeker = GetComponent<Seeker>();
        StartCoroutine(UpdatePathCoroutine());

        // Imposta inizialmente la visibilità in base alla checkbox
        UpdatePathVisibility();
    }

    IEnumerator UpdatePathCoroutine()
    {
        while (true)
        {
            if (ShowPaths) // Solo aggiornare il percorso se è visibile
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
        if (!ShowPaths) return; // Non fare nulla se i percorsi sono nascosti

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

    // Metodo per aggiornare la visibilità in base alla checkbox
    private void UpdatePathVisibility()
    {
        if (!ShowPaths)
        {
            lineRenderer.positionCount = 0;
        }
    }

    // Aggiungi questo metodo per permettere di cambiare la visibilità da altri script
    public void SetPathsVisible(bool visible)
    {
        ShowPaths = visible;
        UpdatePathVisibility();
    }

    void OnEnable()
    {
        PathVisibilityController.Instance?.RegisterVisualizer(this);
    }

    void OnDisable()
    {
        PathVisibilityController.Instance?.UnregisterVisualizer(this);
    }
}