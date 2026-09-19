import { route } from '@fixtures';
import { PlexMediaType, VideoQuality } from '@dto';

const viewports = [
	{ name: 'small phone portrait', width: 320, height: 568 },
	{ name: 'phone portrait', width: 375, height: 812 },
	{ name: 'phone landscape', width: 568, height: 320 },
	{ name: 'small tablet', width: 768, height: 1024 },
	{ name: 'desktop', width: 1280, height: 800 },
	{ name: 'wide desktop', width: 1920, height: 1080 },
] as const;

function assertNoPageOverflow(): void {
	cy.document().should((document) => {
		const root = document.documentElement;
		expect(root.scrollWidth, 'document width').to.be.lte(root.clientWidth + 1);

		const overflowing = Array.from(document.body.querySelectorAll<HTMLElement>('[data-cy], .q-dialog, .q-drawer, .q-page')).filter((element) => {
			const style = getComputedStyle(element);
			if (style.display === 'none' || style.visibility === 'hidden') return false;
			const rect = element.getBoundingClientRect();
			if (rect.width <= 0 || rect.bottom <= 0 || rect.top >= root.clientHeight) return false;
			return rect.left < -1 || rect.right > root.clientWidth + 1;
		});
		expect(overflowing.map((element) => element.outerHTML.slice(0, 120)), 'overflowing visible elements').to.deep.equal([]);
	});
}

function assertNavigationDrawerBackground(isMobile: boolean): void {
	cy.get('.navigation-drawer')
		.should('exist')
		.then(($drawer) => {
			const drawer = $drawer[0];
			if (!drawer) throw new Error('Navigation drawer not found');

			const backgroundColor = getComputedStyle(drawer).backgroundColor;
			const isTransparent = backgroundColor === 'rgba(0, 0, 0, 0)' || backgroundColor === 'transparent';
			expect(isTransparent, 'navigation drawer background').to.equal(!isMobile);
		});
}

function assertDesktopNavigationBorder(): void {
	cy.get('.navigation-drawer')
		.should('be.visible')
		.then(($drawer) => {
			const drawer = $drawer[0];
			if (!drawer) throw new Error('Navigation drawer not found');

			expect(parseFloat(getComputedStyle(drawer).borderRightWidth), 'desktop navigation border').to.be.greaterThan(0);
		});
}

function assertPosterRowsDoNotOverlap(): void {
	cy.get('.media-poster-card:visible')
		.should('have.length.at.least', 1)
		.each(($card) => {
			const cardRect = $card[0]!.getBoundingClientRect();
			const quality = $card[0]!.querySelector<HTMLElement>('.media-poster-quality-bar');
			const qualityRect = quality?.getBoundingClientRect();
			if (qualityRect && qualityRect.bottom > cardRect.bottom + 1) {
				throw new Error(`Quality bar exceeds poster card: ${qualityRect.bottom} > ${cardRect.bottom}`);
			}
		});

	cy.get('.poster-table-item')
		.should('have.length.at.least', 2)
		.should(($rows) => {
			const rows = Array.from($rows, (row) => row.getBoundingClientRect())
				.sort((first, second) => first.top - second.top);
			for (let index = 1; index < rows.length; index++) {
				const previous = rows[index - 1];
				const current = rows[index];
				if (previous && current && current.top < previous.bottom - 1) {
					throw new Error(`Poster rows overlap: ${previous.bottom} > ${current.top}`);
				}
			}
		});
}

function assertTouchTargets(selector: string, label: string): void {
	cy.get(selector)
		.filter(':visible')
		.should('have.length.at.least', 1)
		.each(($target) => {
			const rect = $target[0]!.getBoundingClientRect();
			expect(rect.width, `${label} width`).to.be.gte(44);
			expect(rect.height, `${label} height`).to.be.gte(44);
		});
}

function waitForPageLoad(): void {
	cy.getCy('page-load-completed', { timeout: 20000 }).should('be.visible');
}

