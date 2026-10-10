<template>
	<template v-if="serverStore.getVisibleServers.length > 0">
		<q-expansion-item
			v-for="(server, index) in serverStore.getVisibleServers"
			:key="index"
			:label="serverStore.getServerName(server.id)"
			expand-icon="mdi-chevron-down">
			<!-- Server header	-->
			<template #header>
				<q-item-section
					side
					no-wrap>
					<QStatus :value="serverConnectionStore.isServerConnected(server.id)" />
				</q-item-section>

				<q-item-section>
					<div
						class="server-name"
						:data-cy="`server-drawer-item-${server.id}`">
						<q-icon
							v-if="server.owned"
							name="mdi-home"
							size="24px"
							left />
						<span
							class="server-name-text"
							:class="{ 'inaccessible-item-text': !accountStore.getHasAccountServerAccess(server.id) }">
							{{ serverStore.getServerName(server.id) }}
						</span>
						<q-icon
							v-if="isServerSyncing(server.id)"
							name="mdi-sync"
							size="14px"
							class="server-sync-icon"
							aria-label="server syncing" />
					</div>
				</q-item-section>
				<q-item-section side>
					<q-btn
						icon="mdi-cog"
						flat
						:data-cy="`server-dialog-${index}`"
						@click.stop="dialogStore.openServerSettingsDialog(server.id)" />
				</q-item-section>
			</template>
			<!-- Render libraries -->
			<q-list v-if="filterLibraries(server.id).length > 0">
				<q-item
					v-for="(library, y) in filterLibraries(server.id)"
					:key="y"
					v-ripple
					clickable
					:class="{
						'active-library-item': isActiveLibrary(library.id),
						'inaccessible-item-text': !hasLibraryAccess(server.id, library.id),
					}"
					:data-cy="`server-drawer-library-${library.id}`"
					active-class="text-orange"
					@click="openMediaPage(library)">
					<q-item-section avatar>
						<QMediaTypeIcon
							:active="library.syncedAt != null"
							:loading="libraryStore.getIsLibrarySyncing(library.id)"
							:media-type="library.type" />
					</q-item-section>
					<q-item-section>
						<span
							:class="{
								'active-library-text': isActiveLibrary(library.id),
								'inaccessible-item-text': !hasLibraryAccess(server.id, library.id),
							}">
							{{ libraryStore.getLibraryName(library.id) }}
						</span>
					</q-item-section>
				</q-item>
			</q-list>
			<!-- No libraries available -->
			<template v-else>
				<q-item
					v-if="!accountStore.refreshingServerIds.has(server.id)"
					:data-cy="`server-drawer-item-${server.id}-no-libraries`"
					clickable
					@click="runReSyncAccount(server.id)">
					<q-item-section>{{ t('components.server-drawer.no-libraries') }}</q-item-section>
				</q-item>
				<q-item
					v-else
					:data-cy="`server-drawer-item-${server.id}-refresh-loading`">
					<QRow justify="center">
						<QCol cols="auto">
							<QSpinnerDots
								color="primary"
								size="40px" />
						</QCol>
					</QRow>
				</q-item>
			</template>
		</q-expansion-item>

		<ServerDialog />
	</template>
	<!-- With valid server available -->

	<!-- No servers available -->
	<template v-else>
		<q-item>
			<q-item-section>{{ t('components.server-drawer.no-servers.header') }}</q-item-section>
		</q-item>
		<q-item>
			<q-item-section>{{ t('components.server-drawer.no-servers.description') }}</q-item-section>
		</q-item>
	</template>
</template>

<script setup lang="ts">
import Log from 'consola';
import { type PlexLibraryDTO, PlexMediaType } from '@dto';
import { useSubscription } from '@vueuse/rxjs';
import { tap } from 'rxjs/operators';
import {
	useLibraryStore,
	useServerStore,
	useDialogStore,
	useServerConnectionStore,
	useAccountStore,
} from '@store';
import { orderBy } from 'lodash-es';
import { useI18n } from '#imports';

const { t } = useI18n();
const router = useRouter();
const route = useRoute();
const serverStore = useServerStore();
const libraryStore = useLibraryStore();
const dialogStore = useDialogStore();
const serverConnectionStore = useServerConnectionStore();
const accountStore = useAccountStore();

// Check if a library is currently active based on route
function hasLibraryAccess(serverId: number, libraryId: number): boolean {
	return accountStore.getHasAccountServerAccess(serverId)
		&& accountStore.getHasAccountLibraryAccess(libraryId);
}

function isActiveLibrary(libraryId: number): boolean {
	const currentLibraryId = route.params.libraryId ?? route.params.id;
	return currentLibraryId !== undefined && Number(currentLibraryId) === libraryId;
}

function filterLibraries(plexServerId: number): PlexLibraryDTO[] {
	return orderBy(
		libraryStore.getLibrariesByServerId(plexServerId),
		[(library) => getLibraryTypeSortOrder(library.type), (library) => libraryStore.getLibraryName(library.id).toLocaleLowerCase()],
		['asc', 'asc'],
	);
}

