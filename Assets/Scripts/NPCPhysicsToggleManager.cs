using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
using Pathfinding;
using System.Collections.Generic;
using System.Linq;

public class NPCPhysicsToggleManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Toggle collisionToggle;

    [Header("Default Physics Settings")]
    [SerializeField] private PhysicsSettings defaultPhysicsSettings = new PhysicsSettings();

    [Header("Performance Settings")]
    [SerializeField] private bool batchUpdates = true;
    [SerializeField] private int maxUpdatesPerFrame = 10;
    [SerializeField] private bool cleanupNullReferences = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Collections ottimizzate con HashSet per performance O(1)
    private readonly HashSet<NavMeshAgent> navMeshAgents = new HashSet<NavMeshAgent>();
    private readonly HashSet<AIPath> aStarAgents = new HashSet<AIPath>();
    private readonly Dictionary<AIPath, NPCPhysicsState> physicsStates = new Dictionary<AIPath, NPCPhysicsState>();

    // Stato corrente
    private bool currentCollisionState = true;
    private bool isInitialized = false;

    // Batch processing
    private Queue<System.Action> pendingUpdates = new Queue<System.Action>();

    // Cached layer mask
    private int defaultLayer = -1;

    #region Data Structures

    [System.Serializable]
    public class PhysicsSettings
    {
        [Header("Collider Settings")]
        public float capsuleRadius = 0.5f;
        public float capsuleHeight = 2f;
        public Vector3 capsuleCenter = new Vector3(0, 1f, 0);

        [Header("Rigidbody Settings")]
        public float mass = 1f;
        public float linearDamping = 8f;
        public float angularDamping = 10f;
        public CollisionDetectionMode collisionMode = CollisionDetectionMode.Continuous;

        [Header("AIPath Settings")]
        public float minMaxSpeed = 3f;
        public float slowdownDistance = 3f;
        public float pickNextWaypointDist = 2f;
        public float rotationSpeed = 180f;
    }

    private class NPCPhysicsState
    {
        public bool hadCollider;
        public bool hadRigidbody;
        public bool hadAvoidance;
        public bool wasKinematic;
        public bool wasDetectCollisions;
        public bool wasTrigger;

        public void SaveState(GameObject npc)
        {
            var collider = npc.GetComponent<Collider>();
            var rb = npc.GetComponent<Rigidbody>();
            var avoidance = npc.GetComponent<NPCAvoidance>();

            hadCollider = collider != null;
            hadRigidbody = rb != null;
            hadAvoidance = avoidance != null;

            if (rb != null)
            {
                wasKinematic = rb.isKinematic;
                wasDetectCollisions = rb.detectCollisions;
            }

            if (collider != null)
            {
                wasTrigger = collider.isTrigger;
            }
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeManager();
    }

    private void Start()
    {
        SetupToggle();
    }

    private void Update()
    {
        if (batchUpdates)
        {
            ProcessBatchUpdates();
        }

        if (cleanupNullReferences && Time.frameCount % 300 == 0) // Ogni 5 secondi a 60fps
        {
            CleanupNullReferences();
        }
    }

    private void OnDestroy()
    {
        CleanupManager();
    }

    #endregion

    #region Initialization & Cleanup

    private void InitializeManager()
    {
        defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer == -1) defaultLayer = 0;

        isInitialized = true;

        if (enableDebugLogs)
            Debug.Log($"[NPCPhysicsToggleManager] Initialized with default layer: {defaultLayer}");
    }

    private void SetupToggle()
    {
        if (collisionToggle == null)
        {
            Debug.LogError("[NPCPhysicsToggleManager] Toggle non assegnato!", this);
            return;
        }

        collisionToggle.onValueChanged.AddListener(OnToggleChanged);
        currentCollisionState = collisionToggle.isOn;

        if (enableDebugLogs)
            Debug.Log($"[NPCPhysicsToggleManager] Toggle setup completed. Initial state: {currentCollisionState}");
    }

    private void CleanupManager()
    {
        if (collisionToggle != null)
        {
            collisionToggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        navMeshAgents.Clear();
        aStarAgents.Clear();
        physicsStates.Clear();
        pendingUpdates.Clear();
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Registra un NPC e applica lo stato di collisione corrente
    /// </summary>
    /// <param name="npc">GameObject dell'NPC da registrare</param>
    /// <returns>True se registrato con successo</returns>
    public bool RegisterNPC(GameObject npc)
    {
        if (!IsValidNPC(npc)) return false;

        bool registered = false;

        // Registra NavMeshAgent
        if (npc.TryGetComponent(out NavMeshAgent navAgent))
        {
            if (navMeshAgents.Add(navAgent))
            {
                ApplyNavMeshCollision(navAgent, currentCollisionState);
                registered = true;
            }
        }

        // Registra AIPath
        if (npc.TryGetComponent(out AIPath aiPath))
        {
            if (aStarAgents.Add(aiPath))
            {
                // Salva stato originale
                var state = new NPCPhysicsState();
                state.SaveState(npc);
                physicsStates[aiPath] = state;

                ApplyAStarCollision(aiPath, currentCollisionState);
                registered = true;
            }
        }

        if (enableDebugLogs && registered)
            Debug.Log($"[NPCPhysicsToggleManager] Registered NPC: {npc.name}");

        return registered;
    }

    /// <summary>
    /// Rimuove un NPC dal manager
    /// </summary>
    /// <param name="npc">GameObject dell'NPC da rimuovere</param>
    /// <returns>True se rimosso con successo</returns>
    public bool UnregisterNPC(GameObject npc)
    {
        if (npc == null) return false;

        bool unregistered = false;

        if (npc.TryGetComponent(out NavMeshAgent navAgent))
        {
            unregistered |= navMeshAgents.Remove(navAgent);
        }

        if (npc.TryGetComponent(out AIPath aiPath))
        {
            unregistered |= aStarAgents.Remove(aiPath);
            physicsStates.Remove(aiPath);
        }

        return unregistered;
    }

    /// <summary>
    /// Forza l'aggiornamento di tutti gli NPC registrati
    /// </summary>
    public void ForceUpdateAllNPCs()
    {
        OnToggleChanged(currentCollisionState);
    }

    /// <summary>
    /// Ottiene il numero di NPC attualmente registrati
    /// </summary>
    public (int navMesh, int aStar) GetRegisteredCount()
    {
        return (navMeshAgents.Count, aStarAgents.Count);
    }

    #endregion

    #region Toggle Management

    private void OnToggleChanged(bool enabled)
    {
        currentCollisionState = enabled;

        if (enableDebugLogs)
            Debug.Log($"[NPCPhysicsToggleManager] Collision state changed to: {enabled}");

        if (batchUpdates)
        {
            QueueBatchUpdates(enabled);
        }
        else
        {
            ApplyImmediateUpdates(enabled);
        }
    }

    private void QueueBatchUpdates(bool enabled)
    {
        // Queue NavMesh updates
        foreach (var agent in navMeshAgents.Where(a => a != null))
        {
            var capturedAgent = agent; // Closure capture
            pendingUpdates.Enqueue(() => ApplyNavMeshCollision(capturedAgent, enabled));
        }

        // Queue AStar updates
        foreach (var ai in aStarAgents.Where(a => a != null))
        {
            var capturedAI = ai; // Closure capture
            pendingUpdates.Enqueue(() => ApplyAStarCollision(capturedAI, enabled));
        }
    }

    private void ApplyImmediateUpdates(bool enabled)
    {
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

    private void ProcessBatchUpdates()
    {
        int processed = 0;
        while (pendingUpdates.Count > 0 && processed < maxUpdatesPerFrame)
        {
            try
            {
                pendingUpdates.Dequeue().Invoke();
                processed++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NPCPhysicsToggleManager] Error during batch update: {e.Message}");
            }
        }
    }

    #endregion

    #region Collision Application

    private void ApplyNavMeshCollision(NavMeshAgent agent, bool enabled)
    {
        if (agent == null) return;

        try
        {
            agent.obstacleAvoidanceType = enabled
                ? ObstacleAvoidanceType.HighQualityObstacleAvoidance
                : ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NPCPhysicsToggleManager] Error applying NavMesh collision to {agent.name}: {e.Message}");
        }
    }

    private void ApplyAStarCollision(AIPath ai, bool enabled)
    {
        if (ai == null) return;

        try
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
        catch (System.Exception e)
        {
            Debug.LogError($"[NPCPhysicsToggleManager] Error applying AStar collision to {ai.name}: {e.Message}");
        }
    }

    private void EnableAStarAvoidance(AIPath ai)
    {
        var gameObj = ai.gameObject;

        // Setup Collider
        var collider = SetupCollider(gameObj);

        // Setup Rigidbody
        var rb = SetupRigidbody(gameObj);

        // Setup Avoidance
        SetupAvoidance(gameObj);

        // Configure AIPath
        ConfigureAIPath(ai);

        // Set Layer
        if (gameObj.layer == 0)
        {
            gameObj.layer = defaultLayer;
        }
    }

    private void DisableAStarAvoidance(AIPath ai)
    {
        var gameObj = ai.gameObject;

        // Disable avoidance
        var avoidance = gameObj.GetComponent<NPCAvoidance>();
        if (avoidance != null)
        {
            avoidance.enabled = false;
        }

        // Configure Rigidbody
        var rb = gameObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Configure Collider
        var collider = gameObj.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    #endregion

    #region Component Setup

    private Collider SetupCollider(GameObject gameObj)
    {
        var collider = gameObj.GetComponent<Collider>();
        if (collider == null)
        {
            var capsule = gameObj.AddComponent<CapsuleCollider>();
            capsule.radius = defaultPhysicsSettings.capsuleRadius;
            capsule.height = defaultPhysicsSettings.capsuleHeight;
            capsule.center = defaultPhysicsSettings.capsuleCenter;
            collider = capsule;
        }

        collider.isTrigger = false;
        collider.enabled = true;

        return collider;
    }

    private Rigidbody SetupRigidbody(GameObject gameObj)
    {
        var rb = gameObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObj.AddComponent<Rigidbody>();
        }

        rb.isKinematic = false;
        rb.mass = defaultPhysicsSettings.mass;
        rb.linearDamping = defaultPhysicsSettings.linearDamping;
        rb.angularDamping = defaultPhysicsSettings.angularDamping;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.collisionDetectionMode = defaultPhysicsSettings.collisionMode;

        return rb;
    }

    private NPCAvoidance SetupAvoidance(GameObject gameObj)
    {
        var avoidance = gameObj.GetComponent<NPCAvoidance>();
        if (avoidance == null)
        {
            avoidance = gameObj.AddComponent<NPCAvoidance>();
        }
        avoidance.enabled = true;

        return avoidance;
    }

    private void ConfigureAIPath(AIPath ai)
    {
        ai.maxSpeed = Mathf.Max(ai.maxSpeed, defaultPhysicsSettings.minMaxSpeed);
        ai.slowdownDistance = defaultPhysicsSettings.slowdownDistance;
        ai.pickNextWaypointDist = defaultPhysicsSettings.pickNextWaypointDist;
        ai.enableRotation = true;
        ai.rotationSpeed = defaultPhysicsSettings.rotationSpeed;
    }

    #endregion

    #region Validation & Cleanup

    private bool IsValidNPC(GameObject npc)
    {
        if (npc == null)
        {
            Debug.LogWarning("[NPCPhysicsToggleManager] Attempted to register null NPC");
            return false;
        }

        if (!isInitialized)
        {
            Debug.LogWarning("[NPCPhysicsToggleManager] Manager not initialized");
            return false;
        }

        return npc.GetComponent<NavMeshAgent>() != null || npc.GetComponent<AIPath>() != null;
    }

    private void CleanupNullReferences()
    {
        // Cleanup NavMesh agents
        navMeshAgents.RemoveWhere(agent => agent == null);

        // Cleanup AStar agents
        var keysToRemove = physicsStates.Keys.Where(key => key == null).ToList();
        foreach (var key in keysToRemove)
        {
            physicsStates.Remove(key);
        }
        aStarAgents.RemoveWhere(ai => ai == null);

        if (enableDebugLogs && keysToRemove.Count > 0)
            Debug.Log($"[NPCPhysicsToggleManager] Cleaned up {keysToRemove.Count} null references");
    }

    #endregion

    #region Editor Support

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnValidate()
    {
        if (defaultPhysicsSettings.capsuleRadius <= 0)
            defaultPhysicsSettings.capsuleRadius = 0.5f;

        if (defaultPhysicsSettings.capsuleHeight <= 0)
            defaultPhysicsSettings.capsuleHeight = 2f;

        if (maxUpdatesPerFrame <= 0)
            maxUpdatesPerFrame = 1;
    }

    #endregion
}