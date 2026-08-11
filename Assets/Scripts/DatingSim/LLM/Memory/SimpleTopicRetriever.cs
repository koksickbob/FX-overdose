using System.Collections.Generic;
using System.Threading.Tasks;

namespace FXOverdose.DatingSim.LLM.Memory
{
    public class SimpleTopicRetriever : IMemoryRetriever
    {
        public Task<List<string>> RetrieveMemoriesAsync(string userMessage, MemoryTopic topic, int count)
        {
            List<string> memories = new List<string>();
            if (DatingSimMemoryDB.Instance != null && count > 0)
            {
                string recentContext = DatingSimMemoryDB.Instance.GetRecentContextString();
                if (!string.IsNullOrEmpty(recentContext) && recentContext != "최근 대화 없음")
                    memories.Add(recentContext);
            }

            return Task.FromResult(memories);
        }
    }
}
