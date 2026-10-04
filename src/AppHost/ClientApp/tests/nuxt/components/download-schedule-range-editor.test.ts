import { useNuxtApp } from '#app';
import { fireEvent, getByRole, queryByRole } from '@testing-library/dom';
import { defineComponent, h, nextTick, reactive, render, type AppContext } from 'vue';
import { afterEach, describe, expect, test } from 'vitest';
import type { DownloadScheduleRange } from '@components/Views/Settings/downloadScheduleSelection';
import DownloadScheduleRangeEditor from '@components/Views/Settings/DownloadScheduleRangeEditor.vue';

const days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
const range = { days: [0], from: 18, until: 19 };
interface EditorProps {
	range: DownloadScheduleRange;
	days: string[];
	selection: number[];
	selectedLimits: (number | null)[];
	defaultLimit?: number | null;
	saving: boolean;
}

interface EditorHarness {
	container: HTMLElement;
	props: EditorProps;
	applied: (number | null)[];
	previews: (number | null | undefined)[];
	unmount: () => void;
}

async function mountEditor(): Promise<EditorHarness> {
	const container = document.createElement('div');
	document.body.append(container);
	const props = reactive<EditorProps>({
		range,
		days,
		selection: [],
		selectedLimits: [],
		saving: false,
	});
	const applied: (number | null)[] = [];
	const previews: (number | null | undefined)[] = [];
	const Root = defineComponent({
		setup: () => () => h(DownloadScheduleRangeEditor, {
			...props,
			onApply: (limit: number | null) => applied.push(limit),
			onPreview: (limit: number | null | undefined) => previews.push(limit),
		}),
	});
	const vnode = h(Root);
	vnode.appContext = useNuxtApp().vueApp._context as AppContext;
	render(vnode, container);
	props.selection = [18];
	props.selectedLimits = [100];
	await nextTick();
	return {
		container,
		props,
		applied,
		previews,
		unmount: () => {
			render(null, container);
			container.remove();
		},
	};
}

function limitInput(container: HTMLElement): HTMLInputElement {
	return getByRole(container, 'spinbutton') as HTMLInputElement;
}

async function enterLimit(container: HTMLElement, value: number, commit = true) {
	const input = limitInput(container);
	input.focus();
	input.select();
	for (const key of String(value)) fireEvent.keyPress(input, { key, code: `Digit${key}` });
	if (commit) fireEvent.blur(input);
	await nextTick();
}

function expectNumericValue(container: HTMLElement, value: number) {
	expect(Number(limitInput(container).value.replace(/[^\d.-]/g, ''))).toBe(value);
}

