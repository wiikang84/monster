using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 타워 디펜스 (Kenney Tower Defense Kit 실모델 사용, 코드-퍼스트)
// Play를 누르면 자동으로 그리드 맵/경로/적/타워가 구성된다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindFirstObjectByType<GameManager>() == null)
            new GameObject("GameManager").AddComponent<GameManager>();
    }

    // ── 상태 ──
    enum State { Ready, InWave, Won, Lost }
    State state = State.Ready;
    int gold = 120, lives = 10, waveIndex = 0;
    bool allSpawned = false;
    const int TOWER_COST = 50, KILL_GOLD = 12;

    // ── 그리드/경로 ──
    // (M2) COLS/ROWS 는 스테이지 그리드에서 결정됨. 맵/경로/슬롯도 데이터(StageDef.grid)로 생성.
    int COLS = 10, ROWS = 10;
    float cell = 2f;
    float hover;
    Material colormapMat;
    Material slotMat;   // 타워 설치 가능 타일 강조용 (한눈에 보이도록 별도 색)
    Font uiFont;        // 한글 UI 폰트(Pretendard). WebGL 기본 폰트엔 한글 글리프가 없어 필수.
    Transform envRoot, spawnedRoot;

    // (M2) 데이터 기반 맵/경로 서비스 + 현재 스테이지
    Tower.Data.StageDef stage;
    Tower.Map.MapService map;
    Tower.Map.PathService pathSvc;

    // (M2) 아래 하드코딩 경로/슬롯은 그리드 파싱으로 대체 — 보존 위해 주석 처리
    // readonly List<Vector2Int> corners = new List<Vector2Int>
    // {
    //     new Vector2Int(0,2), new Vector2Int(8,2), new Vector2Int(8,5),
    //     new Vector2Int(2,5), new Vector2Int(2,8), new Vector2Int(9,8),
    // };
    // HashSet<Vector2Int> pathSet;
    public readonly List<Vector3> Waypoints = new List<Vector3>();
    // readonly List<Vector2Int> slotCells = new List<Vector2Int>
    // {
    //     new Vector2Int(2,3), new Vector2Int(5,3), new Vector2Int(7,3), new Vector2Int(7,4),
    //     new Vector2Int(3,6), new Vector2Int(5,6), new Vector2Int(3,7), new Vector2Int(5,9),
    // };
    readonly List<TowerSlot> slots = new List<TowerSlot>();

    public readonly List<Enemy> Enemies = new List<Enemy>();
    struct WaveEntry { public int basic, fast; public WaveEntry(int b, int f){ basic=b; fast=f; } }
    readonly List<WaveEntry> waves = new List<WaveEntry>
    { new WaveEntry(5,0), new WaveEntry(8,0), new WaveEntry(6,4) };

    Vector3 Center(int c, int r) => new Vector3(c * cell, 0, r * cell);

    void Awake()
    {
        Instance = this;
        envRoot = new GameObject("Env").transform;
        spawnedRoot = new GameObject("Spawned").transform;

        var tex = Resources.Load<Texture2D>("colormap");
        colormapMat = new Material(Shader.Find("Standard"));
        if (tex) colormapMat.mainTexture = tex;
        colormapMat.SetFloat("_Glossiness", 0f);

        // 설치 타일은 파란색으로 또렷하게 (어디에 지을 수 있는지 보이도록)
        slotMat = new Material(Shader.Find("Standard"));
        slotMat.color = new Color(0.25f, 0.55f, 1f);
        slotMat.SetFloat("_Glossiness", 0f);
        slotMat.EnableKeyword("_EMISSION");
        slotMat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.7f));

        // 한글 폰트 로드 (Assets/Resources/UIFont.ttf = Pretendard, 동적폰트라 WebGL에서도 렌더됨)
        uiFont = Resources.Load<Font>("UIFont");

        cell = MeasureCell();
        hover = cell * 0.55f;

        // (M2) 스테이지 데이터 로드 → 그리드에서 맵/경로 생성
        var db = Tower.Core.ServiceLocator.Get<Tower.Data.ContentDB>();
        stage = db?.Stage("stage_01");
        map = new Tower.Map.MapService();
        pathSvc = new Tower.Map.PathService();

        if (stage != null && stage.grid != null && stage.grid.Length > 0)
        {
            map.Parse(stage);
            COLS = map.Cols; ROWS = map.Rows;
            pathSvc.Build(map, (c, r) => new Vector3(c * cell, hover, r * cell));
            Waypoints.Clear();
            if (pathSvc.Paths.Count > 0) Waypoints.AddRange(pathSvc.Paths[0]);
        }
        else
        {
            Debug.LogError("[GameManager] stage_01 데이터를 불러오지 못했습니다.");
        }

        BuildEnvironment();
    }

    float MeasureCell()
    {
        var p = Resources.Load<GameObject>("Models/tile");
        if (p == null) return 2f;
        var tmp = Instantiate(p);
        float c = 2f;
        var rs = tmp.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            c = Mathf.Max(b.size.x, b.size.z);
        }
        DestroyImmediate(tmp);
        return c <= 0.01f ? 2f : c;
    }

    // (M2) corners 하드코딩 경로 생성은 PathService(BFS)로 대체 — 보존 위해 주석 처리
    // void BuildPathData()
    // {
    //     pathSet = new HashSet<Vector2Int>();
    //     for (int i = 0; i < corners.Count - 1; i++)
    //     {
    //         var a = corners[i]; var b = corners[i + 1];
    //         var step = new Vector2Int(Sgn(b.x - a.x), Sgn(b.y - a.y));
    //         var cur = a;
    //         if (i == 0) pathSet.Add(cur);
    //         while (cur != b) { cur += step; pathSet.Add(cur); }
    //     }
    //     foreach (var cn in corners)
    //         Waypoints.Add(new Vector3(cn.x * cell, hover, cn.y * cell));
    // }
    // static int Sgn(int v) => v == 0 ? 0 : (v > 0 ? 1 : -1);

    GameObject Make(string model, Vector3 pos, Transform parent)
    {
        var prefab = Resources.Load<GameObject>("Models/" + model);
        GameObject go = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = model;
        go.transform.position = pos;
        go.transform.SetParent(parent);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = colormapMat;
        return go;
    }
    public GameObject SpawnModel(string model, Vector3 pos) => Make(model, pos, spawnedRoot);

    float HeightOf(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return cell * 0.5f;
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.size.y;
    }

    void BuildEnvironment()
    {
        if (Camera.main == null)
        {
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
        }
        var cam = Camera.main;
        float board = (COLS - 1) * cell;
        Vector3 center = new Vector3((COLS - 1) * 0.5f * cell, 0, (ROWS - 1) * 0.5f * cell);
        cam.transform.position = center + new Vector3(0, board * 1.15f, -board * 0.62f);
        cam.transform.LookAt(center + Vector3.up * cell * 0.3f);
        cam.fieldOfView = 55;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.46f, 0.62f, 0.78f);

        if (FindFirstObjectByType<Light>() == null)
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.15f;
            l.transform.rotation = Quaternion.Euler(50, -40, 0);
        }

        // (M2) 그리드 데이터로 타일 생성 (길=tile-dirt, 그 외=tile)
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                bool isPath = map.IsWalkable(c, r);
                Make(isPath ? "tile-dirt" : "tile", Center(c, r), envRoot);
            }

        // 스폰점 / 기지 (그리드의 S / G)
        foreach (var sp in map.SpawnCells)
            Make("spawn-round", Center(sp.x, sp.y) + Vector3.up * 0.02f, envRoot);
        foreach (var bp in map.BaseCells)
        {
            var baseGo = Make("wood-structure-high", Center(bp.x, bp.y) + Vector3.up * 0.02f, envRoot);
            baseGo.transform.localScale *= 1.1f;
        }

        // 설치 슬롯 (그리드의 o)
        foreach (var sc in map.SlotCells)
        {
            var go = Make("selection-a", Center(sc.x, sc.y) + Vector3.up * 0.03f, envRoot);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = slotMat; // 파란 강조색
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.3f, 0);
            bc.size = new Vector3(cell * 0.9f, 0.6f, cell * 0.9f);
            slots.Add(go.AddComponent<TowerSlot>());
        }
    }

    static bool _dbgGui;   // OnGUI 예외 1회 로깅용(안전장치)
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
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                var slot = hit.collider.GetComponentInParent<TowerSlot>();
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
        Vector3 p = new Vector3(slot.transform.position.x, 0, slot.transform.position.z);
        var baseGo = Make("tower-round-base", p, spawnedRoot);
        float h = HeightOf(baseGo);
        var weapon = Make("weapon-turret", p + Vector3.up * h, baseGo.transform);
        var t = baseGo.AddComponent<TowerUnit>();
        t.weapon = weapon.transform;
        t.range = cell * 3.5f;
        t.projectileSpeed = cell * 8f;
    }

    void StartWave()
    {
        if (state != State.Ready) return;
        state = State.InWave; allSpawned = false;
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
        var go = Make(fast ? "enemy-ufo-b" : "enemy-ufo-a", Waypoints[0], spawnedRoot);
        var e = go.AddComponent<Enemy>();
        e.Init(this, fast ? 35 : 60, (fast ? 2.4f : 1.4f) * cell);
        Enemies.Add(e);
    }

    public void OnEnemyKilled(Enemy e) { Enemies.Remove(e); gold += KILL_GOLD; }
    public void OnEnemyReachedBase(Enemy e)
    {
        Enemies.Remove(e); lives--;
        if (lives <= 0) { lives = 0; state = State.Lost; }
    }

    void Restart()
    {
        StopAllCoroutines();
        for (int i = spawnedRoot.childCount - 1; i >= 0; i--)
            Destroy(spawnedRoot.GetChild(i).gameObject);
        Enemies.Clear();
        foreach (var s in slots) s.Occupied = false;
        gold = 120; lives = 10; waveIndex = 0; allSpawned = false; state = State.Ready;
    }

    void OnGUI()
    {
      try {
        // GUI.skin 전역을 건드리면(특히 GUI.skin.font 대입) WebGL 빌드에서 NRE가 나는 경우가 있어,
        // 폰트는 각 스타일에 직접 지정하고 전역 스킨은 읽기만 한다(M5에서 uGUI로 교체 예정).
        var skin = GUI.skin;

        var box = skin != null ? new GUIStyle(skin.box) : new GUIStyle();
        box.fontSize = 18; box.alignment = TextAnchor.MiddleLeft; box.padding = new RectOffset(12, 12, 8, 8);
        var label = skin != null ? new GUIStyle(skin.label) : new GUIStyle();
        label.fontSize = 15;
        var big = skin != null ? new GUIStyle(skin.label) : new GUIStyle();
        big.fontSize = 36; big.fontStyle = FontStyle.Bold; big.alignment = TextAnchor.MiddleCenter; big.normal.textColor = Color.white;
        var btn = skin != null ? new GUIStyle(skin.button) : new GUIStyle();
        btn.fontSize = 20; btn.fontStyle = FontStyle.Bold;

        if (uiFont != null) { box.font = uiFont; label.font = uiFont; big.font = uiFont; btn.font = uiFont; }

        GUI.Box(new Rect(10, 10, 360, 44), $"  골드 {gold}      라이프 {lives}      웨이브 {waveIndex + 1}/{waves.Count}", box);
        GUI.Label(new Rect(12, 60, 600, 24), $"파란 선택타일을 클릭해 타워 설치 (비용 {TOWER_COST}골드)", label);

        if (state == State.Ready)
            if (GUI.Button(new Rect(10, 92, 200, 50), $"웨이브 {waveIndex + 1} 시작 ▶", btn)) StartWave();

        if (state == State.Won || state == State.Lost)
        {
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, Screen.height / 2 - 90, Screen.width, 60), state == State.Won ? "클리어!" : "게임 오버", big);
            if (GUI.Button(new Rect(Screen.width / 2 - 90, Screen.height / 2, 180, 56), "다시 시작", btn)) Restart();
        }
      } catch (System.Exception ex) { if (!_dbgGui) { _dbgGui = true; Debug.LogError("[OnGUI-ERR] " + ex.Message); } }
    }
}
