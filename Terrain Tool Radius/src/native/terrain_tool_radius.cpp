// Terrain Tool Radius: the pickaxe digs and the rake flattens a bigger area, set by Pickaxe Radius and Rake Radius in
// Shroudforge's F9 menu. Shroudforge loads this DLL through native-plugin.ini. At start it finds the game's terraform code,
// puts both code caves (terraform_hooks.h) in one page near it and jumps there from the two hook sites; then it watches its
// installed extended.mod.json, where Shroudforge keeps the F9 values, and passes changes to the caves.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>

#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>

#include "settings_file.h"
#include "terraform_hooks.h"

namespace {

using namespace terrain_tool_radius;

constexpr const char* kModVersion = "1.0.0";  // Build.ps1 checks this against mod.json
constexpr DWORD kSettingsCheckIntervalMilliseconds = 1000;

HMODULE modModule = nullptr;
std::wstring modFolder;  // the folder holding this DLL, extended.mod.json and the log

std::wstring LogPath() { return modFolder + L"\\terrain-tool-radius.log"; }
std::wstring SettingsPath() { return modFolder + L"\\extended.mod.json"; }

// Starts a new log each game start; the previous one is kept as terrain-tool-radius.prev.log.
void StartLog() {
    const std::wstring logPath = LogPath();
    const std::wstring previousLogPath = modFolder + L"\\terrain-tool-radius.prev.log";
    MoveFileExW(logPath.c_str(), previousLogPath.c_str(), MOVEFILE_REPLACE_EXISTING);
}

void Log(const char* format, ...) {
    char message[1024];
    va_list arguments;
    va_start(arguments, format);
    std::vsnprintf(message, sizeof message, format, arguments);
    va_end(arguments);

    SYSTEMTIME localTime;
    GetLocalTime(&localTime);
    FILE* logFile = nullptr;
    if (_wfopen_s(&logFile, LogPath().c_str(), L"a") != 0 || logFile == nullptr) return;
    std::fprintf(logFile, "[%02u:%02u:%02u] %s\n", localTime.wHour, localTime.wMinute, localTime.wSecond, message);
    std::fclose(logFile);
}

std::wstring FolderOf(const std::wstring& path) {
    const std::size_t separator = path.find_last_of(L"\\/");
    return separator == std::wstring::npos ? std::wstring() : path.substr(0, separator);
}

std::wstring ModulePath(HMODULE module) {
    std::wstring path(MAX_PATH, L'\0');
    while (true) {
        const DWORD length = GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0) return {};
        if (length < path.size()) {
            path.resize(length);
            return path;
        }
        path.resize(path.size() * 2);
    }
}

bool IsGameProcess() {
    const std::wstring executablePath = ModulePath(nullptr);
    const std::wstring gameName = L"\\enshrouded.exe";
    return executablePath.size() >= gameName.size() &&
           _wcsicmp(executablePath.c_str() + executablePath.size() - gameName.size(), gameName.c_str()) == 0;
}

struct FileStamp {
    FILETIME lastWriteTime{};
    DWORD sizeLow = 0;

    bool operator==(const FileStamp& other) const {
        return CompareFileTime(&lastWriteTime, &other.lastWriteTime) == 0 && sizeLow == other.sizeLow;
    }
};

bool ReadFileStamp(const std::wstring& path, FileStamp& stamp) {
    WIN32_FILE_ATTRIBUTE_DATA attributes;
    if (!GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &attributes)) return false;
    stamp.lastWriteTime = attributes.ftLastWriteTime;
    stamp.sizeLow = attributes.nFileSizeLow;
    return true;
}

bool ReadTextFile(const std::wstring& path, std::string& text) {
    const HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                                    OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE) return false;
    LARGE_INTEGER size{};
    bool ok = GetFileSizeEx(file, &size) && size.QuadPart < 1024 * 1024;
    if (ok) {
        text.resize(static_cast<std::size_t>(size.QuadPart));
        DWORD bytesRead = 0;
        ok = text.empty() || (ReadFile(file, text.data(), static_cast<DWORD>(text.size()), &bytesRead, nullptr) && bytesRead == text.size());
    }
    CloseHandle(file);
    return ok;
}

