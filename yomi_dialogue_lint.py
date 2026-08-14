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
    "Assets/Scripts/DatingSim/Dialogue/YomiTalkTopics.cs",
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


TALK_TABLE = "Assets/Scripts/DatingSim/Dialogue/YomiTalkTopics.cs"
MAX_CHOICE_LEN = 25    # 선택지 버튼 1줄 · 17pt
MAX_BURST_LEN = 60     # 말풍선 1개 (일상 대사 상한)
MAX_NARRATION_LEN = 40 # 지문 1줄

# ── 호감도 배분 (2026-08-14 개편, 계획서 14장) ──
# 자유 채팅이 하루 1회가 되면서 토픽 1편 = 하루치다. 힌트 임계(2/3)와 직결되므로
# 총획득은 상한이 아니라 "정확히" 검사한다 — 모자라면 명시 힌트가 영영 안 나온다.
TOPIC_GAIN_TOTAL = 3   # 토픽 1편의 총획득 (직면 +2 · 착지 +1)
TOPIC_LOSS_TOTAL = -2  # 토픽 1편의 총감소 (-1 선택지를 서로 다른 노드에 2개)
CHOICE_AFF_MIN = -1    # 선택지 하나의 하한
CHOICE_AFF_MAX = 2     # 선택지 하나의 상한 (= 토픽의 피크. 직면 노드에만 둔다)
NARRATION_MARK = "※"
RICH_TAG = re.compile(r"<[^>]+>")   # 타자기 연출이 글자 수를 그대로 센다 (TS28)

# ⚠️ 선택지는 3인자(즉답 포함)가 될 수 있다. 닫는 괄호를 바로 요구하면 즉답이 붙은 선택지를
#    통째로 못 보고 "선택지 0개"로 세면서 조용히 통과한다 — 실패가 성공처럼 보인다. (TS14)
#    호감도는 음수(-1)가 허용되므로 부호까지 잡는다.
CHOICE_RE = re.compile(
    r'new TalkChoice\("((?:[^"\\]|\\.)*)"\s*,\s*TalkTrait\.(\w+)\s*,\s*(-?\d+)'
    r'\s*(?:,\s*"((?:[^"\\]|\\.)*)")?\s*\)', re.S
)

# 플레이어 성격 5축 (바이블 2.2절). 태그가 없던 시절 무던함이 75%, 감춘 불안이 0건이었다.
TRAITS = ["Plain", "Warm", "Duty", "Anxious", "Waver"]
TRAIT_DOMINANCE = 0.6   # 한 축이 토픽의 이 비율을 넘으면 경고

# 플레이어가 관계의 우위를 들이대는 표현. 좁게만 잡는다 — "얹혀사는 거 아니야" 같은 부정문은 정당하다.
SUPERIORITY = re.compile(r"돈은 내가|내가 벌|누구 덕에|나가라|나가 살")
EMOJI = re.compile(r"[\U0001F000-\U0001FAFF☀-➿]")

# ── 말맛 쿼터 (13장 V-1~V-3). 문장 질은 사람이 보므로 전부 경고다. ──
PLAIN_END_MAX = 0.40      # 평서 "." 종결 비율 상한 (V-1)
ECHO_MIN = 4              # 토픽당 에코(요미 단어 받아치기) 최소 (V-2)
FILLER_MIN = 2            # 토픽당 추임새 개시 최소 (V-3)
FILLER = re.compile(r"^(어|아니|음|글쎄|야|와|아)[\s,.]")
# V-1이 잡으려는 건 "밋밋한 단문 평서 + 마침표" 연발이다.
# 부드러운 어미로 끝나거나(~잖아/~는데/~봐/~마/~돼/~줘 등),
# 문장 안에 이미 리듬 장치(두 마디 · 도치 쉼표 · 말줄임)가 있으면 평서로 세지 않는다.
SOFT_END = re.compile(r"(잖아|는데|은데|건데|던데|라니까|다니까|니까|네|지|게|자|고|며|든|봐|마|돼|줘|래)\.$")
TEXTURE = re.compile(r"\.\.\.|,|.\. ")  # 말줄임 · 쉼표 · 문장 중간의 마침표(두 마디)


def is_plain_end(line):
    st = line.rstrip()
    if not st.endswith(".") or st.endswith("..."):
        return False
    return not SOFT_END.search(st) and not TEXTURE.search(st)


