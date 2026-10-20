using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon.Core
{
    [DefaultExecutionOrder(-900)]
    public class StatusSaveFile : MonoBehaviour, ISerializationCallbackReceiver
    {
        public static StatusSaveFile Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool logSavePath = true;

        [Tooltip("Load()에서 세이브 요약을 출력합니다. 장비/아이템 데이터가 제대로 읽혔는지 확인할 때 사용합니다.")]
        [SerializeField] private bool logLoadSummary = true;

        [Tooltip("Save()에서 세이브 요약을 출력합니다. 너무 자주 저장되는 경우 아래 간격/횟수 제한을 함께 사용합니다.")]
        [SerializeField] private bool logSaveSummary = true;

        [Tooltip("로그에 inventory/equips 정보를 포함합니다.")]
        [SerializeField] private bool logInventorySummary = true;

        [Tooltip("Save 로그를 최소 몇 초 간격으로 출력할지 설정합니다. 0이면 시간 제한 없음.")]
        [SerializeField] private float saveSummaryLogInterval = 3f;

        [Tooltip("Save 로그를 몇 회 저장마다 출력할지 설정합니다. 1이면 매번, 10이면 10번째 저장마다 출력합니다.")]
        [SerializeField] private int saveSummaryLogEveryNthSave = 10;

        // Unity hot reload는 Dictionary를 직렬화하지 못하므로 전체 데이터를 JSON으로 보존합니다.
        [NonSerialized] private StatusSaveData data = new();
        [SerializeField, HideInInspector] private string serializedDataJson;
        private bool isQuitting;
        private int saveSummaryRequestCount;
        private float lastSaveSummaryLogTime = -9999f;
        private IStatusSaveStorage storage;
        private bool purchaseInProgress;
        private PlayTimeSession playTimeSession;
        private bool applicationPaused;
        private bool dungeonSceneTransition;
        private string timingSceneName;
        private string savedRunId;
        private float savedRunSeconds;

        public StatusSaveData Data => data;

        public event Action<StatusSaveData> OnSaveDataChanged;

        private string ActiveSavePath => EnsureStorage().ActivePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateOnPlayStart()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("StatusSaveFileManager").AddComponent<StatusSaveFile>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (logSavePath)
                {
                    Debug.Log(
                        $"[StatusSaveFile] Duplicate destroyed. " +
                        $"ThisID={GetInstanceID()}, ExistingID={Instance.GetInstanceID()}"
                    );
                }

                Destroy(gameObject);
                return;
            }

            Instance = this;
            storage = StatusSaveStorageFactory.Create();

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);

            if (logSavePath)
            {
                Debug.Log(
                    $"[StatusSaveFile] Instance set. ID={GetInstanceID()}, " +
                    $"ActivePath={ActiveSavePath}"
                );
            }

            Load();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || Instance != null)
                return;

            // Domain Reload 이후 Awake는 다시 실행되지 않으므로 싱글턴을 재연결합니다.
            Instance = this;
            if (string.IsNullOrWhiteSpace(serializedDataJson))
                Load();
            else
                StartPlayTimeSession();
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private string TimingScene => applicationPaused || dungeonSceneTransition ? "" : timingSceneName;

        private void StartPlayTimeSession()
        {
            if (Application.isPlaying && Instance == this)
            {
                timingSceneName = SceneManager.GetActiveScene().name;
                playTimeSession = new PlayTimeSession(data, Time.realtimeSinceStartupAsDouble, Time.timeAsDouble, TimingScene);
            }
        }

        /// <summary>현재 프레임까지 메모리 시간만 확정합니다. 파일 쓰기는 Save에 합칩니다.</summary>
        public void CapturePlayTime()
        {
            playTimeSession?.Sample(data, Time.realtimeSinceStartupAsDouble, Time.timeAsDouble, TimingScene);
        }

        /// <summary>체크포인트 확정 후 지연 로드 프레임을 던전 플레이 시간에 넣지 않습니다.</summary>
        public void PauseDungeonTimeForSceneChange()
        {
            CapturePlayTime();
            dungeonSceneTransition = true;
            playTimeSession?.BindRun(data, TimingScene);
        }

        private void Update()
        {
            if (Instance != this || isQuitting) return;
            CapturePlayTime();
            if (playTimeSession?.IsSaveDue(Time.realtimeSinceStartupAsDouble) == true)
                Save();
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            if (Instance != this || isQuitting) return;
            string previousName = timingSceneName;
            CapturePlayTime();
            timingSceneName = current.name;
            dungeonSceneTransition = false;
            playTimeSession?.BindRun(data, TimingScene);
            // 체크포인트/클리어 전환은 이미 같은 프레임의 트랜잭션에 포함됐습니다.
            // 메뉴로 바로 나가는 경로는 아직 저장하지 않은 던전 시간만 보존합니다.
            if (previousName?.StartsWith("DUNGEON_", StringComparison.OrdinalIgnoreCase) == true &&
                data.active_run?.run_id == savedRunId && data.clear_time_seconds != savedRunSeconds)
                Save();
        }

        public void OnBeforeSerialize()
        {
            if (data != null)
                serializedDataJson = StatusSaveDefaults.Serialize(data);
        }

        public void OnAfterDeserialize()
        {
            // 직렬화 콜백에서는 파일/Unity 오브젝트에 접근하지 않고 순수 데이터만 복구합니다.
            if (StatusSaveDefaults.TryDeserialize(serializedDataJson, out StatusSaveData restored, out _))
                data = restored;
            else
                serializedDataJson = null;
        }

        private IStatusSaveStorage EnsureStorage()
        {
            storage ??= StatusSaveStorageFactory.Create();
            return storage;
        }

        public void Load()
        {
            try
            {
                IStatusSaveStorage activeStorage = EnsureStorage();
                string generatedDefaultJson = StatusSaveDefaults.Serialize(StatusSaveDefaults.Create());

                if (!activeStorage.TryLoadOrCreate(
                        StatusSaveDefaults.IsValidJson,
                        generatedDefaultJson,
                        out string json))
                {
                    throw new InvalidOperationException(activeStorage.LastError);
                }

                if (!StatusSaveDefaults.TryDeserialize(json, out data, out string deserializeError))
                    throw new InvalidOperationException(deserializeError);

                EnsureNotNull();
                StartPlayTimeSession();

                if (DungeonProgressMigration.IsLegacyJson(json) &&
                    !activeStorage.TryWrite(StatusSaveDefaults.Serialize(data), StatusSaveDefaults.IsValidJson))
                    Debug.LogWarning("[StatusSaveFile] 저장 버전 이행 쓰기 실패. 기존 파일/백업을 유지합니다: " + activeStorage.LastError);

                if (GameDatabase.Instance != null)
                {
                    EquipmentStatCalculator.Recalculate(data, GameDatabase.Instance);
                }

                if (logSavePath && logLoadSummary)
                    Debug.Log(BuildSaveSummary("Loaded"));

                if (!string.IsNullOrWhiteSpace(activeStorage.LastRecoveryAction))
                {
                    Debug.LogWarning(
                        $"[StatusSaveFile] {activeStorage.LastRecoveryAction} " +
                        $"Path={activeStorage.ActivePath}");
                }

                NotifySaveDataChangedSafely("Load");
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatusSaveFile] Load failed: {e}");

                data = StatusSaveDefaults.Create();
                EnsureNotNull();
                StartPlayTimeSession();
                NotifySaveDataChangedSafely("LoadFallback");
            }
        }

        public bool Save() => SaveCore(captureTime: true);

        private bool SaveCore(bool captureTime)
        {
            try
            {
                if (captureTime) CapturePlayTime();
                EnsureNotNull();

                if (GameDatabase.Instance != null)
                {
                    EquipmentStatCalculator.Recalculate(data, GameDatabase.Instance);
                }

                DungeonProgressService.CaptureActiveRun(data);
                string json = StatusSaveDefaults.Serialize(data);
                IStatusSaveStorage activeStorage = EnsureStorage();

                if (!activeStorage.TryWrite(json, StatusSaveDefaults.IsValidJson))
                {
                    playTimeSession?.SaveFailed(Time.realtimeSinceStartupAsDouble);
                    Debug.LogError(
                        $"[StatusSaveFile] Save failed. Path={activeStorage.ActivePath}, " +
                        $"Error={activeStorage.LastError}");
                    return false;
                }

                playTimeSession?.Saved();
                savedRunId = data.active_run?.run_id;
                savedRunSeconds = data.clear_time_seconds;
                playTimeSession?.BindRun(data, TimingScene);

                if (ShouldLogSaveSummary())
                    Debug.Log(BuildSaveSummary("Saved"));

                // 종료 중에는 UI 오브젝트의 파괴 순서가 보장되지 않는다.
                // 파일 쓰기는 끝났으므로 화면 갱신 알림은 생략한다.
                if (!isQuitting)
                    NotifySaveDataChangedSafely("Save");

                return true;
            }
            catch (Exception e)
            {
                playTimeSession?.SaveFailed(Time.realtimeSinceStartupAsDouble);
                Debug.LogError($"[StatusSaveFile] Save failed: {e}");
                return false;
            }
        }

        private void NotifySaveDataChangedSafely(string source)
        {
            Action<StatusSaveData> listeners = OnSaveDataChanged;
            if (listeners == null)
                return;

            foreach (Delegate callback in listeners.GetInvocationList())
            {
                if (callback is not Action<StatusSaveData> listener)
                    continue;

                try
                {
                    listener(data);
                }
                catch (Exception listenerException)
                {
                    string declaringType = listener.Method.DeclaringType?.FullName ?? "<unknown>";
                    Debug.LogError(
                        $"[StatusSaveFile] {source} notification failed. " +
                        $"Listener={declaringType}.{listener.Method.Name}\n" +
                        listenerException);
                }
            }
        }

        private void EnsureNotNull()
        {
            data ??= StatusSaveDefaults.Create();
            StatusSaveDefaults.Normalize(data);
        }

        /// <summary>기록/재개 상태와 런 식별자를 함께 저장합니다. 쓰기 실패 시 메모리도 이전 값으로 되돌립니다.</summary>
        public bool TryUpdateDungeonProgress(Func<StatusSaveData, bool> change)
        {
            CapturePlayTime();
            EnsureNotNull();
            var previous = data;
            if (!StatusSaveDefaults.TryDeserialize(StatusSaveDefaults.Serialize(data), out var candidate, out _)) return false;
            try
            {
                if (!change(candidate)) return false;
                data = candidate;
                if (SaveCore(captureTime: false)) return true;
            }
            catch (Exception error) { Debug.LogError("[StatusSaveFile] Dungeon update failed: " + error); }
            data = previous;
            playTimeSession?.BindRun(data, TimingScene);
            return false;
        }

        public bool TryFinalizeDungeonClear() => TryUpdateDungeonProgress(DungeonProgressService.CommitClear);

        private bool ShouldLogSaveSummary()
        {
            if (!logSavePath || !logSaveSummary)
                return false;

            saveSummaryRequestCount++;

            // 첫 저장은 항상 찍어서 어떤 파일에 저장되는지 바로 확인할 수 있게 합니다.
            if (saveSummaryRequestCount == 1)
            {
                lastSaveSummaryLogTime = Time.unscaledTime;
                return true;
            }

            int every = Mathf.Max(1, saveSummaryLogEveryNthSave);
            if (every > 1 && saveSummaryRequestCount % every != 0)
                return false;

            if (saveSummaryLogInterval > 0f && Time.unscaledTime - lastSaveSummaryLogTime < saveSummaryLogInterval)
                return false;

            lastSaveSummaryLogTime = Time.unscaledTime;
            return true;
        }

        private string BuildSaveSummary(string action)
        {
            var sb = new StringBuilder();

            sb.Append($"[StatusSaveFile] {action}. ID={GetInstanceID()}, ");
            sb.Append($"Path={ActiveSavePath}, ");
            sb.Append($"user_name={data.user_name}, ");
            sb.Append($"gold={data.gold}, ");
            sb.Append($"maxHP={data.main_stats.max_HP}, ");
            sb.Append($"broken_traps={data.broken_traps}, ");
            sb.Append($"clear_time={data.clear_time_seconds:F2}, ");
            sb.Append($"hit_count={data.hit_count}");

            if (logInventorySummary)
            {
                sb.AppendLine();
                sb.Append($"  inventory.item={FormatIntMapList(data.inventory?.item)}, ");
                sb.Append($"inventory.equipment={FormatIntMapList(data.inventory?.equipment)}, ");
                sb.Append($"inventory.module={FormatIntMapList(data.inventory?.module)}");
                sb.AppendLine();
                sb.Append($"  equips.item={FormatStringMapList(data.equips?.item)}, ");
                sb.Append($"equips.equip={FormatStringMapList(data.equips?.equip)}, ");
                sb.Append($"equips.module={FormatStringMapList(data.equips?.module)}");
            }

            return sb.ToString();
        }

        private static string FormatIntMapList(List<Dictionary<string, int>> list)
        {
            if (list == null || list.Count == 0)
                return "[]";

            var sb = new StringBuilder("[");
            bool first = true;

            foreach (Dictionary<string, int> map in list)
            {
                if (map == null) continue;

                foreach (KeyValuePair<string, int> pair in map)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(pair.Key);
                    sb.Append("x");
                    sb.Append(pair.Value);
                }
            }

            sb.Append("]");
            return sb.ToString();
        }

        private static string FormatStringMapList(List<Dictionary<string, string>> list)
        {
            if (list == null || list.Count == 0)
                return "[]";

            var sb = new StringBuilder("[");
            bool first = true;

            foreach (Dictionary<string, string> map in list)
            {
                if (map == null) continue;

                foreach (KeyValuePair<string, string> pair in map)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(pair.Key);
                    sb.Append("=");
                    sb.Append(string.IsNullOrWhiteSpace(pair.Value) ? "none" : pair.Value);
                }
            }

            sb.Append("]");
            return sb.ToString();
        }

        public void SetUserName(string userName)
        {
            EnsureNotNull();

            data.user_name = userName?.Trim() ?? string.Empty;
            Save();
        }

        public void SetGold(int gold)
        {
            EnsureNotNull();

            data.gold = Mathf.Max(0, gold);
            Save();
        }

        public void AddGold(int amount)
        {
            EnsureNotNull();

            data.gold = Mathf.Max(0, data.gold + amount);
            Save();
        }

        /// <summary>DB 가격으로 재검증한 골드 차감과 소비 아이템 지급을 한 번에 저장합니다.</summary>
        public bool TryPurchaseItems(GameDatabase database, IReadOnlyDictionary<string, int> quantities, out string error)
        {
            error = string.Empty;
            if (purchaseInProgress) { error = "결제 중입니다."; return false; }
            if (database == null || quantities == null || quantities.Count == 0)
            { error = "구매할 아이템이 없습니다."; return false; }
            EnsureNotNull();
            var normalized = new Dictionary<string, int>();
            long total = 0;
            foreach (var pair in quantities)
            {
                string id = GameDatabase.NormalizeId(pair.Key);
                if (GameDatabase.TryParseKey(pair.Key, out string category, out string parsed))
                {
                    if (category != GameDatabase.CategoryItem) { error = "구매할 수 없는 아이템입니다."; return false; }
                    id = parsed;
                }
                if (pair.Value <= 0 || !database.TryGetItem(id, out var item) || item.gold < 0)
                { error = "아이템 또는 가격을 확인해 주세요."; return false; }
                normalized.TryGetValue(id, out int previous);
                if (previous > int.MaxValue - pair.Value)
                { error = "구매 수량이 너무 많습니다."; return false; }
                normalized[id] = previous + pair.Value;
                total += (long)item.gold * pair.Value;
                if (total > int.MaxValue) { error = "구매 금액이 너무 큽니다."; return false; }
            }
            if (data.gold < total) { error = "골드가 부족합니다."; return false; }

            // 성공 전에는 기존 인벤토리 사전/목록을 수정하지 않습니다.
            var originalItems = data.inventory.item;
            var updatedItems = new List<Dictionary<string, int>>();
            foreach (var map in originalItems) updatedItems.Add(map == null ? null : new Dictionary<string, int>(map));
            foreach (var pair in normalized)
            {
                long owned = 0;
                Dictionary<string, int> target = null;
                foreach (var map in updatedItems)
                {
                    if (map == null) continue;
                    var matchingKeys = new List<string>();
                    foreach (var entry in map)
                        if (GameDatabase.NormalizeId(entry.Key) == pair.Key)
                        { owned += Math.Max(0, entry.Value); matchingKeys.Add(entry.Key); }
                    if (matchingKeys.Count > 0) target ??= map;
                    foreach (string key in matchingKeys) map.Remove(key);
                }
                if (owned + pair.Value > int.MaxValue)
                { error = "보유 수량이 너무 많습니다."; return false; }
                if (target == null) { target = new Dictionary<string, int>(); updatedItems.Add(target); }
                target[pair.Key] = (int)owned + pair.Value;
            }

            int originalGold = data.gold;
            purchaseInProgress = true;
            try
            {
                data.gold -= (int)total;
                data.inventory.item = updatedItems;
                if (Save()) return true;
                data.gold = originalGold;
                data.inventory.item = originalItems;
                error = "저장에 실패했습니다. 다시 결제해 주세요.";
                return false;
            }
            finally { purchaseInProgress = false; }
        }

        public void SetMainStats(int maxHp, int physicalResist, int magicalResist)
        {
            EnsureNotNull();

            int normalizedMaxHp = Mathf.Max(1, maxHp);
            data.main_stats.max_HP = normalizedMaxHp;
            data.main_stats.physical_resist = physicalResist;
            data.main_stats.magical_resist = magicalResist;

            data.base_main_stats ??= new MainStats();
            data.base_main_stats.max_HP = normalizedMaxHp;
            data.base_main_stats.physical_resist = physicalResist;
            data.base_main_stats.magical_resist = magicalResist;

            Save();
        }

        public void SetSubStats(
            int speed,
            int poisonResist,
            int electricResist,
            int amberResist,
            int freezeResist)
        {
            EnsureNotNull();

            data.sub_stats.speed = speed;
            data.sub_stats.poison_resist = poisonResist;
            data.sub_stats.electric_resist = electricResist;
            data.sub_stats.amber_resist = amberResist;
            data.sub_stats.freeze_resist = freezeResist;

            data.base_sub_stats ??= new SubStats();
            data.base_sub_stats.speed = speed;
            data.base_sub_stats.poison_resist = poisonResist;
            data.base_sub_stats.electric_resist = electricResist;
            data.base_sub_stats.amber_resist = amberResist;
            data.base_sub_stats.freeze_resist = freezeResist;

            Save();
        }

        public int GetBrokenTraps()
        {
            EnsureNotNull();
            return Mathf.Max(0, data.broken_traps);
        }

        public void AddBrokenTrap(int amount = 1)
        {
            EnsureNotNull();

            if (amount <= 0)
                return;

            data.broken_traps += amount;

            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            Save();

            Debug.Log($"[StatusSaveFile] broken_traps increased: {data.broken_traps}");
        }

        public void ResetBrokenTraps()
        {
            EnsureNotNull();

            data.broken_traps = 0;
            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            Save();

            Debug.Log("[StatusSaveFile] broken_traps reset.");
        }

        public int GetClearRewardGold()
        {
            EnsureNotNull();

            int brokenTraps = Mathf.Max(0, data.broken_traps);
            return 500 + 50 * brokenTraps;
        }

        public int ClaimClearRewardGold()
        {
            EnsureNotNull();

            if (data.clear_reward_claimed)
            {
                Debug.Log(
                    $"[StatusSaveFile] Clear reward already claimed. " +
                    $"last_getting_gold={data.last_getting_gold}"
                );

                return data.last_getting_gold;
            }

            int rewardGold = GetClearRewardGold();

            data.gold = Mathf.Max(0, data.gold + rewardGold);
            data.last_getting_gold = rewardGold;
            data.clear_reward_claimed = true;

            Save();

            Debug.Log(
                $"[StatusSaveFile] Clear reward claimed. " +
                $"broken_traps={data.broken_traps}, reward={rewardGold}, totalGold={data.gold}"
            );

            return rewardGold;
        }

        public void ResetClearRewardState()
        {
            EnsureNotNull();

            data.clear_reward_claimed = false;
            data.last_getting_gold = 0;

            Save();
        }

        public float GetClearTimeSeconds()
        {
            EnsureNotNull();
            return Mathf.Max(0f, data.clear_time_seconds);
        }

        public int GetHitCount()
        {
            EnsureNotNull();
            return Mathf.Max(0, data.hit_count);
        }

        public void AddClearTime(float deltaSeconds, bool saveImmediately = false)
        {
            EnsureNotNull();

            if (deltaSeconds <= 0f)
                return;

            data.clear_time_seconds = Mathf.Max(0f, data.clear_time_seconds + deltaSeconds);

            if (saveImmediately)
                Save();
        }

        public void AddHitCount(int amount = 1)
        {
            EnsureNotNull();

            if (amount <= 0)
                return;

            data.hit_count += amount;

            Save();

            Debug.Log($"[StatusSaveFile] hit_count increased: {data.hit_count}");
        }

        public void ResetRunResultStats()
        {
            TryUpdateDungeonProgress(current =>
            {
                DungeonProgressService.CaptureActiveRun(current);
                current.broken_traps = 0; current.clear_reward_claimed = false; current.last_getting_gold = 0;
                current.clear_time_seconds = 0; current.hit_count = 0;
                current.active_run = new ActiveDungeonRunData();
                return true;
            });
        }

        /// <summary>
        /// 지정한 퀵슬롯에 저장된 소비 아이템 키를 반환합니다.
        /// 저장 파일에는 raw ID가 저장되며, 반환값은 item:000 형식입니다.
        /// </summary>
        public string GetQuickSlotItemKey(int slotNumber)
        {
            EnsureNotNull();

            if (slotNumber < 1 || slotNumber > 5)
                return GameDatabase.NoneKey;

            string slotKey = $"slot{slotNumber}";

            foreach (Dictionary<string, string> map in data.equips.item)
            {
                if (map == null)
                    continue;

                if (!map.TryGetValue(slotKey, out string rawId))
                    continue;

                rawId = GameDatabase.NormalizeId(rawId);

                if (string.IsNullOrWhiteSpace(rawId) ||
                    rawId == GameDatabase.NoneKey)
                {
                    return GameDatabase.NoneKey;
                }

                return GameDatabase.MakeKey(
                    GameDatabase.CategoryItem,
                    rawId
                );
            }

            return GameDatabase.NoneKey;
        }

        /// <summary>
        /// 지정한 퀵슬롯에 소비 아이템을 등록하고 즉시 저장합니다.
        /// itemKey는 item:000 또는 000 형식을 받을 수 있습니다.
        /// </summary>
        public bool SetQuickSlotItemKey(int slotNumber, string itemKey)
        {
            EnsureNotNull();

            if (slotNumber < 1 || slotNumber > 5)
            {
                Debug.LogWarning(
                    $"[StatusSaveFile] 잘못된 퀵 슬롯 번호입니다: {slotNumber}"
                );

                return false;
            }

            string rawId;

            if (string.IsNullOrWhiteSpace(itemKey) ||
                itemKey == GameDatabase.NoneKey)
            {
                rawId = GameDatabase.NoneKey;
            }
            else if (GameDatabase.TryParseKey(
                         itemKey,
                         out string category,
                         out string parsedId))
            {
                if (category != GameDatabase.CategoryItem)
                {
                    Debug.LogWarning(
                        $"[StatusSaveFile] 퀵 슬롯에는 아이템만 등록할 수 있습니다: {itemKey}"
                    );

                    return false;
                }

                rawId = GameDatabase.NormalizeId(parsedId);
            }
            else
            {
                // "000"처럼 raw ID를 직접 전달한 경우입니다.
                rawId = GameDatabase.NormalizeId(itemKey);
            }

            if (string.IsNullOrWhiteSpace(rawId))
                rawId = GameDatabase.NoneKey;

            string slotKey = $"slot{slotNumber}";
            Dictionary<string, string> targetMap = null;

            // 같은 slotKey가 중복돼 있다면 첫 번째 항목만 유지합니다.
            foreach (Dictionary<string, string> map in data.equips.item)
            {
                if (map == null || !map.ContainsKey(slotKey))
                    continue;

                if (targetMap == null)
                {
                    targetMap = map;
                }
                else
                {
                    map.Remove(slotKey);
                }
            }

            if (targetMap == null)
            {
                targetMap = new Dictionary<string, string>();
                data.equips.item.Add(targetMap);
            }

            targetMap[slotKey] = rawId;

            Save();
            return true;
        }

        /// <summary>
        /// 지정한 퀵슬롯의 등록을 해제합니다.
        /// </summary>
        public bool ClearQuickSlotItem(int slotNumber)
        {
            return SetQuickSlotItemKey(
                slotNumber,
                GameDatabase.NoneKey
            );
        }

        public string GetActiveSavePath()
        {
            return ActiveSavePath;
        }

        public string GetSourceSavePath()
        {
            return EnsureStorage().DefaultPath;
        }

        private void OnApplicationQuit()
        {
            if (Instance != this || isQuitting) return;
            isQuitting = true;
            Save();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (Instance != this || isQuitting || applicationPaused == pauseStatus) return;
            CapturePlayTime();
            applicationPaused = pauseStatus;
            playTimeSession?.BindRun(data, TimingScene);
            if (pauseStatus) Save();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Instance = null;

            if (!isQuitting && logSavePath)
                Debug.Log($"[StatusSaveFile] Destroyed. ID={GetInstanceID()}");
        }

    }
}
