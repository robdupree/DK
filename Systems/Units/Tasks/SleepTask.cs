using UnityEngine;
using DK;

namespace DK.Tasks
{
    public class SleepTask : ITask
    {
        private Vector3 bedPos;
        private float timer;

        public SleepTask(Vector3 bedPosition, float duration)
        {
            bedPos = bedPosition;
            timer = duration;
        }

        public void OnEnter(UnitAI ai) => ai.MoveTo(bedPos);

        public bool UpdateTask(UnitAI ai)
        {
            if (!ai.IsAtDestination()) return false;
            timer -= Time.deltaTime;
            return timer <= 0f;
        }

        public void OnExit(UnitAI ai) { }
    }
}