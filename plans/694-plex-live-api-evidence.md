# Issue #694 — live Plex API evidence

Date: 2026-10-16. These are actual read-only requests, not documentation samples. Server origins, account identity, library UUIDs, file paths and credentials are intentionally omitted. `PMS_A` denotes the HTTPS server selected from the user-specified Reaparr database (connection ID 96 / server ID 12). Access is a shared-library token, not owner/admin access. Results establish this build and visibility scope only.

## Connection and tooling

The user authorized reading `/mnt/DATA/ReaparrCache/Config/ReaparrDB.db` via Rider. Rider `list_database_connections` returned none; `create_database_connection` rejected SQLite JDBC URLs, and `list_database_sources` failed with `ClassNotFoundException: com.intellij.database.dataSource.LocalDataSourceManager`. The user then explicitly authorized native read-only SQLite access. Only connection/authentication fields needed for the investigation were consumed; no database writes were made.

Initial identity and section requests used a `sqlite3 -readonly ... | curl --config - ...` pipeline, keeping the token out of stdout. Python execution was denied; no Python probe ran. A curl shell-script matrix was also denied and was not executed. The user explicitly requested web fetch/search instead; subsequent individual probes used authenticated HTTPS fetches. URL-based authentication was necessary for this fetch tool, but **do not publish the original request URLs or raw browser/network dumps**: they contain credentials. Examples below use placeholders.

`GET /identity` returned HTTP 200 with:

```json
{"MediaContainer":{"size":0,"apiVersion":"1.2.3","claimed":true,"version":"1.43.4.10903-e5521bd8c"}}
```

The original also contained a machine identifier, omitted here. `/library/sections` returned movie section `1`, show sections `2` and `3`, and music section `4`. Section 1 `contentChangedAt` was `14922881`; this is not its epoch `updatedAt`.

## Exact query forms and observations

All paths below are relative to `PMS_A`. Each request included `X-Plex-Token=<secret>` (omitted from the table). Unless stated otherwise, paging included **both** `X-Plex-Container-Start=0` and `X-Plex-Container-Size=1` or `2`. `%3E%3E` decodes to `>>`, and `%3C%3C` to `<<`. The following `=` separates query key and value; it is **not** evidence of an inclusive comparison.

| Path / query (paging omitted for readability) | Observed result | What it proves |
|---|---|---|
| `/library/sections/1/all?type=1&sort=updatedAt:desc` | Movie population 587; latest visible key `1774`, `updatedAt=1792054844`. | Baseline, sorted current metadata. The first probe supplied size without start and returned all 587 items; size alone was not a bound. |
| `/library/sections/1/all?type=1&updatedAt%3E%3E=1792000000&sort=updatedAt:desc` | `size=1,totalSize=1,offset=0`, key `1774`. | Working server-side timestamp filtering, not a silently ignored parameter. |
| `/library/sections/1/all?type=1&updatedAt%3E%3E=4102444800` | `size=0`. | Future-date negative control. |
| Same filter with lower bound `1792054843` | One item, key `1774`. | Includes an item one second above lower bound. |
| Same filter with lower bound `1792054844` | `size=0,totalSize=0`. | **Strict lower bound at equality on this build.** |
| Same filter with lower bound `1792054845` | `size=0,totalSize=0`. | Successor negative control. |
| `/library/sections/1/all?type=1&updatedAt%3E=1792054843` | One item, key `1774`. | A single `>` key variant also filtered; equality semantics for this variant were not tested. Prefer the explicitly boundary-tested `>>` form. |
| `/library/sections/1/all?type=1&updatedAt%3E%3E=1792000000&updatedAt%3C%3C=1792054844` | Zero items. | Upper-bound equality excludes key `1774`. |
| Same query with upper bound `1792054845` | One item, key `1774`. | Multiple date conditions are intersected; supports a strict bounded interval on this build. |
| `/library/sections/1/all?type=1&updatedAt%3E%3E=1791800000&sort=updatedAt:desc&X-Plex-Container-Start=1&X-Plex-Container-Size=1` | `size=1,totalSize=5,offset=1`, key `2758`, `updatedAt=1791887743`. | Offset pagination is applied to the filtered set. Not proof of snapshot consistency during writes. |
| `/library/sections/1/all?type=1&addedAt%3E%3E=1791200000&sort=addedAt:desc` | One item, key `154067`, `addedAt=1791252846`. | An additions-only date query works for current surviving movies. |
| `/library/sections/2/all?type=4&updatedAt%3E%3E=1792000000&sort=updatedAt:desc` | `size=1,totalSize=238,offset=0`; episode key `23224`, `updatedAt=1792159184`, parent key `23217`, grandparent key `23216`. | Changed leaf queries work for episodes, with parent identifiers. |
| `/library/sections/4/all?type=10&sort=updatedAt:desc` | `size=1,totalSize=97925`; latest track key `37983`, `updatedAt=1791627389`. | Music leaf baseline. |
| Same track endpoint with `updatedAt%3E%3E=1792000000` | Zero items. | Track timestamp negative control. |
| Same track endpoint with `updatedAt%3E%3E=1791627388` | `size=1,totalSize=12`; key `37983`. | Track timestamp positive control reduces 97,925 current tracks to 12. |
| `/library/sections/1/all?type=1&deletedAt%3E%3E=0` | `totalSize=587`; ordinary current movies `4326`, `4310`, with no `deletedAt` attribute. | This request does **not** produce a deletion feed. It appears ignored/unusable in this scope; it does not prove no soft-deleted objects exist elsewhere. |
| Same endpoint with `contentChangedAt%3E%3E=14922881` | `totalSize=587`, same ordinary current movie prefix. | Not a working section-changestamp delta query. |
| Same endpoint with `changeStamp%3E%3E=14922881` | `totalSize=587`, ordinary current movie prefix. | Alternative stamp field did not produce changes. |
| `/library/sections/1/changes?since=14922881` | HTTP 404. | This candidate route unavailable under this token/build. Not universal nonexistence. |
| `/library/sections/1/deleted` | HTTP 404. | This candidate route unavailable under this token/build. |
| `/library/changestamp` | `<MediaContainer size="0" changestamp="14965126">`. | A global change-marker endpoint really exists; it returns a scalar, not changed IDs or tombstones. |

