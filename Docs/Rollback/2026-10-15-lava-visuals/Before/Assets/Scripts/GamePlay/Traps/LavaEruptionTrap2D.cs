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
        [SerializeField] private SpriteRenderer idleFloor;
        [SerializeField] private Transform idleMask;
        [SerializeField] private GameObject column;
        [SerializeField] private LayerMask playerMask = 1 << 10;
        private Phase phase;
        private float elapsed;
        private bool trapEnabled = true;
        private ThemeTrapDamage2D damage;
        private readonly Collider2D[] overlaps = new Collider2D[32];
        private readonly HashSet<Health> uniqueTargets = new HashSet<Health>();
        private readonly Dictionary<Health, float> nextHit = new Dictionary<Health, float>();
        public bool IsErupting => phase == Phase.Eruption && trapEnabled;
        public bool CanBeDestroyedByPlayer() => false;
        private void Awake() { damage = GetComponent<ThemeTrapDamage2D>(); ResetCycle(); }
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
            if (idleFloor != null) idleFloor.enabled = phase != Phase.Eruption;
            if (idleMask != null) idleMask.localScale = new Vector3(phase == Phase.Warning ? Mathf.Max(.0001f, 1f - elapsed / warningDuration) : 1f, 1f, 1f);
            if (column != null) column.SetActive(IsErupting);
            if (IsErupting) DamageOnTile();
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
            if (idleFloor != null) idleFloor.enabled = true;
            if (idleMask != null) idleMask.localScale = Vector3.one;
        }
        public void SetTrapEnabled(bool value) { trapEnabled = value; if (!value) ResetCycle(); }
        private void OnDisable() => ResetCycle();
    }
}
