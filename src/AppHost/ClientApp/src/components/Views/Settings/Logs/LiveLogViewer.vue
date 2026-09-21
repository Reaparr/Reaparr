<template>
	<div
		class="live-log-viewer q-pa-md"
		data-cy="live-log-viewer">
		<QToolbar class="live-log-viewer__toolbar q-pa-none">
			<QToolbarTitle>
				{{ t('pages.settings.logs.title') }}

				<!-- Help Icon -->
				<HelpButton
					icon="mdi-help-circle-outline"
					class="q-ma-sm"
					@click="helpStore.openHelpDialog({
						label: t('help.settings.logs.title.label'),
						title: t('help.settings.logs.title.title'),
						text: t('help.settings.logs.title.text'),
					})" />
			</QToolbarTitle>
			<q-checkbox
				class="live-log-viewer__pause"
				:model-value="pauseScroll"
				dense
				size="sm"
				:label="t('pages.settings.logs.pause-scroll')"
				@update:model-value="onPauseScrollChanged" />

			<!-- Sort Logs -->
			<IconButton
				class="live-log-viewer__toolbar-action"
				:icon="logsStore.sortDirection === SortDirection.Asc ? 'mdi-sort-ascending' : 'mdi-sort-descending'"
				:title="logsStore.sortDirection === SortDirection.Asc ? t('pages.settings.logs.sort.oldest-first') : t('pages.settings.logs.sort.newest-first')"
				@click="onSortDirectionToggled" />

			<!-- Clear Logs -->
			<IconButton
				class="live-log-viewer__toolbar-action"
				icon="mdi-delete-sweep"
				:title="t('pages.settings.logs.clear')"
				@click="logsStore.clearLogs()" />

			<!-- Refresh Logs -->
			<IconButton
				class="live-log-viewer__toolbar-action"
				icon="mdi-refresh"
				:title="t('pages.settings.logs.refresh')"
				@click="useSubscription(logsStore.refreshLogs().subscribe())" />
		</QToolbar>

		<!-- Search Bar -->
		<div class="row q-col-gutter-md q-mb-md">
			<div class="col-12 col-md-5">
				<q-input
					:model-value="searchInput"
					dense
					clearable
					outlined
					:label="t('pages.settings.logs.search')"
					@update:model-value="onSearchInput"
					@clear="onSearchClear">
					<template #prepend>
						<q-icon name="mdi-magnify" />
					</template>
				</q-input>
			</div>
			<div class="col-12 col-md-4">
				<!-- Log Level Filter -->
				<q-select
					:model-value="logsStore.selectedLevels"
					:options="levelOptions"
					option-label="label"
					option-value="value"
					multiple
					emit-value
					map-options
					dense
					outlined
					:label="t('pages.settings.logs.level-filter')"
					@update:model-value="onSelectedLevelsChanged">
					<template #option="{ itemProps, opt, selected, toggleOption }">
						<q-item
							v-bind="itemProps"
							clickable
							@click="toggleOption(opt)">
							<q-item-section side>
								<q-checkbox
									:model-value="selected"
									dense
									@update:model-value="toggleOption(opt)" />
							</q-item-section>
							<q-item-section>
								<q-item-label>{{ opt.label }}</q-item-label>
							</q-item-section>
							<q-item-section side>
								<q-badge
									:color="levelColor(opt.value)"
									text-color="black">
									{{ logsStore.levelCount(opt.value) }}
								</q-badge>
							</q-item-section>
						</q-item>
					</template>
				</q-select>
			</div>
		</div>

		<div class="row items-center q-mb-sm text-caption text-grey-6">
			<div>{{ t('pages.settings.logs.visible-count', { count: logsStore.getLogs.length, total: logsStore.logs.length }) }}</div>
		</div>

		<div
			ref="scrollElement"
			class="live-log-viewer__scroll">
			<template v-if="logsStore.getLogs.length === 0">
				<QAlert type="info">
					{{ t('pages.settings.logs.empty') }}
				</QAlert>
			</template>
			<template v-else>
				<QVirtualScroll
					:items="logsStore.getLogs"
					separator>
					<template #default="{ item }: {item: LiveLogEventDTO }">
						<div
							class="live-log-viewer__row"
							:class="logsStore.levelClass(item.level)"
							@click="copyLogEntry(item)">
							<div class="live-log-viewer__row-header row items-center q-col-gutter-sm">
								<!-- Selection Checkbox -->
								<div class="col-auto">
									<q-checkbox
										class="live-log-viewer__entry-checkbox"
										:aria-label="`${t('general.commands.selection')} ${item.sequence}`"
										:model-value="selectedEntries.has(item.sequence)"
										dense
										size="sm"
										@update:model-value="onEntrySelectionChanged(item.sequence, $event)"
										@click.stop />
								</div>
								<!-- Level Badge -->
								<div class="col-auto">
									<QBadge
										:color="levelColor(item.level)"
										text-color="black">
										{{ levelLabel(item.level) }}
									</QBadge>
								</div>
								<!-- TimeStamp -->
								<div class="col-auto text-grey-5 live-log-viewer__timestamp">
									<QDateTime
										short-date
										time
										:text="item.timestamp" />
								</div>
								<div
									v-if="item.sourceContext"
									class="col text-grey-6 ellipsis">
									{{ item.sourceContext }}
								</div>
								<div class="col-auto">
									<QBtn
										class="live-log-viewer__copy-action"
										:aria-label="t('pages.settings.logs.copy')"
										flat
										round
										dense
										size="xs"
										icon="mdi-content-copy"
										color="grey-6"
										:title="t('pages.settings.logs.copy')"
										@click.stop="copyLogEntry(item)" />
								</div>
							</div>
							<pre class="live-log-viewer__message"><template
								v-for="(segment, index) in highlightSegments(item.message)"
								:key="`message-${item.sequence}-${index}`"><mark
									v-if="segment.isMatch"
							class="live-log-viewer__highlight">{{ segment.text }}</mark><template v-else>{{ segment.text }}</template></template></pre>
							<pre
								v-if="item.exception"
								class="live-log-viewer__exception"><template
									v-for="(segment, index) in highlightSegments(item.exception)"
									:key="`exception-${item.sequence}-${index}`"><mark
										v-if="segment.isMatch"
							class="live-log-viewer__highlight">{{ segment.text }}</mark><template v-else>{{ segment.text }}</template></template></pre>
						</div>
					</template>
				</QVirtualScroll>
			</template>
		</div>

		<!-- Bottom action bar -->
		<div class="live-log-viewer__bottom-bar row items-center justify-end q-mt-sm">
			<BaseButton
				v-if="selectedEntries.size > 0"
				icon="mdi-content-copy"
				:label="t('pages.settings.logs.copy-selected', { count: selectedEntries.size })"
				class="q-mr-sm"
				@click="copySelectedEntries" />
			<BaseButton
				v-else
				icon="mdi-content-copy"
				:label="t('pages.settings.logs.copy-all')"
				@click="copyAllEntries" />
		</div>
	</div>
