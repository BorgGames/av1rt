using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// Fixed-size value arrays for C struct members such as `int_mv mv[2]`. They copy by value like
// the C arrays they replace, index with [i] and convert to Span<T>. Add sizes here as needed.
[InlineArray(1)] internal struct Arr1<T> { private T _e0; }
[InlineArray(2)] internal struct Arr2<T> { private T _e0; }
[InlineArray(3)] internal struct Arr3<T> { private T _e0; }
[InlineArray(4)] internal struct Arr4<T> { private T _e0; }
[InlineArray(5)] internal struct Arr5<T> { private T _e0; }
[InlineArray(6)] internal struct Arr6<T> { private T _e0; }
[InlineArray(7)] internal struct Arr7<T> { private T _e0; }
[InlineArray(8)] internal struct Arr8<T> { private T _e0; }
[InlineArray(9)] internal struct Arr9<T> { private T _e0; }
[InlineArray(10)] internal struct Arr10<T> { private T _e0; }
[InlineArray(11)] internal struct Arr11<T> { private T _e0; }
[InlineArray(12)] internal struct Arr12<T> { private T _e0; }
[InlineArray(13)] internal struct Arr13<T> { private T _e0; }
[InlineArray(14)] internal struct Arr14<T> { private T _e0; }
[InlineArray(15)] internal struct Arr15<T> { private T _e0; }
[InlineArray(16)] internal struct Arr16<T> { private T _e0; }
[InlineArray(18)] internal struct Arr18<T> { private T _e0; }
[InlineArray(20)] internal struct Arr20<T> { private T _e0; }
[InlineArray(21)] internal struct Arr21<T> { private T _e0; }
[InlineArray(22)] internal struct Arr22<T> { private T _e0; }
[InlineArray(24)] internal struct Arr24<T> { private T _e0; }
[InlineArray(25)] internal struct Arr25<T> { private T _e0; }
[InlineArray(28)] internal struct Arr28<T> { private T _e0; }
[InlineArray(32)] internal struct Arr32<T> { private T _e0; }
[InlineArray(48)] internal struct Arr48<T> { private T _e0; }
[InlineArray(64)] internal struct Arr64<T> { private T _e0; }
[InlineArray(128)] internal struct Arr128<T> { private T _e0; }
[InlineArray(256)] internal struct Arr256<T> { private T _e0; }
