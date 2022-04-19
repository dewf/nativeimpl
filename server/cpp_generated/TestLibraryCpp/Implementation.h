#include "NativeImplServer.h"
#include <functional>
#include <memory>
#include <string>
#include <vector>
#include <map>

struct CoolEnum {
public:
	enum class Tag {
		Value1,
		Value2,
		Value3
	};
	const Tag tag;
	struct Value1 {
		static CoolEnum make() {
			CoolEnum ret{ Tag::Value1 };
			ret.value1 = std::shared_ptr<Value1>(new Value1());
			return ret;
		}
	};
	struct Value2 {
		const int x;
		static CoolEnum make(int x) {
			CoolEnum ret{ Tag::Value2 };
			ret.value2 = std::shared_ptr<Value2>(new Value2{ x });
			return ret;
		}
	};
	struct Value3 {
		const std::string m;
		const std::vector<int> things;
		static CoolEnum make(std::string m, std::vector<int> things) {
			CoolEnum ret{ Tag::Value3 };
			ret.value3 = std::shared_ptr<Value3>(new Value3{ m, things });
			return ret;
		}
	};
	std::shared_ptr<Value1> value1;
	std::shared_ptr<Value2> value2;
	std::shared_ptr<Value3> value3;
};

int someFunction(CoolEnum woot);