</template>

<script setup lang="ts">
import { get, set, useClipboard, useDebounceFn } from '@vueuse/core';
import { format } from 'date-fns';
import { useLogsStore } from '@store';
import type { LiveLogEventDTO } from '@dto';
import { LogSeverity } from '@dto';
import { SortDirection } from '@enums';

const logsStore = useLogsStore();
const helpStore = useHelpStore();
const { copy } = useClipboard({ legacy: true });

const { t } = useI18n();

const scrollElement = ref<HTMLElement | null>(null);
const pauseScroll = ref(false);
const selectedEntries = ref<Set<number>>(new Set());
const searchInput = ref(logsStore.searchText);

const applySearch = useDebounceFn((value: string) => {
	logsStore.setSearch(value);
}, 300);

function onSearchInput(value: string | number | null): void {
	const text = value?.toString() ?? '';
	set(searchInput, text);
	applySearch(text);
}

function onSearchClear(): void {
	set(searchInput, '');
	logsStore.clearSearch();
}

const levelOptions = computed(() => [
	{ label: t('general.logs.level.verbose'), value: LogSeverity.Verbose },
	{ label: t('general.logs.level.debug'), value: LogSeverity.Debug },
	{ label: t('general.logs.level.information'), value: LogSeverity.Information },
	{ label: t('general.logs.level.warning'), value: LogSeverity.Warning },
	{ label: t('general.logs.level.error'), value: LogSeverity.Error },
	{ label: t('general.logs.level.fatal'), value: LogSeverity.Fatal },
] as { label: string; value: LogSeverity }[]);

