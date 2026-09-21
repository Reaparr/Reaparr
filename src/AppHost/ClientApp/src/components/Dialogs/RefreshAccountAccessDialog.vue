<template>
	<QCardDialog
		full-height
		:name="DialogType.RefreshAccountAccessDialog"
		:cy="DialogType.RefreshAccountAccessDialog"
		:type="[] as RefreshPlexAccountAccessRapportDTO[]"
		@opened="onOpened"
		@closed="onClosed">
		<template #title>
			<QText
				align="center"
				size="h4"
				:value="'Plex Account Access Report'" />
		</template>
		<template #top-row>
			<q-separator />
			<q-tabs
				v-model="tab"
				dense
				class="text-grey"
				active-color="primary"
				indicator-color="primary"
				align="justify"
				narrow-indicator>
				<q-tab
					v-for="plexAccount in plexAccessNodes"
					:key="plexAccount.plexAccountId"
					:data-cy="`refresh-account-access-tab-${plexAccount.plexAccountId}`"
					:name="plexAccount.plexAccountId"
					:label="plexAccount.plexAccountName" />
			</q-tabs>
			<q-separator />
		</template>
		<template #default>
			<q-tab-panels
				v-model="tab"
				animated>
				<q-tab-panel
					v-for="plexAccount in plexAccessNodes"
					:key="plexAccount.plexAccountId"
					:name="plexAccount.plexAccountId">
					<q-tree
						v-model:expanded="expanded"
						:nodes="plexAccount.servers"
						:node-key="'id' as keyof IAccessNode"
						dense
						:data-cy="`refresh-account-access-tree-${plexAccount.plexAccountId}`"
						default-expand-all>
						<template #default-header="{ node }: { node: IAccessNode }">
							<QRow
								class="access-dialog-node-row q-mr-lg"
								:cy="`access-row-${node.id}`"
								align="center"
								justify="between">
								<QCol cols="auto">
									<QIconTooltip
										:value="node.state"
										:options="options" />
									<!-- Server Header Prepend -->
									<template v-if="node.isServer">
										<!--	Row Icon -->
										<q-icon
											class="q-mx-sm"
											name="mdi-server"
											size="28px" />

										<QStatus
											class="q-mr-sm"
											:value="!node.isServerOffline" />
									</template>
									<!-- Library Header  Prepend -->
									<template v-else-if="node.isLibrary">
										<QMediaTypeIcon
											class="q-mx-sm"
											:media-type="node.libraryType" />
									</template>
								</QCol>
								<!-- Row Title -->
								<QCol class="access-dialog-title-col">
									<QText
										:bold="node.isServer? 'bold' : 'regular'"
										class="access-dialog-title q-ml-sm q-mt-auto"
										:value="node.name"
										size="body2"
										:cy="`access-dialog-title-${node.id}`" />
								</QCol>
							</QRow>
						</template>
						<template #default-body="{ node }: { node: IAccessNode }">
							<QRow
								v-if="node.isServer && node.isServerOffline"
								:full-width="false"
								class="q-ml-lg">
								<QCol>
									<QText
										:cy="`access-dialog-server-offline-text-${node.plexServerId}`"
										:value="$t('components.refresh-account-access-dialog.offline-server-message')" />
								</QCol>
							</QRow>
						</template>
					</q-tree>
				</q-tab-panel>
			</q-tab-panels>
		</template>
		<!-- Actions -->
		<template #actions="{ close }">
			<QRow justify="end">
				<QCol cols="auto">
					<HideButton
						cy="refresh-account-access-dialog-hide-btn"
						@click="close" />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { PlexAccessState } from '@dto';
import type {	PlexLibraryAccessRapportDTO,
	PlexServerAccessRapportDTO,
	RefreshPlexAccountAccessRapportDTO,	PlexMediaType } from '@dto';
import { DialogType } from '@enums';
import type { QIconTooltipData } from '@interfaces';
import { sortBy } from 'lodash-es';
import { useLibraryStore } from '@store';

