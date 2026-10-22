import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { defineComponent, h } from 'vue';
import { flushPromises } from '@vue/test-utils';
import { mountSuspended } from '@nuxt/test-utils/runtime';
import { createI18n } from 'vue-i18n';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { FolderPathPaths } from '@api/api-paths';
import { useDialogStore } from '@store';
import DirectoryBrowser from '@/components/Dialogs/DirectoryBrowser.vue';
import { FileSystemEntityType, FolderType, PlexMediaType, type FileSystemDTO, type FileSystemModelDTO, type FolderPathDTO } from '@dto';
import { DialogType } from '@enums';
import messages from '@/lang/en-US.json';

const CardDialog = defineComponent({
	name: 'QCardDialog',
	props: { loading: Boolean },
	emits: ['opened', 'closed'],
	setup(props, { slots, emit }) {
		return () => h('section', { 'aria-busy': props.loading }, [
			slots.title?.(),
			slots['top-row']?.(),
			slots.default?.(),
			slots.actions?.({ close: () => emit('closed') }),
		]);
	},
});

const PTextField = defineComponent({
	name: 'PTextField',
	props: { modelValue: { type: String, default: '' } },
	emits: ['update:modelValue'],
	setup(props, { emit }) {
		return () => h('input', {
			value: props.modelValue,
			onInput: (event: Event) => {
				if (event.target instanceof HTMLInputElement)
					emit('update:modelValue', event.target.value);
			},
		});
	},
});

const ActionButton = (name: string) => defineComponent({
	name,
	props: {
		disabled: { type: Boolean, default: false },
		loading: { type: Boolean, default: false },
	},
	emits: ['click'],
	setup(props, { emit, attrs }) {
		return () => h('button', {
			...attrs,
			'data-action': name,
			'data-cy': attrs.cy,
			disabled: props.disabled,
			'data-loading': props.loading ? 'true' : undefined,
			onClick: () => emit('click'),
		});
	},
});

const Transparent = (name: string, tag = 'div') => defineComponent({
	name,
	setup(_, { slots }) {
		return () => h(tag, slots.default?.());
	},
});

const Alert = defineComponent({
	name: 'QAlert',
	setup(_, { attrs, slots }) {
		return () => h('div', { ...attrs, 'data-cy': attrs.cy }, slots.default?.());
	},
});

const folderPath: FolderPathDTO = {
	id: 7,
	displayName: 'Movies',
	directory: '/root',
	mediaType: PlexMediaType.Movie,
	folderType: FolderType.MovieFolder,
	isValid: true,
	isDefault: true,
};

const writableDirectory: FileSystemModelDTO = {
	name: 'Movies',
	path: '/root/Movies',
	type: FileSystemEntityType.Folder,
	extension: '',
	size: 0,
	lastModified: '',
	hasReadPermission: true,
	hasWritePermission: true,
};

function directoryResult(directory: string, writable = true): FileSystemDTO {
	return {
		current: { ...writableDirectory, path: directory, hasWritePermission: writable },
		directories: [{
			...writableDirectory,
			name: `${directory.slice(directory.lastIndexOf('/') + 1)}-child`,
			path: `${directory}/child`,
		}],
		files: [],
		parent: directory.slice(0, directory.lastIndexOf('/')) || '/',
	};
}

function deferredDirectoryResponse() {
	let reply!: (value: [number, unknown]) => void;
	const response = new Promise<[number, unknown]>((resolve) => {
		reply = resolve;
	});
	return { response, reply };
}

const mounted: Array<{ unmount: () => void }> = [];

afterEach(() => {
	mounted.forEach((wrapper) => wrapper.unmount());
	mounted.length = 0;
	vi.restoreAllMocks();
});

