# LLM 모델 교체 및 다운로드 가이드

이 문서는 FX Overdose 프로젝트에서 사용되는 로컬 LLM(LLMUnity 기반)을 한국어에 최적화된 모델로 교체하고 적용하는 방법을 안내합니다.

## 1. 개요 및 교체 배경
기존에 사용하던 범용/다국어 모델(예: Qwen 계열)은 한국어(Korean)로만 응답하라는 프롬프트 지시를 가끔 무시하고 중국어 간체나 영어를 출력하는 환각(Hallucination) 현상이 발생했습니다. 이는 게임 내 UI에서 폰트 깨짐(Missing Glyph, □□□ 현상)을 유발하는 주요 원인이었습니다.

이를 해결하기 위해 한국어 어휘와 뉘앙스에 특화된 초경량(3B) 모델인 **`Llama-3.2-Korean-Bllossom-3B`** 로 모델을 교체합니다.

---

## 2. 모델 다운로드 및 배치

1. **HuggingFace에서 모델 다운로드**
   아래 링크를 클릭하여 GGUF 포맷의 양자화(Q4_K_M) 모델을 다운로드합니다. (용량 약 2.2GB)
   * [📥 llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M.gguf 다운로드 링크](https://huggingface.co/Bllossom/llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M/resolve/main/llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M.gguf)

2. **프로젝트 폴더에 파일 배치**
   다운로드한 `.gguf` 파일을 FX Overdose 유니티 프로젝트의 다음 경로에 넣습니다.
   * **경로:** `Assets/StreamingAssets/Models/`
   * **최종 파일 위치:** `Assets/StreamingAssets/Models/llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M.gguf`

---

## 3. 유니티 씬(Scene) 적용 방법

`LLMUnity` 컴포넌트가 새 모델을 인식하도록 씬(Scene) 설정을 변경해야 합니다.

1. 유니티 에디터를 열고 `Assets/Scenes/TitleScene.unity` 를 더블클릭하여 엽니다. (또는 LLM 컴포넌트가 있는 다른 씬)
2. 하이어라키(Hierarchy) 창에서 `LLMSafeGenerator` (또는 LLM 스크립트가 붙어있는 게임 오브젝트)를 선택합니다.
3. 인스펙터(Inspector) 창에서 **LLM** 컴포넌트를 찾습니다.
4. **Model** 필드를 클릭하여 방금 `StreamingAssets/Models` 폴더에 넣은 `llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M.gguf` 파일로 변경해 줍니다.
5. 씬(Scene)을 저장(`Ctrl + S`)합니다.

---

## 4. 프롬프트 및 안전 장치 (LLMSafeGenerator.cs)

새로운 모델 적용과 더불어, 스크립트(`Assets/Scripts/AI/LLM/LLMSafeGenerator.cs`)에는 다음과 같은 방어 로직이 적용되어 있습니다.

* **강력한 시스템 프롬프트:** `[IMPORTANT] 무조건 한국어(Korean)로만 응답하세요! 영어나 한자, 중국어를 사용하면 절대 안 됩니다.`
* **한국어 검출기(`IsValidKoreanText`):** LLM이 반환한 텍스트에 한글 음절(가-힣)이 최소 5자 이상 포함되어 있지 않다면(즉, 중국어나 영어로만 생성되었다면), 해당 응답을 폐기하고 안전한 **[더미 데이터]**를 반환하여 폰트 깨짐을 원천 차단합니다.

이제 에디터에서 플레이 버튼을 눌러 돌발 이벤트 텍스트가 자연스러운 한국어로 잘 나오는지 테스트하시면 됩니다!
