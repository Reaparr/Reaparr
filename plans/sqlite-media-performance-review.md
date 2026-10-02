# SQLite / Media-Table Performance Review

**Reviewed:** 2026-08-17  
**Live database:** `/mnt/DATA/ReaparrCache/Config/ReaparrDB.db`  
**Method:** Rider SQLite data source plus read-only `sqlite3` inspection (`dbstat`, `EXPLAIN QUERY PLAN`, integrity checks) and application query/schema review.

## Executive summary

The database is healthy but is already large enough that the current “load an entire library and reconcile in memory” strategy will not scale comfortably.

- Main database: **4.0 GiB**
- WAL sidecar at inspection: **149 MiB**
- Page size: **4 KiB**
- Main database pages: **1,033,216** (~4.0 GiB)
- Freelist pages: **26** (~104 KiB): there is effectively no reclaimable bloat at the time measured.
- Journal mode: **WAL**
- `synchronous`: **FULL** (`2`)
- Auto-vacuum: **disabled**
- Connection default cache: **-2000 KiB**
- Foreign keys, for the inspected Rider connection: **disabled**
- `PRAGMA integrity_check`, `quick_check`: **ok**
- `foreign_key_check`: **0 violations**

The bottleneck is real data volume and query/access design, not corruption or an immediately repairable amount of free pages.

## Evidence: media tables dominate storage

### Largest tables (`dbstat` allocated bytes)

| Object | Rows | Allocated size | Notes |
|---|---:|---:|---|
| `PlexTvShowEpisodes` | 3,829,916 | **1,927.7 MiB** | Largest table; wide row with many metadata text columns. |
| `PlexTvShowEpisodeData` | 3,884,704 | **687.5 MiB** | One-or-more media/part records per episode. |
| `PlexMovie` | 429,759 | 225.8 MiB | Also wide metadata rows. |
| `PlexMovieData` | 445,794 | 87.3 MiB | Movie media/part rows. |
| `PlexTvShowSeason` | 272,251 | 71.3 MiB | Structural hierarchy. |
| `PlexTvShows` | 119,305 | 61.6 MiB | Structural hierarchy. |

Media-related indexes add hundreds of MiB. The heaviest episode indexes alone are:

| Index | Allocated size |
|---|---:|
| `IX_PlexTvShowEpisodeData_PlexTvShowEpisodeId_Quality` | 67.6 MiB |
| `IX_PlexTvShowEpisodes_PlexApiRatingKey_PlexServerId` | 65.5 MiB |
| `IX_PlexTvShowEpisodes_TvShowSeasonId_SortIndex` | 61.7 MiB |
| `IX_PlexTvShowEpisodes_TvShowId_SortIndex` | 61.6 MiB |
| `IX_PlexTvShowEpisodeData_PlexApiRatingKey` | 57.3 MiB |
| `IX_PlexTvShowEpisodeData_Quality` | 48.1 MiB |

The large single-column indexes are not automatically wrong. However, every retained index adds write amplification to library syncs, database size, WAL growth, and checkpoint work. Their continued existence should be based on measured production query plans.

### Library skew

The largest libraries hold roughly **0.5–0.8 million episode rows each**; examples from the sample include 806,702, 760,039, and 539,888 episodes. A library-wide sync that materializes full episode entities therefore has high memory allocation and I/O cost even before reconciling changes.

## Findings and recommended changes

### 1. Replace full-library graph hydration during TV reconciliation — **highest priority**

**Evidence**

`SyncPlexTvShowsCommandHandler.ReconcileTvShows` currently executes:

```csharp
_dbContext.PlexTvShows.AsNoTracking()
    .Include(x => x.Seasons)
        .ThenInclude(x => x.Episodes)
    .Where(x => x.PlexLibraryId == plexLibraryId)
    .ToListAsync(cancellationToken);
```

It then creates multiple in-memory dictionaries/lists for every show, season, and episode in the library. In this live sample, a single library may mean ~800k wide `PlexTvShowEpisodes` rows, plus hierarchy and object graph overhead.

**Impact**

- Large allocation/GC pressure and long sync latency.
- EF materializes columns such as summaries, titles, GUIDs, and other large text fields even though reconciliation mostly needs keys/IDs/change fields.
- One enormous joined `Include` query risks result multiplication and expensive sorting/materialization.

**Recommendation**

Reconcile at the narrowest useful scope:

