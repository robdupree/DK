using DK;

public interface ITask
{
    /// <summary>
    /// Called once when the task starts.
    /// </summary>
    void OnEnter(UnitAI ai);

    /// <summary>
    /// Called each Update. Return true when the task is complete.
    /// </summary>
    bool UpdateTask(UnitAI ai);

    /// <summary>
    /// Called once when the task ends or is aborted.
    /// </summary>
    void OnExit(UnitAI ai);
}