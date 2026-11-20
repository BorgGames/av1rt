using System.Runtime.CompilerServices;

namespace Av1rt.Aom;

// Fixed-size value arrays for C struct members such as `int_mv mv[2]`. They copy by value like
// the C arrays they replace, index with [i] and convert to Span<T>. Add sizes here as needed.
[InlineArray(1)] internal partial struct Arr1<T> { private T _e0; }
[InlineArray(2)] internal partial struct Arr2<T> { private T _e0; }
[InlineArray(3)] internal partial struct Arr3<T> { private T _e0; }
[InlineArray(4)] internal partial struct Arr4<T> { private T _e0; }
[InlineArray(5)] internal partial struct Arr5<T> { private T _e0; }
[InlineArray(6)] internal partial struct Arr6<T> { private T _e0; }
[InlineArray(7)] internal partial struct Arr7<T> { private T _e0; }
[InlineArray(8)] internal partial struct Arr8<T> { private T _e0; }
[InlineArray(9)] internal partial struct Arr9<T> { private T _e0; }
[InlineArray(10)] internal partial struct Arr10<T> { private T _e0; }
[InlineArray(11)] internal partial struct Arr11<T> { private T _e0; }
[InlineArray(12)] internal partial struct Arr12<T> { private T _e0; }
[InlineArray(13)] internal partial struct Arr13<T> { private T _e0; }
[InlineArray(14)] internal partial struct Arr14<T> { private T _e0; }
[InlineArray(15)] internal partial struct Arr15<T> { private T _e0; }
[InlineArray(16)] internal partial struct Arr16<T> { private T _e0; }
[InlineArray(18)] internal partial struct Arr18<T> { private T _e0; }
[InlineArray(20)] internal partial struct Arr20<T> { private T _e0; }
[InlineArray(21)] internal partial struct Arr21<T> { private T _e0; }
[InlineArray(22)] internal partial struct Arr22<T> { private T _e0; }
[InlineArray(24)] internal partial struct Arr24<T> { private T _e0; }
[InlineArray(25)] internal partial struct Arr25<T> { private T _e0; }
[InlineArray(28)] internal partial struct Arr28<T> { private T _e0; }
[InlineArray(32)] internal partial struct Arr32<T> { private T _e0; }
[InlineArray(48)] internal partial struct Arr48<T> { private T _e0; }
[InlineArray(64)] internal partial struct Arr64<T> { private T _e0; }
[InlineArray(128)] internal partial struct Arr128<T> { private T _e0; }
[InlineArray(256)] internal partial struct Arr256<T> { private T _e0; }
