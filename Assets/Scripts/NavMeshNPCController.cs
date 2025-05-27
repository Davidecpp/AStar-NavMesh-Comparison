using UnityEngine;
using UnityEngine.AI;
using System.Diagnostics;
using UnityEngine.UI;
using TMPro;
using Debug = UnityEngine.Debug;

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


    // Prova altro
    void Update()
    {
        if (target != null)
        {
            // Ricalcola il percorso ogni 60 frame
            if (isMoving && Time.frameCount % 60 == 0)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                agent.SetDestination(target.position);

                stopwatch.Stop();
                lastCalcTime = stopwatch.Elapsed.TotalMilliseconds;
            }

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
                if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                {
                    movementStopwatch.Stop();
                    //Debug.Log("Tempo impiegato NavMesh: " + movementStopwatch.Elapsed.TotalSeconds + " secondi");
                    isMoving = false;
                }
            }
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }
    }

    public void SetNewTarget(Transform newTarget)
    {
        target = newTarget;

        // Calcola path e misura il tempo
        NavMeshPath path = new NavMeshPath();
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        bool success = NavMesh.CalculatePath(transform.position, target.position, NavMesh.AllAreas, path);

        stopwatch.Stop();
        lastCalcTime = stopwatch.Elapsed.TotalMilliseconds;

        // Se il percorso è valido, imposta la destinazione
        if (success)
        {
            agent.SetPath(path);
            //DrawPath(path);
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