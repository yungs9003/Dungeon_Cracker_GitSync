using System.Collections;
using UnityEngine;
using Dungeon.Map;
using Dungeon.UI;

namespace Dungeon.Checkpoint
{
    public class CheckpointMapLoader2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private JsonBlockBuilder2D builder;
        [SerializeField] private ScreenFader fader;

        [Header("Map")]
        [SerializeField] private string mapPath = "Maps/CheckpointScene/checkpoint";

        [Header("Fade")]
        [SerializeField] private float fadeInSeconds = 0.5f;

        private void Awake()
        {
            if (builder == null)
                builder = FindFirstObjectByType<JsonBlockBuilder2D>();

            if (fader == null)
                fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);
        }

        private void Start()
        {
            if (builder == null)
            {
                Debug.LogError("[CheckpointMapLoader2D] JsonBlockBuilder2D가 없습니다.");
                return;
            }

            builder.BuildFromJsonPath(mapPath);

            if (fader != null)
                StartCoroutine(fader.FadeIn(0.01f));
        }
    }
}