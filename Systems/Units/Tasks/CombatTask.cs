using UnityEngine;
using DK;

namespace DK.Tasks
{
    public class CombatTask : ITask
    {
        private Transform target;
        private float range, cooldown, timer;

        public CombatTask(Transform enemy, float range = 2f, float cooldown = 1f)
        {
            target = enemy;
            this.range = range;
            this.cooldown = cooldown;
            timer = 0f;
        }

        public void OnEnter(UnitAI ai) { }

        public bool UpdateTask(UnitAI ai)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return true;
            float d = Vector3.Distance(ai.transform.position, target.position);
            if (d > range)
            {
                ai.MoveTo(target.position);
                return false;
            }
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                // TODO: implement actual attack/damage logic here
                timer = cooldown;
            }
            return false;
        }

        public void OnExit(UnitAI ai) { }
    }
}
