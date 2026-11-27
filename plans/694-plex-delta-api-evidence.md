# Issue 694 — Plex library incremental/delta API evidence

**Research date:** 2026-10-16  
**Scope:** Plex Media Server HTTP library reads, timestamp filters, event streams, deletion markers, and checkpoint/revision evidence.  
**Purpose:** Evidence for a reliable library-level incremental synchronizer (adds, metadata/media/part/stream changes, deletes, pagination, recovery, and performance).

**Companion live evidence:** [Live PMS/API and signed-in browser results](694-plex-live-api-evidence.md) independently confirms timestamp queries on PMS 1.43.4.10903, strict comparison boundaries, compact inventory pages, and negative delta/deletion probes. The “no live evidence” limitation below applies only to this published-source research slice, not the complete investigation.

## Evidence rules and limitations

This is a research/design note, not a server implementation. Sources are separated into:

* **Official contract/support evidence:** Plex's published PMS API documentation and Plex Support articles.
* **Published non-contract evidence:** public client implementations and community OpenAPI specifications. These show what clients try, but do **not** establish a PMS guarantee.
* **Live evidence:** none in this work. No PMS URL, token, database, or authenticated browser session was available to this researcher; no server requests were made. Consequently this document does not claim that a route is universally absent or that any candidate query works on a particular PMS build. The qualification commands below are reproducible read-only probes for an authorized test server.

Plex's own API documentation warns that many keys discovered by walking responses are provider-specific rather than API-contract paths. A route that exists on one server/provider is therefore not automatically a supported universal endpoint. Conversely, failure to find a route in a published spec is not proof that no build or provider implements it.

## Executive endpoint verdict

| Mechanism | What the published evidence supports | Delta suitability | Important gap |
|---|---|---|---|
| `GET /library/sections/{sectionKey}/all` | Official docs: “Get the items in a section, potentially filtering them.” It returns a `MediaContainer` and metadata with `ratingKey`, `addedAt`, `updatedAt`, and nested media/parts in published examples. | **Full/filtered enumeration; candidate polling source, not a proven delta feed.** | No documented atomic snapshot, cursor, upper-bound checkpoint, retention window, delete tombstone feed, or guarantee that every media/part/stream mutation advances the parent `updatedAt`. |
| Media query filters on `/all` | Official docs define a query-string media-filter language and date operators (`>>=`, `<<=`, etc.); date values are epoch seconds or relative values. The section description publishes fields/sorts, including `addedAt` in the example. | **`addedAt` is a reasonable add candidate; `updatedAt` is a qualification candidate only.** | The docs show `updatedAt` in item responses but do not promise it as an incremental cursor or enumerate it in the sample section sort list. The server can accept, reject, ignore, or scope fields by media type/provider. |
| `GET /library/sections/{sectionKey}/allLeaves` | Official docs: “Get all leaves in a section (such as episodes in a show section).” | **Full leaf enumeration.** | No published `since`, cursor, revision, tombstone, or replay semantics. |
| `GET /library/sections/{sectionKey}/changes` | No operation with this path was found in the official operation catalogue or the current community OpenAPI path list reviewed. | **Unsupported as a contract; probe only.** | This is not a universal-nonexistence claim: undocumented/provider-specific routes and version differences remain possible. |
| `GET /library/all` | Official docs: “Request all metadata items according to a query.” It is a full query/enumeration operation, with an official `Zoolander` example. | **Global full/filtered enumeration.** | Same timestamp, deletion, snapshot, and pagination-race gaps as `/all`; it does not document a change log. |
| `GET /library/metadata/{ratingKey}` | Official support documentation shows item detail with nested `Media`, `Part`, and `Stream`. | **Authoritative re-fetch after a candidate change.** | It identifies current state only; no historical revisions or deletion-history guarantee. |
| `GET /:/eventsource/notifications` (SSE) and WebSocket notification paths | Official docs say real-time events cover library scanning and changes to media. The published community spec describes binary streams and filters. | **Low-latency hint; not a recoverable delta log on published evidence.** | No documented event cursor, `Last-Event-ID` replay, checkpoint, retention, ordering, or complete metadata/delete schema. Disconnect recovery is unspecified. |
| Plex account webhooks (`library.new`) | Official support docs describe an outbound HTTP POST for new library content and a payload containing metadata timestamps. | **Optional add hint only.** | Plex Pass/admin-owner setup; no documented metadata-change/delete event, replay, cursor, or retention. It is push configuration, not a pull library API. |
| Section `contentChangedAt` | Official examples include it. A Plex forum answer describes it as an internally incremented shared “ChangeStamp.” | **Coarse invalidation hint; not a usable public cursor without qualification.** | No documented endpoint accepting the stamp, mapping to item changes, event history, or retention. Its scope is shared/internal and it is not Unix time. |
| `/status/sessions/history/all` (as exposed by a client) | Python PlexAPI uses this for playback/view history. | **Not a library delta source.** | It records viewing history, not metadata/media/part/stream mutations. |