std::string DescribeSetting(const SettingRule& rule, const SettingValue& setting) {
    char description[256];
    std::snprintf(description, sizeof description, "%.*s x%g", static_cast<int>(rule.label.size()), rule.label.data(), setting.value);
    std::string text = description;
    if (setting.value == 1.0) text += " (the game's own)";
    if (setting.source == SettingSource::Clamped) {
        std::snprintf(description, sizeof description, " (%g in extended.mod.json is outside %g to %g)", setting.fileValue, rule.minimum, rule.maximum);
        text += description;
    } else if (setting.source == SettingSource::Missing) {
        text += " (default: no value in extended.mod.json)";
    }
    return text;
}

void LogSettings(const char* heading, const ToolRadiusSettings& settings) {
    Log("%s: %s, %s", heading, DescribeSetting(kPickaxeRadiusRule, settings.pickaxeRadius).c_str(),
        DescribeSetting(kRakeRadiusRule, settings.rakeRadius).c_str());
}

// Passes the settings to the caves. A setting at x1 switches its cave off, so those hits stay exactly the game's own.
void ApplySettings(std::uint8_t* page, const ToolRadiusSettings& settings) {
    const auto pickaxeFactor = static_cast<float>(settings.pickaxeRadius.value);
    const float pickaxeVolumeFactor = pickaxeFactor * pickaxeFactor * pickaxeFactor;
    const auto rakeFactor = static_cast<float>(settings.rakeRadius.value);

    volatile std::uint8_t* const pickaxeEnabled = page + cave_page::kPickaxeEnabled;
    volatile std::uint8_t* const rakeEnabled = page + cave_page::kRakeEnabled;
    *reinterpret_cast<volatile float*>(page + cave_page::kPickaxeFactor) = pickaxeFactor;
    for (std::size_t lane = 0; lane < 4; ++lane) {
        *reinterpret_cast<volatile float*>(page + cave_page::kPickaxeVolumeFactor + lane * sizeof(float)) = pickaxeVolumeFactor;
    }
    *reinterpret_cast<volatile float*>(page + cave_page::kRakeFactor) = rakeFactor;
    *pickaxeEnabled = pickaxeFactor != 1.0f ? 1 : 0;
    *rakeEnabled = rakeFactor != 1.0f ? 1 : 0;
}

struct CodeSection {
    std::uint8_t* start = nullptr;
    std::size_t size = 0;
};

CodeSection FindGameCode(HMODULE game) {
    auto* const imageStart = reinterpret_cast<std::uint8_t*>(game);
    const auto* dosHeader = reinterpret_cast<const IMAGE_DOS_HEADER*>(imageStart);
    const auto* ntHeaders = reinterpret_cast<const IMAGE_NT_HEADERS64*>(imageStart + dosHeader->e_lfanew);
    const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(ntHeaders);
    for (WORD index = 0; index < ntHeaders->FileHeader.NumberOfSections; ++index, ++section) {
        if (std::memcmp(section->Name, ".text", 6) == 0) return {imageStart + section->VirtualAddress, section->Misc.VirtualSize};
    }
    return {};
}

// A page within reach of 32-bit relative jumps from target: searched downward from it, then upward.
std::uint8_t* AllocateCavePageNear(std::uintptr_t target) {
    SYSTEM_INFO systemInfo;
    GetSystemInfo(&systemInfo);
    const std::uintptr_t granularity = systemInfo.dwAllocationGranularity;
    const std::uintptr_t maximumDistance = 0x70000000;
    const std::uintptr_t lowest = target > maximumDistance + granularity ? target - maximumDistance : granularity;
    const std::uintptr_t highest = target + maximumDistance;
    const std::uintptr_t alignedTarget = target & ~(granularity - 1);

    for (std::uintptr_t candidate = alignedTarget - granularity; candidate >= lowest;) {
        MEMORY_BASIC_INFORMATION region{};
        if (VirtualQuery(reinterpret_cast<void*>(candidate), &region, sizeof region) == 0) break;
        if (region.State == MEM_FREE) {
            if (void* page = VirtualAlloc(reinterpret_cast<void*>(candidate), cave_page::kPageSize, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE)) {
                return static_cast<std::uint8_t*>(page);
            }
            candidate -= granularity;
        } else {
            candidate = reinterpret_cast<std::uintptr_t>(region.AllocationBase) - granularity;
        }
    }
    for (std::uintptr_t candidate = alignedTarget + granularity; candidate + cave_page::kPageSize <= highest;) {
        MEMORY_BASIC_INFORMATION region{};
        if (VirtualQuery(reinterpret_cast<void*>(candidate), &region, sizeof region) == 0) break;
        if (region.State == MEM_FREE) {
            if (void* page = VirtualAlloc(reinterpret_cast<void*>(candidate), cave_page::kPageSize, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE)) {
                return static_cast<std::uint8_t*>(page);
            }
            candidate += granularity;
        } else {
            const std::uintptr_t regionEnd = reinterpret_cast<std::uintptr_t>(region.BaseAddress) + region.RegionSize;
            candidate = (regionEnd + granularity - 1) & ~(granularity - 1);
        }
    }
    return nullptr;
}

