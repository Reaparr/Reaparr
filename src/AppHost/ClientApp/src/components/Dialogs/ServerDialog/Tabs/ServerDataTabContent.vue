<template>
	<!--	Server Data Tab Content	-->
	<q-markup-table
		class="server-data-table"
		wrap-cells>
		<tbody v-if="plexServer">
			<!-- Machine Identifier -->
			<tr>
				<td style="width: 30%">
					{{ t('components.server-dialog.tabs.server-data.headers.machine-id') }}
				</td>
				<td>{{ plexServer.machineIdentifier }}</td>
			</tr>
			<!-- Device -->
			<tr>
				<td>{{ t('components.server-dialog.tabs.server-data.headers.device') }}</td>
				<td>{{ plexServer.device }}</td>
			</tr>
			<!-- Platform and platform version -->
			<tr>
				<td>{{ t('components.server-dialog.tabs.server-data.headers.platform') }}</td>
				<td>
					{{
						$t('components.server-dialog.tabs.server-data.values.platform-version', {
							platform: plexServer.platform,
							platformVersion: plexServer.platformVersion,
						})
					}}
				</td>
			</tr>
			<!-- Product and version -->
			<tr>
				<td>{{ t('components.server-dialog.tabs.server-data.headers.plex-version') }}</td>
				<td>
					{{
						$t('components.server-dialog.tabs.server-data.values.product-version', {
							product: plexServer.product,
							productVersion: plexServer.productVersion,
						})
					}}
				</td>
			</tr>
			<!-- Created On -->
			<tr>
				<td>{{ t('components.server-dialog.tabs.server-data.headers.created-on') }}</td>
				<td>
					<QDateTime
						short-date
						:text="plexServer.createdAt" />
				</td>
			</tr>
			<tr>
				<td>{{ t('components.server-dialog.tabs.server-data.headers.last-seen-at') }}</td>
				<td>
					<QDateTime
						short-date
						:text="plexServer.lastSeenAt" />
				</td>
			</tr>
			<tr>
				<td>{{ t('components.server-dialog.tabs.server-data.headers.current-status') }}</td>
				<td>
					<QStatus :value="serverStore.getServerStatus(plexServer.id)" />
				</td>
			</tr>
		</tbody>
		<tbody v-else>
			<tr>
				<td>{{ t('general.error.invalid-server') }}</td>
			</tr>
		</tbody>
	</q-markup-table>
</template>

<script setup lang="ts">
import type { PlexServerDTO } from '@dto';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const serverStore = useServerStore();

withDefaults(
	defineProps<{
		plexServer?: PlexServerDTO | null;
		isVisible?: boolean;
	}>(),
	{
		plexServer: null,
		isVisible: false,
	},
);
</script>

<style lang="scss">
@media (max-width: $breakpoint-xs-max) {
  .server-data-table {
    box-shadow: none;
    background: transparent;

    table,
    tbody,
    tr,
    td {
      display: block;
      width: 100% !important;
    }

    tr {
      padding-block: 0.75rem;
      border-bottom: 1px solid currentcolor;
    }

    td {
      min-height: 0;
      height: auto;
      padding: 0.125rem 0;
      white-space: normal;
      overflow-wrap: anywhere;
    }

    td:first-child {
      font-weight: 600;
    }
  }
}
</style>
