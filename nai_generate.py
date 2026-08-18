#!/usr/bin/env python3
"""프롤로그 컷씬 CG를 NovelAI Diffusion으로 생성한다.

프롬프트는 이 파일에 없다 — docs/P2_05_UI_and_Art/P2_05_Prologue_Cutscene_NovelAI_Prompt_Sheet.md
3절을 파싱해서 쓴다. 시트가 단일 진실 원천이고, 여기 복사해두면 두 벌이 되어 반드시 갈라진다.

사용법:
    python nai_generate.py --dry-run              # 키 없이 파싱만 검증 (Anlas 0)
    python nai_generate.py CUT-01 CUT-03 -n 3     # 두 컷을 3장씩
    python nai_generate.py CUT-06 -n 6            # 감정 절정 컷은 후보를 많이
    python nai_generate.py CUT-04 --seed 123456   # 시드 고정 (일관성 — 시트 4절)
    python nai_generate.py --seed 123456 --vibe ArtPreviews/EVT_MAIN_001/CUT-06_s123456.png
                                                  # 화풍 레퍼런스(Vibe Transfer, 인코딩 2 Anlas) — 시트 4절 3단계

토큰: .env.local 의 NOVELAI_TOKEN, 또는 같은 이름의 환경변수.
      novelai.net → User Settings → Account → Get Persistent API Token
"""

import argparse
import base64
import io
import json
import os
import random
import re
import sys
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SHEET = ROOT / "docs/P2_05_UI_and_Art/P2_05_Prologue_Cutscene_NovelAI_Prompt_Sheet.md"
ENV_FILE = ROOT / ".env.local"
DEFAULT_OUT = ROOT / "ArtPreviews/EVT_MAIN_001"
API_URL = "https://image.novelai.net/ai/generate-image"
ENCODE_URL = "https://image.novelai.net/ai/encode-vibe"
VIBE_CACHE = ROOT / "ArtPreviews/_vibe_cache"

# ⚠️ 지우지 말 것. 이게 없으면 Cloudflare가 `Python-urllib` UA를 막아 본문 `error code: 1010`과 함께 403이 온다.
# 인증 실패(401)처럼 안 보여서 토큰을 의심하게 되는 함정이라 실측 결과를 여기 남긴다.
USER_AGENT = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
              "(KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36")

# 시트 2.1절의 값. 1792x1024는 유료 구간(Anlas 소모)이다 — 무료 구간(1216x832)은 확대 시 기존 컷보다 흐렸다.
# .env.local 의 NAI_MODEL / NAI_WIDTH / NAI_HEIGHT / NAI_STEPS / NAI_SCALE 로 덮어쓸 수 있다.
MODEL = "nai-diffusion-4-5-full"
WIDTH, HEIGHT = 1792, 1024
STEPS, SCALE = 28, 5.5

# `### CUT-01` 제목 아래 첫 코드 블록 = 프롬프트, 두 번째 = UC. 시트 3절 서두의 계약이다.
CUT_RE = re.compile(r"^#{3,4}\s*(CUT-\d+)", re.M)
HEADING_RE = re.compile(r"^#{1,6}\s", re.M)
BLOCK_RE = re.compile(r"^```(?:text)?[ \t]*\n(.*?)^```", re.M | re.S)


def parse_sheet(path):
    """시트에서 {컷 이름: (프롬프트, UC)}를 뽑는다. 순서 보존."""
    if not path.exists():
        sys.exit(f"시트를 찾지 못했습니다: {path}")

    text = path.read_text(encoding="utf-8")
    cuts, marks = {}, list(CUT_RE.finditer(text))

    for i, m in enumerate(marks):
        # 다음 제목까지가 이 컷의 구역이다. 이걸 안 자르면 4·5절의 코드 블록까지 빨려 들어온다.
        limit = len(text) if i + 1 >= len(marks) else marks[i + 1].start()
        nxt = HEADING_RE.search(text, m.end(), limit)
        blocks = BLOCK_RE.findall(text[m.end():nxt.start() if nxt else limit])

        if len(blocks) < 2:
            sys.exit(f"{m.group(1)}: 코드 블록이 {len(blocks)}개입니다. 프롬프트+UC 2개가 필요합니다.")

        cuts[m.group(1)] = (" ".join(blocks[0].split()), " ".join(blocks[1].split()))

    if not cuts:
        sys.exit(f"{path.name}에서 컷을 하나도 찾지 못했습니다. '### CUT-01' 제목 형식을 확인하십시오.")
    return cuts


