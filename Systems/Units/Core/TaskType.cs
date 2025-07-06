// TaskType.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/TaskType")]
public class TaskType : ScriptableObject
{
    public string taskName;
    public float baseDifficulty = 1f;
    public SkillType requiredSkill;
    public int basePriority = 1;
}

public enum SkillType { Digging, Conquering, Fighting }  // passe an
