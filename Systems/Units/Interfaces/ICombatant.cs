// ICombatant.cs
public interface ICombatant
{
    Faction MyFaction { get; }
    void TakeDamage(int amount);
    bool IsAlive { get; }
}
