using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;

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

        StringBuilder sb = new StringBuilder();
        foreach (char c in uniqueChars)
        {
            sb.Append(c);
        }

        // ⚠️ 아틀라스를 채우려면 Dynamic이어야 합니다. TryAddCharacters는 Static이면
        //    "AtlasPopulationMode is set to Static" 경고만 남기고 <b>아무것도 하지 않습니다.</b>
        //    이 스크립트가 마지막에 Static으로 되돌리므로, 첫 실행 이후로는 매번 그 상태로 시작해
        //    베이크가 통째로 no-op이었습니다. 그런데도 아래 성공 로그는 그대로 찍혀
        //    <b>실패가 성공처럼 보였습니다.</b> 그 사이 추가된 대사의 글자는 폴백 폰트로 렌더돼
        //    문장 중간에서 글꼴이 튀고 있었습니다. (2026-08-14)
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        bool added;
        string missing;
        try
        {
            added = font.TryAddCharacters(sb.ToString(), out missing);
        }
        finally
        {
            // 런타임에 글리프를 동적으로 굽지 않도록 항상 Static으로 되돌립니다.
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

        Debug.Log($"[Prebake] 요청 {uniqueChars.Count}자 처리 완료. 아틀라스 등록 문자 {font.characterTable.Count}자.");
    }
}