def load_env():
    """.env.local 을 읽어 os.environ 에 없는 키만 채운다. 실제 환경변수가 우선한다."""
    if not ENV_FILE.exists():
        return
    for line in ENV_FILE.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        key, sep, value = line.partition("=")
        if sep:
            os.environ.setdefault(key.strip(), value.strip().strip("\"'"))


def apply_overrides():
    global MODEL, WIDTH, HEIGHT, STEPS, SCALE
    MODEL = os.environ.get("NAI_MODEL", MODEL)
    WIDTH = int(os.environ.get("NAI_WIDTH", WIDTH))
    HEIGHT = int(os.environ.get("NAI_HEIGHT", HEIGHT))
    STEPS = int(os.environ.get("NAI_STEPS", STEPS))
    SCALE = float(os.environ.get("NAI_SCALE", SCALE))
    if WIDTH % 64 or HEIGHT % 64:
        sys.exit(f"해상도는 64의 배수여야 합니다: {WIDTH}x{HEIGHT}")


def load_token():
    token = os.environ.get("NOVELAI_TOKEN", "").strip()
    if not token:
        sys.exit(f"NOVELAI_TOKEN이 비어 있습니다. {ENV_FILE.name}에 토큰을 넣으십시오.")
    return token


def encode_vibe(token, image_path, info=1.0):
    """레퍼런스 PNG를 V4 Vibe 인코딩(바이트)으로 바꾼다. 이미지당 2 Anlas — 같은 파일은 캐시에서 재사용한다."""
    cache = VIBE_CACHE / f"{image_path.stem}_{MODEL}_{info}.bin"
    if cache.exists():
        return cache.read_bytes()
    body = {"image": base64.b64encode(image_path.read_bytes()).decode(),
            "information_extracted": info, "model": MODEL}
    data = _post(token, ENCODE_URL, body, accept="application/octet-stream")
    VIBE_CACHE.mkdir(parents=True, exist_ok=True)
    cache.write_bytes(data)
    return data


def build_payload(prompt, uc, seed, vibes=None):
    """V4 계열은 input/negative_prompt만으로는 부족하고 v4_prompt 구조를 같이 보내야 한다."""
    payload = {
        "input": prompt,
        "model": MODEL,
        "action": "generate",
        "parameters": {
            "params_version": 3,
            "width": WIDTH,
            "height": HEIGHT,
            "scale": SCALE,
            "sampler": "k_euler_ancestral",
            "steps": STEPS,
            "n_samples": 1,
            "seed": seed,
            "ucPreset": 0,
            "qualityToggle": False,   # 품질 태그는 시트가 직접 들고 있다. 켜면 이중으로 붙는다
            "sm": False,
            "sm_dyn": False,
            "dynamic_thresholding": False,
            "controlnet_strength": 1,
            "legacy": False,
            "add_original_image": True,
            "cfg_rescale": 0,
            "noise_schedule": "karras",
            "legacy_v3_extend": False,
            "skip_cfg_above_sigma": None,
            "use_coords": False,
            "characterPrompts": [],
            "v4_prompt": {
                "caption": {"base_caption": prompt, "char_captions": []},
                "use_coords": False,
                "use_order": True,
            },
            "v4_negative_prompt": {
                "caption": {"base_caption": uc, "char_captions": []},
                "legacy_uc": False,
            },
            "negative_prompt": uc,
            "prefer_brownian": True,
        },
    }
    if vibes:
        # 화풍·색감을 옮긴다. 인물 동일성은 시드 고정이 담당한다(시트 4절).
        payload["parameters"].update({
            "reference_image_multiple": [base64.b64encode(v).decode() for v, _ in vibes],
            "reference_information_extracted_multiple": [1.0 for _ in vibes],
            "reference_strength_multiple": [s for _, s in vibes],
            "normalize_reference_strength_multiple": True,
        })
    return payload