function levelLabel(level: LogSeverity): string {
	switch (level) {
		case LogSeverity.Verbose:
			return t('general.logs.level.verbose');
		case LogSeverity.Debug:
			return t('general.logs.level.debug');
		case LogSeverity.Information:
			return t('general.logs.level.information');
		case LogSeverity.Success:
			return t('general.logs.level.success');
		case LogSeverity.Warning:
			return t('general.logs.level.warning');
		case LogSeverity.Error:
			return t('general.logs.level.error');
		case LogSeverity.Fatal:
			return t('general.logs.level.fatal');
		default:
			return t('general.commands.unknown');
	}
}

function levelColor(level: LogSeverity): string {
	switch (level) {
		case LogSeverity.Debug:
			return 'grey-6';
		case LogSeverity.Information:
			return 'blue-4';
		case LogSeverity.Warning:
			return 'orange-6';
		case LogSeverity.Error:
			return 'red-5';
		case LogSeverity.Fatal:
			return 'red-8';
		default:
			return 'black-1';
	}
}

type HighlightSegment = {
	text: string;
	isMatch: boolean;
};

function highlightSegments(text: string): HighlightSegment[] {
	const query = logsStore.searchText.trim();
	if (!query) {
		return [{ text, isMatch: false }];
	}

	const escapedQuery = query.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
	const regex = new RegExp(escapedQuery, 'gi');
	const segments: HighlightSegment[] = [];
	let currentIndex = 0;
	let match = regex.exec(text);

	while (match) {
		const matchIndex = match.index;
		if (matchIndex > currentIndex) {
			segments.push({ text: text.slice(currentIndex, matchIndex), isMatch: false });
		}

		segments.push({ text: match[0], isMatch: true });
		currentIndex = matchIndex + match[0].length;
		match = regex.exec(text);
	}

	if (currentIndex < text.length) {
		segments.push({ text: text.slice(currentIndex), isMatch: false });
	}

	return segments.length > 0 ? segments : [{ text, isMatch: false }];
}

function formatTimestamp(timestamp: string): string {
	return format(new Date(timestamp), 'HH:mm:ss.SSS');
}

function formatLogEntry(item: LiveLogEventDTO): string {
	const parts = [
		`[${formatTimestamp(item.timestamp)}] [${levelLabel(item.level)}]`,
		item.sourceContext ? `[${item.sourceContext}]` : null,
		item.message,
		item.exception ? `\n${item.exception}` : null,
	].filter(Boolean);
	return parts.join(' ');
}

function copyLogEntry(item: LiveLogEventDTO): void {
	copy(formatLogEntry(item));
	showSuccessNotification(t('pages.settings.logs.copied-to-clipboard'), 2000);
}

function onEntrySelectionChanged(sequence: number, selected: boolean): void {
	const entries = get(selectedEntries);
	const updated = new Set(entries);
	if (selected) {
		updated.add(sequence);
	} else {
		updated.delete(sequence);
	}
	set(selectedEntries, updated);
}

