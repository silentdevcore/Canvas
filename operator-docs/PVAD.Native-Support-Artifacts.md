# Native Library Support Artifacts

This operator guide covers private Obfuscar mappings and original portable
debug symbols. It is not customer documentation, a production storage service,
or permission to publish packages. Native license-signing keys and support
archive encryption keys are separate purposes and must never be reused.

## Build And Storage Boundary

`tools/native-licensing/support-artifacts.mjs` seals an explicitly selected
`Mapping.txt` and optional `PVAD.*.pdb` files. It does not crawl a directory,
select standalone source files, archive DLLs, accept arbitrary attachment names, or collect
customer documents. A mapping is mandatory. For every recorded module, the
operator supplies original and protected DLL paths and their expected SHA-256
values; the tool checks both before recording the hashes. Symbols must have a
portable-PDB header and match a recorded assembly name. Generate them in the
same isolated build as the original DLL. Header validation is not an independent
proof of a PDB's debug-directory identity: source-build ownership remains a
trusted pipeline boundary.

Disable embedded sources and SourceLink when producing these original symbols.
The archive tool does not inspect arbitrary portable PDBs for embedded source;
only trusted build outputs may be selected.

The encrypted manifest records product version, source commit, dirty-worktree
flag, unique build ID, Obfuscar version, creation/deletion deadline, file sizes,
and original/protected assembly and support-file checksums. No local input paths
are retained. A dirty build is explicitly distinguishable from a commit-only
rebuild; the DLL hashes identify its actual outputs.

The archive uses a fresh AES-256-GCM key and nonce for each operation, wrapping
the key with the reviewed recipient's RSA public key using OAEP/SHA-256.
RSA keys must be at least 3072 bits. The format, algorithm and recipient
fingerprint are authenticated as additional data. The private recipient key is
needed only for recovery, never for sealing on a build runner. The CLI rejects
private recipient material during sealing. No production key is generated or
registered by this tooling.

Encryption provides confidentiality and detects changes to an existing envelope;
it is **not producer authentication**. Anyone holding the public recipient key
could seal another archive. Record the archive SHA-256 in a separately protected
build/release ledger and require that value for recovery. Never obtain the
expected hash from the same untrusted location as the archive. Use existing
release provenance/signing controls for producer identity; this tool is not
code signing or anti-tamper protection.

## Explicit Operator Commands

Prepare an owner-only working directory outside customer package/feed outputs.
Resolve platform aliases to physical paths first; symlinked input/output paths
are rejected. Set up restrictive Windows ACLs separately: Node's POSIX mode
flags do not establish a Windows access policy.

The seal request has exactly `metadata`, `assemblies`, and `files` properties.
This shape is illustrative, not an issued key, license, or valid production
build. Replace every synthetic value with the exact isolated-build evidence:

```json
{
  "metadata": {
    "buildId": "native-support-example",
    "version": "1.1.0",
    "commit": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "sourceDirty": true,
    "obfuscarVersion": "2.2.50",
    "createdAt": "2026-11-20T12:00:00.000Z",
    "deleteAfter": "2026-12-20T12:00:00.000Z"
  },
  "assemblies": [{
    "name": "PVAD.Licensing.dll",
    "originalPath": "/private-build/original/PVAD.Licensing.dll",
    "protectedPath": "/private-build/protected/PVAD.Licensing.dll",
    "originalSha256": "0000000000000000000000000000000000000000000000000000000000000000",
    "protectedSha256": "1111111111111111111111111111111111111111111111111111111111111111"
  }],
  "files": [
    {"name": "Mapping.txt", "path": "/private-build/protected/Mapping.txt"},
    {"name": "PVAD.Licensing.pdb", "path": "/private-build/original/PVAD.Licensing.pdb"}
  ]
}
```

The one global Mapping file must come from the same **joint** protection run as
all listed modules. Never merge different builds' mappings under one identity.
After reviewing the recipient fingerprint and local input evidence:

```bash
node tools/native-licensing/support-artifacts.mjs seal request.json recipient-public.pem support-archive.json
shasum -a 256 support-archive.json
```

Write the resulting digest to the independent protected build ledger. Archive
output is created exclusively, without overwriting an existing file, using
owner-only POSIX permissions. Store only the sealed envelope in the approved
private storage; retain the recipient key and its recoverable backup separately.
Delete plaintext build work only after successful sealing and a recovery drill.
Do not place an archive in a NuGet package, customer image, source repository,
public symbol feed, or generic CI artifact upload, even though it is encrypted.

Recovery requires the known digest from the independent ledger:

```bash
node tools/native-licensing/support-artifacts.mjs restore support-archive.json recipient-private.pem recovered-support EXPECTED_ARCHIVE_SHA256
```

The target directory must not already exist. All checks, authenticated
decryption, retention validation and content hashes complete before extraction.
The new directory uses mode `0700`, its files `0600` on POSIX. Extraction failures
remove only that newly owned directory, never an existing target. The CLI logs
no file contents, private key material or raw exception details.

Compare the affected DLL against the recovered manifest before diagnosing a
customer build. Original portable PDBs describe **pre-obfuscation** DLLs: they
are not rewritten symbols for protected binaries and must not be advertised as
such. Use the joint map for renamed method/type correlation; complete automated
stack-trace deobfuscation and debugger line remapping are not implemented here.
Delete recovered plaintext promptly after the approved support task.

## Retention And Key Lifecycle

Every archive requires a deletion deadline later than its creation timestamp;
recovery outside that window is refused. This is a local safeguard, not automatic
deletion, a trusted offline clock, or an unalterable retention guarantee. The
storage operator must implement lifecycle deletion, access logging, backup
retention, least-privilege access and periodic recovery drills. Preserve old
recipient keys for their still-retained archives during rotation. Losing a
private key makes its archives unrecoverable; the software has no bypass.

The build and recovery hosts are trusted operator machines. The tool rejects
symlinks and duplicate/traversing names, caps 128 modules, 129 support files and
64 MiB of support payload, and uses exclusive output creation. It does not
establish a sandbox against an attacker concurrently modifying the host's
filesystem. JavaScript memory clearing is best effort, not a secure-erasure
guarantee.

Production recipient selection, protected storage/ACLs, ledger permissions,
backup ownership and retention-policy approval remain deployment gates. No
cloud storage or key-management provider has been selected by this work.

## Synthetic Verification

```bash
node --test tools/native-licensing/support-artifacts.test.mjs
node tools/native-licensing/support-retention-rehearsal.mjs
node tools/native-licensing/joint-package-rehearsal.mjs
```

The dedicated rehearsal builds an isolated real license library and original
portable PDB, runs the pinned Obfuscar, creates an ephemeral synthetic RSA
recipient, seals the mapping and symbols, deletes the plaintext staging copy,
and restores the exact bytes. It checks tamper/expiry failures and finally
removes keys, archive, mappings, symbols and binaries. Only `report.json` and
`cleanup.json` remain. It does not provision a production recipient.

Every joint-package profile also rehearses its complete joint mapping and
records a `supportRetention` result. Its customer-candidate builds intentionally
produce no private symbols, so that path archives the mapping only. A prepared
three-OS CI step runs the separate symbol rehearsal and uploads only reports.
Local execution is not evidence of a successful remote/platform run.
