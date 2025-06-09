using UnityEngine;
using Pathfinding;
using System.Diagnostics;
using System.Collections;

public class AStarNPCController : MonoBehaviour
{
    [Header("Movement Settings")]
    public Transform target;

    [Header("Performance Settings")]
    [SerializeField] private int pathRecalculateFrameInterval = 300;
    [SerializeField] private float movementThreshold = 0.1f;
    [SerializeField] private bool enableDistanceCalculation = true;
    [SerializeField] private float updateFrequency = 0.5f;

    [Header("Destination Settings")]
    [SerializeField] private float arrivalDistance = 2f; 
    [SerializeField] private bool stopAtDestination = true;

    // Cached components
    private Seeker seeker;
    private Animator animator;
    private AIPath aiPath;
    private AStarTimer aStarTimer;
    private NPCAvoidance npcAvoidance; 

    // Movement tracking
    private Vector3 lastPosition;
    private float distanceTravelled = 0f;
    private bool isMoving = false;
    private bool isInitialized = false;
    private bool hasArrivedAtDestination = false;

    // Performance optimizations
    private Stopwatch stopwatch;
    private int frameCounter = 0;
    private bool hasValidPath = false;
    private float lastUpdateTime = 0f;
    private float randomOffset = 0f; // For spreading updates

    // Object pooling support
    private bool isPooled = false;
    private NPCSpawner spawner;

    // Cache for performance
    private Vector3 cachedVelocity;
    private float cachedSpeed;
    private float distanceToTarget;

    void Awake()
    {
        CacheComponents();
        InitializeStopwatch();

        // Distribute updates over time
        randomOffset = Random.Range(0f, updateFrequency);
    }

    private void CacheComponents()
    {
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        animator = GetComponent<Animator>();
        aStarTimer = GetComponent<AStarTimer>();
        npcAvoidance = GetComponent<NPCAvoidance>();

        // Validate components
        if (seeker == null) UnityEngine.Debug.LogError($"Seeker component missing on {gameObject.name}");
        if (aiPath == null) UnityEngine.Debug.LogError($"AIPath component missing on {gameObject.name}");

        // Configure AIPath
        if (aiPath != null)
        {
            aiPath.endReachedDistance = arrivalDistance;
            aiPath.slowdownDistance = arrivalDistance * 2f;
        }
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

        // Update at specified frequency
        if (Time.time - lastUpdateTime < updateFrequency) return;

        lastUpdateTime = Time.time;
        frameCounter++;

        if (target != null)
        {
            distanceToTarget = Vector3.Distance(transform.position, target.position);
        }

        CheckArrivalAtDestination();

        // Update movement state and animation
        if (!hasArrivedAtDestination)
        {
            UpdateMovementState();

            // Recalculate path periodically
            if (isMoving && frameCounter % (pathRecalculateFrameInterval / 10) == 0)
            {
                CalculatePathToTarget();
            }
        }
        else
        {
            StopMovement();
        }

        // Update distance calculation
        if (enableDistanceCalculation && isMoving)
        {
            UpdateDistanceTravelled();
        }
    }

    private void CheckArrivalAtDestination()
    {
        if (target == null) return;

        bool wasArrived = hasArrivedAtDestination;

        // Check if arrived based on distance and AIPath state
        hasArrivedAtDestination = (distanceToTarget <= arrivalDistance) ||
                                 (aiPath != null && aiPath.reachedDestination);

        // If just arrived, force the avoidance system to stop
        if (!wasArrived && hasArrivedAtDestination && stopAtDestination)
        {
            if (npcAvoidance != null)
            {
                npcAvoidance.ForceStop();
            }

            StopMovement();
        }
        // If previously arrived but now moving, resume movement
        else if (wasArrived && !hasArrivedAtDestination)
        {
            ResumeMovement();
        }
    }

    private void StopMovement()
    {
        // Deactivate all colliders
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Update animator
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }

        if (aStarTimer != null)
        {
            aStarTimer.ForceStopTimer();
        }

        isMoving = false;
    }

    private void ResumeMovement()
    {
        if (aiPath != null)
        {
            aiPath.canMove = true;
        }
        if (target != null)
        {
            CalculatePathToTarget();
        }
    }

    private void UpdateMovementState()
    {
        if (aiPath == null) return;

        // Cache velocity for performance
        cachedVelocity = aiPath.velocity;
        cachedSpeed = cachedVelocity.magnitude;

        bool wasMoving = isMoving;
        isMoving = cachedSpeed > movementThreshold && !hasArrivedAtDestination;

        // Update animator when movement state changes
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
        if (!ValidateComponents() || hasArrivedAtDestination) return;

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
        hasArrivedAtDestination = false;
        distanceTravelled = 0f;
        lastPosition = transform.position;
        frameCounter = 0;
        hasValidPath = false;
        isInitialized = false;
        lastUpdateTime = Time.time + randomOffset;
        distanceToTarget = float.MaxValue;

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

        // Reset avoidance
        if (npcAvoidance != null)
        {
            npcAvoidance.ForceDestinationCheck();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        hasArrivedAtDestination = false;

        if (isInitialized && target != null)
        {
            CalculatePathToTarget();
        }
    }

    // Set the distance at which the NPC will consider itself arrived at the target
    public void SetArrivalDistance(float distance)
    {
        arrivalDistance = Mathf.Max(0.1f, distance);

        // Update AIPath settings
        if (aiPath != null)
        {
            aiPath.endReachedDistance = arrivalDistance;
            aiPath.slowdownDistance = arrivalDistance * 2f;
        }

        // This is to ensure that the avoidance system respects the new arrival distance
        if (npcAvoidance != null)
        {
            npcAvoidance.SetDestinationRadius(arrivalDistance * 1.5f);
        }
    }

    // Optimized getters
    public float GetDistance() => distanceTravelled;
    public double GetCalcTime() => stopwatch.Elapsed.TotalMilliseconds;
    public float GetPathTime() => aStarTimer?.CurrentTimeSeconds ?? 0f;
    public bool HasValidPath() => hasValidPath;
    public bool IsMoving() => isMoving;
    public bool HasArrived() => hasArrivedAtDestination;
    public float GetDistanceToTarget() => distanceToTarget;

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