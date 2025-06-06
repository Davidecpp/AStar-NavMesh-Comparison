using Pathfinding;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manager ultra-ottimizzato per evitamento NPC
/// Riduce drasticamente i picchi di performance
/// </summary>
public class NPCAvoidanceManager : MonoBehaviour
{
    public static NPCAvoidanceManager Instance { get; private set; }

    [Header("Performance Settings")]
    [SerializeField] private int npcProcessedPerFrame = 5; // Ridotto da 20 a 5
    [SerializeField] private float updateInterval = 0.2f; // Aumentato da 0.1f
    [SerializeField] private int maxNPCsToProcess = 100; // Limite massimo

    [Header("Spatial Partitioning")]
    [SerializeField] private float cellSize = 15f; // Aumentato per celle più grandi
    [SerializeField] private int maxNPCsPerCell = 30; // Ridotto da 50

    [Header("Advanced Optimizations")]
    [SerializeField] private bool useLODSystem = true;
    [SerializeField] private float highDetailDistance = 20f;
    [SerializeField] private float mediumDetailDistance = 50f;

    // Collections ottimizzate
    private readonly List<NPCAvoidance> allNPCs = new List<NPCAvoidance>(1000);
    private readonly Dictionary<Vector2Int, List<NPCAvoidance>> spatialGrid = new Dictionary<Vector2Int, List<NPCAvoidance>>();
    
    // Pooling avanzato
    private readonly Stack<List<NPCAvoidance>> listPool = new Stack<List<NPCAvoidance>>();
    private readonly Stack<List<Vector2Int>> cellPool = new Stack<List<Vector2Int>>();

    // LOD Groups
    private readonly List<NPCAvoidance> highDetailNPCs = new List<NPCAvoidance>();
    private readonly List<NPCAvoidance> mediumDetailNPCs = new List<NPCAvoidance>();
    private readonly List<NPCAvoidance> lowDetailNPCs = new List<NPCAvoidance>();

    // Processing state
    private int currentProcessingIndex = 0;
    private bool isProcessing = false;
    private float lastFullUpdate = 0f;

    // Camera reference for LOD
    private Transform playerTransform;
    private Camera mainCamera;

    // Performance monitoring
    private float frameTimeAccumulator = 0f;
    private int frameCount = 0;
    private const float MAX_FRAME_TIME = 16.0f; // Target 60fps

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSystem()
    {
        // Find camera and player
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            playerTransform = mainCamera.transform;
        }

        // Pre-populate pools
        for (int i = 0; i < 50; i++)
        {
            listPool.Push(new List<NPCAvoidance>());
            cellPool.Push(new List<Vector2Int>());
        }

        StartCoroutine(ProcessNPCsCoroutine());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterNPC(NPCAvoidance npc)
    {
        if (npc != null && !allNPCs.Contains(npc))
        {
            allNPCs.Add(npc);
        }
    }

    public void UnregisterNPC(NPCAvoidance npc)
    {
        allNPCs.Remove(npc);
        highDetailNPCs.Remove(npc);
        mediumDetailNPCs.Remove(npc);
        lowDetailNPCs.Remove(npc);
        RemoveFromSpatialGrid(npc);
    }

    /// <summary>
    /// Coroutine principale ultra-ottimizzata
    /// </summary>
    private IEnumerator ProcessNPCsCoroutine()
    {
        while (true)
        {
            float frameStartTime = Time.realtimeSinceStartup;

            if (allNPCs.Count > 0 && !isProcessing)
            {
                yield return StartCoroutine(ProcessBatchOptimized());
            }

            // Adatta frequenza in base alle performance
            float frameTime = (Time.realtimeSinceStartup - frameStartTime) * 1000f;
            frameTimeAccumulator += frameTime;
            frameCount++;

            if (frameCount >= 10)
            {
                float avgFrameTime = frameTimeAccumulator / frameCount;
                AdaptProcessingRate(avgFrameTime);
                frameTimeAccumulator = 0f;
                frameCount = 0;
            }

            yield return new WaitForSeconds(updateInterval);
        }
    }

    /// <summary>
    /// Processing batch ultra-ottimizzato con LOD
    /// </summary>
    private IEnumerator ProcessBatchOptimized()
    {
        isProcessing = true;

        // Update LOD groups solo ogni tanto
        if (Time.time - lastFullUpdate > 1f)
        {
            UpdateLODGroups();
            lastFullUpdate = Time.time;
            yield return null;
        }

        // Update spatial grid incrementale
        yield return StartCoroutine(UpdateSpatialGridIncremental());

        // Process NPCs con priorità LOD
        yield return StartCoroutine(ProcessNPCsByLOD());

        isProcessing = false;
    }

    /// <summary>
    /// Update LOD groups basato su distanza dal player
    /// </summary>
    private void UpdateLODGroups()
    {
        if (playerTransform == null) return;

        highDetailNPCs.Clear();
        mediumDetailNPCs.Clear();
        lowDetailNPCs.Clear();

        Vector3 playerPos = playerTransform.position;

        for (int i = 0; i < allNPCs.Count; i++)
        {
            var npc = allNPCs[i];
            if (npc == null) continue;

            float distanceSqr = (npc.CachedPosition - playerPos).sqrMagnitude;

            if (distanceSqr < highDetailDistance * highDetailDistance)
            {
                highDetailNPCs.Add(npc);
            }
            else if (distanceSqr < mediumDetailDistance * mediumDetailDistance)
            {
                mediumDetailNPCs.Add(npc);
            }
            else
            {
                lowDetailNPCs.Add(npc);
            }
        }
    }

