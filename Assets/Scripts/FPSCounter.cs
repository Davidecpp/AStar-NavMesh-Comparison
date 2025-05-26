using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    float deltaTime = 0.0f;
    float updateInterval = 0.5f;
    float timeSinceLastUpdate = 0.0f;
    string fpsText = "";

    float maxFPS = float.MinValue;
    float minFPS = float.MaxValue;

    int firstFrame = 0;

    void Update()
    {
        // Ignora momento iniziale
        if (firstFrame <= 20)
        {
            firstFrame++;
            return;
        }
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        timeSinceLastUpdate += Time.unscaledDeltaTime;

        if (timeSinceLastUpdate >= updateInterval)
        {
            float fps = 1.0f / deltaTime;

            // Aggiorna i valori di FPS massimo e minimo
            if (fps > maxFPS) maxFPS = fps;
            if (fps < minFPS) minFPS = fps;

            fpsText = $"FPS: {fps:F0} | Max: {maxFPS:F0} | Min: {minFPS:F0}";
            timeSinceLastUpdate = 0f;
        }
    }

    void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(10, 10, w, h * 2 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / 50;
        style.normal.textColor = Color.white;

        GUI.Label(rect, fpsText, style);
    }
}
