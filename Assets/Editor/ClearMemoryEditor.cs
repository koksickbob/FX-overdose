using UnityEditor;
using UnityEngine;
using System.IO;

namespace FXOverdose.Editor
{
    /// <summary>
    /// 제거된 미연시 자유 채팅(LLM) 시스템이 남긴 대화 기억 폴더를 정리합니다.
    /// 시스템 자체는 2026-08-12에 삭제되었으나, 기존 플레이어 데이터에 폴더가 남아 있을 수 있어 정리 도구만 존치합니다.
    /// </summary>
    public class ClearMemoryEditor
    {
        [MenuItem("FX Overdose/AI/Clear DatingSim Memory Residue (구 자유 채팅 잔여 데이터)")]
        public static void ClearMemory()
        {
            string dir = Path.Combine(Application.persistentDataPath, "DatingSimMemories");
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
                Debug.Log("[FX Overdose] 구 자유 채팅 대화 기억 폴더(DatingSimMemories)를 삭제했습니다.");
            }
            else
            {
                Debug.Log("[FX Overdose] 삭제할 잔여 대화 기억 데이터가 없습니다.");
            }
        }
    }
}
