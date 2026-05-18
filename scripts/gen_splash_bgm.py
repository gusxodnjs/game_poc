"""
gen_splash_bgm.py — TERRA PoC 빅뱅 스플래시 BGM 트랙 절차적 생성기.

- stdlib 만 사용 (wave, struct, math, random) — numpy 없음.
- 출력: Assets/Audio/splash_bgm_v1.wav (44.1kHz mono 16-bit PCM, 약 8초)
- 시퀀스 타이밍은 docs/splash_v2_bigbang.md 의 빅뱅 시퀀스에 동기.

구간 구조 (총 8000ms):
- 0–1500ms     : 무 → 작은 점. 110Hz + 220Hz 드론 (페이드인 0.0 → 0.15)
- 1500–1700ms  : 임계 광원. 220Hz → 880Hz 글리산도 sweep
- 1700–2200ms  : 폭발. 화이트 노이즈 burst + 55Hz boom
- 2200–6500ms  : 응집 → 원시 행성. 110/165/220Hz 화음 패드 (LFO, detune)
- 6500–8000ms  : 페이드아웃 (0.1 → 0.0)

라이선스: CC0 1.0 Universal (생성 산출물 self-generated, TERRA PoC 팀).
"""

from __future__ import annotations

import math
import os
import random
import struct
import wave
from typing import List

# ---------------------------------------------------------------------------
# 상수
# ---------------------------------------------------------------------------

SAMPLE_RATE = 44100
OUTPUT_PATH = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets",
    "Audio",
    "splash_bgm_v1.wav",
)

# 총 길이 (초). 시퀀스 8초.
TOTAL_DURATION = 8.0

# 재현 가능한 노이즈를 위해 시드 고정.
random.seed(20260518)


# ---------------------------------------------------------------------------
# Helper 함수
# ---------------------------------------------------------------------------


def sine(freq: float, duration: float, sample_rate: int = SAMPLE_RATE) -> List[float]:
    """단일 주파수의 사인파 샘플을 생성."""
    n_samples = int(duration * sample_rate)
    two_pi_f_over_sr = 2.0 * math.pi * freq / sample_rate
    return [math.sin(two_pi_f_over_sr * i) for i in range(n_samples)]


def sine_sweep(
    start_freq: float,
    end_freq: float,
    duration: float,
    sample_rate: int = SAMPLE_RATE,
) -> List[float]:
    """주파수가 선형 보간되는 사인 sweep (글리산도)."""
    n_samples = int(duration * sample_rate)
    samples: List[float] = []
    phase = 0.0
    for i in range(n_samples):
        t = i / n_samples if n_samples > 0 else 0.0
        freq = start_freq + (end_freq - start_freq) * t
        phase += 2.0 * math.pi * freq / sample_rate
        samples.append(math.sin(phase))
    return samples


def noise(duration: float, sample_rate: int = SAMPLE_RATE) -> List[float]:
    """[-1, 1] 균등 분포 화이트 노이즈."""
    n_samples = int(duration * sample_rate)
    return [random.uniform(-1.0, 1.0) for _ in range(n_samples)]


def envelope(
    samples: List[float],
    attack: float,
    decay: float,
    sample_rate: int = SAMPLE_RATE,
) -> List[float]:
    """선형 attack/decay envelope 적용."""
    n = len(samples)
    if n == 0:
        return samples
    attack_n = int(attack * sample_rate)
    decay_n = int(decay * sample_rate)
    out = [0.0] * n
    for i in range(n):
        if i < attack_n and attack_n > 0:
            gain = i / attack_n
        elif i >= n - decay_n and decay_n > 0:
            gain = max(0.0, (n - i) / decay_n)
        else:
            gain = 1.0
        out[i] = samples[i] * gain
    return out


def fade(samples: List[float], start_gain: float, end_gain: float) -> List[float]:
    """전체 구간 선형 페이드 (start_gain → end_gain)."""
    n = len(samples)
    if n == 0:
        return samples
    out = [0.0] * n
    for i in range(n):
        t = i / (n - 1) if n > 1 else 0.0
        gain = start_gain + (end_gain - start_gain) * t
        out[i] = samples[i] * gain
    return out


