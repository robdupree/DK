using UnityEngine;
using DK;

namespace DK.Tasks
{
    public class WanderTask : ITask
    {
        private Vector3 targetPosition;
        private bool hasArrived = false;
        private float stuckTimer = 0f;
        private const float MAX_STUCK_TIME = 3f;
        private Vector3 lastPosition;
        private bool hasStartedMoving = false; // NEUE VARIABLE

        public WanderTask(Vector3 target)
        {
            targetPosition = target;
        }

        public void OnEnter(UnitAI ai)
        {
            if (ai == null) return;

            ai.MoveTo(targetPosition);
            lastPosition = ai.transform.position;
            hasStartedMoving = false; // Reset
        }

        public bool UpdateTask(UnitAI ai)
        {
            if (ai == null) return true;

            float distanceToTarget = Vector3.Distance(ai.transform.position, targetPosition);

            // Prüfe ob Bewegung gestartet wurde
            if (!hasStartedMoving && ai.Agent.velocity.sqrMagnitude > 0.1f)
            {
                hasStartedMoving = true;
            }

            // Ankunfts-Check: Nur wenn tatsächlich angekommen UND sich nicht mehr bewegt
            if (distanceToTarget < 1.5f || ai.HasTrulyArrived())
            {
                // ZUSÄTZLICHE PRÜFUNG: Ist Imp wirklich zum Stillstand gekommen?
                bool hasStoppedMoving = ai.Agent.velocity.sqrMagnitude < 0.05f;

                if (hasStoppedMoving || !hasStartedMoving)
                {
                    hasArrived = true;

                    // WICHTIG: Animation sofort korrigieren
                    if (ai.animator != null)
                    {
                        ai.animator.SetFloat("Speed", 0f);
                    }

                    return true;
                }
            }

            // Stuck-Detection nur wenn sich bewegen sollte
            if (hasStartedMoving)
            {
                float distanceMoved = Vector3.Distance(ai.transform.position, lastPosition);
                if (distanceMoved < 0.1f)
                {
                    stuckTimer += Time.deltaTime;
                    if (stuckTimer > MAX_STUCK_TIME)
                    {
                        // Animation stoppen vor Task-Ende
                        if (ai.animator != null)
                        {
                            ai.animator.SetFloat("Speed", 0f);
                        }
                        return true; // Beende Task wenn stuck
                    }
                }
                else
                {
                    stuckTimer = 0f;
                    lastPosition = ai.transform.position;
                }
            }

            return false;
        }

        public void OnExit(UnitAI ai)
        {
            // WICHTIG: Stelle sicher dass Speed-Parameter auf 0 gesetzt wird
            if (ai?.animator != null)
            {
                ai.animator.SetFloat("Speed", 0f);
            }
        }
    }
}