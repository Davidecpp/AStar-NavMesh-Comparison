using UnityEngine;
using static Heatmap;

public class NPCMovementTracker : MonoBehaviour
{
    [Header("Configuration")]
    public Heatmap heatmapManager;
    [SerializeField] private HeatmapType heatmapType = HeatmapType.All;

    [Header("Performance Settings")]
    [SerializeField] private float trackingInterval = 0.1f; // Registra ogni 0.1 secondi invece di ogni frame
    [SerializeField] private float minMovementThreshold = 0.1f; // Registra solo se si è mosso abbastanza

    // Cache per performance
    private Transform cachedTransform;
    private Vector3 lastRegisteredPosition;
    private float nextTrackingTime;
    private bool isInitialized = false;

    // Costanti per evitare allocazioni di stringhe
    private const string NAVMESH_NAME = "Navmesh";
    private const string ASTAR_NAME = "AStar";

    void Awake()
    {
        // Cache del transform per evitare chiamate ripetute
        cachedTransform = transform;
        lastRegisteredPosition = cachedTransform.position;
    }

    void Start()
    {
        InitializeHeatmapManager();
        DetermineHeatmapType();
        isInitialized = heatmapManager != null;

        if (isInitialized)
        {
            // Registra la posizione iniziale
            RegisterCurrentPosition();
        }
    }

    private void InitializeHeatmapManager()
    {
        if (heatmapManager == null)
        {
            // Usa FindObjectOfType solo se necessario e cachea il risultato
            heatmapManager = FindObjectOfType<Heatmap>();
            if (heatmapManager == null)
            {
                Debug.LogError($"HeatmapManager non trovato per {gameObject.name}!");
            }
        }
    }

    private void DetermineHeatmapType()
    {
        // Determina il tipo una sola volta all'inizializzazione invece che ogni frame
        if (gameObject.name.Contains(NAVMESH_NAME))
        {
            heatmapType = HeatmapType.NavMesh;
        }
        else if (gameObject.name.Contains(ASTAR_NAME))
        {
            heatmapType = HeatmapType.AStar;
        }
        else
        {
            heatmapType = HeatmapType.All;
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        // Controllo temporale per ridurre la frequenza di tracking
        if (Time.time < nextTrackingTime) return;

        // Controlla se l'NPC si è mosso abbastanza per giustificare una registrazione
        Vector3 currentPosition = cachedTransform.position;
        if (Vector3.SqrMagnitude(currentPosition - lastRegisteredPosition) >= minMovementThreshold * minMovementThreshold)
        {
            RegisterCurrentPosition();
            lastRegisteredPosition = currentPosition;
        }

        nextTrackingTime = Time.time + trackingInterval;
    }

    private void RegisterCurrentPosition()
    {
        heatmapManager.RegisterPosition(cachedTransform.position, heatmapType);
    }

    // Metodo pubblico per cambiare il tipo di heatmap a runtime se necessario
    public void SetHeatmapType(HeatmapType newType)
    {
        heatmapType = newType;
    }

    // Metodo per forzare la registrazione immediata (utile per eventi speciali)
    public void ForceRegisterPosition()
    {
        if (isInitialized)
        {
            RegisterCurrentPosition();
            lastRegisteredPosition = cachedTransform.position;
        }
    }

    // Metodo per modificare l'intervallo di tracking a runtime
    public void SetTrackingInterval(float newInterval)
    {
        trackingInterval = Mathf.Max(0.01f, newInterval); // Minimo 0.01 secondi
    }

    // Metodo per modificare la soglia di movimento a runtime
    public void SetMovementThreshold(float newThreshold)
    {
        minMovementThreshold = Mathf.Max(0.01f, newThreshold); // Minimo 0.01 unità
    }

    // Opzionale: Disabilita il tracking quando l'NPC non è visibile
    void OnBecameInvisible()
    {
        enabled = false;
    }

    void OnBecameVisible()
    {
        enabled = true;
    }

    // Debug info nell'Inspector
#if UNITY_EDITOR
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = false;

    void OnDrawGizmosSelected()
    {
        if (!showDebugInfo || !isInitialized) return;

        // Mostra la posizione attuale e l'ultima registrata
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lastRegisteredPosition, 0.15f);

        // Linea tra posizione attuale e ultima registrata
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, lastRegisteredPosition);
    }

    void OnValidate()
    {
        // Assicura che i valori siano validi nell'Inspector
        trackingInterval = Mathf.Max(0.01f, trackingInterval);
        minMovementThreshold = Mathf.Max(0.01f, minMovementThreshold);
    }
#endif
}