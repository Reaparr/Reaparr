<template>
	<QCol class="q-py-sm">
		<BaseButton
			flat
			:icon="getButtonIcon"
			:label="getButtonText"
			@click.stop="changeStatus" />
	</QCol>
</template>

<script setup lang="ts">
import { useSubscription } from '@vueuse/rxjs';
import { useServerStore } from '@store';

const props = defineProps<{
	plexServerId: number;
	isDownloadsPausedByUser: boolean;
}>();

const { t } = useI18n();
const serverStore = useServerStore();

const getButtonIcon = computed(() => (props.isDownloadsPausedByUser ? 'mdi-play' : 'mdi-pause'));
const getButtonText = computed(() =>
	props.isDownloadsPausedByUser ? t('components.server-download-status.start') : t('components.server-download-status.pause'),
);

function changeStatus(): void {
	useSubscription(serverStore.setServerPaused(props.plexServerId, !props.isDownloadsPausedByUser).subscribe());
}
</script>
