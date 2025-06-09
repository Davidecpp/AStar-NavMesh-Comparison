using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    private Keyboard keyboard;
    void Start()
    {
        keyboard = Keyboard.current;
    }
    void Update()
    {
        // Check for Escape key press to reload the current scene
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
