using UnityEngine;

public class InputManager : MonoBehaviour
{
    void Update()
    {
        // Health Display Toggle
        if (Input.GetKeyDown(KeyCode.H))
        {
            DungeonTileData.ToggleGlobalHealthDisplay();
        }

        // Hier können Sie weitere Hotkeys hinzufügen...
    }
}