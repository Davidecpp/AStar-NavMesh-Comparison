using UnityEngine;
using UnityEngine.InputSystem;
using Pathfinding;
using static MovingObstacle;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;

public class WallPlacer : MonoBehaviour
{
    public GameObject wallPrefab;
    public LayerMask placementMask;
    public LayerMask wallMask;
    public Material highlightMaterial;
    public Material previewMaterial;

    private Camera mainCamera;
    private GameObject wallHighlighted;
    private Material originalMaterial;

    public NavMeshSurface navMeshSurface;
    public delegate void OnNavMeshUpdated(); // Evento per aggiornare il NavMesh
    public static event OnNavMeshUpdated NavMeshUpdated;

    private Quaternion currentRotation = Quaternion.identity;

    private GameObject previewWall;

    void Start()
    {
        mainCamera = Camera.main;

        // Crea la preview una sola volta
        previewWall = Instantiate(wallPrefab);
        SetPreviewMaterial(previewWall);
        previewWall.GetComponent<Collider>().enabled = false;
    }

    void Update()
    {
        // Controllo per evitare piazzamento se il pannello delle statistiche degli NPC è aperto
        if (NPCSpawner.panelStatsOpen)
        {
            previewWall.SetActive(false);
            ClearHighlight();
            return;
        }
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            previewWall.SetActive(false);
            ClearHighlight();
            return;
        }
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        // Posizionamento della preview
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementMask))
        {
            Vector3 placePosition = hit.point;
            placePosition.x = Mathf.Round(placePosition.x);
            placePosition.z = Mathf.Round(placePosition.z);

            previewWall.transform.position = placePosition;
            previewWall.transform.rotation = currentRotation;
            previewWall.SetActive(true);
        }
        else
        {
            previewWall.SetActive(false);
        }

        // Cambio rotazione con Spazio
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            currentRotation = currentRotation == Quaternion.identity
                ? Quaternion.Euler(0, 90, 0)
                : Quaternion.identity;
        }

        // Evidenziazione muro esistente
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, wallMask))
        {
            GameObject wall = hitInfo.collider.gameObject;

            if (wall != wallHighlighted)
            {
                ClearHighlight();

                wallHighlighted = wall;
                var renderer = wall.GetComponent<Renderer>();
                if (renderer != null)
                {
                    originalMaterial = renderer.material;
                    renderer.material = highlightMaterial;
                }
            }
        }
        else
        {
            ClearHighlight();
        }

        // Posizionamento muro reale con click sinistro
        if (Mouse.current.leftButton.wasPressedThisFrame && previewWall.activeSelf)
        {
            Vector3 pos = previewWall.transform.position;
            Quaternion rot = previewWall.transform.rotation;

            GameObject wallInstance = Instantiate(wallPrefab, pos, rot);
            Bounds bounds = wallInstance.GetComponent<Collider>().bounds;
            AstarPath.active.UpdateGraphs(bounds);
            RecalculateNavPath();
        }

        // Rimozione muro con click destro
        if (Mouse.current.rightButton.wasPressedThisFrame && wallHighlighted != null)
        {
            Bounds bounds = wallHighlighted.GetComponent<Collider>().bounds;
            Destroy(wallHighlighted);
            AstarPath.active.UpdateGraphs(bounds);
            wallHighlighted = null;
            RecalculateNavPath();
        }
    }

    void RecalculateNavPath()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);

            // Avvisa gli NPC di ricalcolare il percorso
            NavMeshUpdated?.Invoke();
        }
    }

    void SetPreviewMaterial(GameObject wall)
    {
        Renderer[] renderers = wall.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.material = previewMaterial;
        }
    }

    void ClearHighlight()
    {
        if (wallHighlighted != null)
        {
            var renderer = wallHighlighted.GetComponent<Renderer>();
            if (renderer != null && originalMaterial != null)
            {
                renderer.material = originalMaterial;
            }
            wallHighlighted = null;
            originalMaterial = null;
        }
    }
}