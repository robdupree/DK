// GoldPile.cs
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoldPile : MonoBehaviour
{
    private const int MaxGold = 50;
    private const int Steps = 5;
    private int currentGold = 0;

    [Tooltip("Referenz zu dem 3D-Visual, das skaliert werden soll (z.B. eine Goldklumpen-Mesh)")]
    public Transform pileVisual;

    [Tooltip("Optionale TextMesh für die Anzeige der Menge (3D-Text), falls gewünscht")]
    public TextMesh amountText;

    private Vector3 originalScale;

    void Awake()
    {
        if (pileVisual == null)
        {
            Debug.LogError("GoldPile: 'pileVisual' nicht gesetzt!");
            return;
        }
        originalScale = pileVisual.localScale;
        UpdateVisual();
    }

    /// <summary>
    /// Fügt Gold in den Haufen ein. Bis maximal MaxGold. Gibt zurück, wie viel tatsächlich aufgenommen wurde.
    /// </summary>
    public int AddGold(int amount)
    {
        if (currentGold >= MaxGold)
        {
            Debug.Log($"[GoldPile] AddGold({amount}) auf GameObject {name}: bereits voll (currentGold={currentGold})", this);
            return 0;
        }

        int spaceLeft = MaxGold - currentGold;
        int toAdd = Mathf.Min(spaceLeft, amount);
        currentGold += toAdd;
        Debug.Log($"[GoldPile] AddGold({amount}) auf {name}: tatsächlich hinzugefügt={toAdd}, neuer currentGold={currentGold}", this);
        UpdateVisual();
        return toAdd;
    }


    /// <summary>
    /// Gibt true zurück, wenn dieser Haufen bereits MaxGold erreicht hat.
    /// </summary>
    public bool IsFull()
    {
        return currentGold >= MaxGold;
    }

    /// <summary>
    /// Öffentlich zugängliche Eigenschaft, um den aktuell im Haufen gespeicherten Goldwert auszulesen.
    /// </summary>
    public int CurrentGold => currentGold;

    private void UpdateVisual()
    {
        if (pileVisual == null)
            return;

        if (currentGold <= 0)
        {
            pileVisual.gameObject.SetActive(false);
            if (amountText != null) amountText.text = "";
            return;
        }

        pileVisual.gameObject.SetActive(true);

        int stepIndex = (currentGold - 1) / (MaxGold / Steps);
        stepIndex = Mathf.Clamp(stepIndex, 0, Steps - 1);

        float scaleFactor = (stepIndex + 1) / (float)Steps;
        pileVisual.localScale = originalScale * scaleFactor;

        if (amountText != null)
            amountText.text = currentGold.ToString();
    }
}
