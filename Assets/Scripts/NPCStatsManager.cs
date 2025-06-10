using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using System.IO;
using System.Text;

public class NPCStatsManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text panelStatsTxt;
    public GameObject panelStats;

    [Header("Graphs")]
    public PathDataGraph graficoNavMesh;
    public PathDataGraph graficoAStar;

    [Header("References")]
    public NPCSpawner npcSpawner;

    [Header("Performance Settings")]
    [SerializeField] private float updateInterval = 0.5f;
    [SerializeField] private int maxDetailedNPCs = 20;

    private StringBuilder stringBuilder = new StringBuilder(2048);
    private float lastUpdateTime;

    private struct NPCCalcTimeStats
    {
        public double totalCalcTime;
        public int calcCount;
        public double lastCalcTime;

        public void AddCalcTime(double calcTime)
        {
            totalCalcTime += calcTime;
            calcCount++;
            lastCalcTime = calcTime;
        }

        public double GetAverageCalcTime() => calcCount > 0 ? totalCalcTime / calcCount : 0;
    }

    private Dictionary<NavMeshNPCController, NPCCalcTimeStats> navMeshCalcStats = new();
    private Dictionary<AStarNPCController, NPCCalcTimeStats> aStarCalcStats = new();

    private struct SimpleStats
    {
        public float totalDistance;
        public double totalPathTime;
        public double totalCalcTime;
        public int count;

        public float AvgDistance => count > 0 ? totalDistance / count : 0;
        public double AvgPathTime => count > 0 ? totalPathTime / count : 0;
        public double AvgCalcTime => count > 0 ? totalCalcTime / count : 0;

        public void Add(float dist, double pathTime, double calcTime)
        {
            totalDistance += dist;
            totalPathTime += pathTime;
            totalCalcTime += calcTime;
            count++;
        }

        public void Clear()
        {
            totalDistance = 0;
            totalPathTime = 0;
            totalCalcTime = 0;
            count = 0;
        }
    }

    private SimpleStats navMeshStats;
    private SimpleStats aStarStats;

    public static bool panelStatsOpen = false;

    void Start()
    {
        panelStats.SetActive(false);
        panelStatsTxt.gameObject.SetActive(false);
        lastUpdateTime = Time.time;
    }

    void Update()
    {
        HandleInput();

        if (panelStatsOpen && npcSpawner != null && Time.time - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateStatsSimple();
        }
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.tabKey.wasPressedThisFrame)
        {
            panelStatsOpen = !panelStatsOpen;
            panelStats.SetActive(panelStatsOpen);
            panelStatsTxt.gameObject.SetActive(panelStatsOpen);
            graficoNavMesh?.gameObject.SetActive(panelStatsOpen);
            graficoAStar?.gameObject.SetActive(panelStatsOpen);

            if (panelStatsOpen)
                UpdateStatsSimple();
        }

        if (keyboard.sKey.wasPressedThisFrame)
            SaveStatsToFile(panelStatsTxt.text);
    }

    private void UpdateStatsSimple()
    {
        // Azzera le statistiche correnti per ricalcolarle
        navMeshStats.Clear();
        aStarStats.Clear();
        stringBuilder.Clear();

        ProcessNPCList(navMeshCalcStats, npcSpawner.NavMeshControllers, isNavMesh: true);
        ProcessNPCList(aStarCalcStats, npcSpawner.AStarControllers, isNavMesh: false);

        panelStatsTxt.text = stringBuilder.ToString();

        if (graficoNavMesh != null && navMeshStats.count > 0)
            graficoNavMesh.AddDataPoint((float)navMeshStats.AvgCalcTime, (float)navMeshStats.AvgPathTime, navMeshStats.AvgDistance);

        if (graficoAStar != null && aStarStats.count > 0)
            graficoAStar.AddDataPoint((float)aStarStats.AvgCalcTime, (float)aStarStats.AvgPathTime, aStarStats.AvgDistance);
    }

    private void ProcessNPCList<T>(Dictionary<T, NPCCalcTimeStats> statsDict, List<T> npcList, bool isNavMesh) where T : Component
    {
        if (npcList == null || npcList.Count == 0) return;

        string title = isNavMesh ? "== NPC NavMesh ==" : "== NPC A* ==";
        stringBuilder.AppendLine($"<b>{title}</b>");
        stringBuilder.AppendLine($"<b>Totale NPCs:</b> {npcList.Count}");

        int validNPCs = 0;

        for (int i = 0; i < npcList.Count; i++)
        {
            var npc = npcList[i];
            if (npc == null) continue;

            float dist = isNavMesh ? ((NavMeshNPCController)(object)npc).GetDistance() : ((AStarNPCController)(object)npc).GetDistance();
            double pathTime = isNavMesh ? ((NavMeshNPCController)(object)npc).GetPathTime() : ((AStarNPCController)(object)npc).GetPathTime();
            double calcTime = isNavMesh ? ((NavMeshNPCController)(object)npc).GetCalcTime() : ((AStarNPCController)(object)npc).GetCalcTime();

            // Aggiorna le statistiche di calcolo solo se c'è un nuovo valore
            if (!statsDict.TryGetValue(npc, out var stat))
                stat = new NPCCalcTimeStats();

            if (calcTime > 0 && calcTime != stat.lastCalcTime)
            {
                stat.AddCalcTime(calcTime);
                statsDict[npc] = stat;
            }

            double avgCalc = stat.GetAverageCalcTime();

            // Accumula le statistiche per la media generale
            if (dist > 0 || pathTime > 0 || avgCalc > 0)
            {
                validNPCs++;
                if (isNavMesh)
                    navMeshStats.Add(dist, pathTime, avgCalc);
                else
                    aStarStats.Add(dist, pathTime, avgCalc);
            }

            // Mostra i dettagli solo per i primi N NPCs
            if (i < maxDetailedNPCs)
                stringBuilder.AppendLine($"[{i + 1}] {npc.name} - Dist: {dist:F1}m, Path: {pathTime:F2}s, Calc: {avgCalc:F1}ms");
        }

        // Mostra le statistiche medie calcolate
        SimpleStats currentStats = isNavMesh ? navMeshStats : aStarStats;

        if (currentStats.count > 0)
        {
            stringBuilder.AppendLine($"<b>NPCs Validi:</b> {validNPCs}");
            stringBuilder.AppendLine($"<b>Media Distanza:</b> {currentStats.AvgDistance:F2} m");
            stringBuilder.AppendLine($"<b>Media PathTime:</b> {currentStats.AvgPathTime:F2} s");
            stringBuilder.AppendLine($"<b>Media CalcTime:</b> {currentStats.AvgCalcTime:F2} ms");
        }
        else
        {
            stringBuilder.AppendLine("<color=yellow><b>Nessun dato valido per le statistiche</b></color>");
        }

        stringBuilder.AppendLine();
    }

    // Metodi per registrare NPCs (chiamati dal spawner)
    public void RegisterNavMeshNPC(NavMeshNPCController npc)
    {
        if (npc != null && !navMeshCalcStats.ContainsKey(npc))
        {
            navMeshCalcStats[npc] = new NPCCalcTimeStats();
        }
    }

    public void RegisterAStarNPC(AStarNPCController npc)
    {
        if (npc != null && !aStarCalcStats.ContainsKey(npc))
        {
            aStarCalcStats[npc] = new NPCCalcTimeStats();
        }
    }

    // Metodi per rimuovere NPCs (utili per cleanup)
    public void UnregisterNavMeshNPC(NavMeshNPCController npc)
    {
        if (npc != null && navMeshCalcStats.ContainsKey(npc))
        {
            navMeshCalcStats.Remove(npc);
        }
    }

    public void UnregisterAStarNPC(AStarNPCController npc)
    {
        if (npc != null && aStarCalcStats.ContainsKey(npc))
        {
            aStarCalcStats.Remove(npc);
        }
    }

    private void SaveStatsToFile(string content)
    {
        string path = Path.Combine(Application.dataPath, "NPC_Stats_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
        File.WriteAllText(path, content);
        Debug.Log($"Stats salvate in: {path}");
    }
}