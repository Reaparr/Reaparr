import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia, type Pinia } from 'pinia';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import dayjs from 'dayjs';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generatePlexLibrariesFromPlexServers, generatePlexServers, generateResultDTO, Seed } from '@mock';
import { PlexLibraryPaths, PlexServerPaths } from '@api-urls';
import { PlexAccessState, type PlexLibraryAccessTimelineDTO } from '@dto';
import { useLibraryAccessTimelineStore, useLibraryStore, useServerStore } from '@store';
import LibraryAccessTimelineOverview from '@/components/Overviews/LibraryAccessTimelineOverview.vue';
import messages from '@/lang/en-US.json';

describe('LibraryAccessTimelineOverview local filters', () => {
	let { mock } = baseVars();
	let pinia: Pinia;
	const mounted: Array<{ unmount: () => void }> = [];
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		pinia = createPinia();
		setActivePinia(pinia);
	});
	afterEach(() => {
		mounted.forEach((wrapper) => wrapper.unmount());
		mounted.length = 0;
		vi.useRealTimers();
	});

	async function renderOverview() {
		const wrapper = await mountSuspended(LibraryAccessTimelineOverview, { global: {
			plugins: [pinia, createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
			stubs: { LibraryAccessGanttChart: true },
		} });
		mounted.push(wrapper);
		return wrapper;
	}

	test('Should filter exact intervals locally and fetch fresh history only when mounted again', async () => {
		// Arrange
		const config = { seed: 113, plexServerCount: 2, plexMovieLibraryCount: 2, plexTvShowLibraryCount: 0 };
		const servers = generatePlexServers({ config });
		const libraries = generatePlexLibrariesFromPlexServers({ seed: new Seed(config.seed), plexServers: servers, config });
		const serverA = servers[0]!;
		const serverB = servers[1]!;
		const [recentLibrary, olderLibrary] = libraries.filter((library) => library.plexServerId === serverA.id);
		const otherLibrary = libraries.find((library) => library.plexServerId === serverB.id)!;
		if (!recentLibrary || !olderLibrary) throw new Error('Expected two libraries on the first server');
		const now = dayjs();
		const histories = [
			{ accountId: 101, library: recentLibrary, grantedAt: now.subtract(5, 'day').toISOString(), revokedAt: now.subtract(3, 'day').toISOString() },
			{ accountId: 102, library: olderLibrary, grantedAt: now.subtract(20, 'day').toISOString(), revokedAt: now.subtract(10, 'day').toISOString() },
			{ accountId: 103, library: otherLibrary, grantedAt: now.subtract(4, 'day').toISOString(), revokedAt: now.subtract(2, 'day').toISOString() },
		];
		let timeline: PlexLibraryAccessTimelineDTO = {
			currentState: [],
			events: histories.flatMap((history, index) => [PlexAccessState.Granted, PlexAccessState.Revoked].map((state, transition) => ({
				id: index * 2 + transition + 1,
				plexAccountId: history.accountId,
				plexAccountName: `Account ${history.accountId}`,
				plexServerId: history.library.plexServerId,
				plexServerName: servers.find((server) => server.id === history.library.plexServerId)!.name,
				plexLibraryId: history.library.id,
				plexLibraryName: history.library.title,
				createdAt: transition === 0 ? history.grantedAt : history.revokedAt,
				refreshRunId: `${history.accountId}-${transition}`,
				state,
			}))),
		};
		const expected = histories.map((history) => expect.objectContaining({
			rowId: `account-${history.accountId}-server-${history.library.plexServerId}-library-${history.library.id}`,
			grantedAt: history.grantedAt,
			revokedAt: history.revokedAt,
			start: new Date(history.grantedAt),
			end: new Date(history.revokedAt),
		}));
		const timelinePath = PlexLibraryPaths.getPlexLibraryAccessTimelineEndpoint();
		mock.onGet(timelinePath).reply(() => [200, generateResultDTO(timeline)]);
		mock.onGet(PlexServerPaths.getAllPlexServersEndpoint()).reply(200, generateResultDTO(servers));
		mock.onGet(PlexLibraryPaths.getAllPlexLibrariesEndpoint()).reply(200, generateResultDTO(libraries));
		await subscribeSpyTo(useServerStore().setup()).onComplete();
		await subscribeSpyTo(useLibraryStore().setup()).onComplete();
		await subscribeSpyTo(useLibraryAccessTimelineStore().setup()).onComplete();
		const wrapper = await renderOverview();
		await vi.waitFor(() => expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[0], expected[2]]));
		const [serverFilter, libraryFilter] = wrapper.findAllComponents({ name: 'QSelect' });
		const zoomFilter = wrapper.findComponent({ name: 'QBtnToggle' });
		vi.useFakeTimers();
		vi.setSystemTime(now.toDate());

		// Act and assert: each public control updates the real store's interval selection.
		serverFilter!.vm.$emit('update:modelValue', serverA.id);
		await vi.advanceTimersByTimeAsync(250);
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[0]]);
		zoomFilter.vm.$emit('update:modelValue', 'All');
		await vi.advanceTimersByTimeAsync(250);
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[1], expected[0]]);
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('zoomPreset')).toBe('All');
		libraryFilter!.vm.$emit('update:modelValue', olderLibrary.id);
		await vi.advanceTimersByTimeAsync(250);
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[1]]);
		zoomFilter.vm.$emit('update:modelValue', '7d');
		await vi.advanceTimersByTimeAsync(250);
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).exists()).toBe(false);
		serverFilter!.vm.$emit('update:modelValue', serverB.id);
		await vi.advanceTimersByTimeAsync(250);
		expect(libraryFilter!.props('modelValue')).toBeNull();
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[2]]);
		serverFilter!.vm.$emit('update:modelValue', null);
		await vi.advanceTimersByTimeAsync(250);
		expect(wrapper.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[0], expected[2]]);
		expect(mock.history.get.filter((request) => request.url === timelinePath)).toHaveLength(1);

		// A remount is the existing refresh boundary, and must render the new snapshot.
		wrapper.unmount();
		mounted.length = 0;
		vi.useRealTimers();
		timeline = { currentState: [], events: timeline.events.filter((event) => event.plexAccountId === 103) };
		const remounted = await renderOverview();
		await vi.waitFor(() => expect(remounted.findComponent({ name: 'LibraryAccessGanttChart' }).props('intervals')).toEqual([expected[2]]));
		expect(mock.history.get.filter((request) => request.url === timelinePath)).toHaveLength(2);
	});
});
