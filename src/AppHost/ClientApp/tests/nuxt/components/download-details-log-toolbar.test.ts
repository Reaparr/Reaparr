import { useNuxtApp } from '#app';
import { fireEvent, getByRole, queryByRole } from '@testing-library/dom';
import { h, nextTick, render } from 'vue';
import { afterEach, beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { baseSetup, getAxiosMock } from '@services-test-base';
import DownloadDetailsDialogLogsTabContent from '@components/Dialogs/DownloadDetailsDialog/Tabs/DownloadDetailsDialogLogsTabContent.vue';

describe('Download details log-toolbar accessibility', () => {
	let container: HTMLElement;

	beforeAll(() => baseSetup());
	beforeEach(async () => {
		getAxiosMock();
		container = document.createElement('div');
		document.body.append(container);
		const vnode = h(DownloadDetailsDialogLogsTabContent, { downloadTaskId: 'toolbar-task' });
		vnode.appContext = useNuxtApp().vueApp._context;
		render(vnode, container);
		await nextTick();
	});
	afterEach(() => {
		render(null, container);
		container.remove();
	});

	test('Should expose focusable named controls and announce the changed sort direction', async () => {
		// Arrange
		for (const name of ['Copy all logs', 'Filter by log level', 'Delete all logs']) {
			const button = getByRole(container, 'button', { name });
			button.focus();
			expect(document.activeElement).toBe(button);
		}
		const sortButton = getByRole(container, 'button', { name: 'Oldest first' });

		// Act
		fireEvent.click(sortButton);
		await nextTick();

		// Assert
		expect(getByRole(container, 'button', { name: 'Newest first' })).toBe(sortButton);
		expect(queryByRole(container, 'button', { name: 'Oldest first' })).toBeNull();
	});
});
