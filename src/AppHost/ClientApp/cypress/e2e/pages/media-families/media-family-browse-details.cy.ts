import { headers, route } from '@fixtures';
import { generatePlexMedia, generateResultDTO, type MockConfig } from '@mock';
import { PlexLibraryPaths, PlexMediaPaths } from '@api/api-paths';
import { MediaMetaDataTypes, MediaSortField } from '@enums';
import { PlexMediaType, VideoQuality, type PlexMediaDTO, type PlexMediaMetadataDTO, type PlexMediaStatisticsDTO } from '@dto';

const families = [
	{ type: PlexMediaType.MusicArtist, path: 'music', name: 'Music' },
	{ type: PlexMediaType.PhotoAlbum, path: 'photos', name: 'Photos' },
	{ type: PlexMediaType.OtherVideos, path: 'other-videos', name: 'Other Videos' },
];

function createRoots(type: PlexMediaType, config: MockConfig, libraryId: number, serverId: number): PlexMediaDTO[] {
	const create = (id: number, mediaType: PlexMediaType, title: string, parentId?: number) => generatePlexMedia({
		config,
		partialData: {
			id, type: mediaType, title, searchTitle: title.toLowerCase(), parentId,
			plexLibraryId: libraryId, plexServerId: serverId, hasThumb: false, hasArt: false,
			children: [], mediaData: [], qualities: [], sortIndex: id,
			childCount: 0, grandChildCount: 0, mediaSize: 1024, duration: 6000,
			addedAt: '2024-01-02T00:00:00Z', originallyAvailableAt: '1960-03-04T00:00:00Z',
			updatedAt: '0001-01-01T00:00:00Z', summary: 'Owned media with original file metadata.',
		},
	});
	const roots = [create(100, type, 'Synth collection'), create(200, type, 'Acoustic collection')];
	roots[0]!.year = 1960;
	roots[1]!.year = 1990;
	for (const root of roots) {
		if (type === PlexMediaType.MusicArtist) {
			root.children = [0, 1].map((albumIndex) => {
				const album = create(root.id + 10 + albumIndex * 10, PlexMediaType.MusicAlbum, `Album ${albumIndex + 1}`, root.id);
				album.children = Array.from({ length: albumIndex === 0 ? 2 : 1 }, (_, trackIndex) => {
					const track = create(album.id + trackIndex + 1, PlexMediaType.MusicTrack, `Track ${trackIndex + 1}`, album.id);
					track.mediaData = [{ id: track.id, plexApiMediaId: track.id, plexApiPartId: track.id, fileName: `original-${track.id}.flac`, audioCodec: 'flac', videoCodec: '', videoResolution: VideoQuality.Unknown, duration: 6000, size: 1024 }];
					return track;
				});
				album.childCount = album.children.length;
				album.mediaSize = album.children.reduce((size, track) => size + track.mediaSize, 0);
				return album;
			});
			root.childCount = root.children.length;
			root.grandChildCount = root.children.reduce((count, album) => count + album.childCount, 0);
		} else if (type === PlexMediaType.PhotoAlbum) {
			root.children = Array.from({ length: 20 }, (_, index) => {
				const asset = create(root.id + 10 + index, PlexMediaType.PhotoImage, `Holiday asset ${index + 1}`, root.id);
				const isClip = index === 19;
				asset.mediaData = [{ id: asset.id, plexApiMediaId: asset.id, plexApiPartId: asset.id, fileName: isClip ? `${'holiday-original-recording-'.repeat(8)}.mp4` : `holiday-${index + 1}.jpg`, audioCodec: isClip ? 'aac' : '', videoCodec: isClip ? 'h264' : '', videoResolution: VideoQuality.Unknown, duration: isClip ? 6000 : 0, size: 1024 }];
				return asset;
			});
			root.childCount = root.children.length;
		} else {
			const resolution = root.id === 100 ? VideoQuality.FullHD : VideoQuality.HD;
			root.mediaData = [
				...Array.from({ length: 18 }, (_, index) => ({ id: root.id * 100 + index, plexApiMediaId: root.id, plexApiPartId: root.id * 100 + index, fileName: `multipart-original-part-${index + 1}.mp4`, audioCodec: 'aac', videoCodec: 'h264', videoResolution: resolution, duration: 6000, size: 1024 })),
				{ id: root.id * 100 + 20, plexApiMediaId: root.id + 1, plexApiPartId: root.id * 100 + 20, fileName: `${'alternative-original-'.repeat(8)}.mkv`, audioCodec: 'flac', videoCodec: 'hevc', videoResolution: resolution, duration: 12000, size: 2048 },
			];
			root.qualities = [{ dataId: root.mediaData[0]!.id, mediaId: root.id, mediaDataType: type, quality: resolution }];
		}
	}
	return roots;
}

