import prettyBytes from 'pretty-bytes';
import { headers, route } from '@fixtures';
import { generateDownloadTask, generatePlexMedia, generateResultDTO, generateSupportedSorts, type MockConfig } from '@mock';
import { PlexLibraryPaths, PlexMediaPaths, SettingsPaths } from '@api/api-paths';
import {
	DownloadStatus, DownloadTaskType, FileSystemEntityType, FolderType, MessageTypes, PlexMediaType, VideoQuality, AudioQuality,
	type DownloadMediaDTO, type DownloadPreviewContainerDTO, type DownloadPreviewDTO, type DownloadProgressDTO,
	type DownloadTaskCreationReportDTO, type FolderPathDTO, type PlexMediaDTO, type PlexMediaStatisticsDTO, type SettingsModelDTO,
	type ServerDownloadProgressDTO,
} from '@dto';

const visibleCy = (selector: string) => cy.getCy(selector).filter(':visible').first();
const queueCy = (selector: string) => cy.get('.download-table-desktop, .download-cards')
	.filter(':visible').find(`[data-cy="${selector}"]`).scrollIntoView().should('be.visible');
const submit = () => cy.getCy('download-confirmation-submit').contains('button', /^Download$/);
const taskId = (id: number) => `00000625-0000-4000-8000-${String(id).padStart(12, '0')}`;
const families = {
	music: { type: PlexMediaType.MusicArtist, folderType: FolderType.MusicFolder },
	'other-videos': { type: PlexMediaType.OtherVideos, folderType: FolderType.OtherVideosFolder },
};
type Family = keyof typeof families;

interface WorkflowState {
	root: PlexMediaDTO;
	settings: SettingsModelDTO;
	libraryId: number;
	serverId: number;
	defaultFolder: FolderPathDTO;
	overrideFolder: FolderPathDTO;
	preview: DownloadPreviewContainerDTO;
	queue: ServerDownloadProgressDTO;
	createdDownloads: DownloadProgressDTO[];
	previewFailures: number;
	holdNextPreview: boolean;
	releasePreview?: () => void;
	createCount: number;
}

function createRoot(family: Family, config: MockConfig, libraryId: number, serverId: number): PlexMediaDTO {
	const media = (id: number, type: PlexMediaType, title: string, parentId?: number) => generatePlexMedia({
		config,
		partialData: {
			id, type, title, searchTitle: title.toLowerCase(), parentId, plexLibraryId: libraryId, plexServerId: serverId,
			hasThumb: false, hasArt: false, children: [], mediaData: [], qualities: [], childCount: 0, grandChildCount: 0,
			sortIndex: 1, mediaSize: 3072, duration: 6000, summary: 'Original files owned by this library.',
		},
	});
	const file = (id: number, version: number, name: string, clip = false) => ({
		id, plexApiMediaId: version, plexApiPartId: id + 50000, fileName: name,
		audioCodec: clip ? 'aac' : '', videoCodec: clip ? 'h264' : '',
		audioQuality: AudioQuality.None, videoQuality: clip ? VideoQuality.FullHD : VideoQuality.Unknown, duration: clip ? 6000 : 0, size: 1024,
	});
	const root = media(100, families[family].type, family === 'music' ? 'Synth artist' : 'Owned recording');
	if (family === 'music') {
		root.children = [110, 120].map((id, albumIndex) => {
			const album = media(id, PlexMediaType.MusicAlbum, `Synth album ${albumIndex + 1}`, root.id);
			album.children = [1, 2].map((trackIndex) => {
				const track = media(id + trackIndex, PlexMediaType.MusicTrack, `Synth track ${albumIndex + 1}.${trackIndex}`, id);
				track.sortIndex = trackIndex;
				track.discNumber = trackIndex;
				track.trackNumber = 1;
				track.mediaData = [
					{ ...file(track.id * 100, track.id * 10, `track-${track.id}-part-1.flac`), audioQuality: AudioQuality.Unknown, videoQuality: VideoQuality.None, duration: 6000 },
					{ ...file(track.id * 100 + 1, track.id * 10, `track-${track.id}-part-2.flac`), audioQuality: AudioQuality.Unknown, videoQuality: VideoQuality.None, duration: 6000 },
					{ ...file(track.id * 100 + 2, track.id * 10 + 1, `track-${track.id}-alternative.mp3`), audioQuality: AudioQuality.Unknown, videoQuality: VideoQuality.None, duration: 6000 },
				];
				return track;
			});
			album.childCount = album.children.length;
			return album;
		});
		root.grandChildCount = 4;
	} else {
		root.mediaData = [file(10001, 7001, 'recording-part-1.mp4', true), file(10002, 7001, 'recording-part-2.mp4', true), file(10003, 7002, 'recording-alternative.mkv', true)];
		// Both originals have the same resolution: the selector must identify the version, not rank quality.
		root.qualities = root.mediaData.map((part) => ({ mediaId: root.id, dataId: part.id, mediaDataType: root.type, audioQuality: AudioQuality.None, videoQuality: part.videoQuality }));
	}
	root.childCount = root.children.length;
	root.mediaSize = root.children.length ? root.children.reduce((size, child) => size + child.mediaSize, 0) : 3072;
	return root;
}

