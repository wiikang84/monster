using System.Collections.Generic;
using UnityEngine;

namespace Tower.Audio
{
    /// <summary>
    /// 간단한 효과음 재생기. Resources/Audio/*.ogg 를 이름(확장자 제외) 키로 로드해 풀로 재생.
    /// 키는 towers.json의 shootSound + 코드 상수와 일치.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx I;

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        int idx;
        readonly Dictionary<string, float> lastPlay = new Dictionary<string, float>();

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Sfx");
            I = go.AddComponent<Sfx>();
        }

        void Awake()
        {
            I = this;
            foreach (var c in Resources.LoadAll<AudioClip>("Audio"))
                if (c != null) clips[c.name] = c;

            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                pool[i] = s;
            }
            Debug.Log($"[Sfx] 클립 {clips.Count}개 로드");
        }

        /// <summary>키로 재생. throttle: 같은 키를 이 간격 안엔 한 번만(연사/군집 과다재생 방지).</summary>
        public void Play(string key, float volume = 1f, float throttle = 0f)
        {
            if (string.IsNullOrEmpty(key) || !clips.TryGetValue(key, out var clip) || clip == null) return;
            if (throttle > 0f)
            {
                if (lastPlay.TryGetValue(key, out var t) && Time.unscaledTime - t < throttle) return;
                lastPlay[key] = Time.unscaledTime;
            }
            var src = pool[idx];
            idx = (idx + 1) % pool.Length;
            src.clip = clip; src.volume = volume; src.Play();
        }
    }
}
