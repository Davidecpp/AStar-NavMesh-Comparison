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

    private float heatmapUpdateInterval = 0.2f;
    private float heatmapUpdateTimer = 0f;


    void Start()
    {
        heatmap = new int[width, height];
        heatmapTexture = new Texture2D(width, height);
        heatmapTexture.filterMode = FilterMode.Point;

        if (heatmapDisplay != null)
        {
            heatmapDisplay.texture = heatmapTexture;
            heatmapDisplay.gameObject.SetActive(false);
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

        // Aggiornamento periodico se visibile
        if (heatmapVisible)
        {
            heatmapUpdateTimer += Time.deltaTime;
            if (heatmapUpdateTimer >= heatmapUpdateInterval)
            {
                UpdateHeatmapTexture();
                heatmapUpdateTimer = 0f;
            }
        }
    }

    public void RegisterPosition(Vector3 worldPosition)
    {
        int centerX = Mathf.FloorToInt((worldPosition.x - originOffset.x) / cellSize);
        int centerY = Mathf.FloorToInt((worldPosition.z - originOffset.y) / cellSize);

        int radius = 2; // raggio in celle
        float maxIntensity = 1f;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int px = centerX + x;
                int py = centerY + y;

                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    float distance = Mathf.Sqrt(x * x + y * y);
                    float intensity = Mathf.Clamp01(1 - (distance / (radius + 0.1f))); // calore decrescente

                    heatmap[px, py] += Mathf.RoundToInt(maxIntensity * intensity * 10f); // amplifica
                }
            }
        }

        Debug.Log($"[HEATMAP] Posizione registrata (area): ({centerX}, {centerY})");
    }




    public void UpdateHeatmapTexture()
    {
        int max = heatmap.Cast<int>().Max();
        if (max == 0) max = 1; // evita divisione per zero

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float t = Mathf.Log(heatmap[x, y] + 1) / Mathf.Log(max + 1);
                Color c = GetHeatmapColor(t);
                heatmapTexture.SetPixel(x, y, c);
            }
        }
        heatmapTexture.Apply();
    }

    private Color GetHeatmapColor(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.2f)
            return Color.Lerp(Color.blue, Color.cyan, t / 0.2f); // 0.0 - 0.2
        else if (t < 0.4f)
            return Color.Lerp(Color.cyan, Color.green, (t - 0.2f) / 0.2f); // 0.2 - 0.4
        else if (t < 0.6f)
            return Color.Lerp(Color.green, Color.yellow, (t - 0.4f) / 0.2f); // 0.4 - 0.6
        else if (t < 0.8f)
            return Color.Lerp(Color.yellow, new Color(1f, 0.5f, 0f), (t - 0.6f) / 0.2f); // 0.6 - 0.8 (arancione)
        else
            return Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (t - 0.8f) / 0.2f); // 0.8 - 1.0
    }


}
