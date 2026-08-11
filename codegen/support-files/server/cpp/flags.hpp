// flags.hpp — minimal, opt-in bitmask wrapper for `enum class` types.
// generated with Claude Sonnet 5 2026/08/11
//
// Usage:
//
//     enum class Border : unsigned {
//         None  = 0,
//         Left  = 1 << 0,
//         Top   = 1 << 1,
//         Right = 1 << 2,
//         Bottom= 1 << 3,
//     };
//     DECLARE_FLAGS(Border)                // <-- opt-in, one line, per enum
//
//     Flags<Border> b = Border::Left | Border::Top;
//     b |= Border::Right;
//     if (b & Border::Top) { ... }         // truthy test
//     if (b.test(Border::Top)) { ... }     // explicit test
//     b &= ~Border::Left;                  // clear a bit
//     bool any = static_cast<bool>(b);
//
// DECLARE_FLAGS only unlocks the bitwise operators for that specific enum's
// bare values (Border::Left | Border::Top, ~Border::Left, ...); it does not
// create an alias. You keep spelling the type out explicitly as Flags<Border>
// wherever you use it. Enums that never go through DECLARE_FLAGS are left
// completely untouched — no risk of accidentally making some unrelated
// `enum class` bitwise-operable.

#pragma once

#include <type_traits>

template <typename Enum>
class Flags {
    static_assert(std::is_enum_v<Enum>, "Flags<E> requires an enum type");

public:
    using enum_type       = Enum;
    using underlying_type = std::underlying_type_t<Enum>;

    constexpr Flags() noexcept : m_value(0) {}
    constexpr Flags(Enum e) noexcept : m_value(static_cast<underlying_type>(e)) {}
    constexpr explicit Flags(underlying_type v) noexcept : m_value(v) {}

    // --- bitwise combination -------------------------------------------------
    constexpr Flags operator|(Flags o) const noexcept { return Flags(m_value | o.m_value); }
    constexpr Flags operator&(Flags o) const noexcept { return Flags(m_value & o.m_value); }
    constexpr Flags operator^(Flags o) const noexcept { return Flags(m_value ^ o.m_value); }
    constexpr Flags operator~() const noexcept { return Flags(static_cast<underlying_type>(~m_value)); }

    constexpr Flags& operator|=(Flags o) noexcept { m_value |= o.m_value; return *this; }
    constexpr Flags& operator&=(Flags o) noexcept { m_value &= o.m_value; return *this; }
    constexpr Flags& operator^=(Flags o) noexcept { m_value ^= o.m_value; return *this; }

    // --- testing ---------------------------------------------------------------
    constexpr explicit operator bool() const noexcept { return m_value != 0; }   // any bit set
    constexpr bool operator!() const noexcept { return m_value == 0; }

    // true only if *all* bits of `e` are present (and e itself isn't the zero/None value)
    constexpr bool test(Enum e) const noexcept {
        const auto bits = static_cast<underlying_type>(e);
        return bits != 0 && (m_value & bits) == bits;
    }
    // true if *any* bit shared with `o` is set
    constexpr bool testAny(Flags o) const noexcept { return (m_value & o.m_value) != 0; }

    constexpr underlying_type value() const noexcept { return m_value; }

    constexpr bool operator==(Flags o) const noexcept { return m_value == o.m_value; }
    constexpr bool operator!=(Flags o) const noexcept { return m_value != o.m_value; }

private:
    underlying_type m_value;
};

// Opt-in macro: unlocks the free operators needed so EnumType's bare values
// combine directly (Enum | Enum, ~Enum, Enum | Flags<Enum>, ...). Does NOT
// declare any alias — use Flags<EnumType> explicitly at the point of use.
#define DECLARE_FLAGS(EnumType)                                                           \
    [[maybe_unused]] constexpr Flags<EnumType> operator|(EnumType a, EnumType b) noexcept { \
        return Flags<EnumType>(a) | b;                                                    \
    }                                                                                      \
    [[maybe_unused]] constexpr Flags<EnumType> operator&(EnumType a, EnumType b) noexcept { \
        return Flags<EnumType>(a) & b;                                                    \
    }                                                                                      \
    [[maybe_unused]] constexpr Flags<EnumType> operator^(EnumType a, EnumType b) noexcept { \
        return Flags<EnumType>(a) ^ b;                                                    \
    }                                                                                      \
    [[maybe_unused]] constexpr Flags<EnumType> operator~(EnumType a) noexcept {            \
        return ~Flags<EnumType>(a);                                                       \
    }                                                                                      \
    [[maybe_unused]] constexpr Flags<EnumType> operator|(EnumType a, Flags<EnumType> b) noexcept { \
        return b | a;                                                                     \
    }                                                                                      \
    [[maybe_unused]] constexpr Flags<EnumType> operator&(EnumType a, Flags<EnumType> b) noexcept { \
        return b & a;                                                                     \
    }                                                                                      \
    [[maybe_unused]] constexpr Flags<EnumType> operator^(EnumType a, Flags<EnumType> b) noexcept { \
        return b ^ a;                                                                     \
    }