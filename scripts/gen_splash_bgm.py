"""
gen_splash_bgm.py — TERRA PoC 빅뱅 스플래시 BGM 트랙 절차적 생성기.

- stdlib 만 사용 (wave, struct, math, random, argparse) — numpy 없음.
- 출력:
    - v1: Assets/Audio/splash_bgm_v1.wav (44.1kHz mono 16-bit PCM, 약 8초)
    - v2: Assets/Audio/splash_bgm_v2.wav (44.1kHz mono 16-bit PCM, 약 10초)
- 시퀀스 타이밍은 docs/splash_v2_bigbang.md (v1) 와
  docs/superpowers/specs/2026-05-21-splash-v4-design.md §4 (v2) 에 동기.

v1 구간 구조 (총 8000ms):
- 0–1500ms     : 무 → 작은 점. 110Hz + 220Hz 드론 (페이드인 0.0 → 0.15)
- 1500–1700ms  : 임계 광원. 220Hz → 880Hz 글리산도 sweep
- 1700–2200ms  : 폭발. 화이트 노이즈 burst + 55Hz boom
- 2200–6500ms  : 응집 → 원시 행성. 110/165/220Hz 화음 패드 (LFO, detune)
- 6500–8000ms  : 페이드아웃 (0.1 → 0.0)

v2 구간 구조 (총 10000ms):
- 0–4000ms     : 드론 페이드인
- 4000–4500ms  : sweep
- 4500–5500ms  : 폭발 (peak)
- 5500–9000ms  : 화음 패드
- 9000–10000ms : fade

라이선스: CC0 1.0 Universal (생성 산출물 self-generated, TERRA PoC 팀).
"""

from __future__ import annotations

import argparse
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

_REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 버전별 타이밍 테이블. 각 (label, start_ms, end_ms) 튜플은 절대 시간 (ms).
TIMINGS = {
    "v1": {
        "total_ms": 8000,
        "sections": [
            ("drone_in", 0, 1500),
            ("sweep", 1500, 1700),
            ("explosion", 1700, 2200),
            ("pad", 2200, 6500),
            ("fade", 6500, 8000),
        ],
        "output": os.path.join(_REPO_ROOT, "Assets", "Audio", "splash_bgm_v1.wav"),
    },
    "v2": {
        "total_ms": 10000,
        "sections": [
            ("drone_in", 0, 4000),
            ("sweep", 4000, 4500),
            ("explosion", 4500, 5500),
            ("pad", 5500, 9000),
            ("fade", 9000, 10000),
        ],
        "output": os.path.join(_REPO_ROOT, "Assets", "Audio", "splash_bgm_v2.wav"),
    },
}

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
#
# 각 함수는 (start_ms, end_ms) 를 받아 해당 구간 길이만큼의 샘플을 반환한다.
# 내부 sub-timing (attack/decay 등) 은 구간 길이에 비례하지 않고,
# 음향적으로 의미 있는 절대 시간 (또는 비율) 로 잡는다.
# ---------------------------------------------------------------------------


def section_void_drone(start_ms: int, end_ms: int) -> List[float]:
    """낮은 드론, volume 0.0 → 0.15 페이드인. 구간 전체에 걸쳐 페이드."""
    duration = (end_ms - start_ms) / 1000.0
    drone_low = sine(110.0, duration)
    drone_mid = scale(sine(220.0, duration), 0.5)
    mixed = [a + b for a, b in zip(drone_low, drone_mid)]
    # 페이드인 (구간 전체).
    faded = fade(mixed, 0.0, 0.15)
    return faded


def section_singularity_sweep(start_ms: int, end_ms: int) -> List[float]:
    """220Hz → 880Hz 글리산도. 임계 광원 압축감."""
    duration = (end_ms - start_ms) / 1000.0
    sweep = sine_sweep(220.0, 880.0, duration)
    # Attack/decay 는 구간 길이의 10% / 25% 로 비례 (짧은 구간 (0.2s) 에서는
    # 기존 v1 값 (0.02s / 0.05s) 과 동일, 긴 구간 (0.5s) 에서는 비례 확대).
    attack_s = duration * 0.10
    decay_s = duration * 0.25
    enveloped = envelope(sweep, attack=attack_s, decay=decay_s)
    return scale(enveloped, 0.25)