def _post(token, url, body, accept):
    req = urllib.request.Request(
        url, data=json.dumps(body).encode("utf-8"), method="POST",
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json",
                 "Accept": accept, "User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=300) as res:
        return res.read()


def generate(token, prompt, uc, seed, vibes=None):
    """PNG 바이트를 돌려준다. 응답은 PNG 하나가 든 ZIP이다."""
    data = _post(token, API_URL, build_payload(prompt, uc, seed, vibes), accept="application/x-zip-compressed")
    archive = zipfile.ZipFile(io.BytesIO(data))
    return archive.read(archive.namelist()[0])


def main():
    # 윈도우 콘솔 기본 코덱(cp949)이 em dash·체크표시에서 터진다. 한글 출력이 많아 UTF-8로 고정한다.
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")

    ap = argparse.ArgumentParser(description="프롤로그 컷씬 CG 생성 (NovelAI)")
    ap.add_argument("cuts", nargs="*", help="생성할 컷 (예: CUT-01 CUT-03). 생략하면 전체")
    ap.add_argument("-n", "--count", type=int, default=3, help="컷당 후보 수 (기본 3, 시트 2.1절)")
    ap.add_argument("--seed", type=int, help="시드 고정. 일관성 확보용 — 시트 4절 2단계")
    ap.add_argument("--out", type=Path, default=DEFAULT_OUT, help=f"출력 폴더 (기본 {DEFAULT_OUT.name})")
    ap.add_argument("--vibe", action="append", default=[], metavar="PNG[:STRENGTH]",
                    help="화풍 레퍼런스 이미지 (Vibe Transfer). 강도 기본 0.6, 반복 가능. 시트 4절 3단계")
    ap.add_argument("--dry-run", action="store_true", help="파싱만 검증하고 API를 부르지 않는다")
    args = ap.parse_args()

    load_env()
    apply_overrides()
    cuts = parse_sheet(SHEET)
    unknown = [c for c in args.cuts if c not in cuts]
    if unknown:
        sys.exit(f"시트에 없는 컷: {', '.join(unknown)} (있는 것: {', '.join(cuts)})")
    targets = args.cuts or list(cuts)

    if args.dry_run:
        print(f"시트: {SHEET.name} — 컷 {len(cuts)}개 파싱 성공\n")
        for name in targets:
            prompt, uc = cuts[name]
            print(f"[{name}] 프롬프트 {len(prompt.split(','))}태그 / UC {len(uc.split(','))}태그")
            print(f"  앞머리: {prompt[:90]}...")
        print(f"\n총 {len(targets)}컷 × {args.count}장 = {len(targets) * args.count}회 생성 예정. "
              f"({MODEL}, {WIDTH}x{HEIGHT}, {STEPS}스텝, scale {SCALE})")
        return

    token = load_token()
    args.out.mkdir(parents=True, exist_ok=True)

    vibes = []
    for spec in args.vibe:
        path, _, strength = spec.rpartition(":") if ":" in spec[2:] else (spec, "", "")
        path = Path(path)
        if not path.exists():
            sys.exit(f"Vibe 레퍼런스를 찾지 못했습니다: {path}")
        vibes.append((encode_vibe(token, path), float(strength or 0.6)))
        print(f"  vibe: {path.name} (강도 {vibes[-1][1]})")

    for name in targets:
        prompt, uc = cuts[name]
        for i in range(args.count):
            # 시드를 고정하면 후보끼리 같은 그림이 나오므로, 고정 시 기준 시드에서 1씩 민다.
            seed = (args.seed + i) if args.seed is not None else random.randint(0, 2**32 - 1)
            dest = args.out / f"{name}_s{seed}.png"
            try:
                dest.write_bytes(generate(token, prompt, uc, seed, vibes))
                print(f"  ✓ {dest.relative_to(ROOT)}")
            except urllib.error.HTTPError as e:
                body = e.read().decode("utf-8", "replace")[:300]
                hint = {
                    401: "토큰이 틀렸거나 만료됐습니다.",
                    402: "Anlas가 부족합니다. 잔액을 확인하십시오.",
                    429: "요청이 너무 잦습니다. 잠시 후 다시 시도하십시오.",
                }.get(e.code, "")
                print(f"  ✗ {name} seed={seed} — HTTP {e.code} {hint}\n    {body}", file=sys.stderr)
                if e.code in (401, 402):
                    sys.exit("  중단. 남은 컷은 충전 후 같은 --seed/--vibe 로 다시 돌리십시오.")
            except urllib.error.URLError as e:
                sys.exit(f"  ✗ 접속 실패: {e.reason}")

    print(f"\n완료. 후보를 골라 EVT_MAIN_001_Cut0X.png로 이름을 바꿔 "
          f"Assets/Resources/DatingSim/Events/EVT_MAIN_001/ 에 넣으십시오 (시트 6절).")


if __name__ == "__main__":
    main()
