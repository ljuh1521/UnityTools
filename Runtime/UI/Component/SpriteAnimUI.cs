using UnityEngine;

namespace UnityTools.UI
{
    /// <summary>
    /// 스프라이트 시트를 한 장씩 갈아 끼워 재생하는 부품. 스파인을 쓸 만큼은 아닌 짧은 연출용이다
    /// (요리 획득 씬의 슬롯 빛·붉은 프레임이 이걸 쓴다 — 기획서 18페이지 "0.1초 간격으로 재생").
    ///
    /// 프레임은 프리팹이 들고 있는다 — 그림이 Resources 밖(Assets/Sources)에 있어 코드가 이름으로
    /// 못 불러온다. 잘라 둔 조각을 인스펙터에서 순서대로 꽂으면 된다.
    /// </summary>
    public class SpriteAnimUI : ElementUI
    {
        public ImageUI image;

        [Tooltip("재생할 순서대로. 비어 있으면 아무것도 하지 않는다.")]
        public Sprite[] frames;

        [Tooltip("한 장을 보여주는 시간(초). 기획 기본값은 0.1초.")]
        public float interval = 0.1f;

        public bool loop;

        [Tooltip("한 번 재생하고 스스로 사라진다. 끄면 마지막 장이 그대로 남는다.")]
        public bool hideWhenDone = true;

        private float _elapsed;
        private int _index = -1;
        private bool _playing;

        /// <summary>첫 장부터 재생한다. 켜면서 부르면 된다.</summary>
        public void Play()
        {
            if (frames == null || frames.Length == 0) return;

            _elapsed = 0f;
            _index = -1;
            _playing = true;

            Step();
        }

        public void Stop()
        {
            _playing = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // 꺼졌다 켜지는 것만으로 처음부터 돌게 한다 — 슬롯마다 껐다 켜며 쓰기 때문이다.
            if (frames != null && frames.Length > 0) Play();
        }

        protected override void Update()
        {
            base.Update();

            if (!_playing) return;

            _elapsed += Time.deltaTime;

            if (_elapsed < interval) return;

            _elapsed -= interval;

            Step();
        }

        private void Step()
        {
            int next = _index + 1;

            if (next >= frames.Length)
            {
                if (!loop)
                {
                    _playing = false;

                    // 마지막 장이 화면에 그대로 남으면 연출이 끝난 뒤에도 자국이 보인다
                    // (요리 슬롯의 흰 빛이 계속 걸려 있었다, 2026-09-16).
                    if (hideWhenDone) SetActive(false);

                    return;
                }

                next = 0;
            }

            _index = next;

            if (image != null) image.Sprite = frames[_index];
        }
    }
}
