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
    [SerializeField] private int spawnBatchSize = 10; // NPCs per frame
    [SerializeField] private float batchDelay = 0.016f; // ~60fps delay between batches
    [SerializeField] private bool useObjectPooling = true;

    [Header("UI Elements")]
    public TMP_InputField numberInput;
    public TMP_Dropdown npcTypeDropdown;
    public TMP_Text panelStatsTxt;
    public GameObject panelStats;
    public TMP_Text spawnProgressText; // Add this UI element

    [Header("Managers")]
    public NPCPhysicsToggleManager physicsToggleManager;
    public PathDataGraph graficoNavMesh;
    public PathDataGraph graficoAStar;

    // Object Pooling
    private Queue<GameObject> navMeshPool = new Queue<GameObject>();
    private Queue<GameObject> aStarPool = new Queue<GameObject>();
    private const int INITIAL_POOL_SIZE = 50;

    // Cache per performance
    private List<GameObject> npcList = new List<GameObject>();
    private List<NavMeshNPCController> navMeshControllers = new List<NavMeshNPCController>();
    private List<AStarNPCController> aStarControllers = new List<AStarNPCController>();

    private Keyboard keyboard;
    private StringBuilder stringBuilder = new StringBuilder(1024);

    // Cache per statistiche
    private readonly List<string> tempNavMeshStats = new List<string>();
    private readonly List<string> tempAStarStats = new List<string>();

    // Spawn progress tracking
    private bool isSpawning = false;
    private Coroutine currentSpawnCoroutine;

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

        public void Add(float distance, double pathTime, double calcTime)
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

    // Costanti per evitare allocazioni
    private const string NAVMESH_HEADER = "<b>== NPC NavMesh ==</b>\n";
    private const string ASTAR_HEADER = "<b>== NPC A* ==</b>\n";
    private const string MEDIA_DISTANZA = "\n<b>Media distanza:</b> ";
    private const string MEDIA_PATHTIME = "\n<b>Media PathTime:</b> ";
    private const string MEDIA_CALCTIME = "\n<b>Media CalcTime:</b> ";

    void Start()
    {
        InitializeComponents();
        SetupDropdown();
        SetupUI();
        
        if (useObjectPooling)
        {
            InitializeObjectPools();
        }
    }

    private void InitializeComponents()
    {
        keyboard = Keyboard.current;
    }

    private void SetupDropdown()
    {
        npcTypeDropdown.options.Clear();
        npcTypeDropdown.options.Add(new TMP_Dropdown.OptionData("NavMesh"));
        npcTypeDropdown.options.Add(new TMP_Dropdown.OptionData("A*"));
        npcTypeDropdown.options.Add(new TMP_Dropdown.OptionData("Tutti"));
        npcTypeDropdown.value = 0;
    }

    private void SetupUI()
    {
        panelStats.SetActive(false);
        panelStatsTxt.gameObject.SetActive(false);
        
        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(false);
    }

    private void InitializeObjectPools()
    {
        // Pre-populate pools
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            GameObject navMeshObj = Instantiate(npcNavMeshPrefab);
            navMeshObj.SetActive(false);
            navMeshPool.Enqueue(navMeshObj);

            GameObject aStarObj = Instantiate(npcAStarPrefab);
            aStarObj.SetActive(false);
            aStarPool.Enqueue(aStarObj);
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

        currentSpawnCoroutine = StartCoroutine(SpawnNPCsCoroutine(count, npcTypeDropdown.value));
    }

    private IEnumerator SpawnNPCsCoroutine(int count, int npcType)
    {
        isSpawning = true;
        
        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(true);

        int totalToSpawn = CalculateExpectedNPCs(count, npcType);
        int spawned = 0;

        // Pre-alloca spazio nelle liste
        EnsureListCapacity(totalToSpawn);

        // Temporarily disable physics auto-sync for better performance
        bool originalAutoSync = Physics.autoSyncTransforms;
        Physics.autoSyncTransforms = false;

        for (int i = 0; i < count; i += spawnBatchSize)
        {
            int batchEnd = Mathf.Min(i + spawnBatchSize, count);
            
            for (int j = i; j < batchEnd; j++)
            {
                Vector3 spawnPos = CalculateSpawnPosition();

                if (ShouldSpawnNavMesh(npcType))
                {
                    SpawnNavMeshNPC(spawnPos);
                    spawned++;
                }

                if (ShouldSpawnAStar(npcType))
                {
                    SpawnAStarNPC(spawnPos);
                    spawned++;
                }
            }

            // Update progress
            if (spawnProgressText != null)
            {
                float progress = (float)spawned / totalToSpawn * 100f;
                spawnProgressText.text = $"Spawning NPCs: {progress:F1}% ({spawned}/{totalToSpawn})";
            }

            // Wait before next batch
            yield return new WaitForSeconds(batchDelay);
        }

        // Re-enable physics auto-sync
        Physics.autoSyncTransforms = originalAutoSync;
        Physics.SyncTransforms(); // Manual sync after batch spawning

        // Hide progress text
        if (spawnProgressText != null)
            spawnProgressText.gameObject.SetActive(false);

        isSpawning = false;
        currentSpawnCoroutine = null;
        
        Debug.Log($"Spawn completato: {spawned} NPCs creati in {count * batchDelay:F2} secondi");
    }

    private bool ValidateInput(out int count)
    {
        if (!int.TryParse(numberInput.text, out count) || count <= 0)
        {
            Debug.LogWarning("Numero NPC non valido.");
            return false;
        }
        return true;
    }

    private int CalculateExpectedNPCs(int count, int npcType)
    {
        return npcType == 2 ? count * 2 : count;
    }

    private void EnsureListCapacity(int additionalCapacity)
    {
        int newCapacity = npcList.Count + additionalCapacity;
        if (npcList.Capacity < newCapacity)
        {
            npcList.Capacity = newCapacity;
            navMeshControllers.Capacity = Mathf.Max(navMeshControllers.Capacity, newCapacity);
            aStarControllers.Capacity = Mathf.Max(aStarControllers.Capacity, newCapacity);
        }
    }

    private Vector3 CalculateSpawnPosition()
    {
        // Use a more efficient random position calculation
        float angle = Random.Range(0f, 2f * Mathf.PI);
        float distance = Random.Range(0.5f, 2f);
        
        Vector3 spawnPos = spawnPoint.position;
        spawnPos.x += Mathf.Cos(angle) * distance;
        spawnPos.z += Mathf.Sin(angle) * distance;
        
        return spawnPos;
    }

    private bool ShouldSpawnNavMesh(int npcType) => npcType == 0 || npcType == 2;
    private bool ShouldSpawnAStar(int npcType) => npcType == 1 || npcType == 2;

    private void SpawnNavMeshNPC(Vector3 spawnPos)
    {
        GameObject navNpc = GetOrCreateNavMeshNPC();
        
        navNpc.transform.position = spawnPos;
        navNpc.transform.rotation = Quaternion.identity;
        navNpc.SetActive(true);
        
        npcList.Add(navNpc);
        physicsToggleManager.RegisterNPC(navNpc);

        var navController = navNpc.GetComponent<NavMeshNPCController>();
        if (navController != null)
        {
            navController.target = npcTarget;
            navMeshControllers.Add(navController);
            
            // Reset controller state if needed
            navController.ResetNPC(); // Implement this method in your controller
        }
    }

    private void SpawnAStarNPC(Vector3 spawnPos)
    {
        Vector3 aStarSpawnPos = spawnPos + Vector3.right * 1f;
        GameObject aStarNpc = GetOrCreateAStarNPC();
        
        aStarNpc.transform.position = aStarSpawnPos;
        aStarNpc.transform.rotation = Quaternion.identity;
        aStarNpc.SetActive(true);
        
        npcList.Add(aStarNpc);
        physicsToggleManager.RegisterNPC(aStarNpc);

        var aStarController = aStarNpc.GetComponent<AStarNPCController>();
        if (aStarController != null)
        {
            aStarController.target = npcTarget;
            aStarControllers.Add(aStarController);
            
            // Reset controller state if needed
            aStarController.ResetNPC(); // Implement this method in your controller
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

    // Method to return NPCs to pool when destroyed
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
        
        // Only update stats if not spawning (to reduce overhead during spawn)
        if (!isSpawning)
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

        // Add key to cancel spawning
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
        if (!panelStatsOpen) return;

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
            if (navMeshControllers[i] == null)
            {
                navMeshControllers.RemoveAt(i);
                continue;
            }

            var controller = navMeshControllers[i];
            float dist = controller.GetDistance();
            double pathTime = controller.GetPathTime();
            double calcTime = controller.GetCalcTime();

            navMeshStats.Add(dist, pathTime, calcTime);
            tempNavMeshStats.Add($"[NavMesh NPC] {controller.name} - Distanza: {dist:F2} m, PathTime: {pathTime:F2} s, CalcTime: {calcTime:F2} ms");
        }
    }

    private void ProcessAStarNPCs()
    {
        for (int i = aStarControllers.Count - 1; i >= 0; i--)
        {
            if (aStarControllers[i] == null)
            {
                aStarControllers.RemoveAt(i);
                continue;
            }

            var controller = aStarControllers[i];
            float dist = controller.GetDistance();
            float pathTime = controller.GetPathTime();
            double calcTime = controller.GetCalcTime();

            aStarStats.Add(dist, pathTime, calcTime);
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

        foreach (string stat in tempNavMeshStats)
        {
            stringBuilder.AppendLine(stat);
        }

        float avgDistance = navMeshStats.distanceTotal / navMeshStats.count;
        double avgPathTime = navMeshStats.pathTimeTotal / navMeshStats.count;
        double avgCalcTime = navMeshStats.calcTimeTotal / navMeshStats.count;

        stringBuilder.Append(MEDIA_DISTANZA).Append(avgDistance.ToString("F2")).Append(" m");
        stringBuilder.Append(MEDIA_PATHTIME).Append(avgPathTime.ToString("F2")).Append(" s");
        stringBuilder.Append(MEDIA_CALCTIME).Append(avgCalcTime.ToString("F2")).AppendLine(" ms\n");
    }

    private void AppendAStarStats()
    {
        stringBuilder.Append(ASTAR_HEADER);

        foreach (string stat in tempAStarStats)
        {
            stringBuilder.AppendLine(stat);
        }

        float avgDistance = aStarStats.distanceTotal / aStarStats.count;
        float avgPathTime = (float)(aStarStats.pathTimeTotal / aStarStats.count);
        double avgCalcTime = aStarStats.calcTimeTotal / aStarStats.count;

        stringBuilder.Append(MEDIA_DISTANZA).Append(avgDistance.ToString("F2")).Append(" m");
        stringBuilder.Append(MEDIA_PATHTIME).Append(avgPathTime.ToString("F2")).Append(" s");
        stringBuilder.Append(MEDIA_CALCTIME).Append(avgCalcTime.ToString("F2")).AppendLine(" ms\n");
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
        
        // Clear pools
        navMeshPool?.Clear();
        aStarPool?.Clear();
    }
}