describe('DirectoryBrowser', () => {
	let { mock } = baseVars();
	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	async function render(props: { saving: boolean; errorMessage: string }) {
		const directoryResult: FileSystemDTO = {
			current: { ...writableDirectory, path: folderPath.directory },
			directories: [writableDirectory],
			files: [],
			parent: '/',
		};
		mock.onGet(FolderPathPaths.getFolderPathDirectoryEndpoint()).reply(200, generateResultDTO(directoryResult));
		const wrapper = await mountSuspended(DirectoryBrowser, {
			props,
			global: {
				plugins: [createI18n({ legacy: false, locale: 'en-US', messages: { 'en-US': messages } })],
				stubs: {
					QCardDialog: CardDialog,
					PTextField,
					ConfirmButton: ActionButton('ConfirmButton'),
					CancelButton: ActionButton('CancelButton'),
					QAlert: Alert,
					QRow: Transparent('QRow'),
					QCol: Transparent('QCol'),
					'q-markup-table': Transparent('QMarkupTable', 'table'),
					'q-tooltip': true,
				},
			},
		});
		mounted.push(wrapper);
		return wrapper;
	}

	test('Should emit selected paths without closing and report both lifecycle directions', async () => {
		// Arrange
		const wrapper = await render({ saving: false, errorMessage: '' });
		const dialogStore = useDialogStore();
		const closeDialog = vi.spyOn(dialogStore, 'closeDialog');
		const cardDialog = wrapper.getComponent(CardDialog);

		// Act
		cardDialog.vm.$emit('opened', folderPath);
		await flushPromises();
		await wrapper.get('[data-cy="directory-browser-confirm-button"]').trigger('click');

		// Assert
		expect(wrapper.emitted('opened')?.[0]?.[0]).toEqual(folderPath);
		expect(wrapper.emitted('confirm')?.[0]?.[0]).toEqual(folderPath);
		expect(wrapper.emitted('closed')).toBeUndefined();
		expect(closeDialog).not.toHaveBeenCalledWith(DialogType.DirectoryBrowserDialog);

		// Act implicit close, then explicit cancel
		cardDialog.vm.$emit('closed');
		await wrapper.get('[data-action="CancelButton"]').trigger('click');

		// Assert
		expect(wrapper.emitted('closed')).toHaveLength(1);
		expect(wrapper.emitted('cancel')).toHaveLength(1);
		expect(closeDialog).toHaveBeenCalledWith(DialogType.DirectoryBrowserDialog);
	});

	test('Should show save errors and block navigation and confirmation while saving', async () => {
		// Arrange
		const wrapper = await render({ saving: false, errorMessage: '' });
		const cardDialog = wrapper.getComponent(CardDialog);
		cardDialog.vm.$emit('opened', folderPath);
		await flushPromises();
		await wrapper.setProps({ saving: true, errorMessage: 'The directory could not be saved.' });
		const initialRequests = mock.history.get.length;

		// Act
		await wrapper.get('[data-cy="directory-browser-row-0"]').trigger('click');
		await wrapper.get('[data-cy="directory-browser-confirm-button"]').trigger('click');
		await flushPromises();

		// Assert
		expect(wrapper.get('[data-cy="directory-browser-save-error"]').text()).toContain('The directory could not be saved.');
		expect(wrapper.get('[data-cy="directory-browser-confirm-button"]').attributes('disabled')).toBeDefined();
		expect(wrapper.getComponent(PTextField).attributes('readonly')).toBeDefined();
		expect(mock.history.get.length).toBe(initialRequests);
		expect(wrapper.emitted('confirm')).toBeUndefined();
	});

	test('Should accept only the latest directory permissions and parent after reordered lookups', async () => {
		// Arrange
		const wrapper = await render({ saving: false, errorMessage: '' });
		wrapper.getComponent(CardDialog).vm.$emit('opened', folderPath);
		await flushPromises();
		const oldBack = wrapper.get('[data-cy="directory-browser-row-return"]');
		const older = deferredDirectoryResponse();
		const newer = deferredDirectoryResponse();
		const endpoint = FolderPathPaths.getFolderPathDirectoryEndpoint();
		mock.resetHandlers();
		mock.onGet(endpoint).replyOnce(() => older.response)
			.onGet(endpoint).replyOnce(() => newer.response)
			.onGet(endpoint).reply(200, generateResultDTO(directoryResult('/root')));
		const confirm = wrapper.get('[data-cy="directory-browser-confirm-button"]');

		try {
			// Act: changing the path revokes the previous writable result immediately.
			await wrapper.get('input').setValue('/old-folder');
			await vi.waitFor(() => expect(mock.history.get).toHaveLength(2));
			expect(confirm.attributes('disabled')).toBeDefined();
			expect(wrapper.find('[data-cy="directory-browser-row-0"]').exists()).toBe(false);
			expect(wrapper.find('[data-cy="directory-browser-row-return"]').exists()).toBe(false);
			wrapper.getComponent({ name: 'ConfirmButton' }).vm.$emit('click');
			await oldBack.trigger('click');
			expect(wrapper.emitted('confirm')).toBeUndefined();
			expect(mock.history.get).toHaveLength(2);
			await wrapper.get('input').setValue('/root/new');
			await vi.waitFor(() => expect(mock.history.get).toHaveLength(3));
			newer.reply([200, generateResultDTO(directoryResult('/root/new', false))]);
			await flushPromises();
			older.reply([200, generateResultDTO(directoryResult('/old-folder'))]);
			await flushPromises();

			// Assert: the late writable response cannot replace the newer read-only result.
			expect(wrapper.get('input').element.value).toBe('/root/new');
			expect(wrapper.get('[data-cy="directory-browser-row-0"]').text()).toContain('new-child');
			expect(confirm.attributes('disabled')).toBeDefined();
			wrapper.getComponent({ name: 'ConfirmButton' }).vm.$emit('click');
			expect(wrapper.emitted('confirm')).toBeUndefined();

			// Act and assert: Back uses the newer parent and permits the resolved writable path.
			await wrapper.get('[data-cy="directory-browser-row-return"]').trigger('click');
			await flushPromises();
			expect(mock.history.get.at(-1)?.params).toEqual({ path: '/root' });
			expect(confirm.attributes('disabled')).toBeUndefined();
			await confirm.trigger('click');
			expect(wrapper.emitted('confirm')).toEqual([[folderPath]]);
		} finally {
			older.reply([200, generateResultDTO(directoryResult('/old-folder'))]);
			newer.reply([200, generateResultDTO(directoryResult('/root/new', false))]);
			await flushPromises();
		}
	});

	test('Should keep path editing nonblocking and progress owned by the latest pending lookup', async () => {
		// Arrange
		const wrapper = await render({ saving: false, errorMessage: '' });
		wrapper.getComponent(CardDialog).vm.$emit('opened', folderPath);
		await flushPromises();
		const older = deferredDirectoryResponse();
		const newer = deferredDirectoryResponse();
		const endpoint = FolderPathPaths.getFolderPathDirectoryEndpoint();
		mock.resetHandlers();
		mock.onGet(endpoint).replyOnce(() => older.response)
			.onGet(endpoint).replyOnce(() => newer.response);

		try {
			// Act: replacing a pending lookup keeps the input available and busy.
			await wrapper.get('input').setValue('/older');
			await vi.waitFor(() => expect(mock.history.get).toHaveLength(2));
			expect(wrapper.get('input').attributes('aria-busy')).toBe('true');
			expect(wrapper.get('input').attributes('readonly')).toBeUndefined();
			expect(wrapper.getComponent(CardDialog).attributes('aria-busy')).toBe('false');
			await wrapper.get('input').setValue('/newer');
			await vi.waitFor(() => expect(mock.history.get).toHaveLength(3));
			expect(wrapper.get('input').attributes('aria-busy')).toBe('true');
			older.reply([200, generateResultDTO(directoryResult('/older'))]);
			await flushPromises();

			// Assert: completing an obsolete lookup cannot stop the current progress.
			expect(wrapper.get('input').attributes('aria-busy')).toBe('true');
			expect(wrapper.get('input').element.value).toBe('/newer');
			expect(wrapper.find('[data-cy="directory-browser-row-0"]').exists()).toBe(false);
			expect(wrapper.get('[data-cy="directory-browser-confirm-button"]').attributes('disabled')).toBeDefined();
			newer.reply([200, generateResultDTO(directoryResult('/newer'))]);
			await flushPromises();
			expect(wrapper.get('input').attributes('aria-busy')).toBe('false');
			expect(wrapper.get('[data-cy="directory-browser-row-0"]').text()).toContain('newer-child');
			expect(wrapper.get('[data-cy="directory-browser-confirm-button"]').attributes('disabled')).toBeUndefined();
		} finally {
			older.reply([200, generateResultDTO(directoryResult('/older'))]);
			newer.reply([200, generateResultDTO(directoryResult('/newer'))]);
			await flushPromises();
		}
	});

	test('Should release a failed first load and allow correction to a writable directory', async () => {
		// Arrange
		const wrapper = await render({ saving: false, errorMessage: '' });
		const endpoint = FolderPathPaths.getFolderPathDirectoryEndpoint();
		mock.resetHandlers();
		mock.onGet(endpoint).replyOnce(500, generateFailedResultDTO({ statusCode: 500 }))
			.onGet(endpoint).reply(200, generateResultDTO(directoryResult('/root/recovered')));
		wrapper.getComponent(CardDialog).vm.$emit('opened', folderPath);

		// Act and assert: failure leaves no permissions and releases the first-load overlay.
		await flushPromises();
		expect(wrapper.getComponent(CardDialog).attributes('aria-busy')).toBe('false');
		expect(wrapper.get('input').attributes('aria-busy')).toBe('false');
		expect(wrapper.get('[data-cy="directory-browser-confirm-button"]').attributes('disabled')).toBeDefined();
		expect(wrapper.find('[data-cy="directory-browser-row-0"]').exists()).toBe(false);
		expect(wrapper.get('input').attributes('readonly')).toBeUndefined();
		await wrapper.get('input').setValue('/root/recovered');
		await flushPromises();
		await wrapper.get('[data-cy="directory-browser-confirm-button"]').trigger('click');
		expect(wrapper.emitted('confirm')).toEqual([[{ ...folderPath, directory: '/root/recovered' }]]);
	});

	test('Should keep a reopened dialog independent of a previous pending lookup', async () => {
		// Arrange
		const wrapper = await render({ saving: false, errorMessage: '' });
		const old = deferredDirectoryResponse();
		const endpoint = FolderPathPaths.getFolderPathDirectoryEndpoint();
		mock.resetHandlers();
		mock.onGet(endpoint).replyOnce(() => old.response)
			.onGet(endpoint).reply(200, generateResultDTO(directoryResult('/new-dialog')));
		const dialog = wrapper.getComponent(CardDialog);
		const reopened = { ...folderPath, id: 8, displayName: 'Other destination', directory: '/new-dialog' };
		dialog.vm.$emit('opened', folderPath);
		await vi.waitFor(() => expect(mock.history.get).toHaveLength(1));
		expect(wrapper.get('input').attributes('aria-busy')).toBe('true');

		try {
			// Act: close and reopen before the old read-only response arrives.
			dialog.vm.$emit('closed');
			await flushPromises();
			expect(wrapper.get('input').attributes('aria-busy')).toBe('false');
			wrapper.getComponent({ name: 'ConfirmButton' }).vm.$emit('click');
			expect(wrapper.emitted('confirm')).toBeUndefined();
			dialog.vm.$emit('opened', reopened);
			await flushPromises();
			old.reply([200, generateResultDTO(directoryResult('/root', false))]);
			await flushPromises();

			// Assert
			expect(wrapper.get('input').element.value).toBe('/new-dialog');
			expect(wrapper.get('[data-cy="directory-browser-row-0"]').text()).toContain('new-dialog-child');
			expect(wrapper.get('input').attributes('aria-busy')).toBe('false');
			expect(wrapper.get('[data-cy="directory-browser-confirm-button"]').attributes('disabled')).toBeUndefined();
			await wrapper.get('[data-cy="directory-browser-confirm-button"]').trigger('click');
			expect(wrapper.emitted('confirm')).toEqual([[reopened]]);
		} finally {
			old.reply([200, generateResultDTO(directoryResult('/root', false))]);
			await flushPromises();
		}
	});
});
