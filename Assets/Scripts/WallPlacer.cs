using UnityEngine;
using UnityEngine.InputSystem;
using Pathfinding;
using static MovingObstacle;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;
using System.Collections;

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

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh costruito con successo.");
        }
        else
        {
            Debug.LogWarning("NavMeshSurface non assegnato. Assicurati di assegnarlo nell'Inspector.");
        }

        // Crea la preview
        CreatePreviewWall();
    }

    void Update()
    {
        // Se la preview è stata distrutta (cambio scena), ricreala
        if (previewWall == null)
        {
            CreatePreviewWall();
            if (previewWall == null) return; // Se ancora null, wallPrefab potrebbe essere null
        }

        if (wallHighlighted != null && wallHighlighted.Equals(null))
        {
            wallHighlighted = null; // È stato distrutto dal sistema
        }

        // Controllo per evitare piazzamento se il pannello delle statistiche degli NPC aperto
        if (NPCSpawner.panelStatsOpen)
        {
            previewWall.SetActive(false);
            ClearHighlight();
            return;
        }
        // Controllo per evitare piazzamento se il mouse non si trova su un oggetto UI
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
            //Debug.Log($"Muro posizionato in {pos} con rotazione {rot.eulerAngles}");
        }

        // Rimozione muro con click destro
        if (Mouse.current.rightButton.wasPressedThisFrame && wallHighlighted != null)
        {
            Bounds bounds = wallHighlighted.GetComponent<Collider>().bounds;
            Destroy(wallHighlighted);
            AstarPath.active.UpdateGraphs(bounds);
            wallHighlighted = null;
            StartCoroutine(DelayedNavMeshUpdate());
            //Debug.Log($"Muro rimosso da {bounds.center} con dimensioni {bounds.size}");
        }
    }

    // Metodo per creare la preview wall
    void CreatePreviewWall()
    {
        if (wallPrefab != null)
        {
            previewWall = Instantiate(wallPrefab);
            SetPreviewMaterial(previewWall);
            previewWall.GetComponent<Collider>().enabled = false;
        }
        else
        {
            Debug.LogWarning("wallPrefab è null! Assicurati di assegnarlo nell'Inspector.");
        }
    }

    // Ricalcola il percorso del NavMesh e notifica gli NPC
    void RecalculateNavPath()
    {
        if (previewWall != null)
            previewWall.SetActive(false); // Disattiva la preview temporaneamente

        if (navMeshSurface != null)
        {
            navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            NavMeshUpdated?.Invoke(); // Notifica agli NPC di ricalcolare
        }

        if (previewWall != null)
            previewWall.SetActive(true); // Riattiva dopo aggiornamento
    }
    IEnumerator DelayedNavMeshUpdate()
    {
        yield return null; // Attendi un frame per completare Destroy

        RecalculateNavPath();
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