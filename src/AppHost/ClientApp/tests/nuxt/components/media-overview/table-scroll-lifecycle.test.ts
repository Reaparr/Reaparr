import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, getActivePinia, setActivePinia } from 'pinia';
import { defineComponent, shallowRef } from 'vue';
import { flushPromises } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { BehaviorSubject } from 'rxjs';
import { baseSetup, getAxiosMock } from '@services-test-base';
import { useMediaOverviewStore } from '@store';
import MediaTable from '@/components/MediaOverview/MediaTable/MediaTable.vue';

const { scrollToIndex } = vi.hoisted(() => ({ scrollToIndex: vi.fn() }));
vi.mock('@tanstack/vue-virtual', () => ({
	useVirtualizer: () => shallowRef({ scrollToIndex, getTotalSize: () => 0, getVirtualItems: () => [] }),
}));

const QScroll = defineComponent({
	methods: {
		getScrollTarget(): HTMLElement {
			return this.$el;
		},
	},
	template: '<div><slot /></div>',
});

describe('MediaTable scroll lifecycle', () => {
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		scrollToIndex.mockClear();
	});

	test('Should replay the pending scroll and stop scrolling the old table after unmount', async () => {
		// Arrange
		const store = useMediaOverviewStore();
		const commands = new BehaviorSubject({ index: 4, highlight: false });
		vi.spyOn(store, 'getScrollCommand').mockReturnValue(commands.asObservable());
		const wrapper = await mountSuspended(MediaTable, {
			props: { rows: [] },
			global: { plugins: [getActivePinia()!], stubs: { QScroll, MediaTableHeader: true } },
		});

		// Act and assert: the pre-mount request is replayed, and live commands still reach the table.
		await flushPromises();
		commands.next({ index: 8, highlight: false });
		expect(scrollToIndex.mock.calls).toEqual([[4, { align: 'start' }], [8, { align: 'start' }]]);
		wrapper.unmount();
		commands.next({ index: 12, highlight: false });
		expect(commands.observed).toBe(false);
		expect(scrollToIndex).toHaveBeenCalledTimes(2);
	});
});
