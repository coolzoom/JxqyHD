#!/usr/bin/env python3
"""Generate minimal MonoGame OpenGL .mgfxo effect files for the Jxqy shaders.

Replaces the XNA .contentproj/Windows-D3D effect build. Emits raw MGFX v10
(profile=0) blobs consumed directly by `new Effect(device, bytes)`; the engine
loads them from Content/effect/*.mgfxo instead of .xnb.

Format reference: MonoGame.Framework Effect.ReadEffect / Shader(GraphicsDevice,
BinaryReader) (v3.8.4).
"""
import struct
import sys
import zlib
from pathlib import Path


def wstr(s: str) -> bytes:
    """7-bit-encoded-length prefixed UTF-8 string (BinaryReader.ReadString)."""
    b = s.encode("utf-8")
    n = len(b)
    out = bytearray()
    while True:
        x = n & 0x7F
        n >>= 7
        if n:
            out.append(x | 0x80)
        else:
            out.append(x)
            break
    return bytes(out) + b


# EffectParameterClass / EffectParameterType
CLS_SCALAR, CLS_VECTOR, CLS_MATRIX, CLS_OBJECT = 0, 1, 2, 3
T_BOOL, T_INT32, T_SINGLE, T_TEX2D = 1, 2, 3, 7
USAGE_POS, USAGE_COLOR, USAGE_TEX = 0, 1, 2


class Param:
    """name, class, type, rows, cols, default floats (row-major)"""
    def __init__(self, name, cls, typ, rows=1, cols=1, data=None):
        self.name, self.cls, self.typ, self.rows, self.cols = name, cls, typ, rows, cols
        self.data = data or []

    def scalar(name, v=0.0):
        return Param(name, CLS_SCALAR, T_SINGLE, 1, 1, [float(v)])

    def vec2(name, x=0.0, y=0.0):
        return Param(name, CLS_VECTOR, T_SINGLE, 1, 2, [x, y])

    def vec4(name, x=0.0, y=0.0, z=0.0, w=0.0):
        return Param(name, CLS_VECTOR, T_SINGLE, 1, 4, [x, y, z, w])

    def mat4(name):
        # identity
        return Param(name, CLS_MATRIX, T_SINGLE, 4, 4,
                     [1.0 if i % 5 == 0 else 0.0 for i in range(16)])

    def tex(name):
        return Param(name, CLS_OBJECT, T_TEX2D, 0, 0, [])


GLSL_HEAD = "#ifdef GL_ES\nprecision highp float;\nprecision mediump int;\n#endif\n\n"

VS_SRC = GLSL_HEAD + """
uniform vec4 vs_cb[4];
attribute vec3 a_pos;
attribute vec4 a_col;
attribute vec2 a_tex;
varying vec4 v_col;
varying vec4 v_tex;

void main()
{
    mat4 m = mat4(vs_cb[0], vs_cb[1], vs_cb[2], vs_cb[3]);
    gl_Position = m * vec4(a_pos, 1.0);
    v_col = a_col;
    v_tex = vec4(a_tex, 0.0, 1.0);
}
"""

