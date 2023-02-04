module simpleset;

private:

// thanks to: https://gist.github.com/9rnsr/4152297
template isEnum(X...) if (X.length == 1)
{
    static if (is(X[0] == enum))
    {
        enum isEnum = true;
    }
    else static if (!is(X[0]) &&
                    !is(typeof(X[0]) == void) &&
                    !isFunction!(X[0]))
    {
        enum isEnum =
            !is(typeof({ auto ptr = &X[0]; }))
         && !is(typeof({ enum off = X[0].offsetof; }));
    }
    else
        enum isEnum = false;
}

public:

struct Set(T) {
private:
    bool[T] items;
public:
    this(T[] values...) {
        foreach (v; values) {
            items[v] = true;
        }
    }

    size_t length() {
        return items.length;
    }

    // ??
    // this(ref return scope Set!T rhs) {
    //     items = rhs.items.dup;
    // }

    static Set!T empty; // = Set!T([]);

    void clear() {
        items.clear;
    }

    // to avoid errors copying a ref const(Set) to a non-const
    void opAssign(ref const(Set!T) rhs) {
        items = cast(bool[T]) rhs.items.dup;
    }

    void set()(auto ref T rhs) {
        items[rhs] = true;
    }

    void opOpAssign(string op)(T rhs) if (op == "~") {
        set(rhs);
    }

    void set()(T[] toAdd...) {
        foreach (r; toAdd) {
            items[r] = true;
        }
    }

    void clearExcept(T rhs) {
        const present = (rhs in items) != null;
        items.clear;
        if (present) items[rhs] = true;
    }

    void remove(T rhs) {
        items.remove(rhs);
    }

    void opOpAssign(string op)(auto ref T rhs) if (op == "-") { // ie, 'and' ('not' rhs)
        remove(rhs);
    }

    bool contains(T rhs) const {
        return (rhs in items) != null;
    }

    string toString() const {
        import std.conv: to;
        return items.keys.to!string;
    }

    // foreach capability
    int opApply(int delegate(ref const(T)) dg) {
        foreach (ref k; items.byKey) {
            if (dg(k)) return 1;
        }
        return 0;
    }

    T[] array() {
        return items.keys;
    }

    // enum-only stuff ======================================
    static if(isEnum!T) {
        // TODO: choose size based on number of members:
        // 8 bits, 16 bits, etc etc
        // right now hardcoded to uint
        static Set!T fromUint(uint flags) {
            import std.traits: EnumMembers;
            auto result = empty;
            foreach(member; EnumMembers!T) {
                if (flags & member) {
                    result |= member;
                }
            }
            return result;
        }
        uint toUint() {
            uint result;
            foreach(k; items.keys()) {
                result |= k;
            }
            return result;
        }

        // enable bitwise operators only if it's an enum
        void opOpAssign(string op)(T rhs) if (op == "|") { // |=
            set(rhs);
        }
        void opOpAssign(string op)(T rhs) if (op == "&") { // &=
            clearExcept(rhs);
        }
        bool opBinary(string op)(T rhs) const if (op == "&") {
            return contains(rhs);
        }
    }
}