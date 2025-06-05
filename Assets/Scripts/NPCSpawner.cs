using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using System.IO;
using System.Text;

public class NPCSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject npcNavMeshPrefab;
    public GameObject npcAStarPrefab;

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public Transform npcTarget;

    [Header("Performance Settings")]
    [SerializeField] private int spawnBatchSize = 10;
    [SerializeField] private float batchDelay = 0.016f;
    [SerializeField] private bool useObjectPooling = true;

    [Header("UI Elements")]
    public TMP_InputField numberInput;
    public TMP_Dropdown npcTypeDropdown;
    public TMP_Text panelStatsTxt;
    public GameObject panelStats;
    public TMP_Text spawnProgressText;

    [Header("Managers")]
    public NPCPhysicsToggleManager physicsToggleManager;
    public PathDataGraph graficoNavMesh;
    public PathDataGraph graficoAStar;

    // Object Pooling - Pre-allocate larger pools
    private Queue<GameObject> navMeshPool = new Queue<GameObject>();
    private Queue<GameObject> aStarPool = new Queue<GameObject>();
    private const int INITIAL_POOL_SIZE = 100; // Increased from 50

    // Cache for performance - Pre-allocate larger capacity
    private List<GameObject> npcList = new List<GameObject>(200);
    private List<NavMeshNPCController> navMeshControllers = new List<NavMeshNPCController>(100);
    private List<AStarNPCController> aStarControllers = new List<AStarNPCController>(100);

    private Keyboard keyboard;
    private StringBuilder stringBuilder = new StringBuilder(2048); // Increased capacity

    // Cache per statistiche - Pre-allocate
    private readonly List<string> tempNavMeshStats = new List<string>(100);
    private readonly List<string> tempAStarStats = new List<string>(100);

    // Spawn progress tracking
    private bool isSpawning = false;
    private Coroutine currentSpawnCoroutine;

    // Cached spawn data to avoid recalculations
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
    private Vector3[] spawnPositions = new Vector3[50]; // Max batch size buffer
    private readonly System.Random random = new System.Random();

    private struct NPCStats
    {
        public float distanceTotal;
        public double pathTimeTotal;
        public double calcTimeTotal;
        public int count;

        public void Reset()
        {
            distanceTotal = 0f;
            pathTimeTotal = 0;
            calcTimeTotal = 0;
            count = 0;
        }

        public void AddUnchecked(float distance, double pathTime, double calcTime)
        {
            distanceTotal += distance;
            pathTimeTotal += pathTime;
            calcTimeTotal += calcTime;
            count++;
        }
    }

    private NPCStats navMeshStats;
    private NPCStats aStarStats;

    public static bool panelStatsOpen = false;

    // Costanti per evitare allocazioni - più complete
    private const string NAVMESH_HEADER = "<b>== NPC NavMesh ==</b>\n";
    private const string ASTAR_HEADER = "<b>== NPC A* ==</b>\n";
    private const string MEDIA_DISTANZA = "\n<b>Media distanza:</b> ";
    private const string MEDIA_PATHTIME = "\n<b>Media PathTime:</b> ";
    private const string MEDIA_CALCTIME = "\n<b>Media CalcTime:</b> ";
    private const string UNIT_M = " m";
    private const string UNIT_S = " s";
    private const string UNIT_MS = " ms\n\n";

    // Cached WaitForSeconds objects
    private WaitForSeconds batchWait;
    private readonly WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

    void Start()
    {
        InitializeComponents();
        SetupDropdown();
        SetupUI();

        // Pre-create WaitForSeconds to avoid allocations
        batchWait = new WaitForSeconds(batchDelay);

        if (useObjectPooling)
        {
            StartCoroutine(InitializeObjectPoolsAsync());
        }
    }

    private void InitializeComponents()
    {
        keyboard = Keyboard.current;
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
        panelStats.SetActive(false);
        panelStatsTxt.gameObject.SetActive(false);

        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(false);
    }

    private IEnumerator InitializeObjectPoolsAsync()
    {
        // Initialize pools across multiple frames to reduce frame drops
        const int itemsPerFrame = 10;
        int itemsCreated = 0;

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

        // Stop any existing spawn coroutine
        if (currentSpawnCoroutine != null)
        {
            StopCoroutine(currentSpawnCoroutine);
        }

        // Pre-calculate spawn data once
        PrepareSpawnData(count, npcTypeDropdown.value);

        currentSpawnCoroutine = StartCoroutine(SpawnNPCsCoroutineOptimized(count));
    }

    private void PrepareSpawnData(int count, int npcType)
    {
        cachedSpawnData.spawnNavMesh = npcType == 0 || npcType == 2;
        cachedSpawnData.spawnAStar = npcType == 1 || npcType == 2;

        cachedSpawnData.navMeshCount = cachedSpawnData.spawnNavMesh ? count : 0;
        cachedSpawnData.aStarCount = cachedSpawnData.spawnAStar ? count : 0;
        cachedSpawnData.totalToSpawn = cachedSpawnData.navMeshCount + cachedSpawnData.aStarCount;

        // Pre-allocate list capacity
        EnsureListCapacity(cachedSpawnData.totalToSpawn);
    }

    private IEnumerator SpawnNPCsCoroutineOptimized(int count)
    {
        isSpawning = true;

        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(true);

        int spawned = 0;
        Vector3 spawnBasePos = spawnPoint.position;

        // Temporarily disable physics auto-sync for better performance
        bool originalAutoSync = Physics.autoSyncTransforms;
        Physics.autoSyncTransforms = false;

        try
        {
            for (int i = 0; i < count; i += spawnBatchSize)
            {
                int batchEnd = Mathf.Min(i + spawnBatchSize, count);
                int batchSize = batchEnd - i;

                // Pre-calculate all spawn positions for this batch
                PreCalculateSpawnPositions(batchSize, spawnBasePos);

                // Spawn batch without individual position calculations
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

                // Update progress less frequently
                if (spawnProgressText != null && (spawned % 20 == 0 || spawned == cachedSpawnData.totalToSpawn))
                {
                    UpdateProgressText(spawned);
                }

                // Wait before next batch
                yield return batchWait;
            }
        }
        finally
        {
            // Re-enable physics auto-sync
            Physics.autoSyncTransforms = originalAutoSync;
            Physics.SyncTransforms();

            // Hide progress text
            if (spawnProgressText != null)
                spawnProgressText.gameObject.SetActive(false);

            isSpawning = false;
            currentSpawnCoroutine = null;
        }

        Debug.Log($"Spawn completato: {spawned} NPCs creati");
    }

    private void PreCalculateSpawnPositions(int batchSize, Vector3 basePos)
    {
        for (int i = 0; i < batchSize; i++)
        {
            // Use System.Random for better performance than UnityEngine.Random
            float angle = (float)(random.NextDouble() * 2.0 * System.Math.PI);
            float distance = (float)(random.NextDouble() * 1.5 + 0.5); // 0.5f to 2f range

            spawnPositions[i] = new Vector3(
                basePos.x + Mathf.Cos(angle) * distance,
                basePos.y,
                basePos.z + Mathf.Sin(angle) * distance
            );
        }
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
        }
    }

    private void SpawnAStarNPCOptimized(Vector3 spawnPos)
    {
        // Inline the offset calculation
        spawnPos.x += 1f;

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

        // Only update stats if not spawning and panel is open
        if (!isSpawning && panelStatsOpen)
        {
            UpdateStats();
        }
    }

    private void HandleInput()
    {
        if (keyboard.tabKey.wasPressedThisFrame)
        {
            CloseStats();
            ToggleGraphs();
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            SaveStatsToFile(panelStatsTxt.text);
        }

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

    private void ToggleGraphs()
    {
        if (graficoNavMesh != null)
            graficoNavMesh.gameObject.SetActive(!graficoNavMesh.gameObject.activeSelf);

        if (graficoAStar != null)
            graficoAStar.gameObject.SetActive(!graficoAStar.gameObject.activeSelf);
    }

    private void UpdateStats()
    {
        ClearTempCollections();
        ResetStats();

        ProcessNavMeshNPCs();
        ProcessAStarNPCs();

        UpdateGraphs();
        UpdateStatsText();
    }

    private void ClearTempCollections()
    {
        tempNavMeshStats.Clear();
        tempAStarStats.Clear();
    }

    private void ResetStats()
    {
        navMeshStats.Reset();
        aStarStats.Reset();
    }

    private void ProcessNavMeshNPCs()
    {
        for (int i = navMeshControllers.Count - 1; i >= 0; i--)
        {
            var controller = navMeshControllers[i];
            if (controller == null)
            {
                navMeshControllers.RemoveAt(i);
                continue;
            }

            float dist = controller.GetDistance();
            double pathTime = controller.GetPathTime();
            double calcTime = controller.GetCalcTime();

            navMeshStats.AddUnchecked(dist, pathTime, calcTime);
            tempNavMeshStats.Add($"[NavMesh NPC] {controller.name} - Distanza: {dist:F2} m, PathTime: {pathTime:F2} s, CalcTime: {calcTime:F2} ms");
        }
    }

    private void ProcessAStarNPCs()
    {
        for (int i = aStarControllers.Count - 1; i >= 0; i--)
        {
            var controller = aStarControllers[i];
            if (controller == null)
            {
                aStarControllers.RemoveAt(i);
                continue;
            }

            float dist = controller.GetDistance();
            float pathTime = controller.GetPathTime();
            double calcTime = controller.GetCalcTime();

            aStarStats.AddUnchecked(dist, pathTime, calcTime);
            tempAStarStats.Add($"[A* NPC] {controller.name} - Distanza: {dist:F2} m, PathTime: {pathTime:F2} s, CalcTime: {calcTime:F2} ms");
        }
    }

    private void UpdateGraphs()
    {
        if (navMeshStats.count > 0 && graficoNavMesh != null)
        {
            graficoNavMesh.AddDataPoint(
                (float)(navMeshStats.calcTimeTotal / navMeshStats.count),
                (float)(navMeshStats.pathTimeTotal / navMeshStats.count),
                navMeshStats.distanceTotal / navMeshStats.count
            );
        }

        if (aStarStats.count > 0 && graficoAStar != null)
        {
            graficoAStar.AddDataPoint(
                (float)(aStarStats.calcTimeTotal / aStarStats.count),
                (float)(aStarStats.pathTimeTotal / aStarStats.count),
                aStarStats.distanceTotal / aStarStats.count
            );
        }
    }

    private void UpdateStatsText()
    {
        stringBuilder.Clear();

        if (tempNavMeshStats.Count > 0)
        {
            AppendNavMeshStats();
        }

        if (tempAStarStats.Count > 0)
        {
            AppendAStarStats();
        }

        panelStatsTxt.text = stringBuilder.ToString();
    }

    private void AppendNavMeshStats()
    {
        stringBuilder.Append(NAVMESH_HEADER);

        for (int i = 0; i < tempNavMeshStats.Count; i++)
        {
            stringBuilder.AppendLine(tempNavMeshStats[i]);
        }

        float avgDistance = navMeshStats.distanceTotal / navMeshStats.count;
        double avgPathTime = navMeshStats.pathTimeTotal / navMeshStats.count;
        double avgCalcTime = navMeshStats.calcTimeTotal / navMeshStats.count;

        stringBuilder.Append(MEDIA_DISTANZA).Append(avgDistance.ToString("F2")).Append(UNIT_M);
        stringBuilder.Append(MEDIA_PATHTIME).Append(avgPathTime.ToString("F2")).Append(UNIT_S);
        stringBuilder.Append(MEDIA_CALCTIME).Append(avgCalcTime.ToString("F2")).Append(UNIT_MS);
    }

    private void AppendAStarStats()
    {
        stringBuilder.Append(ASTAR_HEADER);

        for (int i = 0; i < tempAStarStats.Count; i++)
        {
            stringBuilder.AppendLine(tempAStarStats[i]);
        }

        float avgDistance = aStarStats.distanceTotal / aStarStats.count;
        float avgPathTime = (float)(aStarStats.pathTimeTotal / aStarStats.count);
        double avgCalcTime = aStarStats.calcTimeTotal / aStarStats.count;

        stringBuilder.Append(MEDIA_DISTANZA).Append(avgDistance.ToString("F2")).Append(UNIT_M);
        stringBuilder.Append(MEDIA_PATHTIME).Append(avgPathTime.ToString("F2")).Append(UNIT_S);
        stringBuilder.Append(MEDIA_CALCTIME).Append(avgCalcTime.ToString("F2")).Append(UNIT_MS);
    }

    private void CloseStats()
    {
        panelStatsOpen = !panelStatsOpen;
        panelStats.SetActive(panelStatsOpen);
        panelStatsTxt.gameObject.SetActive(panelStatsOpen);
    }

    private void SaveStatsToFile(string content)
    {
        string folderPath = Path.Combine(Application.dataPath, "NPCStatsLogs");

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string fileName = $"NPC_Stats_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        string fullPath = Path.Combine(folderPath, fileName);

        try
        {
            File.WriteAllText(fullPath, content);
            Debug.Log($"Statistiche salvate in: {fullPath}");
        }
        catch (IOException e)
        {
            Debug.LogError($"Errore nel salvataggio file: {e.Message}");
        }
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
        tempNavMeshStats?.Clear();
        tempAStarStats?.Clear();

        navMeshPool?.Clear();
        aStarPool?.Clear();
    }
}