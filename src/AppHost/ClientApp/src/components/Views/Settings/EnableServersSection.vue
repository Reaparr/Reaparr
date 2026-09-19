<template>
	<QSection :header="$t('pages.settings.ui.enable-servers-section.header')">
		<q-list
			v-if="serverStore.getDisabledServers.length"
			bordered>
			<q-item
				v-for="server in serverStore.getDisabledServers"
				:key="server.id"
				v-ripple
				clickable>
				<q-item-section avatar>
					<IconSquareButton
						icon="mdi-eye-check-outline"
						:tooltip-text="$t('general.commands.enabled')"
						@click="onServerEnable(server.id)" />
				</q-item-section>

				<q-item-section>
					<q-item-label>{{ serverStore.getServerName(server.id) }}</q-item-label>
				</q-item-section>
			</q-item>
		</q-list>
		<QAlert
			v-else
			type="info">
			{{ t('pages.settings.ui.enable-servers-section.no-servers-disabled') }}
		</QAlert>
	</QSection>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';

const serverStore = useServerStore();
const { t } = useI18n();

function onServerEnable(plexServerId: number): void {
	useSubscription(
		serverStore
			.setServerEnabled(plexServerId, true)
			.subscribe(),
	);
}
</script>
