# PXA PDF Trust Synthetic Corpus

These files are synthetic interoperability and diagnostic examples. They contain no
customer documents, production certificates, private keys, operational credentials, or licensed
third-party material.

The encrypted fixture uses the synthetic reader passphrase
`synthetic-reader-password`. It is test data, not an account or production credential.

Each PDF has an adjacent `*.expected.json` file containing the stable portion of the
expected `PdfTrustReport`. `corpus.json` pins every PDF by SHA-256 and size. Cryptographic
fixtures are immutable checked-in artifacts because secure encryption and key generation
must use randomness; regenerating the corpus intentionally produces new hashes.

Regenerate the complete corpus from the repository root with:

```bash
dotnet run --project tools/pdf-trust-corpus/PXA.PdfTrustCorpus.csproj
```

Review every changed PDF and expected report before accepting a corpus update.
