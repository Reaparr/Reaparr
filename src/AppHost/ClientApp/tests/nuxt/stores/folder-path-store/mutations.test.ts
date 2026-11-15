import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';

import { baseSetup, baseVars, getAxiosMock, subscribeSpyTo } from '@services-test-base';
import { FolderPathPaths } from '@api/api-paths';
import { generateDefaultFolderPaths, generateFailedResultDTO, generateFolderPath, generateResultDTO } from '@mock';
import type { FolderPathDTO, BaseResultDTO } from '@dto';
import { PlexMediaType } from '@dto';
import { useFolderPathStore } from '@store';
import { FrontendTelemetryAction, FrontendTelemetryEvent } from '@composables/telemetry/events';

const { capture } = vi.hoisted(() => ({ capture: vi.fn() }));
beforeEach(async () => {
	const { createTelemetry } = await import('@composables/telemetry/createTelemetry');
	Object.assign(useNuxtApp().$telemetry, createTelemetry({ capture }));
});

function createInitialFolderPaths(): FolderPathDTO[] {
	const defaults = generateDefaultFolderPaths({ config: { seed: 4901 } });
	return [
		defaults[0]!,
		generateFolderPath({
			id: 101,
			type: PlexMediaType.Movie,
			config: { seed: 4901 },
			partialData: {
				directory: '/library/movies',
				displayName: 'Library Movies',
			},
		}),
	];
}

function createFailure(statusCode: number, message: string) {
	return generateFailedResultDTO({
		statusCode,
		errors: [{ message, metadata: { source: 'folder-path-test' }, reasons: [] }],
	});
}

