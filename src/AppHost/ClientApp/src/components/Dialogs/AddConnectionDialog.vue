<template>
	<QCardDialog
		min-width="40vw"
		max-width="40vw"
		:type="{} as IConnectionDialog"
		:name="DialogType.AddConnectionDialog"
		:loading="false"
		persistent
		close-button
		@opened="onOpen"
		@closed="onClose">
		<template #title>
			{{
				$t('components.add-connection-dialog.header', {
					serverName: plexServerName,
				})
			}}
		</template>
		<!--	Help text	-->
		<template #default>
			<div>
				<QRow
					wrap
					class="connection-url-row">
					<QCol cols="auto">
						<QConnectionIcon :type="type" />
					</QCol>
					<QCol class="q-mx-md">
						<q-input
							v-model:model-value="url"
							:label="$t('components.add-connection-dialog.input.connection-url')" />
					</QCol>
					<QCol cols="auto">
						<ValidIcon
							:valid="urlValidation.valid"
							:text="urlValidation.text" />
					</QCol>
				</QRow>
				<!-- Property Preview -->
				<QRow
					class="connection-preview q-my-md"
					gutter="md"
					justify="around">
					<QCol cols="3">
						<q-input
							:outlined="false"
							:model-value="connection.protocol"
							readonly
							:label="$t('components.add-connection-dialog.input.protocol')" />
					</QCol>
					<QCol>
						<q-input
							:outlined="false"
							:model-value="connection.address"
							readonly
							:label="$t('components.add-connection-dialog.input.host-name')" />
					</QCol>
					<QCol cols="3">
						<q-input
							:outlined="false"
							:model-value="connection.port > 0 ? connection.port : ''"
							readonly
							:label="$t('components.add-connection-dialog.input.port')" />
					</QCol>
				</QRow>
				<!-- Validation Response -->
				<QRow
					v-if="response"
					justify="around">
					<QCol cols="4">
						<q-input
							:outlined="false"
							:model-value="response.version"
							readonly
							:label="$t('components.server-dialog.tabs.server-data.headers.plex-version')" />
					</QCol>
					<QCol cols="6">
						<q-input
							:outlined="false"
							:model-value="response.machineIdentifier"
							readonly
							:label="$t('components.server-dialog.tabs.server-data.headers.machine-id')" />
					</QCol>
				</QRow>
			</div>
		</template>
		<!--  Actions	-->
		<template #actions="{ close }">
			<QRow
				justify="between"
				gutter="md">
				<QCol
					v-if="plexServerConnectionId > 0"
					cols="auto">
					<DeleteButton
						@click="deleteConnection(close)" />
				</QCol>
				<QSpace v-if="plexServerConnectionId > 0" />
				<QCol cols="auto">
					<ValidationButton
						trigger-once
						:label="$t('components.add-connection-dialog.test-connection-button')"
						:loading="loadingTestConnection"
						:is-validated="isValidTestConnection"
						:disabled="urlValidation.valid !== ValidationLevel.Valid"
						@click="checkConnection" />
				</QCol>
				<QCol cols="auto">
					<BaseButton
						:label="plexServerConnectionId > 0
							? $t('components.add-connection-dialog.update-connection')
							: $t('components.add-connection-dialog.add-connection-button')"
						color="positive"
						:loading="loadingCreateConnection"
						:disabled="urlValidation.valid !== ValidationLevel.Valid"
						@click="plexServerConnectionId > 0
							? updateConnection(close)
							: createConnection(close) " />
				</QCol>
			</QRow>
		</template>
	</QCardDialog>
</template>

<script setup lang="ts">
import { get, set } from '@vueuse/core';
import { DialogType, ValidationLevel } from '@enums';
import type { IConnectionDialog } from '@interfaces';
import type { CreatePlexServerConnectionEndpointRequest, ServerIdentityDTO } from '@dto';
import { PlexConnectionTypes } from '@dto';
import { useServerConnectionStore, useServerStore, useSubscription } from '#imports';

const serverStore = useServerStore();
const connectionStore = useServerConnectionStore();

const url = ref<string>('');
const { t } = useI18n();
const plexServerId = ref<number>(0);
const plexServerConnectionId = ref<number>(0);

const loadingTestConnection = ref(false);
const isValidTestConnection = ref(false);

const loadingCreateConnection = ref(false);

const plexServerName = computed((): string => serverStore.getServerName(get(plexServerId)));

const response = ref<ServerIdentityDTO | null>(null);

const parsedUrl = computed((): URL | null => {
	try {
		return new URL(get(url));
	} catch (e) {
		return e === 'TypeError' ? null : null;
	}
});

