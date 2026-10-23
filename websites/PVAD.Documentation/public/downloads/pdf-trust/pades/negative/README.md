# PXA PAdES Negative Trust Corpus

This directory contains synthetic failure cases for PAdES validation. EU Digital
Signature Services 6.4 evaluated every file in a container with network access
disabled and a read-only filesystem.

The cases distinguish an expired signing certificate, an untrusted chain, a
revoked signer, unavailable revocation evidence, corrupted embedded LT evidence,
and a cryptographically corrupted LTA archive timestamp. The simple, detailed,
and diagnostic XML reports are retained beside every PDF and pinned in
`negative-corpus.json`.

All certificates and revocation lists are synthetic and test-only. This directory
contains no private keys, passwords, customer documents, production certificates,
or operational credentials.
