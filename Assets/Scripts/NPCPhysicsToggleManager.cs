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
        var rb = ai.GetComponent<Rigidbody>();

        if (enabled)
        {
            if (rb == null)
            {
                rb = ai.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
            rb.detectCollisions = true;
        }
        else
        {
            if (rb != null)
                Destroy(rb);
        }
    }
}