# Issue 694 — first-party Plex event alternatives

## Scope and evidence labels

This note evaluates first-party Plex mechanisms that might be used to drive a library-level incremental/delta sync: webhooks, the Plex Media Server (PMS) EventSource/WebSocket notification streams, Plex push notifications, and direct reads of the PMS SQLite database. It also records the ordinary PMS library HTTP API because every event-based design needs a canonical read/reconciliation path.

Evidence is deliberately separated:

- **[Official guarantee]** — a Plex-owned support/developer document or Plex-owned GitHub repository.
- **[Official published example]** — a sample application or payload published by Plex, useful evidence of intended use but not a delivery contract.
- **[Client-source evidence]** — a maintained client library's source/documentation describing observed PMS behavior; not a Plex compatibility guarantee.
- **[Third-party reference]** — a non-Plex API reference; useful for reproducible protocol details, but not an official contract.
- **[Inference]** — a consequence for a synchronizer, derived from the source facts.

No Plex server was contacted here; there are no live measurements or live payloads in this document. Examples with placeholders are not claimed to be observed on a particular server.

For actual authenticated PMS requests, signed-in Plex Web inspection, tested timestamp boundaries and compact inventory projection, see the [companion live evidence](694-plex-live-api-evidence.md). This note's no-live-measurements limitation applies to event/database-alternative research only.

## Executive verdict

1. **Plex does not publish a durable, replayable library change log with a server cursor.** The official PMS EventSource/WebSocket documentation describes a live event stream and filtering, but does not document an event ID, cursor, `Last-Event-ID`, replay window, retention period, ordering guarantee, or resume token. Plex's Webhooks and Push Notifications articles likewise do not specify a retry/queue/retention contract.
2. **Webhooks are useful additions/playback triggers, not a library delta API.** `library.new` covers new accessible content and `library.on.deck` covers a change to a user's On Deck. There is no documented webhook event for metadata edits, media/part/stream changes, a metadata refresh completion, or deletion. Webhook delivery is Plex Pass-only for the admin/owner account and has no documented replay behavior.
3. **SSE/WebSocket notifications are useful live scan/activity hints, but not an authoritative delta feed.** Official docs describe broad categories (library scans, media changes, preference changes, activity progress), not a complete stable event/payload schema. A widely used client maps internal library timeline states including `created`, processing stages, and `deleted`, but that mapping is client-source evidence rather than a published Plex contract. A reconnect cannot be assumed to fill a gap.
4. **The database can expose more change-oriented fields, but it is an internal local implementation detail.** Plex's own repair/restore instructions require stopping the server before opening or replacing the database. A published 2019 schema snapshot contains `updated_at`/`deleted_at` fields and a `library_timeline_entries` table, but Plex does not publish a versioned schema or stability guarantee. This is unsuitable as a remote supported API and is risky as the sole sync source.
5. **The authoritative design must be snapshot/reconciliation based.** Use an event or webhook only to wake the synchronizer and narrow which items to re-read. Persist the receiver's own durable queue/sequence. On connection loss, server downtime, database restore/corruption, unknown server-version behavior, or any suspected missed event, perform a full paginated reconciliation. Detect deletes by comparing current state with the previous state; do not expect a Plex tombstone stream.

## 1. Ordinary PMS HTTP API: the required reconciliation baseline

### Authentication and API/version evidence

- **[Official guarantee]** Plex's PMS API reference says most endpoints require a token obtained from plex.tv, normally in the `X-Plex-Token` header. It identifies `X-Plex-Client-Identifier` as typically required and says `X-Plex-*` headers may also be supplied as query arguments. See [PMS API — API Info / Headers / Auth](https://developer.plex.tv/pms/).
- **[Official guarantee]** The older support reference gives the same read-only pattern, for example `GET http://[PMS_IP_Address]:32400/library/sections?X-Plex-Token=...`, and shows library and item XML responses. See [Plex Media Server URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/).
- **[Official guarantee]** The current PMS API page has a versioning section. It treats the pre-publication API as `0.0`, documents API `1.0.0` for PMS `>=1.41.9`, and lists later additions by PMS version. The notification section does not state an endpoint-specific minimum PMS version. Do not infer a minimum version for EventSource/WebSocket from the general API version table.
- **[Inference]** A library-level synchronizer should use an owner/admin token when the target is the owner's complete library. A shared-user token is not a documented substitute for owner visibility; event visibility and library access are account-scoped.

### Snapshot, item detail, pagination, and response size

