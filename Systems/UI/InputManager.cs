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

        // Hier k�nnen Sie weitere Hotkeys hinzuf�gen...
    }
}