    /// <summary>
    /// Update spatial grid in modo incrementale
    /// </summary>
    private IEnumerator UpdateSpatialGridIncremental()
    {
        // Clear solo celle che servono
        var activeCells = cellPool.Count > 0 ? cellPool.Pop() : new List<Vector2Int>();
        activeCells.Clear();

        // Trova celle attive
        foreach (var npc in allNPCs)
        {
            if (npc != null)
            {
                var cell = WorldToGrid(npc.CachedPosition);
                if (!activeCells.Contains(cell))
                {
                    activeCells.Add(cell);
                }
            }
        }

        // Clear solo celle attive
        foreach (var cell in activeCells)
        {
            if (spatialGrid.TryGetValue(cell, out var cellList))
            {
                cellList.Clear();
            }
        }

        yield return null;

        // Re-populate celle attive
        int processed = 0;
        foreach (var npc in allNPCs)
        {
            if (npc != null)
            {
                var cell = WorldToGrid(npc.CachedPosition);
                
                if (!spatialGrid.TryGetValue(cell, out var cellList))
                {
                    cellList = listPool.Count > 0 ? listPool.Pop() : new List<NPCAvoidance>();
                    spatialGrid[cell] = cellList;
                }

                if (cellList.Count < maxNPCsPerCell)
                {
                    cellList.Add(npc);
                }

                processed++;
                if (processed % 20 == 0) // Yield ogni 20 NPC
                {
                    yield return null;
                }
            }
        }

        cellPool.Push(activeCells);
    }

    /// <summary>
    /// Process NPCs con priorità LOD
    /// </summary>
    private IEnumerator ProcessNPCsByLOD()
    {
        int processed = 0;

        // High detail - process tutti
        foreach (var npc in highDetailNPCs)
        {
            if (ProcessSingleNPCOptimized(npc))
            {
                processed++;
                if (processed >= npcProcessedPerFrame)
                {
                    yield return null;
                    processed = 0;
                }
            }
        }

        // Medium detail - process 50%
        for (int i = 0; i < mediumDetailNPCs.Count; i += 2)
        {
            if (ProcessSingleNPCOptimized(mediumDetailNPCs[i]))
            {
                processed++;
                if (processed >= npcProcessedPerFrame)
                {
                    yield return null;
                    processed = 0;
                }
            }
        }

        // Low detail - process 25%
        for (int i = 0; i < lowDetailNPCs.Count; i += 4)
        {
            if (ProcessSingleNPCOptimized(lowDetailNPCs[i]))
            {
                processed++;
                if (processed >= npcProcessedPerFrame)
                {
                    yield return null;
                    processed = 0;
                }
            }
        }
    }

    /// <summary>
    /// Process singolo NPC ottimizzato
    /// </summary>
    private bool ProcessSingleNPCOptimized(NPCAvoidance npc)
    {
        if (npc == null || !npc.isActiveAndEnabled) return false;

        npc.UpdateCachedValues();
        npc.CheckDestinationStatus();

        if (npc.ShouldCalculateAvoidance())
        {
            CalculateAvoidanceOptimized(npc);
        }

        return true;
    }

    /// <summary>
    /// Calcolo avoidance ottimizzato con sqrMagnitude
    /// </summary>
    private void CalculateAvoidanceOptimized(NPCAvoidance npc)
    {
        var neighbors = GetNearbyNPCsOptimized(npc);
        npc.CalculateAvoidanceFromNeighbors(neighbors);
    }

    /// <summary>
    /// Get nearby NPCs ultra-ottimizzato
    /// </summary>
    private List<NPCAvoidance> GetNearbyNPCsOptimized(NPCAvoidance npc)
    {
        var nearbyNPCs = new List<NPCAvoidance>();
        var npcCell = WorldToGrid(npc.CachedPosition);
        float avoidanceRadiusSqr = npc.AvoidanceRadius * npc.AvoidanceRadius;

        // Check solo 3x3 grid attorno all'NPC
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                var cell = new Vector2Int(npcCell.x + x, npcCell.y + z);
                if (spatialGrid.TryGetValue(cell, out var cellNPCs))
                {
                    foreach (var otherNPC in cellNPCs)
                    {
                        if (otherNPC != npc && otherNPC != null)
                        {
                            // Usa sqrMagnitude per evitare Sqrt()
                            float distanceSqr = (npc.CachedPosition - otherNPC.CachedPosition).sqrMagnitude;
                            if (distanceSqr <= avoidanceRadiusSqr)
                            {
                                nearbyNPCs.Add(otherNPC);
                            }
                        }
                    }
                }
            }
        }

        return nearbyNPCs;
    }

    /// <summary>
    /// Adatta processing rate in base alle performance
    /// </summary>
    private void AdaptProcessingRate(float avgFrameTime)
    {
        if (avgFrameTime > MAX_FRAME_TIME)
        {
            // Performance scarse - riduci carico
            npcProcessedPerFrame = Mathf.Max(1, npcProcessedPerFrame - 1);
            updateInterval = Mathf.Min(0.5f, updateInterval + 0.02f);
        }
        else if (avgFrameTime < MAX_FRAME_TIME * 0.7f)
        {
            // Performance buone - aumenta carico
            npcProcessedPerFrame = Mathf.Min(10, npcProcessedPerFrame + 1);
            updateInterval = Mathf.Max(0.1f, updateInterval - 0.01f);
        }
    }

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize)
        );
    }

    private void RemoveFromSpatialGrid(NPCAvoidance npc)
    {
        if (npc == null) return;

        var cell = WorldToGrid(npc.CachedPosition);
        if (spatialGrid.TryGetValue(cell, out var cellList))
        {
            cellList.Remove(npc);
        }
    }
}