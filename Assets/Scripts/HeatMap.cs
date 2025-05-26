using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Heatmap : MonoBehaviour
{
    public int width = 100;
    public int height = 100;
    public float cellSize = 1f;
    public Texture2D heatmapTexture;
    public Material heatmapMaterial;
    public GameObject heatmapPlane;

    private int[,] heatmap;
    private bool heatmapVisible = false;
    private Renderer planeRenderer;

    Keyboard Keyboard;

    public Vector2 originOffset = new Vector2(0, 0); // Offset per X e Z
    public RawImage heatmapDisplay;


    void Start()
    {
        heatmap = new int[width, height];
        heatmapTexture = new Texture2D(width, height);
        heatmapTexture.filterMode = FilterMode.Point;

        if (heatmapDisplay != null)
        {
            heatmapDisplay.texture = heatmapTexture;
            heatmapDisplay.gameObject.SetActive(false); // inizialmente nascosta
        }

        planeRenderer = heatmapPlane.GetComponent<Renderer>();
        planeRenderer.material = heatmapMaterial;
        planeRenderer.material.mainTexture = heatmapTexture;

        // Scala il plane per coprire l'intera area della heatmap
        heatmapPlane.transform.localScale = new Vector3(
            width * cellSize / 10f,
            1f,
            height * cellSize / 10f
        );

        heatmapPlane.transform.position = new Vector3(
    originOffset.x + (width * cellSize) / 2f,
    0.1f,
    originOffset.y + (height * cellSize) / 2f
);


        heatmapPlane.SetActive(false); // start nascosto
        Keyboard = Keyboard.current;
    }


    void Update()
    {
        if (Keyboard.spaceKey.wasPressedThisFrame)
        {
            heatmapVisible = !heatmapVisible;

            if (heatmapDisplay != null)
                heatmapDisplay.gameObject.SetActive(heatmapVisible);

            if (heatmapVisible)
                UpdateHeatmapTexture();
        }

    }

    public void RegisterPosition(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt((worldPosition.x - originOffset.x) / cellSize);
        int y = Mathf.FloorToInt((worldPosition.z - originOffset.y) / cellSize);

        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            heatmap[x, y]++;
            Debug.Log($"[HEATMAP] Posizione registrata: ({x}, {y})");
        }
        else
        {
            Debug.LogWarning($"[HEATMAP] Posizione fuori dai limiti: {worldPosition}");
        }
    }



    public void UpdateHeatmapTexture()
    {
        int max = heatmap.Cast<int>().Max();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float t = Mathf.Log(heatmap[x, y] + 1) / Mathf.Log(max + 1);
                //t = Mathf.Clamp01(t * 5);
                Color c = Color.Lerp(Color.blue, Color.red, t);
                heatmapTexture.SetPixel(x, y, c);
            }
        }
        heatmapTexture.Apply();

    }
}
