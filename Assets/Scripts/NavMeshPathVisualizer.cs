using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(LineRenderer), typeof(NavMeshAgent))]
public class NavMeshPathLine : MonoBehaviour
{
    private NavMeshAgent agent;
    private LineRenderer lineRenderer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (agent.hasPath)
        {
            NavMeshPath path = agent.path;
            lineRenderer.positionCount = path.corners.Length;
            lineRenderer.SetPositions(path.corners);
        }
        else
        {
            lineRenderer.positionCount = 0;
        }

    }
}
