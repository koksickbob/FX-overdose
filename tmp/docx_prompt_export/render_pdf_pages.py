from pathlib import Path

import fitz


ROOT = Path(__file__).resolve().parents[2]
PDF = ROOT / "tmp" / "docx_prompt_export" / "rendered" / "prompt_summary.pdf"
OUTPUT = ROOT / "tmp" / "docx_prompt_export" / "rendered" / "pages"


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for existing in OUTPUT.glob("page-*.png"):
        existing.unlink()
    document = fitz.open(PDF)
    matrix = fitz.Matrix(1.75, 1.75)
    for index, page in enumerate(document):
        pixmap = page.get_pixmap(matrix=matrix, alpha=False)
        pixmap.save(OUTPUT / f"page-{index + 1:03d}.png")
    print(f"pages={document.page_count}")


if __name__ == "__main__":
    main()
