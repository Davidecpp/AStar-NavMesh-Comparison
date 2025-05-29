using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using System.IO;

public class NPCSpawner : MonoBehaviour
{
    public GameObject npcNavMeshPrefab;
    public GameObject npcAStarPrefab;
    public Transform spawnPoint;

    public TMP_InputField numberInput;
    public TMP_Dropdown npcTypeDropdown;

    public Transform npcTarget;
    List<GameObject> npcList = new List<GameObject>();

    Keyboard keyboard;
    public TMP_Text panelStatsTxt;
    public GameObject panelStats;

    public NPCPhysicsToggleManager physicsToggleManager;

    public static bool panelStatsOpen = false;

    void Start()
    {
        keyboard = Keyboard.current;

        npcTypeDropdown.options.Clear();
        npcTypeDropdown.options.Add(new TMP_Dropdown.OptionData("NavMesh"));
        npcTypeDropdown.options.Add(new TMP_Dropdown.OptionData("A*"));
        npcTypeDropdown.options.Add(new TMP_Dropdown.OptionData("Tutti"));
        npcTypeDropdown.value = 0;

        panelStats.SetActive(false);
        panelStatsTxt.gameObject.SetActive(false);
    }

    // Spawn NPCs in base al numero e tipo selezionato
    public void SpawnNPCs()
    {
        // Controlla se il campo di input è vuoto o non valido
        if (!int.TryParse(numberInput.text, out int count) || count <= 0)
        {
            Debug.LogWarning("Numero NPC non valido.");
            return;
        }

        int npcType = npcTypeDropdown.value;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = spawnPoint.position + Random.insideUnitSphere * 2f;
            spawnPos.y = spawnPoint.position.y;

            if (npcType == 0 || npcType == 2) // NavMesh o Entrambi
            {
                GameObject navNpc = Instantiate(npcNavMeshPrefab, spawnPos, Quaternion.identity);
                npcList.Add(navNpc);
                physicsToggleManager.RegisterNPC(navNpc);


                var navController = navNpc.GetComponent<NavMeshNPCController>();
                if (navController != null)
                    navController.target = npcTarget;
            }

            if (npcType == 1 || npcType == 2) // A* o Entrambi
            {
                GameObject aStarNpc = Instantiate(npcAStarPrefab, spawnPos + Vector3.right * 1f, Quaternion.identity); // spostato di poco per evitare sovrapposizione
                npcList.Add(aStarNpc);
                physicsToggleManager.RegisterNPC(aStarNpc);

                var aStarController = aStarNpc.GetComponent<AStarNPCController>();
                if (aStarController != null)
                    aStarController.target = npcTarget;
            }
        }
    }

    void Update()
    {
        if (keyboard.tabKey.wasPressedThisFrame)
        {
            CloseStats();
        }
        if(keyboard.sKey.wasPressedThisFrame)
        {
            SaveStatsToFile(panelStatsTxt.text);
        }
        printList();
    }

    // Stampa le informazioni degli NPC
    void printList()
    {
        string fullText = "";

        // Liste di output per NPC
        List<string> navMeshStats = new List<string>();
        List<string> aStarStats = new List<string>();
        List<string> unknownStats = new List<string>();

        // Variabili somma per NavMesh
        float navDistanceTotal = 0f;
        double navPathTimeTotal = 0;
        double navCalcTimeTotal = 0;
        int navCount = 0;

        // Variabili somma per A*
        float aStarDistanceTotal = 0f;
        float aStarPathTimeTotal = 0;
        double aStarCalcTimeTotal = 0;
        int aStarCount = 0;

        foreach (GameObject npc in npcList)
        {
            // Controlla se l'NPC ha un controller NavMesh o A*
            var navController = npc.GetComponent<NavMeshNPCController>();
            if (navController != null)
            {
                float dist = navController.GetDistance();
                double pathTime = navController.GetPathTime();
                double calcTime = navController.GetCalcTime();

                // Aggiungi i dati alla lista NavMesh
                navDistanceTotal += dist;
                navPathTimeTotal += pathTime;
                navCalcTimeTotal += calcTime;
                navCount++;

                navMeshStats.Add($"[NavMesh NPC] {npc.name} - Distanza: {dist:F2} m, PathTime: {pathTime:F2} s, CalcTime: {calcTime:F2} ms");
                continue;
            }

            var aStarController = npc.GetComponent<AStarNPCController>();
            if (aStarController != null)
            {
                float dist = aStarController.GetDistance();
                float pathTime = aStarController.GetPathTime();
                double calcTime = aStarController.GetCalcTime();

                // Aggiungi i dati alla lista A*
                aStarDistanceTotal += dist;
                aStarPathTimeTotal += pathTime;
                aStarCalcTimeTotal += calcTime;
                aStarCount++;

                aStarStats.Add($"[A* NPC] {npc.name} - Distanza: {dist:F2} m, PathTime: {pathTime:F2} s, CalcTime: {calcTime:F2} ms");
                continue;
            }

            unknownStats.Add($"NPC {npc.name} non ha un controller riconosciuto.");
        }

        // NAVMESH
        if (navMeshStats.Count > 0)
        {
            fullText += "<b>== NPC NavMesh ==</b>\n";
            fullText += string.Join("\n", navMeshStats);
            fullText += $"\n<b>Media distanza:</b> {(navDistanceTotal / navCount):F2} m";
            fullText += $"\n<b>Media PathTime:</b> {(navPathTimeTotal / navCount):F2} s";
            fullText += $"\n<b>Media CalcTime:</b> {(navCalcTimeTotal / navCount):F2} ms\n\n";
        }

        // ASTAR
        if (aStarStats.Count > 0)
        {
            fullText += "<b>== NPC A* ==</b>\n";
            fullText += string.Join("\n", aStarStats);
            fullText += $"\n<b>Media distanza:</b> {(aStarDistanceTotal / aStarCount):F2} m";
            fullText += $"\n<b>Media PathTime:</b> {(aStarPathTimeTotal / aStarCount):F2} s";
            fullText += $"\n<b>Media CalcTime:</b> {(aStarCalcTimeTotal / aStarCount):F2} ms\n\n";
        }

        // UNKNOWN
        if (unknownStats.Count > 0)
        {
            fullText += "<b>== NPC Sconosciuti ==</b>\n";
            fullText += string.Join("\n", unknownStats);
        }

        panelStatsTxt.text = fullText;
    }


    // Chiude pannello delle statistiche
    private void CloseStats()
    {
        panelStatsOpen = !panelStatsOpen;
        panelStats.SetActive(panelStatsOpen);
        panelStatsTxt.gameObject.SetActive(panelStatsOpen);
    }

    // Salva le statistiche in un file di testo
    void SaveStatsToFile(string content)
    {
        string folderPath = Application.dataPath + "/NPCStatsLogs";
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string fileName = "NPC_Stats_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt";
        string fullPath = Path.Combine(folderPath, fileName);

        try
        {
            File.WriteAllText(fullPath, content);
            Debug.Log("Statistiche salvate in: " + fullPath);
        }
        catch (IOException e)
        {
            Debug.LogError("Errore nel salvataggio file: " + e.Message);
        }
    }
}