function previewNode(media: PlexMediaDTO): DownloadPreviewDTO {
	return { key: `preview-${media.id}`, title: media.title, type: media.type, size: media.mediaSize, qualities: [], children: media.children.map(previewNode) };
}

function queueNode(media: PlexMediaDTO): DownloadProgressDTO {
	return { id: taskId(media.id), title: media.title, mediaType: media.type, status: DownloadStatus.Queued,
		percentage: 0, dataReceived: 0, dataTotal: media.mediaSize, downloadSpeed: 0, timeRemaining: 0, children: media.children.map(queueNode) };
}

function selectPreview(state: WorkflowState, media: PlexMediaDTO) {
	const node = previewNode(media);
	const expand = (item: DownloadPreviewDTO): Record<string, boolean> => ({ [item.key]: true, ...Object.assign({}, ...item.children.map(expand)) });
	state.preview = { previews: [node], totalSize: node.size, expanded: expand(node) };
	state.createdDownloads = [queueNode(media)];
}

function setupFamily(family: Family) {
	// Registered first so an unhandled API call fails instead of reaching a backend or Plex.
	cy.intercept('**/api/**', (request) => {
		throw new Error(`Unmocked API request: ${request.method} ${request.url}`);
	});
	return cy.basePageSetup({
		seed: 625, plexServerCount: 1, plexMovieLibraryCount: 1, plexTvShowLibraryCount: 0,
		movieCount: 0, tvShowCount: 0, movieDownloadTask: 0, tvShowDownloadTask: 0, firstTimeSetup: false,
		override: { plexLibraries: (libraries) => libraries.map((library) => ({ ...library, id: 45, name: `${family} library`, count: 1 })) },
	}).then(({ config, plexLibraries, settings }) => {
		cy.intercept('GET', SettingsPaths.getUserSettingsEndpoint(), (request) => request.reply(reply(settings)));
		const library = { ...plexLibraries[0]!, type: families[family].type };
		const root = createRoot(family, config, library.id, library.plexServerId);
		const folder = (id: number, name: string, isDefault: boolean): FolderPathDTO => ({
			id, displayName: name, directory: `/owned/${family}/${name.toLowerCase()}`, mediaType: families[family].type,
			folderType: families[family].folderType, isDefault, isValid: true,
		});
		const state: WorkflowState = {
			root, settings, libraryId: library.id, serverId: library.plexServerId,
			defaultFolder: folder(81, 'Default', true), overrideFolder: folder(82, 'Archive', false),
			preview: { previews: [], totalSize: 0, expanded: {} }, queue: { id: library.plexServerId, downloadableTasksCount: 0, downloads: [] },
			createdDownloads: [], previewFailures: 0, holdNextPreview: false, createCount: 0,
		};
		selectPreview(state, root);
		const reply = (value: unknown) => ({ statusCode: 200, body: generateResultDTO(value), ...headers });
		cy.intercept('GET', '**/api/Integration', reply([]));
		cy.intercept('GET', PlexLibraryPaths.getAllPlexLibrariesEndpoint(), reply([library]));
		cy.intercept('GET', PlexLibraryPaths.getPlexLibraryByIdEndpoint(library.id), reply(library));
		cy.intercept('GET', '**/api/FolderPath', reply([
			state.overrideFolder,
			{ ...state.defaultFolder, id: 83, mediaType: PlexMediaType.Movie, folderType: FolderType.MovieFolder, directory: '/wrong/movie/default' },
			state.defaultFolder,
		]));
		cy.intercept('GET', PlexLibraryPaths.getLibraryMediaMetadata(library.id, { mediaType: library.type }), reply({ countries: [], countryCount: 0, genres: [], genreCount: 0, roles: [], roleCount: 0, qualities: [], qualityCount: 0, mediaCount: 1, supportedSorts: generateSupportedSorts(library.type) }));
		cy.intercept('GET', PlexLibraryPaths.getMetadataFilter(library.id, { mediaType: library.type }), reply({ countries: [], genres: [], roles: [], qualities: [] }));
		cy.intercept({ method: 'GET', pathname: '/api/PlexMedia' }, (request) => {
			expect(request.query.mediaType).to.equal(library.type);
			const stats: PlexMediaStatisticsDTO = {
				countries: [], genres: [], roles: [], qualities: [], mediaList: [root], mediaCount: 1, totalCount: 1,
				mediaSize: root.mediaSize, totalMediaSize: root.mediaSize, page: 1, pageSize: 100, queryHash: family,
				navigationIndexes: [], movieCount: 0, tvShowCount: 0, seasonCount: 0, episodeCount: 0,
				totalMovieCount: 0, totalTvShowCount: 0, totalSeasonCount: 0, totalEpisodeCount: 0,
			};
			request.reply(reply(stats));
		}).as('familyBrowse');
		cy.intercept('GET', PlexMediaPaths.getMediaDetailByIdEndpoint(root.id, { type: root.type }), reply(root)).as('familyDetail');
		cy.intercept({ method: 'GET', pathname: '/api/Download' }, (request) => request.reply(reply([state.queue]))).as('queue');
		cy.intercept('POST', '**/api/Download/preview', (request) => {
			if (request.body.downloadMedias[0]?.type === PlexMediaType.PhotoImage) {
				request.alias = 'imagePreview';
				state.releasePreview?.();
			}
			if (state.previewFailures > 0) {
				state.previewFailures--;
				request.reply({ statusCode: 500, body: { ...generateResultDTO(null), isSuccess: false, statusCode: 500, errors: [{ message: 'Preview unavailable', reasons: [] }] }, ...headers });
				return;
			}
			const response = reply(Cypress._.cloneDeep(state.preview));
			if (state.holdNextPreview) {
				state.holdNextPreview = false;
				return new Promise<void>((resolve) => {
					state.releasePreview = () => {
						state.releasePreview = undefined;
						request.reply(response);
						resolve();
					};
				});
			}
			request.reply(response);
		}).as('preview');
		cy.intercept('POST', '**/api/Download/create', (request) => {
			state.createCount++;
			state.queue.downloads = Cypress._.cloneDeep(state.createdDownloads);
			state.queue.downloadableTasksCount = state.createdDownloads.length;
			const report: DownloadTaskCreationReportDTO = {
				movies: 0, tvShows: 0, seasons: 0, episodes: 0, musicArtists: 0, musicAlbums: 0, musicTracks: 0,
				photoFolders: 0, photoImages: 0, otherVideos: 0, total: state.createdDownloads.length,
			};
			const counts = { [PlexMediaType.PhotoAlbum]: 'photoFolders', [PlexMediaType.PhotoImage]: 'photoImages',
				[PlexMediaType.MusicArtist]: 'musicArtists', [PlexMediaType.MusicAlbum]: 'musicAlbums', [PlexMediaType.MusicTrack]: 'musicTracks', [PlexMediaType.OtherVideos]: 'otherVideos' } as const;
			for (const item of state.createdDownloads)
				report[counts[item.mediaType as keyof typeof counts]]++;
			request.reply(reply(report));
		}).as('create');
		return cy.wrap(state);
	});
}

