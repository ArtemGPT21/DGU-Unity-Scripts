using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController1 : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 6.5f;
    public float crouchSpeed = 2f;

    [Header("Mouse")]
    public float mouseSensitivity = 2f;
    public Transform playerCamera;

    [Header("Crouch")]
    public float standingHeight = 1.8f;
    public float crouchingHeight = 1.0f;

    [Header("Flashlight")]
    public Light flashlight;
    public AudioSource flashlightAudio;
    public AudioClip flashlightOn;
    public AudioClip flashlightOff;

    [Header("Gravity & Jump")]
    public float gravity = -20f;
    public float jumpHeight = 0.4f;

    private CharacterController controller;
    private float verticalVelocity; // ✅ Единая переменная для вертикальной скорости
    private float xRotation;
    private bool isCrouching = false;
    private bool isNoclipping = false; // ✅ Флаг для корректного Noclip

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        controller.height = standingHeight;
    }

    void Update()
    {
        Look();
        Move();
        HandleCrouch();
        HandleFlashlight();
        HandleNoclip();
        // ✅ Убран несуществующий вызов SetVerticalVelocity из Update
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void Move()
    {
        float speed = walkSpeed;

        if (Input.GetKey(KeyCode.LeftControl))
            speed = crouchSpeed;
        else if (Input.GetKey(KeyCode.LeftShift))
            speed = sprintSpeed;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        // Гравитация
        if (controller.isGrounded && verticalVelocity < 0)
            verticalVelocity = -2f;

        // Прыжок
        if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity; // ✅ Используем единую вертикальную скорость

        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>
    /// Вызывается извне (например, TeleportPoint) для установки вертикального импульса.
    /// </summary>
    public void SetVerticalVelocity(float verticalSpeed)
    {
        // ✅ Записываем именно в ту переменную, которая используется в Move()
        verticalVelocity = verticalSpeed;
    }

    void HandleNoclip()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            isNoclipping = !isNoclipping;
            controller.enabled = !isNoclipping; // ✅ Переключаем только при нажатии
        }

        if (isNoclipping)
        {
            transform.position += transform.forward * 1800f * Time.deltaTime;
        }
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
            controller.height = isCrouching ? crouchingHeight : standingHeight;

            // ✅ Корректируем позицию при приседании, чтобы не проваливаться/взлетать
            float heightDiff = standingHeight - crouchingHeight;
            if (isCrouching)
                transform.position -= Vector3.up * (heightDiff * 0.5f);
            else
                transform.position += Vector3.up * (heightDiff * 0.5f);
        }
    }

    void HandleFlashlight()
    {
        if (flashlight == null || flashlightAudio == null)
            return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            flashlight.enabled = !flashlight.enabled;

            if (flashlight.enabled)
                flashlightAudio.PlayOneShot(flashlightOn);
            else
                flashlightAudio.PlayOneShot(flashlightOff);
        }
    }
}
