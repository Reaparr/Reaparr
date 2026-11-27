import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { useMediaStore } from '@store';

describe('Media thumbnail replay portability', () => {
	beforeAll(() => baseSetup());
	beforeEach(() => setActivePinia(createPinia()));

	test('Should retain image bytes without a browser-local URL and reuse the cached thumbnail', async () => {
		const mock = getAxiosMock();
		const image = new Blob(['thumbnail bytes'], { type: 'image/png' });
		const query = { plexServerId: 11, plexKey: '22', metaDataKey: 33, width: 200, height: 300 };
		mock.onGet('/api/PlexMedia/thumbnail').reply(200, image);
		const store = useMediaStore();

		const first = subscribeSpyTo(store.getMediaThumbnailUrl(query));
		await first.onComplete();
		const second = subscribeSpyTo(store.getMediaThumbnailUrl(query));
		await second.onComplete();

		expect(first.getFirstValue()).toBe('data:image/png;base64,dGh1bWJuYWlsIGJ5dGVz');
		expect(second.getFirstValue()).toBe(first.getFirstValue());
		expect(mock.history.get.filter((request) => request.url === '/api/PlexMedia/thumbnail')).toHaveLength(1);
	});
});
