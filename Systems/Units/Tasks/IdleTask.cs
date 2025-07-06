using UnityEngine;
using DK;

namespace DK.Tasks
{
    public class IdleTask : ITask
    {
        private float timer;
        public IdleTask(float duration) { timer = duration; }

        public void OnEnter(UnitAI ai) { }

        public bool UpdateTask(UnitAI ai)
        {
            timer -= Time.deltaTime;
            return timer <= 0f;
        }

        public void OnExit(UnitAI ai) { }
    }
}