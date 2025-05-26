using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

public class PerformanceOverlay : MonoBehaviour
{
    GUIStyle style;
    float deltaTime = 0.0f;

    ProfilerRecorder mainThreadRecorder;
    ProfilerRecorder gcMemoryRecorder;

    void OnEnable()
    {
        // Avvia i recorder per CPU e RAM
        mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");

        // Stile grafico
        style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = Color.white;
    }

    void OnDisable()
    {
        mainThreadRecorder.Dispose();
        gcMemoryRecorder.Dispose();
    }

    void Update()
    {
        // Calcola delta time smussato
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        float fps = 1.0f / deltaTime;
        float frameTimeMs = deltaTime * 1000.0f;

        float mainThreadMs = mainThreadRecorder.Valid ? mainThreadRecorder.LastValue / 1_000_000f : -1f;
        float ramMB = gcMemoryRecorder.Valid ? gcMemoryRecorder.LastValue / (1024f * 1024f) : -1f;

        string text = $"FPS: {fps:F1}\n" +
                      $"Frame Time: {frameTimeMs:F1} ms\n" +
                      $"Main Thread CPU: {mainThreadMs:F2} ms\n" +
                      $"GC RAM Used: {ramMB:F2} MB";

        GUI.Label(new Rect(10, 50, 500, 100), text, style);
    }
}
