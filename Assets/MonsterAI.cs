using UnityEngine;
using UnityEngine.AI; // Modern NavMesh system is active!
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    public enum MonsterState { Patrol, Chase, Jumpscare }

    [Header("References")]
    [Tooltip("The player transform that the monster will chase.")]
    public Transform playerTarget;
    [Tooltip("The main camera of the player (for jumpscare look-at).")]
    public Transform playerCamera;
    [Tooltip("The AudioSource that plays the creepy chase/breathing sound.")]
    public AudioSource monsterAudio;
    [Tooltip("The AudioSource on the camera or player that plays the heartbeat.")]
    public AudioSource heartbeatAudio;

    [Header("Movement Settings")]
    public float patrolSpeed = 2.0f;
    public float chaseSpeed = 4.5f;
    [Tooltip("How close the player must get for the monster to notice them.")]
    public float detectionRange = 10.0f;
    [Tooltip("How far the monster wanders during patrol.")]
    public float patrolRadius = 15.0f;

    [Header("Audio & Jumpscare Customization")]
    [Tooltip("Maximum volume for heartbeat when the monster is right on top of the player.")]
    public float maxHeartbeatVolume = 1.0f;
    [Tooltip("Scary scream sound effect played when the monster catches the player.")]
    public AudioClip jumpscareScreamClip;

    private NavMeshAgent _agent;
    private MonsterState _currentState;
    private Vector3 _patrolTarget;
    private bool _isJumpscareTriggered = false;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _currentState = MonsterState.Patrol;
        _agent.speed = patrolSpeed;

        if (playerTarget == null)
        {
            // Auto-locate the Player by Tag if not assigned
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTarget = player.transform;
        }

        // Auto-locate Player Camera if not assigned
        if (playerCamera == null && playerTarget != null)
        {
            playerCamera = playerTarget.GetComponentInChildren<Camera>()?.transform;
        }

        // Set the first random patrol destination
        SetNextPatrolDestination();
    }

    void Update()
    {
        // If jumpscare is active, smoothly force the player camera to stare at the monster
        if (_isJumpscareTriggered)
        {
            SmoothLookAtMonster();
            return;
        }

        if (playerTarget == null) return;

        // YENİ: Boy farkından kaynaklanan çarpışma hatalarını önlemek için sadece X ve Z eksenindeki mesafeyi ölçüyoruz (2D)
        Vector3 flatMonsterPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatPlayerPos = new Vector3(playerTarget.position.x, 0, playerTarget.position.z);
        float distanceToPlayer = Vector3.Distance(flatMonsterPos, flatPlayerPos);

        // Control Heartbeat volume based on proximity
        UpdateHeartbeatSound(distanceToPlayer);

        switch (_currentState)
        {
            case MonsterState.Patrol:
                HandlePatrolState(distanceToPlayer);
                break;
            case MonsterState.Chase:
                HandleChaseState(distanceToPlayer);
                break;
        }
    }

    void HandlePatrolState(float distanceToPlayer)
    {
        _agent.speed = patrolSpeed;

        // If monster reached its patrol destination, pick a new one
        if (!_agent.pathPending && _agent.remainingDistance <= 0.5f)
        {
            SetNextPatrolDestination();
        }

        // Target detection check
        if (distanceToPlayer <= detectionRange)
        {
            StartChase();
        }
    }

    void HandleChaseState(float distanceToPlayer)
    {
        _agent.speed = chaseSpeed;
        
        // Continuously update destination to player's current position
        _agent.SetDestination(playerTarget.position);

        // If player gets too far away, lose them and go back to patrol
        if (distanceToPlayer > detectionRange * 1.5f)
        {
            StartPatrol();
        }

        // Check if monster caught the player (Jumpscare distance)
        // Mesafeyi 2.2f'ye çıkardık ki fizik çarpışmasından (ittirmeden) hemen ÖNCE kod tetiklensin!
        if (distanceToPlayer <= 2.2f) 
        {
            TriggerJumpscare();
        }
    }

    void StartChase()
    {
        _currentState = MonsterState.Chase;
        
        // Play scary alert sound if assigned
        if (monsterAudio != null && !monsterAudio.isPlaying)
        {
            monsterAudio.Play();
        }
    }

    void StartPatrol()
    {
        _currentState = MonsterState.Patrol;
        SetNextPatrolDestination();
    }

    void SetNextPatrolDestination()
    {
        // Find a random point on the NavMesh within the radius
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;
        
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randomDirection, out navHit, patrolRadius, -1))
        {
            _patrolTarget = navHit.position;
            _agent.SetDestination(_patrolTarget);
        }
    }

    void UpdateHeartbeatSound(float distanceToPlayer)
    {
        if (heartbeatAudio == null) return;

        // Play heartbeat if not playing
        if (!heartbeatAudio.isPlaying) heartbeatAudio.Play();

        // Calculate volume: louder as distance gets smaller (within detection * 1.5 range)
        float maxRange = detectionRange * 1.5f;
        if (distanceToPlayer < maxRange)
        {
            float t = 1.0f - (distanceToPlayer / maxRange);
            heartbeatAudio.volume = Mathf.Lerp(0.0f, maxHeartbeatVolume, t);
        }
        else
        {
            heartbeatAudio.volume = Mathf.Lerp(heartbeatAudio.volume, 0f, Time.deltaTime * 2f);
        }
    }

    void TriggerJumpscare()
    {
        _isJumpscareTriggered = true;
        _currentState = MonsterState.Jumpscare;
        
        // Canavarın fiziğini ve itmesini tamamen durdur
        if (_agent.isOnNavMesh) _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _agent.enabled = false; 

        Debug.LogWarning("JUMPSCARE! Monster caught the player.");

        // 1. OYUNCUYU DONDUR: Tüm fizik ve kontrol bileşenlerini kapatıyoruz ki sürüklenme dursun!
        if (playerTarget != null)
        {
            // Farenin dönüşünü engelle
            FirstPersonController fpc = playerTarget.GetComponent<FirstPersonController>();
            if (fpc != null) fpc.enabled = false;

            // FİZİKSEL ÇARPIŞMAYI (İTİLMEYİ) ENGELLE!
            CharacterController cc = playerTarget.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; 

            // Stop any player rigidbody physics
            Rigidbody rb = playerTarget.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        // 2. PLAY SCREAM SOUND
        if (monsterAudio != null)
        {
            monsterAudio.Stop();
            monsterAudio.volume = 1.0f; // Max volume for the jump scare!
            monsterAudio.spatialBlend = 0.0f; // Make it 2D (in player's head) for ultimate panic

            if (jumpscareScreamClip != null)
            {
                monsterAudio.clip = jumpscareScreamClip;
                monsterAudio.Play();
            }
            else
            {
                // Backup alert sound if no scream clip is assigned
                monsterAudio.Play();
            }
        }

        // Simple Game Over behavior: Reload the scene after 2.5 seconds
        StartCoroutine(ResetLevelAfterDelay(2.5f));
    }

    // Smoothly rotates the player's camera to lock onto the monster's "face" during jumpscare
    private void SmoothLookAtMonster()
    {
        if (playerCamera == null) return;

        // Aim for the upper middle part of the monster (cylinder's face area)
        Vector3 targetPoint = transform.position + Vector3.up * 1.5f;
        Vector3 directionToMonster = targetPoint - playerCamera.position;
        
        if (directionToMonster != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToMonster);
            playerCamera.rotation = Quaternion.Slerp(playerCamera.rotation, targetRotation, Time.deltaTime * 8.0f);
        }
    }

    private IEnumerator ResetLevelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Reloads the active scene to restart the game
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
}
