# PVAD PowerShell SDK Checklist

## Goal

Deliver a PowerShell module for Windows, Linux, and macOS automation against PVAD Cloud and local PVAD Server deployments.

## Priority And Dependencies

- [ ] P2: Start after the primary SDK releases are stable.
- [ ] Complete the shared contract work in `PVAD.SDK-Roadmap.md` first.
- [ ] Verify publisher ownership and reserve the proposed `PVAD.Client` module name in PowerShell Gallery.

## Module And Commands

- [ ] Support PowerShell 7.4 or newer.
- [ ] Publish the `PVAD.Client` module to PowerShell Gallery.
- [ ] Generate or wrap the shared OpenAPI transport contract.
- [ ] Add connection-profile commands for Cloud and local PVAD Server.
- [ ] Add cmdlets for PDF, Templates, Migration, Import, Export, and Spreadsheet workflows.
- [ ] Use approved verb-noun command names and pipeline-friendly parameters.
- [ ] Support file paths, byte streams, pipeline input, and pipeline output.
- [ ] Support API-key and bearer-token authentication through secure values.
- [ ] Map Problem Details to actionable PowerShell errors.

## Security And Usability

- [ ] Keep credentials out of command history, transcripts, and verbose logs.
- [ ] Support SecretManagement-compatible credential retrieval.
- [ ] Add `-WhatIf` only to commands with meaningful local or remote side effects.
- [ ] Provide progress and cancellation behavior for large transfers and jobs.
- [ ] Provide discoverable comment-based help and runnable examples.

## Distribution And Tests

- [ ] Sign release modules and publish semantic versions.
- [ ] Test module installation from PowerShell Gallery.
- [ ] Run Pester unit tests on Windows, Linux, and macOS.
- [ ] Run integration tests against the PVAD Server container.
- [ ] Test pipeline behavior, secure credentials, uploads, downloads, and errors.

## Acceptance Criteria

- [ ] Administrators can complete common workflows without writing raw HTTP requests.
- [ ] Commands behave consistently across supported operating systems.
- [ ] Credentials never appear in normal output or logs.
- [ ] The same commands work with Cloud and local PVAD Server profiles.
