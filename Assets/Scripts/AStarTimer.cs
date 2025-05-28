using UnityEngine;
using Pathfinding;
using System.Diagnostics;
using UnityEngine.UI;
using TMPro;
using Debug = UnityEngine.Debug;

public class AStarTimer : MonoBehaviour
{
    private AIPath aiPath;
    private Stopwatch stopwatch;
    private bool timerStarted = false;
    private bool timerStopped = false;


    void Start()
    {
        aiPath = GetComponent<AIPath>();
        stopwatch = new Stopwatch();

        if (aiPath == null)
            Debug.LogError("AIPath component non trovato!");
    }


    void Update()
    {
        if (aiPath == null) return;

        //Debug.Log($"hasPath: {aiPath.hasPath}, reachedEndOfPath: {aiPath.reachedEndOfPath}, pathPending: {aiPath.pathPending}, velocity: {aiPath.velocity.magnitude}");

        // Inizia il timer quando l'agente ha un percorso valido e si sta muovendo
        if (!timerStarted && aiPath.hasPath && !aiPath.reachedEndOfPath && !aiPath.pathPending && aiPath.velocity.magnitude > 0.1f)
        {
            stopwatch.Start();
            timerStarted = true;
            timerStopped = false;
        }

        if (timerStarted && !timerStopped && aiPath.reachedEndOfPath && aiPath.velocity.magnitude < 0.05f)
        {
            stopwatch.Stop();
            timerStopped = true;
        }
    }

    public void ResetTimer()
    {
        stopwatch.Reset();
        timerStarted = false;
        timerStopped = false;
    }
    public float CurrentTimeSeconds
    {
        get
        {
            return (float)stopwatch.Elapsed.TotalSeconds;
        }
    }
}
