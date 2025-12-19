using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(Rigidbody))]
public class Respawn : MonoBehaviour
{
    [Header("Respawn Point (JsonBlockBuilder3D가 SetRespawnPoint로 넣어줌)")]
    [SerializeField] private Vector3 respawnPoint;

    [Header("Option")]
    [SerializeField] private bool respawnOnDie = true;
    [SerializeField] private bool resetVelocity = true;

    private Health health;
    private Rigidbody rb;
    private Coroutine respawnCo;

    private void Awake()
    {
        health = GetComponent<Health>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (health != null && respawnOnDie)
            health.onDied.AddListener(HandleDied);
    }

    private void OnDisable()
    {
        if (health != null && respawnOnDie)
            health.onDied.RemoveListener(HandleDied);
    }

    // JsonBlockBuilder3D에서 player.start 기반으로 호출해주면 됨
    public void SetRespawnPoint(Vector3 p)
    {
        respawnPoint = p;
    }

    private void HandleDied()
    {
        if (respawnCo != null) StopCoroutine(respawnCo);
        respawnCo = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        // 물리 충돌 안정성을 위해 한 프레임 대기(선택)
        yield return null;

        if (resetVelocity && rb != null)
        {
            rb.linearVelocity = Vector3.zero;   // Unity 6 기준
            rb.angularVelocity = Vector3.zero;
        }

        // 텔레포트
        transform.position = respawnPoint;
        Physics.SyncTransforms();

        // HP 풀회복 (Health.cs에 ReviveFull 추가 필요)
        health.ReviveFull();

        respawnCo = null;
    }
}
