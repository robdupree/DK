// GameModeManager.cs
using UnityEngine;

public enum GameMode
{
    Default,
    RoomPlacing,
    DigSelection
}

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }
    public GameMode CurrentMode { get; private set; } = GameMode.Default;

    private int currentGold = 0;

    /// <summary>
    /// Fügt dem Spielerkonto Gold hinzu und loggt den neuen Gesamtwert.
    /// </summary>
    public void AddGold(int amount)
    {
        currentGold += amount;
        Debug.Log($"[GameModeManager] AddGold({amount}) aufgerufen. Neuer PlayerGold = {currentGold}");
    }

    /// <summary>
    /// Der aktuelle Gold-Vorrat des Spielers.
    /// </summary>
    public int PlayerGold => currentGold;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    public void SetMode(GameMode mode)
    {
        CurrentMode = mode;
        Debug.Log("Game Mode: " + mode);
    }
}