function visitDetail(family: Family, state: WorkflowState) {
	cy.visit(route(`/${family}/${state.libraryId}/details/${state.root.id}`));
	cy.getPageData();
	cy.get('.media-title').should('be.visible').and('have.text', state.root.title);
}

function openConfirmation(phone = false, waitForVisible = true) {
	if (phone) {
		visibleCy('media-overview-bar-mobile-menu').click();
		visibleCy('media-overview-bar-mobile-download-button').click();
	} else {
		visibleCy('media-overview-bar-download-button').click();
	}
	cy.getCy('download-confirmation-dialog').should(waitForVisible ? 'be.visible' : 'exist');
}

function assertCommand(state: WorkflowState, type: PlexMediaType, id: number, qualities: DownloadMediaDTO['qualities'] = []) {
	return { mediaIds: [id], type, plexLibraryId: state.libraryId, plexServerId: state.serverId, qualities, selectionKind: null, keepCompletedInDownloadFolder: false };
}

function selectMediaTableRow(containerCy: string, title: string) {
	cy.getCy(containerCy).then(($container) => {
		const rows = $container.find('[data-cy="media-q-table-mobile-card"], tbody tr');
		const matches = rows.filter((_, row) => row.textContent?.includes(title) ?? false);
		expect(matches, `media table row ${title}`).to.have.length(1);
		cy.wrap(matches).find('[role="checkbox"]').click();
	});
}