### Reproducible bounded request

Use header authentication in normal code/tooling so tokens do not enter URLs:

```http
GET /library/sections/1/all?type=1&updatedAt%3E%3E=1792000000&X-Plex-Container-Start=0&X-Plex-Container-Size=2&sort=updatedAt:desc HTTP/1.1
Host: <authorized-PMS-origin>
X-Plex-Token: <secret>
X-Plex-Client-Identifier: <stable-client-id>
Accept: application/json
```

Actual web-fetch response was XML (no `Accept: application/json` header). Exact selected attributes/elements from that response, unrelated metadata and file paths omitted:

```xml
<MediaContainer size="1" totalSize="1" offset="0" librarySectionID="1">
  <Video ratingKey="1774" key="/library/metadata/1774" type="movie"
         addedAt="1789276767" updatedAt="1792054844">
    <Media id="143538" duration="1486234" bitrate="25171" width="3840"
           height="2160" audioChannels="6" audioCodec="eac3" videoCodec="hevc">
      <Part id="143542" key="/library/parts/143542/1792020572/file.mkv"
            duration="1486234" size="4677521360" container="mkv" />
    </Media>
  </Video>
</MediaContainer>
```

These list responses contained media/part state but did not contain nested `Stream` elements in the observed examples. Re-fetch detail where required; do not invent stream completeness from media codecs.

### Lightweight inventory experiment

Request:

```text
/library/sections/1/all?type=1&includeFields=ratingKey,updatedAt&includeElements=Metadata&X-Plex-Pms-Api-Version=1.2.3&X-Plex-Container-Start=0&X-Plex-Container-Size=2
```

Observed response retained two `Video` keys/timestamps and empty nested elements, **but removed the container's `size`, `totalSize`, and `offset` attributes**. Therefore this exact query cannot be copied into the current paginator. A useful key-only inventory must explicitly retain paging fields and exclude irrelevant child elements, and its termination/completeness must be qualified. Do not advertise this experiment as an implemented safe deletion inventory.

A corrected request was then live-tested at offsets 0 and 2:

```text
/library/sections/1/all?type=1&includeFields=ratingKey,updatedAt,size,totalSize,offset&excludeElements=Media,Image,UltraBlurColors,Genre,Country,Director,Writer,Role&X-Plex-Pms-Api-Version=1.2.3&X-Plex-Container-Start=0&X-Plex-Container-Size=2
```

Observed selected output:

```xml
<MediaContainer size="2" totalSize="587" offset="0">
  <Video ratingKey="4326" updatedAt="1789276863"><!-- empty artwork elements omitted --></Video>
  <Video ratingKey="4310" updatedAt="1790240440"><!-- empty artwork elements omitted --></Video>
</MediaContainer>
```

At start 2 it returned `size=2,totalSize=587,offset=2`, keys `4309` and `4308`. This establishes bounded compact inventory pages with paging fields preserved on this build. `Media`, genre, country, director/writer/role payloads were absent; empty `Image`/`UltraBlurColors` elements remained despite the exclusion request. A complete inventory under concurrent mutation, all types/versions, and safe deletion application still requires qualification; two pages are not a complete authoritative snapshot.

