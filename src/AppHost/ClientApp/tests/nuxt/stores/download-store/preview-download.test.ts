import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { DownloadPaths } from '@api-urls';
import { PhotoItemKind, PlexMediaType, type DownloadMediaDTO, type DownloadPreviewContainerDTO } from '@dto';
import { generateResultDTO } from '@mock';
import { useDownloadStore } from '@store';

describe('DownloadStore.previewDownload() request contract', () => {
	let { mock } = baseVars();

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test.each([
		[PlexMediaType.PhotoImage, PhotoItemKind.Asset],
		[PlexMediaType.Movie, null],
	] as const)('Should send a preview envelope and preserve the %s selection identity', async (type, selectionKind) => {
		// Arrange
		const store = useDownloadStore();
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: [], patchSequenceByServer: {} }));
		await subscribeSpyTo(store.setup()).onComplete();
		const selection: DownloadMediaDTO = {
			mediaIds: [71], type, selectionKind,
			plexServerId: 3, plexLibraryId: 4,
			qualities: [], keepCompletedInDownloadFolder: false,
		};
		const preview: DownloadPreviewContainerDTO = {
			expanded: {}, totalSize: 8192,
			previews: [{ key: 'asset-71', title: 'Selected original', type, size: 8192, children: [], qualities: [] }],
		};
		mock.onPost(DownloadPaths.getDownloadPreviewEndpoint()).reply(200, generateResultDTO(preview));

		// Act
		const result = subscribeSpyTo(store.previewDownload({ downloadMedias: [selection], customDestinationFolderPath: '' }));
		await result.onComplete();

		// Assert
		expect(mock.history.post).toHaveLength(1);
		expect(JSON.parse(mock.history.post[0]!.data)).toEqual({
			downloadMedias: [{
				mediaIds: [71], type, selectionKind,
				plexServerId: 3, plexLibraryId: 4,
				qualities: [], keepCompletedInDownloadFolder: false,
			}],
			customDestinationFolderPath: '',
		});
		expect(result.getLastValue()).toEqual(preview);
	});
});
