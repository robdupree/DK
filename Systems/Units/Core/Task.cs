using UnityEngine;

[System.Serializable]
public class Task
{
    public TaskType type;
    public Vector2Int location;
    public bool isAssigned = false;
    public bool isCompleted = false;
    public float progress = 0f;

    public GameObject assignedAgent; // optional für Validierung
}
