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
    Tower.Gameplay.RangeRing selRing;
    Rect panelRect, menuRect, startBtnRect;
    float vignetteUntil = 0f;

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
    }

    // ───────────────────────── 입력 ─────────────────────────
    void Update()
    {
        if (state == State.InWave && allSpawned && Enemies.Count == 0)
        {
            if (waveIndex >= stage.waves.Count - 1) { state = State.Won; Tower.Audio.Sfx.I?.Play("jingles_NES13"); }
            else { waveIndex++; state = State.Ready; allSpawned = false; }
        }
        if (state == State.Won || state == State.Lost) return;

        if (Input.GetMouseButtonDown(0))
        {
            // IMGUI 패널/메뉴/버튼 위 클릭은 월드 처리에서 제외
            Vector2 g = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if ((selected != null && panelRect.Contains(g)) ||
                (pendingSlot != null && menuRect.Contains(g)) ||
                (state == State.Ready && startBtnRect.Contains(g)))
                return;

            HandleWorldClick();
        }
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
                Deselect();
                if (allowedTowers.Count == 1) TryBuild(allowedTowers[0], slot);
                else { pendingSlot = slot; Tower.Audio.Sfx.I?.Play("click1", 0.6f); }
                return;
            }
        }
        Deselect();   // 빈 곳 클릭 → 해제
    }

    void Select(TowerUnit t)
    {
        selected = t; pendingSlot = null;
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
        Tower.Audio.Sfx.I?.Play("confirmation_001", 0.7f);
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

            DrawFloatingTexts();
            DrawBuildMenu(label, btn);
            DrawTowerPanel(label, btn);
            DrawVignette();
            DrawEndScreen(big, btn);
        }
        catch (System.Exception ex) { if (!_dbgGui) { _dbgGui = true; Debug.LogError("[OnGUI-ERR] ps=" + _ps + " : " + ex.Message); } }
    }

    void DrawBuildMenu(GUIStyle label, GUIStyle btn)
    {
        if (pendingSlot == null) { menuRect = new Rect(-1, -1, 0, 0); return; }
        Vector3 sp = Camera.main.WorldToScreenPoint(pendingSlot.transform.position);
        float w = 180, rowH = 40, h = 30 + allowedTowers.Count * rowH;
        float x = Mathf.Clamp(sp.x - w / 2, 4, Screen.width - w - 4);
        float y = Mathf.Clamp(Screen.height - sp.y - h - 10, 4, Screen.height - h - 4);
        menuRect = new Rect(x, y, w, h);
        var _bg = GUI.color;
        GUI.color = new Color(0.10f, 0.12f, 0.16f, 0.92f);
        GUI.DrawTexture(menuRect, Texture2D.whiteTexture);
        GUI.color = _bg;
        GUI.Label(new Rect(x + 8, y + 4, w - 16, 20), "타워 건설", label);
        for (int i = 0; i < allowedTowers.Count; i++)
        {
            var d = allowedTowers[i];
            var r = new Rect(x + 8, y + 26 + i * rowH, w - 16, rowH - 6);
            GUI.enabled = gold >= d.cost;
            if (GUI.Button(r, $"{d.displayName}  {d.cost}G", btn)) TryBuild(d, pendingSlot);
            GUI.enabled = true;
        }
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
