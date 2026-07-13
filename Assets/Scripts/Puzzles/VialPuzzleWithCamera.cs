using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class VialPuzzleWithCamera : MonoBehaviour
{
    [Header("Порядок колб")]
    public List<string> correctOrder = new List<string> { "Blue", "Green", "Red" };
    private List<string> playerOrder = new List<string>();

    [Header("Камера (плавное движение)")]
    public Camera mainCamera;
    public Transform puzzleCameraPosition; // Объект с позицией для крупного плана
    public Transform puzzleCameraTarget;   // Объект, на который смотреть (опционально)
    public float cameraMoveSpeed = 2f;

    private Vector3 originalPos;
    private Quaternion originalRot;

    [Header("UI")]
    public TextMeshProUGUI feedbackText;
    public GameObject uiPanel;

    [Header("Дверь")]
    public GameObject doorToOpen;

    [Header("Звуки")]
    public AudioSource audioSource;
    public AudioClip successSound;
    public AudioClip failSound;
    public AudioClip clickSound;

    private bool isPuzzleSolved = false;
    private bool isPlayerNear = false;
    private bool isCameraMoving = false;

    void Start()
    {
        originalPos = mainCamera.transform.position;
        originalRot = mainCamera.transform.rotation;

        if (uiPanel != null) uiPanel.SetActive(false);
        if (feedbackText != null) feedbackText.text = "Найди правильный порядок: Синяя → Зелёная → Красная";
    }

    void Update()
    {
        if (isPlayerNear && !isPuzzleSolved && !isCameraMoving)
        {
            StartCoroutine(MoveToPuzzleCamera());
        }
    }

    // ---------- ОСНОВНАЯ ЛОГИКА ----------
    public void PressVial(string vialColor)
    {
        if (isPuzzleSolved) return;

        if (audioSource && clickSound) audioSource.PlayOneShot(clickSound);
        playerOrder.Add(vialColor);
        UpdateUI($"Выбрано: {string.Join(" → ", playerOrder)}");

        if (playerOrder.Count > correctOrder.Count)
        {
            StartCoroutine(WrongOrderCoroutine());
            return;
        }

        for (int i = 0; i < playerOrder.Count; i++)
        {
            if (playerOrder[i] != correctOrder[i])
            {
                StartCoroutine(WrongOrderCoroutine());
                return;
            }
        }

        if (playerOrder.Count == correctOrder.Count)
        {
            StartCoroutine(RightOrderCoroutine());
        }
    }

    // ---------- КАМЕРА ----------
    IEnumerator MoveToPuzzleCamera()
    {
        isCameraMoving = true;
        float t = 0;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (t < 1)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            mainCamera.transform.position = Vector3.Lerp(startPos, puzzleCameraPosition.position, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, puzzleCameraPosition.rotation, t);
            yield return null;
        }

        isCameraMoving = false;
        if (uiPanel != null) uiPanel.SetActive(true);
    }

    IEnumerator ReturnToMainCamera()
    {
        isCameraMoving = true;
        float t = 0;
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        while (t < 1)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            mainCamera.transform.position = Vector3.Lerp(startPos, originalPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, originalRot, t);
            yield return null;
        }

        isCameraMoving = false;
        if (uiPanel != null) uiPanel.SetActive(false);
    }

    // ---------- ПРОВЕРКИ ----------
    IEnumerator WrongOrderCoroutine()
    {
        if (audioSource && failSound) audioSource.PlayOneShot(failSound);
        UpdateUI("❌ Неправильно! Начни заново.");
        playerOrder.Clear();

        yield return new WaitForSeconds(1.5f);
        UpdateUI("Попробуй снова: Синяя → Зелёная → Красная");
    }

    IEnumerator RightOrderCoroutine()
    {
        isPuzzleSolved = true;
        if (audioSource && successSound) audioSource.PlayOneShot(successSound);
        UpdateUI("✅ Верно! Дверь открывается...");

        // Анимация двери
        if (doorToOpen != null)
            StartCoroutine(OpenDoorAnimation());

        yield return new WaitForSeconds(2f);
        StartCoroutine(ReturnToMainCamera());
    }

    IEnumerator OpenDoorAnimation()
    {
        Vector3 startPos = doorToOpen.transform.position;
        Vector3 endPos = startPos + Vector3.up * 3f;
        float duration = 1.5f;
        float t = 0;

        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            float eased = 1 - Mathf.Pow(1 - progress, 3);
            doorToOpen.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }
    }

    // ---------- UI ----------
    void UpdateUI(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    // ---------- ДЛЯ ТРИГГЕРА ----------
    public void SetPlayerNear(bool isNear)
    {
        isPlayerNear = isNear;
        if (!isNear && !isPuzzleSolved && !isCameraMoving)
        {
            StartCoroutine(ReturnToMainCamera());
            UpdateUI("Вернись к колбам, чтобы продолжить.");
            playerOrder.Clear();
        }
    }

    public bool IsPuzzleSolved()
    {
        return isPuzzleSolved;
    }
}
