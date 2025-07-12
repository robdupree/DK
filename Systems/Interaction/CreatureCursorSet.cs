using UnityEngine;

/// <summary>
/// Verwaltet die verschiedenen Cursor-States für Kreatur-Interaktionen
/// </summary>
[CreateAssetMenu(fileName = "CreatureCursorSet", menuName = "Dungeon/Cursor Set")]
public class CreatureCursorSet : ScriptableObject
{
    [Header("Cursor Textures")]
    [Tooltip("Standard-Cursor")]
    public Texture2D defaultCursor;

    [Tooltip("Hand-Cursor (Hover über Kreatur)")]
    public Texture2D handOpenCursor;

    [Tooltip("Greifende Hand (Kreatur wird gehalten)")]
    public Texture2D handGrabCursor;

    [Tooltip("Schlag-Hand")]
    public Texture2D handSlapCursor;

    [Tooltip("Zeigefinger (für UI)")]
    public Texture2D pointerCursor;

    [Header("Cursor Hotspots")]
    [Tooltip("Hotspot für Default-Cursor")]
    public Vector2 defaultHotspot = Vector2.zero;

    [Tooltip("Hotspot für Hand-Cursor")]
    public Vector2 handHotspot = new Vector2(16, 16);

    [Tooltip("Hotspot für Grab-Cursor")]
    public Vector2 grabHotspot = new Vector2(16, 16);

    [Tooltip("Hotspot für Slap-Cursor")]
    public Vector2 slapHotspot = new Vector2(16, 16);

    [Tooltip("Hotspot für Pointer-Cursor")]
    public Vector2 pointerHotspot = new Vector2(8, 8);

    [Header("Cursor Animations")]
    [Tooltip("Animierte Cursor-Frames für Grab")]
    public Texture2D[] grabAnimationFrames;

    [Tooltip("Animations-Geschwindigkeit (Frames pro Sekunde)")]
    public float animationSpeed = 10f;
}