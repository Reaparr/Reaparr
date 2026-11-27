import { describe, beforeAll, beforeEach, test, expect } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock } from '@services-test-base';
import type { DownloadPatchMessagePackDTO, DownloadProgressDTO, ServerDownloadProgressDTO } from '@dto';
import { DownloadStatus, PlexMediaType } from '@dto';
import { useDownloadStore } from '@store';

function createDownload(id: string, children: DownloadProgressDTO[] = []): DownloadProgressDTO {
	return {
		children,
		dataReceived: 0,
		dataTotal: 100,
		downloadSpeed: 0,
		id,
		mediaType: PlexMediaType.Movie,
		percentage: 0,
		status: DownloadStatus.Queued,
		timeRemaining: 0,
		title: id,
	};
}

function createPatch(partial: Partial<DownloadPatchMessagePackDTO> = {}): DownloadPatchMessagePackDTO {
	return {
		deletedIds: [],
		sequence: 1,
		serverId: 7,
		upserts: [],
		...partial,
	};
}

describe('DownloadStore.updateDownloadPatch()', () => {
	let { mock: _mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		_mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should update only patched fields while preserving unrelated tree references', () => {
		// Arrange
		const downloadStore = useDownloadStore();
		const target = createDownload('target');
		const sibling = createDownload('sibling');
		const root = createDownload('root', [target, sibling]);
		const serverDownload: ServerDownloadProgressDTO = {
			id: 7,
			downloadableTasksCount: 2,
			downloads: [root],
		};
		downloadStore.serverDownloads = [serverDownload];
		const downloads = downloadStore.serverDownloads[0]!.downloads;
		const storedRoot = downloads[0]!;
		const storedTarget = storedRoot.children[0]!;
		const storedSibling = storedRoot.children[1]!;

		// Act
		downloadStore.updateDownloadPatch(createPatch({
			upserts: [{
				id: 'target',
				parentId: 'root',
				status: DownloadStatus.Downloading,
				percentage: 42.5,
				dataReceived: 425,
				dataTotal: 1000,
				downloadSpeed: 25,
				timeRemaining: 23,
			}],
		}));

		// Assert
		expect(downloadStore.serverDownloads[0]!.downloads).toBe(downloads);
		expect(downloadStore.serverDownloads[0]!.downloads[0]).toBe(storedRoot);
		expect(downloadStore.serverDownloads[0]!.downloads[0]!.children[0]).toBe(storedTarget);
		expect(downloadStore.serverDownloads[0]!.downloads[0]!.children[1]).toBe(storedSibling);
		expect(storedTarget).toMatchObject({
			status: DownloadStatus.Downloading,
			percentage: 42.5,
			dataReceived: 425,
			dataTotal: 1000,
			downloadSpeed: 25,
			timeRemaining: 23,
		});
		expect(storedSibling).toMatchObject({
			status: DownloadStatus.Queued,
			percentage: 0,
		});
	});

	test('Should preserve full-progress collection identity while merging node updates', () => {
		// Arrange
		const downloadStore = useDownloadStore();
		const root = createDownload('root');
		downloadStore.serverDownloads = [{
			id: 7,
			downloadableTasksCount: 1,
			downloads: [root],
		}];
		const downloads = downloadStore.serverDownloads[0]!.downloads;
		const storedRoot = downloads[0]!;

		// Build the cached node index before the full-progress merge.
		downloadStore.updateDownloadPatch(createPatch({
			upserts: [{
				id: 'root',
				parentId: '',
				status: DownloadStatus.Downloading,
				percentage: 10,
				dataReceived: 10,
				dataTotal: 100,
				downloadSpeed: 1,
				timeRemaining: 90,
			}],
		}));

		// Act
		const added = createDownload('added');
		downloadStore.updateServerDownloadProgress({
			id: 7,
			downloadableTasksCount: 2,
			downloads: [{
				...createDownload('root'),
				status: DownloadStatus.Downloading,
				percentage: 42,
			}, added],
		});
		downloadStore.updateDownloadPatch(createPatch({
			sequence: 2,
			upserts: [{
				id: 'added',
				parentId: '',
				status: DownloadStatus.Downloading,
				percentage: 7,
				dataReceived: 7,
				dataTotal: 100,
				downloadSpeed: 2,
				timeRemaining: 46,
			}],
		}));

		// Assert
		expect(downloadStore.serverDownloads[0]!.downloads).toBe(downloads);
		expect(downloadStore.serverDownloads[0]!.downloads[0]).toBe(storedRoot);
		expect(storedRoot).toMatchObject({
			status: DownloadStatus.Downloading,
			percentage: 42,
		});
		expect(downloadStore.serverDownloads[0]!.downloads[1]).toMatchObject({
			id: 'added',
			status: DownloadStatus.Downloading,
			percentage: 7,
		});
	});

	test('Should remove deleted subtrees in place while preserving surviving nodes', () => {
		// Arrange
		const downloadStore = useDownloadStore();
		const removedChild = createDownload('removed-child', [createDownload('removed-grandchild')]);
		const survivingChild = createDownload('surviving-child');
		const root = createDownload('root', [removedChild, survivingChild]);
		downloadStore.serverDownloads = [{
			id: 7,
			downloadableTasksCount: 2,
			downloads: [root],
		}];
		const downloads = downloadStore.serverDownloads[0]!.downloads;
		const children = downloads[0]!.children;
		const survivingNode = children[1]!;

		// Act
		downloadStore.updateDownloadPatch(createPatch({ deletedIds: ['removed-child'] }));

		// Assert
		expect(downloadStore.serverDownloads[0]!.downloads).toBe(downloads);
		expect(downloads[0]!.children).toBe(children);
		expect(downloads[0]!.children).toEqual([survivingNode]);
		expect(downloads[0]!.children[0]).toBe(survivingNode);
	});

	test.each([false, true])('Should prune all newly empty ancestors after deleting the last leaf (mixed patch: %s)', (mixedPatch) => {
		// Arrange
		const downloadStore = useDownloadStore();
		const emptyBranch = createDownload('empty-show', [createDownload('season', [
			createDownload('episode', [createDownload('last-file')]),
		])]);
		const retainedBranch = createDownload('retained-show', [createDownload('retained-file')]);
		downloadStore.serverDownloads = [{ id: 7, downloadableTasksCount: 2, downloads: [emptyBranch, retainedBranch] }];
		const downloads = downloadStore.getDownloadsByServerId(7);
		const retainedNode = downloads[1]!;
		const progress = {
			status: DownloadStatus.Downloading, percentage: 25, dataReceived: 25, dataTotal: 100, downloadSpeed: 5, timeRemaining: 15,
		};

		// Act
		downloadStore.updateDownloadPatch(createPatch({
			deletedIds: ['last-file'],
			upserts: mixedPatch
				? [{ id: 'empty-show', parentId: '', ...progress }, { id: 'retained-file', parentId: 'retained-show', ...progress }]
				: [],
		}));

		// Assert
		expect(downloadStore.getDownloadsByServerId(7)).toBe(downloads);
		expect(downloads.map((node) => node.id)).toEqual(['retained-show']);
		expect(downloads[0]).toBe(retainedNode);
		expect(retainedNode.children.map((node) => node.id)).toEqual(['retained-file']);
		expect(retainedNode.children[0]!.status).toBe(mixedPatch ? DownloadStatus.Downloading : DownloadStatus.Queued);
	});

	test('Should update a surviving node when deleting a sibling in the same patch', () => {
		// Arrange
		const downloadStore = useDownloadStore();
		const removedChild = createDownload('removed-child');
		const survivingChild = createDownload('surviving-child');
		const root = createDownload('root', [removedChild, survivingChild]);
		downloadStore.serverDownloads = [{
			id: 7,
			downloadableTasksCount: 2,
			downloads: [root],
		}];
		const downloads = downloadStore.serverDownloads[0]!.downloads;
		const survivingNode = downloads[0]!.children[1]!;

		// Act
		downloadStore.updateDownloadPatch(createPatch({
			deletedIds: ['removed-child'],
			upserts: [{
				id: 'surviving-child',
				parentId: 'root',
				status: DownloadStatus.Downloading,
				percentage: 55,
				dataReceived: 55,
				dataTotal: 100,
				downloadSpeed: 5,
				timeRemaining: 9,
			}],
		}));

		// Assert
		expect(downloads[0]!.children).toEqual([survivingNode]);
		expect(survivingNode).toMatchObject({
			status: DownloadStatus.Downloading,
			percentage: 55,
		});
	});
});
