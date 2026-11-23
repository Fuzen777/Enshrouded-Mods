// The game's terraform code this mod changes, and the code caves that change it.
//
// One game function runs every terraform hit: pickaxe digs, rake flattening, bombs, enemy attacks. In game build 6aeb144c it
// loads the hit's radius at exe+0x3ED150 (movss xmm8, [rcx]), switches on the hit's terraform type (0 dig, 1 add, 2-3 flatten,
// 4-5 overgrowth, 6-7 decay; flatten hits may load another radius there), and all types meet again at exe+0x3ED1A6
// (movaps xmm0, xmm8, then the call that takes the final radius). These are the two places Advanced Terrain Edit 1.5.1a by
// Lombrar changes for its Pickaxe Radius and Rake radius factor, and the caves below do what its code does there:
//
// - Pickaxe: when the hit comes from a player's tool ([rbp+0x340] is 0 and [rbp+0x348] is set; bombs and enemy attacks don't
//   pass this), the radius is multiplied by the Pickaxe Radius setting, and the hit's four voxel budgets in xmm14 and the one
//   at [rsp+0x78] by its cube, so the bigger volume gets a matching budget.
// - Rake: for flatten hits (type 2 or 3, kept at [rsp+0x7c]) the final radius is multiplied by the Rake Radius setting. ATE
//   does this only for the host's own player; this mod does it for every player in a world you host.
//
// Each cave starts or ends with the game instruction it replaces. The rake cave makes the replaced call itself, pushing the
// game's own return address, so the called function returns straight into the game's code.
#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <initializer_list>
#include <string>
#include <string_view>
#include <vector>

#include "pattern_scan.h"

