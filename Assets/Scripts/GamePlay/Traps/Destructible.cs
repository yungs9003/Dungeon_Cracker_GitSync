using UnityEngine;
using Dungeon.Core;

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

            alreadyDestroyed = true;

            if (disableTrapsBeforeDestroy)
                DisableTraps();

            AddBrokenTrapCount();

            SpawnReplacement();

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

        private void SpawnReplacement()
        {
            if (replacementPrefab == null)
                return;

            Transform baseTf = replacementSpawnPoint != null ? replacementSpawnPoint : destroyRoot;
            if (baseTf == null)
                baseTf = transform;

            Vector3 spawnPos = baseTf.position;
            Quaternion spawnRot = copyDestroyedObjectRotation ? baseTf.rotation : Quaternion.identity;

            Instantiate(replacementPrefab, spawnPos, spawnRot);
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