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

## Finding the first divergence

When an end-to-end case fails, write the C# output to an IVF with the CLI
(`dotnet run --project tools/Av1rt.Cli -c Release -- encode .testdata/<clip> out.ivf <case options>`) and run

    python -I tools/oracle/inspect_diff.py .oracle/ref/<case>.ivf out.ivf [--limit N]

It decodes both streams with libaom's inspection decoder (`.oracle/build-inspect`, a pure-C decoder
built with `-DCONFIG_INSPECTION=1 -DCONFIG_AV1_ENCODER=0`, targets `inspect` and `aomdec`) and prints the
first frame whose header fields or per-4x4 decisions (block size, modes, MVs, references, skip,
transform size/type, filters, CDEF, delta q, segment, bits per superblock) differ. If decisions agree
but bytes differ, the difference is in coefficients or entropy coding: compare reconstructions
(`aomdec --rawvideo -o out.yuv`) or the per-superblock bit counts.
