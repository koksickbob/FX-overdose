# -*- coding: utf-8 -*-
"""
이벤트 추적표 생성기.

`docs/P2_Story/scenario_통합.md` 를 파싱해 이벤트 74건의 현황표를 만든다.
기계로 뽑을 수 있는 열(챕터·장소·선택지 수·수치 델타·인게임 텍스트 유무)만 채우고,
사람이 갱신하는 진행 열(데이터/배경/감정/구현)은 기존 표에서 읽어와 보존한다.

    python docs/P2_08_Story_Implementation/build_tracker.py

주의: 통합 문서를 다시 만들면 이 스크립트도 다시 돌린다.
"""
import io
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "P2_Story", "scenario_통합.md")
OUT = os.path.join(HERE, "P2_08_01_Event_Tracker.md")

# 사람이 손으로 갱신하는 열. 기존 표가 있으면 그 값을 그대로 가져온다.
MANUAL_COLS = ["데이터", "배경", "감정", "구현"]
TODO = "☐"


def parse_events(lines):
    """통합 문서 2부에서 이벤트 블록을 순서대로 뽑는다."""
    events = []
    chapter = group = None
    cur = None
    part = None
    in_draft = False
    for line in lines:
        if re.match(r"^# ", line):
            part = line
            if not line.startswith("# 2."):
                cur = None  # 2부를 벗어나면 수집 중단
            continue
        if part is None or not part.startswith("# 2."):
            continue

        m = re.match(r"^## (2\.\d+ .*)$", line)
        if m:
            chapter = m.group(1).strip()
            group = None
            cur = None
            continue
        m = re.match(r"^### (메인 시나리오|사이드 스토리.*|인게임 전용.*|［.*)$", line)
        if m:
            t = m.group(1)
            group = "메인" if t.startswith("메인") else ("사이드" if t.startswith("사이드") else None)
            continue
        m = re.match(r"^#### (.*)$", line)
        if m:
            title = m.group(1).split("  ·  ")[0].strip()
            eid = re.match(r"^\[([^\]]+)\]", title)
            cur = {
                "id": eid.group(1).strip() if eid else title,
                "title": title,
                "chapter": chapter,
                "group": group or "—",
                "draft": [],
                "ingame": True,
            }
            events.append(cur)
            in_draft = False
            continue
        if cur is None:
            continue
        if line.strip() == "**［기획 초안］**":
            in_draft = True
            continue
        if line.strip() == "**［인게임 텍스트］**":
            in_draft = False
            continue
        if line.strip() == "*(인게임 텍스트 미작성)*":
            cur["ingame"] = False
            continue
        if in_draft:
            cur["draft"].append(line)
    return events


CHOICE_RE = re.compile(r"\[선택[:：]\s*(.+?)\]\s*$|\[선택[:：]\s*(.+?)\](?=\s)")
NUM_RE = re.compile(r"호감도\s*([+\-]?\d+).*?의존도\s*([+\-]?\d+)")


def choice_stats(draft):
    """선택지 묶음 수, 총 선택지 수, 호감도/의존도 진폭을 센다."""
    groups = []
    for line in draft:
        for m in re.finditer(r"\[선택[:：](.+?)\]", line):
            opts = [o.strip() for o in m.group(1).split(" / ") if o.strip()]
            groups.append(opts)
    if not groups:
        return 0, 0, "—"
    total = sum(len(g) for g in groups)
    aff, obs = [], []
    for g in groups:
        for o in g:
            n = NUM_RE.search(o)
            if n:
                aff.append(int(n.group(1)))
                obs.append(int(n.group(2)))
            else:
                a = re.search(r"호감도\s*([+\-]?\d+)", o)
                if a:
                    aff.append(int(a.group(1)))
    if not aff:
        return len(groups), total, "—"
    span = "호 %+d~%+d" % (min(aff), max(aff))
    if obs:
        span += " · 의 %+d~%+d" % (min(obs), max(obs))
    return len(groups), total, span