PS = {
    "refraction": GLSL_HEAD + """
uniform vec4 ps_cb[1];
uniform sampler2D TextureSampler;
uniform sampler2D DisplacementSampler;
varying vec4 v_col;
varying vec4 v_tex;

void main()
{
    vec2 disp = texture2D(DisplacementSampler, ps_cb[0].xy + v_tex.xy / 3.0).xy;
    vec2 uv = v_tex.xy + disp * 0.015;
    gl_FragColor = texture2D(TextureSampler, uv) * v_col;
}
""",
    "grayscale": GLSL_HEAD + """
uniform sampler2D coloredTextureSampler;
varying vec4 v_tex;

void main()
{
    vec4 color = texture2D(coloredTextureSampler, v_tex.xy);
    float g = dot(color.rgb, vec3(0.3, 0.59, 0.11));
    gl_FragColor = vec4(g, g, g, color.a);
}
""",
    "outedge": GLSL_HEAD + """
uniform vec4 ps_cb[2];
uniform sampler2D coloredTextureSampler;
varying vec4 v_tex;

void main()
{
    vec4 color = texture2D(coloredTextureSampler, v_tex.xy);
    float th = ps_cb[1].x;
    float rx = ps_cb[1].y;
    float ry = ps_cb[1].z;
    if (color.a < th) {
        if (texture2D(coloredTextureSampler, v_tex.xy - vec2(rx, ry)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(-rx, ry)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(rx, -ry)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(-rx, -ry)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(0.0, ry)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(rx, 0.0)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(-rx, 0.0)).a > th ||
            texture2D(coloredTextureSampler, v_tex.xy - vec2(0.0, -ry)).a > th) {
            gl_FragColor = ps_cb[0];
            return;
        }
    }
    gl_FragColor = vec4(0.0);
}
""",
    "Transparent": GLSL_HEAD + """
uniform vec4 ps_cb[1];
uniform sampler2D colorTexture;
varying vec4 v_col;
varying vec4 v_tex;

void main()
{
    vec4 OUT = vec4(0.0);
    if (ps_cb[0].y == 1.0) {
        vec4 color = texture2D(colorTexture, v_tex.xy);
        float g = dot(color.rgb, vec3(0.3, 0.59, 0.11));
        OUT.rgb = vec3(g);
        OUT.a = color.a;
    } else {
        OUT = texture2D(colorTexture, v_tex.xy);
    }
    gl_FragColor = OUT * v_col * ps_cb[0].x;
}
""",
    "AlphaTest": GLSL_HEAD + """
uniform vec4 ps_cb[1];
uniform sampler2D colorTexture;
varying vec4 v_tex;

void main()
{
    vec4 color = texture2D(colorTexture, v_tex.xy);
    if (color.a <= ps_cb[0].x)
        discard;
    gl_FragColor = color;
}
""",
}


def pack_param(p: Param) -> bytes:
    b = bytearray()
    b += struct.pack("<BB", p.cls, p.typ)
    b += wstr(p.name)
    b += wstr("")                 # semantic
    b += struct.pack("<i", 0)     # annotations
    b += struct.pack("<BB", p.rows, p.cols)
    b += struct.pack("<i", 0)     # elements
    b += struct.pack("<i", 0)     # struct members
    if p.typ in (T_SINGLE, T_INT32, T_BOOL) and p.data:
        b += struct.pack("<%df" % (p.rows * p.cols), *p.data)
    return b


def pack_cbuffer(name: str, size: int, entries) -> bytes:
    """entries: list of (param_index, byte_offset)"""
    b = bytearray()
    b += wstr(name)
    b += struct.pack("<h", size)
    b += struct.pack("<i", len(entries))
    for idx, off in entries:
        b += struct.pack("<iH", idx, off)
    return b


def pack_shader(is_vs: bool, code: str, samplers, cbuffers, attribs) -> bytes:
    """samplers: list of (uniform_name, texSlot, samplerSlot, param_index)
       attribs: list of (name, usage, index)"""
    b = bytearray()
    b += struct.pack("<B", 1 if is_vs else 0)
    code_b = code.encode("ascii")
    b += struct.pack("<i", len(code_b))
    b += code_b
    b += struct.pack("<B", len(samplers))
    for name, tex_slot, smp_slot, pidx in samplers:
        b += struct.pack("<BBB", 0, tex_slot, smp_slot)  # Sampler2D
        b += struct.pack("<B", 0)                        # hasState = false
        b += wstr(name)
        b += struct.pack("<B", pidx)
    b += struct.pack("<B", len(cbuffers))
    for c in cbuffers:
        b += struct.pack("<B", c)
    b += struct.pack("<B", len(attribs))
    for name, usage, idx in attribs:
        b += wstr(name)
        b += struct.pack("<BBh", usage, idx, 0)
    return b


