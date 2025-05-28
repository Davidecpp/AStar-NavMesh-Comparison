using UnityEngine;
using UnityEngine.InputSystem;

public class CameraZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 10f;

    [Header("Smooth Zoom")]
    [SerializeField] private bool useSmoothZoom = true;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Zoom to Mouse")]
    [SerializeField] private bool zoomToMouse = true;
    [SerializeField] private float zoomToMouseSensitivity = 1f;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity = 0f;
    private Vector3 targetPosition;
    private Vector3 positionVelocity = Vector3.zero;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (cam == null)
            cam = Camera.main;

        targetZoom = cam.fieldOfView;
        targetPosition = transform.position;
    }

    void Update()
    {
        HandleZoomInput();

        if (useSmoothZoom)
        {
            ApplySmoothZoom();
            ApplySmoothPosition();
        }
        else
        {
            ApplyDirectZoom();
            ApplyDirectPosition();
        }
    }

    void HandleZoomInput()
    {
        // Ottieni l'input della rotellina del mouse usando il nuovo Input System
        Vector2 scrollInput = Mouse.current.scroll.ReadValue();

        if (scrollInput.y != 0f)
        {
            float normalizedScroll = Mathf.Sign(scrollInput.y);
            float previousZoom = targetZoom;

            // Calcola il nuovo zoom target
            targetZoom -= normalizedScroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

            // Se lo zoom è cambiato e vogliamo zoomare verso il mouse
            if (zoomToMouse && targetZoom != previousZoom)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

                // Converti la posizione del mouse in un raggio dal mondo
                Ray mouseRay = cam.ScreenPointToRay(mouseScreenPos);

                Vector3 mouseWorldPos = GetMouseWorldPosition(mouseRay);

                float zoomFactor = (previousZoom - targetZoom) / previousZoom;
                Vector3 direction = mouseWorldPos - transform.position;

                targetPosition += direction * zoomFactor * zoomToMouseSensitivity;
            }
        }
    }

    // Calcola la posizione del mouse nel mondo
    Vector3 GetMouseWorldPosition(Ray mouseRay)
    {
        float projectionDistance = 10f;

        return mouseRay.origin + mouseRay.direction * projectionDistance;
    }

    void ApplySmoothZoom()
    {
        // Applica lo zoom con interpolazione smooth
        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetZoom, ref zoomVelocity, smoothTime);
    }

    void ApplyDirectZoom()
    {
        // Applica lo zoom direttamente
        cam.fieldOfView = targetZoom;
    }

    void ApplySmoothPosition()
    {
        // Applica il movimento della camera con interpolazione smooth
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, smoothTime);
    }

    void ApplyDirectPosition()
    {
        // Applica il movimento della camera direttamente
        transform.position = targetPosition;
    }

    // Metodo pubblico per impostare lo zoom da script
    public void SetZoom(float newZoom)
    {
        targetZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
    }

    // Metodo pubblico per ottenere lo zoom corrente
    public float GetCurrentZoom()
    {
        return cam.fieldOfView;
    }

    // Metodo per impostare la posizione target
    public void SetTargetPosition(Vector3 newPosition)
    {
        targetPosition = newPosition;
    }
}