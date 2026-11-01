import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type MockAdapter from 'axios-mock-adapter';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateLibrarySyncJobQueue, generateResultDTO } from '@mock';
import { DownloadPaths } from '@api-urls';
import { DownloadStatus, IntegrationProvisioningState, IntegrationType, NotificationLevel, PhotoItemKind, PlexMediaType, TestConnectionStatus, type DownloadProgressDTO, type ServerDownloadProgressDTO } from '@dto';
import {
	useAccountStore,
	useAuthenticationStore,
	useDownloadStore,
	useGlobalStore,
	useIntegrationStore,
	useLibraryStore,
	usePhotoBrowsingStore,
	useNotificationsStore,
	useSignalrStore,
} from '@store';

function createDownload(id: string): DownloadProgressDTO {
	return {
		children: [],
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

function createServerDownload(id: number, downloadId: string): ServerDownloadProgressDTO {
	return {
		id,
		downloadableTasksCount: 1,
		downloads: [createDownload(downloadId)],
	};
}

function countDownloadRequests(mock: MockAdapter): number {
	return mock.history.get.filter((request) => request.url === DownloadPaths.getAllDownloadTasksEndpoint()).length;
}

describe('GlobalStore.resetSession()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
	});

	test('Should discard old-session queue responses while allowing a current-session queue after reset', async () => {
		// Arrange
		const globalStore = useGlobalStore();
		const authenticationStore = useAuthenticationStore();
		const downloadStore = useDownloadStore();
		const signalrStore = useSignalrStore();
		const oldQueue = [createServerDownload(101, 'old-session-download')];
		const currentQueue = [createServerDownload(202, 'new-session-download')];
		let releaseOldResponse!: (response: [number, unknown]) => void;
		const oldResponse = new Promise<[number, unknown]>((resolve) => {
			releaseOldResponse = resolve;
		});
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint())
			.replyOnce(200, generateResultDTO({ downloads: oldQueue, patchSequenceByServer: {} }))
			.onGet(DownloadPaths.getAllDownloadTasksEndpoint())
			.replyOnce(() => oldResponse)
			.onGet(DownloadPaths.getAllDownloadTasksEndpoint())
			.reply(200, generateResultDTO({ downloads: currentQueue, patchSequenceByServer: {} }));
		await subscribeSpyTo(downloadStore.setup()).onComplete();
		expect(downloadStore.serverDownloads).toEqual(oldQueue);
		authenticationStore.isLoggedIn = true;

		const oldFetch = subscribeSpyTo(downloadStore.fetchDownloadList());
		await vi.waitFor(() => expect(countDownloadRequests(mock)).toBe(2));
		let finishSignalrStop!: () => void;
		const signalrStop = new Promise<void>((resolve) => {
			finishSignalrStop = resolve;
		});
		const resetSignalr = vi.spyOn(signalrStore, 'resetSession').mockReturnValue(signalrStop);

		// Act
		const reset = globalStore.resetSession();
		const overlappingReset = globalStore.resetSession();
		const pausedFetch = subscribeSpyTo(downloadStore.fetchDownloadList());

		try {
			// Assert immediate auth/session boundary and suspended SDK behavior
			expect(authenticationStore.isLoggedIn).toBe(false);
			expect(resetSignalr).toHaveBeenCalledOnce();
			await pausedFetch.onComplete();
			expect(pausedFetch.getValues()).toEqual([]);
			expect(downloadStore.serverDownloads).toEqual(oldQueue);
			expect(countDownloadRequests(mock)).toBe(2);

			finishSignalrStop();
			await Promise.all([reset, overlappingReset]);
			expect(downloadStore.serverDownloads).toEqual([]);

			const currentFetch = subscribeSpyTo(downloadStore.fetchDownloadList());
			await currentFetch.onComplete();
			expect(currentFetch.getFirstValue()?.value).toEqual({ downloads: currentQueue, patchSequenceByServer: {} });
			expect(downloadStore.serverDownloads).toEqual(currentQueue);
			expect(downloadStore.serverDownloads.map((server) => server.id)).toEqual([202]);
			expect(downloadStore.serverDownloads[0]?.downloads[0]?.id).toBe('new-session-download');

			releaseOldResponse([200, generateResultDTO({ downloads: oldQueue, patchSequenceByServer: {} })]);
			await oldFetch.onComplete();
			expect(downloadStore.serverDownloads).toEqual(currentQueue);
			expect(countDownloadRequests(mock)).toBe(3);
		} finally {
			finishSignalrStop();
			releaseOldResponse([200, generateResultDTO({ downloads: oldQueue, patchSequenceByServer: {} })]);
			await Promise.all([reset, overlappingReset, oldFetch.onComplete()]);
		}
	});

	test('Should clear every session store when SignalR teardown fails', async () => {
		// Arrange
		const globalStore = useGlobalStore();
		const signalrStore = useSignalrStore();
		const accountStore = useAccountStore();
		const authenticationStore = useAuthenticationStore();
		const libraryStore = useLibraryStore();
		const integrationStore = useIntegrationStore();
		const photoStore = usePhotoBrowsingStore();
		const notificationsStore = useNotificationsStore();
		notificationsStore.notifications = [{
			id: 17, message: 'Previous session notification', hidden: false,
			level: NotificationLevel.Information, createdAt: '2026-10-20T00:00:00Z',
		}];
		photoStore.selectedSelectors = [{
			id: 33,
			plexServerId: 1,
			plexLibraryId: 1,
			kind: PhotoItemKind.Asset,
			type: PlexMediaType.PhotoImage,
			title: 'Selected photo',
			stale: false,
		}];
		accountStore.accounts = [{ id: 1 } as (typeof accountStore.accounts)[number]];
		authenticationStore.isLoggedIn = true;
		libraryStore.syncQueues = [generateLibrarySyncJobQueue({ plexLibraryId: 1, plexServerId: 1 })];
		integrationStore.items = [{
			id: 'integration-1',
			name: 'Sonarr',
			type: IntegrationType.Sonarr,
			baseUrl: 'http://sonarr',
			category: 'reaparr-sonarr',
			downloadFolderId: 1,
			externalDownloadClientId: null,
			externalIndexerId: null,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestHttpStatusCode: null,
			lastConnectionTestStatus: TestConnectionStatus.Unknown,
			lastConnectionTestedAt: null,
			provisioningState: IntegrationProvisioningState.Unconfigured,
		}];
		vi.spyOn(signalrStore, 'resetSession').mockRejectedValue(new Error('Hub stop failed'));

		// Act
		await globalStore.resetSession();

		// Assert
		expect(signalrStore.resetSession).toHaveBeenCalledOnce();
		expect(accountStore.accounts).toEqual([]);
		expect(authenticationStore.isLoggedIn).toBe(false);
		expect(libraryStore.syncQueues).toEqual([]);
		expect(integrationStore.items).toEqual([]);
		expect(photoStore.selectedSelectors).toEqual([]);
		expect(notificationsStore.notifications).toEqual([]);
	});

	test('Should resume future queue requests when SignalR teardown fails', async () => {
		// Arrange
		const globalStore = useGlobalStore();
		const signalrStore = useSignalrStore();
		const downloadStore = useDownloadStore();
		const recoveredQueue = [createServerDownload(303, 'teardown-recovered-download')];
		vi.spyOn(signalrStore, 'resetSession').mockRejectedValue(new Error('Hub stop failed'));
		mock.onGet(DownloadPaths.getAllDownloadTasksEndpoint()).reply(200, generateResultDTO({ downloads: recoveredQueue, patchSequenceByServer: {} }));

		// Act
		await globalStore.resetSession();
		const recoveredFetch = subscribeSpyTo(downloadStore.fetchDownloadList());
		await recoveredFetch.onComplete();

		// Assert
		expect(recoveredFetch.getFirstValue()?.value).toEqual({ downloads: recoveredQueue, patchSequenceByServer: {} });
		expect(downloadStore.serverDownloads).toEqual(recoveredQueue);
		expect(downloadStore.serverDownloads[0]?.downloads[0]?.id).toBe('teardown-recovered-download');
	});
});