const { t } = useI18n();
const libraryStore = useLibraryStore();
const refreshRapports = ref<RefreshPlexAccountAccessRapportDTO[]>([]);

const expanded = ref<number[]>([]);
const tab = ref(0);

function sortState(x) {
	switch (x.state) {
		case PlexAccessState.Granted:
			return 0;
		case PlexAccessState.Revoked:
			return 1;
		case PlexAccessState.Updated:
			return 2;
		case PlexAccessState.Unknown:
			return 3;
		default:
			return 10;
	}
}

const plexAccessNodes = computed((): IPlexAccountAccessRapportNode[] => {
	return get(refreshRapports).map((rapport) => {
		const servers = rapport.access.map((x): IAccessNode => ({
			id: `server-${x.plexServerId}`,
			plexServerId: x.plexServerId,
			state: x.state,
			isServerOffline: x.isServerOffline,
			name: x.plexServerName,
			isServer: true,
			isLibrary: false,
			children: sortBy(x.libraryAccess.map((y): IAccessNode => {
				const library = libraryStore.getLibrary(y.plexLibraryId);
				if (!library) {
					throw new Error(`Could not find library with id ${y.plexLibraryId}`);
				}
				return {
					id: `server-${x.plexServerId}-lib-${y.plexLibraryId}`,
					libraryType: library.type,
					name: y.plexLibraryName,
					plexServerId: y.plexServerId,
					state: y.state,
					isServer: false,
					isLibrary: true,
					isServerOffline: false,
				};
			}),
			// Sort first by state and then by title
			(x) => sortState(x), (x) => x.name.toLowerCase()),
		}));

		return {
			plexAccountId: rapport.plexAccountId,
			plexAccountName: rapport.plexAccountName,
			servers: sortBy(servers, (x) => !x.isServerOffline, (x) => sortState(x), (x) => x.name.toLowerCase()),
		};
	});
});

const options = computed((): QIconTooltipData[] =>
	[{
		value: PlexAccessState.Granted,
		icon: 'mdi-timeline-plus-outline',
		tooltip: t('components.refresh-account-access-dialog.access.granted'),
		color: 'positive',
	},
	{
		value: PlexAccessState.Revoked,
		icon: 'mdi-timeline-alert-outline',
		tooltip: t('components.refresh-account-access-dialog.access.revoked'),
		color: 'negative',
	},
	{
		value: PlexAccessState.Updated,
		icon: 'mdi-timeline-check-outline',
		tooltip: t('components.refresh-account-access-dialog.access.updated'),
		color: 'grey',
	},
	{
		value: PlexAccessState.Unknown,
		icon: 'mdi-timeline-question-outline',
		tooltip: t('components.refresh-account-access-dialog.access.unknown'),
		color: 'warning',
	},
	]);

function onOpened(data: RefreshPlexAccountAccessRapportDTO[]) {
	if (data && data.length > 0) {
		set(refreshRapports, data);
		set(tab, data[0]!.plexAccountId);
	}
}

function onClosed() {
	set(refreshRapports, []);
}

interface IAccessNode extends Omit<PlexLibraryAccessRapportDTO, 'plexLibraryName' | 'plexLibraryId'>, Omit<PlexServerAccessRapportDTO, 'libraryAccess' | 'plexServerName'> {
	id: string;
	name: string;
	isServer: boolean;
	isLibrary: boolean;
	children?: IAccessNode[];
	libraryType?: PlexMediaType;
}

interface IPlexAccountAccessRapportNode {
	plexAccountId: number;
	plexAccountName: string;
	servers: IAccessNode[];
}
</script>

<style lang="scss">
@media (max-width: $breakpoint-sm-max) {
  .access-dialog-title-col {
    min-width: 0;
  }

  .access-dialog-title {
    min-width: 0;
    overflow-wrap: anywhere;
    word-break: break-word;
  }
}
</style>
