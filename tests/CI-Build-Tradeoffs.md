# Backend CI build tradeoffs (#706)

The current workflow has a standalone solution build and 15 independent backend test jobs. Each test job restores and builds its own project and references before running tests. The standalone build and the test jobs can run at the same time. Sharing the solution output would remove overlapping compile work, but the test jobs would have to wait for the build and artifact transfer.

## Measurements

GitHub Actions reports step timestamps at one-second resolution. The solution column sums restore/setup and build; matrix setup includes SDK setup, cache, restore, and build, but not test execution. Summed durations describe runner work, not elapsed time or compiler-only CPU.

| Run | Solution setup + build | 15 matrix setup steps, summed | Integration setup | Integration test execution |
| --- | ---: | ---: | ---: | ---: |
| [45990710252](https://github.com/Reaparr/Reaparr/actions/runs/45990710252) | 151 s | 1,584 s | 82 s | 241 s |
| [45704063026](https://github.com/Reaparr/Reaparr/actions/runs/45704063026) | 168 s | 1,467 s | 92 s | 238 s |
| [45697285317](https://github.com/Reaparr/Reaparr/actions/runs/45697285317) | 139 s | 1,423 s | 127 s | 322 s |

Representative local Linux Debug/net11.0 build output, archived with `tar | gzip -1`: BaseTests 84.9 MB, Application 88.3 MB, Integration 88.5 MB. Compression took 3.4–6.6 s per sample. These are independent archives; they duplicate dependencies. These measurements do not include GitHub artifact upload, download, extraction, or queue time.

The simplest build-once model starts integration tests only after the existing solution setup/build. With the recorded step durations, that would add 69 s, 76 s, or 12 s before integration test execution, **before** artifact transfer. The model assumes the build takes the same time and compares setup phases rather than predicting end-to-end CI duration.

## Decision

Keep the current matrix and standalone solution build. Build-once may reduce aggregate billed runner time, but the samples do not support claiming a faster PR check, and the current workflow has a shared deadline. The solution build also covers solution entries outside the test-project matrix.

If runner cost justifies a hosted A/B experiment, use the same revision and runner image for both variants. Measure summed runner seconds and time to the final required backend check, including all upload/download steps. Require the same project/case counts, failing-build behavior, TRX and Cobertura artifacts, and sandbox isolation before switching.

### Reproduce

```sh
gh run view 45990710252 --repo Reaparr/Reaparr --json jobs
# Use startedAt/completedAt for Back-End-Build, Back-End-Unit-Tests, and
# Back-End-Integration-Tests. Compare Dotnet Setup with the Run Tests steps.
p=tests/IntegrationTests/IntegrationTests/bin/Debug/net11.0
time tar -C "$p" -cf - . | gzip -1 | wc -c
```
