import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateDefaultFolderPaths, generateFolderPath, generateResultDTO } from '@mock';
import { FolderPathPaths } from '@api/api-paths';
import { PlexMediaType } from '@dto';
import { useFolderPathStore } from '@store';

describe('FolderPathStore family destinations', () => {
	let { mock } = baseVars();

	beforeAll(() => baseSetup());
	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
	});

	test('Should expose supported defaults and isolate each library family after loading destinations', async () => {
		// Arrange
		const defaults = generateDefaultFolderPaths();
		const families = [
			{ type: PlexMediaType.Movie, defaultId: 2 },
			{ type: PlexMediaType.TvShow, defaultId: 3 },
			{ type: PlexMediaType.MusicArtist, defaultId: 4 },
			{ type: PlexMediaType.PhotoAlbum, defaultId: 5 },
			{ type: PlexMediaType.OtherVideos, defaultId: 6 },
		];
		const custom = families.map(({ type }, index) => generateFolderPath({ id: 11 + index, type }));
		mock.onGet(FolderPathPaths.getAllFolderPathsEndpoint()).reply(200, generateResultDTO([...custom, ...defaults]));
		const store = useFolderPathStore();

		// Act
		await subscribeSpyTo(store.setup()).onComplete();

		// Assert
		expect(store.getDefaultFolderPaths.map(({ id }) => id)).toEqual([1, 2, 3, 4, 5, 6]);
		for (const [index, family] of families.entries()) {
			expect(store.getFolderPathOptions(family.type).map(({ id }) => id).sort((a, b) => a - b))
				.toEqual([family.defaultId, 11 + index]);
		}
	});

	test('Should keep optional family paths editable without blocking setup and still reject invalid required paths', () => {
		// Arrange
		const store = useFolderPathStore();
		store.folderPaths = generateDefaultFolderPaths().map((path) => ({ ...path, isValid: path.id <= 3 }));

		// Act
		const optionalPaths = store.getDefaultFolderPaths.filter(({ id }) => id >= 4);

		// Assert
		expect(optionalPaths.map(({ id }) => id)).toEqual([4, 5, 6]);
		expect(optionalPaths.every(({ isValid }) => !isValid)).toBe(true);
		expect(store.areDefaultFolderPathsValid).toBe(true);
		store.folderPaths.find(({ id }) => id === 2)!.isValid = false;
		expect(store.areDefaultFolderPathsValid).toBe(false);
	});
});
