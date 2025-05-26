using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private Keyboard keyboard;
    public GameObject obstacle;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        keyboard = Keyboard.current;
    }

    // Update is called once per frame
    void Update()
    {
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(0);
        }
        /*if (keyboard.eKey.wasPressedThisFrame)
        {
            if (obstacle.activeSelf)
            {
                obstacle.SetActive(false);
            }
            else
            {
                obstacle.SetActive(true);
            }
            Bounds bounds = obstacle.GetComponent<Collider>().bounds;
            AstarPath.active.UpdateGraphs(bounds);
        }*/
    }
}
