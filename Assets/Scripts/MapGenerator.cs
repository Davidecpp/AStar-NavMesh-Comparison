using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int width = 10;
    public int depth = 10;

    public GameObject floorPrefab;
    public GameObject obstaclePrefab;
    public GameObject spawnPointPrefab;
    public GameObject destinationPrefab;

    [Range(0f, 1f)]
    public float obstacleChance = 0.2f;
    public float tileSize = 1f;

    [HideInInspector] public Transform currentSpawnPoint;
    [HideInInspector] public Transform currentDestination;


    void Start()
    {
        GenerateMap();
    }

    public void GenerateMap()
    {
        // Cancella mappa precedente
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // Crea il pavimento
        Vector3 floorPosition = new Vector3((width * tileSize) / 2f - tileSize / 2f, 0, (depth * tileSize) / 2f - tileSize / 2f);
        GameObject floor = Instantiate(floorPrefab, floorPosition, Quaternion.identity, transform);
        floor.transform.localScale = new Vector3(width * tileSize, 1, depth * tileSize); // adatta se usi un Cube

        Vector3 spawnPos = new Vector3(0, spawnPointPrefab.transform.localScale.y / 2f, 0);
        Vector3 destPos = new Vector3((width - 1) * tileSize, destinationPrefab.transform.localScale.y / 2f, (depth - 1) * tileSize);

        // Instanzia gli oggetti
        GameObject spawn = Instantiate(spawnPointPrefab, spawnPos, Quaternion.identity, transform);
        GameObject dest = Instantiate(destinationPrefab, destPos, Quaternion.identity, transform);

        // Salva i riferimenti per lo spawner
        currentSpawnPoint = spawn.transform;
        currentDestination = dest.transform;


        // Genera ostacoli
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                // Evita di generare ostacoli su spawn e destination
                if ((x == 0 && z == 0) || (x == width - 1 && z == depth - 1))
                    continue;

                if (Random.value < obstacleChance)
                {
                    float h = obstaclePrefab.transform.localScale.y;
                    Vector3 obstaclePos = new Vector3(x * tileSize, h / 2f, z * tileSize);
                    Quaternion rot = Quaternion.Euler(0, Random.Range(0, 4) * 90, 0);
                    Instantiate(obstaclePrefab, obstaclePos, rot, transform);
                }
            }
        }

    }

}