def pack_effect(params, cbuffers, shaders, technique_name="Technique1", pass_name="Pass1"):
    b = bytearray()
    b += struct.pack("<i", len(cbuffers))
    for cb in cbuffers:
        b += cb
    b += struct.pack("<i", len(shaders))
    for s in shaders:
        b += s
    b += struct.pack("<i", len(params))
    for p in params:
        b += pack_param(p)
    # techniques
    b += struct.pack("<i", 1)
    b += wstr(technique_name)
    b += struct.pack("<i", 0)   # annotations
    b += struct.pack("<i", 1)   # pass count
    b += wstr(pass_name)
    b += struct.pack("<i", 0)   # annotations
    b += struct.pack("<i", 0)   # vertex shader index
    b += struct.pack("<i", 1)   # pixel shader index
    b += struct.pack("<B", 0)   # blend state absent
    b += struct.pack("<B", 0)   # depth state absent
    b += struct.pack("<B", 0)   # rasterizer absent
    return b


def build(name: str, params, ps_cbsize: int, ps_entries, ps_samplers):
    """Assemble full .mgfxo bytes. params = ordered Param list; shader 0=VS, 1=PS."""
    mat_idx = next(i for i, p in enumerate(params) if p.name == "MatrixTransform")
    cbuffers = [pack_cbuffer("vs_cb", 64, [(mat_idx, 0)])]
    ps_cb_idx = []
    if ps_cbsize:
        cbuffers.append(pack_cbuffer("ps_cb", ps_cbsize, ps_entries))
        ps_cb_idx = [1]
    vs = pack_shader(True, VS_SRC, [], [0],
                     [("a_pos", USAGE_POS, 0), ("a_col", USAGE_COLOR, 0),
                      ("a_tex", USAGE_TEX, 0)])
    ps = pack_shader(False, PS[name], ps_samplers, ps_cb_idx, [])
    body = pack_effect(params, cbuffers, [vs, ps])
    # effectKey must be unique per effect: MonoGame clones cached effects by key.
    key = zlib.crc32(name.encode("utf-8")) & 0x7FFFFFFF
    return b"MGFX" + struct.pack("<BBi", 10, 0, key) + body + b"MGFX"


def main(outdir):
    outdir = Path(outdir)
    outdir.mkdir(parents=True, exist_ok=True)

    effs = {
        # name -> (params, ps_cb_size, ps_cb_entries, ps_samplers)
        "refraction": (
            [Param.vec2("DisplacementScroll"),
             Param.tex("TextureSampler"),
             Param.tex("DisplacementSampler"),
             Param.mat4("MatrixTransform")],
            16, [(0, 0)],
            [("TextureSampler", 0, 0, 1), ("DisplacementSampler", 1, 1, 2)],
        ),
        "grayscale": (
            [Param.tex("coloredTexture"),
             Param.mat4("MatrixTransform")],
            0, [],
            [("coloredTextureSampler", 0, 0, 0)],
        ),
        "outedge": (
            [Param.vec4("EdgeColor", 0.6, 0.0, 0.0, 0.6),
             Param.scalar("AplhaThreshold", 0.784),
             Param.scalar("radiusx", 0.0),
             Param.scalar("radiusy", 0.0),
             Param.tex("coloredTexture"),
             Param.mat4("MatrixTransform")],
            32, [(0, 0), (1, 16), (2, 20), (3, 24)],
            [("coloredTextureSampler", 0, 0, 4)],
        ),
        "Transparent": (
            [Param.scalar("alpha", 0.0),
             Param.scalar("useGrayScale", 0.0),
             Param.tex("colorTexture"),
             Param.mat4("MatrixTransform")],
            16, [(0, 0), (1, 4)],
            [("colorTexture", 0, 0, 2)],
        ),
        "AlphaTest": (
            [Param.scalar("MinAlpha", 0.0),
             Param.tex("colorTexture"),
             Param.mat4("MatrixTransform")],
            16, [(0, 0)],
            [("colorTexture", 0, 0, 1)],
        ),
    }

    for name, (params, cbsize, entries, samplers) in effs.items():
        data = build(name, params, cbsize, entries, samplers)
        out = outdir / (name + ".mgfxo")
        out.write_bytes(data)
        print(f"{out} {len(data)} bytes")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Data/Content/effect")