### Bottom-line verdict

The published evidence does **not** establish a reliable, server-supported library delta API that simultaneously provides additions, arbitrary metadata/media/part/stream changes, deletions, a stable cursor/checkpoint, pagination safety, and recovery after downtime. `/all` plus media-query date filters can be tested as a polling optimization, but it must be treated as a **candidate changed-item query**, not as a server-certified delta protocol. A synchronizer still needs a complete baseline/reconciliation path and an overlap/recovery strategy.

## Official PMS API evidence

### 1. API format, authentication, and headers

Plex's PMS developer documentation ([developer.plex.tv/pms](https://developer.plex.tv/pms/)) states:

* PMS responses default to XML; clients request JSON with `Accept: application/json`.
* `X-Plex-Token` is the authentication token obtained from plex.tv. Most endpoints require token authentication.
* `X-Plex-Client-Identifier` is an opaque client identifier and is typically required. It should be stable for the client instance.
* Common product/platform/device headers are accepted. Plex says `X-Plex-*` headers may also be sent as query arguments.
* The official support URL-commands article shows token-in-query examples, but the read-only examples in this plan use the header to avoid leaking a token through URLs, proxy logs, shell history, and referers.

Relevant official support guidance is [Plex Media Server URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/). It says default requests must be authenticated and demonstrates `/library/sections`, `/library/sections/{id}/all`, and `/library/metadata/{ratingKey}`.

The official docs do not publish a per-operation matrix saying that a section read requires an administrator. A token can only see libraries permitted to that account/share; a shared user, restricted user, and owner may receive different sections or fields. The community spec reviewed below models a shared-user/admin token security scheme for general PMS operations, but it is not an official Plex guarantee. Treat a `401`, `403`, an omitted restricted library, or redacted metadata as a normal qualification result, not evidence that the route does not exist.

The docs describe the `manage` media-provider feature as the gate for library-management operations and generally admin-only. That is separate from read-only enumeration. Do not request management permissions or invoke refresh/delete operations merely to test reads.

### 2. PMS API version header and known published versions

The official PMS documentation says a client may send `X-Plex-Pms-Api-Version`; if it is absent, version `0.0` is assumed. The published version history states:

| PMS API version | Supported PMS version | Published change noted by Plex |
|---|---:|---|
| `1.0.0` | `>= 1.41.9` | First public API; `/downloadQueue`; `includeFields` behavior/name change. |
| `1.1.0` | `>= 1.42.0` | Playlist `types` change and photo transcode endpoints. |
| `1.1.1` | `>= 1.42.2` | No library-delta feature listed. |
| `1.2.0` | `>= 1.43.0` | Media-provider metadata endpoints. |
| `1.2.1` | `>= 1.43.1` | EPG provider endpoints. |
| `1.2.2` | `>= 1.43.2` | Audio layout and codec endpoints. |
| `1.2.3` | `>= 1.43.4` | Current published history at research time. |

None of these published changes documents a library `changes` route, a library revision cursor, or timestamp-filter guarantees. If using `includeFields`, account for the documented API-version behavior: from API 1.0, `includeFields` means “include only these fields”; `includeOptionalFields` requests optional fields. Inclusion/exclusion is a request, not a guarantee, and endpoints may ignore it.

### 3. Library section and item shapes

The official section example contains `Directory` entries with fields including:

```json
{
  "key": "1",
  "type": "movie",
  "title": "Movies",
  "updatedAt": 1689270983,
  "createdAt": 1689270983,
  "scannedAt": 1706626696,
  "contentChangedAt": 1234
}
```

This is a **published documentation example, not a live server response**. It demonstrates field presence only; it does not define update triggers, precision, monotonicity, or a query contract for these values.

The official item examples contain integer `addedAt` and `updatedAt` values, plus nested `Media` and `Part` elements. The support article's older XML example additionally shows nested `Stream` entries with video/audio/subtitle attributes. The metadata hierarchy uses `ratingKey` and `key` values such as `/library/metadata/1049`.

