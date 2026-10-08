using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;
using Dungeon.Core;

namespace Dungeon.Traps
{
    [RequireComponent(typeof(Collider2D))]
    public class ElectricTileTrap2D : MonoBehaviour, ITrapToggle
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer electricRenderer;

        [Header("Electric Animation")]
        [SerializeField] private Sprite[] electricFrames;
        [SerializeField] private float frameInterval = 0.08f;

        [Header("Cycle")]
        [SerializeField] private float offDuration = 1.5f;
        [SerializeField] private float onDuration = 1.0f;
        [SerializeField] private bool startOn = false;

        [Header("Hit")]
        [SerializeField] private float damageInterval = 0.5f;

        [Header("Trap Toggle")]
        [SerializeField] private bool trapEnabled = true;

        private bool isElectricOn;
        private Coroutine cycleRoutine;
        private Coroutine animationRoutine;

        private readonly HashSet<Health> playersInside = new();
        private readonly Dictionary<Health, float> nextDamageTime = new();

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            Collider2D col = GetComponent<Collider2D>();
            col.isTrigger = true;

            if (electricRenderer != null)
                electricRenderer.enabled = false;
        }

        private void OnEnable()
        {
            cycleRoutine = StartCoroutine(CycleRoutine());
        }

        private void OnDisable()
        {
            if (cycleRoutine != null)
                StopCoroutine(cycleRoutine);

            if (animationRoutine != null)
                StopCoroutine(animationRoutine);
        }

        private IEnumerator CycleRoutine()
        {
            if (startOn)
                TurnElectricOn();
            else
                TurnElectricOff();

            while (true)
            {
                if (!trapEnabled)
                {
                    TurnElectricOff();
                    yield return null;
                    continue;
                }

                TurnElectricOff();
                yield return new WaitForSeconds(offDuration);

                TurnElectricOn();
                yield return new WaitForSeconds(onDuration);
            }
        }

        private void TurnElectricOn()
        {
            if (isElectricOn) return;

            isElectricOn = true;

            if (electricRenderer != null)
                electricRenderer.enabled = true;

            if (animationRoutine != null)
                StopCoroutine(animationRoutine);

            animationRoutine = StartCoroutine(ElectricAnimationRoutine());

            DamagePlayersInside();
        }

        private void TurnElectricOff()
        {
            isElectricOn = false;

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            if (electricRenderer != null)
                electricRenderer.enabled = false;

            nextDamageTime.Clear();
        }

        private IEnumerator ElectricAnimationRoutine()
        {
            int index = 0;

            while (isElectricOn)
            {
                if (electricFrames != null && electricFrames.Length > 0 && electricRenderer != null)
                {
                    electricRenderer.sprite = electricFrames[index];
                    index = (index + 1) % electricFrames.Length;
                }

                yield return new WaitForSeconds(frameInterval);
            }
        }

        private void Update()
        {
            if (!trapEnabled) return;
            if (!isElectricOn) return;

            DamagePlayersInside();
        }

        private void DamagePlayersInside()
        {
            float now = Time.time;

            foreach (Health health in playersInside)
            {
                if (health == null) continue;

                if (nextDamageTime.TryGetValue(health, out float nextTime) && now < nextTime)
                    continue;

                TrapDamageEvent.Raise(gameObject, health);
                nextDamageTime[health] = now + damageInterval;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            playersInside.Add(health);

            if (trapEnabled && isElectricOn)
            {
                DamagePlayersInside();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            playersInside.Remove(health);
            nextDamageTime.Remove(health);
        }

        public void SetTrapEnabled(bool enabled)
        {
            trapEnabled = enabled;

            if (!trapEnabled)
            {
                TurnElectricOff();
            }
        }
    }
}
