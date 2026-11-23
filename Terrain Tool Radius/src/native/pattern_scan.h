// Byte patterns written as hex text ("F3 44 0F ?? 01", where ?? is any byte), and searches for them in code bytes.
#pragma once

#include <cstddef>
#include <cstdint>
#include <string_view>
#include <vector>

namespace terrain_tool_radius {

struct BytePattern {
    std::vector<std::uint8_t> bytes;
    std::vector<bool> isWildcard;

    std::size_t Size() const { return bytes.size(); }
};

inline int HexDigitValue(char digit) {
    if (digit >= '0' && digit <= '9') return digit - '0';
    if (digit >= 'A' && digit <= 'F') return digit - 'A' + 10;
    if (digit >= 'a' && digit <= 'f') return digit - 'a' + 10;
    return -1;
}

// Parses space-separated hex bytes; "??" matches any byte. An unreadable token makes the pattern empty, which never matches.
inline BytePattern ParsePattern(std::string_view text) {
    BytePattern pattern;
    std::size_t position = 0;
    while (position < text.size()) {
        if (text[position] == ' ') {
            ++position;
            continue;
        }
        if (position + 1 >= text.size()) return {};
        const char high = text[position];
        const char low = text[position + 1];
        if (high == '?' && low == '?') {
            pattern.bytes.push_back(0);
            pattern.isWildcard.push_back(true);
        } else {
            const int highValue = HexDigitValue(high);
            const int lowValue = HexDigitValue(low);
            if (highValue < 0 || lowValue < 0) return {};
            pattern.bytes.push_back(static_cast<std::uint8_t>(highValue * 16 + lowValue));
            pattern.isWildcard.push_back(false);
        }
        position += 2;
    }
    return pattern;
}

inline bool MatchesAt(const std::uint8_t* data, const BytePattern& pattern) {
    for (std::size_t index = 0; index < pattern.Size(); ++index) {
        if (!pattern.isWildcard[index] && data[index] != pattern.bytes[index]) return false;
    }
    return true;
}

// Every offset in data where the pattern matches.
inline std::vector<std::size_t> FindAll(const std::uint8_t* data, std::size_t size, const BytePattern& pattern) {
    std::vector<std::size_t> matchOffsets;
    if (pattern.Size() == 0 || size < pattern.Size()) return matchOffsets;
    for (std::size_t offset = 0; offset + pattern.Size() <= size; ++offset) {
        if (MatchesAt(data + offset, pattern)) matchOffsets.push_back(offset);
    }
    return matchOffsets;
}

inline bool Contains(const std::uint8_t* data, std::size_t size, const BytePattern& pattern) {
    if (pattern.Size() == 0 || size < pattern.Size()) return false;
    for (std::size_t offset = 0; offset + pattern.Size() <= size; ++offset) {
        if (MatchesAt(data + offset, pattern)) return true;
    }
    return false;
}

}  // namespace terrain_tool_radius