function assertPreview(state: WorkflowState, command: DownloadMediaDTO | DownloadMediaDTO[], media: PlexMediaDTO, noQuality = true) {
	cy.wait('@preview').its('request.body').should('deep.equal', {
		downloadMedias: Array.isArray(command) ? command : [command],
		destinationFolderPathId: state.defaultFolder.id,
		customDestinationFolderPath: '',
	});
	cy.getCy('download-confirmation-dialog').within(() => {
		cy.getCy(`column-title-preview-${media.id}`).should('be.visible').and('have.text', media.title);
		cy.getCy(`column-dataTotal-preview-${media.id}`).should('be.visible').and('have.text', prettyBytes(media.mediaSize, { locale: 'en-US' }));
		if (noQuality) {
			cy.get('[data-cy^="column-qualities-"]').should('not.exist');
			cy.contains('th', /^Quality$/).should('not.exist');
		}
		cy.get('input, [role="checkbox"], [role="radio"]').should('not.exist');
	});
	submit().should('not.be.disabled');
}

function chooseDestination(state: WorkflowState) {
	cy.getCy('download-confirmation-submit').find('button[aria-haspopup]').should('be.visible').and(($button) => {
		if ($button[0]!.ownerDocument.defaultView!.innerWidth <= 599) {
			const rect = $button[0]!.getBoundingClientRect();
			expect(rect.width, 'Destination expander touch width').to.be.at.least(44);
			expect(rect.height, 'Destination expander touch height').to.be.at.least(44);
		}
	}).click();
	cy.getCy(`download-confirmation-destination-${state.overrideFolder.id}`).should('be.visible').click();
	cy.get('body').type('{esc}');
	cy.getCy('download-confirmation-dialog').should('contain.text', state.overrideFolder.directory).and('not.contain.text', '/wrong/movie/default');
}

function createAndOpenQueue(state: WorkflowState, command: DownloadMediaDTO | DownloadMediaDTO[], media: PlexMediaDTO, destinationId = state.defaultFolder.id, customPath = '') {
	submit().should('not.be.disabled').click();
	cy.wait('@create').its('request.body').should('deep.equal', { downloadMedias: Array.isArray(command) ? command : [command], destinationFolderPathId: destinationId, customDestinationFolderPath: customPath });
	cy.getCy('download-confirmation-dialog').should('not.exist');
	cy.wait('@queue');
	cy.visit(route('/downloads'));
	cy.getPageData();
	queueCy(`column-title-${taskId(media.id)}`).should('have.text', media.title);
	queueCy(`column-status-${taskId(media.id)}`).should('have.text', 'Queued');
}

