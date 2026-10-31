import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject } from 'rxjs';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateResultDTO } from '@mock';
import { plexPhotoApi } from '@api';
import { PhotoItemKind, PlexMediaType } from '@dto';
import type { PhotoAlbumBrowseDTO, PhotoAlbumPageDTO, PlexLibraryDTO, RefreshDataType } from '@dto';
import { useLibraryStore, usePhotoBrowsingStore, useSignalrStore } from '@store';
import { mockPhotoMetadata } from '../../helpers/photo-metadata';
import type { ResultDTO } from '@interfaces';
import type { IPhotoBrowseActivation } from '@/store/photoBrowsingStore';
import { createDefaultMediaOverviewSort } from '@composables';

describe('PhotoBrowsingStore - photo session lifecycle', () => {
	const owner = {};
	beforeAll(() => baseSetup());
	beforeEach(() => {
		getAxiosMock();
		setActivePinia(createPinia());
		vi.restoreAllMocks();
		mockPhotoMetadata();
	});

	const route: IPhotoBrowseActivation = { mode: 'library', libraryId: 7, view: 'albums', folderId: null, searchQuery: '', sort: createDefaultMediaOverviewSort() };

	test('preserves selection while navigating within Photos and clears it only when leaving Photos', () => {
		const store = usePhotoBrowsingStore();
		const album = { id: 1, plexServerId: 2, plexLibraryId: 3, title: 'Selected album' } as PhotoAlbumBrowseDTO;
		store.toggleAlbum(album, true);

		store.deactivate(owner);
		expect(store.selectedCount).toBe(1);
		store.leavePhotos(owner);
		expect(store.selectedCount).toBe(0);
	});

	test('keeps selection keys scoped by item kind', () => {
		const store = usePhotoBrowsingStore();
		store.selectedSelectors = [{ id: 1, plexServerId: 2, plexLibraryId: 3, kind: PhotoItemKind.Album, type: PlexMediaType.PhotoAlbum, title: 'Album', stale: false }];
		store.selectedSelectors.push({ id: 1, plexServerId: 2, plexLibraryId: 3, kind: PhotoItemKind.Asset, type: PlexMediaType.PhotoImage, title: 'Asset', stale: false });
		expect(store.selectedCount).toBe(2);
	});

	test('resets the sparse cache, dialog and pending response', async () => {
		const response = new Subject<ResultDTO<PhotoAlbumPageDTO>>();
		vi.spyOn(plexPhotoApi, 'getPhotoAlbumsEndpoint').mockReturnValue(response.asObservable());
		useLibraryStore().updateLibrary({ id: 7, plexServerId: 1, type: PlexMediaType.PhotoAlbum, isEnabled: true } as PlexLibraryDTO);
		const store = usePhotoBrowsingStore();
		store.toggleAlbum({ id: 33, plexServerId: 1, plexLibraryId: 7, title: 'Selected' } as PhotoAlbumBrowseDTO, true);
		const activation = subscribeSpyTo(store.activate(route, owner));

		store.$reset();
		response.next(generateResultDTO({ items: [], page: 1, pageSize: 100, totalCount: 0, totalAssetBytes: 0 } as PhotoAlbumPageDTO));
		response.complete();
		await activation.onComplete();

		expect(store.albums.items).toEqual([]);
		expect(store.selectedCount).toBe(0);
		expect(store.dialog.open).toBe(false);
		expect(store.active).toBe(false);
	});

	test('setup is idempotent and subscribes again after reset', async () => {
		const notifications = new Subject<RefreshDataType>();
		const signalr = useSignalrStore();
		const refreshSubscription = vi.spyOn(signalr, 'getRefreshNotification').mockReturnValue(notifications.asObservable());
		const store = usePhotoBrowsingStore();

		await subscribeSpyTo(store.setup()).onComplete();
		await subscribeSpyTo(store.setup()).onComplete();
		expect(refreshSubscription).toHaveBeenCalledOnce();
		store.$reset();
		await subscribeSpyTo(store.setup()).onComplete();
		expect(refreshSubscription).toHaveBeenCalledTimes(2);
	});
});