namespace terrain_tool_radius {

// From cmp byte ptr [rbp+0x340], 0 to the switch's ja to the meeting point (exe+0x3ED0EA to exe+0x3ED15A in build 6aeb144c).
// It holds every offset the pickaxe cave relies on (0x340, 0x348, the type stored at [rsp+0x7c]) and the radius load itself.
inline constexpr std::string_view kRadiusCodePattern =
    "80 BD 40 03 00 00 00 F3 44 0F 59 CE 8B 3F F3 44 0F 59 D6 75 ?? 49 8B 47 10 48 8D 54 24 60 48 8B 8D 48 03 00 00 "
    "48 89 44 24 60 49 8B 47 18 48 89 44 24 68 E8 ?? ?? ?? ?? 89 44 24 44 33 DB 0F B6 85 ?? ?? ?? ?? EB ?? 33 DB "
    "89 5C 24 44 48 8B 4D ?? 48 8D 15 ?? ?? ?? ?? 0F B6 C0 C6 85 ?? ?? ?? ?? 00 89 44 24 7C F3 44 0F 10 01 83 F8 07 77 ??";
inline constexpr std::size_t kPickaxeHookOffset = 0x66;  // movss xmm8, dword ptr [rcx]: the hit's radius (5 bytes)
inline constexpr std::size_t kMergeJumpOffset = 0x6E;    // ja rel8 to the point where every terraform type meets again
inline constexpr std::size_t kPickaxeHookSize = 5;

// The same code with the radius load as any 5 bytes, to tell a game update from another mod's jump at the hook.
inline constexpr std::size_t kHookBytesPatternOffset = kPickaxeHookOffset * 3;  // "F3 44 0F 10 01" starts this many characters in

// Within the 0x60 bytes before kRadiusCodePattern: the stores of the voxel budgets the pickaxe cave scales.
inline constexpr std::size_t kSetupSearchSize = 0x60;
inline constexpr std::string_view kVoxelBudgetStorePattern = "89 4C 24 78";  // mov dword ptr [rsp+0x78], ecx
inline constexpr std::string_view kVoxelBudgetLoadPattern = "44 0F 10 37";   // movups xmm14, xmmword ptr [rdi]

// At the meeting point: movaps xmm0, xmm8, then call rel32 with the final radius.
inline constexpr std::string_view kRakeHookPattern = "41 0F 28 C0 E8 ?? ?? ?? ?? 0F 57 D2";
inline constexpr std::size_t kRakeHookSize = 9;

enum class SiteStatus {
    Found,
    RadiusCodeNotFound,      // a game update changed the terraform code
    RadiusCodeAlreadyHooked, // another mod's jump is at the pickaxe hook (Advanced Terrain Edit's Pickaxe Radius, for one)
    RadiusCodeAmbiguous,     // the pattern matches more than once
    VoxelBudgetsNotFound,    // the voxel budgets aren't where the pickaxe cave expects them
};

enum class RakeSiteStatus {
    Found,
    NotFound,        // the meeting point doesn't hold the expected code
    AlreadyHooked,   // another mod's jump is there (Advanced Terrain Edit's rake factor, for one)
};

struct TerraformSites {
    SiteStatus status = SiteStatus::RadiusCodeNotFound;
    RakeSiteStatus rakeStatus = RakeSiteStatus::NotFound;
    std::size_t pickaxeHookOffset = 0;  // offsets in the code that was searched
    std::size_t rakeHookOffset = 0;
    std::ptrdiff_t rakeCallTargetOffset = 0;  // the function the meeting point calls (may lie outside the searched code)
};

inline std::int32_t ReadInt32(const std::uint8_t* bytes) {
    return static_cast<std::int32_t>(static_cast<std::uint32_t>(bytes[0]) | static_cast<std::uint32_t>(bytes[1]) << 8 |
                                     static_cast<std::uint32_t>(bytes[2]) << 16 | static_cast<std::uint32_t>(bytes[3]) << 24);
}

// Finds both hook sites in the game's code (the bytes of its .text section).
inline TerraformSites FindTerraformSites(const std::uint8_t* code, std::size_t codeSize) {
    TerraformSites sites;
    const std::vector<std::size_t> matches = FindAll(code, codeSize, ParsePattern(kRadiusCodePattern));
    if (matches.size() > 1) {
        sites.status = SiteStatus::RadiusCodeAmbiguous;
        return sites;
    }
    if (matches.empty()) {
        std::string looseText(kRadiusCodePattern);
        looseText.replace(kHookBytesPatternOffset, 14, "?? ?? ?? ?? ??");
        const std::vector<std::size_t> looseMatches = FindAll(code, codeSize, ParsePattern(looseText));
        const bool hooked = looseMatches.size() == 1 && code[looseMatches[0] + kPickaxeHookOffset] == 0xE9;
        sites.status = hooked ? SiteStatus::RadiusCodeAlreadyHooked : SiteStatus::RadiusCodeNotFound;
        return sites;
    }

    const std::size_t patternStart = matches[0];
    const std::size_t setupStart = patternStart >= kSetupSearchSize ? patternStart - kSetupSearchSize : 0;
    const std::size_t setupSize = patternStart - setupStart;
    if (!Contains(code + setupStart, setupSize, ParsePattern(kVoxelBudgetStorePattern)) ||
        !Contains(code + setupStart, setupSize, ParsePattern(kVoxelBudgetLoadPattern))) {
        sites.status = SiteStatus::VoxelBudgetsNotFound;
        return sites;
    }
    sites.status = SiteStatus::Found;
    sites.pickaxeHookOffset = patternStart + kPickaxeHookOffset;

    const std::ptrdiff_t mergeJumpEnd = static_cast<std::ptrdiff_t>(patternStart + kMergeJumpOffset + 2);
    const std::ptrdiff_t rakeHookPosition = mergeJumpEnd + static_cast<std::int8_t>(code[patternStart + kMergeJumpOffset + 1]);
    const BytePattern rakePattern = ParsePattern(kRakeHookPattern);
    if (rakeHookPosition < 0 || static_cast<std::size_t>(rakeHookPosition) + rakePattern.Size() > codeSize) return sites;
    const auto rakeHookOffset = static_cast<std::size_t>(rakeHookPosition);
    if (code[rakeHookOffset] == 0xE9) {
        sites.rakeStatus = RakeSiteStatus::AlreadyHooked;
        return sites;
    }
    if (!MatchesAt(code + rakeHookOffset, rakePattern)) return sites;
    sites.rakeStatus = RakeSiteStatus::Found;
    sites.rakeHookOffset = rakeHookOffset;
    sites.rakeCallTargetOffset = static_cast<std::ptrdiff_t>(rakeHookOffset + kRakeHookSize) + ReadInt32(code + rakeHookOffset + 5);
    return sites;
}

// One page near the game's code, holding the settings the caves read and both caves.
namespace cave_page {
inline constexpr std::size_t kPickaxeFactor = 0x00;       // float: Pickaxe Radius
inline constexpr std::size_t kRakeFactor = 0x04;          // float: Rake Radius
inline constexpr std::size_t kPickaxeEnabled = 0x08;      // byte: 0 at x1, so the hit stays exactly the game's own
inline constexpr std::size_t kRakeEnabled = 0x09;         // byte
inline constexpr std::size_t kPickaxeVolumeFactor = 0x10; // 4 floats, 16-byte aligned for mulps: Pickaxe Radius cubed
inline constexpr std::size_t kRakeReturnAddress = 0x20;   // qword: the instruction after the replaced call
inline constexpr std::size_t kRakeCallTarget = 0x28;      // qword: the function the replaced call calls
inline constexpr std::size_t kPickaxeCave = 0x40;
inline constexpr std::size_t kRakeCave = 0xC0;
inline constexpr std::size_t kCaveCapacity = 0x80;
inline constexpr std::size_t kPageSize = 0x1000;
}  // namespace cave_page

// Machine code for a known address, with 32-bit relative references checked to be in range.
class CodeEmitter {
public:
    explicit CodeEmitter(std::uintptr_t startAddress) : startAddress_(startAddress) {}

