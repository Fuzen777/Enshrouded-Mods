"""Disassembles the caves test_main.exe wrote and checks them instruction by instruction against what they must do.

Usage: py tests/check_caves.py <caves.bin>   (needs capstone)
"""
import re
import struct
import sys

from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_REG_RIP

# Keep in step with cave_page in src/native/terraform_hooks.h.
PICKAXE_FACTOR = 0x00
RAKE_FACTOR = 0x04
PICKAXE_ENABLED = 0x08
RAKE_ENABLED = 0x09
PICKAXE_VOLUME_FACTOR = 0x10
RAKE_RETURN_ADDRESS = 0x20
RAKE_CALL_TARGET = 0x28
PICKAXE_CAVE = 0x40
RAKE_CAVE = 0xC0


def disassemble(code, address):
    """Each instruction as text, with rip-relative operands written as the absolute address they reach."""
    disassembler = Cs(CS_ARCH_X86, CS_MODE_64)
    disassembler.detail = True
    lines = []
    decoded_size = 0
    for instruction in disassembler.disasm(code, address):
        text = f"{instruction.mnemonic} {instruction.op_str}".strip()
        for operand in instruction.operands:
            if operand.type == X86_OP_MEM and operand.mem.base == X86_REG_RIP:
                target = instruction.address + instruction.size + operand.mem.disp
                text = re.sub(r"\[rip [+-] 0x[0-9a-f]+\]", f"[0x{target:x}]", text)
        lines.append((instruction.address, text))
        decoded_size += instruction.size
    if decoded_size != len(code):
        raise AssertionError(f"only {decoded_size} of {len(code)} bytes at 0x{address:x} decode")
    return lines


def compare(name, actual, expected):
    failures = 0
    for index in range(max(len(actual), len(expected))):
        actual_text = actual[index][1] if index < len(actual) else "(missing)"
        expected_text = expected[index] if index < len(expected) else "(extra)"
        marker = "  " if actual_text == expected_text else "!!"
        if actual_text != expected_text:
            failures += 1
        print(f"{marker} {name} {index:2}: {actual_text}" + ("" if actual_text == expected_text else f"   expected: {expected_text}"))
    return failures


def main():
    data = open(sys.argv[1], "rb").read()
    if data[:8] != b"TTRCAVES":
        raise SystemExit("not a caves file")
    page, pickaxe_hook, rake_hook = struct.unpack_from("<QQQ", data, 8)
    pickaxe_size, rake_size = struct.unpack_from("<II", data, 32)
    offset = 40
    pickaxe_cave = data[offset:offset + pickaxe_size]
    offset += pickaxe_size
    rake_cave = data[offset:offset + rake_size]
    offset += rake_size
    pickaxe_site = data[offset:offset + 5]
    rake_site = data[offset + 5:offset + 14]

    back = pickaxe_hook + 5
    pickaxe = disassemble(pickaxe_cave, page + PICKAXE_CAVE)
    expected_pickaxe = [
        "movss xmm8, dword ptr [rcx]",
        f"cmp byte ptr [0x{page + PICKAXE_ENABLED:x}], 0",
        f"je 0x{back:x}",
        "cmp byte ptr [rbp + 0x340], 0",
        f"jne 0x{back:x}",
        "cmp qword ptr [rbp + 0x348], 0",
        f"je 0x{back:x}",
        f"mulss xmm8, dword ptr [0x{page + PICKAXE_FACTOR:x}]",
        "cvtdq2ps xmm0, xmm14",
        f"mulps xmm0, xmmword ptr [0x{page + PICKAXE_VOLUME_FACTOR:x}]",
        "cvttps2dq xmm14, xmm0",
        "cvtsi2ss xmm0, dword ptr [rsp + 0x78]",
        f"mulss xmm0, dword ptr [0x{page + PICKAXE_VOLUME_FACTOR:x}]",
        "cvttps2dq xmm0, xmm0",
        "movd dword ptr [rsp + 0x78], xmm0",
        f"jmp 0x{back:x}",
    ]

    rake = disassemble(rake_cave, page + RAKE_CAVE)
    skip = next((address for address, text in rake if text == "movaps xmm0, xmm8"), 0)
    expected_rake = [
        f"cmp byte ptr [0x{page + RAKE_ENABLED:x}], 0",
        f"je 0x{skip:x}",
        "cmp dword ptr [rsp + 0x7c], 2",
        f"jb 0x{skip:x}",
        "cmp dword ptr [rsp + 0x7c], 3",
        f"ja 0x{skip:x}",
        f"mulss xmm8, dword ptr [0x{page + RAKE_FACTOR:x}]",
        "movaps xmm0, xmm8",
        f"push qword ptr [0x{page + RAKE_RETURN_ADDRESS:x}]",
        f"jmp qword ptr [0x{page + RAKE_CALL_TARGET:x}]",
    ]

    failures = compare("pickaxe cave", pickaxe, expected_pickaxe)
    failures += compare("rake cave", rake, expected_rake)
    failures += compare("pickaxe site", disassemble(pickaxe_site, pickaxe_hook), [f"jmp 0x{page + PICKAXE_CAVE:x}"])
    failures += compare("rake site", disassemble(rake_site, rake_hook), [f"jmp 0x{page + RAKE_CAVE:x}", "nop dword ptr [rax]"])
    if failures:
        raise SystemExit(f"{failures} instruction(s) differ")
    print("OK")


if __name__ == "__main__":
    main()
