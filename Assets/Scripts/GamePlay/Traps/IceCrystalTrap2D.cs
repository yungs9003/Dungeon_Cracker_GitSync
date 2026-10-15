using Dungeon.Core;
using UnityEngine;

namespace Dungeon.Traps
{
    public sealed class IceCrystalTrap2D : MonoBehaviour, ITrapToggle, IPlayerDestructionGate
    {
        private enum Phase { Floor, Frost, Crystal }
        [SerializeField, Min(.01f)] private float floorDuration = 2f;
        [SerializeField, Min(.01f)] private float frostDuration = 1f;
        [SerializeField] private SpriteRenderer frostRenderer;
        [SerializeField] private GameObject crystalPrefab;
        private Phase phase;
        private float elapsed;
        private bool trapEnabled = true;
        private GameObject crystal;
        public GameObject ActiveCrystal => crystal;
        public bool CanBeDestroyedByPlayer() => false;
        private void Awake() { if (frostRenderer != null) frostRenderer.enabled = false; }
        private void Update() => Tick(Time.deltaTime);
        private void Tick(float dt)
        {
            if (!trapEnabled) return;
            if (phase == Phase.Crystal)
            {
                if (crystal != null) return;
                phase = Phase.Floor; elapsed = 0f;
                if (frostRenderer != null) frostRenderer.enabled = false;
                return;
            }
            elapsed += dt;
            if (phase == Phase.Floor && elapsed >= floorDuration)
            {
                phase = Phase.Frost; elapsed = 0f;
                if (frostRenderer != null) { frostRenderer.color = new Color(1f, 1f, 1f, 0f); frostRenderer.enabled = true; }
            }
            else if (phase == Phase.Frost)
            {
                if (frostRenderer != null) frostRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(elapsed / frostDuration));
                if (elapsed < frostDuration || crystalPrefab == null) return;
                crystal = Instantiate(crystalPrefab, transform.position, transform.rotation, transform);
                var obstacle = crystal.GetComponent<IceCrystalObstacle2D>();
                if (obstacle != null) obstacle.SetDamageSource(GetComponent<ThemeTrapDamage2D>());
                phase = Phase.Crystal; elapsed = 0f;
            }
        }
        public void SetTrapEnabled(bool value)
        {
            trapEnabled = value;
            if (!value)
            {
                if (crystal != null) { crystal.SetActive(false); DestroyObject(crystal); }
                crystal = null; phase = Phase.Floor; elapsed = 0f;
                if (frostRenderer != null) frostRenderer.enabled = false;
            }
        }
        private void OnDisable() { SetTrapEnabled(false); }
        private void OnEnable() { trapEnabled = true; }
        private static void DestroyObject(GameObject obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    }
}
