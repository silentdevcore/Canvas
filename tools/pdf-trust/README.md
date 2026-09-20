# PDF Trust Governance and Evidence

`validate-standards-baseline.mjs` checks that every required standards family is
tracked. Its default mode validates structure; `--release` additionally requires
licensed exact editions, corrigenda review, and external policy approval. The
release check is intentionally fail-closed until those external decisions exist.

`validate-external-evidence.mjs` verifies safe repository-relative paths and
SHA-256 hashes for synthetic interoperability fixtures and their independent
tool reports. Adding a validator result requires updating the pinned manifest;
unreviewed files cannot silently replace accepted evidence.

```bash
node tools/pdf-trust/validate-standards-baseline.mjs
node tools/pdf-trust/validate-external-evidence.mjs
```

No licensed standard text, customer document, real certificate, credential, or
private key belongs in these files.
