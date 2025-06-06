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
    [SerializeField] private int maxUpdatesPerFrame = 20;
    [SerializeField] private bool cleanupNullReferences = true;
    [SerializeField] private float cleanupInterval = 5f;

    [Header("Optimization Settings")]
    [SerializeField] private bool useObjectPooling = true;
    [SerializeField] private bool cacheComponents = true;
    [SerializeField] private bool useFrameSpread = true;
    [SerializeField] private int frameSpreadAmount = 3;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Collections ottimizzate
    private readonly Dictionary<GameObject, NPCPhysicsState> npcStates = new Dictionary<GameObject, NPCPhysicsState>();
    private readonly HashSet<NavMeshAgent> navMeshAgents = new HashSet<NavMeshAgent>();
    private readonly HashSet<AIPath> aStarAgents = new HashSet<AIPath>();

    // Cached components per performance
    private readonly Dictionary<GameObject, ComponentCache> componentCache = new Dictionary<GameObject, ComponentCache>();

    // Stato corrente
    private bool currentCollisionState = true;
    private bool isInitialized = false;

    // Batch processing ottimizzato
    private readonly Queue<BatchUpdate> pendingUpdates = new Queue<BatchUpdate>();
    private int currentFrame = 0;

    // Cleanup timer
    private float lastCleanupTime;

    // Cached layer mask
    private int defaultLayer = -1;

    // Object pooling
    private readonly Stack<ComponentCache> componentCachePool = new Stack<ComponentCache>();

    #region Data Structures

    [System.Serializable]
    public class PhysicsSettings
    {
        [Header("BoxCollider Settings")]
        public Vector3 boxSize = new Vector3(1f, 2f, 1f);
        public Vector3 boxCenter = new Vector3(0, 1f, 0);
        public bool isTrigger = false;

        [Header("Rigidbody Settings")]
        public float mass = 1f;
        public float linearDamping = 8f;
        public float angularDamping = 10f;
        public CollisionDetectionMode collisionMode = CollisionDetectionMode.Discrete;
        public RigidbodyConstraints constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        [Header("AIPath Settings")]
        public float minMaxSpeed = 3f;
        public float slowdownDistance = 3f;
        public float pickNextWaypointDist = 2f;
        public float rotationSpeed = 180f;
    }

    private struct BatchUpdate
    {
        public GameObject npc;
        public bool enabled;
        public int frameToProcess;

        public BatchUpdate(GameObject npc, bool enabled, int frameToProcess)
        {
            this.npc = npc;
            this.enabled = enabled;
            this.frameToProcess = frameToProcess;
        }
    }

    private class ComponentCache
    {
        public BoxCollider boxCollider;
        public Rigidbody rigidbody;
        public NPCAvoidance avoidance;
        public NavMeshAgent navMeshAgent;
        public AIPath aiPath;
        public Transform transform;

        // Dati originali del BoxCollider
        public Vector3 originalBoxSize;
        public Vector3 originalBoxCenter;
        public bool originalIsTrigger;

        public void CacheComponents(GameObject npc)
        {
            boxCollider = npc.GetComponent<BoxCollider>();
            rigidbody = npc.GetComponent<Rigidbody>();
            avoidance = npc.GetComponent<NPCAvoidance>();
            navMeshAgent = npc.GetComponent<NavMeshAgent>();
            aiPath = npc.GetComponent<AIPath>();
            transform = npc.transform;

            // Cache dati originali del BoxCollider
            if (boxCollider != null)
            {
                originalBoxSize = boxCollider.size;
                originalBoxCenter = boxCollider.center;
                originalIsTrigger = boxCollider.isTrigger;
            }
        }

        public void Clear()
        {
            boxCollider = null;
            rigidbody = null;
            avoidance = null;
            navMeshAgent = null;
            aiPath = null;
            transform = null;
        }
    }

    private class NPCPhysicsState
    {
        public bool hadBoxCollider;
        public bool hadRigidbody;
        public bool hadAvoidance;
        public bool wasKinematic;
        public bool wasDetectCollisions;
        public bool wasTrigger;
        public Vector3 originalBoxSize;
        public Vector3 originalBoxCenter;

        public void SaveState(ComponentCache cache)
        {
            hadBoxCollider = cache.boxCollider != null;
            hadRigidbody = cache.rigidbody != null;
            hadAvoidance = cache.avoidance != null;

            if (cache.rigidbody != null)
            {
                wasKinematic = cache.rigidbody.isKinematic;
                wasDetectCollisions = cache.rigidbody.detectCollisions;
            }

            if (cache.boxCollider != null)
            {
                wasTrigger = cache.boxCollider.isTrigger;
                originalBoxSize = cache.originalBoxSize;
                originalBoxCenter = cache.originalBoxCenter;
            }
        }

        public void RestoreState(ComponentCache cache)
        {
            if (cache.rigidbody != null)
            {
                cache.rigidbody.isKinematic = wasKinematic;
                cache.rigidbody.detectCollisions = wasDetectCollisions;
            }

            if (cache.boxCollider != null)
            {
                cache.boxCollider.isTrigger = wasTrigger;
                cache.boxCollider.size = originalBoxSize;
                cache.boxCollider.center = originalBoxCenter;
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
        currentFrame++;

        if (batchUpdates)
        {
            ProcessBatchUpdates();
        }

        // Cleanup periodico
        if (cleanupNullReferences && Time.time - lastCleanupTime > cleanupInterval)
        {
            CleanupNullReferences();
            lastCleanupTime = Time.time;
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

        lastCleanupTime = Time.time;
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

        // Cleanup collections
        npcStates.Clear();
        navMeshAgents.Clear();
        aStarAgents.Clear();
        componentCache.Clear();
        pendingUpdates.Clear();
        componentCachePool.Clear();
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Registra un NPC e applica lo stato di collisione corrente
    /// </summary>
    public bool RegisterNPC(GameObject npc)
    {
        if (!IsValidNPC(npc)) return false;

        // Evita duplicati
        if (npcStates.ContainsKey(npc)) return false;

        // Ottieni o crea cache dei componenti
        var cache = GetOrCreateComponentCache(npc);
        cache.CacheComponents(npc);

        // Salva stato originale
        var state = new NPCPhysicsState();
        state.SaveState(cache);
        npcStates[npc] = state;

        // Registra agenti
        RegisterAgents(cache);

        // Applica stato corrente
        if (batchUpdates && useFrameSpread)
        {
            QueueBatchUpdate(npc, currentCollisionState);
        }
        else
        {
            ApplyCollisionState(cache, currentCollisionState);
        }

        if (enableDebugLogs)
            Debug.Log($"[NPCPhysicsToggleManager] Registered NPC: {npc.name}");

        return true;
    }

    /// <summary>
    /// Rimuove un NPC dal manager
    /// </summary>
    public bool UnregisterNPC(GameObject npc)
    {
        if (npc == null) return false;

        bool removed = false;

        // Rimuovi dallo stato
        if (npcStates.Remove(npc))
        {
            removed = true;

            // Ripristina stato originale se possibile
            if (componentCache.TryGetValue(npc, out var cache))
            {
                UnregisterAgents(cache);

                // Restituisci cache al pool
                if (useObjectPooling)
                {
                    cache.Clear();
                    componentCachePool.Push(cache);
                }

                componentCache.Remove(npc);
            }
        }

        return removed;
    }

    /// <summary>
    /// Forza l'aggiornamento di tutti gli NPC registrati
    /// </summary>
    public void ForceUpdateAllNPCs()
    {
        OnToggleChanged(currentCollisionState);
    }

    /// <summary>
    /// Ottiene statistiche sul manager
    /// </summary>
    public (int total, int navMesh, int aStar, int pending) GetStatistics()
    {
        return (npcStates.Count, navMeshAgents.Count, aStarAgents.Count, pendingUpdates.Count);
    }

    #endregion

    #region Component Management

    private ComponentCache GetOrCreateComponentCache(GameObject npc)
    {
        if (cacheComponents && componentCache.TryGetValue(npc, out var existingCache))
        {
            return existingCache;
        }

        ComponentCache cache;
        if (useObjectPooling && componentCachePool.Count > 0)
        {
            cache = componentCachePool.Pop();
        }
        else
        {
            cache = new ComponentCache();
        }

        if (cacheComponents)
        {
            componentCache[npc] = cache;
        }

        return cache;
    }

    private void RegisterAgents(ComponentCache cache)
    {
        if (cache.navMeshAgent != null)
        {
            navMeshAgents.Add(cache.navMeshAgent);
        }

        if (cache.aiPath != null)
        {
            aStarAgents.Add(cache.aiPath);
        }
    }

    private void UnregisterAgents(ComponentCache cache)
    {
        if (cache.navMeshAgent != null)
        {
            navMeshAgents.Remove(cache.navMeshAgent);
        }

        if (cache.aiPath != null)
        {
            aStarAgents.Remove(cache.aiPath);
        }
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
            QueueAllUpdates(enabled);
        }
        else
        {
            ApplyImmediateUpdates(enabled);
        }
    }

    private void QueueAllUpdates(bool enabled)
    {
        int frameOffset = 0;

        foreach (var npc in npcStates.Keys)
        {
            if (npc != null)
            {
                int targetFrame = useFrameSpread ? currentFrame + frameOffset : currentFrame;
                QueueBatchUpdate(npc, enabled, targetFrame);

                if (useFrameSpread)
                {
                    frameOffset = (frameOffset + 1) % frameSpreadAmount;
                }
            }
        }
    }

    private void QueueBatchUpdate(GameObject npc, bool enabled, int targetFrame = -1)
    {
        if (targetFrame == -1)
            targetFrame = currentFrame + 1;

        pendingUpdates.Enqueue(new BatchUpdate(npc, enabled, targetFrame));
    }

    private void ApplyImmediateUpdates(bool enabled)
    {
        foreach (var kvp in componentCache)
        {
            if (kvp.Key != null)
            {
                ApplyCollisionState(kvp.Value, enabled);
            }
        }
    }

    private void ProcessBatchUpdates()
    {
        int processed = 0;

        while (pendingUpdates.Count > 0 && processed < maxUpdatesPerFrame)
        {
            var update = pendingUpdates.Peek();

            // Controlla se è il momento di processare questo update
            if (useFrameSpread && currentFrame < update.frameToProcess)
            {
                break;
            }

            pendingUpdates.Dequeue();

            try
            {
                if (update.npc != null && componentCache.TryGetValue(update.npc, out var cache))
                {
                    ApplyCollisionState(cache, update.enabled);
                    processed++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NPCPhysicsToggleManager] Error during batch update: {e.Message}");
            }
        }
    }

    #endregion

    #region Collision Application

    private void ApplyCollisionState(ComponentCache cache, bool enabled)
    {
        if (cache.navMeshAgent != null)
        {
            ApplyNavMeshCollision(cache.navMeshAgent, enabled);
        }

        if (cache.aiPath != null)
        {
            ApplyAStarCollision(cache, enabled);
        }
    }

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
            Debug.LogError($"[NPCPhysicsToggleManager] Error applying NavMesh collision: {e.Message}");
        }
    }

    private void ApplyAStarCollision(ComponentCache cache, bool enabled)
    {
        if (cache.aiPath == null) return;

        try
        {
            if (enabled)
            {
                EnableAStarAvoidance(cache);
            }
            else
            {
                DisableAStarAvoidance(cache);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NPCPhysicsToggleManager] Error applying AStar collision: {e.Message}");
        }
    }

    private void EnableAStarAvoidance(ComponentCache cache)
    {
        var gameObj = cache.aiPath.gameObject;

        // Setup BoxCollider
        SetupBoxCollider(cache);

        // Setup Rigidbody
        SetupRigidbody(cache);

        // Setup Avoidance
        SetupAvoidance(cache);

        // Configure AIPath
        ConfigureAIPath(cache.aiPath);

        // Set Layer
        if (gameObj.layer == 0)
        {
            gameObj.layer = defaultLayer;
        }
    }

    private void DisableAStarAvoidance(ComponentCache cache)
    {
        // Disable avoidance
        if (cache.avoidance != null)
        {
            cache.avoidance.enabled = false;
        }

        // Configure Rigidbody
        if (cache.rigidbody != null)
        {
            cache.rigidbody.isKinematic = true;
            cache.rigidbody.detectCollisions = false;
        }

        // Configure BoxCollider
        if (cache.boxCollider != null)
        {
            cache.boxCollider.isTrigger = true;
        }
    }

    #endregion

    #region Component Setup

    private void SetupBoxCollider(ComponentCache cache)
    {
        var gameObj = cache.aiPath.gameObject;

        if (cache.boxCollider == null)
        {
            cache.boxCollider = gameObj.AddComponent<BoxCollider>();
            cache.boxCollider.size = defaultPhysicsSettings.boxSize;
            cache.boxCollider.center = defaultPhysicsSettings.boxCenter;
        }

        cache.boxCollider.isTrigger = defaultPhysicsSettings.isTrigger;
        cache.boxCollider.enabled = true;
    }

    private void SetupRigidbody(ComponentCache cache)
    {
        var gameObj = cache.aiPath.gameObject;

        if (cache.rigidbody == null)
        {
            cache.rigidbody = gameObj.AddComponent<Rigidbody>();
        }

        var rb = cache.rigidbody;
        rb.isKinematic = false;
        rb.mass = defaultPhysicsSettings.mass;
        rb.linearDamping = defaultPhysicsSettings.linearDamping;
        rb.angularDamping = defaultPhysicsSettings.angularDamping;
        rb.useGravity = false;
        rb.constraints = defaultPhysicsSettings.constraints;
        rb.collisionDetectionMode = defaultPhysicsSettings.collisionMode;
    }

    private void SetupAvoidance(ComponentCache cache)
    {
        var gameObj = cache.aiPath.gameObject;

        if (cache.avoidance == null)
        {
            cache.avoidance = gameObj.AddComponent<NPCAvoidance>();
        }

        cache.avoidance.enabled = true;
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
            if (enableDebugLogs)
                Debug.LogWarning("[NPCPhysicsToggleManager] Attempted to register null NPC");
            return false;
        }

        if (!isInitialized)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[NPCPhysicsToggleManager] Manager not initialized");
            return false;
        }

        return npc.GetComponent<NavMeshAgent>() != null || npc.GetComponent<AIPath>() != null;
    }

    private void CleanupNullReferences()
    {
        // Cleanup con lista temporanea per evitare modifiche durante iterazione
        var keysToRemove = new List<GameObject>();

        foreach (var kvp in npcStates)
        {
            if (kvp.Key == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            UnregisterNPC(key);
        }

        // Cleanup agenti
        navMeshAgents.RemoveWhere(agent => agent == null);
        aStarAgents.RemoveWhere(ai => ai == null);

        if (enableDebugLogs && keysToRemove.Count > 0)
            Debug.Log($"[NPCPhysicsToggleManager] Cleaned up {keysToRemove.Count} null references");
    }

    #endregion

    #region Editor Support

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnValidate()
    {
        if (defaultPhysicsSettings.boxSize.x <= 0) defaultPhysicsSettings.boxSize.x = 1f;
        if (defaultPhysicsSettings.boxSize.y <= 0) defaultPhysicsSettings.boxSize.y = 2f;
        if (defaultPhysicsSettings.boxSize.z <= 0) defaultPhysicsSettings.boxSize.z = 1f;

        if (maxUpdatesPerFrame <= 0) maxUpdatesPerFrame = 1;
        if (cleanupInterval <= 0) cleanupInterval = 1f;
        if (frameSpreadAmount <= 0) frameSpreadAmount = 1;
    }

    #endregion
}