- **[Official guarantee]** Library sections are listed at `/library/sections`; a section's contents are available from its `key` (the support example uses `/library/sections/29/all`); a canonical item can be fetched at `/library/metadata/{ratingKey}`. The item response can contain `Media`, `Part`, and nested `Stream` elements. The support example explicitly describes `streamType=1` video, `2` audio, and `3` subtitle. See [Plex Media Server URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/).
- **[Official guarantee]** The PMS API reference documents `X-Plex-Container-Start` and `X-Plex-Container-Size` for paged responses. A paged `MediaContainer` normally reports `offset`, current `size`, and optionally `totalSize`; the response must be inspected because not every endpoint paginates in exactly the requested way. It also documents a `limit` query parameter for rich media queries, but `limit` does not return the full collection total. See [PMS API — Pagination](https://developer.plex.tv/pms/).
- **[Official guarantee]** The reference documents response customization (`includeFields`, `excludeFields`, `includeElements`, `excludeElements`, and optional-field/element variants). It explicitly describes these as requests, not guarantees: an endpoint or item may ignore them. See [PMS API — Response Customization](https://developer.plex.tv/pms/).
- **[Inference]** A full reconciliation must continue until the server's actual pagination metadata says the section is exhausted. A fixed page size or a single response cannot be treated as the complete library. A detail read is required whenever the target state includes nested media, part, or stream properties that the section page did not return.

### Timestamp/query caveat

- **[Official guarantee]** PMS metadata examples expose fields such as `addedAt` and `updatedAt`; the support URL-command example includes both on a `Video` item. The API's Media Queries section describes comparison operators, including `>>=` and `<<=`, and relative date values (for example, `>>=-3y`) for fields that a particular section advertises in its `Field` list. The section details endpoint is `/library/sections/{sectionId}?includeDetails=1`. See [PMS API — Media Queries](https://developer.plex.tv/pms/) and [Plex Media Server URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/).
- **Important limitation [Official guarantee + inference]:** the current API reference does not present `updatedAt` as a universal, endpoint-independent change-feed parameter for every `/library/sections/{id}/all` response. It says fields are declared per section and that query support depends on the endpoint. Therefore `updatedAt` filtering is a possible optimization only after probing the section's advertised fields and validating behavior on the supported PMS versions; it is not a documented cross-version event cursor or deletion feed.
- **Inference:** even where `updatedAt` filtering works, timestamps are item attributes rather than server-issued event sequence numbers. Ties, clock/second granularity, metadata-vs-file changes, and missing tombstones make it unsuitable as the only correctness mechanism. An overlap window plus ID comparison can reduce work, but recovery still needs a complete reconciliation.

## 2. Plex Webhooks

### Official event set and payload

- **[Official guarantee]** Webhooks are configured in Plex Web App account settings and Plex Media Server posts a JSON payload to the configured URL. For some events a thumbnail JPEG is an additional multipart part. The article describes the payload as top-level `event`, `user`, and `owner` flags plus `Account`, `Server`, `Player`, and `Metadata` objects. See [Webhooks](https://support.plex.tv/articles/115002267687-webhooks/).
- **[Official guarantee]** The documented event names are:

  | Group | Event | Official meaning |
  | --- | --- | --- |
  | New content | `library.on.deck` | An item is added that appears in the user's On Deck; a poster is attached. |
  | New content | `library.new` | A new item is added to a library to which the user has access; a poster is attached. |
  | Playback | `media.pause`, `media.play`, `media.rate`, `media.resume`, `media.scrobble`, `media.stop` | Playback/rating activity; `media.scrobble` means playback passed the 90% mark. |
  | Server-owner | `admin.database.backup` | Scheduled-task database backup completed successfully. |
  | Server-owner | `admin.database.corrupted` | Server database corruption was detected. |
  | Server-owner | `device.new` | A device accessed the owner's server; this can be background connection testing, not active playback. |
  | Server-owner | `playback.started` | A shared user started playback on the owner's server. |

  There is no documented `library.deleted`, `library.updated`, metadata-refresh, media-part, media-stream, or scan-complete webhook event.
- **[Official guarantee]** The official example payload contains identifiers (`ratingKey`, `key`, `guid`, `librarySectionID`) and item fields such as `addedAt` and `updatedAt`, but the article does not define the webhook payload as a complete canonical metadata/media/part/stream snapshot. Its example has no nested `Media`, `Part`, or `Stream` data. A receiver needing those values must re-read the canonical item endpoint.
- **[Inference]** `library.new` can be a useful hint to hydrate a newly added key. It cannot tell a synchronizer that all metadata and stream analysis has finished, and it does not cover later metadata/file changes. `library.on.deck` is user-view-specific and is not a library-level change signal.

### Scope, requirements, and version evidence

- **[Official guarantee]** Webhooks are a premium feature requiring an active Plex Pass subscription for the admin/owner account of the PMS. They are tied to a specific Plex account. The article says webhooks travel with the user across servers, provided the server is running PMS `v1.3.4` or newer.
- **[Official guarantee]** The event is scoped by the configured account and access: a user's webhook receives events for that user and shared users as described by Plex, while owner-only events are explicitly restricted to the server owner/admin.
- **[Official guarantee]** The receiver is an HTTP URL configured in the account UI. Plex's article does not specify a webhook registration API, a callback signature, a shared-secret mechanism, a retry count/backoff, an ordering guarantee, a delivery ID, or an undelivered-event retention period.
- **[Inference]** If the receiver is unreachable, a reliable client must assume a delivery gap. The published documentation does not justify assuming that Plex queues the event until the receiver returns or replays it later. Secure transport/authentication and idempotency are receiver responsibilities unless a newer Plex contract explicitly documents otherwise.

### Authentic published examples

These are Plex-owned repositories linked by the Webhooks support article, so they demonstrate the intended payload shape and use, but they are not a delivery/SLA specification:

- **[Official published example]** [plexinc/webhooks-notifications `index.js` (pinned commit)](https://github.com/plexinc/webhooks-notifications/blob/cef376f0f51e99060abae24b4a9833f368126528/index.js) parses `req.body.payload` from a multipart request, reacts to `media.play`/`media.pause`/`media.resume`/`media.stop`, and returns HTTP 200. Its [README](https://github.com/plexinc/webhooks-notifications/blob/cef376f0f51e99060abae24b4a9833f368126528/README.md) instructs users to register the URL in Plex Web App.
- **[Official published example]** [plexinc/webhooks-slack `index.js` (pinned commit)](https://github.com/plexinc/webhooks-slack/blob/89b9d9bf535de57791358592b8eac4b45d2341e7/index.js) parses `req.body.payload`, handles the optional `thumb`, filters to media playback/rating events, and returns HTTP 200. It is an integration example, not a durable queue.
- **[Official published example]** [plexinc/webhooks-home-automation](https://github.com/plexinc/webhooks-home-automation) uses a webhook to control lighting. This demonstrates that webhooks are intended for immediate automation triggers rather than a library journal.

### Webhook suitability

| Requirement | Webhook result | Reason |
| --- | --- | --- |
| Add detection | **Partial** | `library.new` is explicit, but account/library access scoped and not a completion marker. |
| Metadata changes | **No documented coverage** | No metadata-updated/refresh event. `updatedAt` in a payload is a value, not an event/cursor. |
| Part/stream changes | **No documented coverage** | No part/stream event or complete nested payload contract. |
| Deletions | **No documented coverage** | No delete event or tombstone. |
| Replay/checkpoint | **No** | No delivery ID, retry, retention, or replay contract. |
| Version/auth | **Plex Pass owner/admin; PMS >=1.3.4** | See official requirements above. |
| Overall | **Optional trigger only** | Persist locally, hydrate canonical state, and reconcile independently. |

## 3. PMS EventSource (SSE) and WebSocket notifications

### Officially documented transport

- **[Official guarantee]** The current PMS API reference documents a live EventSource/SSE endpoint `GET /:/eventsource/notifications` and a WebSocket alternative `GET /:/websocket/notifications`. It describes these as real-time server events covering library scans, preference changes, media changes, and activity progress. It also documents a `filters` query parameter (the page's parameter heading has appeared as `filter`, while the examples use `filters`) with include/exclude forms such as `filters=-log`, `filters=foo,bar`, `filters=`, and `filters=-foo,bar`. See the [EventSource operation](https://developer.plex.tv/pms/#tag/Events/operation/eventsourceGetSlash), [WebSocket operation](https://developer.plex.tv/pms/#tag/Events/operation/websocketGetSlash), and [PMS API reference](https://developer.plex.tv/pms/).
- **[Official guarantee]** The PMS API reference requires the usual PMS authentication headers, especially `X-Plex-Token`; it identifies `X-Plex-Client-Identifier` as typically required. It does not state that a Plex Pass subscription is required for these local server notification transports.
- **[Official guarantee]** The official page does not publish a complete event-name catalogue or a stable schema for each notification type. It also warns generally that paths which are not documented are not part of the API contract even if they happen to exist in a provider.

### Exact read-only notification requests

These are reproducible request forms with placeholders. They are **not run samples** and must not be populated with a real token in source control or logs.

**Official SSE form:**

```bash
curl --no-buffer \
  -H 'Accept: text/event-stream' \
  -H 'X-Plex-Client-Identifier: reaparr-<stable-client-id>' \
  -H 'X-Plex-Token: <PMS-user-token>' \
  'https://<plex-direct-host>:<port>/:/eventsource/notifications?filters=-log'
```

The official connection form uses the server's reachable PMS host (Plex's remote examples use a `plex.direct` host) and the token/client headers described above. A local non-TLS PMS may use the usual `http://<host>:32400` form; the support URL-command documentation shows that local form. The request is a GET and has no mutating action.

**Official WebSocket target:**

```text
wss://<plex-direct-host>:<port>/:/websocket/notifications?filters=-log
```

Open a WebSocket to that target and send the same `X-Plex-Token` and `X-Plex-Client-Identifier` headers in the handshake. The official reference documents the URL and filters, but does not prescribe a particular command-line WebSocket client or a replay protocol.

**Legacy/client-source compatibility note:** the pinned `python-plexapi` source below uses the plural path `/:/websockets/notifications` and derives a `ws`/`wss` URL from the server URL. Plexopedia's third-party reference also documents the plural path. That is useful for testing older PMS/client combinations, but the current official API page documents singular `/:/websocket/notifications`; do not treat the plural path as a current official contract without compatibility testing.

### Published protocol and client-source evidence

- **[Third-party reference]** [Plexopedia — Listen for Events](https://www.plexopedia.com/plex-media-server/api/server/listen-events/) documents the SSE response as `text/event-stream`, with `event:` and `data:` fields, and publishes examples for `ping`, `activity`, `playing`, and `transcodeSession.start/update/end` notifications. It says the server sends a `ping` about every 10 seconds and lists filters `activity`, `playing`, `transcodeSession.start`, `transcodeSession.update`, and `transcodeSession.end`. Treat those details as a third-party description, not a Plex compatibility guarantee.
- **[Third-party reference]** [Plexopedia — Listen for Notifications](https://www.plexopedia.com/plex-media-server/api/server/listen-notifications/) documents the plural WebSocket path and a `NotificationContainer` containing log notifications when the `log` filter is selected. This is diagnostic/log streaming, not a durable library change log.
- **[Client-source evidence]** The pinned [python-plexapi `AlertListener`](https://github.com/pushingkarmaorg/python-plexapi/blob/09ea3c51043160aa3b7c9cc268ab32d39f4bb970/plexapi/alert.py) opens `/:/websockets/notifications`, parses each JSON message's `NotificationContainer`, and keeps no server cursor or replay state. Its docstring records observed library timeline states for identifier `com.plexapp.plugins.library`:

  | State | Meaning recorded by the client |
  | ---: | --- |
  | `0` | item created |
  | `1` | reporting progress on item processing |
  | `2` | matching the item |
  | `3` | downloading metadata |
  | `4` | processing downloaded metadata |
  | `5` | item processed |
  | `9` | item deleted |

  The same source notes processing may remain at state `1` when no metadata agent is configured. These values are valuable for an implementation investigation, but they are not an official versioned event schema, do not identify every changed field, and do not establish that state `9` is a durable deletion tombstone.
- **[Inference]** The stream can be used to wake a synchronizer around scans and then trigger item reads. It cannot by itself answer which metadata, media, part, or stream fields changed unless the receiver re-fetches and compares canonical item state. An activity event may identify a scan/context rather than every affected leaf item.

### Replay, retention, disconnect, and version limits

- **[Official guarantee by omission of contract]** The published Events section does not document event IDs, `Last-Event-ID`, a cursor parameter, a resume token, a replay endpoint, a retention window, or ordering/delivery guarantees. It only describes connecting and filtering a real-time stream.
- **[Third-party reference]** The published SSE examples show `event` and `data` frames and the periodic ping, but no documented event ID or replay procedure. The client source above calls `run_forever()` and reconnecting would start a new live connection; it does not supply a server checkpoint.
- **[Inference]** A disconnect, PMS restart, network outage, or client downtime must be treated as a potentially unbounded gap. Reconnecting to the stream is necessary for future events but cannot be assumed to recover events emitted during the gap. The recovery operation is a paginated library reconciliation.
- **Version evidence:** the current API page does not publish an EventSource/WebSocket-specific minimum PMS version. The old client source is evidence that the plural WebSocket path has existed in deployed PMS versions, not a promise for all versions. A client should feature-detect the documented endpoint and keep a snapshot fallback.

### Event-stream suitability

| Requirement | EventSource/WebSocket result | Reason |
| --- | --- | --- |
| Add detection | **Potential live hint** | Official docs mention library scans; client source observes a created state. |
| Metadata changes | **Potential broad hint, not a field diff** | Official docs say media changes, but no complete event/payload schema. |
| Part/stream changes | **No documented coverage** | No stable event type or nested payload contract; hydrate the item. |
| Deletions | **Observed-but-unsupported hint** | Client source maps state `9`; Plex does not publish it as a stable tombstone API. |
| Replay/checkpoint | **No documented support** | No cursor/ID/replay/retention contract. |
| Version/auth | **PMS token/client ID; no Plex Pass requirement stated; no event-specific minimum stated** | Must account for endpoint/version differences. |
| Overall | **Optional live trigger only** | Persist own queue and reconcile after every gap. |

## 4. Plex Push Notifications (mobile/Plex services)

Push notifications are related to webhooks but are not a server-to-Reaparr event API.

- **[Official guarantee]** Personal-server push notifications require PMS `v1.16.0` or newer. The PMS and mobile Plex app must be signed in to a Plex account. The server owner must opt in under *Settings > Server > General > Push Notifications*; the feature is Plex Pass-only. See [Push Notifications](https://support.plex.tv/articles/push-notifications/).
- **[Official guarantee]** Personal-media notification categories include new media shared with a user, new server shared with a user, new content added to a Movie/TV/Music library, new TV episodes in Continue Watching, and playback started for an owner's server. PMS categories include new devices, database corruption detected, and database backed up. Delivery is through Plex services and is configured in mobile/account notification settings; it is not a callback URL that Reaparr can consume directly.
- **[Official guarantee]** Plex documents no machine-readable cursor, replay/retention window, per-item delete event, metadata update event, part/stream event, or callback delivery guarantee for these notifications. The docs also note that the first media notification after enabling the feature is not sent until a scan initializes it.
- **[Inference]** Push is unsuitable for synchronization. At most it can be a human-facing signal that a later server API reconciliation may be worthwhile. It adds Plex service/account/app dependencies and a Plex Pass requirement without supplying a durable change log.

## 5. Direct PMS SQLite/database tracking

### What Plex officially supports

- **[Official guarantee]** Plex identifies the main database as `com.plexapp.plugins.library.db` under `Plug-in Support/Databases`; database backups also include `com.plexapp.plugins.library.blobs.db`. See [Restore a Database Backed Up via Scheduled Tasks](https://support.plex.tv/articles/202485658-restore-a-database-backed-up-via-scheduled-tasks/) and [Where is the Plex Media Server data directory located?](https://support.plex.tv/articles/202915258-where-is-the-plex-media-server-data-directory-located/).
- **[Official guarantee]** Plex's database-repair instructions say to quit/stop PMS before opening the database, make a copy first, and use the bundled Plex SQLite tool. They explain that regular SQLite commands can be run in the shell, but the bundled tool is needed for Plex-specific repair behavior. See [Repair a Corrupted Database](https://support.plex.tv/articles/repair-a-corrupted-database/).
- **[Official guarantee]** Scheduled Tasks creates a core SQL database backup every three days if the database is not already corrupted and keeps up to three rotating copies. Plex explicitly warns that this is not a backup of all metadata content. See [Scheduled Tasks](https://support.plex.tv/articles/201553286-scheduled-tasks/).
- **[Inference]** A database reader therefore needs local filesystem access and a safe copy/snapshot workflow. There is no documented remote SQL endpoint, Plex API token scope, or Plex Pass feature for querying these internal tables. Reading the live file while PMS is running is not the workflow Plex documents.

### Published schema evidence (not a Plex contract)

- **[Client/source evidence]** The pinned [mutanthost/plex_schema `plex3.sql`](https://github.com/mutanthost/plex_schema/blob/5086c4ff278732d0246837d86e63adf0c4f2676e/plex3.sql) is a SQLiteStudio-generated schema snapshot from 2019, not a Plex-owned schema specification. It contains the following change-oriented structures:

  | Table | Fields shown in that snapshot | Possible use / risk |
  | --- | --- | --- |
  | `metadata_items` | `added_at`, `created_at`, `updated_at`, `deleted_at`, `changed_at`, `resources_changed_at` | Candidate metadata/resource change detection; internal names and semantics can change. |
  | `media_items` | `created_at`, `updated_at`, `deleted_at` | Candidate media-row change/deletion detection; no public stability guarantee. |
  | `media_parts` | `created_at`, `updated_at`, `deleted_at` | Candidate part/file change/deletion detection; hard purges may remove evidence. |
  | `media_streams` | `created_at`, `updated_at` in the snapshot | Stream rows can change without a `deleted_at` column in this old snapshot; absence is not a tombstone. |
  | `library_timeline_entries` | `library_section_id`, `metadata_item_id`, `state`, `updated_at` | Candidate scan-processing state; retention/update semantics are undocumented. |

- **[Client-source evidence]** The Python PlexAPI state table above is consistent with the old `library_timeline_entries` concept, but neither source is a current PMS schema/API contract. Do not hard-code table names, columns, state values, or counter semantics without a versioned compatibility policy and a server-specific migration test matrix.
- **[Inference]** If a local integration elects to use a copied DB as an optimization, it must treat the result as a current-state snapshot, not as a guaranteed append-only journal. A `deleted_at` field may help when present, but a row can be hard-deleted, a parent can be deleted while children are cascaded, the library trash can retain an unavailable item, and database optimization/restore can rewrite history.

### Delete/trash semantics that matter to sync

- **[Official guarantee]** Scanning checks folders/subfolders for new or removed media and is recommended after adding/deleting/renaming/moving files. See [Scanning vs Refreshing a Library](https://support.plex.tv/articles/200289306-scanning-vs-refreshing-a-library/).
- **[Official guarantee]** When a file is missing/unavailable, PMS normally puts the library item in Library Trash. It remains restorable if the file returns until Trash is emptied. If automatic emptying is enabled, it is removed after a scan; manually emptying Trash discards it and it can no longer be automatically restored. See [Emptying Library Trash](https://support.plex.tv/articles/200289326-emptying-library-trash/) and [Library settings](https://support.plex.tv/articles/200289526-library/).
- **[Inference]** A synchronizer must choose whether “unavailable/in Trash” is an update or a delete. Neither `library.new` nor the official notification docs supplies that policy. A live API snapshot may expose an unavailable item before it disappears; after Trash is emptied, only comparing against the previous local state can identify its removal.

### Database tracking suitability

| Requirement | Direct DB result | Reason |
| --- | --- | --- |
| Add/update detection | **Potentially strong on one compatible installation** | Internal timestamps/timeline rows may expose changes sooner than a full API walk. |
| Metadata/part/stream changes | **Potentially observable, not portable** | Old schema has row timestamps, but no supported semantics or migration contract. |
| Deletions | **Potentially observable only while tombstones remain** | Trash/hard-delete/cascade/optimization can remove evidence; schema is unofficial. |
| Replay/checkpoint | **No** | Internal rows are not documented as an append-only log or retained journal. |
| Auth/version | **Local file access; no Plex Pass; server must be stopped for documented DB access** | Not viable for a remote PMS without filesystem access. |
| Overall | **Optional local optimization/diagnostic only** | Must be paired with HTTP reconciliation and a restore detector. |

## 6. Deletion, metadata, media-part, and stream coverage matrix

| Mechanism | New item | Metadata edit/refresh | Media/part/stream change | Delete/missing file | Durable replay |
| --- | --- | --- | --- | --- | --- |
| Webhook | Official `library.new` (account scoped) | Not documented | Not documented | Not documented | Not documented |
| PMS SSE/WebSocket | Official docs mention scans; client source observes state `0` | Broad “media changes”/processing hints only; no stable field diff | No documented event contract; re-read item | Client source maps state `9`, but unsupported as a tombstone | No documented cursor/replay/retention |
| Push notifications | New library content / Continue Watching categories | Not documented | Not documented | Not documented | Not documented |
| HTTP library snapshot | Present in current state | Present after re-read | Present in canonical `Media` → `Part` → `Stream` response | Absence or unavailable/trash state can be compared | The client owns snapshots; PMS supplies no historical stream |
| Internal SQLite | Current rows/timeline may show it | Internal timestamps/timeline may show it | Old schema has row timestamps | Old schema may have `deleted_at`; hard deletes/trash caveats | No supported journal/retention contract |

**Conclusion [Inference]:** no event alternative covers all four change classes with a documented replayable checkpoint. Events can lower latency and reduce the common-case reads, but they cannot replace a full reconciliation for correctness.

## 7. Restore and downtime implications

### PMS stopped or unavailable

- **[Official guarantee]** The DB restore procedure starts by stopping/ quitting PMS, moving aside the current DB and WAL/SHM files, copying dated backup files into place, restoring permissions, and restarting PMS. See [Restore a Database Backed Up via Scheduled Tasks](https://support.plex.tv/articles/202485658-restore-a-database-backed-up-via-scheduled-tasks/).
- **[Official guarantee]** The scheduled database backup is only the core SQL database; it is not all metadata/artwork content. The normal data-directory backup covers a broader set of server data. See [Scheduled Tasks](https://support.plex.tv/articles/201553286-scheduled-tasks/) and [Backing Up Plex Media Server Data](https://support.plex.tv/articles/201539237-backing-up-plex-media-server-data/).
- **[Inference]** While PMS is down, a WebSocket/SSE connection is gone and webhook callbacks cannot be delivered by that server. Plex publishes no event-log handoff/replay mechanism for the downtime. A later connection only covers future events; the synchronizer must reconcile the current library.
- **[Inference]** A restored DB can move the observed state backwards: rows, metadata matching, timestamps, trash status, and viewstate may reflect the backup point rather than the last state seen by Reaparr. Even if rating keys remain stable, an `updatedAt` watermark from before restore cannot prove that no earlier item changed. Treat restore/corruption signals, machine identity changes, or a detected timestamp regression as a new snapshot epoch and reconcile all sections.
- **[Inference]** The “every three days/up to three copies” statement is not a guaranteed replay horizon. It must not be converted into “nine days of history”: tasks can be disabled, delayed, or skipped when the DB is corrupted, and the docs do not promise exact retention timing.

### Plex Trash and scans

- **[Official guarantee]** A missing network share or drive can cause items to enter Trash rather than disappear immediately. Automatic emptying is a setting, not an event-stream guarantee. See [Emptying Library Trash](https://support.plex.tv/articles/200289326-emptying-library-trash/).
- **[Inference]** A full reconciliation after recovery should model “currently present,” “currently unavailable/trash,” and “absent after trash purge” distinctly if the target system needs reversible delete semantics. Events alone cannot reconstruct which transition occurred while Reaparr was offline.

## 8. Request-cost model (symbolic; no invented measurements)

Let:

- `S` = number of library sections;
- `N_s` = number of items returned by section `s` (choose the target item granularity, e.g. leaf episodes/tracks or all hierarchy nodes);
- `P_s` = the actual page size accepted by that section/API call;
- `U` = number of unique item keys that an event-driven run must hydrate in detail;
- `E` = number of event frames received during the interval;
- `D` = number of changed items in the interval;
- `R` = number of recovery/reconciliation intervals.

These are variables, not measurements of any Plex server.

### Full snapshot

At minimum, section discovery plus paged content costs:

```text
HTTP requests ≈ 1 + Σ_s ceil(N_s / P_s)
```

The `1` is the section list; add one canonical `/library/metadata/{ratingKey}` request per item when the section response does not include every required nested field. The response bytes are proportional to the fields/elements returned and the `Media` → `Part` → `Stream` hierarchy. The official API says to inspect actual pagination metadata because the requested page size may not be honored exactly.

### Event-assisted run

A live stream/webhook receiver has one long-lived connection or inbound delivery per configured endpoint and approximately:

```text
HTTP detail requests ≈ U
```

if each event identifies a key and the receiver hydrates only unique keys. Webhook payloads and some notification payloads may contain enough metadata to avoid a detail read for a narrow automation, but a library synchronizer still needs canonical reads to detect part/stream changes and compare fields. If the stream reports only a section/activity context, `U` can approach `ΣN_s`; at that point the cost is close to a full scan without its completeness guarantees.

### Recovery cost

Because no Plex event source documents replay, every unproven gap has worst-case reconciliation cost:

```text
recovery cost ≈ 1 + Σ_s ceil(N_s / P_s)
```

plus required detail reads. Thus an event path can improve common-case latency/bytes but cannot provide a bounded worst-case delta cost. A durable consumer queue reduces local processing loss; it does not create server-side replay.

### Database snapshot

A copied DB can be read locally without network pagination, so its network-request count is zero after the copy. The costs are disk I/O, schema adaptation, local joins, permissions, and the documented requirement to stop PMS before opening/copying the DB safely. It remains a current-state/internal snapshot and cannot prove what happened between two DB snapshots unless the implementation owns a durable local history.

### Performance-safe optimization

Use the official pagination and response-customization mechanisms, and hydrate only keys that require nested details. A timestamp/media-query filter may reduce `N_s` when a section advertises that field, but it is an optimization and not a deletion/replay contract. No benchmark or byte count is asserted here.

## 9. Recommended role in a reliable synchronizer

The following is a design consequence of the sources, not a claim that Plex guarantees these semantics:

1. **Canonical state:** periodically enumerate `/library/sections` and every target section through the documented paginated API. Store a local state keyed by server machine identifier/library UUID/rating key and include whatever metadata, part, and stream signature the product needs.
2. **Low-latency hints:** optionally register `library.new` webhooks and/or subscribe to the documented EventSource/WebSocket stream. Treat each input as a wakeup (`scan likely started`, `key may need hydration`), not as a committed delta.
3. **Durable consumer queue:** assign a local receive sequence and persist raw webhook/notification frames before processing. This protects against Reaparr crashes after receipt; it does not recover events lost before receipt.
4. **Hydration and comparison:** after the scan/processing activity settles, fetch canonical metadata for affected keys. Compare nested `Media`, `Part`, and `Stream` identifiers/properties rather than assuming a webhook/notification body is complete.
5. **Delete reconciliation:** derive deletions by comparing the current enumerated set with the previous set. Preserve a separate unavailable/Trash state if required by the target behavior.
6. **Recovery rules:** after stream disconnect, receiver outage, PMS downtime, token/server identity change, detected database corruption/restore, unsupported event shape, or an `updatedAt` regression, discard any assumption that the server stream is caught up and run a full reconciliation.
7. **No Plex cursor field:** persist the local state watermark (for example, the last completed snapshot epoch and local received-event sequence), but do not expose it as a Plex server cursor or promise that replay from Plex is possible.

**Final suitability verdict:** Plex webhooks, EventSource/SSE, WebSocket notifications, and push notifications are valuable *triggers*. None is suitable as the sole durable library-level incremental API for Issue 694. Direct DB tracking can be an optional same-host optimization only under a tightly versioned compatibility policy. The correctness boundary remains a paginated current-state reconciliation, with event-triggered targeted reads used to reduce normal-case work.

## 10. Source index

### Plex-owned documentation

- [PMS API reference](https://developer.plex.tv/pms/) — official auth headers, API versioning, pagination, response customization, media queries, and metadata/part paths.
- [PMS API — EventSource operation](https://developer.plex.tv/pms/#tag/Events/operation/eventsourceGetSlash) and [WebSocket operation](https://developer.plex.tv/pms/#tag/Events/operation/websocketGetSlash) — exact documented notification paths, auth scheme, and filters.
- [Webhooks](https://support.plex.tv/articles/115002267687-webhooks/) — Plex Pass, PMS `1.3.4+` travel behavior, event list, multipart payload, official example payload.
- [Push Notifications](https://support.plex.tv/articles/push-notifications/) — PMS `1.16.0+`, Plex Pass, sign-in/opt-in, mobile/Plex-service categories.
- [Plex Media Server URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/) — tokenized read examples, sections/items, `addedAt`/`updatedAt`, `Media`/`Part`/`Stream` response structure, scan/refresh commands.
- [Scanning vs Refreshing a Library](https://support.plex.tv/articles/200289306-scanning-vs-refreshing-a-library/) — scanner sees new/removed/renamed/moved files; refresh semantics and interruptions.
- [Library settings](https://support.plex.tv/articles/200289526-library/) — automatic/periodic scans, empty-trash policy, media deletion behavior.
- [Emptying Library Trash](https://support.plex.tv/articles/200289326-emptying-library-trash/) — unavailable/missing files, restore-before-empty, irreversible purge after emptying.
- [Scheduled Tasks](https://support.plex.tv/articles/201553286-scheduled-tasks/) — three-day core DB backup task, up to three rotating copies, not all metadata.
- [Restore a Database Backed Up via Scheduled Tasks](https://support.plex.tv/articles/202485658-restore-a-database-backed-up-via-scheduled-tasks/) — stop/copy/replace/restart procedure and DB filenames.
- [Repair a Corrupted Database](https://support.plex.tv/articles/repair-a-corrupted-database/) — stop PMS before DB access, Plex SQLite, copy-before-work guidance.
- [Where is the PMS data directory located?](https://support.plex.tv/articles/202915258-where-is-the-plex-media-server-data-directory-located/) — platform paths and the data-directory relationship.
- [Backing Up Plex Media Server Data](https://support.plex.tv/articles/201539237-backing-up-plex-media-server-data/) — broader server-data backup scope and metadata/settings caveats.

### Plex-owned published examples

- [webhooks-notifications README](https://github.com/plexinc/webhooks-notifications/blob/cef376f0f51e99060abae24b4a9833f368126528/README.md) and [index.js](https://github.com/plexinc/webhooks-notifications/blob/cef376f0f51e99060abae24b4a9833f368126528/index.js).
- [webhooks-slack README](https://github.com/plexinc/webhooks-slack/blob/89b9d9bf535de57791358592b8eac4b45d2341e7/README.md) and [index.js](https://github.com/plexinc/webhooks-slack/blob/89b9d9bf535de57791358592b8eac4b45d2341e7/index.js).
- [webhooks-home-automation](https://github.com/plexinc/webhooks-home-automation) — linked by the official Webhooks article.

### Client/source and third-party protocol evidence

- [python-plexapi `AlertListener` (pinned source)](https://github.com/pushingkarmaorg/python-plexapi/blob/09ea3c51043160aa3b7c9cc268ab32d39f4bb970/plexapi/alert.py) — plural legacy WebSocket path, `NotificationContainer`, observed library timeline states.
- [Plexopedia — Listen for Events](https://www.plexopedia.com/plex-media-server/api/server/listen-events/) — third-party SSE framing/filter examples and activity/playing/transcode examples.
- [Plexopedia — Listen for Notifications](https://www.plexopedia.com/plex-media-server/api/server/listen-notifications/) — third-party legacy WebSocket/log notification description.
- [mutanthost/plex_schema `plex3.sql` (pinned 2019 snapshot)](https://github.com/mutanthost/plex_schema/blob/5086c4ff278732d0246837d86e63adf0c4f2676e/plex3.sql) — non-Plex schema snapshot showing internal timestamp/deletion/timeline tables; not a current contract.
