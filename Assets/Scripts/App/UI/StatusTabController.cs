using UnityEngine;

namespace Dungeon.UI
{
    public class StatusTabController : MonoBehaviour
    {
        [Header("Pages")]
        [SerializeField] private GameObject statusPage;
        [SerializeField] private GameObject equipPage;
        [SerializeField] private GameObject modulePage;
        [SerializeField] private GameObject itemPage;

        [Header("Refresh")]
        [SerializeField] private StatusPageUI statusPageUI;

        private void OnEnable()
        {
            ShowStatusPage();
        }

        public void ShowStatusPage()
        {
            SetPage(statusPage);

            if (statusPageUI != null)
                statusPageUI.Refresh();
        }

        public void ShowEquipPage()
        {
            SetPage(equipPage);
        }

        public void ShowModulePage()
        {
            SetPage(modulePage);
        }

        public void ShowItemPage()
        {
            SetPage(itemPage);
        }

        private void SetPage(GameObject target)
        {
            if (statusPage != null)
                statusPage.SetActive(statusPage == target);

            if (equipPage != null)
                equipPage.SetActive(equipPage == target);

            if (modulePage != null)
                modulePage.SetActive(modulePage == target);

            if (itemPage != null)
                itemPage.SetActive(itemPage == target);
        }
    }
}