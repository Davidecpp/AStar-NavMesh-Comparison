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

    // Cache static heatmap manager to avoid multiple searches
    private static Heatmap _cachedHeatmapManager;
    private static bool _heatmapSearched = false;

    // Cache for performance
    private Transform cachedTransform;
    private Vector3 lastRegisteredPosition;
    private float nextTrackingTime;
    private bool isInitialized = false;

    // Pre-computed hash codes for heatmap types
    private static readonly int NAVMESH_HASH = "Navmesh".GetHashCode();
    private static readonly int ASTAR_HASH = "AStar".GetHashCode();

    void Awake()
    {
        cachedTransform = transform;
        lastRegisteredPosition = cachedTransform.position;

        DetermineHeatmapType();
    }

    void Start()
    {
        InitializeHeatmapManager();

        if (heatmapManager != null)
        {
            isInitialized = true;
            RegisterCurrentPosition();
        }
        else
        {
            enabled = false;
            Debug.LogWarning($"HeatmapManager non trovato per {gameObject.name}! Componente disabilitato.");
        }
    }

    private void InitializeHeatmapManager()
    {
        if (heatmapManager != null) return;

        // Uses cached heatmap manager if available
        if (!_heatmapSearched)
        {
            _cachedHeatmapManager = FindObjectOfType<Heatmap>();
            _heatmapSearched = true;
        }

        heatmapManager = _cachedHeatmapManager;
    }

    private void DetermineHeatmapType()
    {
        // Uses gameObject name to determine heatmap type
        int nameHash = gameObject.name.GetHashCode();

        if (gameObject.name.IndexOf("Navmesh", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            heatmapType = HeatmapType.NavMesh;
        }
        else if (gameObject.name.IndexOf("AStar", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            heatmapType = HeatmapType.AStar;
        }
        // heatmapType remains HeatmapType.All if no specific type is found
    }

    void Update()
    {
        // Early exit optimization
        if (!isInitialized || Time.time < nextTrackingTime) return;

        // Check if the position has changed significantly
        Vector3 currentPosition = cachedTransform.position;
        float sqrDistance = (currentPosition - lastRegisteredPosition).sqrMagnitude;

        // Only register position if it exceeds the movement threshold
        if (sqrDistance >= minMovementThreshold * minMovementThreshold)
        {
            RegisterCurrentPosition();
            lastRegisteredPosition = currentPosition;
        }
        nextTrackingTime = Time.time + trackingInterval;
    }

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

    // Reset cache and search state
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