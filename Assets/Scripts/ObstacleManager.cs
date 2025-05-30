using UnityEngine;
using System.Collections.Generic;
using Unity.AI.Navigation;
using System.Collections;

public class ObstacleManager : MonoBehaviour
{
    public GameObject obstaclePrefab;
    public int obstacleCount = 20;
    public float mapWidth = 20f;
    public float mapDepth = 20f;
    public float minDistanceBetweenObstacles = 1.5f;
    public float minDistanceFromSpawnPoints = 2f; // Distanza minima da SpawnPoint e Destination
    public string obstacleLayerName = "Obstacle";
    public string groundLayerName = "Ground";
    public float raycastHeight = 10f;
    public float obstacleRadius = 1f; // Raggio/dimensione approssimativa dell'ostacolo
    public bool autoCalculateObstacleSize = true; // Calcola automaticamente la dimensione

    private List<Vector3> placedPositions = new List<Vector3>();
    private int obstacleLayer;
    private LayerMask groundLayer;
    public NavMeshSurface navMeshSurface;

    void Start()
    {
        obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
        if (obstacleLayer == -1)
        {
            Debug.LogError($"Layer '{obstacleLayerName}' non trovato!");
            return;
        }

        groundLayer = LayerMask.GetMask(groundLayerName);
        if (groundLayer == 0)
        {
            Debug.LogError($"Layer '{groundLayerName}' non trovato o non usato!");
            return;
        }

        // Calcola automaticamente la dimensione dell'ostacolo se richiesto
        if (autoCalculateObstacleSize && obstaclePrefab != null)
        {
            CalculateObstacleSize();
        }

        ReplaceObstacles();
    }

    public void ReplaceObstacles()
    {
        RemoveExistingObstacles();
        placedPositions.Clear();

        int placed = 0;
        int attempts = 0;

        while (placed < obstacleCount && attempts < obstacleCount * 10)
        {
            Vector3 randomXZ = new Vector3(
                Random.Range(-mapWidth / 2f, mapWidth / 2f),
                raycastHeight,
                Random.Range(-mapDepth / 2f, mapDepth / 2f)
            );

            if (Physics.Raycast(randomXZ, Vector3.down, out RaycastHit hit, raycastHeight * 2, groundLayer))
            {
                Vector3 spawnPos = hit.point;

                if (IsFarEnoughFromOthers(spawnPos) && !IsNearSpawnPointOrDestination(spawnPos))
                {
                    Quaternion rotation = Quaternion.Euler(0f, Random.value < 0.5f ? 0f : 90f, 0f);
                    GameObject newObstacle = Instantiate(obstaclePrefab, spawnPos, rotation);
                    newObstacle.layer = obstacleLayer;
                    placedPositions.Add(spawnPos);
                    placed++;
                }
            }

            attempts++;
        }

        Debug.Log($"Piazzati {placed} ostacoli su {obstacleCount} richiesti.");
        StartCoroutine(DelayedUpdate());
    }

    IEnumerator DelayedUpdate()
    {
        yield return new WaitForSeconds(0.2f);
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
        AstarPath.active.Scan();
    }

    private bool IsFarEnoughFromOthers(Vector3 pos)
    {
        foreach (var existing in placedPositions)
        {
            if (Vector3.Distance(pos, existing) < minDistanceBetweenObstacles)
                return false;
        }
        return true;
    }

    private bool IsNearSpawnPointOrDestination(Vector3 position)
    {
        // Usa il raggio dell'ostacolo per il controllo di sovrapposizione
        float effectiveRadius = obstacleRadius;

        // Controlla se ci sono collider nell'area che sarà occupata dall'ostacolo
        Collider[] overlapping = Physics.OverlapSphere(position, effectiveRadius);

        foreach (var collider in overlapping)
        {
            // Controlla se l'oggetto ha tag SpawnPoint o Destination
            if (collider.CompareTag("SpawnPoint") || collider.CompareTag("Destination"))
            {
                Debug.Log($"Ostacolo bloccato: sovrapposizione diretta con {collider.name} ({collider.tag})");
                return true;
            }
        }

        // Controllo aggiuntivo per distanza minima (centro-centro + raggio ostacolo + raggio sicurezza)
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        GameObject[] destinations = GameObject.FindGameObjectsWithTag("Destination");

        foreach (var spawnPoint in spawnPoints)
        {
            float requiredDistance = minDistanceFromSpawnPoints + effectiveRadius;

            // Aggiungi anche il raggio dell'oggetto SpawnPoint/Destination se ha un collider
            Collider spawnCollider = spawnPoint.GetComponent<Collider>();
            if (spawnCollider != null)
            {
                requiredDistance += GetColliderRadius(spawnCollider);
            }

            if (Vector3.Distance(position, spawnPoint.transform.position) < requiredDistance)
            {
                Debug.Log($"Ostacolo bloccato: troppo vicino a SpawnPoint {spawnPoint.name}");
                return true;
            }
        }

        foreach (var destination in destinations)
        {
            float requiredDistance = minDistanceFromSpawnPoints + effectiveRadius;

            // Aggiungi anche il raggio dell'oggetto Destination se ha un collider
            Collider destCollider = destination.GetComponent<Collider>();
            if (destCollider != null)
            {
                requiredDistance += GetColliderRadius(destCollider);
            }

            if (Vector3.Distance(position, destination.transform.position) < requiredDistance)
            {
                Debug.Log($"Ostacolo bloccato: troppo vicino a Destination {destination.name}");
                return true;
            }
        }

        return false;
    }

    private float GetColliderRadius(Collider collider)
    {
        if (collider is SphereCollider sphere)
        {
            return sphere.radius * Mathf.Max(collider.transform.localScale.x, collider.transform.localScale.z);
        }
        else if (collider is CapsuleCollider capsule)
        {
            return capsule.radius * Mathf.Max(collider.transform.localScale.x, collider.transform.localScale.z);
        }
        else if (collider is BoxCollider box)
        {
            Vector3 size = box.size;
            size.Scale(collider.transform.localScale);
            return Mathf.Max(size.x, size.z) * 0.5f;
        }
        else
        {
            // Per altri tipi di collider, usa i bounds
            return Mathf.Max(collider.bounds.size.x, collider.bounds.size.z) * 0.5f;
        }
    }

    private void CalculateObstacleSize()
    {
        if (obstaclePrefab == null) return;

        // Prova a ottenere il collider dal prefab
        Collider prefabCollider = obstaclePrefab.GetComponent<Collider>();
        if (prefabCollider != null)
        {
            obstacleRadius = GetColliderRadius(prefabCollider);
            Debug.Log($"Dimensione ostacolo calcolata automaticamente: {obstacleRadius}");
        }
        else
        {
            // Se non c'è collider, prova con il renderer
            Renderer prefabRenderer = obstaclePrefab.GetComponent<Renderer>();
            if (prefabRenderer != null)
            {
                Vector3 size = prefabRenderer.bounds.size;
                obstacleRadius = Mathf.Max(size.x, size.z) * 0.5f;
                Debug.Log($"Dimensione ostacolo calcolata da renderer: {obstacleRadius}");
            }
            else
            {
                Debug.LogWarning("Impossibile calcolare la dimensione dell'ostacolo automaticamente. Usa il valore manuale.");
            }
        }
    }

    private void RemoveExistingObstacles()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        int removed = 0;

        foreach (var obj in allObjects)
        {
            if (obj.layer == obstacleLayer)
            {
                Destroy(obj);
                removed++;
            }
        }

        Debug.Log($"Rimossi {removed} ostacoli dal layer '{obstacleLayerName}'.");
    }
}