def section_explosion(start_ms: int, end_ms: int) -> List[float]:
    """화이트 노이즈 burst (감쇠) + 저주파 boom. 폭발 peak."""
    duration = (end_ms - start_ms) / 1000.0

    # 화이트 노이즈 burst: 빠른 attack (1%), 긴 decay (구간의 90%).
    attack_s = min(0.005, duration * 0.01)
    decay_s = duration * 0.90
    n = noise(duration)
    n_env = envelope(n, attack=attack_s, decay=decay_s)
    n_scaled = scale(n_env, 0.4)

    # 저주파 boom (55Hz). 구간의 80% 길이.
    boom_duration = duration * 0.80
    boom = sine(55.0, boom_duration)
    boom_attack = min(0.01, boom_duration * 0.025)
    boom_decay = boom_duration * 0.875
    boom_env = envelope(boom, attack=boom_attack, decay=boom_decay)
    boom_scaled = scale(boom_env, 0.6)
    # boom 을 duration 길이로 zero-pad.
    pad_len = int(duration * SAMPLE_RATE) - len(boom_scaled)
    if pad_len > 0:
        boom_padded = boom_scaled + [0.0] * pad_len
    else:
        boom_padded = boom_scaled[: int(duration * SAMPLE_RATE)]

    return [a + b for a, b in zip(n_scaled, boom_padded)]


def section_ambient_pad(start_ms: int, end_ms: int) -> List[float]:
    """화음 패드 (110/165/220Hz, slight detune, LFO)."""
    duration = (end_ms - start_ms) / 1000.0

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

    # 전체 게인 0.1, 시작 부분에 짧은 페이드인 (응집 시작감).
    scaled = scale(modulated, 0.1 / 3.0)  # /3 화음 합산 대응.
    # 첫 0.3초 페이드인 (구간 길이가 0.3초 미만이면 비율 조정).
    n = len(scaled)
    fade_in_s = min(0.3, duration * 0.1)
    fade_in_n = int(fade_in_s * SAMPLE_RATE)
    for i in range(min(fade_in_n, n)):
        scaled[i] *= i / fade_in_n
    return scaled


def section_fade_tail(start_ms: int, end_ms: int) -> List[float]:
    """페이드아웃 잔향 (110Hz + 220Hz). 1.0 → 0.0 페이드."""
    duration = (end_ms - start_ms) / 1000.0
    tail = sine(110.0, duration)
    tail_detune = sine(220.0, duration)
    tail_mixed = [(a + b * 0.5) for a, b in zip(tail, tail_detune)]
    tail_scaled = scale(tail_mixed, 0.06)
    tail_faded = fade(tail_scaled, 1.0, 0.0)
    return tail_faded


# 섹션 label → 합성 함수 매핑.
SECTION_FUNCS = {
    "drone_in": section_void_drone,
    "sweep": section_singularity_sweep,
    "explosion": section_explosion,
    "pad": section_ambient_pad,
    "fade": section_fade_tail,
}


# ---------------------------------------------------------------------------
# 메인
# ---------------------------------------------------------------------------


def render(version: str) -> List[float]:
    """버전별 타이밍에 따라 전체 트랙을 합성."""
    cfg = TIMINGS[version]
    total_samples = int((cfg["total_ms"] / 1000.0) * SAMPLE_RATE)
    track = [0.0] * total_samples

    for label, start_ms, end_ms in cfg["sections"]:
        func = SECTION_FUNCS[label]
        section = func(start_ms, end_ms)
        start_sample = int((start_ms / 1000.0) * SAMPLE_RATE)
        add_at(track, section, start_sample=start_sample)

    # 마지막 안전장치: 전체 트랙에서 -1..1 범위 보장.
    return mix(track)


def main() -> None:
    parser = argparse.ArgumentParser(description="TERRA PoC splash BGM 절차적 생성기.")
    parser.add_argument(
        "--version",
        choices=["v1", "v2"],
        default="v1",
        help="생성할 BGM 버전 (default: v1).",
    )
    args = parser.parse_args()

    cfg = TIMINGS[args.version]
    output_path = cfg["output"]

    track = render(args.version)

    # 출력 디렉토리 생성 + 파일 쓰기.
    write_wav(output_path, track, SAMPLE_RATE)

    # 결과 보고.
    file_size = os.path.getsize(output_path)
    print(f"Wrote: {output_path}")
    print(f"  Version     : {args.version}")
    print(f"  Sample rate : {SAMPLE_RATE} Hz")
    print(f"  Duration    : {len(track) / SAMPLE_RATE:.3f} s")
    print(f"  Samples     : {len(track)}")
    print(f"  File size   : {file_size} bytes ({file_size / 1024:.1f} KB)")


if __name__ == "__main__":
    main()
