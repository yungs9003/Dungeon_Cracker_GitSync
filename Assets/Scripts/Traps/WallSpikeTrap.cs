using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallSpikeTrap : MonoBehaviour, ITrapToggle
{
    [Header("Motion (LOCAL, XZ only)")]
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
    [SerializeField] private Collider damageTrigger;

    [Header("Toggle")]
    [SerializeField] private bool startEnabled = true;     // 처음부터 켜져있나?
    [SerializeField] private bool forceRetractOnDisable = true; // 꺼지면 즉시 회수?

    private Vector3 startLocalPos;
    private Vector3 endLocalPos;

    private bool trapEnabled;
    private bool isRunning;
    private bool armed;
    private Coroutine runningCo;

    private readonly Dictionary<Collider, float> lastHit = new();

    private void Awake()
    {
        startLocalPos = transform.localPosition;

        // ✅ y축은 그대로 두고, XZ(로컬 forward)로만 이동
        Vector3 dir = new Vector3(0f, 0f, 1f);
        endLocalPos = startLocalPos + dir * extendDistance;

        if (damageTrigger != null)
            damageTrigger.isTrigger = true;

        // 초기 상태 적용
        SetTrapEnabled(startEnabled);
    }

    /// <summary>
    /// 스위치/외부에서 호출: 함정 켜기/끄기
    /// </summary>
    public void SetTrapEnabled(bool enabled)
    {
        trapEnabled = enabled;

        if (!trapEnabled)
        {
            armed = false;
            lastHit.Clear();

            // 진행 중이면 중단
            if (runningCo != null)
            {
                StopCoroutine(runningCo);
                runningCo = null;
            }
            isRunning = false;

            // 데미지 트리거 자체를 꺼서 완전 안전하게 (원하면 유지해도 됨)
            if (damageTrigger != null) damageTrigger.enabled = false;

            // 원위치 회수
            if (forceRetractOnDisable)
                transform.localPosition = startLocalPos;
        }
        else
        {
            // 켜질 때 트리거 복구
            if (damageTrigger != null) damageTrigger.enabled = true;
        }
    }

    /// <summary>
    /// 센서(또는 타이머)에서 호출: 한 번 발동
    /// </summary>
    public void TriggerOnce()
    {
        if (!trapEnabled) return;     // ✅ 꺼져있으면 무시
        if (isRunning) return;

        runningCo = StartCoroutine(RunCycle());
    }

    private IEnumerator RunCycle()
    {
        isRunning = true;

        // Extend
        armed = true;
        yield return MoveLocal(startLocalPos, endLocalPos, extendTime);

        // Hold
        float t = 0f;
        while (t < holdTime)
        {
            // 진행 중에 스위치로 꺼질 수 있으니 체크
            if (!trapEnabled)
            {
                armed = false;
                isRunning = false;
                runningCo = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // Retract
        armed = false;
        yield return MoveLocal(endLocalPos, startLocalPos, retractTime);

        // Cooldown
        t = 0f;
        while (t < cooldownTime)
        {
            if (!trapEnabled)
            {
                isRunning = false;
                runningCo = null;
                yield break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        isRunning = false;
        runningCo = null;
    }

    private IEnumerator MoveLocal(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            transform.localPosition = to;
            yield break;
        }

        float u = 0f;
        while (u < 1f)
        {
            // 이동 중에도 꺼질 수 있음
            if (!trapEnabled)
            {
                transform.localPosition = startLocalPos;
                yield break;
            }

            u += Time.deltaTime / duration;
            float e = ease != null ? ease.Evaluate(Mathf.Clamp01(u)) : Mathf.Clamp01(u);
            transform.localPosition = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }

        transform.localPosition = to;
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other);
    private void OnTriggerStay(Collider other) => TryDamage(other);

    private void TryDamage(Collider other)
    {
        if (!trapEnabled) return;
        if (!armed) return;

        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;

        float now = Time.time;
        if (lastHit.TryGetValue(other, out float last) && now - last < hitCooldownPerTarget) return;
        lastHit[other] = now;

        var health = other.GetComponentInParent<Health>() ?? other.GetComponent<Health>();
        if (health != null) health.TakeDamage(damage);
    }
}