function copySelectedEntries(): void {
	const sequences = get(selectedEntries);
	const entries = logsStore.getLogs.filter((x) => sequences.has(x.sequence));
	const text = entries.map(formatLogEntry).join('\n');
	copy(text);
	showSuccessNotification(t('pages.settings.logs.copied-to-clipboard'), 2000);
}

function copyAllEntries(): void {
	const text = logsStore.getLogs.map(formatLogEntry).join('\n');
	copy(text);
	showSuccessNotification(t('pages.settings.logs.copied-to-clipboard'), 2000);
}

function onSelectedLevelsChanged(value: LogSeverity[] | null): void {
	logsStore.selectedLevels = value ?? [];
}

function onSortDirectionToggled(): void {
	logsStore.toggleSortDirection();
	if (logsStore.sortDirection === SortDirection.Asc) {
		void scrollToBottom();
	} else {
		void scrollToTop();
	}
}

function onPauseScrollChanged(value: boolean): void {
	set(pauseScroll, value);

	if (!value) {
		if (logsStore.sortDirection === SortDirection.Asc) {
			void scrollToBottom();
		} else {
			void scrollToTop();
		}
	}
}

async function scrollToBottom(): Promise<void> {
	if (logsStore.sortDirection !== SortDirection.Asc || get(pauseScroll)) {
		return;
	}

	await nextTick();
	const element = get(scrollElement);
	if (!element) {
		return;
	}

	element.scrollTop = element.scrollHeight;
}

async function scrollToTop(): Promise<void> {
	if (logsStore.sortDirection !== SortDirection.Desc || get(pauseScroll)) {
		return;
	}

	await nextTick();
	const element = get(scrollElement);
	if (!element) {
		return;
	}

	element.scrollTop = 0;
}

watch(
	() => logsStore.getLogs.length,
	async () => {
		if (logsStore.sortDirection === SortDirection.Asc) {
			await scrollToBottom();
		} else {
			await scrollToTop();
		}
	},
);

onMounted(() => {
	useSubscription(logsStore.refreshLogs().subscribe());

	void scrollToBottom();
});

onUnmounted(() => logsStore.$reset());
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.live-log-viewer {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);

  &__scroll {
    flex: 1;
    overflow: auto;
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: 8px;
    background: rgba(0, 0, 0, 0.25);
  }

  &__spacer {
    position: relative;
    width: 100%;
  }

  &__row {
    position: relative;
    padding: 0.75rem 1rem;
    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    font-family: monospace;
    cursor: pointer;

    &:hover {
      background: rgba(255, 255, 255, 0.03);
    }

    &--debug {
      border-left: 4px solid #9e9e9e;
    }

    &--information {
      border-left: 4px solid #42a5f5;
    }

    &--warning {
      border-left: 4px solid #fdd835;
    }

    &--error,
    &--fatal {
      border-left: 4px solid #ef5350;
    }
  }

  &__timestamp {
    min-width: 86px;
  }

  &__message,
  &__exception {
    margin: 0.5rem 0 0;
    white-space: pre-wrap;
    word-break: break-word;
    font-family: inherit;
  }

  &__exception {
    color: #ef9a9a;
  }

  &__highlight {
    background-color: #fdd835;
    color: #000;
    border-radius: 2px;
    padding: 0 1px;
  }

  &__bottom-bar {
    padding-top: 8px;
  }
}
@media (max-width: $breakpoint-sm-max) {
  .live-log-viewer {
    height: auto;
    min-height: 100%;

    &__toolbar {
      flex-wrap: wrap;
      gap: 0.25rem;

      .q-toolbar__title {
        flex: 1 1 100%;
        min-width: 0;
        padding: 0;
      }
    }

    &__pause,
    &__toolbar-action,
    &__entry-checkbox,
    &__copy-action {
      min-width: 44px;
      min-height: 44px;
    }

    &__copy-action {
      width: 44px;
      height: 44px;
    }
  }
}
</style>