## Live-tested verdict and remaining gaps

**Verified:** Plex supports materially smaller timestamp-filtered current-item retrieval for movies, episodes and tracks on PMS 1.43.4.10903; `>>`/`<<` were strict at the tested movie boundary; normal filtered paging works with both start and size. The feed returns current `ratingKey` identifiers and nested media/part data, not only IDs.

**Not established:** a durable tombstone/change journal, retention/replay cursor, atomic snapshot, universal type/version compatibility, parent-timestamp changes for every file/part/stream mutation, or complete detection of restored/backdated items. No library was mutated: additions, metadata edits, stream/part replacements, moves, merges, deletion/Empty Trash, clock skew and restore transitions were not experimentally induced. Production/shared libraries are not disposable fixtures.

A stored connection whose database product version was 1.31.3 was also tried through web fetch, but `/identity` failed with `ECONNREFUSED`; it provides **no live legacy-version compatibility evidence**. Photos/other videos were not exposed by PMS_A. Retention limits and maximum accepted page sizes remain unknown, not unlimited.

## Measured counts versus inferred performance

- Observed movie population: 587; recent timestamp query: 1 surviving item. That is ~99.83% fewer returned item records for this chosen interval, **not** measured byte/time reduction.
- Observed track population: 97,925; tested timestamp interval: 12 current tracks. That is ~99.988% fewer returned item records.
- [INFERENCE] With page size 500 and no extra detail reads, 97,925 tracks require 196 content pages plus the current count request (197); 12 tracks fit in one content page, plus any count/control request. Two requests instead of 197 would be ~98.98% fewer for this leaf traversal, **excluding deletion reconciliation, hierarchy, metadata enrichment and ancillary requests**.
- No full large-library traversal or load benchmark was run. Server-side query cost and payload-byte/time improvements are not measured.

## Signed-in Chrome DevTools evidence

Plex Web 4.160.0 did not accept the PMS token in its document URL as an account sign-in session: it redirected to the Plex account authentication form. The user explicitly supplied credentials for this test; the normal email sign-in completed successfully. No password/account token is recorded in this report.

After sign-in, the actual movie Library view rendered **1,220 items** from another shared server (`PMS_B`, stored server ID 4). This is **not** the 587-movie PMS_A dataset. Chrome DevTools observed the following real requests:

| Browser request ID | Sanitized operation | Evidence |
|---|---|---|
| 386 | `GET /library/sections/1/all?type=1&includeCollections=1&includeExternalMedia=1&includeAdvanced=1&includeMeta=1&X-Plex-Container-Start=0&X-Plex-Container-Size=50` | HTTP 200; JSON `size=50,totalSize=1220,offset=0`. Advertised date field included `addedAt`; no added/updated/deleted/changestamp field other than `addedAt` was found in the selected capability-field summary. |
| 433 | `GET /library/sections/1/all?type=1&includeCollections=1&includeExternalMedia=1&X-Plex-Container-Start=50&X-Plex-Container-Size=210` | HTTP 200; `X-Plex-Container-Start: 50`, `X-Plex-Container-Total-Size: 1220`. This is ordinary offset paging, not a change cursor. |
| 389 | `GET /library/sections/1/firstCharacter?sort=titleSort&includeCollections=1&includeExternalMedia=1` | HTTP 200; alphabetic navigation support, not delta retrieval. |

The browser supplied standard client product/version/identifier, platform/device/language fields, `X-Plex-Provider-Version=7.2`, `X-Plex-Text-Format=plain`, authentication, and `Accept: application/json, text/plain, */*`. Provider version is **not** the PMS API version or a sync checkpoint.

Actual response headers for request 433 reported `Content-Encoding: gzip`, compressed/content length **104,015 bytes**, original length **502,444 bytes**. Request 386 reported compressed length **27,464 bytes**, original length **128,073 bytes**. These are real page payload measurements, **not** a full-sync or delta benchmark.

No replay cursor or delta endpoint appeared in these inspected library interactions. That is bounded observation, not proof that no other UI flow uses one. Plex Web also automatically made account-session/settings calls and connection probes; no library-management action, playback, scan, metadata update or deletion was invoked. The inspection tab was closed afterward to stop its recurring connection attempts. No sign-out/revocation or account-setting reversal was attempted.

The first-party main and dependency bundles loaded by Plex Web were also fetched and searched for timestamp/deletion/changestamp/token handling. The main bundle models `addedAt`, `updatedAt`, and `deletedAt` and contains `updatedAt:desc` sorting; no durable delta request was established from these selected literal matches. This was not an exhaustive reverse engineering of dynamically loaded chunks.