def location(eid):
    if eid.startswith("주요 이벤트"):
        return {"주요 이벤트①": "집(CP1)", "주요 이벤트②": "전시관(CP2)",
                "주요 이벤트③": "수족관(CP3)", "주요 이벤트④": "전망대(CP4)",
                "주요 이벤트-B": "집(CP-B)"}.get(eid, "집")
    special = {"내집마련": "집(새 집)", "진엔딩": "엔딩 CG", "에덴엔딩": "엔딩 CG", "여행": "온천(1박2일)"}
    head = eid.split("-")[0].strip()
    return special.get(head, head)


def read_manual(path):
    """기존 표에서 사람이 채운 열을 회수한다."""
    kept = {}
    if not os.path.exists(path):
        return kept
    for line in io.open(path, encoding="utf-8").read().split("\n"):
        if not line.startswith("| `"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 10:
            continue
        kept[cells[0].strip("`")] = cells[-4:]
    return kept


def main():
    lines = io.open(SRC, encoding="utf-8").read().split("\n")
    events = parse_events(lines)
    kept = read_manual(OUT)

    out = []
    out.append("# 이벤트 추적표")
    out.append("")
    out.append("> **자동 생성 문서.** `python docs/P2_08_Story_Implementation/build_tracker.py` 로 갱신한다.")
    out.append("> 왼쪽 6열은 [scenario_통합.md](../P2_Story/scenario_통합.md)에서 기계로 뽑으므로 손으로 고치지 말 것.")
    out.append("> 오른쪽 4열(데이터·배경·감정·구현)만 사람이 채우며, 재생성해도 보존된다.")
    out.append("")
    out.append("범례 — `☐` 미착수 / `◐` 작업 중 / `☑` 완료 / `—` 해당 없음")
    out.append("")
    out.append("| 열 | 의미 |")
    out.append("| --- | --- |")
    out.append("| 텍스트 | 인게임 서술·대사 집필 여부 (통합 문서 기준) |")
    out.append("| 선택지 | 선택 묶음 수 × 총 선택지 수 |")
    out.append("| 진폭 | 선택지별 호감도/의존도 증감 범위 |")
    out.append("| 데이터 | `EventCatalog`의 `EventNode` 변환 완료 여부 |")
    out.append("| 배경 | 해당 장소 배경 에셋 |")
    out.append("| 감정 | 필요한 요미 스탠딩 감정 지정·확보 |")
    out.append("| 구현 | 트리거 조건 배선 후 인게임 재생 확인 |")
    out.append("")

    by_chapter = []
    for e in events:
        if not by_chapter or by_chapter[-1][0] != e["chapter"]:
            by_chapter.append((e["chapter"], []))
        by_chapter[-1][1].append(e)

    totals = {"n": 0, "text": 0}
    for chapter, evs in by_chapter:
        done = sum(1 for e in evs if e["ingame"])
        out.append("## %s" % chapter)
        out.append("")
        out.append("이벤트 %d건 · 텍스트 완료 %d건" % (len(evs), done))
        out.append("")
        out.append("| ID | 장소 | 분류 | 텍스트 | 선택지 | 진폭 | 데이터 | 배경 | 감정 | 구현 |")
        out.append("| --- | --- | --- | :---: | :---: | --- | :---: | :---: | :---: | :---: |")
        for e in evs:
            g, total, span = choice_stats(e["draft"])
            man = kept.get(e["id"], [TODO] * 4)
            out.append("| `%s` | %s | %s | %s | %s | %s | %s |" % (
                e["id"], location(e["id"]), e["group"],
                "☑" if e["ingame"] else TODO,
                "—" if g == 0 else ("%d×%d" % (g, total) if g > 1 else str(total)),
                span, " | ".join(man)))
            totals["n"] += 1
            totals["text"] += 1 if e["ingame"] else 0
        out.append("")

    out.append("## 합계")
    out.append("")
    out.append("이벤트 **%d건** · 인게임 텍스트 **%d건 완료 / %d건 미작성**"
               % (totals["n"], totals["text"], totals["n"] - totals["text"]))
    out.append("")

    io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(out))
    print("wrote %s (%d events)" % (os.path.basename(OUT), totals["n"]))


if __name__ == "__main__":
    main()