1. Project only the columns actually used to determine create/update/delete (`Id`, server/library IDs, rating key, parent IDs/keys, quality/version/hash/change markers).
2. Read/update in bounded batches or by incoming key set, not the entire library graph.
3. Use a compact DTO/key projection rather than entities for the “current” side.
4. Process incoming shows in chunks; use `ExecuteUpdateAsync`, `ExecuteDeleteAsync`, or parameterized SQL for set-based operations where entity-level domain logic is not needed.
5. Keep transaction boundaries short. A reasonable starting batch range is 500–2,000 rows, benchmarked against WAL and lock duration.

**Success criteria**

- Peak memory and elapsed time recorded for a 500k+ episode library sync.
- No full entity graph materialized simply to build reconciliation dictionaries.
- Database command count and rows/bytes read decrease materially.

---

### 2. Add composite indexes for demonstrated high-volume query shapes — **highest priority**

#### Metadata-enrichment queue

**Evidence**

`ProcessEpisodeMetadataCommand` queries:

```csharp
PlexTvShowEpisodeData
    .Where(m => m.NeedsGeneratedName
        && m.PlexServerId == command.ServerId
        && m.GeneratedNameSyncedAt == null)
    .Select(p => p.PlexApiRatingKey)
    .Distinct()
    .Take(MAX_ITEMS_PER_RUN)
```

The live plan uses only `IX_PlexTvShowEpisodeData_PlexServerId`, then creates a **temporary B-tree for `DISTINCT`**:

```text
SEARCH PlexTvShowEpisodeData USING INDEX IX_PlexTvShowEpisodeData_PlexServerId
USE TEMP B-TREE FOR GROUP BY/DISTINCT
```

At 3.88M media rows, this is a high-risk repeated background-job scan.

**Recommendation**

Add a partial index matching the work queue:

```sql
CREATE INDEX IX_PlexTvShowEpisodeData_PendingGeneratedNameByServerRatingKey
ON PlexTvShowEpisodeData (PlexServerId, PlexApiRatingKey)
WHERE NeedsGeneratedName = 1
  AND GeneratedNameSyncedAt IS NULL;
```

Model equivalent (verify the generated migration has the SQLite filter):

```csharp
builder.HasIndex(x => new { x.PlexServerId, x.PlexApiRatingKey })
    .HasFilter("NeedsGeneratedName = 1 AND GeneratedNameSyncedAt IS NULL");
```

This is deliberately narrow: it indexes only work still pending and also provides ordered rating keys, which should eliminate or substantially reduce the temp distinct structure.

#### TV-show comparison detail retrieval

**Evidence**

`GetRemoteTvShowRowsAsync` filters episodes using both:

```csharp
x.PlexLibraryId == tvShow.PlexLibraryId && x.TvShowId == tvShow.Id
```

Current plan starts with only `IX_PlexTvShowEpisodes_PlexLibraryId`, scanning all episodes in the library and filtering `TvShowId` afterwards. It additionally uses a temp B-tree for the requested season/episode ordering.

**Recommendation**

Add an index appropriate to the final SQL and ordering. Initial candidate:

```sql
CREATE INDEX IX_PlexTvShowEpisodes_LibraryShowSeasonEpisode
ON PlexTvShowEpisodes (PlexLibraryId, TvShowId, TvShowSeasonId, EpisodeNumber);
```

The exact final ordering joins the season row and has a null conditional, so validate with `EXPLAIN QUERY PLAN` after implementation. If the season number is required for ordering, consider denormalizing it on `PlexTvShowEpisodes` or accepting the small per-show sort. The essential win is avoiding a scan of every episode in the library to load one show.

**Success criteria**

- `EXPLAIN QUERY PLAN` shows the new composite index for both shapes.
- The metadata query no longer shows a large temporary distinct/group B-tree.
- The comparison query has a seek on both `PlexLibraryId` and `TvShowId`.

#### TV-show comparison candidate collation — PR #674

IMDb candidate filtering uses `Guid_IMDB COLLATE NOCASE` so case-variant ASCII identifiers reach the `OrdinalIgnoreCase` matcher. Filtering remains scoped to the owned library and the identifiers from each remote batch; matching priority is unchanged.

The existing IMDb index uses case-sensitive collation, so this predicate can use its library prefix but cannot seek its IMDb component. The fix does not change the schema or other queries' collation.

---

### 3. Introduce media retention / bounded catalog policy — **highest product-level priority**

The core data itself occupies ~3.0 GiB before the supporting indexes, and free space is negligible. `VACUUM` cannot fix ongoing catalog growth; it can only compact space after data is removed.

**Recommendation**