function setupFamily(family: (typeof families)[number]) {
	return cy.basePageSetup({
		seed: 625, plexServerCount: 1, plexMovieLibraryCount: 1, plexTvShowLibraryCount: 0,
		movieCount: 0, tvShowCount: 0, firstTimeSetup: false,
		override: {
			plexLibraries: (libraries) => libraries.map((library) => ({ ...library, id: 45, name: `${family.name} library`, count: 2, mediaSize: 2048 })),
		},
	}).then(({ config, plexLibraries }) => {
		const library = { ...plexLibraries[0]!, type: family.type };
		cy.intercept('GET', PlexLibraryPaths.getAllPlexLibrariesEndpoint(), { statusCode: 200, body: generateResultDTO([library]), ...headers });
		cy.intercept('GET', PlexLibraryPaths.getPlexLibraryByIdEndpoint(library.id), { statusCode: 200, body: generateResultDTO(library), ...headers });
		const roots = createRoots(family.type, config, library.id, library.plexServerId);
		const qualities = family.type === PlexMediaType.OtherVideos
			? [{ id: 1080, count: 1, name: '1080p', quality: VideoQuality.FullHD }, { id: 720, count: 1, name: '720p', quality: VideoQuality.HD }]
			: [];
		const metadata: PlexMediaMetadataDTO = {
			countries: [], countryCount: 0, genres: [{ id: 7, name: 'Jazz' }, { id: 8, name: 'Acoustic' }], genreCount: 2,
			roles: [], roleCount: 0, qualities, qualityCount: qualities.length, mediaCount: roots.length,
		};
		cy.intercept('GET', PlexLibraryPaths.getLibraryMediaMetadata(library.id, { mediaType: family.type }), { statusCode: 200, body: generateResultDTO(metadata), ...headers });
		cy.intercept('GET', PlexLibraryPaths.getMetadataFilter(library.id, { mediaType: family.type }), { statusCode: 200, body: generateResultDTO({ countries: [], genres: [7, 8], roles: [], qualities: qualities.map((quality) => quality.id) }), ...headers });
		cy.intercept('GET', '**/api/PlexMedia*', (request) => {
			let rows = roots.filter((root) => Number(request.query.plexLibraryId ?? library.id) === library.id && String(request.query.mediaType ?? family.type) === root.type);
			const query = String(request.query.q ?? '').toLowerCase();
			const sort = String(request.query.sort ?? 'Title:Asc');
			if (family.type !== PlexMediaType.OtherVideos && (request.query.qualityId || sort.toLowerCase().startsWith('quality:'))) rows = [];
			if (request.query.comparisonState) rows = [];
			if (request.query.genreId) rows = rows.filter((root) => root.id === (Number(request.query.genreId) === 7 ? 100 : 200));
			if (request.query.qualityId) rows = rows.filter((root) => root.id === (Number(request.query.qualityId) === 1080 ? 100 : 200));
			rows = rows.filter((root) => root.title.toLowerCase().includes(query));
			rows.sort((left, right) => {
				const comparison = sort.toLowerCase().startsWith('year:') ? left.year - right.year : left.title.localeCompare(right.title);
				return sort.toLowerCase().endsWith(':desc') ? -comparison : comparison;
			});
			const mediaSize = rows.reduce((size, root) => size + root.mediaSize, 0);
			const response: PlexMediaStatisticsDTO = {
				countries: [], genres: [7, 8], roles: [], qualities: qualities.map((quality) => quality.id),
				mediaList: rows.map((root, index) => ({ ...root, sortIndex: index + 1 })), mediaCount: rows.length, totalCount: rows.length,
				mediaSize, totalMediaSize: mediaSize, page: 1, pageSize: 100, queryHash: `${query}-${sort}-${request.query.genreId}-${request.query.qualityId}`,
				navigationIndexes: [], movieCount: 0, tvShowCount: 0, seasonCount: 0, episodeCount: 0,
				totalMovieCount: 0, totalTvShowCount: 0, totalSeasonCount: 0, totalEpisodeCount: 0,
			};
			request.reply({ statusCode: 200, body: generateResultDTO(response), ...headers });
		}).as('familyBrowse');
		for (const root of roots) {
			cy.intercept('GET', PlexMediaPaths.getMediaDetailByIdEndpoint(root.id, { type: family.type }), { statusCode: 200, body: generateResultDTO(root), ...headers }).as(`familyDetail${root.id}`);
		}
		return cy.wrap({ library, roots });
	});
}

