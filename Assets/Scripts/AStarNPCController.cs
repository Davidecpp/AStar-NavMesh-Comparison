using UnityEngine;
using Pathfinding;
using System.Diagnostics;
using TMPro;
using System.Collections;

public class AStarNPCController : MonoBehaviour
{
    public Transform target;
    private Seeker seeker;
    private Stopwatch stopwatch;
    private Animator animator;
    private AIPath aiPath;
    private AStarTimer aStarTimer;

    private Vector3 lastPosition;
    public float distanceTravelled = 0f;
    private bool isMoving = false;

    

    void Awake()
    {
        seeker = GetComponent<Seeker>();
        aiPath = GetComponent<AIPath>();
        animator = GetComponent<Animator>();
        aStarTimer = GetComponent<AStarTimer>();
        stopwatch = new Stopwatch();
        lastPosition = transform.position;
    }

    void Start()
    {
        StartCoroutine(StartDelayed());
    }

    IEnumerator StartDelayed()
    {
        yield return new WaitForSeconds(0.01f);
        CalculatePathToTarget();
    }

    void Update()
    {
        if (target != null)
        {
            // Ricalcola il percorso ogni 30 frame
            if (isMoving && Time.frameCount % 30 == 0)
            {
                CalculatePathToTarget();
            }

            // Calcola velocità per l’animazione
            float speed = aiPath.velocity.magnitude;
            animator.SetFloat("Speed", speed);

            // Se si sta muovendo
            if (speed > 0.05f)
            {
                isMoving = true;
                float delta = Vector3.Distance(transform.position, lastPosition);
                distanceTravelled += delta;
                lastPosition = transform.position;
            }
            else
            {
                isMoving = false;
            }
        }
    }

    public void CalculatePathToTarget()
    {
        if (seeker == null)
        {
            UnityEngine.Debug.LogError("Seeker non trovato!");
            return;
        }

        if (target == null)
        {
            UnityEngine.Debug.LogWarning("Target non assegnato, impossibile calcolare il percorso.");
            return;
        }

        aiPath.destination = target.position;
        aiPath.canMove = true;

        stopwatch.Restart();
        seeker.StartPath(transform.position, target.position, OnPathComplete);
    }

    void OnPathComplete(Path path)
    {
        stopwatch.Stop();

        /*if (calcTimeTxt != null)
        {
            if (!path.error)
            {
                //UnityEngine.Debug.Log("Tempo di calcolo A*: " + stopwatch.Elapsed.TotalMilliseconds + " ms");
                calcTimeTxt.text = stopwatch.Elapsed.TotalMilliseconds.ToString("F2") + " ms";
            }
            else
            {
                UnityEngine.Debug.LogWarning("A* non ha trovato un percorso valido.");
                calcTimeTxt.text = "Errore";
            }
        }*/
    }

    public float GetDistance()
    {
        return distanceTravelled;
    }
    public double GetCalcTime()
    {
        return stopwatch.Elapsed.TotalMilliseconds;
    }
    public float GetPathTime()
    {
        if (aStarTimer != null)
        {
            return aStarTimer.CurrentTimeSeconds;
        }
        return 0f;
    }
}
