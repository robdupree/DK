// Datei: FactionMember.cs
using UnityEngine;


[DisallowMultipleComponent]
public class FactionMember : MonoBehaviour
{
    [Tooltip("Welche Fraktion ist diese Einheit?")]
    public Faction faction = Faction.Neutral;
}
