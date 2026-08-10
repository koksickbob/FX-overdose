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

        private IMemoryRetriever currentRetriever;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 기본 Retriever 설정 (추후 VectorRetriever로 런타임 교체 가능)
                currentRetriever = new SimpleTopicRetriever();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetRetriever(IMemoryRetriever retriever)
        {
            currentRetriever = retriever;
        }

        public string GetFilePath(MemoryTopic topic)
        {
            int slot = SaveLoadManager.Instance != null ? SaveLoadManager.Instance.ActiveStorySlotIndex : 0;
            string dir = Path.Combine(Application.persistentDataPath, "DatingSimMemories");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"Slot{slot}_Memories_{topic}.json");
        }

        public async Task SaveConversationAsync(MemoryTopic topic, string text)
        {
            string path = GetFilePath(topic);
            List<string> memories = new List<string>();

            // 1. 기존 메모리 로드 (비동기)
            if (File.Exists(path))
            {
                try
                {
                    string json = await Task.Run(() => File.ReadAllText(path));
                    MemoryListWrapper wrapper = JsonUtility.FromJson<MemoryListWrapper>(json);
                    if (wrapper != null && wrapper.memories != null)
                        memories = wrapper.memories;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DatingSimMemoryDB] Failed to read memories: {e.Message}");
                }
            }

            // 2. 새 대화 추가
            memories.Add(text);

            // 3. 다시 저장 (비동기)
            try
            {
                MemoryListWrapper newWrapper = new MemoryListWrapper { memories = memories };
                string newJson = JsonUtility.ToJson(newWrapper, true);
                await Task.Run(() => File.WriteAllText(path, newJson));
            }
            catch (Exception e)
            {
                Debug.LogError($"[DatingSimMemoryDB] Failed to write memories: {e.Message}");
            }
        }

        public async Task<List<string>> LoadRecentConversationsAsync(string userMessage, MemoryTopic topic, int count)
        {
            if (currentRetriever != null)
            {
                return await currentRetriever.RetrieveMemoriesAsync(userMessage, topic, count);
            }
            return new List<string>();
        }

        [Serializable]
        public class MemoryListWrapper
        {
            public List<string> memories = new List<string>();
        }
    }
}
