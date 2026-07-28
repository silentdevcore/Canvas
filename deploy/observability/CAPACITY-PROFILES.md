# PXA Capacity Profiles

These v1 profiles are release gates for the containerized PXA.WebApi and observability stack. They
are sizing baselines, not commercial usage limits.

## On-Premise Small

- One WebApi replica: 1 vCPU and 2 GiB memory.
- Target concurrency: four representative document operations.
- Observability storage uses local persistent volumes.
- Intended for evaluation and small installations; scale up before raising concurrency.

## Cloud Standard

- One independently scalable WebApi replica: 2 vCPU and 4 GiB memory.
- Target concurrency: eight representative document operations.
- Loki and Tempo use the S3-compatible Cloud configuration.
- Horizontal autoscaling and production object-storage tests remain deployment responsibilities.

## Release Test

Run the same 100-page fixture under both container limits:

```bash
PXA_CAPACITY_API_KEY='pxa_<synthetic-service-account-key>' \
  deploy/observability/capacity-profile-test.sh onprem-small 1 2147483648 4
PXA_CAPACITY_API_KEY='pxa_<synthetic-service-account-key>' \
  deploy/observability/capacity-profile-test.sh cloud-standard 2 4294967296 8
```

The gate requires zero failed requests, p95 at or below two seconds, and peak memory below 90% of
the configured WebApi limit. Record p95, throughput, CPU, and memory for each release environment.
The local Rancher Desktop result validates the profile contract and workload; Cloud production
must repeat the same command against its deployed container class.

## July 2026 Reference Results

The local ARM64 Rancher Desktop run used the protected WebApi export route, a synthetic
service-account key, 100 requests, and a 100-page PDF per request.

| Profile | Concurrency | p95 | Throughput | Peak CPU | Peak memory |
| --- | ---: | ---: | ---: | ---: | ---: |
| On-Premise Small, 1 vCPU / 2 GiB | 4 | 0.179954 s | 25.26 req/s | 88.28% | 7.04% |
| Cloud Standard, 2 vCPU / 4 GiB | 8 | 0.180031 s | 48.78 req/s | 87.49% | 5.13% |

Both profiles completed without failed requests and remained inside the two-second p95 and 90%
memory budgets. The temporary service-account credential was removed after the run.
