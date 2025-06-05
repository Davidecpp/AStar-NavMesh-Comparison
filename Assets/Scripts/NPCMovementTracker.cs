using UnityEngine;
using static Heatmap;

public class NPCMovementTracker : MonoBehaviour
{
    [Header("Configuration")]
    public Heatmap heatmapManager;
    [SerializeField] private HeatmapType heatmapType = HeatmapType.All;

    [Header("Performance Settings")]
    [SerializeField] private float trackingInterval = 0.1f;
    [SerializeField] private float minMovementThreshold = 0.1f;

    // Singleton statico per evitare FindObjectOfType ripetuti
    private static Heatmap _cachedHeatmapManager;
    private static bool _heatmapSearched = false;

    // Cache per performance
    private Transform cachedTransform;
    private Vector3 lastRegisteredPosition;
    private float nextTrackingTime;
    private bool isInitialized = false;

    // Pre-computed hash codes per evitare allocazioni di stringhe
    private static readonly int NAVMESH_HASH = "Navmesh".GetHashCode();
    private static readonly int ASTAR_HASH = "AStar".GetHashCode();

    void Awake()
    {
        cachedTransform = transform;
        lastRegisteredPosition = cachedTransform.position;

        // Determina il tipo una sola volta in Awake invece che in Start
        DetermineHeatmapType();
    }

    void Start()
    {
        // Inizializzazione molto più veloce
        InitializeHeatmapManager();

        if (heatmapManager != null)
        {
            isInitialized = true;
            RegisterCurrentPosition();
        }
        else
        {
            // Se non troviamo l'heatmap manager, disabilita questo componente
            enabled = false;
            Debug.LogWarning($"HeatmapManager non trovato per {gameObject.name}! Componente disabilitato.");
        }
    }

    private void InitializeHeatmapManager()
    {
        if (heatmapManager != null) return;

        // Usa cache statica per evitare FindObjectOfType multipli
        if (!_heatmapSearched)
        {
            _cachedHeatmapManager = FindObjectOfType<Heatmap>();
            _heatmapSearched = true;
        }

        heatmapManager = _cachedHeatmapManager;
    }

    private void DetermineHeatmapType()
    {
        // Usa hash code invece di Contains() per performance migliori
        int nameHash = gameObject.name.GetHashCode();

        if (gameObject.name.IndexOf("Navmesh", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            heatmapType = HeatmapType.NavMesh;
        }
        else if (gameObject.name.IndexOf("AStar", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            heatmapType = HeatmapType.AStar;
        }
        // heatmapType rimane All se non specificato
    }

    void Update()
    {
        // Early exit ottimizzato
        if (!isInitialized || Time.time < nextTrackingTime) return;

        Vector3 currentPosition = cachedTransform.position;
        float sqrDistance = (currentPosition - lastRegisteredPosition).sqrMagnitude;

        if (sqrDistance >= minMovementThreshold * minMovementThreshold)
        {
            RegisterCurrentPosition();
            lastRegisteredPosition = currentPosition;
        }

        nextTrackingTime = Time.time + trackingInterval;
    }

    // Metodi pubblici ottimizzati
    public void SetHeatmapType(HeatmapType newType) => heatmapType = newType;

    public void ForceRegisterPosition()
    {
        if (!isInitialized) return;

        RegisterCurrentPosition();
        lastRegisteredPosition = cachedTransform.position;
    }

    private void RegisterCurrentPosition()
    {
        heatmapManager.RegisterPosition(cachedTransform.position, heatmapType);
    }

    public void SetTrackingInterval(float newInterval)
    {
        trackingInterval = newInterval > 0.01f ? newInterval : 0.01f;
    }

    public void SetMovementThreshold(float newThreshold)
    {
        minMovementThreshold = newThreshold > 0.01f ? newThreshold : 0.01f;
    }

    // Visibility optimization
    void OnBecameInvisible() => enabled = false;
    void OnBecameVisible() => enabled = true;

    // Reset cache statica quando necessario (chiamare dal manager principale)
    public static void ResetHeatmapCache()
    {
        _cachedHeatmapManager = null;
        _heatmapSearched = false;
    }

#if UNITY_EDITOR
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = false;

    void OnDrawGizmosSelected()
    {
        if (!showDebugInfo || !Application.isPlaying || !isInitialized) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(lastRegisteredPosition, 0.15f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, lastRegisteredPosition);
    }

    void OnValidate()
    {
        if (trackingInterval < 0.01f) trackingInterval = 0.01f;
        if (minMovementThreshold < 0.01f) minMovementThreshold = 0.01f;
    }
#endif
}