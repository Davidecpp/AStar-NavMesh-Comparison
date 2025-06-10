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

    [Header("Physics Settings")]
    [SerializeField] private float maxVelocity = 10f;

    [Header("Stuck Detection & Recovery")]
    [SerializeField] private float stuckCheckDistance = 0.3f; // Aumentato per essere più sensibile
    [SerializeField] private float stuckTimeThreshold = 1.5f; // Ridotto per intervento più rapido
    [SerializeField] private float unstuckForce = 8f;
    [SerializeField] private float cornerDetectionRadius = 1.5f;
    [SerializeField] private int maxUnstuckAttempts = 5;
    [SerializeField] private float pathRecalculateDelay = 0.3f;
    [SerializeField] private LayerMask obstacleLayerMask = -1;

    // Cached components
    private Seeker seeker;
    private Animator animator;
    private AIPath aiPath;
    private AStarTimer aStarTimer;
    private NPCAvoidance npcAvoidance;
    private Rigidbody rb;

    // Movement tracking
    private Vector3 lastPosition;
    private float distanceTravelled = 0f;
    private bool isMoving = false;
    private bool isInitialized = false;
    private bool hasArrivedAtDestination = false;

    // Enhanced stuck detection
    private Vector3 lastStuckCheckPosition;
    private float stuckTimer = 0f;
    private bool isStuck = false;
    private int unstuckAttempts = 0;
    private float lastUnstuckTime = 0f;
    private Vector3[] recentPositions = new Vector3[5]; // Track recent positions
    private int positionIndex = 0;
    private float cornerStuckTimer = 0f;
    private bool isInCorner = false;
    private Vector3 lastTargetPosition;
    private float noProgressTimer = 0f;

    // Performance optimizations
    private Stopwatch stopwatch;
    private int frameCounter = 0;
    private bool hasValidPath = false;
    private float lastUpdateTime = 0f;
    private float randomOffset = 0f;

    // Object pooling support
    private bool isPooled = false;
    private NPCSpawner spawner;

    // Cache for performance
    private Vector3 cachedVelocity;
    private float cachedSpeed;
    private float distanceToTarget;
    private float lastDistanceToTarget;

    void Awake()
    {
        CacheComponents();
        InitializeStopwatch();

        // Distribute updates over time
        randomOffset = Random.Range(0f, updateFrequency);

        // Initialize position tracking
        Vector3 startPos = transform.position;
        for (int i = 0; i < recentPositions.Length; i++)
        {
            recentPositions[i] = startPos;
        }
    }

    private void CacheComponents()
    {
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        animator = GetComponent<Animator>();
        aStarTimer = GetComponent<AStarTimer>();
        npcAvoidance = GetComponent<NPCAvoidance>();
        rb = GetComponent<Rigidbody>();

        // Validate components
        if (seeker == null) UnityEngine.Debug.LogError($"Seeker component missing on {gameObject.name}");
        if (aiPath == null) UnityEngine.Debug.LogError($"AIPath component missing on {gameObject.name}");
        if (rb == null) UnityEngine.Debug.LogError($"Rigidbody component missing on {gameObject.name}");

        if (aiPath != null)
        {
            aiPath.endReachedDistance = arrivalDistance;
            aiPath.slowdownDistance = arrivalDistance * 2f;
            aiPath.maxSpeed = maxVelocity * 0.8f;
            aiPath.pickNextWaypointDist = 1f;
            aiPath.whenCloseToDestination = CloseToDestinationMode.Stop;

            // Migliora la navigazione negli angoli
            aiPath.rotationSpeed = 360f; // Rotazione più veloce
            aiPath.slowWhenNotFacingTarget = true;
        }

        if (rb != null)
        {
            rb.mass = 1f;
            rb.linearDamping = 2f;
            rb.angularDamping = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    private void InitializeStopwatch()
    {
        stopwatch = new Stopwatch();
        lastPosition = transform.position;
        lastStuckCheckPosition = transform.position;
        lastUpdateTime = Time.time + randomOffset;
        lastTargetPosition = target != null ? target.position : Vector3.zero;
        lastDistanceToTarget = float.MaxValue;
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
        yield return new WaitForSeconds(Random.Range(0.01f, 0.1f));

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Wait one more frame to ensure physics stability
        yield return new WaitForFixedUpdate();

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

        // Update position tracking
        recentPositions[positionIndex] = transform.position;
        positionIndex = (positionIndex + 1) % recentPositions.Length;

        if (target != null)
        {
            lastDistanceToTarget = distanceToTarget;
            distanceToTarget = Vector3.Distance(transform.position, target.position);

            // Check if target moved significantly
            if (Vector3.Distance(target.position, lastTargetPosition) > 2f)
            {
                lastTargetPosition = target.position;
                CalculatePathToTarget();
            }
        }

        CheckArrivalAtDestination();

        // Update movement state and advanced stuck detection
        if (!hasArrivedAtDestination)
        {
            UpdateMovementState();
            CheckIfStuckAdvanced();
            CheckCornerStuck();
            CheckNoProgressStuck();

            // Recalculate path periodically or if stuck
            if ((isMoving && frameCounter % (pathRecalculateFrameInterval / 10) == 0) ||
                isStuck ||
                (Time.time - lastUnstuckTime > 3f && unstuckAttempts > 0))
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

        // Limit velocity to prevent physics explosions
        LimitVelocity();
    }

    private void CheckIfStuckAdvanced()
    {
        Vector3 currentPos = transform.position;
        float distanceMoved = Vector3.Distance(currentPos, lastStuckCheckPosition);

        // Check if the NPC is truly stuck (multiple conditions)
        bool isReallyStuck = false;

        // Condition 1: Basic distance check
        if (distanceMoved < stuckCheckDistance && isMoving)
        {
            stuckTimer += updateFrequency;
        }
        else
        {
            stuckTimer = 0f;
            lastStuckCheckPosition = currentPos;
        }

        // Condition 2: Check if oscillating in small area
        bool isOscillating = CheckOscillation();

        // Condition 3: Check if velocity is very low despite wanting to move
        bool hasLowVelocity = cachedSpeed < movementThreshold && aiPath != null && aiPath.canMove;

        // Condition 4: Check if getting farther from target despite trying to move
        bool movingAwayFromTarget = distanceToTarget > lastDistanceToTarget + 0.1f && isMoving;

        isReallyStuck = (stuckTimer >= stuckTimeThreshold) || isOscillating ||
                       (hasLowVelocity && stuckTimer > stuckTimeThreshold * 0.5f) ||
                       movingAwayFromTarget;

        if (isReallyStuck && !isStuck)
        {
            isStuck = true;
            StartCoroutine(HandleStuckSituation());
        }
        else if (!isReallyStuck && stuckTimer == 0f)
        {
            isStuck = false;
            unstuckAttempts = 0;
        }
    }

    private bool CheckOscillation()
    {
        if (recentPositions.Length < 3) return false;

        Vector3 avgPosition = Vector3.zero;
        for (int i = 0; i < recentPositions.Length; i++)
        {
            avgPosition += recentPositions[i];
        }
        avgPosition /= recentPositions.Length;

        float maxDistance = 0f;
        for (int i = 0; i < recentPositions.Length; i++)
        {
            float distance = Vector3.Distance(recentPositions[i], avgPosition);
            if (distance > maxDistance)
                maxDistance = distance;
        }

        // If all recent positions are within a small radius, we might be oscillating
        return maxDistance < stuckCheckDistance * 1.5f && isMoving;
    }

    private void CheckCornerStuck()
    {
        if (!isMoving) return;

        // Cast rays in multiple directions to detect if we're in a corner
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        bool frontBlocked = Physics.Raycast(transform.position, forward, cornerDetectionRadius, obstacleLayerMask);
        bool rightBlocked = Physics.Raycast(transform.position, right, cornerDetectionRadius, obstacleLayerMask);
        bool leftBlocked = Physics.Raycast(transform.position, -right, cornerDetectionRadius, obstacleLayerMask);

        // Check diagonal directions too
        bool frontRightBlocked = Physics.Raycast(transform.position, (forward + right).normalized, cornerDetectionRadius, obstacleLayerMask);
        bool frontLeftBlocked = Physics.Raycast(transform.position, (forward - right).normalized, cornerDetectionRadius, obstacleLayerMask);

        int blockedDirections = 0;
        if (frontBlocked) blockedDirections++;
        if (rightBlocked) blockedDirections++;
        if (leftBlocked) blockedDirections++;
        if (frontRightBlocked) blockedDirections++;
        if (frontLeftBlocked) blockedDirections++;

        bool currentlyInCorner = blockedDirections >= 3;

        if (currentlyInCorner && cachedSpeed < movementThreshold)
        {
            cornerStuckTimer += updateFrequency;
            isInCorner = true;

            if (cornerStuckTimer >= stuckTimeThreshold * 0.7f)
            {
                StartCoroutine(EscapeCorner());
                cornerStuckTimer = 0f;
            }
        }
        else
        {
            cornerStuckTimer = 0f;
            isInCorner = false;
        }
    }

    private void CheckNoProgressStuck()
    {
        if (!isMoving || target == null) return;

        // If distance to target hasn't decreased in a while, we might be stuck
        if (distanceToTarget >= lastDistanceToTarget - 0.05f)
        {
            noProgressTimer += updateFrequency;

            if (noProgressTimer >= stuckTimeThreshold * 1.5f)
            {
                UnityEngine.Debug.Log($"No progress detected for {gameObject.name}, attempting recovery");
                StartCoroutine(HandleNoProgressStuck());
                noProgressTimer = 0f;
            }
        }
        else
        {
            noProgressTimer = 0f;
        }
    }

    private IEnumerator HandleStuckSituation()
    {
        if (unstuckAttempts >= maxUnstuckAttempts)
        {
            UnityEngine.Debug.LogWarning($"Max unstuck attempts reached for {gameObject.name}, resetting position");
            yield return StartCoroutine(TeleportToSafePosition());
            yield break;
        }

        unstuckAttempts++;
        lastUnstuckTime = Time.time;

        UnityEngine.Debug.Log($"Handling stuck situation for {gameObject.name} (attempt {unstuckAttempts})");

        // Stop current movement
        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        // Apply unstuck force with better direction calculation
        Vector3 unstuckDirection = CalculateUnstuckDirection();
        if (rb != null)
        {
            rb.AddForce(unstuckDirection * unstuckForce, ForceMode.Impulse);
        }

        // Wait a bit for physics to settle
        yield return new WaitForSeconds(0.3f);

        // Try to move to a nearby walkable position
        Vector3 newPosition = FindNearbyWalkablePosition();
        if (newPosition != Vector3.zero)
        {
            transform.position = newPosition;
        }

        yield return new WaitForSeconds(pathRecalculateDelay);

        // Recalculate path
        CalculatePathToTarget();

        // Reset stuck state after a delay
        yield return new WaitForSeconds(0.5f);
        stuckTimer = 0f;
    }

    private IEnumerator EscapeCorner()
    {
        UnityEngine.Debug.Log($"Escaping corner for {gameObject.name}");

        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        // Find the best escape direction
        Vector3 escapeDirection = FindBestEscapeDirection();

        // Apply force to escape
        if (rb != null)
        {
            rb.AddForce(escapeDirection * unstuckForce * 1.5f, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(0.5f);

        // Try to find a better position
        Vector3 betterPosition = FindNearbyWalkablePosition();
        if (betterPosition != Vector3.zero)
        {
            transform.position = betterPosition;
        }

        yield return new WaitForSeconds(0.3f);
        CalculatePathToTarget();
    }

    private IEnumerator HandleNoProgressStuck()
    {
        // Try a more aggressive approach for no-progress situations
        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        // Move to a random nearby position
        Vector3 randomOffset = new Vector3(
            Random.Range(-2f, 2f),
            0f,
            Random.Range(-2f, 2f)
        );

        Vector3 newPos = transform.position + randomOffset;
        Vector3 walkablePos = FindNearbyWalkablePosition(newPos);

        if (walkablePos != Vector3.zero)
        {
            transform.position = walkablePos;
        }

        yield return new WaitForSeconds(0.3f);
        CalculatePathToTarget();
    }

    private Vector3 CalculateUnstuckDirection()
    {
        Vector3 directionToTarget = (target.position - transform.position).normalized;

        // If we have a clear direction to target, use it
        if (!Physics.Raycast(transform.position, directionToTarget, 2f, obstacleLayerMask))
        {
            return directionToTarget;
        }

        // Otherwise, find the direction with least obstacles
        Vector3[] directions = {
            transform.right,
            -transform.right,
            transform.forward,
            -transform.forward,
            (transform.right + transform.forward).normalized,
            (-transform.right + transform.forward).normalized,
            (transform.right - transform.forward).normalized,
            (-transform.right - transform.forward).normalized
        };

        float maxDistance = 0f;
        Vector3 bestDirection = Random.insideUnitSphere.normalized;
        bestDirection.y = 0; // Keep on ground

        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, 5f, obstacleLayerMask))
            {
                if (hit.distance > maxDistance)
                {
                    maxDistance = hit.distance;
                    bestDirection = dir;
                }
            }
            else
            {
                return dir; // Found completely clear direction
            }
        }

        return bestDirection;
    }

    private Vector3 FindBestEscapeDirection()
    {
        Vector3[] escapeDirections = {
            -transform.forward, // Move backwards
            -transform.right,   // Move left
            transform.right,    // Move right
            (-transform.right - transform.forward).normalized, // Diagonal back-left
            (transform.right - transform.forward).normalized   // Diagonal back-right
        };

        foreach (Vector3 dir in escapeDirections)
        {
            if (!Physics.Raycast(transform.position, dir, 1.5f, obstacleLayerMask))
            {
                return dir;
            }
        }

        // If all directions blocked, go up and back
        return Vector3.up + (-transform.forward);
    }

    private Vector3 FindNearbyWalkablePosition(Vector3 centerPos = default)
    {
        if (centerPos == default)
            centerPos = transform.position;

        // Try multiple positions in a spiral pattern
        for (float radius = 1f; radius <= 5f; radius += 1f)
        {
            for (int angle = 0; angle < 360; angle += 45)
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector3 testPos = centerPos + new Vector3(
                    Mathf.Cos(radians) * radius,
                    0f,
                    Mathf.Sin(radians) * radius
                );

                // Check if position is walkable
                if (IsPositionWalkable(testPos))
                {
                    return testPos;
                }
            }
        }

        return Vector3.zero; // No walkable position found
    }

    private bool IsPositionWalkable(Vector3 position)
    {
        // Check if there's ground below
        if (!Physics.Raycast(position + Vector3.up, Vector3.down, 2f))
            return false;

        // Check if there are no obstacles at this position
        if (Physics.CheckSphere(position, 0.5f, obstacleLayerMask))
            return false;

        // Additional check: is the position on a walkable node?
        GraphNode node = AstarPath.active.GetNearest(position).node;
        if (node == null || !node.Walkable)
            return false;

        // Check if we can reach this position from current position
        GraphNode currentNode = AstarPath.active.GetNearest(transform.position).node;
        if (currentNode == null || !currentNode.Walkable)
            return false;

        // Simple reachability check - if both nodes are walkable and in same area
        return node.Area == currentNode.Area;
    }

    private IEnumerator TeleportToSafePosition()
    {
        UnityEngine.Debug.LogWarning($"Teleporting {gameObject.name} to safe position");

        // Try to find a position closer to the target
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        Vector3 safePosition = target.position - directionToTarget * (arrivalDistance * 3f);

        Vector3 walkablePosition = FindNearbyWalkablePosition(safePosition);
        if (walkablePosition != Vector3.zero)
        {
            transform.position = walkablePosition;
        }
        else
        {
            // Last resort: move closer to target
            transform.position = Vector3.MoveTowards(transform.position, target.position, 2f);
        }

        yield return new WaitForSeconds(0.5f);

        // Reset all stuck-related variables
        ResetStuckState();
        CalculatePathToTarget();
    }

    private void ResetStuckState()
    {
        isStuck = false;
        stuckTimer = 0f;
        unstuckAttempts = 0;
        cornerStuckTimer = 0f;
        isInCorner = false;
        noProgressTimer = 0f;
        lastStuckCheckPosition = transform.position;

        // Reset position tracking
        for (int i = 0; i < recentPositions.Length; i++)
        {
            recentPositions[i] = transform.position;
        }
    }

    private void UnstuckNPC()
    {
        // This method is kept for backwards compatibility but now uses the enhanced system
        if (!isStuck)
        {
            StartCoroutine(HandleStuckSituation());
        }
    }

    private IEnumerator RecalculatePathDelayed()
    {
        yield return new WaitForSeconds(pathRecalculateDelay);
        CalculatePathToTarget();
    }

    private void LimitVelocity()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;

        // Limit horizontal velocity
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
        if (horizontalVelocity.magnitude > maxVelocity)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxVelocity;
            rb.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
        }

        // Limit vertical velocity to prevent flying
        if (Mathf.Abs(velocity.y) > maxVelocity * 0.5f)
        {
            rb.linearVelocity = new Vector3(velocity.x, Mathf.Sign(velocity.y) * maxVelocity * 0.5f, velocity.z);
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
            ResetStuckState(); // Reset stuck state when arriving
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
        ResetStuckState();
    }

    private void ResumeMovement()
    {
        // Reactivate colliders
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = true;
        }

        if (aiPath != null)
        {
            aiPath.canMove = true;
        }

        if (target != null)
        {
            CalculatePathToTarget();
        }

        ResetStuckState();
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

            // If path fails, try alternative recovery
            if (isStuck || unstuckAttempts > 0)
            {
                StartCoroutine(HandlePathFailure());
            }
        }
        else
        {
            // Reset stuck state when we get a valid path
            if (isStuck && unstuckAttempts < 2) // Don't reset if we've tried multiple times
            {
                ResetStuckState();
            }
        }
    }

    private IEnumerator HandlePathFailure()
    {
        yield return new WaitForSeconds(0.5f);

        // Try to move to a more accessible position
        Vector3 betterPosition = FindNearbyWalkablePosition();
        if (betterPosition != Vector3.zero)
        {
            transform.position = betterPosition;
            yield return new WaitForSeconds(0.3f);
            CalculatePathToTarget();
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
        lastDistanceToTarget = float.MaxValue;

        // Reset enhanced stuck detection
        ResetStuckState();

        // Reset pathfinding
        if (aiPath != null)
        {
            aiPath.canMove = false;
            aiPath.destination = transform.position;
        }

        // Reset physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reactivate colliders
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = true;
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
        ResetStuckState();
        lastTargetPosition = newTarget != null ? newTarget.position : Vector3.zero;

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
    public bool IsStuck() => isStuck;
    public bool IsInCorner() => isInCorner;
    public int GetUnstuckAttempts() => unstuckAttempts;

    // Debug methods
    public void ForceUnstuck()
    {
        if (!isStuck)
        {
            isStuck = true;
            StartCoroutine(HandleStuckSituation());
        }
    }

    public void ForcePathRecalculation()
    {
        if (target != null)
        {
            CalculatePathToTarget();
        }
    }

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

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw arrival distance
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, arrivalDistance);

        // Draw stuck check distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stuckCheckDistance);

        // Draw corner detection radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, cornerDetectionRadius);

        // Draw recent positions
        Gizmos.color = Color.blue;
        for (int i = 0; i < recentPositions.Length; i++)
        {
            Gizmos.DrawWireSphere(recentPositions[i], 0.2f);
        }

        // Draw direction rays for corner detection
        if (isInCorner)
        {
            Gizmos.color = Color.magenta;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            Gizmos.DrawRay(transform.position, forward * cornerDetectionRadius);
            Gizmos.DrawRay(transform.position, right * cornerDetectionRadius);
            Gizmos.DrawRay(transform.position, -right * cornerDetectionRadius);
            Gizmos.DrawRay(transform.position, (forward + right).normalized * cornerDetectionRadius);
            Gizmos.DrawRay(transform.position, (forward - right).normalized * cornerDetectionRadius);
        }

        // Draw path to target
        if (target != null)
        {
            Gizmos.color = isStuck ? Color.red : Color.white;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}