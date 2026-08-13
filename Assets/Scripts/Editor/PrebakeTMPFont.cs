using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine.TextCore.LowLevel;

public class PrebakeTMPFont
{
    [MenuItem("Tools/Prebake All Scripts Text into Font")]
    public static void Bake()
    {
        string fontPath = "Assets/Fonts/PFStardustBold Dynamic SDF.asset";
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        if (font == null)
        {
            Debug.LogError("Font not found at " + fontPath);
            return;
        }

        // ── 원본 TTF 참조 확인 ──────────────────────────────────────────────
        // Static으로 저장된 폰트 에셋은 m_SourceFontFile이 비어 있습니다({fileID: 0}).
        // TMP가 GUID로 이 참조를 복원하지 못하면 TryAddCharacters는 글리프를 굽기도 전에
        // LoadFontFace 단계에서 <b>입력 문자 전체</b>를 실패로 반환합니다.
        // (2026-08-14 "베이크 실패 2159자" — 실패 목록에 이미 아틀라스에 있던 문자까지
        //  포함돼 있었던 것이 이 경로의 증거입니다. 등록된 문자는 개별 실패로는 나올 수 없습니다.)
        var so = new SerializedObject(font);
        string srcGuid = so.FindProperty("m_SourceFontFileGUID").stringValue;
        string srcPath = AssetDatabase.GUIDToAssetPath(srcGuid);
        Font sourceFont = string.IsNullOrEmpty(srcPath) ? null : AssetDatabase.LoadAssetAtPath<Font>(srcPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[Prebake] 원본 폰트(.ttf)를 찾지 못했습니다. GUID={srcGuid}, 경로='{srcPath}'. " +
                           "폰트 에셋 인스펙터에서 Source Font File을 다시 지정한 뒤 재실행하세요.");
            return;
        }

        // 페이스가 실제로 열리는지 베이크 전에 확인합니다. 실패하면 추측 대신 에러 코드를 남깁니다.
        bool needPathFallback = false;
        string srcFullPath = Path.GetFullPath(srcPath);
        var faceErr = FontEngine.LoadFontFace(sourceFont, (int)font.faceInfo.pointSize);
        if (faceErr != FontEngineError.Success)
        {
            var pathErr = FontEngine.LoadFontFace(srcFullPath, (int)font.faceInfo.pointSize);
            if (pathErr != FontEngineError.Success)
            {
                Debug.LogError($"[Prebake] 원본 폰트 페이스를 열지 못했습니다. Font 오브젝트={faceErr}, 파일 경로={pathErr}, 경로='{srcFullPath}'. " +
                               "경로의 한글이 원인이라면 .ttf 파일명을 영문으로 바꿔보세요. GUID 참조라 기존 링크는 깨지지 않습니다.");
                return;
            }
            // Font 오브젝트로는 안 열리고 파일 경로로는 열리는 상태.
            // TMP는 m_SourceFontFile 로드 실패 시 m_SourceFontFilePath로 재시도하므로 그 폴백을 켜줍니다.
            needPathFallback = true;
            Debug.LogWarning($"[Prebake] Font 오브젝트 로드 실패({faceErr}) → 파일 경로 폴백으로 진행합니다: {srcFullPath}");
        }

        HashSet<char> uniqueChars = new HashSet<char>();
        string[] scriptFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        foreach (string file in scriptFiles)
        {
            string content = File.ReadAllText(file);
            foreach (char c in content)
            {
                // Only add hangul and common symbols
                if ((c >= 0xAC00 && c <= 0xD7A3) ||
                    (c >= 0x3131 && c <= 0x318E) ||
                    (c >= 0x0020 && c <= 0x007E))
                {
                    uniqueChars.Add(c);
                }
            }
        }

