using UnityEngine;
using Pathfinding;
using System.Diagnostics;
using UnityEngine.UI;
using TMPro;
using Debug = UnityEngine.Debug;

public class AStarTimer : MonoBehaviour
{
    private AIPath aiPath;
    private AStarNPCController npcController; 
    private Stopwatch stopwatch;
    private bool timerStarted = false;
    private bool timerStopped = false;

    private float lastVelocityMagnitude = 0f;
    private bool lastReachedEndOfPath = false;

    void Start()
    {
        aiPath = GetComponent<AIPath>();
        npcController = GetComponent<AStarNPCController>(); 
        stopwatch = new Stopwatch();

        if (aiPath == null)
            Debug.LogError("AIPath component non trovato!");
        if (npcController == null)
            Debug.LogError("AStarNPCController component non trovato!");
    }

    void Update()
    {
        if (aiPath == null) return;

        // Cache 
        float currentVelocityMagnitude = aiPath.velocity.magnitude;
        bool currentReachedEndOfPath = aiPath.reachedEndOfPath;

        // Start timer when the agent has a valid path and is moving
        if (!timerStarted && ShouldStartTimer(currentVelocityMagnitude))
        {
            stopwatch.Start();
            timerStarted = true;
            timerStopped = false;
            //Debug.Log($"Timer avviato per {gameObject.name}");
        }

        // Stop timer when the agent reaches the end of the path or stops moving
        if (timerStarted && !timerStopped && ShouldStopTimer(currentVelocityMagnitude, currentReachedEndOfPath))
        {
            stopwatch.Stop();
            timerStopped = true;
            //Debug.Log($"Timer fermato per {gameObject.name} - Tempo: {CurrentTimeSeconds:F2}s");
        }

        // Update the last velocity and reached end of path status
        lastVelocityMagnitude = currentVelocityMagnitude;
        lastReachedEndOfPath = currentReachedEndOfPath;
    }

    private bool ShouldStartTimer(float velocityMagnitude)
    {
        return aiPath.hasPath &&
               !aiPath.reachedEndOfPath &&
               !aiPath.pathPending &&
               velocityMagnitude > 0.1f &&
               (npcController == null || !npcController.HasArrived());
    }

    // Determines when to stop the timer based on the agent's state
    private bool ShouldStopTimer(float velocityMagnitude, bool reachedEndOfPath)
    {
        if (npcController != null && npcController.HasArrived())
        {
            return true;
        }

        bool hasReachedEnd = reachedEndOfPath ||
                            (aiPath.hasPath && !aiPath.pathPending &&
                             Vector3.Distance(transform.position, aiPath.destination) < aiPath.endReachedDistance);

        bool hasStopped = velocityMagnitude < 0.05f;
        bool movementDisabled = !aiPath.canMove && velocityMagnitude < 0.1f;

        return (hasReachedEnd && hasStopped) || movementDisabled;
    }

    public void ResetTimer()
    {
        stopwatch.Reset();
        timerStarted = false;
        timerStopped = false;
        lastVelocityMagnitude = 0f;
        lastReachedEndOfPath = false;
        Debug.Log($"Timer resettato per {gameObject.name}");
    }

    // Called to forcefully stop the timer, e.g., when the NPC is destroyed or the game ends
    public void ForceStopTimer()
    {
        if (timerStarted && !timerStopped)
        {
            stopwatch.Stop();
            timerStopped = true;
            //Debug.Log($"Timer forzatamente fermato per {gameObject.name} - Tempo: {CurrentTimeSeconds:F2}s");
        }
    }

    public float CurrentTimeSeconds
    {
        get
        {
            return (float)stopwatch.Elapsed.TotalSeconds;
        }
    }

    // Proprietà di debug
    public bool IsTimerRunning => timerStarted && !timerStopped;
    public bool HasTimerStarted => timerStarted;
    public bool HasTimerStopped => timerStopped;
}