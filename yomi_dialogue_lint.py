"""요미 대사 규칙 검사기.

기준 문서: docs/P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md (4절 말투 하드 룰)

사용법:  python yomi_dialogue_lint.py
종료 코드: 위반이 있으면 1

표시 표면마다 길이 상한이 다르다. 60자는 LLM 단발 생성 제약이지 모든 대사의 규칙이 아니다.
"""

import glob
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

# --- 표시 표면별 길이 상한 -------------------------------------------------
LEN_TRADING_BUBBLE = 80   # AIVisualController 말풍선 (Ellipsis). 실측 최대 71자
LEN_DAILY = 60            # 데이팅 일상 대사
LEN_EVENT_POPUP = 150     # 돌발 이벤트 팝업 본문 (Overflow 설정이라 여유 있음)
LEN_LLM = 60              # LLM 단발 생성 대사

# 요미 대사를 문자열 리터럴로 들고 있는 스크립트. 새로 생기면 여기에 추가한다.
# .cs는 대사와 UI 라벨·뉴스 본문이 섞여 있어 호칭/자기지칭만 검사한다.
CS_SOURCES = [
    "Assets/Scripts/Editor/GenerateTemplateFallbackText.cs",
    "Assets/Editor/ChoiceEventAssetGenerator.cs",
    "Assets/Scripts/Events/ChoiceEventController.cs",
    "Assets/Scripts/AI/MentalDrainGimmickController.cs",
    "Assets/Scripts/AI/LLM/LLMSafeGenerator.cs",
    "Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs",
    "Assets/Scripts/DatingSim/YomiRoom/YomiRoomUIController.cs",
    "Assets/Scripts/DatingSim/Dialogue/PlaceholderDialogueProvider.cs",
]

# "마스터"가 요미 대사가 아닌 용도로 쓰이는 곳 (업적명, 파일 용어 등)
CS_ALLOW = re.compile(r"트레이딩 마스터|마스터 CSV|MasterPlan|ScenarioMaster")

MASTER = re.compile(r"마스터")

# 자기 자신을 기계로 지칭하는 표현만 잡는다.
# "알고리즘 트레이딩", "시스템이 막혔어"(거래소)는 정상 어휘이므로 제외한다.
AI_SELF = re.compile(
    r"재부팅|학습 데이터|프로그램된|전원이 꺼|요미는 AI|나는 AI|요미도 AI|인공지능이(야|라)"
)

HONORIFIC = re.compile(
    r"(습니다|입니다|세요|셔요|예요|에요|어요|아요|네요|지요|해요|하죠|이죠|"
    r"십시오|드려요|드릴게요|드립니다)"
)
# 문장 끝의 '~요'만 존댓말로 본다. 주요/중요/필요 같은 명사를 오탐하지 않도록 종결 위치로 한정.
SENTENCE_END_YO = re.compile(r"[가-힣]요$")
NOUN_YO = re.compile(r"(주|중|필|수|개|강|소)요$")
# 상대를 향한 비꼼 1문장은 허용한다 (바이블 4.3절 예외).
SARCASM = re.compile(r"(오빠나|그쪽이나|알아서) .*(세요|십시오|하시죠|드세요)")


def decode(raw: str) -> str:
    """Unity YAML의 \\uXXXX 이스케이프를 사람이 읽을 수 있는 한글로 되돌린다."""
    flat = re.sub(r"\s+", " ", raw.replace("\n", " ")).strip()
    try:
        return flat.encode().decode("unicode_escape")
    except UnicodeDecodeError:
        return flat


def yaml_scalars(text: str, field: str):
    """`field: "..."` 형태의 값을 줄바꿈으로 접힌 것까지 포함해 뽑는다."""
    return [m.group(1) for m in re.finditer(rf'{field}: "((?:[^"\\]|\\.)*)"', text, re.S)]


def is_honorific(line: str) -> bool:
    if SARCASM.search(line):
        return False
    body = line.replace("요미", "§")  # 이름의 '요'가 오탐되지 않도록 치환
    if HONORIFIC.search(body):
        return True
    for sentence in re.split(r"[.!?~…\n]+", body):
        s = sentence.strip().strip('"\'')
        if s and SENTENCE_END_YO.search(s) and not NOUN_YO.search(s):
            return True
    return False


def violations(line: str, limit, check_tone: bool):
    found = []
    if MASTER.search(line):
        found.append("마스터호칭")
    if AI_SELF.search(line):
        found.append("AI자기지칭")
    if check_tone:
        if is_honorific(line):
            found.append("존댓말")
        if limit and len(line) > limit:
            found.append(f"길이{len(line)}/{limit}")
    return found


def collect():
    rows = []  # (출처, 대사, 길이상한, 톤검사여부)

    db = [
        ("Assets/YomiDialogueDatabase.asset", LEN_TRADING_BUBBLE),
        ("Assets/YomiDailyDialogueDatabase.asset", LEN_DAILY),
    ]
    for path, limit in db:
        if not os.path.exists(path):
            continue
        text = open(path, encoding="utf-8").read()
        for raw in yaml_scalars(text, "- text"):
            rows.append((os.path.basename(path), decode(raw), limit, True))

    for path in glob.glob("Assets/Resources/Events/**/*.asset", recursive=True):
        text = open(path, encoding="utf-8").read()
        name = os.path.basename(path)
        for raw in yaml_scalars(text, "AIMonologue"):
            rows.append((f"{name}:AIMonologue", decode(raw), LEN_EVENT_POPUP, True))
        block = re.search(r"FallbackMonologues:\n((?:  - .*\n)+)", text)
        if block:
            for raw in re.findall(r"  - (.*)", block.group(1)):
                rows.append((f"{name}:Fallback", decode(raw.strip().strip('"')), LEN_LLM, True))

    for path in CS_SOURCES:
        if not os.path.exists(path):
            continue
        for i, raw_line in enumerate(open(path, encoding="utf-8"), 1):
            if not re.search(r"[가-힣]", raw_line) or CS_ALLOW.search(raw_line):
                continue
            for literal in re.findall(r'"((?:[^"\\]|\\.)*)"', raw_line):
                if re.search(r"[가-힣]", literal):
                    rows.append((f"{os.path.basename(path)}:{i}", literal, None, False))

    return rows


def main():
    rows = collect()
    bad = [
        (src, line, v)
        for src, line, limit, tone in rows
        if (v := violations(line, limit, tone))
    ]

    print(f"검사 대상 {len(rows)}줄 / 위반 {len(bad)}줄\n")
    counts = {}
    for _, _, vs in bad:
        for v in vs:
            key = "길이초과" if v.startswith("길이") else v
            counts[key] = counts.get(key, 0) + 1
    if counts:
        print("위반 요약:", counts, "\n")

    shown = set()
    for src, line, v in bad:
        key = (src.split(":")[0], line)
        if key in shown:
            continue
        shown.add(key)
        print(f'[{",".join(v)}] {src}\n    {line}')

    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
