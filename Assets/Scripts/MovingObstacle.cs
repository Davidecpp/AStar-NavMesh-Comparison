using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Pathfinding;

[RequireComponent(typeof(Collider))]
public class MovingObstacle : MonoBehaviour
{
    public Vector3 startPosition;
    public Vector3 endPosition;
    public float speed = 2f;

    public NavMeshSurface navMeshSurface;

    public delegate void OnNavMeshUpdated();
    public static event OnNavMeshUpdated NavMeshUpdated;

    public float updateInterval = 2f; 
    private float timer = 0f;
    private Vector3 lastPosition;

    private Collider obstacleCollider;

    void Start()
    {
        // Initialize start and end positions
        startPosition = transform.position;
        endPosition = startPosition + new Vector3(5f, 0f, 0f);
        lastPosition = transform.position;

        obstacleCollider = GetComponent<Collider>();
        if (obstacleCollider == null)
        {
            Debug.LogError("MovingObstacle richiede un Collider!");
        }
    }

    void Update()
    {
        // Movement oscillates between startPosition and endPosition
        transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.PingPong(Time.time * speed, 1));

        timer += Time.deltaTime;

        if (timer >= updateInterval && Vector3.Distance(transform.position, lastPosition) > 0.1f)
        {
            timer = 0f;
            lastPosition = transform.position;

            UpdateAStarGraph();
            UpdateNavMesh();
        }
    }

    // Update A* Graph and NavMesh when the obstacle moves
    void UpdateAStarGraph()
    {
        if (AstarPath.active != null && obstacleCollider != null)
        {
            Bounds bounds = obstacleCollider.bounds;
            GraphUpdateObject guo = new GraphUpdateObject(bounds);
            AstarPath.active.UpdateGraphs(guo);
            Debug.Log("A* Graph aggiornato");
        }
    }

    void UpdateNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            Debug.Log("NavMesh aggiornato");
            NavMeshUpdated?.Invoke();
        }
    }
}
