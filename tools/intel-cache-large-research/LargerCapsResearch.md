# Historical tracing notes and experimental delivery

Earlier sections describe the investigation at that stage. The delivery section below supersedes statements that the experimental catalogue has no 8/16 GiB options. The separate normal branch remains unchanged.

# Limits above 4 GiB: research findings (2026-10-13)

Status: isolated instruction tests passed; NOT an enabled app feature and NOT a validated cache-capacity increase. No game, installed driver, private setup, registry, or enforcement setting was changed for this investigation.

## Image qualification

Intel Xe2 DLL 32.0.101.9034, 42,440,832 bytes. Original SHA-256:
`1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229`.
All RVAs below refer only to this exact image. Offset-only matches on other images are insufficient.

## Trace

- Constructor RVA 0x18C850 initializes configuration +0x08 to 4 GiB and +0x10 to 512 MiB. Five direct call candidates were located and their enclosing functions disassembled. Indirect callers were not exhaustively enumerated.
- RVA 0x17DA20 copies both fields as qwords into +0x58/+0x60. Cache base constructor 0x181030 calls 0xA12B0 to copy configuration into object +0x940, preserving both as object +0x948/+0x950.
- RVA 0x17A441 reads the running total at object +0xBE0, adds 0xB8 and the incoming size, then compares the result as unsigned 64-bit against +0x948. The original JAE rejects equality and larger values. This is a separate aggregate-budget check. Its precise full subsystem semantics and all alternate paths are not established; do not automatically change it.
- RVA 0x17AE08 checks +0xBE0 and incoming serialized size against +0x950 using 64-bit unsigned comparisons. RVA 0x17B573 compares a file size against +0x950.
- RVA 0x17CE60 uses GetFileSizeEx and returns the 64-bit result. File I/O helpers 0x17CA40 and 0x17CCF0 split their 64-bit offset into low/high DWORDs in an OVERLAPPED structure, and add each transferred DWORD count back to the 64-bit position. DWORD per-call transfer counts are not evidence of a 4 GiB whole-file limit.

## Isolated tests

The research-only 8/16 GiB samples alter only the original 24-byte constructor region at raw 0x18BC5C. They store the original neighboring 4 GiB field, shift RAX to 8/16 GiB, store the cap, and preserve initialization of configuration +0. Full-image normalization reproduces the qualified original byte for byte. No code cave or external branch was added.

LargerCapConstructorProbe.cpp reuses the existing constructor harness. Its external helper is replaced by RET exactly as in the original/2/4 GiB probe. It confirms 8/16 GiB output, original 4 GiB neighbor, return=this, and identical other initialized/untouched bytes. It does not execute the cache subsystem or load a driver.

CacheBudgetProbe.cpp executes the exact arithmetic/comparison bytes lifted from RVA 0x17A441..0x17A458, with an ABI-preserving wrapper. Five cases pass: just below the boundary succeeds; equality fails; 6 GiB against a 4 GiB neighbor fails; 6 GiB against an 8 GiB neighbor passes; aggregate 3+2 GiB against 4 GiB fails. This proves the local check, not a complete cache-loading path.

The published production Engine rejects both research-only samples with Unsupported cap encoding. Neither sample is installed or offered in the app.

## What remains before an 8 GiB option

1. Trace ownership/lifetime and every relevant read/write of the aggregate counter and budget, including pointer aliases, indirect dispatch and cold paths. Establish the effect of raising the neighboring field on memory use; do not assume disk and RAM budgets are interchangeable.
2. Review serialization, allocation, eviction, reload and file transfer paths for truncation/overflow. The 64-bit paths found above do not establish every path.
3. Build and qualify a complete proposed patch with exact-image validation, generated output hash, failure tests and recovery. Pin only supported images. Preserve Windows enforcement.
4. Observe the patched module in an explicitly approved private test, then demonstrate valid cache entries and successful reload beyond 4 GiB. Game startup alone is insufficient. Synthetic padding or unrelated shared cache growth is not evidence.

Conclusion: 4 GiB is not the instruction encoding ceiling. Larger values can be initialized, but a second 4 GiB aggregate check makes a cap-only increase insufficient evidence of useful larger capacity. Successful runtime behavior above 4 GiB remains unverified.

## Follow-up: reload boundary and RAM cost

The subsequent trace qualifies the secondary +0x948 check more precisely: RVA 0x17A370 performs file-backed append/update work through the CacheFile object at +0xC10. +0xBE0 increases by bytes written at 0x17A523/0x17A564. This supports identifying +0x948 as an append/aggregate storage budget in this path; it was premature to label it exclusively a RAM or cache-loading budget. Other paths still require review.

