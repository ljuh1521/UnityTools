using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace UnityTools.Editor
{
    /// <summary>
    /// 텍스처 임포트 프리셋을 폴더에 일괄로 씌운다.
    ///
    /// 프리셋은 임포터의 <b>모든</b> 값을 덮어쓴다. 그래서 그냥 돌리면 텍스처마다 따로 잡아 둔 설정
    /// (스프라이트 시트로 잘라 둔 조각, 9슬라이스 테두리, 피벗, PPU)이 되돌릴 수 없게 날아간다 —
    /// 그런 텍스처는 건너뛰고 무엇을 건너뛰었는지 알린다.
    ///
    /// 대상 폴더와 프리셋 경로는 프로젝트마다 다르므로 여기서 정하지 않고 인자로 받는다.
    /// </summary>
    public static class TexturePresetTools
    {
        /// <summary>폴더 아래 텍스처에 프리셋을 씌운다. 씌운 개수를 돌려준다.</summary>
        public static int ApplyToFolder(string presetPath, string folder, string label)
        {
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);

            if (preset == null)
            {
                Debug.LogError($"[텍스처 프리셋] 프리셋이 없습니다: {presetPath}");
                return 0;
            }

            if (!Directory.Exists(folder))
            {
                Debug.LogError($"[텍스처 프리셋] 대상 폴더가 없습니다: {folder}");
                return 0;
            }

            var paths = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".tga"))
                .ToArray();

            int applied = 0;
            var skipped = new List<string>();

            foreach (string path in paths)
            {
                string assetPath = path.Replace("\\", "/");

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (importer == null || !preset.CanBeAppliedTo(importer)) continue;

                if (WouldDropSprite(preset, importer, out string reason) ||
                    WouldLoseSetting(importer, out reason))
                {
                    skipped.Add($"{assetPath} ({reason})");
                    continue;
                }

                preset.ApplyTo(importer);
                importer.SaveAndReimport();

                applied++;
            }

            Debug.Log($"[텍스처 프리셋] {label} — 적용 {applied}개 / 건너뜀 {skipped.Count}개");

            if (skipped.Count > 0)
            {
                Debug.LogWarning($"[텍스처 프리셋] 건너뛴 텍스처 {skipped.Count}개입니다. " +
                                 "프리셋을 씌우면 괄호 안의 것이 사라지므로 손대지 않았습니다.\n  " +
                                 string.Join("\n  ", skipped));
            }

            return applied;
        }

        /// <summary>
        /// 지금 스프라이트인 그림이 프리셋을 씌우면 스프라이트가 아니게 되거나 스프라이트 모드가 바뀌는지 본다.
        /// 그러면 그 그림을 쓰는 곳(SpriteRenderer·Image)의 참조는 남은 채 스프라이트만 사라져 <b>오류 없이
        /// 빈 칸이 된다</b> — DefenceR 에서 스파인 폴더에 섞인 낱장 그림 52장이 일반 텍스처로 덮여 불릿 그림
        /// 50개가 두 달 반 동안 안 보였다(2026-08-10 ~ 10-26).
        ///
        /// 일부러 스프라이트를 일반 텍스처로 바꾸려던 것이어도 여기서는 건너뛴다 — 쓰는 곳이 남아 있는지
        /// 이 도구는 모르므로, 그런 변경은 손으로 한다. 반대(일반 텍스처 → 스프라이트)는 잃는 게 없어 그대로 씌운다.
        /// </summary>
        public static bool WouldDropSprite(Preset preset, TextureImporter importer, out string reason)
        {
            reason = null;

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode == SpriteImportMode.None) return false;

            // 프리셋에 안 적힌 값은 씌워도 안 바뀐다 — 지금 값으로 본다.
            var type = (TextureImporterType)PresetInt(preset, "m_TextureType", (int)importer.textureType);
            var mode = (SpriteImportMode)PresetInt(preset, "m_SpriteMode", (int)importer.spriteImportMode);

            if (type != TextureImporterType.Sprite)
            {
                reason = $"스프라이트가 사라짐 — 프리셋은 {type}";
                return true;
            }

            if (mode != importer.spriteImportMode)
            {
                reason = $"스프라이트 모드가 바뀜 — {importer.spriteImportMode} → {mode}";
                return true;
            }

            return false;
        }

        private static int PresetInt(Preset preset, string propertyPath, int fallback)
        {
            foreach (var modification in preset.PropertyModifications)
            {
                if (modification.propertyPath == propertyPath && int.TryParse(modification.value, out int value))
                    return value;
            }

            return fallback;
        }

        /// <summary>
        /// 프리셋을 씌우면 잃는 개별 설정이 있는지 본다.
        /// 스프라이트 시트는 잘라 둔 조각이 통째로 날아가고, 9슬라이스는 늘일 때 모서리가 뭉개진다.
        /// </summary>
        public static bool WouldLoseSetting(TextureImporter importer, out string reason)
        {
            reason = null;

            // 스프라이트가 아니면 잃을 조각·테두리·피벗이 없다. 이걸 안 보면 스프라이트 모드 값만 Multiple 로
            // 남은 일반 텍스처가 영영 건너뛰어진다 — 유니티 6000.3 에서 새로 들인 일반 텍스처가 그랬다(SDK Host 실측).
            if (importer.textureType != TextureImporterType.Sprite) return false;

            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                reason = "스프라이트 시트(Multiple)";
                return true;
            }

            if (importer.spriteBorder != Vector4.zero)
            {
                reason = $"9슬라이스 테두리 {importer.spriteBorder}";
                return true;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            if (settings.spriteAlignment != (int)SpriteAlignment.Center)
            {
                reason = $"피벗 {(SpriteAlignment)settings.spriteAlignment}";
                return true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
            {
                reason = $"PPU {importer.spritePixelsPerUnit}";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 스파인 머티리얼의 Straight Alpha를 켠다. UI(SkeletonGraphic)에 얹으면 이 값이 꺼져 있는 동안
        /// 테두리가 검게 번져 보이는데, 임포트할 때마다 다시 꺼지므로 폴더째 고친다.
        /// 셰이더 이름만 보므로 spine-unity 어셈블리에 의존하지 않는다.
        /// </summary>
        public static int FixSpineMaterials(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null || material.shader == null) continue;
                if (!material.shader.name.Contains("Spine/Skeleton")) continue;

                material.SetInt("_StraightAlphaInput", 1);
                EditorUtility.SetDirty(material);

                count++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[스파인 머티리얼] {count}개를 Straight Alpha로 고쳤습니다.");

            return count;
        }
    }
}
