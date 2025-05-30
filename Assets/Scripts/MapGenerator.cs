using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public int width = 10;
    public int depth = 10;

    public GameObject floorPrefab;
    public GameObject obstaclePrefab;

    [Range(0f, 1f)]
    public float obstacleChance = 0.2f;

    public float tileSize = 1f;

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

        // Crea un solo pavimento grande
        Vector3 floorPosition = new Vector3((width * tileSize) / 2f - tileSize / 2f, 0, (depth * tileSize) / 2f - tileSize / 2f);
        GameObject floor = Instantiate(floorPrefab, floorPosition, Quaternion.identity, transform);
        floor.transform.localScale = new Vector3(width * tileSize, 1, depth * tileSize); // 10 = scala standard Plane

        // Genera ostacoli
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
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
