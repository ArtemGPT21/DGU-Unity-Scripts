using UnityEngine;

public class VialTrigger : MonoBehaviour
{
    public string vialColor; // Blue, Green, Red
    public VialPuzzleWithCamera puzzleManager;

    [Header("Подсветка")]
    public Material highlightMaterial;
    private Material originalMaterial;
    private Renderer rend;

    private bool isPlayerInside = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalMaterial = rend.material;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = true;
            puzzleManager.SetPlayerNear(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            puzzleManager.SetPlayerNear(false);
            // Сбрасываем подсветку при выходе
            if (rend != null)
                rend.material = originalMaterial;
        }
    }

    void OnMouseEnter()
    {
        if (isPlayerInside && !puzzleManager.IsPuzzleSolved() && rend != null)
            rend.material = highlightMaterial;
    }

    void OnMouseExit()
    {
        if (rend != null && !puzzleManager.IsPuzzleSolved())
            rend.material = originalMaterial;
    }

    void OnMouseDown()
    {
        if (puzzleManager != null && isPlayerInside)
        {
            puzzleManager.PressVial(vialColor);
        }
        else if (!isPlayerInside)
        {
            Debug.Log("Подойди поближе к колбам!");
        }
    }
}
