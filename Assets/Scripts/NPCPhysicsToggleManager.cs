using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Pathfinding;
using System.Collections.Generic;

public class NPCPhysicsToggleManager : MonoBehaviour
{
    public Toggle collisionToggle;

    private List<NavMeshAgent> navMeshAgents = new List<NavMeshAgent>();
    private List<AIPath> aStarAgents = new List<AIPath>();

    private bool currentCollisionState = true;

    private void Start()
    {
        if (collisionToggle != null)
        {
            collisionToggle.onValueChanged.AddListener(OnToggleChanged);
            currentCollisionState = collisionToggle.isOn;
        }
        else
        {
            Debug.LogError("Toggle non assegnato!");
        }
    }

    // Registra un NPC e applica lo stato di collisione corrente
    public void RegisterNPC(GameObject npc)
    {
        if (npc.TryGetComponent(out NavMeshAgent agent))
        {
            navMeshAgents.Add(agent);
            ApplyNavMeshCollision(agent, currentCollisionState);
        }

        if (npc.TryGetComponent(out AIPath aiPath))
        {
            aStarAgents.Add(aiPath);
            ApplyAStarCollision(aiPath, currentCollisionState);
        }
    }

    private void OnToggleChanged(bool enabled)
    {
        currentCollisionState = enabled;

        // Applica lo stato di collisione a tutti gli NPC registrati
        foreach (var agent in navMeshAgents)
        {
            if (agent != null)
                ApplyNavMeshCollision(agent, enabled);
        }

        foreach (var ai in aStarAgents)
        {
            if (ai != null)
                ApplyAStarCollision(ai, enabled);
        }
    }

