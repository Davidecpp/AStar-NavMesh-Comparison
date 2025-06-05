using Pathfinding;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manager centralizzato per gestire l'evitamento di tutti gli NPC
/// Riduce drasticamente il numero di update individuali
/// </summary>
public class NPCAvoidanceManager : MonoBehaviour
{
    public static NPCAvoidanceManager Instance { get; private set; }

    [Header("Performance Settings")]
    [SerializeField] private int npcProcessedPerFrame = 20;
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private bool useMultiThreading = true;

    [Header("Spatial Partitioning")]
    [SerializeField] private float cellSize = 10f;
    [SerializeField] private int maxNPCsPerCell = 50;

    // Collections ottimizzate
    private readonly List<NPCAvoidance> allNPCs = new List<NPCAvoidance>(1000);
    private readonly Queue<NPCAvoidance> updateQueue = new Queue<NPCAvoidance>(1000);
    private readonly Dictionary<Vector2Int, List<NPCAvoidance>> spatialGrid = new Dictionary<Vector2Int, List<NPCAvoidance>>();

    // Pooling per performance
    private readonly Stack<List<NPCAvoidance>> listPool = new Stack<List<NPCAvoidance>>();
    private readonly Collider[] sharedColliderArray = new Collider[64];

    // Threading
    private readonly object lockObject = new object();
    private bool isProcessing = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(ProcessNPCsCoroutine());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Registra un NPC nel sistema centralizzato
    /// </summary>
    public void RegisterNPC(NPCAvoidance npc)
    {
        if (npc != null && !allNPCs.Contains(npc))
        {
            allNPCs.Add(npc);
            updateQueue.Enqueue(npc);
        }
    }

    /// <summary>
    /// Rimuove un NPC dal sistema
    /// </summary>
    public void UnregisterNPC(NPCAvoidance npc)
    {
        allNPCs.Remove(npc);
        RemoveFromSpatialGrid(npc);
    }

    /// <summary>
    /// Coroutine principale che processa gli NPC in batch
    /// </summary>
    private IEnumerator ProcessNPCsCoroutine()
    {
        while (true)
        {
            if (allNPCs.Count > 0 && !isProcessing)
            {
                yield return StartCoroutine(ProcessBatchOfNPCs());
            }
            yield return new WaitForSeconds(updateInterval);
        }
    }

    /// <summary>
    /// Processa un batch di NPC per frame
    /// </summary>
    private IEnumerator ProcessBatchOfNPCs()
    {
        isProcessing = true;

        // Aggiorna griglia spaziale
        UpdateSpatialGrid();
        yield return null;

        int processed = 0;
        int totalNPCs = allNPCs.Count;

        // Processa NPC in batch
        for (int i = 0; i < totalNPCs; i++)
        {
            if (processed >= npcProcessedPerFrame)
            {
                processed = 0;
                yield return null; // Pausa per il prossimo frame
            }

            var npc = allNPCs[i];
            if (npc != null && npc.isActiveAndEnabled)
            {
                ProcessSingleNPC(npc);
                processed++;
            }
        }

        isProcessing = false;
    }

    /// <summary>
    /// Aggiorna la griglia spaziale per ottimizzare le query di vicinanza
    /// </summary>
    private void UpdateSpatialGrid()
    {
        // Clear existing grid
        foreach (var list in spatialGrid.Values)
        {
            list.Clear();
            listPool.Push(list);
        }
        spatialGrid.Clear();

        // Populate grid
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
            }
        }
    }

    /// <summary>
    /// Processa un singolo NPC
    /// </summary>
    private void ProcessSingleNPC(NPCAvoidance npc)
    {
        npc.UpdateCachedValues();
        npc.CheckDestinationStatus();

        if (npc.ShouldCalculateAvoidance())
        {
            CalculateAvoidanceForNPC(npc);
        }
    }

    /// <summary>
    /// Calcola l'evitamento per un NPC usando la griglia spaziale
    /// </summary>
    private void CalculateAvoidanceForNPC(NPCAvoidance npc)
    {
        var neighbors = GetNearbyNPCs(npc);
        npc.CalculateAvoidanceFromNeighbors(neighbors);
    }

    /// <summary>
    /// Ottiene gli NPC vicini usando la griglia spaziale
    /// </summary>
    private List<NPCAvoidance> GetNearbyNPCs(NPCAvoidance npc)
    {
        var nearbyNPCs = new List<NPCAvoidance>();
        var npcCell = WorldToGrid(npc.CachedPosition);

        // Controlla celle adiacenti (3x3 grid)
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
                            float distance = Vector3.Distance(npc.CachedPosition, otherNPC.CachedPosition);
                            if (distance <= npc.AvoidanceRadius)
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