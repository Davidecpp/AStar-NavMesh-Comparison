using UnityEngine;
using Pathfinding;
using System.Diagnostics;
using System.Collections;

public class AStarNPCController : MonoBehaviour
{
    [Header("Movement Settings")]
    public Transform target;

    [Header("Performance Settings")]
    [SerializeField] private int pathRecalculateFrameInterval = 60;
    [SerializeField] private float movementThreshold = 0.1f;
    [SerializeField] private bool enableDistanceCalculation = true;
    [SerializeField] private float updateFrequency = 0.1f; // Update solo ogni 100ms

    // Cached components
    private Seeker seeker;
    private Animator animator;
    private AIPath aiPath;
    private AStarTimer aStarTimer;

    // Movement tracking
    private Vector3 lastPosition;
    private float distanceTravelled = 0f;
    private bool isMoving = false;
    private bool isInitialized = false;

    // Performance optimizations
    private Stopwatch stopwatch;
    private int frameCounter = 0;
    private bool hasValidPath = false;
    private float lastUpdateTime = 0f;
    private float randomOffset = 0f; // Per distribuire gli update

    // Object pooling support
    private bool isPooled = false;
    private NPCSpawner spawner;

    // Cache per evitare allocazioni
    private Vector3 cachedVelocity;
    private float cachedSpeed;

    void Awake()
    {
        CacheComponents();
        InitializeStopwatch();

        // Distribuisci gli update per evitare picchi di performance
        randomOffset = Random.Range(0f, updateFrequency);
    }

    private void CacheComponents()
    {
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        animator = GetComponent<Animator>();
        aStarTimer = GetComponent<AStarTimer>();

        // Validate components
        if (seeker == null) UnityEngine.Debug.LogError($"Seeker component missing on {gameObject.name}");
        if (aiPath == null) UnityEngine.Debug.LogError($"AIPath component missing on {gameObject.name}");
    }

    private void InitializeStopwatch()
    {
        stopwatch = new Stopwatch();
        lastPosition = transform.position;
        lastUpdateTime = Time.time + randomOffset;
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

        if (target != null)
        {
            CalculatePathToTarget();
        }

        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || target == null) return;

        // Update solo a intervalli regolari invece che ogni frame
        if (Time.time - lastUpdateTime < updateFrequency) return;

        lastUpdateTime = Time.time;
        frameCounter++;

        // Update movement state and animation
        UpdateMovementState();

        // Recalculate path periodically (less frequently)
        if (isMoving && frameCounter % (pathRecalculateFrameInterval / 10) == 0) // Ridotto intervallo
        {
            CalculatePathToTarget();
        }

        // Update distance calculation (optional for performance)
        if (enableDistanceCalculation && isMoving)
        {
            UpdateDistanceTravelled();
        }
    }

    private void UpdateMovementState()
    {
        // Cache velocity per evitare accessi multipli
        cachedVelocity = aiPath.velocity;
        cachedSpeed = cachedVelocity.magnitude;

        bool wasMoving = isMoving;
        isMoving = cachedSpeed > movementThreshold;

        // Update animator solo quando speed cambia significativamente
        if (animator != null && Mathf.Abs(animator.GetFloat("Speed") - cachedSpeed) > 0.1f)
        {
            animator.SetFloat("Speed", cachedSpeed);
        }

        // Reset frame counter when movement state changes
        if (wasMoving != isMoving)
        {
            frameCounter = 0;
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

    public void CalculatePathToTarget()
    {
        if (!ValidateComponents()) return;

        // Set destination
        aiPath.destination = target.position;
        aiPath.canMove = true;

        // Start timing
        stopwatch.Restart();

        // Calculate path
        seeker.StartPath(transform.position, target.position, OnPathComplete);
    }

    private bool ValidateComponents()
    {
        if (seeker == null)
        {
            UnityEngine.Debug.LogError($"Seeker component missing on {gameObject.name}");
            return false;
        }

        if (target == null)
        {
            UnityEngine.Debug.LogWarning($"Target not assigned on {gameObject.name}");
            return false;
        }

        return true;
    }

    private void OnPathComplete(Path path)
    {
        stopwatch.Stop();
        hasValidPath = !path.error;

        if (path.error)
        {
            UnityEngine.Debug.LogWarning($"Path calculation failed for {gameObject.name}: {path.errorLog}");
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
        lastUpdateTime = Time.time + randomOffset;

        // Reset pathfinding
        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.destination = transform.position;
        }

        // Reset stopwatch
        if (stopwatch != null)
        {
            stopwatch.Reset();
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
            CalculatePathToTarget();
        }
    }

    // Optimized getters
    public float GetDistance() => distanceTravelled;
    public double GetCalcTime() => stopwatch.Elapsed.TotalMilliseconds;
    public float GetPathTime() => aStarTimer?.CurrentTimeSeconds ?? 0f;
    public bool HasValidPath() => hasValidPath;
    public bool IsMoving() => isMoving;

    // Cleanup
    private void OnDestroy()
    {
        if (stopwatch != null)
        {
            stopwatch.Stop();
        }
    }

    // For object pooling
    public void ReturnToPool()
    {
        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject, false);
        }
    }

    public void SetSpawner(NPCSpawner spawner)
    {
        this.spawner = spawner;
        isPooled = true;
    }
}