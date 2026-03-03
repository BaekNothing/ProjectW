using UnityEngine;

namespace ProjectW.IngameMvp
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class RoutineObjectSpriteAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private float fps = 4f;
        [SerializeField] private bool loop = true;
        [SerializeField] private Sprite[] frames;

        private SpriteRenderer _spriteRenderer;
        private int _frameIndex;
        private float _timer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (_spriteRenderer == null || frames == null || frames.Length <= 1)
            {
                return;
            }

            var frameSeconds = 1f / Mathf.Max(0.1f, fps);
            _timer += Time.deltaTime;
            if (_timer < frameSeconds)
            {
                return;
            }

            _timer -= frameSeconds;
            _frameIndex += 1;

            if (_frameIndex >= frames.Length)
            {
                _frameIndex = loop ? 0 : frames.Length - 1;
            }

            var frame = frames[_frameIndex];
            if (frame != null)
            {
                _spriteRenderer.sprite = frame;
            }
        }

        public void Configure(Sprite[] newFrames, float newFps)
        {
            frames = newFrames;
            fps = newFps;
            _frameIndex = 0;
            _timer = 0f;
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_spriteRenderer != null && frames != null && frames.Length > 0 && frames[0] != null)
            {
                _spriteRenderer.sprite = frames[0];
            }
        }
    }
}
