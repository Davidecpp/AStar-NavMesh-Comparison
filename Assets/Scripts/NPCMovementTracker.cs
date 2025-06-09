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

    // Cache for performance
    private Transform cachedTransform;
    private Vector3 lastRegisteredPosition;
    private float nextTrackingTime;
    private bool isInitialized = false;

    void Awake()
    {
        cachedTransform = transform;
        lastRegisteredPosition = cachedTransform.position;
        DetermineHeatmapType();
    }

    void Start()
    {
        InitializeHeatmapManager();
        InitializeTracking();
    }

    void OnEnable()
    {
        // Reinitialize if the component is enabled after being disabled
        if (isInitialized)
        {
            InitializeHeatmapManager();
            if (heatmapManager != null)
            {
                RegisterCurrentPosition();
            }
        }
    }

    private void InitializeHeatmapManager()
    {
        if (heatmapManager == null)
        {
            heatmapManager = FindObjectOfType<Heatmap>();
        }

        if (heatmapManager == null)
        {
            // Retry after a short delay if Heatmap manager is not found
            Invoke(nameof(RetryFindHeatmap), 0.1f);
        }
    }

    private void RetryFindHeatmap()
    {
        if (heatmapManager == null)
        {
            heatmapManager = FindObjectOfType<Heatmap>();
        }

        if (heatmapManager != null && !isInitialized)
        {
            InitializeTracking();
        }
    }

    private void InitializeTracking()
    {
        if (heatmapManager != null)
        {
            isInitialized = true;
            enabled = true;
            RegisterCurrentPosition();
            Debug.Log($"Tracking inizializzato per {gameObject.name} con tipo {heatmapType}");
        }
        else
        {
            isInitialized = false;
            enabled = false;
            Debug.LogWarning($"HeatmapManager non trovato per {gameObject.name}! Componente disabilitato.");
        }
    }

    private void DetermineHeatmapType()
    {
        // Uses gameObject name to determine heatmap type
        string objectName = gameObject.name.ToLower();

        if (objectName.Contains("navmesh"))
        {
            heatmapType = HeatmapType.NavMesh;
        }
        else if (objectName.Contains("astar"))
        {
            heatmapType = HeatmapType.AStar;
        }
        // heatmapType remains HeatmapType.All if no specific type is found
    }

    void Update()
    {
        // Early exit optimization
        if (!isInitialized || heatmapManager == null || Time.time < nextTrackingTime) return;

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

    public void SetHeatmapType(HeatmapType newType)
    {
        heatmapType = newType;
    }

    public void ForceRegisterPosition()
    {
        if (!isInitialized || heatmapManager == null) return;

        RegisterCurrentPosition();
        lastRegisteredPosition = cachedTransform.position;
    }

    private void RegisterCurrentPosition()
    {
        if (heatmapManager != null)
        {
            heatmapManager.RegisterPosition(cachedTransform.position, heatmapType);
        }
    }

    public void SetTrackingInterval(float newInterval)
    {
        trackingInterval = newInterval > 0.01f ? newInterval : 0.01f;
    }

    public void SetMovementThreshold(float newThreshold)
    {
        minMovementThreshold = newThreshold > 0.01f ? newThreshold : 0.01f;
    }

    public void ReinitializeTracker()
    {
        isInitialized = false;
        InitializeHeatmapManager();
        InitializeTracking();
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