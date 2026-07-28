using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Dungeon.UI
{
    /// <summary>
    /// Status 창의 상단 탭 버튼을 관리합니다.
    ///
    /// 하나의 탭을 선택하면:
    /// - RightPanel의 ContentPage 중 하나만 켭니다.
    /// - LeftPanel의 Page 중 하나만 켭니다.
    /// - 선택된 버튼의 표시 상태를 갱신합니다.
    ///
    /// 페이지 이름에 의존하지 않고 Inspector에 직접 연결해서 사용하는 방식입니다.
    /// </summary>
    public class StatusTabSwitcher : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private List<TabBinding> tabs = new();

        [Header("Default")]
        [SerializeField] private int defaultTabIndex = 0;
        [SerializeField] private bool selectDefaultOnEnable = true;

        [Header("Button Visual")]
        [SerializeField] private bool updateButtonInteractable = true;

        [Tooltip("선택된 버튼은 Interactable을 끄고, 선택되지 않은 버튼은 Interactable을 켭니다.")]
        [SerializeField] private bool selectedButtonNotInteractable = true;

        private readonly List<UnityAction> registeredActions = new();
        private int currentIndex = -1;

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            if (selectDefaultOnEnable)
                SelectTab(defaultTabIndex);
            else if (currentIndex >= 0)
                SelectTab(currentIndex);
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void BindButtons()
        {
            UnbindButtons();

            registeredActions.Clear();

            for (int i = 0; i < tabs.Count; i++)
            {
                int capturedIndex = i;
                Button button = tabs[i].button;

                if (button == null)
                {
                    registeredActions.Add(null);
                    continue;
                }

                UnityAction action = () => SelectTab(capturedIndex);
                button.onClick.AddListener(action);
                registeredActions.Add(action);
            }
        }

        private void UnbindButtons()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (i >= registeredActions.Count)
                    break;

                Button button = tabs[i].button;
                UnityAction action = registeredActions[i];

                if (button != null && action != null)
                    button.onClick.RemoveListener(action);
            }
        }

        public void SelectTab(int index)
        {
            if (tabs == null || tabs.Count == 0)
                return;

            if (index < 0 || index >= tabs.Count)
            {
                Debug.LogWarning($"[StatusTabSwitcher] 잘못된 탭 index입니다: {index}");
                return;
            }

            currentIndex = index;

            for (int i = 0; i < tabs.Count; i++)
            {
                bool selected = i == index;
                TabBinding tab = tabs[i];

                if (tab.rightContentPage != null)
                    tab.rightContentPage.SetActive(selected);

                if (tab.leftPage != null)
                    tab.leftPage.SetActive(selected);

                RefreshButtonState(tab, selected);
            }

            Debug.Log($"[StatusTabSwitcher] Selected tab: {tabs[index].tabName}");
        }

        private void RefreshButtonState(TabBinding tab, bool selected)
        {
            if (tab == null || tab.button == null)
                return;

            if (updateButtonInteractable)
            {
                tab.button.interactable = selectedButtonNotInteractable
                    ? !selected
                    : true;
            }

            if (tab.selectedVisual != null)
                tab.selectedVisual.SetActive(selected);

            if (tab.normalVisual != null)
                tab.normalVisual.SetActive(!selected);
        }

        public void SelectStatus()
        {
            SelectTab(0);
        }

        public void SelectEquipment()
        {
            SelectTab(1);
        }

        public void SelectModule()
        {
            SelectTab(2);
        }

        public void SelectItem()
        {
            SelectTab(3);
        }
    }

    [Serializable]
    public class TabBinding
    {
        [Tooltip("디버그용 이름입니다. 예: Status, Equipment, Module, Item")]
        public string tabName;

        [Tooltip("상단 탭 버튼입니다.")]
        public Button button;

        [Tooltip("RightPanel/ContentPanel 아래의 페이지입니다.")]
        public GameObject rightContentPage;

        [Tooltip("LeftPanel 아래의 페이지입니다.")]
        public GameObject leftPage;

        [Header("Optional Visuals")]
        [Tooltip("선택 상태 전용 오브젝트가 있으면 연결합니다. 없으면 비워둬도 됩니다.")]
        public GameObject selectedVisual;

        [Tooltip("일반 상태 전용 오브젝트가 있으면 연결합니다. 없으면 비워둬도 됩니다.")]
        public GameObject normalVisual;
    }
}