RVA 0x17B4E0 reloads the cache file:
- GetFileSizeEx through 0x17CE60 returns the whole file size as 64-bit RBX.
- 0x17B5CD passes RBX to allocator wrapper 0xA1A80. That wrapper requests that full size through 0xFDB570 (jump to allocation routine 0xFDB0F0) and zeroes the same size through 0x101A6E0.
- 0x17B613 passes the full size in R8 to read helper 0x17CCF0. This helper issues ONE ReadFile call and has no successful-read loop. ReadFile interprets the byte-count argument as DWORD. It returns a DWORD count which the helper zero-extends and returns. The caller at 0x17B628 compares the returned count against the original 64-bit file size and branches to the failure path on mismatch.

This is a real whole-file boundary problem, not merely the normal DWORD size of one chunk. The older observation that 64-bit offsets were supported remains true but does not make this whole-file read safe.

### Native evidence

CacheReadBoundaryProbe.cpp lifts all 204 bytes of the original read helper from the qualified image. Its RIP-relative cookie value and import calls are relocated into private test memory; the external cookie check is replaced by RET; external failure branches are redirected to a local trap. ReadFile is replaced by a successful simulated read that records the DWORD request and OVERLAPPED offsets without accessing the buffer or any file. Unexpected error/wait branches terminate the fixture. No driver is loaded.

Six cases pass while reproducing the limitation:
- 4 GiB minus one: complete DWORD-sized request.
- Exactly 4 GiB: API request and returned count zero.
- 4 GiB plus 4096 bytes: API request and return 4096.
- 6 GiB: API request and return 2 GiB.
- 8 GiB: API request and return zero.
- 6 GiB at file position 5 GiB plus 77: original high offset is preserved, while the request still truncates to 2 GiB.

CacheAllocationProbe.cpp lifts the 67-byte original allocator wrapper, redirecting allocation/zeroing calls to recording stubs. Four cases (1/4/8/16 GiB) prove it forwards the whole size to both calls and preserves its return/object behavior. It does not allocate or touch those large buffers. A higher configured cap does not allocate the cap immediately; a file reaching that size and entering this reload path would request the large temporary buffer. Peak process RAM also includes other live buffers and cannot be inferred from this fixture.

ChunkedCacheReadPrototype.cpp is an isolated C++ algorithm, NOT a driver patch. It caps requests at 256 MiB and advances 64-bit file/destination offsets. Eight tests pass: complete simulated 4/8/16 GiB reads; partial successful reads; premature EOF; I/O failure; offset overflow; invalid transfer count. The fixture uses no real I/O or large buffers. It proves the proposed loop algorithm only; it does not solve the original whole-file allocation or parser lifetime requirements.

Microsoft ReadFile documentation confirms the DWORD count contract:
https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-readfile

### Revised implementation requirement

An 8 GiB app option requires more than a constructor/budget patch: the reload implementation must support multiple reads, retain correct asynchronous completion and exception behavior, and address or explicitly accept whole-file memory cost. A bounded-memory solution needs a parser/lifetime review before choosing streaming or file mapping. The serializer/parser may retain pointers into the contiguous buffer, so switching to streaming cannot be assumed safe.

Production engine/catalogue and working game setups remain unchanged. Larger samples remain rejected. No cache growth/reload beyond 4 GiB has been verified with real shader entries. These findings do not attribute the earlier Ready or Not startup problem to cache size: its separate private-routing issue was repaired and no large cache file was demonstrated there.

## Experimental branch and actual bounded I/O test

The research files now live on the separate `intel-cache-large-experimental` branch, under `tools/intel-cache-large-research/`. The base is `4b9cd3e`. Pending WinUI/proxy/CLI work in the primary checkout was not committed or copied to this branch.

The native bounded-reader test read and verified every byte of a real sparse 8 GiB + 4096-byte file. Its reader buffer is 1 MiB (plus a separate 1 MiB expected-data test buffer), independent of file length. Marker data crosses 4 GiB and appears above 8 GiB. The test file occupied 262,144 bytes on disk after flushing and was deleted on close; no game/cache/system file was accessed. Thirteen checks passed including cancellation, partial transfers, consumer stop, EOF, I/O errors, overflow and cleanup. Pending Win32 I/O is completed before buffer reuse using one reusable completion event.

This test eliminates a basic Windows-I/O feasibility question. It does not implement Intel parser integration, prove real shader cache growth, or establish shader entry pointer lifetimes. The app's accepted cap catalogue is unchanged.

## Testable experimental delivery (2026-10-13)