The official type table maps common result types as follows:

| Type name | Number |
|---|---:|
| `movie` | 1 |
| `show` | 2 |
| `season` | 3 |
| `episode` | 4 |
| `artist` | 8 |
| `album` | 9 |
| `track` | 10 |
| `photo` | 13 |
| `photoalbum` | 14 |
| `collection` | 18 |

Use the `type`/feature keys returned by the server rather than assuming every library uses the same hierarchy. Plex's media-provider guidance specifically recommends consuming `/media/providers`, not hardwiring `/library/sections`, and says clients should respect type keys instead of blindly appending `/all`. The familiar `/library/sections/{id}/all` route remains useful for PMS qualification and is shown in official support/examples, but a provider may expose a different key.

### 4. `/all`, `/allLeaves`, and media queries

The official API operation catalogue has separate operation descriptions:

* **“Get the items in a section, potentially filtering them”** — the `/all` family.
* **“Get all leaves in a section (such as episodes in a show section)”** — the `/allLeaves` family.
* **“Request all metadata items according to a query”** — the global `/library/all` operation.

The media-query section of the official docs says:

* Queries select subsets of media using query-string filters and sorts.
* Fields are described by `Field` elements from `/library/sections/X?includeDetails=1`.
* Date values use epoch seconds; relative values such as `-3y` are supported.
* `>>=` is “greater than or equal” and `<<=` is “less than or equal” in the documented query language (the docs also describe the underlying greater-than/less-than operators).
* `type` must be supplied for the result type; `sourceType` can scope hierarchical fields.
* `sort` accepts one or more fields; `limit` bounds returned results.

The official section example advertises `addedAt` as a “Date Added” sort. It does **not** advertise `updatedAt` in that sample sort list, even though item examples include `updatedAt`. This distinction matters: a response field is not automatically a documented filter/cursor.

A conservative candidate query is therefore:

```text
/library/sections/{sectionKey}/all
  ?type=1
  &updatedAt>>={checkpointEpochSeconds}
  &sort=updatedAt:asc
```

Whether this returns the expected set must be tested on the target PMS version and each library type. A server may reject the field, ignore it, expose it only for some result types, or update it for only some classes of changes. The official docs do not promise that `updatedAt` is a complete change sequence.

### 5. Pagination and limits

Plex's official pagination guidance says:

* Send both `X-Plex-Container-Start` and `X-Plex-Container-Size` for an offset page.
* A supported `X-Plex-Container-Size: 0` can learn total size without returning content.
* The response must be inspected: the server may force pagination or return a different number than requested.
* Paginated responses may include `X-Plex-Container-Start` and `X-Plex-Container-Total-Size` headers.
* A `MediaContainer` typically contains `offset`, `size`, and optional `totalSize`.
* `limit` and container headers can be combined. With `limit`, the returned total is bounded by the limit rather than necessarily reporting the collection's real total.
* The docs do not state a universal maximum page size.

Offset pagination is not a snapshot guarantee. If the library mutates between page requests, an item can move across the offset boundary, creating a duplicate or gap. Use a deterministic sort where the server accepts it, record response offsets/totals, and qualify mutation races. A timestamp filter plus `sort=updatedAt:asc,ratingKey:asc` is a useful test shape, but `ratingKey` as a tie-breaker is a client strategy, not a published server guarantee.

### 6. `deletedAt` and deletion lifecycle

One official developer-docs example titled “Get all albums in a music section” contains this metadata excerpt:

```json
{
  "ratingKey": "265",
  "key": "/library/metadata/265/children",
  "type": "album",
  "title": "Mandatory Fun",
  "addedAt": 1681152176,
  "updatedAt": 1715112705,
  "deletedAt": 1682628386
}
```

This is an **authentic published Plex documentation example, not a live observation**. The docs do not explain whether `deletedAt` is a tombstone, an unavailable/trash marker, a deletion time retained after removal, a provider-specific field, or how long it remains queryable. The example alone must not be used as a tombstone contract. The current community OpenAPI specification reviewed below does not model `deletedAt` at all.

