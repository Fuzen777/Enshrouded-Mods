// Reads the two F9 settings from the mod's installed extended.mod.json, where Shroudforge stores the values set in its menu.
#pragma once

#include <charconv>
#include <cmath>
#include <optional>
#include <string_view>

namespace terrain_tool_radius {

struct SettingRule {
    std::string_view key;
    std::string_view label;
    double defaultValue;
    double minimum;
    double maximum;
};

// Keep in step with src\terrain-tool-radius\extended.mod.json.
inline constexpr SettingRule kPickaxeRadiusRule{"pickaxeRadiusMultiplier", "Pickaxe Radius", 3.0, 1.0, 5.0};
inline constexpr SettingRule kRakeRadiusRule{"rakeRadiusMultiplier", "Rake Radius", 4.0, 1.0, 4.0};

enum class SettingSource {
    File,        // read from the file as it is
    Clamped,     // read from the file, outside the setting's range, so brought back to its nearest end
    Missing,     // no readable number in the file, so the default is used
};

struct SettingValue {
    double value = 0.0;
    SettingSource source = SettingSource::Missing;
    double fileValue = 0.0;  // what the file said, for SettingSource::Clamped
};

struct ToolRadiusSettings {
    SettingValue pickaxeRadius;
    SettingValue rakeRadius;
};

inline std::size_t SkipWhitespace(std::string_view text, std::size_t position) {
    while (position < text.size() && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) {
        ++position;
    }
    return position;
}

// A number field of the setting object named key: "key": { ..., "value": 3.5, ... }.
inline std::optional<double> FindSettingField(std::string_view json, std::string_view key, std::string_view field) {
    std::size_t searchFrom = 0;
    while (true) {
        const std::size_t keyStart = json.find(key, searchFrom);
        if (keyStart == std::string_view::npos) return std::nullopt;
        searchFrom = keyStart + key.size();
        if (keyStart == 0 || json[keyStart - 1] != '"' || searchFrom >= json.size() || json[searchFrom] != '"') continue;

        std::size_t position = SkipWhitespace(json, searchFrom + 1);
        if (position >= json.size() || json[position] != ':') continue;
        position = SkipWhitespace(json, position + 1);
        if (position >= json.size() || json[position] != '{') continue;

        // The end of this object, counting nested objects and skipping strings.
        const std::size_t objectStart = position;
        std::size_t objectEnd = std::string_view::npos;
        int depth = 0;
        bool inString = false;
        for (std::size_t index = objectStart; index < json.size(); ++index) {
            const char character = json[index];
            if (inString) {
                if (character == '\\') ++index;
                else if (character == '"') inString = false;
                continue;
            }
            if (character == '"') inString = true;
            else if (character == '{') ++depth;
            else if (character == '}' && --depth == 0) {
                objectEnd = index;
                break;
            }
        }
        if (objectEnd == std::string_view::npos) return std::nullopt;

        const std::string_view object = json.substr(objectStart, objectEnd - objectStart);
        std::size_t fieldKey = object.find(field);
        while (fieldKey != std::string_view::npos &&
               (fieldKey == 0 || object[fieldKey - 1] != '"' || fieldKey + field.size() >= object.size() || object[fieldKey + field.size()] != '"')) {
            fieldKey = object.find(field, fieldKey + 1);
        }
        if (fieldKey == std::string_view::npos) return std::nullopt;
        position = SkipWhitespace(object, fieldKey + field.size() + 1);
        if (position >= object.size() || object[position] != ':') return std::nullopt;
        position = SkipWhitespace(object, position + 1);

        double number = 0.0;
        const char* numberEnd = object.data() + object.size();
        const auto [parsedEnd, error] = std::from_chars(object.data() + position, numberEnd, number);
        if (error != std::errc() || !std::isfinite(number)) return std::nullopt;
        return number;
    }
}

inline SettingValue ReadSetting(std::string_view json, const SettingRule& rule) {
    SettingValue setting;
    const std::optional<double> fileNumber = FindSettingField(json, rule.key, "value");
    if (!fileNumber) {
        setting.value = rule.defaultValue;
        setting.source = SettingSource::Missing;
        return setting;
    }
    setting.fileValue = *fileNumber;
    if (*fileNumber < rule.minimum || *fileNumber > rule.maximum) {
        setting.value = *fileNumber < rule.minimum ? rule.minimum : rule.maximum;
        setting.source = SettingSource::Clamped;
        return setting;
    }
    setting.value = *fileNumber;
    setting.source = SettingSource::File;
    return setting;
}

inline ToolRadiusSettings ReadToolRadiusSettings(std::string_view json) {
    return {ReadSetting(json, kPickaxeRadiusRule), ReadSetting(json, kRakeRadiusRule)};
}

}  // namespace terrain_tool_radius