The branch now contains the WinUI app source, cloned from the pending cache-tool work in the normal checkout without unrelated CLI/proxy changes. The experimental executable is ArcCache.Experimental.exe. It offers 8/16 GiB with explicit experimental/RAM consent, known-image-only validation and private-copy rollback. Those experimental changes do not affect the normal branch's source or catalogue.

The implementation deliberately preserves contiguous allocation and parser ownership. It therefore does not claim bounded RAM use. It raises both constructor fields and replaces only the full-cache read call with an appended assembly loop that calls the original read helper for <=256 MiB at a time. The original async/error handling is reused. MASM generated code/unwind data; the existing exception table is copied unchanged and extended by one sorted RUNTIME_FUNCTION; a valid PE checksum is computed. Full normalization restores the original SHA; exact deterministic payload output hashes are pinned:

- 8 GiB: BF74F45935E6BEB8B958728F96D49E7384C99ACEE0B82247981A1FC820C7405C
- 16 GiB: D7B7D5092232165AA2B37A181FD0940C5167C2EEFD1D696FCAC83C1159CB3132

Eight native wrapper checks pass, including partial/zero transfers and Windows exception unwinding. Nineteen patch checks pass, including byte-identical 4 GiB/stock rollback, unrelated/payload/unwind mutation rejection and Windows PE mapping/function metadata lookup without driver execution. Independent PE parsing confirms checksum and sorted table preservation. A separate temporary full-cohort install test passes six checks for 8 GiB install, ACL equivalence, before/after DLL+receipt backups, 4 GiB reversion and stock removal. No actual game installation was changed in these tests.

A subsequent native D3D12 probe DID execute the experimental Intel user-mode driver from its own .exe.local test folder. B580 was explicitly selected; no UHD fallback was allowed. Both the 8 GiB and 16 GiB copies loaded at the expected private path, with independently confirmed disk hashes, and D3D12CreateDevice plus CreateComputePipelineState returned S_OK. The probe compiled one trivial compute shader and did not dispatch GPU work or launch a game. This may create a small cache entry for the probe itself. No driver/system file or enforcement setting was changed. Initial path matching incorrectly treated Windows' .Local casing as different; this was corrected to exact case-insensitive path comparison and the probe rerun successfully.

This establishes device/pipeline startup compatibility for the probe, not game compatibility, large-file shader serialization, cache growth beyond 4 GiB or real large-cache reload correctness. Those remain unverified. Whole-file allocation and memory pressure remain explicit limitations.

The read-only desktop overlay now accepts any detected game executable rather than a hardcoded Witcher process name. It reports process private/working-set memory, observed driver path/qualified disk cap, shared Intel cache disk size/net growth and read failures. It uses 2-second samples and 10-second module observations; a qualified disk cap is cached once per observed module path. No injection/hooks/elevation. Borderless/windowed games are the supported display case; arbitrary exclusive fullscreen compatibility is not claimed.

## Driver-review and Xe3 follow-up

Implemented active-device inventory, metadata update detection and explicit compatibility review. Simulated update UI QA confirms Apply=false, private Launch=false, stock Recovery=true and a visible Check new DLLs action. The saved cap is retained rather than reported as stock. The simulation changes UI state only; registry/driver/game files were not changed. Ten catalogue tests pass, including a matching signature in a tampered image remaining blocked. Read-only worker requests with mutation consent are also refused.

Xe3 original image: SHA-256 029E091F5B67A7418B39900E2AC4EAC9EF64767595E0138FF6569CC42FEB4911; size 42,191,848 bytes; constructor raw 0x1900E0, RVA 0x190CE0; cap immediate raw 0x1900F0. One executable-section signature match. Independent disassembly confirms the 512 MiB qword cap, 4 GiB neighbor and constructor boundaries. Constructor-only samples:
- 2 GiB SHA-256 A9ADC9BEB2C9B3C4A0CADA7C057DA16F3EF866C0BDE0B1CA44F965C79FC5E686
- 4 GiB SHA-256 C9B043204D7BA788B3E3C1C6FAD1BF048CAB548499BB79F3CBBA616E33224A8D

Both samples pass isolated constructor state tests. No Xe3 DLL was loaded as a graphics driver and no game was patched with Xe3. Current hardware enumeration exposes only B580 (Xe2). Xe3 hardware/runtime, full dependency cohort, downstream cache capacity/eviction and larger reload patch locations remain unqualified. The app reports Xe3ConstructorReviewedHardwareUnverified and PatchEnabled=false.

Primary hardware references:
https://www.intel.com/content/www/us/en/products/sku/241598/intel-arc-b580-graphics/specifications.html
https://cdrdv2-public.intel.com/872188/872188-001.pdf
