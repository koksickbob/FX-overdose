using System.Collections.Generic;
using System.Threading.Tasks;

namespace FXOverdose.DatingSim.LLM.Memory
{
    /// <summary>
    /// 의미 기반(Vector RAG) 또는 단순 파일 추출 등 다양한 메모리 검색 전략을 
    /// 런타임에 갈아끼우기 위한 인터페이스입니다.
    /// </summary>
    public interface IMemoryRetriever
    {
        /// <summary>
        /// 특정 주제나 컨텍스트에 맞춰 최근 기억(대화)들을 비동기로 검색하여 반환합니다.
        /// </summary>
        /// <param name="userMessage">유저의 최신 입력 (RAG 임베딩 쿼리용으로 사용 가능)</param>
        /// <param name="topic">검색 대상 카테고리/토픽</param>
        /// <param name="count">가져올 기억의 최대 개수</param>
        Task<List<string>> RetrieveMemoriesAsync(string userMessage, MemoryTopic topic, int count);
    }
}
