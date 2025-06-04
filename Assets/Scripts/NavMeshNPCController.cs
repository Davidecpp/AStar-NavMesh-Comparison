using UnityEngine;
using UnityEngine.AI;
using System.Diagnostics;
using System.Collections;

public class NavMeshNPCController : MonoBehaviour
{
    [Header("Movement Settings")]
    public Transform target;

    [Header("Performance Settings")]
    [SerializeField] private float movementThreshold = 0.1f;
    [SerializeField] private float destinationOffset = 2f;
    [SerializeField] private bool enableDistanceCalculation = true;
    [SerializeField] private int pathCheckFrameInterval = 30;

    // Cached components
    private NavMeshAgent agent;
    private Animator animator;

    // Movement tracking
    private Stopwatch movementStopwatch;
    private bool isMoving = false;
    private Vector3 lastPosition;
    private float distanceTravelled = 0f;
    private double lastCalcTime = 0.0;
    private bool isInitialized = false;

    // Performance optimizations
    private int frameCounter = 0;
    private bool hasValidPath = false;
    private bool isDestroyed = false;

    // Object pooling support
    private bool isPooled = false;
    private NPCSpawner spawner;

    void Awake()
    {
        CacheComponents();
        InitializeTracking();
    }

    private void CacheComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Validate components
        if (agent == null) UnityEngine.Debug.LogError($"NavMeshAgent component missing on {gameObject.name}");
    }

    private void InitializeTracking()
    {
        movementStopwatch = new Stopwatch();
        lastPosition = transform.position;
    }

    void Start()
    {
        if (!isInitialized)
        {
            StartCoroutine(InitializeDelayed());
        }
    }

    private IEnumerator InitializeDelayed()
    {
        // Wait a random delay to spread initialization across frames
        yield return new WaitForSeconds(Random.Range(0.01f, 0.05f));

        // Subscribe to NavMesh updates
        WallPlacer.NavMeshUpdated += OnNavMeshUpdated;

        if (target != null)
        {
            SetNewTarget(target);
        }

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || target == null || agent == null) return;

        frameCounter++;

        // Check movement state periodically (not every frame)
        if (frameCounter % pathCheckFrameInterval == 0)
        {
            UpdateMovementState();
        }

        // Update distance calculation (optional for performance)
        if (enableDistanceCalculation && isMoving)
        {
            UpdateDistanceTravelled();
        }

        // Update animator
        UpdateAnimator();
    }

    private void UpdateMovementState()
    {
        if (agent == null) return;

        bool wasMoving = isMoving;

        // Check if should start moving
        if (!isMoving && ShouldStartMoving())
        {
            movementStopwatch.Restart();
            isMoving = true;
        }

        // Check if should stop moving
        if (isMoving && ShouldStopMoving())
        {
            movementStopwatch.Stop();
            isMoving = false;
            StopAgent();
        }
    }

    private bool ShouldStartMoving()
    {
        return agent.hasPath &&
               agent.remainingDistance > agent.stoppingDistance &&
               agent.velocity.sqrMagnitude > movementThreshold * movementThreshold;
    }

    private bool ShouldStopMoving()
    {
        return !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance &&
               (!agent.hasPath || agent.velocity.sqrMagnitude < movementThreshold * movementThreshold);
    }

    private void StopAgent()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void UpdateDistanceTravelled()
    {
        Vector3 currentPosition = transform.position;
        float delta = Vector3.Distance(currentPosition, lastPosition);

        // Only update if movement is significant
        if (delta > 0.01f)
        {
            distanceTravelled += delta;
            lastPosition = currentPosition;
        }
    }

    private void UpdateAnimator()
    {
        if (animator != null && agent != null)
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }
    }

    private void OnNavMeshUpdated()
    {
        if (isDestroyed || target == null || agent == null) return;

        // Recalculate path when NavMesh is updated
        agent.ResetPath();
        agent.SetDestination(target.position);
    }

    public void SetNewTarget(Transform newTarget)
    {
        target = newTarget;
        if (agent == null || target == null) return;

        // Calculate destination with random offset
        Vector2 offset2D = Random.insideUnitCircle * destinationOffset;
        Vector3 offset = new Vector3(offset2D.x, 0, offset2D.y);
        Vector3 destination = target.position + offset;

        // Calculate path with timing
        NavMeshPath path = new NavMeshPath();
        Stopwatch stopwatch = new Stopwatch();

        stopwatch.Start();
        bool success = NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path);
        stopwatch.Stop();

        lastCalcTime = stopwatch.Elapsed.TotalMilliseconds;
        hasValidPath = success;

        if (success)
        {
            agent.SetPath(path);
            agent.isStopped = false;
        }
        else
        {
            UnityEngine.Debug.LogWarning($"NavMesh path calculation failed for {gameObject.name}");
        }
    }

    // Object pooling support
    public void ResetNPC()
    {
        // Reset movement state
        isMoving = false;
        distanceTravelled = 0f;
        lastPosition = transform.position;
        frameCounter = 0;
        hasValidPath = false;
        isInitialized = false;

        // Reset agent
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = false;
            agent.velocity = Vector3.zero;
        }

        // Reset stopwatch
        if (movementStopwatch != null)
        {
            movementStopwatch.Reset();
        }

        // Reset animator
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (isInitialized && target != null)
        {
            SetNewTarget(target);
        }
    }

    // Optimized getters
    public float GetDistance() => distanceTravelled;
    public double GetPathTime() => movementStopwatch?.Elapsed.TotalSeconds ?? 0.0;
    public double GetCalcTime() => lastCalcTime;
    public bool HasValidPath() => hasValidPath;
    public bool IsMoving() => isMoving;

    // Cleanup
    private void OnDestroy()
    {
        isDestroyed = true;

        // Unsubscribe from events
        WallPlacer.NavMeshUpdated -= OnNavMeshUpdated;

        // Stop stopwatch
        if (movementStopwatch != null)
        {
            movementStopwatch.Stop();
        }
    }

    // For object pooling
    public void ReturnToPool()
    {
        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject, true);
        }
    }

    public void SetSpawner(NPCSpawner spawner)
    {
        this.spawner = spawner;
        isPooled = true;
    }
}