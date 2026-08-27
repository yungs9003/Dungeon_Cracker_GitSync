using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Dungeon.Core;

namespace Dungeon.Player
{
    /// <summary>
    /// 숫자 키 1~5를 눌러 savefile의 slot1~slot5에 등록된 소비 아이템을 사용합니다.
    ///
    /// 현재 버전에서는 아이템 고유 효과는 아직 실행하지 않고,
    /// 정상적인 소비 아이템 사용으로 판단되면 보유 수량을 1 감소시켜 즉시 저장합니다.
    /// 이후 TryApplyItemEffect()에 아이템별 효과를 추가할 수 있습니다.
    /// </summary>
    public class QuickSlotItemUseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDatabase database;

        [Header("Input")]
        [Tooltip("숫자 키를 통한 퀵슬롯 사용을 허용합니다.")]
        [SerializeField] private bool inputEnabled = true;

        [Tooltip("키보드 위쪽 숫자 키와 함께 숫자패드 1~5도 허용합니다.")]
        [SerializeField] private bool allowNumpadKeys = true;

        [Header("Cooldown")]
        [Tooltip("소비 아이템을 성공적으로 사용한 뒤 다음 아이템을 사용할 수 있을 때까지의 공용 쿨다운입니다.")]
        [SerializeField, Min(0f)] private float useCooldownSeconds = 1f;

        [Header("Empty Count Policy")]
        [Tooltip("보유 수량이 0이 되면 해당 아이템이 등록된 퀵슬롯을 자동으로 비웁니다.")]
        [SerializeField] private bool clearQuickSlotsWhenDepleted = true;

        [Header("Current Placeholder Effect")]
        [Tooltip("아직 효과가 구현되지 않은 소비 아이템도 정상 사용으로 처리하여 수량을 감소시킵니다.")]
        [SerializeField] private bool consumeItemsWithoutImplementedEffect = true;

        [Header("Debug")]
        [SerializeField] private bool logActions = true;

        private float nextUsableTime;
        private Coroutine activeEffectRoutine;

        public bool InputEnabled => inputEnabled;
        public float RemainingCooldown =>
            Mathf.Max(0f, nextUsableTime - Time.time);

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (!inputEnabled)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            int pressedSlot = GetPressedSlotNumber(keyboard);
            if (pressedSlot == 0)
                return;

