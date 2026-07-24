#pragma once

// ChatGPT-generated slop!
// consider it a crude placeholder until we have more time to spend cleaning up the whole C++ side of things

#include <compare>
#include <concepts>
#include <type_traits>

namespace flags
{

template<typename E>
concept Enum = std::is_enum_v<E>;

template<Enum E>
class Flags
{
public:
    using enum_type = E;
    using underlying_type = std::underlying_type_t<E>;

    //-------------------------------------------------------------------------
    // Construction

    constexpr Flags() noexcept = default;

    constexpr explicit Flags(E value) noexcept
        : m_bits(static_cast<underlying_type>(value))
    {
    }

    constexpr explicit Flags(underlying_type bits) noexcept
        : m_bits(bits)
    {
    }

    //-------------------------------------------------------------------------
    // Observers

    [[nodiscard]]
    constexpr underlying_type bits() const noexcept
    {
        return m_bits;
    }

    [[nodiscard]]
    constexpr explicit operator bool() const noexcept
    {
        return m_bits != 0;
    }

    [[nodiscard]]
    constexpr bool any() const noexcept
    {
        return m_bits != 0;
    }

    [[nodiscard]]
    constexpr bool none() const noexcept
    {
        return m_bits == 0;
    }

    [[nodiscard]]
    constexpr bool empty() const noexcept
    {
        return m_bits == 0;
    }

    [[nodiscard]]
    constexpr bool test(Flags mask) const noexcept
    {
        return (m_bits & mask.m_bits) != 0;
    }

    [[nodiscard]]
    constexpr bool contains(Flags mask) const noexcept
    {
        return (m_bits & mask.m_bits) == mask.m_bits;
    }

    [[nodiscard]]
    constexpr bool test(E flag) const noexcept
    {
        return test(Flags(flag));
    }

    [[nodiscard]]
    constexpr bool contains(E flag) const noexcept
    {
        return contains(Flags(flag));
    }

    //-------------------------------------------------------------------------
    // Modification

    constexpr Flags& set(E flag) noexcept
    {
        m_bits |= static_cast<underlying_type>(flag);
        return *this;
    }

    constexpr Flags& clear(E flag) noexcept
    {
        m_bits &= ~static_cast<underlying_type>(flag);
        return *this;
    }

    constexpr Flags& toggle(E flag) noexcept
    {
        m_bits ^= static_cast<underlying_type>(flag);
        return *this;
    }

    constexpr Flags& reset() noexcept
    {
        m_bits = 0;
        return *this;
    }

    //-------------------------------------------------------------------------
    // Compound assignment

    constexpr Flags& operator|=(Flags rhs) noexcept
    {
        m_bits |= rhs.m_bits;
        return *this;
    }

    constexpr Flags& operator&=(Flags rhs) noexcept
    {
        m_bits &= rhs.m_bits;
        return *this;
    }

    constexpr Flags& operator^=(Flags rhs) noexcept
    {
        m_bits ^= rhs.m_bits;
        return *this;
    }

    constexpr Flags& operator|=(E rhs) noexcept
    {
        return *this |= Flags(rhs);
    }

    constexpr Flags& operator&=(E rhs) noexcept
    {
        return *this &= Flags(rhs);
    }

    constexpr Flags& operator^=(E rhs) noexcept
    {
        return *this ^= Flags(rhs);
    }

    //-------------------------------------------------------------------------
    // Comparison

    constexpr auto operator<=>(const Flags&) const noexcept = default;

private:
    underlying_type m_bits = 0;
};

//==============================================================================
// Flags / Flags

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator|(Flags<E> lhs, Flags<E> rhs) noexcept
{
    lhs |= rhs;
    return lhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator&(Flags<E> lhs, Flags<E> rhs) noexcept
{
    lhs &= rhs;
    return lhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator^(Flags<E> lhs, Flags<E> rhs) noexcept
{
    lhs ^= rhs;
    return lhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator~(Flags<E> value) noexcept
{
    using U = typename Flags<E>::underlying_type;
    return Flags<E>(static_cast<U>(~value.bits()));
}

//==============================================================================
// Enum / Enum

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator|(E lhs, E rhs) noexcept
{
    return Flags<E>(lhs) | Flags<E>(rhs);
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator&(E lhs, E rhs) noexcept
{
    return Flags<E>(lhs) & Flags<E>(rhs);
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator^(E lhs, E rhs) noexcept
{
    return Flags<E>(lhs) ^ Flags<E>(rhs);
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator~(E value) noexcept
{
    return ~Flags<E>(value);
}

//==============================================================================
// Mixed

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator|(Flags<E> lhs, E rhs) noexcept
{
    lhs |= rhs;
    return lhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator|(E lhs, Flags<E> rhs) noexcept
{
    rhs |= lhs;
    return rhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator&(Flags<E> lhs, E rhs) noexcept
{
    lhs &= rhs;
    return lhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator&(E lhs, Flags<E> rhs) noexcept
{
    rhs &= lhs;
    return rhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator^(Flags<E> lhs, E rhs) noexcept
{
    lhs ^= rhs;
    return lhs;
}

template<Enum E>
[[nodiscard]]
constexpr Flags<E> operator^(E lhs, Flags<E> rhs) noexcept
{
    rhs ^= lhs;
    return rhs;
}

//==============================================================================
// Comparison with single enum

template<Enum E>
[[nodiscard]]
constexpr bool operator==(Flags<E> lhs, E rhs) noexcept
{
    return lhs.bits() == static_cast<typename Flags<E>::underlying_type>(rhs);
}

template<Enum E>
[[nodiscard]]
constexpr bool operator==(E lhs, Flags<E> rhs) noexcept
{
    return rhs == lhs;
}

template<Enum E>
[[nodiscard]]
constexpr bool operator!=(Flags<E> lhs, E rhs) noexcept
{
    return !(lhs == rhs);
}

template<Enum E>
[[nodiscard]]
constexpr bool operator!=(E lhs, Flags<E> rhs) noexcept
{
    return !(rhs == lhs);
}

} // namespace util