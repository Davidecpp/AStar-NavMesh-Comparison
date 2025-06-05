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
    [SerializeField] private float destinationRadius = 1f;
    [SerializeField] private bool disableCollisionAtDestination = true;

    // Componenti cached
    private AIPath aiPath;
    private Rigidbody rb;
    private Collider col;

    // Stato pubblico per il manager
    public Vector3 CachedPosition { get; private set; }
    public Vector3 CachedDestination { get; private set; }
    public float AvoidanceRadius => avoidanceRadius;
    public bool HasReachedDestination { get; private set; }

    // Stato interno
    private Vector3 avoidanceDirection;
    private bool wasAtDestination;
    private float cachedDistanceToDestination;

    // Costanti
    private const float MIN_DISTANCE = 0.1f;
    private const float MIN_AVOIDANCE_MAGNITUDE = 0.1f;
    private static readonly Vector3 UP_VECTOR = Vector3.up;

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        InitializeState();
    }

    private void Start()
    {
        // Registra nel manager centralizzato invece di usare FixedUpdate
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

    private void CacheComponents()
    {
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (aiPath == null) Debug.LogWarning($"AIPath component missing on {gameObject.name}");
        if (rb == null) Debug.LogWarning($"Rigidbody component missing on {gameObject.name}");
    }

    private void InitializeState()
    {
        avoidanceDirection = Vector3.zero;
        HasReachedDestination = false;
        wasAtDestination = false;
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

        HasReachedDestination = aiPath.reachedDestination &&
                              cachedDistanceToDestination <= destinationRadius;

        if (wasAtDestination != HasReachedDestination && disableCollisionAtDestination)
        {
            UpdateCollisionState();
        }
    }

    public bool ShouldCalculateAvoidance()
    {
        return !HasReachedDestination || !disableCollisionAtDestination;
    }

    public void CalculateAvoidanceFromNeighbors(List<NPCAvoidance> neighbors)
    {
        avoidanceDirection = Vector3.zero;

        foreach (var neighbor in neighbors)
        {
            if (neighbor == null || neighbor == this) continue;

            // Ignora NPC che hanno raggiunto la destinazione
            if (neighbor.HasReachedDestination && neighbor.disableCollisionAtDestination)
                continue;

            Vector3 directionAway = CachedPosition - neighbor.CachedPosition;
            float distance = directionAway.magnitude;

            if (distance > MIN_DISTANCE && distance < avoidanceRadius)
            {
                float forceMagnitude = (avoidanceRadius - distance) / avoidanceRadius;
                directionAway = Vector3.ProjectOnPlane(directionAway.normalized, UP_VECTOR);
                avoidanceDirection += directionAway * forceMagnitude;
            }
        }

        // Normalizza se necessario
        if (avoidanceDirection.sqrMagnitude > 1f)
        {
            avoidanceDirection.Normalize();
        }

        // Applica forza se necessario
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

        if (HasReachedDestination)
        {
            col.isTrigger = true;
            rb.isKinematic = true;
            rb.detectCollisions = false;
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

        Vector3 avoidanceVelocity = avoidanceDirection * avoidanceForce;

        if (avoidanceVelocity.sqrMagnitude > maxAvoidanceSpeed * maxAvoidanceSpeed)
        {
            avoidanceVelocity = avoidanceVelocity.normalized * maxAvoidanceSpeed;
        }

        rb.AddForce(avoidanceVelocity, ForceMode.Force);
        LimitHorizontalVelocity();
    }

    private void LimitHorizontalVelocity()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

        if (horizontalVelocity.sqrMagnitude > maxAvoidanceSpeed * maxAvoidanceSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxAvoidanceSpeed;
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

    public void ForceDestinationCheck()
    {
        UpdateCachedValues();
        CheckDestinationStatus();
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Vector3 position = transform.position;

        // Raggio di evitamento
        Gizmos.color = HasReachedDestination ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(position, avoidanceRadius);

        // Raggio di destinazione
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(position, destinationRadius);

        // Direzione di evitamento
        if (avoidanceDirection.sqrMagnitude > MIN_AVOIDANCE_MAGNITUDE * MIN_AVOIDANCE_MAGNITUDE)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(position, avoidanceDirection * 2f);
        }

        // Linea verso destinazione
        if (aiPath != null && CachedDestination != Vector3.zero)
        {
            Gizmos.color = HasReachedDestination ? Color.green : Color.cyan;
            Gizmos.DrawLine(position, CachedDestination);
        }
    }

    #endregion
}