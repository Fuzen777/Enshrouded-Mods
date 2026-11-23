// Terrain Tool Radius checks that need no running game: the settings parser and the manifest's settings, both hook sites in
// enshrouded.exe on disk, and the caves' jumps. Writes the caves to a file for check_caves.py to disassemble.
// Usage: test_main.exe <enshrouded.exe> <extended.mod.json> <caves.bin>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iterator>
#include <string>
#include <vector>

#include "../src/native/settings_file.h"
#include "../src/native/terraform_hooks.h"

using namespace terrain_tool_radius;

namespace {

int failureCount = 0;

#define CHECK(condition)                                                                 \
    do {                                                                                 \
        if (!(condition)) {                                                              \
            ++failureCount;                                                              \
            std::printf("FAILED line %d: %s\n", __LINE__, #condition);                   \
        }                                                                                \
    } while (false)

constexpr std::uint32_t kKnownBuildStamp = 0x6AEB144C;  // the 4 November 2026 update, as ATE 1.5.1a's hooks were found on

bool ReadBinaryFile(const char* path, std::vector<std::uint8_t>& bytes) {
    std::ifstream file(path, std::ios::binary);
    if (!file) return false;
    bytes.assign(std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>());
    return true;
}

std::uint32_t ReadUInt32(const std::vector<std::uint8_t>& bytes, std::size_t offset) {
    return static_cast<std::uint32_t>(ReadInt32(bytes.data() + offset));
}

void TestSettingsParser() {
    const ToolRadiusSettings empty = ReadToolRadiusSettings("");
    CHECK(empty.pickaxeRadius.value == 3.0 && empty.pickaxeRadius.source == SettingSource::Missing);
    CHECK(empty.rakeRadius.value == 4.0 && empty.rakeRadius.source == SettingSource::Missing);

    // As Shroudforge rewrites it: values changed, other fields around them, no final newline.
    const ToolRadiusSettings edited = ReadToolRadiusSettings(
        R"({"settings":{"pickaxeRadiusMultiplier":{"label":"Pickaxe Radius","value":2.5,"min":1},)"
        R"("rakeRadiusMultiplier": {"enabledWhen": {"setting": "x", "equals": true}, "value" : 1}},)"
        R"("changelog":["mentions \"pickaxeRadiusMultiplier\": {\"value\": 9}"]})");
    CHECK(edited.pickaxeRadius.value == 2.5 && edited.pickaxeRadius.source == SettingSource::File);
    CHECK(edited.rakeRadius.value == 1.0 && edited.rakeRadius.source == SettingSource::File);

    // A key quoted inside an earlier string isn't taken for the setting.
    const ToolRadiusSettings quotedFirst = ReadToolRadiusSettings(
        R"({"description":"see \"pickaxeRadiusMultiplier\"","pickaxeRadiusMultiplier":{"value":4.5},"rakeRadiusMultiplier":{"value":2}})");
    CHECK(quotedFirst.pickaxeRadius.value == 4.5);
    CHECK(quotedFirst.rakeRadius.value == 2.0);

    const ToolRadiusSettings outOfRange = ReadToolRadiusSettings(
        R"({"pickaxeRadiusMultiplier":{"value":7},"rakeRadiusMultiplier":{"value":0.5}})");
    CHECK(outOfRange.pickaxeRadius.value == 5.0 && outOfRange.pickaxeRadius.source == SettingSource::Clamped);
    CHECK(outOfRange.pickaxeRadius.fileValue == 7.0);
    CHECK(outOfRange.rakeRadius.value == 1.0 && outOfRange.rakeRadius.source == SettingSource::Clamped);

    const ToolRadiusSettings notNumbers = ReadToolRadiusSettings(
        R"({"pickaxeRadiusMultiplier":{"value":"3"},"rakeRadiusMultiplier":{"label":"Rake Radius"}})");
    CHECK(notNumbers.pickaxeRadius.value == 3.0 && notNumbers.pickaxeRadius.source == SettingSource::Missing);
    CHECK(notNumbers.rakeRadius.value == 4.0 && notNumbers.rakeRadius.source == SettingSource::Missing);
}

// The manifest's default, min and max must be the DLL's.
void TestManifest(const char* manifestPath) {
    std::vector<std::uint8_t> bytes;
    if (!ReadBinaryFile(manifestPath, bytes)) {
        std::printf("FAILED: can't read %s\n", manifestPath);
        ++failureCount;
        return;
    }
    const std::string json(bytes.begin(), bytes.end());
    for (const SettingRule* rule : {&kPickaxeRadiusRule, &kRakeRadiusRule}) {
        CHECK(FindSettingField(json, rule->key, "value") == rule->defaultValue);
        CHECK(FindSettingField(json, rule->key, "min") == rule->minimum);
        CHECK(FindSettingField(json, rule->key, "max") == rule->maximum);
    }
    const ToolRadiusSettings settings = ReadToolRadiusSettings(json);
    CHECK(settings.pickaxeRadius.source == SettingSource::File && settings.rakeRadius.source == SettingSource::File);
}