bool WriteGameCode(std::uintptr_t address, const std::uint8_t* bytes, std::size_t size) {
    DWORD oldProtection = 0;
    if (!VirtualProtect(reinterpret_cast<void*>(address), size, PAGE_EXECUTE_READWRITE, &oldProtection)) return false;
    std::memcpy(reinterpret_cast<void*>(address), bytes, size);
    DWORD unusedProtection = 0;
    VirtualProtect(reinterpret_cast<void*>(address), size, oldProtection, &unusedProtection);
    FlushInstructionCache(GetCurrentProcess(), reinterpret_cast<void*>(address), size);
    return true;
}

// Finds the hook sites, builds the caves and jumps to them. Returns the cave page, or nullptr when nothing was changed.
std::uint8_t* InstallHooks(HMODULE game, const ToolRadiusSettings& settings) {
    const auto gameStart = reinterpret_cast<std::uintptr_t>(game);
    const CodeSection gameCode = FindGameCode(game);
    if (gameCode.start == nullptr) {
        Log("ERROR: the game's code section wasn't found: nothing changed");
        return nullptr;
    }

    const TerraformSites sites = FindTerraformSites(gameCode.start, gameCode.size);
    switch (sites.status) {
    case SiteStatus::Found:
        break;
    case SiteStatus::RadiusCodeAlreadyHooked:
        Log("ERROR: another mod already changed the terraform code (Advanced Terrain Edit's Pickaxe Radius, for one): nothing "
            "changed. Disable that mod, or this one.");
        return nullptr;
    case SiteStatus::RadiusCodeAmbiguous:
        Log("ERROR: the terraform code was found more than once (game update?): nothing changed");
        return nullptr;
    case SiteStatus::VoxelBudgetsNotFound:
        Log("ERROR: the terraform code's voxel budgets aren't where expected (game update?): nothing changed");
        return nullptr;
    case SiteStatus::RadiusCodeNotFound:
    default:
        Log("ERROR: the terraform code wasn't found (game update?): nothing changed");
        return nullptr;
    }

    const auto pickaxeHook = reinterpret_cast<std::uintptr_t>(gameCode.start) + sites.pickaxeHookOffset;
    const bool rakeFound = sites.rakeStatus == RakeSiteStatus::Found;
    const std::uintptr_t rakeHook = rakeFound ? reinterpret_cast<std::uintptr_t>(gameCode.start) + sites.rakeHookOffset : pickaxeHook;

    std::uint8_t* const page = AllocateCavePageNear(pickaxeHook);
    if (page == nullptr) {
        Log("ERROR: no memory near the game's code for the hooks: nothing changed");
        return nullptr;
    }
    const BuiltCaves caves = BuildCaves(reinterpret_cast<std::uintptr_t>(page), pickaxeHook, rakeHook);
    if (!caves.inRange) {
        Log("ERROR: the hooks' memory is out of reach of the game's code: nothing changed");
        VirtualFree(page, 0, MEM_RELEASE);
        return nullptr;
    }

    ApplySettings(page, settings);
    const std::uint64_t rakeReturnAddress = rakeHook + kRakeHookSize;
    const auto rakeCallTarget = static_cast<std::uint64_t>(reinterpret_cast<std::intptr_t>(gameCode.start) + sites.rakeCallTargetOffset);
    std::memcpy(page + cave_page::kRakeReturnAddress, &rakeReturnAddress, sizeof rakeReturnAddress);
    std::memcpy(page + cave_page::kRakeCallTarget, &rakeCallTarget, sizeof rakeCallTarget);
    std::memcpy(page + cave_page::kPickaxeCave, caves.pickaxeCave.data(), caves.pickaxeCave.size());
    std::memcpy(page + cave_page::kRakeCave, caves.rakeCave.data(), caves.rakeCave.size());
    FlushInstructionCache(GetCurrentProcess(), page, cave_page::kPageSize);

    if (WriteGameCode(pickaxeHook, caves.pickaxeSiteJump.data(), caves.pickaxeSiteJump.size())) {
        Log("pickaxe hook active @ exe+0x%llX", static_cast<unsigned long long>(pickaxeHook - gameStart));
    } else {
        Log("ERROR: the pickaxe hook couldn't be written (error %lu)", GetLastError());
    }
    if (sites.rakeStatus == RakeSiteStatus::AlreadyHooked) {
        Log("ERROR: another mod already changed the rake's code (Advanced Terrain Edit, for one): Rake Radius does nothing");
    } else if (!rakeFound) {
        Log("ERROR: the rake's code wasn't found (game update?): Rake Radius does nothing");
    } else if (WriteGameCode(rakeHook, caves.rakeSiteJump.data(), caves.rakeSiteJump.size())) {
        Log("rake hook active @ exe+0x%llX", static_cast<unsigned long long>(rakeHook - gameStart));
    } else {
        Log("ERROR: the rake hook couldn't be written (error %lu)", GetLastError());
    }
    return page;
}

