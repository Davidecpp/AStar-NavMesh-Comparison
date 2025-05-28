using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

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

    public enum HeatmapType { All, NavMesh, AStar }
    public HeatmapType currentHeatmapType = HeatmapType.All;

    private int[,] navMeshHeatmap;
    private int[,] astarHeatmap;

    public TMP_Text heatmapInfoText;

    public Button allButton;
    public Button navMeshButton;
    public Button aStarButton;



    void Start()
    {
        heatmap = new int[width, height];
        navMeshHeatmap = new int[width, height];
        astarHeatmap = new int[width, height];

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

        heatmapPlane.SetActive(false);
        Keyboard = Keyboard.current;
        SetHeatmapAll();
    }


    void Update()
    {
        // Spazio per toggle heatmap
        if (Keyboard.spaceKey.wasPressedThisFrame)
        {
            heatmapVisible = !heatmapVisible;

            if (heatmapDisplay != null)
                heatmapDisplay.gameObject.SetActive(heatmapVisible);

            if (heatmapVisible)
                UpdateHeatmapTexture();
        }

        // Cambia tipo di heatmap con tasti Q, W, E
        if (Keyboard.qKey.wasPressedThisFrame)
        {
            SetHeatmapAll();
        }
        if (Keyboard.wKey.wasPressedThisFrame)
        {
            SetHeatmapNavMesh();
        }
        if (Keyboard.eKey.wasPressedThisFrame)
        {
            SetHeatmapAStar();
        }

        // Aggiornamento periodico della texture, se visibile
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

    // Registra una posizione nella heatmap
    public void RegisterPosition(Vector3 worldPosition, HeatmapType type)
    {
        int centerX = Mathf.FloorToInt((worldPosition.x - originOffset.x) / cellSize);
        int centerY = Mathf.FloorToInt((worldPosition.z - originOffset.y) / cellSize);

        int radius = 2;
        float maxIntensity = 1f;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                int px = centerX + x;
                int py = centerY + y;

                // Controlla se le coordinate sono all'interno dei limiti della heatmap
                if (px >= 0 && px < width && py >= 0 && py < height)
                {
                    float distance = Mathf.Sqrt(x * x + y * y);
                    float intensity = Mathf.Clamp01(1 - (distance / (radius + 0.1f)));
                    int value = Mathf.RoundToInt(maxIntensity * intensity * 10f);

                    // Aggiungi il valore alla heatmap corrispondente
                    switch (type)
                    {
                        case HeatmapType.NavMesh:
                            navMeshHeatmap[px, py] += value;
                            break;
                        case HeatmapType.AStar:
                            astarHeatmap[px, py] += value;
                            break;
                        case HeatmapType.All:
                            navMeshHeatmap[px, py] += value;
                            astarHeatmap[px, py] += value;
                            break;
                    }
                }
            }
        }
    }


    // Aggiorna la texture della heatmap in base ai dati registrati
    public void UpdateHeatmapTexture()
    {
        int[,] targetHeatmap;

        // Determina quale heatmap usare in base al tipo corrente
        switch (currentHeatmapType)
        {
            case HeatmapType.NavMesh:
                targetHeatmap = navMeshHeatmap;
                break;
            case HeatmapType.AStar:
                targetHeatmap = astarHeatmap;
                break;
            case HeatmapType.All:
                targetHeatmap = new int[width, height];
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        targetHeatmap[x, y] = navMeshHeatmap[x, y] + astarHeatmap[x, y];
                break;
            default:
                return;
        }

        int max = targetHeatmap.Cast<int>().Max();
        if (max == 0) max = 1;

        // Aggiorna i pixel della texture in base ai valori della heatmap
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float t = Mathf.Log(targetHeatmap[x, y] + 1) / Mathf.Log(max + 1);
                Color c = GetHeatmapColor(t);
                heatmapTexture.SetPixel(x, y, c);
            }
        }

        heatmapTexture.Apply();
    }

    // Imposta il tipo di heatmap corrente e aggiorna la texture
    public void SetHeatmapAll()
    {
        currentHeatmapType = HeatmapType.All;
        UpdateHeatmapTexture();
        if (heatmapInfoText != null)
            heatmapInfoText.text = "Heatmap Type: All (NavMesh + AStar)";
        UpdateButtonHighlights();
    }

    public void SetHeatmapNavMesh()
    {
        currentHeatmapType = HeatmapType.NavMesh;
        UpdateHeatmapTexture();
        if (heatmapInfoText != null)
            heatmapInfoText.text = "Heatmap Type: NavMesh";
        UpdateButtonHighlights();
    }

    public void SetHeatmapAStar()
    {
        currentHeatmapType = HeatmapType.AStar;
        UpdateHeatmapTexture();
        if (heatmapInfoText != null)
            heatmapInfoText.text = "Heatmap Type: AStar";
        UpdateButtonHighlights();
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

    private void UpdateButtonHighlights()
    {
        Color activeColor = Color.white;     // evidenziato
        Color inactiveColor = Color.gray;    // disattivo

        if (allButton != null)
            allButton.image.color = (currentHeatmapType == HeatmapType.All) ? activeColor : inactiveColor;

        if (navMeshButton != null)
            navMeshButton.image.color = (currentHeatmapType == HeatmapType.NavMesh) ? activeColor : inactiveColor;

        if (aStarButton != null)
            aStarButton.image.color = (currentHeatmapType == HeatmapType.AStar) ? activeColor : inactiveColor;
    }

}
