using System.Collections;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Dungeon.Core;

namespace Dungeon.Player
{
    [RequireComponent(typeof(Health), typeof(Rigidbody2D))]
    public class Respawn2D : MonoBehaviour
    {
        [Header("Start Respawn Point (낭떠러지/트랩 복귀용 또는 예외 상황용)")]
        [SerializeField] private Vector2 startRespawnPoint;

        [Header("Last Safe Point (낭떠러지/트랩 복귀용)")]
        [SerializeField] private Vector2 lastSafePoint;

        [Header("Option")]
        [SerializeField] private bool respawnOnDie = true;
        [SerializeField] private bool resetVelocity = true;
        [SerializeField] private bool initializeFromStartPosition = true;

        [Header("Checkpoint Respawn")]
        [SerializeField] private bool useCheckpointSceneRespawn = true;

        [Header("No Checkpoint Death")]
        [Tooltip("체크포인트를 한 번도 찍지 않은 상태에서 HP가 0이 되면 현재 맵 시작점이 아니라 던전 씬을 다시 로드해서 첫 번째 JSON 맵부터 시작합니다.")]
        [SerializeField] private bool restartWholeDungeonWhenNoCheckpoint = true;

        [Header("Pit/Trap Anti-spam")]
        [SerializeField] private float trapCooldown = 0.2f;

        private float nextTrapAllowedTime;

        private Health health;
        private Rigidbody2D rb;
        private Coroutine respawnCo;

        private void Awake()
        {
            health = GetComponent<Health>();
            rb = GetComponent<Rigidbody2D>();

            if (initializeFromStartPosition)
            {
                Vector2 p = transform.position;
                if (startRespawnPoint == Vector2.zero)
                    startRespawnPoint = p;

                if (lastSafePoint == Vector2.zero)
                    lastSafePoint = p;
            }
        }

        private void OnEnable()
        {
            if (health != null && respawnOnDie)
                health.onDied.AddListener(HandleDied);
        }

        private void OnDisable()
        {
            if (health != null && respawnOnDie)
                health.onDied.RemoveListener(HandleDied);
        }

        public void SetRespawnPoint(Vector2 p)
        {
            startRespawnPoint = p;
        }

        public void SetLastSafePoint(Vector2 p)
        {
            lastSafePoint = p;
        }

        public Vector2 GetLastSafePoint() => lastSafePoint;
        public Vector2 GetStartRespawnPoint() => startRespawnPoint;

        public void RespawnToStart()
        {
            TeleportToRaw(startRespawnPoint, reviveFull: true);
        }

        public void RespawnToLastSafePoint()
        {
            TeleportToRaw(lastSafePoint, reviveFull: false);
        }

        public bool ReturnToLastSafePointFromTrap(int damage = 0)
        {
            if (Time.time < nextTrapAllowedTime)
                return false;

            nextTrapAllowedTime = Time.time + trapCooldown;

            if (damage > 0 && health != null)
                health.TakeDamage(damage);

            // 데미지로 죽었으면 onDied가 처리한다.
            // 체크포인트가 있으면 체크포인트 씬으로, 없으면 던전 씬을 다시 로드한다.
            if (health != null && health.currentHP <= 0)
                return false;

            TeleportToRaw(lastSafePoint, reviveFull: false);
            return true;
        }

        public void TeleportToRaw(Vector2 worldPos, bool reviveFull = false)
        {
            if (respawnCo != null)
                StopCoroutine(respawnCo);

            respawnCo = StartCoroutine(CoTeleport(worldPos, reviveFull));
        }

