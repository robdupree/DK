using UnityEngine;
using DK;

namespace DK.Tasks
{
    public class IdleTask : ITask
    {
        private float timer;
        private float animationSwitchTimer;
        private float animationSwitchInterval;
        private string currentIdleAnimation = "";
        private bool hasAnimator = false;
        private UnitAI unitAI;
        private bool wasInterrupted = false; // NEUE VARIABLE

        public IdleTask(float duration)
        {
            timer = duration;
            animationSwitchTimer = 0f;
            wasInterrupted = false;
        }

        public void OnEnter(UnitAI ai)
        {
            unitAI = ai;
            hasAnimator = ai.animator != null;
            wasInterrupted = false;

            if (hasAnimator)
            {
                // Setze Bewegungsgeschwindigkeit auf 0
                ai.animator.SetFloat("Speed", 0f);

                // Setze ersten Animationswechsel-Timer
                animationSwitchInterval = ai.GetRandomAnimationSwitchTime();
                animationSwitchTimer = animationSwitchInterval;

                // Aktiviere erste Idle-Animation
                SwitchToRandomIdleAnimation(ai);

                if (ai.debugIdleAnimations)
                {
                    Debug.Log($"[IdleTask] {ai.name} startet Idle mit Animation: {currentIdleAnimation} " +
                             $"(nächster Wechsel in {animationSwitchInterval:F1}s)");
                }
            }
        }

        public bool UpdateTask(UnitAI ai)
        {
            // NEUE PRÜFUNG: Wurde Task unterbrochen für wichtigere Arbeit?
            if (HasHigherPriorityTask(ai))
            {
                wasInterrupted = true;
                Debug.Log($"[IdleTask] {ai.name} Idle-Task unterbrochen für wichtigere Arbeit");
                return true; // Beende IdleTask sofort
            }

            timer -= Time.deltaTime;

            // Nur Animationswechsel wenn nicht unterbrochen und Animator vorhanden
            if (hasAnimator && !wasInterrupted)
            {
                HandleAnimationSwitching(ai);
            }

            return timer <= 0f;
        }

        /// <summary>
        /// Prüft ob wichtigere Tasks verfügbar sind
        /// </summary>
        private bool HasHigherPriorityTask(UnitAI ai)
        {
            // Prüfe auf High-Priority Tasks
            if (ai.highPriority.Count > 0)
            {
                return true;
            }

            // Prüfe auf Normal-Priority Tasks (Dig, Conquer, etc.)
            if (ai.normalPriority.Count > 0)
            {
                return true;
            }

            return false;
        }

        private void HandleAnimationSwitching(UnitAI ai)
        {
            animationSwitchTimer -= Time.deltaTime;

            if (animationSwitchTimer <= 0f)
            {
                // Wechsle zu neuer Idle-Animation
                SwitchToRandomIdleAnimation(ai);

                // Setze neues zufälliges Intervall
                animationSwitchInterval = ai.GetRandomAnimationSwitchTime();
                animationSwitchTimer = animationSwitchInterval;

                if (ai.debugIdleAnimations)
                {
                    Debug.Log($"[IdleTask] {ai.name} Animation-Wechsel: {currentIdleAnimation} " +
                             $"(nächster Wechsel in {animationSwitchInterval:F1}s)");
                }
            }
        }

        private void SwitchToRandomIdleAnimation(UnitAI ai)
        {
            string newAnimation = ai.GetRandomIdleAnimation(currentIdleAnimation);

            if (newAnimation != currentIdleAnimation)
            {
                currentIdleAnimation = newAnimation;
                SetIdleAnimation(ai, currentIdleAnimation);
            }
        }

        private void SetIdleAnimation(UnitAI ai, string animationName)
        {
            if (!hasAnimator || string.IsNullOrEmpty(animationName) || wasInterrupted)
                return;

            // Prüfe ob Animation verfügbar ist
            if (!ai.HasIdleAnimation(animationName))
            {
                if (ai.debugIdleAnimations)
                {
                    Debug.LogWarning($"[IdleTask] Animation '{animationName}' nicht gefunden für {ai.name}");
                }
                return;
            }

            // Aktiviere die gewünschte Animation
            if (HasAnimatorParameter(ai.animator, animationName, AnimatorControllerParameterType.Trigger))
            {
                ai.animator.SetTrigger(animationName);

                if (ai.debugIdleAnimations)
                {
                    Debug.Log($"[IdleTask] {ai.name} Trigger gesetzt: {animationName}");
                }
            }
            else if (HasAnimatorParameter(ai.animator, animationName, AnimatorControllerParameterType.Bool))
            {
                // Für Bools: Erst alle deaktivieren, dann gewünschte aktivieren
                foreach (string idleAnim in ai.availableIdleAnimations)
                {
                    if (HasAnimatorParameter(ai.animator, idleAnim, AnimatorControllerParameterType.Bool))
                    {
                        ai.animator.SetBool(idleAnim, false);
                    }
                }
                ai.animator.SetBool(animationName, true);

                if (ai.debugIdleAnimations)
                {
                    Debug.Log($"[IdleTask] {ai.name} Bool aktiviert: {animationName}");
                }
            }

            // Integer-basierte Idle-States
            if (HasAnimatorParameter(ai.animator, "IdleIndex", AnimatorControllerParameterType.Int))
            {
                int animIndex = System.Array.IndexOf(ai.availableIdleAnimations, animationName);
                if (animIndex >= 0)
                {
                    ai.animator.SetInteger("IdleIndex", animIndex);

                    if (ai.debugIdleAnimations)
                    {
                        Debug.Log($"[IdleTask] {ai.name} IdleIndex gesetzt: {animIndex} für {animationName}");
                    }
                }
            }
        }

        private bool HasAnimatorParameter(Animator animator, string paramName, AnimatorControllerParameterType paramType)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == paramName && param.type == paramType)
                    return true;
            }
            return false;
        }

        public void OnExit(UnitAI ai)
        {
            if (hasAnimator)
            {
                // AGGRESSIVES Cleanup aller Idle-Animationen
                StopAllIdleAnimations(ai);

                if (ai.debugIdleAnimations)
                {
                    Debug.Log($"[IdleTask] {ai.name} beendet Idle-Animationen" +
                             (wasInterrupted ? " (unterbrochen)" : " (regulär)"));
                }
            }
        }

        /// <summary>
        /// Stoppt ALLE Idle-Animationen aggressiv
        /// </summary>
        private void StopAllIdleAnimations(UnitAI ai)
        {
            // Bool-Parameter deaktivieren
            foreach (string idleAnim in ai.availableIdleAnimations)
            {
                if (HasAnimatorParameter(ai.animator, idleAnim, AnimatorControllerParameterType.Bool))
                {
                    ai.animator.SetBool(idleAnim, false);
                }
            }

            // Standard Parameter zurücksetzen
            if (HasAnimatorParameter(ai.animator, "IsIdle", AnimatorControllerParameterType.Bool))
            {
                ai.animator.SetBool("IsIdle", false);
            }

            if (HasAnimatorParameter(ai.animator, "IdleIndex", AnimatorControllerParameterType.Int))
            {
                ai.animator.SetInteger("IdleIndex", 0);
            }

            // WICHTIG: Setze Speed auf 0 um sicherzustellen dass nicht gelaufen wird
            ai.animator.SetFloat("Speed", 0f);

            // Forciere Update der Animator-Parameter
            ai.animator.Update(0f);
        }
    }
}