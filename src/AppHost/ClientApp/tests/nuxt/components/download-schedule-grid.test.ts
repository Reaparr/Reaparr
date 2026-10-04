import { useNuxtApp } from '#app';
import { fireEvent, getByRole } from '@testing-library/dom';
import { defineComponent, h, nextTick, reactive, render, type AppContext } from 'vue';
import { afterEach, describe, expect, test } from 'vitest';
import DownloadScheduleGrid from '@components/Views/Settings/DownloadScheduleGrid.vue';
import { getDownloadScheduleSlots, type DownloadScheduleRange } from '@components/Views/Settings/downloadScheduleSelection';

interface GridHarness {
	container: HTMLElement;
	props: { limits: (number | null)[]; selection: number[]; previewLimit?: number | null };
	unmount: () => void;
}

async function mountGrid(): Promise<GridHarness> {
	const container = document.createElement('div');
	document.body.append(container);
	const props = reactive({
		limits: Array<number | null>(336).fill(null),
		selection: [18],
		previewLimit: undefined,
		days: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
		label: 'Download schedule',
		unlimitedLabel: 'Unlimited',
		locale: 'en-US',
		timeZone: 'UTC',
	});
	const Root = defineComponent({
		setup: () => () => h(DownloadScheduleGrid, {
			...props,
			onSelect: (range: DownloadScheduleRange) => { props.selection = getDownloadScheduleSlots(range); },
		}),
	});
	const vnode = h(Root);
	vnode.appContext = useNuxtApp().vueApp._context as AppContext;
	render(vnode, container);
	await nextTick();
	return {
		container,
		props,
		unmount: () => {
			render(null, container);
			container.remove();
		},
	};
}

describe('DownloadScheduleGrid selection gestures', () => {
	let harness: GridHarness | undefined;
	afterEach(() => harness?.unmount());

	test('Should show kB/s on confirmed rate bands before and after changing the selection', async () => {
		// Arrange
		harness = await mountGrid();
		harness.props.limits.fill(5000);
		harness.props.selection = [];
		await nextTick();

		// Assert
		expect([...harness.container.querySelectorAll('.schedule-band-label')].map(label => label.textContent?.trim())).toEqual(Array(7).fill('5,000 kB/s'));

		// Act
		harness.props.selection = [18];
		harness.props.previewLimit = 3000;
		await nextTick();

		// Assert
		expect([...harness.container.querySelectorAll('.schedule-band-label')].map(label => label.textContent?.trim())).toEqual([
			'5,000 kB/s', '3,000 kB/s', '5,000 kB/s', ...Array(6).fill('5,000 kB/s'),
		]);
	});

	test('Should preview the chosen rate only inside the selection without changing saved limits', async () => {
		// Arrange
		harness = await mountGrid();
		harness.props.limits.fill(100);
		harness.props.selection = [18, 19, 66, 67];

		// Act
		harness.props.previewLimit = 4300;
		await nextTick();

		// Assert
		expect(getByRole(harness.container, 'gridcell', { name: 'Monday 09:00–09:30: 4,300 kB/s' }).getAttribute('aria-selected')).toBe('true');
		expect(getByRole(harness.container, 'gridcell', { name: 'Tuesday 09:30–10:00: 4,300 kB/s' }).getAttribute('aria-selected')).toBe('true');
		expect(getByRole(harness.container, 'gridcell', { name: 'Monday 10:00–10:30: 100 kB/s' }).getAttribute('aria-selected')).toBe('false');
		expect(harness.props.limits[18]).toBe(100);

		// Act
		harness.props.previewLimit = null;
		harness.props.selection = [18];
		await nextTick();

		// Assert
		expect(getByRole(harness.container, 'gridcell', { name: 'Monday 09:00–09:30: Unlimited' }).getAttribute('aria-selected')).toBe('true');
		expect(getByRole(harness.container, 'gridcell', { name: 'Monday 09:30–10:00: 100 kB/s' }).getAttribute('aria-selected')).toBe('false');
	});

	test('Should preserve selection during touch scrolling and select only after a completed tap', async () => {
		// Arrange
		harness = await mountGrid();
		const original = getByRole(harness.container, 'gridcell', { name: 'Monday 09:00–09:30: Unlimited' });
		const target = getByRole(harness.container, 'gridcell', { name: 'Monday 09:30–10:00: Unlimited' });
		const pointer = { bubbles: true, pointerType: 'touch', isPrimary: true, pointerId: 1, button: 0 };

		// Act
		fireEvent(target, new PointerEvent('pointerdown', pointer));
		fireEvent(target, new PointerEvent('pointercancel', pointer));
		await nextTick();

		// Assert
		expect(original.getAttribute('aria-selected')).toBe('true');
		expect(target.getAttribute('aria-selected')).toBe('false');

		// Act
		fireEvent(target, new PointerEvent('pointerdown', pointer));
		fireEvent(target, new PointerEvent('pointerup', pointer));
		fireEvent.click(target, { detail: 1 });
		await nextTick();

		// Assert
		expect(original.getAttribute('aria-selected')).toBe('false');
		expect(target.getAttribute('aria-selected')).toBe('true');
	});

	test('Should support assistive clicks and extend the selected interval with Shift and arrows', async () => {
		// Arrange
		harness = await mountGrid();
		const monday = getByRole(harness.container, 'gridcell', { name: 'Monday 09:30–10:00: Unlimited' });
		const tuesday = getByRole(harness.container, 'gridcell', { name: 'Tuesday 09:30–10:00: Unlimited' });

		// Act
		fireEvent.click(monday, { detail: 0 });
		await nextTick();
		fireEvent.keyDown(monday, { key: 'ArrowDown', shiftKey: true });
		await nextTick();

		// Assert
		expect(monday.getAttribute('aria-selected')).toBe('true');
		expect(tuesday.getAttribute('aria-selected')).toBe('true');
		expect(document.activeElement).toBe(tuesday);
	});
});
