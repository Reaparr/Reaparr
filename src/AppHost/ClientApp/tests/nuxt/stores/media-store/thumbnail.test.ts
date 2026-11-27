import { afterEach, beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { useMediaStore } from '@store';

describe('Media thumbnail Blob cache', () => {
	beforeAll(() => baseSetup());
	beforeEach(() => setActivePinia(createPinia()));
	afterEach(() => vi.restoreAllMocks());

	test('Should retain the fetched Blob and reuse its cached object URL', async () => {
		const mock = getAxiosMock();
		const image = new Blob(['thumbnail bytes'], { type: 'image/png' });
		const createObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:thumbnail-cache-test');
		const query = { plexServerId: 11, plexKey: '22', metaDataKey: 33, width: 200, height: 300 };
		mock.onGet('/api/PlexMedia/thumbnail').reply(200, image);
		const store = useMediaStore();

		const first = subscribeSpyTo(store.getMediaThumbnailUrl(query));
		await first.onComplete();
		const second = subscribeSpyTo(store.getMediaThumbnailUrl(query));
		await second.onComplete();

		expect(createObjectURL).toHaveBeenCalledExactlyOnceWith(image);
		expect(first.getFirstValue()).toBe('blob:thumbnail-cache-test');
		expect(second.getFirstValue()).toBe(first.getFirstValue());
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia/thumbnail')).toHaveLength(1);
	});
});
