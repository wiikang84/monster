using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Tower.Data;

// 타워 디펜스 — 데이터 기반(StageDef/TowerDef/EnemyDef). 고정 슬롯.
// (M3+) 타워 선택→사거리원+업그레이드/판매 패널, 빌드 메뉴, 광역/슬로우/치명타, 사운드, 손맛(데미지숫자·골드팝업·피격플래시·비네트).
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
    int gold, lives, waveIndex = 0;
    bool allSpawned = false;
    int runningGroups = 0;
    const float SELL_RATE = 0.7f;

    // ── 그리드/경로 ──
    int COLS = 10, ROWS = 10;
    float cell = 2f;
    float hover;
    Material colormapMat, slotMat;
    Font uiFont;
    Transform envRoot, spawnedRoot;

    StageDef stage;
    ContentDB db;
    readonly List<TowerDef> allowedTowers = new List<TowerDef>();
    Tower.Map.MapService map;
    Tower.Map.PathService pathSvc;

    public readonly List<Vector3> Waypoints = new List<Vector3>();
    readonly List<TowerSlot> slots = new List<TowerSlot>();
    public readonly List<Enemy> Enemies = new List<Enemy>();

    // ── 선택/빌드 UI ──
    TowerUnit selected;
    TowerSlot pendingSlot;        // 빌드 메뉴가 열린 슬롯
    TowerSlot hoveredSlot;        // 마우스가 올라간 빈 슬롯(PC 호버)
    Tower.Gameplay.RangeRing selRing;     // 선택 타워(노랑)
    Tower.Gameplay.RangeRing previewRing; // 설치 미리보기(시안)
    Rect panelRect, menuRect, startBtnRect, paletteRect;
    TowerDef buildTower;   // 건설 모드: 하단 팔레트에서 고른 타워(null=건설모드 아님)
    bool helpDismissed = false;   // 첫 진입 도움말 닫힘 여부
    float vignetteUntil = 0f;
    bool firstBuildDone = false;
    string hintText = ""; float hintUntil = 0f;

    // ── 떠다니는 텍스트(데미지/골드/레벨업) ──
    struct FloatText { public Vector3 world; public string text; public Color color; public float born, life, size; }
    readonly List<FloatText> floats = new List<FloatText>();

    Vector3 Center(int c, int r) => new Vector3(c * cell, 0, r * cell);

    void Awake()
    {
        Instance = this;
        envRoot = new GameObject("Env").transform;
        spawnedRoot = new GameObject("Spawned").transform;
        Tower.Audio.Sfx.Ensure();

        var tex = Resources.Load<Texture2D>("colormap");
        colormapMat = new Material(Shader.Find("Standard"));
        if (tex) colormapMat.mainTexture = tex;
        colormapMat.SetFloat("_Glossiness", 0f);

        slotMat = new Material(Shader.Find("Standard"));
        slotMat.color = new Color(0.25f, 0.55f, 1f);
        slotMat.SetFloat("_Glossiness", 0f);
        slotMat.EnableKeyword("_EMISSION");
        slotMat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.7f));

        uiFont = Resources.Load<Font>("UIFont");

        cell = MeasureCell();
        hover = cell * 0.55f;

        db = Tower.Core.ServiceLocator.Get<ContentDB>();
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

            gold = stage.startGold; lives = stage.startLives;
            allowedTowers.Clear();
            foreach (var id in stage.allowedTowers) { var d = db.Tower(id); if (d != null) allowedTowers.Add(d); }
        }
        else { Debug.LogError("[GameManager] stage_01 로드 실패"); gold = 120; lives = 10; }

        selRing = Tower.Gameplay.RangeRing.Create(new Color(1f, 0.82f, 0.25f, 1f), 0.06f * cell);
        previewRing = Tower.Gameplay.RangeRing.Create(new Color(0.21f, 0.77f, 1f, 1f), 0.06f * cell);

        SetupGraphics();
        BuildEnvironment();
    }

    void SetupGraphics()
    {
        QualitySettings.antiAliasing = 4;               // MSAA — 계단현상 완화
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowDistance = Mathf.Max(60f, COLS * cell * 2.2f);
        QualitySettings.pixelLightCount = 3;

        // 부드러운 환경광(삼색)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.66f, 0.74f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.56f, 0.60f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.32f, 0.28f);

        // 안개로 깊이감
        float board = COLS * cell;
        Color bg = new Color(0.55f, 0.70f, 0.86f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = bg;
        RenderSettings.fogStartDistance = board * 1.0f;
        RenderSettings.fogEndDistance = board * 2.8f;

        // 스카이박스는 칙칙해서 사용 안 함 → 깔끔한 단색 하늘 + 안개로 깊이감.
        RenderSettings.skybox = null;
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
        cam.fieldOfView = 52;
        cam.allowMSAA = true;
        cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.70f, 0.86f);

        if (FindFirstObjectByType<Light>() == null)
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 1.05f;
            l.color = new Color(1f, 0.96f, 0.88f);            // 따뜻한 햇빛
            l.transform.rotation = Quaternion.Euler(48, -38, 0);
            l.shadows = LightShadows.Soft;                     // 부드러운 그림자
            l.shadowStrength = 0.55f;
        }

        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
                Make(map.IsWalkable(c, r) ? "tile-dirt" : "tile", Center(c, r), envRoot);

        foreach (var sp in map.SpawnCells)
            Make("spawn-round", Center(sp.x, sp.y) + Vector3.up * 0.02f, envRoot);
        foreach (var bp in map.BaseCells)
        {
            var baseGo = Make("wood-structure-high", Center(bp.x, bp.y) + Vector3.up * 0.02f, envRoot);
            baseGo.transform.localScale *= 1.1f;
        }
        foreach (var sc in map.SlotCells)
        {
            var go = Make("selection-a", Center(sc.x, sc.y) + Vector3.up * 0.03f, envRoot);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = slotMat;
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.3f, 0);
            bc.size = new Vector3(cell * 0.9f, 0.6f, cell * 0.9f);
            slots.Add(go.AddComponent<TowerSlot>());
        }

        ScatterDecorations();
    }

    // 빈 잔디 칸에 나무/바위/크리스탈을 결정론적으로 산점 → 디오라마 느낌 (콜라이더 없음, 클릭 방해 안 함)
    void ScatterDecorations()
    {
        string[] deco = { "detail-tree", "detail-tree-large", "detail-rocks", "detail-crystal" };
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                if (map.At(c, r) != Tower.Map.CellType.Empty) continue;
                uint h = (uint)((c * 73856093) ^ (r * 19349663) ^ 0x5f3759df);
                if (h % 100u >= 26u) continue;                 // ~26% 칸에만(과밀 방지)
                string m = deco[(h / 100u) % 4u];
                float ox = ((h % 7u) / 7f - 0.5f) * cell * 0.4f;
                float oz = ((h / 7u % 7u) / 7f - 0.5f) * cell * 0.4f;
                var d = Make(m, Center(c, r) + new Vector3(ox, 0.02f, oz), envRoot);
                d.transform.rotation = Quaternion.Euler(0, h % 360u, 0);
                float s = 0.7f + (h % 30u) / 90f;
                d.transform.localScale *= s;
            }
    }

    // ───────────────────────── 입력 ─────────────────────────
    void Update()
    {
        if (state == State.InWave && allSpawned && Enemies.Count == 0)
        {
            if (waveIndex >= stage.waves.Count - 1) { state = State.Won; Tower.Audio.Sfx.I?.Play("jingles_NES13"); }
            else { waveIndex++; state = State.Ready; allSpawned = false; }
        }
        if (state == State.Won || state == State.Lost) { previewRing.Hide(); return; }

        // 첫 도움말 표시 중엔 월드 입력 차단([시작하기] 버튼만 동작)
        if (!helpDismissed && waveIndex == 0 && state == State.Ready) return;

        UpdateHover();

        if (Input.GetMouseButtonDown(0))
        {
            // IMGUI 패널/메뉴/버튼 위 클릭은 월드 처리에서 제외
            Vector2 g = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if ((selected != null && panelRect.Contains(g)) ||
                (pendingSlot != null && menuRect.Contains(g)) ||
                (state == State.Ready && startBtnRect.Contains(g)) ||
                paletteRect.Contains(g))             // 하단 팔레트 클릭은 월드 처리 제외
                return;

            HandleWorldClick();
        }

        // 건설 모드: 설치 슬롯 전체를 펄스 발광 → "어디 지을 수 있는지" 한눈에
        if (slotMat != null)
        {
            if (buildTower != null)
            {
                float k = 0.25f + 0.6f * Mathf.PingPong(Time.time * 2.2f, 1f);
                slotMat.SetColor("_EmissionColor", new Color(0.12f, 0.55f, 1f) * k);
            }
            else slotMat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.7f));
        }
    }

    // PC 호버: 빈 슬롯 위에 오면 시안 사거리 미리보기. (빌드 메뉴 열렸을 땐 메뉴가 미리보기 제어)
    void UpdateHover()
    {
        if (pendingSlot != null) return;
        Vector2 g = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        bool overUI = (selected != null && panelRect.Contains(g)) || (state == State.Ready && startBtnRect.Contains(g)) || paletteRect.Contains(g);
        if (!overUI && buildTower != null && Camera.main != null)   // 건설 모드에서만 슬롯 호버 미리보기
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                var slot = hit.collider.GetComponentInParent<TowerSlot>();
                if (slot != null && !slot.Occupied)
                {
                    hoveredSlot = slot;
                    previewRing.ShowAt(slot.transform.position, buildTower.range);
                    return;
                }
            }
        }
        hoveredSlot = null;
        previewRing.Hide();
    }

    void HandleWorldClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            // 타워 자체에 콜라이더가 없으므로, 클릭은 보통 그 아래 슬롯 콜라이더에 맞는다.
            var tower = hit.collider.GetComponentInParent<TowerUnit>();
            var slot = hit.collider.GetComponentInParent<TowerSlot>();
            if (tower == null && slot != null && slot.Occupied) tower = slot.Tower;

            if (tower != null) { Select(tower); return; }

            if (slot != null && !slot.Occupied)
            {
                selected = null; selRing.Hide();
                // (신) 건설 모드면 고른 타워 설치(설치 후 모드 유지 = 연속 설치). 아니면 먼저 팔레트에서 고르라 안내.
                if (buildTower != null) TryBuild(buildTower, slot);
                else { hintText = "아래에서 포탑을 먼저 고르세요."; hintUntil = Time.time + 2f; Tower.Audio.Sfx.I?.Play("error_002", 0.4f); }
                return;
            }
        }
        Deselect();              // 빈 곳 클릭 → 선택 해제
        buildTower = null;       // + 건설 모드 해제
        previewRing.Hide();
    }

    void Select(TowerUnit t)
    {
        selected = t; pendingSlot = null; buildTower = null; previewRing.Hide();
        selRing.ShowAt(t.transform.position, t.Range);
        Tower.Audio.Sfx.I?.Play("click1", 0.6f);
    }

    void Deselect() { selected = null; pendingSlot = null; selRing.Hide(); }

    bool TryBuild(TowerDef def, TowerSlot slot)
    {
        if (gold < def.cost) { Tower.Audio.Sfx.I?.Play("error_002", 0.5f); return false; }
        gold -= def.cost;
        BuildTower(def, slot);
        pendingSlot = null;
        previewRing.Hide();
        Tower.Audio.Sfx.I?.Play("confirmation_001", 0.7f);
        if (!firstBuildDone) { firstBuildDone = true; hintText = "포탑을 클릭하면 업그레이드 · 판매!"; hintUntil = Time.time + 5f; }
        return true;
    }

    void BuildTower(TowerDef def, TowerSlot slot)
    {
        slot.Occupied = true;
        Vector3 p = new Vector3(slot.transform.position.x, 0, slot.transform.position.z);
        var baseGo = Make(def.baseModel, p, spawnedRoot);

        if (!string.IsNullOrEmpty(def.tint) && ColorUtility.TryParseHtmlString(def.tint, out var tc))
        {
            var m = new Material(colormapMat); m.color = tc;
            foreach (var r in baseGo.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
        }

        float h = HeightOf(baseGo);
        var weapon = Make(def.weaponModel, p + Vector3.up * h, baseGo.transform);
        var t = baseGo.AddComponent<TowerUnit>();
        t.SetDef(def);
        t.weapon = weapon.transform;
        t.Slot = slot;
        slot.Tower = t;
    }

    void DoUpgrade()
    {
        if (selected == null || !selected.CanUpgrade) return;
        var u = selected.NextUpgrade;
        if (gold < u.cost) { Tower.Audio.Sfx.I?.Play("error_002", 0.5f); return; }
        gold -= u.cost;
        selected.Upgrade();
        selRing.AnimateTo(selected.Range);
        Tower.Audio.Sfx.I?.Play("powerUp1", 0.8f);
        Popup(selected.transform.position + Vector3.up * (cell * 0.8f), $"LV.{selected.Tier}!", new Color(1f, 0.85f, 0.2f), 0.8f, 26);
        StartCoroutine(Punch(selected.transform, 1f + (selected.Tier - 1) * 0.12f));
    }

    void DoSell()
    {
        if (selected == null) return;
        gold += selected.RefundGold;
        Popup(selected.transform.position + Vector3.up * (cell * 0.6f), $"+{selected.RefundGold}", new Color(1f, 0.9f, 0.3f), 0.6f, 20);
        if (selected.Slot != null) { selected.Slot.Occupied = false; selected.Slot.Tower = null; }
        Tower.Audio.Sfx.I?.Play("metalClick", 0.7f);
        Destroy(selected.gameObject);
        Deselect();
    }

    IEnumerator Punch(Transform tr, float target)
    {
        Vector3 from = tr.localScale;
        Vector3 to = Vector3.one * target;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float k = t / 0.2f;
            float e = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f); // EaseOutBack
            tr.localScale = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }
        tr.localScale = to;
    }

    // ───────────────────────── 웨이브 ─────────────────────────
    void StartWave()
    {
        if (state != State.Ready) return;
        state = State.InWave; allSpawned = false;
        Tower.Audio.Sfx.I?.Play("computerNoise_000", 0.6f);
        var w = stage.waves[waveIndex];
        runningGroups = w.groups.Count;
        if (runningGroups == 0) { allSpawned = true; return; }
        foreach (var grp in w.groups) StartCoroutine(SpawnGroup(grp));
    }

    IEnumerator SpawnGroup(SpawnGroup grp)
    {
        if (grp.startDelay > 0f) yield return new WaitForSeconds(grp.startDelay);
        var def = db.Enemy(grp.enemyId);
        for (int i = 0; i < grp.count; i++)
        {
            if (def != null) SpawnEnemy(def);
            if (i < grp.count - 1) yield return new WaitForSeconds(grp.interval);
        }
        runningGroups--;
        if (runningGroups <= 0) allSpawned = true;
    }

    void SpawnEnemy(EnemyDef def)
    {
        if (Waypoints.Count == 0) return;
        var go = Make(def.model, Waypoints[0], spawnedRoot);
        var e = go.AddComponent<Enemy>();
        e.Init(this, def, cell);
        Enemies.Add(e);
    }

    public void OnEnemyKilled(Enemy e)
    {
        Enemies.Remove(e);
        gold += e.Reward;
        Tower.Audio.Sfx.I?.Play("explosionCrunch_002", 0.55f, 0.03f);
        Tower.Audio.Sfx.I?.Play("handleCoins", 0.4f, 0.05f);
        Popup(e.transform.position + Vector3.up * (cell * 0.5f), $"+{e.Reward}", new Color(1f, 0.88f, 0.3f), 0.5f, 18);
    }

    public void OnEnemyReachedBase(Enemy e)
    {
        Enemies.Remove(e); lives -= e.LivesCost;
        vignetteUntil = Time.time + 0.3f;
        Tower.Audio.Sfx.I?.Play("error_002", 0.6f);
        if (lives <= 0) { lives = 0; state = State.Lost; Tower.Audio.Sfx.I?.Play("jingles_NES03"); }
    }

    // 데미지 숫자(빨강/치명타 금색) — Enemy가 호출
    public void ShowDamage(Vector3 world, int dmg, bool crit)
    {
        Popup(world + Vector3.up * (cell * 0.4f), dmg.ToString(),
              crit ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.95f, 0.95f), 0.5f, crit ? 24 : 16);
    }

    void Popup(Vector3 world, string text, Color color, float life, float size)
    {
        floats.Add(new FloatText { world = world, text = text, color = color, born = Time.time, life = life, size = size });
    }

    void Restart()
    {
        StopAllCoroutines();
        for (int i = spawnedRoot.childCount - 1; i >= 0; i--)
            Destroy(spawnedRoot.GetChild(i).gameObject);
        Enemies.Clear(); floats.Clear();
        foreach (var s in slots) { s.Occupied = false; s.Tower = null; }
        Deselect();
        gold = stage != null ? stage.startGold : 120;
        lives = stage != null ? stage.startLives : 10;
        waveIndex = 0; allSpawned = false; runningGroups = 0; state = State.Ready;
    }

    // ───────────────────────── UI (IMGUI, M5에서 uGUI로 승격 예정) ─────────────────────────
    static bool _dbgGui;
    void OnGUI()
    {
        try
        {
            var skin = GUI.skin;
            GUIStyle box = skin != null ? new GUIStyle(skin.box) : new GUIStyle();
            box.fontSize = 18; box.alignment = TextAnchor.MiddleLeft; box.padding = new RectOffset(12, 12, 8, 8);
            GUIStyle label = skin != null ? new GUIStyle(skin.label) : new GUIStyle();
            label.fontSize = 14;
            GUIStyle big = skin != null ? new GUIStyle(skin.label) : new GUIStyle();
            big.fontSize = 36; big.fontStyle = FontStyle.Bold; big.alignment = TextAnchor.MiddleCenter; big.normal.textColor = Color.white;
            GUIStyle btn = skin != null ? new GUIStyle(skin.button) : new GUIStyle();
            btn.fontSize = 18; btn.fontStyle = FontStyle.Bold;
            if (uiFont != null) { box.font = uiFont; label.font = uiFont; big.font = uiFont; btn.font = uiFont; }

            int total = stage != null ? stage.waves.Count : 0;
            GUI.Box(new Rect(10, 10, 380, 44), $"  골드 {gold}      라이프 {lives}      웨이브 {Mathf.Min(waveIndex + 1, total)}/{total}", box);

            startBtnRect = new Rect(10, 64, 220, 50);
            if (state == State.Ready)
                if (GUI.Button(startBtnRect, $"웨이브 {waveIndex + 1} 시작 ▶", btn)) { Tower.Audio.Sfx.I?.Play("click1", 0.6f); StartWave(); }

            DrawGuidance(label);
            DrawUpgradeIndicators();
            DrawBuildableSlots();                 // (신) 건설 모드: 빈 슬롯마다 (+) — 어디 짓는지 표시
            DrawFloatingTexts();
            DrawHoverTooltip(label);
            // DrawBuildMenu(label, btn);          // (구) 슬롯 위 팝업 메뉴 → 하단 팔레트로 대체. 코드 보존.
            DrawBuildPalette(label, btn);         // (신) 하단 건설 팔레트(타워 카드 선택)
            DrawTowerPanel(label, btn);
            DrawHint(label);
            DrawVignette();
            DrawWavePreview(label);               // (신) 다음 웨이브 미리보기
            DrawFirstHelp(big, label, btn);       // (신) 첫 진입 안내 오버레이
            DrawEndScreen(big, btn);
        }
        catch (System.Exception ex) { if (!_dbgGui) { _dbgGui = true; Debug.LogError("[OnGUI-ERR] ps=" + _ps + " : " + ex.Message); } }
    }

    // (신) 하단 건설 팔레트 — 타워 카드. 클릭 시 건설 모드 토글(다시 누르면 취소).
    void DrawBuildPalette(GUIStyle label, GUIStyle btn)
    {
        int n = allowedTowers.Count;
        if (n == 0 || state == State.Won || state == State.Lost) { paletteRect = new Rect(-1, -1, 0, 0); return; }

        float cardW = 168, cardH = 70, gap = 10, pad = 8;
        float totalW = n * cardW + (n - 1) * gap;
        float x0 = Mathf.Max(pad, (Screen.width - totalW) / 2f);
        float y = Screen.height - cardH - 14;
        paletteRect = new Rect(x0 - pad, y - pad, totalW + pad * 2, cardH + pad * 2);

        var _bg = GUI.color;
        GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.86f);
        GUI.DrawTexture(paletteRect, Texture2D.whiteTexture);
        GUI.color = _bg;

        var cardStyle = new GUIStyle(btn) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        if (uiFont != null) cardStyle.font = uiFont;

        for (int i = 0; i < n; i++)
        {
            var d = allowedTowers[i];
            var r = new Rect(x0 + i * (cardW + gap), y, cardW, cardH);
            bool afford = gold >= d.cost;
            bool active = buildTower == d;

            if (active)   // 선택 강조 테두리(시안)
            {
                var _c = GUI.color; GUI.color = new Color(0.25f, 0.85f, 1f, 0.95f);
                GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture);
                GUI.color = _c;
            }

            GUI.enabled = afford;
            if (GUI.Button(r, $"{d.displayName}  {d.cost}G\n{d.desc}", cardStyle))
            {
                buildTower = (buildTower == d) ? null : d;   // 토글
                selected = null; selRing.Hide(); previewRing.Hide();
                Tower.Audio.Sfx.I?.Play("click1", 0.6f);
            }
            GUI.enabled = true;
        }
    }

    // (신) 건설 모드에서 빈 슬롯마다 (+) 아이콘(카메라 각도 무관, 펄스). "어디 지을 수 있는지" 한눈에.
    void DrawBuildableSlots()
    {
        if (buildTower == null || Camera.main == null) return;
        float a = 0.55f + 0.45f * Mathf.PingPong(Time.time * 2.2f, 1f);
        var st = new GUIStyle { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (uiFont != null) st.font = uiFont;
        st.normal.textColor = new Color(0.22f, 0.80f, 1f, a);
        foreach (var s in slots)
        {
            if (s == null || s.Occupied) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(s.transform.position + Vector3.up * (cell * 0.35f));
            if (sp.z < 0) continue;
            GUI.Label(new Rect(sp.x - 24, Screen.height - sp.y - 24, 48, 48), "+", st);
        }
    }

    void DrawBuildMenu(GUIStyle label, GUIStyle btn)
    {
        if (pendingSlot == null) { menuRect = new Rect(-1, -1, 0, 0); return; }
        Vector3 sp = Camera.main.WorldToScreenPoint(pendingSlot.transform.position);
        float w = 230, rowH = 52, head = 26;
        float h = head + allowedTowers.Count * rowH + 6;
        float x = Mathf.Clamp(sp.x - w / 2, 4, Screen.width - w - 4);
        float y = Mathf.Clamp(Screen.height - sp.y - h - 10, 4, Screen.height - h - 4);
        menuRect = new Rect(x, y, w, h);
        var _bg = GUI.color;
        GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.94f);
        GUI.DrawTexture(menuRect, Texture2D.whiteTexture);
        GUI.color = _bg;
        GUI.Label(new Rect(x + 10, y + 5, w - 16, 18), "포탑 건설 — 선택", label);

        TowerDef previewTower = allowedTowers.Count > 0 ? allowedTowers[0] : null;
        Vector2 mouse = Event.current != null ? Event.current.mousePosition : new Vector2(-1, -1);

        for (int i = 0; i < allowedTowers.Count; i++)
        {
            var d = allowedTowers[i];
            var r = new Rect(x + 8, y + head + i * rowH, w - 16, rowH - 6);
            if (r.Contains(mouse)) previewTower = d;
            bool afford = gold >= d.cost;
            GUI.enabled = afford;
            string cardText = $"{d.displayName}   {d.cost}G\n{d.desc}";
            if (GUI.Button(r, cardText, btn)) { TryBuild(d, pendingSlot); GUI.enabled = true; return; }
            GUI.enabled = true;
        }

        // 메뉴에서 가리키는 타워의 사거리를 슬롯에 미리보기(시안)
        if (previewTower != null) previewRing.ShowAt(pendingSlot.transform.position, previewTower.range);
    }

    // 상태별 1줄 가이드(불친절 해소)
    void DrawGuidance(GUIStyle label)
    {
        if (state == State.Won || state == State.Lost) return;
        string guide;
        if (buildTower != null) guide = $"반짝이는 칸(+)을 눌러 {buildTower.displayName} 설치 · 빈 곳을 누르면 취소";
        else if (state == State.InWave) guide = "적을 막으세요!  포탑을 클릭하면 강화할 수 있어요.";
        else
        {
            bool hasTower = false, canUp = false;
            foreach (var s in slots)
                if (s.Occupied && s.Tower != null) { hasTower = true; if (s.Tower.CanUpgrade && gold >= s.Tower.NextUpgrade.cost) canUp = true; }
            int cheapest = int.MaxValue;
            foreach (var d in allowedTowers) if (d.cost < cheapest) cheapest = d.cost;
            if (!hasTower && gold >= cheapest) guide = "반짝이는 칸을 눌러 포탑을 지으세요.";
            else if (canUp) guide = "포탑을 클릭해 업그레이드하세요!";
            else guide = "준비되면 ‘웨이브 시작’을 누르세요.";
        }
        var st = new GUIStyle(label) { fontSize = 15 };
        st.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
        GUI.Label(new Rect(12, 120, 600, 22), guide, st);
    }

    // 업그레이드 가능+골드 충분 타워 위에 깜빡이는 ⬆
    void DrawUpgradeIndicators()
    {
        if (state == State.Won || state == State.Lost || Camera.main == null) return;
        float a = 0.5f + 0.5f * Mathf.PingPong(Time.time * 2f, 1f);
        var st = new GUIStyle { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (uiFont != null) st.font = uiFont;
        st.normal.textColor = new Color(1f, 0.85f, 0.2f, a);
        foreach (var s in slots)
        {
            if (s == null || !s.Occupied || s.Tower == null) continue;
            if (s.Tower == selected) continue;
            if (!s.Tower.CanUpgrade || gold < s.Tower.NextUpgrade.cost) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(s.Tower.transform.position + Vector3.up * (cell * 1.0f));
            if (sp.z < 0) continue;
            GUI.Label(new Rect(sp.x - 20, Screen.height - sp.y - 14, 40, 28), "⬆", st);
        }
    }

    // 호버한 빈 슬롯 옆 비용 툴팁
    void DrawHoverTooltip(GUIStyle label)
    {
        if (hoveredSlot == null || pendingSlot != null || allowedTowers.Count == 0) return;
        Vector2 m = Event.current != null ? Event.current.mousePosition : new Vector2(-1, -1);
        if (m.x < 0) return;
        string t = allowedTowers.Count == 1 ? $"{allowedTowers[0].displayName} · {allowedTowers[0].cost}G" : "여기에 포탑 설치";
        var st = new GUIStyle(label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        st.normal.textColor = new Color(0.85f, 0.95f, 1f);
        var r = new Rect(m.x + 16, m.y - 6, 150, 22);
        var _bg = GUI.color; GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.9f);
        GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = _bg;
        GUI.Label(r, t, st);
    }

    // 일시 안내 토스트
    void DrawHint(GUIStyle label)
    {
        if (Time.time >= hintUntil || string.IsNullOrEmpty(hintText)) return;
        var st = new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        st.normal.textColor = Color.white;
        float w = 360, h = 36;
        var r = new Rect((Screen.width - w) / 2, 150, w, h);
        var _bg = GUI.color; GUI.color = new Color(0.12f, 0.45f, 0.85f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = _bg;
        GUI.Label(r, hintText, st);
    }

    static int _ps;
    void DrawTowerPanel(GUIStyle label, GUIStyle btn)
    {
        if (selected == null) { panelRect = new Rect(-1, -1, 0, 0); return; }
        _ps = 1;
        float w = 360, h = 158;
        panelRect = new Rect(10, Screen.height - h - 10, w, h);
        _ps = 2;
        var _bg = GUI.color;
        GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.90f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = _bg;
        float x = panelRect.x + 12, y = panelRect.y + 8;

        _ps = 3; GUI.Label(new Rect(x, y, w - 20, 24), $"{(selected.Def != null ? selected.Def.displayName : "?")}   Lv.{selected.Tier}", label);
        _ps = 4; GUI.Label(new Rect(x, y + 24, w - 20, 22),
            $"데미지 {selected.Damage}   사거리 {selected.Range:0.0}   공속 {selected.FireRate:0.00}/s", label);

        _ps = 5; var up = selected.NextUpgrade;
        var upR = new Rect(x, y + 50, w - 24, 50);
        if (up != null)
        {
            GUI.enabled = gold >= up.cost;
            string preview = $"데미지 {selected.Damage}→{selected.Damage + up.damage}" +
                             (up.range > 0 ? $"  사거리 +{up.range:0.0}" : "") +
                             (up.fireRate > 0 ? $"  공속 +{up.fireRate:0.00}" : "");
            _ps = 6; if (GUI.Button(upR, $"⬆ 업그레이드  ({up.cost}G)\n{preview}", btn)) DoUpgrade();
            GUI.enabled = true;
        }
        else { _ps = 7; GUI.Button(upR, "MAX 레벨", btn); }

        _ps = 8; var tgR = new Rect(x, y + 104, (w - 32) * 0.55f, 40);
        if (GUI.Button(tgR, $"타게팅: {TargetingKo(selected.Targeting)}", btn))
            selected.Targeting = (TargetingMode)(((int)selected.Targeting + 1) % 4);

        _ps = 9; var sellR = new Rect(tgR.xMax + 8, y + 104, (w - 32) * 0.45f, 40);
        if (GUI.Button(sellR, $"판매 +{selected.RefundGold}", btn)) DoSell();
    }

    static string TargetingKo(TargetingMode m)
    {
        switch (m)
        {
            case TargetingMode.First: return "선두";
            case TargetingMode.MaxHp: return "최대체력";
            case TargetingMode.MinHp: return "최소체력";
            default: return "최근접";
        }
    }

    void DrawFloatingTexts()
    {
        var cam = Camera.main;
        var st = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        if (uiFont != null) st.font = uiFont;
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            var f = floats[i];
            float age = Time.time - f.born;
            if (age >= f.life) { floats.RemoveAt(i); continue; }
            Vector3 sp = cam.WorldToScreenPoint(f.world);
            if (sp.z < 0) continue;
            float a = 1f - age / f.life;
            st.fontSize = (int)f.size;
            st.normal.textColor = new Color(f.color.r, f.color.g, f.color.b, a);
            float gx = sp.x, gy = Screen.height - sp.y - age * 38f;
            GUI.Label(new Rect(gx - 60, gy - 14, 120, 28), f.text, st);
        }
    }

    void DrawVignette()
    {
        if (Time.time >= vignetteUntil) return;
        float a = (vignetteUntil - Time.time) / 0.3f * 0.5f;
        var c = GUI.color; GUI.color = new Color(1f, 0f, 0f, a);
        int t = 24;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - t, Screen.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 0, t, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width - t, 0, t, Screen.height), Texture2D.whiteTexture);
        GUI.color = c;
    }

    // (신) 다음 웨이브 미리보기 — 우측, Ready 상태에서 적 종류×수량.
    void DrawWavePreview(GUIStyle label)
    {
        if (stage == null || state != State.Ready || waveIndex >= stage.waves.Count) return;
        var wv = stage.waves[waveIndex];
        var seen = new List<string>(); var cnt = new List<int>();
        foreach (var g in wv.groups)
        {
            int idx = seen.IndexOf(g.enemyId);
            if (idx < 0) { seen.Add(g.enemyId); cnt.Add(g.count); }
            else cnt[idx] += g.count;
        }
        if (seen.Count == 0) return;
        float w = 196, h = 34 + seen.Count * 22;
        var r = new Rect(Screen.width - w - 12, 66, w, h);
        var c = GUI.color; GUI.color = new Color(0.08f, 0.10f, 0.14f, 0.86f);
        GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = c;
        var head = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold };
        head.normal.textColor = new Color(1f, 0.85f, 0.4f);
        GUI.Label(new Rect(r.x + 10, r.y + 6, w - 16, 20), $"다음 웨이브  {waveIndex + 1}/{stage.waves.Count}", head);
        var body = new GUIStyle(label) { fontSize = 13 };
        body.normal.textColor = Color.white;
        for (int i = 0; i < seen.Count; i++)
        {
            var ed = db?.Enemy(seen[i]);
            string nm = ed != null ? ed.displayName : seen[i];
            GUI.Label(new Rect(r.x + 12, r.y + 28 + i * 22, w - 18, 20), $"{nm}  x{cnt[i]}", body);
        }
    }

    // (신) 첫 진입 도움말 오버레이 — 조작·상성 1장 안내. [시작하기]로 닫음.
    void DrawFirstHelp(GUIStyle big, GUIStyle label, GUIStyle btn)
    {
        if (helpDismissed || waveIndex != 0 || state != State.Ready) return;
        var c = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        float w = 470, h = 320;
        var box = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = c;
        var title = new GUIStyle(big) { fontSize = 26 };
        GUI.Label(new Rect(box.x, box.y + 16, w, 38), "외계 침공 방어!", title);
        var body = new GUIStyle(label) { fontSize = 16, alignment = TextAnchor.UpperLeft, wordWrap = true };
        body.normal.textColor = new Color(1f, 1f, 1f, 0.95f);
        GUI.Label(new Rect(box.x + 30, box.y + 64, w - 60, 184),
            "적이 길을 따라 기지로 쳐들어옵니다.\n\n" +
            "1.  아래에서 포탑을 고르세요\n" +
            "2.  반짝이는 (+) 칸을 눌러 설치\n" +
            "3.  [웨이브 시작]으로 막으세요\n\n" +
            "· 포탑을 클릭하면 업그레이드 / 판매\n" +
            "· 광역=군집,  둔화=고속,  대공=비행 에 강해요", body);
        if (GUI.Button(new Rect(box.x + w / 2 - 85, box.y + h - 56, 170, 42), "시작하기", btn))
        { helpDismissed = true; Tower.Audio.Sfx.I?.Play("confirmation_001", 0.7f); }
    }

    void DrawEndScreen(GUIStyle big, GUIStyle btn)
    {
        if (state != State.Won && state != State.Lost) return;
        var c = GUI.color; GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = c;
        GUI.Label(new Rect(0, Screen.height / 2 - 90, Screen.width, 60), state == State.Won ? "클리어!" : "게임 오버", big);
        if (GUI.Button(new Rect(Screen.width / 2 - 90, Screen.height / 2, 180, 56), "다시 시작", btn)) Restart();
    }
}
