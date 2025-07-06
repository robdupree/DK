// TileState.cs
public enum TileState
{
    Wall_Intact,
    Wall_Marked,
    Wall_BeingDug,

    Floor_Dug,

    Room_Treasury,
    Room_Lair,
    Room_Training,

    Wall_Gold,
    Wall_JewelVein,
    Wall_Rock,

    Floor_Neutral,    // neu: unbebaute Erde
    Floor_Conquered   // neu: unter Kontrolle
}