void TestPatternText() {
    CHECK(kRadiusCodePattern.substr(kHookBytesPatternOffset, 14) == "F3 44 0F 10 01");
    const BytePattern pattern = ParsePattern(kRadiusCodePattern);
    CHECK(pattern.Size() == kMergeJumpOffset + 2);
    CHECK(!pattern.isWildcard[kPickaxeHookOffset] && pattern.bytes[kPickaxeHookOffset] == 0xF3);
    CHECK(!pattern.isWildcard[kMergeJumpOffset] && pattern.bytes[kMergeJumpOffset] == 0x77);
    CHECK(ParsePattern("F3 4G").Size() == 0);
}

struct GameSites {
    bool found = false;
    std::uint32_t pickaxeHookRva = 0;
    std::uint32_t rakeHookRva = 0;
};

GameSites TestGameSites(const char* gamePath) {
    GameSites gameSites;
    std::vector<std::uint8_t> image;
    if (!ReadBinaryFile(gamePath, image)) {
        std::printf("FAILED: can't read %s\n", gamePath);
        ++failureCount;
        return gameSites;
    }
    const std::uint32_t headersOffset = ReadUInt32(image, 0x3C);
    const std::uint32_t buildStamp = ReadUInt32(image, headersOffset + 8);
    const std::uint16_t sectionCount = static_cast<std::uint16_t>(image[headersOffset + 6] | image[headersOffset + 7] << 8);
    const std::uint16_t optionalHeaderSize = static_cast<std::uint16_t>(image[headersOffset + 20] | image[headersOffset + 21] << 8);
    std::size_t sectionHeader = headersOffset + 24 + optionalHeaderSize;
    std::uint32_t codeRva = 0;
    std::vector<std::uint8_t> code;
    for (std::uint16_t index = 0; index < sectionCount; ++index, sectionHeader += 40) {
        if (std::memcmp(image.data() + sectionHeader, ".text", 6) != 0) continue;
        const std::uint32_t virtualSize = ReadUInt32(image, sectionHeader + 8);
        codeRva = ReadUInt32(image, sectionHeader + 12);
        const std::uint32_t rawSize = ReadUInt32(image, sectionHeader + 16);
        const std::uint32_t rawOffset = ReadUInt32(image, sectionHeader + 20);
        code.assign(image.begin() + rawOffset, image.begin() + rawOffset + (virtualSize < rawSize ? virtualSize : rawSize));
    }
    CHECK(!code.empty());
    if (code.empty()) return gameSites;

    const TerraformSites sites = FindTerraformSites(code.data(), code.size());
    CHECK(sites.status == SiteStatus::Found);
    CHECK(sites.rakeStatus == RakeSiteStatus::Found);
    if (sites.status != SiteStatus::Found || sites.rakeStatus != RakeSiteStatus::Found) return gameSites;

    gameSites.found = true;
    gameSites.pickaxeHookRva = codeRva + static_cast<std::uint32_t>(sites.pickaxeHookOffset);
    gameSites.rakeHookRva = codeRva + static_cast<std::uint32_t>(sites.rakeHookOffset);
    const auto callTargetRva = static_cast<std::uint32_t>(codeRva + sites.rakeCallTargetOffset);
    std::printf("enshrouded.exe build stamp %08x: pickaxe hook exe+0x%X, rake hook exe+0x%X, rake call target exe+0x%X\n",
                buildStamp, gameSites.pickaxeHookRva, gameSites.rakeHookRva, callTargetRva);
    if (buildStamp == kKnownBuildStamp) {
        CHECK(gameSites.pickaxeHookRva == 0x3ED150);  // where ATE 1.5.1a's radius hook is
        CHECK(gameSites.rakeHookRva == 0x3ED1A6);     // where its terraform rules hook (with the rake factor) is
        CHECK(callTargetRva == 0xA9A530);
    } else {
        std::printf("note: not build %08x, so the sites' addresses weren't compared with ATE's\n", kKnownBuildStamp);
    }

    // With another mod's jump at either site.
    std::vector<std::uint8_t> hookedCode = code;
    hookedCode[sites.pickaxeHookOffset] = 0xE9;
    CHECK(FindTerraformSites(hookedCode.data(), hookedCode.size()).status == SiteStatus::RadiusCodeAlreadyHooked);
    hookedCode = code;
    hookedCode[sites.rakeHookOffset] = 0xE9;
    const TerraformSites rakeHooked = FindTerraformSites(hookedCode.data(), hookedCode.size());
    CHECK(rakeHooked.status == SiteStatus::Found && rakeHooked.rakeStatus == RakeSiteStatus::AlreadyHooked);
    return gameSites;
}

