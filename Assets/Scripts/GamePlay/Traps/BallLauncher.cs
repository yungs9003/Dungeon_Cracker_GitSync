using UnityEngine;
using Dungeon.Core;

namespace Dungeon.Traps
{
public class BallLauncher : MonoBehaviour, ITrapToggle
{
    [Header("Ball Prefab")]
    [SerializeField] private RollingBallTrap ballPrefab;

    [Header("Spawn Offset (로컬)")]
    [SerializeField] private Vector3 spawnLocalOffset = new Vector3(0f, 0.6f, 0.6f);

    [Header("Grid")]
    [SerializeField] private float cellSize = 1f;   // ✅ 한 칸 이동 거리

    [Header("Stats")]
    [SerializeField] private float speed = 6f;
    [SerializeField] private float spawnCooldown = 2.0f;
    [SerializeField] private bool autoSpawn = true;
    public void SetTrapEnabled(bool enabled){
        autoSpawn = enabled;
        Debug.Log($"[BallLauncher:{name}] SetTrapEnabled({enabled})");
    }
    [Header("Anti-clipping")]
    [SerializeField] private float forwardPadding = 0.2f;
    [SerializeField] private float checkRadius = 0.25f;
    [SerializeField] private int pushOutIters = 8;

    private float lastSpawnTime;
    private Collider ownerCol;

    private void Awake()
    {
        ownerCol = GetComponent<Collider>();
    }

    private void Update()
    {
        if (!autoSpawn) return;

        if (Time.time - lastSpawnTime >= spawnCooldown)
        {
            lastSpawnTime = Time.time;
            Spawn();
        }
    }

    public void Spawn()
    {
        Debug.Log($"[BallLauncher:{name}] Spawn called. autoSpawn={autoSpawn}");
        if (ballPrefab == null) return;

        Vector3 dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();

        Vector3 spawnPos = transform.TransformPoint(spawnLocalOffset);
        spawnPos += dir * cellSize;
        // Bow에서 쓰던 방식: ownerCol 기준으로 높이 맞추기 :contentReference[oaicite:3]{index=3}
        if (ownerCol != null)
            spawnPos.y = ownerCol.bounds.center.y;

        // 벽 안에서 생기는 거 방지: 앞으로 조금 밀기(기존 Bow의 forwardPadding과 동일 컨셉) :contentReference[oaicite:4]{index=4}
        spawnPos += dir * forwardPadding;

        // 그래도 겹치면 앞으로 더 밀어내기
        for (int i = 0; i < pushOutIters; i++)
        {
            if (!Physics.CheckSphere(spawnPos, checkRadius, ~0, QueryTriggerInteraction.Ignore))
                break;
            spawnPos += dir * (checkRadius * 0.6f);
        }

        RollingBallTrap ball = Instantiate(ballPrefab, spawnPos, Quaternion.identity);
        ball.Launch(dir, speed, TrapDamageEvent.ResolveTrapCode(gameObject), ownerCol);
        Debug.Log($"[{name}] forward={transform.forward}, rotY={transform.eulerAngles.y}");

    }
}
}
