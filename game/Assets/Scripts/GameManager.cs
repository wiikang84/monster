using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 타워 디펜스 그레이박스 프로토타입 (코드-퍼스트)
// 씬에 아무 것도 없어도 Play를 누르면 자동으로 게임이 구성된다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // ── Play 누르면 자동 부트스트랩 (씬 세팅 불필요) ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<GameManager>() == null)
            new GameObject("GameManager").AddComponent<GameManager>();
    }

    // ── 게임 상태 ──
    enum State { Ready, InWave, Won, Lost }
    State state = State.Ready;

    int gold = 120;
    int lives = 10;
    int waveIndex = 0;
    bool allSpawned = false;

    const int TOWER_COST = 50;
    const int KILL_GOLD = 12;

    // ── 경로 (적이 따라가는 웨이포인트) ──
    public readonly List<Vector3> Waypoints = new List<Vector3>
    {
        new Vector3(0, 0.6f, 3),
        new Vector3(16, 0.6f, 3),
        new Vector3(16, 0.6f, 10),
        new Vector3(4, 0.6f, 10),
        new Vector3(4, 0.6f, 17),
        new Vector3(20, 0.6f, 17), // 마지막 = 기지(도달 시 라이프 감소)
    };

    // ── 런타임 컨테이너/목록 ──
    Transform spawnedRoot;
    public Transform SpawnedRoot => spawnedRoot;
    public readonly List<Enemy> Enemies = new List<Enemy>();
    readonly List<TowerSlot> slots = new List<TowerSlot>();

    // ── 웨이브 정의 ──
    struct WaveEntry { public int basic; public int fast; public WaveEntry(int b, int f) { basic = b; fast = f; } }
    readonly List<WaveEntry> waves = new List<WaveEntry>
    {
        new WaveEntry(5, 0),
        new WaveEntry(8, 0),
        new WaveEntry(6, 4),
    };

    void Awake()
    {
        Instance = this;
        spawnedRoot = new GameObject("Spawned").transform;
        BuildEnvironment();
    }

    // ====================== 레벨 구성 ======================
    void BuildEnvironment()
    {
        // 카메라
        if (Camera.main == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        var cam = Camera.main;
        cam.transform.position = new Vector3(10, 19, -6);
        cam.transform.LookAt(new Vector3(10, 0, 9));
        cam.fieldOfView = 60;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.45f, 0.6f, 0.75f);

        // 조명
        if (FindFirstObjectByType<Light>() == null)
        {
            var lightGo = new GameObject("Sun");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // 바닥
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(10, 0, 9);
        ground.transform.localScale = new Vector3(2.4f, 1, 2.4f);
        SetColor(ground, new Color(0.36f, 0.6f, 0.33f));

        // 경로 타일 (세그먼트마다 길쭉한 큐브)
        for (int i = 0; i < Waypoints.Count - 1; i++)
        {
            Vector3 a = Waypoints[i]; a.y = 0.06f;
            Vector3 b = Waypoints[i + 1]; b.y = 0.06f;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(tile.GetComponent<Collider>()); // 경로는 클릭 대상 아님
            tile.name = "Path";
            tile.transform.position = (a + b) * 0.5f;
            tile.transform.rotation = Quaternion.LookRotation(dir);
            tile.transform.localScale = new Vector3(2.0f, 0.12f, len + 2.0f);
            SetColor(tile, new Color(0.62f, 0.5f, 0.34f));
        }

        // 기지(마지막 지점) 표시
        var last = Waypoints[Waypoints.Count - 1];
        var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(baseGo.GetComponent<Collider>());
        baseGo.name = "Base";
        baseGo.transform.position = new Vector3(last.x, 0.8f, last.z);
        baseGo.transform.localScale = new Vector3(1.8f, 1.6f, 1.8f);
        SetColor(baseGo, new Color(0.85f, 0.2f, 0.2f));

        // 타워 설치 슬롯
        Vector2[] slotPos =
        {
            new Vector2(6, 6), new Vector2(11, 6), new Vector2(13, 7),
            new Vector2(8, 13), new Vector2(12, 13), new Vector2(1, 13), new Vector2(15, 20),
        };
        foreach (var p in slotPos)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "Slot";
            s.transform.position = new Vector3(p.x, 0.1f, p.y);
            s.transform.localScale = new Vector3(1.7f, 0.2f, 1.7f);
            SetColor(s, new Color(0.55f, 0.55f, 0.6f));
            slots.Add(s.AddComponent<TowerSlot>());
        }
    }

    // ====================== 입력 (타워 설치) ======================
    void Update()
    {
        if (state == State.InWave && allSpawned && Enemies.Count == 0)
        {
            if (waveIndex >= waves.Count - 1) state = State.Won;
            else { waveIndex++; state = State.Ready; allSpawned = false; }
        }

        if (state == State.Won || state == State.Lost) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                var slot = hit.collider.GetComponent<TowerSlot>();
                if (slot != null && !slot.Occupied && gold >= TOWER_COST)
                {
                    gold -= TOWER_COST;
                    BuildTower(slot);
                }
            }
        }
    }

    void BuildTower(TowerSlot slot)
    {
        slot.Occupied = true;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Tower";
        go.transform.SetParent(spawnedRoot);
        go.transform.position = slot.transform.position + Vector3.up * 0.7f;
        go.transform.localScale = new Vector3(1.0f, 1.3f, 1.0f);
        SetColor(go, new Color(0.2f, 0.45f, 0.85f));

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(go.transform);
        tip.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
        tip.transform.localPosition = new Vector3(0, 0.7f, 0);
        SetColor(tip, new Color(0.1f, 0.25f, 0.55f));

        go.AddComponent<Tower>();
    }

    // ====================== 웨이브/스폰 ======================
    void StartWave()
    {
        if (state != State.Ready) return;
        state = State.InWave;
        allSpawned = false;
        StartCoroutine(SpawnRoutine(waves[waveIndex]));
    }

    IEnumerator SpawnRoutine(WaveEntry w)
    {
        for (int i = 0; i < w.basic; i++) { SpawnEnemy(false); yield return new WaitForSeconds(0.8f); }
        for (int i = 0; i < w.fast; i++) { SpawnEnemy(true); yield return new WaitForSeconds(0.8f); }
        allSpawned = true;
    }

    void SpawnEnemy(bool fast)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = fast ? "Enemy_Fast" : "Enemy";
        go.transform.SetParent(spawnedRoot);
        go.transform.position = Waypoints[0];
        go.transform.localScale = fast ? new Vector3(0.7f, 0.7f, 0.7f) : new Vector3(0.9f, 0.9f, 0.9f);
        SetColor(go, fast ? new Color(0.95f, 0.85f, 0.2f) : new Color(0.85f, 0.25f, 0.2f));
        var e = go.AddComponent<Enemy>();
        e.Init(this, fast ? 35 : 60, fast ? 4.5f : 2.5f);
        Enemies.Add(e);
    }

    // ====================== 콜백 (Enemy가 호출) ======================
    public void OnEnemyKilled(Enemy e)
    {
        Enemies.Remove(e);
        gold += KILL_GOLD;
    }

    public void OnEnemyReachedBase(Enemy e)
    {
        Enemies.Remove(e);
        lives--;
        if (lives <= 0) { lives = 0; state = State.Lost; }
    }

    // ====================== 리스타트 ======================
    void Restart()
    {
        StopAllCoroutines();
        for (int i = spawnedRoot.childCount - 1; i >= 0; i--)
            Destroy(spawnedRoot.GetChild(i).gameObject);
        Enemies.Clear();
        foreach (var s in slots) s.Occupied = false;
        gold = 120; lives = 10; waveIndex = 0; allSpawned = false;
        state = State.Ready;
    }

    // ====================== HUD (OnGUI) ======================
    void OnGUI()
    {
        var box = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 8, 8) };
        var big = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        var btn = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };

        GUI.Box(new Rect(10, 10, 360, 44),
            $"  골드 {gold}      라이프 {lives}      웨이브 {waveIndex + 1}/{waves.Count}", box);
        GUI.Label(new Rect(10, 60, 600, 24), $"  슬롯(회색)을 클릭해 타워 설치 (비용 {TOWER_COST}골드)");

        if (state == State.Ready)
            if (GUI.Button(new Rect(10, 92, 200, 50), $"웨이브 {waveIndex + 1} 시작 ▶", btn))
                StartWave();

        if (state == State.Won || state == State.Lost)
        {
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height / 2 - 90, Screen.width, 60),
                state == State.Won ? "클리어!" : "게임 오버", big);
            if (GUI.Button(new Rect(Screen.width / 2 - 90, Screen.height / 2, 180, 56), "다시 시작", btn))
                Restart();
        }
    }

    // ── 렌더 파이프라인 무관 색상 적용 ──
    public static void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var m = r.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        m.color = c;
    }
}
