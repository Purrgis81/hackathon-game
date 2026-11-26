using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BeachResultsPresentation : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        public string displayName;
        public Animator animator;
        public string characterId;
        public Transform characterRoot;
        public Transform head;
        public Vector3 sunglassesPosition;
        public Quaternion sunglassesRotation;
        public Vector3 sunglassesScale;
        public AnimationClip[] rankCelebrations;
    }

    [SerializeField] Entry[] placements;
    [SerializeField] string[] previewRanking;
    [SerializeField] Transform winnerSunglasses;
    [SerializeField] Camera stageCamera;
    [SerializeField] Transform cameraTarget;

    struct RankSlot
    {
        public Pose pose;
        public Vector3 scale;
    }

    const string SceneName = "BeachResults";
    static string[] pendingRanking;
    static string[] pendingLabels, pendingScores;
    static Color[] pendingTints;
    static int pendingLocalPlace;
    MatchTextOverlay resultText;
    GameObject resultCue;
    bool localVictory;
    string[] labels, scores;
    readonly List<GameObject> copies = new();
    RankSlot[] rankSlots;
    Entry[] currentRanking;
    Vector3 cameraStart;
    float elapsed;
    Texture2D roundPanel;
    Font koreanFont;
    GUIStyle headline, rank, player, score, button;
    static readonly Color Ink = new(0.075f, .23f, .27f);
    static readonly Color[] Accents = { new(1f,.8f,.38f), new(.79f,.79f,1f), new(1f,.73f,.77f), new(.75f,.91f,.86f) };

    void OnEnable()
    {
        cameraStart = stageCamera.transform.position;
        if (currentRanking != null)
        {
            Replay();
            return;
        }

        var nextRanking = pendingRanking;
        int localPlace = nextRanking == null ? 1 : pendingLocalPlace;
        pendingRanking = null;
        pendingLocalPlace = 0;
        localVictory = localPlace == 1;
        if (localPlace > 0)
            resultCue = MatchTextOverlay.Load(localVictory ? "YouWin" : "YouLose");
        SetRanking(nextRanking ?? (previewRanking is { Length: > 0 }
            ? previewRanking
            : Array.ConvertAll(placements, entry => entry.characterId)));
        if(pendingTints!=null){
            var ink=new MaterialPropertyBlock();
            for(int i=0;i<currentRanking.Length;i++)
                foreach(var surface in currentRanking[i].characterRoot.GetComponentsInChildren<SkinnedMeshRenderer>()){
                    surface.GetPropertyBlock(ink);
                    ink.SetColor("_Color",pendingTints[i]);
                    surface.SetPropertyBlock(ink);
                }
            pendingTints=null;
        }
        labels = pendingLabels;
        scores = pendingScores;
        pendingLabels = pendingScores = null;
        PlayFanfare();
    }

    void PlayFanfare()
    {
        if (!stageCamera.TryGetComponent<AudioListener>(out _))
            stageCamera.gameObject.AddComponent<AudioListener>();
        var fanfare = gameObject.AddComponent<AudioSource>();
        fanfare.playOnAwake = false;
        fanfare.clip = Resources.Load<AudioClip>("Sound/PodiumFanfare");
        fanfare.volume = .65f;
        fanfare.Play();
    }

    // The caller supplies character IDs in finishing order, starting with first place.
    public static AsyncOperation LoadResults(params string[] characterIds) => LoadMatchResults(characterIds, null, null);

    public static AsyncOperation LoadMatchResults(string[] characterIds, string[] playerLabels, string[] scoreLabels, int localPlace = 0, Color[] tints = null)
    {
        if (characterIds == null || characterIds.Length == 0 || characterIds.Length > Accents.Length)
            throw new ArgumentException("Provide one character ID per finishing place.", nameof(characterIds));

        foreach (string id in characterIds)
        {
            if (!string.Equals(id, "Rabbit", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(id, "Cat", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(id, "Bear", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(id, "Frog", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Unknown results character: {id}", nameof(characterIds));
        }

        if (!Application.CanStreamedLevelBeLoaded(SceneName))
            throw new InvalidOperationException("Add BeachResults to the active Unity Build Profile.");

        if ((playerLabels == null) != (scoreLabels == null) || playerLabels != null &&
            (playerLabels.Length != characterIds.Length || scoreLabels.Length != characterIds.Length))
            throw new ArgumentException("Provide a name and score for each participant.");
        if (localPlace < 0 || localPlace > characterIds.Length)
            throw new ArgumentOutOfRangeException(nameof(localPlace));
        if(tints!=null && tints.Length!=characterIds.Length)throw new ArgumentException("Provide one tint per participant.",nameof(tints));

        pendingTints=(Color[])tints?.Clone();
        pendingLocalPlace = localPlace;
        pendingLabels = (string[])playerLabels?.Clone();
        pendingScores = (string[])scoreLabels?.Clone();
        pendingRanking = (string[])characterIds.Clone();
        try
        {
            return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
        }
        catch
        {
            pendingRanking = pendingLabels = pendingScores = null;
            pendingTints=null;
            pendingLocalPlace = 0;
            throw;
        }
    }

    public void SetRanking(params string[] characterIds)
    {
        if (characterIds == null || characterIds.Length == 0 || characterIds.Length > placements.Length)
            throw new ArgumentException("Provide one character ID per finishing place.", nameof(characterIds));

        var ordered = new Entry[characterIds.Length];
        for (int i = 0; i < ordered.Length; i++)
        {
            string id = characterIds[i];
            ordered[i] = Array.Find(placements, entry => string.Equals(entry.characterId, id, StringComparison.OrdinalIgnoreCase));
            if (ordered[i] == null)
                throw new ArgumentException($"Unknown results character: {id}", nameof(characterIds));
        }

        if (rankSlots == null)
        {
            rankSlots = Array.ConvertAll(placements, entry => new RankSlot {
                pose = new Pose(entry.characterRoot.position, entry.characterRoot.rotation),
                scale = entry.characterRoot.localScale
            });
        }

        winnerSunglasses.SetParent(transform, false);
        foreach (var copy in copies)
        {
            copy.SetActive(false);
            Destroy(copy);
        }
        copies.Clear();
        for (int i = 1; i < ordered.Length; i++)
        {
            if (Array.IndexOf(ordered, ordered[i]) != i)
                ordered[i] = Duplicate(ordered[i]);
        }

        currentRanking = ordered;
        labels = scores = null;
        foreach (var entry in placements)
            entry.characterRoot.gameObject.SetActive(Array.IndexOf(ordered, entry) >= 0);
        for (int i = 0; i < ordered.Length; i++)
        {
            var root = ordered[i].characterRoot;
            var slot = rankSlots[i];
            root.SetPositionAndRotation(slot.pose.position, slot.pose.rotation);
            root.localScale = slot.scale;
        }

        var winner = ordered[0];
        winnerSunglasses.SetParent(winner.head, false);
        winnerSunglasses.SetLocalPositionAndRotation(winner.sunglassesPosition, winner.sunglassesRotation);
        winnerSunglasses.localScale = winner.sunglassesScale;
        Replay();
    }

    Entry Duplicate(Entry original)
    {
        var copy = Instantiate(original.characterRoot.gameObject, original.characterRoot.parent);
        copies.Add(copy);
        var animator = copy.GetComponentInChildren<Animator>(true);
        var head = Array.Find(animator.GetComponentsInChildren<Transform>(true), joint => joint.name == "Head");
        copy.SetActive(true);
        return new Entry {
            displayName = original.displayName,
            characterId = original.characterId,
            characterRoot = copy.transform,
            animator = animator,
            head = head,
            sunglassesPosition = original.sunglassesPosition,
            sunglassesRotation = original.sunglassesRotation,
            sunglassesScale = original.sunglassesScale,
            rankCelebrations = original.rankCelebrations
        };
    }

    void OnDisable()
    {
        resultText?.Hide();
        if (roundPanel) Destroy(roundPanel);
        if (koreanFont) Destroy(koreanFont);
        headline = null;
    }

    void OnDestroy() => resultText?.Dispose();

    void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        EvaluatePerformers();
        stageCamera.transform.position = cameraStart + new Vector3(.16f * Mathf.Sin(elapsed * .34f), .034f * Mathf.Sin(elapsed * .51f), 0);
        stageCamera.transform.LookAt(cameraTarget);
        if (resultCue != null)
        {
            resultText ??= new MatchTextOverlay(stageCamera, transform);
            resultText.Show(resultCue, .79f, .9f, new Vector4(elapsed, localVictory ? 1 : 2, 0, 0));
        }
    }

    void EvaluatePerformers()
    {
        for (int i = 0; i < currentRanking.Length; i++)
        {
            var entry = currentRanking[i];
            var clip = entry.rankCelebrations[i];
            float duration = clip.length;
            clip.SampleAnimation(entry.animator.gameObject, duration > 0 ? Mathf.Repeat(elapsed, duration) : 0);
        }
    }

    void Replay()
    {
        elapsed = 0;
        EvaluatePerformers();
    }

    void OnGUI()
    {
        if (currentRanking == null) return;
        if (headline == null) InitializeStyles();
        Matrix4x4 previous = GUI.matrix;
        Color previousColor = GUI.color;
        float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 810f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        Rect safe = Screen.safeArea;
        float left = safe.xMin / scale;
        float right = safe.xMax / scale;
        float top = (Screen.height - safe.yMax) / scale;
        float bottom = (Screen.height - safe.yMin) / scale;
        float center = (left + right) * .5f;
        GUI.color = Color.white;
        DrawPanel(new Rect(center - 146, top + 28, 292, 62), new Color(.98f, .98f, .89f, .94f));
        GUI.Label(new Rect(center - 146, top + 28, 292, 62), scores == null ? "경기 결과" : "5라운드 종합 순위", headline);

        float rowWidth = Mathf.Min(1200, right - left - 40);
        float gap = 8, rowX = center - rowWidth * .5f, rowY = bottom - 75;
        float cardWidth = (rowWidth - gap * currentRanking.Length) / (currentRanking.Length + 1);
        for (int i = 0; i < currentRanking.Length; i++)
        {
            float x = rowX + (cardWidth + gap) * i;
            var box = new Rect(x, rowY, cardWidth, 48);
            DrawPanel(box, new Color(.98f, .98f, .91f, .94f));
            DrawPanel(new Rect(x + 8, rowY + 8, 36, 32), Accents[i]);
            GUI.Label(new Rect(x + 8, rowY + 8, 36, 32), (i + 1).ToString(), rank);
            GUI.Label(new Rect(x + 53, rowY, cardWidth - 60, scores == null ? 48 : 27),
                labels == null ? currentRanking[i].displayName : labels[i], player);
            if (scores != null)
                GUI.Label(new Rect(x + 53, rowY + 24, cardWidth - 60, 22), scores[i], score);
        }
        var replay = new Rect(rowX + (cardWidth + gap) * currentRanking.Length, rowY, cardWidth, 48);
        DrawPanel(replay, new Color(.09f, .30f, .31f, .98f));
        if (GUI.Button(replay, "다시 보기", button)) Replay();
        var home = new Rect(right - 166, top + 32, 138, 44);
        DrawPanel(home, new Color(.09f, .30f, .31f, .98f));
        if (GUI.Button(home, "처음으로", button)) SceneManager.LoadScene("StoneChamber");
        GUI.color = previousColor;
        GUI.matrix = previous;
    }

    void DrawPanel(Rect bounds, Color tint)
    {
        GUI.DrawTexture(bounds, roundPanel, ScaleMode.StretchToFill, true, 0, tint, 0, 11);
    }

    void InitializeStyles()
    {
        roundPanel = new Texture2D(1, 1);
        roundPanel.SetPixel(0, 0, Color.white);
        roundPanel.Apply();
        koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR" }, 36);
        headline = MakeStyle(32, TextAnchor.MiddleCenter, Ink);
        rank = MakeStyle(19, TextAnchor.MiddleCenter, Ink);
        player = MakeStyle(18, TextAnchor.MiddleLeft, Ink);
        player.clipping = TextClipping.Clip;
        score = MakeStyle(15, TextAnchor.MiddleLeft, Ink);
        button = MakeStyle(18, TextAnchor.MiddleCenter, Color.white);
        button.hover.textColor = new Color(1, .87f, .52f);
    }

    GUIStyle MakeStyle(int size, TextAnchor align, Color ink)
    {
        return new GUIStyle(GUIStyle.none)
        {
            font = koreanFont,
            fontSize = size,
            alignment = align,
            fontStyle = FontStyle.Bold,
            normal = { textColor = ink }
        };
    }
}
