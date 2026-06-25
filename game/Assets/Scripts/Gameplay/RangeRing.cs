using UnityEngine;

namespace Tower.Gameplay
{
    /// <summary>타워 사거리 표시용 LineRenderer 원. 반경 확장 애니메이션 지원.</summary>
    public sealed class RangeRing : MonoBehaviour
    {
        const int N = 64;
        LineRenderer lr;
        float curR, targetR, animFrom, animT = 1f;

        public static RangeRing Create(Color color, float width)
        {
            var go = new GameObject("RangeRing");
            var rr = go.AddComponent<RangeRing>();
            rr.Setup(color, width);
            go.SetActive(false);
            return rr;
        }

        void Setup(Color color, float width)
        {
            lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = N;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            // Unlit 셰이더 — 조명·안개 영향 없이 항상 선명(Standard는 부감+안개에서 묻혔음). WebGL 호환.
            var sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            mat.color = color;
            lr.material = mat;
            lr.startColor = lr.endColor = color;   // 버텍스 컬러도 지정(가시성 보강)
            lr.numCapVertices = 4;
            Rebuild(0f);
        }

        void Rebuild(float r)
        {
            for (int i = 0; i < N; i++)
            {
                float a = (float)i / N * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r));  // 바닥 위로 띄워 안 묻히게
            }
        }

        public void ShowAt(Vector3 worldPos, float range)
        {
            transform.position = new Vector3(worldPos.x, 0f, worldPos.z);
            curR = targetR = range; animT = 1f;
            Rebuild(range);
            gameObject.SetActive(true);
        }

        /// <summary>사거리 변화를 0.25초 애니메이션으로(업그레이드 시).</summary>
        public void AnimateTo(float range)
        {
            animFrom = curR; targetR = range; animT = 0f;
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            if (animT < 1f)
            {
                animT = Mathf.Min(1f, animT + Time.deltaTime / 0.25f);
                float e = 1f - Mathf.Pow(1f - animT, 3f); // EaseOutCubic
                curR = Mathf.Lerp(animFrom, targetR, e);
                Rebuild(curR);
            }
        }
    }
}
