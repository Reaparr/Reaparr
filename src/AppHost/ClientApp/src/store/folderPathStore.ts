import { get } from '@vueuse/core';
import { defineStore, acceptHMRUpdate } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { tap, map } from 'rxjs/operators';
import type { Observable } from 'rxjs';
import { throwError } from 'rxjs';
import { type FolderPathDTO, FolderType, PlexMediaType } from '@dto';
import { StoreNames, type ISetupResult, type IFolderPathGroup } from '@interfaces';
import { folderPathApi } from '@api';
import { useI18n } from 'vue-i18n';
import { cloneDeep, orderBy } from 'lodash-es';

interface IFolderPathStoreState {
	folderPaths: FolderPathDTO[];
}

export const useFolderPathStore = defineStore(StoreNames.FolderPathStore, () => {
	const defaultState: IFolderPathStoreState = {
		folderPaths: [],
	};

	const state = reactive<IFolderPathStoreState>(cloneDeep(defaultState));

	function updateFolderPathInState(folderPath: FolderPathDTO) {
		const i = state.folderPaths.findIndex((x) => x.id === folderPath.id);
		if (i > -1) {
			state.folderPaths.splice(i, 1, folderPath);
		}
	}

	// Actions
	const actions = {
		setup(): Observable<ISetupResult> {
			return actions.refreshFolderPaths().pipe(
				map((result) => ({
					name: StoreNames.FolderPathStore,
					isSuccess: result.isSuccess,
				})),
			);
		},
		refreshFolderPaths() {
			return folderPathApi.getAllFolderPathsEndpoint().pipe(
				tap((result) => {
					if (result.isSuccess && result.value) {
						state.folderPaths = result.value ?? [];
					}
				}),
			);
		},
		createFolderPath(folderPath: FolderPathDTO) {
			return folderPathApi.createFolderPathEndpoint(folderPath).pipe(
				tap((result) => {
					if (result?.isSuccess && result.value) {
						state.folderPaths.push(result.value);
					}
				}),
			);
		},
		setFolderPathDirectory(folderPathId: number, directory: string): Observable<FolderPathDTO> {
			let folderPath = getters.getFolderPath(folderPathId);
			if (folderPath) {
				folderPath = { ...folderPath, directory };
				return actions.updateFolderPath(folderPath);
			}
			return throwError(() => 'Could not find folderPath with id: ' + folderPathId);
		},
		setFolderPathDisplayName(folderPathId: number, displayName: string): Observable<FolderPathDTO> {
			let folderPath = getters.getFolderPath(folderPathId);
			if (folderPath) {
				folderPath = { ...folderPath, displayName };
				return actions.updateFolderPath(folderPath);
			}
			return throwError(() => 'Could not find folderPath with id: ' + folderPathId);
		},
		updateFolderPath(folderPath: FolderPathDTO): Observable<FolderPathDTO> {
			return folderPathApi.updateFolderPathEndpoint(folderPath).pipe(
				tap((x) => {
					if (x.value) {
						updateFolderPathInState(x.value);
					}
				}),
				map((x) => x.value!),
			);
		},
		deleteFolderPath(folderPathId: number) {
			return folderPathApi.deleteFolderPathEndpoint(folderPathId).pipe(
				tap((result) => {
					if (!result.isSuccess) {
						return;
					}

					const i = state.folderPaths.findIndex((x) => x.id === folderPathId);
					if (i > -1) {
						state.folderPaths.splice(i, 1);
					}
				}),
			);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	// Getters
	const getters = {
		getFolderPaths: (): FolderPathDTO[] => state.folderPaths,
		getFolderPath: (id: number): FolderPathDTO | undefined =>
			state.folderPaths.find((x) => x.id === id),
		getFolderPathOptions: (type: PlexMediaType): FolderPathDTO[] => {
			if (type === PlexMediaType.Movie || type === PlexMediaType.TvShow || type === PlexMediaType.MusicArtist || type === PlexMediaType.PhotoAlbum || type === PlexMediaType.OtherVideos) {
				return state.folderPaths.filter((x) => x.mediaType === type);
			}

			return state.folderPaths;
		},
		getDefaultFolderPaths: computed(() =>
			state.folderPaths.filter((x) =>
				x.isDefault
				&& [
					FolderType.DownloadFolder,
					FolderType.MovieFolder,
					FolderType.TvShowFolder,
					FolderType.MusicFolder,
					FolderType.PhotosFolder,
					FolderType.OtherVideosFolder,
				].includes(x.folderType),
			),
		),
		areDefaultFolderPathsValid: computed(() =>
			state.folderPaths
				.filter((x) => x.isDefault && [FolderType.DownloadFolder, FolderType.MovieFolder, FolderType.TvShowFolder].includes(x.folderType))
				.every((x: FolderPathDTO) => x.isValid),
		),
		getFolderPathsGroups: (onlyDefaults: boolean): IFolderPathGroup[] => {
			const { t } = useI18n();
			const defaultFolderPathGroup: IFolderPathGroup = {
				header: t('components.folder-paths-overview.main.header'),
				paths: get(getters.getDefaultFolderPaths),
				mediaType: PlexMediaType.None,
				folderType: FolderType.None,
				isFolderDeletable: false,
				isFolderNameEditable: false,
				isFolderAddable: false,
			};

			if (onlyDefaults) {
				return [defaultFolderPathGroup];
			}

			const folderPathGroupDefinitions: Pick<IFolderPathGroup, 'header' | 'mediaType' | 'folderType'>[] = [
				{
					header: t('components.folder-paths-overview.download.header'),
					mediaType: PlexMediaType.None,
					folderType: FolderType.DownloadFolder,
				},
				{
					header: t('components.folder-paths-overview.movie.header'),
					mediaType: PlexMediaType.Movie,
					folderType: FolderType.MovieFolder,
				},
				{
					header: t('components.folder-paths-overview.tv-show.header'),
					mediaType: PlexMediaType.TvShow,
					folderType: FolderType.TvShowFolder,
				},
				{
					header: t('components.folder-paths-overview.music.header'),
					mediaType: PlexMediaType.MusicArtist,
					folderType: FolderType.MusicFolder,
				},
				{
					header: t('components.folder-paths-overview.photos.header'),
					mediaType: PlexMediaType.PhotoAlbum,
					folderType: FolderType.PhotosFolder,
				},
				{
					header: t('components.folder-paths-overview.other-videos.header'),
					mediaType: PlexMediaType.OtherVideos,
					folderType: FolderType.OtherVideosFolder,
				},
			];

			return folderPathGroupDefinitions.map((definition) => ({
				...definition,
				paths: orderBy(
					state.folderPaths.filter((x) => x.folderType === definition.folderType),
					['isDefault'],
					['desc'],
				),
				isFolderDeletable: true,
				isFolderNameEditable: true,
				isFolderAddable: true,
			}));
		},
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useFolderPathStore, import.meta.hot));
}
