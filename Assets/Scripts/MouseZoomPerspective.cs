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
        // Ottieni il componente Camera
        cam = GetComponent<Camera>();

        // Se non c'è una camera su questo oggetto, cerca quella principale
        if (cam == null)
            cam = Camera.main;

        // Imposta lo zoom iniziale come quello corrente
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
            // Normalizza il valore dello scroll
            float normalizedScroll = Mathf.Sign(scrollInput.y);

            // Salva il FOV precedente per calcolare il movimento
            float previousZoom = targetZoom;

            // Calcola il nuovo zoom target
            targetZoom -= normalizedScroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

            // Se lo zoom è cambiato e vogliamo zoomare verso il mouse
            if (zoomToMouse && targetZoom != previousZoom)
            {
                // Ottieni la posizione del mouse in coordinate schermo
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

                // Converti la posizione del mouse in un raggio dal mondo
                Ray mouseRay = cam.ScreenPointToRay(mouseScreenPos);

                // Calcola il punto nel mondo verso cui zoomare
                // Usiamo una distanza fissa o il piano z=0 per semplicità
                Vector3 mouseWorldPos = GetMouseWorldPosition(mouseRay);

                // Calcola quanto movimento è necessario per centrare il mouse
                float zoomFactor = (previousZoom - targetZoom) / previousZoom;
                Vector3 direction = mouseWorldPos - transform.position;

                // Applica il movimento proporzionale al cambio di zoom
                targetPosition += direction * zoomFactor * zoomToMouseSensitivity;
            }
        }
    }

    Vector3 GetMouseWorldPosition(Ray mouseRay)
    {
        // Per una camera 3D, proiettiamo su un piano a una certa distanza
        // Puoi modificare questo metodo in base alle tue esigenze specifiche

        // Distanza di proiezione (regolabile in base al tuo setup)
        float projectionDistance = 10f;

        // Se hai un piano specifico su cui proiettare (es. y=0 per un piano orizzontale)
        // puoi usare Plane.Raycast invece

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