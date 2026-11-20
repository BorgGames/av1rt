# Testing the port

Two levels, both against libaom itself.

## 1. Differential unit tests (per function)

For DSP kernels, transforms, quantisers, the entropy coder, context derivations and other
self-contained functions, call libaom's C implementation and the C# port with the same
(random and edge-case) inputs and require identical outputs.

1. Write `tools/oracle/shim/shim_<pkg>.c` exporting thin wrappers around the libaom functions
   you need (`__declspec(dllexport)`, plain C types and pointers only). Internal libaom
   functions are reachable because the shim links the static `aom.lib` of the pure-C oracle
   build. File-`static` functions are not reachable: test them through a public caller.
2. Build it: `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/oracle/shim/build-shim.ps1 -Name <pkg>`
   → `.oracle/shims/aomshim_<pkg>.dll` (copied next to the tests on build).
3. Add xunit tests in `tests/Av1rt.Tests/<Area>/`, declaring the imports with `[DllImport("aomshim_<pkg>")]`
   and passing arrays (they are pinned automatically). See `tests/Av1rt.Tests/Shims/SmokeShim.cs`.
4. Run with a private output dir when others may be building:
   `dotnet test tests/Av1rt.Tests/Av1rt.Tests.csproj -p:IntermediateOutputPath=obj/<pkg>/ -p:OutputPath=bin/<pkg>/ --filter <YourTests>`

Use a fixed-seed `System.Random` so failures reproduce. Cover block sizes, strides that are
not the width, extreme pixel values (0/255), and boundary cases.

## 2. End-to-end bit-exactness

`av1rt` and `tools/oracle/av1rt_oracle.exe` encode the same `.y4m` with the same settings; the IVF
payloads must be byte-identical, and the reconstruction must match `ffmpeg -c:v libdav1d`'s decode.