def scale(samples: List[float], gain: float) -> List[float]:
    """전체 구간 일정 게인."""
    return [s * gain for s in samples]


def lfo_modulate(
    samples: List[float],
    lfo_freq: float,
    lfo_depth: float,
    sample_rate: int = SAMPLE_RATE,
) -> List[float]:
    """LFO 로 진폭 변조 (sample * (1 + depth * sin(2π·lfo_freq·t)))."""
    n = len(samples)
    out = [0.0] * n
    two_pi_f_over_sr = 2.0 * math.pi * lfo_freq / sample_rate
    for i in range(n):
        mod = 1.0 + lfo_depth * math.sin(two_pi_f_over_sr * i)
        out[i] = samples[i] * mod
    return out


def add_at(target: List[float], src: List[float], start_sample: int) -> None:
    """target 리스트의 start_sample 인덱스 부터 src 를 in-place 합성."""
    n_target = len(target)
    n_src = len(src)
    end = min(n_target, start_sample + n_src)
    for i in range(start_sample, end):
        target[i] += src[i - start_sample]


def mix(*tracks: List[float]) -> List[float]:
    """여러 트랙을 sample-wise 합산 후 [-1, 1] 로 clip."""
    if not tracks:
        return []
    n = max(len(t) for t in tracks)
    out = [0.0] * n
    for t in tracks:
        for i, s in enumerate(t):
            out[i] += s
    # Clip
    return [max(-1.0, min(1.0, s)) for s in out]


def to_pcm16(samples: List[float]) -> bytes:
    """[-1, 1] float → 16-bit signed PCM little-endian bytes."""
    buf = bytearray()
    for s in samples:
        # Clip 한 번 더 보장.
        s_clipped = max(-1.0, min(1.0, s))
        int_val = int(s_clipped * 32767.0)
        buf += struct.pack("<h", int_val)
    return bytes(buf)