describe('FolderPathStore mutations', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		capture.mockClear();
	});

	async function setupStore(folderPaths: FolderPathDTO[]) {
		mock.onGet(FolderPathPaths.getAllFolderPathsEndpoint()).reply(200, generateResultDTO(folderPaths));
		const store = useFolderPathStore();
		await subscribeSpyTo(store.setup()).onComplete();
		return store;
	}

	test('Should preserve a failed create and add only the retried folder path', async () => {
		// Arrange
		const initialFolderPaths = createInitialFolderPaths();
		const store = await setupStore(initialFolderPaths);
		const createdFolderPath = generateFolderPath({
			id: 102,
			type: PlexMediaType.TvShow,
			config: { seed: 4901 },
			partialData: { directory: '/library/tv', displayName: 'Library TV' },
		});
		const candidate = { ...createdFolderPath, id: 0 };
		const failedResult = { ...createFailure(500, 'Folder creation failed.'), value: createdFolderPath };
		mock.onPost(FolderPathPaths.createFolderPathEndpoint()).replyOnce(500, failedResult);

		// Act
		const failed = subscribeSpyTo(store.createFolderPath(candidate));
		await failed.onComplete();

		// Assert
		expect(failed.getFirstValue()).toEqual(failedResult);
		expect(store.getFolderPaths()).toEqual(initialFolderPaths);
		expect(JSON.parse(mock.history.post[0]!.data)).toEqual(candidate);

		// Arrange retry
		const successfulResult = generateResultDTO(createdFolderPath);
		mock.onPost(FolderPathPaths.createFolderPathEndpoint()).replyOnce(200, successfulResult);

		// Act retry
		const retried = subscribeSpyTo(store.createFolderPath(candidate));
		await retried.onComplete();

		// Assert retry
		expect(retried.getFirstValue()).toEqual(successfulResult);
		expect(store.getFolderPaths()).toEqual([...initialFolderPaths, createdFolderPath]);
		expect(capture).toHaveBeenCalledTimes(2);
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendFolderInteraction, {
				action: FrontendTelemetryAction.Create,
				folder_type: candidate.folderType,
				media_type: candidate.mediaType,
			}],
			[FrontendTelemetryEvent.FrontendFolderInteraction, {
				action: FrontendTelemetryAction.Create,
				folder_type: candidate.folderType,
				media_type: candidate.mediaType,
			}],
		]);
		expect(JSON.stringify(capture.mock.calls)).not.toContain('/library/tv');
		expect(JSON.stringify(capture.mock.calls)).not.toContain('Library TV');
	});

	test.each([
		{ field: 'directory', value: '/library/movies-updated', statusCode: 400 },
		{ field: 'displayName', value: 'Updated Library Movies', statusCode: 500 },
	])('Should preserve a failed $field update and apply its successful retry only to the intended row', async ({ field, value, statusCode }) => {
		// Arrange
		const initialFolderPaths = createInitialFolderPaths();
		const store = await setupStore(initialFolderPaths);
		const existing = initialFolderPaths[1]!;
		const updated = field === 'directory'
			? { ...existing, directory: value }
			: { ...existing, displayName: value };
		const failedResult = { ...createFailure(statusCode, 'Folder update failed.'), value: updated };
		mock.onPut(FolderPathPaths.updateFolderPathEndpoint()).replyOnce(statusCode, failedResult);

		// Act
		const failed = subscribeSpyTo(
			field === 'directory'
				? store.setFolderPathDirectory(existing.id, value)
				: store.setFolderPathDisplayName(existing.id, value),
		);
		await failed.onComplete();

		// Assert
		expect(failed.getFirstValue()).toEqual(failedResult);
		expect(store.getFolderPaths()).toEqual(initialFolderPaths);
		expect(JSON.parse(mock.history.put[0]!.data)).toEqual(updated);

		// Arrange retry
		const successfulResult = generateResultDTO(updated);
		mock.onPut(FolderPathPaths.updateFolderPathEndpoint()).replyOnce(200, successfulResult);

		// Act retry
		const retried = subscribeSpyTo(
			field === 'directory'
				? store.setFolderPathDirectory(existing.id, value)
				: store.setFolderPathDisplayName(existing.id, value),
		);
		await retried.onComplete();

		// Assert retry
		expect(retried.getFirstValue()).toEqual(successfulResult);
		expect(store.getFolderPaths()).toEqual([initialFolderPaths[0], updated]);
		const expectedAction = field === 'directory' ? FrontendTelemetryAction.Confirm : FrontendTelemetryAction.Update;
		expect(capture.mock.calls).toEqual([
			[FrontendTelemetryEvent.FrontendFolderInteraction, {
				action: expectedAction,
				folder_path_id: existing.id,
			}],
			[FrontendTelemetryEvent.FrontendFolderInteraction, {
				action: expectedAction,
				folder_path_id: existing.id,
			}],
		]);
	});

	test('Should preserve a failed delete and remove only the retried folder path', async () => {
		// Arrange
		const initialFolderPaths = createInitialFolderPaths();
		const store = await setupStore(initialFolderPaths);
		const deletedFolderPath = initialFolderPaths[1]!;
		const failedResult = createFailure(500, 'Folder deletion failed.');
		mock.onDelete(FolderPathPaths.deleteFolderPathEndpoint(deletedFolderPath.id)).replyOnce(500, failedResult);

		// Act
		const failed = subscribeSpyTo(store.deleteFolderPath(deletedFolderPath.id));
		await failed.onComplete();

		// Assert
		expect(failed.getFirstValue()).toEqual(failedResult);
		expect(store.getFolderPaths()).toEqual(initialFolderPaths);

		// Arrange retry: the actual endpoint returns HTTP 200 with a base result.
		const successfulResult: BaseResultDTO = {
			isSuccess: true,
			statusCode: 200,
			errors: [],
			successes: [],
		};
		mock.onDelete(FolderPathPaths.deleteFolderPathEndpoint(deletedFolderPath.id)).replyOnce(200, successfulResult);

		// Act retry
		const retried = subscribeSpyTo(store.deleteFolderPath(deletedFolderPath.id));
		await retried.onComplete();

		// Assert retry
		expect(retried.getFirstValue()).toEqual(successfulResult);
		expect(capture).toHaveBeenCalledTimes(2);
		expect(capture).toHaveBeenNthCalledWith(1, FrontendTelemetryEvent.FrontendFolderInteraction, {
			action: FrontendTelemetryAction.Delete,
			folder_path_id: deletedFolderPath.id,
		});
		expect(capture).toHaveBeenNthCalledWith(2, FrontendTelemetryEvent.FrontendFolderInteraction, {
			action: FrontendTelemetryAction.Delete,
			folder_path_id: deletedFolderPath.id,
		});
		expect(store.getFolderPaths()).toEqual([initialFolderPaths[0]]);
	});
});
