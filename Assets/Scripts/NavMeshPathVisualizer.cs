using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(LineRenderer), typeof(NavMeshAgent))]
public class NavMeshPathLine : MonoBehaviour
{
    private NavMeshAgent agent;
    private LineRenderer lineRenderer;

    // Cache dell'ultimo percorso valido per evitare scomparse temporanee
    private Vector3[] lastValidPath;
    private float pathUpdateDelay = 0.1f; // Ritardo per evitare aggiornamenti troppo frequenti
    private float lastPathUpdate;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        lineRenderer = GetComponent<LineRenderer>();

        // Configurazione iniziale del LineRenderer
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
    }

    void Update()
    {
        // Aggiorna il percorso solo se è passato abbastanza tempo dall'ultimo aggiornamento
        if (Time.time - lastPathUpdate < pathUpdateDelay)
            return;

        if (agent.hasPath && agent.path.corners.Length > 1)
        {
            // Percorso valido disponibile
            NavMeshPath path = agent.path;
            lastValidPath = new Vector3[path.corners.Length];

            // Copia i punti del percorso
            for (int i = 0; i < path.corners.Length; i++)
            {
                lastValidPath[i] = path.corners[i];
            }

            // Aggiorna il LineRenderer
            lineRenderer.positionCount = lastValidPath.Length;
            lineRenderer.SetPositions(lastValidPath);
            lineRenderer.enabled = true;

            lastPathUpdate = Time.time;
        }
        else if (agent.pathPending)
        {
            // Il percorso è in fase di calcolo - mantieni l'ultimo percorso valido
            if (lastValidPath != null && lastValidPath.Length > 1)
            {
                lineRenderer.positionCount = lastValidPath.Length;
                lineRenderer.SetPositions(lastValidPath);
                lineRenderer.enabled = true;
            }
        }
        else if (!agent.hasPath && lastValidPath == null)
        {
            // Nessun percorso e nessun percorso cached - nascondi la linea
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        // Se l'agente ha raggiunto la destinazione, nascondi gradualmente la linea
        if (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            if (agent.velocity.sqrMagnitude < 0.01f)
            {
                lineRenderer.enabled = false;
                lastValidPath = null;
            }
        }
    }

    // Metodo pubblico per forzare l'aggiornamento del percorso (utile quando il NavMesh viene aggiornato)
    public void ForcePathUpdate()
    {
        lastPathUpdate = 0f; // Resetta il timer per permettere aggiornamento immediato
    }
}