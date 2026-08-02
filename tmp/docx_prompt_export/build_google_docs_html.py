from __future__ import annotations

import html
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "docs" / "생성형AI_활용_프롬프트_정리.md"
OUTPUT = ROOT / "tmp" / "docx_prompt_export" / "prompt_summary_google_docs.html"


def inline_markup(text: str) -> str:
    escaped = html.escape(text, quote=False)
    escaped = re.sub(r"`([^`]+)`", r"<code>\1</code>", escaped)
    escaped = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", escaped)
    return escaped


def table_block(lines: list[str], start: int) -> tuple[str, int]:
    rows: list[list[str]] = []
    i = start
    while i < len(lines) and lines[i].strip().startswith("|"):
        cells = [cell.strip() for cell in lines[i].strip().strip("|").split("|")]
        rows.append(cells)
        i += 1

    if len(rows) >= 2 and all(re.fullmatch(r":?-{3,}:?", cell) for cell in rows[1]):
        header = rows[0]
        body = rows[2:]
        out = ["<table>", "<thead><tr>"]
        out.extend(f"<th>{inline_markup(cell)}</th>" for cell in header)
        out.append("</tr></thead><tbody>")
        for row in body:
            out.append("<tr>")
            out.extend(f"<td>{inline_markup(cell)}</td>" for cell in row)
            out.append("</tr>")
        out.append("</tbody></table>")
        return "".join(out), i

    return "", start


def markdown_to_html(markdown: str) -> str:
    lines = markdown.splitlines()
    blocks: list[str] = []
    paragraph: list[str] = []
    in_code = False
    code_lines: list[str] = []
    list_type: str | None = None
    list_items: list[str] = []

    def flush_paragraph() -> None:
        nonlocal paragraph
        if paragraph:
            joined = " ".join(part.strip() for part in paragraph)
            blocks.append(f"<p>{inline_markup(joined)}</p>")
            paragraph = []

    def flush_list() -> None:
        nonlocal list_type, list_items
        if list_type and list_items:
            blocks.append(f"<{list_type}>")
            blocks.extend(f"<li>{inline_markup(item)}</li>" for item in list_items)
            blocks.append(f"</{list_type}>")
        list_type = None
        list_items = []

    i = 0
    while i < len(lines):
        raw = lines[i]
        stripped = raw.strip()

        if stripped.startswith("```"):
            flush_paragraph()
            flush_list()
            if in_code:
                blocks.append(f"<pre>{html.escape(chr(10).join(code_lines))}</pre>")
                code_lines = []
                in_code = False
            else:
                in_code = True
            i += 1
            continue

        if in_code:
            code_lines.append(raw)
            i += 1
            continue

        if not stripped:
            flush_paragraph()
            flush_list()
            i += 1
            continue

        if stripped.startswith("|"):
            flush_paragraph()
            flush_list()
            table_html, next_i = table_block(lines, i)
            if table_html:
                blocks.append(table_html)
                i = next_i
                continue

        heading = re.match(r"^(#{1,4})\s+(.+)$", stripped)
        if heading:
            flush_paragraph()
            flush_list()
            level = len(heading.group(1))
            text = inline_markup(heading.group(2))
            if level == 1:
                blocks.append(f'<p class="doc-title">{text}</p>')
                blocks.append('<p class="doc-subtitle">제출용 프롬프트 정제본 · 2026년 8월 2일</p>')
            elif level == 2:
                blocks.append(f"<h1>{text}</h1>")
            elif level == 3:
                blocks.append(f"<h2>{text}</h2>")
            else:
                blocks.append(f"<h3>{text}</h3>")
            i += 1
            continue

        ordered = re.match(r"^\d+\.\s+(.+)$", stripped)
        bullet = re.match(r"^-\s+(.+)$", stripped)
        if ordered or bullet:
            flush_paragraph()
            current_type = "ol" if ordered else "ul"
            if list_type and list_type != current_type:
                flush_list()
            list_type = current_type
            list_items.append((ordered or bullet).group(1))
            i += 1
            continue

        paragraph.append(stripped)
        i += 1

    flush_paragraph()
    flush_list()
    if in_code:
        blocks.append(f"<pre>{html.escape(chr(10).join(code_lines))}</pre>")

    body = "\n".join(blocks)
    return f"""<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="utf-8">
<title>FX OVERDOSE 생성형 AI 활용 프롬프트 정리서</title>
<style>
  @page {{ size: Letter portrait; margin: 1in; }}
  body {{
    margin: 0;
    color: #000000;
    background: #ffffff;
    font-family: Arial, "Apple SD Gothic Neo", sans-serif;
    font-size: 11pt;
    line-height: 1.15;
  }}
  .doc-title {{
    margin: 0 0 3pt 0;
    font-size: 26pt;
    line-height: 1.08;
    font-weight: 400;
    color: #000000;
    page-break-after: avoid;
  }}
  .doc-subtitle {{
    margin: 0 0 18pt 0;
    color: #555555;
    font-size: 10.5pt;
  }}
  h1 {{
    margin: 20pt 0 6pt 0;
    font-size: 20pt;
    line-height: 1.12;
    font-weight: 400;
    color: #000000;
    page-break-after: avoid;
  }}
  h2 {{
    margin: 18pt 0 6pt 0;
    font-size: 16pt;
    line-height: 1.12;
    font-weight: 400;
    color: #000000;
    page-break-after: avoid;
  }}
  h3 {{
    margin: 16pt 0 4pt 0;
    font-size: 14pt;
    line-height: 1.12;
    font-weight: 400;
    color: #434343;
    page-break-after: avoid;
  }}
  p {{ margin: 0 0 8pt 0; }}
  strong {{ font-weight: 700; }}
  code {{
    font-family: Arial, "Apple SD Gothic Neo", sans-serif;
    font-size: 10.5pt;
    color: #202124;
  }}
  pre {{
    margin: 4pt 0 10pt 0;
    padding: 9pt 11pt;
    border: 1px solid #dadce0;
    background: #f8f9fa;
    color: #202124;
    font-family: Arial, "Apple SD Gothic Neo", sans-serif;
    font-size: 10.5pt;
    line-height: 1.22;
    white-space: pre-wrap;
    page-break-inside: avoid;
  }}
  table {{
    width: 100%;
    margin: 6pt 0 14pt 0;
    border-collapse: collapse;
    table-layout: fixed;
    font-size: 10.5pt;
  }}
  th, td {{
    padding: 5pt 7pt;
    border: 1px solid #dadce0;
    text-align: left;
    vertical-align: middle;
  }}
  th {{ font-weight: 700; background: #ffffff; }}
  th:first-child, td:first-child {{ width: 24%; }}
  ol, ul {{ margin: 0 0 8pt 0.5in; padding: 0; }}
  li {{ margin: 0 0 4pt 0; padding-left: 0; }}
</style>
</head>
<body>
{body}
</body>
</html>
"""


def main() -> None:
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    markdown = SOURCE.read_text(encoding="utf-8")
    OUTPUT.write_text(markdown_to_html(markdown), encoding="utf-8")
    print(OUTPUT)


if __name__ == "__main__":
    main()
