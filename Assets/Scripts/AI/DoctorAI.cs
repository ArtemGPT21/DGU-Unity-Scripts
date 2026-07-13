using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class DoctorAI : MonoBehaviour
{
    // ============ ССЫЛКИ ============
    [Header("Игрок")]
    [SerializeField] private Transform player;
    [SerializeField] private PlayerState playerState;

    [Header("Параметры")]
    [SerializeField] private float chaseSpeed = 9f;
    [SerializeField] private float patrolSpeed = 2.0f;
    [SerializeField] private float killDistance = 1.5f;
    [SerializeField] private float viewDistance = 25f;
    [SerializeField] private float wanderRadius = 15f;
    [SerializeField] private float wanderInterval = 5f;

    [Header("Потеря игрока из виду (защита от дёрганого Chase)")]
    [SerializeField] private float loseSightGracePeriod = 1.2f; // сколько секунд ждать, прежде чем сбросить Chase

    [Header("Телепортация (когда игрок подошёл вплотную)")]
    [SerializeField] private float teleportDistance = 3f;
    [SerializeField] private float teleportCooldown = 10f;
    [SerializeField] private float vanishDuration = 0.8f;
    [SerializeField] private float behindPlayerOffset = 4f;
    [SerializeField] private float teleportRandomRadius = 25f;
    [SerializeField] private float chanceToAppearBehind = 0.5f;

    [Header("Защита от застревания")]
    [SerializeField] private float stuckThreshold = 2.5f;      // сколько секунд считать "застрявшим"
    [SerializeField] private float stuckMinSpeed = 0.3f;       // если скорость ниже — считаем, что стоим
    [SerializeField] private float unstuckSearchRadius = 5f;   // радиус поиска свободной точки
    [SerializeField] private int maxUnstuckAttempts = 3;       // максимум попыток выбраться до телепорта
    [SerializeField] private LayerMask obstacleMask = ~0;      // какие слои считаются "препятствием" при поиске свободной точки

    [Header("Эффекты телепортации")]
    [SerializeField] private AudioClip teleportSound;
    [SerializeField] private AudioClip appearSound;
    [SerializeField] private GameObject vanishParticles;
    [SerializeField] private GameObject appearParticles;

    [Header("Звуки")]
    [SerializeField] private AudioClip[] randomSounds;
    [SerializeField] private float soundCooldownMin = 6f;
    [SerializeField] private float soundCooldownMax = 15f;
    [SerializeField] private float chaseSoundCooldown = 3f;

    [Header("UI: текст при появлении")]
    [SerializeField] private GameObject popupTextObject;
    [SerializeField] private string[] spawnTexts = { "Он здесь...", "Не оборачивайся...", "Доктор наблюдает..." };
    [SerializeField] private float popupDuration = 3f;

    // ============ ВНУТРЕННЕЕ ============
    private enum State { Patrol, Chase, Kill, Teleporting }
    private State currentState = State.Patrol;

    private NavMeshAgent agent;
    private AudioSource audioSource;
    private Renderer[] renderers;
    private Collider[] colliders;
    private float nextSoundTime;
    private float nextWanderTime;
    private float nextTeleportTime;
    private Vector3 wanderTarget;
    private bool hasWanderTarget;

    // Для детекта застревания
    private float stuckTimer;
    private Vector3 lastPosition;
    private int unstuckAttempts;

    // Для сглаживания потери видимости в Chase
    private float lastSeenTime;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        agent.speed = patrolSpeed;

        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        ShowSpawnText();
        PickNewWanderTarget();
        lastPosition = transform.position;
    }

    private void Update()
    {
        if (player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // === ПРОВЕРКА ТЕЛЕПОРТАЦИИ ===
        // Не даём телепорту стартовать, если уже почти убиваем игрока —
        // иначе можно "проскочить" State.Kill и потерять убийство.
        if (currentState == State.Patrol &&
            distToPlayer <= teleportDistance &&
            distToPlayer > killDistance &&
            Time.time >= nextTeleportTime)
        {
            StartCoroutine(TeleportRoutine());
            return;
        }

        if (currentState == State.Teleporting) return;

        // === ПРОВЕРКА ЗАСТРЯВАНИЯ ===
        CheckIfStuck();

        // === ОБЫЧНАЯ ЛОГИКА ===
        bool playerVisible = playerState != null &&
                             (playerState.IsStanding || playerState.IsFlashlightOn);

        bool canSeePlayerNow = playerVisible && distToPlayer < viewDistance && HasLineOfSight();

        if (canSeePlayerNow)
            lastSeenTime = Time.time;

        if (currentState != State.Kill)
        {
            if (distToPlayer <= killDistance && playerVisible)
            {
                currentState = State.Kill;
            }
            else if (canSeePlayerNow)
            {
                currentState = State.Chase;
            }
            else if (currentState == State.Chase)
            {
                // Не сбрасываем погоню мгновенно из-за одного "мигнувшего" кадра
                // рейкаста/видимости — даём grace period, иначе Chase дёргается.
                if (Time.time - lastSeenTime > loseSightGracePeriod)
                    currentState = State.Patrol;
                // иначе остаёмся в Chase ещё немного, доктор доходит до последней
                // известной точки игрока
            }
            else
            {
                currentState = State.Patrol;
            }
        }

        switch (currentState)
        {
            case State.Patrol:
                DoPatrol();
                agent.speed = patrolSpeed;
                TryPlayRandomSound(soundCooldownMin, soundCooldownMax);
                break;

            case State.Chase:
                DoChase();
                agent.speed = chaseSpeed;
                TryPlayRandomSound(chaseSoundCooldown, chaseSoundCooldown);
                if (distToPlayer <= killDistance && playerVisible)
                    currentState = State.Kill;
                break;

            case State.Kill:
                DoKill();
                break;
        }
    }

    // ======== ЗАЩИТА ОТ ЗАСТРЯВАНИЯ ========
    private void CheckIfStuck()
    {
        // Проверяем только если агент реально должен двигаться
        if (agent.isStopped || currentState == State.Teleporting || currentState == State.Kill)
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
            return;
        }

        float currentSpeed = agent.velocity.magnitude;
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        // Если скорость низкая И мы почти не двигаемся, И агент реально куда-то идёт
        bool hasDestination = !agent.pathPending && agent.hasPath;
        if (hasDestination && currentSpeed < stuckMinSpeed && distanceMoved < 0.1f)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckThreshold)
            {
                TryUnstuck();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            unstuckAttempts = 0;
        }

        lastPosition = transform.position;
    }

    private void TryUnstuck()
    {
        unstuckAttempts++;
        Debug.Log($"DoctorAI: застрял! Попытка выбраться #{unstuckAttempts}");

        if (unstuckAttempts >= maxUnstuckAttempts)
        {
            // Если не удалось выбраться — телепортируемся (если ещё не телепортируемся)
            if (currentState != State.Teleporting)
            {
                Debug.Log("DoctorAI: не удалось выбраться, телепортируемся");
                unstuckAttempts = 0;
                StartCoroutine(TeleportRoutine());
            }
            return;
        }

        // Ищем ближайшую свободную точку на NavMesh
        Vector3 freePoint = FindNearestFreePoint();
        if (freePoint != Vector3.zero)
        {
            agent.Warp(freePoint);
            Debug.Log("DoctorAI: варпнули на свободную точку");

            // Обновляем destination
            if (currentState == State.Chase && player != null)
                agent.SetDestination(player.position);
            else
                PickNewWanderTarget();
        }
        else
        {
            Debug.LogWarning("DoctorAI: не нашли свободную точку");
        }
    }

    private Vector3 FindNearestFreePoint()
    {
        // Ищем в радиусе вокруг текущей позиции
        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = Random.insideUnitSphere * unstuckSearchRadius;
            dir.y = 0;
            Vector3 candidate = transform.position + dir;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, unstuckSearchRadius, NavMesh.AllAreas))
            {
                // Проверяем, что точка не внутри препятствия (по заданной маске,
                // чтобы игрок/триггеры/сам доктор не считались "занятостью")
                if (!Physics.CheckSphere(hit.position, 0.5f, obstacleMask, QueryTriggerInteraction.Ignore))
                    return hit.position;
            }
        }

        // Если не нашли — пробуем варпнуть на ближайшую точку NavMesh
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit nearestHit, unstuckSearchRadius * 2f, NavMesh.AllAreas))
        {
            return nearestHit.position;
        }

        return Vector3.zero;
    }

    // ======== ТЕЛЕПОРТАЦИЯ ========
    private IEnumerator TeleportRoutine()
    {
        currentState = State.Teleporting;
        agent.isStopped = true;
        nextTeleportTime = Time.time + teleportCooldown;

        // 1. Эффект исчезновения
        PlaySound(teleportSound);
        SpawnParticles(vanishParticles, transform.position);

        // 2. Скрываем
        SetVisible(false);

        yield return new WaitForSeconds(vanishDuration);

        // 3. Выбираем новую точку
        Vector3 spawnPos = PickTeleportDestination();

        // 4. Телепортируем
        if (agent.Warp(spawnPos))
        {
            if (player != null)
            {
                Vector3 dirToPlayer = player.position - spawnPos;
                dirToPlayer.y = 0;
                if (dirToPlayer.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(dirToPlayer);
            }

            PlaySound(appearSound);
            SpawnParticles(appearParticles, spawnPos);
        }
        else
        {
            Debug.LogWarning("DoctorAI: не удалось варпнуть на NavMesh");
        }

        SetVisible(true);
        agent.isStopped = false;
        currentState = State.Patrol;
        unstuckAttempts = 0;
        stuckTimer = 0f;
        lastPosition = transform.position;
        PickNewWanderTarget();
    }

    private Vector3 PickTeleportDestination()
    {
        if (player == null)
            return PickRandomNavMeshPoint();

        if (Random.value < chanceToAppearBehind)
            return PickBehindPlayer();
        else
            return PickRandomNavMeshPoint();
    }

    private Vector3 PickBehindPlayer()
    {
        Vector3 behind = player.position - player.forward * behindPlayerOffset;
        if (NavMesh.SamplePosition(behind, out NavMeshHit hit, behindPlayerOffset * 2f, NavMesh.AllAreas))
        {
            if (!Physics.CheckSphere(hit.position, 0.5f, obstacleMask, QueryTriggerInteraction.Ignore))
                return hit.position;
        }
        return PickRandomNavMeshPoint();
    }

    private Vector3 PickRandomNavMeshPoint()
    {
        for (int i = 0; i < 20; i++)
        {
            Vector3 dir = Random.insideUnitSphere * teleportRandomRadius;
            Vector3 candidate = transform.position + dir;
            candidate.y = transform.position.y;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, teleportRandomRadius, NavMesh.AllAreas))
            {
                float d = player != null ? Vector3.Distance(hit.position, player.position) : teleportDistance + 3f;
                if (d > teleportDistance + 2f && !Physics.CheckSphere(hit.position, 0.5f, obstacleMask, QueryTriggerInteraction.Ignore))
                    return hit.position;
            }
        }
        return transform.position;
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
        foreach (var c in colliders) c.enabled = visible;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    private void SpawnParticles(GameObject prefab, Vector3 pos)
    {
        if (prefab != null)
            Instantiate(prefab, pos, Quaternion.identity);
    }

    // ======== ПАТРУЛЬ ========
    private void DoPatrol()
    {
        if (Time.time >= nextWanderTime || !hasWanderTarget)
        {
            PickNewWanderTarget();
            nextWanderTime = Time.time + wanderInterval;
        }

        // Ждём, пока путь просчитается, прежде чем судить по remainingDistance
        if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= 0.5f)
        {
            PickNewWanderTarget();
            nextWanderTime = Time.time + wanderInterval;
        }
    }

    private void PickNewWanderTarget()
    {
        if (player == null)
        {
            hasWanderTarget = false;
            return;
        }

        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = Random.insideUnitSphere * wanderRadius;
            Vector3 candidate = transform.position + dir;
            candidate.y = transform.position.y;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                float d = Vector3.Distance(hit.position, player.position);
                if (d > 5f)
                {
                    wanderTarget = hit.position;
                    agent.SetDestination(wanderTarget);
                    hasWanderTarget = true;
                    return;
                }
            }
        }
        // Если за 10 попыток не нашли валидную точку — оставляем текущую цель,
        // но всё равно попробуем снова на следующем цикле (nextWanderTime уже не сброшен).
    }

    // ======== ПРЕСЛЕДОВАНИЕ ========
    private void DoChase()
    {
        if (player != null)
            agent.SetDestination(player.position);
    }

    // ======== УБИЙСТВО ========
    private void DoKill()
    {
        agent.isStopped = true;
        Debug.Log("Доктор убил игрока!");
    }

    // ======== ЗВУКИ ========
    private void TryPlayRandomSound(float minCd, float maxCd)
    {
        if (Time.time < nextSoundTime) return;
        if (randomSounds == null || randomSounds.Length == 0) return;

        AudioClip clip = randomSounds[Random.Range(0, randomSounds.Length)];
        audioSource.PlayOneShot(clip);
        nextSoundTime = Time.time + Random.Range(minCd, maxCd);
    }

    // ======== ТЕКСТ ПРИ ПОЯВЛЕНИИ ========
    private void ShowSpawnText()
    {
        if (popupTextObject == null) return;
        StartCoroutine(PopupRoutine());
    }

    private IEnumerator PopupRoutine()
    {
        Text txt = popupTextObject.GetComponent<Text>();
        if (txt != null && spawnTexts.Length > 0)
            txt.text = spawnTexts[Random.Range(0, spawnTexts.Length)];

        popupTextObject.SetActive(true);
        yield return new WaitForSeconds(popupDuration);
        popupTextObject.SetActive(false);
    }

    // ======== ПРЯМАЯ ВИДИМОСТЬ ========
    private bool HasLineOfSight()
    {
        if (player == null) return false;

        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPoint = player.position + Vector3.up * 0.5f; // примерно центр тела игрока
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;

        if (dist <= 0.01f) return true;
        dir /= dist;

        // Небольшой запас, чтобы луч гарантированно доставал до коллайдера игрока
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist + 0.5f))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player))
                return true;
            return false; // упёрлись в препятствие раньше, чем в игрока
        }

        // Луч вообще ни во что не попал — считаем, что видимости нет
        // (например, если у игрока нет коллайдера в этом месте)
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, teleportDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, unstuckSearchRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
}
