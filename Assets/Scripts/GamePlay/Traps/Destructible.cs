using UnityEngine;
using Dungeon.Core;
using Dungeon.Map;

namespace Dungeon.Traps
{
    public class Destructible : MonoBehaviour
    {
        [Header("What to destroy")]
        [Tooltip("보통 블록 루트(함정/콜라이더/비주얼이 묶여있는 최상위)를 넣어주세요.")]
        [SerializeField] private Transform destroyRoot;

        [Header("Trap toggle (optional)")]
        [Tooltip("비워두면 destroyRoot 하위에서 ITrapToggle 전부 찾아서 꺼요.")]
        [SerializeField] private MonoBehaviour[] explicitTrapToggles;

        [Header("Replacement (optional)")]
        [SerializeField] private GameObject replacementPrefab;
        [SerializeField] private Transform replacementSpawnPoint;
        [SerializeField] private bool copyDestroyedObjectRotation = true;

        [Header("Behavior")]
        [SerializeField] private bool disableTrapsBeforeDestroy = true;
        [SerializeField] private bool destroyNextFrame = true;

        [Header("Save Count")]
        [SerializeField] private bool countAsBrokenTrap = true;
        public bool CountsAsBrokenTrap => countAsBrokenTrap;

        private bool alreadyDestroyed;

        private void Awake()
        {
            if (destroyRoot == null)
                destroyRoot = transform;
        }

        public void ExecuteDestruction()
        {
            if (alreadyDestroyed)
                return;

            var builder = (destroyRoot != null ? destroyRoot : transform).GetComponentInParent<JsonBlockBuilder2D>();
            var handling = builder != null
                ? builder.TryHandleTileDestruction(destroyRoot != null ? destroyRoot.gameObject : gameObject)
                : TileDestructionHandling.NotHandled;
            if (handling == TileDestructionHandling.Rejected)
                return;

            alreadyDestroyed = true;

            if (disableTrapsBeforeDestroy)
                DisableTraps();

            AddBrokenTrapCount();

            if (handling == TileDestructionHandling.NotHandled)
                SpawnReplacement(builder);
            else if (destroyRoot != null)
            {
                // 새 바닥과 오래된 함정이 한 프레임 겹치거나 통로를 막지 않도록 한다.
                foreach (var collider in destroyRoot.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                foreach (var renderer in destroyRoot.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            }

            if (destroyRoot == null)
                return;

            if (destroyNextFrame)
                StartCoroutine(DestroyCo());
            else
                Destroy(destroyRoot.gameObject);
        }

        private void AddBrokenTrapCount()
        {
            if (!countAsBrokenTrap)
                return;

            if (StatusSaveFile.Instance == null)
            {
                Debug.LogWarning("[Destructible] StatusSaveFile.Instance is missing. broken_traps was not increased.");
                return;
            }

            StatusSaveFile.Instance.AddBrokenTrap(1);
        }

        private void SpawnReplacement(JsonBlockBuilder2D builder)
        {
            if (replacementPrefab == null)
                return;

            Transform baseTf = replacementSpawnPoint != null ? replacementSpawnPoint : destroyRoot;
            if (baseTf == null)
                baseTf = transform;

            Vector3 spawnPos = baseTf.position;
            Quaternion spawnRot = copyDestroyedObjectRotation ? baseTf.rotation : Quaternion.identity;

            // 파괴 대상의 형제로 생성해 맵 전환 때 함께 정리하고, 자식 해체는 살아 있는 부모를 유지한다.
            Transform parent = destroyRoot != null ? destroyRoot.parent : transform.parent;
            GameObject replacement = Instantiate(replacementPrefab, spawnPos, spawnRot, parent);
            if (builder != null)
            {
                builder.ApplySpriteTheme(replacement);
                builder.RegisterLegacyReplacement(destroyRoot != null ? destroyRoot.gameObject : gameObject,
                    replacement, replacementPrefab.name);
            }
        }

        private void DisableTraps()
        {
            if (destroyRoot == null)
                return;

            if (explicitTrapToggles != null && explicitTrapToggles.Length > 0)
            {
                foreach (var mb in explicitTrapToggles)
                {
                    if (mb == null)
                        continue;

                    if (mb is ITrapToggle t)
                        t.SetTrapEnabled(false);
                }

                return;
            }

            var toggles = destroyRoot.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in toggles)
            {
                if (mb is ITrapToggle t)
                    t.SetTrapEnabled(false);
            }
        }

        private System.Collections.IEnumerator DestroyCo()
        {
            yield return null;

            if (destroyRoot != null)
                Destroy(destroyRoot.gameObject);
        }
    }
}
