set inputPath to "/Users/bluem/Documents/GitHub/FX-overdose/docs/FX_OVERDOSE_생성형AI_활용_프롬프트_정리.docx"
set outputPath to "/Users/bluem/Documents/GitHub/FX-overdose/tmp/docx_prompt_export/rendered/prompt_summary.pdf"

tell application "Microsoft Word"
    activate
    open file name inputPath read only true add to recent files false
    delay 3
    set openedDocument to document 1
    save as openedDocument file name outputPath file format format PDF add to recent files false
    close active document saving no
end tell

return outputPath
