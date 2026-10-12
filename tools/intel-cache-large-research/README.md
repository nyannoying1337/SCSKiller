# Larger Intel cache: isolated research

Branch: `intel-cache-large-experimental`. This checkout starts from SCSKiller commit `4b9cd3e` and contains only this research folder. The working WinUI app, its pending UI changes, driver catalogue, and game installations are untouched. There is no larger-cap installer, injection DLL, or enabled 8/16 GiB app option here.

## Implemented

- `BoundedCacheReader.h`: native C++ Windows reader with 64-bit offsets and bounded buffers. One reusable completion event; pending I/O completes before buffer reuse. Cancellation is checked between chunks. Premature EOF, I/O failure, invalid transfer counts, invalid bounds and consumer stops are explicit results. File handles are borrowed, never closed by the reader.
- `BoundedCacheReaderTests.cpp`: real sparse-file test of an 8 GiB + 4096-byte file, with sentinels crossing the 4 GiB boundary. Every byte is verified with a 1 MiB reader buffer and a separate 1 MiB expected-data buffer. The temporary file uses delete-on-close and cleanup is verified. No driver is loaded or patched. The test declines to create a large non-sparse file when sparse support is unavailable.
- Original-byte probes and `LargerCapsResearch.md`: evidence for the cap constructor, separate budget check, whole-file allocation and DWORD read truncation. Those probes intentionally stub external calls and do not exercise a complete driver.

## Run the reader tests

Use an x64 MSVC developer command prompt and run `build-reader-tests.cmd` from this folder. The script also tries the installed VS2022 BuildTools default path if `cl` is absent. Generated executables and objects stay in ignored `bin/`. Tests need a Windows temporary directory on a filesystem supporting sparse files. No administrator rights or enforcement changes are required.

Verified locally on 2026-10-13: 13 checks pass; 8,589,938,688 bytes read in 8193 chunks, sparse disk allocation 262,144 bytes. This is synthetic file I/O evidence, not shader-cache capacity evidence.

## Integration requirements

The current Intel reload path allocates/zeroes one buffer for the entire cache file and issues a single DWORD-sized ReadFile request. Fixing only the constructor or only the read request does not fix both problems.

The bounded reader's callback may use its buffer only during that callback. Intel's serializer/parser may retain pointers into the original contiguous buffer. Inspect parser constructors, entry lifetimes, shared ownership, asynchronous servicing, exceptions and eviction before replacing that buffer. Choose owned chunks, streaming or a read-only mapping only after establishing the lifetime contract. Mapping also does not guarantee low process working-set use.

Any eventual binary patch requires exact original hash and instruction qualification, complete code/unwind/relocation review, failure recovery, private-only opt-in test installation, module verification, and real shader entries that survive reload above 4 GiB. Do not bypass Windows enforcement or anti-cheat. No game compatibility or larger-cache claim follows from these prototypes.