function publishStatus(state: WorkflowState, item: DownloadProgressDTO, status: DownloadStatus, percentage = 0) {
	cy.then(() => {
		Object.assign(item, { status, percentage, dataReceived: item.dataTotal * percentage / 100, downloadSpeed: status === DownloadStatus.Downloading ? 1024 : 0 });
	});
	cy.then(() => cy.hubPublish('download', MessageTypes.ServerDownloadProgress, Cypress._.cloneDeep(state.queue)));
	queueCy(`column-status-${item.id}`).should('have.text', status);
}

function installLifecycle(state: WorkflowState, item: DownloadProgressDTO, taskType: DownloadTaskType, config: MockConfig) {
	for (const action of ['pause', 'start', 'stop', 'restart']) {
		cy.intercept('PUT', `**/api/Download/${action}/${item.id}`, { statusCode: 200, body: generateResultDTO(null), ...headers }).as(action);
	}
	cy.intercept('DELETE', '**/api/Download/delete', (request) => {
		expect(request.body).to.deep.equal([item.id]);
		state.queue.downloads = [];
		request.reply({ statusCode: 200, body: generateResultDTO(null), ...headers });
	}).as('delete');
	cy.intercept('DELETE', '**/api/Download/clear/tasks', (request) => {
		expect(request.body).to.deep.equal([item.id]);
		state.queue.downloads = [];
		request.reply({ statusCode: 200, body: generateResultDTO({ count: 1 }), ...headers });
	}).as('clear');
	const detail = generateDownloadTask({ id: item.id, plexLibraryId: state.libraryId, plexServerId: state.serverId, type: taskType, config,
		partial: { ...item, children: [], downloadTaskType: taskType, plexLibraryId: state.libraryId, plexServerId: state.serverId, fullTitle: item.title, fileName: 'owned-original', destinationDirectory: state.defaultFolder.directory } });
	cy.intercept('GET', `**/api/Download/detail/${item.id}*`, { statusCode: 200, body: generateResultDTO(detail), ...headers }).as('taskDetail');
	cy.intercept('GET', `**/api/Download/logs/${item.id}*`, { statusCode: 200, body: generateResultDTO([]), ...headers }).as('taskLogs');
}

function assertTaskDetails(state: WorkflowState, item: DownloadProgressDTO, taskType: DownloadTaskType) {
	queueCy(`column-actions-details-${item.id}`).should('have.attr', 'aria-label', `Details: ${item.title}`).click();
	cy.wait('@taskDetail').its('request.url').should('include', `/api/Download/detail/${item.id}`);
	cy.wait('@taskLogs').then(({ request }) => {
		const query = new URL(request.url).searchParams;
		expect(query.get('type')).to.equal(taskType);
		expect(query.get('plexLibraryId')).to.equal(String(state.libraryId));
		expect(query.get('plexServerId')).to.equal(String(state.serverId));
	});
	cy.getCy('download-details-dialog-title').should('have.text', item.title);
	visibleCy('dialog-close-button').click();
}

function assertAction(item: DownloadProgressDTO, action: string) {
	queueCy(`column-actions-${action}-${item.id}`).should('not.be.disabled').click();
	cy.wait(`@${action}`).its('request.url').should('include', `/api/Download/${action}/${item.id}`);
}

