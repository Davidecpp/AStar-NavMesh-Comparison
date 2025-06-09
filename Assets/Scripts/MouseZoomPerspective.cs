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

    [Header("Middle Mouse Drag")]
    [SerializeField] private bool enableMiddleMouseDrag = true;
    [SerializeField] private float dragSensitivity = 2f;
    [SerializeField] private bool invertDragX = false;
    [SerializeField] private bool invertDragY = false;

    private Camera cam;
    private float targetZoom;
    private float zoomVelocity = 0f;
    private Vector3 targetPosition;
    private Vector3 positionVelocity = Vector3.zero;

    private bool isMiddleMousePressed = false;
    private Vector2 lastMousePosition;

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
        HandleMiddleMouseDrag();

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

    // Handle zoom input from the mouse scroll wheel
    void HandleZoomInput()
    {
        Vector2 scrollInput = Mouse.current.scroll.ReadValue();

        if (scrollInput.y != 0f)
        {
            float normalizedScroll = Mathf.Sign(scrollInput.y);
            float previousZoom = targetZoom;

            // Calculate the new target zoom based on scroll input
            targetZoom -= normalizedScroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

            if (zoomToMouse && targetZoom != previousZoom)
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

                // Converts mouse position to a ray in the world
                Ray mouseRay = cam.ScreenPointToRay(mouseScreenPos);

                Vector3 mouseWorldPos = GetMouseWorldPosition(mouseRay);

                float zoomFactor = (previousZoom - targetZoom) / previousZoom;
                Vector3 direction = mouseWorldPos - transform.position;

                targetPosition += direction * zoomFactor * zoomToMouseSensitivity;
            }
        }
    }

    void HandleMiddleMouseDrag()
    {
        if (!enableMiddleMouseDrag) return;

        // Check if the middle mouse button is pressed or released
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isMiddleMousePressed = true;
            lastMousePosition = Mouse.current.position.ReadValue();
        }
        else if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isMiddleMousePressed = false;
        }

        // If the middle mouse is pressed, calculate the movement
        if (isMiddleMousePressed)
        {
            Vector2 currentMousePosition = Mouse.current.position.ReadValue();
            Vector2 mouseDelta = currentMousePosition - lastMousePosition;

            // Converts mouse delta to world delta
            Vector3 worldDelta = ScreenToWorldDelta(mouseDelta);

            if (invertDragX) worldDelta.x = -worldDelta.x;
            if (invertDragY) worldDelta.z = -worldDelta.z;

            targetPosition -= worldDelta * dragSensitivity;
            lastMousePosition = currentMousePosition;
        }
    }

    // Converts the mouse delta in screen space to a delta in world space.
    Vector3 ScreenToWorldDelta(Vector2 screenDelta)
    {
        // Calculate the scale factor based on camera distance and FOV
        float distance = Mathf.Abs(transform.position.y);
        float frustumHeight = 2.0f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * cam.aspect;

        Vector2 normalizedDelta = new Vector2(
            screenDelta.x / Screen.width,
            screenDelta.y / Screen.height
        );

        // Converts normalized delta to world space
        Vector3 worldDelta = new Vector3(
            normalizedDelta.x * frustumWidth,
            0f,
            normalizedDelta.y * frustumHeight
        );

        return worldDelta;
    }

    // Calculates the world position based on the mouse ray and a projection distance.
    Vector3 GetMouseWorldPosition(Ray mouseRay)
    {
        float projectionDistance = 10f;
        return mouseRay.origin + mouseRay.direction * projectionDistance;
    }

    void ApplySmoothZoom()
    {
        cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetZoom, ref zoomVelocity, smoothTime);
    }

    void ApplyDirectZoom()
    {
        cam.fieldOfView = targetZoom;
    }

    void ApplySmoothPosition()
    {
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, smoothTime);
    }

    void ApplyDirectPosition()
    {
        transform.position = targetPosition;
    }

    public void SetZoom(float newZoom)
    {
        targetZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);
    }

    public float GetCurrentZoom()
    {
        return cam.fieldOfView;
    }

    public void SetTargetPosition(Vector3 newPosition)
    {
        targetPosition = newPosition;
    }

    // For enabling/disabling middle mouse drag and setting sensitivity
    public void SetMiddleMouseDragEnabled(bool enabled)
    {
        enableMiddleMouseDrag = enabled;
    }

    public void SetDragSensitivity(float sensitivity)
    {
        dragSensitivity = sensitivity;
    }
}