using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityTools.UI
{
    public class ToastUI : GenericUI
    {
        /// <summary>
        /// 토스트가 사라졌다. 목록에서 빼는 것처럼 <b>모아 두는 쪽이 할 일</b>을 여기에 붙인다 —
        /// 그 목록을 어디에 두는지는 게임마다 다르므로 패키지가 알 수 없다.
        /// </summary>
        public static Action<ToastUI> Ended;

        public enum Mode
        {
            Idle,
            Start,
            Push,
            Wait,
            End,
        }
        
        //public Toast toastName;
        [Header("Toast")]
        public RectTransform rootTransform;
        public Mode toastMode;
        public TextUI textUI;

        [NonSerialized] public float RemainTime;
        [NonSerialized] public float MaxTime;
        [NonSerialized] public Vector2 StartPos;
        [NonSerialized] public Vector2 EndPos;
        [NonSerialized] public bool OnList;

        protected override void Update()
        {
            base.Update();
            
            RemainTime -= Time.deltaTime;

            switch (toastMode)
            {
                case Mode.Start:
                {
                    var lerp = (MaxTime - RemainTime) * 10f;
                    rootTransform.localScale = Vector3.Lerp(new Vector3(1, 0, 1), Vector3.one, lerp);

                    if (lerp >= 1)
                    {
                        toastMode = Mode.Wait;
                        rootTransform.localScale = Vector3.one;
                    }

                    break;
                }
                case Mode.Push:
                {
                    var lerp = (MaxTime - RemainTime) * 10f;
                    var pos = Vector2.Lerp(StartPos, EndPos, lerp);

                    RectTransform.anchoredPosition = pos;

                    if (lerp >= 1)
                    {
                        toastMode = Mode.Wait;
                        RectTransform.anchoredPosition = EndPos;
                    }
                    
                    break;
                }
                case Mode.Wait:
                {
                    if (RemainTime <= 0) toastMode = Mode.End;
                    break;
                }
                case Mode.End:
                {
                    var lerp = -RemainTime * 10f;
                    rootTransform.localScale = Vector3.Lerp(Vector3.one, new Vector3(1, 0, 1), lerp);

                    if (lerp >= 1)
                    {
                        SetActive(false);
                        toastMode = Mode.Idle;
                        if (OnList) Ended?.Invoke(this);
                    }
                    break;
                }
            }
        }
        
        public void InitToast(Vector2 startPos, string dis, float time = 3, bool onList = true)
        {
            RectTransform.anchoredPosition = startPos;

            InitToast(dis, time, onList);
        }    
        
        public void InitToast(string dis, float time = 3, bool onList = true)
        {
            if (dis == null) return;
            
            textUI.Text = dis;
            MaxTime = time;
            RemainTime = time;
            toastMode = Mode.Start;
            StartPos = RectTransform.anchoredPosition;
            EndPos = StartPos;
            OnList = onList;
                
            rootTransform.localScale = new Vector3(1, 0, 1);
            SetActive(true);
        }    
        
        public void InitToast(float time = 1.5f, bool onList = true)
        {
            MaxTime = time;
            RemainTime = time;
            toastMode = Mode.Start;
            StartPos = RectTransform.anchoredPosition;
            EndPos = StartPos;
            OnList = onList;
                
            rootTransform.localScale = new Vector3(1, 0, 1);
            SetActive(true);
        }
    }
}