for (const scope of ['artist', 'album', 'track'] as const) {
	it(`Music ${scope} uses current table rows and ancestor precedence with the matching family destination`, () => {
		cy.viewport(scope === 'track' ? 320 : 1280, scope === 'track' ? 568 : 800);
		setupFamily('music').then((state) => {
			const album = state.root.children[0]!;
			const track = album.children[0]!;
			const target = scope === 'artist' ? state.root : scope === 'album' ? album : track;
			selectPreview(state, target);
			visitDetail('music', state);
			cy.getCy(`music-album-${album.id}`).find('.q-expansion-item__container > .q-item').first().click();
			for (const leaf of album.children)
				cy.getCy(`music-album-${album.id}`).should('contain.text', leaf.title);
			selectMediaTableRow(`music-album-${album.id}`, track.title);
			if (scope !== 'track') cy.getCy(`music-album-checkbox-${album.id}`).click();
			if (scope === 'artist') cy.getCy('music-artist-checkbox').click();
			cy.getCy(`music-album-${album.id}`).find('.q-expansion-item__container > .q-item').first().click();
			cy.getCy(`music-album-${album.id}`).find('[data-cy="media-q-table-mobile-card"], tbody tr').filter(`:contains("${track.title}")`).should('not.be.visible');
			cy.getCy(`music-album-${album.id}`).find('.q-expansion-item__container > .q-item').first().click();
			cy.getCy(`music-album-${album.id}`).find('[data-cy="media-q-table-mobile-card"], tbody tr').filter(`:contains("${track.title}")`).find('[role="checkbox"]').should('have.attr', 'aria-checked', 'true');
			openConfirmation(scope === 'track');
			const command = assertCommand(state, target.type, target.id);
			assertPreview(state, command, target);
			cy.getCy('download-confirmation-dialog').should('contain.text', state.defaultFolder.directory);
			if (scope === 'album') chooseDestination(state);
			createAndOpenQueue(state, command, target, scope === 'album' ? state.overrideFolder.id : state.defaultFolder.id);
			cy.get('@pageData').then((data) => {
				const { config } = data as unknown as { config: MockConfig };
				const item = state.queue.downloads[0]!;
				const type = scope === 'artist' ? DownloadTaskType.MusicArtist : scope === 'album' ? DownloadTaskType.MusicAlbum : DownloadTaskType.MusicTrack;
				installLifecycle(state, item, type, config);
				assertTaskDetails(state, item, type);
				assertAction(item, 'start');
				publishStatus(state, item, DownloadStatus.Downloading, 50);
				assertAction(item, 'pause');
				publishStatus(state, item, DownloadStatus.Paused, 50);
			});
		});
	});
}

it('Music mixed selections require confirmation when the track preference is enabled', () => {
	cy.viewport(1280, 800);
	setupFamily('music').then((state) => {
		state.settings.confirmationSettings.askDownloadMusicAlbumConfirmation = false;
		state.settings.confirmationSettings.askDownloadMusicTrackConfirmation = true;
		const album = state.root.children[0]!;
		const otherAlbum = state.root.children[1]!;
		const track = otherAlbum.children[0]!;
		selectPreview(state, album);
		state.preview.previews.push(previewNode(track));
		state.preview.totalSize += track.mediaSize;
		state.createdDownloads.push(queueNode(track));
		visitDetail('music', state);

		cy.getCy(`music-album-checkbox-${album.id}`).click();
		cy.getCy(`music-album-${otherAlbum.id}`).find('.q-expansion-item__container > .q-item').first().click();
		selectMediaTableRow(`music-album-${otherAlbum.id}`, track.title);
		openConfirmation();
		const commands = [assertCommand(state, album.type, album.id), assertCommand(state, track.type, track.id)];
		assertPreview(state, commands, album);
		cy.getCy(`column-title-preview-${track.id}`).should('be.visible').and('have.text', track.title);
		cy.then(() => expect(state.createCount).to.equal(0));

		createAndOpenQueue(state, commands, album);
		queueCy(`column-title-${taskId(track.id)}`).should('have.text', track.title);
		queueCy(`column-status-${taskId(track.id)}`).should('have.text', 'Queued');
		cy.reload();
		cy.getPageData();
		queueCy(`column-title-${taskId(album.id)}`).should('have.text', album.title);
		queueCy(`column-title-${taskId(track.id)}`).should('have.text', track.title);
		cy.then(() => expect(state.createCount).to.equal(1));
	});
});

it('Music Back recovers from an invalid library and retains a valid library for an invalid artist', () => {
	setupFamily('music').then((state) => {
		cy.visit(route(`/music/Infinity/details/${state.root.id}`));
		cy.getPageData();
		cy.getCy('music-details-error').should('contain.text', 'Invalid music library or artist.');
		cy.get('@familyDetail.all').should('have.length', 0);
		cy.getCy('music-details-error').contains('button', 'Back').click();
		cy.location('pathname').should('equal', '/music');

		cy.visit(route(`/music/${state.libraryId}/details/not-an-id`));
		cy.getPageData();
		cy.getCy('music-details-error').should('contain.text', 'Invalid music library or artist.');
		cy.getCy('music-details-error').contains('button', 'Back').click();
		cy.location('pathname').should('equal', `/music/${state.libraryId}`);
		cy.contains('.media-poster-card', state.root.title).should('be.visible');
		cy.get('@familyDetail.all').should('have.length', 0);
	});
});

