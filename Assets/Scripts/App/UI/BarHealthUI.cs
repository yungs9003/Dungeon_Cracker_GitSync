using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Dungeon.Player;

namespace Dungeon.UI
{
    /// <summary>
    /// 사각형 막대형 HP UI를 제어합니다.
    /// 
    /// 권장 계층 구조:
    /// HealthPanelRoot (Image: 흰색 패널)
    /// ├─ HealthBarBack   (Image: 검은색 배경 막대)
    /// │  ├─ HealthFill   (Image: 초/노/빨 HP 막대, Filled-Horizontal)
    /// │  └─ Dividers     (BarHealthDividersUI, 10HP/50HP 경계선)
    /// ├─ HPText          (TMP_Text: 현재HP/최대HP)
    /// └─ TrapGoldText    (TMP_Text: 부순 함정 보너스 골드)
    /// </summary>
    public class BarHealthUI : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("플레이어의 Health 컴포넌트입니다. 비워두면 씬에서 자동 탐색합니다.")]
        public Health health;

        [Tooltip("검은색 배경 막대입니다. 체력이 줄어든 영역은 이 이미지가 드러납니다.")]
        public Image barBackground;

        [Tooltip("실제 HP 비율만큼 채워지는 막대 이미지입니다. 기존 원형 UI의 healthCircle 참조를 유지하기 위해 필드 이름은 healthCircle로 유지했습니다.")]
        public Image healthCircle;

        [Tooltip("현재 체력/최대 체력을 표시하는 텍스트입니다.")]
        public TMP_Text healthText;

        [Tooltip("10HP마다 얇은 선, 50HP마다 굵은 선을 생성하는 UI입니다.")]
        public BarHealthDividersUI dividersUI;

        [Header("Bar Colors")]
        public Color backgroundColor = Color.black;
        public Color normalColor = Color.green;
        public Color warningColor = Color.yellow;
        public Color dangerColor = Color.red;

        [Header("Thresholds")]
        [Range(0f, 1f)] public float yellowThreshold = 0.5f;
        [Range(0f, 1f)] public float redThreshold = 0.15f;

        [Header("HP Text")]
        public bool autoResizeText = true;
        public float textFontMin = 18f;
        public float textFontMax = 42f;
        public string hpTextFormat = "{0}/{1}";

        [Header("Broken Trap Gold")]
        [Tooltip("부순 함정 보너스 골드를 표시할 텍스트입니다.")]
        public TMP_Text brokenTrapGoldText;

        [Tooltip("필요하면 씬의 SaveManager를 직접 드래그해서 넣을 수 있습니다. 비워도 자동 탐색합니다.")]
        public MonoBehaviour saveManagerOverride;

        [Tooltip("부순 함정 1개당 표시할 골드입니다.")]
        public int goldPerBrokenTrap = 50;

        [Tooltip("기본 보상까지 함께 표시하고 싶으면 500처럼 입력합니다. 부순 함정 보너스만 보려면 0으로 둡니다.")]
        public int baseGoldReward = 0;

        [Tooltip("{0} 위치에 계산된 골드가 들어갑니다.")]
        public string brokenTrapGoldFormat = "획득 골드 : {0}";

        [Tooltip("런타임 중 함정 파괴 수를 다시 읽는 주기입니다.")]
        public float brokenTrapRefreshInterval = 0.25f;

        private float nextBrokenTrapRefreshTime;
        private int lastDisplayedBrokenTrapCount = int.MinValue;

        private void Awake()
        {
            AutoBindReferences();
            SetupBarImages();
            SetupTexts();
        }

        private void OnEnable()
        {
            AutoBindReferences();

            if (health != null)
            {
                health.onHealthChanged.AddListener(OnHealthChanged);
                OnHealthChanged(health.currentHP, health.maxHP);
            }

            RefreshBrokenTrapGold(force: true);
        }

        private void OnDisable()
        {
            if (health != null)
                health.onHealthChanged.RemoveListener(OnHealthChanged);
        }

        private void Update()
        {
            // broken_traps는 Health 이벤트와 별개로 바뀌므로 일정 주기로만 가볍게 갱신합니다.
            if (brokenTrapGoldText == null) return;
            if (Time.unscaledTime < nextBrokenTrapRefreshTime) return;

            RefreshBrokenTrapGold(force: false);
            nextBrokenTrapRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, brokenTrapRefreshInterval);
        }

        /// <summary>
        /// Inspector에 참조를 넣지 않았을 때 이름/타입 기준으로 필요한 UI 요소를 자동 연결합니다.
        /// 자동 연결이 실패한 항목은 Inspector에서 직접 넣으면 됩니다.
        /// </summary>
        private void AutoBindReferences()
        {
            if (health == null)
                health = FindAnyObjectByType<Health>();

            if (barBackground == null)
                barBackground = FindChildImageByName("HealthBarBack", "HPBarBack", "BarBackground");

            if (healthCircle == null)
                healthCircle = FindChildImageByName("HealthFill", "HPFill", "HealthRing", "HealthRingRoot");

            if (healthText == null)
                healthText = FindChildTextByName("HPText", "HealthText", "HPStageText");

            if (brokenTrapGoldText == null)
                brokenTrapGoldText = FindChildTextByName("TrapGoldText", "BrokenTrapGoldText", "GoldText");

            if (dividersUI == null)
                dividersUI = GetComponentInChildren<BarHealthDividersUI>(true);
        }

        /// <summary>
        /// 검은 배경 막대와 HP Fill 막대의 Image 설정을 막대형 UI에 맞춥니다.
        /// HP Fill은 Left → Right 방향의 Filled 이미지로 사용합니다.
        /// </summary>
        private void SetupBarImages()
        {
            if (barBackground != null)
            {
                barBackground.color = backgroundColor;
                barBackground.raycastTarget = false;
            }

            if (healthCircle != null)
            {
                healthCircle.type = Image.Type.Filled;
                healthCircle.fillMethod = Image.FillMethod.Horizontal;
                healthCircle.fillOrigin = 0; // 0 = Left
                healthCircle.fillAmount = 1f;
                healthCircle.raycastTarget = false;
            }
        }

        /// <summary>
        /// 체력 텍스트와 보상 텍스트의 기본 표시 방식을 설정합니다.
        /// </summary>
        private void SetupTexts()
        {
            if (healthText != null)
            {
                healthText.enableAutoSizing = autoResizeText;
                healthText.fontSizeMin = textFontMin;
                healthText.fontSizeMax = textFontMax;
                healthText.alignment = TextAlignmentOptions.Center;
                healthText.overflowMode = TextOverflowModes.Overflow;
            }

            if (brokenTrapGoldText != null)
                brokenTrapGoldText.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// Health 컴포넌트의 체력 변경 이벤트를 받아 막대, 색상, 텍스트, Divider를 동기화합니다.
        /// </summary>
        private void OnHealthChanged(int current, int max)
        {
            max = Mathf.Max(1, max);
            current = Mathf.Clamp(current, 0, max);

            float ratio = Mathf.Clamp01((float)current / max);

            if (healthCircle != null)
            {
                healthCircle.fillAmount = ratio;
                healthCircle.color = GetBarColor(ratio);
            }

            if (healthText != null)
                healthText.text = string.Format(hpTextFormat, current, max);

            if (dividersUI != null)
                dividersUI.Sync(max);
        }

        /// <summary>
        /// 체력 비율에 따라 HP Fill 색상을 선택합니다.
        /// 기본값: 초록 → 노랑 → 빨강.
        /// </summary>
        private Color GetBarColor(float ratio)
        {
            float safeThreshold = Mathf.Clamp01(yellowThreshold);
            float dangerThreshold = Mathf.Clamp01(redThreshold);

            if (safeThreshold < dangerThreshold)
                safeThreshold = dangerThreshold;

            if (ratio > safeThreshold) return normalColor;
            if (ratio > dangerThreshold) return warningColor;
            return dangerColor;
        }

        /// <summary>
        /// savefile의 broken_traps 값을 읽어서 "부순 함정 수 * goldPerBrokenTrap" 보상을 표시합니다.
        /// 현재 프로젝트의 SaveManager API가 바뀌어도 깨지지 않도록 reflection과 JSON fallback을 함께 사용합니다.
        /// </summary>
        public void RefreshBrokenTrapGold(bool force = true)
        {
            if (brokenTrapGoldText == null) return;

            int brokenTrapCount = 0;
            TryGetBrokenTrapCount(out brokenTrapCount);

            if (!force && brokenTrapCount == lastDisplayedBrokenTrapCount)
                return;

            lastDisplayedBrokenTrapCount = brokenTrapCount;

            int gold = baseGoldReward + Mathf.Max(0, brokenTrapCount) * Mathf.Max(0, goldPerBrokenTrap);
            brokenTrapGoldText.text = string.Format(brokenTrapGoldFormat, gold);
        }

        /// <summary>
        /// 1순위: Inspector에 지정한 SaveManager
        /// 2순위: 씬의 SaveManager류 MonoBehaviour
        /// 3순위: persistentDataPath / StreamingAssets의 JSON 파일
        /// 순서로 broken_traps 값을 찾습니다.
        /// </summary>
        private bool TryGetBrokenTrapCount(out int count)
        {
            count = 0;

            if (saveManagerOverride != null && TryFindIntMemberRecursive(saveManagerOverride, "broken_traps", out count))
                return true;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // SaveManager 이름을 가진 오브젝트를 먼저 확인합니다.
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                string typeName = mb.GetType().Name;

                if (typeName.IndexOf("SaveManager", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    TryFindIntMemberRecursive(mb, "broken_traps", out count))
                    return true;
            }

            // 이름은 다르지만 save data를 들고 있는 매니저가 있을 수 있어 한 번 더 넓게 탐색합니다.
            foreach (var mb in behaviours)
            {
                if (mb == null) continue;
                if (TryFindIntMemberRecursive(mb, "broken_traps", out count))
                    return true;
            }

            return TryGetBrokenTrapCountFromJson(out count);
        }

        /// <summary>
        /// private field/property 안쪽에 들어있는 save data까지 재귀적으로 탐색합니다.
        /// 예: SaveManager.data.broken_traps 같은 구조를 찾기 위한 처리입니다.
        /// </summary>
        private static bool TryFindIntMemberRecursive(object root, string targetName, out int value)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return TryFindIntMemberRecursiveInternal(root, targetName, out value, visited, depth: 0);
        }

        private static bool TryFindIntMemberRecursiveInternal(object obj, string targetName, out int value, HashSet<object> visited, int depth)
        {
            value = 0;
            if (obj == null) return false;
            if (depth > 4) return false;

            Type type = obj.GetType();
            if (IsTerminalType(type)) return false;
            if (!visited.Add(obj)) return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in type.GetFields(flags))
            {
                object fieldValue;
                try { fieldValue = field.GetValue(obj); }
                catch { continue; }

                if (string.Equals(field.Name, targetName, StringComparison.OrdinalIgnoreCase) && TryConvertToInt(fieldValue, out value))
                    return true;

                if (ShouldSearchChild(field.FieldType) && TryFindIntMemberRecursiveInternal(fieldValue, targetName, out value, visited, depth + 1))
                    return true;
            }

            foreach (var prop in type.GetProperties(flags))
            {
                if (prop.GetIndexParameters().Length > 0) continue;

                object propValue;
                try { propValue = prop.GetValue(obj); }
                catch { continue; }

                if (string.Equals(prop.Name, targetName, StringComparison.OrdinalIgnoreCase) && TryConvertToInt(propValue, out value))
                    return true;

                if (ShouldSearchChild(prop.PropertyType) && TryFindIntMemberRecursiveInternal(propValue, targetName, out value, visited, depth + 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Reflection으로 찾지 못했을 때 JSON 파일에서 직접 "broken_traps" 값을 찾습니다.
        /// </summary>
        private static bool TryGetBrokenTrapCountFromJson(out int count)
        {
            count = 0;
            string[] candidateFiles =
            {
                Path.Combine(Application.persistentDataPath, "savefile_dev_runtime.json"),
                Path.Combine(Application.persistentDataPath, "savefile.json"),
                Path.Combine(Application.persistentDataPath, "player_save.json"),
                Path.Combine(Application.streamingAssetsPath, "savefile.json"),
                Path.Combine(Application.streamingAssetsPath, "savefile_dev_runtime.json")
            };

            foreach (string path in candidateFiles)
            {
                if (TryReadBrokenTrapCountFromJsonFile(path, out count))
                    return true;
            }

            return false;
        }

        private static bool TryReadBrokenTrapCountFromJsonFile(string path, out int count)
        {
            count = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return false;

                string json = File.ReadAllText(path);
                Match match = Regex.Match(json, "\\\"broken_traps\\\"\\s*:\\s*(\\d+)");

                if (!match.Success)
                    return false;

                return int.TryParse(match.Groups[1].Value, out count);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertToInt(object obj, out int value)
        {
            value = 0;
            if (obj == null) return false;

            if (obj is int i)
            {
                value = i;
                return true;
            }

            if (obj is long l)
            {
                value = l > int.MaxValue ? int.MaxValue : Mathf.Max(0, (int)l);
                return true;
            }

            return int.TryParse(obj.ToString(), out value);
        }

        private static bool ShouldSearchChild(Type type)
        {
            if (type == null) return false;
            if (IsTerminalType(type)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) && !typeof(MonoBehaviour).IsAssignableFrom(type)) return false;
            if (type.Namespace != null && type.Namespace.StartsWith("System")) return false;
            return true;
        }

        private static bool IsTerminalType(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        private Image FindChildImageByName(params string[] names)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (string target in names)
            {
                foreach (var img in images)
                {
                    if (img != null && string.Equals(img.name, target, StringComparison.OrdinalIgnoreCase))
                        return img;
                }
            }
            return null;
        }

        private TMP_Text FindChildTextByName(params string[] names)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (string target in names)
            {
                foreach (var text in texts)
                {
                    if (text != null && string.Equals(text.name, target, StringComparison.OrdinalIgnoreCase))
                        return text;
                }
            }
            return null;
        }

        /// <summary>
        /// object 참조를 기준으로 HashSet을 만들기 위한 comparer입니다.
        /// save data 순환 참조 때문에 재귀가 무한히 도는 것을 방지합니다.
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
