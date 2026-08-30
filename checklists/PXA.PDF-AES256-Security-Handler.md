# PXA PDF AES-256 and PDF 2.0 Security Handler

## Purpose

PXA defaults new encrypted Generator, Designer, and WebApi output to AES-256 revision 6.
`Aes128` is an explicit interoperability bridge and `Rc4_128` remains an explicit legacy choice.
The typed Importer opens revision-6 AES-256, revision-4 AES-128, and revision-3 RC4 documents
without writing decrypted content to disk. PXA also supports X.509 recipient encryption through
the interoperable Adobe.PubSec `adbe.pkcs7.s5` AESV2 profile.

## Why not extend the existing handler

`PXA.Pdf`'s `StandardSecurityHandler` encrypts already-serialized object bodies by locating
literal strings and stream payloads in the emitted bytes. That is why AES was deferred: RC4 is
a stream cipher and preserves length, while AES does not, so every `/Length` would have to be
rewritten in text that has already been produced.

The typed object model and serializer added for the full-rewrite save strategy remove that
constraint. Generator security handlers encrypt before final serialization so stream lengths can
be recomputed. The Importer has matching, read-only security handlers that decrypt parsed objects
transactionally and leave the source bytes untouched.

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
- [x] Apply the complete RFC 4013 profile used by revision 6: mappings, Unicode 3.2
  prohibited/unassigned tables, NFKC, bidirectional checks, UTF-8, and the 127-byte cap.

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
- [x] Make AES-256 the normal Generator and WebApi export path, then move RC4 behind an explicit
  legacy compatibility choice.
- [x] Mutate password-protected input in memory and re-encrypt it as one fresh revision;
  require the owner password and explicit replacement encryption settings.
- [x] Fail closed when source credentials or output encryption are missing, and keep
  encrypted incremental append unsupported until it can preserve its security semantics.

## Compatibility Profiles

- [x] AES-128 (`/V 4 /R 4`, `/AESV2`) as an interoperability bridge.
- [x] Decrypting legacy RC4 and AES-128 documents.
- [x] Certificate and public-key security handlers using Adobe.PubSec `adbe.pkcs7.s5`, CMS
  recipient envelopes, and AESV2 object encryption.
- [x] Designer Page Settings and export-flow changes.
- [x] Generator and WebApi integration using PDF 2.0 AES-256 output.
- [x] Independent validation against Acrobat, qpdf, and a third library.

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
- [x] Test SASLprep mappings, prohibited and unassigned characters, bidirectional rules,
  Unicode equivalence, and the post-encoding password limit.
- [x] Prove encrypted mutation produces one encrypted revision, rotates credentials, rejects
  user-only authority, and never exposes credentials through option string representations.
- [x] Show local password-strength guidance for user and owner passwords in all six Designer
  languages without network or telemetry use.
- [x] Validate one generated PDF independently with Poppler `pdfinfo`; it reports PDF 2.0,
  AES-256, and the expected print/copy/change permissions.
- [x] Open Generator AES-256 output with PdfPig and round-trip it through the typed PXA Importer.
- [x] Validate AES-256, AES-128, and RC4 output with qpdf 12.4.1 and Poppler, including
  password authentication, permissions, algorithms, syntax, decryption, and extracted text.
- [x] Open AES-256 output through Adobe Acrobat's `app.openDoc` interface and verify the
  returned document identity.
- [x] Round-trip X.509 recipient encryption with the matching private key and reject an
  unrelated certificate.
- [x] Validate the current foundation with the full solution, Importer, API, Generator,
  Documentation, Designer, and release-fragment test suites.
- [x] Run `dotnet build PXA.sln`, the Importer and API suites, and `git diff --check` after
  Generator/API integration and independent interoperability fixtures are complete.