it('Music poster quality labels do not filter downloads and the normal artist action still confirms', () => {
	cy.viewport(320, 568);
	setupFamily('music').then((state) => {
		state.root.qualities = [{
			mediaId: state.root.id, dataId: -1, mediaDataType: PlexMediaType.MusicArtist,
			audioQuality: AudioQuality.FLAC16Bit441KHz, videoQuality: VideoQuality.None,
		}];
		cy.visit(route(`/music/${state.libraryId}`));
		cy.getPageData();
		cy.contains('.media-poster-card', state.root.title).find('.media-poster-quality-bar .q-chip')
			.should('be.visible').and('contain.text', 'FLAC 16-bit 44.1 kHz')
			.and('not.have.class', 'q-chip--clickable').click();
		cy.get('@preview.all').should('have.length', 0);
		cy.get('@create.all').should('have.length', 0);

		cy.getCy('media-poster-menu-trigger').click();
		cy.getCy('media-poster-menu-download').click();
		cy.getCy('download-confirmation-dialog').should('be.visible');
		const command = assertCommand(state, PlexMediaType.MusicArtist, state.root.id);
		assertPreview(state, command, state.root);
		cy.then(() => expect(state.createCount).to.equal(0));
		createAndOpenQueue(state, command, state.root);
	});
});

for (const destination of ['default', 'override', 'custom'] as const) {
	it(`Other Videos downloads selected original parts using the ${destination} family destination and retains its queue lifecycle`, () => {
		cy.viewport(destination === 'custom' ? 320 : 1280, destination === 'custom' ? 568 : 800);
		setupFamily('other-videos').then((state) => {
			visitDetail('other-videos', state);
			const selectedParts = destination === 'default'
				? state.root.mediaData
				: [state.root.mediaData.find((part) => part.id === (destination === 'custom' ? 10003 : 10001))!];
			if (destination === 'default')
				cy.getCy('other-video-root-checkbox').click();
			else
				selectMediaTableRow('other-video-media-list', selectedParts[0]!.fileName);
			if (destination === 'custom') {
				cy.getCy('other-video-media-list').find(`[aria-label="Selection ${selectedParts[0]!.fileName}"]`).should('be.visible').and(($control) => {
					const rect = $control[0]!.getBoundingClientRect();
					expect(rect.width, 'Original touch target width').to.be.at.least(44);
					expect(rect.height, 'Original touch target height').to.be.at.least(44);
				});
			}
			const commands = selectedParts.map((part) => assertCommand(state, PlexMediaType.OtherVideos, state.root.id, [{
				mediaId: state.root.id, dataId: part.id, mediaDataType: PlexMediaType.OtherVideos, audioQuality: AudioQuality.None, videoQuality: part.videoQuality,
			}]));
			openConfirmation(destination === 'custom');
			assertPreview(state, commands, state.root, false);
			cy.getCy('download-confirmation-dialog').should('contain.text', state.defaultFolder.directory);
			if (destination === 'override') chooseDestination(state);
			if (destination === 'custom') {
				const folder = { name: 'Custom originals', path: '/custom/originals', type: FileSystemEntityType.Folder, extension: '', size: 0, hasReadPermission: true, hasWritePermission: true };
				cy.intercept('GET', '**/api/FolderPath/directory*', (request) => {
					const path = String(request.query.path ?? '');
					request.reply({ statusCode: 200, body: generateResultDTO({ current: { ...folder, path }, directories: path === folder.path ? [] : [folder], files: [], parent: '/' }), ...headers });
				}).as('customDirectory');
				cy.getCy('download-confirmation-submit').find('button[aria-haspopup]').click();
				cy.getCy('download-confirmation-destination-custom').click();
				cy.wait('@customDirectory');
				cy.getCy('directory-browser-row-0').should('be.visible').and('contain.text', folder.name).click();
				cy.wait('@customDirectory').then(({ request }) => expect(new URL(request.url).searchParams.get('path')).to.equal(folder.path));
				cy.getCy('directory-browser-confirm-button').should('not.be.disabled').click();
				cy.getCy('download-confirmation-dialog').should('contain.text', folder.path);
			}
			createAndOpenQueue(state, commands, state.root, destination === 'override' ? state.overrideFolder.id : destination === 'custom' ? 0 : state.defaultFolder.id, destination === 'custom' ? '/custom/originals' : '');
			cy.get('@pageData').then((data) => {
				const { config } = data as unknown as { config: MockConfig };
				const item = state.queue.downloads[0]!;
				installLifecycle(state, item, DownloadTaskType.OtherVideo, config);
				assertTaskDetails(state, item, DownloadTaskType.OtherVideo);
				publishStatus(state, item, DownloadStatus.Downloading, 50);
				assertAction(item, 'stop');
				publishStatus(state, item, DownloadStatus.Stopped, 50);
				cy.get('.download-table-desktop, .download-cards').filter(':visible').should(($queue) => {
					expect($queue.find(`[data-cy="column-actions-start-${item.id}"]`)).to.have.length(0);
				});
				assertAction(item, 'restart');
				publishStatus(state, item, DownloadStatus.Downloading, 50);
				assertAction(item, 'stop');
				publishStatus(state, item, DownloadStatus.Stopped, 50);
				visibleCy(`column-actions-delete-${item.id}`).click();
				cy.wait('@delete');
				cy.wait('@queue');
				cy.getCy(`column-title-${item.id}`).should('not.exist');
			});
		});
	});
}

