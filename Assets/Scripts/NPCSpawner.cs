using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using System.IO;

public class NPCSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject npcNavMeshPrefab;
    public GameObject npcAStarPrefab;

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public Transform npcTarget;
    [SerializeField] private float spawnRadius = 0.5f; // Ridotto il raggio di default
    [SerializeField] private float minSpawnDistance = 0.2f; // Ridotta distanza minima tra NPCs
    [SerializeField] private bool useGridSpawn = true; // Usa spawn a griglia per grandi quantità
    [SerializeField] private int gridThreshold = 50; // Ridotta soglia per griglia
    [SerializeField] private bool forceSmallRadius = true; // NUOVO: forza il raggio piccolo

    [Header("Performance Settings")]
    [SerializeField] private int spawnBatchSize = 10;
    [SerializeField] private float batchDelay = 0.016f;
    [SerializeField] private bool useObjectPooling = true;

    [Header("UI Elements")]
    public TMP_InputField numberInput;
    public TMP_Dropdown npcTypeDropdown;
    public TMP_Text spawnProgressText;

    [Header("Managers")]
    public NPCPhysicsToggleManager physicsToggleManager;
    public NPCStatsManager statsManager;

    // Object Pooling
    private Queue<GameObject> navMeshPool = new Queue<GameObject>();
    private Queue<GameObject> aStarPool = new Queue<GameObject>();
    private const int INITIAL_POOL_SIZE = 100;

    // NPC Lists
    private List<GameObject> npcList = new List<GameObject>(200);
    private List<NavMeshNPCController> navMeshControllers = new List<NavMeshNPCController>(100);
    private List<AStarNPCController> aStarControllers = new List<AStarNPCController>(100);

    // Spawn progress tracking
    private bool isSpawning = false;
    private Coroutine currentSpawnCoroutine;

    // Cached spawn data
    private struct SpawnData
    {
        public int totalToSpawn;
        public int navMeshCount;
        public int aStarCount;
        public bool spawnNavMesh;
        public bool spawnAStar;
    }

    private SpawnData cachedSpawnData;

    // Pre-allocated arrays for batch spawning
    private Vector3[] spawnPositions = new Vector3[50];
    private readonly System.Random random = new System.Random();

    // Cached WaitForSeconds objects
    private WaitForSeconds batchWait;
    private readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

    // Grid spawn variables
    private int currentGridIndex = 0;
    private float calculatedSpawnRadius;

    // Properties for stats manager access
    public List<NavMeshNPCController> NavMeshControllers => navMeshControllers;
    public List<AStarNPCController> AStarControllers => aStarControllers;

    void Start()
    {
        InitializeComponents();
        SetupDropdown();
        SetupUI();

        batchWait = new WaitForSeconds(batchDelay);

        if (useObjectPooling)
        {
            StartCoroutine(InitializeObjectPoolsAsync());
        }
    }

    private void InitializeComponents()
    {
        // Initialize any required components
    }

    private void SetupDropdown()
    {
        var options = npcTypeDropdown.options;
        options.Clear();
        options.Add(new TMP_Dropdown.OptionData("NavMesh"));
        options.Add(new TMP_Dropdown.OptionData("A*"));
        options.Add(new TMP_Dropdown.OptionData("Tutti"));
        npcTypeDropdown.value = 0;
    }

    private void SetupUI()
    {
        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(false);
    }

    // Asynchronous initialization of object pools
    private IEnumerator InitializeObjectPoolsAsync()
    {
        const int itemsPerFrame = 10;
        int itemsCreated = 0;

        // Pre-fill the pools with a specified number of NPCs
        while (itemsCreated < INITIAL_POOL_SIZE)
        {
            int itemsThisFrame = Mathf.Min(itemsPerFrame, INITIAL_POOL_SIZE - itemsCreated);

            for (int i = 0; i < itemsThisFrame; i++)
            {
                GameObject navMeshObj = Instantiate(npcNavMeshPrefab);
                navMeshObj.SetActive(false);
                navMeshPool.Enqueue(navMeshObj);

                GameObject aStarObj = Instantiate(npcAStarPrefab);
                aStarObj.SetActive(false);
                aStarPool.Enqueue(aStarObj);
            }

            itemsCreated += itemsThisFrame;
            yield return waitForEndOfFrame;
        }
    }

    public void SpawnNPCs()
    {
        if (isSpawning)
        {
            Debug.LogWarning("Spawn già in corso. Attendere il completamento.");
            return;
        }

        if (!ValidateInput(out int count)) return;

        if (currentSpawnCoroutine != null)
        {
            StopCoroutine(currentSpawnCoroutine);
        }

        PrepareSpawnData(count, npcTypeDropdown.value);
        currentSpawnCoroutine = StartCoroutine(SpawnNPCsCoroutineOptimized(count));
    }

    // Prepares the spawn data based on the selected NPC type and count
    private void PrepareSpawnData(int count, int npcType)
    {
        cachedSpawnData.spawnNavMesh = npcType == 0 || npcType == 2;
        cachedSpawnData.spawnAStar = npcType == 1 || npcType == 2;

        cachedSpawnData.navMeshCount = cachedSpawnData.spawnNavMesh ? count : 0;
        cachedSpawnData.aStarCount = cachedSpawnData.spawnAStar ? count : 0;
        cachedSpawnData.totalToSpawn = cachedSpawnData.navMeshCount + cachedSpawnData.aStarCount;

        // Calcola il raggio di spawn in base al numero di NPCs
        CalculateOptimalSpawnRadius(cachedSpawnData.totalToSpawn);

        // Reset grid index
        currentGridIndex = 0;

        EnsureListCapacity(cachedSpawnData.totalToSpawn);
    }

    private void CalculateOptimalSpawnRadius(int totalNPCs)
    {
        if (forceSmallRadius)
        {
            // MODIFICA: usa sempre il raggio impostato nell'inspector
            calculatedSpawnRadius = spawnRadius;
            Debug.Log($"Usando raggio fisso: {calculatedSpawnRadius:F2}m per {totalNPCs} NPCs");
            return;
        }

        if (totalNPCs <= gridThreshold)
        {
            // Per pochi NPCs, usa il raggio configurato
            calculatedSpawnRadius = spawnRadius;
        }
        else
        {
            // Per molti NPCs, calcola un raggio che permetta di posizionarli tutti
            float requiredArea = totalNPCs * (minSpawnDistance * minSpawnDistance);
            calculatedSpawnRadius = Mathf.Sqrt(requiredArea / Mathf.PI);

            // Assicurati che non sia troppo piccolo
            calculatedSpawnRadius = Mathf.Max(calculatedSpawnRadius, spawnRadius);

            Debug.Log($"Calcolato raggio di spawn: {calculatedSpawnRadius:F2}m per {totalNPCs} NPCs");
        }
    }

    // Coroutine to spawn NPCs in batches
    private IEnumerator SpawnNPCsCoroutineOptimized(int count)
    {
        isSpawning = true;

        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(true);

        int spawned = 0;
        Vector3 spawnBasePos = spawnPoint.position;

        bool originalAutoSync = Physics.autoSyncTransforms;
        Physics.autoSyncTransforms = false;

        try
        {
            // Pre-allocate spawn positions array based on the batch size
            for (int i = 0; i < count; i += spawnBatchSize)
            {
                int batchEnd = Mathf.Min(i + spawnBatchSize, count);
                int batchSize = batchEnd - i;

                PreCalculateSpawnPositions(batchSize, spawnBasePos, spawned);

                // Spawn NPCs in the current batch
                for (int j = 0; j < batchSize; j++)
                {
                    Vector3 spawnPos = spawnPositions[j];

                    if (cachedSpawnData.spawnNavMesh)
                    {
                        SpawnNavMeshNPCOptimized(spawnPos);
                        spawned++;
                    }

                    if (cachedSpawnData.spawnAStar)
                    {
                        SpawnAStarNPCOptimized(spawnPos);
                        spawned++;
                    }
                }

                if (spawnProgressText != null && (spawned % 20 == 0 || spawned == cachedSpawnData.totalToSpawn))
                {
                    UpdateProgressText(spawned);
                }

                yield return batchWait;
            }
        }
        finally
        {
            Physics.autoSyncTransforms = originalAutoSync;
            Physics.SyncTransforms();

            if (spawnProgressText != null)
                spawnProgressText.gameObject.SetActive(false);

            isSpawning = false;
            currentSpawnCoroutine = null;
        }

        Debug.Log($"Spawn completato: {spawned} NPCs creati in raggio {calculatedSpawnRadius:F2}m");
    }

    // Pre-calculates spawn positions with better distribution
    private void PreCalculateSpawnPositions(int batchSize, Vector3 basePos, int startIndex)
    {
        bool shouldUseGrid = cachedSpawnData.totalToSpawn >= gridThreshold && useGridSpawn;

        for (int i = 0; i < batchSize; i++)
        {
            Vector3 spawnPos;

            if (shouldUseGrid)
            {
                spawnPos = GetGridSpawnPosition(basePos, startIndex + i);
            }
            else
            {
                spawnPos = GetRandomSpawnPosition(basePos);
            }

            spawnPositions[i] = spawnPos;
        }
    }

    private Vector3 GetGridSpawnPosition(Vector3 basePos, int index)
    {
        // MODIFICA: usa spacing molto piccolo per tenere gli NPC super vicini
        float gridSpacing = 0.15f; // Fisso molto piccolo

        // Calcola una griglia compattissima
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(cachedSpawnData.totalToSpawn));

        // Converti l'indice in coordinate di griglia
        int x = index % gridSize;
        int z = index / gridSize;

        // Centra la griglia - usa spacing molto piccolo
        float offsetX = (gridSize - 1) * gridSpacing * 0.5f;
        float offsetZ = (gridSize - 1) * gridSpacing * 0.5f;

        Vector3 gridPos = new Vector3(
            basePos.x + (x * gridSpacing) - offsetX,
            basePos.y,
            basePos.z + (z * gridSpacing) - offsetZ
        );

        // MODIFICA: nessun offset casuale per massima compattezza
        // Vector2 randomOffset = Random.insideUnitCircle * (gridSpacing * 0.1f);
        // gridPos.x += randomOffset.x;
        // gridPos.z += randomOffset.y;

        return gridPos;
    }

    private Vector3 GetRandomSpawnPosition(Vector3 basePos)
    {
        // MODIFICA: usa raggio molto piccolo per spawn casuali
        float maxDistance = Mathf.Min(calculatedSpawnRadius, 0.8f); // Limita a massimo 0.8 unità

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(0f, maxDistance);

        // Rimuovi la distribuzione quadratica per permettere spawn più al centro
        // distance = Mathf.Sqrt(distance / maxDistance) * maxDistance;

        Vector3 spawnPos = new Vector3(
            basePos.x + Mathf.Cos(angle) * distance,
            basePos.y,
            basePos.z + Mathf.Sin(angle) * distance
        );

        return spawnPos;
    }

    private void UpdateProgressText(int spawned)
    {
        float progress = (float)spawned / cachedSpawnData.totalToSpawn * 100f;
        spawnProgressText.text = $"Spawning NPCs: {progress:F0}% ({spawned}/{cachedSpawnData.totalToSpawn})";
    }

    private bool ValidateInput(out int count)
    {
        return int.TryParse(numberInput.text, out count) && count > 0;
    }

    private void EnsureListCapacity(int additionalCapacity)
    {
        int newCapacity = npcList.Count + additionalCapacity;

        if (npcList.Capacity < newCapacity)
        {
            npcList.Capacity = newCapacity;

            int navMeshCapacity = navMeshControllers.Count + cachedSpawnData.navMeshCount;
            int aStarCapacity = aStarControllers.Count + cachedSpawnData.aStarCount;

            if (navMeshControllers.Capacity < navMeshCapacity)
                navMeshControllers.Capacity = navMeshCapacity;
            if (aStarControllers.Capacity < aStarCapacity)
                aStarControllers.Capacity = aStarCapacity;
        }
    }

    // Optimized spawn methods for NavMesh and A* NPCs
    private void SpawnNavMeshNPCOptimized(Vector3 spawnPos)
    {
        GameObject navNpc = GetOrCreateNavMeshNPC();

        navNpc.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
        navNpc.SetActive(true);

        npcList.Add(navNpc);
        physicsToggleManager.RegisterNPC(navNpc);

        var navController = navNpc.GetComponent<NavMeshNPCController>();
        if (navController != null)
        {
            navController.target = npcTarget;
            navMeshControllers.Add(navController);
            navController.ResetNPC();

            // Notify stats manager of new NPC
            if (statsManager != null)
                statsManager.RegisterNavMeshNPC(navController);
        }
    }

    private void SpawnAStarNPCOptimized(Vector3 spawnPos)
    {
        // MODIFICA: offset piccolissimo per evitare sovrapposizioni
        spawnPos.x += 0.05f;

        GameObject aStarNpc = GetOrCreateAStarNPC();

        aStarNpc.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);
        aStarNpc.SetActive(true);

        npcList.Add(aStarNpc);
        physicsToggleManager.RegisterNPC(aStarNpc);

        var aStarController = aStarNpc.GetComponent<AStarNPCController>();
        if (aStarController != null)
        {
            aStarController.target = npcTarget;
            aStarControllers.Add(aStarController);
            aStarController.ResetNPC();

            // Notify stats manager of new NPC
            if (statsManager != null)
                statsManager.RegisterAStarNPC(aStarController);
        }
    }

    private GameObject GetOrCreateNavMeshNPC()
    {
        if (useObjectPooling && navMeshPool.Count > 0)
        {
            return navMeshPool.Dequeue();
        }

        return Instantiate(npcNavMeshPrefab);
    }

    private GameObject GetOrCreateAStarNPC()
    {
        if (useObjectPooling && aStarPool.Count > 0)
        {
            return aStarPool.Dequeue();
        }

        return Instantiate(npcAStarPrefab);
    }

    // Method to return NPCs to the pool
    public void ReturnToPool(GameObject npc, bool isNavMesh)
    {
        if (!useObjectPooling) return;

        npc.SetActive(false);

        if (isNavMesh)
            navMeshPool.Enqueue(npc);
        else
            aStarPool.Enqueue(npc);
    }

    void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame && isSpawning)
        {
            CancelSpawning();
        }
    }

    private void CancelSpawning()
    {
        if (currentSpawnCoroutine != null)
        {
            StopCoroutine(currentSpawnCoroutine);
            currentSpawnCoroutine = null;
        }

        isSpawning = false;

        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(false);

        Debug.Log("Spawn cancellato dall'utente");
    }

    private void OnDestroy()
    {
        if (currentSpawnCoroutine != null)
        {
            StopCoroutine(currentSpawnCoroutine);
        }

        npcList?.Clear();
        navMeshControllers?.Clear();
        aStarControllers?.Clear();
        navMeshPool?.Clear();
        aStarPool?.Clear();
    }
}