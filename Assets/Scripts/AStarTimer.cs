using UnityEngine;
using Pathfinding;
using System.Diagnostics;
using UnityEngine.UI;
using TMPro;
using Debug = UnityEngine.Debug;

public class AStarTimer : MonoBehaviour
{
    private AIPath aiPath;
    private AStarNPCController npcController; // Riferimento al controller
    private Stopwatch stopwatch;
    private bool timerStarted = false;
    private bool timerStopped = false;

    // Cache per ottimizzazioni
    private float lastVelocityMagnitude = 0f;
    private bool lastReachedEndOfPath = false;

    void Start()
    {
        aiPath = GetComponent<AIPath>();
        npcController = GetComponent<AStarNPCController>(); // Ottieni riferimento al controller
        stopwatch = new Stopwatch();

        if (aiPath == null)
            Debug.LogError("AIPath component non trovato!");
        if (npcController == null)
            Debug.LogError("AStarNPCController component non trovato!");
    }

    void Update()
    {
        if (aiPath == null) return;

        // Cache dei valori per evitare accessi multipli
        float currentVelocityMagnitude = aiPath.velocity.magnitude;
        bool currentReachedEndOfPath = aiPath.reachedEndOfPath;

        // Inizia il timer quando l'agente ha un percorso valido e si sta muovendo
        if (!timerStarted && ShouldStartTimer(currentVelocityMagnitude))
        {
            stopwatch.Start();
            timerStarted = true;
            timerStopped = false;
            Debug.Log($"Timer avviato per {gameObject.name}");
        }

        // Ferma il timer quando arriva alla destinazione
        if (timerStarted && !timerStopped && ShouldStopTimer(currentVelocityMagnitude, currentReachedEndOfPath))
        {
            stopwatch.Stop();
            timerStopped = true;
            Debug.Log($"Timer fermato per {gameObject.name} - Tempo: {CurrentTimeSeconds:F2}s");
        }

        // Aggiorna cache
        lastVelocityMagnitude = currentVelocityMagnitude;
        lastReachedEndOfPath = currentReachedEndOfPath;
    }

    private bool ShouldStartTimer(float velocityMagnitude)
    {
        return aiPath.hasPath &&
               !aiPath.reachedEndOfPath &&
               !aiPath.pathPending &&
               velocityMagnitude > 0.1f &&
               (npcController == null || !npcController.HasArrived()); // Controlla anche il controller
    }

    private bool ShouldStopTimer(float velocityMagnitude, bool reachedEndOfPath)
    {
        // Metodo 1: Usa la logica del controller se disponibile
        if (npcController != null && npcController.HasArrived())
        {
            return true;
        }

        // Metodo 2: Logica tradizionale con miglioramenti
        bool hasReachedEnd = reachedEndOfPath ||
                            (aiPath.hasPath && !aiPath.pathPending &&
                             Vector3.Distance(transform.position, aiPath.destination) < aiPath.endReachedDistance);

        bool hasStopped = velocityMagnitude < 0.05f;

        // Metodo 3: Controlla anche se canMove è stato disabilitato
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

    // Metodo chiamato dal controller quando arriva alla destinazione
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