def write_wav(path: str, samples: List[float], sample_rate: int = SAMPLE_RATE) -> None:
    """모노 16-bit PCM WAV 파일 작성."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    pcm_bytes = to_pcm16(samples)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)  # 16-bit
        wf.setframerate(sample_rate)
        wf.writeframes(pcm_bytes)


# ---------------------------------------------------------------------------
# 구간별 트랙 생성 함수
# ---------------------------------------------------------------------------


def section_void_drone() -> List[float]:
    """0–1500ms: 낮은 드론, volume 0.0 → 0.15 페이드인."""
    duration = 1.5
    drone_low = sine(110.0, duration)
    drone_mid = scale(sine(220.0, duration), 0.5)
    mixed = [a + b for a, b in zip(drone_low, drone_mid)]
    # 페이드인.
    faded = fade(mixed, 0.0, 0.15)
    return faded


def section_singularity_sweep() -> List[float]:
    """1500–1700ms: 220Hz → 880Hz 글리산도 (0.2초). 임계 광원 압축감."""
    duration = 0.2
    sweep = sine_sweep(220.0, 880.0, duration)
    # 짧은 attack, 거의 즉시 decay 시작.
    enveloped = envelope(sweep, attack=0.02, decay=0.05)
    return scale(enveloped, 0.25)


def section_explosion() -> List[float]:
    """1700–2200ms: 화이트 노이즈 burst (감쇠) + 55Hz boom."""
    duration = 0.5

    # 화이트 노이즈 burst: 빠른 attack, 긴 decay.
    n = noise(duration)
    n_env = envelope(n, attack=0.005, decay=0.45)
    n_scaled = scale(n_env, 0.4)

    # 저주파 boom (55Hz, 0.4초 감쇠).
    boom_duration = 0.4
    boom = sine(55.0, boom_duration)
    boom_env = envelope(boom, attack=0.01, decay=0.35)
    boom_scaled = scale(boom_env, 0.6)
    # boom 을 duration 길이로 zero-pad.
    boom_padded = boom_scaled + [0.0] * (int(duration * SAMPLE_RATE) - len(boom_scaled))

    return [a + b for a, b in zip(n_scaled, boom_padded)]


def section_ambient_pad() -> List[float]:
    """2200–6500ms: 화음 패드 (110/165/220Hz, slight detune, LFO)."""
    duration = 4.3

    # 베이스 110Hz + 약간의 detune 사인.
    pad1 = sine(110.0, duration)
    pad1_detune = sine(112.0, duration)
    pad1_mixed = [(a + b) * 0.5 for a, b in zip(pad1, pad1_detune)]

    # 5도 165Hz + detune.
    pad2 = sine(165.0, duration)
    pad2_detune = sine(163.0, duration)
    pad2_mixed = [(a + b) * 0.5 for a, b in zip(pad2, pad2_detune)]

    # 옥타브 220Hz + detune.
    pad3 = sine(220.0, duration)
    pad3_detune = sine(222.0, duration)
    pad3_mixed = [(a + b) * 0.5 for a, b in zip(pad3, pad3_detune)]

    # 화음 합성.
    chord = [a + b + c for a, b, c in zip(pad1_mixed, pad2_mixed, pad3_mixed)]

    # LFO 로 가벼운 진폭 변조 (0.3Hz, depth 0.15).
    modulated = lfo_modulate(chord, lfo_freq=0.3, lfo_depth=0.15)

    # 전체 게인 0.1, 시작 부분에 짧은 페이드인 (응집 시작감), 끝은 자연스러운 hold.
    scaled = scale(modulated, 0.1 / 3.0)  # /3 화음 합산 대응.
    # 첫 0.3초 페이드인.
    n = len(scaled)
    fade_in_n = int(0.3 * SAMPLE_RATE)
    for i in range(min(fade_in_n, n)):
        scaled[i] *= i / fade_in_n
    return scaled


# ---------------------------------------------------------------------------
# 메인
# ---------------------------------------------------------------------------


def main() -> None:
    total_samples = int(TOTAL_DURATION * SAMPLE_RATE)
    track = [0.0] * total_samples

    # 0–1500ms : 드론.
    drone = section_void_drone()
    add_at(track, drone, start_sample=0)

    # 1500–1700ms : sweep.
    sweep = section_singularity_sweep()
    add_at(track, sweep, start_sample=int(1.5 * SAMPLE_RATE))

    # 1700–2200ms : 폭발.
    explosion = section_explosion()
    add_at(track, explosion, start_sample=int(1.7 * SAMPLE_RATE))

    # 2200–6500ms : 앰비언트 패드.
    pad = section_ambient_pad()
    add_at(track, pad, start_sample=int(2.2 * SAMPLE_RATE))

    # 6500–8000ms : 페이드아웃 (전체 트랙의 마지막 1.5초 게인 0.1 → 0.0).
    # 패드가 6500ms 까지 자연스럽게 흐른 뒤, 6500–8000ms 사이에 잔향만 남기고 페이드.
    # 패드는 이미 6500ms 에 끝났으므로 이 구간은 비어 있다.
    # 자연스러운 잔향을 위해 패드 꼬리(110Hz 잔향) 를 1.5초 동안 페이드아웃.
    tail_duration = 1.5
    tail = sine(110.0, tail_duration)
    tail_detune = sine(220.0, tail_duration)
    tail_mixed = [(a + b * 0.5) for a, b in zip(tail, tail_detune)]
    tail_scaled = scale(tail_mixed, 0.06)
    tail_faded = fade(tail_scaled, 1.0, 0.0)
    add_at(track, tail_faded, start_sample=int(6.5 * SAMPLE_RATE))

    # 마지막 안전장치: 전체 트랙에서 -1..1 범위 보장. mix() 가 clip 수행.
    track_clipped = mix(track)

    # 출력 디렉토리 생성 + 파일 쓰기.
    write_wav(OUTPUT_PATH, track_clipped, SAMPLE_RATE)

    # 결과 보고.
    file_size = os.path.getsize(OUTPUT_PATH)
    print(f"Wrote: {OUTPUT_PATH}")
    print(f"  Sample rate : {SAMPLE_RATE} Hz")
    print(f"  Duration    : {len(track_clipped) / SAMPLE_RATE:.3f} s")
    print(f"  Samples     : {len(track_clipped)}")
    print(f"  File size   : {file_size} bytes ({file_size / 1024:.1f} KB)")


if __name__ == "__main__":
    main()