        private IEnumerator CoTeleport(Vector2 worldPos, bool reviveFull)
        {
            if (resetVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            var kb = GetComponent<KnockbackReceiver2D>();
            if (kb != null)
                kb.CancelKnockback();

            rb.position = worldPos;
            transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);

            yield return null;

            if (resetVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (reviveFull && health != null)
                health.ReviveFull();

            respawnCo = null;
        }

        private void HandleDied()
        {
            string activeScene = SceneManager.GetActiveScene().name;

            if (useCheckpointSceneRespawn)
            {
                string checkpointScene = GetCheckpointSceneName();

                // 이미 체크포인트 씬 안에서 죽은 경우에는 씬 이동을 반복하지 않고 아래 로직으로 넘긴다.
                bool isAlreadyInCheckpointScene =
                    !string.IsNullOrEmpty(checkpointScene) &&
                    activeScene == checkpointScene;

                if (!isAlreadyInCheckpointScene && HasCheckpoint())
                {
                    if (TryLoadCheckpointScene())
                        return;
                }
            }

            // 체크포인트가 아직 없을 때 HP가 0이 되면 현재 JSON 맵의 시작 위치가 아니라
            // 던전 씬을 다시 로드해서 MapSequenceManager가 첫 번째 JSON 맵부터 시작하게 한다.
            if (restartWholeDungeonWhenNoCheckpoint)
            {
                RestartCurrentDungeonSceneFromFirstMap();
                return;
            }

            // 옵션을 끈 경우에만 예전 방식으로 현재 맵의 시작 위치에서 부활한다.
            RespawnToStart();
        }

        private void RestartCurrentDungeonSceneFromFirstMap()
        {
            if (respawnCo != null)
            {
                StopCoroutine(respawnCo);
                respawnCo = null;
            }

            if (resetVelocity && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            var kb = GetComponent<KnockbackReceiver2D>();
            if (kb != null)
                kb.CancelKnockback();

            // 혹시 남아 있을 수 있는 체크포인트/예약 맵 정보를 정리한다.
            ClearCheckpointSave();

            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        // --------------------------------------------------------------------
        // SaveManager reflection bridge
        //
        // Respawn2D는 Dungeon.GamePlay.asmdef 안에 있고,
        // SaveManager는 Dungeon.App.asmdef 안에 있을 수 있다.
        // GamePlay -> App 직접 참조를 추가하면 순환 참조가 생기므로,
        // 여기서는 SaveManager를 직접 using하지 않고 reflection으로 호출한다.
        // --------------------------------------------------------------------

        private object GetSaveManagerInstance()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type saveManagerType =
                    asm.GetType("Dungeon.Manager.SaveManager") ??
                    asm.GetType("Dungeon.App.Manager.SaveManager") ??
                    asm.GetType("SaveManager");

                if (saveManagerType == null)
                    continue;

                PropertyInfo instanceProp = saveManagerType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static
                );

                if (instanceProp != null)
                    return instanceProp.GetValue(null);

                FieldInfo instanceField = saveManagerType.GetField(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static
                );

                if (instanceField != null)
                    return instanceField.GetValue(null);
            }

            return null;
        }

        private bool HasCheckpoint()
        {
            object saveManager = GetSaveManagerInstance();
            if (saveManager == null)
                return false;

            Type t = saveManager.GetType();

            PropertyInfo prop = t.GetProperty(
                "HasCheckpoint",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (prop != null && prop.PropertyType == typeof(bool))
                return (bool)prop.GetValue(saveManager);

            FieldInfo field = t.GetField(
                "hasCheckpoint",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(saveManager);

            // SaveData 안에 hasCheckpoint가 들어있는 구조까지 대응
            object saveData = GetSaveDataObject(saveManager);
            if (saveData != null)
            {
                Type dataType = saveData.GetType();

                PropertyInfo dataProp = dataType.GetProperty(
                    "hasCheckpoint",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (dataProp != null && dataProp.PropertyType == typeof(bool))
                    return (bool)dataProp.GetValue(saveData);

                FieldInfo dataField = dataType.GetField(
                    "hasCheckpoint",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (dataField != null && dataField.FieldType == typeof(bool))
                    return (bool)dataField.GetValue(saveData);
            }

            return false;
        }

        private string GetCheckpointSceneName()
        {
            object saveManager = GetSaveManagerInstance();
            if (saveManager == null)
                return string.Empty;

            Type t = saveManager.GetType();

            PropertyInfo prop = t.GetProperty(
                "CheckpointSceneName",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (prop != null && prop.PropertyType == typeof(string))
                return prop.GetValue(saveManager) as string;

            FieldInfo field = t.GetField(
                "checkpointSceneName",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (field != null && field.FieldType == typeof(string))
                return field.GetValue(saveManager) as string;

            return string.Empty;
        }

        private bool TryLoadCheckpointScene()
        {
            object saveManager = GetSaveManagerInstance();
            if (saveManager == null)
                return false;

            MethodInfo method = saveManager.GetType().GetMethod(
                "LoadCheckpointSceneIfAvailable",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (method == null)
                return false;

            object result = method.Invoke(saveManager, null);

            if (result is bool b)
                return b;

            return true;
        }

        private void ClearCheckpointSave()
        {
            object saveManager = GetSaveManagerInstance();
            if (saveManager == null)
                return;

            MethodInfo method = saveManager.GetType().GetMethod(
                "ClearCheckpoint",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            method?.Invoke(saveManager, null);
        }

        private object GetSaveDataObject(object saveManager)
        {
            if (saveManager == null)
                return null;

            Type t = saveManager.GetType();

            PropertyInfo prop = t.GetProperty(
                "Data",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (prop != null)
                return prop.GetValue(saveManager);

            FieldInfo field = t.GetField(
                "data",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (field != null)
                return field.GetValue(saveManager);

            return null;
        }
    }
}
