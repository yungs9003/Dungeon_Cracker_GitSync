using UnityEngine;

namespace Dungeon.UI;

public class ButtonPopupLink : MonoBehaviour
{
    [SerializeField] private DungeonPopupPanelController popup; // <- 여기!
    [SerializeField] private string dungeonId;                 // 예: DUNGEON_001

    public void OpenPopup()
    {
        if (popup == null)
        {
            Debug.LogError("[ButtonPopupLink] DungeonPopupPanelController is not assigned.");
            return;
        }

        popup.OpenDungeon(dungeonId);
    }
}
