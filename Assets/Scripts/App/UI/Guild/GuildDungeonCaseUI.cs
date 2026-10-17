using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI.Guild
{
    [RequireComponent(typeof(Button))]
    public sealed class GuildDungeonCaseUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private GameObject completionStamp;
        private GuildPageUI page;
        public string DungeonId { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsCompleted { get; private set; }

        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Select);
        private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Select);

        public void ConfigureArtwork(Image image, TMP_Text label, Sprite normal, Sprite selected)
        { background = image; numberLabel = label; normalSprite = normal; selectedSprite = selected; }

        public void ConfigureCompletionStamp(GameObject stamp)
        { completionStamp = stamp; SetCompleted(false); }

        public void Bind(GuildPageUI owner, GuildDungeonDefinition definition)
        {
            page = owner; DungeonId = definition.id;
            numberLabel.text = $"No.{definition.caseNumber:00}";
            SetCompleted(false);
        }

        public void SetCompleted(bool value)
        {
            IsCompleted = value;
            if (completionStamp != null) completionStamp.SetActive(value);
        }

        public void SetSelected(bool value)
        {
            IsSelected = value;
            background.sprite = value ? selectedSprite : normalSprite;
        }

        public void Select() { if (page != null) page.SelectDungeon(DungeonId); }
    }
}
