using UnityEngine;

public class Bow : MonoBehaviour, ITrapToggle
{
    [Header("Projectile Prefab (Arrow 프리팹)")]
    [SerializeField] private Arrow projectilePrefab;

    [Header("Muzzle (블록 로컬 기준 오프셋)")]
    [SerializeField] private Vector3 muzzleLocalOffset = new Vector3(0f, 0.6f, 0.6f);

    [Header("Stats")]
    [SerializeField] private float speed = 4f;
    [SerializeField] private int damage = 2;
    [SerializeField] private float fireCooldown = 3.0f;
    [SerializeField] private bool autoFire = true;
    public void SetTrapEnabled(bool enabled) => autoFire = enabled;

    private float lastFireTime;
    private Collider ownerCol;

    private void Awake()
    {
        ownerCol = GetComponent<Collider>(); // 블록 콜라이더(있으면)
    }

    private void Update()
    {
        if (!autoFire) return;

        if (Time.time - lastFireTime >= fireCooldown)
        {
            lastFireTime = Time.time;
            Fire();
        }
    }

    public void Fire()
    {
        if (projectilePrefab == null) return;

        // ✅ 블록이 바라보는 방향(= JSON 회전이 적용된 결과)
        Vector3 dir = transform.forward;
        dir.y = 0f;
        dir.Normalize();

        // ✅ 블록 로컬 offset → 월드로 변환 (회전 연동됨)
        Vector3 spawnPos = transform.TransformPoint(muzzleLocalOffset);

        // ✅ 추가: y만 블록 "중간 높이"로 강제
        // ownerCol이 블록의 Collider라고 가정
        if (ownerCol != null)
            spawnPos.y = ownerCol.bounds.center.y;

        // ✅ (2) 벽을 뚫고 보이는 것 방지: 발사 방향으로 약간 앞으로 밀기
        const float forwardPadding = 0.15f; // 필요하면 0.05~0.3에서 조절
        spawnPos += dir * forwardPadding;

        // ✅ 투사체 "이미지 회전"을 발사 방향으로 맞춤
        Quaternion spawnRot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(-90f, 90f, 0f);
        
    
        Arrow arrow = Instantiate(projectilePrefab, spawnPos, spawnRot);
        arrow.Launch(dir, speed, damage, spawnRot , ownerCol);

    }
}
