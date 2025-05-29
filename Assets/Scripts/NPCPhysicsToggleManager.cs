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

    private AIPath aiPath;
    private Rigidbody rb;
    private Vector3 avoidanceDirection;

    private void Start()
    {
        aiPath = GetComponent<AIPath>();
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (aiPath == null || rb == null || !enabled) return;

        CalculateAvoidance();
        ApplyAvoidanceForce();
    }

    private void CalculateAvoidance()
    {
        avoidanceDirection = Vector3.zero;

        // Trova tutti i collider nelle vicinanze
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, avoidanceRadius, avoidanceLayers);

        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue;

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

    private void OnDrawGizmosSelected()
    {
        // Disegna il raggio di evitamento
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, avoidanceRadius);

        // Disegna la direzione di evitamento
        if (avoidanceDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, avoidanceDirection * 2f);
        }
    }
}