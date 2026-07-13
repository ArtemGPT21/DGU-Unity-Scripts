using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [Header("Настройки предмета")]
    public string itemName = "Карта";           // Название предмета
    public string description = "Старая карта"; // Описание (для записок)
    public KeyCode pickupKey = KeyCode.E;
    public float pickupDistance = 2.5f;

    [Header("UI подсказка (опционально)")]
    public GameObject hintObject; // Текст "Нажмите E"

    private GameObject player;
    private InventorySystem inventory;
    private bool isPickedUp = false;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            inventory = player.GetComponent<InventorySystem>();
        }
        else
        {
            Debug.LogError("Не найден объект с тегом 'Player'!");
        }

        if (hintObject != null) hintObject.SetActive(false);
    }

    void Update()
    {
        if (isPickedUp || player == null || inventory == null) return;

        float distance = Vector3.Distance(transform.position, player.transform.position);

        // Показываем/скрываем подсказку
        if (hintObject != null)
        {
            hintObject.SetActive(distance <= pickupDistance);
        }

        // Подбор на E
        if (distance <= pickupDistance && Input.GetKeyDown(pickupKey))
        {
            PickUp();
        }
    }

    void PickUp()
    {
        inventory.AddItem(itemName);
        Debug.Log("📜 " + itemName + ": " + description);
        isPickedUp = true;
        Destroy(gameObject);
    }
}
