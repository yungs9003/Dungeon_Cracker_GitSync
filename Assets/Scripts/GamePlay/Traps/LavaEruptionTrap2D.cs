using System.Collections.Generic;
using Dungeon.Core;
using Dungeon.Player;
using UnityEngine;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(ThemeTrapDamage2D))]
    public sealed class LavaEruptionTrap2D : MonoBehaviour, ITrapToggle, IPlayerDestructionGate
    {
        private enum Phase { Rest, Warning, Eruption }
        [SerializeField, Min(.01f)] private float restDuration = 2f;
        [SerializeField, Min(.01f)] private float warningDuration = 1f;
        [SerializeField, Min(.01f)] private float eruptionDuration = 1f;
        [SerializeField, Min(.1f)] private float damageInterval = .5f;
        [Tooltip("용암 이미지가 위로 한 칸 이동하는 시간입니다.")]
        [SerializeField, Min(.05f)] private float cellRiseTime = .25f;
        [Tooltip("이어지는 이미지 경계의 겹침 높이(칸). 렌더링 경계의 미세한 틈을 막습니다.")]
        [SerializeField, Range(0f, .1f)] private float segmentOverlap = .02f;
        [SerializeField] private SpriteRenderer idleFloor;
        [SerializeField] private GameObject column;
        [SerializeField] private SpriteRenderer[] risingSegments;
        [SerializeField] private LayerMask playerMask = 1 << 10;
        private Phase phase;
        private float elapsed;
        private bool trapEnabled = true;
        private ThemeTrapDamage2D damage;
        private Color idleColor = Color.white;
        private readonly Collider2D[] overlaps = new Collider2D[32];
        private readonly HashSet<Health> uniqueTargets = new HashSet<Health>();
        private readonly Dictionary<Health, float> nextHit = new Dictionary<Health, float>();
        public bool IsErupting => phase == Phase.Eruption && trapEnabled;
        public bool CanBeDestroyedByPlayer() => false;
        private void Awake()
        {
            damage = GetComponent<ThemeTrapDamage2D>();
            if (idleFloor != null) idleColor = idleFloor.color;
            ResetCycle();
        }
        private void Update() => Tick(Time.deltaTime);
        private void Tick(float dt)
        {
            if (!trapEnabled) return;
            elapsed += dt;
            float duration = phase == Phase.Rest ? restDuration : phase == Phase.Warning ? warningDuration : eruptionDuration;
            if (elapsed >= Mathf.Max(.01f, duration))
            {
                elapsed = 0f;
                phase = phase == Phase.Rest ? Phase.Warning : phase == Phase.Warning ? Phase.Eruption : Phase.Rest;
                nextHit.Clear();
            }
            if (idleFloor != null)
            {
                idleFloor.enabled = phase != Phase.Eruption;
                Color color = idleColor;
                color.a *= phase == Phase.Warning ? 1f - Mathf.Clamp01(elapsed / Mathf.Max(.01f, warningDuration)) : 1f;
                idleFloor.color = color;
            }
            if (column != null) column.SetActive(IsErupting);
            if (IsErupting) { UpdateRisingSegments(); DamageOnTile(); }
        }
        private void UpdateRisingSegments()
        {
            if (risingSegments == null || risingSegments.Length == 0) return;
            float travel = elapsed / Mathf.Max(.05f, cellRiseTime);
            for (int i = 0; i < risingSegments.Length; i++)
            {
                SpriteRenderer segment = risingSegments[i];
                if (segment == null || segment.sprite == null) continue;
                float age = travel - i;
                segment.enabled = age >= -1f;
                if (!segment.enabled) continue;
                // 표시 창은 -0.5~1.5입니다. 위로 완전히 나간 슬롯을 바로 바닥 아래(-1칸)로 돌려 빈 구간을 막습니다.
                float offset = Mathf.Repeat(age + 1f, risingSegments.Length) - 1f;
                segment.transform.localPosition = new Vector3(0f, -.5f + offset, 0f);
                Vector2 size = segment.sprite.bounds.size;
                segment.transform.localScale = new Vector3(1f / Mathf.Max(.0001f, size.x),
                    (1f + Mathf.Clamp(segmentOverlap, 0f, .1f)) / Mathf.Max(.0001f, size.y), 1f);
            }
        }
        private void DamageOnTile()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(playerMask); filter.useTriggers = true;
            int count = Physics2D.OverlapBox(transform.position, Vector2.one, transform.eulerAngles.z, filter, overlaps);
            uniqueTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                Health health = overlaps[i].GetComponentInParent<Health>();
                if (health == null || !uniqueTargets.Add(health) || PlayerEffectLock2D.IsProtectedTarget(health)) continue;
                Rigidbody2D body = health.GetComponentInParent<Rigidbody2D>();
                Vector3 local = transform.InverseTransformPoint(body != null ? (Vector3)body.worldCenterOfMass : health.transform.position);
                if (Mathf.Abs(local.x) > .5f || Mathf.Abs(local.y) > .5f) continue;
                if (nextHit.TryGetValue(health, out float next) && Time.time < next) continue;
                if (damage != null && damage.Hit(health)) nextHit[health] = Time.time + damageInterval;
            }
        }
        private void ResetCycle()
        {
            phase = Phase.Rest; elapsed = 0f; nextHit.Clear();
            if (column != null) column.SetActive(false);
            if (idleFloor != null) { idleFloor.enabled = true; idleFloor.color = idleColor; }
        }
        public void SetTrapEnabled(bool value) { trapEnabled = value; if (!value) ResetCycle(); }
        private void OnDisable() => ResetCycle();
    }
}
