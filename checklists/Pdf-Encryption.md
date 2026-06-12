# Canvas.Pdf Encryption (Password Protection & Permissions)

## Status

**V1 shipped (RC4-128).** `PdfSaveOptions.Encryption` encrypts strings and streams with the Standard
Security Handler (`/V 2 /R 3`). Verified end-to-end: PdfPig opens the output with the password and
recovers the text. **AES-128 (`/V 4 /R 4`) is deferred to V2** — RC4 was chosen first because, as a
stream cipher, it preserves byte length and avoids rewriting every `/Length` in the already-serialized
object bodies. The handler is structured so AES slots in as an additional mode.

## Goal

Add document encryption to `Canvas.Pdf` so generated PDFs can be password-protected with owner/user
passwords and permission flags — the clearest functional gap versus DevExpress PDF
(`PdfEncryptionOptions`). Today [PdfSaveOptions.cs](../Canvas/Pdf/PdfSaveOptions.cs) only exposes
`CompressContentStreams` and `CollectDiagnostics`; there is no encryption anywhere in `Canvas/Pdf/`.

Target: the **Standard Security Handler**. **V1 ships RC4-128 (revision 3, `/V 2 /R 3`)**; AES-128
(revision 4, `/AESV2`) is deferred to V2. AES-256 (PDF 2.0, rev 6) is explicitly out of scope.

## Scope

- [x] V1: encrypt strings and streams in newly generated documents only (Canvas.Pdf is generation-only).
- [x] V1: owner password, user password, permission flags, RC4-128. (AES-128 deferred to V2.)
- [x] Out of scope: AES-256 / PDF 2.0 rev 6, public-key (certificate) security, decrypting/re-saving
      existing PDFs, digital signatures.

## Public API

- [x] New `PdfEncryptionOptions` class:
  - [x] `string? UserPassword` (open-document password; empty = openable without prompt).
  - [x] `string? OwnerPassword` (permissions password; defaults to user password if unset).
  - [x] `PdfPermissions Permissions` (`[Flags]`: Print, Modify, Copy, AnnotateAndFillForms,
        FillFormsOnly, ExtractForAccessibility, Assemble, PrintHighResolution).
  - [x] `PdfEncryptionAlgorithm Algorithm` (`Rc4_128` default; `Aes128` present but throws `NotSupportedException` until V2).
- [x] Extend [PdfSaveOptions.cs](../Canvas/Pdf/PdfSaveOptions.cs) with `PdfEncryptionOptions? Encryption { get; init; }`.
- [x] `PdfDocument.Save(...)` / `ToBytes(...)` already accept `PdfSaveOptions`, so no new overloads needed —
      encryption flows through existing options.

## Crypto building blocks (`Canvas/Pdf/Serialization/Security/`)

- [x] `Rc4.cs` — hand-rolled RC4 (≈15 lines; not in .NET BCL). Used both for RC4 mode and inside the
      rev-3 `/O`/`/U` key algorithms even when the data mode is AES.
- [x] Reuse `System.Security.Cryptography.MD5` from the BCL. (BCL `Aes` reserved for V2.)
- [x] `StandardSecurityHandler.cs` implementing the PDF 32000-1 algorithms:
  - [x] Algorithm 2 — compute the file encryption key from padded user password + `/O` + `/P` + first
        `/ID` element (+ rev-4 metadata byte).
  - [x] Algorithm 3 — compute `/O` from owner & user passwords.
  - [x] Algorithm 4/5 — compute `/U` (rev 2 vs rev 3+).
  - [x] Algorithm 1 — per-object key = MD5(fileKey + objNum(3 bytes) + gen(2 bytes) [+ "sAlT" for AES]),
        truncated to `min(keyLen+5, 16)`; then RC4 or AES-CBC the data.

## Writer integration ([PdfWriter.cs](../Canvas/Pdf/Serialization/PdfWriter.cs))

The writer builds `PdfIndirectObject`s then `Serialize`s them. Encryption must touch **every string and
stream** except the `/Encrypt` dict and the `/ID`. Because object bodies are currently assembled as
opaque strings, this is the most invasive part.