function assertNoHorizontalOverflow() {
	cy.document().should((document) => {
		expect(document.documentElement.scrollWidth, 'page content fits the viewport').to.be.lte(document.documentElement.clientWidth + 1);
	});
}

function assertTouchControl(selector: string) {
	cy.getCy(selector).should(($control) => {
		const bounds = $control[0]!.getBoundingClientRect();
		expect(bounds.width, 'selection touch width').to.be.gte(44);
		expect(bounds.height, 'selection touch height').to.be.gte(44);
		expect($control.attr('aria-label'), 'selection accessible name').not.to.equal('');
		expect($control.attr('aria-label'), 'selection accessible name').not.to.equal(undefined);
	});
}

function scrollLastFileIntoViewport(selector: string) {
	cy.get(selector).then(($last) => {
		let parent = $last[0]!.parentElement;
		while (parent && !(/auto|scroll/.test(getComputedStyle(parent).overflowY) && parent.scrollHeight > parent.clientHeight)) parent = parent.parentElement;
		if (parent) {
			cy.wrap(parent).scrollTo('bottom');
		} else {
			cy.document().then((document) => {
				const scrollingElement = document.scrollingElement!;
				expect(getComputedStyle(scrollingElement).overflowY, 'document is user-scrollable').not.to.match(/hidden|clip/);
				cy.scrollTo('bottom');
			});
		}
	});
	cy.get(selector).should(($last) => {
		const bounds = $last[0]!.getBoundingClientRect();
		expect(bounds.top, 'last original can be reached on a phone').to.be.gte(0);
		expect(bounds.bottom, 'last original fits below the screen edge').to.be.lte($last[0]!.ownerDocument.defaultView!.innerHeight);
	});
}

