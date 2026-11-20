<template>
	<QCardDialog
		:loading="false"
		:name="DialogType.HelpInfoDialog"
		:type="{} as IHelp"
		width="500px"
		@closed="onClose"
		@opened="onOpen">
		<template #title>
			{{ helpTitle ? helpTitle : missingHelpTitle }}
		</template>
		<!--	Help text	-->
		<template #default>
			<div class="i18n-formatting">
				<VueMarkdown
					:plugins="[markdownItTargetBlank]"
					:source="helpText ? helpText : missingHelpText" />
			</div>
		</template>
		<!--	Close action	-->
		<template #actions="{ close }">
			<q-space />
			<q-btn
				:label="t('general.commands.close')"
				flat
				@click="() => close()" />
		</template>
	</QCardDialog>
</template>

<script lang="ts" setup>
import { get, set } from '@vueuse/core';
import type { IHelp } from '@interfaces';
import { DialogType } from '@enums';
import { useI18n } from 'vue-i18n';
import VueMarkdown from 'vue-markdown-render';
import type MarkdownIt from 'markdown-it';
import type { Token } from 'markdown-it';

const { t } = useI18n();
const helpTitle = ref('');
const helpText = ref('');

const missingHelpTitle = ref(t('help.default.title'));
const missingHelpText = ref(t('help.default.text'));

function onOpen(event: IHelp): void {
	set(helpTitle, event.title ? event.title : get(missingHelpTitle));
	set(helpText, event.text ? event.text : get(missingHelpText));
}

function onClose() {
	set(helpTitle, '');
	set(helpText, '');
}

// Markdown-it plugin to add target="_blank" and rel="noopener noreferrer" to all links
function markdownItTargetBlank(md: MarkdownIt): void {
	const d = md.renderer.rules.link_open;
	if (!d) {
		return;
	}
	md.renderer.rules.link_open = (tokens: Token[], index: number, options, env, renderer) => {
		const token = tokens[index]!;
		const targetIndex = token.attrIndex('target');
		if (targetIndex < 0) token.attrPush(['target', '_blank']);
		else token.attrs![targetIndex]![1] = '_blank';
		token.attrPush(['rel', 'noopener noreferrer']);
		return d(tokens, index, options, env, renderer);
	};
}
</script>