const connection = computed((): CreatePlexServerConnectionEndpointRequest => {
	const url = get(parsedUrl);
	if (url === null) {
		return {
			protocol: '',
			address: '',
			port: 0,
			url: '',
			plexServerId: get(plexServerId),
		};
	}

	const protocol = url.protocol.startsWith('https') ? 'https' : 'http';
	let port = url.port ? parseInt(url.port) : 0;
	if (port === 0) {
		port = protocol === 'https' ? 443 : 80;
	}

	return {
		protocol,
		address: url.hostname ?? '',
		port,
		url: url.origin ?? '',
		plexServerId: get(plexServerId),
	};
});

const urlValidation = computed((): { valid: ValidationLevel; text: string } => {
	const url = get(parsedUrl);
	if (url === null) {
		return {
			valid: ValidationLevel.Invalid,
			text: t('components.add-connection-dialog.url-has-invalid-format'),
		};
	}

	const urlExist = connectionStore.IsUrlExisting(get(connection).url);
	if (urlExist.plexServerId > 0) {
		return {
			valid: ValidationLevel.Warning,
			text: t('components.add-connection-dialog.url-already-exists', {
				serverName: serverStore.getServerName(urlExist.plexServerId),
			}),
		};
	}

	return {
		valid: ValidationLevel.Valid,
		text: t('components.add-connection-dialog.url-has-valid-format'),
	};
});

const isLocalIp = computed(() => {
	// Check for IPv4 local ranges
	const ipv4LocalRanges = [
		/^10\.\d{1,3}\.\d{1,3}\.\d{1,3}$/,
		/^172\.(1[6-9]|2\d|3[0-1])\.\d{1,3}\.\d{1,3}$/,
		/^192\.168\.\d{1,3}\.\d{1,3}$/,
		/^127\.\d{1,3}\.\d{1,3}\.\d{1,3}$/,
	];

	// Check if IP matches any of the IPv4 local ranges
	if (ipv4LocalRanges.some((range) => range.test(get(connection).address))) {
		return true;
	}

	// Check for IPv6 local addresses
	const ipv6LocalPrefixes = [
		/^fc[0-9a-f]{2}:/, // Unique local address (ULA) range
		/^::1$/, // Loopback address
	];

	// Check if IP matches any of the IPv6 local prefixes
	return ipv6LocalPrefixes.some((prefix) => prefix.test(get(connection).address));
});

const type = computed(() => {
	if (get(isLocalIp)) {
		return PlexConnectionTypes.Local;
	}

	if (get(parsedUrl)?.href.includes('plex.direct')) {
		return PlexConnectionTypes.PlexRelay;
	}

	return PlexConnectionTypes.Public;
});

function checkConnection() {
	set(loadingTestConnection, true);
	set(response, null);
	useSubscription(connectionStore.checkServerConnectionUrl(get(connection).url).subscribe({
		next: (result) => {
			set(isValidTestConnection, result.isSuccess);
			if (result.isSuccess) {
				set(response, result.value);
			}
		},
		complete: () => {
			set(loadingTestConnection, false);
		},
	}));
}

function createConnection(close: () => void) {
	set(loadingCreateConnection, true);
	set(response, null);
	useSubscription(connectionStore.createServerConnection(get(connection)).subscribe({
		next: (result) => {
			if (result.isSuccess) {
				close();
			}
		},
		error: () => {
			set(loadingCreateConnection, false);
		},
		complete: () => {
			set(loadingCreateConnection, false);
		},
	}));
}

function updateConnection(close: () => void) {
	set(loadingCreateConnection, true);
	set(response, null);
	useSubscription(connectionStore.updateServerConnection({
		...get(connection),
		id: get(plexServerConnectionId),
	}).subscribe({
		next: (result) => {
			if (result.isSuccess) {
				close();
			}
		},
		error: () => {
			set(loadingCreateConnection, false);
		},
		complete: () => {
			set(loadingCreateConnection, false);
		},
	}));
}

function deleteConnection(close: () => void) {
	set(response, null);
	useSubscription(connectionStore.deleteServerConnection(get(plexServerConnectionId)).subscribe({
		next: (result) => {
			if (result.isSuccess) {
				close();
			}
		},
	}));
}

function onOpen(data: IConnectionDialog) {
	set(plexServerId, data.plexServerId);
	set(plexServerConnectionId, data.plexServerConnectionId);
	set(url, connectionStore.getServerConnection(data.plexServerConnectionId)?.url ?? '');
}

function onClose() {
	set(url, '');
	set(plexServerId, 0);
	set(plexServerConnectionId, 0);
	set(loadingTestConnection, false);
	set(isValidTestConnection, false);
	set(loadingCreateConnection, false);
	set(response, null);
}
</script>

<style lang="scss">
@media (max-width: $breakpoint-xs-max) {
  .connection-url-row,
  .connection-preview {
    flex-direction: column;
    margin-inline: 0;

    > .col,
    > [class*='col-'] {
      width: 100%;
      max-width: 100%;
      margin-inline: 0;
    }
  }
}
</style>