for (const scenario of [
	{ family: 'music', control: 'music-track', field: 'askDownloadMusicTrackConfirmation' },
	{ family: 'other-videos', control: 'other-videos', field: 'askDownloadOtherVideosConfirmation' },
] as const) {
	it(`${scenario.family} skips confirmation only after its own saved preference and keeps canonical default-destination commands`, () => {
		cy.viewport(1280, 800);
		setupFamily(scenario.family).then((state) => {
			const target = scenario.family === 'music'
				? state.root.children[0]!.children[0]!
				: state.root;
			selectPreview(state, target);
			cy.visit(route('/settings/ui'));
			cy.getPageData();
			cy.getCy(`ask-download-${scenario.control}-confirmation`).scrollIntoView().click();
			cy.awaitSettingsUpdate().then(({ request }) => {
				expect(request.body.confirmationSettings[scenario.field]).to.equal(false);
				expect(request.body.confirmationSettings.askDownloadMovieConfirmation).to.equal(true);
				expect(request.body.confirmationSettings.askDownloadTvShowConfirmation).to.equal(true);
			});
			cy.reload();
			cy.getPageData();
			cy.getCy(`ask-download-${scenario.control}-confirmation`).should('have.attr', 'aria-checked', 'false');
			visitDetail(scenario.family, state);
			const commands = scenario.family === 'other-videos'
				? state.root.mediaData.map((part) => assertCommand(state, PlexMediaType.OtherVideos, state.root.id, [{
						mediaId: state.root.id, dataId: part.id, mediaDataType: PlexMediaType.OtherVideos, audioQuality: AudioQuality.None, videoQuality: part.videoQuality,
					}]))
				: [assertCommand(state, target.type, target.id)];
			if (scenario.family === 'music') {
				const album = state.root.children[0]!;
				cy.getCy(`music-album-${album.id}`).find('.q-expansion-item__container > .q-item').first().click();
				selectMediaTableRow(`music-album-${album.id}`, target.title);
			} else {
				cy.getCy('other-video-root-checkbox').click();
			}
			visibleCy('media-overview-bar-download-button').click();
			cy.wait('@create').its('request.body').should('deep.equal', {
				downloadMedias: commands,
				destinationFolderPathId: null,
				customDestinationFolderPath: '',
			});
			cy.getCy('download-confirmation-dialog').should('not.exist');
			cy.get('@preview.all').should('have.length', 0);
			cy.visit(route('/downloads'));
			cy.getPageData();
			queueCy(`column-title-${taskId(target.id)}`).should('have.text', target.title);
		});
	});
}