Define what the app must retain. Strong options, from least to most aggressive:

1. Retain the full catalog only for libraries actively monitored/compared.
2. For inactive/disconnected libraries, retain a compact comparison snapshot rather than all per-episode descriptive/media fields.
3. Add an explicit per-library retention setting (for example: full / comparison-only / remove after N days disconnected).
4. Delete media, media-data, join rows, comparisons, and related historical rows in chunks, preserving referential integrity.
5. Separate ephemeral history/log/queue data from canonical library metadata and apply TTLs to it.

Before deletion, provide a dry-run that reports per-table rows and estimated reclaimed bytes. Run retention batches while keeping transactions short; schedule compaction separately.

**Success criteria**

- Storage growth is bounded by a user-visible policy rather than total historical library exposure.
- Deleting a stale library is predictable, resumable, and does not block regular syncs for a long transaction.

---

### 4. Split hot identity/sync columns from cold descriptive columns — **high priority for long-term scale**

`PlexTvShowEpisodes` is a very wide table (title, studio, summary, full title, GUID variations, rating metadata, dates, artwork flags, etc.) and is 1.93 GiB for 3.83M rows. Its `dbstat` payload occupancy is already high (~90.7%), so there is no simple bloat fix.

**Recommendation**

Vertically partition the media model:

- **Hot/base table:** identity, hierarchy, library/server IDs, rating key, sort/episode number, quality, status/change fields used by sync/comparison/listing.
- **Cold/details table:** summary, studio, lengthy titles, GUID variants, ratings, and fields used only by expanded detail pages.
- Keep media-part fields in their existing dedicated data tables; similarly minimize repeated parent fields that can be obtained from the parent record when not required by a hot query.

Load details only for detail endpoints/views. Migrate progressively and benchmark actual row-size reduction; this has a higher implementation cost but produces lasting I/O and cache improvements.

**Success criteria**

- Common sync/list/comparison paths avoid fetching description-heavy columns.
- The hot episode table materially shrinks and fits more useful rows per page/cache working set.

---

### 5. Rationalize indexes using real workload data — **high priority, do after query measurements**

The episode/media tables have many single-column indexes alongside composites. For example, `PlexTvShowEpisodes` retains individual indexes for `PlexLibraryId`, `PlexServerId`, and `SortIndex`, plus composite relationship/order indexes. `PlexTvShowEpisodeData` has individual server, library, rating-key, and quality indexes, plus `(PlexTvShowEpisodeId, Quality)`.

At this size, each index costs tens of MiB and is updated on inserts, updates, and deletes. Do **not** delete them speculatively: SQLite index usefulness depends on actual query predicates, joins, and sort patterns.

**Recommendation**

1. Enable/capture EF command telemetry with normalized SQL, duration, rows returned, and call-site tag.
2. Run `EXPLAIN QUERY PLAN` for the top slow/high-frequency shapes.
3. For each candidate index, compare plans and timings in a copied production-like database before/after removal.
4. Drop only indexes with no workload dependency, replacing multiple singles with a composite only when its left-prefix behavior covers the real predicates.

Potential review targets first because they are large: individual `Quality`, `SortIndex`, `PlexServerId`, `PlexLibraryId`, and `PlexApiRatingKey` indexes on the high-growth tables. Preserve indexes that support FK cascade/join paths.

**Success criteria**

- Every retained index has a named query/write-path justification.
- Index write cost and file-size growth are reduced without regressing measured plans.

---

### 6. Make read paths projection-first and avoid broad `Include` calls — **high priority**

`AsNoTracking()` is used in several places and is beneficial, but it does not prevent transfer/materialization of all mapped columns. `Include` is useful for aggregate editing, not for read models that need a small selection of fields.

**Recommendation**

- For pages/API responses, use `Select` directly into DTOs rather than entity + `Include` + mapper.
- Avoid broad reusable helpers such as `IncludeAll` in high-cardinality listing/query paths unless the caller genuinely needs every navigation.
- For unavoidable multi-collection loads, evaluate `AsSplitQuery()` to avoid Cartesian row multiplication; benchmark because it trades a large result for extra round trips.
- Ensure pagination is keyset/seek pagination for deep media lists; avoid `Skip` for high offsets.

**Success criteria**

- SQL reads only columns shown/used.
- API and background-job memory allocations fall for large-library operations.

---

### 7. Improve WAL/checkpoint and connection pragma policy — **medium priority**

**Evidence**

