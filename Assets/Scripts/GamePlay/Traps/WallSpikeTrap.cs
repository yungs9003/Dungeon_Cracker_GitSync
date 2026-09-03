using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Dungeon.Core;
using Dungeon.Player;

namespace Dungeon.Traps
{
public class WallSpikeTrap : MonoBehaviour, ITrapToggle
{
    [Header("Motion (LOCAL)")]
    [Tooltip("가시가 뻗는 로컬 방향(보통 Z+ 방향).")]
    [SerializeField] private Vector3 localExtendDir = new Vector3(0f, 0f, 1f);

    [SerializeField] private float extendDistance = 0.8f;
    [SerializeField] private float extendTime = 0.12f;
    [SerializeField] private float holdTime = 0.20f;
    [SerializeField] private float retractTime = 0.12f;
    [SerializeField] private float cooldownTime = 0.6f;

    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Hit")]
    [SerializeField] private float hitCooldownPerTarget = 0.3f;
    [SerializeField] private LayerMask targetMask;

    [Header("Refs")]
    [Tooltip("데미지 판정을 담당하는 콜라이더(보통 spike의 Trigger collider). 비워두면 자기/자식에서 Collider를 찾습니다.")]
    [SerializeField] private Collider damageTrigger;

    [Tooltip("실제로 움직일 트랜스폼(권장: SpikeMesh). 비워두면 이 오브젝트(transform)가 움직입니다.")]
    [SerializeField] private Transform movingPart;

    [Header("Toggle")]
    [SerializeField] private bool startEnabled = true;
    [SerializeField] private bool forceRetractOnDisable = true;

    [Header("Visual (Hide when retracted)")]
    [Tooltip("비워두면 자식 Renderer들을 자동으로 찾아서 제어합니다.")]
    [SerializeField] private Renderer[] spikeRenderers;

    [Tooltip("시작 시(또는 Retract 완료 시) 숨김 상태로 둘지")]
    [SerializeField] private bool hideOnStart = true;

    private Vector3 startLocalPos;
    private Vector3 endLocalPos;

    private bool trapEnabled;
    private bool isRunning;
    private bool armed;
    private Coroutine runningCo;

    private readonly Dictionary<Collider, float> lastHit = new();

    private void Awake()
    {
        if (movingPart == null) movingPart = transform;

        // Trigger collider 자동 할당(없으면 찾기)
        if (damageTrigger == null)
            damageTrigger = GetComponentInChildren<Collider>(true);

        if (damageTrigger != null)
            damageTrigger.isTrigger = true;

        // 위치 계산
        startLocalPos = movingPart.localPosition;
        Vector3 dir = localExtendDir.sqrMagnitude < 0.0001f ? Vector3.forward : localExtendDir.normalized;
        endLocalPos = startLocalPos + dir * extendDistance;

        InitRenderersIfNeeded();
        if (hideOnStart) SetSpikeVisible(false);

        SetTrapEnabled(startEnabled);
    }

    // 외부(센서/트리거)에서 1회 발사 요청할 때 호출
    public void TriggerOnce()
    {
        if (!trapEnabled) return;
        if (isRunning) return;

        // 발사 시작: 보이게
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

            // 비활성화되면 무조건 숨김
            SetSpikeVisible(false);
        }
        else
        {
            if (damageTrigger != null) damageTrigger.enabled = true;

            // 활성화되더라도 기본은 숨김 유지(발사할 때만 보이게)
            if (hideOnStart) SetSpikeVisible(false);
        }
    }

    private IEnumerator RunCycle()
    {
        isRunning = true;

        // Extend 구간: 데미지 가능
        armed = true;
        yield return MoveLocal(startLocalPos, endLocalPos, extendTime);

        // Hold
        float t = 0f;
        while (t < holdTime)
        {
            if (!trapEnabled) { FinishImmediatelyHidden(); yield break; }
            t += Time.deltaTime;
            yield return null;
        }

        // Retract 구간: 더 이상 데미지 X
        armed = false;
        yield return MoveLocal(endLocalPos, startLocalPos, retractTime);

        // ✅ Retract 완료: 숨김 (가장 중요)
        SetSpikeVisible(false);

        // Cooldown
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

    // 데미지 트리거 처리
    private void OnTriggerEnter(Collider other) => TryDamage(other);
    private void OnTriggerStay(Collider other) => TryDamage(other);

    private void TryDamage(Collider other)
    {
        if (!trapEnabled) return;
        if (!armed) return;

        // ✅ ForwardZone 같은 감지용 Trigger 콜라이더는 데미지 대상에서 제외
        if (other.isTrigger) return;

        // LayerMask 필터
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;

        // 대상별 쿨다운
        float now = Time.time;
        if (lastHit.TryGetValue(other, out float last) && now - last < hitCooldownPerTarget) return;
        lastHit[other] = now;

        // Health 찾기
        var health = other.GetComponentInParent<Health>() ?? other.GetComponent<Health>();
        if (health != null)
            TrapDamageEvent.Raise(gameObject, health);
    }

    // -----------------------
    // Visual helpers
    // -----------------------
    private void InitRenderersIfNeeded()
    {
        if (spikeRenderers != null && spikeRenderers.Length > 0) return;

        // Spike 오브젝트 하위 Renderer 자동 수집
        spikeRenderers = GetComponentsInChildren<Renderer>(true)
            .Where(r => r != null)
            .ToArray();
    }

    private void SetSpikeVisible(bool visible)
    {
        if (spikeRenderers == null) return;

        for (int i = 0; i < spikeRenderers.Length; i++)
        {
            if (spikeRenderers[i] != null)
                spikeRenderers[i].enabled = visible;
        }
    }
}
}