Official Plex Support explains the user-visible lifecycle: after content is removed and a scan runs, it may enter Library Trash and can be restored before Empty Trash; Empty Trash removes it from the library. Direct deletion from a Plex app can immediately remove the library item and file when enabled. See [How do I delete something from my Library?](https://support.plex.tv/articles/202606363-how-do-i-delete-something-from-my-library/). This is why qualification must test at least:

1. file missing + scan while Trash is retained;
2. restoration before Empty Trash;
3. Empty Trash;
4. direct app deletion only in a disposable test library.

The official docs do not publish a deletion-history endpoint or a guarantee that `/all` returns deleted items with `deletedAt`. A synchronizer cannot safely infer deletes solely from a field seen in one sample.

### 7. `contentChangedAt` / ChangeStamp

The official section example exposes `contentChangedAt` on each library section but does not define it. A Plex forum thread specifically asks about this field: [What information does the value in ContentChangedAt represent](https://forums.plex.tv/t/what-information-does-the-value-in-contentchangedat-represent/935804). The reply by `@drzoidberg33` says:

> “It's just a number that is incremented every time a change is made. It's a shared value across the database used in various places to mark the point at which something changed. I.e. it's a ChangeStamp. It's only really used internally, you don't need to worry about it.”

This is useful Plex-community/Plex-forum evidence, but not a formal API contract. It supports treating the value as an internal shared change marker rather than Unix time. It does **not** document:

* an HTTP parameter such as `sinceContentChangedAt`;
* a mapping from a stamp to affected `ratingKey`s;
* a durable history/ring buffer of stamps;
* retention after restart or database maintenance;
* ordering across libraries/servers;
* whether every stream/part/metadata update advances the marker.

Use it as a coarse “something may have changed” trigger only until qualification proves stronger behavior on the target versions.

### 8. Events, WebSockets, and webhooks

The official PMS docs say the server can notify clients in real time about library scanning, preference changes, changes to media, and activity progress. They name EventSource (SSE) and WebSocket protocols and show filter examples (`filters=-log`, `filters=foo,bar`, and an empty filter). The docs do not specify event payload schemas, durable IDs, replay, or resume.

The current community OpenAPI spec ([`plex-api-spec.yaml`](https://raw.githubusercontent.com/LukeHagar/plex-api-spec/main/plex-api-spec.yaml), version `1.1.1`, community-maintained) describes:

* `GET /:/eventsource/notifications`, binary event stream, a query parameter named `filter` whose description uses `filters=...` examples;
* `GET /:/websocket/notifications`, binary stream;
* `GET /:/websockets/notifications`, WebSocket alias returning `NotificationContainer` messages and requiring `X-Plex-Token`.

That spec contains no cursor, `Last-Event-ID`, sequence, checkpoint, replay, or retention parameter. This is evidence about the published spec, not proof that no PMS build has an undocumented resume behavior. A reconnecting client must assume events can be lost and reconcile through reads.

Plex's official [Webhooks](https://support.plex.tv/articles/115002267687-webhooks/) article is a different mechanism. It requires an active Plex Pass for the server owner/admin account and posts multipart JSON to a configured URL. Its documented library events are `library.new` and `library.on.deck`; the example payload includes `ratingKey`, `librarySectionID`, `addedAt`, and `updatedAt`. The article does not document a library metadata-change event, deletion event, replay cursor, or retention. Webhooks can be an optional add hint, not the source of truth for a synchronizer.

## Published non-contract client/spec evidence

These sources are explicitly not PMS server guarantees.

### Python PlexAPI (`pkkid/python-plexapi`)

* [`plexapi/library.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/library.py) constructs `/library/sections/{key}/all?...`, supports `container_start`, `container_size`, `limit`, and date-filter operators. Its documented examples use `addedAt>>` and `addedAt<<` and its manually added field/sort lists include `updatedAt` even though the server's section description may not expose it. The source comment calls these fields “available but not exposed on the Plex server”; this is strong evidence that clients try `updatedAt`, not proof that all PMS versions honor it.
* The same source implements `recentlyAdded()` as `search(sort='addedAt:desc', ...)`, which is add-focused and not a mutation log.
* [`plexapi/base.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/base.py) pages by sending `X-Plex-Container-Start`/`X-Plex-Container-Size` and uses `totalSize`/`size`; it has no cursor/checkpoint protocol.
* [`plexapi/alert.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/alert.py) listens on `/:/websockets/notifications`, parses `NotificationContainer`, and documents known library timeline states including state `0` item created and state `9` item deleted. This is client-maintainer documentation, not a server contract; it does not persist a sequence or implement replay/recovery.
* [`plexapi/server.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/server.py) exposes `/status/sessions/history/all` as playback history with `viewedAt`; it is unrelated to library metadata changes.

### Go client (`clambin/mediaclients`)

* [`plex/library.go`](https://raw.githubusercontent.com/clambin/mediaclients/main/plex/library.go) models section `ContentChangedAt`, item `AddedAt`/`UpdatedAt`, and nested `Media`/`MediaPart` data. Its `GetAllLibraryMedia` calls `/library/sections/{key}/allLeaves` in pages of 500 using `X-Plex-Container-Start`/`X-Plex-Container-Size` and `totalSize`, with an empty-page guard. It performs full enumeration and has no delta endpoint, tombstone processing, or resume cursor.
* [`plex/timestamp.go`](https://raw.githubusercontent.com/clambin/mediaclients/main/plex/timestamp.go) interprets integer timestamp values as Unix epoch seconds. That is client decoding evidence; the official media-query docs independently describe date filters as epoch seconds.
* [`plex/library_test.go`](https://raw.githubusercontent.com/clambin/mediaclients/main/plex/library_test.go) and its test server fixtures show full `/allLeaves` responses only. They are test fixtures, not captured PMS responses.

### Community OpenAPI specification

[`LukeHagar/plex-api-spec`](https://github.com/LukeHagar/plex-api-spec) is a published community-maintained OpenAPI spec (the raw file reports version `1.1.1`). It documents `/library/all`, `/library/sections/all`, event streams, and common pagination headers. Its path list has no `/library/sections/{id}/changes` entry and no `deletedAt` schema field. Treat those observations as “not documented by this spec,” not universal absence. Its global security model uses a token with `shared user`/`admin` scopes; this is useful for planning but is not Plex's official per-route permission guarantee.

The older community OpenAPI JSON mirror at [`justingarfield/426381d6fb848e5e579b7f094cd2a535`](https://gist.githubusercontent.com/justingarfield/426381d6fb848e5e579b7f094cd2a535/raw) likewise documents `/library/all` and pagination examples but does not establish a change-log endpoint.

## Exact reproducible read-only probes

These commands intentionally use placeholders. They were **not run** in this research. Use an authorized PMS, a stable client identifier, and a token with access to a disposable/test library. Keep the token in an environment variable and redact tokens, absolute media paths, `accessToken` fields, and private hostnames from saved output.

```bash
set -eu
PMS='http://PMS_HOST:32400'
TOKEN='REDACTED_PLEX_TOKEN'
CID='issue-694-readonly-client'
SECTION='SECTION_KEY_FROM_LIBRARY_SECTIONS'
RATING_KEY='RATING_KEY_FROM_A_LIBRARY_ITEM'

H=(-H 'Accept: application/json' \
   -H "X-Plex-Token: ${TOKEN}" \
   -H "X-Plex-Client-Identifier: ${CID}" \
   -H 'X-Plex-Product: Reaparr-issue-694-research' \
   -H 'X-Plex-Version: 0.0-research')
```

### Identify the server and list sections

```bash
curl --silent --show-error --dump-header - "${H[@]}" \
  "${PMS}/"

curl --silent --show-error --dump-header - "${H[@]}" \
  "${PMS}/library/sections"

# Section details advertise types, fields, filters, and sorts where supported.
curl --silent --show-error --dump-header - "${H[@]}" \
  "${PMS}/library/sections/${SECTION}?includeDetails=1"
```

These are GET requests only. The section key must come from the server response; do not assume numeric IDs across servers.

### Full `/all` page and a candidate `addedAt`/`updatedAt` query

```bash
# A bounded full/filtered page. Save headers separately so pagination metadata is visible.
curl --silent --show-error --dump-header /tmp/plex-694-all.headers \
  --output /tmp/plex-694-all.json \
  "${H[@]}" \
  -H 'X-Plex-Container-Start: 0' \
  -H 'X-Plex-Container-Size: 100' \
  --get "${PMS}/library/sections/${SECTION}/all" \
  --data-urlencode 'type=1' \
  --data-urlencode 'sort=addedAt:asc'

# Candidate incremental query. This is a qualification probe, not a claimed contract.
CHECKPOINT=1700000000
curl --silent --show-error --dump-header /tmp/plex-694-updated.headers \
  --output /tmp/plex-694-updated.json \
  "${H[@]}" \
  -H 'X-Plex-Container-Start: 0' \
  -H 'X-Plex-Container-Size: 100' \
  --get "${PMS}/library/sections/${SECTION}/all" \
  --data-urlencode 'type=1' \
  --data-urlencode "updatedAt>>=${CHECKPOINT}" \
  --data-urlencode 'sort=updatedAt:asc,ratingKey:asc'

# Optional field trimming; only use after checking the target PMS API version semantics.
curl --silent --show-error --dump-header - "${H[@]}" \
  --get "${PMS}/library/sections/${SECTION}/all" \
  --data-urlencode 'type=1' \
  --data-urlencode 'includeFields=ratingKey,addedAt,updatedAt,deletedAt,title,type'
```

The candidate query must be compared with an unfiltered baseline over the same test interval. A successful HTTP 200 is not enough: verify that the server actually applies the predicate and returns expected ordering. A 400, ignored predicate, or missing `updatedAt` is an expected qualification outcome.

### Full leaves and item details

```bash
curl --silent --show-error --dump-header /tmp/plex-694-leaves.headers \
  --output /tmp/plex-694-leaves.json \
  "${H[@]}" \
  -H 'X-Plex-Container-Start: 0' \
  -H 'X-Plex-Container-Size: 100' \
  "${PMS}/library/sections/${SECTION}/allLeaves"

curl --silent --show-error --dump-header - "${H[@]}" \
  "${PMS}/library/metadata/${RATING_KEY}"
```

Use the item detail response to compare metadata, `Media`, `Part`, and `Stream` state before and after changes. `allLeaves` is especially useful for TV/music leaf coverage but is still a full read.

### Pagination total without returning content

```bash
curl --silent --show-error --dump-header - "${H[@]}" \
  -H 'X-Plex-Container-Start: 0' \
  -H 'X-Plex-Container-Size: 0' \
  "${PMS}/library/sections/${SECTION}/all"
```

Confirm whether the server returns `X-Plex-Container-Total-Size` and/or `MediaContainer.totalSize`; do not assume every endpoint honors size zero.

### Notification streams (read-only, optional)

```bash
# Keep this bounded; it opens a stream but does not mutate the server.
timeout 30s curl --silent --show-error --no-buffer \
  "${H[@]}" \
  "${PMS}/:/eventsource/notifications"
```

For a WebSocket-capable test tool, the equivalent connection is:

```bash
websocat \
  -H="Accept: application/json" \
  -H="X-Plex-Token: ${TOKEN}" \
  -H="X-Plex-Client-Identifier: ${CID}" \
  "ws://PMS_HOST:32400/:/websockets/notifications"
```

The SSE filter spelling is inconsistent across published material (the official prose uses `filters=...`; the community spec calls the parameter `filter` while giving `filters=...` examples). Start with no filter; if testing filters, record the exact URL and response behavior. Do not infer replay support from a successful connection.

### Unsupported-route capability probe

This is a safe GET intended to distinguish a server/provider implementation from a documented contract. It is not an assertion that the route exists or should exist:

```bash
curl --silent --show-error --dump-header /tmp/plex-694-changes.headers \
  --output /tmp/plex-694-changes.body \
  "${H[@]}" \
  --get "${PMS}/library/sections/${SECTION}/changes" \
  --data-urlencode "since=${CHECKPOINT}"
printf 'HTTP status and headers are in /tmp/plex-694-changes.headers\n'
```

If a server returns 200, preserve the exact PMS version, response schema, required parameters, ordering, deletion behavior, and retention observations. Also probe only on a disposable test server whether the implementation uses `from`, `to`, `cursor`, `revision`, or another parameter; do not ship behavior based on a single undocumented success.

## Qualification test matrix

Run these tests only against a disposable library or a server with a verified backup. They are designed to answer contract questions that the published docs leave open.

| Test | Procedure | Evidence to record | Pass condition for an incremental design |
|---|---|---|---|
| Baseline shape | Enumerate `/all` and `/allLeaves` for each supported library type; fetch a sample item detail. | PMS version/build, API version header, type, `ratingKey`, hierarchy, fields, media/part/stream counts, page headers. | A complete baseline can be reconstructed and item identity is stable within a server/library. |
| Add | Add one uniquely named test item, scan, and query with an overlap around the recorded `addedAt`/`updatedAt`. | Before/after section marker, timestamps, all vs allLeaves visibility, event payloads, page/order. | The add is observable by the candidate query, or the system falls back to reconciliation. |
| Metadata-only edit | Change title/summary/poster or run a targeted metadata refresh without changing the file. | Parent/leaf `updatedAt`, section `contentChangedAt`, item detail diff, event messages. | Every supported edit that matters to Reaparr changes a queried item or is detected by fallback. |
| Media/part change | Replace or re-analyze a media file while preserving logical item identity. | Parent `updatedAt`, `Media`/`Part` IDs, file/size/duration/codecs, section marker. | Parent/leaf candidate query reliably surfaces media/part changes; otherwise force detail refresh/full reconciliation. |
| Stream change | Add/change an audio/subtitle stream or refresh local assets. | `Stream` list and selection attributes before/after, `updatedAt`, event messages. | Stream-only mutations are either surfaced by the timestamp query or covered by periodic detail reconciliation. |
| Rename/move | Rename or move a test file and scan. | `Part.file`, `Part.key`, item timestamp, section marker, delete/add appearance. | The synchronizer handles identity-preserving updates and does not mistake them for a silent delete/add without qualification. |
| Trash deletion | Remove a file, scan, and inspect before Empty Trash. Restore it before Empty Trash. | Presence in `/all`/`allLeaves`, `deletedAt` if any, detail status, event state 9 if emitted. | Semantics are explicitly recorded; no assumption that `deletedAt` is a durable tombstone. |
| Empty Trash | Empty the test library trash after the prior test. | Whether item disappears, whether detail becomes 404, whether any tombstone remains and for how long. | Deletion detection remains correct after tombstone removal, normally through reconciliation or a verified feed. |
| Direct delete | Only with explicit test-server permission, delete a disposable item via the app. | Current-list disappearance, event, `deletedAt`, post-delete detail. | Direct deletion is not conflated with file-missing/trash behavior. |
| Timestamp boundaries | Perform two changes in the same second and one just before/after a checkpoint. Query `>` and `>=` forms. | Returned keys, exact timestamps, duplicates across polls. | Overlap plus idempotent deduplication prevents second-level misses. |
| Paging race | Start a multi-page read; mutate/add an item between page requests. Repeat with stable sort and candidate filter. | Offset/total headers, duplicates/gaps, ordering, whether totals change. | Either the API is proven snapshot-like or the design compensates with overlap/reconciliation. |
| Marker behavior | Record `contentChangedAt` before/after add, metadata edit, part/stream change, delete, restore, Empty Trash, restart. | Raw values and server version; whether it increases and what item set changed. | Use only as a trigger unless a repeatable mapping to complete changed keys is proven. |
| Event loss/reconnect | Subscribe before each mutation; disconnect before another; reconnect with/without `Last-Event-ID`; compare events to full reads. | Payload schema, event IDs (if any), ordering, duplicate/lost events, replay behavior. | Events are hints; recovery does not depend on replay unless explicitly proven. |
| Version matrix | Repeat essential tests on lowest supported PMS, versions around 1.41.9/1.42/1.43.4, and current target builds. | PMS build, `X-Plex-Pms-Api-Version`, status/body/field differences. | A version-qualified behavior table exists; undocumented behavior is not silently generalized. |
| Auth/library matrix | Compare owner/admin, shared user, restricted library, and token query vs header. | Sections visible, 401/403, field redaction, event visibility. | The synchronizer records access scope and never treats a restricted view as a deletion. |
| Performance | Measure baseline full scan, candidate timestamp query, `allLeaves`, and detail re-fetch under representative item counts. | Page size, total bytes, elapsed time, server load/activity, number of details. | The optimization has measured benefit without sacrificing reconciliation/recovery. |

## Recovery and data-model implications

1. **Persist identity and scope:** PMS machine identifier, server URL/connection identity, library section key/UUID, library type, and access/account scope. A numeric section key is local to a server and must not be treated as globally stable.
2. **Use overlap, not a bare timestamp:** If qualification validates `addedAt`/`updatedAt`, poll with a conservative epoch overlap (at least several seconds, sized for clock/scan behavior), sort deterministically where supported, and deduplicate by server + section + `ratingKey`. Re-fetch details for candidate items to capture nested media/part/stream changes.
3. **Do not persist `contentChangedAt` as a replay cursor:** The forum evidence calls it an internal shared ChangeStamp, but no API maps it to a complete change set or history.
4. **Treat events/webhooks as hints:** They reduce latency but do not remove the need for periodic reads. On reconnect, resume from a read-based overlap/full reconciliation; do not assume SSE/WebSocket replay.
5. **Handle deletes by reconciliation unless a feed is proven:** A missing item after a full authorized enumeration is stronger evidence than a one-off `deletedAt` field. Consider Trash and restricted-library permissions before deleting local state. If `deletedAt` is observed, store it as an observed server field, not as a guaranteed tombstone sequence.
6. **Keep a periodic complete baseline:** The baseline catches timestamp omissions, fields that do not advance parent `updatedAt`, missed events, pagination races, Empty Trash, and server upgrades. The interval should be chosen from measured library size/change rate and recovery requirements.
7. **Do not conflate playback history:** `/status/sessions/history/all` and webhook playback events describe viewing activity, not library mutation history.

## Recommendation for issue 694

* Do not implement `/library/sections/{id}/changes` as a supported API without a reproducible, version-qualified server response and explicit deletion/checkpoint semantics.
* Implement/retain a complete enumeration path (`/all` or provider-discovered content key; `/allLeaves` where leaf coverage is required) as the correctness anchor.
* Qualify `addedAt` and `updatedAt` media queries as an optimization. If `updatedAt` does not reliably surface metadata/media/part/stream changes across supported types, use a wider detail-reconciliation window or periodic full scan rather than claiming delta correctness.
* Use `contentChangedAt` only to avoid unnecessary scans when it is unchanged; never use it alone to advance a durable checkpoint.
* Use SSE/WebSocket and, where available, Plex Pass webhooks as latency hints. Always recover with read reconciliation because published docs/specs do not provide replay guarantees.
* Treat deletions as a separate correctness problem. Until `deletedAt`/event semantics are verified across Trash, Empty Trash, and direct deletion, detect deletes by comparing an authorized complete current key set with stored keys.

## Source index

### Official Plex sources

1. [Plex Media Server API documentation](https://developer.plex.tv/pms/) — content types, `Accept`, `X-Plex-*` headers, token auth, media query language, section/all/allLeaves operation descriptions, pagination, API version history, section/item examples, event stream description, provider-path guidance.
2. [Plex Media Server URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/) — published XML examples for `/library/sections`, `/library/sections/{id}/all`, `/library/metadata/{ratingKey}` with `Media`/`Part`/`Stream`; token requirement; scan/refresh command examples.
3. [How do I delete something from my Library?](https://support.plex.tv/articles/202606363-how-do-i-delete-something-from-my-library/) — scan/removal, Library Trash, restoration, Empty Trash, direct deletion lifecycle.
4. [Scanning vs Refreshing a Library](https://support.plex.tv/articles/200289306-scanning-vs-refreshing-a-library/) — official distinction between file scans and metadata refreshes.
5. [Webhooks](https://support.plex.tv/articles/115002267687-webhooks/) — Plex Pass/admin requirement, `library.new`/`library.on.deck`, outbound payload and example metadata.
6. [ContentChangedAt forum discussion](https://forums.plex.tv/t/what-information-does-the-value-in-contentchangedat-represent/935804) — Plex forum reply describing `contentChangedAt` as an internal shared incrementing ChangeStamp; not formal API-contract text.

### Published non-contract sources

7. [LukeHagar/plex-api-spec raw OpenAPI](https://raw.githubusercontent.com/LukeHagar/plex-api-spec/main/plex-api-spec.yaml) — community spec version 1.1.1; `/library/all`, `/library/sections/all`, SSE/WebSocket paths, pagination/security examples; no documented `/changes` path or `deletedAt` schema field.
8. [python-plexapi `library.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/library.py) — client-side `/all` query construction, date filters, pagination, `recentlyAdded`, manually exposed `updatedAt` field/sort.
9. [python-plexapi `base.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/base.py) — client-side offset paging.
10. [python-plexapi `alert.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/alert.py) — client-side WebSocket notifications and state-9 deletion note.
11. [python-plexapi `server.py`](https://raw.githubusercontent.com/pkkid/python-plexapi/master/plexapi/server.py) — client-side playback history endpoint wrapper.
12. [clambin/mediaclients `plex/library.go`](https://raw.githubusercontent.com/clambin/mediaclients/main/plex/library.go) — Go client section/item models and full `/allLeaves` pagination.
13. [clambin/mediaclients `plex/timestamp.go`](https://raw.githubusercontent.com/clambin/mediaclients/main/plex/timestamp.go) — client timestamp decoding.
14. [Older community OpenAPI JSON mirror](https://gist.githubusercontent.com/justingarfield/426381d6fb848e5e579b7f094cd2a535/raw) — historical `/library/all` and response/pagination examples.

All client/spec observations above are explicitly non-contract evidence. No source listed here was used to claim universal endpoint nonexistence.