    private void ApplyNavMeshCollision(NavMeshAgent agent, bool enabled)
    {
        agent.obstacleAvoidanceType = enabled
            ? ObstacleAvoidanceType.HighQualityObstacleAvoidance
            : ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void ApplyAStarCollision(AIPath ai, bool enabled)
    {
        if (enabled)
        {
            EnableAStarAvoidance(ai);
        }
        else
        {
            DisableAStarAvoidance(ai);
        }
    }

    private void EnableAStarAvoidance(AIPath ai)
    {
        var collider = ai.GetComponent<Collider>();
        if (collider == null)
        {
            var capsuleCollider = ai.gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 2f;
            capsuleCollider.center = new Vector3(0, 1f, 0);
        }
        collider.isTrigger = false;
        collider.enabled = true;

        var rb = ai.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = ai.gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.mass = 1f;
        rb.linearDamping = 8f;
        rb.angularDamping = 10f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var avoidance = ai.GetComponent<NPCAvoidance>();
        if (avoidance == null)
        {
            avoidance = ai.gameObject.AddComponent<NPCAvoidance>();
        }
        avoidance.enabled = true;

        ai.maxSpeed = Mathf.Max(ai.maxSpeed, 3f);
        ai.slowdownDistance = 3f;
        ai.pickNextWaypointDist = 2f;
        ai.enableRotation = true;
        ai.rotationSpeed = 180f;

        if (ai.gameObject.layer == 0)
        {
            ai.gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }

    private void DisableAStarAvoidance(AIPath ai)
    {
        // Disabilita componente di evitamento
        var avoidance = ai.GetComponent<NPCAvoidance>();
        if (avoidance != null)
        {
            avoidance.enabled = false;
        }

        // Rendi Rigidbody kinematic
        var rb = ai.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Disabilita collider o rendilo trigger
        var collider = ai.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void OnDestroy()
    {
        if (collisionToggle != null)
        {
            collisionToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }
    }
}

// Componente personalizzato per evitamento senza RVO
public class NPCAvoidance : MonoBehaviour
{
    [Header("Avoidance Settings")]
    public float avoidanceRadius = 2f;
    public float avoidanceForce = 5f;
    public float maxAvoidanceSpeed = 8f;
    public LayerMask avoidanceLayers = -1;

    [Header("Destination Settings")]
    public float destinationRadius = 1f; // Distanza entro cui si considera arrivato a destinazione
    public bool disableCollisionAtDestination = true; // Opzione per disabilitare le collisioni

    private AIPath aiPath;
    private Rigidbody rb;
    private Collider col;
    private Vector3 avoidanceDirection;
    private bool hasReachedDestination = false;

    private void Start()
    {
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    private void FixedUpdate()
    {
        if (aiPath == null || rb == null || !enabled) return;

        CheckDestinationStatus();

        // Solo calcola evitamento se non ha raggiunto la destinazione
        if (!hasReachedDestination || !disableCollisionAtDestination)
        {
            CalculateAvoidance();
            ApplyAvoidanceForce();
        }
    }

    private void CheckDestinationStatus()
    {
        bool wasAtDestination = hasReachedDestination;

        // Controlla se l'NPC ha raggiunto la destinazione
        if (aiPath.destination != null && aiPath.reachedDestination)
        {
            float distanceToDestination = Vector3.Distance(transform.position, aiPath.destination);
            hasReachedDestination = distanceToDestination <= destinationRadius;
        }
        else
        {
            hasReachedDestination = false;
        }

        // Se lo stato è cambiato, aggiorna le collisioni
        if (wasAtDestination != hasReachedDestination && disableCollisionAtDestination)
        {
            UpdateCollisionState();
        }
    }

    private void UpdateCollisionState()
    {
        if (hasReachedDestination)
        {
            // Disabilita collisioni quando arriva a destinazione
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }
        else
        {
            // Riabilita collisioni quando si muove verso una nuova destinazione
            if (col != null)
            {
                col.isTrigger = false;
            }

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
            }
        }
    }

    private void CalculateAvoidance()
    {
        avoidanceDirection = Vector3.zero;

        // Trova tutti i collider nelle vicinanze
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, avoidanceRadius, avoidanceLayers);

        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue;

            // Ignora altri NPC che sono arrivati a destinazione
            var otherAvoidance = collider.GetComponent<NPCAvoidance>();
            if (otherAvoidance != null && otherAvoidance.hasReachedDestination && otherAvoidance.disableCollisionAtDestination)
                continue;

            Vector3 directionAway = transform.position - collider.transform.position;
            float distance = directionAway.magnitude;

            if (distance > 0.1f && distance < avoidanceRadius)
            {
                // Calcola forza di evitamento basata sulla distanza
                float forceMagnitude = (avoidanceRadius - distance) / avoidanceRadius;
                directionAway.Normalize();

                // Considera solo le direzioni orizzontali (X e Z)
                directionAway.y = 0;

                avoidanceDirection += directionAway * forceMagnitude;
            }
        }

        if (avoidanceDirection.magnitude > 1f)
        {
            avoidanceDirection.Normalize();
        }
    }

    private void ApplyAvoidanceForce()
    {
        if (avoidanceDirection.magnitude > 0.1f)
        {
            // Applica forza di evitamento
            Vector3 avoidanceVelocity = avoidanceDirection * avoidanceForce;

            // Limita la velocita massima
            if (avoidanceVelocity.magnitude > maxAvoidanceSpeed)
            {
                avoidanceVelocity = avoidanceVelocity.normalized * maxAvoidanceSpeed;
            }

            // Applica la forza al Rigidbody
            rb.AddForce(avoidanceVelocity, ForceMode.Force);

            // Limita la velocita totale del Rigidbody
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            if (horizontalVelocity.magnitude > maxAvoidanceSpeed)
            {
                horizontalVelocity = horizontalVelocity.normalized * maxAvoidanceSpeed;
                rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
            }
        }
    }

    // Metodo pubblico per forzare il controllo dello stato di destinazione
    public void ForceDestinationCheck()
    {
        CheckDestinationStatus();
    }

    // Metodo pubblico per ottenere lo stato di destinazione
    public bool HasReachedDestination()
    {
        return hasReachedDestination;
    }

    private void OnDrawGizmosSelected()
    {
        // Disegna il raggio di evitamento
        Gizmos.color = hasReachedDestination ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, avoidanceRadius);

        // Disegna il raggio di destinazione
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, destinationRadius);

        // Disegna la direzione di evitamento
        if (avoidanceDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, avoidanceDirection * 2f);
        }

        // Disegna una linea verso la destinazione
        if (aiPath != null && aiPath.destination != Vector3.zero)
        {
            Gizmos.color = hasReachedDestination ? Color.green : Color.cyan;
            Gizmos.DrawLine(transform.position, aiPath.destination);
        }
    }
}