bool SameValues(const ToolRadiusSettings& first, const ToolRadiusSettings& second) {
    return first.pickaxeRadius.value == second.pickaxeRadius.value && first.rakeRadius.value == second.rakeRadius.value;
}

DWORD WINAPI ModThread(void*) {
    StartLog();
    if (!IsGameProcess()) {
        Log("Terrain Tool Radius %s loaded outside enshrouded.exe: nothing changed", kModVersion);
        return 0;
    }
    const HMODULE game = GetModuleHandleW(nullptr);
    const auto* dosHeader = reinterpret_cast<const IMAGE_DOS_HEADER*>(game);
    const auto* ntHeaders = reinterpret_cast<const IMAGE_NT_HEADERS64*>(reinterpret_cast<const std::uint8_t*>(game) + dosHeader->e_lfanew);
    Log("Terrain Tool Radius %s loaded (enshrouded.exe build stamp %08lx)", kModVersion, ntHeaders->FileHeader.TimeDateStamp);

    const std::wstring settingsPath = SettingsPath();
    FileStamp settingsStamp;
    std::string settingsText;
    const bool stampRead = ReadFileStamp(settingsPath, settingsStamp);
    if (!stampRead || !ReadTextFile(settingsPath, settingsText)) {
        Log("extended.mod.json couldn't be read: using the defaults");
        settingsText.clear();
    }
    ToolRadiusSettings settings = ReadToolRadiusSettings(settingsText);
    LogSettings("settings", settings);

    std::uint8_t* const page = InstallHooks(game, settings);
    if (page == nullptr) return 0;

    // Shroudforge writes F9 changes into extended.mod.json; a reading that misses a value (the file caught while it is being
    // written) waits for the next check.
    bool haveSettingsStamp = stampRead;
    while (true) {
        Sleep(kSettingsCheckIntervalMilliseconds);
        FileStamp currentStamp;
        if (!ReadFileStamp(settingsPath, currentStamp) || (haveSettingsStamp && currentStamp == settingsStamp)) continue;
        std::string currentText;
        if (!ReadTextFile(settingsPath, currentText)) continue;
        const ToolRadiusSettings currentSettings = ReadToolRadiusSettings(currentText);
        if (currentSettings.pickaxeRadius.source == SettingSource::Missing || currentSettings.rakeRadius.source == SettingSource::Missing) continue;
        settingsStamp = currentStamp;
        haveSettingsStamp = true;
        if (SameValues(currentSettings, settings)) continue;
        settings = currentSettings;
        ApplySettings(page, settings);
        LogSettings("settings changed", settings);
    }
}

}  // namespace

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(instance);
        modModule = instance;
        modFolder = FolderOf(ModulePath(instance));
        // The game's code jumps into memory this DLL manages for the rest of the session, so it is never unloaded.
        HMODULE pinnedModule = nullptr;
        GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN, reinterpret_cast<LPCWSTR>(&DllMain), &pinnedModule);
        if (const HANDLE thread = CreateThread(nullptr, 0, ModThread, nullptr, 0, nullptr)) CloseHandle(thread);
    }
    return TRUE;
}
