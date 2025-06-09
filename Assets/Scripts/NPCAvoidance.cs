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
    [SerializeField] private float destinationRadius = 2f; // Aumentato da 1f a 2f
    [SerializeField] private float stopDistance = 1.5f; // Nuova distanza per fermarsi completamente
    [SerializeField] private bool disableCollisionAtDestination = true;
    [SerializeField] private float destinationDamping = 0.8f; // Fattore di rallentamento vicino alla destinazione

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
    private bool isNearDestination; // Nuovo flag per prossimit�
    private float timeAtDestination = 0f; // Timer per stabilizzare la destinazione

    // Costanti
    private const float MIN_DISTANCE = 0.1f;
    private const float MIN_AVOIDANCE_MAGNITUDE = 0.1f;
    private const float DESTINATION_STABILIZATION_TIME = 0.5f; // Tempo per considerare stabilmente arrivato
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

        // Configura AIPath per lavorare meglio con il nostro sistema
        if (aiPath != null)
        {
            aiPath.endReachedDistance = stopDistance; // Sincronizza con la nostra logica
            aiPath.slowdownDistance = destinationRadius; // Inizia a rallentare prima
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

        // Calcola se � vicino alla destinazione
        isNearDestination = cachedDistanceToDestination <= destinationRadius;

        // Calcola se ha raggiunto la destinazione (pi� stretto)
        bool reachedByDistance = cachedDistanceToDestination <= stopDistance;
        bool reachedByAIPath = aiPath.reachedDestination || aiPath.reachedEndOfPath;

        // Logica per stabilizzare l'arrivo alla destinazione
        if (reachedByDistance || reachedByAIPath)
        {
            timeAtDestination += Time.deltaTime;

            // Considera raggiunta solo dopo un po' di tempo per evitare oscillazioni
            if (timeAtDestination >= DESTINATION_STABILIZATION_TIME)
            {
                HasReachedDestination = true;

                // Ferma completamente l'AIPath quando arriva
                if (aiPath != null)
                {
                    aiPath.canMove = false;
                    // Ferma completamente il rigidbody
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

            // Riattiva il movimento se non � pi� alla destinazione
            if (aiPath != null && !aiPath.canMove)
            {
                aiPath.canMove = true;
            }
        }

        // Aggiorna collision state solo quando cambia lo stato
        if (wasAtDestination != HasReachedDestination && disableCollisionAtDestination)
        {
            UpdateCollisionState();
        }
    }

    public bool ShouldCalculateAvoidance()
    {
        // Non calcolare avoidance se � fermo alla destinazione
        if (HasReachedDestination && disableCollisionAtDestination)
        {
            return false;
        }

        // Riduce l'avoidance quando � vicino alla destinazione
        return true;
    }

    public void CalculateAvoidanceFromNeighbors(List<NPCAvoidance> neighbors)
    {
        avoidanceDirection = Vector3.zero;

        // Se � alla destinazione, non applicare avoidance
        if (HasReachedDestination && disableCollisionAtDestination)
        {
            return;
        }

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

                // Riduce la forza se � vicino alla destinazione
                if (isNearDestination)
                {
                    float destinationFactor = (cachedDistanceToDestination / destinationRadius);
                    forceMagnitude *= destinationFactor * destinationDamping;
                }

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

            // Ferma completamente il movimento
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

        // Non applicare forza se � fermo alla destinazione
        if (HasReachedDestination && disableCollisionAtDestination)
            return;

        Vector3 avoidanceVelocity = avoidanceDirection * avoidanceForce;

        // Riduce la forza vicino alla destinazione
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

    private void LimitHorizontalVelocity()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);

        float maxSpeed = maxAvoidanceSpeed;

        // Riduce velocit� massima vicino alla destinazione
        if (isNearDestination)
        {
            float destinationFactor = Mathf.Clamp01(cachedDistanceToDestination / destinationRadius);
            maxSpeed *= destinationFactor * destinationDamping;
        }

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
        stopDistance = destinationRadius * 0.75f; // Mantieni stopDistance pi� piccolo

        // Aggiorna anche AIPath
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

        // Raggio di evitamento
        Gizmos.color = HasReachedDestination ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(position, avoidanceRadius);

        // Raggio di destinazione (ora pi� visibile)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(position, destinationRadius);

        // Raggio di stop (pi� piccolo, rosso)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, stopDistance);

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

            // Mostra distanza alla destinazione
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(CachedDestination, destinationRadius);
        }
    }

    #endregion
}