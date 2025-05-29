using UnityEngine;
using UnityEngine.AI;
using System.Diagnostics;
using UnityEngine.UI;
using TMPro;
using Debug = UnityEngine.Debug;
using System.Collections;

public class NavMeshNPCController : MonoBehaviour
{
    public Transform target;
    private NavMeshAgent agent;

    private Stopwatch movementStopwatch = new Stopwatch();

    private bool isMoving = false;

    private Vector3 lastPosition;
    private float distanceTravelled = 0f;
    private double lastCalcTime = 0.0;

    private Animator animator;

    private void OnNavMeshUpdated()
    {
        if (target != null && agent != null)
        {
            //Debug.Log($"{gameObject.name} riceve NavMeshUpdated: ricalcolo destinazione");
            agent.ResetPath();
            agent.SetDestination(target.position);
        }
    }

    private void OnDestroy()
    {
        WallPlacer.NavMeshUpdated -= OnNavMeshUpdated;
    }


    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        movementStopwatch = new Stopwatch();
        lastPosition = transform.position;

        WallPlacer.NavMeshUpdated += OnNavMeshUpdated;

        if (target != null)
        {
            SetNewTarget(target);
        }
    }


    void Update()
    {
        if (target != null)
        {

            // Controlla movimento e aggiorna movementStopwatch
            if (!isMoving && agent.hasPath && agent.remainingDistance > agent.stoppingDistance && agent.velocity.sqrMagnitude > 0.01f)
            {
                movementStopwatch.Restart();
                isMoving = true;
            }

            if (isMoving)
            {
                float delta = Vector3.Distance(transform.position, lastPosition);
                distanceTravelled += delta;
                lastPosition = transform.position;
            }
            // Controlla se il personaggio ha raggiunto la destinazione
            if (isMoving && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
                {
                    movementStopwatch.Stop();
                    isMoving = false;

                    agent.isStopped = true; // ferma del tutto l'agente
                    agent.ResetPath();      // evita piccoli ricalcoli
                }
            }

            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }
    }

    public void SetNewTarget(Transform newTarget)
    {
        target = newTarget;

        // Applica un offset casuale alla destinazione
        Vector2 offset2D = Random.insideUnitCircle * 2f; // raggio di dispersione
        Vector3 offset = new Vector3(offset2D.x, 0, offset2D.y);
        Vector3 destination = target.position + offset;

        NavMeshPath path = new NavMeshPath();
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        bool success = NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path);

        stopwatch.Stop();
        lastCalcTime = stopwatch.Elapsed.TotalMilliseconds;

        if (success)
        {
            agent.SetPath(path);
        }
        else
        {
            Debug.LogWarning("NavMesh non ha trovato un percorso valido.");
        }
    }


    public float GetDistance()
    {
        return distanceTravelled;
    }
    public double GetPathTime() // Restituisce il tempo di percorrenza
    {
        if (movementStopwatch == null)
        {
            Debug.Log("Movement stopwatch non inizializzato!");
            return 0;
        }
        return movementStopwatch.Elapsed.TotalSeconds;
    }
    public double GetCalcTime() // Restituisce il tempo di calcolo dell'ultimo percorso
    {
        return lastCalcTime;
    }
}