function getLibraryTypeSortOrder(type: PlexMediaType): number {
	switch (type) {
		case PlexMediaType.Movie:
			return 0;
		case PlexMediaType.TvShow:
			return 1;
		default:
			return 2;
	}
}

function isServerSyncing(serverId: number): boolean {
	const libraries = filterLibraries(serverId);
	return libraries.some((library) => libraryStore.getIsLibrarySyncing(library.id));
}

function openMediaPage(library: PlexLibraryDTO): void {
	switch (library.type) {
		case PlexMediaType.Movie:
			router.push(`/movies/${library.id}`);
			break;
		case PlexMediaType.OtherVideos:
			router.push(`/other-videos/${library.id}`);
			break;
		case PlexMediaType.TvShow:
			router.push(`/tvshows/${library.id}`);
			break;
		case PlexMediaType.MusicArtist:
		case PlexMediaType.MusicAlbum:
		case PlexMediaType.MusicTrack:
			router.push(`/music/${library.id}`);
			break;
		case PlexMediaType.PhotoAlbum:
		case PlexMediaType.PhotoImage:
			router.push(`/photos/${library.id}`);
			break;
		default:
			Log.error(library.type + ' was neither a movie, tvshow or music library');
			router.push(`/unknown/${library.id}`);
	}
}

function runReSyncAccount(serverId: number): void {
	if (accountStore.refreshingServerIds.has(serverId)) {
		return;
	}

	useSubscription(
		accountStore.reSyncServer(serverId).pipe(tap((data) => {
			if (data.isSuccess) {
				dialogStore.openRefreshPlexAccountAccessDialog(data.value ?? []);
			}
		})).subscribe(),
	);
}
</script>

<style lang="scss">
.server-name {
  width: 190px;
  display: flex;
  align-items: center;
  gap: 6px;
  line-height: 24px;
  align-content: center;
  text-overflow: ellipsis;
}

.server-name-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.inaccessible-item-text {
  text-decoration: line-through;
  color: var(--q-grey-6);
  opacity: 0.62;
}

.server-sync-icon {
  opacity: 0.72;
  color: #ff8a80;
  filter: drop-shadow(0 0 4px rgba(255, 138, 128, 0.35));
  transform-origin: 50% 50%;
  animation: server-sync-spin 1.9s linear infinite, server-sync-breathe 2.8s ease-in-out infinite;
}

.server-panels {
  z-index: 0;

  &.theme--dark {
    .v-expansion-panel {
      background: rgba(0, 0, 0, 0.3);
    }
  }

  &.theme--light {
    .v-expansion-panel {
      background: rgba(255, 255, 255, 0.3);
    }
  }
}

.ps {
  height: 100%;
  width: 100%;
}

// Active library item styles with glowing effect
.active-library-item {
  position: relative;
  background: linear-gradient(90deg, rgba(211, 47, 47, 0.15), rgba(229, 115, 115, 0.15)) !important;
  border-left: 4px solid #d32f2f !important;
  animation: glow-pulse 2s ease-in-out infinite;
  overflow: hidden;

  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: linear-gradient(90deg,
    transparent,
    rgba(211, 47, 47, 0.2),
    transparent
    );
    animation: shimmer 3s ease-in-out infinite;
    pointer-events: none;
  }
}

@keyframes glow-pulse {
  0%, 100% {
    box-shadow: inset 4px 0 20px rgba(211, 47, 47, 0.4),
    0 0 15px rgba(211, 47, 47, 0.2);
  }
  50% {
    box-shadow: inset 4px 0 30px rgba(229, 115, 115, 0.5),
    0 0 20px rgba(229, 115, 115, 0.3);
  }
}

@keyframes shimmer {
  0% {
    transform: translateX(-100%);
  }
  100% {
    transform: translateX(100%);
  }
}

@keyframes text-glow {
  0%, 100% {
    text-shadow: 0 0 8px rgba(239, 154, 154, 0.6),
    0 0 12px rgba(211, 47, 47, 0.4);
  }
  50% {
    text-shadow: 0 0 12px rgba(239, 154, 154, 0.8),
    0 0 18px rgba(239, 154, 154, 0.6),
    0 0 24px rgba(211, 47, 47, 0.4);
  }
}

@keyframes server-sync-spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(-360deg);
  }
}

@keyframes server-sync-breathe {
  0%,
  100% {
    opacity: 0.62;
    color: #ff8a80;
    filter: drop-shadow(0 0 2px rgba(255, 138, 128, 0.25));
  }
  50% {
    opacity: 0.95;
    color: #ffffff;
    filter: drop-shadow(0 0 6px rgba(255, 255, 255, 0.4));
  }
}

@media (prefers-reduced-motion: reduce) {
  .server-sync-icon {
    animation: none !important;
  }
}
</style>
