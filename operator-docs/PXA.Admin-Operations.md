# PXA Admin Operator Guide

> Internal deployment source. This file is intentionally excluded from the public PXA.Documentation navigation and build.

## Classification

This file is an internal operational guideline and runbook, not end-user product documentation. The protected product handbook is available only to authenticated administrators inside PXA.Admin.

## Purpose

This guide prepares a separately protected operator documentation deployment. It contains no credentials, signing material, customer identifiers, or usable bootstrap values.

## Operator Areas

- Configure `admin.powerdoxautomation.com` for Cloud or `admin.{customer-host}` for On-Premise and route same-origin `/api` traffic to PXA.WebApi.
- Require secure host-only cookies, HTTPS, trusted proxy headers, and an explicit production operator allowlist.
- Keep Development identity bootstrap disabled outside local Development.
- Apply EF Core migrations before routing traffic to a new API version.
- Validate database, mail, storage, and API readiness before enabling Admin access.
- Revoke compromised sessions and credentials through audited APIs.
- Suspend an organization only through an approved operational procedure.
- Treat mail outages as queued-delivery incidents; never extract protected outbox payloads.
- Recover databases and external object storage as one consistency boundary.
- Use break-glass access only through a separately approved and audited organizational process.

## Publication Boundary

- Do not import this file from `websites/PXA.Documentation`.
- Publish it only through a protected operator documentation pipeline.
- Replace deployment-specific placeholders at release time through secret-free configuration.
- Review every release for credentials, tokens, customer data, private URLs, and recovery material.
