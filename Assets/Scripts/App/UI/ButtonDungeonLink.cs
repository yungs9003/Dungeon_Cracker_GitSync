using UnityEngine;

namespace Dungeon.UI
{
public class ButtonDungeonLink : MonoBehaviour
{
    [SerializeField] private DungeonPopupPanelController popup;

    [Header("Dungeon ID")]
    [SerializeField] private string dungeonId; // 예: "DUNGEON_001"

    public void OpenPopup()
    {
        if (popup == null)
        {
            Debug.LogError("[ButtonDungeonLink] DungeonPopupPanelController is not assigned.");
            return;
        }

        popup.OpenDungeon(dungeonId);
    }
}
}