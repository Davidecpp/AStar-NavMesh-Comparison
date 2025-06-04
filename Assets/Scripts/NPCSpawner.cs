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

    [Header("UI Elements")]
    public TMP_InputField numberInput;
    public TMP_Dropdown npcTypeDropdown;
    public TMP_Text panelStatsTxt;
    public GameObject panelStats;

    [Header("Managers")]
    public NPCPhysicsToggleManager physicsToggleManager;
    public PathDataGraph graficoNavMesh;
    public PathDataGraph graficoAStar;

    // Cache per performance
    private List<GameObject> npcList = new List<GameObject>();
    private List<NavMeshNPCController> navMeshControllers = new List<NavMeshNPCController>();
    private List<AStarNPCController> aStarControllers = new List<AStarNPCController>();

    private Keyboard keyboard;
    private StringBuilder stringBuilder = new StringBuilder(1024); // Pre-allocato per evitare allocazioni

    // Cache per statistiche (evita allocazioni ripetute)
    private readonly List<string> tempNavMeshStats = new List<string>();
    private readonly List<string> tempAStarStats = new List<string>();

    // Variabili per statistiche (riutilizzate)
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
    }

    public void SpawnNPCs()
    {
        if (!ValidateInput(out int count)) return;

        int npcType = npcTypeDropdown.value;

        // Pre-alloca spazio nelle liste se necessario
        int expectedNewNPCs = CalculateExpectedNPCs(count, npcType);
        EnsureListCapacity(expectedNewNPCs);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = CalculateSpawnPosition();

            if (ShouldSpawnNavMesh(npcType))
                SpawnNavMeshNPC(spawnPos);

            if (ShouldSpawnAStar(npcType))
                SpawnAStarNPC(spawnPos);
        }
    }

    // Validazione dell'input per evitare allocazioni dinamiche
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

    // Assicura che le liste abbiano abbastanza capacità per evitare allocazioni dinamiche
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
        Vector3 spawnPos = spawnPoint.position + Random.insideUnitSphere * 2f;
        spawnPos.y = spawnPoint.position.y;
        return spawnPos;
    }

    private bool ShouldSpawnNavMesh(int npcType) => npcType == 0 || npcType == 2;
    private bool ShouldSpawnAStar(int npcType) => npcType == 1 || npcType == 2;

    private void SpawnNavMeshNPC(Vector3 spawnPos)
    {
        GameObject navNpc = Instantiate(npcNavMeshPrefab, spawnPos, Quaternion.identity);
        npcList.Add(navNpc);
        physicsToggleManager.RegisterNPC(navNpc);

        var navController = navNpc.GetComponent<NavMeshNPCController>();
        if (navController != null)
        {
            navController.target = npcTarget;
            navMeshControllers.Add(navController);
        }
    }

    private void SpawnAStarNPC(Vector3 spawnPos)
    {
        Vector3 aStarSpawnPos = spawnPos + Vector3.right * 1f;
        GameObject aStarNpc = Instantiate(npcAStarPrefab, aStarSpawnPos, Quaternion.identity);
        npcList.Add(aStarNpc);
        physicsToggleManager.RegisterNPC(aStarNpc);

        var aStarController = aStarNpc.GetComponent<AStarNPCController>();
        if (aStarController != null)
        {
            aStarController.target = npcTarget;
            aStarControllers.Add(aStarController);
        }
    }

    void Update()
    {
        HandleInput();
        UpdateStats();
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
        if (!panelStatsOpen) return; // Aggiorna solo se il pannello è visibile

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
        // Rimuovi controller null (NPCs distrutti)
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
        // Rimuovi controller null (NPCs distrutti)
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

    // Aggiorna i grafici con le statistiche calcolate
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

    // Appendi le statistiche NavMesh al StringBuilder
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

    // Appendi le statistiche A* al StringBuilder
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

    // Cleanup quando l'oggetto viene distrutto
    private void OnDestroy()
    {
        npcList?.Clear();
        navMeshControllers?.Clear();
        aStarControllers?.Clear();
        tempNavMeshStats?.Clear();
        tempAStarStats?.Clear();
    }
}