- [x] Add a `/ID` array (two 16-byte hex strings, derived from file metadata/time) to the trailer in
      `Serialize` — **prerequisite**: encryption keys depend on the first `/ID` element. (Currently the
      trailer has `/Size /Root /Info` only.)
- [x] Pass a `StandardSecurityHandler?` (null when no encryption) into `Serialize`, which rewrites each
      object body just before writing — via `EncryptObjectBody(objNum, gen, bytes)`:
  - [x] in non-stream objects, every literal `( ... )` string is RC4'd with the per-object key (escaped
        in/out; RC4 preserves length so xref offsets recomputed from actual write positions stay valid);
  - [x] in stream objects, the payload (located via `/Length`) is RC4'd in place — length unchanged;
  - [x] the `/Encrypt` dictionary object is skipped (not encrypted); the `/ID` lives in the trailer.
- [x] Emit the `/Encrypt` indirect object `<< /Filter /Standard /V 2 /R 3 /Length 128 /P <signed-int>
      /O (...) /U (...) >>` and reference it from the trailer (`/Encrypt n 0 R`). (rev-4 `/CF /AESV2`
      deferred to V2.)

## DevExpress migration wire-back

- [x] Auto-rewrite the common encryption shape: pre-scan `new PdfEncryptionOptions()` +
      `UserPasswordString`/`OwnerPasswordString` assignments + `new PdfSaveOptions { EncryptionOptions = ... }`
      + two-arg `SaveDocument(path, saveOptions)`, drop those source statements, and emit
      `document.Save(path, new PdfSaveOptions { Encryption = new PdfEncryptionOptions { UserPassword = ..., OwnerPassword = ... } })`
      (`CANMIGDEVEXP010`). Also removes consumed `DXFont` declarations (`CANMIGDEVEXP025`).
- [x] Fallback: when encryption is present but not auto-mappable (no two-arg `SaveDocument`), emit the
      `CANMIGDEVEXP024` guidance warning instead.
- [ ] Deferred: map DevExpress `Permissions` enum values to Canvas `PdfPermissions` (passwords map now;
      permissions still need manual translation).
- [x] Update [Code-Migration-DevExpressPdf.md](Code-Migration-DevExpressPdf.md): encryption moves from
      "manual" warning to a real conversion.

## Tests

- [x] `Rc4` round-trip and known-answer vector.
- [x] `StandardSecurityHandler` `/O`, `/U`, file-key against PDF-spec known answers.
- [x] Save with user password → file has `/Encrypt`, `/ID`; strings/streams are not plaintext-readable.
- [ ] Save with AES-128 vs RC4-128 → correct `/V`/`/R`/`/CF` values.
- [x] Permission flags serialize to the correct signed `/P` bit pattern.
- [x] **Interop:** open an encrypted output with an existing reader (e.g. PdfPig / qpdf) using the
      password to confirm real-world decryptability — the key correctness gate.
- [x] Regression: unencrypted save (no `Encryption`) is byte-stable apart from the new `/ID`.
- [x] DevExpress migration: encryption sample converts to `PdfSaveOptions { Encryption = ... }`.

## Verification

1. `dotnet test` for `Canvas.Infrastructure.Pdf.Tests` (or the Canvas.Pdf test project) — crypto + writer.
2. `dotnet build Canvas.sln`.
3. Manual: generate a password-protected PDF, open it in a real viewer, confirm it prompts for the
      password and that permission flags are honoured.

## Resolved decisions

- [x] Primary algorithm tier — **RC4-128 (rev 3) for V1**; AES-128 deferred to V2 (avoids `/Length`
      rewrite on opaque object bodies). AES-256 out of scope.
- [x] `/ID` derivation — `MD5(GUID + UTC ticks + document title)`; both `/ID` elements identical.

## V2 follow-ups

- [ ] AES-128 (`/V 4 /R 4`, `/AESV2`): per-object AES-CBC with random IV, `/CF` crypt filter, and
      `/Length` rewrite for the IV+padding growth.
- [ ] Full DevExpress encryption auto-rewrite (see migration wire-back above).
