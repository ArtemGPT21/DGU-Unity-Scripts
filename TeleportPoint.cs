using UnityEngine;

public class TeleportPoint : MonoBehaviour
{
    [Header("Настройки телепортации")]
    [Tooltip("Куда телепортировать игрока")]
    public Transform destination;

    [Tooltip("Сохранять текущий поворот объекта")]
    public bool keepRotation = true;

    [Tooltip("Тег объекта, который может телепортироваться")]
    public string targetTag = "Player";

    [Header("Настройки подбрасывания")]
    [Tooltip("Вертикальная скорость при телепортации (единиц/сек)")]
    [Range(0f, 15f)]
    public float bounceVelocity = 3f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;

        if (destination == null)
        {
            Debug.LogWarning($"Телепорт {gameObject.name}: не задан destination!", this);
            return;
        }

        CharacterController cc = other.GetComponent<CharacterController>();

        // === БЕЗОПАСНАЯ ТЕЛЕПОРТАЦИЯ ДЛЯ CHARACTERCONTROLLER ===
        if (cc != null)
        {
            // 1. Отключаем контроллер для сброса внутренних коллизий
            cc.enabled = false;

            // 2. Перемещаем трансформ напрямую
            other.transform.position = destination.position;

            // 3. Включаем контроллер обратно
            cc.enabled = true;

            // 4. Подбрасывание: устанавливаем вертикальную скорость
            // ⚠️ ВАЖНО: Замените SetVerticalVelocity на метод из вашего скрипта движения!
            PlayerController1 movement = other.GetComponent<PlayerController1>();
            if (movement != null)
            {
                movement.SetVerticalVelocity(bounceVelocity);
            }
            else
            {
                Debug.LogWarning(
                    "TeleportPoint: Не найден скрипт PlayerMovement с методом SetVerticalVelocity. " +
                    "Подбрасывание не применено.", this);
            }
        }
        else
        {
            // Fallback для объектов без CharacterController
            other.transform.position = destination.position;
        }

        // Поворот
        if (!keepRotation)
        {
            other.transform.rotation = destination.rotation;
        }
    }
}