    void Bytes(std::initializer_list<std::uint8_t> bytes) { code_.insert(code_.end(), bytes.begin(), bytes.end()); }

    // A rel32 to target, for an instruction that ends trailingBytes after it (an immediate that follows it).
    void Rel32(std::uintptr_t target, std::size_t trailingBytes = 0) {
        const std::uintptr_t instructionEnd = startAddress_ + code_.size() + 4 + trailingBytes;
        const std::int64_t distance = static_cast<std::int64_t>(target) - static_cast<std::int64_t>(instructionEnd);
        if (distance < INT32_MIN || distance > INT32_MAX) inRange_ = false;
        const auto relative = static_cast<std::uint32_t>(static_cast<std::int32_t>(distance));
        Bytes({static_cast<std::uint8_t>(relative), static_cast<std::uint8_t>(relative >> 8),
               static_cast<std::uint8_t>(relative >> 16), static_cast<std::uint8_t>(relative >> 24)});
    }

    // A short conditional jump whose target is bound later; returns where its rel8 is.
    std::size_t ShortJump(std::uint8_t opcode) {
        Bytes({opcode, 0x00});
        return code_.size() - 1;
    }

    void BindShortJump(std::size_t rel8Position) {
        const std::size_t distance = code_.size() - (rel8Position + 1);
        if (distance > 127) inRange_ = false;
        code_[rel8Position] = static_cast<std::uint8_t>(distance);
    }

