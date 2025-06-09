using Pathfinding;
using System.Collections.Generic;
using UnityEngine;

public class NPCAvoidance : MonoBehaviour
{
    [Header("Avoidance Settings")]
    [SerializeField] private float avoidanceRadius = 2f;
    [SerializeField] private float avoidanceForce = 5f;
    [SerializeField] private float maxAvoidanceSpeed = 8f;
    [SerializeField] private LayerMask avoidanceLayers = -1;

    [Header("Destination Settings")]
    [SerializeField] private float destinationRadius = 2f;
    [SerializeField] private float stopDistance = 1.5f; 
    [SerializeField] private bool disableCollisionAtDestination = true;
    [SerializeField] private float destinationDamping = 0.8f;

    // Cached components
    private AIPath aiPath;
    private Rigidbody rb;
    private Collider col;

    public Vector3 CachedPosition { get; private set; }
    public Vector3 CachedDestination { get; private set; }
    public float AvoidanceRadius => avoidanceRadius;
    public bool HasReachedDestination { get; private set; }

    // Internal state
    private Vector3 avoidanceDirection;
    private bool wasAtDestination;
    private float cachedDistanceToDestination;
    private bool isNearDestination; 
    private float timeAtDestination = 0f;

    // Costants
    private const float MIN_DISTANCE = 0.1f;
    private const float MIN_AVOIDANCE_MAGNITUDE = 0.1f;
    private const float DESTINATION_STABILIZATION_TIME = 0.5f; 
    private static readonly Vector3 UP_VECTOR = Vector3.up;

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        InitializeState();
    }

    private void Start()
    {
        // Registers NPC in the NPCAvoidanceManager
        if (NPCAvoidanceManager.Instance != null)
        {
            NPCAvoidanceManager.Instance.RegisterNPC(this);
        }
        else
        {
            Debug.LogError("NPCAvoidanceManager not found! Please add it to the scene.");
        }
    }

    private void OnDestroy()
    {
        if (NPCAvoidanceManager.Instance != null)
        {
            NPCAvoidanceManager.Instance.UnregisterNPC(this);
        }
    }

    #endregion

    #region Initialization

    // Caches required components and initializes state variables
    private void CacheComponents()
    {
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (aiPath == null) Debug.LogWarning($"AIPath component missing on {gameObject.name}");
        if (rb == null) Debug.LogWarning($"Rigidbody component missing on {gameObject.name}");

        if (aiPath != null)
        {
            aiPath.endReachedDistance = stopDistance; 
            aiPath.slowdownDistance = destinationRadius; 
        }
    }

    private void InitializeState()
    {
        avoidanceDirection = Vector3.zero;
        HasReachedDestination = false;
        wasAtDestination = false;
        isNearDestination = false;
        timeAtDestination = 0f;
    }

    #endregion

    #region Manager Interface (chiamato dal manager centralizzato)

    public void UpdateCachedValues()
    {
        if (!IsValidForUpdate()) return;

        CachedPosition = transform.position;
        if (aiPath != null && aiPath.destination != Vector3.zero)
        {
            CachedDestination = aiPath.destination;
            cachedDistanceToDestination = Vector3.Distance(CachedPosition, CachedDestination);
        }
    }

    public void CheckDestinationStatus()
    {
        if (aiPath == null) return;

        wasAtDestination = HasReachedDestination;
        bool wasNearDestination = isNearDestination;

        // Calculates the distance to the destination
        isNearDestination = cachedDistanceToDestination <= destinationRadius;

        // Check if the NPC is at the destination
        bool reachedByDistance = cachedDistanceToDestination <= stopDistance;
        bool reachedByAIPath = aiPath.reachedDestination || aiPath.reachedEndOfPath;

        if (reachedByDistance || reachedByAIPath)
        {
            timeAtDestination += Time.deltaTime;

            // Reached destination if stabilized for a certain time
            if (timeAtDestination >= DESTINATION_STABILIZATION_TIME)
            {
                HasReachedDestination = true;

                // Stops movement and disables collision if configured
                if (aiPath != null)
                {
                    aiPath.canMove = false;
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
        }
        else
        {
            timeAtDestination = 0f;
            HasReachedDestination = false;

            // Reactivate movement if it was previously stopped
            if (aiPath != null && !aiPath.canMove)
            {
                aiPath.canMove = true;
            }
        }

        // Updates the collision state based on the new status
        if (wasAtDestination != HasReachedDestination && disableCollisionAtDestination)
        {
            UpdateCollisionState();
        }
    }

    public bool ShouldCalculateAvoidance()
    {
        // Don't calculate avoidance if the NPC is not moving or has reached the destination
        if (HasReachedDestination && disableCollisionAtDestination)
        {
            return false;
        }
        return true;
    }

    public void CalculateAvoidanceFromNeighbors(List<NPCAvoidance> neighbors)
    {
        avoidanceDirection = Vector3.zero;

        // If the NPC has reached the destination and collision is disabled, skip avoidance
        if (HasReachedDestination && disableCollisionAtDestination)
        {
            return;
        }

        foreach (var neighbor in neighbors)
        {
            if (neighbor == null || neighbor == this) continue;

            // Ignores neighbors that are not valid for avoidance
            if (neighbor.HasReachedDestination && neighbor.disableCollisionAtDestination)
                continue;

            Vector3 directionAway = CachedPosition - neighbor.CachedPosition;
            float distance = directionAway.magnitude;

            if (distance > MIN_DISTANCE && distance < avoidanceRadius)
            {
                float forceMagnitude = (avoidanceRadius - distance) / avoidanceRadius;

                // Reduces the force if the neighbor is near the destination
                if (isNearDestination)
                {
                    float destinationFactor = (cachedDistanceToDestination / destinationRadius);
                    forceMagnitude *= destinationFactor * destinationDamping;
                }

                directionAway = Vector3.ProjectOnPlane(directionAway.normalized, UP_VECTOR);
                avoidanceDirection += directionAway * forceMagnitude;
            }
        }
        if (avoidanceDirection.sqrMagnitude > 1f)
        {
            avoidanceDirection.Normalize();
        }

        ApplyAvoidanceForce();
    }

    #endregion

    #region Internal Logic

    private bool IsValidForUpdate()
    {
        return aiPath != null && rb != null && enabled && gameObject.activeInHierarchy;
    }

    private void UpdateCollisionState()
    {
        if (col == null || rb == null) return;

        // Deactivate collision if the NPC has reached the destination and collision is disabled
        if (HasReachedDestination)
        {
            col.isTrigger = true;
            rb.isKinematic = true;
            rb.detectCollisions = false;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            col.isTrigger = false;
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }
    }

    private void ApplyAvoidanceForce()
    {
        if (rb == null || avoidanceDirection.sqrMagnitude < MIN_AVOIDANCE_MAGNITUDE * MIN_AVOIDANCE_MAGNITUDE)
            return;

        // Don't apply avoidance if the NPC has reached the destination and collision is disabled
        if (HasReachedDestination && disableCollisionAtDestination)
            return;

        Vector3 avoidanceVelocity = avoidanceDirection * avoidanceForce;

        // Reduce the avoidance force if the NPC is near the destination
        if (isNearDestination)
        {
            float destinationFactor = Mathf.Clamp01(cachedDistanceToDestination / destinationRadius);
            avoidanceVelocity *= destinationFactor * destinationDamping;
        }

        if (avoidanceVelocity.sqrMagnitude > maxAvoidanceSpeed * maxAvoidanceSpeed)
        {
            avoidanceVelocity = avoidanceVelocity.normalized * maxAvoidanceSpeed;
        }

        rb.AddForce(avoidanceVelocity, ForceMode.Force);
        LimitHorizontalVelocity();
    }

    // Limits the horizontal velocity of the NPC to prevent excessive speed
    private void LimitHorizontalVelocity()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

        float maxSpeed = maxAvoidanceSpeed;

        // If the NPC is near the destination, reduce the speed
        if (isNearDestination)
        {
            float destinationFactor = Mathf.Clamp01(cachedDistanceToDestination / destinationRadius);
            maxSpeed *= destinationFactor * destinationDamping;
        }

        // Limit the horizontal velocity to the maximum speed
        if (horizontalVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;
            rb.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
        }
    }

    #endregion

    #region Public Interface

    public void SetAvoidanceRadius(float radius)
    {
        avoidanceRadius = Mathf.Max(0f, radius);
    }

    public void SetAvoidanceForce(float force)
    {
        avoidanceForce = Mathf.Max(0f, force);
    }

    public void SetDestinationRadius(float radius)
    {
        destinationRadius = Mathf.Max(0.5f, radius);
        stopDistance = destinationRadius * 0.75f;

        if (aiPath != null)
        {
            aiPath.endReachedDistance = stopDistance;
            aiPath.slowdownDistance = destinationRadius;
        }
    }

    public void ForceDestinationCheck()
    {
        UpdateCachedValues();
        CheckDestinationStatus();
    }

    // Forces the NPC to stop immediately, setting it as reached destination
    public void ForceStop()
    {
        HasReachedDestination = true;
        timeAtDestination = DESTINATION_STABILIZATION_TIME;

        if (aiPath != null)
        {
            aiPath.canMove = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        UpdateCollisionState();
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Vector3 position = transform.position;

        // Avoidance radius
        Gizmos.color = HasReachedDestination ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(position, avoidanceRadius);

        // Destination radius 
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(position, destinationRadius);

        // Stop distance
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, stopDistance);

        if (avoidanceDirection.sqrMagnitude > MIN_AVOIDANCE_MAGNITUDE * MIN_AVOIDANCE_MAGNITUDE)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(position, avoidanceDirection * 2f);
        }

        if (aiPath != null && CachedDestination != Vector3.zero)
        {
            Gizmos.color = HasReachedDestination ? Color.green : Color.cyan;
            Gizmos.DrawLine(position, CachedDestination);

            // Distance to destination
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(CachedDestination, destinationRadius);
        }
    }

    #endregion
}