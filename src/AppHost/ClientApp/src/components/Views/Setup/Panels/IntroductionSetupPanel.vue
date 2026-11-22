<template>
	<q-tab-panel
		:data-cy="`setup-panel-${name}`"
		:name="name">
		<QSection
			:header="$t('pages.setup.intro.title')"
			align="center">
			<p>{{ $t('pages.setup.intro.text.p-1') }}</p>
			<QLinkList :list="list" />
		</QSection>
	</q-tab-panel>
</template>

<script lang="ts" setup>
import { discordInviteLink } from '@composables';
import type { SetupPanelType } from '@enums';

defineProps<{
	name: SetupPanelType;
}>();

const { t } = useI18n();

const list = computed((): { text: string; link?: string }[] => {
	return [
		{
			text: t('pages.setup.intro.list.item-1'),
			link: discordInviteLink(),
		},
		{
			text: t('pages.setup.intro.list.item-2'),
			link: 'https://www.reaparr.rocks/docs/faq',
		},
		{
			text: t('pages.setup.intro.list.item-3'),
			link: 'https://github.com/Reaparr/Reaparr/issues',
		},
	];
});
</script>