    const std::vector<std::uint8_t>& Code() const { return code_; }
    bool InRange() const { return inRange_; }

private:
    std::uintptr_t startAddress_;
    std::vector<std::uint8_t> code_;
    bool inRange_ = true;
};

struct BuiltCaves {
    std::vector<std::uint8_t> pickaxeCave;
    std::vector<std::uint8_t> rakeCave;
    std::array<std::uint8_t, kPickaxeHookSize> pickaxeSiteJump{};
    std::array<std::uint8_t, kRakeHookSize> rakeSiteJump{};
    bool inRange = false;  // every relative reference fits, and each cave fits its space in the page
};

inline BuiltCaves BuildCaves(std::uintptr_t pageAddress, std::uintptr_t pickaxeHookAddress, std::uintptr_t rakeHookAddress) {
    using namespace cave_page;
    BuiltCaves caves;
    const std::uintptr_t pickaxeCaveAddress = pageAddress + kPickaxeCave;
    const std::uintptr_t rakeCaveAddress = pageAddress + kRakeCave;
    const std::uintptr_t pickaxeReturn = pickaxeHookAddress + kPickaxeHookSize;

    CodeEmitter pickaxe(pickaxeCaveAddress);
    pickaxe.Bytes({0xF3, 0x44, 0x0F, 0x10, 0x01});                                   // movss xmm8, dword ptr [rcx] (the game's)
    pickaxe.Bytes({0x80, 0x3D}); pickaxe.Rel32(pageAddress + kPickaxeEnabled, 1); pickaxe.Bytes({0x00});  // cmp byte ptr [enabled], 0
    pickaxe.Bytes({0x0F, 0x84}); pickaxe.Rel32(pickaxeReturn);                       // je back
    pickaxe.Bytes({0x80, 0xBD, 0x40, 0x03, 0x00, 0x00, 0x00});                        // cmp byte ptr [rbp+0x340], 0
    pickaxe.Bytes({0x0F, 0x85}); pickaxe.Rel32(pickaxeReturn);                       // jne back: not a player's tool
    pickaxe.Bytes({0x48, 0x83, 0xBD, 0x48, 0x03, 0x00, 0x00, 0x00});                  // cmp qword ptr [rbp+0x348], 0
    pickaxe.Bytes({0x0F, 0x84}); pickaxe.Rel32(pickaxeReturn);                       // je back: not a player's tool
    pickaxe.Bytes({0xF3, 0x44, 0x0F, 0x59, 0x05}); pickaxe.Rel32(pageAddress + kPickaxeFactor);  // mulss xmm8, [Pickaxe Radius]
    pickaxe.Bytes({0x41, 0x0F, 0x5B, 0xC6});                                         // cvtdq2ps xmm0, xmm14
    pickaxe.Bytes({0x0F, 0x59, 0x05}); pickaxe.Rel32(pageAddress + kPickaxeVolumeFactor);  // mulps xmm0, [radius cubed x4]
    pickaxe.Bytes({0xF3, 0x44, 0x0F, 0x5B, 0xF0});                                   // cvttps2dq xmm14, xmm0
    pickaxe.Bytes({0xF3, 0x0F, 0x2A, 0x44, 0x24, 0x78});                             // cvtsi2ss xmm0, dword ptr [rsp+0x78]
    pickaxe.Bytes({0xF3, 0x0F, 0x59, 0x05}); pickaxe.Rel32(pageAddress + kPickaxeVolumeFactor);  // mulss xmm0, [radius cubed]
    pickaxe.Bytes({0xF3, 0x0F, 0x5B, 0xC0});                                         // cvttps2dq xmm0, xmm0
    pickaxe.Bytes({0x66, 0x0F, 0x7E, 0x44, 0x24, 0x78});                             // movd dword ptr [rsp+0x78], xmm0
    pickaxe.Bytes({0xE9}); pickaxe.Rel32(pickaxeReturn);                             // jmp back

    CodeEmitter rake(rakeCaveAddress);
    rake.Bytes({0x80, 0x3D}); rake.Rel32(pageAddress + kRakeEnabled, 1); rake.Bytes({0x00});  // cmp byte ptr [enabled], 0
    const std::size_t skipWhenOff = rake.ShortJump(0x74);                            // je skip
    rake.Bytes({0x83, 0x7C, 0x24, 0x7C, 0x02});                                      // cmp dword ptr [rsp+0x7c], 2 (terraform type)
    const std::size_t skipBelowFlatten = rake.ShortJump(0x72);                       // jb skip
    rake.Bytes({0x83, 0x7C, 0x24, 0x7C, 0x03});                                      // cmp dword ptr [rsp+0x7c], 3
    const std::size_t skipAboveFlatten = rake.ShortJump(0x77);                       // ja skip
    rake.Bytes({0xF3, 0x44, 0x0F, 0x59, 0x05}); rake.Rel32(pageAddress + kRakeFactor);  // mulss xmm8, [Rake Radius]
    rake.BindShortJump(skipWhenOff);
    rake.BindShortJump(skipBelowFlatten);
    rake.BindShortJump(skipAboveFlatten);
    rake.Bytes({0x41, 0x0F, 0x28, 0xC0});                                            // skip: movaps xmm0, xmm8 (the game's)
    rake.Bytes({0xFF, 0x35}); rake.Rel32(pageAddress + kRakeReturnAddress);          // push qword ptr [return address]
    rake.Bytes({0xFF, 0x25}); rake.Rel32(pageAddress + kRakeCallTarget);             // jmp qword ptr [call target]

    CodeEmitter pickaxeSite(pickaxeHookAddress);
    pickaxeSite.Bytes({0xE9}); pickaxeSite.Rel32(pickaxeCaveAddress);                // jmp pickaxe cave
    CodeEmitter rakeSite(rakeHookAddress);
    rakeSite.Bytes({0xE9}); rakeSite.Rel32(rakeCaveAddress);                         // jmp rake cave
    rakeSite.Bytes({0x0F, 0x1F, 0x40, 0x00});                                        // 4-byte nop over the rest of the call

    caves.pickaxeCave = pickaxe.Code();
    caves.rakeCave = rake.Code();
    for (std::size_t index = 0; index < kPickaxeHookSize; ++index) caves.pickaxeSiteJump[index] = pickaxeSite.Code()[index];
    for (std::size_t index = 0; index < kRakeHookSize; ++index) caves.rakeSiteJump[index] = rakeSite.Code()[index];
    caves.inRange = pickaxe.InRange() && rake.InRange() && pickaxeSite.InRange() && rakeSite.InRange() &&
                    caves.pickaxeCave.size() <= kCaveCapacity && caves.rakeCave.size() <= kCaveCapacity &&
                    pickaxeSite.Code().size() == kPickaxeHookSize && rakeSite.Code().size() == kRakeHookSize;
    return caves;
}

}  // namespace terrain_tool_radius
