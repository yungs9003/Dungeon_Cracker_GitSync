using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Dungeon.Core;
using Dungeon.Player;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class WallSpikeTrap2D : MonoBehaviour, ITrapToggle
    {
        [Header("Motion (LOCAL XY)")]
        [SerializeField] private Vector2 localExtendDir = Vector2.up;   // ✅ 기본 발사방향 +Y
        [SerializeField] private float extendDistance = 0.8f;
        [SerializeField] private float extendTime = 0.12f;
        [SerializeField] private float holdTime = 0.20f;
        [SerializeField] private float retractTime = 0.12f;
        [SerializeField] private float cooldownTime = 0.6f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Damage")]
        [SerializeField] private int damage = 1;
        [SerializeField] private float hitCooldownPerTarget = 0.3f;
        [SerializeField] private LayerMask targetMask;

        [Header("Refs")]
        [SerializeField] private Collider2D damageTrigger;
        [SerializeField] private Transform movingPart;

        [Header("Toggle")]
        [SerializeField] private bool startEnabled = true;
        [SerializeField] private bool forceRetractOnDisable = true;

        [Header("Visual (Hide when retracted)")]
        [SerializeField] private Renderer[] spikeRenderers;
        [SerializeField] private bool hideOnStart = true;

        private Vector3 startLocalPos;
        private Vector3 endLocalPos;
        private bool trapEnabled;
        private bool isRunning;
        private bool armed;
        private Coroutine runningCo;
        private readonly Dictionary<Collider2D, float> lastHit = new();

        private void Awake()
        {
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;

            if (movingPart == null) movingPart = transform;
            if (damageTrigger == null) damageTrigger = GetComponentInChildren<Collider2D>(true);
            if (damageTrigger != null) damageTrigger.isTrigger = true;

            startLocalPos = movingPart.localPosition;

            // ✅ fallback도 +Y
            Vector2 dir = localExtendDir.sqrMagnitude < 0.0001f ? Vector2.up : localExtendDir.normalized;
            endLocalPos = startLocalPos + new Vector3(dir.x, dir.y, 0f) * extendDistance;

            InitRenderersIfNeeded();
            if (hideOnStart) SetSpikeVisible(false);

            SetTrapEnabled(startEnabled);
        }

        public void TriggerOnce()
        {
            if (!trapEnabled || isRunning) return;
            SetSpikeVisible(true);
            runningCo = StartCoroutine(RunCycle());
        }

        public void SetTrapEnabled(bool enabled)
        {
            trapEnabled = enabled;

            if (!trapEnabled)
            {
                armed = false;
                lastHit.Clear();

                if (runningCo != null)
                {
                    StopCoroutine(runningCo);
                    runningCo = null;
                }

                isRunning = false;
                if (damageTrigger != null) damageTrigger.enabled = false;

                if (forceRetractOnDisable)
                    movingPart.localPosition = startLocalPos;

                SetSpikeVisible(false);
            }
            else
            {
                if (damageTrigger != null) damageTrigger.enabled = true;
                if (hideOnStart) SetSpikeVisible(false);
            }
        }

        private IEnumerator RunCycle()
        {
            isRunning = true;
            armed = true;
            yield return MoveLocal(startLocalPos, endLocalPos, extendTime);

            float t = 0f;
            while (t < holdTime)
            {
                if (!trapEnabled) { FinishImmediatelyHidden(); yield break; }
                t += Time.deltaTime;
                yield return null;
            }

            armed = false;
            yield return MoveLocal(endLocalPos, startLocalPos, retractTime);
            SetSpikeVisible(false);

            t = 0f;
            while (t < cooldownTime)
            {
                if (!trapEnabled) { FinishImmediatelyHidden(); yield break; }
                t += Time.deltaTime;
                yield return null;
            }

            isRunning = false;
            runningCo = null;
        }

        private void FinishImmediatelyHidden()
        {
            armed = false;
            isRunning = false;
            runningCo = null;
            movingPart.localPosition = startLocalPos;
            SetSpikeVisible(false);
        }

        private IEnumerator MoveLocal(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                movingPart.localPosition = to;
                yield break;
            }

            float u = 0f;
            while (u < 1f)
            {
                if (!trapEnabled)
                {
                    movingPart.localPosition = startLocalPos;
                    SetSpikeVisible(false);
                    yield break;
                }

                u += Time.deltaTime / duration;
                float e = ease != null ? ease.Evaluate(Mathf.Clamp01(u)) : Mathf.Clamp01(u);
                movingPart.localPosition = Vector3.LerpUnclamped(from, to, e);
                yield return null;
            }

            movingPart.localPosition = to;
        }

        private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);
        private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        private void TryDamage(Collider2D other)
        {
            if (!trapEnabled || !armed) return;
            if (other.isTrigger) return;
            if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;

            float now = Time.time;
            if (lastHit.TryGetValue(other, out float last) && now - last < hitCooldownPerTarget) return;
            lastHit[other] = now;

            var health = other.GetComponentInParent<Health>() ?? other.GetComponent<Health>();
            if (health != null)
                health.TakeDamage(damage);
        }

        private void InitRenderersIfNeeded()
        {
            if (spikeRenderers != null && spikeRenderers.Length > 0) return;
            if (movingPart == null) return;

            spikeRenderers = movingPart.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null)
                .ToArray();
        }

        private void SetSpikeVisible(bool visible)
        {
            if (spikeRenderers == null) return;
            for (int i = 0; i < spikeRenderers.Length; i++)
                if (spikeRenderers[i] != null)
                    spikeRenderers[i].enabled = visible;
        }
    }
}