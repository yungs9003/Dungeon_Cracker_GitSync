using UnityEngine;

namespace Dungeon.Traps
{
    public class BallEffectFade2D : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.25f;
        [SerializeField] private bool fadeAlpha = true;

        private SpriteRenderer sr;
        private Color startColor;
        private float timer;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                startColor = sr.color;
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / lifetime);

            if (fadeAlpha && sr != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                sr.color = c;
            }

            if (timer >= lifetime)
                Destroy(gameObject);
        }
    }
}