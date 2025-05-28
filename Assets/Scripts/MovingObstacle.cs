using UnityEngine;
using UnityEngine.AI;
using Pathfinding;
using Unity.AI.Navigation;

public class MovingObstacle : MonoBehaviour
{
    public Vector3 startPosition;
    public Vector3 endPosition;
    public float speed = 2f;
    public NavMeshSurface navMeshSurface;

    public delegate void OnNavMeshUpdated();
    public static event OnNavMeshUpdated NavMeshUpdated;

    public float updateInterval = 0.5f;
    private float timer = 0f;
    private Vector3 lastPosition;

    void Start()
    {
        startPosition = transform.position;
        endPosition = startPosition + new Vector3(5, 0, 0);
        lastPosition = transform.position;
    }

    void Update()
    {
        // Muovi l'ostacolo tra startPosition e endPosition
        transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.PingPong(Time.time * speed, 1));

        timer += Time.deltaTime;
        if (timer >= updateInterval && Vector3.Distance(transform.position, lastPosition) > 0.1f)
        {
            timer = 0f;
            lastPosition = transform.position;

            // Aggiorna A*
            if (TryGetComponent(out Collider col))
                AstarPath.active.UpdateGraphs(col.bounds);

            // Aggiorna NavMesh
            if (navMeshSurface != null)
            {
                navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
                NavMeshUpdated?.Invoke(); // Notifica gli NPC
            }
        }
    }

}
