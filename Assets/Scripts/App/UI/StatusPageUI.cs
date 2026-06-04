using TMPro;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.UI
{
    public class StatusPageUI : MonoBehaviour
    {
        [Header("Save File")]
        [SerializeField] private StatusSaveFile saveFile;

        [Header("Player Health")]
        [SerializeField] private Health health;

        [Header("Left Info")]
        [SerializeField] private TMP_Text nameValueText;
        [SerializeField] private TMP_Text goldValueText;

        [Header("Fixed Top Stats")]
        [SerializeField] private TMP_Text currentHpText;
        [SerializeField] private TMP_Text maxHpText;
        [SerializeField] private TMP_Text physicalGuardValueText;
        [SerializeField] private TMP_Text magicGuardValueText;

        [Header("Scrollable Rows")]
        [SerializeField] private StatusInfoRowUI speedRow;
        [SerializeField] private StatusInfoRowUI poisonRow;
        [SerializeField] private StatusInfoRowUI electricRow;
        [SerializeField] private StatusInfoRowUI emberRow;
        [SerializeField] private StatusInfoRowUI freezeRow;

        private void Awake()
        {
            if (saveFile == null)
                saveFile = StatusSaveFile.Instance;

            if (health == null)
                health = FindFirstObjectByType<Health>();
        }

        private void OnEnable()
        {
            if (saveFile == null)
                saveFile = StatusSaveFile.Instance;

            if (saveFile != null)
                saveFile.OnSaveDataChanged += RefreshFromSaveData;

            if (health == null)
                health = FindFirstObjectByType<Health>();

            if (health != null)
                health.onHealthChanged.AddListener(OnHealthChanged);

            Refresh();
        }

        private void OnDisable()
        {
            if (saveFile != null)
                saveFile.OnSaveDataChanged -= RefreshFromSaveData;

            if (health != null)
                health.onHealthChanged.RemoveListener(OnHealthChanged);
        }

        public void Refresh()
        {
            if (saveFile == null)
                saveFile = StatusSaveFile.Instance;

            if (saveFile == null)
                return;

            RefreshFromSaveData(saveFile.Data);
            RefreshHp();
        }

        private void RefreshFromSaveData(StatusSaveData data)
        {
            if (data == null)
                return;

            if (nameValueText != null)
                nameValueText.text = data.user_name;

            if (goldValueText != null)
                goldValueText.text = $"{data.gold} G";

            if (maxHpText != null)
                maxHpText.text = data.main_stats.max_HP.ToString();

            if (physicalGuardValueText != null)
                physicalGuardValueText.text = data.main_stats.physical_resist.ToString();

            if (magicGuardValueText != null)
                magicGuardValueText.text = data.main_stats.magical_resist.ToString();

            if (speedRow != null)
                speedRow.SetValue(data.sub_stats.speed);

            if (poisonRow != null)
                poisonRow.SetValue(data.sub_stats.GetPoisonResist());

            if (electricRow != null)
                electricRow.SetValue(data.sub_stats.electric_resist);

            if (emberRow != null)
                emberRow.SetValue(data.sub_stats.amber_resist);

            if (freezeRow != null)
                freezeRow.SetValue(data.sub_stats.freeze_resist);

            RefreshHp();
        }

        private void OnHealthChanged(int current, int max)
        {
            RefreshHp();
        }

        private void RefreshHp()
        {
            int currentHp = 0;

            if (health != null)
                currentHp = health.currentHP;
            else if (saveFile != null)
                currentHp = saveFile.Data.main_stats.max_HP;

            if (currentHpText != null)
                currentHpText.text = currentHp.ToString();
        }
    }
}