            TryUseSlot(pressedSlot);
        }

        /// <summary>
        /// 상태창이나 일시정지 UI가 열렸을 때 외부에서 입력을 막을 수 있습니다.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
        }

        /// <summary>
        /// slot1~slot5 중 하나를 사용합니다.
        /// 성공했을 때만 공용 쿨다운이 시작됩니다.
        /// </summary>
        public bool TryUseSlot(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > 5)
                return false;

            if (Time.time < nextUsableTime)
            {
                if (logActions)
                {
                    Debug.Log(
                        $"[QuickSlotItemUseController] Cooldown active. " +
                        $"remaining={RemainingCooldown:F2}s"
                    );
                }

                return false;
            }

            ResolveReferences();

            StatusSaveFile saveFile = GetSaveFile();
            if (saveFile == null || saveFile.Data == null)
            {
                Debug.LogWarning(
                    "[QuickSlotItemUseController] StatusSaveFile을 찾지 못했습니다."
                );
                return false;
            }

            if (database == null)
            {
                Debug.LogWarning(
                    "[QuickSlotItemUseController] GameDatabase를 찾지 못했습니다."
                );
                return false;
            }

            string itemKey = saveFile.GetQuickSlotItemKey(slotNumber);
            if (string.IsNullOrWhiteSpace(itemKey) ||
                itemKey == GameDatabase.NoneKey)
            {
                if (logActions)
                {
                    Debug.Log(
                        $"[QuickSlotItemUseController] Slot is empty. " +
                        $"slot={slotNumber}"
                    );
                }
                return false;
            }

            if (!database.TryGetItemByKey(
                    itemKey,
                    out ItemDefinition definition) ||
                definition == null)
            {
                Debug.LogWarning(
                    $"[QuickSlotItemUseController] DB에서 아이템을 찾지 못했습니다. " +
                    $"slot={slotNumber}, key={itemKey}"
                );
                return false;
            }

            string normalizedType =
                GameDatabase.NormalizeCategoryLikeValue(definition.type);

            if (normalizedType != "consumable")
            {
                Debug.LogWarning(
                    $"[QuickSlotItemUseController] 소비 아이템이 아닙니다. " +
                    $"slot={slotNumber}, key={itemKey}, type={definition.type}"
                );
                return false;
            }

            if (!TryGetRawItemId(itemKey, out string rawId))
            {
                Debug.LogWarning(
                    $"[QuickSlotItemUseController] 아이템 ID 변환에 실패했습니다: {itemKey}"
                );
                return false;
            }

            int ownedCount = GetOwnedItemCount(saveFile.Data, rawId);
            if (ownedCount <= 0)
            {
                // 세이브에 퀵슬롯만 남고 실제 아이템은 없는 오래된/비정상 상태를 정리합니다.
                if (clearQuickSlotsWhenDepleted)
                {
                    ClearQuickSlotsUsingItem(saveFile.Data, rawId);
                    saveFile.Save();
                }

                if (logActions)
                {
                    Debug.Log(
                        $"[QuickSlotItemUseController] Item count is zero. " +
                        $"slot={slotNumber}, key={itemKey}"
                    );
                }

                return false;
            }

            // 효과를 적용하지 못한 경우에는 수량과 쿨다운을 소모하지 않습니다.
            if (!TryApplyItemEffect(definition))
                return false;

            if (!TryDecreaseOwnedItemCount(
                    saveFile.Data,
                    rawId,
                    1,
                    out int remainingCount))
            {
                Debug.LogWarning(
                    $"[QuickSlotItemUseController] 아이템 수량 감소에 실패했습니다. " +
                    $"slot={slotNumber}, key={itemKey}"
                );
                return false;
            }

            if (remainingCount <= 0 && clearQuickSlotsWhenDepleted)
                ClearQuickSlotsUsingItem(saveFile.Data, rawId);

            // Save()가 파일 기록과 OnSaveDataChanged 이벤트를 함께 실행하므로
            // HUD/상태창의 아이콘과 수량 표시도 같은 저장값으로 갱신됩니다.
            saveFile.Save();

            nextUsableTime = Time.time + Mathf.Max(0f, useCooldownSeconds);

            if (logActions)
            {
                Debug.Log(
                    $"[QuickSlotItemUseController] Item used. " +
                    $"slot={slotNumber}, key={itemKey}, " +
                    $"name={definition.name}, remaining={remainingCount}, " +
                    $"cooldown={useCooldownSeconds:F2}s"
                );
            }

            return true;
        }

        private bool TryGetPlayerHealth(out Health health)
        {
            health = FindFirstObjectByType<Health>();
            return health != null;
        }

        /// <summary>
        /// 아이템별 실제 효과를 정의하는 지점입니다.
        /// 현재는 효과가 없더라도 사용 성공으로 처리할 수 있습니다.
        ///
        /// 예시로 급성치료제(item:000)를 구현할 때는 Health 참조를 얻은 뒤
        /// health.Heal(10)을 호출하고 true를 반환하면 됩니다.
        /// 사용할 수 없는 상태라면 false를 반환하여 수량 소모를 막을 수 있습니다.
        /// </summary>
        private bool TryApplyItemEffect(ItemDefinition definition)
        {
            if (definition == null)
                return false;

            switch (GameDatabase.NormalizeId(definition.id))
            {
                // 아직 아이템별 효과는 구현하지 않습니다.
                case "000":
                    if (!TryGetPlayerHealth(out Health health))
                        return false;
                    
                    health.Heal(10);
                    return true;

                case "001":
                    if (!TryGetPlayerHealth(out Health hotHealth))
                        return false;

                    StartHealingOverTime(hotHealth, 5, 10f, 1f);
                    return true;

                case "002":
                    if (!TryGetPlayerHealth(out Health bleedingHealth))
                        return false;

                    bleedingHealth.ClearBleeding();
                    return true;

                case "003":
                    if (!TryGetPlayerHealth(out Health physicalResistanceHealth))
                        return false;

                    physicalResistanceHealth.StartPhysicalResistance(10, 5f);
                    return true;

                case "004":
                    if (!TryGetPlayerHealth(out Health magicalResistanceHealth))
                        return false;

                    magicalResistanceHealth.StartMagicalResistance(10, 5f);
                    return true;

                default:
                    if (logActions)
                    {
                        Debug.Log(
                            $"[QuickSlotItemUseController] " +
                            $"No item effect implemented yet: " +
                            $"id={definition.id}, name={definition.name}"
                        );
                    }

                    return consumeItemsWithoutImplementedEffect;
            }
        }

        private void StartHealingOverTime(Health health, int amountPerTick, float duration, float tickInterval)
        {
            if (activeEffectRoutine != null)
                StopCoroutine(activeEffectRoutine);

            activeEffectRoutine = StartCoroutine(HealOverTimeRoutine(health, amountPerTick, duration, tickInterval));
        }

        private IEnumerator HealOverTimeRoutine(Health health, int amountPerTick, float duration, float tickInterval)
        {
            if (health == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                health.Heal(amountPerTick);
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }

            activeEffectRoutine = null;
        }

        private int GetPressedSlotNumber(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame ||
                (allowNumpadKeys && keyboard.numpad1Key.wasPressedThisFrame))
                return 1;

            if (keyboard.digit2Key.wasPressedThisFrame ||
                (allowNumpadKeys && keyboard.numpad2Key.wasPressedThisFrame))
                return 2;

            if (keyboard.digit3Key.wasPressedThisFrame ||
                (allowNumpadKeys && keyboard.numpad3Key.wasPressedThisFrame))
                return 3;

            if (keyboard.digit4Key.wasPressedThisFrame ||
                (allowNumpadKeys && keyboard.numpad4Key.wasPressedThisFrame))
                return 4;

            if (keyboard.digit5Key.wasPressedThisFrame ||
                (allowNumpadKeys && keyboard.numpad5Key.wasPressedThisFrame))
                return 5;

            return 0;
        }

        private void ResolveReferences()
        {
            if (database == null)
                database = GameDatabase.Instance;

            if (database == null)
                database = FindFirstObjectByType<GameDatabase>();
        }

        private static StatusSaveFile GetSaveFile()
        {
            if (StatusSaveFile.Instance != null)
                return StatusSaveFile.Instance;

            return FindFirstObjectByType<StatusSaveFile>();
        }

        private static bool TryGetRawItemId(
            string itemKey,
            out string rawId)
        {
            rawId = GameDatabase.NoneKey;

            if (string.IsNullOrWhiteSpace(itemKey) ||
                itemKey == GameDatabase.NoneKey)
            {
                return false;
            }

            string normalized = GameDatabase.NormalizeKey(itemKey);

            if (GameDatabase.TryParseKey(
                    normalized,
                    out string category,
                    out string parsedId))
            {
                if (category != GameDatabase.CategoryItem)
                    return false;

                rawId = GameDatabase.NormalizeId(parsedId);
                return rawId != GameDatabase.NoneKey;
            }

            rawId = GameDatabase.NormalizeId(normalized);
            return rawId != GameDatabase.NoneKey;
        }

        private static int GetOwnedItemCount(
            StatusSaveData data,
            string rawId)
        {
            if (data?.inventory?.item == null)
                return 0;

            rawId = GameDatabase.NormalizeId(rawId);
            int total = 0;

            foreach (Dictionary<string, int> map in data.inventory.item)
            {
                if (map == null)
                    continue;

                if (map.TryGetValue(rawId, out int count))
                    total += Mathf.Max(0, count);
            }

            return total;
        }

        private static bool TryDecreaseOwnedItemCount(
            StatusSaveData data,
            string rawId,
            int amount,
            out int remainingCount)
        {
            remainingCount = 0;

            if (amount <= 0 || data?.inventory?.item == null)
                return false;

            rawId = GameDatabase.NormalizeId(rawId);
            int total = GetOwnedItemCount(data, rawId);

            if (total < amount)
                return false;

            int amountLeft = amount;

            foreach (Dictionary<string, int> map in data.inventory.item)
            {
                if (map == null || amountLeft <= 0)
                    continue;

                if (!map.TryGetValue(rawId, out int currentCount) ||
                    currentCount <= 0)
                {
                    continue;
                }

                int consumedHere = Mathf.Min(currentCount, amountLeft);
                int newCount = currentCount - consumedHere;
                amountLeft -= consumedHere;

                if (newCount > 0)
                    map[rawId] = newCount;
                else
                    map.Remove(rawId);
            }

            // 비어 버린 Dictionary 항목은 savefile에서 제거합니다.
            for (int i = data.inventory.item.Count - 1; i >= 0; i--)
            {
                Dictionary<string, int> map = data.inventory.item[i];
                if (map == null || map.Count == 0)
                    data.inventory.item.RemoveAt(i);
            }

            remainingCount = total - amount;
            return amountLeft == 0;
        }

        private static void ClearQuickSlotsUsingItem(
            StatusSaveData data,
            string rawId)
        {
            if (data?.equips?.item == null)
                return;

            rawId = GameDatabase.NormalizeId(rawId);

            foreach (Dictionary<string, string> map in data.equips.item)
            {
                if (map == null)
                    continue;

                // Dictionary를 순회하면서 값을 바꾸기 위해 key 목록을 복사합니다.
                var keys = new List<string>(map.Keys);

                foreach (string key in keys)
                {
                    string equippedRawId =
                        GameDatabase.NormalizeId(map[key]);

                    if (equippedRawId == rawId)
                        map[key] = GameDatabase.NoneKey;
                }
            }
        }
    }
}
