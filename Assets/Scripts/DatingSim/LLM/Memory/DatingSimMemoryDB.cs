using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.DatingSim.LLM.Memory
{
    public class DatingSimMemoryDB : MonoBehaviour
    {
        public static DatingSimMemoryDB Instance { get; private set; }

        // [단기 기억 버퍼] 현재 진행 중인 시나리오의 최근 대화를 캐싱하여 티키타카(연속성) 유지
        private List<string> currentSceneDialogueBuffer = new List<string>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 유저/요미 대화가 발생할 때마다 버퍼에 추가
        public void AppendToSceneBuffer(string speaker, string message)
        {
            currentSceneDialogueBuffer.Add($"{speaker}: {message}");
            
            // 버퍼가 너무 길어지면(10턴 초과 시) 가장 오래된 대화 삭제하여 프롬프트 토큰 최적화
            if (currentSceneDialogueBuffer.Count > 20) 
            {
                currentSceneDialogueBuffer.RemoveAt(0);
            }
        }

        // LLM 프롬프트 조립 시(ConstructPrompt) 주입할 단기 기억 텍스트 반환
        public string GetRecentContextString()
        {
            if (currentSceneDialogueBuffer.Count == 0) return "최근 대화 없음";
            return string.Join("\n", currentSceneDialogueBuffer);
        }

        // 씬(Scenario) 종료 시 버퍼 초기화 및 파일로 영구 보관(Chunking)
        public async Task ArchiveSceneAndClearAsync(string scenarioID)
        {
            if (currentSceneDialogueBuffer.Count == 0) return;

            string dir = Path.Combine(Application.persistentDataPath, "DatingSimMemories");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"ArchivedScenes.json");
            
            // 차후 장기 아카이브 저장 로직 (Json 직렬화) 구현
            // ...
            
            currentSceneDialogueBuffer.Clear();
            await Task.Yield();
        }
    }
}