describe('Responsive layout', () => {
	for (const viewport of viewports) {
		it(`keeps the shell and media overview usable at ${viewport.name}`, () => {
			cy.viewport(viewport.width, viewport.height);
			cy.basePageSetup({
				plexAccountCount: 1,
				plexServerCount: 1,
				plexMovieLibraryCount: 1,
				movieCount: 100,
				isLoggedIn: true,
			}).then(({ plexLibraries, mediaData }) => {
				const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
				if (!library) throw new Error('Movie library not found');
				if (viewport.width === 320) {
					const mediaItem = mediaData.find((item) => item.libraryId === library.id)?.media[0];
					if (!mediaItem) throw new Error('Movie media not found');
					mediaItem.qualities = [
						{
							mediaDataType: mediaItem.type,
							quality: VideoQuality.FullHD,
							mediaId: mediaItem.id,
							dataId: mediaItem.id,
						},
						{
							mediaDataType: mediaItem.type,
							quality: VideoQuality.HD,
							mediaId: mediaItem.id,
							dataId: mediaItem.id + 1,
						},
					];
				}

				cy.visit(route(`/movies/${library.id}`));
				waitForPageLoad();
				assertNavigationDrawerBackground(viewport.width <= 1023);
				assertPosterRowsDoNotOverlap();
				if (viewport.width <= 1023) {
					cy.get('.media-overview-bar-header__details').should('not.be.visible');
					cy.get('.media-overview-bar__search').should('not.be.visible');
					cy.get('.media-overview-bar__actions').should('not.be.visible');
					cy.get('.media-overview-bar__mobile-search').should('be.visible');
					assertTouchTargets('.app-bar__toolbar .q-btn', 'mobile app bar action');
					assertTouchTargets('.media-overview-bar-header__mobile-trigger', 'media overview header action');
					assertTouchTargets('.media-overview-bar__mobile-menu-trigger', 'media overview menu action');
					cy.get('.media-overview-bar-header__mobile-trigger').then(($trigger) => {
						const triggerRect = $trigger[0]!.getBoundingClientRect();
						const iconRect = $trigger[0]!.querySelector('.q-icon')!.getBoundingClientRect();
						expect(iconRect.top + iconRect.height / 2, 'media type icon vertical center')
							.to.be.closeTo(triggerRect.top + triggerRect.height / 2, 1);
						cy.get('.media-overview-bar__mobile-search').then(($search) => {
							const barRect = $trigger[0]!.closest('.media-overview-bar')!.getBoundingClientRect();
							const searchRect = $search[0]!.getBoundingClientRect();
							expect(triggerRect.left - barRect.left, 'media type icon horizontal margin')
								.to.be.closeTo(4, 1);
							expect(iconRect.left + iconRect.width / 2, 'media type icon horizontal center')
								.to.be.closeTo((barRect.left + searchRect.left) / 2, 1);
						});
					});
					assertTouchTargets('.comparison-state-button .q-btn', 'poster comparison action');
					cy.get('.comparison-state-button .q-btn').should(($buttons) => {
						for (const button of Array.from($buttons)) {
							const ariaLabel = button.getAttribute('aria-label');
							expect(ariaLabel, 'comparison action accessible name').not.to.equal(null);
							expect(ariaLabel, 'comparison action accessible name').not.to.equal('');
						}
					});
					cy.getCy('account-selector-btn').should('have.attr', 'aria-label').and('not.be.empty');
					cy.getCy('notifications-button').should('have.attr', 'aria-label').and('not.be.empty');
					cy.getCy('media-overview-filter-btn').should('have.attr', 'aria-label').and('not.be.empty');
					assertTouchTargets('[data-cy="media-overview-filter-btn"]', 'media overview filter action');
					cy.get('.media-overview-bar__title').then(($title) => {
						const title = $title[0];
						if (!title) throw new Error('Media overview title not found');
						const titleRect = title.getBoundingClientRect();
						cy.get('.media-overview-bar__mobile-search').then(($search) => {
							const search = $search[0];
							if (!search) throw new Error('Mobile search not found');
							const searchRect = search.getBoundingClientRect();
							expect(searchRect.left, 'search starts after title').to.be.gte(titleRect.right - 1);
							expect(searchRect.top, 'search stays on title row').to.be.lt(titleRect.bottom);
						});
					});
					cy.getCy('media-overview-filter-btn').should('be.visible');
					if (viewport.width <= 599) {
						cy.get('.media-poster--overlay').first().should('not.be.visible');
					}
					if (viewport.width === 320) {
						cy.get('.media-poster-card:visible').first().should(($card) => {
							expect($card[0]!.getBoundingClientRect().width, 'small-phone poster width').to.be.gte(140);
						});
						cy.get('.media-poster-quality-bar .hover-expand-chip--compact').each(($quality) => {
							const qualityRect = $quality[0]!.getBoundingClientRect();
							expect(qualityRect.width, 'compact quality width').to.be.gte(32);
							expect(qualityRect.height, 'compact quality height').to.be.gte(32);
							expect(qualityRect.width, 'compact quality remains circular').to.be.closeTo(qualityRect.height, 1);
						});
					}
					if (viewport.width <= 599) {
						cy.get('.media-overview-content').then(($content) => {
							const content = $content[0];
							if (!content) throw new Error('Media overview content not found');
							const contentRect = content.getBoundingClientRect();
							cy.get('.alphabet-navigation-container')
								.should('be.visible')
								.then(($navigation) => {
									const navigation = $navigation[0];
									if (!navigation) throw new Error('Alphabet navigation not found');
									const navigationRect = navigation.getBoundingClientRect();
									const styles = getComputedStyle(navigation);
									expect(styles.position).to.equal('absolute');
									expect(navigationRect.top, 'index reaches content top').to.be.closeTo(contentRect.top, 1);
									expect(navigationRect.bottom, 'index reaches content bottom').to.be.closeTo(contentRect.bottom, 1);
								});
						});
					}
					cy.getCy('media-overview-bar-header-mobile-trigger').click();
					cy.get('.media-overview-bar-header__mobile-menu').should('be.visible');
					cy.getCy('media-overview-bar-header-mobile-trigger').click();

					cy.getCy('media-overview-bar-mobile-menu').click();
					cy.getCy('change-view-mode-btn').filter(':visible').should('be.visible');
					cy.getCy('media-overview-bar-mobile-menu').click();
				} else {
					cy.get('.media-overview-bar__mobile-menu-trigger').should('not.be.visible');
					cy.get('.media-overview-bar__actions').should('be.visible');
					cy.getCy('media-overview-filter-btn').should('be.visible');
					cy.getCy('change-view-mode-btn').should('be.visible');
					cy.get('.media-overview-bar__search input').should('have.css', 'font-size', '20px');
					assertDesktopNavigationBorder();
				}
				assertNoPageOverflow();
			});
		});
	}

	it('restores persistent navigation and compacts the media toolbar after tablet rotation', () => {
		cy.viewport(768, 1024);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 100,
			isLoggedIn: true,
		}).then(({ plexLibraries }) => {
			const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
			if (!library) throw new Error('Movie library not found');
			cy.visit(route(`/movies/${library.id}`));
		});
		waitForPageLoad();
		cy.get('.navigation-drawer').should('not.be.visible');

		cy.viewport(1024, 768);
		cy.get('.navigation-drawer').should('be.visible').then(($drawer) => {
			expect($drawer[0]!.getBoundingClientRect().left, 'persistent drawer starts at viewport edge').to.be.closeTo(0, 1);
		});
		cy.get('.media-overview-bar__actions').should('not.be.visible');
		cy.get('.media-overview-bar__mobile-menu-trigger').should('be.visible');
		cy.get('.media-overview-bar__mobile-search .q-field').should(($search) => {
			expect($search[0]!.getBoundingClientRect().width, 'compact search remains usable').to.be.gte(176);
		});
		assertNoPageOverflow();
	});
	it('remeasures poster rows when their content height and viewport change', () => {
		cy.viewport(1920, 1080);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 100,
			isLoggedIn: true,
		}).then(({ plexLibraries }) => {
			const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
			if (!library) throw new Error('Movie library not found');

			cy.visit(route(`/movies/${library.id}?scrollIndex=22`));
		});
		waitForPageLoad();
		cy.get('.poster-table-item').first().find('.media-poster-card').first().then(($card) => {
			const card = $card[0];
			if (!card) throw new Error('Poster card not found');
			card.style.minHeight = `${card.getBoundingClientRect().height + 80}px`;
		});
		assertPosterRowsDoNotOverlap();

		cy.viewport(320, 568);
		assertPosterRowsDoNotOverlap();
		cy.viewport(1920, 1080);
		assertPosterRowsDoNotOverlap();
	});

	it('keeps desktop alphabet letters individually clickable', () => {
		cy.viewport(1280, 800);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 100,
			isLoggedIn: true,
		}).then(({ plexLibraries }) => {
			const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
			if (!library) throw new Error('Movie library not found');
			cy.visit(route(`/movies/${library.id}?scrollIndex=46`));
		});
		waitForPageLoad();
		cy.get('.media-poster-card:visible').first().then(($card) => {
			const contextMenuEvent = new MouseEvent('contextmenu', { bubbles: true, cancelable: true });
			expect($card[0]!.dispatchEvent(contextMenuEvent), 'desktop context menu remains available').to.equal(true);
		});
		cy.get('#poster-table').should(($table) => {
			const table = $table[0];
			if (!table) throw new Error('Poster table not found');
			expect((table as HTMLElement).scrollTop, 'query starts away from first letter').to.be.greaterThan(50);
		});
		cy.get('.alphabet-navigation .navigation-btn:visible')
			.should('have.length.at.least', 2)
			.first()
			.click();
		cy.get('#poster-table').should(($table) => {
			const table = $table[0];
			if (!table) throw new Error('Poster table not found');
			expect((table as HTMLElement).scrollTop, 'alphabet click scrolls to the selected letter').to.be.lessThan(50);
		});
	});

	it('keeps mobile alphabet letters tappable without triggering drag navigation', () => {
		cy.viewport(375, 812);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 100,
			isLoggedIn: true,
		}).then(({ plexLibraries }) => {
			const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
			if (!library) throw new Error('Movie library not found');
			cy.visit(route(`/movies/${library.id}?scrollIndex=46`));
		});
		waitForPageLoad();
		cy.get('#poster-table').should(($table) => {
			expect(($table[0] as HTMLElement).scrollTop).to.be.greaterThan(50);
		});
		cy.get('.alphabet-navigation .navigation-btn:visible').first().click();
		cy.get('#poster-table').should(($table) => {
			expect(($table[0] as HTMLElement).scrollTop, 'mobile alphabet tap scrolls').to.be.lessThan(50);
		});
	});

	it('overlays media actions at the bottom of a tapped phone poster after restoring a deep scroll position', () => {
		cy.viewport(320, 568);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 100,
			isLoggedIn: true,
		}).then(({ plexLibraries, mediaData }) => {
			const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
			if (!library) throw new Error('Movie library not found');
			const libraryMedia = mediaData.find((item) => item.libraryId === library.id)?.media ?? [];
			libraryMedia.forEach((item) => {
				item.hasThumb = true;
			});
			cy.intercept('GET', '**/api/PlexMedia/thumbnail*', {
				statusCode: 200,
				headers: { 'content-type': 'image/png' },
				body: Cypress.Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64'),
			});
			cy.visit(route(`/movies/${library.id}?scrollIndex=46`));
		});
		waitForPageLoad();
		cy.get('.media-poster-card:visible').first()
			.should('have.attr', 'role', 'button')
			.and('have.attr', 'tabindex', '0');
		cy.get('.media-poster-card:visible').first().should('have.attr', 'aria-label');
		cy.get('.media-poster-card:visible').first()
			.focus()
			.trigger('keydown', { key: 'Enter' });
		cy.getCy('media-poster-menu').should('be.visible');
		cy.get('body').type('{esc}');
		cy.get('#poster-table').should(($table) => {
			expect(($table[0] as HTMLElement).scrollTop).to.be.greaterThan(50);
		});
		cy.get('.media-poster-card:visible').first().as('tappedPoster').find('.media-poster--image').click();
		cy.getCy('media-poster-menu').should('be.visible');
		cy.get('@tappedPoster').then(($card) => {
			const cardRect = $card[0]!.getBoundingClientRect();
			cy.getCy('media-poster-menu').should(($menu) => {
				const menuRect = $menu[0]!.getBoundingClientRect();
				expect(menuRect.top, 'menu starts inside poster').to.be.gte(cardRect.top - 1);
				expect(menuRect.bottom, 'menu aligns with poster bottom').to.be.closeTo(cardRect.bottom, 1);
			});
		});
		cy.getCy('media-poster-menu-details').should(($item) => {
			expect($item[0]!.getBoundingClientRect().height, 'poster details action touch height').to.be.gte(44);
		});
		cy.getCy('media-poster-menu-download').should(($item) => {
			expect($item[0]!.getBoundingClientRect().height, 'poster download action touch height').to.be.gte(44);
		});
	});

	it('keeps comparison options readable with a compact phone action', () => {
		cy.viewport(320, 568);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieCount: 10,
			isLoggedIn: true,
		}).then(({ plexLibraries, mediaData }) => {
			const library = plexLibraries.find((item) => item.type === PlexMediaType.Movie);
			if (!library) throw new Error('Movie library not found');
			const mediaItem = mediaData.find((item) => item.libraryId === library.id)?.media[0];
			if (!mediaItem) throw new Error('Movie media not found');

			mediaItem.comparisonId = 3;
			mediaItem.title = 'A deliberately long comparison title that must not consume the dialog';
			cy.intercept('GET', `**/api/PlexMedia/comparison-details/${mediaItem.id}*`, {
				statusCode: 200,
				body: {
					isSuccess: true,
					statusCode: 200,
					errors: [],
					successes: [],
					value: {
						plexMediaId: mediaItem.id,
						type: mediaItem.type,
						state: 3,
						rows: [{
							title: mediaItem.title,
							plexMediaId: mediaItem.id,
							type: mediaItem.type,
							state: 3,
							remoteQuality: 'FullHD',
							ownedQuality: 'None',
							plexLibraryId: mediaItem.plexLibraryId,
							plexServerId: mediaItem.plexServerId,
							children: [],
						}],
					},
				},
			}).as('comparisonDetails');
			cy.visit(route(`/movies/${library.id}`));
		});

		waitForPageLoad();
		cy.getCy('comparison-chip-Missing').filter(':visible').first().scrollIntoView().click();
		cy.wait('@comparisonDetails');
		cy.getCy('media-comparison-details-dialog').should('be.visible');
		cy.getCy('media-comparison-details-table').within(() => {
			cy.get('.media-comparison-details__row-content')
				.should('have.length.at.least', 1);
			cy.get('.p-treetable-table-container').should(($container) => {
				const container = $container[0] as HTMLElement;
				expect(container.scrollWidth, 'comparison options fit without horizontal scrolling').to.be.lte(container.clientWidth + 1);
			});
		});
		cy.getCy('media-comparison-details-dialog-download-button').should(($button) => {
			const rect = $button[0]!.getBoundingClientRect();
			expect(rect.height, 'download action touch height').to.be.within(44, 48);
			expect(rect.right, 'download action stays inside viewport').to.be.lte(320);
		});
		assertNoPageOverflow();
		cy.get('body').type('{esc}');
		cy.getCy('media-comparison-details-dialog').should('not.exist');
		cy.viewport(1280, 800);
		cy.getCy('comparison-chip-Missing').filter(':visible').first().click();
		cy.wait('@comparisonDetails');
		cy.getCy('media-comparison-details-dialog').should('be.visible');
		cy.get('.media-comparison-details__header')
			.find('[data-cy="media-comparison-details-dialog-download-button"]')
			.should('be.visible');
		cy.get('.dialog-container-actions').should('not.exist');
	});

	it('keeps settings controls and actions usable across mobile breakpoints', () => {
		cy.viewport(320, 568);
		cy.basePageSetup({
			plexAccountCount: 0,
			plexServerCount: 0,
			invalidDefaultFolderPaths: true,
		});
		cy.visit(route('/settings/advanced'));
		waitForPageLoad();
		cy.get('.settings-page').scrollTo('bottom');
		cy.getCy('go-to-setup-button').should('be.visible').then(($button) => {
			const rect = $button[0]!.getBoundingClientRect();
			expect(rect.left, 'action starts inside viewport').to.be.gte(0);
			expect(rect.right, 'action ends inside viewport').to.be.lte(320);
			expect(rect.height, 'action touch height').to.be.gte(44);
		});
		assertNoPageOverflow();

		cy.visit(route('/settings/paths'));
		waitForPageLoad();
		cy.getCy('default-download-folder-edit-button')
			.should('have.attr', 'aria-label')
			.and('not.be.empty');
		cy.getCy('download-folder-add-button')
			.should('have.attr', 'aria-label')
			.and('not.be.empty');
		cy.get('.help-row-default-slot').first().parent().find('button').first()
			.should('have.attr', 'aria-label', 'Help');
		assertNoPageOverflow();

		cy.viewport(768, 1024);
		cy.visit(route('/settings/paths'));
		waitForPageLoad();
		cy.get('.folder-path-tabs').should('be.visible');
		cy.getCy('folder-path-tab-movie-folder').click();
		cy.getCy('folder-path-tab-movie-folder').should('have.attr', 'aria-selected', 'true');
		cy.get('[data-cy$="-row"]').first().should('be.visible');
		assertNoPageOverflow();
	});

	it('keeps logs and library access history usable on a small phone', () => {
		cy.viewport(320, 568);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			isLoggedIn: true,
		});
		cy.intercept('GET', '**/api/Debug/logs*', {
			body: {
				isSuccess: true,
				value: [{
					exception: null,
					level: 'Information',
					message: 'Responsive log entry',
					sequence: 1,
					sourceContext: 'Responsive audit',
					timestamp: '2026-09-19T00:00:00Z',
				}],
			},
		});
		cy.visit(route('/settings/logs'));
		waitForPageLoad();
		cy.get('.live-log-viewer__toolbar .q-toolbar__title').should('contain.text', 'Logs');
		cy.get('.live-log-viewer__pause:visible, .live-log-viewer__toolbar-action:visible')
			.should('have.length', 4)
			.each(($control) => {
				const rect = $control[0]!.getBoundingClientRect();
				expect(rect.width, 'log toolbar control touch width').to.be.gte(44);
				expect(rect.height, 'log toolbar control touch height').to.be.gte(44);
			});
		cy.get('.live-log-viewer__entry-checkbox')
			.should('have.attr', 'aria-label')
			.and('not.be.empty');
		cy.get('.live-log-viewer__entry-checkbox:visible, .live-log-viewer__copy-action:visible').each(($control) => {
			const rect = $control[0]!.getBoundingClientRect();
			expect(rect.width, 'log row control touch width').to.be.gte(44);
			expect(rect.height, 'log row control touch height').to.be.gte(44);
		});
		assertNoPageOverflow();

		cy.intercept('GET', '**/api/PlexLibrary/access-timeline*', {
			body: {
				isSuccess: true,
				value: {
					currentState: [],
					events: [],
				},
			},
		});
		cy.visit(route('/library/access-history'));
		waitForPageLoad();
		cy.get('.library-access-timeline-layout').should(($layout) => {
			const rect = $layout[0]!.getBoundingClientRect();
			expect(rect.left, 'timeline starts inside viewport').to.be.gte(0);
			expect(rect.right, 'timeline ends inside viewport').to.be.lte(320);
		});
		cy.getCy('library-access-timeline-server-filter').should(($filter) => {
			const rect = $filter[0]!.getBoundingClientRect();
			expect(rect.right, 'server filter ends inside viewport').to.be.lte(320);
		});
		cy.getCy('library-access-timeline-zoom-filter').find('button').filter(':visible')
			.should('have.length', 4)
			.each(($button) => {
				const rect = $button[0]!.getBoundingClientRect();
				expect(rect.width, 'timeline zoom touch width').to.be.gte(44);
				expect(rect.height, 'timeline zoom touch height').to.be.gte(44);
			});
		cy.get('.library-access-timeline-scroll-region').should(($region) => {
			expect(getComputedStyle($region[0]!).overflowX, 'timeline can scroll horizontally').to.equal('auto');
		});
		assertNoPageOverflow();
	});

	for (const viewport of viewports.filter(({ width }) => width <= 768)) {
		it(`opens and dismisses mobile drawers at ${viewport.name}`, () => {
			cy.viewport(viewport.width, viewport.height);
			cy.basePageSetup({
				plexAccountCount: 1,
				plexServerCount: 1,
				plexMovieLibraryCount: 1,
				isLoggedIn: true,
			});
			cy.visitEmptyPage();
			waitForPageLoad();

			cy.getCy('navigation-drawer-toggle').click();
			cy.get('.navigation-drawer').should('be.visible');
			cy.get('.navigation-drawer').contains('Downloads').click();
			cy.url().should('include', '/downloads');
			cy.get('.navigation-drawer').should('not.be.visible');
			assertNoPageOverflow();

			cy.getCy('account-selector-btn').click();
			cy.get('[data-cy^="refresh-account-"]').should('have.length.greaterThan', 0)
				.each(($button) => {
					expect($button.attr('aria-label'), 'refresh action accessible name').not.to.equal('');
					const rect = $button[0]!.getBoundingClientRect();
					expect(rect.width, 'refresh action touch width').to.be.gte(44);
					expect(rect.height, 'refresh action touch height').to.be.gte(44);
				});
			cy.get('.account-selector-details').each(($details) => {
				const detailsRect = $details[0]!.getBoundingClientRect();
				const actionRect = $details.next()[0]!.getBoundingClientRect();
				expect(detailsRect.right, 'account details stop before refresh action').to.be.lte(actionRect.left);
			});
			cy.get('body').type('{esc}');

			cy.getCy('notifications-button').click();
			cy.get('.notification-drawer').should(($drawer) => {
				const drawerRect = $drawer[0]!.getBoundingClientRect();
				const appBarRect = Cypress.$('.app-bar')[0]!.getBoundingClientRect();
				expect(drawerRect.top, 'notification drawer starts below app bar').to.be.closeTo(appBarRect.bottom, 1);
				expect(drawerRect.right, 'notification drawer ends at viewport edge').to.be.closeTo(viewport.width, 1);
				expect(drawerRect.left, 'notification drawer starts inside viewport').to.be.gte(0);
				expect(drawerRect.bottom, 'notification drawer ends at viewport bottom').to.be.closeTo(viewport.height, 1);
			});
			cy.get('body').type('{esc}');
			cy.get('.notification-drawer').should('not.be.visible');
		});
	}

	it('collapses and re-expands nested downloads on mobile', () => {
		cy.viewport(375, 812);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieDownloadTask: 2,
		});
		cy.visit(route('/downloads'));
		cy.getCy('download-mobile-list').should('be.visible');
		cy.get('[data-cy^="download-node-toggle-"]').first().as('downloadNodeToggle').should('have.attr', 'aria-expanded', 'true');
		cy.getCy('download-mobile-list').find('.download-card').its('length').then((expandedCount) => {
			cy.get('@downloadNodeToggle').click().should('have.attr', 'aria-expanded', 'false');
			cy.getCy('download-mobile-list').find('.download-card').should(($cards) => {
				expect($cards.length, 'collapsed card count').to.be.lessThan(expandedCount);
			});
			cy.get('@downloadNodeToggle').click().should('have.attr', 'aria-expanded', 'true');
			cy.getCy('download-mobile-list').find('.download-card').should('have.length', expandedCount);
		});
	});

	it('keeps a parent partially selected after deselecting nested downloads', () => {
		cy.viewport(375, 812);
		cy.basePageSetup({
			plexAccountCount: 1,
			plexServerCount: 1,
			plexMovieLibraryCount: 1,
			movieDownloadTask: 2,
		});
		cy.visit(route('/downloads'));
		cy.getCy('download-mobile-list').should('be.visible');
		cy.get('[data-cy^="download-node-toggle-"]').first()
			.closest('.download-card')
			.as('selectedParent')
			.find('[role="checkbox"]')
			.as('parentSelection')
			.click();
		cy.get('@selectedParent')
			.next('.download-card--child')
			.find('[role="checkbox"]')
			.as('firstChildSelection')
			.click();
		cy.get('@firstChildSelection').should('have.attr', 'aria-checked', 'false');
		cy.get('@parentSelection').should('have.attr', 'aria-checked', 'mixed');
		cy.get('.download-card:not(.download-card--child)').eq(1).find('[role="checkbox"]').click();
		cy.get('@firstChildSelection').should('have.attr', 'aria-checked', 'false');
		cy.get('@parentSelection').should('have.attr', 'aria-checked', 'mixed');
	});
	for (const viewport of viewports.filter(({ width }) => width <= 768)) {
		it(`keeps downloads and their detail dialog usable at ${viewport.name}`, () => {
			cy.viewport(viewport.width, viewport.height);
			cy.basePageSetup({
				plexAccountCount: 1,
				plexServerCount: 1,
				plexMovieLibraryCount: 1,
				movieDownloadTask: 2,
				setDownloadDetails: true,
			});
			cy.visit(route('/downloads'));
			cy.getCy('toggle-download-table-button').should('be.visible');
			cy.getCy('toggle-download-table-button')
				.should('have.attr', 'aria-label')
				.and('not.be.empty');
			cy.getCy('download-mobile-list').should(viewport.width <= 1023 ? 'be.visible' : 'not.be.visible');
			cy.getCy('downloads-scroll').find('.q-scrollarea__container').first().should(($scroll) => {
				const rect = $scroll[0]!.getBoundingClientRect();
				expect(rect.bottom, 'downloads use available viewport height').to.be.closeTo(viewport.height, 2);
			});
			cy.get('.download-card:visible').first().should(($card) => {
				const rect = $card[0]!.getBoundingClientRect();
				expect(rect.left, 'download card starts inside viewport').to.be.gte(0);
				expect(rect.right, 'download card ends inside viewport').to.be.lte(viewport.width);
			});
			cy.get('.download-card:visible [data-cy^="column-actions-"]').first()
				.should('have.attr', 'aria-label')
				.and('not.be.empty');
			cy.get('.download-card--child').first().scrollIntoView().should('be.visible').then(($child) => {
				const childRect = $child[0]!.getBoundingClientRect();
				expect(childRect.left, 'child card keeps full mobile width').to.be.lessThan(24);
				expect(childRect.right, 'child card keeps full mobile width').to.be.greaterThan(viewport.width - 24);
			});
			assertNoPageOverflow();
			cy.getPageData().then(({ detailDownloadTasks }) => {
				const task = detailDownloadTasks[0];
				if (!task) throw new Error('Download detail task not found');
				cy.getCy('download-mobile-list')
					.find(`[data-cy="column-title-${task.id}"]`)
					.scrollIntoView()
					.should('be.visible');
				cy.getCy(`column-status-${task.id}`).filter(':visible').should('be.visible');
				cy.getCy(`column-percentage-${task.id}`).filter(':visible').should('be.visible');
				cy.intercept('GET', `/api/Download/logs/${task.id}*`, { body: { isSuccess: true, value: [] } });
				cy.getCy(`column-actions-details-${task.id}`).filter(':visible').click();
				cy.getCy('download-details-dialog-title').should('be.visible');
				cy.get('.q-dialog').should('have.attr', 'aria-label', task.fullTitle);
				cy.getCy('dialog-close-button').should('be.visible');
				cy.getCy('download-details-dialog-download-path').parent().should(($row) => {
					const cells = [...$row[0]!.querySelectorAll('td')];
					expect(cells, 'download path cells').to.have.length(2);
					const labelRect = cells[0]!.getBoundingClientRect();
					const valueRect = cells[1]!.getBoundingClientRect();
					expect(labelRect.width, 'download path label uses the row width').to.be.closeTo(valueRect.width, 1);
					expect(valueRect.top, 'download path value is stacked below its label').to.be.gte(labelRect.bottom);
				});
				assertNoPageOverflow();
			});
		});
	}
});
