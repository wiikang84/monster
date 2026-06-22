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
    const int COLS = 10, ROWS = 10;
    float cell = 2f;
    float hover;
    Material colormapMat;
    Transform envRoot, spawnedRoot;

    readonly List<Vector2Int> corners = new List<Vector2Int>
    {
        new Vector2Int(0,2), new Vector2Int(8,2), new Vector2Int(8,5),
        new Vector2Int(2,5), new Vector2Int(2,8), new Vector2Int(9,8),
    };
    HashSet<Vector2Int> pathSet;
    public readonly List<Vector3> Waypoints = new List<Vector3>();
    readonly List<Vector2Int> slotCells = new List<Vector2Int>
    {
        new Vector2Int(2,3), new Vector2Int(5,3), new Vector2Int(7,3), new Vector2Int(7,4),
        new Vector2Int(3,6), new Vector2Int(5,6), new Vector2Int(3,7), new Vector2Int(5,9),
    };
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

        cell = MeasureCell();
        hover = cell * 0.55f;

        BuildPathData();
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

    void BuildPathData()
    {
        pathSet = new HashSet<Vector2Int>();
        for (int i = 0; i < corners.Count - 1; i++)
        {
            var a = corners[i]; var b = corners[i + 1];
            var step = new Vector2Int(Sgn(b.x - a.x), Sgn(b.y - a.y));
            var cur = a;
            if (i == 0) pathSet.Add(cur);
            while (cur != b) { cur += step; pathSet.Add(cur); }
        }
        foreach (var cn in corners)
            Waypoints.Add(new Vector3(cn.x * cell, hover, cn.y * cell));
    }
    static int Sgn(int v) => v == 0 ? 0 : (v > 0 ? 1 : -1);

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

        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                bool isPath = pathSet.Contains(new Vector2Int(c, r));
                Make(isPath ? "tile-dirt" : "tile", Center(c, r), envRoot);
            }

        Make("spawn-round", Center(corners[0].x, corners[0].y) + Vector3.up * 0.02f, envRoot);
        var last = corners[corners.Count - 1];
        var baseGo = Make("wood-structure-high", Center(last.x, last.y) + Vector3.up * 0.02f, envRoot);
        baseGo.transform.localScale *= 1.1f;

        foreach (var sc in slotCells)
        {
            if (pathSet.Contains(sc)) continue;
            var go = Make("selection-a", Center(sc.x, sc.y) + Vector3.up * 0.03f, envRoot);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.3f, 0);
            bc.size = new Vector3(cell * 0.9f, 0.6f, cell * 0.9f);
            slots.Add(go.AddComponent<TowerSlot>());
        }
    }

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
        var t = baseGo.AddComponent<Tower>();
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
        var box = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 8, 8) };
        var big = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        var btn = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };

        GUI.Box(new Rect(10, 10, 360, 44), $"  골드 {gold}      라이프 {lives}      웨이브 {waveIndex + 1}/{waves.Count}", box);
        GUI.Label(new Rect(10, 60, 600, 24), $"  파란 선택타일을 클릭해 타워 설치 (비용 {TOWER_COST}골드)");

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
    }
}
