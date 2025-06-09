using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PathVisibilityController : MonoBehaviour
{
    public Toggle pathToggle;
    private List<PathVisualizer> visualizers = new List<PathVisualizer>();

    // Singleton instance
    public static PathVisibilityController Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        bool showPaths = PlayerPrefs.GetInt("ShowPaths", 1) == 1;
        pathToggle.isOn = showPaths;
        UpdateAllPaths(showPaths);

        pathToggle.onValueChanged.AddListener(UpdateAllPaths);
    }

    // Method to register a visualizer
    public void RegisterVisualizer(PathVisualizer visualizer)
    {
        if (!visualizers.Contains(visualizer))
            visualizers.Add(visualizer);

        visualizer.SetPathsVisible(pathToggle.isOn);
    }

    public void UnregisterVisualizer(PathVisualizer visualizer)
    {
        visualizers.Remove(visualizer);
    }

    // Method to update visibility of all paths
    void UpdateAllPaths(bool show)
    {
        foreach (var visualizer in visualizers)
        {
            if (visualizer != null)
                visualizer.SetPathsVisible(show);
        }
        PlayerPrefs.SetInt("ShowPaths", show ? 1 : 0);
    }
}