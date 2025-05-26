using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CameraController : MonoBehaviour
{
    public Camera cam1;
    public Camera cam2;
    public Camera aboveCamera;

    private Keyboard keyboard;

    void Start()
    {
        keyboard = Keyboard.current;
    }

    void Update()
    {
        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            if (!cam1.gameObject.activeSelf)
            {
                cam1.gameObject.SetActive(true);
                cam2.gameObject.SetActive(false);
                aboveCamera.gameObject.SetActive(false); 
            }
        }
        if (keyboard.digit2Key.wasPressedThisFrame)
        {
            if (!cam2.gameObject.activeSelf)
            {
                cam2.gameObject.SetActive(true);
                cam1.gameObject.SetActive(false);
                aboveCamera.gameObject.SetActive(false);
            }
        }
        if (keyboard.digit3Key.wasPressedThisFrame)
        {
            if (!aboveCamera.gameObject.activeSelf)
            {
                cam2.gameObject.SetActive(false);
                cam1.gameObject.SetActive(false);
                aboveCamera.gameObject.SetActive(true);
            }
        }
    }
}
