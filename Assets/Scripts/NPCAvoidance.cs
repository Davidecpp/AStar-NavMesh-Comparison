using Pathfinding;
using UnityEngine;
using System.Collections.Generic;

// Componente ottimizzato per evitamento NPC senza RVO
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

    [Header("Performance Settings")]
    [SerializeField] private float updateInterval = 0.1f; // Intervallo di aggiornamento
    [SerializeField] private int maxNeighbors = 10; // Limite massimo di vicini da considerare

    // Componenti cached
    private AIPath aiPath;
    private Rigidbody rb;
    private Collider col;

    // Stato
    private Vector3 avoidanceDirection;
    private bool hasReachedDestination;
    private bool wasAtDestination;

    // Performance optimization
    private float nextUpdateTime;
    private readonly Collider[] nearbyColliders = new Collider[32]; // Array riutilizzabile
    private readonly List<Transform> validNeighbors = new List<Transform>(10);

    // Cache per calcoli
    private Vector3 cachedPosition;
    private Vector3 cachedDestination;
    private float cachedDistanceToDestination;

    // Costanti per evitare allocazioni
    private static readonly Vector3 UP_VECTOR = Vector3.up;
    private const float MIN_DISTANCE = 0.1f;
    private const float MIN_AVOIDANCE_MAGNITUDE = 0.1f;

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        InitializeState();
    }

    private void FixedUpdate()
    {
        if (!IsValidForUpdate()) return;

        UpdateCachedValues();

        // Aggiornamento con intervallo per performance
        if (Time.fixedTime >= nextUpdateTime)
        {
            nextUpdateTime = Time.fixedTime + updateInterval;
            CheckDestinationStatus();
        }

        // Applica evitamento solo se necessario
        if (ShouldCalculateAvoidance())
        {
            CalculateAvoidance();
            ApplyAvoidanceForce();
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
        hasReachedDestination = false;
        wasAtDestination = false;
        nextUpdateTime = Time.fixedTime;
    }

    #endregion

    #region Validation & Updates

    private bool IsValidForUpdate()
    {
        return aiPath != null && rb != null && enabled && gameObject.activeInHierarchy;
    }

    private void UpdateCachedValues()
    {
        cachedPosition = transform.position;
        if (aiPath.destination != Vector3.zero)
        {
            cachedDestination = aiPath.destination;
            cachedDistanceToDestination = Vector3.Distance(cachedPosition, cachedDestination);
        }
    }

    private bool ShouldCalculateAvoidance()
    {
        return !hasReachedDestination || !disableCollisionAtDestination;
    }

    #endregion

    #region Destination Management

    private void CheckDestinationStatus()
    {
        wasAtDestination = hasReachedDestination;

        hasReachedDestination = aiPath.reachedDestination &&
                              cachedDistanceToDestination <= destinationRadius;

        if (wasAtDestination != hasReachedDestination && disableCollisionAtDestination)
        {
            UpdateCollisionState();
        }
    }

    private void UpdateCollisionState()
    {
        if (col == null || rb == null) return;

        if (hasReachedDestination)
        {
            // Disabilita collisioni
            col.isTrigger = true;
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        else
        {
            // Riabilita collisioni
            col.isTrigger = false;
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }
    }

    #endregion

    #region Avoidance Logic

    private void CalculateAvoidance()
    {
        avoidanceDirection = Vector3.zero;

        int colliderCount = Physics.OverlapSphereNonAlloc(
            cachedPosition,
            avoidanceRadius,
            nearbyColliders,
            avoidanceLayers
        );

        if (colliderCount == 0) return;

        FindValidNeighbors(colliderCount);
        CalculateAvoidanceDirection();
    }

    private void FindValidNeighbors(int colliderCount)
    {
        validNeighbors.Clear();
        int processedCount = 0;

        for (int i = 0; i < colliderCount && processedCount < maxNeighbors; i++)
        {
            var collider = nearbyColliders[i];
            if (collider == null || collider.gameObject == gameObject) continue;

            // Ignora NPC che hanno raggiunto la destinazione
            var otherAvoidance = collider.GetComponent<NPCAvoidance>();
            if (otherAvoidance?.hasReachedDestination == true &&
                otherAvoidance.disableCollisionAtDestination)
                continue;

            validNeighbors.Add(collider.transform);
            processedCount++;
        }
    }

    private void CalculateAvoidanceDirection()
    {
        foreach (var neighbor in validNeighbors)
        {
            Vector3 directionAway = cachedPosition - neighbor.position;
            float distance = directionAway.magnitude;

            if (distance > MIN_DISTANCE && distance < avoidanceRadius)
            {
                float forceMagnitude = (avoidanceRadius - distance) / avoidanceRadius;

                // Normalizza e rimuovi componente Y
                directionAway = Vector3.ProjectOnPlane(directionAway.normalized, UP_VECTOR);

                avoidanceDirection += directionAway * forceMagnitude;
            }
        }

        // Normalizza se necessario
        if (avoidanceDirection.sqrMagnitude > 1f)
        {
            avoidanceDirection.Normalize();
        }
    }

    private void ApplyAvoidanceForce()
    {
        if (avoidanceDirection.sqrMagnitude < MIN_AVOIDANCE_MAGNITUDE * MIN_AVOIDANCE_MAGNITUDE)
            return;

        Vector3 avoidanceVelocity = avoidanceDirection * avoidanceForce;

        // Limita velocità
        if (avoidanceVelocity.sqrMagnitude > maxAvoidanceSpeed * maxAvoidanceSpeed)
        {
            avoidanceVelocity = avoidanceVelocity.normalized * maxAvoidanceSpeed;
        }

        rb.AddForce(avoidanceVelocity, ForceMode.Force);

        // Limita velocità orizzontale totale
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

    public void ForceDestinationCheck()
    {
        UpdateCachedValues();
        CheckDestinationStatus();
    }

    public bool HasReachedDestination() => hasReachedDestination;

    public void SetAvoidanceRadius(float radius)
    {
        avoidanceRadius = Mathf.Max(0f, radius);
    }

    public void SetAvoidanceForce(float force)
    {
        avoidanceForce = Mathf.Max(0f, force);
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Vector3 position = transform.position;

        // Raggio di evitamento
        Gizmos.color = hasReachedDestination ? Color.green : Color.yellow;
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
        if (aiPath != null && cachedDestination != Vector3.zero)
        {
            Gizmos.color = hasReachedDestination ? Color.green : Color.cyan;
            Gizmos.DrawLine(position, cachedDestination);
        }

        // Visualizza vicini validi
        Gizmos.color = Color.magenta;
        foreach (var neighbor in validNeighbors)
        {
            if (neighbor != null)
                Gizmos.DrawWireCube(neighbor.position, Vector3.one * 0.5f);
        }
    }

    #endregion
}