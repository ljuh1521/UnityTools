using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.Editor
{
    /// <summary>
    /// 생성기가 프리팹을 다시 만들 때 <b>사람이 프리팹에서 고친 값을 지우지 않게</b> 지킨다.
    ///
    /// 생성기는 매번 프리팹을 새로 조립해 덮어쓴다. 그래서 인스펙터에서 위치·색을 손보면 다음 생성에 사라졌다.
    /// 값을 생성기 상수로 옮겨 적는 건 그때뿐이라, 여기서 <b>세 벌을 비교해</b> 자동으로 이어 붙인다.
    ///
    ///   기준선(지난번 생성기가 만든 값) ─ 지금 프리팹 값 ─ 이번에 생성기가 만든 값
    ///
    ///   지금 값 ≠ 기준선  → 사람이 고친 것이다 → <b>지금 값을 지킨다</b>
    ///   지금 값 = 기준선  → 아무도 안 건드렸다 → 이번 생성값을 쓴다
    ///
    /// 기준선은 <see cref="Root"/> 아래 <c>*.txt</c>로 남긴다(에셋이 아니라 .meta가 안 생긴다).
    /// 기준선이 없으면 무엇이 손질인지 알 수 없으므로 아무것도 지키지 않고 그 사실을 알린다.
    /// </summary>
    public static class PrefabKeeper
    {
        /// <summary>
        /// 기준선을 두는 폴더. 프로젝트에서 바꿀 수 있다 — 이미 쌓인 기준선이 있으면 그 경로를 그대로 넣어야
        /// 손질이 이어진다(경로가 바뀌면 기준선이 없는 것으로 보여 첫 생성에서 한 번 덮어쓴다).
        /// </summary>
        public static string Root = "ProjectSettings/PrefabBaseline";

        private static readonly string NewLine = System.Environment.NewLine + "  ";

        /// <summary>지킬 속성. 글자 내용처럼 실행 중에 바뀌는 값은 넣지 않는다.</summary>
        private static readonly Dictionary<string, (System.Func<GameObject, string> Get,
            System.Action<GameObject, string> Set)> Fields = new()
        {
            ["active"] = (go => go.activeSelf.ToString(), (go, v) => go.SetActive(v == "True")),

            // 자리·크기를 레이아웃이 정하는 오브젝트는 값을 읽지 않는다 —
            // 사람이 정한 값이 아니라 계산 결과라, 저장 전후로 달라져 손질로 오인된다.
            ["rect.pos"] = (go => Driven(go) ? null : Vec(RectOf(go)?.anchoredPosition),
                (go, v) => { if (RectOf(go) is { } r) r.anchoredPosition = ToVec2(v); }),
            ["rect.size"] = (go => Driven(go) ? null : Vec(RectOf(go)?.sizeDelta),
                (go, v) => { if (RectOf(go) is { } r) r.sizeDelta = ToVec2(v); }),
            ["rect.anchorMin"] = (go => Driven(go) ? null : Vec(RectOf(go)?.anchorMin),
                (go, v) => { if (RectOf(go) is { } r) r.anchorMin = ToVec2(v); }),
            ["rect.anchorMax"] = (go => Driven(go) ? null : Vec(RectOf(go)?.anchorMax),
                (go, v) => { if (RectOf(go) is { } r) r.anchorMax = ToVec2(v); }),
            ["rect.pivot"] = (go => Driven(go) ? null : Vec(RectOf(go)?.pivot),
                (go, v) => { if (RectOf(go) is { } r) r.pivot = ToVec2(v); }),
            ["rect.scale"] = (go => Vec3(RectOf(go)?.localScale),
                (go, v) => { if (RectOf(go) is { } r) r.localScale = ToVec3(v); }),

            ["image.sprite"] = (go => SpriteKey(ImageOf(go)?.sprite),
                (go, v) => { if (ImageOf(go) is { } i) i.sprite = ToSprite(v); }),
            ["image.color"] = (go => Col(ImageOf(go)?.color),
                (go, v) => { if (ImageOf(go) is { } i) i.color = ToColor(v); }),
            ["image.type"] = (go => ImageOf(go)?.type.ToString(),
                (go, v) => { if (ImageOf(go) is { } i && System.Enum.TryParse(v, out Image.Type t)) i.type = t; }),
            ["image.ppu"] = (go => Num(ImageOf(go)?.pixelsPerUnitMultiplier),
                (go, v) => { if (ImageOf(go) is { } i) i.pixelsPerUnitMultiplier = ToFloat(v); }),
            ["image.raycast"] = (go => ImageOf(go)?.raycastTarget.ToString(),
                (go, v) => { if (ImageOf(go) is { } i) i.raycastTarget = v == "True"; }),

            ["text.color"] = (go => Col(TextOf(go)?.color),
                (go, v) => { if (TextOf(go) is { } t) t.color = ToColor(v); }),
            // 자동 축소가 켜져 있으면 fontSize는 사람이 정한 값이 아니라 맞춰진 결과다.
            ["text.size"] = (go => TextOf(go) is { enableAutoSizing: false } t ? Num(t.fontSize) : null,
                (go, v) => { if (TextOf(go) is { } t) t.fontSize = ToFloat(v); }),
            ["text.sizeMax"] = (go => Num(TextOf(go)?.fontSizeMax),
                (go, v) => { if (TextOf(go) is { } t) t.fontSizeMax = ToFloat(v); }),
            ["text.autoSize"] = (go => TextOf(go)?.enableAutoSizing.ToString(),
                (go, v) => { if (TextOf(go) is { } t) t.enableAutoSizing = v == "True"; }),
            ["text.align"] = (go => TextOf(go)?.alignment.ToString(),
                (go, v) =>
                {
                    if (TextOf(go) is { } t && System.Enum.TryParse(v, out TMPro.TextAlignmentOptions a))
                        t.alignment = a;
                }),
        };

        /// <summary>이번에 생성기가 만든 값을 훑는다. 프리팹을 저장하기 전에 부른다.</summary>
        public static Dictionary<string, string> Capture(GameObject root)
        {
            var map = new Dictionary<string, string>();

            Walk(root.transform, root.transform, map);

            return map;
        }

        /// <summary>
        /// 사람이 고친 값을 새로 만든 뿌리에 되씌운다. 저장 직전에 부른다.
        /// 사람이 <b>추가한 오브젝트</b>도 같이 옮겨 온다 — 그냥 두면 조용히 사라진다.
        ///
        /// <b>false를 돌려주면 저장하지 말고 멈춘다.</b> 이미 있는 프리팹인데 기준선이 없으면
        /// 무엇이 손질인지 가릴 수가 없어, 저장하는 순간 사람이 다듬은 값이 조용히 사라진다.
        /// </summary>
        /// <returns>저장해도 되는가.</returns>
        public static bool TryRestore(GameObject built, string prefabPath, Dictionary<string, string> generated)
        {
            // 처음 만드는 것이면 지울 손질이 없다.
            if (!File.Exists(prefabPath)) return true;

            var baseline = ReadBaseline(prefabPath);

            if (baseline == null)
            {
                Debug.LogError($"[프리팹 지킴이] 기준선이 없어 손질을 구분할 수 없습니다: {Path.GetFileName(prefabPath)}\n" +
                               "  덮어쓰면 인스펙터에서 다듬은 값이 조용히 사라지므로 만들지 않고 멈췄습니다.\n" +
                               "  지금 프리팹을 버리고 새로 만들 생각이면 그 프리팹을 지우고 다시 돌리세요.");
                return false;
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var current = Capture(contents);
                var objects = Index(built.transform, built.transform);

                var kept = new List<string>();

                foreach (var pair in current)
                {
                    // 지금 값이 지난 생성값과 같으면 아무도 안 건드린 것이다.
                    if (!baseline.TryGetValue(pair.Key, out string before) || before == pair.Value) continue;

                    int split = pair.Key.LastIndexOf('|');
                    string path = pair.Key.Substring(0, split);
                    string field = pair.Key.Substring(split + 1);

                    if (!objects.TryGetValue(path, out var target)) continue;
                    if (!Fields.TryGetValue(field, out var accessor)) continue;

                    // 이번 생성값과도 같으면 굳이 덮어쓸 게 없다.
                    if (generated.TryGetValue(pair.Key, out string now) && now == pair.Value) continue;

                    accessor.Set(target, pair.Value);
                    kept.Add($"{pair.Key} = {pair.Value}");
                }

                int added = RestoreAddedObjects(contents.transform, built.transform, generated);

                if (kept.Count > 0 || added > 0)
                    Debug.Log($"[프리팹 지킴이] {Path.GetFileName(prefabPath)} — 손질한 값 {kept.Count}개, " +
                              $"직접 추가한 오브젝트 {added}개를 이어받았습니다.{NewLine}" +
                              string.Join(NewLine, kept));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return true;
        }

        /// <summary>
        /// 기준선이 <b>저장된 프리팹과 어긋나지 않는지</b> 스스로 확인한다. 저장 직후에 부른다.
        ///
        /// 조립 직후 값과 저장된 값이 다른 속성이 있으면(레이아웃이 계산하는 자리·크기, 자동 축소된 글자 크기 등)
        /// 다음 생성에서 그걸 전부 "사람 손질"로 오인해 붙잡는다 — 실제로 그렇게 21개를 잘못 붙잡았다.
        /// 추적 목록에서 빼야 한다는 신호이므로 조용히 넘기지 않는다.
        /// </summary>
        private static void SelfCheck(string prefabPath, Dictionary<string, string> saving)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var saved = Capture(contents);
                var drift = new List<string>();

                foreach (var pair in saved)
                {
                    if (!saving.TryGetValue(pair.Key, out string made) || made == pair.Value) continue;

                    drift.Add($"{pair.Key}: 저장 전 {made} → 저장 후 {pair.Value}");
                }

                if (drift.Count == 0) return;

                Debug.LogWarning($"[프리팹 지킴이] {Path.GetFileName(prefabPath)} — 저장하면서 값이 바뀌는 속성 " +
                                 $"{drift.Count}개를 찾았습니다. 계산으로 정해지는 값이라 추적 목록에서 빼야 " +
                                 $"다음 생성에서 손질로 오인하지 않습니다.{NewLine}" +
                                 string.Join(NewLine, drift));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>이번 생성값을 기준선으로 남긴다. 저장 직후에 부른다.</summary>
        /// <param name="generated">이번에 생성기가 만든 값(기준선으로 남는다).</param>
        /// <param name="saving">저장 직전 값. 저장하면서 바뀌는 속성을 찾는 데만 쓴다.</param>
        public static void WriteBaseline(string prefabPath, Dictionary<string, string> generated,
            Dictionary<string, string> saving = null)
        {
            SelfCheck(prefabPath, saving ?? generated);

            Directory.CreateDirectory(Root);

            var text = new StringBuilder();

            foreach (var pair in generated) text.AppendLine($"{pair.Key}\t{pair.Value}");

            File.WriteAllText(BaselinePath(prefabPath), text.ToString());
        }

        /// <summary>사람이 프리팹에 직접 넣은 오브젝트를 새 뿌리로 옮긴다.</summary>
        private static int RestoreAddedObjects(Transform current, Transform built, Dictionary<string, string> generated)
        {
            int added = 0;

            var builtIndex = Index(built, built);

            foreach (var child in current.GetComponentsInChildren<Transform>(true))
            {
                if (child == current) continue;

                string path = PathOf(current, child);

                // 생성기가 만든 것이면 이미 있다.
                if (builtIndex.ContainsKey(path)) continue;
                if (generated.ContainsKey($"{path}|active")) continue;

                // 부모까지 사라졌으면 통째로 옮길 수 없다 — 부모 차례에 함께 따라온다.
                string parentPath = PathOf(current, child.parent);

                if (!builtIndex.TryGetValue(parentPath, out var parent)) continue;

                var copy = Object.Instantiate(child.gameObject, parent.transform);
                copy.name = child.name;

                added++;
            }

            return added;
        }

        private static Dictionary<string, GameObject> Index(Transform root, Transform node)
        {
            var map = new Dictionary<string, GameObject>();

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                string path = PathOf(root, child);

                map.TryAdd(path, child.gameObject);
            }

            return map;
        }

        private static void Walk(Transform root, Transform node, Dictionary<string, string> map)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                string path = PathOf(root, child);

                foreach (var field in Fields)
                {
                    string value = field.Value.Get(child.gameObject);

                    if (value != null) map[$"{path}|{field.Key}"] = value;
                }
            }
        }

        /// <summary>뿌리부터의 이름 경로. 이름이 겹치면 순서를 붙여 구분한다.</summary>
        private static string PathOf(Transform root, Transform node)
        {
            if (node == root) return ".";

            var names = new List<string>();

            for (var t = node; t != null && t != root; t = t.parent)
            {
                int same = 0;

                if (t.parent != null)
                {
                    for (int i = 0; i < t.GetSiblingIndex(); i++)
                        if (t.parent.GetChild(i).name == t.name) same++;
                }

                names.Add(same > 0 ? $"{t.name}#{same}" : t.name);
            }

            names.Reverse();

            return string.Join("/", names);
        }

        private static Dictionary<string, string> ReadBaseline(string prefabPath)
        {
            string path = BaselinePath(prefabPath);

            if (!File.Exists(path)) return null;

            var map = new Dictionary<string, string>();

            foreach (string line in File.ReadAllLines(path))
            {
                int tab = line.IndexOf('\t');

                if (tab > 0) map[line.Substring(0, tab)] = line.Substring(tab + 1);
            }

            return map;
        }

        private static string BaselinePath(string prefabPath)
        {
            return $"{Root}/{Path.GetFileNameWithoutExtension(prefabPath)}.txt";
        }

        /// <summary>레이아웃이 자리·크기를 정하는가(부모가 레이아웃 그룹이거나 자기 크기를 내용에 맞추는가).</summary>
        private static bool Driven(GameObject go)
        {
            if (go.GetComponent<ContentSizeFitter>() != null) return true;

            var parent = go.transform.parent;

            return parent != null && parent.GetComponent<LayoutGroup>() != null;
        }

        private static RectTransform RectOf(GameObject go) => go.transform as RectTransform;
        private static Image ImageOf(GameObject go) => go.GetComponent<Image>();
        private static TMPro.TMP_Text TextOf(GameObject go) => go.GetComponent<TMPro.TMP_Text>();

        private static string Vec(Vector2? v) =>
            v == null ? null : $"{Num(v.Value.x)},{Num(v.Value.y)}";

        private static string Vec3(Vector3? v) =>
            v == null ? null : $"{Num(v.Value.x)},{Num(v.Value.y)},{Num(v.Value.z)}";

        private static string Col(Color? c) =>
            c == null ? null : ColorUtility.ToHtmlStringRGBA(c.Value);

        private static string Num(float? f) =>
            f?.ToString("0.####", CultureInfo.InvariantCulture);

        private static float ToFloat(string s) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0;

        private static Vector2 ToVec2(string s)
        {
            var p = s.Split(',');

            return new Vector2(ToFloat(p[0]), ToFloat(p[1]));
        }

        private static Vector3 ToVec3(string s)
        {
            var p = s.Split(',');

            return new Vector3(ToFloat(p[0]), ToFloat(p[1]), ToFloat(p[2]));
        }

        private static Color ToColor(string s) =>
            ColorUtility.TryParseHtmlString("#" + s, out var c) ? c : Color.white;

        private static string SpriteKey(Sprite sprite)
        {
            if (sprite == null) return "";

            return $"{AssetDatabase.GetAssetPath(sprite)}:{sprite.name}";
        }

        private static Sprite ToSprite(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            int split = key.LastIndexOf(':');

            if (split < 0) return null;

            string path = key.Substring(0, split);
            string name = key.Substring(split + 1);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite && sprite.name == name) return sprite;
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
