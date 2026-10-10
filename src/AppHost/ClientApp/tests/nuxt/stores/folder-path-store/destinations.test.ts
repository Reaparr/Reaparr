import { beforeAll, beforeEach, describe, expect, test } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { generateDefaultFolderPaths, generateFolderPath, generateResultDTO } from '@mock';
import { FolderPathPaths } from '@api/api-paths';
import { FolderType, PlexMediaType } from '@dto';
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
		const additionalDefault = generateFolderPath({
			id: 101,
			type: PlexMediaType.Movie,
			partialData: { isDefault: true },
		});
		mock.onGet(FolderPathPaths.getAllFolderPathsEndpoint()).reply(200, generateResultDTO([...custom, ...defaults, additionalDefault]));
		const store = useFolderPathStore();

		// Act
		await subscribeSpyTo(store.setup()).onComplete();

		// Assert
		expect(store.getDefaultFolderPaths.map(({ id }) => id)).toEqual([1, 2, 3, 4, 5, 6, 101]);
		for (const [index, family] of families.entries()) {
			const expectedIds = [
				family.defaultId,
				11 + index,
				...(family.type === PlexMediaType.Movie ? [101] : []),
			].sort((a, b) => a - b);
			expect(store.getFolderPathOptions(family.type).map(({ id }) => id).sort((a, b) => a - b))
				.toEqual(expectedIds);
		}
	});

	test('Should keep optional family paths editable without blocking setup and still reject invalid required paths', () => {
		// Arrange
		const store = useFolderPathStore();
		store.folderPaths = generateDefaultFolderPaths().map((path) => ({
			...path,
			id: path.folderType === FolderType.MovieFolder ? 102 : path.id,
			isValid: path.id <= 3,
		}));

		// Act
		const optionalPaths = store.getDefaultFolderPaths.filter(({ folderType }) =>
			[FolderType.MusicFolder, FolderType.PhotosFolder, FolderType.OtherVideosFolder].includes(folderType),
		);

		// Assert
		expect(optionalPaths.map(({ id }) => id)).toEqual([4, 5, 6]);
		expect(optionalPaths.every(({ isValid }) => !isValid)).toBe(true);
		expect(store.areDefaultFolderPathsValid).toBe(true);
		store.folderPaths.find(({ folderType }) => folderType === FolderType.MovieFolder)!.isValid = false;
		expect(store.areDefaultFolderPathsValid).toBe(false);
	});
});
