<template>
	<QPage>
		<template v-if="downloadStore.getServersWithDownloads.length > 0">
			<QScroll
				class="max-height-page-height-minus-app-bar"
				data-cy="downloads-scroll">
				<!--	The Download Table	-->
				<QRow
					justify="center"
					class="q-mb-lg">
					<QCol cols="12">
						<q-list>
							<DownloadsTable
								v-for="{ plexServer, downloads } in downloadStore.getServersWithDownloads"
								:key="plexServer.id"
								:download-rows="downloads"
								:plex-server="plexServer" />
						</q-list>
					</QCol>
				</QRow>
			</QScroll>
			<DownloadDetailsDialog />
		</template>
		<QRow
			v-else
			justify="center"
			class="q-pa-lg">
			<QAlert>
				{{ $t('pages.downloads.no-downloads') }}
			</QAlert>
		</QRow>
	</QPage>
</template>

<script setup lang="ts">
import { useDownloadStore } from '@store';

const downloadStore = useDownloadStore();
</script>
