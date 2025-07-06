// GroundGold.cs
using UnityEngine;

public class GroundGold : MonoBehaviour
{
    public int amount;
    public TextMesh amountText; // Zählt das Gold visuell (z.B. 10, 20, …)

    public void SetAmount(int a)
    {
        amount = a;
        if (amountText != null)
            amountText.text = amount.ToString();
    }
}
