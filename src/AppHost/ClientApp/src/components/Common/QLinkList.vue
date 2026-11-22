<template>
	<q-list
		bordered
		separator
		:data-cy="cy">
		<q-item
			v-for="(item, index) in list"
			:key="index"
			:href="item.link"
			target="_blank"
			@click="onClick(item.link)">
			<q-item-section>
				<q-item-label>
					<QText
						:cy="`link-list-item-${index}-text`"
						:value="item.text" />
				</q-item-label>
			</q-item-section>
			<q-item-section
				v-if="item.link"
				avatar>
				<ExternalLinkButton
					:href="item.link"
					:cy="`link-list-item-${index}-link`" />
			</q-item-section>
		</q-item>
	</q-list>
</template>

<script setup lang="ts">
import Log from 'consola';
import { sendDesktopMessage } from '@composables';
import { DesktopMessageType } from '@dto';
import { useGlobalStore } from '@store';

const globalStore = useGlobalStore();

withDefaults(defineProps<{
	list?: { text: string; link?: string }[];
	cy?: string;
}>(), {
	list: () => [],
	cy: 'link-list',
});

function onClick(href?: string): void {
	if (globalStore.isDesktopMode) {
		Log.debug('Desktop ExternalLink Click', href);
		sendDesktopMessage({
			type: DesktopMessageType.ExternalLink,
			value: href ?? 'unknown-link',
		});
	}
}
</script>
