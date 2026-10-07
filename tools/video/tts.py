# Озвучка сцен відеоогляду голосом Microsoft uk-UA-OstapNeural (edge-tts) → artifacts/video/audio/<id>.mp3 + durations.json
import asyncio
import io
import json
import os
import subprocess

import edge_tts

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, 'artifacts', 'video', 'audio')
VOICE = os.environ.get('LIS_VOICE', 'uk-UA-OstapNeural')


def duration(path):
    r = subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'default=nw=1:nk=1', path], capture_output=True, text=True)
    return float(r.stdout.strip())


async def main():
    os.makedirs(OUT, exist_ok=True)
    scenes = json.load(io.open(os.path.join(os.path.dirname(__file__), 'scenes.json'), encoding='utf-8'))
    durations = {}
    for s in scenes:
        path = os.path.join(OUT, s['id'] + '.mp3')
        await edge_tts.Communicate(s['text'], VOICE, rate='+4%').save(path)
        durations[s['id']] = round(duration(path), 2)
        print(s['id'], durations[s['id']], 's')
    json.dump(durations, io.open(os.path.join(OUT, 'durations.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)


asyncio.run(main())
