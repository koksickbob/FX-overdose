using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using System;
using System.Linq;

namespace FXOverdose.DatingSim.LLM.Memory
{
    public class SimpleTopicRetriever : IMemoryRetriever
    {
        public async Task<List<string>> RetrieveMemoriesAsync(string userMessage, MemoryTopic topic, int count)
        {
            if (DatingSimMemoryDB.Instance == null) return new List<string>();
            
            string path = DatingSimMemoryDB.Instance.GetFilePath(topic);
            if (!File.Exists(path)) return new List<string>();

            try
            {
                string json = await Task.Run(() => File.ReadAllText(path));
                var wrapper = JsonUtility.FromJson<DatingSimMemoryDB.MemoryListWrapper>(json);
                if (wrapper != null && wrapper.memories != null)
                {
                    // 최신 N개 추출 (리스트 끝부분)
                    var recent = wrapper.memories.Skip(Math.Max(0, wrapper.memories.Count - count)).ToList();
                    return recent;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimpleTopicRetriever] Failed to retrieve memories: {e.Message}");
            }

            return new List<string>();
        }
    }
}
