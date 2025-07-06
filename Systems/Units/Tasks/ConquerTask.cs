using UnityEngine;
using DK.Tasks;
using UnityObject = UnityEngine.Object;

namespace DK.Tasks
{
    public class ConquerTask : ITask
    {
        private readonly Task dataTask;
        private readonly float tickInterval = 0.5f; // optional: Angriffstakt
        private float tickTimer;
        private Vector3Int tilePos;
        private Vector3 worldPos;
        private bool hasArrived;

        public ConquerTask(Task taskData, float duration)
        {
            dataTask = taskData;
            tilePos = new Vector3Int(taskData.location.x, taskData.location.y, 0);
            tickTimer = tickInterval;
            hasArrived = false;
        }

        public void OnEnter(DK.UnitAI ai)
        {
            if (ai.tileController == null && DigManager.Instance != null)
                ai.tileController = DigManager.Instance.tileController;

            var manager = Object.FindFirstObjectByType<ConquestManager>();
            if (manager == null || !manager.TryReserveTile(ai.gameObject, out var reservedPos))
            {
                TaskManager.Instance.CompleteTask(dataTask);
                return;
            }

            tilePos = reservedPos;
            worldPos = ai.tileController.tilemap.GetCellCenterWorld(tilePos);
            ai.MoveTo(worldPos);
        }

        public bool UpdateTask(DK.UnitAI ai)
        {
            var manager = Object.FindFirstObjectByType<ConquestManager>();
            if (manager == null)
                return true;

            if (!hasArrived)
            {
                ai.MoveTo(worldPos);
                if (!ai.IsAtDestination())
                    return false;

                hasArrived = true;
                return false;
            }

            tickTimer -= Time.deltaTime;
            if (tickTimer > 0f)
                return false;
            tickTimer = tickInterval;

            if (!DigManager.Instance.tileMap.TryGetValue(tilePos, out var tile))
                return true;

            bool destroyed = tile.ReduceHealth();
            if (destroyed)
            {
                tile.State = TileState.Floor_Conquered;
                ai.tileController.tilemap.SetTile(tilePos, manager.floorTile);
                manager.FinishReservation(tilePos);
                TaskManager.Instance.CompleteTask(dataTask);
                return true;
            }

            return false;
        }

        public void OnExit(DK.UnitAI ai) { }
    }
}
