import sys
sys.path.append('.')
import yomi_dataset_generator as yomi

def count_leaves(d):
    count = 0
    if isinstance(d, list): return len(d)
    if not isinstance(d, dict): return 0
    for k, v in d.items():
        if isinstance(v, dict):
            if 'dialogues' in v: count += len(v['dialogues'])
            else: count += count_leaves(v)
        elif isinstance(v, list):
            count += len(v)
    return count

print("core_lines count:", count_leaves(yomi.core_lines))
print("director_lines count:", count_leaves(yomi.director_lines))
print("event_lines count:", count_leaves(yomi.event_lines))
