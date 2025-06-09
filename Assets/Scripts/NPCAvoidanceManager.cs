using Pathfinding;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class NPCAvoidanceManager : MonoBehaviour
{
    public static NPCAvoidanceManager Instance { get; private set; }

    [Header("Performance Settings")]
    [SerializeField] private int npcProcessedPerFrame = 5; 
    [SerializeField] private float updateInterval = 0.2f; 
    [SerializeField] private int maxNPCsToProcess = 100; 

    [Header("Spatial Partitioning")]
    [SerializeField] private float cellSize = 15f; 
    [SerializeField] private int maxNPCsPerCell = 30; 

    [Header("Advanced Optimizations")]
    [SerializeField] private bool useLODSystem = true;
    [SerializeField] private float highDetailDistance = 20f;
    [SerializeField] private float mediumDetailDistance = 50f;

    // Collections
    private readonly List<NPCAvoidance> allNPCs = new List<NPCAvoidance>(1000);
    private readonly Dictionary<Vector2Int, List<NPCAvoidance>> spatialGrid = new Dictionary<Vector2Int, List<NPCAvoidance>>();
    
    // Pooling
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

    // Coroutne for processing NPCs
    private IEnumerator ProcessNPCsCoroutine()
    {
        while (true)
        {
            float frameStartTime = Time.realtimeSinceStartup;

            if (allNPCs.Count > 0 && !isProcessing)
            {
                yield return StartCoroutine(ProcessBatchOptimized());
            }

            // Adapt processing rate based on performance
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

    // Process a batch of NPCs with optimizations
    private IEnumerator ProcessBatchOptimized()
    {
        isProcessing = true;

        // Update LOD groups if using LOD system
        if (Time.time - lastFullUpdate > 1f)
        {
            UpdateLODGroups();
            lastFullUpdate = Time.time;
            yield return null;
        }

        // Update spatial grid incrementale
        yield return StartCoroutine(UpdateSpatialGridIncremental());

        // Process NPCs with LOD
        yield return StartCoroutine(ProcessNPCsByLOD());

        isProcessing = false;
    }

    // Update LOD groups based on player position
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

    private IEnumerator UpdateSpatialGridIncremental()
    {
        // Clear spatial grid
        var activeCells = cellPool.Count > 0 ? cellPool.Pop() : new List<Vector2Int>();
        activeCells.Clear();

        // Find active cells
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

        // Clear active cells in the spatial grid
        foreach (var cell in activeCells)
        {
            if (spatialGrid.TryGetValue(cell, out var cellList))
            {
                cellList.Clear();
            }
        }

        yield return null;

        // Re-populate active cells
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
                if (processed % 20 == 0) // Yield every 20 NPC
                {
                    yield return null;
                }
            }
        }

        cellPool.Push(activeCells);
    }

    private IEnumerator ProcessNPCsByLOD()
    {
        int processed = 0;
        // High detail - process 100%
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

    // Process a single NPC with optimizations
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

    private void CalculateAvoidanceOptimized(NPCAvoidance npc)
    {
        var neighbors = GetNearbyNPCsOptimized(npc);
        npc.CalculateAvoidanceFromNeighbors(neighbors);
    }

    // Get nearby NPCs using spatial partitioning
    private List<NPCAvoidance> GetNearbyNPCsOptimized(NPCAvoidance npc)
    {
        var nearbyNPCs = new List<NPCAvoidance>();
        var npcCell = WorldToGrid(npc.CachedPosition);
        float avoidanceRadiusSqr = npc.AvoidanceRadius * npc.AvoidanceRadius;

        // Check the 3x3 grid around the NPC's cell
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

    private void AdaptProcessingRate(float avgFrameTime)
    {
        if (avgFrameTime > MAX_FRAME_TIME)
        {
            // Bad performance, reduce load
            npcProcessedPerFrame = Mathf.Max(1, npcProcessedPerFrame - 1);
            updateInterval = Mathf.Min(0.5f, updateInterval + 0.02f);
        }
        else if (avgFrameTime < MAX_FRAME_TIME * 0.7f)
        {
            // Good performance, increase load
            npcProcessedPerFrame = Mathf.Min(10, npcProcessedPerFrame + 1);
            updateInterval = Mathf.Max(0.1f, updateInterval - 0.01f);
        }
    }

    // Convert world position to grid cell coordinates
    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize)
        );
    }

    // Remove NPC from spatial grid
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