def check_talk_table():
    """선택형 대화 테이블의 구조 계약을 검사한다 (계획서 7.2절)."""
    if not os.path.exists(TALK_TABLE):
        return 0

    src = open(TALK_TABLE, encoding="utf-8").read()
    body = src.split("public static readonly TalkTopic[] All", 1)
    if len(body) < 2:
        return 0
    body = body[1].split("public static string[] HintLinesFor", 1)[0]

    failures = 0
    topics = re.split(r'new TalkTopic\("', body)[1:]
    print(f"[대화 테이블] 토픽 {len(topics)}개 검사")

    # 해금 경계는 리터럴이 아니라 AffectionTier 상수로 적힌다 (T1~T4 확정, 2026-08-14).
    # 같은 파일의 const 선언을 읽어 해석한다 — 모르는 이름이면 -1이 되어 아래 gate 검사가 잡는다.
    consts = dict(re.findall(r"const int (\w+) = (\d+)", src))

    def resolve(token):
        if token.isdigit():
            return int(token)
        return int(consts.get(token.split(".")[-1], -1))

    # 호감도 0(게임 시작 시점)에 열리는 토픽 수. 해금 조건을 잘못 걸면 첫날부터 대화가 막힌다. (TS10)
    # 시간대까지 걸리므로 구간별로 센다 — 밤은 슬롯이 두 칸이라 최소 2편이 필요하다. (TS23)
    slots_per_time = {"Morning": 1, "Noon": 1, "Evening": 1, "Night": 2}
    starting = {k: 0 for k in slots_per_time}
    for chunk in topics:
        gate = re.search(r'",\s*TalkCategory\.\w+,\s*([\w\s|.]+?),\s*([\w.]+),\s*([\w.]+),', chunk)
        if not gate or resolve(gate.group(2)) < 0 or resolve(gate.group(3)) < 0:
            print(f"  ❌ {chunk.split(chr(34), 1)[0]}: 카테고리/시간대/호감도 인자를 못 읽었다")
            failures += 1
            continue
        if resolve(gate.group(2)) != 0:
            continue
        flags = re.findall(r"TalkTime\.(\w+)", gate.group(1))
        if "Any" in flags:
            flags = list(slots_per_time)
        for f in flags:
            if f in starting:
                starting[f] += 1

    for name, need in slots_per_time.items():
        if starting[name] < need:
            print(f"  ❌ {name} 시간대에 호감도 0 토픽이 {starting[name]}편 "
                  f"(슬롯 {need}칸이라 최소 {need}편) — 그 시간대에 대화가 막힌다")
            failures += 1
    print(f"  · 시작 시점 해금 토픽 (아침/낮/저녁/밤): "
          f"{starting['Morning']}/{starting['Noon']}/{starting['Evening']}/{starting['Night']}")

    for chunk in topics:
        topic_id = chunk.split('"', 1)[0]
        nodes = re.split(r"new TalkNode\(", chunk)[1:]

        # 토픽 = 화제가 아니라 장면. 3노드로는 감정 곡선(도입-곁길-균열-직면-전환-착지)이 안 그려진다. (10.2절)
        if not 4 <= len(nodes) <= 8:
            print(f"  ❌ {topic_id}: 노드 {len(nodes)}개 (4~8 이어야 함)")
            failures += 1

        topic_max = 0          # 토픽 최대 획득 호감도
        topic_min = 0          # 토픽 최대 감소 호감도 (음수)
        best_nodes = []        # 피크(+2) 선택지가 있는 노드 인덱스
        choice_counts = []
        narration = 0
        seen_lines = {}        # 같은 장면 안에서의 문장 중복 검사
        trait_count = {t: 0 for t in TRAITS}
        traits_in_node = {}    # 노드 인덱스 → 그 노드에 쓰인 축들
        plain_end = 0          # 평서 "." 종결 (V-1)
        echo = 0               # 요미 단어 받아치기 (V-2)
        filler = 0             # 추임새 개시 (V-3)

        def once(kind, text, where):
            """토픽 안에서 같은 문장을 두 번 쓰면 실패시킨다. (TS20)"""
            n = 0
            # 타자기 연출이 글자 수를 그대로 세므로 리치텍스트 태그가 들어가면 출력이 깨진다. (TS28)
            if RICH_TAG.search(text):
                print(f"  ❌ {topic_id}: {kind}에 리치텍스트 태그 — {where} — {text}")
                n += 1
            key = text.strip()
            if key in seen_lines:
                print(f"  ❌ {topic_id}: {kind} 중복 — {where} / {seen_lines[key]} — {key}")
                return n + 1
            seen_lines[key] = where
            return n

        for n, node in enumerate(nodes):
            head, sep, _ = node.partition("new TalkChoice(")
            lines = [m for m in re.findall(r'"((?:[^"\\]|\\.)*)"', head) if m.strip()]
            choices = CHOICE_RE.findall(node)

            # --- 버스트 (R-1) ---
            if not sep:
                print(f"  ❌ {topic_id}[{n}]: 선택지가 없다")
                failures += 1
            if not 1 <= len(lines) <= 3:
                print(f"  ❌ {topic_id}[{n}]: 말풍선 {len(lines)}개 (1~3 이어야 함)")
                failures += 1
            for line in lines:
                is_narr = line.startswith(NARRATION_MARK)
                limit = MAX_NARRATION_LEN if is_narr else MAX_BURST_LEN
                if is_narr:
                    narration += 1
                if len(line) > limit:
                    print(f"  ❌ {topic_id}[{n}]: {'지문' if is_narr else '말풍선'} {len(line)}자 초과 — {line}")
                    failures += 1
                failures += once("대사", line, f"[{n}]")

            # --- 선택지 (R-3) ---
            if not 2 <= len(choices) <= 4:
                print(f"  ❌ {topic_id}[{n}]: 선택지 {len(choices)}개 (2~4 이어야 함)")
                failures += 1
            choice_counts.append(len(choices))

            # 이 노드의 요미 대사에 나온 단어들. 선택지가 하나라도 재사용하면 에코로 센다. (V-2)
            yomi_words = set(re.findall(r"[가-힣]{2,}", " ".join(lines))) - {"오빠", "요미"}

            node_max = 0
            node_min = 0
            traits_in_node[n] = set()
            for text, trait, aff, reply in choices:
                if is_plain_end(text):
                    plain_end += 1
                if any(w in text for w in yomi_words):
                    echo += 1
                if FILLER.match(text):
                    filler += 1
                aff = int(aff)
                if not CHOICE_AFF_MIN <= aff <= CHOICE_AFF_MAX:
                    print(f"  ❌ {topic_id}[{n}]: 호감도 {aff:+d} — 선택지 하나는 {CHOICE_AFF_MIN}~+{CHOICE_AFF_MAX} 범위여야 한다")
                    failures += 1
                node_max = max(node_max, aff)
                node_min = min(node_min, aff)
                if aff == CHOICE_AFF_MAX and n not in best_nodes:
                    best_nodes.append(n)

                # --- 플레이어 대사 검사 (12장 P-7) ---
                if trait in trait_count:
                    trait_count[trait] += 1
                    traits_in_node[n].add(trait)
                else:
                    print(f"  ❌ {topic_id}[{n}]: 모르는 축 TalkTrait.{trait}")
                    failures += 1
                if SUPERIORITY.search(text):
                    print(f"  ❌ {topic_id}[{n}]: 관계 우위를 들이대는 선택지 — {text}")
                    failures += 1
                if EMOJI.search(text):
                    print(f"  ❌ {topic_id}[{n}]: 선택지에 이모지 — {text}")
                    failures += 1
                if "오빠" in text:
                    print(f"  ❌ {topic_id}[{n}]: 플레이어가 자기를 '오빠'라고 부른다 — {text}")
                    failures += 1

                if len(text) > MAX_CHOICE_LEN:
                    print(f"  ❌ {topic_id}[{n}]: 선택지 {len(text)}자 초과 — {text}")
                    failures += 1
                if reply:
                    if len(reply) > MAX_BURST_LEN:
                        print(f"  ❌ {topic_id}[{n}]: 즉답 {len(reply)}자 초과 — {reply}")
                        failures += 1
                    failures += once("즉답", reply, f"[{n}]")
            topic_max += node_max
            topic_min += node_min

        # --- 토픽 단위 계약 (14장, 2026-08-14 개편) ---
        # 총획득은 정확히 검사한다. 모자라면 명시 힌트(임계 3)가 구조적으로 못 나온다.
        if topic_max != TOPIC_GAIN_TOTAL:
            print(f"  ❌ {topic_id}: 최대 획득 +{topic_max} (정확히 +{TOPIC_GAIN_TOTAL}이어야 함) — 힌트 임계 2/3과 어긋난다")
            failures += 1
        # 총감소도 정확히 -2. 감소 선택지가 아예 없으면 이번 개편의 요구가 빠진 것이고, 넘치면 하루 한도를 깬다.
        if topic_min != TOPIC_LOSS_TOTAL:
            print(f"  ❌ {topic_id}: 최대 감소 {topic_min} (정확히 {TOPIC_LOSS_TOTAL}이어야 함) — -1 선택지를 서로 다른 노드에 2개 둔다")
            failures += 1
        if len(best_nodes) != 1:
            print(f"  ❌ {topic_id}: 피크(+{CHOICE_AFF_MAX}) 노드 {len(best_nodes)}개 (토픽당 정확히 1개여야 함)")
            failures += 1
        half = len(nodes) // 2
        early = [n for n in best_nodes if n < half]
        if early:
            print(f"  ❌ {topic_id}: 피크(+{CHOICE_AFF_MAX})가 전반부 노드 {early}에 있다 (후반 절반에만 둔다)")
            failures += 1
        if nodes and not 3 <= narration <= 5:
            print(f"  ❌ {topic_id}: 지문 {narration}줄 (3~5줄이어야 함)")
            failures += 1
        if len(set(choice_counts)) == 1 and len(choice_counts) > 2:
            print(f"  ⚠️  {topic_id}: 모든 노드의 선택지가 {choice_counts[0]}개 — 리듬이 고정됐다 (경고)")

        # --- 플레이어 5축 커버리지 (12장 P-2) ---
        missing = [t for t in TRAITS if trait_count[t] == 0]
        if missing:
            print(f"  ❌ {topic_id}: 안 쓰인 플레이어 축 {missing} — 5축 전부 최소 1회")
            failures += 1
        total_choices = sum(trait_count.values())
        if total_choices:
            top = max(TRAITS, key=lambda t: trait_count[t])
            if trait_count[top] > total_choices * TRAIT_DOMINANCE:
                print(f"  ⚠️  {topic_id}: {top} 축이 {trait_count[top]}/{total_choices} — 한 축으로 쏠렸다 (경고)")

        # --- 말맛 쿼터 (13장). 전부 경고 — 문장 질은 사람이 본다. ---
        if total_choices:
            if plain_end > total_choices * PLAIN_END_MAX:
                print(f"  ⚠️  {topic_id}: 평서 '.' 종결 {plain_end}/{total_choices} — 40% 초과, 통보처럼 읽힌다 (V-1)")
            if echo < ECHO_MIN:
                print(f"  ⚠️  {topic_id}: 에코 {echo}개 — 요미 단어를 받아치는 선택지가 {ECHO_MIN}개는 있어야 흐름이 이어진다 (V-2)")
            if filler < FILLER_MIN:
                print(f"  ⚠️  {topic_id}: 추임새 개시 {filler}개 — 최소 {FILLER_MIN}개 (V-3)")

        # 요미가 가장 무방비한 노드(피크가 있는 곳)에서 플레이어도 자기를 열어야 한다.
        # 전부 요미를 향한 위로·질문이면 대화가 아니라 상담이 된다. (12.2절)
        if best_nodes and not any(traits_in_node.get(n, set()) & {"Anxious", "Duty"} for n in best_nodes):
            print(f"  ❌ {topic_id}: 피크 노드 {best_nodes}에 Anxious/Duty가 없다 — 플레이어가 자기를 여는 자리가 없다")
            failures += 1

    # 힌트 풀: 4 Regime × 2 티어 = 8칸이 전부 차 있어야 하고, Squeeze 명시 티어엔 방향 단어가 없어야 한다.
    for pool in ("BullVague", "BullClear", "BearVague", "BearClear",
                 "SidewaysVague", "SidewaysClear", "SqueezeVague", "SqueezeClear"):
        m = re.search(rf"{pool} =\s*\{{(.*?)\}};", src, re.S)
        lines = re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(1)) if m else []
        if len(lines) < 5:
            print(f"  ❌ 힌트 풀 {pool}: {len(lines)}줄 (최소 5줄)")
            failures += 1
        if pool == "SqueezeClear":
            for line in lines:
                if "위" in line or "아래" in line:
                    print(f"  ❌ SqueezeClear에 방향 단어 — {line}")
                    failures += 1

    print(f"[대화 테이블] 위반 {failures}건\n")
    return failures


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

    print()
    structural = check_talk_table()
    return 1 if (bad or structural) else 0


if __name__ == "__main__":
    sys.exit(main())
