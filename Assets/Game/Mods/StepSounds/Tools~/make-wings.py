"""Synthesizes soft wing-flap sounds: a low downstroke 'whump' plus a feathery air swish.

Usage: python3 make-wings.py <dir>   writes <dir>/wing_1.wav .. wing_4.wav (mono 22050 Hz).
The mod's Sounds/wing_N.ogg were made from these with:
  ffmpeg -i wing_N.wav -af volume=-18dB -c:a libvorbis -q:a 4 -ar 22050 -ac 1 wing_N.ogg
"""
import math, random, struct, sys, wave

RATE = 22050

def one_pole_lp(x, cutoff):
    a = math.exp(-2 * math.pi * cutoff / RATE); y = 0.0; out = []
    for s in x:
        y = (1 - a) * s + a * y; out.append(y)
    return out

def one_pole_hp(x, cutoff):
    lp = one_pole_lp(x, cutoff)
    return [s - l for s, l in zip(x, lp)]

def env(t, start, attack, tau):
    t -= start
    if t < 0: return 0.0
    if t < attack: return 0.5 - 0.5 * math.cos(math.pi * t / attack)
    return math.exp(-(t - attack) / tau)

def flap(seed, length, body_cut, swish_lo, swish_hi, body_tau, swish_tau, flutter_hz):
    rnd = random.Random(seed)
    n = int(RATE * length)
    noise = [rnd.uniform(-1, 1) for _ in range(n)]
    body = one_pole_lp(one_pole_lp(noise, body_cut), body_cut)
    swish = one_pole_lp(one_pole_lp(one_pole_hp(noise, swish_lo), swish_hi), swish_hi)
    out = []
    for i in range(n):
        t = i / RATE
        flutter = 1 - 0.35 * (0.5 + 0.5 * math.sin(2 * math.pi * flutter_hz * t + seed))
        b = body[i] * env(t, 0.0, 0.03, body_tau) * 3.0
        s = swish[i] * env(t, 0.012, 0.045, swish_tau) * 0.8 * flutter
        out.append(b + s)
    fade = int(RATE * 0.03)          # no click at the end
    for i in range(fade):
        out[n - 1 - i] *= i / fade
    peak = max(abs(v) for v in out) or 1.0
    return [v / peak * 0.7 for v in out]

VARIANTS = [
    (11, 0.32, 380, 700, 2600, 0.060, 0.085, 31),
    (23, 0.30, 340, 650, 2300, 0.055, 0.080, 37),
    (37, 0.34, 420, 750, 2900, 0.065, 0.095, 28),
    (53, 0.31, 360, 680, 2500, 0.058, 0.090, 34),
]
for k, v in enumerate(VARIANTS, 1):
    data = flap(*v)
    with wave.open("%s/wing_%d.wav" % (sys.argv[1], k), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in data))