describe('DownloadScheduleRangeEditor confirmed-limit synchronization', () => {
	let harness: EditorHarness | undefined;

	afterEach(() => harness?.unmount());

	test('Should prefill the highest selected finite limit when choosing a limit for mixed slots', async () => {
		// Arrange
		harness = await mountEditor();
		harness.props.selection = [18, 19, 20];
		harness.props.selectedLimits = [1000, null, 5000];
		harness.props.defaultLimit = 9000;
		await nextTick();

		// Act
		fireEvent.click(getByRole(harness.container, 'radio', { name: 'Limit' }));
		await nextTick();
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Assert
		expectNumericValue(harness.container, 5000);
		expect(harness.applied).toEqual([5000]);
	});

	test('Should reuse the existing schedule maximum for unlimited slots without changing them until Apply', async () => {
		// Arrange
		harness = await mountEditor();
		harness.props.selectedLimits = [null];
		harness.props.defaultLimit = 5000;
		await nextTick();
		expect(harness.applied).toEqual([]);

		// Act
		fireEvent.click(getByRole(harness.container, 'radio', { name: 'Limit' }));
		await nextTick();
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Assert
		expectNumericValue(harness.container, 5000);
		expect(harness.applied).toEqual([5000]);
		expect(harness.props.selectedLimits).toEqual([null]);
	});

	test('Should preview and apply an entered limit before the formatted field loses focus', async () => {
		// Arrange
		harness = await mountEditor();

		// Act
		await enterLimit(harness.container, 5000, false);
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Assert
		expect(harness.applied).toEqual([5000]);
		expect(harness.previews.at(-1)).toBe(5000);
	});

	test('Should not submit the previous rate when the speed field is cleared before blur', async () => {
		// Arrange
		harness = await mountEditor();
		const input = limitInput(harness.container);
		input.focus();
		input.select();

		// Act
		fireEvent.keyDown(input, { key: 'Backspace', code: 'Backspace' });
		await nextTick();
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Assert
		expect(harness.previews.at(-1)).toBeUndefined();
		expect(harness.applied).toEqual([]);
	});

	test('Should update a clean editor when confirmed limits change without changing the selection', async () => {
		// Arrange
		harness = await mountEditor();
		expectNumericValue(harness.container, 100);

		// Act
		harness.props.selectedLimits = [200];
		await nextTick();

		// Assert
		expectNumericValue(harness.container, 200);
	});

	test('Should preserve a genuinely newer local value when confirmed limits change', async () => {
		// Arrange
		harness = await mountEditor();
		await enterLimit(harness.container, 300);

		// Act
		harness.props.selectedLimits = [200];
		await nextTick();

		// Assert
		expectNumericValue(harness.container, 300);
	});

	test('Should preserve a genuinely newer local mode when confirmed limits change', async () => {
		// Arrange
		harness = await mountEditor();
		await fireEvent.click(getByRole(harness.container, 'radio', { name: 'Unlimited' }));
		await nextTick();

		// Act
		harness.props.selectedLimits = [200];
		await nextTick();
		await fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Assert
		expect(queryByRole(harness.container, 'spinbutton')).toBeNull();
		expect(harness.applied).toEqual([null]);
	});

	test.each([300, null])('Should apply the edited limit of %s after selecting different hours', async (editedLimit) => {
		// Arrange
		harness = await mountEditor();
		if (editedLimit === null) {
			fireEvent.click(getByRole(harness.container, 'radio', { name: 'Unlimited' }));
			await nextTick();
		} else {
			await enterLimit(harness.container, editedLimit);
		}

		// Act
		harness.props.selection = [19];
		harness.props.selectedLimits = editedLimit === null ? [400] : [null];
		await nextTick();
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Assert
		expect(harness.applied).toEqual([editedLimit]);
	});

	test.each([100, 300])('Should retain a newer draft of %i when an older Apply is acknowledged', async (newerLimit) => {
		// Arrange
		harness = await mountEditor();
		await enterLimit(harness.container, 200);
		await fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));
		expect(harness.applied).toEqual([200]);
		await enterLimit(harness.container, newerLimit);

		// Act
		harness.props.selectedLimits = [200];
		await nextTick();

		// Assert
		expectNumericValue(harness.container, newerLimit);
	});

	test('Should retain a submitted draft while another confirmed value arrives before acknowledgement', async () => {
		// Arrange
		harness = await mountEditor();
		await enterLimit(harness.container, 200);
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));

		// Act
		harness.props.selectedLimits = [150];
		await nextTick();

		// Assert
		expectNumericValue(harness.container, 200);
	});

	test('Should preserve in-progress typing when confirmed limits change before blur', async () => {
		// Arrange
		harness = await mountEditor();
		await enterLimit(harness.container, 300, false);

		// Act
		harness.props.selectedLimits = [200];
		await nextTick();

		// Assert
		expect(Number.parseFloat(limitInput(harness.container).value)).toBe(300);
		fireEvent.blur(limitInput(harness.container));
		await nextTick();
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));
		expect(harness.applied).toEqual([300]);
	});

	test.each([100, 200])('Should synchronize later confirmations after a submitted limit of %i is acknowledged', async (submitted) => {
		// Arrange
		harness = await mountEditor();
		await enterLimit(harness.container, submitted);
		fireEvent.click(getByRole(harness.container, 'button', { name: 'Apply to selection & save' }));
		expect(harness.applied).toEqual([submitted]);
		harness.props.selectedLimits = [submitted];
		await nextTick();

		// Act
		harness.props.selectedLimits = [400];
		await nextTick();

		// Assert
		expectNumericValue(harness.container, 400);
	});

	test('Should give the numeric bandwidth input a descriptive accessible name with units', async () => {
		// Arrange
		harness = await mountEditor();

		// Act
		const input = getByRole(harness.container, 'spinbutton', { name: /limit.*kB\/s/i });

		// Assert
		expect(input).toBe(limitInput(harness.container));
	});
});
