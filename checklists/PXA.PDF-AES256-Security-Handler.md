# PXA PDF AES-256 and PDF 2.0 Security Handler

## Purpose

PXA still ships RC4-128 through its normal Generator path. `PdfEncryptionAlgorithm.Aes128`
exists but throws. The typed Importer security layer can now write and open AES-256 revision-6
documents, but this checklist is not complete until Generator, Designer, Viewer, API, and
independent interoperability work use that layer safely. This contributes to the P1 AES-256
section of `PXA.PDF-Trust-Foundation.md`; it does not complete that section.

## Why not extend the existing handler

`PXA.Pdf`'s `StandardSecurityHandler` encrypts already-serialized object bodies by locating
literal strings and stream payloads in the emitted bytes. That is why AES was deferred: RC4 is
a stream cipher and preserves length, while AES does not, so every `/Length` would have to be
rewritten in text that has already been produced.

The typed object model and serializer added for the full-rewrite save strategy remove that
constraint. Encrypting at the object-model level, before serialization, lets the serializer
compute `/Length` from the ciphertext it is handed. The new handler therefore lives in
`PXA.Importer` alongside the model, and serves both directions: encrypting on write and
decrypting on parse.

## Cryptographic Rules

- [x] Use platform cryptography only. Do not hand-roll AES, SHA-2, random generation, or
  secret comparison.
- [x] Compare password hashes and permission blocks in constant time.
- [x] Generate salts, initialization vectors, and file encryption keys with a
  cryptographically secure generator.
- [x] Clear derived key material after use where the runtime allows it.
- [x] Never log, return, or place a password or file key in a diagnostic, report, or audit
  record.

## Revision 6 (PDF 2.0, AES-256)

- [x] Implement Algorithm 2.B, the hardened hash, exactly: the 64-round minimum, the
  SHA-256/384/512 selection by modulus, and the trailing-byte termination rule.
- [x] Implement Algorithm 8 to compute `/U` and `/UE` from the user password.
- [x] Implement Algorithm 9 to compute `/O` and `/OE`, binding the owner hash to `/U`.
- [x] Implement Algorithm 10 to compute `/Perms` from the permission bits, the metadata flag,
  and the file key.
- [x] Implement Algorithm 2.A to authenticate a user or owner password and recover the file
  encryption key.
- [x] Verify `/Perms` after authentication and treat a mismatch as tampering.
- [x] Fail closed when `/Perms` validation fails; never expose decoded permission bits as
  trusted permissions after an integrity failure.
- [x] Encrypt and decrypt strings and streams with AES-256-CBC, a per-value random
  initialization vector, and PKCS#7 padding.
- [x] Use the file encryption key directly for objects, without the revision-4 per-object
  derivation, as revision 6 requires.
- [x] Write `/V 5 /R 6`, the `/CF` crypt filter with `/AESV3`, and `/StmF` and `/StrF`.
- [x] Honour `/EncryptMetadata`.
- [x] Resolve `/StrF`, `/StmF`, and `/EFF` and accept only selected `/AESV3` or `/Identity`
  filters; reject malformed, unknown, or explicit stream `/Crypt` configurations.

## Passwords

- [x] Normalize passwords as the selected revision requires and document precisely which
  subset of the profile is implemented rather than claiming full conformance.
- [x] Truncate to the revision's maximum length after encoding, not before.
- [x] Accept an empty user password, which yields a document that opens without a prompt.
- [x] Require a distinct owner password whenever permissions are restricted, including when
  the user password is empty.

## Reading Encrypted Documents

- [x] Decrypt strings and streams during parsing so an encrypted document can be inspected.
- [x] Decrypt object streams before expanding compressed objects, while leaving cross-reference
  streams and trailer identifiers untouched.
- [x] Report which password opened the document and the decoded permissions.
- [x] Leave the `/Encrypt` dictionary and the `/ID` unencrypted, and never decrypt values
  inside the `/Encrypt` dictionary.
- [x] Do not write decrypted content to disk.

## Writing Encrypted Documents

- [x] Encrypt through the full-rewrite save strategy, which already recomputes lengths.
- [x] Refuse to encrypt a signed document, since encrypting changes every signed byte.
- [ ] Make AES-256 the normal Generator and WebApi export path, then move RC4 behind an explicit
  legacy compatibility choice. `PXA.Pdf` and `DesignJsonMapper` still default to RC4 today.

## Deferred

- [ ] AES-128 (`/V 4 /R 4`, `/AESV2`) as an interoperability bridge.
- [ ] Decrypting legacy RC4 and AES-128 documents.
- [ ] Certificate and public-key security handlers.
- [ ] Designer Page Settings and export-flow changes.
- [ ] Generator and WebApi integration using the typed AES-256 writer.
- [ ] Independent validation against Acrobat, qpdf, and a third library.

## Verification

- [x] Round-trip: encrypt, reparse, authenticate with the user password, and recover the
  original strings and stream payloads.
- [x] Authenticate with the owner password and recover the same file key.
- [x] Reject a wrong password without revealing which of the two failed.
- [x] Detect a tampered `/Perms` block.
- [x] Reject unsupported crypt-filter selections and preserve valid `/Identity` selections.
- [x] Open an encrypted object stream before expanding its compressed objects.
- [x] Prove a failed decrypt does not leave a partially decrypted object graph.
- [x] Prove ciphertext differs across runs for the same input, since salts and IVs are random.
- [x] Prove no plaintext string or stream payload survives in the encrypted bytes.
- [x] Validate one generated PDF independently with Poppler `pdfinfo`; it reports PDF 2.0,
  AES-256, and the expected print/copy/change permissions.
- [x] Validate the current foundation with `dotnet build PXA.sln`, 346 Importer tests,
  450 passing API tests with 65 environment-gated skips, Documentation tests/build,
  release-fragment validation, and `git diff --check`.
- [ ] Run `dotnet build PXA.sln`, the Importer and API suites, and `git diff --check` after
  Generator/API integration and independent interoperability fixtures are complete.
