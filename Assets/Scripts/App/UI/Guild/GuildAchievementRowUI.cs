using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI.Guild
{
    public sealed class GuildAchievementRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text conditionLabel;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private Button verifyButton;
        [SerializeField] private TMP_Text verifyLabel;
        [SerializeField] private Image verifyUnderline;
        [SerializeField] private GameObject certifiedStamp;
        private GuildPageUI page;
        public string AchievementId { get; private set; }
        public GuildAchievementState State { get; private set; }
        public Button VerifyButton => verifyButton;

        private void OnEnable() { if (verifyButton != null) verifyButton.onClick.AddListener(Verify); }
        private void OnDisable() { if (verifyButton != null) verifyButton.onClick.RemoveListener(Verify); }

        public void ConfigureArtwork(TMP_Text condition, TMP_Text reward, Button verify, TMP_Text label, Image underline, GameObject stamp)
        {
            conditionLabel = condition; rewardLabel = reward; verifyButton = verify;
            verifyLabel = label; verifyUnderline = underline; certifiedStamp = stamp;
        }

        public void Bind(GuildPageUI owner, GuildAchievementDefinition definition, int index)
        {
            page = owner; AchievementId = definition.id;
            conditionLabel.text = $"{index + 1}) {definition.condition}";
            rewardLabel.text = $"REWARD : {definition.rewardLabel}";
        }

        public void SetState(GuildAchievementState state)
        {
            State = state;
            bool ready = state == GuildAchievementState.Verifiable;
            verifyButton.interactable = ready;
            Color color = ready ? new Color(.831f, .596f, .235f) : state == GuildAchievementState.Certified ? Color.black : new Color(.48f, .48f, .48f);
            verifyLabel.color = color; verifyUnderline.color = color;
            certifiedStamp.SetActive(state == GuildAchievementState.Certified);
        }

        public void Verify() { if (page != null) page.TryVerify(this); }
    }
}