        // ── 추가할 문자 사전 선별 ──────────────────────────────────────────
        // TMP의 TryAddCharacters는 "추가할 글리프가 하나도 없음"(전부 이미 등록됐거나
        // 폰트에 글리프 자체가 없음)을 <b>실패</b>로 보고합니다 — false를 반환하고
        // missing에 입력 <b>전체</b>를 담습니다. 2026-08-14 "베이크 실패 2159자"의 실체가
        // 이것이었습니다(스캔 고유 문자 수 == 실패 문자 수 == 2159, 아틀라스는 이미 완성 상태).
        // 그래서 여기서 미리 걸러 진짜 새 문자만 요청합니다.
        int alreadyBaked = 0;
        StringBuilder noGlyph = new StringBuilder(); // 폰트에 글리프가 없어 구울 수 없는 글자
        StringBuilder sb = new StringBuilder();      // 새로 구워야 하는 글자
        foreach (char c in uniqueChars)
        {
            if (font.HasCharacter(c)) { alreadyBaked++; continue; }
            // 위 LoadFontFace 사전 확인으로 페이스가 로드된 상태라 글리프 조회가 가능합니다.
            if (!FontEngine.TryGetGlyphIndex(c, out uint glyphIndex) || glyphIndex == 0) { noGlyph.Append(c); continue; }
            sb.Append(c);
        }

        if (noGlyph.Length > 0)
        {
            Debug.LogWarning($"[Prebake] 폰트에 글리프가 없어 구울 수 없는 문자 {noGlyph.Length}자: {noGlyph}\n" +
                             "이 글자들은 폴백 폰트로 렌더됩니다. 화면에 실제로 나오는 문구라면 다른 표현으로 바꾸십시오.");
        }

        if (sb.Length == 0)
        {
            Debug.Log($"[Prebake] 새로 추가할 문자가 없습니다 — 스캔 {uniqueChars.Count}자 중 {alreadyBaked}자는 이미 아틀라스에 등록돼 있습니다. " +
                      $"(아틀라스 등록 문자 {font.characterTable.Count}자)");
            return;
        }

        // ⚠️ 아틀라스를 채우려면 Dynamic이어야 합니다. TryAddCharacters는 Static이면
        //    "AtlasPopulationMode is set to Static" 경고만 남기고 <b>아무것도 하지 않습니다.</b>
        //    이 스크립트가 마지막에 Static으로 되돌리므로, 첫 실행 이후로는 매번 그 상태로 시작해
        //    베이크가 통째로 no-op이었습니다. 그런데도 아래 성공 로그는 그대로 찍혀
        //    <b>실패가 성공처럼 보였습니다.</b> 그 사이 추가된 대사의 글자는 폴백 폰트로 렌더돼
        //    문장 중간에서 글꼴이 튀고 있었습니다. (2026-08-14)
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        // ⚠️ 참조 주입은 반드시 위 세터 <b>뒤</b>에 합니다. Dynamic 세터가 m_SourceFontFile을
        //    에디터 참조 필드로 덮어쓰는데, 그 필드는 인스펙터를 한 번도 안 열었으면 null이라
        //    세터가 오히려 참조를 지워버릴 수 있습니다.
        so.Update();
        so.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
        if (needPathFallback)
            so.FindProperty("m_SourceFontFilePath").stringValue = srcFullPath;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool added;
        string missing;
        try
        {
            added = font.TryAddCharacters(sb.ToString(), out missing);
        }
        finally
        {
            // 런타임에 글리프를 동적으로 굽지 않도록 항상 Static으로 되돌립니다.
            // (Static 세터가 m_SourceFontFile도 함께 비워 원래 저장 상태로 돌아갑니다.)
            font.atlasPopulationMode = AtlasPopulationMode.Static;
        }

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();

        // 결과를 확인하지 않으면 같은 실패를 또 놓칩니다. 성공 로그는 진짜 성공했을 때만 찍습니다.
        if (!added)
        {
            Debug.LogError($"[Prebake] 베이크 실패. 아틀라스에 넣지 못한 문자 {(missing != null ? missing.Length : 0)}자: {missing}");
            return;
        }

        if (!string.IsNullOrEmpty(missing))
        {
            Debug.LogWarning($"[Prebake] 폰트에 글리프가 없어 건너뛴 문자 {missing.Length}자: {missing}\n" +
                             "이 글자들은 폴백 폰트로 렌더되어 문장 중간에 글꼴이 달라집니다. " +
                             "화면에 나오는 문구라면 다른 표현으로 바꾸십시오.");
        }

        Debug.Log($"[Prebake] 새 문자 {sb.Length}자 추가 완료 (스캔 {uniqueChars.Count}자, 기존 등록 {alreadyBaked}자). " +
                  $"아틀라스 등록 문자 {font.characterTable.Count}자.");
    }
}