- WAL is correctly enabled.
- The WAL was 149 MiB at inspection.
- `synchronous = FULL` maximizes durability but makes write-heavy sync batches more expensive.
- Default cache size is only ~2 MiB, very small compared with a multi-GiB active dataset.
- `foreign_keys = 0` for the inspected Rider connection. This does not prove application connections are disabled, but it must be explicitly set/verified per application connection because SQLite foreign-key enforcement is connection-local.

**Recommendation**

1. Keep WAL. Monitor WAL size, checkpoint duration, busy/locked errors, and reader lifetime.
2. Ensure application read contexts are promptly disposed; a long-lived reader prevents checkpoint truncation.
3. Set a deliberate connection initialization policy, for example:
   - `PRAGMA foreign_keys = ON;`
   - `PRAGMA busy_timeout = ...;`
   - an appropriate `cache_size` for memory budget;
   - review `wal_autocheckpoint` rather than assuming its 1000-page default suits large sync batches.
4. Consider `synchronous=NORMAL` only if the product accepts the documented durability trade-off under OS/power failure. Do not change it merely for a benchmark.
5. Use explicit WAL checkpoints during low activity or after known large write jobs where monitoring proves they are needed; do not continuously force truncate checkpoints while readers are active.

**Success criteria**

- PRAGMAs are set and tested on every application connection.
- WAL growth and checkpoint latency are observable.
- No stalled checkpoints caused by leaked/long-running readers.

---

### 8. Define deletion and compaction operations separately — **medium priority**

`auto_vacuum` is disabled. That is not a defect: enabling it later requires a database rebuild/VACUUM and it may increase ongoing write overhead. The 26-page freelist proves there is currently no benefit to a normal `VACUUM`.

**Recommendation**

- Do not run `VACUUM` now for “performance”; it will rewrite ~4 GiB and reclaim virtually nothing.
- After a large retention purge, plan an offline/maintenance-window `VACUUM` or use `VACUUM INTO` to create a compact verified replacement database with rollback capacity.
- Consider incremental auto-vacuum only if regular large deletes and strict file-size reclamation justify its permanent overhead; it requires a migration/rebuild and operational testing.

**Success criteria**

- Reclamation is scheduled after actual deletion and with space/time budgeting.
- No surprise multi-GiB rewrite occurs during normal operation.

## Recommended implementation order

1. **Composite pending-metadata index** and **library+show episode index**, with migration and query-plan tests.
2. Rework TV reconciliation to use narrow projections and bounded batches, then measure time/memory/WAL effects.
3. Add query telemetry and establish an index-audit test/benchmark process.
4. Implement user-configurable stale-library/media retention with dry run and chunked deletion.
5. Vertically partition hot vs. cold episode/movie metadata once workload measurements confirm the frequent hot paths.
6. Add/verify connection pragma initialization, WAL/checkpoint metrics, and operational maintenance documentation.

## Validation checklist for every change

- Use a copy of this 4 GiB database or a representative fixture.
- Capture baseline: wall time, peak managed memory, SQL duration/count, WAL growth, database file size.
- Run `EXPLAIN QUERY PLAN` for changed query shapes and assert the expected index/seek appears.
- Run integration tests plus `PRAGMA integrity_check` and `PRAGMA foreign_key_check` after migrations/retention.
- Validate concurrent sync/read behavior to catch WAL writer contention.
- Deploy high-risk index or retention changes behind observability and a rollback plan.

## Implementation decisions (2026-08-17)

### Approved for implementation

- **Point 1:** Rework TV-library reconciliation to eliminate full entity-graph hydration.
- **Point 2:** Add both evidence-based high-volume media indexes.
- **Point 6:** Refactor high-cardinality reads toward projection-first access. This should be delivered incrementally, starting with measured paths, to avoid response-contract regressions.

### Design/spec only

- **Point 5:** Query telemetry and repeatable index-audit benchmark process.
- **Point 7:** Connection-PRAGMA policy and WAL/checkpoint observability (retain `synchronous=FULL`; no durability change approved).
- **Point 8:** Future post-retention compaction/maintenance workflow.

### Deferred

- **Point 3:** Media retention / bounded catalog policy.
- **Point 4:** Hot/cold vertical partition of media tables.

## Notes on what not to do

- Do not blindly add indexes: index growth and write amplification are already substantial.
- Do not run `VACUUM` before removing data.
- Do not assume `AsNoTracking` makes a full-graph, full-column query cheap.
- Do not turn `synchronous` down without an explicit durability decision.
- Do not rely on FK enforcement unless `foreign_keys=ON` is guaranteed for each application connection.