std::uintptr_t RelativeTarget(const std::uint8_t* instruction, std::uintptr_t instructionAddress, std::size_t rel32Offset,
                              std::size_t instructionSize) {
    return instructionAddress + instructionSize + static_cast<std::intptr_t>(ReadInt32(instruction + rel32Offset));
}

void AppendUInt64(std::vector<std::uint8_t>& bytes, std::uint64_t value) {
    for (int shift = 0; shift < 64; shift += 8) bytes.push_back(static_cast<std::uint8_t>(value >> shift));
}

void AppendUInt32(std::vector<std::uint8_t>& bytes, std::uint32_t value) {
    for (int shift = 0; shift < 32; shift += 8) bytes.push_back(static_cast<std::uint8_t>(value >> shift));
}

void TestCaves(const GameSites& gameSites, const char* cavesPath) {
    // Addresses as they could be in the game: the exe at a typical base, the page 128 KB below it.
    const std::uintptr_t imageBase = 0x7FF6A0000000;
    const std::uintptr_t page = imageBase - 0x20000;
    const std::uintptr_t pickaxeHook = imageBase + gameSites.pickaxeHookRva;
    const std::uintptr_t rakeHook = imageBase + gameSites.rakeHookRva;
    const BuiltCaves caves = BuildCaves(page, pickaxeHook, rakeHook);
    CHECK(caves.inRange);
    std::printf("caves: pickaxe %zu bytes, rake %zu bytes (room for %zu each)\n", caves.pickaxeCave.size(), caves.rakeCave.size(),
                cave_page::kCaveCapacity);

    CHECK(caves.pickaxeSiteJump[0] == 0xE9);
    CHECK(RelativeTarget(caves.pickaxeSiteJump.data(), pickaxeHook, 1, 5) == page + cave_page::kPickaxeCave);
    CHECK(caves.rakeSiteJump[0] == 0xE9);
    CHECK(RelativeTarget(caves.rakeSiteJump.data(), rakeHook, 1, 5) == page + cave_page::kRakeCave);
    // The pickaxe cave's last instruction jumps back to the game.
    const std::size_t pickaxeLast = caves.pickaxeCave.size() - 5;
    CHECK(caves.pickaxeCave[pickaxeLast] == 0xE9);
    CHECK(RelativeTarget(caves.pickaxeCave.data() + pickaxeLast, page + cave_page::kPickaxeCave + pickaxeLast, 1, 5) == pickaxeHook + kPickaxeHookSize);
    CHECK(page % 16 == 0 && cave_page::kPickaxeVolumeFactor % 16 == 0);

    // A page beyond 32-bit reach is refused.
    CHECK(!BuildCaves(imageBase + 0x100000000ULL, pickaxeHook, rakeHook).inRange);

    std::vector<std::uint8_t> output = {'T', 'T', 'R', 'C', 'A', 'V', 'E', 'S'};
    AppendUInt64(output, page);
    AppendUInt64(output, pickaxeHook);
    AppendUInt64(output, rakeHook);
    AppendUInt32(output, static_cast<std::uint32_t>(caves.pickaxeCave.size()));
    AppendUInt32(output, static_cast<std::uint32_t>(caves.rakeCave.size()));
    output.insert(output.end(), caves.pickaxeCave.begin(), caves.pickaxeCave.end());
    output.insert(output.end(), caves.rakeCave.begin(), caves.rakeCave.end());
    output.insert(output.end(), caves.pickaxeSiteJump.begin(), caves.pickaxeSiteJump.end());
    output.insert(output.end(), caves.rakeSiteJump.begin(), caves.rakeSiteJump.end());
    std::ofstream file(cavesPath, std::ios::binary);
    file.write(reinterpret_cast<const char*>(output.data()), static_cast<std::streamsize>(output.size()));
    CHECK(file.good());
}

}  // namespace

int main(int argumentCount, char** arguments) {
    if (argumentCount != 4) {
        std::printf("usage: test_main.exe <enshrouded.exe> <extended.mod.json> <caves.bin>\n");
        return 2;
    }
    TestSettingsParser();
    TestManifest(arguments[2]);
    TestPatternText();
    const GameSites gameSites = TestGameSites(arguments[1]);
    if (gameSites.found) TestCaves(gameSites, arguments[3]);
    if (failureCount != 0) {
        std::printf("%d check(s) failed\n", failureCount);
        return 1;
    }
    std::printf("OK\n");
    return 0;
}
