import re

with open('Assets/Scripts/AI/AIVisualController.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
skip = False

for i, line in enumerate(lines):
    # Remove fields that belong to sprite controller
    if 'private const float CostumeOpticalScale' in line:
        pass
    if 'private const float CostumeOpticalFootCompensation' in line:
        pass
        
    # We can just write a script that explicitly keeps dialogue methods.