for (const family of families) {
	describe(`${family.name} independent browse and detail`, () => {
		it('searches and sorts roots and applies only reported, applicable metadata filters', () => {
			cy.viewport(1280, 800);
			setupFamily(family).then(({ library }) => cy.visit(route(`/${family.path}/${library.id}`)));
			cy.getPageData();
			cy.get('.media-poster-card').should('have.length', 2).first().should('contain.text', 'Acoustic collection');
			cy.getCy('media-overview-sort-btn').click();
			cy.getCy(`sort-option-${MediaSortField.Year}-btn`).click();
			cy.get('.media-poster-card').first().should('contain.text', 'Synth collection');
			cy.getCy('media-overview-filter-btn').filter(':visible').click();
			cy.getCy(`media-filter-menu-category-${MediaMetaDataTypes.ComparisonState}`).should('not.exist');
			cy.getCy(`media-filter-menu-category-${MediaMetaDataTypes.Quality}`).should(family.type === PlexMediaType.OtherVideos ? 'be.visible' : 'not.exist');
			cy.getCy(`media-filter-menu-category-${MediaMetaDataTypes.Genres}`).click();
			cy.contains('.media-filter-menu-list .q-item', 'Jazz').click();
			cy.get('body').type('{esc}');
			cy.get('.media-filter-menu-list').should('not.exist');
			cy.get('.media-poster-card').should('have.length', 1).should('contain.text', 'Synth collection');
			cy.get('.media-overview-bar__search .q-chip__icon--remove').filter(':visible').click();
			cy.get('.media-poster-card').should('have.length', 2);
			cy.get('.media-overview-bar__search input').filter(':visible').type('acoustic');
			cy.get('.media-poster-card').should('have.length', 1).should('contain.text', 'Acoustic collection');
			assertNoHorizontalOverflow();
		});

		it('opens the owning detail route and keeps original metadata and selection usable on a narrow phone', () => {
			cy.viewport(320, 568);
			setupFamily(family).then(({ library, roots }) => {
				const root = roots[0]!;
				cy.visit(route(`/${family.path}/${library.id}`));
				cy.getPageData();
				cy.contains('.media-poster-card', root.title).scrollIntoView().should('be.visible');
				cy.contains('.media-poster-card', root.title).find('[data-cy="media-poster-menu-trigger"]').click();
				cy.getCy('media-poster-menu-details').should('be.visible').click({ scrollBehavior: false });
				cy.location('pathname').should('eq', `/${family.path}/${library.id}/details/${root.id}`);
				cy.get('h1').should('have.text', root.title);
				cy.get('dl').should('contain.text', '1960').find('dt').should('not.contain.text', 'Updated');
				if (family.type === PlexMediaType.MusicArtist) {
					assertTouchControl('music-artist-checkbox');
					cy.getCy(`music-album-${root.children[0]!.id}`).find('.q-expansion-item__container > .q-item').first().click();
					const track = root.children[0]!.children[0]!;
					cy.getCy(`music-track-checkbox-${root.children[0]!.id}-${track.id}`).click();
					cy.contains('original-' + track.id + '.flac').should('be.visible');
					cy.getCy('music-artist-checkbox').should('have.attr', 'aria-checked', 'mixed');
				} else if (family.type === PlexMediaType.PhotoAlbum) {
					const asset = root.children.at(-1)!;
					const selector = `[data-cy="photo-asset-${asset.id}"]`;
					scrollLastFileIntoViewport(selector);
					assertTouchControl(`photo-asset-checkbox-${asset.id}`);
					cy.getCy(`photo-asset-checkbox-${asset.id}`).click();
					cy.getCy('photo-selected-count').should('contain.text', '1');
					cy.getCy('photo-album-checkbox').should('have.attr', 'aria-checked', 'mixed');
					cy.get(selector).should('contain.text', '.mp4').and('contain.text', 'h264');
					cy.get(`${selector} [style*="--media-thumbnail-width"]`).should(($image) => {
						const bounds = $image[0]!.getBoundingClientRect();
						expect(bounds.width, 'asset thumbnail width').to.be.closeTo(72, 1);
						expect(bounds.height, 'asset thumbnail height').to.be.closeTo(72, 1);
					});
				} else {
					scrollLastFileIntoViewport('.other-video-files__item:last-child');
					cy.get('.other-video-files').should('contain.text', 'multipart-original-part-18.mp4').and('contain.text', 'hevc').and('contain.text', 'flac');
					cy.getCy('other-video-root-checkbox').scrollIntoView();
					assertTouchControl('other-video-root-checkbox');
					cy.getCy('other-video-root-checkbox').click();
					cy.getCy('other-video-selected-count').should('contain.text', '1');
				}
				cy.getCy('media-overview-bar-mobile-menu').click();
				cy.getCy('media-overview-bar-mobile-download-button').should('be.visible');
				assertNoHorizontalOverflow();
			});
		});
	});
}
