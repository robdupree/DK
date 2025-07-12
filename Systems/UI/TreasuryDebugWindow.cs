// TreasuryDebugWindow.cs
using System.Linq;
using UnityEngine;

public class TreasuryDebugWindow : MonoBehaviour
{
    private Rect windowRect = new Rect(10, 10, 250, 100);

    void OnGUI()
    {
        windowRect = GUI.Window(0, windowRect, DrawWindow, "Schatzkammer-Status");
    }

    private void DrawWindow(int windowID)
    {
        if (DigManager.Instance == null || DigManager.Instance.tileController == null)
        {
            GUILayout.Label("DigManager oder TileController nicht gefunden");
            GUI.DragWindow();
            return;
        }

        // Wir zählen nur die Anzahl der Treasury-Tiles und deren Gesamtkapazität
        int treasuryTileCount = 0;
        int totalCapacity = 0;
        foreach (var kv in DigManager.Instance.tileMap)
        {
            if (kv.Value.State == TileState.Room_Treasury)
            {
                treasuryTileCount++;
                totalCapacity += 50;
            }
        }

        GUILayout.Label($"Anzahl Treasury-Tiles: {treasuryTileCount}");
        GUILayout.Label($"Gesamtkapazität: {totalCapacity}");

        // Jetzt liest das Fenster den aktuellen Player-Gold-Stand aus:
        int playerGold = GameModeManager.Instance.PlayerGold;
        GUILayout.Label($"Aktuell belegt (Player): {playerGold}");

        GUI.DragWindow();
    }
}
