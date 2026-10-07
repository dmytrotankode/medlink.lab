# Зведення відео з кадрів CDP screencast (часові мітки → тривалості, concat demuxer) та озвучки сцен (зсув за timeline.json) у MP4.
import io
import json
import os
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'artifacts', 'video')
tl = json.load(io.open(os.path.join(OUT, 'timeline.json'), encoding='utf-8'))
frames, end = tl['frameList'], tl['endMs']
listing = os.path.join(OUT, 'frames.txt')
NL = '\n'


def entry(name, duration=None):
    path = os.path.join(tl['frames'], name).replace('\\', '/')
    line = f"file '{path}'{NL}"
    if duration is not None:
        line += f"duration {duration:.3f}{NL}"
    return line


with io.open(listing, 'w', encoding='utf-8') as f:
    if frames and frames[0]['t'] > 0:
        f.write(entry(frames[0]['name'], frames[0]['t'] / 1000))
    for i, fr in enumerate(frames):
        nxt = frames[i + 1]['t'] if i + 1 < len(frames) else end
        f.write(entry(fr['name'], max(0.001, (nxt - fr['t']) / 1000)))
    f.write(entry(frames[-1]['name']))

scenes = tl['scenes']
args = ['ffmpeg', '-y', '-f', 'concat', '-safe', '0', '-i', listing]
filters = ['[0:v]scale=1366:768:force_original_aspect_ratio=decrease,pad=1366:768:(ow-iw)/2:(oh-ih)/2:white,fps=25,format=yuv420p[vout]']
for i, s in enumerate(scenes, start=1):
    args += ['-i', os.path.join(OUT, 'audio', s['id'] + '.mp3')]
    delay = max(0, s['startMs'] + 400)
    filters.append(f'[{i}:a]adelay={delay}|{delay}[a{i}]')
mix = ''.join(f'[a{i}]' for i in range(1, len(scenes) + 1))
filters.append(f'{mix}amix=inputs={len(scenes)}:normalize=0:dropout_transition=0[aout]')
dst = os.path.join(OUT, 'MedLink_LIS_demo_UA.mp4')
args += ['-filter_complex', ';'.join(filters), '-map', '[vout]', '-map', '[aout]', '-c:v', 'libx264', '-preset', 'medium', '-crf', '23',
         '-c:a', 'aac', '-b:a', '160k', '-t', f'{end / 1000 + 1:.1f}', dst]
subprocess.run(args, check=True)
print('video:', dst)
