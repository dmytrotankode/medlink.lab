# Зведення відео (webm з Playwright) і озвучки сцен (mp3, зсув за timeline.json) у MP4 (H.264 + AAC).
import io
import json
import os
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'artifacts', 'video')
tl = json.load(io.open(os.path.join(OUT, 'timeline.json'), encoding='utf-8'))
scenes = tl['scenes']
args = ['ffmpeg', '-y', '-i', tl['video']]
filters = []
for i, s in enumerate(scenes, start=1):
    args += ['-i', os.path.join(OUT, 'audio', s['id'] + '.mp3')]
    delay = max(0, s['startMs'] + 400)
    filters.append(f'[{i}:a]adelay={delay}|{delay}[a{i}]')
mix = ''.join(f'[a{i}]' for i in range(1, len(scenes) + 1))
filters.append(f'{mix}amix=inputs={len(scenes)}:normalize=0:dropout_transition=0[aout]')
dst = os.path.join(OUT, 'MedLink_LIS_demo_UA.mp4')
args += ['-filter_complex', ';'.join(filters), '-map', '0:v', '-map', '[aout]', '-c:v', 'libx264', '-preset', 'medium', '-crf', '23', '-pix_fmt', 'yuv420p',
         '-c:a', 'aac', '-b:a', '160k', '-shortest', dst]
subprocess.run(args, check=True)
print('video:', dst)
